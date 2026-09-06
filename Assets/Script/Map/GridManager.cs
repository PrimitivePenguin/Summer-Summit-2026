using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Layout")]
    [SerializeField] private Vector2 origin = new Vector2(3.5f, -5.5f);
    [SerializeField] private int width = 50;
    [SerializeField] private int height = 50;
    [SerializeField] private float cellSize = 1f;

    [Header("Agent Size & Collision")]
    [SerializeField] private float agentWidth = 0.8f;
    [SerializeField] private float skinWidth = 0.05f;
    [SerializeField] private LayerMask obstacleMask;

    private bool dirty;
    public void MarkDirty() => dirty = true;
    private void LateUpdate() { if (dirty) { dirty = false; BuildWalkableGrid(); } }   // batched: 10 crates dying in one frame = one rebuild
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private bool[,] walkableGrid;
    private static readonly Vector2Int[] CardinalDirs = {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0)
    };

    private void Awake()
    {
        Instance = this;
        BuildWalkableGrid();
    }


    // ── Pathfinding ─────────────────────────────────
    public void BuildWalkableGrid()
    {
        walkableGrid = new bool[width, height];
        Vector2 checkSize = Vector2.one * Mathf.Max(0.1f, agentWidth - skinWidth);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 worldPos = GridToWorld(new Vector2Int(x, y));
                Collider2D hit = Physics2D.OverlapBox(worldPos, checkSize, 0f, obstacleMask);
                walkableGrid[x, y] = (hit == null);
            }
        }
    }

    public bool IsInBounds(Vector2Int pos) => pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;

    public bool IsWalkable(Vector2Int pos) => IsInBounds(pos) && walkableGrid[pos.x, pos.y];

    public Vector2 GridToWorld(Vector2Int gridPos)
    {
        float x = origin.x + (gridPos.x - (width - 1) * 0.5f) * cellSize;
        float y = origin.y + (gridPos.y - (height - 1) * 0.5f) * cellSize;
        return new Vector2(x, y);
    }

    public Vector2Int WorldToGrid(Vector2 worldPos)
    {
        Vector2 local = worldPos - origin;
        int x = Mathf.RoundToInt(local.x / cellSize + (width - 1) * 0.5f);
        int y = Mathf.RoundToInt(local.y / cellSize + (height - 1) * 0.5f);
        return new Vector2Int(x, y);
    }

    private Vector2Int GetNearestWalkable(Vector2Int cell)
    {
        if (IsWalkable(cell)) return cell;

        for (int r = 1; r <= 3; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    Vector2Int neighbor = new Vector2Int(cell.x + dx, cell.y + dy);
                    if (IsWalkable(neighbor)) return neighbor;
                }
            }
        }
        return cell;
    }

    public List<Vector2> FindPath(Vector2 startWorld, Vector2 goalWorld)
    {
        Vector2Int start = GetNearestWalkable(WorldToGrid(startWorld));
        Vector2Int goal = GetNearestWalkable(WorldToGrid(goalWorld));

        if (!IsInBounds(start) || !IsInBounds(goal)) return null;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> parentMap = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);
        bool reached = false;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == goal)
            {
                reached = true;
                break;
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2Int next = current + CardinalDirs[i];

                if (IsInBounds(next) && !visited.Contains(next) && walkableGrid[next.x, next.y])
                {
                    visited.Add(next);
                    parentMap[next] = current;
                    queue.Enqueue(next);
                }
            }
        }

        if (!reached) return null;

        List<Vector2> rawPath = new List<Vector2>();
        Vector2Int curr = goal;
        while (curr != start)
        {
            rawPath.Add(GridToWorld(curr));
            curr = parentMap[curr];
        }
        rawPath.Add(startWorld);
        rawPath.Reverse();
        rawPath[rawPath.Count - 1] = goalWorld;

        return SmoothPath(rawPath);
    }

    private List<Vector2> SmoothPath(List<Vector2> path)
    {
        if (path == null || path.Count <= 2) return path;

        List<Vector2> smoothed = new List<Vector2> { path[0] };
        Vector2 checkSize = Vector2.one * Mathf.Max(0.1f, agentWidth - skinWidth);
        int current = 0;

        while (current < path.Count - 1)
        {
            int furthest = current + 1;

            for (int candidate = current + 2; candidate < path.Count; candidate++)
            {
                Vector2 from = path[current];
                Vector2 to = path[candidate];
                Vector2 diff = to - from;
                float dist = diff.magnitude;

                if (dist < 0.001f) continue;

                RaycastHit2D hit = Physics2D.BoxCast(from, checkSize, 0f, diff.normalized, dist, obstacleMask);
                if (hit.collider == null)
                {
                    furthest = candidate;
                }
                else
                {
                    break;
                }
            }

            smoothed.Add(path[furthest]);
            current = furthest;
        }

        return smoothed;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || walkableGrid == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Gizmos.color = walkableGrid[x, y] 
                    ? new Color(0f, 1f, 0f, 0.12f) 
                    : new Color(1f, 0f, 0f, 0.28f);

                Gizmos.DrawCube(GridToWorld(new Vector2Int(x, y)), Vector3.one * (cellSize * 0.9f));
            }
        }
    }
}