/// <summary>
/// Playable maps shown from New Game. Add a scene here and to Build Settings when a new map exists.
/// </summary>
public static class MapCatalog
{
    public const string MainMenuScene = "MainMenu";

    public static readonly MapInfo[] Maps =
    {
        new MapInfo("Map 1", "MapScene1"),
    };

    public readonly struct MapInfo
    {
        public readonly string label;
        public readonly string sceneName;

        public MapInfo(string label, string sceneName)
        {
            this.label = label;
            this.sceneName = sceneName;
        }
    }
}
