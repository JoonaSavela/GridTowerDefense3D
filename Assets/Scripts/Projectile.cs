using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 12f;

    float damage;
    float slowFactor = 1f;
    float slowDuration;
    Enemy target;

    public void Launch(Enemy enemy, float shotDamage, float shotSpeed, float shotSlowFactor = 1f, float shotSlowDuration = 0f)
    {
        target = enemy;
        damage = shotDamage;
        speed = shotSpeed;
        slowFactor = shotSlowFactor;
        slowDuration = shotSlowDuration;
    }

    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = target.transform.position;
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, destination) <= 0.2f)
        {
            if (slowFactor < 1f)
                target.ApplySlow(slowFactor, slowDuration);
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
