using UnityEngine;
[CreateAssetMenu(fileName = "BulletSpawnData", menuName = "ScriptableObjects/BulletSpawnData", order = 1)]
public class BulletSpawnData : ScriptableObject
{
    [Header("Bullet Spawn Settings")]
    public GameObject bulletResource;
    public float bulletSpeed;
    public float bulletDespawnDist;
    public bool holdFire;
    [Header("Bullet Pattern")]
    public float spreadAngle;
    [HideInInspector] public float minRotation;
    [HideInInspector] public float maxRotation;
    public int numBullets;
    public bool isRandom;
    public bool isParent;
    public float burstDelay;

    [Header("Bullet Properties")]
    public float cooldown;
    public int damage;
    public LayerMask collisionLayers;
    
}
