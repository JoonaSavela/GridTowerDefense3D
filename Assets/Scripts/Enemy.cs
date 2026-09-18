using System.Collections.Generic;
using GridTowerDefense.Pathfinding;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed = 3f;
    public float tolerance = 0.2f;
    public float damage = 10f;

    FloorGrid floorGrid;
    PathResult pathResult;
    List<GameObject> waypoints = new List<GameObject>();
    int waypointIndex;
    float repathTimer;
    bool attacking;
    Tower targetTower;
    Base targetBase;

    void Start()
    {
        floorGrid = FindFirstObjectByType<FloorGrid>();
        RecalculatePath();
    }

    void Update()
    {
        if (attacking)
        {
            UpdateAttack();
            return;
        }

        if (waypoints.Count == 0)
        {
            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                repathTimer = 0.5f;
                RecalculatePath();
            }
            return;
        }

        if (waypointIndex >= waypoints.Count)
        {
            BeginAttackAtDestination();
            return;
        }

        MoveAlongPath();
    }

    public void RecalculatePath()
    {
        attacking = false;
        targetTower = null;
        targetBase = null;

        if (floorGrid == null)
        {
            Debug.LogWarning("Enemy: no FloorGrid in the scene.");
            waypoints.Clear();
            return;
        }

        pathResult = floorGrid.FindPathToBaseFrom(transform.position);
        waypoints = floorGrid.ResolveWaypoints(pathResult);
        waypointIndex = 0;

        // Already standing on the first tile(s) — don't walk in place.
        while (waypointIndex < waypoints.Count &&
               HorizontalDistance(transform.position, waypoints[waypointIndex].transform.position) <= tolerance)
        {
            waypointIndex++;
        }

        if (waypoints.Count == 0)
            Debug.LogWarning("Enemy: no path found.");
    }

    void MoveAlongPath()
    {
        Vector3 targetPos = waypoints[waypointIndex].transform.position;
        targetPos.y = transform.position.y;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            speed * Time.deltaTime);

        if (HorizontalDistance(transform.position, targetPos) <= tolerance)
            waypointIndex++;
    }

    void BeginAttackAtDestination()
    {
        attacking = true;

        if (pathResult != null && pathResult.ReachesBase)
        {
            targetBase = FindFirstObjectByType<Base>();
            if (targetBase != null)
                return;
        }

        if (pathResult != null &&
            pathResult.BlockingTower.HasValue &&
            floorGrid.TryGetTowerAt(pathResult.BlockingTower.Value, out Tower tower))
        {
            targetTower = tower;
            return;
        }

        // Nothing left to hit (tower already destroyed, etc.).
        attacking = false;
        waypoints.Clear();
        repathTimer = 0f;
    }

    void UpdateAttack()
    {
        if (targetBase != null)
        {
            targetBase.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (targetTower != null)
        {
            targetTower.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        RecalculatePath();
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
