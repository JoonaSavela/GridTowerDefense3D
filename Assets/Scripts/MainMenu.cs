using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The panels and buttons live in the MainMenu scene so they can be edited in the editor.
/// This script only switches panels and loads scenes.
/// Map buttons under <see cref="mapButtonRoot"/> are used in hierarchy order, matching <see cref="MapCatalog.Maps"/>.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public GameObject rootPanel;
    public GameObject mapPanel;
    public GameObject optionsPanel;
    public Button newGameButton;
    public Button optionsButton;
    public Button quitButton;
    public Button mapBackButton;
    public Button optionsBackButton;
    public RectTransform mapButtonRoot;

    void Awake()
    {
        if (newGameButton != null)
            newGameButton.onClick.AddListener(ShowMaps);
        if (optionsButton != null)
            optionsButton.onClick.AddListener(ShowOptions);
        if (quitButton != null)
            quitButton.onClick.AddListener(GameScenes.Quit);
        if (mapBackButton != null)
            mapBackButton.onClick.AddListener(ShowRoot);
        if (optionsBackButton != null)
            optionsBackButton.onClick.AddListener(ShowRoot);

        WireMapButtons();
        ShowRoot();
    }

    void WireMapButtons()
    {
        if (mapButtonRoot == null)
            return;

        MapCatalog.MapInfo[] maps = MapCatalog.Maps;
        Button[] buttons = mapButtonRoot.GetComponentsInChildren<Button>(true);
        int mapIndex = 0;
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == mapBackButton)
                continue;

            if (mapIndex >= maps.Length)
                break;

            string sceneName = maps[mapIndex].sceneName;
            button.onClick.AddListener(() => GameScenes.LoadMap(sceneName));
            mapIndex++;
        }
    }

    public void ShowMaps()
    {
        SetPanel(rootPanel, false);
        SetPanel(mapPanel, true);
        SetPanel(optionsPanel, false);
    }

    public void ShowOptions()
    {
        SetPanel(rootPanel, false);
        SetPanel(mapPanel, false);
        SetPanel(optionsPanel, true);
    }

    public void ShowRoot()
    {
        SetPanel(rootPanel, true);
        SetPanel(mapPanel, false);
        SetPanel(optionsPanel, false);
    }

    static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}
