using System;
using System.Collections.Generic;

namespace GridTowerDefense.Pathfinding
{
    /// <summary>
    /// A* pathfinding on an 8-connected grid (no corner cutting through towers).
    /// Cardinal steps cost 1, diagonals cost √2, so raw paths stay close to the
    /// Euclidean shortest route. Paths are then string-pulled for any-angle movement.
    /// When the base is unreachable, finds a path that ends on a blocking tower tile.
    /// </summary>
    public static class GridPathfinder
    {
        private const float CardinalCost = 1f;
        private const float DiagonalCost = 1.41421356f;
        private const float CostEpsilon = 1e-4f;

        public static PathResult FindPath(PathGrid grid, GridCoord start, GridCoord goal)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (!grid.Contains(start) || !grid.Contains(goal))
                return PathResult.None();

            // Start itself is tower-blocked: enemies are already on / attacking that tile.
            if (grid.IsBlockedByTower(start))
                return PathResult.ToBlockingTower(new[] { start }, start);

            if (start == goal)
                return PathResult.ToBase(new[] { start });

            if (TryFindWalkablePath(grid, start, goal, out List<GridCoord> pathToBase))
            {
                List<GridCoord> smoothed = PathSmoother.Smooth(grid, pathToBase, allowBlockedEnd: false);
                return PathResult.ToBase(smoothed, pathToBase);
            }

            if (TryFindPathToBlockingTower(grid, start, goal, out List<GridCoord> pathToTower, out GridCoord tower))
            {
                List<GridCoord> smoothed = PathSmoother.Smooth(grid, pathToTower, allowBlockedEnd: true);
                return PathResult.ToBlockingTower(smoothed, tower, pathToTower);
            }

            return PathResult.None();
        }

        private static bool TryFindWalkablePath(
            PathGrid grid,
            GridCoord start,
            GridCoord goal,
            out List<GridCoord> path)
        {
            path = null;

            if (!grid.IsWalkable(start))
                return false;

            if (!grid.IsWalkable(goal))
                return false;

            var cameFrom = new Dictionary<GridCoord, GridCoord>();
            var gScore = new Dictionary<GridCoord, float> { [start] = 0f };
            var open = new MinPriorityQueue();
            var closed = new HashSet<GridCoord>();

            open.Enqueue(start, Heuristic(start, goal));

            while (open.Count > 0)
            {
                GridCoord current = open.Dequeue();
                if (!closed.Add(current))
                    continue;

                if (current == goal)
                {
                    path = ReconstructPath(cameFrom, start, goal);
                    return true;
                }

                float currentG = gScore[current];
                foreach (GridCoord neighbor in grid.GetWalkableNeighbors(current))
                {
                    if (closed.Contains(neighbor))
                        continue;

                    float tentativeG = currentG + StepCost(current, neighbor);
                    if (gScore.TryGetValue(neighbor, out float existingG) && tentativeG >= existingG - CostEpsilon)
                        continue;

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    open.Enqueue(neighbor, tentativeG + Heuristic(neighbor, goal));
                }
            }

            return false;
        }

        /// <summary>
        /// Picks a tower adjacent to the spawn-reachable region.
        /// Prefers towers whose removal alone would reopen a path to the base;
        /// among those (or all candidates if none reopen), picks the closest from spawn.
        /// </summary>
        private static bool TryFindPathToBlockingTower(
            PathGrid grid,
            GridCoord start,
            GridCoord goal,
            out List<GridCoord> path,
            out GridCoord blockingTower)
        {
            path = null;
            blockingTower = default;

            if (!grid.IsWalkable(start))
                return false;

            Dictionary<GridCoord, GridCoord> cameFrom;
            Dictionary<GridCoord, float> gScore;
            HashSet<GridCoord> reachable = CollectReachable(grid, start, out cameFrom, out gScore);

            var candidates = new List<TowerCandidate>();
            foreach (GridCoord towerTile in grid.TowerBlockedTiles)
            {
                if (!grid.Contains(towerTile))
                    continue;

                if (!TryGetClosestReachableNeighbor(
                        grid,
                        towerTile,
                        reachable,
                        gScore,
                        out GridCoord approach,
                        out float approachCost))
                    continue;

                bool opensPath = WouldOpenPathToGoal(grid, start, goal, towerTile);
                candidates.Add(new TowerCandidate(towerTile, approach, approachCost, opensPath));
            }

            if (candidates.Count == 0)
                return false;

            candidates.Sort(CompareCandidates);
            TowerCandidate best = candidates[0];
            blockingTower = best.Tower;

            path = ReconstructPath(cameFrom, start, best.ApproachTile);
            path.Add(best.Tower);
            return true;
        }

        private static int CompareCandidates(TowerCandidate a, TowerCandidate b)
        {
            int openCompare = b.OpensPath.CompareTo(a.OpensPath);
            if (openCompare != 0)
                return openCompare;

            int distanceCompare = a.ApproachCost.CompareTo(b.ApproachCost);
            if (distanceCompare != 0)
                return distanceCompare;

            int xCompare = a.Tower.X.CompareTo(b.Tower.X);
            if (xCompare != 0)
                return xCompare;

            return a.Tower.Z.CompareTo(b.Tower.Z);
        }

        private static bool WouldOpenPathToGoal(
            PathGrid grid,
            GridCoord start,
            GridCoord goal,
            GridCoord temporarilyWalkableTower)
        {
            if (!grid.IsWalkable(start))
                return false;

            bool goalWalkable = grid.IsWalkable(goal) || goal == temporarilyWalkableTower;
            if (!goalWalkable && !grid.Contains(goal))
                return false;

            if (grid.IsBlockedByTower(goal) && goal != temporarilyWalkableTower)
                return false;

            var visited = new HashSet<GridCoord> { start };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                GridCoord current = queue.Dequeue();
                if (current == goal)
                    return true;

                foreach (GridCoord neighbor in grid.GetWalkableNeighbors(current, temporarilyWalkableTower))
                {
                    if (!visited.Add(neighbor))
                        continue;

                    queue.Enqueue(neighbor);
                }
            }

            return false;
        }

        /// <summary>
        /// Dijkstra flood from <paramref name="start"/> so every reachable tile stores
        /// the minimum movement cost and a parent pointer for path reconstruction.
        /// </summary>
        private static HashSet<GridCoord> CollectReachable(
            PathGrid grid,
            GridCoord start,
            out Dictionary<GridCoord, GridCoord> cameFrom,
            out Dictionary<GridCoord, float> gScore)
        {
            cameFrom = new Dictionary<GridCoord, GridCoord>();
            gScore = new Dictionary<GridCoord, float> { [start] = 0f };
            var open = new MinPriorityQueue();
            var closed = new HashSet<GridCoord>();

            open.Enqueue(start, 0f);

            while (open.Count > 0)
            {
                GridCoord current = open.Dequeue();
                if (!closed.Add(current))
                    continue;

                float currentG = gScore[current];
                foreach (GridCoord neighbor in grid.GetWalkableNeighbors(current))
                {
                    if (closed.Contains(neighbor))
                        continue;

                    float tentativeG = currentG + StepCost(current, neighbor);
                    if (gScore.TryGetValue(neighbor, out float existingG) && tentativeG >= existingG - CostEpsilon)
                        continue;

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    open.Enqueue(neighbor, tentativeG);
                }
            }

            return closed;
        }

        private static bool TryGetClosestReachableNeighbor(
            PathGrid grid,
            GridCoord towerTile,
            HashSet<GridCoord> reachable,
            Dictionary<GridCoord, float> gScore,
            out GridCoord approach,
            out float approachCost)
        {
            approach = default;
            approachCost = float.MaxValue;
            bool found = false;

            foreach (GridCoord offset in GridCoord.AllNeighborOffsets)
            {
                GridCoord neighbor = towerTile.Offset(offset);
                if (!reachable.Contains(neighbor))
                    continue;
                if (!grid.CanStepOntoTower(neighbor, towerTile))
                    continue;

                float cost = gScore[neighbor];
                if (!found || cost < approachCost - CostEpsilon)
                {
                    found = true;
                    approach = neighbor;
                    approachCost = cost;
                }
                else if (cost <= approachCost + CostEpsilon)
                {
                    if (neighbor.X < approach.X || (neighbor.X == approach.X && neighbor.Z < approach.Z))
                        approach = neighbor;
                }
            }

            return found;
        }

        private static float StepCost(GridCoord from, GridCoord to)
        {
            int dx = Math.Abs(to.X - from.X);
            int dz = Math.Abs(to.Z - from.Z);
            return dx == 1 && dz == 1 ? DiagonalCost : CardinalCost;
        }

        /// <summary>
        /// Octile distance: admissible (and consistent) for cardinal=1, diagonal=√2.
        /// </summary>
        private static float Heuristic(GridCoord a, GridCoord b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dz = Math.Abs(a.Z - b.Z);
            int min = Math.Min(dx, dz);
            int max = Math.Max(dx, dz);
            return max + (DiagonalCost - CardinalCost) * min;
        }

        private static List<GridCoord> ReconstructPath(
            Dictionary<GridCoord, GridCoord> cameFrom,
            GridCoord start,
            GridCoord end)
        {
            var path = new List<GridCoord>();
            GridCoord current = end;
            path.Add(current);

            while (current != start)
            {
                current = cameFrom[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        private readonly struct TowerCandidate
        {
            public TowerCandidate(
                GridCoord tower,
                GridCoord approachTile,
                float approachCost,
                bool opensPath)
            {
                Tower = tower;
                ApproachTile = approachTile;
                ApproachCost = approachCost;
                OpensPath = opensPath;
            }

            public GridCoord Tower { get; }
            public GridCoord ApproachTile { get; }
            public float ApproachCost { get; }
            public bool OpensPath { get; }
        }

        /// <summary>
        /// Binary min-heap. Unity's .NET Standard 2.1 profile has no PriorityQueue&lt;,&gt;.
        /// Duplicate entries are allowed; callers skip stale ones via a closed set.
        /// </summary>
        private sealed class MinPriorityQueue
        {
            private readonly List<Entry> _heap = new List<Entry>();

            public int Count => _heap.Count;

            public void Enqueue(GridCoord node, float priority)
            {
                _heap.Add(new Entry(node, priority));
                SiftUp(_heap.Count - 1);
            }

            public GridCoord Dequeue()
            {
                int last = _heap.Count - 1;
                GridCoord result = _heap[0].Node;
                _heap[0] = _heap[last];
                _heap.RemoveAt(last);
                if (_heap.Count > 0)
                    SiftDown(0);
                return result;
            }

            private void SiftUp(int index)
            {
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (_heap[index].Priority >= _heap[parent].Priority)
                        break;

                    Swap(index, parent);
                    index = parent;
                }
            }

            private void SiftDown(int index)
            {
                while (true)
                {
                    int left = index * 2 + 1;
                    int right = left + 1;
                    int smallest = index;

                    if (left < _heap.Count && _heap[left].Priority < _heap[smallest].Priority)
                        smallest = left;
                    if (right < _heap.Count && _heap[right].Priority < _heap[smallest].Priority)
                        smallest = right;

                    if (smallest == index)
                        break;

                    Swap(index, smallest);
                    index = smallest;
                }
            }

            private void Swap(int a, int b)
            {
                Entry tmp = _heap[a];
                _heap[a] = _heap[b];
                _heap[b] = tmp;
            }

            private readonly struct Entry
            {
                public Entry(GridCoord node, float priority)
                {
                    Node = node;
                    Priority = priority;
                }

                public GridCoord Node { get; }
                public float Priority { get; }
            }
        }
    }
}
