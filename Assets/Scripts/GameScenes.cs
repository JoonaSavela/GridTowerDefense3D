using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameScenes
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void KeepTimeRunningOnLoad()
    {
        SceneManager.sceneLoaded += (_, __) =>
        {
            Time.timeScale = 1f;
            FloorTile.PointerEnabled = true;
        };
    }

    public static void LoadMainMenu()
    {
        Load(MapCatalog.MainMenuScene);
    }

    public static void LoadMap(string sceneName)
    {
        Load(sceneName);
    }

    public static void ReloadCurrent()
    {
        Load(SceneManager.GetActiveScene().name);
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static void Load(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Scene '" + sceneName + "' is not in Build Settings.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
