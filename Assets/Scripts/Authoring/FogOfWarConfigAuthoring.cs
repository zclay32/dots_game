using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for fog of war configuration.
/// Bakes a FogOfWarConfig singleton that defines the visibility grid.
/// Grid dimensions are computed from the map radius to cover the entire play area.
/// </summary>
public class FogOfWarConfigAuthoring : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Size of each tile in world units")]
    public float tileSize = 2f;

    [Header("Vision Settings")]
    [Tooltip("Default vision radius for units in world units")]
    public float defaultVisionRadius = 10f;

    class Baker : Baker<FogOfWarConfigAuthoring>
    {
        public override void Bake(FogOfWarConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Grid dimensions will be set at runtime based on GameConfig.MapRadius
            // For now, use reasonable defaults that will be overridden
            // Map radius of 80 with tile size 2 = 80 tiles per side (160 world units coverage)
            int defaultGridSize = 80;
            float defaultMapRadius = 80f;

            AddComponent(entity, new FogOfWarConfig
            {
                GridWidth = defaultGridSize,
                GridHeight = defaultGridSize,
                TileSize = authoring.tileSize,
                GridOrigin = new float2(-defaultMapRadius, -defaultMapRadius),
                DefaultVisionRadius = authoring.defaultVisionRadius
            });
        }
    }
}
