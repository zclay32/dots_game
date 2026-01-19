using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that executes zombie attacks when in Attacking state.
/// Deals damage to entities in front of the zombie within attack cone.
/// Transitions to Cooldown state after dealing damage.
///
/// PARALLEL: Uses deferred damage queue to eliminate race conditions.
/// Uses double-buffered spatial hash (ReadBuffer) - no sync point needed.
/// </summary>
[UpdateAfter(typeof(ZombieStateMachineSystem))]
[UpdateBefore(typeof(DamageApplicationSystem))]
public partial struct ZombieCombatExecutionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialHashConfig>();
    }

    // Note: Not Burst-compiled because it accesses static SpatialHashDoubleBuffer
    // Heavy work is done in the Burst-compiled ZombieConeAttackJob
    public void OnUpdate(ref SystemState state)
    {
        // Use ReadBuffer from double-buffered spatial hash (1-frame-old data, guaranteed complete)
        if (!SpatialHashDoubleBuffer.IsCreated) return;
        var hashMap = SpatialHashDoubleBuffer.ReadBuffer;
        if (!hashMap.IsCreated) return;

        var config = SystemAPI.GetSingleton<SpatialHashConfig>();

        // Ensure damage queue is ready
        if (!DamageEventQueue.IsCreated)
            DamageEventQueue.Initialize();

        // Get read-only lookups for parallel access
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var healthLookup = SystemAPI.GetComponentLookup<Health>(true);
        var factionLookup = SystemAPI.GetComponentLookup<Faction>(true);

        // Schedule parallel cone attack job
        var coneAttackJob = new ZombieConeAttackJob
        {
            SpatialHashMap = hashMap,
            TransformLookup = transformLookup,
            HealthLookup = healthLookup,
            FactionLookup = factionLookup,
            DamageQueue = DamageEventQueue.GetParallelWriter(),
            CellSize = config.CellSize,
            WorldOffset = config.WorldOffset
        };

        state.Dependency = coneAttackJob.ScheduleParallel(state.Dependency);
    }
}

/// <summary>
/// Parallel job for zombie cone attacks.
/// Uses spatial hash to find targets in attack cone, queues damage events.
/// </summary>
[BurstCompile]
public partial struct ZombieConeAttackJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialHashMap;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<Health> HealthLookup;
    [ReadOnly] public ComponentLookup<Faction> FactionLookup;
    public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;
    public float CellSize;
    public float2 WorldOffset;

    void Execute(
        ref ZombieCombatState combatState,
        in ZombieCombatConfig combatConfig,
        in LocalTransform transform,
        in EnemyUnit enemyTag,
        Entity entity)
    {
        // Only process zombies in Attacking state
        if (combatState.State != ZombieCombatAIState.Attacking)
            return;

        float2 myPos = new float2(transform.Position.x, transform.Position.y);

        // Get facing direction
        float3 forward3 = math.mul(transform.Rotation, new float3(0, 1, 0));
        float2 facing = math.normalizesafe(new float2(forward3.x, forward3.y));

        // Pre-calculate attack parameters
        float attackRange = combatConfig.AttackRange;
        float attackRangeSq = attackRange * attackRange;
        float coneAngleRad = math.radians(combatConfig.AttackConeAngle);
        float coneDot = math.cos(coneAngleRad * 0.5f);
        float attackDamage = combatConfig.AttackDamage;

        // Find and damage all targets in cone using spatial hash
        int2 myCell = SpatialHashHelper.WorldToCell(myPos, CellSize, WorldOffset);
        int cellRadius = (int)math.ceil(attackRange / CellSize);

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
                    // Skip self
                    if (other == entity) continue;

                    // Skip non-player units (cheapest check first after self)
                    if (!FactionLookup.HasComponent(other)) continue;
                    if (FactionLookup[other].Value != FactionType.Player) continue;

                    // Get transform for distance check
                    if (!TransformLookup.HasComponent(other)) continue;
                    var otherTransform = TransformLookup[other];
                    float2 otherPos = new float2(otherTransform.Position.x, otherTransform.Position.y);

                    // Distance check using squared distance (avoid sqrt)
                    float distanceSq = math.distancesq(myPos, otherPos);
                    if (distanceSq > attackRangeSq) continue;

                    // Check if in cone
                    float2 toTarget = math.normalizesafe(otherPos - myPos);
                    float dot = math.dot(facing, toTarget);
                    if (dot < coneDot) continue;

                    // Skip dead units (check after geometric tests since it requires lookup)
                    if (!HealthLookup.HasComponent(other)) continue;
                    var health = HealthLookup[other];
                    if (health.IsDead) continue;

                    // Target is in attack cone - queue damage event!
                    DamageQueue.Enqueue(new DamageEvent
                    {
                        Target = other,
                        Damage = attackDamage,
                        HitDirection = toTarget,
                        Attacker = entity,
                        AttackerIsPlayer = false
                    });

                } while (SpatialHashMap.TryGetNextValue(out other, ref iterator));
            }
        }

        // Transition to Cooldown state
        combatState.State = ZombieCombatAIState.Cooldown;
        combatState.StateTimer = combatConfig.AttackCooldown;
    }
}
