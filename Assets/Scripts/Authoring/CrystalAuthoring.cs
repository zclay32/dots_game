using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component for the Crystal entity.
/// The Crystal is the central structure that must be protected.
/// Zombies kills within range feed souls to the crystal.
/// </summary>
public class CrystalAuthoring : MonoBehaviour
{
    [Header("Crystal Power")]
    [Tooltip("Starting power level")]
    public float startingPower = 0f;

    [Header("Soul Harvesting")]
    [Tooltip("Range within which zombie kills feed souls to the crystal")]
    public float soulHarvestRange = 100f;

    [Tooltip("Base soul value per zombie kill")]
    public float soulValueBase = 1f;

    [Header("Threat")]
    [Tooltip("How far the crystal's presence attracts zombies")]
    public float threatRadius = 50f;

    [Header("Structure")]
    [Tooltip("Size of the crystal footprint in tiles (e.g., 4 = 4x4)")]
    public int tileFootprint = 4;

    class Baker : Baker<CrystalAuthoring>
    {
        public override void Bake(CrystalAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Crystal tag (singleton identifier)
            AddComponent(entity, new Crystal());

            // Power tracking
            AddComponent(entity, new CrystalPower
            {
                Current = authoring.startingPower,
                Lifetime = authoring.startingPower
            });

            // Configuration
            AddComponent(entity, new CrystalConfig
            {
                SoulHarvestRange = authoring.soulHarvestRange,
                SoulValueBase = authoring.soulValueBase,
                ThreatRadius = authoring.threatRadius,
                TileFootprint = authoring.tileFootprint
            });

            // Threat level (starts at 0)
            AddComponent(entity, new ThreatLevel
            {
                Level = 0,
                Multiplier = 1f
            });

            // Obstacle for pathfinding and collision (tile-based)
            AddComponent(entity, new Obstacle
            {
                TileWidth = authoring.tileFootprint,
                TileHeight = authoring.tileFootprint
            });
        }
    }
}
