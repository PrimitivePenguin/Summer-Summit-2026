using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PatrolRoute))]
public class PatrolRouteEditor : Editor
{
    private static bool placementMode = false;
    

    void OnSceneGUI()
    {
        PatrolRoute route = (PatrolRoute)target;
        if (route.waypoints == null) return;

        // Always draw handles for existing points
        DrawWaypointHandles(route);

        if (!placementMode) return;

        // Show a label so user knows placement mode is active
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 220, 30));
        GUILayout.Label("PLACEMENT MODE — Click to add point", EditorStyles.helpBox);
        GUILayout.EndArea();
        Handles.EndGUI();

        // Block all default tools while in placement mode
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        Event e = Event.current;

        // Left click (no shift needed in placement mode)
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector3 worldPos = GetWorldPosition(e.mousePosition);
            if (worldPos != Vector3.positiveInfinity)
            {
                Undo.RecordObject(route, "Add Patrol Point");
                AddWaypoint(route, worldPos);
                EditorUtility.SetDirty(route);
            }
            e.Use();
        }

        // Right click or Escape = exit placement mode
        if ((e.type == EventType.MouseDown && e.button == 1) ||
            (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
        {
            placementMode = false;
            e.Use();
            Repaint();
        }

        // Force scene to repaint so the label stays visible
        SceneView.RepaintAll();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PatrolRoute route = (PatrolRoute)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Patrol Route Tools", EditorStyles.boldLabel);

        // Big toggle button for placement mode
        GUI.backgroundColor = placementMode ? Color.red : Color.green;
        if (GUILayout.Button(placementMode ? "STOP Placing Points (Esc)" : "START Placing Points", GUILayout.Height(30)))
        {
            placementMode = !placementMode;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        if (placementMode)
            EditorGUILayout.HelpBox("Click anywhere in Scene View to place a waypoint.\nRight-click or Esc to stop.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox("Press the green button above, then click in Scene View to place waypoints.\nDrag yellow handles to reposition.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = route.waypoints != null && route.waypoints.Length > 0;
        if (GUILayout.Button("Remove Last"))
        {
            Undo.RecordObject(route, "Remove Last Patrol Point");
            RemoveLast(route);
            EditorUtility.SetDirty(route);
        }
        GUI.enabled = true;

        GUI.enabled = route.waypoints != null && route.waypoints.Length > 1;
        if (GUILayout.Button("Reverse Order"))
        {
            Undo.RecordObject(route, "Reverse Patrol Route");
            System.Array.Reverse(route.waypoints);
            EditorUtility.SetDirty(route);
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear All"))
        {
            if (EditorUtility.DisplayDialog("Clear Patrol Route", "Delete all waypoints?", "Clear", "Cancel"))
            {
                Undo.RecordObject(route, "Clear Patrol Route");
                route.waypoints = new PatrolWayPoint[0];
                EditorUtility.SetDirty(route);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            $"Waypoint count: {(route.waypoints == null ? 0 : route.waypoints.Length)}",
            EditorStyles.miniLabel);
    }

    void DrawWaypointHandles(PatrolRoute route)
    {
        for (int i = 0; i < route.waypoints.Length; i++)
        {
            Vector3 pos = route.GetCenter(i);   // local → world for display

            EditorGUI.BeginChangeCheck();
            Handles.color = Color.yellow;
            Vector3 newPos = Handles.FreeMoveHandle(pos, 0.3f, Vector3.zero, Handles.SphereHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(route, "Move Patrol Point");
                newPos.z = 0f;
                route.waypoints[i].localPosition = route.transform.InverseTransformPoint(newPos);
                EditorUtility.SetDirty(route);
            }
        }
    }

    Vector3 GetWorldPosition(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);

        // 2D mode
        if (SceneView.currentDrawingSceneView != null &&
            SceneView.currentDrawingSceneView.in2DMode)
        {
            return new Vector3(ray.origin.x, ray.origin.y, 0f);
        }

        // Perspective: intersect with Z=0 plane
        if (Mathf.Abs(ray.direction.z) > 0.001f)
        {
            float t = -ray.origin.z / ray.direction.z;
            Vector3 pos = ray.origin + ray.direction * t;
            pos.z = 0f;
            return pos;
        }

        return Vector3.positiveInfinity;
    }

    void AddWaypoint(PatrolRoute route, Vector3 worldPos)
    {
        int oldLen = route.waypoints == null ? 0 : route.waypoints.Length;
        PatrolWayPoint[] newPoints = new PatrolWayPoint[oldLen + 1];
        if (route.waypoints != null)
            System.Array.Copy(route.waypoints, newPoints, oldLen);

        newPoints[oldLen] = new PatrolWayPoint {
            name = $"Waypoint {oldLen}",
            localPosition = route.transform.InverseTransformPoint(worldPos),
            radius = 0f
        };

        route.waypoints = newPoints;
        ReindexWaypoints(route);
    }

    void RemoveLast(PatrolRoute route)
    {
        if (route.waypoints == null || route.waypoints.Length == 0) return;
        int newLen = route.waypoints.Length - 1;
        PatrolWayPoint[] newPoints = new PatrolWayPoint[newLen];
        System.Array.Copy(route.waypoints, newPoints, newLen);
        route.waypoints = newPoints;
        ReindexWaypoints(route);
    }

    private void ReindexWaypoints(PatrolRoute route)
    {
        if (route.waypoints == null) return;
        for (int i = 0; i < route.waypoints.Length; i++)
        {
            route.waypoints[i].name = $"Waypoint {i}";
        }
    }
}