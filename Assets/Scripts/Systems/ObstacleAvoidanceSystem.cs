using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Prevents units from occupying unwalkable cells.
/// Uses the flow field walkable grid for O(1) collision checks.
/// Runs after movement to push units out of obstacles.
/// </summary>
[UpdateAfter(typeof(UnitMovementSystem))]
public partial struct ObstacleAvoidanceSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Wait for flow field to be created
        if (!FlowFieldData.IsCreated)
            return;

        float cellSize = FlowFieldData.CellSize;
        float2 worldOffset = FlowFieldData.WorldOffset;
        int gridWidth = FlowFieldData.GridWidth;
        int gridHeight = FlowFieldData.GridHeight;

        // Check all units (soldiers and zombies)
        foreach (var transform in
            SystemAPI.Query<RefRW<LocalTransform>>()
            .WithAny<PlayerUnit, EnemyUnit>())
        {
            float2 unitPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);
            int2 cell = FlowFieldData.WorldToGrid(unitPos);
            int index = FlowFieldData.GridToIndex(cell);

            // Skip if out of bounds or cell is walkable
            if (index < 0 || index >= FlowFieldData.Walkable.Length)
                continue;

            if (FlowFieldData.Walkable[index])
                continue;

            // Unit is in unwalkable cell - find nearest walkable cell and push there
            float2 newPos = FindNearestWalkablePosition(unitPos, cell, cellSize, worldOffset, gridWidth, gridHeight);

            transform.ValueRW.Position.x = newPos.x;
            transform.ValueRW.Position.y = newPos.y;
        }
    }

    private float2 FindNearestWalkablePosition(float2 currentPos, int2 currentCell, float cellSize, float2 worldOffset, int gridWidth, int gridHeight)
    {
        // Search in expanding rings around current cell
        const int maxSearchRadius = 5;

        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            float bestDist = float.MaxValue;
            int2 bestCell = currentCell;
            bool found = false;

            // Check all cells at this radius
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Only check cells on the edge of the current ring
                    if (math.abs(dx) != radius && math.abs(dy) != radius)
                        continue;

                    int2 checkCell = currentCell + new int2(dx, dy);

                    // Bounds check
                    if (checkCell.x < 0 || checkCell.x >= gridWidth ||
                        checkCell.y < 0 || checkCell.y >= gridHeight)
                        continue;

                    int checkIndex = checkCell.y * gridWidth + checkCell.x;

                    if (checkIndex >= 0 && checkIndex < FlowFieldData.Walkable.Length &&
                        FlowFieldData.Walkable[checkIndex])
                    {
                        // Calculate cell center
                        float2 cellCenter = new float2(
                            checkCell.x * cellSize - worldOffset.x + cellSize * 0.5f,
                            checkCell.y * cellSize - worldOffset.y + cellSize * 0.5f
                        );

                        float dist = math.distancesq(currentPos, cellCenter);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestCell = checkCell;
                            found = true;
                        }
                    }
                }
            }

            if (found)
            {
                // Return center of the best walkable cell
                return new float2(
                    bestCell.x * cellSize - worldOffset.x + cellSize * 0.5f,
                    bestCell.y * cellSize - worldOffset.y + cellSize * 0.5f
                );
            }
        }

        // Fallback - couldn't find walkable cell, stay in place
        return currentPos;
    }
}
