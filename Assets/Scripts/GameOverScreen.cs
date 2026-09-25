using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The canvas lives in the map scene so it can be edited in the editor.
/// It stays inactive until the base is destroyed.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    public Button playAgainButton;
    public Button mainMenuButton;

    static bool open;
    bool keepVisible;

    public static bool IsOpen => open;

    void Awake()
    {
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(PlayAgain);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GameScenes.LoadMainMenu);

        if (!keepVisible)
            gameObject.SetActive(false);
    }

    public static void Show()
    {
        if (open)
            return;

        GameOverScreen screen = FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
        if (screen == null)
        {
            Debug.LogError("GameOverScreen is missing from this map scene.");
            return;
        }

        screen.keepVisible = true;
        screen.gameObject.SetActive(true);
        open = true;
        Time.timeScale = 0f;
        StopMapInteraction();
    }

    void OnDestroy()
    {
        open = false;
    }

    static void StopMapInteraction()
    {
        FloorTile.PointerEnabled = false;

        FloorTile[] tiles = FindObjectsByType<FloorTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
            tiles[i].ClearPointerFeedback();

        TowerShop[] shops = FindObjectsByType<TowerShop>(FindObjectsSortMode.None);
        for (int i = 0; i < shops.Length; i++)
            shops[i].CancelPlacement();
    }

    void PlayAgain()
    {
        GameScenes.ReloadCurrent();
    }
}
