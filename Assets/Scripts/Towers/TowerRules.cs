using System;
using System.Collections.Generic;
using GridTowerDefense.Pathfinding;

namespace GridTowerDefense.Towers
{
    public enum TowerKind
    {
        SingleTarget = 0,
        Area = 1,
        Sniper = 2,
        Barrage = 3,
        Stub = 4,
    }

    /// <summary>
    /// Combat stats for a tower built from a stub shape.
    /// Single-target towers (including snipers) get a long range that shrinks as the
    /// tile count grows, and less damage when the shape is small.
    /// Area towers (including barrages) only cover the tiles touching the structure,
    /// so their range stays far shorter. Their damage still grows with tile count.
    /// Sniper requires 4 tiles. Barrage requires 5. The largest single-target shape
    /// that fits in 3×3 is 6 tiles, and snipers that size deal bonus damage.
    /// Barrage slow gets stronger at 7 tiles. Formation cost, later upgrade cost,
    /// health, and the upgrade level cap all scale with the number of tiles.
    /// </summary>
    public static class TowerRules
    {
        public const int SniperMinimumTiles = 4;
        public const int BarrageMinimumTiles = 5;
        public const int SniperBonusTiles = 6;
        public const int BarrageHeavyTiles = 7;

        public const int BasicFormationCostPerTile = 25;
        public const int SpecialFormationCostPerTile = 40;
        public const int UpgradeCostPerTile = 20;
        public const int HealthPerTile = 20;

        public const float SingleDamageBase = 6f;
        public const float SingleDamagePerExtraTile = 5f;
        public const float AreaDamageBase = 4f;
        public const float AreaDamagePerExtraTile = 3f;
        public const float SniperDamageBase = 12f;
        public const float SniperDamagePerExtraTile = 8f;
        public const float SniperHeavyDamageMultiplier = 1.25f;
        public const float BarrageDamageBase = 5f;
        public const float BarrageDamagePerExtraTile = 4f;

        public const float SingleFireInterval = 0.95f;
        public const float AreaFireInterval = 1.15f;
        public const float SniperFireInterval = 1.7f;
        public const float BarrageFireInterval = 0.7f;

        public const float SingleRangeAtOneTile = 6.5f;
        public const float SingleRangeDropPerExtraTile = 0.45f;
        public const float SniperRangeAtOneTile = 8f;
        public const float SniperRangeDropPerExtraTile = 0.25f;
        public const float BarrageRangeBonus = 0.25f;
        public const float RangePadding = 0.75f;

        public const float BarrageSlowFactor = 0.7f;
        public const float BarrageSlowDuration = 1.2f;
        public const float BarrageHeavySlowFactor = 0.5f;
        public const float BarrageHeavySlowDuration = 1.6f;

        public static IReadOnlyList<TowerOffer> OffersFor(TowerShapeAnalysis shape)
        {
            if (!shape.IsValid)
                return Array.Empty<TowerOffer>();

            if (shape.PrefersSingleTarget)
            {
                return new[]
                {
                    Offer(shape, TowerKind.SingleTarget, minimumTiles: 1),
                    Offer(shape, TowerKind.Sniper, SniperMinimumTiles),
                };
            }

            return new[]
            {
                Offer(shape, TowerKind.Area, minimumTiles: 1),
                Offer(shape, TowerKind.Barrage, BarrageMinimumTiles),
            };
        }

        public static bool IsAvailable(TowerShapeAnalysis shape, TowerKind kind)
        {
            foreach (TowerOffer offer in OffersFor(shape))
            {
                if (offer.Kind == kind && offer.IsUnlocked)
                    return true;
            }

            return false;
        }

        public static TowerCombatStats StatsFor(TowerShapeAnalysis shape, TowerKind kind)
        {
            if (!IsAvailable(shape, kind))
                return default;

            int tiles = shape.TileCount;
            float ring = AdjacentRingRange(shape.Cells);
            float damage = DamageFor(kind, tiles);
            float range = RangeFor(kind, tiles, ring);
            float fireInterval = FireIntervalFor(kind);
            bool hitAll = kind == TowerKind.Area || kind == TowerKind.Barrage;
            float slowFactor = 1f;
            float slowDuration = 0f;
            string trait = "";

            if (kind == TowerKind.Area)
                trait = "all in range";

            if (kind == TowerKind.Barrage)
            {
                bool heavy = tiles >= BarrageHeavyTiles;
                slowFactor = heavy ? BarrageHeavySlowFactor : BarrageSlowFactor;
                slowDuration = heavy ? BarrageHeavySlowDuration : BarrageSlowDuration;
                trait = heavy ? "all in range, heavy slow" : "all in range, slow";
            }

            if (kind == TowerKind.Sniper && tiles >= SniperBonusTiles)
                trait = "bonus damage";

            int formationPerTile = kind == TowerKind.Sniper || kind == TowerKind.Barrage
                ? SpecialFormationCostPerTile
                : BasicFormationCostPerTile;

            return new TowerCombatStats(
                isValid: true,
                kind: kind,
                tileCount: tiles,
                damage: damage,
                range: range,
                fireInterval: fireInterval,
                hitAllInRange: hitAll,
                slowFactor: slowFactor,
                slowDuration: slowDuration,
                maxUpgradeLevel: tiles,
                formationCost: formationPerTile * tiles,
                upgradeCost: UpgradeCostPerTile * tiles,
                health: HealthPerTile * tiles,
                trait: trait);
        }

        public static float AdjacentRingRange(IReadOnlyList<GridCoord> cells)
        {
            if (cells == null || cells.Count == 0)
                return 0f;

            double sumX = 0;
            double sumZ = 0;
            int minX = cells[0].X;
            int maxX = cells[0].X;
            int minZ = cells[0].Z;
            int maxZ = cells[0].Z;
            var occupied = new HashSet<GridCoord>(cells);
            foreach (GridCoord cell in cells)
            {
                sumX += cell.X;
                sumZ += cell.Z;
                if (cell.X < minX) minX = cell.X;
                if (cell.X > maxX) maxX = cell.X;
                if (cell.Z < minZ) minZ = cell.Z;
                if (cell.Z > maxZ) maxZ = cell.Z;
            }

            double originX = sumX / cells.Count;
            double originZ = sumZ / cells.Count;
            double farthest = 0;
            for (int x = minX - 1; x <= maxX + 1; x++)
            {
                for (int z = minZ - 1; z <= maxZ + 1; z++)
                {
                    var candidate = new GridCoord(x, z);
                    if (occupied.Contains(candidate))
                        continue;
                    if (!TowerShape.IsInExclusionZone(candidate, cells))
                        continue;

                    double dx = x - originX;
                    double dz = z - originZ;
                    double distance = Math.Sqrt(dx * dx + dz * dz);
                    if (distance > farthest)
                        farthest = distance;
                }
            }

            // Padding covers the rest of a tile, not only its center, so an enemy
            // standing anywhere on a touching tile is inside the area range.
            return (float)farthest + RangePadding;
        }

        public static float SingleTargetRange(int tileCount)
        {
            int extra = Math.Max(0, tileCount - 1);
            return SingleRangeAtOneTile - SingleRangeDropPerExtraTile * extra;
        }

        public static float SniperRange(int tileCount)
        {
            int extra = Math.Max(0, tileCount - 1);
            return SniperRangeAtOneTile - SniperRangeDropPerExtraTile * extra;
        }

        public static string DisplayName(TowerKind kind)
        {
            switch (kind)
            {
                case TowerKind.SingleTarget: return "Single target";
                case TowerKind.Area: return "Area";
                case TowerKind.Sniper: return "Sniper";
                case TowerKind.Barrage: return "Barrage";
                default: return "Stub";
            }
        }

        static TowerOffer Offer(TowerShapeAnalysis shape, TowerKind kind, int minimumTiles)
        {
            bool unlocked = shape.TileCount >= minimumTiles;
            return new TowerOffer(kind, unlocked, minimumTiles);
        }

        static float DamageFor(TowerKind kind, int tiles)
        {
            int extra = Math.Max(0, tiles - 1);
            switch (kind)
            {
                case TowerKind.Area:
                    return AreaDamageBase + AreaDamagePerExtraTile * extra;
                case TowerKind.Sniper:
                    float sniper = SniperDamageBase + SniperDamagePerExtraTile * extra;
                    if (tiles >= SniperBonusTiles)
                        sniper *= SniperHeavyDamageMultiplier;
                    return sniper;
                case TowerKind.Barrage:
                    return BarrageDamageBase + BarrageDamagePerExtraTile * extra;
                default:
                    return SingleDamageBase + SingleDamagePerExtraTile * extra;
            }
        }

        static float RangeFor(TowerKind kind, int tiles, float ring)
        {
            switch (kind)
            {
                case TowerKind.Area:
                    return ring;
                case TowerKind.Sniper:
                    return SniperRange(tiles);
                case TowerKind.Barrage:
                    return ring + BarrageRangeBonus;
                default:
                    return SingleTargetRange(tiles);
            }
        }

        static float FireIntervalFor(TowerKind kind)
        {
            switch (kind)
            {
                case TowerKind.Area: return AreaFireInterval;
                case TowerKind.Sniper: return SniperFireInterval;
                case TowerKind.Barrage: return BarrageFireInterval;
                default: return SingleFireInterval;
            }
        }
    }

    public readonly struct TowerOffer
    {
        public TowerOffer(TowerKind kind, bool isUnlocked, int minimumTiles)
        {
            Kind = kind;
            IsUnlocked = isUnlocked;
            MinimumTiles = minimumTiles;
        }

        public TowerKind Kind { get; }
        public bool IsUnlocked { get; }
        public int MinimumTiles { get; }
    }

    public readonly struct TowerCombatStats
    {
        public TowerCombatStats(
            bool isValid,
            TowerKind kind,
            int tileCount,
            float damage,
            float range,
            float fireInterval,
            bool hitAllInRange,
            float slowFactor,
            float slowDuration,
            int maxUpgradeLevel,
            int formationCost,
            int upgradeCost,
            int health,
            string trait)
        {
            IsValid = isValid;
            Kind = kind;
            TileCount = tileCount;
            Damage = damage;
            Range = range;
            FireInterval = fireInterval;
            HitAllInRange = hitAllInRange;
            SlowFactor = slowFactor;
            SlowDuration = slowDuration;
            MaxUpgradeLevel = maxUpgradeLevel;
            FormationCost = formationCost;
            UpgradeCost = upgradeCost;
            Health = health;
            Trait = trait ?? "";
        }

        public bool IsValid { get; }
        public TowerKind Kind { get; }
        public int TileCount { get; }
        public float Damage { get; }
        public float Range { get; }
        public float FireInterval { get; }
        public bool HitAllInRange { get; }
        public float SlowFactor { get; }
        public float SlowDuration { get; }
        public int MaxUpgradeLevel { get; }
        public int FormationCost { get; }
        public int UpgradeCost { get; }
        public int Health { get; }
        public string Trait { get; }
    }
}
