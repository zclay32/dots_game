using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Lightweight separation for units to prevent stacking.
/// - Zombies: Only separate when in combat, reduced strength when attacking
/// - Soldiers: Always separate from other soldiers
///
/// Runs every 3 frames for performance.
///
/// TWO-PASS PARALLEL APPROACH:
/// Pass 1: ComputeSeparationJob - computes forces for all units with SeparationForce
/// Pass 2: ApplySeparationJob - applies forces to LocalTransform
/// This avoids the aliasing issue where we can't read neighbor transforms while writing own transform.
///
/// Uses double-buffered spatial hash (ReadBuffer) - no sync points needed.
/// </summary>
[UpdateAfter(typeof(SpatialHashSystem))]
[UpdateBefore(typeof(UnitMovementSystem))]
public partial struct UnitSeparationSystem : ISystem
{
    private uint _randomSeed;
    private int _frameCount;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialHashConfig>();
        _randomSeed = 12345;
    }

    public void OnUpdate(ref SystemState state)
    {
        // Only run every 3 frames
        _frameCount++;
        if (_frameCount % 3 != 0)
            return;

        // Use double-buffered spatial hash - ReadBuffer has last frame's completed data
        if (!SpatialHashDoubleBuffer.IsCreated) return;

        var hashMap = SpatialHashDoubleBuffer.ReadBuffer;
        if (!hashMap.IsCreated) return;

        _randomSeed += 1;
        float deltaTime = SystemAPI.Time.DeltaTime * 3f; // Compensate for frame skip
        var config = SystemAPI.GetSingleton<SpatialHashConfig>();

        // PASS 1: Compute separation forces for all units (parallel)
        state.Dependency = new ComputeSeparationJob
        {
            SpatialHashMap = hashMap,
            TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            PlayerUnitLookup = SystemAPI.GetComponentLookup<PlayerUnit>(true),
            EnemyUnitLookup = SystemAPI.GetComponentLookup<EnemyUnit>(true),
            CombatTargetLookup = SystemAPI.GetComponentLookup<CombatTarget>(true),
            VelocityLookup = SystemAPI.GetComponentLookup<Velocity>(true),
            CellSize = config.CellSize,
            WorldOffset = config.WorldOffset,
            DeltaTime = deltaTime,
            RandomSeed = _randomSeed
        }.ScheduleParallel(state.Dependency);

        // PASS 2: Apply separation forces to all units (parallel)
        // Pass walkable grid to prevent pushing units into obstacles
        bool flowFieldReady = FlowFieldData.IsCreated;
        state.Dependency = new ApplySeparationJob
        {
            FlowFieldReady = flowFieldReady,
            GridWidth = flowFieldReady ? FlowFieldData.GridWidth : 0,
            GridHeight = flowFieldReady ? FlowFieldData.GridHeight : 0,
            CellSize = flowFieldReady ? FlowFieldData.CellSize : 1f,
            WorldOffset = flowFieldReady ? FlowFieldData.WorldOffset : float2.zero,
            Walkable = flowFieldReady ? FlowFieldData.Walkable : default
        }.ScheduleParallel(state.Dependency);
    }
}

/// <summary>
/// Unified separation job that handles both soldiers and zombies.
/// Uses component lookups to determine unit type and apply appropriate behavior.
/// </summary>
[BurstCompile]
public partial struct ComputeSeparationJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialHashMap;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<PlayerUnit> PlayerUnitLookup;
    [ReadOnly] public ComponentLookup<EnemyUnit> EnemyUnitLookup;
    [ReadOnly] public ComponentLookup<CombatTarget> CombatTargetLookup;
    [ReadOnly] public ComponentLookup<Velocity> VelocityLookup;
    public float CellSize;
    public float2 WorldOffset;
    public float DeltaTime;
    public uint RandomSeed;

    // Soldier settings
    private const float SoldierSeparationRadius = 0.6f;
    private const float SoldierSeparationStrength = 3f;
    private const float SoldierRandomOffset = 0.05f;

    // Zombie settings
    private const float ZombieSeparationRadius = 0.5f;
    private const float ZombieSeparationStrength = 2f;
    private const float ZombieStoppedStrengthMultiplier = 0.3f;
    private const float ZombieRandomOffset = 0.1f;

    void Execute(
        Entity entity,
        ref SeparationForce separationForce,
        in LocalTransform transform,
        [EntityIndexInQuery] int entityIndex)
    {
        bool isSoldier = PlayerUnitLookup.HasComponent(entity);
        bool isZombie = EnemyUnitLookup.HasComponent(entity);

        // Determine separation parameters based on unit type
        float separationRadius;
        float separationStrength;
        float randomOffsetScale;
        bool onlySeparateFromSameType;

        if (isSoldier)
        {
            separationRadius = SoldierSeparationRadius;
            separationStrength = SoldierSeparationStrength;
            randomOffsetScale = SoldierRandomOffset;
            onlySeparateFromSameType = true; // Soldiers only separate from soldiers
        }
        else if (isZombie)
        {
            // Zombies only separate when in combat
            if (!CombatTargetLookup.HasComponent(entity))
            {
                separationForce.Force = float2.zero;
                return;
            }
            var combatTarget = CombatTargetLookup[entity];
            if (combatTarget.Target == Entity.Null)
            {
                separationForce.Force = float2.zero;
                return;
            }

            separationRadius = ZombieSeparationRadius;
            separationStrength = ZombieSeparationStrength;
            randomOffsetScale = ZombieRandomOffset;
            onlySeparateFromSameType = false; // Zombies separate from all neighbors

            // Reduce strength when stopped (in attack position)
            if (VelocityLookup.HasComponent(entity))
            {
                var velocity = VelocityLookup[entity];
                if (math.lengthsq(velocity.Value) < 0.01f)
                {
                    separationStrength *= ZombieStoppedStrengthMultiplier;
                }
            }
        }
        else
        {
            // Unknown unit type - skip
            separationForce.Force = float2.zero;
            return;
        }

        // Compute separation force
        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        int2 myCell = SpatialHashHelper.WorldToCell(myPos, CellSize, WorldOffset);

        float2 separation = float2.zero;
        int neighborCount = 0;
        float separationRadiusSq = separationRadius * separationRadius;

        // Check neighboring cells
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int2 cell = myCell + new int2(dx, dy);
                int hash = SpatialHashHelper.GetCellHash(cell);

                if (!SpatialHashMap.TryGetFirstValue(hash, out Entity neighbor, out var iterator))
                    continue;

                do
                {
                    // Skip self
                    if (neighbor == entity) continue;

                    // Filter by unit type if needed
                    if (onlySeparateFromSameType)
                    {
                        if (isSoldier && !PlayerUnitLookup.HasComponent(neighbor)) continue;
                    }

                    // Get neighbor position
                    if (!TransformLookup.HasComponent(neighbor)) continue;
                    var neighborTransform = TransformLookup[neighbor];
                    float2 neighborPos = new float2(neighborTransform.Position.x, neighborTransform.Position.y);

                    float2 diff = myPos - neighborPos;
                    float distSq = math.lengthsq(diff);

                    // Skip far units
                    if (distSq < 0.0001f || distSq > separationRadiusSq) continue;

                    // Push away from neighbor
                    float dist = math.sqrt(distSq);
                    float strength = 1f - (dist / separationRadius);
                    separation += (diff / dist) * strength;
                    neighborCount++;

                } while (SpatialHashMap.TryGetNextValue(out neighbor, ref iterator));
            }
        }

        if (neighborCount > 0)
        {
            // Apply separation with some randomness to break symmetry
            var random = Random.CreateFromIndex(RandomSeed + (uint)entityIndex);
            float2 randomOffset = random.NextFloat2Direction() * randomOffsetScale;

            separationForce.Force = (separation / neighborCount + randomOffset) * separationStrength * DeltaTime;
        }
        else
        {
            separationForce.Force = float2.zero;
        }
    }
}

/// <summary>
/// Pass 2: Apply separation forces to transforms.
/// Only reads SeparationForce, only writes to LocalTransform - no aliasing.
/// Checks walkability to avoid pushing units into obstacles.
/// </summary>
[BurstCompile]
public partial struct ApplySeparationJob : IJobEntity
{
    public bool FlowFieldReady;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
    public float2 WorldOffset;
    [ReadOnly] public NativeArray<bool> Walkable;

    void Execute(ref LocalTransform transform, in SeparationForce separationForce)
    {
        if (math.lengthsq(separationForce.Force) < 0.0001f)
            return;

        float2 currentPos = new float2(transform.Position.x, transform.Position.y);
        float2 newPos = currentPos + separationForce.Force;

        // Check if new position is walkable before applying
        if (!FlowFieldReady || !Walkable.IsCreated || IsWalkable(newPos))
        {
            transform.Position.x = newPos.x;
            transform.Position.y = newPos.y;
        }
        else
        {
            // Try sliding along X only
            float2 slideX = new float2(currentPos.x + separationForce.Force.x, currentPos.y);
            if (IsWalkable(slideX))
            {
                transform.Position.x = slideX.x;
            }
            else
            {
                // Try sliding along Y only
                float2 slideY = new float2(currentPos.x, currentPos.y + separationForce.Force.y);
                if (IsWalkable(slideY))
                {
                    transform.Position.y = slideY.y;
                }
                // If neither works, don't apply separation (stay in place)
            }
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
}
