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
    [SerializeField] private float arriveRadius = 0.3f;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;

    [Header("Search Settings")]
    [SerializeField] private AimController aimController;
    [SerializeField] private float searchSweepAngle = 50f;
    [SerializeField] private float searchSweepSpeed = 2f;

    private float searchBaseAngle;
    private float searchTimer;

    [Header("Pathfinding")]
    [SerializeField] private EnemyPathfinding pathfinding;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;

    private Rigidbody2D rb;
    private int patrolIndex = 0;

    private Vector2 desiredVelocity;
    private bool commandedThisFrame;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (pathfinding == null) pathfinding = GetComponent<EnemyPathfinding>();
    }

    public void Initialize(float moveSpeed, float patrolSpeed, float fleeSpeed)
    {
        this.moveSpeed = moveSpeed;
        this.patrolSpeed = patrolSpeed;
        this.fleeSpeed = fleeSpeed;
    }

    // ──────────── COMMANDS (called from Update by EnemyController) ────────────

    public void MoveToward(Vector2 target) => SetDesiredToward(target, moveSpeed);

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

    public void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) { Stop(); return; }

        Vector2 target = patrolPoints[patrolIndex].position;
        SetDesiredToward(target, patrolSpeed);

        if (IsInRange(target, arriveRadius))
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
    }

    // Move with A* pathfinding (avoids walls)
    public void MoveWithPathfinding(Vector2 target)
    {
        if (pathfinding == null) { MoveToward(target); return; }

        Vector2 nextPos = pathfinding.GetNextWaypoint(target);
        SetDesiredToward(nextPos, moveSpeed);
    }

    // Search/scan behavior: oscillate aim while standing still
    public void StartSearching()
    {
        if (aimController == null) aimController = GetComponent<AimController>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();

        searchBaseAngle = aimController != null ? aimController.GetFacingAngle() : transform.eulerAngles.z;
        searchTimer = 0f;
        Stop();
    }

    public void Search()
    {
        Stop();
        if (aimController == null) return;

        searchTimer += Time.deltaTime * searchSweepSpeed;
        float offset = Mathf.Sin(searchTimer) * searchSweepAngle;
        float targetAngle = searchBaseAngle + offset;
        aimController.SnapTo(targetAngle);
    }

    public void Stop() => SetDesired(Vector2.zero);

    public bool IsInRange(Vector2 target, float range)
        => Vector2.Distance(transform.position, target) < range;

    // ──────────── PHYSICS (actual movement + collision) ────────────

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

    // ──────────── INTERNAL HELPERS ────────────

    private void SetDesiredToward(Vector2 target, float speed)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        SetDesired(dir * speed);
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