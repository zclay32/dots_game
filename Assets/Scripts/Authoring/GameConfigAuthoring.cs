using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for game configuration defaults.
/// These values are used when playing the game scene directly (without main menu).
/// When launching from main menu, these values are overwritten by GameSceneBootstrap.
/// </summary>
public class GameConfigAuthoring : MonoBehaviour
{
    [Header("Default Spawn Settings")]
    [Tooltip("Number of soldiers to spawn")]
    public int defaultSoldierCount = 100;

    [Tooltip("Number of initial zombies to spawn")]
    public int defaultZombieCount = 10000;

    [Header("Default Map Settings")]
    [Tooltip("Distance from center to map edge")]
    public float defaultMapRadius = 80f;

    [Tooltip("Minimum distance from center for zombie spawns")]
    public float defaultZombieMinDistance = 15f;

    [Tooltip("Center point of the map")]
    public Vector2 defaultMapCenter = Vector2.zero;

    class Baker : Baker<GameConfigAuthoring>
    {
        public override void Bake(GameConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Add game configuration with defaults
            AddComponent(entity, new GameConfig
            {
                SoldierCount = authoring.defaultSoldierCount,
                InitialZombieCount = authoring.defaultZombieCount,
                MapRadius = authoring.defaultMapRadius,
                ZombieMinDistance = authoring.defaultZombieMinDistance,
                MapCenter = new float2(authoring.defaultMapCenter.x, authoring.defaultMapCenter.y)
            });

            // Add spawn request - disabled by default, enabled by GameSceneBootstrap
            AddComponent(entity, new SpawnRequest
            {
                ShouldSpawn = false
            });
        }
    }
}
