using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that generates a flow field for soldier pathfinding to their click destination.
/// Regenerates when the move command destination changes.
/// Uses the same walkable grid as the zombie flow field.
/// </summary>
[UpdateBefore(typeof(UnitMovementSystem))]
public partial struct SoldierFlowFieldSystem : ISystem
{
    private bool _initialized;
    private float2 _lastDestination;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FlowFieldConfig>();
    }

    public void OnDestroy(ref SystemState state)
    {
        SoldierFlowFieldData.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Wait for main flow field to be created (it has the walkable data)
        if (!FlowFieldData.IsCreated)
            return;

        // Initialize soldier flow field if needed
        if (!_initialized)
        {
            SoldierFlowFieldData.Create();
            _initialized = true;
            UnityEngine.Debug.Log("[SoldierFlowField] Initialized");
        }

        if (!SoldierFlowFieldData.IsCreated)
            return;

        // Find the current move command destination (from any soldier with an active command)
        float2 destination = float2.zero;
        bool hasDestination = false;

        foreach (var (moveCommand, transform) in
            SystemAPI.Query<RefRO<MoveCommand>, RefRO<LocalTransform>>()
            .WithAll<PlayerUnit>())
        {
            if (moveCommand.ValueRO.HasCommand)
            {
                destination = moveCommand.ValueRO.Target;
                hasDestination = true;
                break; // Use first soldier's destination (they all share the same one)
            }
        }

        if (!hasDestination)
        {
            SoldierFlowFieldData.HasDestination = false;
            return;
        }

        // Check if destination changed (with small tolerance for floating point)
        float distSq = math.distancesq(destination, _lastDestination);
        if (SoldierFlowFieldData.HasDestination && distSq < 0.1f)
        {
            return; // Same destination, no need to regenerate
        }

        // Generate new flow field to this destination
        _lastDestination = destination;
        SoldierFlowFieldData.CurrentDestination = destination;
        GenerateFlowField(destination);
        SoldierFlowFieldData.HasDestination = true;
    }

    void GenerateFlowField(float2 destination)
    {
        int width = FlowFieldData.GridWidth;
        int height = FlowFieldData.GridHeight;
        int totalCells = width * height;

        // Reset integration field
        for (int i = 0; i < totalCells; i++)
        {
            SoldierFlowFieldData.IntegrationField[i] = int.MaxValue;
        }

        // BFS from destination
        var currentWave = new NativeList<int2>(Allocator.TempJob);
        var nextWave = new NativeList<int2>(Allocator.TempJob);

        // Get destination cell - snap to nearest walkable if destination is in obstacle
        int2 destCell = FlowFieldData.WorldToGrid(destination);
        int destIndex = FlowFieldData.GridToIndex(destCell);

        // Check if destination is walkable; if not, find nearest walkable cell
        if (destIndex >= 0 && destIndex < totalCells && !FlowFieldData.Walkable[destIndex])
        {
            destCell = FindNearestWalkableCell(destCell, width, height);
            destIndex = FlowFieldData.GridToIndex(destCell);
        }

        if (destIndex >= 0 && destIndex < totalCells && FlowFieldData.Walkable[destIndex])
        {
            SoldierFlowFieldData.IntegrationField[destIndex] = 0;
            currentWave.Add(destCell);

            // Update the actual destination to the walkable cell center
            float2 actualDest = new float2(
                destCell.x * FlowFieldData.CellSize - FlowFieldData.WorldOffset.x + FlowFieldData.CellSize * 0.5f,
                destCell.y * FlowFieldData.CellSize - FlowFieldData.WorldOffset.y + FlowFieldData.CellSize * 0.5f
            );
            SoldierFlowFieldData.CurrentDestination = actualDest;
        }

        // Neighbor offsets (8 directions)
        var neighbors = new NativeArray<int2>(8, Allocator.TempJob);
        neighbors[0] = new int2(1, 0);   // Right
        neighbors[1] = new int2(-1, 0);  // Left
        neighbors[2] = new int2(0, 1);   // Up
        neighbors[3] = new int2(0, -1);  // Down
        neighbors[4] = new int2(1, 1);   // Diagonal
        neighbors[5] = new int2(-1, 1);
        neighbors[6] = new int2(1, -1);
        neighbors[7] = new int2(-1, -1);

        // Process waves using parallel jobs
        while (currentWave.Length > 0)
        {
            if (nextWave.Capacity < currentWave.Length * 8)
            {
                nextWave.Capacity = currentWave.Length * 8;
            }

            var processWaveJob = new ProcessSoldierWaveJob
            {
                CurrentWave = currentWave.AsArray(),
                IntegrationField = SoldierFlowFieldData.IntegrationField,
                Walkable = FlowFieldData.Walkable, // Use shared walkable grid
                Neighbors = neighbors,
                NextWave = nextWave.AsParallelWriter(),
                GridWidth = width,
                GridHeight = height
            };

            processWaveJob.Schedule(currentWave.Length, 64).Complete();

            currentWave.Clear();
            (currentWave, nextWave) = (nextWave, currentWave);
        }

        // Generate flow directions
        var generateFlowJob = new GenerateSoldierFlowDirectionsJob
        {
            IntegrationField = SoldierFlowFieldData.IntegrationField,
            FlowDirections = SoldierFlowFieldData.FlowDirections,
            Neighbors = neighbors,
            GridWidth = width,
            GridHeight = height
        };

        generateFlowJob.Schedule(totalCells, 256).Complete();

        neighbors.Dispose();
        currentWave.Dispose();
        nextWave.Dispose();
    }

    /// <summary>
    /// Find the nearest walkable cell to the given cell using expanding ring search
    /// </summary>
    int2 FindNearestWalkableCell(int2 cell, int width, int height)
    {
        const int maxSearchRadius = 10;

        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            float bestDistSq = float.MaxValue;
            int2 bestCell = cell;
            bool found = false;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Only check cells on the edge of the ring
                    if (math.abs(dx) != radius && math.abs(dy) != radius)
                        continue;

                    int2 checkCell = cell + new int2(dx, dy);

                    if (checkCell.x < 0 || checkCell.x >= width ||
                        checkCell.y < 0 || checkCell.y >= height)
                        continue;

                    int checkIndex = checkCell.y * width + checkCell.x;

                    if (FlowFieldData.Walkable[checkIndex])
                    {
                        float distSq = dx * dx + dy * dy;
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestCell = checkCell;
                            found = true;
                        }
                    }
                }
            }

            if (found)
                return bestCell;
        }

        return cell; // Fallback to original if nothing found
    }
}

/// <summary>
/// Parallel job to process one wave of BFS for soldier flow field
/// </summary>
[BurstCompile]
unsafe struct ProcessSoldierWaveJob : IJobParallelFor
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

        for (int i = 0; i < 8; i++)
        {
            int2 neighbor = current + Neighbors[i];

            if (neighbor.x < 0 || neighbor.x >= GridWidth ||
                neighbor.y < 0 || neighbor.y >= GridHeight)
                continue;

            int neighborIndex = GridToIndex(neighbor);

            if (!Walkable[neighborIndex])
                continue;

            int moveCost = (i < 4) ? 10 : 14;
            int newCost = currentCost + moveCost;

            int originalCost = IntegrationField[neighborIndex];

            if (newCost < originalCost)
            {
                int* ptr = (int*)IntegrationField.GetUnsafePtr();
                int previousCost = System.Threading.Interlocked.CompareExchange(
                    ref ptr[neighborIndex],
                    newCost,
                    originalCost
                );

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
/// Parallel job to generate flow directions for soldier pathfinding
/// </summary>
[BurstCompile]
struct GenerateSoldierFlowDirectionsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int> IntegrationField;
    [NativeDisableParallelForRestriction] public NativeArray<float2> FlowDirections;
    [ReadOnly] public NativeArray<int2> Neighbors;

    public int GridWidth;
    public int GridHeight;

    public void Execute(int index)
    {
        int x = index % GridWidth;
        int y = index / GridWidth;

        if (IntegrationField[index] == int.MaxValue)
        {
            FlowDirections[index] = float2.zero;
            return;
        }

        int2 current = new int2(x, y);
        int lowestCost = IntegrationField[index];
        float2 bestDirection = float2.zero;

        for (int i = 0; i < 8; i++)
        {
            int2 neighbor = current + Neighbors[i];

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
