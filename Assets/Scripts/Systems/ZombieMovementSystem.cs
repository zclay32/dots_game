using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that moves zombies based on their state machine state
/// Replaces the zombie portion of UnitMovementSystem for state-machine-controlled zombies
///
/// Movement by state:
/// - Idle: No movement
/// - Wandering: Slow movement toward wander target
/// - Chasing: Full speed toward cached target position
/// - WindingUp/Attacking/Cooldown: No movement (stationary)
///
/// Uses Burst-compiled parallel job for performance.
/// Target positions are cached by ZombieStateMachineSystem to avoid ComponentLookup aliasing.
/// </summary>
[BurstCompile]
[UpdateAfter(typeof(ZombieStateMachineSystem))]
[UpdateBefore(typeof(FacingRotationSystem))]
public partial struct ZombieMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        new MoveZombiesByStateJob
        {
            DeltaTime = deltaTime
        }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct MoveZombiesByStateJob : IJobEntity
{
    public float DeltaTime;

    void Execute(ref LocalTransform transform, ref Velocity velocity, ref TargetPosition targetPosition,
                 in MoveSpeed speed, in ZombieCombatState combatState, in ZombieCombatConfig combatConfig,
                 in EnemyUnit enemyTag)
    {
        float2 currentPos = new float2(transform.Position.x, transform.Position.y);

        switch (combatState.State)
        {
            case ZombieCombatAIState.Idle:
                // No movement when idle
                velocity.Value = float2.zero;
                targetPosition.HasTarget = false;
                break;

            case ZombieCombatAIState.Wandering:
                // Slow movement toward wander target
                MoveToward(ref transform, ref velocity, ref targetPosition, currentPos,
                    combatState.WanderTarget, speed.Value * combatConfig.WanderSpeedMultiplier, DeltaTime);
                break;

            case ZombieCombatAIState.Chasing:
                // Full speed toward cached target position
                if (combatState.HasTarget)
                {
                    float2 targetPos = combatState.CachedTargetPos;

                    // Set target position for other systems (facing, etc.)
                    targetPosition.Value = targetPos;
                    targetPosition.HasTarget = true;

                    // Check if close enough to stop
                    float distance = math.distance(currentPos, targetPos);
                    if (distance <= combatConfig.AttackRange)
                    {
                        // In range - stop moving
                        velocity.Value = float2.zero;
                    }
                    else
                    {
                        // Move toward target
                        MoveToward(ref transform, ref velocity, ref targetPosition, currentPos,
                            targetPos, speed.Value, DeltaTime);
                    }
                }
                else
                {
                    velocity.Value = float2.zero;
                    targetPosition.HasTarget = false;
                }
                break;

            case ZombieCombatAIState.WindingUp:
            case ZombieCombatAIState.Attacking:
            case ZombieCombatAIState.Cooldown:
                // Stationary during combat - but keep target position for facing
                velocity.Value = float2.zero;
                if (combatState.HasTarget)
                {
                    targetPosition.Value = combatState.CachedTargetPos;
                    targetPosition.HasTarget = true;
                }
                break;
        }
    }

    private void MoveToward(ref LocalTransform transform, ref Velocity velocity, ref TargetPosition targetPosition,
        float2 currentPos, float2 targetPos, float moveSpeed, float deltaTime)
    {
        targetPosition.Value = targetPos;
        targetPosition.HasTarget = true;

        float2 direction = targetPos - currentPos;
        float distance = math.length(direction);

        if (distance > 0.1f)
        {
            float2 normalizedDir = direction / distance;
            velocity.Value = normalizedDir * moveSpeed;

            float2 movement = velocity.Value * deltaTime;
            transform.Position.x += movement.x;
            transform.Position.y += movement.y;
        }
        else
        {
            velocity.Value = float2.zero;
        }
    }
}
