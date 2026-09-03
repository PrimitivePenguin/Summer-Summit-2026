using UnityEngine;

/// <summary>
/// Rigidbody2D-based movement for enemies. Commands (MoveToward, Strafe, etc.)
/// set a desired velocity; FixedUpdate applies it so collisions resolve properly.
/// Decides nothing itself — the caller (EnemyController / TacticalBrain) chooses.
/// </summary>
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

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;         // leave null if unused

    private Rigidbody2D rb;
    private int patrolIndex = 0;

    private Vector2 desiredVelocity;    // set by command methods, applied in FixedUpdate
    private bool commandedThisFrame;    // did anyone issue a command since last physics step?

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    // ── Future EnemyData integration (Section 5 migration path) ──
    public void Initialize(float moveSpeed, float patrolSpeed, float fleeSpeed)
    {
        this.moveSpeed = moveSpeed;
        this.patrolSpeed = patrolSpeed;
        this.fleeSpeed = fleeSpeed;
    }

    // ────────────── COMMANDS (called from Update by controller/brain) ──────────────
    // These no longer touch rb directly — they record intent for FixedUpdate.

    /// <summary>Move toward a world position at chase speed.</summary>
    public void MoveToward(Vector2 target) => SetDesiredToward(target, moveSpeed);

    /// <summary>Move directly away from a world position at flee speed.</summary>
    public void MoveAwayFrom(Vector2 threat)
    {
        Vector2 dir = ((Vector2)transform.position - threat).normalized;
        SetDesired(dir * fleeSpeed);
    }

    /// <summary>Circle-strafe around a target (perpendicular to the self→target line).</summary>
    public void Strafe(Vector2 target, bool clockwise = true)
    {
        Vector2 toTarget = ((Vector2)transform.position - target).normalized;
        Vector2 perp = clockwise
            ? new Vector2(-toTarget.y, toTarget.x)
            : new Vector2(toTarget.y, -toTarget.x);
        SetDesired(perp * moveSpeed);
    }

    /// <summary>Move in an arbitrary direction at a given speed (generic command).</summary>
    public void Move(Vector2 direction, float speed) => SetDesired(direction.normalized * speed);

    public void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) { Stop(); return; }

        Vector2 target = patrolPoints[patrolIndex].position;
        SetDesiredToward(target, patrolSpeed);

        if (IsInRange(target, arriveRadius))
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
    }

    public void Stop() => SetDesired(Vector2.zero);

    public bool IsInRange(Vector2 target, float range)
        => Vector2.Distance(transform.position, target) < range;

    // ────────────── PHYSICS (actual movement + collision) ──────────────

    private void FixedUpdate()
    {
        // If no command was issued this step, treat it as "stop" so the enemy
        // doesn't coast forever on its last velocity.
        Vector2 target = commandedThisFrame ? desiredVelocity : Vector2.zero;

        if (acceleration <= 0f)
            rb.linearVelocity = target;                 // instant
        else
            rb.linearVelocity = Vector2.MoveTowards(
                rb.linearVelocity, target, acceleration * Time.fixedDeltaTime);

        UpdateAnimator(rb.linearVelocity);
        commandedThisFrame = false;                     // reset for next step
    }

    // ────────────── internal helpers ──────────────

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

        if (walking) // remember last facing when stopped, same trick as Player.cs
        {
            animator.SetFloat("LastInputX", velocity.x);
            animator.SetFloat("LastInputY", velocity.y);
        }
    }
    private void Update()
    {
        // Move(Vector2.right, moveSpeed);   // slides right, should stop at a wall
        
    }
}
