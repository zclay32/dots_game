using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Tag component to identify units
/// </summary>
public struct Unit : IComponentData
{
    // Empty tag - just marks this entity as a unit
}

/// <summary>
/// Movement speed for an entity
/// </summary>
public struct MoveSpeed : IComponentData
{
    public float Value;
}

/// <summary>
/// Current velocity/direction
/// </summary>
public struct Velocity : IComponentData
{
    public float2 Value;
}

/// <summary>
/// Target position to move towards
/// </summary>
public struct TargetPosition : IComponentData
{
    public float2 Value;
    public bool HasTarget;
}

/// <summary>
/// Cached target angle for soldier facing (avoids transform lookups in rotation pass)
/// </summary>
public struct SoldierTargetAngle : IComponentData
{
    public float Value;
    public bool HasValidAngle;
}

/// <summary>
/// Cached target angle for zombie facing (avoids transform lookups in rotation pass)
/// </summary>
public struct ZombieTargetAngle : IComponentData
{
    public float Value;
    public bool HasValidAngle;
}

/// <summary>
/// Separation force computed by UnitSeparationSystem's compute pass.
/// Applied in a second pass to avoid aliasing issues with parallel jobs.
/// </summary>
public struct SeparationForce : IComponentData
{
    public float2 Force;
}
