using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Faction identifier - determines who is friend or foe
/// </summary>
public struct Faction : IComponentData
{
    public FactionType Value;
}

public enum FactionType : byte
{
    Player = 0,
    Enemy = 1,
    Neutral = 2
}

/// <summary>
/// Health component for destructible entities
/// </summary>
public struct Health : IComponentData
{
    public float Current;
    public float Max;
    
    public bool IsDead => Current <= 0;
    public float Percent => Max > 0 ? Current / Max : 0;
}

/// <summary>
/// Combat stats for entities that can attack
/// </summary>
public struct Combat : IComponentData
{
    public float AttackDamage;
    public float AttackRange;
    public float AttackCooldown;
    public float CurrentCooldown;
    public float AttackWindup;        // Time to wait before first attack after stopping
    public float CurrentWindup;       // Current windup timer (counts down to 0)
    
    public bool CanAttack => CurrentCooldown <= 0 && CurrentWindup <= 0;
}

/// <summary>
/// Current target entity for combat
/// Check Target != Entity.Null instead of using a separate HasTarget flag
/// </summary>
public struct CombatTarget : IComponentData
{
    public Entity Target;
}

/// <summary>
/// Tag for entities that are dead and should be destroyed
/// </summary>
public struct Dead : IComponentData { }

/// <summary>
/// Tracks the direction of the last hit received (for death effects)
/// </summary>
public struct LastHitDirection : IComponentData
{
    public float2 Direction;  // Normalized direction from attacker to this unit
}

/// <summary>
/// Tag for player-controlled units
/// </summary>
public struct PlayerUnit : IComponentData { }

/// <summary>
/// Tag for enemy units (zombies)
/// </summary>
public struct EnemyUnit : IComponentData { }

/// <summary>
/// Zombie AI state - determines behavior and search radius
/// </summary>
public struct ZombieState : IComponentData
{
    public ZombieAIState State;
    public float AggroRadius;      // Detection radius
    public float ChaseRadius;      // Max chase distance
    public float AlertTimer;       // How long to stay alert after losing target
}

public enum ZombieAIState : byte
{
    Dormant = 0,   // Standing still, tiny detection radius
    Alert = 1,     // Heard something, larger detection radius
    Chasing = 2    // Actively pursuing a target
}

/// <summary>
/// Radius of the entity for targeting purposes.
/// Added to attack range checks so attackers can hit large entities from their edge.
/// For example, a 4x4 crystal at (0,0) has edges at ~2 units from center,
/// so TargetRadius=2.5 allows melee attackers to hit from the edge.
/// </summary>
public struct TargetRadius : IComponentData
{
    public float Value;
}
