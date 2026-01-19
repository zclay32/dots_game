using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// OPTIMIZED: Combat system using hot/cold component separation
/// Hot components (CombatState, HealthCurrent) are accessed together for better cache locality
/// Cold components (CombatConfig, HealthMax) are only accessed when needed
///
/// This system processes soldier and zombie attacks separately to avoid faction branching
/// Units can only attack when standing still (velocity near zero)
/// Soldiers create noise events and must face their target (within 18 degrees)
/// Zombies require windup time and must face their target (within 45 degrees)
/// </summary>
[UpdateAfter(typeof(UnitMovementSystem))]
public partial struct CombatSystemOptimized : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // Disable this system by default - enable it in Unity Inspector to test
        state.Enabled = false;
    }

    // Note: OnUpdate cannot be Burst compiled because it calls managed code (NoiseEventManager, MuzzleFlashManager)
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;

        // Update combat timers (parallel) - only touches hot CombatState data
        new UpdateCombatTimersOptimizedJob { DeltaTime = deltaTime }.ScheduleParallel();

        // Complete jobs before applying damage
        state.Dependency.Complete();

        // Ensure noise managers are ready
        if (!NoiseEventManager.IsCreated)
            NoiseEventManager.Initialize();
        if (!NoiseEventManagerEnhanced.IsCreated)
            NoiseEventManagerEnhanced.Initialize();

        // Process attacks
        ProcessSoldierAttacksOptimized(ref state);
        ProcessZombieAttacksOptimized(ref state);
    }

    void ProcessSoldierAttacksOptimized(ref SystemState state)
    {
        // Lookups - combat config is read-only (cold data accessed infrequently)
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var healthLookup = SystemAPI.GetComponentLookup<HealthCurrent>(false);
        var zombieStateLookup = SystemAPI.GetComponentLookup<ZombieState>(false);
        var combatTargetLookup = SystemAPI.GetComponentLookup<CombatTarget>(false);
        var lastHitLookup = SystemAPI.GetComponentLookup<LastHitDirection>(false);

        // Query only player units - accesses hot components together
        foreach (var (combatState, combatConfig, target, transform, velocity, noiseConfig, entity) in
            SystemAPI.Query<RefRW<CombatState>, RefRO<CombatConfig>, RefRO<CombatTarget>,
                RefRO<LocalTransform>, RefRO<Velocity>, RefRO<GunshotNoiseConfig>>()
                .WithAll<PlayerUnit>()
                .WithEntityAccess())
        {
            if (target.ValueRO.Target == Entity.Null || !combatState.ValueRO.CanAttack)
                continue;

            // Only attack when standing still
            if (math.lengthsq(velocity.ValueRO.Value) > 0.01f)
                continue;

            // Get target transform and health (hot data)
            if (!transformLookup.TryGetComponent(target.ValueRO.Target, out var targetTransform))
                continue;
            if (!healthLookup.TryGetComponent(target.ValueRO.Target, out var targetHealth))
                continue;

            // Check range using squared distance
            float2 myPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);
            float2 targetPos = new float2(targetTransform.Position.x, targetTransform.Position.y);
            float distanceSq = math.distancesq(myPos, targetPos);
            float attackRangeSq = combatConfig.ValueRO.AttackRange * combatConfig.ValueRO.AttackRange;

            if (distanceSq <= attackRangeSq)
            {
                // Check if facing target (dot product > 0.95 = ~18 degrees)
                float2 toTarget = math.normalizesafe(targetPos - myPos);
                quaternion rot = transform.ValueRO.Rotation;
                float3 forward3 = math.mul(rot, new float3(0, 1, 0));
                float2 facing = new float2(forward3.x, forward3.y);

                if (math.dot(facing, toTarget) < 0.95f)
                    continue;

                if (targetHealth.IsDead)
                    continue;

                // Apply damage using hot component
                targetHealth.Value -= combatConfig.ValueRO.AttackDamage;
                healthLookup[target.ValueRO.Target] = targetHealth;

                // Record hit direction for death effects
                if (lastHitLookup.HasComponent(target.ValueRO.Target))
                {
                    lastHitLookup[target.ValueRO.Target] = new LastHitDirection { Direction = toTarget };
                }

                // Create enhanced noise event with exponential falloff
                // Uses per-soldier noise configuration from GunshotNoiseConfig component
                float maxNoiseRadius = combatConfig.ValueRO.AttackRange * noiseConfig.ValueRO.RangeMultiplier;
                NoiseEventManagerEnhanced.CreateNoise(
                    myPos,
                    maxNoiseRadius,
                    intensity: noiseConfig.ValueRO.Intensity,
                    falloffExponent: noiseConfig.ValueRO.FalloffExponent
                );

                // Also create legacy noise for backward compatibility (remove when migrated)
                NoiseEventManager.CreateNoise(myPos, maxNoiseRadius);

                float2 direction = math.normalizesafe(targetPos - myPos);
                MuzzleFlashManager.CreateFlash(myPos, direction);

                // Alert zombie target if applicable
                if (zombieStateLookup.TryGetComponent(target.ValueRO.Target, out var zombieState))
                {
                    zombieState.State = ZombieAIState.Chasing;
                    zombieState.AlertTimer = 5f;
                    zombieStateLookup[target.ValueRO.Target] = zombieState;

                    if (combatTargetLookup.TryGetComponent(target.ValueRO.Target, out var zombieCombatTarget))
                    {
                        zombieCombatTarget.Target = entity;
                        combatTargetLookup[target.ValueRO.Target] = zombieCombatTarget;
                    }
                }

                // Reset cooldown using config value
                combatState.ValueRW.CurrentCooldown = combatConfig.ValueRO.AttackCooldown;
            }
        }
    }

    void ProcessZombieAttacksOptimized(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var healthLookup = SystemAPI.GetComponentLookup<HealthCurrent>(false);
        var lastHitLookup = SystemAPI.GetComponentLookup<LastHitDirection>(false);

        // Query only zombies
        foreach (var (combatState, combatConfig, target, transform, velocity) in
            SystemAPI.Query<RefRW<CombatState>, RefRO<CombatConfig>, RefRO<CombatTarget>,
                RefRO<LocalTransform>, RefRO<Velocity>>()
                .WithAll<ZombieState>()
                .WithNone<PlayerUnit>())
        {
            if (target.ValueRO.Target == Entity.Null || !combatState.ValueRO.CanAttack)
                continue;

            // Only attack when standing still
            float speed = math.length(velocity.ValueRO.Value);
            if (speed > 0.1f)
            {
                combatState.ValueRW.CurrentWindup = combatConfig.ValueRO.AttackWindup;
                continue;
            }

            if (!transformLookup.TryGetComponent(target.ValueRO.Target, out var targetTransform))
                continue;
            if (!healthLookup.TryGetComponent(target.ValueRO.Target, out var targetHealth))
                continue;

            float2 myPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);
            float2 targetPos = new float2(targetTransform.Position.x, targetTransform.Position.y);
            float distance = math.distance(myPos, targetPos);

            if (distance <= combatConfig.ValueRO.AttackRange)
            {
                // Check if facing target (dot product > 0.7 = ~45 degrees)
                float2 toTarget = math.normalizesafe(targetPos - myPos);
                quaternion rot = transform.ValueRO.Rotation;
                float3 forward3 = math.mul(rot, new float3(0, 1, 0));
                float2 facing = new float2(forward3.x, forward3.y);

                if (math.dot(facing, toTarget) < 0.7f)
                {
                    combatState.ValueRW.CurrentWindup = combatConfig.ValueRO.AttackWindup;
                    continue;
                }

                if (targetHealth.IsDead)
                    continue;

                // Apply damage
                targetHealth.Value -= combatConfig.ValueRO.AttackDamage;
                healthLookup[target.ValueRO.Target] = targetHealth;

                // Record hit direction for death effects
                if (lastHitLookup.HasComponent(target.ValueRO.Target))
                {
                    lastHitLookup[target.ValueRO.Target] = new LastHitDirection { Direction = toTarget };
                }

                combatState.ValueRW.CurrentCooldown = combatConfig.ValueRO.AttackCooldown;
            }
            else
            {
                combatState.ValueRW.CurrentWindup = combatConfig.ValueRO.AttackWindup;
            }
        }
    }
}

/// <summary>
/// OPTIMIZED: Timer update job that only touches hot CombatState data
/// This has perfect cache locality since all hot data is together
/// </summary>
[BurstCompile]
public partial struct UpdateCombatTimersOptimizedJob : IJobEntity
{
    public float DeltaTime;

    void Execute(ref CombatState combatState)
    {
        if (combatState.CurrentCooldown > 0)
        {
            combatState.CurrentCooldown -= DeltaTime;
        }

        if (combatState.CurrentWindup > 0)
        {
            combatState.CurrentWindup -= DeltaTime;
        }
    }
}
