using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that updates combat timers (cooldowns, windups) in parallel.
/// Runs BEFORE CombatSystem so timers are ready when combat is processed.
/// </summary>
[UpdateAfter(typeof(UnitMovementSystem))]
[UpdateBefore(typeof(CombatSystem))]
public partial struct CombatTimerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        state.Dependency = new UpdateCombatTimersJob { DeltaTime = deltaTime }.ScheduleParallel(state.Dependency);
    }
}

/// <summary>
/// System that handles combat - units attack their targets.
/// Uses DEFERRED DAMAGE pattern for full parallelization:
/// - Soldier and zombie attacks run as parallel jobs
/// - Damage events are queued to DamageEventQueue
/// - DamageApplicationSystem processes the queue after all combat jobs complete
///
/// NO Complete() CALLS - all jobs chain via state.Dependency
/// </summary>
[UpdateAfter(typeof(UnitMovementSystem))]
[UpdateAfter(typeof(TargetFindingSystem))]
[UpdateAfter(typeof(CombatTimerSystem))]
[UpdateBefore(typeof(DamageApplicationSystem))]
public partial struct CombatSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialHashConfig>();

        // Initialize event managers
        DamageEventQueue.Initialize();
        CombatDebugEventQueue.Initialize();
    }

    public void OnDestroy(ref SystemState state)
    {
        // Clean up noise managers (safe to call even if already disposed)
        NoiseEventManager.Dispose();
        NoiseEventManagerEnhanced.Dispose();
        CombatDebugEventQueue.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Ensure noise managers are ready (both legacy and enhanced)
        if (!NoiseEventManager.IsCreated)
            NoiseEventManager.Initialize();
        if (!NoiseEventManagerEnhanced.IsCreated)
            NoiseEventManagerEnhanced.Initialize();
        if (!MuzzleFlashManager.IsCreated)
            MuzzleFlashManager.Initialize();

        // Get component lookups (read-only for parallel access)
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var healthLookup = SystemAPI.GetComponentLookup<Health>(true);

        // Schedule soldier attack job - runs in parallel, queues damage events
        var soldierJob = new SoldierAttackJob
        {
            TransformLookup = transformLookup,
            HealthLookup = healthLookup,
            DamageQueue = DamageEventQueue.GetParallelWriter(),
            DebugQueue = CombatDebugEventQueue.GetParallelWriter(),
            EnableDebug = false // Set to true to enable debug logging
        };
        state.Dependency = soldierJob.ScheduleParallel(state.Dependency);

        // Schedule legacy zombie attack job - runs in parallel, queues damage events
        // Note: New zombies use ZombieCombatExecutionSystem instead
        var zombieJob = new LegacyZombieAttackJob
        {
            TransformLookup = transformLookup,
            HealthLookup = healthLookup,
            DamageQueue = DamageEventQueue.GetParallelWriter()
        };
        state.Dependency = zombieJob.ScheduleParallel(state.Dependency);

        // Note: Noise and muzzle flash events are created by CombatEffectsSystem
        // after the damage is applied, ensuring effects only occur for successful attacks.
    }
}

/// <summary>
/// Parallel job for soldier attacks.
/// Queues damage events instead of applying directly - eliminates race conditions.
/// Uses CombatState (hot) and CombatConfig (cold) instead of legacy Combat component.
/// </summary>
[BurstCompile]
public partial struct SoldierAttackJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<Health> HealthLookup;
    public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;
    public NativeQueue<SoldierCombatDebugEvent>.ParallelWriter DebugQueue;
    public bool EnableDebug;

    void Execute(
        ref CombatState combatState,
        in CombatConfig combatConfig,
        ref CombatTarget target,
        in LocalTransform transform,
        in Velocity velocity,
        in PlayerUnit playerTag,
        Entity entity)
    {
        Entity targetEntity = target.Target;

        // Early exit checks (cheapest first)
        if (targetEntity == Entity.Null)
        {
            if (EnableDebug) DebugQueue.Enqueue(new SoldierCombatDebugEvent
            {
                Soldier = entity, Target = Entity.Null,
                Reason = SoldierAttackFailReason.NoTarget
            });
            return;
        }

        // Check cooldown using hot component
        bool canAttack = combatState.CurrentCooldown <= 0 && combatState.CurrentWindup <= 0;
        if (!canAttack)
        {
            if (EnableDebug) DebugQueue.Enqueue(new SoldierCombatDebugEvent
            {
                Soldier = entity, Target = targetEntity,
                Reason = SoldierAttackFailReason.OnCooldown,
                Value1 = combatState.CurrentCooldown, Value2 = combatState.CurrentWindup
            });
            return;
        }

        // Only attack when standing still
        float speedSq = math.lengthsq(velocity.Value);
        if (speedSq > 0.01f)
        {
            if (EnableDebug) DebugQueue.Enqueue(new SoldierCombatDebugEvent
            {
                Soldier = entity, Target = targetEntity,
                Reason = SoldierAttackFailReason.Moving,
                Value1 = speedSq
            });
            return;
        }

        // Validate target exists and get transform
        if (!TransformLookup.HasComponent(targetEntity))
        {
            if (EnableDebug) DebugQueue.Enqueue(new SoldierCombatDebugEvent
            {
                Soldier = entity, Target = targetEntity,
                Reason = SoldierAttackFailReason.TargetNoTransform
            });
            target.Target = Entity.Null;
            return;
        }
        var targetTransform = TransformLookup[targetEntity];

        // Validate target has health
        if (!HealthLookup.HasComponent(targetEntity))
        {
            if (EnableDebug) DebugQueue.Enqueue(new SoldierCombatDebugEvent
            {
                Soldier = entity, Target = targetEntity,
                Reason = SoldierAttackFailReason.TargetNoHealth
            });
            target.Target = Entity.Null;
            return;
        }
        var targetHealth = HealthLookup[targetEntity];

        // Clear target if already dead
        if (targetHealth.IsDead)
        {
            if (EnableDebug) DebugQueue.Enqueue(new SoldierCombatDebugEvent
            {
                Soldier = entity, Target = targetEntity,
                Reason = SoldierAttackFailReason.TargetDead
            });
            target.Target = Entity.Null;
            return;
        }

        // Calculate positions
        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        float2 targetPos = new float2(targetTransform.Position.x, targetTransform.Position.y);

        // Range check using squared distance
        float attackRangeSq = combatConfig.AttackRange * combatConfig.AttackRange;
        float distanceSq = math.distancesq(myPos, targetPos);

        if (distanceSq > attackRangeSq)
        {
            if (EnableDebug) DebugQueue.Enqueue(new SoldierCombatDebugEvent
            {
                Soldier = entity, Target = targetEntity,
                Reason = SoldierAttackFailReason.OutOfRange,
                Value1 = math.sqrt(distanceSq), Value2 = combatConfig.AttackRange
            });
            return;
        }

        // Facing check
        float2 toTarget = math.normalizesafe(targetPos - myPos);
        float3 forward3 = math.mul(transform.Rotation, new float3(0, 1, 0));
        float2 facing = new float2(forward3.x, forward3.y);

        // Must face target (dot > 0.95 means within ~18 degrees)
        float dot = math.dot(facing, toTarget);
        if (dot < 0.95f)
        {
            if (EnableDebug) DebugQueue.Enqueue(new SoldierCombatDebugEvent
            {
                Soldier = entity, Target = targetEntity,
                Reason = SoldierAttackFailReason.NotFacing,
                Value1 = dot, Value2 = 0.95f
            });
            return;
        }

        // --- Attack succeeds - queue damage event ---
        DamageQueue.Enqueue(new DamageEvent
        {
            Target = targetEntity,
            Damage = combatConfig.AttackDamage,
            HitDirection = toTarget,
            Attacker = entity,
            AttackerIsPlayer = true
        });

        if (EnableDebug) DebugQueue.Enqueue(new SoldierCombatDebugEvent
        {
            Soldier = entity, Target = targetEntity,
            Reason = SoldierAttackFailReason.AttackSuccess,
            Value1 = combatConfig.AttackDamage
        });

        // Set cooldown on CombatState (hot component - source of truth)
        combatState.CurrentCooldown = combatConfig.AttackCooldown;

        // Note: Noise and muzzle flash are created by CombatEffectsSystem after damage is confirmed
    }
}

/// <summary>
/// Parallel job for legacy zombie attacks (ZombieState without ZombieCombatState).
/// New zombies use ZombieCombatExecutionSystem instead.
/// </summary>
[BurstCompile]
public partial struct LegacyZombieAttackJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<Health> HealthLookup;
    public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;

    void Execute(
        ref Combat combat,
        ref CombatTarget target,
        in LocalTransform transform,
        in Velocity velocity,
        in ZombieState zombieState,
        Entity entity)
    {
        Entity targetEntity = target.Target;

        // Early exit checks
        if (targetEntity == Entity.Null)
            return;

        if (!combat.CanAttack)
            return;

        // Only attack when standing still
        float speedSq = math.lengthsq(velocity.Value);
        if (speedSq > 0.01f)
        {
            // Moving - reset windup timer
            combat.CurrentWindup = combat.AttackWindup;
            return;
        }

        // Validate target exists
        if (!TransformLookup.HasComponent(targetEntity))
        {
            target.Target = Entity.Null;
            return;
        }
        var targetTransform = TransformLookup[targetEntity];

        // Validate target has health
        if (!HealthLookup.HasComponent(targetEntity))
        {
            target.Target = Entity.Null;
            return;
        }
        var targetHealth = HealthLookup[targetEntity];

        // Clear target if dead
        if (targetHealth.IsDead)
        {
            target.Target = Entity.Null;
            return;
        }

        // Calculate positions
        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        float2 targetPos = new float2(targetTransform.Position.x, targetTransform.Position.y);
        float distanceSq = math.distancesq(myPos, targetPos);

        // Use slightly larger range for windup
        float windupRange = combat.AttackRange * 1.15f;
        float windupRangeSq = windupRange * windupRange;

        if (distanceSq > windupRangeSq)
        {
            // Out of windup range - reset windup
            combat.CurrentWindup = combat.AttackWindup;
            return;
        }

        // Facing check
        float2 toTarget = math.normalizesafe(targetPos - myPos);
        float3 forward3 = math.mul(transform.Rotation, new float3(0, 1, 0));
        float2 facing = new float2(forward3.x, forward3.y);

        // Must face target (dot > 0.7 means within ~45 degrees)
        if (math.dot(facing, toTarget) < 0.7f)
        {
            // Not facing target - reset windup
            combat.CurrentWindup = combat.AttackWindup;
            return;
        }

        // Only deal damage if within actual attack range
        float attackRangeSq = combat.AttackRange * combat.AttackRange;
        if (distanceSq <= attackRangeSq)
        {
            // Queue damage event
            DamageQueue.Enqueue(new DamageEvent
            {
                Target = targetEntity,
                Damage = combat.AttackDamage,
                HitDirection = toTarget,
                Attacker = entity,
                AttackerIsPlayer = false
            });

            combat.CurrentCooldown = combat.AttackCooldown;
        }
        // else: in windup range but not attack range - windup continues but no damage
    }
}

[BurstCompile]
public partial struct UpdateCombatTimersJob : IJobEntity
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
