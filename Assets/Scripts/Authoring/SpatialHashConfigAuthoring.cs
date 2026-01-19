using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for spatial hash configuration
/// Add to a GameObject in your subscene to configure the spatial grid
/// </summary>
public class SpatialHashConfigAuthoring : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Size of each cell in world units")]
    public float cellSize = 2f;
    
    [Tooltip("Number of cells in X and Y dimensions")]
    public Vector2Int gridSize = new Vector2Int(100, 100);
    
    [Tooltip("World offset to handle negative coordinates (half of world size)")]
    public Vector2 worldOffset = new Vector2(100f, 100f);
    
    class Baker : Baker<SpatialHashConfigAuthoring>
    {
        public override void Bake(SpatialHashConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new SpatialHashConfig
            {
                CellSize = authoring.cellSize,
                GridSize = new int2(authoring.gridSize.x, authoring.gridSize.y),
                WorldOffset = new float2(authoring.worldOffset.x, authoring.worldOffset.y)
            });
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw grid bounds
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        
        float worldWidth = cellSize * gridSize.x;
        float worldHeight = cellSize * gridSize.y;
        
        Vector3 center = new Vector3(
            worldWidth / 2f - worldOffset.x,
            worldHeight / 2f - worldOffset.y,
            0
        );
        
        Vector3 size = new Vector3(worldWidth, worldHeight, 0);
        
        Gizmos.DrawWireCube(center, size);
        
        // Draw some grid lines
        Gizmos.color = new Color(0, 1, 0, 0.1f);
        int linesToDraw = Mathf.Min(gridSize.x, 50); // Limit lines for performance
        float step = worldWidth / linesToDraw;
        
        for (int i = 0; i <= linesToDraw; i++)
        {
            float x = i * step - worldOffset.x;
            Gizmos.DrawLine(
                new Vector3(x, -worldOffset.y, 0),
                new Vector3(x, worldHeight - worldOffset.y, 0)
            );
        }
        
        linesToDraw = Mathf.Min(gridSize.y, 50);
        step = worldHeight / linesToDraw;
        
        for (int i = 0; i <= linesToDraw; i++)
        {
            float y = i * step - worldOffset.y;
            Gizmos.DrawLine(
                new Vector3(-worldOffset.x, y, 0),
                new Vector3(worldWidth - worldOffset.x, y, 0)
            );
        }
    }
}
