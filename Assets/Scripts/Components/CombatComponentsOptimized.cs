using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// HOT: Combat state data that changes frequently (every frame)
/// Separated from config data for better cache locality
/// </summary>
public struct CombatState : IComponentData
{
    public float CurrentCooldown;
    public float CurrentWindup;

    public bool CanAttack => CurrentCooldown <= 0 && CurrentWindup <= 0;
}

/// <summary>
/// COLD: Combat configuration data that rarely changes
/// Separated from state for better cache locality
/// </summary>
public struct CombatConfig : IComponentData
{
    public float AttackDamage;
    public float AttackRange;
    public float AttackCooldown;
    public float AttackWindup;
}

/// <summary>
/// HOT: Current health value that changes when damaged
/// </summary>
public struct HealthCurrent : IComponentData
{
    public float Value;

    public bool IsDead => Value <= 0;
}

/// <summary>
/// COLD: Maximum health configuration (rarely accessed)
/// </summary>
public struct HealthMax : IComponentData
{
    public float Value;

    public float GetPercent(float current) => Value > 0 ? current / Value : 0;
}

// Note: Legacy Combat and Health components are defined in CombatComponents.cs
// This file only contains the new optimized hot/cold split components
