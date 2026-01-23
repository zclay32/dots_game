using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for soldier (player unit) prefab
/// </summary>
public class SoldierAuthoring : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    
    [Header("Health")]
    public float maxHealth = 100f;
    
    [Header("Combat")]
    public float attackDamage = 25f;
    public float attackRange = 5f;
    public float attackCooldown = 0.5f;

    [Header("Gunshot Noise")]
    public float noiseRangeMultiplier = 15f;  // Multiplier on attack range (15x = 75 units if attackRange is 5)
    public float noiseIntensity = 2.0f;       // How loud (1.0 = normal, 2.0 = very loud)
    public float noiseFalloffExponent = 1.2f; // How fast sound fades (1.0 = linear, 2.0 = quadratic)

    [Header("Vision")]
    public float visionRadius = 10f;  // Vision range in world units
    
    class Baker : Baker<SoldierAuthoring>
    {
        public override void Bake(SoldierAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Unit tag
            AddComponent(entity, new Unit());
            AddComponent(entity, new PlayerUnit());
            
            // Faction
            AddComponent(entity, new Faction { Value = FactionType.Player });
            
            // Movement
            AddComponent(entity, new MoveSpeed { Value = authoring.moveSpeed });
            AddComponent(entity, new Velocity { Value = float2.zero });
            AddComponent(entity, new TargetPosition { Value = float2.zero, HasTarget = false });
            
            // Spatial
            AddComponent(entity, new SpatialCell { Cell = int2.zero });
            
            // Health - legacy component for compatibility
            AddComponent(entity, new Health
            {
                Current = authoring.maxHealth,
                Max = authoring.maxHealth
            });

            // Health - optimized hot/cold split
            AddComponent(entity, new HealthCurrent { Value = authoring.maxHealth });
            AddComponent(entity, new HealthMax { Value = authoring.maxHealth });

            // Combat - legacy component for compatibility
            AddComponent(entity, new Combat
            {
                AttackDamage = authoring.attackDamage,
                AttackRange = authoring.attackRange,
                AttackCooldown = authoring.attackCooldown,
                CurrentCooldown = 0,
                AttackWindup = 0f,
                CurrentWindup = 0f
            });

            // Combat - optimized hot/cold split
            AddComponent(entity, new CombatState
            {
                CurrentCooldown = 0,
                CurrentWindup = 0f  // Soldiers fire instantly
            });
            AddComponent(entity, new CombatConfig
            {
                AttackDamage = authoring.attackDamage,
                AttackRange = authoring.attackRange,
                AttackCooldown = authoring.attackCooldown,
                AttackWindup = 0f
            });
            
            AddComponent(entity, new CombatTarget { Target = Entity.Null });

            // Soldier-specific: Target angle cache for parallel facing system
            AddComponent(entity, new SoldierTargetAngle { Value = 0f, HasValidAngle = false });

            // Selection - soldiers can be selected and given move commands
            AddComponent(entity, new Selectable());
            AddComponent(entity, new MoveCommand { Target = float2.zero, HasCommand = false });

            // Idle rotation - soldiers quickly scan when idle (alert behavior)
            AddComponent(entity, new IdleRotationState
            {
                CurrentRotationSpeed = 1.5f,
                TargetAngle = 0f,
                TimeUntilNextChange = 1.5f
            });
            AddComponent(entity, new IdleRotationConfig
            {
                MinRotationSpeed = 1.0f,  // Faster, alert scanning
                MaxRotationSpeed = 2.5f,  // Quick snaps to new positions
                MinChangeInterval = 1.0f,  // More frequent direction changes
                MaxChangeInterval = 3.0f,
                MaxAngleChange = math.PI * 0.75f  // Turn up to 135 degrees
            });

            // Gunshot noise configuration - how loud this soldier's weapon is
            AddComponent(entity, new GunshotNoiseConfig
            {
                RangeMultiplier = authoring.noiseRangeMultiplier,
                Intensity = authoring.noiseIntensity,
                FalloffExponent = authoring.noiseFalloffExponent
            });

            // Track last hit direction for death effects
            AddComponent(entity, new LastHitDirection { Direction = float2.zero });

            // Separation force (used by two-pass separation system)
            AddComponent(entity, new SeparationForce { Force = float2.zero });

            // Vision for fog of war
            AddComponent(entity, new VisionSource { VisionRadius = authoring.visionRadius });
        }
    }
}
