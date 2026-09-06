using UnityEngine;

// One batch of identical enemies inside a wave.
[System.Serializable]
public class SpawnGroup
{
    public GameObject prefab;
    [Min(1)]  public int count = 3;
    [Min(0f)] public float startDelay = 0f;      // seconds after wave start before this group begins
    [Min(0f)] public float spacing = 0.5f;       // seconds between each spawn in this group
    public string spawnGroup = "";               // matches SpawnPoint.group; "" = any point
    public PatrolRoute route;                    // null = EnemySpawner.defaultRoute
}

// One wave. Groups run CONCURRENTLY, each on its own startDelay.
[System.Serializable]
public class WaveData
{
    public string waveName = "Wave";
    public SpawnGroup[] groups;
    [Min(0)]  public int maxAliveAtOnce = 0;     // 0 = uncapped; else spawning pauses while alive >= this
    [Min(0f)] public float postWaveDelay = 3f;   // breather after the wave is cleared
}