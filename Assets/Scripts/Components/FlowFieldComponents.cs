using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// Flow field configuration singleton
/// </summary>
public struct FlowFieldConfig : IComponentData
{
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
    public float2 WorldOffset; // To convert world pos to grid pos
}

/// <summary>
/// Tag to mark the flow field target (e.g., command center or player units)
/// </summary>
public struct FlowFieldTarget : IComponentData { }

/// <summary>
/// Static class to hold the flow field data
/// Using static because NativeArrays can't be stored in IComponentData
/// </summary>
public static class FlowFieldData
{
    // Direction to move from each cell (normalized float2)
    public static NativeArray<float2> FlowDirections;
    
    // Cost to reach target from each cell (for debugging/visualization)
    public static NativeArray<int> IntegrationField;
    
    // Whether each cell is walkable
    public static NativeArray<bool> Walkable;
    
    public static bool IsCreated => FlowDirections.IsCreated;
    
    public static int GridWidth;
    public static int GridHeight;
    public static float CellSize;
    public static float2 WorldOffset;
    
    public static void Create(int width, int height, float cellSize, float2 worldOffset)
    {
        Dispose();
        
        GridWidth = width;
        GridHeight = height;
        CellSize = cellSize;
        WorldOffset = worldOffset;
        
        int totalCells = width * height;
        FlowDirections = new NativeArray<float2>(totalCells, Allocator.Persistent);
        IntegrationField = new NativeArray<int>(totalCells, Allocator.Persistent);
        Walkable = new NativeArray<bool>(totalCells, Allocator.Persistent);
        
        // Initialize all cells as walkable
        for (int i = 0; i < totalCells; i++)
        {
            Walkable[i] = true;
            IntegrationField[i] = int.MaxValue;
            FlowDirections[i] = float2.zero;
        }
    }
    
    public static void Dispose()
    {
        if (FlowDirections.IsCreated) FlowDirections.Dispose();
        if (IntegrationField.IsCreated) IntegrationField.Dispose();
        if (Walkable.IsCreated) Walkable.Dispose();
    }
    
    /// <summary>
    /// Convert world position to grid cell coordinates
    /// </summary>
    public static int2 WorldToGrid(float2 worldPos)
    {
        float2 localPos = worldPos + WorldOffset;
        return new int2(
            (int)math.floor(localPos.x / CellSize),
            (int)math.floor(localPos.y / CellSize)
        );
    }
    
    /// <summary>
    /// Convert grid cell to flat array index
    /// </summary>
    public static int GridToIndex(int2 cell)
    {
        if (cell.x < 0 || cell.x >= GridWidth || cell.y < 0 || cell.y >= GridHeight)
            return -1;
        return cell.y * GridWidth + cell.x;
    }
    
    /// <summary>
    /// Get flow direction at world position
    /// </summary>
    public static float2 GetFlowDirection(float2 worldPos)
    {
        int2 cell = WorldToGrid(worldPos);
        int index = GridToIndex(cell);
        
        if (index < 0 || index >= FlowDirections.Length)
            return float2.zero;
        
        return FlowDirections[index];
    }
}
