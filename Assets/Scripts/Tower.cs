using System.Collections.Generic;
using GridTowerDefense.Pathfinding;
using GridTowerDefense.Towers;
using UnityEngine;

public class Tower : MonoBehaviour
{
    public const float StubHealth = 12f;

    public static readonly Vector3 StubScale = new Vector3(0.42f, 0.16f, 0.42f);
    public static readonly Vector3 BuiltScale = new Vector3(0.92f, 0.62f, 0.92f);

    public float health = 20f;
    public float range = 4f;
    public float fireInterval = 1f;
    public float damage = 10f;
    public int cost = 50;
    public Projectile projectilePrefab;

    [Header("Upgrades")]
    public int maxUpgradeLevel = 3;
    public float damageBonus = 5f;
    public float fireIntervalMultiplier = 0.8f;
    public float minFireInterval = 0.2f;
    public float rangeBonus = 0.75f;
    public int damageUpgradeCost = 40;
    public int fireRateUpgradeCost = 40;
    public int rangeUpgradeCost = 40;
    public float upgradeCostGrowth = 1.5f;

    [Header("Structure")]
    [SerializeField] bool isStub;
    [SerializeField] TowerKind kind = TowerKind.SingleTarget;

    float cooldown;
    int damageLevel;
    int fireRateLevel;
    int rangeLevel;
    float baseFireInterval;
    float baseProjectileSpeed;
    bool hitAllInRange;
    float slowFactor = 1f;
    float slowDuration;
    readonly List<GridCoord> occupied = new List<GridCoord>();

    public bool IsStub => isStub;
    public TowerKind Kind => kind;
    public int TileCount => OccupiedCells.Count;
    public int DamageLevel => damageLevel;
    public int FireRateLevel => fireRateLevel;
    public int RangeLevel => rangeLevel;
    public bool CanUpgradeDamage => !isStub && damageLevel < maxUpgradeLevel;
    public bool CanUpgradeFireRate => !isStub && fireRateLevel < maxUpgradeLevel && fireInterval > minFireInterval;
    public bool CanUpgradeRange => !isStub && rangeLevel < maxUpgradeLevel;
    public int DamageUpgradePrice => Price(damageUpgradeCost, damageLevel);
    public int FireRateUpgradePrice => Price(fireRateUpgradeCost, fireRateLevel);
    public int RangeUpgradePrice => Price(rangeUpgradeCost, rangeLevel);
    public float NextDamage => damage + damageBonus;
    public float NextFireInterval => Mathf.Max(minFireInterval, fireInterval * fireIntervalMultiplier);
    public float NextRange => range + rangeBonus;
    public float ProjectileSpeed => baseProjectileSpeed * (baseFireInterval / Mathf.Max(minFireInterval, fireInterval));

    public IReadOnlyList<GridCoord> OccupiedCells
    {
        get
        {
            EnsureCells();
            return occupied;
        }
    }

    void Awake()
    {
        baseFireInterval = Mathf.Max(minFireInterval, fireInterval);
        baseProjectileSpeed = projectilePrefab != null ? projectilePrefab.speed : 12f;
    }

    public void InitializeAsStub(GridCoord cell)
    {
        isStub = true;
        kind = TowerKind.Stub;
        health = StubHealth;
        damage = 0f;
        range = 0f;
        hitAllInRange = false;
        occupied.Clear();
        occupied.Add(cell);
        transform.localScale = StubScale;
        gameObject.name = "TowerStub";
        EnsureCollider();
        Tint(GetComponent<Renderer>(), ColorFor(TowerKind.Stub));
    }

    public void ConfigureStructure(
        TowerCombatStats stats,
        IReadOnlyList<GridCoord> cells,
        IReadOnlyList<Vector3> segmentWorldPositions)
    {
        isStub = false;
        kind = stats.Kind;
        damage = stats.Damage;
        range = stats.Range;
        fireInterval = stats.FireInterval;
        baseFireInterval = Mathf.Max(minFireInterval, fireInterval);
        hitAllInRange = stats.HitAllInRange;
        slowFactor = stats.SlowFactor;
        slowDuration = stats.SlowDuration;
        maxUpgradeLevel = Mathf.Max(1, stats.MaxUpgradeLevel);
        health = stats.Health;
        damageUpgradeCost = stats.UpgradeCost;
        fireRateUpgradeCost = stats.UpgradeCost;
        rangeUpgradeCost = stats.UpgradeCost;
        damageLevel = 0;
        fireRateLevel = 0;
        rangeLevel = 0;

        occupied.Clear();
        if (cells != null)
            occupied.AddRange(cells);

        gameObject.name = "Tower" + stats.Kind;
        Color color = ColorFor(stats.Kind);
        bool singleTile = segmentWorldPositions == null || segmentWorldPositions.Count <= 1;
        if (singleTile)
        {
            transform.localScale = BuiltScale;
            EnsureCollider();
            Tint(GetComponent<Renderer>(), color);
            return;
        }

        transform.localScale = Vector3.one;
        Renderer rootRenderer = GetComponent<Renderer>();
        SpawnSegments(segmentWorldPositions, color);
        if (rootRenderer != null)
            rootRenderer.enabled = false;
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0f)
            Destroy(gameObject);
    }

    void Update()
    {
        if (isStub || range <= 0f)
            return;

        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
            return;

        if (hitAllInRange)
        {
            if (!ShootAllInRange())
                return;
        }
        else
        {
            Enemy target = FindNearestEnemy();
            if (target == null)
                return;

            Shoot(target);
        }

        cooldown = fireInterval;
    }

    Enemy FindNearestEnemy()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Enemy closest = null;
        float closestDistance = range;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            float distance = HorizontalDistance(transform.position, enemy.transform.position);
            if (distance <= closestDistance)
            {
                closest = enemy;
                closestDistance = distance;
            }
        }

        return closest;
    }

    bool ShootAllInRange()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        bool fired = false;
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            float distance = HorizontalDistance(transform.position, enemy.transform.position);
            if (distance > range)
                continue;

            Shoot(enemy);
            fired = true;
        }

        return fired;
    }

    void Shoot(Enemy target)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Tower: no projectile prefab assigned.");
            return;
        }

        Projectile shot = Instantiate(projectilePrefab, MuzzlePosition(), projectilePrefab.transform.rotation);
        shot.Launch(target, damage, ProjectileSpeed, slowFactor, slowDuration);
    }

    public bool TryUpgradeDamage()
    {
        if (!CanUpgradeDamage || !TryPay(DamageUpgradePrice))
            return false;

        damage += damageBonus;
        damageLevel++;
        return true;
    }

    public bool TryUpgradeFireRate()
    {
        if (!CanUpgradeFireRate || !TryPay(FireRateUpgradePrice))
            return false;

        fireInterval = NextFireInterval;
        fireRateLevel++;
        return true;
    }

    public bool TryUpgradeRange()
    {
        if (!CanUpgradeRange || !TryPay(RangeUpgradePrice))
            return false;

        range += rangeBonus;
        rangeLevel++;
        return true;
    }

    public float VisualBottomY()
    {
        float bottom = float.PositiveInfinity;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (renderer == null || !renderer.enabled)
                continue;

            bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        }

        return float.IsPositiveInfinity(bottom) ? transform.position.y : bottom;
    }

    public static Color ColorFor(TowerKind towerKind)
    {
        switch (towerKind)
        {
            case TowerKind.Area: return new Color(0.95f, 0.5f, 0.18f);
            case TowerKind.Sniper: return new Color(0.58f, 0.36f, 0.92f);
            case TowerKind.Barrage: return new Color(0.9f, 0.22f, 0.28f);
            case TowerKind.Stub: return new Color(0.45f, 0.48f, 0.52f);
            default: return new Color(0.28f, 0.52f, 0.95f);
        }
    }

    void SpawnSegments(IReadOnlyList<Vector3> worldPositions, Color color)
    {
        MeshFilter sourceFilter = GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
        if (sourceFilter == null || sourceRenderer == null)
            return;

        for (int i = 0; i < worldPositions.Count; i++)
        {
            GameObject piece = new GameObject("TowerSegment");
            piece.transform.SetParent(transform, false);
            piece.transform.localScale = BuiltScale;
            piece.transform.position = worldPositions[i];

            MeshFilter filter = piece.AddComponent<MeshFilter>();
            filter.sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;
            piece.AddComponent<BoxCollider>();
            Tint(renderer, color);
        }
    }

    bool TryPay(int price)
    {
        GameHud hud = FindFirstObjectByType<GameHud>();
        if (hud == null)
        {
            Debug.LogWarning("Tower: GameHud not found, so the upgrade cannot be paid for.");
            return false;
        }

        return hud.TrySpend(price);
    }

    int Price(int baseCost, int level)
    {
        float growth = Mathf.Max(1f, upgradeCostGrowth);
        return Mathf.Max(0, Mathf.RoundToInt(baseCost * Mathf.Pow(growth, level)));
    }

    void EnsureCells()
    {
        if (occupied.Count > 0)
            return;

        occupied.Add(new GridCoord(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.z)));
    }

    Vector3 MuzzlePosition()
    {
        float top = float.NegativeInfinity;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (renderer == null || !renderer.enabled)
                continue;

            top = Mathf.Max(top, renderer.bounds.max.y);
        }

        Vector3 position = transform.position;
        if (!float.IsNegativeInfinity(top))
            position.y = top;
        return position;
    }

    void EnsureCollider()
    {
        if (GetComponent<Collider>() == null)
            gameObject.AddComponent<BoxCollider>();
    }

    static void Tint(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        Material material = renderer.material;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmosSelected()
    {
        if (isStub || range <= 0f)
            return;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
