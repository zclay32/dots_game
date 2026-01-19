using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that processes noise events and alerts nearby zombies
/// Uses spatial hash for efficient neighbor lookup instead of iterating all zombies
/// </summary>
[UpdateAfter(typeof(CombatSystem))]
public partial struct NoiseAlertSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        NoiseEventManager.Initialize();
        state.RequireForUpdate<SpatialHashConfig>();
    }
    
    public void OnDestroy(ref SystemState state)
    {
        NoiseEventManager.Dispose();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        if (!NoiseEventManager.IsCreated)
            return;

        // Use double-buffered spatial hash - ReadBuffer has last frame's completed data
        if (!SpatialHashDoubleBuffer.IsCreated)
            return;

        var hashMap = SpatialHashDoubleBuffer.ReadBuffer;
        if (!hashMap.IsCreated)
            return;

        // Hash map is guaranteed ready - using ReadBuffer (1-frame-old data, guaranteed complete)
        var config = SystemAPI.GetSingleton<SpatialHashConfig>();
        var zombieStateLookup = SystemAPI.GetComponentLookup<ZombieState>(false);
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        
        // Process all pending noise events
        while (NoiseEventManager.PendingNoises.TryDequeue(out NoiseEvent noise))
        {
            AlertZombiesNearNoise(
                noise.Position, 
                noise.Radius, 
                hashMap, 
                config.CellSize, 
                config.WorldOffset,
                ref zombieStateLookup,
                ref transformLookup
            );
        }
    }
    
    void AlertZombiesNearNoise(
        float2 noisePos, 
        float radius,
        NativeParallelMultiHashMap<int, Entity> hashMap,
        float cellSize,
        float2 worldOffset,
        ref ComponentLookup<ZombieState> zombieStateLookup,
        ref ComponentLookup<LocalTransform> transformLookup)
    {
        float radiusSq = radius * radius;
        int2 centerCell = SpatialHashHelper.WorldToCell(noisePos, cellSize, worldOffset);
        int cellRadius = (int)math.ceil(radius / cellSize);
        
        // Only check cells within noise radius
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                int2 cell = centerCell + new int2(x, y);
                int hash = SpatialHashHelper.GetCellHash(cell);
                
                if (!hashMap.TryGetFirstValue(hash, out Entity entity, out var iterator))
                    continue;
                
                do
                {
                    // Skip if not a zombie with state
                    if (!zombieStateLookup.HasComponent(entity))
                        continue;
                    
                    var zombieState = zombieStateLookup[entity];
                    
                    // Only alert dormant zombies
                    if (zombieState.State != ZombieAIState.Dormant)
                        continue;
                    
                    // Check actual distance
                    if (!transformLookup.HasComponent(entity))
                        continue;
                    
                    var transform = transformLookup[entity];
                    float2 zombiePos = new float2(transform.Position.x, transform.Position.y);
                    float distSq = math.distancesq(zombiePos, noisePos);
                    
                    if (distSq <= radiusSq)
                    {
                        zombieState.State = ZombieAIState.Alert;
                        zombieState.AlertTimer = 5f;
                        zombieStateLookup[entity] = zombieState;
                    }
                    
                } while (hashMap.TryGetNextValue(out entity, ref iterator));
            }
        }
    }
}
