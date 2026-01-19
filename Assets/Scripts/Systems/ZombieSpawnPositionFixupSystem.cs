using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// One-time fixup system that syncs zombie SpawnPosition with their actual world position.
/// This handles cases where zombies are instantiated from prefabs and moved to new positions,
/// but their SpawnPosition wasn't updated (causing them to wander toward origin).
///
/// Runs once after the first frame, then disables itself.
/// </summary>
[UpdateAfter(typeof(CombatSpawnerSystem))]
[UpdateAfter(typeof(UnitSpawnerSystem))]
public partial struct ZombieSpawnPositionFixupSystem : ISystem
{
    private bool _hasRun;

    public void OnCreate(ref SystemState state)
    {
        _hasRun = false;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Skip first frame to let spawners run
        if (!_hasRun)
        {
            _hasRun = true;
            return;
        }

        // Only run once
        state.Enabled = false;

        int fixedCount = 0;

        // Fix up all zombies: set SpawnPosition to their current position
        foreach (var (combatConfig, combatState, transform) in
            SystemAPI.Query<RefRW<ZombieCombatConfig>, RefRW<ZombieCombatState>, RefRO<LocalTransform>>()
                .WithAll<EnemyUnit>())
        {
            float2 currentPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);

            // Update spawn position to current position
            var config = combatConfig.ValueRW;
            config.SpawnPosition = currentPos;
            combatConfig.ValueRW = config;

            // Update wander target to current position (so they don't immediately wander away)
            var state2 = combatState.ValueRW;
            state2.WanderTarget = currentPos;
            combatState.ValueRW = state2;

            fixedCount++;
        }

        if (fixedCount > 0)
        {
            UnityEngine.Debug.Log($"[ZombieSpawnPositionFixup] Fixed spawn positions for {fixedCount} zombies");
        }
    }
}
