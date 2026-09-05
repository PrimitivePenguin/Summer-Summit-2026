using UnityEngine;

// Physics-driven steering for enemies. Knows HOW to move, never WHY.
//
// Two-phase pattern:
//   Phase 1 (Update, driven by EnemyController/behaviors) — command methods set desiredVelocity
//   Phase 2 (FixedUpdate)                                 — desiredVelocity applied to the Rigidbody2D
//
// FIXES IN THIS VERSION:
//   Awake              — enforces gravityScale 0 + freezeRotation. Every enemy prefab shipped as a
//                        Dynamic body with gravity 1 and free rotation: gravity fought FixedUpdate
//                        (constant downward drift = "offset"), and any brush against a collider
//                        left angular velocity nobody cancelled (= "spin").
//   Patrol             — now FACES the direction it walks (nothing called AimAt while patrolling,
//                        so the vision cone pointed wherever physics last left it). Give-up is
//                        progress-based instead of PathFailed-based: best-effort A* almost never
//                        reports Failed, so unreachable waypoints were never skipped.
//   SetPatrolTarget    — debug log that called GetCenter(N) and threw removed.
//   OnDrawGizmosSelected — UnityEditor.Handles wrapped in #if UNITY_EDITOR (was a build breaker).

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Speeds")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float fleeSpeed = 4f;

    [Header("Steering")]
    [SerializeField] private float acceleration = 20f;       // velocity ramp (0 = instant)
    [SerializeField] private float arriveRadius = 0.3f;
    [SerializeField] private float whiskerDistance = 0.8f;
    [SerializeField] private LayerMask obstacleMask;         // set to Collision

    [Header("Physics Safety")]
    [Tooltip("Force gravityScale=0 and freezeRotation=true on Awake. Top-down enemies must never spin or fall.")]
    [SerializeField] private bool enforceTopDownPhysics = true;

    [Header("Patrol")]
    [SerializeField] private PatrolRoute patrolRoute;
    [SerializeField] private float waypointGiveUpTime = 4f;  // seconds WITHOUT PROGRESS before skipping
    [SerializeField] private float progressEpsilon = 0.05f;  // how much closer counts as progress
    [SerializeField] private bool facePatrolDirection = true;

    [Header("Search")]
    [SerializeField] private AimController aimController;
    [SerializeField] private float searchSweepAngle = 50f;
    [SerializeField] private float searchSweepSpeed = 2f;
    [SerializeField] private float searchDuration = 5f;

    [Header("Pathfinding")]
    [SerializeField] private EnemyPathfinding pathfinding;
    [SerializeField] private float repathInterval = 0.5f;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private Rigidbody2D rb;
    private Vector2 desiredVelocity;
    private bool commandedThisFrame;
    private float repathTimer;

    private int patrolIndex = 0;
    private Vector3 currentPatrolTarget;
    private float waypointTimer;
    private float bestDistToWaypoint;   // closest we have been to the current waypoint

    private float searchBaseAngle;
    private float searchTimer;
    private float searchDurationTime;
    private bool isSearching;

    // ── Unity ─────────────────────────────────────────────────────────────────

    // INPUT:  none
    // OUTPUT: caches Rigidbody2D/Animator, applies top-down physics safety
    // USE:    REPLACES old Awake — adds the gravity/rotation enforcement
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (obstacleMask == 0) obstacleMask = LayerMask.GetMask("Collision");

        if (enforceTopDownPhysics)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.angularVelocity = 0f;
        }
    }

    // INPUT:  patrolRoute, pathfinding
    // OUTPUT: resolves refs, seeds first waypoint, staggers repath timer
    // USE:    runs once; random stagger stops all enemies repathing on the same frame
    private void Start()
    {
        if (pathfinding == null) pathfinding = GetComponent<EnemyPathfinding>();
        if (aimController == null) aimController = GetComponent<AimController>();

        repathTimer = Random.Range(0f, repathInterval);

        if (patrolRoute != null && patrolRoute.WaypointCount > 0)
            SetPatrolTarget(0);
    }

    // INPUT:  speed values
    // OUTPUT: overwrites inspector speeds
    // USE:    future EnemyData migration
    public void Initialize(float moveSpeed, float patrolSpeed, float fleeSpeed)
    {
        this.moveSpeed = moveSpeed;
        this.patrolSpeed = patrolSpeed;
        this.fleeSpeed = fleeSpeed;
    }

    // ── Movement commands ─────────────────────────────────────────────────────

    // INPUT:  world position
    // OUTPUT: desiredVelocity straight at target at moveSpeed — no pathfinding
    // USE:    short hops only
    public void MoveToward(Vector2 target) => SetDesiredToward(target, moveSpeed);

    // INPUT:  world position, speed
    // OUTPUT: desiredVelocity toward next A* node; repaths on a timer
    // USE:    chase, investigate, defender repositioning
    public void MoveTowardSmart(Vector2 target, float speed)
    {
        if (pathfinding == null) { SetDesiredToward(target, speed); return; }
        TickRepath(target);
        SetDesiredToward(pathfinding.GetNextWayPoint(target), speed);
    }

    // INPUT:  world position
    // OUTPUT: as above at moveSpeed
    // USE:    convenience overload
    public void MoveTowardSmart(Vector2 target) => MoveTowardSmart(target, moveSpeed);

    // INPUT:  threat position
    // OUTPUT: desiredVelocity directly away at fleeSpeed
    // USE:    Skirmisher retreat
    public void MoveAwayFrom(Vector2 threat)
    {
        Vector2 dir = ((Vector2)transform.position - threat).normalized;
        SetDesired(ApplyWhisker(dir) * fleeSpeed);
    }

    // INPUT:  orbit centre, direction
    // OUTPUT: desiredVelocity perpendicular to the centre at moveSpeed
    // USE:    circle-strafe
    public void Strafe(Vector2 target, bool clockwise = true)
    {
        Vector2 toTarget = ((Vector2)transform.position - target).normalized;
        Vector2 perp = clockwise ? new Vector2(-toTarget.y, toTarget.x) : new Vector2(toTarget.y, -toTarget.x);
        SetDesired(ApplyWhisker(perp) * moveSpeed);
    }

    // INPUT:  direction, speed
    // OUTPUT: desiredVelocity along direction
    // USE:    generic escape hatch
    public void Move(Vector2 direction, float speed) => SetDesired(direction.normalized * speed);

    // INPUT:  none
    // OUTPUT: desiredVelocity = zero
    // USE:    Idle / hold position
    public void Stop() => SetDesired(Vector2.zero);

    // INPUT:  target, range
    // OUTPUT: true if within range
    // USE:    arrival checks
    public bool IsInRange(Vector2 target, float range) => Vector2.Distance(transform.position, target) < range;

    // INPUT:  world position to look at
    // OUTPUT: rotates via AimController (turn-rate limited)
    // USE:    keep the vision cone pointed where the body is going
    public void FaceToward(Vector2 target)
    {
        if (aimController != null) aimController.AimAt(target);
    }

    // ── Patrol ────────────────────────────────────────────────────────────────

    // INPUT:  patrolRoute, currentPatrolTarget, waypointTimer, bestDistToWaypoint
    // OUTPUT: steers along A* path facing its travel direction; advances on arrival
    //         or after waypointGiveUpTime seconds with no progress
    // USE:    Idle behavior of Chaser/Skirmisher. REPLACES old Patrol()
    public void Patrol()
    {
        if (patrolRoute == null || patrolRoute.WaypointCount == 0) { Stop(); return; }

        float dist = Vector2.Distance(transform.position, currentPatrolTarget);

        // Progress-based give-up: reset the clock every time we get measurably closer.
        if (dist < bestDistToWaypoint - progressEpsilon)
        {
            bestDistToWaypoint = dist;
            waypointTimer = waypointGiveUpTime;
        }
        else
        {
            waypointTimer -= Time.deltaTime;
        }

        bool arrived = dist < arriveRadius;
        bool givenUp = waypointTimer <= 0f;

        if (arrived || givenUp)
        {
            if (givenUp)
                Debug.LogWarning($"[EnemyMovement] {name}: no progress toward waypoint {patrolIndex} for {waypointGiveUpTime}s, skipping");
            AdvanceWaypoint();
            return;
        }

        Vector2 steer = currentPatrolTarget;
        if (pathfinding != null)
        {
            TickRepath(currentPatrolTarget);
            steer = pathfinding.GetNextWayPoint(currentPatrolTarget);
        }

        SetDesiredToward(steer, patrolSpeed);
        if (facePatrolDirection) FaceToward(steer);
    }

    // INPUT:  all waypoint centres
    // OUTPUT: patrolIndex = nearest waypoint, repaths to it
    // USE:    on entering Patrol — rejoin the route where you stand
    public void ResumePatrolFromNearest()
    {
        if (patrolRoute == null || patrolRoute.WaypointCount == 0) return;

        float best = float.MaxValue;
        int bestIndex = 0;
        for (int i = 0; i < patrolRoute.WaypointCount; i++)
        {
            float d = Vector2.SqrMagnitude((Vector2)patrolRoute.GetCenter(i) - (Vector2)transform.position);
            if (d < best) { best = d; bestIndex = i; }
        }
        SetPatrolTarget(bestIndex);
    }

    // INPUT:  none
    // OUTPUT: patrolIndex + 1 (wrapping), repaths
    // USE:    Patrol() on arrival/give-up
    private void AdvanceWaypoint()
    {
        patrolIndex = (patrolIndex + 1) % patrolRoute.WaypointCount;
        SetPatrolTarget(patrolIndex);
    }

    // INPUT:  waypoint index
    // OUTPUT: samples the target point, resets give-up + progress tracking, repaths
    // USE:    single funnel for every patrol target change. REPLACES old SetPatrolTarget
    //         (removed the Debug.Log that called GetCenter(N) and threw)
    private void SetPatrolTarget(int index)
    {
        if (!patrolRoute.IsValidIndex(index)) index = 0;

        patrolIndex = index;
        currentPatrolTarget = patrolRoute.SampleWaypoint(index);
        waypointTimer = waypointGiveUpTime;
        bestDistToWaypoint = Vector2.Distance(transform.position, currentPatrolTarget);

        if (pathfinding != null) pathfinding.ComputePath(currentPatrolTarget);
    }

    // ── Search (sweep in place) ───────────────────────────────────────────────

    // INPUT:  none
    // OUTPUT: true while sweeping
    // USE:    EnemyController Search → Patrol condition
    public bool IsSearching() => isSearching;

    // INPUT:  current facing angle
    // OUTPUT: records sweep centre, resets timers, stops
    // USE:    called ONCE on entering Search
    public void StartSearching()
    {
        if (aimController == null) aimController = GetComponent<AimController>();

        searchBaseAngle = aimController != null ? aimController.GetFacingAngle() : transform.eulerAngles.z;
        searchTimer = 0f;
        searchDurationTime = searchDuration;
        isSearching = true;
        Stop();
    }

    // INPUT:  searchBaseAngle, sweep settings
    // OUTPUT: oscillates facing; false when the search expires
    // USE:    every frame in Search state
    public bool Search()
    {
        Stop();
        if (aimController == null) { isSearching = false; return false; }

        searchDurationTime -= Time.deltaTime;
        if (searchDurationTime <= 0f) { isSearching = false; return false; }

        searchTimer += Time.deltaTime * searchSweepSpeed;
        aimController.AimAtAngle(searchBaseAngle + Mathf.Sin(searchTimer) * searchSweepAngle);
        return true;
    }

    // INPUT:  none
    // OUTPUT: clears isSearching
    // USE:    Search exit hook
    public void StopSearching() => isSearching = false;

    // ── Physics ───────────────────────────────────────────────────────────────

    // INPUT:  desiredVelocity, commandedThisFrame, acceleration
    // OUTPUT: writes rb.linearVelocity, updates animator
    // USE:    Unity physics step
    private void FixedUpdate()
    {
        Vector2 target = commandedThisFrame ? desiredVelocity : Vector2.zero;

        rb.linearVelocity = acceleration <= 0f
            ? target
            : Vector2.MoveTowards(rb.linearVelocity, target, acceleration * Time.fixedDeltaTime);

        UpdateAnimator(rb.linearVelocity);
        commandedThisFrame = false;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    // INPUT:  target
    // OUTPUT: recomputes the A* path when repathTimer expires
    // USE:    shared by Patrol and MoveTowardSmart (was duplicated)
    private void TickRepath(Vector2 target)
    {
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            pathfinding.ComputePath(target);
            repathTimer = repathInterval;
        }
    }

    // INPUT:  target, speed
    // OUTPUT: desiredVelocity toward target, wall-deflected
    // USE:    every "go to a point" command
    private void SetDesiredToward(Vector2 target, float speed)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        SetDesired(ApplyWhisker(dir) * speed);
    }

    // INPUT:  unit direction
    // OUTPUT: same direction, or slid along a wall within whiskerDistance
    // USE:    keeps straight-line fallback from face-planting
    private Vector2 ApplyWhisker(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return dir;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, whiskerDistance, obstacleMask);
        if (hit.collider == null) return dir;

        Vector2 slide = Vector2.Perpendicular(hit.normal);
        if (Vector2.Dot(slide, dir) < 0f) slide = -slide;
        return slide.normalized;
    }

    // INPUT:  velocity
    // OUTPUT: stores it, flags command
    // USE:    all command methods
    private void SetDesired(Vector2 velocity)
    {
        desiredVelocity = velocity;
        commandedThisFrame = true;
    }

    // INPUT:  rigidbody velocity
    // OUTPUT: animator params (mirrors Player.cs)
    // USE:    optional
    private void UpdateAnimator(Vector2 velocity)
    {
        if (animator == null) return;

        bool walking = velocity.sqrMagnitude > 0.01f;
        animator.SetBool("isWalking", walking);
        animator.SetFloat("InputX", velocity.x);
        animator.SetFloat("InputY", velocity.y);
        if (walking)
        {
            animator.SetFloat("LastInputX", velocity.x);
            animator.SetFloat("LastInputY", velocity.y);
        }
    }

    // INPUT:  none
    // OUTPUT: Scene view only
    // USE:    shows current waypoint target. REPLACES old gizmo (Handles now editor-guarded)
    private void OnDrawGizmosSelected()
    {
        if (patrolRoute == null || patrolRoute.WaypointCount == 0) return;

        Vector3 target = currentPatrolTarget;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, target);
        Gizmos.DrawSphere(target, 0.2f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(target + Vector3.up * 0.5f, $"WP {patrolIndex}");
#endif
    }
}