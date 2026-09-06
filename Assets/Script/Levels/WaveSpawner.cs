using UnityEngine;
public class WaveSpawner : MonoBehaviour
{
    [System.Serializable] public class Entry { public GameObject prefab; [Min(0)] public float weight = 1f; }

    [SerializeField] private Entry[] entries;
    [SerializeField] private EnemySpawner spawner;          // finds SpawnPoints itself — replaces the old Transform[] array
    [SerializeField] private float startDelay = 3f, interval = 4f;
    [SerializeField] private int maxAlive = 8;
    [SerializeField] private AnimationCurve intervalOverTime = AnimationCurve.Linear(0, 1, 1, 0.5f);

    private float timer; private int alive; private float elapsed; private LevelManager level;

    // INPUT:  LevelManager.Instance, spawner
    // OUTPUT: alive count subscription, first-spawn timer
    // USE:    Unity
    private void Start()
    {
        level = LevelManager.Instance;
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        timer = startDelay;
        level.OnEnemiesRemainingChanged += n => alive = n;
    }

    private void Update()
    {
        if (level == null || level.IsLevelEnded) return;
        elapsed += Time.deltaTime; timer -= Time.deltaTime;
        if (timer > 0f || alive >= maxAlive) return;
        SpawnOne();
        timer = interval * intervalOverTime.Evaluate(Mathf.Clamp01(elapsed / level.SurviveDuration));
    }

    // REPLACES WaveSpawner.SpawnOne
    // INPUT:  weighted entries
    // OUTPUT: one enemy at a legal point, routed via spawner.Default Route
    // USE:    Update
    private void SpawnOne() => spawner.Spawn(Pick().prefab);

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