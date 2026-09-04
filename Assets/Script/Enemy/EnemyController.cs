using UnityEngine;

public class EnemyController : MonoBehaviour
{
    // get child components
    [SerializeField] BulletSpawn bulletSpawn;
    [SerializeField] AimController aimController;
    EnemyVision vision;
    private EnemyMovement movement;
    Transform playerTransform;

    void Start()
    {
        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();
        vision = GetComponent<EnemyVision>();
        movement = GetComponent<EnemyMovement>();

        if (vision == null)
        {
            Debug.LogError($"[EnemyAI] EnemyVision not found on {gameObject.name} — is it on the root object?");
            return; // stops Start here so Initialize doesn't throw on top of it
        }
        bulletSpawn.isAutomaticSpawn = true;

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
            // 1. Aim the weapon
            aimController.AimAt(playerTransform.position);

            // 2. Command movement toward the player's current position
            movement.MoveToward(playerTransform.position);

            bulletSpawn.isAutomaticSpawn = true;
        }
        else if (vision.awareness > 0f)
        {
            // Player broke line of sight: move toward where they were last seen
            aimController.AimAt(vision.lastKnownPosition);
            movement.MoveToward(vision.lastKnownPosition);

            bulletSpawn.isAutomaticSpawn = false;
        }
        else
        {
            // Fully unaware: stop moving or patrol
            movement.Patrol(); // or movement.Patrol();
            bulletSpawn.isAutomaticSpawn = false;
        }
    }
}