using UnityEngine;

public enum EnemyArchetype{
    Chaser,
    Defender,
    Skirmisher
}


public class EnemyController : MonoBehaviour
{
    [Header("Archetype")]
    [SerializeField] private EnemyArchetype archetype = EnemyArchetype.Skirmisher;

    [Header("Components")]
    [SerializeField] private BulletSpawn bulletSpawn;
    [SerializeField] private AimController aimController;
    private EnemyVision vision;
    private EnemyMovement movement;
    private Damageable damageable;
    private Transform playerTransform;

    [Header("Death Settings")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("DefenderSettings")]
    [SerializeField] private float defenseRadius = 3.5f;
    private Vector2 defenseAnchor;

    [Header("Skirmisher Settings")]
    [SerializeField] private float preferredMinDist = 4.0f; // retreat if player is too close
    [SerializeField] private float preferredMaxDist = 7.0f; // advance if player is too far
    [SerializeField] private bool strafeClockwise = true;
    [SerializeField] private float strafeSwitchInterval = 2.5f;
    private float strafeTimer;

    [Header("Chaser Settings")]
    [SerializeField] private float attackRange = 2.2f; // Point Blank Range to blast you

    [Header("Defender Patrol")]
    [SerializeField] private float defenderWanderInterval = 3f;
    private float wanderTimer;
    private Vector2 wanderTarget;

    void Awake()
    {
        damageable = GetComponent<Damageable>();
        if (damageable != null){
            damageable.OnDeath += HandleDeath;
        }
        else{
            Debug.LogWarning($"[EnemyController] No Damageable Component found on {gameObject.name}!");
        }
    }

    void OnDestroy(){
        if (damageable != null){
            damageable.OnDeath -= HandleDeath;
        }
    }

    void Start()
    {

        // 1. Tell LevelManager this enemy exists (works for pre-placed or spawned enemies)
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.RegisterEnemy();
        }

        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();
        vision = GetComponent<EnemyVision>();
        movement = GetComponent<EnemyMovement>();

        if (vision == null)
        {
            Debug.LogError($"[EnemyAI] EnemyVision not found on {gameObject.name} — is it on the root object?");
            return; // stops Start here so Initialize doesn't throw on top of it
        }

        defenseAnchor = transform.position;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;

        vision.Initialize(playerTransform, aimController);
    }

    void Update()
    {
        if (playerTransform == null) return;

        vision.Tick();

        if (vision.canSeePlayer)
        {
            Vector2 targetPos = playerTransform.position;
            aimController.AimAt(targetPos);

            // Execute archetype-specific positioning & firing logic
            ExecuteArchetypeMovement(targetPos, isDirectSight: true);
        }
        else if (vision.awareness > 0f)
        {
            Vector2 lastSeen = vision.lastKnownPosition;
            bulletSpawn.isAutomaticSpawn = false;

            if (archetype == EnemyArchetype.Defender)
            {
                // Defender watches the corner where you vanished, but NEVER leaves its anchor
                aimController.AimAt(lastSeen);

                float distFromAnchor = Vector2.Distance(transform.position, defenseAnchor);
                if (distFromAnchor > 0.5f)
                {
                    movement.MoveToward(defenseAnchor);
                }
                else
                {
                    movement.Stop();
                }
            }
            else
            {
                // Chasers and Skirmishers advance to investigate
                aimController.AimAt(lastSeen);

                float distToLastSeen = Vector2.Distance(transform.position, lastSeen);
                if (distToLastSeen > 0.6f)
                {
                    movement.MoveToward(lastSeen);
                }
                else
                {
                    movement.Search();
                }
            }
        }
        else
        {
            // Unaware state
            bulletSpawn.isAutomaticSpawn = false;
            HandleUnawareState();
        }
    }

    private void ExecuteArchetypeMovement(Vector2 targetPos, bool isDirectSight)
    {
        float distanceToPlayer = Vector2.Distance(transform.position, targetPos);

        switch (archetype)
        {
            case EnemyArchetype.Chaser:
                // Sprints into close range; pauses to blast
                if (distanceToPlayer <= attackRange)
                {
                    movement.Search();
                    bulletSpawn.isAutomaticSpawn = true;
                }
                else
                {
                    movement.MoveToward(targetPos);
                    bulletSpawn.isAutomaticSpawn = false; // Hold fire until within blast range
                }
                break;

            case EnemyArchetype.Defender:
                float myDistFromAnchor = Vector2.Distance(transform.position, defenseAnchor);
                float playerDistFromAnchor = Vector2.Distance(targetPos, defenseAnchor);

                // 1. Hard leash: pulled past boundary -> force return immediately
                if (myDistFromAnchor > defenseRadius)
                {
                    movement.MoveToward(defenseAnchor);
                }
                // 2. Player invaded the defense perimeter: close in or strafe around them
                else if (playerDistFromAnchor <= defenseRadius)
                {
                    // Strafe around the invader while inside the zone
                    movement.Strafe(targetPos, strafeClockwise);
                }
                // 3. Player is outside the perimeter: advance to edge or circle the anchor
                else
                {
                    // If still near center, push up toward player until hitting the boundary
                    if (myDistFromAnchor < defenseRadius * 0.8f)
                    {
                        movement.MoveToward(targetPos);
                    }
                    else
                    {
                        // At the border: strafe around the anchor to stay mobile without leaving
                        movement.Strafe(defenseAnchor, strafeClockwise);
                    }
                }

                // Toggle strafe direction periodically so it doesn't get stuck on obstacles
                strafeTimer -= Time.deltaTime;
                if (strafeTimer <= 0f)
                {
                    strafeClockwise = !strafeClockwise;
                    strafeTimer = strafeSwitchInterval;
                }

                bulletSpawn.isAutomaticSpawn = aimController.IsAimedAt(targetPos, 15f);
                break;

            case EnemyArchetype.Skirmisher:
                // Clockwise/counter-clockwise strafe oscillation
                strafeTimer -= Time.deltaTime;
                if (strafeTimer <= 0f)
                {
                    strafeClockwise = !strafeClockwise;
                    strafeTimer = strafeSwitchInterval;
                }

                // Zone the player
                if (distanceToPlayer < preferredMinDist)
                {
                    // Back up
                    movement.MoveAwayFrom(targetPos);
                }
                else if (distanceToPlayer > preferredMaxDist)
                {
                    // Close the gap
                    movement.MoveToward(targetPos);
                }
                else
                {
                    // Sweet spot: circle-strafe
                    movement.Strafe(targetPos, strafeClockwise);
                }

                bulletSpawn.isAutomaticSpawn = true;
                break;
        }
    }

    private void HandleUnawareState()
    {
        switch (archetype)
        {
            case EnemyArchetype.Defender:
                wanderTimer -= Time.deltaTime;
                if (wanderTimer <= 0f || Vector2.Distance(transform.position, wanderTarget) < 0.4f)
                {
                    // Pick a new random point inside the defense radius
                    wanderTarget = defenseAnchor + (Random.insideUnitCircle * (defenseRadius * 0.7f));
                    wanderTimer = defenderWanderInterval;
                }

                movement.MoveToward(wanderTarget);
                break;

            case EnemyArchetype.Chaser:
            case EnemyArchetype.Skirmisher:
                movement.Patrol();
                break;
        }
    }

    private void HandleDeath(){
        // Decrement the count when killed
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.UnregisterEnemy();
        }

        if (deathEffectPrefab != null){
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (archetype == EnemyArchetype.Defender)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = Application.isPlaying ? (Vector3)defenseAnchor : transform.position;
            Gizmos.DrawWireSphere(center, defenseRadius);
        }
    }
}