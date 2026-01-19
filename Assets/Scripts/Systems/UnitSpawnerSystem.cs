using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that spawns units at startup
/// </summary>
[BurstCompile]
public partial struct UnitSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitSpawner>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Only run once
        state.Enabled = false;
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var random = new Random(12345);
        
        foreach (var spawner in SystemAPI.Query<RefRO<UnitSpawner>>())
        {
            var spawnerData = spawner.ValueRO;
            
            for (int i = 0; i < spawnerData.SpawnCount; i++)
            {
                // Spawn unit from prefab
                var unit = ecb.Instantiate(spawnerData.UnitPrefab);
                
                // Random position within spawn radius
                float angle = random.NextFloat(0f, math.PI * 2f);
                float distance = random.NextFloat(0f, spawnerData.SpawnRadius);
                float3 position = new float3(
                    math.cos(angle) * distance,
                    math.sin(angle) * distance * 0.5f, // Isometric Y compression
                    0
                );
                
                ecb.SetComponent(unit, LocalTransform.FromPosition(position));

                // Random speed
                float speed = random.NextFloat(spawnerData.MinSpeed, spawnerData.MaxSpeed);
                ecb.SetComponent(unit, new MoveSpeed { Value = speed });

                // Random initial target
                float targetAngle = random.NextFloat(0f, math.PI * 2f);
                float targetDist = random.NextFloat(5f, spawnerData.SpawnRadius);
                ecb.SetComponent(unit, new TargetPosition
                {
                    Value = new float2(
                        math.cos(targetAngle) * targetDist,
                        math.sin(targetAngle) * targetDist * 0.5f
                    ),
                    HasTarget = true
                });

                // Note: ZombieSpawnPositionFixupSystem will fix SpawnPosition after spawning
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        
        UnityEngine.Debug.Log($"Spawned units!");
    }
}
