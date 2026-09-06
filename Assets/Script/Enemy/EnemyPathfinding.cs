using System.Collections.Generic;
using UnityEngine;

public class EnemyPathfinding : MonoBehaviour
{
    [Header("Steering Settings")]
    [SerializeField] private float cornerArriveThreshold = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool drawPath = true;

    private List<Vector2> currentPath = new List<Vector2>();

    public bool HasPath => currentPath != null && currentPath.Count > 0;

    public bool ComputePath(Vector3 targetWorldPos)
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager instance not found in scene!");
            return false;
        }

        currentPath = GridManager.Instance.FindPath(transform.position, targetWorldPos);
        return HasPath;
    }

    // Pops path corners as the enemy reaches them along arbitrary slopes
    public Vector2 GetCurrentSteeringTarget(Vector2 fallback)
    {
        if (currentPath == null || currentPath.Count == 0) 
            return fallback;

        // Only advance to the next node if we are close to the CURRENT target node
        if (Vector2.Distance(transform.position, currentPath[0]) < cornerArriveThreshold)
        {
            currentPath.RemoveAt(0);
        }

        // Return the active node if available; only use fallback if path is fully traversed
        return currentPath.Count > 0 ? currentPath[0] : fallback;
    }

    // Aliases to support both naming conventions seamlessly
    public Vector2 GetNextWaypoint(Vector2 fallback) => GetCurrentSteeringTarget(fallback);
    public Vector2 GetNextWayPoint(Vector2 fallback) => GetCurrentSteeringTarget(fallback);

    public void ClearPath() => currentPath?.Clear();

    private void OnDrawGizmos()
    {
        if (drawPath && currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentPath[0]);

            for (int i = 0; i < currentPath.Count - 1; i++)
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);

            foreach (var pt in currentPath)
                Gizmos.DrawSphere(pt, 0.15f);
        }
    }
}