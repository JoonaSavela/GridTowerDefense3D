using UnityEngine;

public class Tower : MonoBehaviour
{
    public float health = 20f;
    public float range = 4f;
    public float fireInterval = 1f;
    public float damage = 10f;
    public int cost = 50;
    public Projectile projectilePrefab;

    float cooldown;

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
        shot.Launch(target, damage);
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
