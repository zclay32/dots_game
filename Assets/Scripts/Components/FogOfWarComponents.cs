using Unity.Entities;

/// <summary>
/// Visibility state for fog of war cells.
/// Uses byte for efficient storage in NativeArray.
/// </summary>
public enum VisibilityState : byte
{
    Hidden = 0,    // Never seen - fully obscured
    Explored = 1,  // Previously seen - darkened/grayed
    Visible = 2    // Currently visible - full brightness
}

/// <summary>
/// Component that marks an entity as a source of vision.
/// Add to soldiers or other units that should reveal the fog.
/// </summary>
public struct VisionSource : IComponentData
{
    public float VisionRadius;  // Vision range in world units
}
