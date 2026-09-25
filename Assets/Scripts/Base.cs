using UnityEngine;

public class Base : MonoBehaviour
{
    public float health = 100f;

    public void TakeDamage(float damage)
    {
        if (health <= 0f)
            return;

        health -= damage;
        if (health > 0f)
            return;

        health = 0f;
        GameOverScreen.Show();
    }
}
