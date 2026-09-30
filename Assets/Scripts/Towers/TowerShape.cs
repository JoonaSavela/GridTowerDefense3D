using System.Collections.Generic;
using GridTowerDefense.Pathfinding;

namespace GridTowerDefense.Towers
{
    /// <summary>
    /// A connected set of stub tiles that can be upgraded into one tower.
    /// The shape must fit in a 3×3 square. Links are unordered adjacent pairs:
    /// when diagonal links are at least the cardinal links, the shape builds a
    /// single-target tower; otherwise it builds an area tower.
    /// </summary>
    public static class TowerShape
    {
        public const int MaxSpan = 3;

        public static TowerShapeAnalysis Analyze(IReadOnlyList<GridCoord> cells)
        {
            if (cells == null || cells.Count == 0)
                return TowerShapeAnalysis.Invalid("empty");

            var unique = new HashSet<GridCoord>();
            foreach (GridCoord cell in cells)
            {
                if (!unique.Add(cell))
                    return TowerShapeAnalysis.Invalid("duplicate");
            }

            int minX = cells[0].X;
            int maxX = cells[0].X;
            int minZ = cells[0].Z;
            int maxZ = cells[0].Z;
            for (int i = 1; i < cells.Count; i++)
            {
                GridCoord cell = cells[i];
                if (cell.X < minX) minX = cell.X;
                if (cell.X > maxX) maxX = cell.X;
                if (cell.Z < minZ) minZ = cell.Z;
                if (cell.Z > maxZ) maxZ = cell.Z;
            }

            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;
            if (width > MaxSpan || height > MaxSpan)
                return TowerShapeAnalysis.Invalid("too large");

            if (ConnectedComponent(cells, cells[0]).Count != unique.Count)
                return TowerShapeAnalysis.Invalid("disconnected");

            int cardinal = 0;
            int diagonal = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                for (int j = i + 1; j < cells.Count; j++)
                {
                    int dx = Abs(cells[i].X - cells[j].X);
                    int dz = Abs(cells[i].Z - cells[j].Z);
                    if (dx > 1 || dz > 1 || (dx == 0 && dz == 0))
                        continue;

                    if (dx == 0 || dz == 0)
                        cardinal++;
                    else
                        diagonal++;
                }
            }

            var copy = new GridCoord[cells.Count];
            for (int i = 0; i < cells.Count; i++)
                copy[i] = cells[i];

            return new TowerShapeAnalysis(
                isValid: true,
                failure: null,
                tileCount: cells.Count,
                cardinalLinks: cardinal,
                diagonalLinks: diagonal,
                prefersSingleTarget: diagonal >= cardinal,
                width: width,
                height: height,
                cells: copy);
        }

        /// <summary>
        /// True when <paramref name="cell"/> can be added and the result is still a valid shape.
        /// </summary>
        public static bool TryAddCell(
            IReadOnlyList<GridCoord> cells,
            GridCoord cell,
            out List<GridCoord> next)
        {
            next = new List<GridCoord>();
            if (cells != null)
                next.AddRange(cells);

            if (next.Contains(cell))
            {
                next = null;
                return false;
            }

            next.Add(cell);
            if (!Analyze(next).IsValid)
            {
                next = null;
                return false;
            }

            return true;
        }

        public static HashSet<GridCoord> ConnectedComponent(IReadOnlyList<GridCoord> cells, GridCoord seed)
        {
            var component = new HashSet<GridCoord>();
            if (cells == null || cells.Count == 0)
                return component;

            var set = new HashSet<GridCoord>(cells);
            if (!set.Contains(seed))
                return component;

            var queue = new Queue<GridCoord>();
            queue.Enqueue(seed);
            component.Add(seed);
            while (queue.Count > 0)
            {
                GridCoord current = queue.Dequeue();
                foreach (GridCoord offset in GridCoord.AllNeighborOffsets)
                {
                    GridCoord neighbor = current.Offset(offset);
                    if (set.Contains(neighbor) && component.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return component;
        }

        /// <summary>
        /// Tiles orthogonally or diagonally adjacent to a finished structure.
        /// The structure's own tiles are not in this zone.
        /// </summary>
        public static bool IsInExclusionZone(GridCoord cell, IReadOnlyList<GridCoord> structureCells)
        {
            if (structureCells == null)
                return false;

            bool adjacent = false;
            foreach (GridCoord occupied in structureCells)
            {
                if (cell.Equals(occupied))
                    return false;

                if (cell.ChebyshevDistanceTo(occupied) == 1)
                    adjacent = true;
            }

            return adjacent;
        }

        static int Abs(int value) => value < 0 ? -value : value;
    }

    public readonly struct TowerShapeAnalysis
    {
        public TowerShapeAnalysis(
            bool isValid,
            string failure,
            int tileCount,
            int cardinalLinks,
            int diagonalLinks,
            bool prefersSingleTarget,
            int width,
            int height,
            GridCoord[] cells)
        {
            IsValid = isValid;
            Failure = failure;
            TileCount = tileCount;
            CardinalLinks = cardinalLinks;
            DiagonalLinks = diagonalLinks;
            PrefersSingleTarget = prefersSingleTarget;
            Width = width;
            Height = height;
            Cells = cells ?? System.Array.Empty<GridCoord>();
        }

        public bool IsValid { get; }
        public string Failure { get; }
        public int TileCount { get; }
        public int CardinalLinks { get; }
        public int DiagonalLinks { get; }
        public bool PrefersSingleTarget { get; }
        public int Width { get; }
        public int Height { get; }
        public GridCoord[] Cells { get; }

        public static TowerShapeAnalysis Invalid(string failure) =>
            new TowerShapeAnalysis(
                isValid: false,
                failure: failure,
                tileCount: 0,
                cardinalLinks: 0,
                diagonalLinks: 0,
                prefersSingleTarget: false,
                width: 0,
                height: 0,
                cells: System.Array.Empty<GridCoord>());
    }
}
