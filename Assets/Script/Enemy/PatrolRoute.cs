using UnityEngine;

/// <summary>
/// Stores a list of world-space patrol waypoints on this GameObject.
/// Points are plain Vector3 positions — no child GameObjects needed.
/// 
/// HOW TO SET UP:
///   1. Create empty GameObject in scene → name it "PatrolRoute"
///   2. Add this component
///   3. Select it and Shift+Click in scene view to place waypoints
///   4. Drag from Hierarchy into Assets/Prefabs/ → prefab created
///   5. Drag the scene instance into Enemy's EnemyMovement → "Patrol Route" slot
/// 
/// HOW TO USE PER-ENEMY:
///   One PatrolRoute per enemy: each enemy gets its own instance in the scene.
///   Shared route: drag the same PatrolRoute into multiple enemies' slots.
/// </summary>
[System.Serializable]
public class PatrolWayPoint
{
    public string name = "Waypoint";

    public Vector3 localPosition;   // LOCAL to the PatrolRoute transform — enables prefab reuse
    public float radius = 0f;       // 0 = exact point, >0 = sample anywhere in the circle
}
public class PatrolRoute : MonoBehaviour
{
    [Header("Waypoints")]
    [SerializeField] public PatrolWayPoint[] waypoints = new PatrolWayPoint[0];

    [Header("Map Bounds (optional)")]
    [Tooltip("Sampled points are kept inside this collider. Leave null to skip the check.")]
    [SerializeField] private PolygonCollider2D mapBounds;
    [SerializeField] private int maxSampleAttempts = 8;

    [Header("Visualization")]
    [SerializeField] private bool toggleVisualization = true;
    [SerializeField] private Color routeColor = Color.cyan;
    [SerializeField] private float waypointRadius = 0.2f;   // gizmo dot size, NOT the sample radius
    [SerializeField] private bool showLabels = true;

    public int WaypointCount => waypoints == null ? 0 : waypoints.Length;

    // Applies transform.TransformPoint so moving the PatrolRoute object moves every point
    public Vector3 GetCenter(int i) => transform.TransformPoint(waypoints[i].localPosition);

    // INPUT: waypoint index, mapBounds, maxSampleAttempts
    // OUTPUT: world position of the waypoint, sampled inside radius if >0 and inside mapBounds else ret center
    public Vector3 SampleWaypoint(int i)
    {
        if (waypoints == null || i < 0 || i >= waypoints.Length) return transform.position;

        Vector3 center = GetCenter(i);
        float r = waypoints[i].radius;
        if (r <= 0f) return center;

        for (int attempt = 0; attempt < maxSampleAttempts; attempt++)
        {
            Vector2 candidate = (Vector2)center + Random.insideUnitCircle * r;
            if (mapBounds == null || mapBounds.OverlapPoint(candidate))
                return candidate;
        }

        // Every sample landed outside the map — fall back to the center
        return center;
    }

    public string GetName(int i) =>
        (waypoints != null && i >= 0 && i < waypoints.Length) ? waypoints[i].name : "";

    // Toggleable

    void OnDrawGizmos()
    {
        if (!toggleVisualization) return;
        if (waypoints == null || waypoints.Length == 0) return;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 point = GetCenter(i);
            Vector3 next  = GetCenter((i + 1) % waypoints.Length);

            Gizmos.color = routeColor;
            Gizmos.DrawSphere(point, waypointRadius);

            // Wire circle showing the sample area for this waypoint
            if (waypoints[i].radius > 0f)
            {
                Gizmos.color = new Color(routeColor.r, routeColor.g, routeColor.b, 0.35f);
                Gizmos.DrawWireSphere(point, waypoints[i].radius);
            }

            Gizmos.color = new Color(routeColor.r, routeColor.g, routeColor.b, 0.5f);
            Gizmos.DrawLine(point, next);

    #if UNITY_EDITOR
            if (showLabels)
                UnityEditor.Handles.Label(point + Vector3.up * 0.4f, $"  {i}: {waypoints[i].name}",
                    new GUIStyle { normal = { textColor = routeColor }, fontStyle = FontStyle.Bold });
    #endif
        }
    }
}