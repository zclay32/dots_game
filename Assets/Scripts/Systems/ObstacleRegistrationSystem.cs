using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Registers obstacles with the flow field by marking cells as unwalkable.
/// Runs once after the flow field is created, and re-runs if obstacle count changes.
/// </summary>
[UpdateAfter(typeof(FlowFieldSystem))]
public partial struct ObstacleRegistrationSystem : ISystem
{
    private bool _registered;
    private int _lastObstacleCount;

    public void OnUpdate(ref SystemState state)
    {
        // Wait for flow field to be created
        if (!FlowFieldData.IsCreated)
            return;

        // Count obstacles
        int obstacleCount = 0;
        foreach (var obstacle in SystemAPI.Query<RefRO<Obstacle>>())
        {
            obstacleCount++;
        }

        // Skip if already registered and count unchanged
        if (_registered && obstacleCount == _lastObstacleCount)
            return;

        // Reset walkable grid (in case obstacles were removed)
        for (int i = 0; i < FlowFieldData.Walkable.Length; i++)
        {
            FlowFieldData.Walkable[i] = true;
        }

        int totalCellsMarked = 0;

        // Mark obstacle cells as unwalkable
        foreach (var (obstacle, transform) in
            SystemAPI.Query<RefRO<Obstacle>, RefRO<LocalTransform>>())
        {
            float2 obstaclePos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);
            int tileWidth = obstacle.ValueRO.TileWidth;
            int tileHeight = obstacle.ValueRO.TileHeight;

            // Get the center cell of the obstacle
            int2 centerCell = FlowFieldData.WorldToGrid(obstaclePos);

            // Calculate tile bounds (centered on obstacle position)
            // For a 4x4: halfWidth=2, so we go from center-2 to center+1 (4 tiles total)
            int halfWidth = tileWidth / 2;
            int halfHeight = tileHeight / 2;

            int2 minCell = new int2(centerCell.x - halfWidth, centerCell.y - halfHeight);
            int2 maxCell = new int2(centerCell.x + halfWidth - 1, centerCell.y + halfHeight - 1);

            // Adjust for odd dimensions (e.g., 3x3 should be -1 to +1)
            if (tileWidth % 2 == 1) maxCell.x++;
            if (tileHeight % 2 == 1) maxCell.y++;

            // Clamp to grid bounds
            minCell = math.max(minCell, int2.zero);
            maxCell = math.min(maxCell, new int2(FlowFieldData.GridWidth - 1, FlowFieldData.GridHeight - 1));

            // Mark all tiles in the obstacle footprint as unwalkable
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int index = FlowFieldData.GridToIndex(new int2(x, y));

                    if (index >= 0 && index < FlowFieldData.Walkable.Length && FlowFieldData.Walkable[index])
                    {
                        FlowFieldData.Walkable[index] = false;
                        totalCellsMarked++;
                    }
                }
            }
        }

        _registered = true;
        _lastObstacleCount = obstacleCount;

        // Trigger flow field regeneration so zombies path around obstacles
        FlowFieldData.NeedsRegeneration = true;

        UnityEngine.Debug.Log($"[ObstacleRegistration] Registered {obstacleCount} obstacles, marked {totalCellsMarked} cells as unwalkable");
    }
}
