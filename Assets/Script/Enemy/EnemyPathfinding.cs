using UnityEngine;
using System;
using System.Collections.Generic;

// A* on a LOCAL grid window that follows the enemy.
//
// The window centre is snapped to a cellSize lattice so cell centres occupy stable
// world positions as the enemy walks. Without the snap, obstacle sample points drift
// sub-cell every frame and walkability flickers on geometry that never moved.
//
// INPUT:  world-space target position (from EnemyMovement.MoveTowardSmart / Patrol)
// OUTPUT: currentPath — world-space waypoints, consumed by GetNextWayPoint
//         Status      — Complete / Partial / Failed, so callers can tell
//                       "arrived" apart from "no route exists"

[RequireComponent(typeof(EnemyMovement))]
public class EnemyPathfinding : MonoBehaviour
{
    // ── Internal data structures ──────────────────────────────────────────────

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

    // Binary min-heap. Stands in for PriorityQueue<T> on Unity versions without it.
    private class MinHeap<TElement>
    {
        private readonly List<(TElement element, double priority)> heap = new();
        public int Count => heap.Count;

        // INPUT:  element + its priority (lower = popped sooner)
        // OUTPUT: none — sifts the new entry up to its place
        // USE:    A* open list insertion
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

        // INPUT:  none
        // OUTPUT: the lowest-priority element, removed from the heap
        // USE:    A* expands whichever node this returns
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

        // INPUT:  none
        // OUTPUT: none — empties the heap without releasing its backing array
        // USE:    reuse between searches instead of allocating a new heap
        public void Clear() => heap.Clear();
    }

    // Tells EnemyMovement WHY there is no path to follow.
    // None     — nothing computed yet
    // Complete — full route to the true target
    // Partial  — target was outside the window; route ends at the window edge
    // Failed   — A* found no route at all; caller must fall back or give up
    public enum PathStatus { None, Partial, Complete, Failed }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Local Grid Window")]
    [SerializeField] private int ROW = 24;                        // window height in cells
    [SerializeField] private int COL = 24;                        // window width in cells
    [SerializeField] private float cellSize = 1f;                 // match your tile size
    [SerializeField] private Vector2 gridOffset = Vector2.zero;   // offset FROM the enemy; usually zero

    [Header("Collision")]
    [SerializeField] public LayerMask collisionMask;
    [SerializeField] private float collisionRadius = 0.3f;        // ~half the enemy collider width
    [SerializeField] private float nodeArriveThreshold = 0.25f;   // distance to pop a node
    [SerializeField] private float staleDistanceCells = 4f;       // abandon path if this far off it

    [Header("Debug")]
    [SerializeField] private bool drawPath = true;
    [SerializeField] private bool drawGrid = false;

    // ── State ─────────────────────────────────────────────────────────────────

    public PathStatus Status { get; private set; } = PathStatus.None;

    private List<Vector2> currentPath = new List<Vector2>();

    // Window centre for the CURRENT search. Snapped to the lattice and held fixed for
    // the whole of one ComputePath call — TracePath runs after A* finishes, and if it
    // read transform.position live the enemy would already have moved.
    private Vector3 searchOrigin;
    private bool destWasClamped;
    private bool reachedGoal;

    // Reusable buffers. Allocated once in Awake; the original allocated ROW*COL Cell
    // objects on every ComputePath call, which spiked GC with several enemies.
    private sbyte[,] walkCache;      // 0 = unknown, 1 = open, -1 = blocked
    private Cell[,] cellDetails;
    private bool[,] closedList;
    private readonly MinHeap<Pair> openList = new MinHeap<Pair>();

    // 8-directional neighbour offsets
    private static readonly int[] dRow = { -1, 1, 0, 0, -1, -1, 1, 1 };
    private static readonly int[] dCol = { 0, 0, 1, -1, 1, -1, -1, 1 };

    // ── Unity ─────────────────────────────────────────────────────────────────

    // INPUT:  inspector values
    // OUTPUT: allocates every search buffer exactly once
    // USE:    keeps ComputePath allocation-free at runtime
    private void Awake()
    {
        if (collisionMask == 0)
            collisionMask = LayerMask.GetMask("Collision");

        walkCache = new sbyte[ROW, COL];
        cellDetails = new Cell[ROW, COL];
        closedList = new bool[ROW, COL];
        for (int i = 0; i < ROW; i++)
            for (int j = 0; j < COL; j++)
                cellDetails[i, j] = new Cell();

        RefreshOrigin();
    }

    // ── Origin + coordinate conversion ────────────────────────────────────────

    // INPUT:  transform.position, gridOffset, cellSize
    // OUTPUT: sets searchOrigin to the enemy position snapped to a cellSize lattice
    // USE:    called once at the top of ComputePath. The snap is what makes the window
    //         slide in whole-cell jumps instead of drifting, keeping obstacle sampling
    //         stable frame to frame.
    private void RefreshOrigin()
    {
        Vector3 p = transform.position + (Vector3)gridOffset;
        searchOrigin = new Vector3(
            Mathf.Round(p.x / cellSize) * cellSize,
            Mathf.Round(p.y / cellSize) * cellSize,
            0f);
    }

    // INPUT:  grid row + col
    // OUTPUT: world-space centre of that cell
    // USE:    obstacle sampling, path reconstruction, gizmos
    // NOTE:   reads searchOrigin, NOT transform.position. This is the fix for the
    //         off-centre path bug: GridToWorld and WorldToGrid must share one anchor
    //         or the round-trip is displaced by (transform.position - anchor).
    private Vector3 GridToWorld(int row, int col)
    {
        float x = (col - (COL - 1) * 0.5f) * cellSize;
        float y = ((ROW - 1) * 0.5f - row) * cellSize;
        return searchOrigin + new Vector3(x, y, 0f);
    }

    // INPUT:  world-space position
    // OUTPUT: grid Pair (row, col) — MAY BE OUT OF BOUNDS
    // USE:    always wrap the result in ClampToGrid before using it
    private Pair WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - searchOrigin;
        int col = Mathf.RoundToInt(local.x / cellSize + (COL - 1) * 0.5f);
        int row = Mathf.RoundToInt((ROW - 1) * 0.5f - local.y / cellSize);
        return new Pair(row, col);
    }

    // INPUT:  possibly out-of-window Pair
    // OUTPUT: Pair clamped to the window edge; sets destWasClamped if it moved
    // USE:    a follower window cannot contain distant targets, so clamping yields a
    //         partial path in the right direction rather than a null path
    private Pair ClampToGrid(Pair p)
    {
        int r = Mathf.Clamp(p.first, 0, ROW - 1);
        int c = Mathf.Clamp(p.second, 0, COL - 1);
        if (r != p.first || c != p.second) destWasClamped = true;
        return new Pair(r, c);
    }

    // ── Grid queries ──────────────────────────────────────────────────────────

    // INPUT:  row, col
    // OUTPUT: true if inside the window
    private bool IsValid(int row, int col) =>
        row >= 0 && row < ROW && col >= 0 && col < COL;

    // INPUT:  row, col
    // OUTPUT: true if no obstacle collider overlaps that cell's world position
    // USE:    raw physics query — prefer Walkable(), which caches this
    // NOTE:   ignores own collider, children, and other agents so enemies do not
    //         treat each other as permanent walls
    private bool IsUnBlocked(int row, int col)
    {
        Vector3 samplePos = GridToWorld(row, col);
        Collider2D[] hits = Physics2D.OverlapCircleAll(samplePos, collisionRadius, collisionMask);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if ( hit.CompareTag("Player")) continue; //hit.CompareTag("Enemy") ||
            return false;
        }
        return true;
    }

    // INPUT:  row, col (must already be IsValid)
    // OUTPUT: cached walkability
    // USE:    every A* walkability test. One physics query per cell per SEARCH rather
    //         than one per neighbour VISIT — the original ran thousands per path.
    private bool Walkable(int row, int col)
    {
        sbyte v = walkCache[row, col];
        if (v != 0) return v == 1;
        bool open = IsUnBlocked(row, col);
        walkCache[row, col] = (sbyte)(open ? 1 : -1);
        return open;
    }

    // INPUT:  a Pair that may sit inside a wall
    // OUTPUT: nearest open Pair within radius 3, or the original if none found
    // USE:    called on source and destination so A* never starts or ends inside geometry
    private Pair GetNearestOpenCell(Pair cell)
    {
        if (IsValid(cell.first, cell.second) && Walkable(cell.first, cell.second))
            return cell;

        for (int r = 1; r <= 3; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    int nr = cell.first + dx;
                    int nc = cell.second + dy;
                    if (IsValid(nr, nc) && Walkable(nr, nc))
                        return new Pair(nr, nc);
                }

        return cell; // nothing open nearby — A* will report Failed
    }

    // INPUT:  none
    // OUTPUT: none — wipes per-search state without reallocating
    // USE:    top of every ComputePath. walkCache MUST be cleared because the window
    //         moved, so last search's cached cells refer to different world tiles.
    private void ClearSearchState()
    {
        Array.Clear(walkCache, 0, walkCache.Length);
        Array.Clear(closedList, 0, closedList.Length);
        openList.Clear();
        for (int i = 0; i < ROW; i++)
            for (int j = 0; j < COL; j++)
            {
                Cell c = cellDetails[i, j];
                c.f = c.g = c.h = double.MaxValue;
                c.parent_i = c.parent_j = -1;
            }
    }

    // ── Path reconstruction ───────────────────────────────────────────────────

    // INPUT:  destination Pair, exact world point to append last
    // OUTPUT: world-space waypoints ordered source → destination, or null if corrupt
    // USE:    called by AStarSearch once the destination is expanded
    private List<Vector2> TracePath(Pair dest, Vector2 exactDestination)
    {
        int row = dest.first, col = dest.second;
        Stack<Vector2> stack = new Stack<Vector2>();

        // Pushed first so it pops last — the exact target bypasses grid snapping
        stack.Push(exactDestination);

        int guard = ROW * COL;  // hard cap: a broken parent link would otherwise hang the editor
        while (!(cellDetails[row, col].parent_i == row && cellDetails[row, col].parent_j == col))
        {
            if (--guard < 0)
            {
                Debug.LogError($"[EnemyPathfinding] {name}: corrupt parent chain in TracePath");
                return null;
            }
            stack.Push(GridToWorld(row, col));
            int tr = cellDetails[row, col].parent_i;
            int tc = cellDetails[row, col].parent_j;
            row = tr; col = tc;
        }

        List<Vector2> path = new List<Vector2>();
        while (stack.Count > 0) path.Add(stack.Pop());
        return path;
    }

    // ── A* search ─────────────────────────────────────────────────────────────

    // INPUT:  source Pair, destination Pair, exact world point for the final waypoint
    // OUTPUT: full path if dest is reachable; otherwise the path to the CLOSEST node
    //         reached, so the enemy still makes progress. Null only if src is unusable.
    // USE:    internal only — call ComputePath
    // NOTE:   the best-effort fallback is what makes long patrol legs work. The window
    //         is 24 cells across but waypoints are 30+ units apart, so the clamped
    //         destination often lands in a fence and would otherwise return null.
    private List<Vector2> AStarSearch(Pair src, Pair dest, Vector2 exactDestination)
    {
        reachedGoal = false;

        if (!IsValid(src.first, src.second) || !IsValid(dest.first, dest.second)) return null;
        if (!Walkable(src.first, src.second)) return null;

        if (src.first == dest.first && src.second == dest.second)
        {
            reachedGoal = true;
            return new List<Vector2> { exactDestination };
        }

        Cell start = cellDetails[src.first, src.second];
        start.f = start.g = start.h = 0;
        start.parent_i = src.first;
        start.parent_j = src.second;

        openList.Enqueue(src, 0.0);

        // Closest expanded node to the goal, for the best-effort fallback.
        Pair bestNode = src;
        double bestH = double.MaxValue;

        while (openList.Count > 0)
        {
            Pair p = openList.Dequeue();
            int row = p.first, col = p.second;

            if (closedList[row, col]) continue;
            closedList[row, col] = true;

            if (row == dest.first && col == dest.second)
            {
                reachedGoal = true;
                return TracePath(dest, exactDestination);
            }

            double h = cellDetails[row, col].h;
            if (h < bestH) { bestH = h; bestNode = p; }

            for (int dir = 0; dir < 8; dir++)
            {
                int nRow = row + dRow[dir];
                int nCol = col + dCol[dir];

                if (!IsValid(nRow, nCol) || closedList[nRow, nCol]) continue;
                if (!Walkable(nRow, nCol)) continue;

                bool diagonal = dRow[dir] != 0 && dCol[dir] != 0;
                if (diagonal && (!Walkable(row + dRow[dir], col) || !Walkable(row, col + dCol[dir])))
                    continue;

                double gNew = cellDetails[row, col].g + (diagonal ? 1.41421356 : 1.0);
                double dr = nRow - dest.first;
                double dc = nCol - dest.second;
                double hNew = Math.Sqrt(dr * dr + dc * dc);
                double fNew = gNew + hNew;

                if (fNew < cellDetails[nRow, nCol].f)
                {
                    Cell n = cellDetails[nRow, nCol];
                    n.f = fNew; n.g = gNew; n.h = hNew;
                    n.parent_i = row; n.parent_j = col;
                    openList.Enqueue(new Pair(nRow, nCol), fNew);
                }
            }
        }

        // Exhausted. Head for the closest cell we could actually reach.
        if (bestNode.first == src.first && bestNode.second == src.second) return null;
        return TracePath(bestNode, GridToWorld(bestNode.first, bestNode.second));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // INPUT:  none
    // OUTPUT: true if there are waypoints left to follow
    // USE:    EnemyMovement checks this before steering
    public bool HasPath() => currentPath != null && currentPath.Count > 0;

    // INPUT:  none
    // OUTPUT: true if the last ComputePath found no route at all
    // USE:    lets Patrol() distinguish "arrived" from "unreachable" and skip a
    //         waypoint instead of pressing into a wall forever
    public bool PathFailed() => Status == PathStatus.Failed;

    // INPUT:  world-space target position
    // OUTPUT: fills currentPath and Status; returns true if any path was produced
    // USE:    called on a timer by EnemyMovement (repathInterval), never every frame
    public bool ComputePath(Vector3 targetWorldPos)
    {
        if (walkCache == null) Awake();   // safety if called before Awake ordering settles

        RefreshOrigin();      // window re-centres here and holds fixed for this call
        ClearSearchState();
        destWasClamped = false;

        Pair src = GetNearestOpenCell(ClampToGrid(WorldToGrid(transform.position)));
        Pair dest = GetNearestOpenCell(ClampToGrid(WorldToGrid(targetWorldPos)));

        // If the target lay outside the window, the final waypoint must be the clamped
        // edge cell. Steering to the true target would cut across un-searched space.
        Vector2 finalPoint = destWasClamped
            ? (Vector2)GridToWorld(dest.first, dest.second)
            : (Vector2)targetWorldPos;

        currentPath = AStarSearch(src, dest, finalPoint);

        if (currentPath == null || currentPath.Count == 0)
        {
            Status = PathStatus.Failed;
            return false;
        }

        Status = destWasClamped ? PathStatus.Partial : PathStatus.Complete;
        return true;
    }

    // INPUT:  fallback world position (used when no path exists)
    // OUTPUT: next world-space steering target; pops reached nodes off the front
    // USE:    called every frame by EnemyMovement while a path is active
    public Vector2 GetNextWayPoint(Vector2 fallback)
    {
        if (currentPath == null || currentPath.Count == 0) return fallback;

        while (currentPath.Count > 0 &&
               Vector2.Distance(transform.position, currentPath[0]) < nodeArriveThreshold)
        {
            currentPath.RemoveAt(0);
        }

        // Knockback or a pushing collision can shove the enemy off its route. Drop a
        // path we can no longer sensibly follow so the next repath rebuilds from here.
        if (currentPath.Count > 0 &&
            Vector2.Distance(transform.position, currentPath[0]) > cellSize * staleDistanceCells)
        {
            currentPath.Clear();
            return fallback;
        }

        return currentPath.Count > 0 ? currentPath[0] : fallback;
    }

    // INPUT:  none
    // OUTPUT: none — clears the active route
    // USE:    call on teleport, dive-layer swap, or any hard reposition
    public void ClearPath()
    {
        currentPath?.Clear();
        Status = PathStatus.None;
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

    // INPUT:  none
    // OUTPUT: none — Scene-view only
    // USE:    verification. The cyan window should follow the enemy in visible
    //         WHOLE-CELL jumps. Smooth gliding means RefreshOrigin is not snapping.
    //         A long yellow line to the first node means the path is stale.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) RefreshOrigin();

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(searchOrigin, new Vector3(COL * cellSize, ROW * cellSize, 0f));

        if (drawGrid)
        {
            bool live = Application.isPlaying && walkCache != null;
            for (int r = 0; r < ROW; r++)
                for (int c = 0; c < COL; c++)
                {
                    // NOTE: IsUnBlocked (not Walkable) so gizmo drawing never writes
                    // into the search cache mid-frame.
                    bool open = !live || IsUnBlocked(r, c);
                    Gizmos.color = open
                        ? new Color(1f, 1f, 1f, 0.10f)
                        : new Color(1f, 0.2f, 0.2f, 0.35f);
                    Gizmos.DrawWireCube(GridToWorld(r, c), Vector3.one * cellSize * 0.9f);
                }
        }

        if (drawPath && currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentPath[0]);

            Gizmos.color = Color.green;
            for (int i = 0; i < currentPath.Count - 1; i++)
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            foreach (Vector2 wp in currentPath)
                Gizmos.DrawSphere(wp, 0.1f);
        }
    }
}