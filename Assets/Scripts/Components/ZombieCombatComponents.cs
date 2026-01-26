using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Zombie AI state machine states
/// See ZOMBIE_COMBAT_PRD.md for full documentation
/// </summary>
public enum ZombieCombatAIState : byte
{
    Idle = 0,       // No target, standing still
    Wandering = 1,  // No target, slowly moving within wander range
    Chasing = 2,    // Has target, moving at full speed
    WindingUp = 3,  // Has target, stopped, preparing to attack
    Attacking = 4,  // Has target, dealing damage this frame
    Cooldown = 5    // Has target, recovering from attack
}

/// <summary>
/// HOT component - Updated every frame
/// Tracks current state machine state and timers
/// </summary>
public struct ZombieCombatState : IComponentData
{
    public ZombieCombatAIState State;
    public float StateTimer;           // Countdown timer for current state
    public Entity CurrentTarget;       // Current combat target
    public bool HasTarget;             // Whether target is valid
    public float2 WanderTarget;        // Current wander destination
    public bool HasEngagedTarget;      // True after first attack on current target (skips windup)
    public float2 CachedTargetPos;     // Cached position of CurrentTarget (updated by state machine)
    public bool NeedsTargetSearch;     // Set when zombie loses target - triggers immediate search
}

/// <summary>
/// COLD component - Read only configuration
/// Set once during baking, rarely changes
/// </summary>
public struct ZombieCombatConfig : IComponentData
{
    public float AttackDamage;
    public float AttackRange;
    public float AttackCooldown;
    public float AttackWindup;
    public float AttackConeAngle;      // Degrees - width of damage cone
    public float AggroRadius;          // Detection radius - how close to notice a target
    public float ChaseRadius;          // Max chase distance - how far to chase before giving up
    public float WanderRadius;         // Max distance from spawn when wandering
    public float WanderSpeedMultiplier; // Fraction of max speed when wandering
    public float2 SpawnPosition;       // Original spawn position for wander range
}
