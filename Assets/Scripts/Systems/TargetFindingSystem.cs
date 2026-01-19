using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that finds combat targets for units
/// Zombies use different search radius based on AI state (dormant vs alert)
/// Runs every 10 frames, offset by 0 (staggered with other systems)
/// </summary>
[UpdateAfter(typeof(SpatialHashSystem))]
[UpdateBefore(typeof(CombatSystem))]
public partial struct TargetFindingSystem : ISystem
{
    private int _frameCount;
    private const int UPDATE_INTERVAL = 10;
    private const int FRAME_OFFSET = 0; // Stagger offset
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialHashConfig>();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        // Use double-buffered spatial hash - ReadBuffer has last frame's completed data
        if (!SpatialHashDoubleBuffer.IsCreated)
            return;

        var hashMap = SpatialHashDoubleBuffer.ReadBuffer;
        if (!hashMap.IsCreated)
            return;

        // Only update targets every N frames (with offset for staggering)
        _frameCount++;
        if ((_frameCount + FRAME_OFFSET) % UPDATE_INTERVAL != 0)
            return;

        var config = SystemAPI.GetSingleton<SpatialHashConfig>();

        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var factionLookup = SystemAPI.GetComponentLookup<Faction>(true);
        var healthLookup = SystemAPI.GetComponentLookup<Health>(true);

        // Hash map is guaranteed ready - using ReadBuffer (1-frame-old data, guaranteed complete)
        // Find targets for zombies (with state-based radius)
        var findZombieTargetsJob = new FindZombieTargetsJob
        {
            SpatialHashMap = hashMap,
            TransformLookup = transformLookup,
            FactionLookup = factionLookup,
            HealthLookup = healthLookup,
            CellSize = config.CellSize,
            WorldOffset = config.WorldOffset,
            DeltaTime = SystemAPI.Time.DeltaTime * 10f // Account for running every 10 frames
        };

        state.Dependency = findZombieTargetsJob.ScheduleParallel(state.Dependency);

        // Find targets for soldiers (always use full radius)
        var findSoldierTargetsJob = new FindSoldierTargetsJob
        {
            SpatialHashMap = hashMap,
            TransformLookup = transformLookup,
            FactionLookup = factionLookup,
            HealthLookup = healthLookup,
            CellSize = config.CellSize,
            WorldOffset = config.WorldOffset,
            SearchRadius = 8f
        };

        state.Dependency = findSoldierTargetsJob.ScheduleParallel(state.Dependency);
    }
}

/// <summary>
/// Job for zombie target finding with state-based search radius
/// NOTE: Zombies with ZombieCombatState use the new ZombieStateMachineSystem instead
/// </summary>
[BurstCompile]
[WithNone(typeof(ZombieCombatState))]
public partial struct FindZombieTargetsJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialHashMap;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<Faction> FactionLookup;
    [ReadOnly] public ComponentLookup<Health> HealthLookup;

    public float CellSize;
    public float2 WorldOffset;
    public float DeltaTime;

    void Execute(Entity entity, ref CombatTarget combatTarget, ref ZombieState zombieState,
                 in LocalTransform transform, in Faction faction)
    {
        // Determine search radius based on state
        float searchRadius = zombieState.State == ZombieAIState.Dormant 
            ? zombieState.AlertRadius 
            : zombieState.ChaseRadius;
        
        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        int2 myCell = SpatialHashHelper.WorldToCell(myPos, CellSize, WorldOffset);
        
        Entity bestTarget = Entity.Null;
        float bestDistance = float.MaxValue;
        
        int cellRadius = (int)math.ceil(searchRadius / CellSize);
        
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                int2 cell = myCell + new int2(x, y);
                int hash = SpatialHashHelper.GetCellHash(cell);
                
                if (SpatialHashMap.TryGetFirstValue(hash, out Entity otherEntity, out var iterator))
                {
                    do
                    {
                        if (otherEntity == entity)
                            continue;
                        
                        if (!FactionLookup.HasComponent(otherEntity))
                            continue;
                        
                        var otherFaction = FactionLookup[otherEntity];
                        if (otherFaction.Value == faction.Value)
                            continue;
                        
                        if (HealthLookup.HasComponent(otherEntity))
                        {
                            var health = HealthLookup[otherEntity];
                            if (health.IsDead)
                                continue;
                        }
                        
                        if (!TransformLookup.HasComponent(otherEntity))
                            continue;
                        
                        var otherTransform = TransformLookup[otherEntity];
                        float2 otherPos = new float2(otherTransform.Position.x, otherTransform.Position.y);
                        float distance = math.distance(myPos, otherPos);
                        
                        if (distance <= searchRadius && distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestTarget = otherEntity;
                        }
                        
                    } while (SpatialHashMap.TryGetNextValue(out otherEntity, ref iterator));
                }
            }
        }
        
        // Update combat target
        combatTarget.Target = bestTarget;
        
        // Update zombie state based on whether we found a target
        if (bestTarget != Entity.Null)
        {
            // Found target - become alert/chasing
            zombieState.State = ZombieAIState.Chasing;
            zombieState.AlertTimer = 5f; // Stay alert for 5 seconds after losing target
        }
        else if (zombieState.State != ZombieAIState.Dormant)
        {
            // No target - count down alert timer
            zombieState.AlertTimer -= DeltaTime;
            if (zombieState.AlertTimer <= 0)
            {
                zombieState.State = ZombieAIState.Dormant;
            }
            else
            {
                zombieState.State = ZombieAIState.Alert;
            }
        }
    }
}

/// <summary>
/// Job for soldier target finding (simpler, no state)
/// </summary>
[BurstCompile]
public partial struct FindSoldierTargetsJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialHashMap;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<Faction> FactionLookup;
    [ReadOnly] public ComponentLookup<Health> HealthLookup;
    
    public float CellSize;
    public float2 WorldOffset;
    public float SearchRadius;
    
    void Execute(Entity entity, ref CombatTarget combatTarget, in LocalTransform transform, 
                 in Faction faction, in PlayerUnit playerTag)
    {
        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        int2 myCell = SpatialHashHelper.WorldToCell(myPos, CellSize, WorldOffset);
        
        Entity bestTarget = Entity.Null;
        float bestDistance = float.MaxValue;
        
        int cellRadius = (int)math.ceil(SearchRadius / CellSize);
        
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                int2 cell = myCell + new int2(x, y);
                int hash = SpatialHashHelper.GetCellHash(cell);
                
                if (SpatialHashMap.TryGetFirstValue(hash, out Entity otherEntity, out var iterator))
                {
                    do
                    {
                        if (otherEntity == entity)
                            continue;
                        
                        if (!FactionLookup.HasComponent(otherEntity))
                            continue;
                        
                        var otherFaction = FactionLookup[otherEntity];
                        if (otherFaction.Value == faction.Value)
                            continue;
                        
                        if (HealthLookup.HasComponent(otherEntity))
                        {
                            var health = HealthLookup[otherEntity];
                            if (health.IsDead)
                                continue;
                        }
                        
                        if (!TransformLookup.HasComponent(otherEntity))
                            continue;
                        
                        var otherTransform = TransformLookup[otherEntity];
                        float2 otherPos = new float2(otherTransform.Position.x, otherTransform.Position.y);
                        float distance = math.distance(myPos, otherPos);
                        
                        if (distance <= SearchRadius && distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestTarget = otherEntity;
                        }
                        
                    } while (SpatialHashMap.TryGetNextValue(out otherEntity, ref iterator));
                }
            }
        }
        
        combatTarget.Target = bestTarget;
    }
}
