using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    public Transform target;
    public float tolerance = 1.0f;
    public float damage = 10f;

    private void Awake()
    {
        if (target == null)
        {
            target = GameObject.FindGameObjectWithTag("Base").transform;
        }
    }

    private void Update()
    {
        if (target != null)
        {
            GetComponent<NavMeshAgent>().SetDestination(target.position);
        }

        if (Vector3.Distance(transform.position, target.position) < tolerance)
        {
            target.GetComponent<Base>().TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
