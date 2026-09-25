using UnityEngine;

public class Tower : MonoBehaviour
{
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

    float cooldown;
    int damageLevel;
    int fireRateLevel;
    int rangeLevel;
    float baseFireInterval;
    float baseProjectileSpeed;

    public int DamageLevel => damageLevel;
    public int FireRateLevel => fireRateLevel;
    public int RangeLevel => rangeLevel;
    public bool CanUpgradeDamage => damageLevel < maxUpgradeLevel;
    public bool CanUpgradeFireRate => fireRateLevel < maxUpgradeLevel && fireInterval > minFireInterval;
    public bool CanUpgradeRange => rangeLevel < maxUpgradeLevel;
    public int DamageUpgradePrice => Price(damageUpgradeCost, damageLevel);
    public int FireRateUpgradePrice => Price(fireRateUpgradeCost, fireRateLevel);
    public int RangeUpgradePrice => Price(rangeUpgradeCost, rangeLevel);
    public float NextDamage => damage + damageBonus;
    public float NextFireInterval => Mathf.Max(minFireInterval, fireInterval * fireIntervalMultiplier);
    public float NextRange => range + rangeBonus;
    public float ProjectileSpeed => baseProjectileSpeed * (baseFireInterval / Mathf.Max(minFireInterval, fireInterval));

    void Awake()
    {
        baseFireInterval = Mathf.Max(minFireInterval, fireInterval);
        baseProjectileSpeed = projectilePrefab != null ? projectilePrefab.speed : 12f;
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0f)
            Destroy(gameObject);
    }

    void Update()
    {
        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
            return;

        Enemy target = FindNearestEnemy();
        if (target == null)
            return;

        Shoot(target);
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

    void Shoot(Enemy target)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Tower: no projectile prefab assigned.");
            return;
        }

        Projectile shot = Instantiate(projectilePrefab, MuzzlePosition(), projectilePrefab.transform.rotation);
        shot.Launch(target, damage, ProjectileSpeed);
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

    Vector3 MuzzlePosition()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
            return transform.position;

        Bounds bounds = renderer.bounds;
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
