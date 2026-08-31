using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnRate = 1.0f;
    public int waveIndex = 0;
    public int enemyCount = 0;
    public float spawnTimer = 0.0f;
    public int[] waveEnemies = { 
        3,
        5,
        10,
        20,
        30,
    };

    private void Update()
    {
        if (enemyCount >= waveEnemies[waveIndex]) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnRate)
        {
            SpawnEnemy();
            spawnTimer = 0.0f;
        }
    }

    private void SpawnEnemy()
    {
        Vector3 spawnPos = transform.position;
        spawnPos.y += 1.0f;
   
        Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        enemyCount++;
    }

    public void NextWave()
    {
        waveIndex++;
        enemyCount = 0;
        spawnTimer = 0.0f;
    }
}
