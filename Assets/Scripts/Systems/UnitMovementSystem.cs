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
                float distToCommand = math.distance(currentPos, moveCommand.ValueRO.Target);

                // If we've reached the destination, clear the command
                if (distToCommand < 0.5f)
                {
                    moveCommand.ValueRW.HasCommand = false;
                    targetPos.ValueRW.HasTarget = false;
                }
                else
                {
                    targetPos.ValueRW.Value = moveCommand.ValueRO.Target;
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
/// Runs in parallel using Burst compilation for maximum performance
/// </summary>
[BurstCompile]
[UpdateAfter(typeof(UpdateCombatTargetPositionSystem))]
public partial struct UnitMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        // Process soldiers (with MoveCommand)
        new MoveSoldiersJob
        {
            DeltaTime = deltaTime
        }.ScheduleParallel();
        
        // Process zombies (without MoveCommand)
        new MoveZombiesJob
        {
            DeltaTime = deltaTime
        }.ScheduleParallel();
    }
}

/// <summary>
/// Job that moves soldiers - respects MoveCommand priority
/// </summary>
[BurstCompile]
public partial struct MoveSoldiersJob : IJobEntity
{
    public float DeltaTime;
    
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

