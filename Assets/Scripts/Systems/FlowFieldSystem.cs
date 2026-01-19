using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that generates and updates the flow field
/// Targets all player units (soldiers) as destinations
/// Runs every 30 frames, offset by 15 (staggered with other systems)
/// </summary>
public partial struct FlowFieldSystem : ISystem
{
    private int _frameCount;
    private bool _initialized;
    private const int UPDATE_INTERVAL = 30;
    private const int FRAME_OFFSET = 15; // Stagger offset
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FlowFieldConfig>();
    }
    
    public void OnDestroy(ref SystemState state)
    {
        FlowFieldData.Dispose();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<FlowFieldConfig>();
        
        // Initialize flow field data if needed
        if (!_initialized)
        {
            FlowFieldData.Create(
                config.GridWidth, 
                config.GridHeight, 
                config.CellSize, 
                config.WorldOffset
            );
            _initialized = true;
            UnityEngine.Debug.Log($"FlowField initialized: {config.GridWidth}x{config.GridHeight}");
        }
        
        // Only update flow field every N frames (with offset for staggering)
        _frameCount++;
        if ((_frameCount + FRAME_OFFSET) % UPDATE_INTERVAL != 0)
            return;
        
        // Collect all player unit positions as targets
        var targetPositions = new NativeList<float2>(Allocator.Temp);
        
        foreach (var (transform, playerTag) in 
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerUnit>>())
        {
            targetPositions.Add(new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y));
        }
        
        if (targetPositions.Length == 0)
        {
            targetPositions.Dispose();
            return;
        }
        
        // Generate flow field
        GenerateFlowField(config, targetPositions);
        
        targetPositions.Dispose();
    }
    
    void GenerateFlowField(FlowFieldConfig config, NativeList<float2> targets)
    {
        int width = config.GridWidth;
        int height = config.GridHeight;
        int totalCells = width * height;

        // Reset integration field
        for (int i = 0; i < totalCells; i++)
        {
            FlowFieldData.IntegrationField[i] = int.MaxValue;
        }

        // Dual buffer approach for wave-based parallel BFS
        // Use TempJob allocator since these will be passed to scheduled jobs
        var currentWave = new NativeList<int2>(Allocator.TempJob);
        var nextWave = new NativeList<int2>(Allocator.TempJob);

        // Set all target cells to cost 0 and add to first wave
        for (int i = 0; i < targets.Length; i++)
        {
            int2 cell = FlowFieldData.WorldToGrid(targets[i]);
            int index = FlowFieldData.GridToIndex(cell);

            if (index >= 0 && index < totalCells)
            {
                FlowFieldData.IntegrationField[index] = 0;
                currentWave.Add(cell);
            }
        }

        // Neighbor offsets (8 directions)
        // Use TempJob allocator since this will be passed to scheduled jobs
        var neighbors = new NativeArray<int2>(8, Allocator.TempJob);
        neighbors[0] = new int2(1, 0);   // Right
        neighbors[1] = new int2(-1, 0);  // Left
        neighbors[2] = new int2(0, 1);   // Up
        neighbors[3] = new int2(0, -1);  // Down
        neighbors[4] = new int2(1, 1);   // Diagonal
        neighbors[5] = new int2(-1, 1);
        neighbors[6] = new int2(1, -1);
        neighbors[7] = new int2(-1, -1);

        // Process waves in parallel until no more cells to process
        while (currentWave.Length > 0)
        {
            // Ensure nextWave has capacity for potential new cells
            // Each cell can add up to 8 neighbors, but typically much less
            if (nextWave.Capacity < currentWave.Length * 8)
            {
                nextWave.Capacity = currentWave.Length * 8;
            }

            // Process all cells in current wave in parallel
            var processWaveJob = new ProcessWaveJob
            {
                CurrentWave = currentWave.AsArray(),
                IntegrationField = FlowFieldData.IntegrationField,
                Walkable = FlowFieldData.Walkable,
                Neighbors = neighbors,
                NextWave = nextWave.AsParallelWriter(),
                GridWidth = width,
                GridHeight = height
            };

            // Schedule parallel job - batch size of 64 cells per thread
            processWaveJob.Schedule(currentWave.Length, 64).Complete();

            // Swap buffers for next iteration - clear current and swap references
            currentWave.Clear();
            (currentWave, nextWave) = (nextWave, currentWave);
        }

        // Generate flow directions from integration field (also parallelized)
        var generateFlowJob = new GenerateFlowDirectionsJob
        {
            IntegrationField = FlowFieldData.IntegrationField,
            FlowDirections = FlowFieldData.FlowDirections,
            Neighbors = neighbors,
            GridWidth = width,
            GridHeight = height
        };

        generateFlowJob.Schedule(totalCells, 256).Complete();

        neighbors.Dispose();
        currentWave.Dispose();
        nextWave.Dispose();
    }
}

/// <summary>
/// Parallel job to process one wave of BFS
/// Each cell in the wave checks its neighbors and adds valid ones to the next wave
/// </summary>
[BurstCompile]
unsafe struct ProcessWaveJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int2> CurrentWave;
    [NativeDisableParallelForRestriction] public NativeArray<int> IntegrationField;
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<bool> Walkable;
    [ReadOnly] public NativeArray<int2> Neighbors;
    public NativeList<int2>.ParallelWriter NextWave;

    public int GridWidth;
    public int GridHeight;

    public void Execute(int index)
    {
        int2 current = CurrentWave[index];
        int currentIndex = GridToIndex(current);
        int currentCost = IntegrationField[currentIndex];

        // Check all 8 neighbors
        for (int i = 0; i < 8; i++)
        {
            int2 neighbor = current + Neighbors[i];

            // Bounds check
            if (neighbor.x < 0 || neighbor.x >= GridWidth ||
                neighbor.y < 0 || neighbor.y >= GridHeight)
                continue;

            int neighborIndex = GridToIndex(neighbor);

            // Skip if not walkable
            if (!Walkable[neighborIndex])
                continue;

            // Calculate cost (diagonal = 14, cardinal = 10)
            int moveCost = (i < 4) ? 10 : 14;
            int newCost = currentCost + moveCost;

            // Atomic comparison and update to handle race conditions
            int originalCost = IntegrationField[neighborIndex];

            // Only update if we found a better path
            if (newCost < originalCost)
            {
                // Try to update atomically using Interlocked for thread safety
                int* ptr = (int*)IntegrationField.GetUnsafePtr();
                int previousCost = System.Threading.Interlocked.CompareExchange(
                    ref ptr[neighborIndex],
                    newCost,
                    originalCost
                );

                // If we successfully updated, add to next wave
                if (previousCost == originalCost)
                {
                    NextWave.AddNoResize(neighbor);
                }
            }
        }
    }

    readonly int GridToIndex(int2 cell)
    {
        return cell.y * GridWidth + cell.x;
    }
}

/// <summary>
/// Parallel job to generate flow directions from integration field
/// Each cell finds its neighbor with the lowest cost
/// </summary>
[BurstCompile]
struct GenerateFlowDirectionsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int> IntegrationField;
    [NativeDisableParallelForRestriction] public NativeArray<float2> FlowDirections;
    [ReadOnly] public NativeArray<int2> Neighbors;

    public int GridWidth;
    public int GridHeight;

    public void Execute(int index)
    {
        // Convert index to 2D coords
        int x = index % GridWidth;
        int y = index / GridWidth;

        // Skip if unreachable
        if (IntegrationField[index] == int.MaxValue)
        {
            FlowDirections[index] = float2.zero;
            return;
        }

        // Find neighbor with lowest cost
        int2 current = new int2(x, y);
        int lowestCost = IntegrationField[index];
        float2 bestDirection = float2.zero;

        for (int i = 0; i < 8; i++)
        {
            int2 neighbor = current + Neighbors[i];

            // Bounds check
            if (neighbor.x < 0 || neighbor.x >= GridWidth ||
                neighbor.y < 0 || neighbor.y >= GridHeight)
                continue;

            int neighborIndex = neighbor.y * GridWidth + neighbor.x;
            int neighborCost = IntegrationField[neighborIndex];

            if (neighborCost < lowestCost)
            {
                lowestCost = neighborCost;
                bestDirection = math.normalize(new float2(Neighbors[i].x, Neighbors[i].y));
            }
        }

        FlowDirections[index] = bestDirection;
    }
}
