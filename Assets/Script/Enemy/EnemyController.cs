using UnityEngine;

public enum EnemyArchetype
{
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

    [Header("Movement Settings")]
    [SerializeField] private float repathInterval = 0.4f;
    private float repathTimer;
    private EnemyPathfinding pathfinding;

    [Header("Death Settings")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Defender Settings")]
    [SerializeField] private float defenseRadius = 3.5f;
    private Vector2 defenseAnchor;

    [Header("Skirmisher Settings")]
    [SerializeField] private float preferredMinDist = 4.0f;
    [SerializeField] private float preferredMaxDist = 7.0f;
    [SerializeField] private bool strafeClockwise = true;
    [SerializeField] private float strafeSwitchInterval = 2.5f;
    private float strafeTimer;

    [Header("Chaser Settings")]
    [SerializeField] private float attackRange = 2.2f;

    void Awake()
    {
        damageable = GetComponent<Damageable>();
        if (damageable != null)
        {
            damageable.OnDeath += HandleDeath;
        }
        else
        {
            Debug.LogWarning($"[EnemyController] No Damageable Component found on {gameObject.name}!");
        }
    }

    void OnDestroy()
    {
        if (damageable != null)
        {
            damageable.OnDeath -= HandleDeath;
        }
    }

    void Start()
    {
        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();
        vision = GetComponent<EnemyVision>();
        movement = GetComponent<EnemyMovement>();
        pathfinding = GetComponent<EnemyPathfinding>();

        repathTimer = 0f;

        if (vision == null)
        {
            Debug.LogError($"[EnemyController] EnemyVision not found on {gameObject.name}");
            return;
        }

        defenseAnchor = transform.position;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;

        vision.Initialize(playerTransform, aimController);
        bulletSpawn.isAutomaticSpawn = false;  // controlled by archetype logic
    }

    void Update()
    {
        if (playerTransform == null) return;

        vision.Tick();

        // Countdown the repath timer every frame
        repathTimer -= Time.deltaTime;

        if (vision.canSeePlayer)
        {
            Vector2 targetPos = playerTransform.position;
            aimController.AimAt(targetPos);

            // Repath periodically
            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                pathfinding.ComputePath(targetPos);
            }

            ExecuteArchetypeMovement(targetPos, isDirectSight: true);
        }
        else if (vision.awareness > 0f)
        {
            // Lost direct line-of-sight: path toward last known location
            Vector2 lastSeen = vision.lastKnownPosition;
            aimController.AimAt(lastSeen);

            // Repath to last known position
            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                pathfinding.ComputePath(lastSeen);
            }

            float distToLastSeen = Vector2.Distance(transform.position, lastSeen);
            if (distToLastSeen > 0.6f)
            {
                movement.MoveWithPathfinding(lastSeen);
            }
            else
            {
                movement.Search();
            }

            bulletSpawn.isAutomaticSpawn = false;
        }
        else
        {
            // Fully unaware
            HandleUnawareState();
        }
    }

    private void ExecuteArchetypeMovement(Vector2 targetPos, bool isDirectSight)
    {
        float distanceToPlayer = Vector2.Distance(transform.position, targetPos);

        switch (archetype)
        {
            case EnemyArchetype.Chaser:
                if (distanceToPlayer <= attackRange)
                {
                    movement.Search();
                    bulletSpawn.isAutomaticSpawn = true;
                }
                else
                {
                    movement.MoveWithPathfinding(targetPos);
                    bulletSpawn.isAutomaticSpawn = false;
                }
                break;

            case EnemyArchetype.Defender:
                float distFromAnchor = Vector2.Distance(transform.position, defenseAnchor);

                if (distFromAnchor > defenseRadius)
                {
                    movement.MoveToward(defenseAnchor);
                }
                else
                {
                    movement.Search();
                }

                bulletSpawn.isAutomaticSpawn = aimController.IsAimedAt(targetPos, 15f);
                break;

            case EnemyArchetype.Skirmisher:
                strafeTimer -= Time.deltaTime;
                if (strafeTimer <= 0f)
                {
                    strafeClockwise = !strafeClockwise;
                    strafeTimer = strafeSwitchInterval;
                }

                if (distanceToPlayer < preferredMinDist)
                {
                    movement.MoveAwayFrom(targetPos);
                }
                else if (distanceToPlayer > preferredMaxDist)
                {
                    movement.MoveWithPathfinding(targetPos);
                }
                else
                {
                    movement.Strafe(targetPos, strafeClockwise);
                }

                bulletSpawn.isAutomaticSpawn = true;
                break;
        }
    }

    private void HandleUnawareState()
    {
        bulletSpawn.isAutomaticSpawn = false;

        switch (archetype)
        {
            case EnemyArchetype.Defender:
                if (Vector2.Distance(transform.position, defenseAnchor) > 0.5f)
                {
                    movement.MoveToward(defenseAnchor);
                }
                else
                {
                    movement.Search();
                }
                break;

            case EnemyArchetype.Chaser:
            case EnemyArchetype.Skirmisher:
                movement.Search();
                break;
        }
    }

    private void HandleDeath()
    {
        if (deathEffectPrefab != null)
        {
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