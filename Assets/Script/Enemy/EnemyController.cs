using UnityEngine;

// The five behavioural states. If EnemyEnums.cs already declares an EnemyState,
// DELETE ONE OF THEM — C# will not compile two enums with the same name in the
// same assembly.
public enum EnemyState
{
    Idle,        // player outside load range and no awareness — do nothing at all
    Patrol,      // awareness zero, player is loaded — route or guard-wander
    Engage,      // direct line of sight right now
    Investigate, // lost sight, awareness above zero — head for the last known position
    Search       // arrived at the last known position — sweep in place
}

// Decides WHICH state the enemy is in. Never decides HOW to execute it — that is
// delegated to the assigned ArchetypeBehavior asset.
//
// INPUT:  EnemyVision (canSeePlayer, awareness, lastKnownPosition, IsPlayerInLoadRange)
// OUTPUT: state transitions, then one behavior call per frame
//
// TO ADD A BEHAVIOUR: write a new ArchetypeBehavior subclass and make an asset.
// This file does not change.
// TO ADD A STATE: one enum entry, one case in EvaluateTransitions, one case in the
// Update switch, and optionally an enter/exit hook in SetState.
public class EnemyController : MonoBehaviour
{
    [Header("Behavior")]
    [SerializeField] private ArchetypeBehavior behavior;   // drag a Chaser/Skirmisher/Defender asset

    [Header("Components")]
    [SerializeField] private BulletSpawn bulletSpawn;
    [SerializeField] private AimController aimController;

    [Header("Investigation")]
    [SerializeField] private float investigateArriveRadius = 0.6f;
    [SerializeField] private float investigateTimeout = 8f;   // defenders never "arrive" — this
                                                              // is what moves them on to Search

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
    // OUTPUT: current state — for debug HUDs and gizmos
    public EnemyState CurrentState => state;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    // INPUT:  none
    // OUTPUT: caches Damageable and subscribes HandleDeath
    void Awake()
    {
        damageable = GetComponent<Damageable>();
        if (damageable != null)
            damageable.OnDeath += HandleDeath;
        else
            Debug.LogWarning($"[EnemyController] No Damageable on {name}");
    }

    // INPUT:  none
    // OUTPUT: unsubscribes the death handler
    // USE:    prevents a dangling delegate when the enemy is destroyed
    void OnDestroy()
    {
        if (damageable != null)
            damageable.OnDeath -= HandleDeath;
    }

    // INPUT:  inspector refs, scene tag "Player"
    // OUTPUT: resolves components, registers with LevelManager, builds the EnemyContext
    // USE:    the context is built ONCE per enemy — behavior assets are shared and
    //         must not hold per-enemy state
    void Start()
    {
        if (LevelManager.Instance != null)
            LevelManager.Instance.RegisterEnemy();

        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();

        // AimController MUST be on the root. On a child, rotation never propagates to
        // sibling components, so the FOV cone and bullet spawn point diverge.
        if (aimController == null) aimController = GetComponent<AimController>();
        if (aimController == null)
            Debug.LogError($"[EnemyController] AimController must be on the ROOT of {name}");

        vision = GetComponent<EnemyVision>();
        movement = GetComponent<EnemyMovement>();

        if (vision == null)
        {
            Debug.LogError($"[EnemyController] EnemyVision not found on {name} — is it on the root?");
            return;
        }
        if (behavior == null)
        {
            Debug.LogError($"[EnemyController] No ArchetypeBehavior assigned to {name}");
            return;
        }

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
            anchor = transform.position
        };
    }

    // ── Frame loop ────────────────────────────────────────────────────────────

    // INPUT:  vision state
    // OUTPUT: evaluates transitions, refreshes the context, dispatches to the behavior
    // USE:    exactly one behavior call per frame — no nested archetype branching here
    void Update()
    {
        if (playerTransform == null || behavior == null || vision == null || ctx == null) return;

        vision.Tick();
        stateTimer -= Time.deltaTime;

        EvaluateTransitions();

        // Engage tracks the live player; every other state works off the remembered fix.
        ctx.targetPos = (state == EnemyState.Engage)
            ? (Vector2)playerTransform.position
            : vision.lastKnownPosition;
        ctx.distToTarget = Vector2.Distance(transform.position, ctx.targetPos);

        switch (state)
        {
            case EnemyState.Idle: movement.Stop(); break;
            case EnemyState.Patrol: behavior.Idle(ctx); break;
            case EnemyState.Engage: behavior.Engage(ctx); break;
            case EnemyState.Investigate: behavior.Investigate(ctx); break;
            case EnemyState.Search: behavior.Search(ctx); break;
        }
    }

    // INPUT:  vision.canSeePlayer, vision.awareness, vision.IsPlayerInLoadRange, stateTimer
    // OUTPUT: may call SetState
    // USE:    pure transition logic — no archetype knowledge, no movement calls
    private void EvaluateTransitions()
    {
        // Direct sight outranks everything, from any state.
        if (vision.canSeePlayer) { SetState(EnemyState.Engage); return; }

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
                SetState(EnemyState.Investigate);   // sight broke this frame
                break;

            case EnemyState.Investigate:
                // Arrived, or ran out of patience. Defenders never arrive, so the
                // timeout is what advances them.
                if (movement.IsInRange(vision.lastKnownPosition, investigateArriveRadius)
                    || stateTimer <= 0f)
                    SetState(EnemyState.Search);
                break;

            case EnemyState.Search:
                // Search() clears IsSearching when its duration expires.
                if (vision.awareness <= 0f || !movement.IsSearching())
                    SetState(EnemyState.Patrol);
                break;
        }
    }

    // INPUT:  target state
    // OUTPUT: runs exit hooks, swaps state, runs enter hooks
    // USE:    the ONLY place side effects fire on a state change. StartSearching is
    //         called exactly once per Search entry (calling it per frame would reset
    //         the sweep forever), and ResumePatrolFromNearest only on entering Patrol.
    private void SetState(EnemyState next)
    {
        if (next == state) return;
        Debug.Log($"[{name}] state: {state} → {next}");
        // EXIT
        if (state == EnemyState.Search) movement.StopSearching();

        state = next;
        stateTimer = 0f;

        // ENTER
        switch (next)
        {
            case EnemyState.Search: movement.StartSearching(); break;
            case EnemyState.Patrol: 
                vision.ForgetLastKnown();       
                movement.ResumePatrolFromNearest();
                break;

            case EnemyState.Investigate: stateTimer = investigateTimeout; break;
            case EnemyState.Idle: movement.Stop(); break;
        }

        // Hard safety: nothing outside Engage may leave the trigger held down.
        if (bulletSpawn != null && next != EnemyState.Engage)
            bulletSpawn.isAutomaticSpawn = false;

        if (logTransitions) Debug.Log($"[EnemyController] {name} → {next}");
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    // INPUT:  none (raised by Damageable.OnDeath)
    // OUTPUT: decrements the LevelManager count, spawns the death effect, destroys self
    private void HandleDeath()
    {
        if (LevelManager.Instance != null)
            LevelManager.Instance.UnregisterEnemy();

        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

    // INPUT:  behavior, anchor
    // OUTPUT: draws the Defender leash and the current state label
    // USE:    Scene view, selected object only
    private void OnDrawGizmosSelected()
    {
        if (behavior is DefenderBehavior def)
        {
            Gizmos.color = Color.cyan;
            Vector3 centre = (Application.isPlaying && ctx != null)
                ? (Vector3)ctx.anchor
                : transform.position;
            Gizmos.DrawWireSphere(centre, def.GetDefenseRadius());
        }
    }
}