using UnityEngine;

/// <summary>
/// One wave with exact values. Listed waves are used as authored.
/// Waves past the list are produced by <see cref="WaveScaling"/> from the last entry.
/// </summary>
[System.Serializable]
public class WaveDefinition
{
    [Min(1)] public int enemyCount = 3;
    [Min(0.05f)] public float spawnInterval = 1f;
    [Min(1f)] public float health = 30f;
    [Min(0.1f)] public float speed = 3f;
    [Min(0f)] public float damage = 10f;
    [Min(0)] public int reward = 10;

    public WaveDefinition Clone()
    {
        return new WaveDefinition
        {
            enemyCount = enemyCount,
            spawnInterval = spawnInterval,
            health = health,
            speed = speed,
            damage = damage,
            reward = reward,
        };
    }

    public override string ToString()
    {
        return enemyCount + " enemies, every " + spawnInterval.ToString("0.00") +
               "s, hp " + health.ToString("0") + ", speed " + speed.ToString("0.00") +
               ", damage " + damage.ToString("0") + ", reward " + reward;
    }
}

/// <summary>
/// How each stat changes for every wave after the authored list.
/// Each step does value * multiply + add, then clamps.
/// Set multiply to 1 for a flat increase, or add to 0 for a pure multiplier.
/// </summary>
[System.Serializable]
public class WaveScaling
{
    [Header("Count")]
    public int countAdd = 5;
    public float countMultiply = 1f;
    public int countMin = 1;
    public int countMax = 50;

    [Header("Spawn interval")]
    public float intervalAdd = -0.12f;
    public float intervalMultiply = 1f;
    public float intervalMin = 0.28f;
    public float intervalMax = 3f;

    [Header("Health")]
    public float healthAdd = 14f;
    public float healthMultiply = 1.2f;
    public float healthMin = 1f;
    public float healthMax = 2000f;

    [Header("Speed")]
    public float speedAdd = 0.35f;
    public float speedMultiply = 1f;
    public float speedMin = 0.1f;
    public float speedMax = 7f;

    [Header("Damage")]
    public float damageAdd = 0f;
    public float damageMultiply = 1f;
    public float damageMin = 0f;
    public float damageMax = 200f;

    [Header("Reward")]
    public int rewardAdd = 0;
    public float rewardMultiply = 1f;
    public int rewardMin = 0;
    public int rewardMax = 100;

    public WaveDefinition Apply(WaveDefinition baseline, int steps)
    {
        WaveDefinition wave = baseline != null ? baseline.Clone() : new WaveDefinition();
        steps = Mathf.Max(0, steps);
        for (int i = 0; i < steps; i++)
        {
            wave.enemyCount = StepInt(wave.enemyCount, countAdd, countMultiply, countMin, countMax);
            wave.spawnInterval = Step(wave.spawnInterval, intervalAdd, intervalMultiply, intervalMin, intervalMax);
            wave.health = Step(wave.health, healthAdd, healthMultiply, healthMin, healthMax);
            wave.speed = Step(wave.speed, speedAdd, speedMultiply, speedMin, speedMax);
            wave.damage = Step(wave.damage, damageAdd, damageMultiply, damageMin, damageMax);
            wave.reward = StepInt(wave.reward, rewardAdd, rewardMultiply, rewardMin, rewardMax);
        }

        return wave;
    }

    static float Step(float value, float add, float multiply, float min, float max)
    {
        return Mathf.Clamp(value * multiply + add, min, max);
    }

    static int StepInt(int value, int add, float multiply, int min, int max)
    {
        return Mathf.Clamp(Mathf.RoundToInt(value * multiply + add), min, max);
    }
}

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;

    [Tooltip("These waves use the values below. Anything after the list is generated.")]
    public WaveDefinition[] authoredWaves =
    {
        new WaveDefinition { enemyCount = 3, spawnInterval = 1f, health = 30f, speed = 3f, damage = 10f, reward = 10 },
        new WaveDefinition { enemyCount = 5, spawnInterval = 1f, health = 30f, speed = 3f, damage = 10f, reward = 10 },
        new WaveDefinition { enemyCount = 10, spawnInterval = 1f, health = 30f, speed = 3f, damage = 10f, reward = 10 },
    };

    [Tooltip("When enabled, waves past the authored list keep scaling. When disabled, they repeat the last authored wave.")]
    public bool scaleBeyondAuthored = true;
    public WaveScaling scaling = new WaveScaling();

    [Header("Runtime")]
    public int waveIndex = -1;
    public int enemyCount = 0;
    public float spawnTimer = 0.0f;
    public bool isSpawning = false;

    WaveDefinition currentWave;

    void Update()
    {
        if (!isSpawning || currentWave == null)
            return;

        if (enemyCount >= currentWave.enemyCount)
        {
            isSpawning = false;
            return;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentWave.spawnInterval)
        {
            SpawnEnemy();
            spawnTimer = 0.0f;
        }
    }

    void SpawnEnemy()
    {
        Vector3 spawnPos = transform.position;
        spawnPos.y += 1.0f;

        GameObject instance = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        Enemy enemy = instance.GetComponent<Enemy>();
        if (enemy != null)
            enemy.ApplyWaveStats(currentWave.health, currentWave.speed, currentWave.damage, currentWave.reward);

        enemyCount++;
    }

    public void SpawnWave()
    {
        if (isSpawning)
            return;

        waveIndex++;
        currentWave = GetWave(waveIndex);
        enemyCount = 0;
        spawnTimer = 0.0f;
        isSpawning = true;
    }

    public WaveDefinition GetWave(int index)
    {
        if (authoredWaves != null && authoredWaves.Length > 0 && index < authoredWaves.Length)
            return authoredWaves[index];

        WaveDefinition baseline = LastAuthoredWave();
        int authoredCount = authoredWaves != null ? authoredWaves.Length : 0;
        int stepsPastAuthored = index - Mathf.Max(0, authoredCount - 1);
        if (!scaleBeyondAuthored || scaling == null)
            return baseline;

        return scaling.Apply(baseline, stepsPastAuthored);
    }

    WaveDefinition LastAuthoredWave()
    {
        if (authoredWaves != null && authoredWaves.Length > 0)
            return authoredWaves[authoredWaves.Length - 1];

        return new WaveDefinition();
    }

    [ContextMenu("Log Next 12 Waves")]
    void LogUpcomingWaves()
    {
        int start = Mathf.Max(0, waveIndex + 1);
        for (int i = start; i < start + 12; i++)
            Debug.Log("Wave " + (i + 1) + ": " + GetWave(i));
    }
}
