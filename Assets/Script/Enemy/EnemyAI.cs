using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    // get child components
    [SerializeField] BulletSpawn bulletSpawn;
    [SerializeField] AimController aimController;
    
    Transform playerTransform;

    void Start()
    {
        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();
        
        bulletSpawn.isAutomaticSpawn = true;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
        if (playerTransform != null)
        {
            aimController.AimAt(playerTransform.position);
        }
    }
}