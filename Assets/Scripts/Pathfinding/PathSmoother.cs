using System;
using System.Collections.Generic;

namespace GridTowerDefense.Pathfinding
{
    /// <summary>
    /// Removes unnecessary waypoints so agents can travel in straight lines at any angle
    /// through open space, while still respecting towers and agent width.
    /// </summary>
    public static class PathSmoother
    {
        /// <summary>
        /// Half-width of an enemy in tile units (tile centers are 1 unit apart).
        /// Used so line-of-sight rejects paths that only a zero-width line could fit through.
        /// </summary>
        public const float DefaultAgentRadius = 0.4f;

        /// <summary>
        /// Returns a new waypoint list with intermediate points removed when there is a
        /// clear line of sight between farther waypoints.
        /// </summary>
        /// <param name="allowBlockedEnd">
        /// When true, the final waypoint may be a tower-blocked tile (attack target).
        /// </param>
        /// <param name="agentRadius">
        /// Enemy half-width in tile units. LOS is checked on the center line and on
        /// parallel lines offset left/right by this radius.
        /// </param>
        public static List<GridCoord> Smooth(
            PathGrid grid,
            IReadOnlyList<GridCoord> path,
            bool allowBlockedEnd = false,
            float agentRadius = DefaultAgentRadius)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (path.Count <= 2)
                return new List<GridCoord>(path);

            var smoothed = new List<GridCoord> { path[0] };
            int anchor = 0;

            while (anchor < path.Count - 1)
            {
                int farthest = anchor + 1;
                for (int i = path.Count - 1; i > anchor; i--)
                {
                    bool endIsBlockedTarget = allowBlockedEnd
                        && i == path.Count - 1
                        && grid.IsBlockedByTower(path[i]);

                    if (HasLineOfSight(grid, path[anchor], path[i], endIsBlockedTarget, agentRadius))
                    {
                        farthest = i;
                        break;
                    }
                }

                smoothed.Add(path[farthest]);
                anchor = farthest;
            }

            return smoothed;
        }

        /// <summary>
        /// True if a straight corridor of the given agent radius stays in walkable space
        /// between tile centers and does not squeeze between diagonally adjacent towers.
        /// </summary>
        public static bool HasLineOfSight(
            PathGrid grid,
            GridCoord start,
            GridCoord end,
            bool allowBlockedEnd = false,
            float agentRadius = DefaultAgentRadius)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (start == end)
                return allowBlockedEnd
                    ? grid.Contains(start)
                    : grid.IsWalkable(start);

            // Adjacent step: reuse the same rules as grid movement.
            if (start.ChebyshevDistanceTo(end) == 1)
            {
                if (allowBlockedEnd && grid.IsBlockedByTower(end))
                    return grid.CanStepOntoTower(start, end);
                return grid.CanStep(start, end);
            }

            return TraceThickLineClear(grid, start, end, allowBlockedEnd, agentRadius);
        }

        /// <summary>
        /// Center-line supercover check, then the same check on parallel rays offset by
        /// ±agentRadius (and half-radius) perpendicular to the path.
        /// </summary>
        private static bool TraceThickLineClear(
            PathGrid grid,
            GridCoord start,
            GridCoord end,
            bool allowBlockedEnd,
            float agentRadius)
        {
            if (!TraceLineClear(grid, start, end, allowBlockedEnd))
                return false;

            if (agentRadius <= 0f)
                return true;

            double x0 = start.X;
            double z0 = start.Z;
            double x1 = end.X;
            double z1 = end.Z;
            double dx = x1 - x0;
            double dz = z1 - z0;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (length < 1e-9)
                return true;

            double dirX = dx / length;
            double dirZ = dz / length;
            double perpX = -dirZ;
            double perpZ = dirX;

            // Keep offset samples away from the endpoints so a shift into a neighboring
            // tower at the start/goal tile doesn't false-reject a legal corridor.
            double inset = Math.Min(agentRadius, length * 0.5);
            double sx = x0 + dirX * inset;
            double sz = z0 + dirZ * inset;
            double ex = x1 - dirX * inset;
            double ez = z1 - dirZ * inset;

            float[] offsets = { -agentRadius, agentRadius };

            foreach (float offset in offsets)
            {
                if (!TraceSegmentClearFloat(
                        grid,
                        sx + perpX * offset,
                        sz + perpZ * offset,
                        ex + perpX * offset,
                        ez + perpZ * offset,
                        end,
                        allowBlockedEnd))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Supercover-style grid traversal between cell centers (zero-width).
        /// </summary>
        private static bool TraceLineClear(
            PathGrid grid,
            GridCoord start,
            GridCoord end,
            bool allowBlockedEnd)
        {
            int x0 = start.X;
            int z0 = start.Z;
            int x1 = end.X;
            int z1 = end.Z;

            int dx = Math.Abs(x1 - x0);
            int dz = Math.Abs(z1 - z0);
            int x = x0;
            int z = z0;

            int xStep = Math.Sign(x1 - x0);
            int zStep = Math.Sign(z1 - z0);

            int cells = dx + dz + 1;
            int error = dx - dz;
            dx *= 2;
            dz *= 2;

            for (int i = 0; i < cells; i++)
            {
                var cell = new GridCoord(x, z);
                bool isEnd = cell == end;

                if (!IsCellAllowed(grid, cell, isStart: cell == start, isEnd, allowBlockedEnd))
                    return false;

                if (cell == end)
                    return true;

                if (error > 0)
                {
                    x += xStep;
                    error -= dz;
                }
                else if (error < 0)
                {
                    z += zStep;
                    error += dx;
                }
                else
                {
                    var sideA = new GridCoord(x + xStep, z);
                    var sideB = new GridCoord(x, z + zStep);
                    if (!grid.IsWalkable(sideA) || !grid.IsWalkable(sideB))
                        return false;

                    x += xStep;
                    z += zStep;
                    error -= dz;
                    error += dx;
                    i++;
                }
            }

            return x == x1 && z == z1;
        }

        /// <summary>
        /// Amanatides &amp; Woo traversal for a segment between arbitrary points.
        /// Cells are unit squares centered on integer coordinates.
        /// </summary>
        private static bool TraceSegmentClearFloat(
            PathGrid grid,
            double x0,
            double z0,
            double x1,
            double z1,
            GridCoord pathEnd,
            bool allowBlockedEnd)
        {
            const double epsilon = 1e-9;

            double dx = x1 - x0;
            double dz = z1 - z0;

            int x = (int)Math.Floor(x0 + 0.5);
            int z = (int)Math.Floor(z0 + 0.5);
            int lastX = (int)Math.Floor(x1 + 0.5);
            int lastZ = (int)Math.Floor(z1 + 0.5);

            int stepX = dx > epsilon ? 1 : dx < -epsilon ? -1 : 0;
            int stepZ = dz > epsilon ? 1 : dz < -epsilon ? -1 : 0;

            double tDeltaX = stepX != 0 ? Math.Abs(1.0 / dx) : double.PositiveInfinity;
            double tDeltaZ = stepZ != 0 ? Math.Abs(1.0 / dz) : double.PositiveInfinity;

            double tMaxX = stepX > 0
                ? (x + 0.5 - x0) / dx
                : stepX < 0
                    ? (x - 0.5 - x0) / dx
                    : double.PositiveInfinity;

            double tMaxZ = stepZ > 0
                ? (z + 0.5 - z0) / dz
                : stepZ < 0
                    ? (z - 0.5 - z0) / dz
                    : double.PositiveInfinity;

            int guard = Math.Abs(lastX - x) + Math.Abs(lastZ - z) + 8;
            while (guard-- > 0)
            {
                var cell = new GridCoord(x, z);
                if (!IsCellAllowedOnOffsetRay(grid, cell, pathEnd, allowBlockedEnd))
                    return false;

                if (x == lastX && z == lastZ)
                    return true;

                bool crossX = tMaxX <= tMaxZ + epsilon;
                bool crossZ = tMaxZ <= tMaxX + epsilon;

                if (crossX && crossZ && stepX != 0 && stepZ != 0)
                {
                    // Exact corner: both flanking cells must be clear.
                    var sideA = new GridCoord(x + stepX, z);
                    var sideB = new GridCoord(x, z + stepZ);
                    if (!IsCellAllowedOnOffsetRay(grid, sideA, pathEnd, allowBlockedEnd)
                        || !IsCellAllowedOnOffsetRay(grid, sideB, pathEnd, allowBlockedEnd))
                    {
                        return false;
                    }

                    if (tMaxX > 1.0 + epsilon)
                        return true;

                    x += stepX;
                    z += stepZ;
                    tMaxX += tDeltaX;
                    tMaxZ += tDeltaZ;
                }
                else if (crossX)
                {
                    if (tMaxX > 1.0 + epsilon)
                        return true;

                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    if (tMaxZ > 1.0 + epsilon)
                        return true;

                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }

            return false;
        }

        private static bool IsCellAllowed(
            PathGrid grid,
            GridCoord cell,
            bool isStart,
            bool isEnd,
            bool allowBlockedEnd)
        {
            if (isStart)
                return grid.IsWalkable(cell) || (allowBlockedEnd && grid.IsBlockedByTower(cell));

            if (isEnd && allowBlockedEnd && grid.IsBlockedByTower(cell))
                return grid.Contains(cell);

            return grid.IsWalkable(cell);
        }

        private static bool IsCellAllowedOnOffsetRay(
            PathGrid grid,
            GridCoord cell,
            GridCoord pathEnd,
            bool allowBlockedEnd)
        {
            if (allowBlockedEnd && cell == pathEnd && grid.IsBlockedByTower(cell))
                return true;

            return grid.IsWalkable(cell);
        }
    }
}
