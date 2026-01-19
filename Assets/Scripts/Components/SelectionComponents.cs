using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Tag component for selected units
/// </summary>
public struct Selected : IComponentData { }

/// <summary>
/// Tag component for units that can be selected (player units only)
/// </summary>
public struct Selectable : IComponentData { }

/// <summary>
/// Component for units that have a manual move command
/// Takes priority over combat target chasing
/// </summary>
public struct MoveCommand : IComponentData
{
    public float2 Target;
    public bool HasCommand;
}
