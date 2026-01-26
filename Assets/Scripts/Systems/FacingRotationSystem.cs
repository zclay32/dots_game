using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that rotates units to face their target or movement direction
/// Soldiers rotate toward targets when stationary with a target
/// Zombies rotate based on movement (handled by flow field)
/// </summary>
[BurstCompile]
[UpdateAfter(typeof(UnitMovementSystem))]
[UpdateBefore(typeof(CombatSystem))]
public partial struct FacingRotationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        float deltaTime = SystemAPI.Time.DeltaTime;

        // Two-pass approach for soldiers to enable parallelization:
        // Pass 1: Calculate target angles (parallel - read-only lookups)
        state.Dependency = new CalculateSoldierTargetAnglesJob
        {
            TransformLookup = transformLookup
        }.ScheduleParallel(state.Dependency);

        // Pass 2: Apply rotation based on calculated angles (must wait for pass 1)
        state.Dependency = new ApplySoldierRotationJob
        {
            RotationSpeed = 10f,
            DeltaTime = deltaTime
        }.ScheduleParallel(state.Dependency);

        // Two-pass approach for zombies (same pattern to avoid aliasing):
        // Pass 1: Calculate target angles for zombies
        state.Dependency = new CalculateZombieTargetAnglesJob
        {
            TransformLookup = transformLookup,
            ZombieCombatStateLookup = SystemAPI.GetComponentLookup<ZombieCombatState>(true)
        }.ScheduleParallel(state.Dependency);

        // Pass 2: Apply rotation to zombies
        state.Dependency = new ApplyZombieRotationJob
        {
            RotationSpeed = 5f,
            DeltaTime = deltaTime
        }.ScheduleParallel(state.Dependency);

        // Moving units face their movement direction (parallel - no lookups)
        state.Dependency = new UpdateMovementFacingJob().ScheduleParallel(state.Dependency);
    }
}

/// <summary>
/// Pass 1: Calculate target angles for soldiers (parallel with read-only lookups)
/// </summary>
[BurstCompile]
public partial struct CalculateSoldierTargetAnglesJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

    void Execute(ref SoldierTargetAngle targetAngle, ref CombatTarget target, in LocalTransform transform,
                 in Velocity velocity, in PlayerUnit playerTag)
    {
        // Only calculate angle when standing still and have a target
        if (math.lengthsq(velocity.Value) > 0.01f)
        {
            targetAngle.HasValidAngle = false;
            return;
        }

        if (target.Target == Entity.Null)
        {
            targetAngle.HasValidAngle = false;
            return;
        }

        // Get target position (read-only lookup - safe in parallel)
        // Also clear target if it no longer exists
        if (!TransformLookup.TryGetComponent(target.Target, out var targetTransform))
        {
            targetAngle.HasValidAngle = false;
            target.Target = Entity.Null;
            return;
        }

        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        float2 targetPos = new float2(targetTransform.Position.x, targetTransform.Position.y);
        float2 toTarget = targetPos - myPos;

        // Calculate and store desired angle
        targetAngle.Value = math.atan2(toTarget.y, toTarget.x) - math.PI * 0.5f;
        targetAngle.HasValidAngle = true;
    }
}

/// <summary>
/// Pass 2: Apply rotation based on calculated angles (parallel - no lookups needed)
/// </summary>
[BurstCompile]
public partial struct ApplySoldierRotationJob : IJobEntity
{
    public float RotationSpeed;
    public float DeltaTime;

    void Execute(ref LocalTransform transform, in SoldierTargetAngle targetAngle, in PlayerUnit playerTag)
    {
        if (!targetAngle.HasValidAngle)
            return;

        // Get current angle
        float currentAngle = math.atan2(
            2f * (transform.Rotation.value.w * transform.Rotation.value.z),
            1f - 2f * (transform.Rotation.value.z * transform.Rotation.value.z)
        );

        // Smoothly rotate toward target
        float angleDiff = math.fmod(targetAngle.Value - currentAngle + math.PI * 3f, math.PI * 2f) - math.PI;
        float rotationStep = math.clamp(angleDiff, -RotationSpeed * DeltaTime, RotationSpeed * DeltaTime);
        float newAngle = currentAngle + rotationStep;

        transform.Rotation = quaternion.RotateZ(newAngle);
    }
}

/// <summary>
/// Moving units face their movement direction (zombies, moving soldiers)
/// </summary>
[BurstCompile]
public partial struct UpdateMovementFacingJob : IJobEntity
{
    void Execute(ref LocalTransform transform, in Velocity velocity)
    {
        // Only rotate if moving
        if (math.lengthsq(velocity.Value) < 0.01f)
            return;

        // Calculate angle from velocity
        // atan2 gives angle in radians, with 0 pointing right (+X)
        // We want 0 to point up (+Y) for a triangle, so subtract PI/2
        float angle = math.atan2(velocity.Value.y, velocity.Value.x) - math.PI * 0.5f;

        // Apply rotation around Z axis (2D rotation)
        transform.Rotation = quaternion.RotateZ(angle);
    }
}

/// <summary>
/// Pass 1: Calculate target angles for zombies (parallel with read-only lookups)
/// Supports both legacy CombatTarget and new ZombieCombatState target systems
/// </summary>
[BurstCompile]
public partial struct CalculateZombieTargetAnglesJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<ZombieCombatState> ZombieCombatStateLookup;

    void Execute(Entity entity, ref ZombieTargetAngle targetAngle, ref CombatTarget target, in LocalTransform transform,
                 in Velocity velocity, in ZombieState zombieState)
    {
        // Only calculate angle when standing still
        if (math.lengthsq(velocity.Value) > 0.01f)
        {
            targetAngle.HasValidAngle = false;
            return;
        }

        // Check new state machine target first, then legacy CombatTarget
        Entity targetEntity = Entity.Null;

        if (ZombieCombatStateLookup.HasComponent(entity))
        {
            var combatState = ZombieCombatStateLookup[entity];
            if (combatState.HasTarget)
            {
                targetEntity = combatState.CurrentTarget;
            }
        }

        // Fallback to legacy CombatTarget
        if (targetEntity == Entity.Null && target.Target != Entity.Null)
        {
            targetEntity = target.Target;
        }

        if (targetEntity == Entity.Null)
        {
            targetAngle.HasValidAngle = false;
            return;
        }

        // Get target position (read-only lookup - safe in parallel)
        if (!TransformLookup.TryGetComponent(targetEntity, out var targetTransform))
        {
            targetAngle.HasValidAngle = false;
            return;
        }

        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        float2 targetPos = new float2(targetTransform.Position.x, targetTransform.Position.y);
        float2 toTarget = targetPos - myPos;

        // Calculate and store desired angle
        targetAngle.Value = math.atan2(toTarget.y, toTarget.x) - math.PI * 0.5f;
        targetAngle.HasValidAngle = true;
    }
}

/// <summary>
/// Pass 2: Apply rotation to zombies based on calculated angles (parallel - no lookups needed)
/// </summary>
[BurstCompile]
public partial struct ApplyZombieRotationJob : IJobEntity
{
    public float RotationSpeed;
    public float DeltaTime;

    void Execute(ref LocalTransform transform, in ZombieTargetAngle targetAngle, in ZombieState zombieState)
    {
        if (!targetAngle.HasValidAngle)
            return;

        // Get current angle
        float currentAngle = math.atan2(
            2f * (transform.Rotation.value.w * transform.Rotation.value.z),
            1f - 2f * (transform.Rotation.value.z * transform.Rotation.value.z)
        );

        // Smoothly rotate toward target
        float angleDiff = math.fmod(targetAngle.Value - currentAngle + math.PI * 3f, math.PI * 2f) - math.PI;
        float rotationStep = math.clamp(angleDiff, -RotationSpeed * DeltaTime, RotationSpeed * DeltaTime);
        float newAngle = currentAngle + rotationStep;

        transform.Rotation = quaternion.RotateZ(newAngle);
    }
}
