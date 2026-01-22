using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// ECS system that calculates fog of war visibility based on soldier positions.
/// Updates the FogOfWarManager's visibility grid every 4 frames.
///
/// Algorithm:
/// 1. Decay all Visible cells to Explored
/// 2. For each soldier with VisionSource, mark cells within vision radius as Visible
/// </summary>
[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct FogOfWarSystem : ISystem
{
    private int _frameCount;
    private const int UPDATE_INTERVAL = 8;  // Update fog every 8 frames (~7.5 FPS at 60 FPS) for better performance

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FogOfWarConfig>();
        _frameCount = 0;
    }

    public void OnUpdate(ref SystemState state)
    {
        _frameCount++;
        if (_frameCount % UPDATE_INTERVAL != 0) return;

        // Get the FogOfWarManager singleton
        var fogManager = FogOfWarManager.Instance;
        if (fogManager == null || !fogManager.IsInitialized)
            return;

        var config = SystemAPI.GetSingleton<FogOfWarConfig>();

        // Step 1: Decay all Visible cells to Explored
        fogManager.DecayVisibility();

        // Step 2: For each unit with VisionSource, reveal cells within vision radius
        foreach (var (visionSource, transform) in
            SystemAPI.Query<RefRO<VisionSource>, RefRO<LocalTransform>>()
                .WithAll<PlayerUnit>())
        {
            float2 unitPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);
            float visionRadius = visionSource.ValueRO.VisionRadius;

            RevealArea(fogManager, config, unitPos, visionRadius);
        }
    }

    private void RevealArea(FogOfWarManager fogManager, FogOfWarConfig config, float2 unitPos, float visionRadius)
    {
        // For isometric grids with cell size (2, 1):
        // The Y axis is visually compressed by half (2:1 ratio).
        // To make vision appear circular in the isometric view, we need to
        // scale the Y distance by the aspect ratio when checking distance.

        float cellSizeX = fogManager.CellSizeX;  // 2.0
        float cellSizeY = fogManager.CellSizeY;  // 1.0
        float halfCellX = cellSizeX * 0.5f;
        float halfCellY = cellSizeY * 0.5f;

        // Aspect ratio for isometric projection (Y is compressed)
        float aspectRatio = cellSizeX / cellSizeY;  // 2.0

        // Calculate cell radius for the elliptical vision area
        // In isometric space, cells are spaced ~1.12 units apart diagonally
        float diagonalCellDist = math.sqrt(halfCellX * halfCellX + halfCellY * halfCellY);
        int cellRadius = (int)math.ceil(visionRadius / diagonalCellDist) + 1;

        int2 unitCell = fogManager.WorldToCell(unitPos);

        // Get the world position of the unit's cell center (single Unity API call)
        float2 unitCellCenter = fogManager.CellToWorld(unitCell);

        // Calculate offset from cell center to unit position
        float2 unitOffset = unitPos - unitCellCenter;

        float visionRadiusSq = visionRadius * visionRadius;

        // Iterate over cells within the bounding box
        for (int dy = -cellRadius; dy <= cellRadius; dy++)
        {
            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                int2 cell = new int2(unitCell.x + dx, unitCell.y + dy);

                // Check bounds
                if (!fogManager.IsCellInBounds(cell))
                    continue;

                // Calculate delta from unit cell to target cell in world space using isometric formula
                // Cell offset (dx, dy) maps to world offset ((dx+dy)*halfCellX, (dy-dx)*halfCellY)
                float2 cellDelta = new float2(
                    (dx + dy) * halfCellX,
                    (dy - dx) * halfCellY
                );

                // Subtract unit's offset from its cell center to get distance from unit to target cell center
                float2 delta = cellDelta - unitOffset;

                // Scale Y by aspect ratio to make vision appear circular in isometric view
                float scaledDistSq = delta.x * delta.x + (delta.y * aspectRatio) * (delta.y * aspectRatio);

                // If within vision radius, mark as visible
                if (scaledDistSq <= visionRadiusSq)
                {
                    int index = fogManager.CellToIndex(cell);
                    fogManager.SetVisibility(index, VisibilityState.Visible);
                }
            }
        }
    }
}
