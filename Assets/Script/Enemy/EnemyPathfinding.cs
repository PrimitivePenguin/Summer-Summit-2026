using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(EnemyMovement))]
public class EnemyPathfinding : MonoBehaviour
{
    public struct Pair
    {
        public int first, second;
        public Pair(int first, int second) { this.first = first; this.second = second; }
    }

    private class Cell
    {
        public int parent_i = -1, parent_j = -1;
        public double f = double.MaxValue, g = double.MaxValue, h = double.MaxValue;
    }

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

    [Header("World Grid Settings")]
    [SerializeField] private int ROW = 40;
    [SerializeField] private int COL = 40;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector2 gridOrigin = Vector2.zero; // World center of your arena

    [Header("Collision")]
    [SerializeField] public LayerMask collisionMask;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float nodeArriveThreshold = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool drawPath = true;

    private List<Vector2> currentPath = new List<Vector2>();

    private void Awake()
    {
        if (collisionMask == 0)
            collisionMask = LayerMask.GetMask("Collision");
    }

    public bool HasPath => currentPath != null && currentPath.Count > 0;

    private Vector3 GridToWorld(int row, int col)
    {
        float x = gridOrigin.x + (col - (COL - 1) * 0.5f) * cellSize;
        float y = gridOrigin.y + ((ROW - 1) * 0.5f - row) * cellSize;
        return new Vector3(x, y, 0f);
    }

    private Pair WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - (Vector3)gridOrigin;
        int col = Mathf.RoundToInt(local.x / cellSize + (COL - 1) * 0.5f);
        int row = Mathf.RoundToInt((ROW - 1) * 0.5f - local.y / cellSize);
        return new Pair(row, col);
    }

    private bool IsValid(int row, int col) => row >= 0 && row < ROW && col >= 0 && col < COL;

    private bool IsUnBlocked(int row, int col)
    {
        Vector3 samplePos = GridToWorld(row, col);
        Collider2D[] hits = Physics2D.OverlapCircleAll(samplePos, collisionRadius, collisionMask);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.CompareTag("Enemy") || hit.CompareTag("Player")) continue;
            return false;
        }
        return true;
    }

    // Finds the nearest open node if a point was placed slightly inside a wall
    private Pair GetNearestOpenCell(Pair cell)
    {
        if (IsValid(cell.first, cell.second) && IsUnBlocked(cell.first, cell.second))
            return cell;

        for (int r = 1; r <= 3; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int nr = cell.first + dx;
                    int nc = cell.second + dy;
                    if (IsValid(nr, nc) && IsUnBlocked(nr, nc))
                        return new Pair(nr, nc);
                }
            }
        }
        return cell;
    }

    public bool ComputePath(Vector3 targetWorldPos)
    {
        Pair src = GetNearestOpenCell(WorldToGrid(transform.position));
        Pair dest = GetNearestOpenCell(WorldToGrid(targetWorldPos));

        // Pass targetWorldPos down into AStarSearch
        currentPath = AStarSearch(src, dest, targetWorldPos);
        return currentPath != null && currentPath.Count > 0;
    }

    private List<Vector2> AStarSearch(Pair src, Pair dest, Vector3 exactDestination)
    {
        if (!IsValid(src.first, src.second) || !IsValid(dest.first, dest.second)) return null;
        if (!IsUnBlocked(src.first, src.second) || !IsUnBlocked(dest.first, dest.second)) return null;
        if (src.first == dest.first && src.second == dest.second) return new List<Vector2> { exactDestination };

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
                int nRow = row + dRow[dir];
                int nCol = col + dCol[dir];

                if (!IsValid(nRow, nCol)) continue;

                if (nRow == dest.first && nCol == dest.second)
                {
                    cellDetails[nRow, nCol].parent_i = row;
                    cellDetails[nRow, nCol].parent_j = col;
                    return TracePath(cellDetails, dest, exactDestination);
                }

                if (!closedList[nRow, nCol] && IsUnBlocked(nRow, nCol))
                {
                    double gNew = cellDetails[row, col].g + (Math.Abs(dRow[dir]) + Math.Abs(dCol[dir]) == 2 ? 1.414 : 1.0);
                    double hNew = Math.Sqrt(Math.Pow(nRow - dest.first, 2) + Math.Pow(nCol - dest.second, 2));
                    double fNew = gNew + hNew;

                    if (cellDetails[nRow, nCol].f > fNew)
                    {
                        openList.Enqueue(new Pair(nRow, nCol), fNew);
                        cellDetails[nRow, nCol].f = fNew;
                        cellDetails[nRow, nCol].g = gNew;
                        cellDetails[nRow, nCol].h = hNew;
                        cellDetails[nRow, nCol].parent_i = row;
                        cellDetails[nRow, nCol].parent_j = col;
                    }
                }
            }
        }
        return null;
    }

    private List<Vector2> TracePath(Cell[,] cellDetails, Pair dest, Vector3 exactDestination)
    {
        int row = dest.first;
        int col = dest.second;
        Stack<Vector2> stack = new Stack<Vector2>();

        // Exact world target is pushed first so it ends up as the final destination
        stack.Push(exactDestination);

        while (!(cellDetails[row, col].parent_i == row && cellDetails[row, col].parent_j == col))
        {
            stack.Push(GridToWorld(row, col));
            int tempRow = cellDetails[row, col].parent_i;
            int tempCol = cellDetails[row, col].parent_j;
            row = tempRow;
            col = tempCol;
        }

        List<Vector2> path = new List<Vector2>();
        while (stack.Count > 0) path.Add(stack.Pop());
        return path;
    }

    // Pops nodes along the path as the enemy reaches them
    public Vector2 GetCurrentSteeringTarget(Vector2 fallback)
    {
        if (currentPath == null || currentPath.Count == 0) return fallback;

        while (currentPath.Count > 0 && Vector2.Distance(transform.position, currentPath[0]) < nodeArriveThreshold)
        {
            currentPath.RemoveAt(0);
        }

        return currentPath.Count > 0 ? currentPath[0] : fallback;
    }

    public Vector2 GetNextWaypoint(Vector2 fallback) => GetCurrentSteeringTarget(fallback);

    private void OnDrawGizmos()
    {
        if (drawPath && currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < currentPath.Count - 1; i++)
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            foreach (var wp in currentPath)
                Gizmos.DrawSphere(wp, 0.1f);
        }
    }
}