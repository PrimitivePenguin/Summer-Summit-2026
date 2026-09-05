using UnityEngine;

// Physics-driven steering for enemies. Knows HOW to move, never WHY.
//
// Two-phase pattern, mirroring Player.cs:
//   Phase 1 (Update, driven by EnemyController/behaviors) — command methods set desiredVelocity
//   Phase 2 (FixedUpdate)                                 — desiredVelocity applied to the Rigidbody2D
//
// If no command is issued during a physics step, velocity decays to zero rather than
// coasting on the last command forever.

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Speeds")]
    [SerializeField] private float moveSpeed = 3f;      // chase speed
    [SerializeField] private float patrolSpeed = 1.5f;  // patrol speed
    [SerializeField] private float fleeSpeed = 4f;      // retreat speed

    [Header("Steering")]
    [SerializeField] private float acceleration = 20f;  // velocity ramp (0 = instant)
    [SerializeField] private float arriveRadius = 0.3f; // "close enough" threshold
    [SerializeField] private float whiskerDistance = 0.8f;   // lookahead for wall-slide
    [SerializeField] private LayerMask obstacleMask;         // set to Collision

    [Header("Patrol")]
    [SerializeField] private PatrolRoute patrolRoute;
    [SerializeField] private float waypointGiveUpTime = 4f;  // abandon unreachable waypoints

    [Header("Search")]
    [SerializeField] private AimController aimController;
    [SerializeField] private float searchSweepAngle = 50f;
    [SerializeField] private float searchSweepSpeed = 2f;    // oscillation rate
    [SerializeField] private float searchDuration = 5f;      // seconds before giving up

    [Header("Pathfinding")]
    [SerializeField] private EnemyPathfinding pathfinding;
    [SerializeField] private float repathInterval = 0.5f;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;              // leave null if unused

    // ── Runtime state ─────────────────────────────────────────────────────────

    private Rigidbody2D rb;

    private Vector2 desiredVelocity;    // set by command methods, applied in FixedUpdate
    private bool commandedThisFrame;    // did anyone issue a command since the last physics step?

    private float repathTimer;

    private int patrolIndex = 0;
    private Vector3 currentPatrolTarget;
    private float waypointTimer;        // counts down the give-up clock for one waypoint

    private float searchBaseAngle;
    private float searchTimer;          // drives the sweep oscillation
    private float searchDurationTime;   // counts down the whole search
    private bool isSearching;

    // ── Unity ─────────────────────────────────────────────────────────────────

    // INPUT:  none
    // OUTPUT: caches Rigidbody2D and optional Animator
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (obstacleMask == 0) obstacleMask = LayerMask.GetMask("Collision");
    }

    // INPUT:  patrolRoute, pathfinding
    // OUTPUT: resolves component refs, seeds the first patrol waypoint,
    //         staggers the repath timer
    // USE:    the random repath offset stops every enemy in the level from running
    //         A* on the same frame
    private void Start()
    {
        if (pathfinding == null) pathfinding = GetComponent<EnemyPathfinding>();
        if (aimController == null) aimController = GetComponent<AimController>();

        repathTimer = Random.Range(0f, repathInterval);

        if (patrolRoute != null && patrolRoute.WaypointCount > 0)
            SetPatrolTarget(0);
    }

    // INPUT:  speed values (future EnemyData migration)
    // OUTPUT: overwrites the inspector speeds
    // USE:    called by EnemyController.Start once EnemyData exists
    public void Initialize(float moveSpeed, float patrolSpeed, float fleeSpeed)
    {
        this.moveSpeed = moveSpeed;
        this.patrolSpeed = patrolSpeed;
        this.fleeSpeed = fleeSpeed;
    }

    // ── Movement commands (called from Update by behaviors) ───────────────────

    // INPUT:  world position
    // OUTPUT: sets desiredVelocity straight at the target at moveSpeed — NO pathfinding
    // USE:    short hops only (returning to an anchor a metre away). Prefer
    //         MoveTowardSmart for anything that could have a wall in between.
    public void MoveToward(Vector2 target) => SetDesiredToward(target, moveSpeed);

    // INPUT:  world position, speed
    // OUTPUT: sets desiredVelocity toward the next A* waypoint; repaths on a timer
    // USE:    the default for chase, investigate, and defender repositioning
    // NOTE:   if pathfinding is missing or A* failed, SetDesiredToward's whisker check
    //         degrades this to a wall-sliding straight line rather than a face-plant
    public void MoveTowardSmart(Vector2 target, float speed)
    {
        if (pathfinding == null) { SetDesiredToward(target, speed); return; }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            pathfinding.ComputePath(target);
            repathTimer = repathInterval;
        }

        Vector2 step = pathfinding.GetNextWayPoint(target);
        SetDesiredToward(step, speed);
    }

    // INPUT:  world position
    // OUTPUT: as above, at moveSpeed
    // USE:    convenience overload so chase calls need not pass a speed
    public void MoveTowardSmart(Vector2 target) => MoveTowardSmart(target, moveSpeed);

    // INPUT:  world position of a threat
    // OUTPUT: sets desiredVelocity directly away from it at fleeSpeed
    // USE:    Skirmisher retreat when the player closes inside preferredMinDist
    public void MoveAwayFrom(Vector2 threat)
    {
        Vector2 dir = ((Vector2)transform.position - threat).normalized;
        SetDesired(ApplyWhisker(dir) * fleeSpeed);
    }

    // INPUT:  world position to orbit, rotation direction
    // OUTPUT: sets desiredVelocity perpendicular to the target at moveSpeed
    // USE:    circle-strafing in the Skirmisher sweet spot and Defender perimeter
    public void Strafe(Vector2 target, bool clockwise = true)
    {
        Vector2 toTarget = ((Vector2)transform.position - target).normalized;
        Vector2 perp = clockwise
            ? new Vector2(-toTarget.y, toTarget.x)
            : new Vector2(toTarget.y, -toTarget.x);
        SetDesired(ApplyWhisker(perp) * moveSpeed);
    }

    // INPUT:  direction (need not be normalized), speed
    // OUTPUT: sets desiredVelocity along that direction
    // USE:    generic escape hatch for one-off behaviors
    public void Move(Vector2 direction, float speed) => SetDesired(direction.normalized * speed);

    // INPUT:  none
    // OUTPUT: sets desiredVelocity to zero
    // USE:    Idle, and any behavior that wants to hold position while aiming
    public void Stop() => SetDesired(Vector2.zero);

    // INPUT:  target position, distance threshold
    // OUTPUT: true if within range
    // USE:    arrival checks in behaviors and EnemyController transitions
    public bool IsInRange(Vector2 target, float range)
        => Vector2.Distance(transform.position, target) < range;

    // ── Patrol ────────────────────────────────────────────────────────────────

    // INPUT:  patrolRoute, currentPatrolTarget, patrolIndex, arriveRadius, waypointTimer
    // OUTPUT: steers along the A* path; advances on arrival OR on give-up
    // USE:    the Idle behavior of Chaser and Skirmisher
    // NOTE:   repaths on the same timer MoveTowardSmart uses. Without it a Partial path
    //         (waypoint outside the grid window) empties at the window edge and the
    //         enemy straight-lines the rest with no correction.
    public void Patrol()
    {
        if (patrolRoute == null || patrolRoute.WaypointCount == 0) { Stop(); return; }

        waypointTimer -= Time.deltaTime;

        bool arrived = IsInRange(currentPatrolTarget, arriveRadius);
        bool givenUp = pathfinding != null && pathfinding.PathFailed() && waypointTimer <= 0f;

        if (arrived || givenUp)
        {
            if (givenUp)
                Debug.LogWarning($"[EnemyMovement] {name}: waypoint {patrolIndex} unreachable, skipping");
            AdvanceWaypoint();
            return;
        }

        if (pathfinding == null) { SetDesiredToward(currentPatrolTarget, patrolSpeed); return; }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            pathfinding.ComputePath(currentPatrolTarget);
            repathTimer = repathInterval;
        }

        SetDesiredToward(pathfinding.GetNextWayPoint(currentPatrolTarget), patrolSpeed);
    }

    // INPUT:  all waypoint centres on the route
    // OUTPUT: snaps patrolIndex to the nearest waypoint and repaths to it
    // USE:    called on entering Patrol so the enemy rejoins the route where it stands
    //         rather than walking back to whichever waypoint it left
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
    // OUTPUT: steps patrolIndex forward (wrapping) and repaths
    private void AdvanceWaypoint()
    {

        patrolIndex = (patrolIndex + 1) % patrolRoute.WaypointCount; 
        SetPatrolTarget(patrolIndex);
    }

    // INPUT:  waypoint index
    // OUTPUT: samples a point inside that waypoint's radius, resets the give-up clock,
    //         and computes a fresh path
    // USE:    single funnel for every patrol target change, so the timer can never be
    //         left stale
    private void SetPatrolTarget(int index)
    { // to patrolroute calc +1 (in patrolroute.cs) because the last length seems to end at n-1
        patrolIndex = index -1;
        currentPatrolTarget = patrolRoute.SampleWaypoint(index);
        waypointTimer = waypointGiveUpTime;

        Vector3 waypointPos = patrolRoute.GetCenter(index);
        Debug.Log($"[{name}] SetPatrolTarget index={index} " +
              $"WaypointCount={patrolRoute.WaypointCount} " +
              $"GetCenter={waypointPos} " +
              $"SampleWaypoint={currentPatrolTarget}");
        if (pathfinding != null) pathfinding.ComputePath(currentPatrolTarget);
    }

    // ── Search (sweep in place) ───────────────────────────────────────────────

    // INPUT:  none
    // OUTPUT: true while actively sweeping
    // USE:    EnemyController's Search → Patrol transition condition
    public bool IsSearching() => isSearching;

    // INPUT:  current facing angle from AimController
    // OUTPUT: records the sweep centre, resets both timers, sets isSearching, stops moving
    // USE:    called EXACTLY ONCE on entering the Search state. Calling Search() without
    //         this leaves searchDurationTime stale from the last run, which made the
    //         enemy freeze at the last known position instead of sweeping.
    public void StartSearching()
    {
        if (aimController == null) aimController = GetComponent<AimController>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();

        searchBaseAngle = aimController != null
            ? aimController.GetFacingAngle()
            : transform.eulerAngles.z;

        searchTimer = 0f;
        searchDurationTime = searchDuration;
        isSearching = true;
        Stop();
    }

    // INPUT:  searchBaseAngle, searchTimer, searchSweepAngle, searchSweepSpeed
    // OUTPUT: oscillates facing via aimController.AimAtAngle; returns false when the
    //         search duration expires (which also clears isSearching)
    // USE:    called every frame while in the Search state
    public bool Search()
    {
        Stop();   // body holds position; only the head moves

        if (aimController == null) { isSearching = false; return false; }

        searchDurationTime -= Time.deltaTime;
        if (searchDurationTime <= 0f) { isSearching = false; return false; }

        searchTimer += Time.deltaTime * searchSweepSpeed;

        float offset = Mathf.Sin(searchTimer) * searchSweepAngle;
        aimController.AimAtAngle(searchBaseAngle + offset);
        return true;
    }

    // INPUT:  none
    // OUTPUT: clears isSearching
    // USE:    EnemyController exit hook, so a re-entry always calls StartSearching again
    public void StopSearching() => isSearching = false;

    // ── Physics ───────────────────────────────────────────────────────────────

    // INPUT:  desiredVelocity, commandedThisFrame, acceleration
    // OUTPUT: writes rb.linearVelocity, updates the animator, clears the command flag
    private void FixedUpdate()
    {
        Vector2 target = commandedThisFrame ? desiredVelocity : Vector2.zero;

        if (acceleration <= 0f)
            rb.linearVelocity = target;
        else
            rb.linearVelocity = Vector2.MoveTowards(
                rb.linearVelocity, target, acceleration * Time.fixedDeltaTime);

        UpdateAnimator(rb.linearVelocity);
        commandedThisFrame = false;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    // INPUT:  world target, speed
    // OUTPUT: sets desiredVelocity toward the target, deflected along any wall ahead
    // USE:    every "go to a point" command funnels through here
    private void SetDesiredToward(Vector2 target, float speed)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        SetDesired(ApplyWhisker(dir) * speed);
    }

    // INPUT:  intended unit direction
    // OUTPUT: the same direction, or one projected along a wall surface if one is
    //         within whiskerDistance
    // USE:    keeps the straight-line fallback from stalling face-first against
    //         geometry when A* fails
    // NOTE:   set Physics2D.queriesStartInColliders = false in Project Settings, or
    //         this raycast hits the enemy's own collider every frame
    private Vector2 ApplyWhisker(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return dir;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, whiskerDistance, obstacleMask);
        if (hit.collider == null) return dir;

        Vector2 slide = Vector2.Perpendicular(hit.normal);
        if (Vector2.Dot(slide, dir) < 0f) slide = -slide;
        return slide.normalized;
    }

    // INPUT:  velocity vector
    // OUTPUT: stores it and flags that a command arrived this frame
    private void SetDesired(Vector2 velocity)
    {
        desiredVelocity = velocity;
        commandedThisFrame = true;
    }

    // INPUT:  actual rigidbody velocity
    // OUTPUT: drives animator parameters; mirrors Player.cs naming
    // USE:    LastInputX/Y preserve facing when the enemy stops
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
    // OUTPUT: Scene-view only
    // USE:    shows which waypoint is the current target and draws a line to it
    private void OnDrawGizmosSelected()
    {
        if (patrolRoute == null || patrolRoute.WaypointCount == 0) return;

        Vector3 target = currentPatrolTarget;
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, target);
        Gizmos.DrawSphere(target, 0.2f);

        // Draw the waypoint index as a label (Scene view only, selected object only)
        UnityEditor.Handles.Label(target + Vector3.up * 0.5f, $"WP {patrolIndex}");
    }
}