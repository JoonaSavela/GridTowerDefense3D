using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemySpawner))]
public class EnemySpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EnemySpawner spawner = (EnemySpawner)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Resolved Wave", EditorStyles.boldLabel);

        if (Application.isPlaying && spawner.waveIndex >= 0)
            DrawWave("Current", spawner.waveIndex, spawner.GetWave(spawner.waveIndex));

        string key = "EnemySpawner.previewWave." + spawner.GetInstanceID();
        int previewWave = SessionState.GetInt(key, 1);
        int next = EditorGUILayout.IntSlider("Preview wave", previewWave, 1, 30);
        if (next != previewWave)
            SessionState.SetInt(key, next);

        DrawWave("Preview", next - 1, spawner.GetWave(next - 1));
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    static void DrawWave(string label, int index, WaveDefinition wave)
    {
        if (wave == null)
        {
            EditorGUILayout.LabelField(label, "Wave " + (index + 1) + " has no definition.");
            return;
        }

        EditorGUILayout.LabelField(label, "Wave " + (index + 1));
        EditorGUILayout.LabelField("Enemies", wave.enemyCount.ToString());
        EditorGUILayout.LabelField("Spawn interval", wave.spawnInterval.ToString("0.00"));
        EditorGUILayout.LabelField("Health", wave.health.ToString("0.##"));
        EditorGUILayout.LabelField("Speed", wave.speed.ToString("0.##"));
        EditorGUILayout.LabelField("Damage", wave.damage.ToString("0.##"));
        EditorGUILayout.LabelField("Reward", wave.reward.ToString());
    }
}
