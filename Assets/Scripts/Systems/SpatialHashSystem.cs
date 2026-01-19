using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that builds the spatial hash map each frame using double buffering.
/// This allows O(1) lookup of nearby entities without blocking the main thread.
///
/// DOUBLE BUFFER PATTERN:
/// - Swap buffers at frame start (ReadBuffer now has last frame's completed data)
/// - Build new data into WriteBuffer (runs in parallel, no Complete() needed)
/// - Other systems read from ReadBuffer (1-frame-old data, guaranteed complete)
///
/// Trade-off: 1-frame latency for spatial queries. Acceptable because:
/// - Units don't move far in a single frame
/// - Eliminates main thread sync point that was bottlenecking performance
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial struct SpatialHashSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialHashConfig>();

        // Initialize double buffer with capacity for ~10000 entities
        SpatialHashDoubleBuffer.Create(10000);
    }

    public void OnDestroy(ref SystemState state)
    {
        SpatialHashDoubleBuffer.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<SpatialHashConfig>();

        // Swap buffers at frame start - ReadBuffer now contains last frame's completed data
        SpatialHashDoubleBuffer.SwapBuffers();

        // Count entities to size the hashmap appropriately
        var unitQuery = SystemAPI.QueryBuilder().WithAll<Unit, LocalTransform>().Build();
        int entityCount = unitQuery.CalculateEntityCount();

        // Ensure capacity in write buffer
        SpatialHashDoubleBuffer.EnsureWriteBufferCapacity(entityCount + 1000);

        // Clear write buffer for new frame
        SpatialHashDoubleBuffer.ClearWriteBuffer();

        // Build the spatial hash map into WriteBuffer
        var buildJob = new BuildSpatialHashJob
        {
            SpatialHashMap = SpatialHashDoubleBuffer.GetWriteBufferParallelWriter(),
            CellSize = config.CellSize,
            WorldOffset = config.WorldOffset
        };

        // Schedule job - NO Complete() call!
        state.Dependency = buildJob.ScheduleParallel(state.Dependency);

        // Store the job handle so we can complete it next frame during swap
        SpatialHashDoubleBuffer.SetWriteBufferJobHandle(state.Dependency);

        // Also update legacy singleton for backward compatibility during migration
        // Systems can use either SpatialHashDoubleBuffer.ReadBuffer or SpatialHashMapSingleton.HashMap
        SpatialHashMapSingleton.HashMap = SpatialHashDoubleBuffer.ReadBuffer;
        SpatialHashMapSingleton.BuildJobHandle = SpatialHashDoubleBuffer.ReadBufferJobHandle;
    }
}

/// <summary>
/// Job that populates the spatial hash map in parallel
/// </summary>
[BurstCompile]
public partial struct BuildSpatialHashJob : IJobEntity
{
    public NativeParallelMultiHashMap<int, Entity>.ParallelWriter SpatialHashMap;
    public float CellSize;
    public float2 WorldOffset;
    
    void Execute(Entity entity, in LocalTransform transform, in Unit unit)
    {
        float2 pos = new float2(transform.Position.x, transform.Position.y);
        int2 cell = SpatialHashHelper.WorldToCell(pos, CellSize, WorldOffset);
        int hash = SpatialHashHelper.GetCellHash(cell);
        
        SpatialHashMap.Add(hash, entity);
    }
}

/// <summary>
/// Empty component to mark that spatial hash data exists
/// </summary>
public struct SpatialHashMapData : IComponentData { }

/// <summary>
/// Static class to hold reference to the spatial hash map
/// (Workaround since we can't store NativeContainer in IComponentData)
///
/// NOTE: SpatialHashSystem completes its build job before storing the HashMap reference,
/// so all systems that run after SpatialHashSystem can safely read from HashMap.
/// </summary>
public static class SpatialHashMapSingleton
{
    public static NativeParallelMultiHashMap<int, Entity> HashMap;

    // BuildJobHandle kept for backward compatibility but is always default (completed)
    public static JobHandle BuildJobHandle;

    /// <summary>
    /// Get all entities in a cell
    /// </summary>
    public static void GetEntitiesInCell(int2 cell, ref NativeList<Entity> results)
    {
        if (!HashMap.IsCreated)
            return;

        int hash = SpatialHashHelper.GetCellHash(cell);

        if (HashMap.TryGetFirstValue(hash, out Entity entity, out var iterator))
        {
            do
            {
                results.Add(entity);
            } while (HashMap.TryGetNextValue(out entity, ref iterator));
        }
    }

    /// <summary>
    /// Get all entities within a radius (checks neighboring cells)
    /// </summary>
    public static void GetEntitiesInRadius(float2 center, float radius, float cellSize, float2 worldOffset, ref NativeList<Entity> results)
    {
        if (!HashMap.IsCreated)
            return;

        int2 centerCell = SpatialHashHelper.WorldToCell(center, cellSize, worldOffset);
        int cellRadius = (int)math.ceil(radius / cellSize);

        // Check all cells within radius
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                int2 cell = centerCell + new int2(x, y);
                int hash = SpatialHashHelper.GetCellHash(cell);

                if (HashMap.TryGetFirstValue(hash, out Entity entity, out var iterator))
                {
                    do
                    {
                        results.Add(entity);
                    } while (HashMap.TryGetNextValue(out entity, ref iterator));
                }
            }
        }
    }
}
