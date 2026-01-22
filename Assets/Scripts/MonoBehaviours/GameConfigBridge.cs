/// <summary>
/// Static class that bridges game configuration between scenes.
/// Set values from MainMenuManager before loading game scene.
/// GameSceneBootstrap reads these values and applies them to ECS singletons.
/// </summary>
public static class GameConfigBridge
{
    /// <summary>
    /// Whether configuration has been set from the main menu.
    /// If false, game scene will use default baked values.
    /// </summary>
    public static bool HasConfig { get; set; }

    /// <summary>
    /// Number of soldiers to spawn.
    /// </summary>
    public static int SoldierCount { get; set; }

    /// <summary>
    /// Number of initial zombies to spawn.
    /// </summary>
    public static int ZombieCount { get; set; }

    /// <summary>
    /// Distance from center to map edge.
    /// </summary>
    public static float MapRadius { get; set; }

    /// <summary>
    /// Minimum distance from center for zombie spawns.
    /// </summary>
    public static float ZombieMinDistance { get; set; }

    /// <summary>
    /// Clears the configuration after it has been applied.
    /// Call this after reading values in GameSceneBootstrap.
    /// </summary>
    public static void Clear()
    {
        HasConfig = false;
        SoldierCount = 0;
        ZombieCount = 0;
        MapRadius = 0f;
        ZombieMinDistance = 0f;
    }
}
