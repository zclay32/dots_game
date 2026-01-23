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

/// <summary>
/// Tag component added to entities that should be hidden by fog of war.
/// Rendering systems should skip entities with this component.
/// </summary>
public struct FogHidden : IComponentData { }

/// <summary>
/// Stores the original scale of an entity before it was hidden by fog of war.
/// Used to restore scale when the entity becomes visible again.
/// </summary>
public struct FogHiddenScale : IComponentData
{
    public float Value;
}
