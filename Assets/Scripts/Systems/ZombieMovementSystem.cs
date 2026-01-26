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
[UpdateAfter(typeof(ZombieStateMachineSystem))]
[UpdateBefore(typeof(FacingRotationSystem))]
public partial struct ZombieMovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool flowFieldReady = FlowFieldData.IsCreated;
        var targetRadiusLookup = SystemAPI.GetComponentLookup<TargetRadius>(true);

        new MoveZombiesByStateJob
        {
            DeltaTime = deltaTime,
            FlowFieldReady = flowFieldReady,
            GridWidth = flowFieldReady ? FlowFieldData.GridWidth : 0,
            GridHeight = flowFieldReady ? FlowFieldData.GridHeight : 0,
            CellSize = flowFieldReady ? FlowFieldData.CellSize : 1f,
            WorldOffset = flowFieldReady ? FlowFieldData.WorldOffset : float2.zero,
            Walkable = flowFieldReady ? FlowFieldData.Walkable : default,
            FlowDirections = flowFieldReady ? FlowFieldData.FlowDirections : default,
            TargetRadiusLookup = targetRadiusLookup
        }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct MoveZombiesByStateJob : IJobEntity
{
    public float DeltaTime;
    public bool FlowFieldReady;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
    public float2 WorldOffset;
    [ReadOnly] public NativeArray<bool> Walkable;
    [ReadOnly] public NativeArray<float2> FlowDirections;
    [ReadOnly] public ComponentLookup<TargetRadius> TargetRadiusLookup;

    void Execute(ref LocalTransform transform, ref Velocity velocity, ref TargetPosition targetPosition,
                 in MoveSpeed speed, in ZombieCombatState combatState, in ZombieCombatConfig combatConfig,
                 in EnemyUnit enemyTag)
    {
        float2 currentPos = new float2(transform.Position.x, transform.Position.y);

        // Get target radius if targeting a large entity (like crystal)
        float targetRadius = 0f;
        if (combatState.HasTarget && TargetRadiusLookup.HasComponent(combatState.CurrentTarget))
        {
            targetRadius = TargetRadiusLookup[combatState.CurrentTarget].Value;
        }

        switch (combatState.State)
        {
            case ZombieCombatAIState.Idle:
                // No movement when idle
                velocity.Value = float2.zero;
                targetPosition.HasTarget = false;
                break;

            case ZombieCombatAIState.Wandering:
                // Slow movement toward wander target (no flow field - direct movement)
                MoveToward(ref transform, ref velocity, ref targetPosition, currentPos,
                    combatState.WanderTarget, speed.Value * combatConfig.WanderSpeedMultiplier, DeltaTime,
                    useFlowField: false);
                break;

            case ZombieCombatAIState.Chasing:
                // Full speed toward target
                if (combatState.HasTarget)
                {
                    // Chasing a specific entity - use cached target position
                    float2 targetPos = combatState.CachedTargetPos;

                    // Set target position for other systems (facing, etc.)
                    targetPosition.Value = targetPos;
                    targetPosition.HasTarget = true;

                    float distance = math.distance(currentPos, targetPos);

                    // For small targets (soldiers): stop when within attack range
                    // For large targets (crystal with targetRadius): keep moving until blocked
                    if (targetRadius < 0.5f && distance <= combatConfig.AttackRange)
                    {
                        // In range of point target (soldiers) - stop moving
                        velocity.Value = float2.zero;
                    }
                    else
                    {
                        // Use flow field only for distant targets (soldiers moving around)
                        // For close/stationary targets (like crystal), use direct movement
                        // Flow field points toward soldiers, so it would misdirect zombies targeting crystal
                        bool useFlowField = targetRadius < 0.5f && distance > 10f;
                        MoveToward(ref transform, ref velocity, ref targetPosition, currentPos,
                            targetPos, speed.Value, DeltaTime, useFlowField);
                    }
                }
                else
                {
                    // No entity target - chase toward WanderTarget (noise position) at full speed
                    // Use DIRECT movement, not flow field, so zombie investigates the actual noise source
                    float2 chasePos = combatState.WanderTarget;
                    float distanceToNoise = math.distance(currentPos, chasePos);

                    if (distanceToNoise > 0.5f)
                    {
                        // Still moving toward noise location (direct path, not flow field)
                        MoveToward(ref transform, ref velocity, ref targetPosition, currentPos,
                            chasePos, speed.Value, DeltaTime, useFlowField: false);
                    }
                    else
                    {
                        // Arrived at noise location - stop moving
                        // ZombieStateMachineSystem will transition to Idle when StateTimer expires
                        velocity.Value = float2.zero;
                        targetPosition.HasTarget = false;
                    }
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
        float2 currentPos, float2 targetPos, float moveSpeed, float deltaTime, bool useFlowField)
    {
        targetPosition.Value = targetPos;
        targetPosition.HasTarget = true;

        float2 direction = targetPos - currentPos;
        float distance = math.length(direction);

        if (distance > 0.1f)
        {
            float2 moveDir;

            // Use flow field for pathfinding around obstacles (only when chasing entity targets)
            if (useFlowField && FlowFieldReady && FlowDirections.IsCreated)
            {
                float2 flowDir = GetFlowDirection(currentPos);
                if (math.lengthsq(flowDir) > 0.01f)
                {
                    moveDir = flowDir;
                }
                else
                {
                    moveDir = direction / distance;
                }
            }
            else
            {
                // Direct movement toward target (for noise investigation, wandering)
                moveDir = direction / distance;
            }

            float2 intendedVelocity = moveDir * moveSpeed;
            float2 movement = intendedVelocity * deltaTime;
            float2 newPos = currentPos + movement;

            // Check walkability and slide along obstacles if needed
            // Track actual movement for velocity calculation
            float2 actualMovement = float2.zero;

            if (!FlowFieldReady || !Walkable.IsCreated || IsWalkable(newPos))
            {
                transform.Position.x = newPos.x;
                transform.Position.y = newPos.y;
                actualMovement = movement;
            }
            else
            {
                // Try sliding along X axis only
                float2 slideX = new float2(currentPos.x + movement.x, currentPos.y);
                if (IsWalkable(slideX))
                {
                    transform.Position.x = slideX.x;
                    actualMovement.x = movement.x;
                }
                else
                {
                    // Try sliding along Y axis only
                    float2 slideY = new float2(currentPos.x, currentPos.y + movement.y);
                    if (IsWalkable(slideY))
                    {
                        transform.Position.y = slideY.y;
                        actualMovement.y = movement.y;
                    }
                    // If neither works, actualMovement stays zero
                }
            }

            // Set velocity to reflect actual movement (for state machine stopped detection)
            if (math.lengthsq(actualMovement) > 0.0001f)
            {
                velocity.Value = actualMovement / deltaTime;
            }
            else
            {
                // Completely blocked - can't move in any direction
                velocity.Value = float2.zero;
            }
        }
        else
        {
            velocity.Value = float2.zero;
        }
    }

    bool IsWalkable(float2 worldPos)
    {
        float2 localPos = worldPos + WorldOffset;
        int x = (int)math.floor(localPos.x / CellSize);
        int y = (int)math.floor(localPos.y / CellSize);

        if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
            return false;

        int index = y * GridWidth + x;
        if (index < 0 || index >= Walkable.Length)
            return false;

        return Walkable[index];
    }

    float2 GetFlowDirection(float2 worldPos)
    {
        float2 localPos = worldPos + WorldOffset;
        int x = (int)math.floor(localPos.x / CellSize);
        int y = (int)math.floor(localPos.y / CellSize);

        if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
            return float2.zero;

        int index = y * GridWidth + x;
        if (index < 0 || index >= FlowDirections.Length)
            return float2.zero;

        return FlowDirections[index];
    }
}
