using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Visibility state for fog of war cells.
/// </summary>
public enum VisibilityState : byte
{
    Hidden = 0,    // Never seen - fully black
    Explored = 1,  // Previously seen - darkened
    Visible = 2    // Currently visible - full brightness
}

/// <summary>
/// Configuration for the fog of war system.
/// This is a singleton component baked from FogOfWarConfigAuthoring.
/// </summary>
public struct FogOfWarConfig : IComponentData
{
    public int GridWidth;           // Number of tiles horizontally
    public int GridHeight;          // Number of tiles vertically
    public float TileSize;          // Size of each tile in world units
    public float2 GridOrigin;       // World position of bottom-left corner
    public float DefaultVisionRadius; // Default vision radius for units
}

/// <summary>
/// Component that marks an entity as a source of vision.
/// Add to soldiers or other units that should reveal the fog.
/// </summary>
public struct VisionSource : IComponentData
{
    public float VisionRadius;      // Vision range in world units
}

/// <summary>
/// Tag component that marks an entity as hidden by fog of war.
/// Added when entity is in Hidden/Explored areas, removed when in Visible areas.
/// Used by renderers to skip drawing hidden entities.
/// </summary>
public struct FogCulled : IComponentData { }
