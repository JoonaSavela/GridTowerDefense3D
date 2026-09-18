using System;

namespace GridTowerDefense.Pathfinding
{
    /// <summary>
    /// Integer grid coordinate on the XZ plane (Unity Y is up).
    /// </summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public int X { get; }
        public int Z { get; }

        public GridCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public static readonly GridCoord[] CardinalOffsets =
        {
            new GridCoord(0, 1),
            new GridCoord(0, -1),
            new GridCoord(1, 0),
            new GridCoord(-1, 0),
        };

        public GridCoord Offset(GridCoord offset) => new GridCoord(X + offset.X, Z + offset.Z);

        public int ManhattanDistanceTo(GridCoord other) =>
            Math.Abs(X - other.X) + Math.Abs(Z - other.Z);

        public bool Equals(GridCoord other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }

        public static bool operator ==(GridCoord left, GridCoord right) => left.Equals(right);

        public static bool operator !=(GridCoord left, GridCoord right) => !left.Equals(right);

        public override string ToString() => $"({X}, {Z})";
    }
}
