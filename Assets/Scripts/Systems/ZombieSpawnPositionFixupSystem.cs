using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Fixup system that syncs zombie SpawnPosition with their actual world position.
/// This handles cases where zombies are instantiated from prefabs and moved to new positions,
/// but their SpawnPosition wasn't updated (causing them to wander toward origin).
///
/// Runs continuously to handle wave-spawned zombies.
/// Only fixes zombies whose SpawnPosition is at origin (0,0) - indicating they haven't been fixed yet.
/// </summary>
[UpdateAfter(typeof(CombatSpawnerSystem))]
[UpdateAfter(typeof(UnitSpawnerSystem))]
[UpdateAfter(typeof(WaveSpawnerSystem))]
public partial struct ZombieSpawnPositionFixupSystem : ISystem
{
    private int _frameCount;

    public void OnCreate(ref SystemState state)
    {
        _frameCount = 0;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _frameCount++;

        // Skip first frame to let spawners run
        if (_frameCount == 1)
            return;

        // Run on frame 2 (for initial spawns) and then every 10 frames (for wave spawns)
        if (_frameCount != 2 && _frameCount % 10 != 0)
            return;

        // Fix up zombies whose SpawnPosition doesn't match their actual position
        // This happens when zombies are instantiated from prefabs and moved to new positions
        foreach (var (combatConfig, combatState, transform) in
            SystemAPI.Query<RefRW<ZombieCombatConfig>, RefRW<ZombieCombatState>, RefRO<LocalTransform>>()
                .WithAll<EnemyUnit>())
        {
            var config = combatConfig.ValueRW;
            float2 currentPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);

            // Check if SpawnPosition is significantly different from actual position
            // This indicates the zombie was moved after instantiation and needs fixup
            float distanceFromSpawn = math.distance(config.SpawnPosition, currentPos);

            if (distanceFromSpawn > 1f)  // More than 1 unit away = needs fixup
            {
                // Update spawn position to current position
                config.SpawnPosition = currentPos;
                combatConfig.ValueRW = config;

                // For idle/wandering zombies (initial spawn), also update WanderTarget
                // Wave-spawned zombies are in Chasing state and should keep their target (map center)
                var zombieState = combatState.ValueRW;
                if (zombieState.State == ZombieCombatAIState.Idle || zombieState.State == ZombieCombatAIState.Wandering)
                {
                    zombieState.WanderTarget = currentPos;
                    combatState.ValueRW = zombieState;
                }
            }
        }
    }
}
