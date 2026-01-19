using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that spawns soldiers and zombies at startup
/// </summary>
public partial struct CombatSpawnerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CombatSpawner>();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        // Only run once
        state.Enabled = false;
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var random = new Random(12345);
        
        foreach (var spawner in SystemAPI.Query<RefRO<CombatSpawner>>())
        {
            var data = spawner.ValueRO;
            
            // Spawn soldiers in center
            SpawnSoldiers(ref ecb, ref random, data);
            
            // Spawn zombies in ring around center
            SpawnZombies(ref ecb, ref random, data);
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        
        UnityEngine.Debug.Log("Combat spawner complete!");
    }
    
    void SpawnSoldiers(ref EntityCommandBuffer ecb, ref Random random, CombatSpawner data)
    {
        for (int i = 0; i < data.SoldierCount; i++)
        {
            var soldier = ecb.Instantiate(data.SoldierPrefab);
            
            // Random position in circle around spawn center
            float angle = random.NextFloat(0f, math.PI * 2f);
            float distance = random.NextFloat(0f, data.SoldierSpawnRadius);
            
            float3 position = new float3(
                data.SoldierSpawnCenter.x + math.cos(angle) * distance,
                data.SoldierSpawnCenter.y + math.sin(angle) * distance * 0.5f,
                0
            );
            
            ecb.SetComponent(soldier, LocalTransform.FromPosition(position));
            
            // Soldiers don't wander - they hold position (clear target)
            ecb.SetComponent(soldier, new TargetPosition { HasTarget = false });
        }
        
        UnityEngine.Debug.Log($"Spawned {data.SoldierCount} soldiers");
    }
    
    void SpawnZombies(ref EntityCommandBuffer ecb, ref Random random, CombatSpawner data)
    {
        for (int i = 0; i < data.ZombieCount; i++)
        {
            var zombie = ecb.Instantiate(data.ZombiePrefab);
            
            // Random position in ring (between min distance and spawn radius)
            // Use squared random to weight toward outer edge (more zombies far away)
            float angle = random.NextFloat(0f, math.PI * 2f);
            
            // Weight toward outer edge: use sqrt to bias toward max radius
            // t=0 -> minDistance, t=1 -> spawnRadius
            // Using pow(random, 0.5) biases toward 1 (outer edge)
            float t = math.sqrt(random.NextFloat(0f, 1f));
            float distance = math.lerp(data.ZombieMinDistance, data.ZombieSpawnRadius, t);
            
            float3 position = new float3(
                math.cos(angle) * distance,
                math.sin(angle) * distance * 0.5f, // Isometric
                0
            );
            
            ecb.SetComponent(zombie, LocalTransform.FromPosition(position));
            
            // Zombies start dormant - no target (they'll wake up from noise)
            ecb.SetComponent(zombie, new TargetPosition 
            { 
                Value = float2.zero, 
                HasTarget = false 
            });
        }
        
        UnityEngine.Debug.Log($"Spawned {data.ZombieCount} zombies (weighted toward outer edge)");
    }
}
