using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that marks dead entities, triggers death effects, and destroys them
/// Batches death effects for better performance
/// </summary>
[UpdateAfter(typeof(CombatSystem))]
public partial struct DeathSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Ensure death effect manager is ready
        if (!DeathEffectManager.IsCreated)
            DeathEffectManager.Initialize();
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var lastHitLookup = SystemAPI.GetComponentLookup<LastHitDirection>(true);

        // Collect deaths first, then process
        foreach (var (health, transform, faction, entity) in
            SystemAPI.Query<RefRO<Health>, RefRO<LocalTransform>, RefRO<Faction>>()
            .WithEntityAccess())
        {
            if (health.ValueRO.IsDead)
            {
                // Trigger death effect
                float2 pos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);
                bool isEnemy = faction.ValueRO.Value == FactionType.Enemy;

                // Get impact direction if available
                float2 impactDir = float2.zero;
                if (lastHitLookup.HasComponent(entity))
                {
                    impactDir = lastHitLookup[entity].Direction;
                }

                DeathEffectManager.CreateDeathEffect(pos, isEnemy, impactDir);

                // Destroy entity
                ecb.DestroyEntity(entity);
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
