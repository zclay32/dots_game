using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that spawns soldiers and zombies based on GameConfig settings.
/// Triggered by SpawnRequest.ShouldSpawn = true (set by GameSceneBootstrap).
/// Replaces the old CombatSpawnerSystem with configurable spawn counts.
/// </summary>
public partial struct GameSpawnerSystem : ISystem
{
    private bool _hasSpawned;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameConfig>();
        state.RequireForUpdate<PrefabLibrary>();
        state.RequireForUpdate<SpawnRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Check if spawning was requested
        var spawnRequest = SystemAPI.GetSingleton<SpawnRequest>();
        if (!spawnRequest.ShouldSpawn || _hasSpawned)
            return;

        _hasSpawned = true;

        var config = SystemAPI.GetSingleton<GameConfig>();
        var prefabs = SystemAPI.GetSingleton<PrefabLibrary>();

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var random = new Random((uint)System.DateTime.Now.Ticks);

        // Spawn crystal at map center first
        SpawnCrystal(ref ecb, config, prefabs);

        // Spawn soldiers clustered around crystal (not on top of it)
        SpawnSoldiers(ref ecb, ref random, config, prefabs);

        // Spawn zombies biased toward edges
        SpawnZombies(ref ecb, ref random, config, prefabs);

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        // Clear spawn request
        var spawnEntity = SystemAPI.GetSingletonEntity<SpawnRequest>();
        state.EntityManager.SetComponentData(spawnEntity, new SpawnRequest { ShouldSpawn = false });

        UnityEngine.Debug.Log($"[GameSpawner] Spawned crystal, {config.SoldierCount} soldiers, and {config.InitialZombieCount} zombies");
    }

    private void SpawnCrystal(ref EntityCommandBuffer ecb, GameConfig config, PrefabLibrary prefabs)
    {
        if (prefabs.CrystalPrefab == Entity.Null)
        {
            UnityEngine.Debug.LogError("[GameSpawner] CrystalPrefab is not assigned in PrefabLibraryAuthoring!");
            return;
        }

        var crystal = ecb.Instantiate(prefabs.CrystalPrefab);

        // Spawn at map center
        float3 position = new float3(config.MapCenter.x, config.MapCenter.y, 0f);
        ecb.SetComponent(crystal, LocalTransform.FromPosition(position));

        UnityEngine.Debug.Log($"[GameSpawner] Spawned crystal at center ({config.MapCenter.x}, {config.MapCenter.y})");
    }

    private void SpawnSoldiers(ref EntityCommandBuffer ecb, ref Random random, GameConfig config, PrefabLibrary prefabs)
    {
        // Spawn soldiers in a tight cluster just below the crystal
        // Crystal is 4x4 tiles (~4 units), so offset by 5 units below center
        const float spawnOffsetY = -5f;
        const float clusterRadius = 2f;

        float2 spawnCenter = new float2(config.MapCenter.x, config.MapCenter.y + spawnOffsetY);

        for (int i = 0; i < config.SoldierCount; i++)
        {
            var soldier = ecb.Instantiate(prefabs.SoldierPrefab);

            // Random position in small cluster below crystal
            float angle = random.NextFloat(0f, math.PI * 2f);
            float distance = random.NextFloat(0f, clusterRadius);

            float3 position = new float3(
                spawnCenter.x + math.cos(angle) * distance,
                spawnCenter.y + math.sin(angle) * distance,
                0f
            );

            ecb.SetComponent(soldier, LocalTransform.FromPosition(position));

            // Soldiers start without a target
            ecb.SetComponent(soldier, new TargetPosition { HasTarget = false });
        }

        UnityEngine.Debug.Log($"[GameSpawner] Spawned {config.SoldierCount} soldiers below crystal");
    }

    private void SpawnZombies(ref EntityCommandBuffer ecb, ref Random random, GameConfig config, PrefabLibrary prefabs)
    {
        // Spawn zombies between min distance and map radius, biased toward edges
        for (int i = 0; i < config.InitialZombieCount; i++)
        {
            var zombie = ecb.Instantiate(prefabs.ZombiePrefab);

            // Random angle
            float angle = random.NextFloat(0f, math.PI * 2f);

            // Use sqrt to bias toward outer edge
            // t=0 -> minDistance, t=1 -> mapRadius
            float t = math.sqrt(random.NextFloat(0f, 1f));
            float distance = math.lerp(config.ZombieMinDistance, config.MapRadius, t);

            float3 position = new float3(
                config.MapCenter.x + math.cos(angle) * distance,
                config.MapCenter.y + math.sin(angle) * distance,
                0f
            );

            ecb.SetComponent(zombie, LocalTransform.FromPosition(position));

            // Zombies start dormant - no target
            ecb.SetComponent(zombie, new TargetPosition
            {
                Value = float2.zero,
                HasTarget = false
            });
        }

        UnityEngine.Debug.Log($"[GameSpawner] Spawned {config.InitialZombieCount} zombies (min dist {config.ZombieMinDistance}, max {config.MapRadius})");
    }
}
