using UnityEngine;

// Stores world-space patrol waypoints (as local offsets) on this GameObject.

[System.Serializable]
public class PatrolWayPoint
{
    public string name = "Waypoint";
    public Vector3 localPosition;   // LOCAL to the PatrolRoute transform
    public float radius = 0f;       // 0 = exact point, >0 = sample inside circle
}

public class PatrolRoute : MonoBehaviour
{
    [Header("Waypoints")]
    [SerializeField] public PatrolWayPoint[] waypoints = new PatrolWayPoint[0];

    [Header("Map Bounds (optional)")]
    [SerializeField] private PolygonCollider2D mapBounds;
    [SerializeField] private int maxSampleAttempts = 8;

    [Header("Visualization")]
    [SerializeField] private bool toggleVisualization = true;
    [SerializeField] private Color routeColor = Color.cyan;
    [SerializeField] private float waypointRadius = 0.2f;
    [SerializeField] private bool showLabels = true;
    

    // INPUT:  none
    // OUTPUT: number of REAL waypoints — valid indices are 0 .. WaypointCount-1
    // USE:    every loop and modulo in EnemyMovement. (REPLACES: waypoints.Length + 1)
    public int WaypointCount => waypoints != null ? waypoints.Length : 0;

    // INPUT:  waypoint index
    // OUTPUT: true if index addresses a real waypoint
    // USE:    guard before GetCenter / SampleWaypoint
    public bool IsValidIndex(int i) => waypoints != null && i >= 0 && i < waypoints.Length;

    // INPUT:  waypoint index
    // OUTPUT: world-space centre of that waypoint (transform.position if index invalid)
    // USE:    ResumePatrolFromNearest, gizmos, editor handles. (REPLACES: unguarded indexer)
    public Vector3 GetCenter(int i) =>
        IsValidIndex(i) ? transform.TransformPoint(waypoints[i].localPosition) : transform.position;

    // INPUT:  waypoint index
    // OUTPUT: world position inside that waypoint's radius (and inside mapBounds if set),
    //         or the centre if radius is 0 / all samples fell outside the map
    // USE:    SetPatrolTarget — the actual point the enemy walks to
    public Vector3 SampleWaypoint(int i)
    {
        if (!IsValidIndex(i)) return transform.position;

        Vector3 center = GetCenter(i);
        float r = waypoints[i].radius;
        if (r <= 0f) return center;

        for (int attempt = 0; attempt < maxSampleAttempts; attempt++)
        {
            Vector2 candidate = (Vector2)center + Random.insideUnitCircle * r;
            if (mapBounds == null || mapBounds.OverlapPoint(candidate))
                return candidate;
        }
        return center;
    }

    // INPUT:  waypoint index
    // OUTPUT: designer-facing name, or "" if invalid
    // USE:    debug labels
    public string GetName(int i) => IsValidIndex(i) ? waypoints[i].name : "";

    // INPUT:  none
    // OUTPUT: none — Scene view only
    // USE:    draws points, sample circles, route lines, index labels
    void OnDrawGizmos()
    {
        if (!toggleVisualization || waypoints == null || waypoints.Length == 0) return;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 point = GetCenter(i);
            Vector3 next  = GetCenter((i + 1) % waypoints.Length);

            Gizmos.color = routeColor;
            Gizmos.DrawSphere(point, waypointRadius);

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