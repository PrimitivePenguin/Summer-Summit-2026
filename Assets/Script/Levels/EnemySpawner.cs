using UnityEngine;

// Turns "spawn this prefab" into a positioned, routed enemy.
// Knows nothing about waves, timing, or win conditions.
public class EnemySpawner : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] private float minPlayerDistance = 6f;

    [Header("Routing")]
    [Tooltip("Assigned to every spawned enemy unless the SpawnGroup overrides it. Null = keep the prefab's own route.")]
    [SerializeField] private PatrolRoute defaultRoute;

    private SpawnPoint[] points;
    private Transform player;
    private bool warnedNoPoints;

    // INPUT:  scene
    // OUTPUT: cached SpawnPoint list
    // USE:    Unity
    private void Awake() => points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);

    // INPUT:  scene
    // OUTPUT: player transform (by component, NOT tag — PlayerDive may retag the player)
    // USE:    Unity
    private void Start()
    {
        Player p = FindAnyObjectByType<Player>();
        if (p != null) player = p.transform;
    }

    // INPUT:  prefab, group filter ("" = any), route override (null = defaultRoute)
    // OUTPUT: the spawned enemy, or null if no legal point exists this frame
    // USE:    WaveController.RunGroup, WaveSpawner.SpawnOne, debug hotkeys
    public GameObject Spawn(GameObject prefab, string group = "", PatrolRoute route = null)
    {
        SpawnPoint point = PickPoint(group);
        if (point == null) return null;

        GameObject go = Instantiate(prefab, point.Position, Quaternion.identity);

        // SetRoute must run before the enemy's Start (next frame) — see EnemyMovement.SetRoute
        PatrolRoute r = route != null ? route : defaultRoute;
        if (r != null) go.GetComponent<EnemyMovement>()?.SetRoute(r);

        return go;
    }

    // INPUT:  group filter
    // OUTPUT: random matching point farther than minPlayerDistance from the player;
    //         relaxes the distance rule if nothing qualifies; null only if no points match the group at all
    // USE:    Spawn
    private SpawnPoint PickPoint(string group)
    {
        if (points == null || points.Length == 0)
        {
            if (!warnedNoPoints) { Debug.LogError("[EnemySpawner] No SpawnPoint components in scene."); warnedNoPoints = true; }
            return null;
        }

        SpawnPoint best = null; int bestCount = 0;   // reservoir sample: far points
        SpawnPoint any  = null; int anyCount  = 0;   // reservoir sample: any group match

        float minSq = minPlayerDistance * minPlayerDistance;
        foreach (SpawnPoint p in points)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            if (!string.IsNullOrEmpty(group) && p.group != group) continue;

            anyCount++;
            if (Random.Range(0, anyCount) == 0) any = p;

            bool farEnough = player == null || ((Vector2)player.position - p.Position).sqrMagnitude >= minSq;
            if (!farEnough) continue;

            bestCount++;
            if (Random.Range(0, bestCount) == 0) best = p;
        }

        return best != null ? best : any;
    }
}