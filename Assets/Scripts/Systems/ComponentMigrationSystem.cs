using Unity.Burst;
using Unity.Entities;

/// <summary>
/// One-time migration system that converts old monolithic components
/// to optimized hot/cold separated components for better cache locality
/// Runs once at startup to migrate all entities, then disables itself
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct ComponentMigrationSystem : ISystem
{
    private bool _hasMigrated;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _hasMigrated = false;
    }

    public void OnUpdate(ref SystemState state)
    {
        // Only run once
        if (_hasMigrated)
        {
            state.Enabled = false;
            return;
        }

        // Migrate Combat -> CombatState + CombatConfig
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (combat, entity) in
            SystemAPI.Query<RefRO<Combat>>()
                .WithNone<CombatState>()
                .WithEntityAccess())
        {
            // Add new split components
            ecb.AddComponent(entity, new CombatState
            {
                CurrentCooldown = combat.ValueRO.CurrentCooldown,
                CurrentWindup = combat.ValueRO.CurrentWindup
            });

            ecb.AddComponent(entity, new CombatConfig
            {
                AttackDamage = combat.ValueRO.AttackDamage,
                AttackRange = combat.ValueRO.AttackRange,
                AttackCooldown = combat.ValueRO.AttackCooldown,
                AttackWindup = combat.ValueRO.AttackWindup
            });

            // Keep old component for now (can remove later once all systems migrated)
            // ecb.RemoveComponent<Combat>(entity);
        }

        // Migrate Health -> HealthCurrent + HealthMax
        foreach (var (health, entity) in
            SystemAPI.Query<RefRO<Health>>()
                .WithNone<HealthCurrent>()
                .WithEntityAccess())
        {
            ecb.AddComponent(entity, new HealthCurrent
            {
                Value = health.ValueRO.Current
            });

            ecb.AddComponent(entity, new HealthMax
            {
                Value = health.ValueRO.Max
            });

            // Keep old component for now
            // ecb.RemoveComponent<Health>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        _hasMigrated = true;
        UnityEngine.Debug.Log("Component migration complete: Split Combat and Health into hot/cold components");
    }
}

/// <summary>
/// Synchronization system that keeps old and new components in sync
/// This allows gradual migration - systems can be updated one at a time
/// Once all systems are migrated, this system and old components can be removed
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial struct ComponentSyncSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Sync CombatState changes back to Combat (for systems still using old component)
        foreach (var (combatState, combatConfig, combat) in
            SystemAPI.Query<RefRO<CombatState>, RefRO<CombatConfig>, RefRW<Combat>>())
        {
            // Update hot data
            combat.ValueRW.CurrentCooldown = combatState.ValueRO.CurrentCooldown;
            combat.ValueRW.CurrentWindup = combatState.ValueRO.CurrentWindup;

            // Update cold data (in case config was modified)
            combat.ValueRW.AttackDamage = combatConfig.ValueRO.AttackDamage;
            combat.ValueRW.AttackRange = combatConfig.ValueRO.AttackRange;
            combat.ValueRW.AttackCooldown = combatConfig.ValueRO.AttackCooldown;
            combat.ValueRW.AttackWindup = combatConfig.ValueRO.AttackWindup;
        }

        // Sync HealthCurrent changes back to Health
        foreach (var (healthCurrent, healthMax, health) in
            SystemAPI.Query<RefRO<HealthCurrent>, RefRO<HealthMax>, RefRW<Health>>())
        {
            health.ValueRW.Current = healthCurrent.ValueRO.Value;
            health.ValueRW.Max = healthMax.ValueRO.Value;
        }
    }
}
