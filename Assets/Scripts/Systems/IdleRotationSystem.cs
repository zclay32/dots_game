using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// OPTIMIZED: Idle rotation system for units when not moving or attacking
/// Units slowly rotate back and forth to appear alive and alert
///
/// Performance optimizations:
/// - Runs every 3 frames (16ms interval) - idle rotation doesn't need precision
/// - Fully Burst compiled - pure math operations
/// - Hot/cold component separation for cache efficiency
/// - Early exit conditions to skip inactive units
/// - Uses lengthsq to avoid sqrt operations
///
/// Behavior:
/// - Soldiers: Only rotate when idle (no movement, no combat target)
/// - Zombies: Only rotate when dormant (not alert/chasing)
/// - Random rotation patterns per unit for organic appearance
/// </summary>
[BurstCompile]
[UpdateAfter(typeof(FacingRotationSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial struct IdleRotationSystem : ISystem
{
    private int _frameCount;
    private const int UPDATE_INTERVAL = 3;  // Run every 3 frames (~16ms at 60fps)

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _frameCount++;

        // Frame skip for performance
        if (_frameCount % UPDATE_INTERVAL != 0)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime * UPDATE_INTERVAL;
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        // Process soldier idle rotation
        new SoldierIdleRotationJob
        {
            DeltaTime = deltaTime,
            ElapsedTime = elapsedTime
        }.ScheduleParallel();

        // Process zombie idle rotation
        new ZombieIdleRotationJob
        {
            DeltaTime = deltaTime,
            ElapsedTime = elapsedTime
        }.ScheduleParallel();
    }
}

/// <summary>
/// Soldier idle rotation - only rotate when no target and not moving
/// </summary>
[BurstCompile]
public partial struct SoldierIdleRotationJob : IJobEntity
{
    public float DeltaTime;
    public double ElapsedTime;

    void Execute(
        ref IdleRotationState rotationState,
        ref LocalTransform transform,
        in IdleRotationConfig config,
        in Velocity velocity,
        in CombatTarget combatTarget,
        in PlayerUnit soldierTag,
        [EntityIndexInQuery] int entityIndex)
    {
        // Don't rotate if moving
        if (math.lengthsq(velocity.Value) > 0.01f)
            return;

        // Don't rotate if has combat target (facing system handles this)
        if (combatTarget.Target != Entity.Null)
            return;

        // Update rotation state
        IdleRotationJobExtensions.UpdateIdleRotation(
            ref rotationState,
            ref transform,
            in config,
            DeltaTime,
            ElapsedTime,
            entityIndex
        );
    }
}

/// <summary>
/// Zombie idle rotation - only rotate when dormant (not alert/chasing)
/// </summary>
[BurstCompile]
public partial struct ZombieIdleRotationJob : IJobEntity
{
    public float DeltaTime;
    public double ElapsedTime;

    void Execute(
        ref IdleRotationState rotationState,
        ref LocalTransform transform,
        in IdleRotationConfig config,
        in Velocity velocity,
        in ZombieState zombieState,
        in EnemyUnit zombieTag,
        [EntityIndexInQuery] int entityIndex)
    {
        // Don't rotate if moving
        if (math.lengthsq(velocity.Value) > 0.01f)
            return;

        // Only rotate when dormant (not alert/chasing)
        if (zombieState.State != ZombieAIState.Dormant)
            return;

        // Update rotation state
        IdleRotationJobExtensions.UpdateIdleRotation(
            ref rotationState,
            ref transform,
            in config,
            DeltaTime,
            ElapsedTime,
            entityIndex
        );
    }
}

/// <summary>
/// Shared rotation logic - Burst-friendly static method
/// Uses smooth rotation toward random target angles
/// </summary>
[BurstCompile]
static class IdleRotationUtility
{
    public static void UpdateIdleRotation(
        ref IdleRotationState state,
        ref LocalTransform transform,
        in IdleRotationConfig config,
        float deltaTime,
        double elapsedTime,
        int entityIndex)
    {
        // Countdown to next direction change
        state.TimeUntilNextChange -= deltaTime;

        // Pick new target angle when timer expires
        if (state.TimeUntilNextChange <= 0f)
        {
            // Get current angle from rotation
            float currentAngle = GetRotationAngle(in transform.Rotation);

            // Random angle offset within max range - unique seed per entity
            var random = Random.CreateFromIndex((uint)(entityIndex * 73856093 + elapsedTime * 100f + currentAngle * 1000f));
            float angleOffset = random.NextFloat(-config.MaxAngleChange, config.MaxAngleChange);
            state.TargetAngle = currentAngle + angleOffset;

            // Random rotation speed
            state.CurrentRotationSpeed = random.NextFloat(config.MinRotationSpeed, config.MaxRotationSpeed);

            // Randomly choose direction
            if (random.NextBool())
                state.CurrentRotationSpeed = -state.CurrentRotationSpeed;

            // Random time until next change
            state.TimeUntilNextChange = random.NextFloat(config.MinChangeInterval, config.MaxChangeInterval);
        }

        // Get current angle
        float angle = GetRotationAngle(in transform.Rotation);

        // Smoothly rotate toward target
        float angleDiff = math.fmod(state.TargetAngle - angle + math.PI * 3f, math.PI * 2f) - math.PI;
        float rotationStep = math.clamp(
            angleDiff,
            -math.abs(state.CurrentRotationSpeed) * deltaTime,
            math.abs(state.CurrentRotationSpeed) * deltaTime
        );

        float newAngle = angle + rotationStep;
        transform.Rotation = quaternion.RotateZ(newAngle);
    }

    [BurstCompile]
    static float GetRotationAngle(in quaternion rotation)
    {
        // Extract Z-axis rotation angle from quaternion
        return math.atan2(
            2f * (rotation.value.w * rotation.value.z),
            1f - 2f * (rotation.value.z * rotation.value.z)
        );
    }
}

// Extension to make the static method accessible to jobs
[BurstCompile]
static class IdleRotationJobExtensions
{
    [BurstCompile]
    public static void UpdateIdleRotation(
        ref IdleRotationState state,
        ref LocalTransform transform,
        in IdleRotationConfig config,
        float deltaTime,
        double elapsedTime,
        int entityIndex)
    {
        IdleRotationUtility.UpdateIdleRotation(ref state, ref transform, in config, deltaTime, elapsedTime, entityIndex);
    }
}
