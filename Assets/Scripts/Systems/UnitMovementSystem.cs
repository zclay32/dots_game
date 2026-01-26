using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that updates target positions based on combat targets
/// For zombies without direct targets, uses flow field for navigation
/// </summary>
[UpdateAfter(typeof(TargetFindingSystem))]
[UpdateAfter(typeof(FlowFieldSystem))]
[UpdateBefore(typeof(UnitMovementSystem))]
public partial struct UpdateCombatTargetPositionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        bool flowFieldReady = FlowFieldData.IsCreated;
        
        // Update soldiers - prioritize MoveCommand, then hold position (don't chase)
        foreach (var (targetPos, combatTarget, moveCommand, transform) in
            SystemAPI.Query<RefRW<TargetPosition>, RefRW<CombatTarget>, RefRW<MoveCommand>, RefRO<LocalTransform>>()
            .WithAll<PlayerUnit>())
        {
            // Validate combat target - clear if destroyed
            if (combatTarget.ValueRO.Target != Entity.Null && !transformLookup.HasComponent(combatTarget.ValueRO.Target))
            {
                combatTarget.ValueRW.Target = Entity.Null;
            }

            // Priority 1: Move command from player
            if (moveCommand.ValueRO.HasCommand)
            {
                float2 currentPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);

                // Use the actual walkable destination from flow field if available
                // (handles cases where player clicked on an obstacle)
                float2 actualDest = (SoldierFlowFieldData.IsCreated && SoldierFlowFieldData.HasDestination)
                    ? SoldierFlowFieldData.CurrentDestination
                    : moveCommand.ValueRO.Target;

                float distToCommand = math.distance(currentPos, actualDest);

                // If we've reached the destination, clear the command
                if (distToCommand < 1.0f) // Slightly larger threshold for flow field navigation
                {
                    moveCommand.ValueRW.HasCommand = false;
                    targetPos.ValueRW.HasTarget = false;
                }
                else
                {
                    targetPos.ValueRW.Value = actualDest;
                    targetPos.ValueRW.HasTarget = true;
                }
                continue;
            }

            // Priority 2: Hold position - soldiers don't chase enemies
            // They will attack enemies in range but won't move toward them
            targetPos.ValueRW.HasTarget = false;
        }
        
        // Update zombies - use combat target if available, otherwise flow field
        // Note: Zombies with ZombieCombatState are handled by ZombieMovementSystem
        foreach (var (targetPos, combatTarget, transform, zombieState) in
            SystemAPI.Query<RefRW<TargetPosition>, RefRW<CombatTarget>, RefRO<LocalTransform>, RefRO<ZombieState>>()
            .WithAll<EnemyUnit>()
            .WithNone<ZombieCombatState>())
        {
            // If has direct combat target, chase it
            if (combatTarget.ValueRO.Target != Entity.Null)
            {
                // Validate target still exists
                if (!transformLookup.HasComponent(combatTarget.ValueRO.Target))
                {
                    // Target destroyed - clear it immediately
                    combatTarget.ValueRW.Target = Entity.Null;
                }
                else
                {
                    var targetTransform = transformLookup[combatTarget.ValueRO.Target];
                    targetPos.ValueRW.Value = new float2(targetTransform.Position.x, targetTransform.Position.y);
                    targetPos.ValueRW.HasTarget = true;
                    continue; // Skip flow field logic
                }
            }
            // If alert/chasing but no direct target, use flow field
            else if (zombieState.ValueRO.State != ZombieAIState.Dormant && flowFieldReady)
            {
                float2 currentPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);
                float2 flowDir = FlowFieldData.GetFlowDirection(currentPos);
                
                if (math.lengthsq(flowDir) > 0.01f)
                {
                    // Set target slightly ahead in flow direction
                    targetPos.ValueRW.Value = currentPos + flowDir * 2f;
                    targetPos.ValueRW.HasTarget = true;
                }
                else
                {
                    targetPos.ValueRW.HasTarget = false;
                }
            }
            // Dormant - don't move
            else
            {
                targetPos.ValueRW.HasTarget = false;
            }
        }
    }
}

/// <summary>
/// System that moves all units towards their targets
/// Jobs run in parallel using Burst compilation for maximum performance
/// </summary>
[UpdateAfter(typeof(UpdateCombatTargetPositionSystem))]
public partial struct UnitMovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool flowFieldReady = FlowFieldData.IsCreated;

        // Process soldiers (with MoveCommand) - uses soldier flow field for pathfinding
        // Need main flow field for grid info; use its array as fallback when soldier flow field isn't ready
        if (flowFieldReady)
        {
            bool soldierFlowFieldReady = SoldierFlowFieldData.IsCreated && SoldierFlowFieldData.HasDestination;
            new MoveSoldiersJob
            {
                DeltaTime = deltaTime,
                SoldierFlowFieldReady = soldierFlowFieldReady,
                GridWidth = FlowFieldData.GridWidth,
                GridHeight = FlowFieldData.GridHeight,
                CellSize = FlowFieldData.CellSize,
                WorldOffset = FlowFieldData.WorldOffset,
                // Use main flow field as fallback (job checks SoldierFlowFieldReady before using)
                SoldierFlowDirections = soldierFlowFieldReady
                    ? SoldierFlowFieldData.FlowDirections
                    : FlowFieldData.FlowDirections,
                Walkable = FlowFieldData.Walkable
            }.ScheduleParallel();
        }

        // Process zombies (without MoveCommand)
        new MoveZombiesJob
        {
            DeltaTime = deltaTime
        }.ScheduleParallel();
    }
}

/// <summary>
/// Job that moves soldiers - uses soldier flow field for pathfinding around obstacles
/// </summary>
[BurstCompile]
public partial struct MoveSoldiersJob : IJobEntity
{
    public float DeltaTime;
    public bool SoldierFlowFieldReady;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
    public float2 WorldOffset;
    [ReadOnly] public NativeArray<float2> SoldierFlowDirections;
    [ReadOnly] public NativeArray<bool> Walkable;

    void Execute(ref LocalTransform transform, ref Velocity velocity, in MoveSpeed speed,
                 in TargetPosition target, in CombatTarget combatTarget, in Combat combat,
                 in MoveCommand moveCommand, in PlayerUnit playerTag)
    {
        if (!target.HasTarget)
        {
            velocity.Value = float2.zero;
            return;
        }

        float2 currentPos = new float2(transform.Position.x, transform.Position.y);
        float2 targetPos = target.Value;

        // Only stop for combat if we DON'T have an active move command
        if (!moveCommand.HasCommand && combatTarget.Target != Entity.Null)
        {
            float distToTarget = math.distance(currentPos, targetPos);
            if (distToTarget <= combat.AttackRange * 0.9f)
            {
                velocity.Value = float2.zero;
                return;
            }
        }

        float2 direction = targetPos - currentPos;
        float distance = math.length(direction);

        if (distance > 0.5f) // Use larger threshold since we're using flow field
        {
            float2 moveDir;

            // Use soldier flow field for pathfinding when we have a move command
            if (moveCommand.HasCommand && SoldierFlowFieldReady && SoldierFlowDirections.IsCreated)
            {
                moveDir = GetFlowDirection(currentPos);

                // If flow field has no direction, fall back to direct movement
                if (math.lengthsq(moveDir) < 0.01f)
                {
                    moveDir = direction / distance;
                }
            }
            else
            {
                // No move command or flow field not ready - use direct movement
                moveDir = direction / distance;
            }

            velocity.Value = moveDir * speed.Value;

            float2 movement = velocity.Value * DeltaTime;
            float2 newPos = currentPos + movement;

            // Check if new position is walkable - if not, try sliding along obstacle
            if (IsWalkable(newPos))
            {
                // Full movement is valid
                transform.Position.x = newPos.x;
                transform.Position.y = newPos.y;
            }
            else
            {
                // Try sliding along X axis only
                float2 slideX = new float2(currentPos.x + movement.x, currentPos.y);
                if (IsWalkable(slideX))
                {
                    transform.Position.x = slideX.x;
                }
                else
                {
                    // Try sliding along Y axis only
                    float2 slideY = new float2(currentPos.x, currentPos.y + movement.y);
                    if (IsWalkable(slideY))
                    {
                        transform.Position.y = slideY.y;
                    }
                    // If neither works, don't move (stay in place)
                }
            }
        }
        else
        {
            velocity.Value = float2.zero;
        }
    }

    bool IsWalkable(float2 worldPos)
    {
        if (!Walkable.IsCreated)
            return true;

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
        // Convert to grid cell
        float2 localPos = worldPos + WorldOffset;
        int x = (int)math.floor(localPos.x / CellSize);
        int y = (int)math.floor(localPos.y / CellSize);

        // Bounds check
        if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
            return float2.zero;

        int index = y * GridWidth + x;
        if (index < 0 || index >= SoldierFlowDirections.Length)
            return float2.zero;

        return SoldierFlowDirections[index];
    }
}

/// <summary>
/// Legacy job that moves zombies without state machine (no MoveCommand component)
/// Note: Zombies with ZombieCombatState are handled by ZombieMovementSystem instead
/// This job is excluded for zombies with the new state machine components
/// </summary>
[BurstCompile]
[WithNone(typeof(ZombieCombatState))]
public partial struct MoveZombiesJob : IJobEntity
{
    public float DeltaTime;

    void Execute(ref LocalTransform transform, ref Velocity velocity, in MoveSpeed speed,
                 in TargetPosition target, in CombatTarget combatTarget, in Combat combat,
                 in EnemyUnit enemyTag)
    {
        if (!target.HasTarget)
        {
            velocity.Value = float2.zero;
            return;
        }

        float2 currentPos = new float2(transform.Position.x, transform.Position.y);
        float2 targetPos = target.Value;

        // Stop moving if in attack range and has combat target
        // Use full attack range (not 0.9x) to avoid oscillation with separation system
        // Add small buffer (1.1x) for hysteresis - stay stopped until pushed significantly out
        if (combatTarget.Target != Entity.Null)
        {
            float distToTarget = math.distance(currentPos, targetPos);
            // If already very slow (nearly stopped), use larger threshold to stay stopped
            bool alreadyStopped = math.lengthsq(velocity.Value) < 0.01f;
            float stoppingRange = alreadyStopped ? combat.AttackRange * 1.1f : combat.AttackRange;

            if (distToTarget <= stoppingRange)
            {
                velocity.Value = float2.zero;
                return;
            }
        }

        float2 direction = targetPos - currentPos;
        float distance = math.length(direction);

        if (distance > 0.1f)
        {
            float2 normalizedDir = direction / distance;
            velocity.Value = normalizedDir * speed.Value;

            float2 movement = velocity.Value * DeltaTime;
            transform.Position.x += movement.x;
            transform.Position.y += movement.y;
        }
        else
        {
            velocity.Value = float2.zero;
        }
    }
}

