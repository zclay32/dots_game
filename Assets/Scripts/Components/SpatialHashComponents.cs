using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// Component that stores which cell an entity belongs to
/// </summary>
public struct SpatialCell : IComponentData
{
    public int2 Cell;
}

/// <summary>
/// Singleton component containing the spatial hash grid configuration
/// </summary>
public struct SpatialHashConfig : IComponentData
{
    public float CellSize;
    public int2 GridSize; // Number of cells in each dimension
    public float2 WorldOffset; // Offset to handle negative coordinates
}

/// <summary>
/// Helper struct for spatial hash operations
/// </summary>
public static class SpatialHashHelper
{
    /// <summary>
    /// Convert world position to cell coordinates
    /// </summary>
    public static int2 WorldToCell(float2 worldPos, float cellSize, float2 worldOffset)
    {
        float2 offsetPos = worldPos + worldOffset;
        return new int2(
            (int)math.floor(offsetPos.x / cellSize),
            (int)math.floor(offsetPos.y / cellSize)
        );
    }
    
    /// <summary>
    /// Convert cell coordinates to a flat index for array storage
    /// </summary>
    public static int CellToIndex(int2 cell, int2 gridSize)
    {
        // Clamp to grid bounds
        int x = math.clamp(cell.x, 0, gridSize.x - 1);
        int y = math.clamp(cell.y, 0, gridSize.y - 1);
        return y * gridSize.x + x;
    }
    
    /// <summary>
    /// Get hash key for a cell (for use with NativeParallelMultiHashMap)
    /// </summary>
    public static int GetCellHash(int2 cell)
    {
        // Simple hash combining x and y
        return cell.x * 73856093 ^ cell.y * 19349663;
    }
}
