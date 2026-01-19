using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for flow field configuration
/// Add to a GameObject in your subscene
/// </summary>
public class FlowFieldConfigAuthoring : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 100;
    public int gridHeight = 100;
    public float cellSize = 1f;
    public Vector2 worldOffset = new Vector2(50f, 50f);
    
    class Baker : Baker<FlowFieldConfigAuthoring>
    {
        public override void Bake(FlowFieldConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new FlowFieldConfig
            {
                GridWidth = authoring.gridWidth,
                GridHeight = authoring.gridHeight,
                CellSize = authoring.cellSize,
                WorldOffset = new float2(authoring.worldOffset.x, authoring.worldOffset.y)
            });
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw grid bounds
        Gizmos.color = new Color(0, 0, 1, 0.3f);
        
        float worldWidth = cellSize * gridWidth;
        float worldHeight = cellSize * gridHeight;
        
        Vector3 center = new Vector3(
            worldWidth / 2f - worldOffset.x,
            worldHeight / 2f - worldOffset.y,
            0
        );
        
        Vector3 size = new Vector3(worldWidth, worldHeight, 0);
        
        Gizmos.DrawWireCube(center, size);
    }
}
