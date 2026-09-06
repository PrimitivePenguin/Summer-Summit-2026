using UnityEngine;

// Marker for a legal enemy spawn location. Drop on an empty GameObject.
public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Optional label. A SpawnGroup with matching spawnGroup only uses these. Empty = usable by any group.")]
    public string group = "";

    // INPUT:  none
    // OUTPUT: world position
    // USE:    EnemySpawner.Spawn
    public Vector2 Position => transform.position;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawLine(transform.position + Vector3.left * 0.4f, transform.position + Vector3.right * 0.4f);
    }
}