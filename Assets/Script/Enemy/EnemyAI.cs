using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    // get child components
    [SerializeField] BulletSpawn bulletSpawn;
    [SerializeField] AimController aimController;
    EnemyVision vision;
    Transform playerTransform;

    void Start()
    {
        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();
        vision = GetComponent<EnemyVision>();
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

        vision.Tick();   // refresh canSeePlayer / awareness / lastKnownPosition first

        if (vision.canSeePlayer)
        {
            // clear shot: track the live player and let the turret auto-fire
            aimController.AimAt(playerTransform.position);
            bulletSpawn.isAutomaticSpawn = true;
        }
        else if (vision.awareness > 0f)
        {
            // lost the cone but still aware: pivot toward the last known fix, hold fire
            aimController.AimAt(vision.lastKnownPosition);
            bulletSpawn.isAutomaticSpawn = false;
        }
        else
        {
            // fully unaware: idle
            bulletSpawn.isAutomaticSpawn = false;
        }
    }
}