using UnityEngine;

public enum EnemyState
{
    Idle,        // Player outside load range, no awareness
    Patrol,      // Awareness zero, player loaded — route or guard-wander
    Engage,      // Live contact (cone or proximity sense)
    Investigate, // Lost contact, awareness > 0 — head for last known position
    Search       // Arrived at last known position — sweep in place
}

// Decides WHICH state the enemy is in. HOW it executes is the ArchetypeBehavior's job.
public class EnemyController : MonoBehaviour
{
    [Header("Behavior")]
    [SerializeField] private ArchetypeBehavior behavior;

    [Header("Components")]
    [SerializeField] private BulletSpawn bulletSpawn;
    [SerializeField] private AimController aimController;

    [Header("Level Manager (optional override — leave null to use LevelManager.Instance)")]
    [SerializeField] private LevelManager levelManager;

    [Header("Defend Post & Guard (Defender archetype only)")]
    [Tooltip("The position this enemy defends. Leave null to use its spawn position.")]
    [SerializeField] private Transform defendPoint;
    [SerializeField] private float defenseRadius = 3.5f;
    [SerializeField] private float postArriveRadius = 0.3f;
    [SerializeField] private float guardSpinSpeed = 45f; // degrees per second
    private Vector2 defenseAnchor;
    private float currentSpinAngle;

    [Header("Investigation")]
    [SerializeField] private float investigateArriveRadius = 0.6f;
    [SerializeField] private float investigateTimeout = 8f;

    [Header("Death")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Debug")]
    [SerializeField] private bool logTransitions = false;

    private EnemyVision vision;
    private EnemyMovement movement;
    private Damageable damageable;
    private Transform playerTransform;

    private EnemyState state = EnemyState.Idle;
    private float stateTimer;
    private EnemyContext ctx;

    public EnemyState CurrentState => state;
    private LevelManager Level => levelManager != null ? levelManager : LevelManager.Instance;
    private bool IsDefender => behavior != null && behavior.GetType().Name.Contains("Defender");

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
        if (damageable != null) damageable.OnDeath += HandleDeath;
        else Debug.LogWarning($"[EnemyController] No Damageable on {name}");
    }

    private void OnDestroy()
    {
        if (damageable != null) damageable.OnDeath -= HandleDeath;
    }

    private void Start()
    {
        Level?.RegisterEnemy();

        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();
        if (aimController == null) aimController = GetComponent<AimController>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();

        vision = GetComponent<EnemyVision>();
        movement = GetComponent<EnemyMovement>();

        if (vision == null)   { Debug.LogError($"[EnemyController] EnemyVision missing on {name}"); return; }
        if (behavior == null) { Debug.LogError($"[EnemyController] No ArchetypeBehavior on {name}"); return; }

        defenseAnchor = (defendPoint != null) ? (Vector2)defendPoint.position : (Vector2)transform.position;
        currentSpinAngle = aimController != null ? aimController.GetFacingAngle() : transform.eulerAngles.z;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;

        vision.Initialize(playerTransform, aimController);

        ctx = new EnemyContext
        {
            self = transform,
            movement = movement,
            aim = aimController,
            bulletSpawn = bulletSpawn,
            vision = vision,
            anchor = defenseAnchor,
            damageable = damageable, 
            abilities = GetComponent<AbilityRunner>(), 
            controller = this
        };

        state = EnemyState.Patrol;
    }

    // ── Frame Loop ────────────────────────────────────────────────────────────

    private void Update()
    {
        if (playerTransform == null || behavior == null || vision == null || ctx == null) return;

        vision.Tick();
        stateTimer -= Time.deltaTime;

        EvaluateTransitions();

        if (state == EnemyState.Engage)       ctx.targetPos = playerTransform.position;
        else if (vision.hasLastKnown)         ctx.targetPos = vision.lastKnownPosition;
        else                                  ctx.targetPos = transform.position;
        
        ctx.distToTarget = Vector2.Distance(transform.position, ctx.targetPos);


        switch (state)
        {
            case EnemyState.Idle:
                if (IsDefender) HandleDefenderGuard();
                else movement.Patrol();
                break;

            case EnemyState.Patrol:
                if (IsDefender) HandleDefenderGuard();
                else movement.Patrol();
                break;

            case EnemyState.Engage:      
                behavior.Engage(ctx);      
                break;

            case EnemyState.Investigate: 
                behavior.Investigate(ctx); 
                break;

            case EnemyState.Search:      
                behavior.Search(ctx);      
                break;
        }
    }

    // ── Defender Return & Spin ────────────────────────────────────────────────

    private void HandleDefenderGuard()
    {
        float distToPost = Vector2.Distance(transform.position, defenseAnchor);

        if (distToPost > postArriveRadius)
        {
            // 1. Walk back to post
            movement.MoveToward(defenseAnchor);
            if (aimController != null) aimController.AimAt(defenseAnchor);
        }
        else
        {
            // 2. Arrived at post: stand still and slowly rotate 360°
            movement.Stop();

            currentSpinAngle = (currentSpinAngle + guardSpinSpeed * Time.deltaTime) % 360f;
            if (aimController != null)
            {
                aimController.AimAtAngle(currentSpinAngle);
            }
        }
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    private void EvaluateTransitions()
    {
        if (vision.hasContact) { SetState(EnemyState.Engage); return; }

        switch (state)
        {
            case EnemyState.Idle:
                SetState(EnemyState.Patrol);
                break;

            case EnemyState.Patrol:
                if (vision.awareness > 0f) SetState(EnemyState.Investigate);
                break;

            case EnemyState.Engage:
                SetState(EnemyState.Investigate);
                break;

            case EnemyState.Investigate:
                // Defenders won't leave their post to chase ghosts
                if (IsDefender)
                {
                    if (vision.awareness <= 0.2f || stateTimer <= (investigateTimeout - 2.5f))
                        SetState(EnemyState.Search);
                }
                else if (movement.IsInRange(vision.lastKnownPosition, investigateArriveRadius) || stateTimer <= 0f)
                {
                    SetState(EnemyState.Search);
                }
                break;

            case EnemyState.Search:
                if (vision.awareness <= 0f || !movement.IsSearching())
                    SetState(EnemyState.Patrol);
                break;
        }
    }

    private void SetState(EnemyState next)
    {
        if (next == state) return;
        if (logTransitions) Debug.Log($"[EnemyController] {name}: {state} → {next}");

        if (state == EnemyState.Search) movement.StopSearching();

        state = next;
        stateTimer = 0f;

        switch (next)
        {
            case EnemyState.Search:      
                movement.StartSearching(); 
                break;

            case EnemyState.Patrol:
                vision.ForgetLastKnown();

                if (IsDefender)
                {
                    // Sync initial spin angle to wherever it was currently looking
                    if (aimController != null) currentSpinAngle = aimController.GetFacingAngle();
                }
                else
                {
                    movement.ResumePatrolFromNearest();
                }
                break;

            case EnemyState.Investigate: 
                stateTimer = investigateTimeout; 
                break;

            case EnemyState.Idle:        
                break;
        }

        if (bulletSpawn != null && next != EnemyState.Engage)
            bulletSpawn.isAutomaticSpawn = false;
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    private void HandleDeath()
    {
        Level?.UnregisterEnemy();

        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (IsDefender)
        {
            Vector3 centre = Application.isPlaying
                ? (Vector3)defenseAnchor
                : (defendPoint != null ? defendPoint.position : transform.position);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centre, defenseRadius);

            if (defendPoint != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
                Gizmos.DrawLine(transform.position, defendPoint.position);
                Gizmos.DrawSphere(defendPoint.position, 0.2f);
            }
        }
    }
}