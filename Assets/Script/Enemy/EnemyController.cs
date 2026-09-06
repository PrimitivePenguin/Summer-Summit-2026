using UnityEngine;

public enum EnemyState
{
    Idle,        // player outside load range, no awareness
    Patrol,      // awareness zero, player loaded — route or guard-wander
    Engage,      // live contact (cone or proximity sense)
    Investigate, // lost contact, awareness > 0 — head for last known position
    Search       // arrived at last known position — sweep in place
}

// Decides WHICH state the enemy is in. HOW it executes is the ArchetypeBehavior's job.
//
// FIXES IN THIS VERSION:
//   EvaluateTransitions — Engage on vision.hasContact (cone OR sense). Previously only the
//                         cone counted, so a point-blank player could never trigger firing.
//   SetState            — unconditional Debug.Log removed; respects logTransitions.
//   Update              — targetPos falls back to self when there is no valid fix.
//   levelManager slot   — NEW inspector override for the LevelManager (null = singleton).
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

    // INPUT:  none
    // OUTPUT: current state
    // USE:    debug HUDs, gizmos
    public EnemyState CurrentState => state;

    // INPUT:  levelManager slot, singleton
    // OUTPUT: whichever LevelManager applies to this enemy (may be null)
    // USE:    every LevelManager call in this file
    private LevelManager Level => levelManager != null ? levelManager : LevelManager.Instance;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    // INPUT:  none
    // OUTPUT: caches Damageable, subscribes HandleDeath
    // USE:    Unity
    void Awake()
    {
        damageable = GetComponent<Damageable>();
        if (damageable != null) damageable.OnDeath += HandleDeath;
        else Debug.LogWarning($"[EnemyController] No Damageable on {name}");
    }

    // INPUT:  none
    // OUTPUT: unsubscribes death handler
    // USE:    Unity
    void OnDestroy()
    {
        if (damageable != null) damageable.OnDeath -= HandleDeath;
    }

    // INPUT:  inspector refs, "Player" tag
    // OUTPUT: resolves components, registers with LevelManager, builds EnemyContext
    // USE:    Unity. REPLACES old Start (uses Level accessor)
    void Start()
    {
        Level?.RegisterEnemy();

        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();
        if (aimController == null) aimController = GetComponent<AimController>();
        if (aimController == null) Debug.LogError($"[EnemyController] AimController must be on the ROOT of {name}");

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

    // ── Frame loop ────────────────────────────────────────────────────────────

    // INPUT:  vision state
    // OUTPUT: transitions, refreshed context, one behavior call
    // USE:    Unity. REPLACES old Update (targetPos guard)
    void Update()
    {
        if (playerTransform == null || behavior == null || vision == null || ctx == null) return;

        vision.Tick();
        stateTimer -= Time.deltaTime;

        EvaluateTransitions();

        if (state == EnemyState.Engage)      ctx.targetPos = playerTransform.position;
        else if (vision.hasLastKnown)         ctx.targetPos = vision.lastKnownPosition;
        else                                  ctx.targetPos = transform.position;
        ctx.distToTarget = Vector2.Distance(transform.position, ctx.targetPos);

        switch (state)
        {
            case EnemyState.Idle:        movement.Stop();          break;
            case EnemyState.Patrol:      behavior.Idle(ctx);       break;
            case EnemyState.Engage:      behavior.Engage(ctx);     break;
            case EnemyState.Investigate: behavior.Investigate(ctx); break;
            case EnemyState.Search:      behavior.Search(ctx);     break;
        }
    }

    // INPUT:  vision.hasContact, awareness, load range, stateTimer
    // OUTPUT: may call SetState
    // USE:    pure transition logic. REPLACES old EvaluateTransitions (hasContact)
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

    // INPUT:  next state
    // OUTPUT: exit hooks, state swap, enter hooks
    // USE:    the ONLY place state-change side effects live. REPLACES old SetState (log gated)
    private void SetState(EnemyState next)
    {
        if (next == state) return;
        if (logTransitions) Debug.Log($"[EnemyController] {name}: {state} → {next}");

        // EXIT
        if (state == EnemyState.Search) movement.StopSearching();

        state = next;
        stateTimer = 0f;

        // ENTER
        switch (next)
        {
            case EnemyState.Search:      movement.StartSearching(); break;
            case EnemyState.Patrol:
                vision.ForgetLastKnown();
                movement.ResumePatrolFromNearest();
                break;
            case EnemyState.Investigate: stateTimer = investigateTimeout; break;
            case EnemyState.Idle:        movement.Stop(); break;
        }

        if (bulletSpawn != null && next != EnemyState.Engage)
            bulletSpawn.isAutomaticSpawn = false;
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    // INPUT:  none (Damageable.OnDeath — fires once now)
    // OUTPUT: unregisters, spawns effect, destroys self
    // USE:    event handler
    private void HandleDeath()
    {
        Level?.UnregisterEnemy();

        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

    // INPUT:  behavior, anchor
    // OUTPUT: Defender leash gizmo
    // USE:    Scene view, selected only
    private void OnDrawGizmosSelected()
    {
        if (behavior is DefenderBehavior def)
        {
            Vector3 centre = (Application.isPlaying && ctx != null)
                ? (Vector3)ctx.anchor
                : (defendPoint != null ? defendPoint.position : transform.position);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centre, def.GetDefenseRadius());

            // Draw a line from the enemy to the defend point so it is obvious in the scene view
            if (defendPoint != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
                Gizmos.DrawLine(transform.position, defendPoint.position);
                Gizmos.DrawSphere(defendPoint.position, 0.2f);
            }
        }
    }
}