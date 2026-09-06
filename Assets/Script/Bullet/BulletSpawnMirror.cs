using UnityEngine;

[RequireComponent(typeof(BulletSpawn))]
public class BulletSpawnMirror : MonoBehaviour
{
    [SerializeField] private BulletSpawn primary;   // drag BulletSpawner (the one EnemyController finds)
    private BulletSpawn mine;
    private void Awake() => mine = GetComponent<BulletSpawn>();
    private void Update() => mine.isAutomaticSpawn = primary.isAutomaticSpawn;
}