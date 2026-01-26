using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for the Crystal entity.
/// The Crystal is the central structure that must be protected.
/// Zombies will attack it as a fallback target when no soldiers are in range.
/// </summary>
public class CrystalAuthoring : MonoBehaviour
{
    [Header("Structure")]
    [Tooltip("Size of the crystal footprint in tiles (e.g., 4 = 4x4)")]
    public int tileFootprint = 4;

    [Header("Health")]
    [Tooltip("Maximum health of the crystal")]
    public float maxHealth = 500f;

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

            // Unit tag - required for spatial hash indexing so zombies can target it
            AddComponent(entity, new Unit());

            // Faction - Player faction so zombies will target it
            AddComponent(entity, new Faction { Value = FactionType.Player });

            // Health - legacy component for compatibility with DeathSystem
            AddComponent(entity, new Health
            {
                Current = authoring.maxHealth,
                Max = authoring.maxHealth
            });

            // Health - optimized hot/cold split for DamageApplicationSystem
            AddComponent(entity, new HealthCurrent { Value = authoring.maxHealth });
            AddComponent(entity, new HealthMax { Value = authoring.maxHealth });

            // Spatial cell for spatial hash
            AddComponent(entity, new SpatialCell { Cell = int2.zero });

            // Track last hit direction for potential effects
            AddComponent(entity, new LastHitDirection { Direction = float2.zero });

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
