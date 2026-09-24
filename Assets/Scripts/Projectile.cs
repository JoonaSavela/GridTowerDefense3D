using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 12f;

    float damage;
    Enemy target;

    public void Launch(Enemy enemy, float shotDamage)
    {
        target = enemy;
        damage = shotDamage;
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
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
