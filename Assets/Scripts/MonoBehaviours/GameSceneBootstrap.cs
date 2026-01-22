using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bootstrap component that runs when the game scene loads.
/// Reads configuration from GameConfigBridge (set by MainMenuManager)
/// and applies it to ECS singletons before triggering spawning.
///
/// If no config is set (playing directly in editor), uses default baked values.
/// </summary>
public class GameSceneBootstrap : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(ConfigureAndSpawn());
    }

    private IEnumerator ConfigureAndSpawn()
    {
        // Wait for ECS world to initialize
        while (World.DefaultGameObjectInjectionWorld == null)
        {
            yield return null;
        }

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Wait for GameConfig entity to exist (baked from subscene)
        EntityQuery configQuery = default;
        while (true)
        {
            configQuery = em.CreateEntityQuery(typeof(GameConfig));
            if (configQuery.CalculateEntityCount() > 0)
                break;
            yield return null;
        }

        // Apply menu configuration if available
        if (GameConfigBridge.HasConfig)
        {
            Debug.Log($"[GameSceneBootstrap] Applying menu config: {GameConfigBridge.SoldierCount} soldiers, {GameConfigBridge.ZombieCount} zombies");

            var configEntity = configQuery.GetSingletonEntity();
            em.SetComponentData(configEntity, new GameConfig
            {
                SoldierCount = GameConfigBridge.SoldierCount,
                InitialZombieCount = GameConfigBridge.ZombieCount,
                MapRadius = GameConfigBridge.MapRadius,
                ZombieMinDistance = GameConfigBridge.ZombieMinDistance,
                MapCenter = float2.zero
            });

            GameConfigBridge.Clear();
        }
        else
        {
            Debug.Log("[GameSceneBootstrap] No menu config, using default baked values");
        }

        // Wait a frame for config to be applied
        yield return null;

        // Trigger spawning
        var spawnQuery = em.CreateEntityQuery(typeof(SpawnRequest));
        if (spawnQuery.CalculateEntityCount() > 0)
        {
            var spawnEntity = spawnQuery.GetSingletonEntity();
            em.SetComponentData(spawnEntity, new SpawnRequest { ShouldSpawn = true });
            Debug.Log("[GameSceneBootstrap] Triggered spawning");
        }
        else
        {
            Debug.LogWarning("[GameSceneBootstrap] SpawnRequest entity not found!");
        }
    }
}
