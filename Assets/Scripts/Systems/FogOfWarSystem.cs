using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that updates fog of war visibility based on soldier positions.
/// Uses frame skipping for performance optimization.
///
/// Algorithm:
/// 1. Decay all Visible cells to Explored
/// 2. For each soldier, mark cells within vision radius as Visible
/// 3. FogOfWarManager syncs changes to tilemap in LateUpdate
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class FogOfWarSystem : SystemBase
{
    private const int UPDATE_INTERVAL = 4;  // Update every 4 frames (~15 FPS for fog)
    private int _frameCount;

    protected override void OnUpdate()
    {
        _frameCount++;
        if (_frameCount % UPDATE_INTERVAL != 0)
            return;

        // Wait for FogOfWarManager to initialize
        var fogManager = FogOfWarManager.Instance;
        if (fogManager == null || !fogManager.IsInitialized)
            return;

        var gridManager = IsometricGridManager.Instance;
        if (gridManager == null || gridManager.Grid == null)
            return;

        var visibilityGrid = fogManager.GetVisibilityGrid();
        var dirtyFlags = fogManager.GetDirtyFlags();

        int gridWidth = fogManager.GridWidth;
        int gridHeight = fogManager.GridHeight;
        int minCellX = fogManager.MinCellX;
        int minCellY = fogManager.MinCellY;
        int mapRadius = gridManager.mapRadiusInTiles;

        // Step 1: Decay Visible → Explored
        DecayVisibility(visibilityGrid, dirtyFlags, gridWidth, gridHeight,
            minCellX, minCellY, mapRadius);

        // Step 2: Mark cells visible for each soldier with vision
        float cellSizeX = gridManager.cellSize.x;
        float cellSizeY = gridManager.cellSize.y;

        foreach (var (transform, visionSource) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<VisionSource>>()
            .WithAll<PlayerUnit>())
        {
            float3 worldPos = transform.ValueRO.Position;
            float visionRadius = visionSource.ValueRO.VisionRadius;

            RevealCellsInRadius(
                visibilityGrid, dirtyFlags,
                worldPos.x, worldPos.y,
                visionRadius,
                gridWidth, gridHeight,
                minCellX, minCellY,
                cellSizeX, cellSizeY,
                mapRadius,
                gridManager.Grid);
        }

        // Mark manager dirty if any changes occurred
        bool anyDirty = false;
        for (int i = 0; i < dirtyFlags.Length; i++)
        {
            if (dirtyFlags[i])
            {
                anyDirty = true;
                break;
            }
        }

        if (anyDirty)
        {
            fogManager.MarkDirty();
        }
    }

    private void DecayVisibility(
        NativeArray<byte> visibilityGrid,
        NativeArray<bool> dirtyFlags,
        int gridWidth,
        int gridHeight,
        int minCellX,
        int minCellY,
        int mapRadius)
    {
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                // Only process cells within map bounds (diamond shape)
                int cellX = x + minCellX;
                int cellY = y + minCellY;
                int manhattanDist = math.abs(cellX) + math.abs(cellY);

                if (manhattanDist > mapRadius)
                    continue;

                int index = y * gridWidth + x;

                if (visibilityGrid[index] == (byte)VisibilityState.Visible)
                {
                    visibilityGrid[index] = (byte)VisibilityState.Explored;
                    dirtyFlags[index] = true;
                }
            }
        }
    }

    private void RevealCellsInRadius(
        NativeArray<byte> visibilityGrid,
        NativeArray<bool> dirtyFlags,
        float worldX,
        float worldY,
        float visionRadius,
        int gridWidth,
        int gridHeight,
        int minCellX,
        int minCellY,
        float cellSizeX,
        float cellSizeY,
        int mapRadius,
        UnityEngine.Grid grid)
    {
        // Convert world position to cell
        var worldPos = new UnityEngine.Vector3(worldX, worldY, 0);
        var centerCell = grid.WorldToCell(worldPos);

        // Convert vision radius from world units to cell units
        // Use average cell size for uniform circular appearance in cell space
        float avgCellSize = (cellSizeX + cellSizeY) * 0.5f;
        float cellRadius = visionRadius / avgCellSize;
        float cellRadiusSq = cellRadius * cellRadius;
        int cellRadiusInt = (int)math.ceil(cellRadius) + 1;

        // Iterate over cells in bounding box
        for (int dy = -cellRadiusInt; dy <= cellRadiusInt; dy++)
        {
            for (int dx = -cellRadiusInt; dx <= cellRadiusInt; dx++)
            {
                int cellX = centerCell.x + dx;
                int cellY = centerCell.y + dy;

                // Check map bounds (diamond shape)
                int manhattanDist = math.abs(cellX) + math.abs(cellY);
                if (manhattanDist > mapRadius)
                    continue;

                // Check grid array bounds
                int localX = cellX - minCellX;
                int localY = cellY - minCellY;

                if (localX < 0 || localX >= gridWidth ||
                    localY < 0 || localY >= gridHeight)
                    continue;

                // Calculate distance in cell space for uniform isometric appearance
                float distSq = dx * dx + dy * dy;

                // Check if within vision radius (in cell space)
                if (distSq <= cellRadiusSq)
                {
                    int index = localY * gridWidth + localX;

                    if (visibilityGrid[index] != (byte)VisibilityState.Visible)
                    {
                        visibilityGrid[index] = (byte)VisibilityState.Visible;
                        dirtyFlags[index] = true;
                    }
                }
            }
        }
    }
}
