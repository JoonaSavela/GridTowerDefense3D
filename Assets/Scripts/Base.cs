using UnityEngine;

public class Base : MonoBehaviour
{
    public float health = 100f;

    public void TakeDamage(float damage)
    {
        health -= damage;
        if (health <= 0)
        {
            // TODO: Game over
            Debug.Log("Game over");
        }
    }
}
