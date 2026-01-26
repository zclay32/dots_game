using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component for the Crystal entity.
/// The Crystal is the central structure that must be protected.
/// </summary>
public class CrystalAuthoring : MonoBehaviour
{
    [Header("Structure")]
    [Tooltip("Size of the crystal footprint in tiles (e.g., 4 = 4x4)")]
    public int tileFootprint = 4;

    [Header("Vision")]
    [Tooltip("Vision radius for fog of war (larger than soldiers)")]
    public float visionRadius = 15f;

    class Baker : Baker<CrystalAuthoring>
    {
        public override void Bake(CrystalAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Crystal tag (singleton identifier)
            AddComponent(entity, new Crystal());

            // Obstacle for pathfinding and collision (tile-based)
            AddComponent(entity, new Obstacle
            {
                TileWidth = authoring.tileFootprint,
                TileHeight = authoring.tileFootprint
            });

            // Vision for fog of war (crystal has larger vision than soldiers)
            AddComponent(entity, new VisionSource { VisionRadius = authoring.visionRadius });
        }
    }
}
