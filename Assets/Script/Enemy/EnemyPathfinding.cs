using UnityEngine;
using System;
using System.Collections.Generic;

// Runs A* on grid centered around GameObject (usually enemy)
// Grid moves with enemy


[RequireComponent(typeof(EnemyMovement))]
public class EnemyPathfinding : MonoBehaviour
{
    public struct Pair
    {
        public int first, second;

        public Pair(int first, int second)
        {
            this.first = first;
            this.second = second;
        }
    }

    private class Cell
    {
        public int parent_i = -1, parent_j = -1;
        public double f = double.MaxValue, g = double.MaxValue, h = double.MaxValue;
    }

    // Minimal binary min-heap (stands in for System.Collections.Generic.PriorityQueue)
    private class MinHeap<TElement>
    {
        private readonly List<(TElement element, double priority)> heap = new();

        public int Count => heap.Count;

        public void Enqueue(TElement element, double priority)
        {
            heap.Add((element, priority));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].priority <= heap[i].priority) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        public TElement Dequeue()
        {
            var root = heap[0].element;
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            int i = 0, n = heap.Count;
            while (true)
            {
                int l = 2 * i + 1, r = 2 * i + 2, smallest = i;
                if (l < n && heap[l].priority < heap[smallest].priority) smallest = l;
                if (r < n && heap[r].priority < heap[smallest].priority) smallest = r;
                if (smallest == i) break;
                (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
                i = smallest;
            }
            return root;
        }
    }

    [Header("Grid")]
    [SerializeField] private int ROW = 15;
    [SerializeField] private int COL = 15;
    [SerializeField] private float cellSize = 1f;

    [Header("Collision")]
    [SerializeField] public LayerMask collisionMask;
    [SerializeField] private float collisionRadius = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private bool drawPath = true;

    private List<Vector2> currentPath = new List<Vector2>();

    private void Awake()
    {
        if (collisionMask == 0)
            collisionMask = LayerMask.GetMask("Collision");
    }

    // Grid -> World coordinate conversion (parent-centered)

    private Vector3 GridToWorld(int row, int col)
    {
        float xOffset = (col - (COL - 1) * 0.5f) * cellSize;
        float yOffset = ((ROW - 1) * 0.5f - row) * cellSize;
        return transform.position + new Vector3(xOffset, yOffset, 0f);
    }

    // World -> Grid coordinate conversion
    // USE: convert enemy pos into grid coordinates for A* search
    private Pair WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position;
        int col = Mathf.RoundToInt(local.x / cellSize + (COL - 1) * 0.5f);
        int row = Mathf.RoundToInt((ROW - 1) * 0.5f - local.y / cellSize);
        return new Pair(row, col);
    }

    // Check whether cell is within grid bound via obstacle collider
    private bool IsValid(int row, int col)
    {
        return row >= 0 && row < ROW && col >= 0 && col < COL;
    }

    // Check whether cell is blocked (instance method, not static)
    private bool IsUnBlocked(int row, int col)
    {
        Vector3 samplePos = GridToWorld(row, col);  // world space
        Collider2D[] hits = Physics2D.OverlapCircleAll(samplePos, collisionRadius, collisionMask);

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.CompareTag("Enemy") || hit.CompareTag("Player")) continue;
            return false;  // obstacle found
        }
        return true;
    }

    private bool IsDestination(int row, int col, Pair dest)
    {
        return row == dest.first && col == dest.second;
    }

    private double CalculateHValue(int row, int col, Pair dest)
    {
        return Math.Sqrt(
            (row - dest.first) * (row - dest.first)
            + (col - dest.second) * (col - dest.second));
    }

    // Path reconstruction: returns list of world-space waypoints
    // INPUT: all Cell grid, parentlinks, destination
    // OUTPUT: List <Vector2>
    //      World-space waypoints from source to destination, in order
    private List<Vector2> TracePath(Cell[,] cellDetails, Pair dest)
    {
        int row = dest.first;
        int col = dest.second;
        Stack<Pair> stack = new Stack<Pair>();

        while (!(cellDetails[row, col].parent_i == row && cellDetails[row, col].parent_j == col))
        {
            stack.Push(new Pair(row, col));
            int tempRow = cellDetails[row, col].parent_i;
            int tempCol = cellDetails[row, col].parent_j;
            row = tempRow;
            col = tempCol;
        }
        stack.Push(new Pair(row, col));

        List<Vector2> path = new List<Vector2>();
        while (stack.Count > 0)
        {
            Pair p = stack.Pop();
            path.Add(GridToWorld(p.first, p.second));  // convert to world space
        }
        return path;
    }

    // A* pathfinding algorithm
    private List<Vector2> AStarSearch(Pair src, Pair dest)
    {
        if (!IsValid(src.first, src.second))
        {
            Debug.LogWarning("Source is invalid");
            return null;
        }

        if (!IsValid(dest.first, dest.second))
        {
            Debug.LogWarning("Destination is invalid");
            return null;
        }

        if (!IsUnBlocked(src.first, src.second) || !IsUnBlocked(dest.first, dest.second))
        {
            Debug.LogWarning("Source or destination is blocked");
            return null;
        }

        if (IsDestination(src.first, src.second, dest))
        {
            Debug.LogWarning("Already at destination");
            return null;
        }

        bool[,] closedList = new bool[ROW, COL];
        Cell[,] cellDetails = new Cell[ROW, COL];

        for (int i = 0; i < ROW; i++)
            for (int j = 0; j < COL; j++)
                cellDetails[i, j] = new Cell();

        int row = src.first, col = src.second;
        cellDetails[row, col].f = 0;
        cellDetails[row, col].g = 0;
        cellDetails[row, col].h = 0;
        cellDetails[row, col].parent_i = row;
        cellDetails[row, col].parent_j = col;

        MinHeap<Pair> openList = new MinHeap<Pair>();
        openList.Enqueue(new Pair(row, col), 0.0);

        int[] dRow = { -1, 1, 0, 0, -1, -1, 1, 1 };
        int[] dCol = { 0, 0, 1, -1, 1, -1, 1, -1 };

        while (openList.Count > 0)
        {
            Pair p = openList.Dequeue();
            row = p.first;
            col = p.second;

            if (closedList[row, col]) continue;
            closedList[row, col] = true;

            for (int dir = 0; dir < 8; dir++)
            {
                int newRow = row + dRow[dir];
                int newCol = col + dCol[dir];

                if (!IsValid(newRow, newCol)) continue;

                if (IsDestination(newRow, newCol, dest))
                {
                    cellDetails[newRow, newCol].parent_i = row;
                    cellDetails[newRow, newCol].parent_j = col;
                    return TracePath(cellDetails, dest);
                }

                if (!closedList[newRow, newCol] && IsUnBlocked(newRow, newCol))
                {
                    double gNew = cellDetails[row, col].g
                        + (Math.Abs(dRow[dir]) + Math.Abs(dCol[dir]) == 2 ? 1.414 : 1.0);
                    double hNew = CalculateHValue(newRow, newCol, dest);
                    double fNew = gNew + hNew;

                    if (cellDetails[newRow, newCol].f == double.MaxValue
                        || cellDetails[newRow, newCol].f > fNew)
                    {
                        openList.Enqueue(new Pair(newRow, newCol), fNew);
                        cellDetails[newRow, newCol].f = fNew;
                        cellDetails[newRow, newCol].g = gNew;
                        cellDetails[newRow, newCol].h = hNew;
                        cellDetails[newRow, newCol].parent_i = row;
                        cellDetails[newRow, newCol].parent_j = col;
                    }
                }
            }
        }
        return null;  // no path found
    }

    // Public API: compute path from enemy to target

    // INPUT: world-space target position
    // OUTPUT: return results as currentPath
    // USE: EnemyMovement.MoveTowardSmart() on timer
    public List<Vector2> ComputePath(Vector3 targetWorldPos)
    {
        Pair src = WorldToGrid(transform.position);
        Pair dest = WorldToGrid(targetWorldPos);
        currentPath = AStarSearch(src, dest);
        return currentPath;
    }

    // Get next waypoint for movement
    public Vector2 GetNextWaypoint(Vector2 fallback)
    {
        if (currentPath != null && currentPath.Count > 1)
            return currentPath[1];
        else if (currentPath != null && currentPath.Count == 1)
            return currentPath[0];
        else
            return fallback;  // no path, head straight at target
    }

    // Debug drawing
    private void OnDrawGizmos()
    {
        if (drawGrid)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            for (int r = 0; r < ROW; r++)
                for (int c = 0; c < COL; c++)
                    Gizmos.DrawWireCube(GridToWorld(r, c), Vector3.one * cellSize * 0.9f);
        }

        if (drawPath && currentPath != null && currentPath.Count > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < currentPath.Count - 1; i++)
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            foreach (var wp in currentPath)
                Gizmos.DrawSphere(wp, 0.1f);
        }

        // Draw last known position from EnemyVision
        EnemyVision vision = GetComponent<EnemyVision>();
        if (vision != null && vision.awareness > 0f)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.8f);  // yellow
            Gizmos.DrawWireSphere(vision.lastKnownPosition, 0.3f);
            Gizmos.DrawLine(transform.position, vision.lastKnownPosition);
        }
    }
}