using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Wave-based zombie spawning system.
/// Manages wave state machine, spawns zombies in staggered batches from map edges,
/// and tracks victory/defeat conditions.
///
/// Reads zombie prefab from PrefabLibrary singleton.
/// Reads map settings from GameConfig singleton.
/// </summary>
public partial struct WaveSpawnerSystem : ISystem
{
    private Random _random;
    private bool _waitingForSpawn;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WaveConfig>();
        state.RequireForUpdate<WaveState>();
        state.RequireForUpdate<PrefabLibrary>();
        state.RequireForUpdate<GameConfig>();
        _random = new Random(12345);
        _waitingForSpawn = true;
    }

    public void OnUpdate(ref SystemState state)
    {
        // Wait for initial spawning to complete before starting wave countdown
        if (_waitingForSpawn)
        {
            var spawnRequest = SystemAPI.GetSingleton<SpawnRequest>();
            // SpawnRequest starts false, becomes true when bootstrap triggers, then false again after spawning
            // We need to wait until spawning is done (ShouldSpawn was true and is now false)
            // Check if there are any soldiers (spawning has happened)
            int soldierCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<PlayerUnit>>())
                soldierCount++;

            if (soldierCount > 0)
            {
                _waitingForSpawn = false;
                UnityEngine.Debug.Log("[WaveSpawner] Initial spawning complete, starting wave countdown");
            }
            else
            {
                return; // Still waiting for spawning
            }
        }

        var waveConfig = SystemAPI.GetSingleton<WaveConfig>();
        var gameConfig = SystemAPI.GetSingleton<GameConfig>();
        var prefabs = SystemAPI.GetSingleton<PrefabLibrary>();
        var waveState = SystemAPI.GetSingleton<WaveState>();

        float deltaTime = SystemAPI.Time.DeltaTime;

        // Check defeat condition (all soldiers dead)
        int currentSoldierCount = 0;
        foreach (var _ in SystemAPI.Query<RefRO<PlayerUnit>>())
            currentSoldierCount++;

        if (currentSoldierCount == 0 && waveState.Phase != WavePhase.Victory)
        {
            waveState.Phase = WavePhase.Defeat;
            SystemAPI.SetSingleton(waveState);
            return;
        }

        // Count alive zombies
        int totalZombieCount = 0;
        foreach (var _ in SystemAPI.Query<RefRO<EnemyUnit>>())
            totalZombieCount++;
        waveState.TotalZombiesAlive = totalZombieCount;

        // Count wave-spawned zombies for wave completion tracking
        int waveZombieCount = 0;
        foreach (var _ in SystemAPI.Query<RefRO<WaveSpawnedZombie>>())
            waveZombieCount++;
        waveState.ZombiesAlive = waveZombieCount;

        // State machine
        switch (waveState.Phase)
        {
            case WavePhase.Countdown:
                UpdateCountdown(ref state, ref waveState, waveConfig, gameConfig, deltaTime);
                break;

            case WavePhase.Spawning:
                UpdateSpawning(ref state, ref waveState, waveConfig, gameConfig, prefabs, deltaTime);
                break;

            case WavePhase.Active:
                UpdateActive(ref state, ref waveState, waveConfig, gameConfig);
                break;

            case WavePhase.Victory:
            case WavePhase.Defeat:
                // Game over states - do nothing
                break;
        }

        SystemAPI.SetSingleton(waveState);
    }

    private void UpdateCountdown(ref SystemState state, ref WaveState waveState, WaveConfig waveConfig, GameConfig gameConfig, float deltaTime)
    {
        waveState.Timer -= deltaTime;

        if (waveState.Timer <= 0f)
        {
            // Start next wave
            waveState.CurrentWave++;
            waveState.Phase = WavePhase.Spawning;

            // Calculate zombies for this wave
            int zombiesThisWave = waveConfig.BaseZombiesPerWave +
                (waveState.CurrentWave - 1) * waveConfig.ZombiesPerWaveIncrease;
            waveState.ZombiesRemaining = zombiesThisWave;

            // NOTE: SpawnPosition and Direction were already set by PickSpawnLocation
            // at the end of the previous wave (or during initial countdown for wave 1).
            // Do NOT call PickSpawnLocation here - it would overwrite the direction shown in UI!

            // For wave 1 (first wave ever), pick spawn location now since it wasn't pre-picked
            if (waveState.CurrentWave == 1)
            {
                PickSpawnLocation(ref waveState, gameConfig);
            }

            // Reset spawn timer
            waveState.SpawnTimer = 0f;
        }
    }

    private void PickSpawnLocation(ref WaveState waveState, GameConfig gameConfig)
    {
        // Pick random direction
        SpawnDirection dir = (SpawnDirection)_random.NextInt(0, 4);
        waveState.Direction = dir;

        // Calculate spawn position along that edge
        float randomOffset = _random.NextFloat(-gameConfig.MapRadius * 0.8f, gameConfig.MapRadius * 0.8f);

        waveState.SpawnPosition = dir switch
        {
            SpawnDirection.North => new float2(gameConfig.MapCenter.x + randomOffset, gameConfig.MapCenter.y + gameConfig.MapRadius),
            SpawnDirection.South => new float2(gameConfig.MapCenter.x + randomOffset, gameConfig.MapCenter.y - gameConfig.MapRadius),
            SpawnDirection.East => new float2(gameConfig.MapCenter.x + gameConfig.MapRadius, gameConfig.MapCenter.y + randomOffset),
            SpawnDirection.West => new float2(gameConfig.MapCenter.x - gameConfig.MapRadius, gameConfig.MapCenter.y + randomOffset),
            _ => new float2(gameConfig.MapCenter.x, gameConfig.MapCenter.y + gameConfig.MapRadius)
        };
    }

    private void UpdateSpawning(ref SystemState state, ref WaveState waveState, WaveConfig waveConfig, GameConfig gameConfig, PrefabLibrary prefabs, float deltaTime)
    {
        waveState.SpawnTimer -= deltaTime;

        if (waveState.SpawnTimer <= 0f && waveState.ZombiesRemaining > 0)
        {
            // Spawn a batch
            int batchSize = math.min(waveConfig.SpawnBatchSize, waveState.ZombiesRemaining);
            SpawnZombieBatch(ref state, waveConfig, gameConfig, prefabs, waveState, batchSize);

            waveState.ZombiesRemaining -= batchSize;
            waveState.SpawnTimer = waveConfig.SpawnBatchInterval;
        }

        // Check if done spawning
        if (waveState.ZombiesRemaining <= 0)
        {
            waveState.Phase = WavePhase.Active;
        }
    }

    private void SpawnZombieBatch(ref SystemState state, WaveConfig waveConfig, GameConfig gameConfig, PrefabLibrary prefabs, WaveState waveState, int count)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int i = 0; i < count; i++)
        {
            // Add spread to spawn position
            float2 offset = _random.NextFloat2Direction() * _random.NextFloat(0f, waveConfig.SpawnSpread);
            float2 spawnPos = waveState.SpawnPosition + offset;

            // Instantiate zombie from PrefabLibrary
            var entity = ecb.Instantiate(prefabs.ZombiePrefab);

            // Set position (XY plane, Z=0 for 2D)
            ecb.SetComponent(entity, LocalTransform.FromPosition(new float3(spawnPos.x, spawnPos.y, 0f)));

            // Tag as wave-spawned zombie for tracking
            ecb.AddComponent<WaveSpawnedZombie>(entity);

            // Set zombie to chase toward map center at full speed
            // Using Chasing state with HasTarget=false allows:
            // 1. Full speed movement toward center
            // 2. Target detection via ZombieTargetSearchJob (searches Chasing without target too)
            // Wave zombies won't react to noise (already chasing), but will detect nearby players
            ecb.SetComponent(entity, new ZombieCombatState
            {
                State = ZombieCombatAIState.Chasing,
                StateTimer = 120f, // Long chase duration toward center
                WanderTarget = gameConfig.MapCenter, // Destination when no entity target
                CurrentTarget = Entity.Null,
                HasTarget = false,
                HasEngagedTarget = false,
                CachedTargetPos = gameConfig.MapCenter
            });

            // Note: SpawnPosition in ZombieCombatConfig will be set by ZombieSpawnPositionFixupSystem
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private void UpdateActive(ref SystemState state, ref WaveState waveState, WaveConfig waveConfig, GameConfig gameConfig)
    {
        // Check if all zombies are dead
        if (waveState.ZombiesAlive == 0)
        {
            if (waveState.CurrentWave >= waveConfig.TotalWaves)
            {
                // Victory!
                waveState.Phase = WavePhase.Victory;
            }
            else
            {
                // Start countdown to next wave
                waveState.Phase = WavePhase.Countdown;
                waveState.Timer = waveConfig.TimeBetweenWaves;

                // Pre-pick next wave spawn location so UI can show direction
                PickSpawnLocation(ref waveState, gameConfig);
            }
        }
    }
}
