using UnityEngine;
public class WaveSpawner : MonoBehaviour
{
    [System.Serializable] public class Entry { public GameObject prefab; [Min(0)] public float weight = 1f; }

    [SerializeField] private Entry[] entries;
    [SerializeField] private Transform[] spawnPoints;      // empties on the map edge
    [SerializeField] private PatrolRoute centerRoute;      // one waypoint at the house, radius ~3
    [SerializeField] private float startDelay = 3f, interval = 4f;
    [SerializeField] private int maxAlive = 8;
    [SerializeField] private AnimationCurve intervalOverTime = AnimationCurve.Linear(0, 1, 1, 0.5f); // multiplier vs level progress

    private float timer; private int alive; private float elapsed; private LevelManager level;

    private void Start() { level = LevelManager.Instance; timer = startDelay; level.OnEnemiesRemainingChanged += n => alive = n; }
    private void Update()
    {
        if (level == null || level.IsLevelEnded) return;
        elapsed += Time.deltaTime; timer -= Time.deltaTime;
        if (timer > 0f || alive >= maxAlive) return;
        SpawnOne();
        timer = interval * intervalOverTime.Evaluate(Mathf.Clamp01(elapsed / level.SurviveDuration));
    }
    private void SpawnOne()
    {
        var p = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var go = Instantiate(Pick().prefab, p.position, Quaternion.identity);
        go.GetComponent<EnemyMovement>()?.SetRoute(centerRoute);
    }
    private Entry Pick()
    {
        float total = 0f;
        foreach (var e in entries) total += e.weight;

        float roll = Random.Range(0f, total);
        foreach (var e in entries)
        {
            if (roll < e.weight) return e;
            roll -= e.weight;
        }
        return entries[entries.Length - 1];
    }
}