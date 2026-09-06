using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Speeds")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float fleeSpeed = 4f;

    [Header("Steering")]
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float arriveRadius = 0.5f; // Must stay larger than corner threshold (0.35f)

    [Header("Patrol")]
    [SerializeField] private PatrolRoute patrolRoute;

    [Header("Search Settings")]
    [SerializeField] private AimController aimController;
    [SerializeField] private float searchSweepAngle = 50f;
    [SerializeField] private float searchSweepSpeed = 2f;

    [Header("Pathfinding")]
    [SerializeField] private EnemyPathfinding pathfinding;
    [SerializeField] private float repathInterval = 0.35f;
    private float repathTimer;

    private float searchBaseAngle;
    private bool isSearching;
    private float searchTimer;

    private PatrolRoute route; 
    private Vector3 currentPatrolTarget;
    private int patrolIndex = 0;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;

    private Rigidbody2D rb;
    private Vector2 desiredVelocity;
    private bool commandedThisFrame;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.angularVelocity = 0f;
    }

    private void Start()
    {
        if (pathfinding == null) pathfinding = GetComponent<EnemyPathfinding>();
        if (aimController == null) aimController = GetComponent<AimController>();

        route = patrolRoute;

        if (route != null && route.WaypointCount > 0)
        {
            patrolIndex = 0;
            currentPatrolTarget = route.SampleWaypoint(patrolIndex);
            if (pathfinding != null) pathfinding.ComputePath(currentPatrolTarget);
        }
    }

    public void Initialize(float moveSpeed, float patrolSpeed, float fleeSpeed)
    {
        this.moveSpeed = moveSpeed;
        this.patrolSpeed = patrolSpeed;
        this.fleeSpeed = fleeSpeed;
    }

    public bool IsSearching() => isSearching;

    public void StartSearching()
    {
        if (aimController == null) aimController = GetComponent<AimController>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();

        searchBaseAngle = aimController != null ? aimController.GetFacingAngle() : transform.eulerAngles.z;
        searchTimer = 0f;
        isSearching = true;
        Stop();
    }

    public void Search()
    {
        Stop();
        if (aimController == null) return;

        searchTimer += Time.deltaTime * searchSweepSpeed;
        float offset = Mathf.Sin(searchTimer) * searchSweepAngle;
        aimController.SnapTo(searchBaseAngle + offset);
    }

    public void StopSearching() => isSearching = false;

    public void FaceToward(Vector2 target)
    {
        if (aimController != null) aimController.AimAt(target);
    }

    public void MoveToward(Vector2 target) => SetDesiredToward(target, moveSpeed);

    public void MoveTowardSmart(Vector2 target, float speed)
    {
        if (pathfinding == null) { SetDesiredToward(target, speed); return; }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f || !pathfinding.HasPath)
        {
            pathfinding.ComputePath(target);
            repathTimer = repathInterval;
        }

        Vector2 step = pathfinding.GetCurrentSteeringTarget(target);
        SetDesiredToward(step, speed);
    }

    public void MoveTowardSmart(Vector2 target) => MoveTowardSmart(target, moveSpeed);

    public void MoveAwayFrom(Vector2 threat)
    {
        Vector2 dir = ((Vector2)transform.position - threat).normalized;
        SetDesired(dir * fleeSpeed);
    }

    public void Strafe(Vector2 target, bool clockwise = true)
    {
        Vector2 toTarget = ((Vector2)transform.position - target).normalized;
        Vector2 perp = clockwise
            ? new Vector2(-toTarget.y, toTarget.x)
            : new Vector2(toTarget.y, -toTarget.x);
        SetDesired(perp * moveSpeed);
    }

    public void Move(Vector2 direction, float speed) => SetDesired(direction.normalized * speed);
    public void Stop() => SetDesired(Vector2.zero);
    public bool IsInRange(Vector2 target, float range) => Vector2.Distance(transform.position, target) < range;

    // ── Patrol ────────────────────────────────────────────────────────────────

    public void Patrol()
    {
        if (route == null || route.WaypointCount == 0)
        {
            Stop();
            return;
        }

        float distToTarget = Vector2.Distance(transform.position, currentPatrolTarget);

        // 1. Advance to next index ONLY when physically reaching the waypoint
        if (distToTarget <= arriveRadius)
        {
            patrolIndex = (patrolIndex + 1) % route.WaypointCount;
            currentPatrolTarget = route.SampleWaypoint(patrolIndex);
            if (pathfinding != null) pathfinding.ComputePath(currentPatrolTarget);
            return;
        }

        // 2. Recompute path if dropped or empty
        if (pathfinding != null && !pathfinding.HasPath)
        {
            pathfinding.ComputePath(currentPatrolTarget);
        }

        // 3. Steer directly to the active path node
        Vector2 nextStep = (pathfinding != null) 
            ? pathfinding.GetCurrentSteeringTarget(currentPatrolTarget) 
            : (Vector2)currentPatrolTarget;

        SetDesiredToward(nextStep, patrolSpeed);
        FaceToward(nextStep);
    }

    public void ResumePatrolFromNearest()
    {
        if (route == null || route.WaypointCount == 0) return;

        float best = float.MaxValue;
        int bestIndex = 0;
        for (int i = 0; i < route.WaypointCount; i++)
        {
            float d = Vector2.SqrMagnitude((Vector2)route.GetCenter(i) - (Vector2)transform.position);
            if (d < best) 
            { 
                best = d; 
                bestIndex = i; 
            }
        }

        patrolIndex = bestIndex;
        currentPatrolTarget = route.SampleWaypoint(patrolIndex);
        if (pathfinding != null) pathfinding.ComputePath(currentPatrolTarget);
    }

    // ── Wave ───────────────────────────────────────────────────────────────
    // INPUT:  route
    // OUTPUT: patrol state reset to that route's first waypoint
    // USE:    WaveSpawner right after Instantiate (before the enemy's Start runs)
    public void SetRoute(PatrolRoute newRoute) { patrolRoute = newRoute; route = newRoute; patrolIndex = 0; }
    
    // ── Physics ───────────────────────────────────────────────────────────────

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

    private void SetDesiredToward(Vector2 target, float speed)
    {
        Vector2 diff = target - (Vector2)transform.position;
        if (diff.sqrMagnitude < 0.0001f)
        {
            SetDesired(Vector2.zero);
            return;
        }

        SetDesired(diff.normalized * speed);
    }

    private void SetDesired(Vector2 velocity)
    {
        desiredVelocity = velocity;
        commandedThisFrame = true;
    }

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
}