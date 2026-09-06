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

    [Header("Defend Point (Defender archetype only)")]
    [Tooltip("The position this enemy defends. Leave null to use its spawn position.")]
    [SerializeField] private Transform defendPoint;

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
            anchor = (defendPoint != null) ? (Vector2)defendPoint.position : (Vector2)transform.position
        };
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
            case EnemyState.Idle:        movement.Stop();           break;
            case EnemyState.Patrol:      movement.Patrol();         break; // Routes directly to your GridManager patrol
            case EnemyState.Engage:      behavior.Engage(ctx);      break;
            case EnemyState.Investigate: behavior.Investigate(ctx); break;
            case EnemyState.Search:      behavior.Search(ctx);      break;
        }
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    private void EvaluateTransitions()
    {
        if (vision.hasContact) { SetState(EnemyState.Engage); return; }

        switch (state)
        {
            case EnemyState.Idle:
                if (vision.IsPlayerInLoadRange()) SetState(EnemyState.Patrol);
                break;

            case EnemyState.Patrol:
                if (vision.awareness > 0f) SetState(EnemyState.Investigate);
                else if (!vision.IsPlayerInLoadRange()) SetState(EnemyState.Idle);
                break;

            case EnemyState.Engage:
                SetState(EnemyState.Investigate);
                break;

            case EnemyState.Investigate:
                if (movement.IsInRange(vision.lastKnownPosition, investigateArriveRadius) || stateTimer <= 0f)
                    SetState(EnemyState.Search);
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

        // EXIT hooks
        if (state == EnemyState.Search) movement.StopSearching();

        state = next;
        stateTimer = 0f;

        // ENTER hooks
        switch (next)
        {
            case EnemyState.Search:      
                movement.StartSearching(); 
                break;

            case EnemyState.Patrol:
                vision.ForgetLastKnown();
                movement.ResumePatrolFromNearest();
                break;

            case EnemyState.Investigate: 
                stateTimer = investigateTimeout; 
                break;

            case EnemyState.Idle:        
                movement.Stop(); 
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
        if (behavior != null && behavior.GetType().Name.Contains("Defender"))
        {
            Vector3 centre = (Application.isPlaying && ctx != null)
                ? (Vector3)ctx.anchor
                : (defendPoint != null ? defendPoint.position : transform.position);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centre, 3.5f);

            if (defendPoint != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
                Gizmos.DrawLine(transform.position, defendPoint.position);
                Gizmos.DrawSphere(defendPoint.position, 0.2f);
            }
        }
    }
}