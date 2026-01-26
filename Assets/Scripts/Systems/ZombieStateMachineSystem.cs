using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that manages zombie AI state machine transitions
/// See ZOMBIE_COMBAT_PRD.md for state machine documentation
///
/// State flow:
/// Idle/Wandering → (target acquired) → Chasing → (in range) → WindingUp → (timer) → Attacking → Cooldown → Attacking...
///
/// FULLY PARALLEL: All processing runs as Burst-compiled parallel jobs.
/// Uses double-buffered spatial hash (ReadBuffer) - no sync points needed.
/// </summary>
[UpdateAfter(typeof(SpatialHashSystem))]
[UpdateBefore(typeof(UnitMovementSystem))]
public partial struct ZombieStateMachineSystem : ISystem
{
    private uint _randomSeed;
    private int _frameCount;
    private const int TARGET_SEARCH_INTERVAL = 8; // Only search for targets every N frames

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialHashConfig>();
        _randomSeed = 54321;
        _frameCount = 0;
    }

    public void OnUpdate(ref SystemState state)
    {
        _frameCount++;
        _randomSeed += 1;
        float deltaTime = SystemAPI.Time.DeltaTime;

        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var healthLookup = SystemAPI.GetComponentLookup<Health>(true);

        // PASS 1: Burst-compiled parallel job for all state processing
        state.Dependency = new ZombieStateUpdateJob
        {
            DeltaTime = deltaTime,
            RandomSeed = _randomSeed,
            TransformLookup = transformLookup,
            HealthLookup = healthLookup
        }.ScheduleParallel(state.Dependency);

        // PASS 2: Target search for Idle/Wandering zombies (PARALLEL, every N frames)
        // Uses double-buffered spatial hash ReadBuffer (1-frame-old data, guaranteed complete)
        bool doTargetSearch = (_frameCount % TARGET_SEARCH_INTERVAL) == 0;
        if (doTargetSearch && SpatialHashDoubleBuffer.IsCreated)
        {
            var hashMap = SpatialHashDoubleBuffer.ReadBuffer;
            if (hashMap.IsCreated)
            {
                var config = SystemAPI.GetSingleton<SpatialHashConfig>();
                var factionLookup = SystemAPI.GetComponentLookup<Faction>(true);
                var playerUnitLookup = SystemAPI.GetComponentLookup<PlayerUnit>(true);
                var crystalLookup = SystemAPI.GetComponentLookup<Crystal>(true);

                // Schedule parallel target search job - NO Complete() needed!
                state.Dependency = new ZombieTargetSearchJob
                {
                    SpatialHashMap = hashMap,
                    TransformLookup = transformLookup,
                    HealthLookup = healthLookup,
                    FactionLookup = factionLookup,
                    PlayerUnitLookup = playerUnitLookup,
                    CrystalLookup = crystalLookup,
                    CellSize = config.CellSize,
                    WorldOffset = config.WorldOffset
                }.ScheduleParallel(state.Dependency);
            }
        }
    }
}

/// <summary>
/// Parallel job for zombie target searching.
/// Uses spatial hash to find nearest player target for idle/wandering zombies.
/// Implements priority targeting: soldiers (PlayerUnit) are preferred over the crystal.
/// </summary>
[BurstCompile]
public partial struct ZombieTargetSearchJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialHashMap;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<Health> HealthLookup;
    [ReadOnly] public ComponentLookup<Faction> FactionLookup;
    [ReadOnly] public ComponentLookup<PlayerUnit> PlayerUnitLookup;
    [ReadOnly] public ComponentLookup<Crystal> CrystalLookup;
    public float CellSize;
    public float2 WorldOffset;

    void Execute(
        ref ZombieCombatState combatState,
        in ZombieCombatConfig combatConfig,
        in LocalTransform transform,
        in EnemyUnit enemyTag)
    {
        // Search for targets in these states:
        // - Idle: Standing still, looking for targets
        // - Wandering: Moving slowly, can still detect targets
        // - Chasing without target: Moving toward a position (noise or wave spawn), can acquire target
        bool shouldSearch = combatState.State == ZombieCombatAIState.Idle ||
                           combatState.State == ZombieCombatAIState.Wandering ||
                           (combatState.State == ZombieCombatAIState.Chasing && !combatState.HasTarget);

        if (!shouldSearch)
            return;

        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        float searchRadius = combatConfig.AlertRadius;

        Entity target = FindTarget(myPos, searchRadius);

        if (target != Entity.Null)
        {
            // Target found - start chasing
            combatState.HasTarget = true;
            combatState.CurrentTarget = target;
            combatState.HasEngagedTarget = false;
            combatState.State = ZombieCombatAIState.Chasing;

            // Cache target position immediately
            if (TransformLookup.HasComponent(target))
            {
                var targetTransform = TransformLookup[target];
                combatState.CachedTargetPos = new float2(targetTransform.Position.x, targetTransform.Position.y);
            }
        }
    }

    private Entity FindTarget(float2 myPos, float searchRadius)
    {
        int2 myCell = SpatialHashHelper.WorldToCell(myPos, CellSize, WorldOffset);
        int cellRadius = (int)math.ceil(searchRadius / CellSize);

        // Track best unit (soldier) and best crystal separately for priority targeting
        Entity bestUnitTarget = Entity.Null;
        float bestUnitDistanceSq = searchRadius * searchRadius;

        Entity bestCrystalTarget = Entity.Null;
        float bestCrystalDistanceSq = searchRadius * searchRadius;

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                int2 cell = myCell + new int2(x, y);
                int hash = SpatialHashHelper.GetCellHash(cell);

                if (!SpatialHashMap.TryGetFirstValue(hash, out Entity other, out var iterator))
                    continue;

                do
                {
                    // Skip non-player faction
                    if (!FactionLookup.HasComponent(other)) continue;
                    var faction = FactionLookup[other];
                    if (faction.Value != FactionType.Player) continue;

                    // Skip dead units
                    if (!HealthLookup.HasComponent(other)) continue;
                    var health = HealthLookup[other];
                    if (health.IsDead) continue;

                    // Check distance (squared for performance)
                    if (!TransformLookup.HasComponent(other)) continue;
                    var otherTransform = TransformLookup[other];
                    float2 otherPos = new float2(otherTransform.Position.x, otherTransform.Position.y);
                    float distanceSq = math.distancesq(myPos, otherPos);

                    // Categorize target: soldier (PlayerUnit) vs crystal
                    bool isUnit = PlayerUnitLookup.HasComponent(other);
                    bool isCrystal = CrystalLookup.HasComponent(other);

                    if (isUnit && distanceSq < bestUnitDistanceSq)
                    {
                        // Prefer soldiers - they are primary targets
                        bestUnitDistanceSq = distanceSq;
                        bestUnitTarget = other;
                    }
                    else if (isCrystal && distanceSq < bestCrystalDistanceSq)
                    {
                        // Crystal is fallback target
                        bestCrystalDistanceSq = distanceSq;
                        bestCrystalTarget = other;
                    }

                } while (SpatialHashMap.TryGetNextValue(out other, ref iterator));
            }
        }

        // Priority: return soldier if found, otherwise crystal
        if (bestUnitTarget != Entity.Null)
            return bestUnitTarget;

        return bestCrystalTarget;
    }
}

/// <summary>
/// Burst-compiled job for zombie state machine updates
/// Handles all states except target searching
/// </summary>
[BurstCompile]
public partial struct ZombieStateUpdateJob : IJobEntity
{
    public float DeltaTime;
    public uint RandomSeed;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<Health> HealthLookup;

    void Execute(ref ZombieCombatState combatState, in ZombieCombatConfig combatConfig,
                 in LocalTransform transform, in Velocity velocity, in EnemyUnit enemyTag,
                 [EntityIndexInQuery] int entityIndex)
    {
        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        var random = Random.CreateFromIndex(RandomSeed + (uint)entityIndex);

        // Validate current target
        if (combatState.HasTarget)
        {
            bool targetValid = ValidateTarget(combatState.CurrentTarget);
            if (!targetValid)
            {
                // Target lost - clear and transition to idle
                combatState.HasTarget = false;
                combatState.CurrentTarget = Entity.Null;
                combatState.HasEngagedTarget = false;
                combatState.State = ZombieCombatAIState.Idle;
                combatState.StateTimer = random.NextFloat(2f, 5f);
            }
        }

        // Process state machine based on current state
        switch (combatState.State)
        {
            case ZombieCombatAIState.Idle:
                ProcessIdleState(ref combatState, combatConfig, ref random);
                break;

            case ZombieCombatAIState.Wandering:
                ProcessWanderingState(ref combatState, combatConfig, myPos, ref random);
                break;

            case ZombieCombatAIState.Chasing:
                ProcessChasingState(ref combatState, combatConfig, myPos, velocity, ref random);
                break;

            case ZombieCombatAIState.WindingUp:
                ProcessWindingUpState(ref combatState, combatConfig, myPos);
                break;

            case ZombieCombatAIState.Attacking:
                // Attacking is handled by ZombieCombatExecutionSystem
                break;

            case ZombieCombatAIState.Cooldown:
                ProcessCooldownState(ref combatState, combatConfig, myPos);
                break;
        }

        // Cache target position for movement system
        if (combatState.HasTarget && TransformLookup.HasComponent(combatState.CurrentTarget))
        {
            var targetTransform = TransformLookup[combatState.CurrentTarget];
            combatState.CachedTargetPos = new float2(targetTransform.Position.x, targetTransform.Position.y);
        }
    }

    private bool ValidateTarget(Entity target)
    {
        if (target == Entity.Null) return false;
        if (!TransformLookup.HasComponent(target)) return false;
        if (!HealthLookup.HasComponent(target)) return false;

        var health = HealthLookup[target];
        return !health.IsDead;
    }

    private void ProcessIdleState(ref ZombieCombatState combatState, ZombieCombatConfig config, ref Random random)
    {
        // Target searching is done by ZombieTargetSearchJob - here we just handle timer for wandering
        combatState.StateTimer -= DeltaTime;
        if (combatState.StateTimer <= 0)
        {
            // Start wandering
            combatState.State = ZombieCombatAIState.Wandering;
            combatState.StateTimer = random.NextFloat(3f, 8f);

            // Pick random wander target within range of spawn position
            float2 offset = random.NextFloat2Direction() * random.NextFloat(0.5f, config.WanderRadius);
            combatState.WanderTarget = config.SpawnPosition + offset;
        }
    }

    private void ProcessWanderingState(ref ZombieCombatState combatState, ZombieCombatConfig config,
        float2 myPos, ref Random random)
    {
        // Target searching is done by ZombieTargetSearchJob
        // Here we handle wander target updates and timer

        float distToWander = math.distance(myPos, combatState.WanderTarget);
        float distFromSpawn = math.distance(myPos, config.SpawnPosition);
        float wanderTargetDistFromSpawn = math.distance(combatState.WanderTarget, config.SpawnPosition);

        // Check if zombie is investigating something far from spawn (e.g., noise)
        // If wander target is far from spawn, don't override it - let them investigate
        bool isInvestigating = wanderTargetDistFromSpawn > config.WanderRadius * 2f;

        if (isInvestigating)
        {
            // Investigating noise or other distant point - just move toward it
            // When timer expires or arrives, will transition appropriately
            if (distToWander < 0.5f)
            {
                // Arrived at investigation point - go idle here
                combatState.State = ZombieCombatAIState.Idle;
                combatState.StateTimer = random.NextFloat(2f, 5f);
                return;
            }
        }
        else
        {
            // Normal wandering near spawn
            // If zombie has wandered too far from spawn, redirect back toward spawn
            if (distFromSpawn > config.WanderRadius * 1.5f)
            {
                float2 toSpawn = math.normalizesafe(config.SpawnPosition - myPos);
                combatState.WanderTarget = config.SpawnPosition + toSpawn * random.NextFloat(0.5f, config.WanderRadius * 0.5f);
            }
            else if (distToWander < 0.5f)
            {
                // Reached current target - pick new wander target
                float2 offset = random.NextFloat2Direction() * random.NextFloat(0.5f, config.WanderRadius);
                combatState.WanderTarget = config.SpawnPosition + offset;
            }
        }

        // Countdown wander duration
        combatState.StateTimer -= DeltaTime;
        if (combatState.StateTimer <= 0)
        {
            combatState.State = ZombieCombatAIState.Idle;
            combatState.StateTimer = random.NextFloat(2f, 5f);
        }
    }

    private void ProcessChasingState(ref ZombieCombatState combatState, ZombieCombatConfig config,
        float2 myPos, Velocity velocity, ref Random random)
    {
        if (combatState.HasTarget)
        {
            // Chasing a specific entity target - use cached target position
            float2 targetPos = combatState.CachedTargetPos;
            float distance = math.distance(myPos, targetPos);

            // Check if in attack range and stopped
            bool inRange = distance <= config.AttackRange * 1.1f;
            bool stopped = math.lengthsq(velocity.Value) < 0.01f;

            if (inRange && stopped)
            {
                if (combatState.HasEngagedTarget)
                {
                    combatState.State = ZombieCombatAIState.Attacking;
                }
                else
                {
                    combatState.State = ZombieCombatAIState.WindingUp;
                    combatState.StateTimer = config.AttackWindup;
                }
            }
        }
        else
        {
            // Chasing toward a position (noise investigation at full speed)
            // No entity target - just moving toward WanderTarget
            float2 chasePos = combatState.WanderTarget;
            float distanceToPos = math.distance(myPos, chasePos);

            // Check if arrived at the noise position
            if (distanceToPos < 0.5f)
            {
                // Arrived at noise location - go idle here (new "home" position)
                combatState.State = ZombieCombatAIState.Idle;
                combatState.StateTimer = random.NextFloat(2f, 5f);
                return;
            }

            // Countdown chase timer - if it expires before arrival, give up
            combatState.StateTimer -= DeltaTime;
            if (combatState.StateTimer <= 0)
            {
                // Chase timeout - go idle at current position
                combatState.State = ZombieCombatAIState.Idle;
                combatState.StateTimer = random.NextFloat(2f, 5f);
            }
        }
    }

    private void ProcessWindingUpState(ref ZombieCombatState combatState, ZombieCombatConfig config, float2 myPos)
    {
        if (!combatState.HasTarget)
        {
            combatState.State = ZombieCombatAIState.Idle;
            return;
        }

        // Use cached target position
        float2 targetPos = combatState.CachedTargetPos;
        float distance = math.distance(myPos, targetPos);

        if (distance > config.AttackRange * 1.2f)
        {
            combatState.State = ZombieCombatAIState.Chasing;
            return;
        }

        combatState.StateTimer -= DeltaTime;
        if (combatState.StateTimer <= 0)
        {
            combatState.State = ZombieCombatAIState.Attacking;
            combatState.HasEngagedTarget = true;
        }
    }

    private void ProcessCooldownState(ref ZombieCombatState combatState, ZombieCombatConfig config, float2 myPos)
    {
        if (!combatState.HasTarget)
        {
            combatState.State = ZombieCombatAIState.Idle;
            return;
        }

        combatState.StateTimer -= DeltaTime;
        if (combatState.StateTimer <= 0)
        {
            // Use cached target position
            float2 targetPos = combatState.CachedTargetPos;
            float distance = math.distance(myPos, targetPos);

            if (distance <= config.AttackRange * 1.1f)
            {
                combatState.State = ZombieCombatAIState.Attacking;
            }
            else
            {
                combatState.State = ZombieCombatAIState.Chasing;
            }
        }
    }
}
