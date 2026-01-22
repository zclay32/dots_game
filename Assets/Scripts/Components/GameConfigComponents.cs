using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Game configuration - spawning parameters set from main menu or defaults.
/// This is a singleton component.
/// </summary>
public struct GameConfig : IComponentData
{
    public int SoldierCount;          // Number of soldiers to spawn (default: 100)
    public int InitialZombieCount;    // Number of initial zombies (default: 10,000)
    public float MapRadius;           // Distance from center to map edge (default: 80)
    public float ZombieMinDistance;   // Minimum zombie spawn distance from center (default: 15)
    public float2 MapCenter;          // Center of the map (default: 0,0)
}

/// <summary>
/// Prefab references for spawning units.
/// Baked from authoring component - provides Entity references to prefabs.
/// This is a singleton component.
/// </summary>
public struct PrefabLibrary : IComponentData
{
    public Entity SoldierPrefab;
    public Entity ZombiePrefab;
}

/// <summary>
/// Request component to trigger spawning.
/// Set ShouldSpawn = true to trigger GameSpawnerSystem.
/// This is a singleton component.
/// </summary>
public struct SpawnRequest : IComponentData
{
    public bool ShouldSpawn;
}
