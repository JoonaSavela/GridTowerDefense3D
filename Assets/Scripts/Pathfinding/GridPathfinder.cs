using System.Collections.Generic;

namespace GridTowerDefense.Pathfinding
{
    /// <summary>
    /// Breadth-first pathfinding on an unweighted 4-connected grid.
    /// When the base is unreachable, finds a path that ends on a blocking tower tile.
    /// </summary>
    public static class GridPathfinder
    {
        public static PathResult FindPath(PathGrid grid, GridCoord start, GridCoord goal)
        {
            if (grid == null)
                throw new System.ArgumentNullException(nameof(grid));

            if (!grid.Contains(start) || !grid.Contains(goal))
                return PathResult.None();

            // Start itself is tower-blocked: enemies are already on / attacking that tile.
            if (grid.IsBlockedByTower(start))
                return PathResult.ToBlockingTower(new[] { start }, start);

            if (start == goal)
                return PathResult.ToBase(new[] { start });

            if (TryFindWalkablePath(grid, start, goal, out List<GridCoord> pathToBase))
                return PathResult.ToBase(pathToBase);

            if (TryFindPathToBlockingTower(grid, start, goal, out List<GridCoord> pathToTower, out GridCoord tower))
                return PathResult.ToBlockingTower(pathToTower, tower);

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

            // Goal may be walkable (base tile). Towers never occupy the goal in normal play,
            // but if the goal tile is blocked we treat the base as unreachable via walkable path.
            if (!grid.IsWalkable(goal))
                return false;

            var cameFrom = new Dictionary<GridCoord, GridCoord>();
            var visited = new HashSet<GridCoord> { start };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                GridCoord current = queue.Dequeue();
                if (current == goal)
                {
                    path = ReconstructPath(cameFrom, start, goal);
                    return true;
                }

                foreach (GridCoord neighbor in grid.GetWalkableNeighbors(current))
                {
                    if (!visited.Add(neighbor))
                        continue;

                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
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
            HashSet<GridCoord> reachable = CollectReachable(grid, start, out cameFrom);

            var candidates = new List<TowerCandidate>();
            foreach (GridCoord towerTile in grid.TowerBlockedTiles)
            {
                if (!grid.Contains(towerTile))
                    continue;

                if (!TryGetClosestReachableNeighbor(towerTile, reachable, cameFrom, start, out GridCoord approach, out int approachDistance))
                    continue;

                bool opensPath = WouldOpenPathToGoal(grid, start, goal, towerTile);
                candidates.Add(new TowerCandidate(towerTile, approach, approachDistance, opensPath));
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
            // Prefer choke-point towers that alone reopen a route to the base.
            int openCompare = b.OpensPath.CompareTo(a.OpensPath);
            if (openCompare != 0)
                return openCompare;

            int distanceCompare = a.ApproachDistance.CompareTo(b.ApproachDistance);
            if (distanceCompare != 0)
                return distanceCompare;

            // Stable tie-break for deterministic tests.
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

            // Goal occupied by a different tower still blocks.
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

                foreach (GridCoord offset in GridCoord.CardinalOffsets)
                {
                    GridCoord neighbor = current.Offset(offset);
                    if (!grid.Contains(neighbor))
                        continue;

                    bool walkable = grid.IsWalkable(neighbor) || neighbor == temporarilyWalkableTower;
                    if (!walkable)
                        continue;

                    if (!visited.Add(neighbor))
                        continue;

                    queue.Enqueue(neighbor);
                }
            }

            return false;
        }

        private static HashSet<GridCoord> CollectReachable(
            PathGrid grid,
            GridCoord start,
            out Dictionary<GridCoord, GridCoord> cameFrom)
        {
            cameFrom = new Dictionary<GridCoord, GridCoord>();
            var reachable = new HashSet<GridCoord> { start };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                GridCoord current = queue.Dequeue();
                foreach (GridCoord neighbor in grid.GetWalkableNeighbors(current))
                {
                    if (!reachable.Add(neighbor))
                        continue;

                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            return reachable;
        }

        private static bool TryGetClosestReachableNeighbor(
            GridCoord towerTile,
            HashSet<GridCoord> reachable,
            Dictionary<GridCoord, GridCoord> cameFrom,
            GridCoord start,
            out GridCoord approach,
            out int approachDistance)
        {
            approach = default;
            approachDistance = int.MaxValue;
            bool found = false;

            foreach (GridCoord offset in GridCoord.CardinalOffsets)
            {
                GridCoord neighbor = towerTile.Offset(offset);
                if (!reachable.Contains(neighbor))
                    continue;

                int distance = GetPathLength(cameFrom, start, neighbor);
                if (!found || distance < approachDistance)
                {
                    found = true;
                    approach = neighbor;
                    approachDistance = distance;
                }
                else if (distance == approachDistance)
                {
                    // Deterministic neighbor preference.
                    if (neighbor.X < approach.X || (neighbor.X == approach.X && neighbor.Z < approach.Z))
                        approach = neighbor;
                }
            }

            return found;
        }

        private static int GetPathLength(
            Dictionary<GridCoord, GridCoord> cameFrom,
            GridCoord start,
            GridCoord end)
        {
            if (start == end)
                return 0;

            int length = 0;
            GridCoord current = end;
            while (current != start)
            {
                length++;
                current = cameFrom[current];
            }

            return length;
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
                int approachDistance,
                bool opensPath)
            {
                Tower = tower;
                ApproachTile = approachTile;
                ApproachDistance = approachDistance;
                OpensPath = opensPath;
            }

            public GridCoord Tower { get; }
            public GridCoord ApproachTile { get; }
            public int ApproachDistance { get; }
            public bool OpensPath { get; }
        }
    }
}
