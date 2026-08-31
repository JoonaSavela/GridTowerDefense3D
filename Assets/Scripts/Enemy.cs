using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    public Transform target;

    private void Start()
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
    }
}
