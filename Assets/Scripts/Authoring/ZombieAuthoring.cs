using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Authoring component for zombie prefab
/// </summary>
public class ZombieAuthoring : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2f;
    
    [Header("Health")]
    public float maxHealth = 50f;
    
    [Header("Combat")]
    public float attackDamage = 10f;
    public float attackRange = 1f;
    public float attackCooldown = 1f;
    public float attackWindup = 0.5f;  // Delay before first attack after stopping
    public float attackConeAngle = 45f; // Degrees - width of damage cone

    [Header("AI State")]
    public float alertRadius = 3f;    // Detection radius when dormant/idle
    public float aggroRadius = 15f;   // Detection radius when alert/chasing
    public float wanderRadius = 2f;   // Max distance from spawn when wandering (small circle)
    public float wanderSpeedMultiplier = 0.25f; // Fraction of max speed when wandering

    [Header("Noise Sensitivity")]
    public float noiseSensitivityMultiplier = 1.0f;  // 1.0 = normal, 2.0 = extra sensitive, 0.5 = deaf
    public float minActivationProbability = 0.0f;    // Minimum chance to react
    public float maxActivationProbability = 1.0f;    // Maximum chance to react
    
    class Baker : Baker<ZombieAuthoring>
    {
        public override void Bake(ZombieAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Unit tag
            AddComponent(entity, new Unit());
            AddComponent(entity, new EnemyUnit());
            
            // Faction
            AddComponent(entity, new Faction { Value = FactionType.Enemy });
            
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
                AttackWindup = authoring.attackWindup,
                CurrentWindup = authoring.attackWindup  // Start with windup active
            });

            // Combat - optimized hot/cold split
            AddComponent(entity, new CombatState
            {
                CurrentCooldown = 0,
                CurrentWindup = authoring.attackWindup  // Start with windup active
            });
            AddComponent(entity, new CombatConfig
            {
                AttackDamage = authoring.attackDamage,
                AttackRange = authoring.attackRange,
                AttackCooldown = authoring.attackCooldown,
                AttackWindup = authoring.attackWindup
            });
            
            AddComponent(entity, new CombatTarget { Target = Entity.Null });
            
            // AI State - legacy component (kept for compatibility with old systems)
            AddComponent(entity, new ZombieState
            {
                State = ZombieAIState.Dormant,
                AlertRadius = authoring.alertRadius,
                ChaseRadius = authoring.aggroRadius,
                AlertTimer = 0
            });

            // Get spawn position from transform
            var spawnPos = GetComponent<Transform>().position;
            float2 spawnPosition = new float2(spawnPos.x, spawnPos.y);

            // New state machine components
            AddComponent(entity, new ZombieCombatState
            {
                State = ZombieCombatAIState.Idle,
                StateTimer = 2f, // Start with short idle before wandering
                CurrentTarget = Entity.Null,
                HasTarget = false,
                WanderTarget = spawnPosition,
                HasEngagedTarget = false
            });

            AddComponent(entity, new ZombieCombatConfig
            {
                AttackDamage = authoring.attackDamage,
                AttackRange = authoring.attackRange,
                AttackCooldown = authoring.attackCooldown,
                AttackWindup = authoring.attackWindup,
                AttackConeAngle = authoring.attackConeAngle,
                AggroRadius = authoring.aggroRadius,
                AlertRadius = authoring.alertRadius,
                WanderRadius = authoring.wanderRadius,
                WanderSpeedMultiplier = authoring.wanderSpeedMultiplier,
                SpawnPosition = spawnPosition
            });

            // Idle rotation - zombies slowly sway when dormant
            AddComponent(entity, new IdleRotationState
            {
                CurrentRotationSpeed = 0.15f,
                TargetAngle = 0f,
                TimeUntilNextChange = 3f
            });
            AddComponent(entity, new IdleRotationConfig
            {
                MinRotationSpeed = 0.1f,  // Very slow, zombie-like sway
                MaxRotationSpeed = 0.3f,
                MinChangeInterval = 2f,  // Slower changes than soldiers
                MaxChangeInterval = 6f,
                MaxAngleChange = math.PI * 0.25f  // Small turns (45 degrees)
            });

            // Noise sensitivity - how this zombie reacts to gunfire sounds
            AddComponent(entity, new NoiseSensitivity
            {
                SensitivityMultiplier = authoring.noiseSensitivityMultiplier,
                MinActivationProbability = authoring.minActivationProbability,
                MaxActivationProbability = authoring.maxActivationProbability
            });

            // Track last hit direction for death effects
            AddComponent(entity, new LastHitDirection { Direction = float2.zero });

            // Cached target angle for facing (used by two-pass rotation system)
            AddComponent(entity, new ZombieTargetAngle { Value = 0f, HasValidAngle = false });

            // Separation force (used by two-pass separation system)
            AddComponent(entity, new SeparationForce { Force = float2.zero });
        }
    }
}
