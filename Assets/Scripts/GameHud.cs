using TMPro;
using UnityEngine;

public class GameHud : MonoBehaviour
{
    public TMP_Text waveText;
    public TMP_Text healthText;
    public TMP_Text moneyText;
    public int money;

    public bool CanAfford(int cost)
    {
        return cost <= money;
    }

    public bool TrySpend(int cost)
    {
        if (cost < 0)
            cost = 0;
        if (money < cost)
            return false;

        money -= cost;
        return true;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
            return;

        money += amount;
    }

    EnemySpawner spawner;
    Base playerBase;

    void Awake()
    {
        spawner = FindFirstObjectByType<EnemySpawner>();
        playerBase = FindFirstObjectByType<Base>();
    }

    void Update()
    {
        int waveNumber = 0;
        if (spawner != null && spawner.waveIndex >= 0)
            waveNumber = spawner.waveIndex + 1;

        if (waveText != null)
            waveText.text = "Wave: " + waveNumber;

        float health = playerBase != null ? Mathf.Max(0f, playerBase.health) : 0f;
        if (healthText != null)
            healthText.text = "Base health: " + Mathf.CeilToInt(health);

        if (moneyText != null)
            moneyText.text = "Money: " + money;
    }
}
