using System.Collections.Generic;
using GridTowerDefense.Pathfinding;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed = 3f;
    public float tolerance = 0.2f;
    public float damage = 10f;
    public float health = 30f;
    public int reward = 10;

    bool rewarded;

    [Header("Path Debug")]
    [Tooltip("Draw raw (BFS) and smoothed waypoints in the Scene view.")]
    public bool drawPathGizmos = true;
    public Color rawPathColor = new Color(1f, 0.85f, 0.1f, 0.9f);
    public Color smoothedPathColor = new Color(0.2f, 0.9f, 1f, 0.9f);

    FloorGrid floorGrid;
    PathResult pathResult;
    List<GameObject> waypoints = new List<GameObject>();
    int waypointIndex;
    float repathTimer;
    bool attacking;
    Tower targetTower;
    Base targetBase;

    public void ApplyWaveStats(float waveHealth, float waveSpeed, float waveDamage, int waveReward)
    {
        health = waveHealth;
        speed = waveSpeed;
        damage = waveDamage;
        reward = waveReward;
    }

    public void TakeDamage(float amount)
    {
        if (rewarded)
            return;

        health -= amount;
        if (health > 0f)
            return;

        rewarded = true;
        AwardReward();
        Destroy(gameObject);
    }

    void AwardReward()
    {
        if (reward <= 0)
            return;

        GameHud hud = FindFirstObjectByType<GameHud>();
        if (hud != null)
            hud.AddMoney(reward);
    }

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

    void OnDrawGizmosSelected()
    {
        if (!drawPathGizmos || pathResult == null)
            return;

        // Yellow: every cell from BFS. Cyan: string-pulled path the enemy follows.
        DrawPathGizmos(pathResult.RawWaypoints, rawPathColor, heightOffset: 0.35f, sphereRadius: 0.12f);
        DrawPathGizmos(pathResult.Waypoints, smoothedPathColor, heightOffset: 0.55f, sphereRadius: 0.18f);
    }

    void DrawPathGizmos(
        IReadOnlyList<GridCoord> coords,
        Color color,
        float heightOffset,
        float sphereRadius)
    {
        if (coords == null || coords.Count == 0)
            return;

        Gizmos.color = color;
        float y = transform.position.y + heightOffset;
        Vector3 previous = default;

        for (int i = 0; i < coords.Count; i++)
        {
            Vector3 point = new Vector3(coords[i].X, y, coords[i].Z);
            Gizmos.DrawSphere(point, sphereRadius);

            if (i > 0)
                Gizmos.DrawLine(previous, point);

            previous = point;
        }
    }
}

