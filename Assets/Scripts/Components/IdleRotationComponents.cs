using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// HOT: Idle rotation state that changes frequently
/// Units slowly rotate when idle to appear more alive
/// </summary>
public struct IdleRotationState : IComponentData
{
    public float CurrentRotationSpeed;  // Current rotation velocity (rad/s)
    public float TargetAngle;           // Target angle to rotate toward
    public float TimeUntilNextChange;   // Countdown until picking new target angle
}

/// <summary>
/// COLD: Idle rotation configuration (rarely changes)
/// </summary>
public struct IdleRotationConfig : IComponentData
{
    public float MinRotationSpeed;      // Minimum rotation speed (rad/s)
    public float MaxRotationSpeed;      // Maximum rotation speed (rad/s)
    public float MinChangeInterval;     // Min time before changing direction (seconds)
    public float MaxChangeInterval;     // Max time before changing direction (seconds)
    public float MaxAngleChange;        // Maximum angle change per interval (radians)
}
