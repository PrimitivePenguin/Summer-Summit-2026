using UnityEngine;

// Phase 1. Update - Sets direction enemy wants to go to 
// Phase 2. FixedUpdate - Applies that direction to the Rigidbody2D
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    // Variables
    [Header("Speeds")]
    [SerializeField] private float moveSpeed = 3f;      // chase speed
    [SerializeField] private float patrolSpeed = 1.5f;  // patrol speed
    [SerializeField] private float fleeSpeed = 4f;      // retreat speed

    [Header("Steering")]
    [SerializeField] private float acceleration = 20f;  // velocity ramp (0 = instant)
    [SerializeField] private float arriveRadius = 0.3f; // "close enough" threshold

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;

    [Header("Search Settings")]
    [SerializeField] private AimController aimController;
    [SerializeField] private float searchSweepAngle = 50f;
    [SerializeField] private float searchSweepSpeed = 2f; // oscillation speed

    private float searchBaseAngle;
    private bool isSearching;
    private float searchTimer;

    public void StartSearching(){
        if (aimController == null) aimController = GetComponent<AimController>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();

        searchBaseAngle = aimController != null ? aimController.GetFacingAngle() : transform.eulerAngles.z;
        searchTimer = 0f;
        isSearching = true;
        Stop();
    }

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

    public void Initialize(float moveSpeed, float patrolSpeed, float fleeSpeed)
    {
        this.moveSpeed = moveSpeed;
        this.patrolSpeed = patrolSpeed;
        this.fleeSpeed = fleeSpeed;
    }

    // COMMANDS (called from Update by enemyController)

    /// Move towards world position at move speed
    public void MoveToward(Vector2 target) => SetDesiredToward(target, moveSpeed);

    /// Move away from world position at flee speed
    public void MoveAwayFrom(Vector2 threat)
    {
        Vector2 dir = ((Vector2)transform.position - threat).normalized;
        SetDesired(dir * fleeSpeed);
    }

    // Move perpendicular to a target (strafe -> circle around target)
    public void Strafe(Vector2 target, bool clockwise = true)
    {
        Vector2 toTarget = ((Vector2)transform.position - target).normalized;
        Vector2 perp = clockwise
            ? new Vector2(-toTarget.y, toTarget.x)
            : new Vector2(toTarget.y, -toTarget.x);
        SetDesired(perp * moveSpeed);
    }

    // Generic command to move in a direction at a given speed (normalized direction)
    public void Move(Vector2 direction, float speed) => SetDesired(direction.normalized * speed);

    // Patrol through a list of points in order, looping back to the start
    public void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) { Stop(); return; }

        Vector2 target = patrolPoints[patrolIndex].position;
        SetDesiredToward(target, patrolSpeed);

        if (IsInRange(target, arriveRadius))
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
    }

    // Search
    public void Search()
    {
        // 1. Ensure body does not drift
        Stop();

        if (aimController == null) return;

        // 2. Advance oscillation timer
        searchTimer += Time.deltaTime * searchSweepSpeed;

        // 3. Smooth ping-pong sweep: baseAngle +/- sweepAngle
        float offset = Mathf.Sin(searchTimer) * searchSweepAngle;
        float targetAngle = searchBaseAngle + offset;

        // Snap or steer to the sweep angle
        aimController.SnapTo(targetAngle);
    }

    public void StopSearching(){
        isSearching = false;
    }

    // Stop moving (zero velocity)
    public void Stop() => SetDesired(Vector2.zero);

    // Check if a target is within a certain range of enemy
    public bool IsInRange(Vector2 target, float range)
        => Vector2.Distance(transform.position, target) < range;



    // PHYSICS
    private void FixedUpdate()
    {
        // If no command was issued this step, treat it as "stop" so the enemy
        // doesn't coast forever on its last velocity.
        Vector2 target = commandedThisFrame ? desiredVelocity : Vector2.zero;

        // If acceleration != 0, ramp up velocity toward target
        if (acceleration <= 0f)
            rb.linearVelocity = target;                 // instant
        else 
            rb.linearVelocity = Vector2.MoveTowards(
                rb.linearVelocity, target, acceleration * Time.fixedDeltaTime);

        UpdateAnimator(rb.linearVelocity);
        commandedThisFrame = false;                     // reset for next step
    }

    // INTERNAL FUNCTIONS

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