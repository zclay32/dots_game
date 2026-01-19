using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Lightweight separation for zombies to prevent stacking when attacking.
/// Runs every 3 frames for performance.
/// Reduced separation when zombies are in attack range to avoid pushing them out.
///
/// TWO-PASS PARALLEL APPROACH:
/// Pass 1: ComputeSeparationJob - reads neighbor positions, writes to SeparationForce (parallel)
/// Pass 2: ApplySeparationJob - reads SeparationForce, writes to LocalTransform (parallel)
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

        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        // PASS 1: Compute separation forces (parallel)
        state.Dependency = new ComputeSeparationJob
        {
            SpatialHashMap = hashMap,
            TransformLookup = transformLookup,
            CellSize = config.CellSize,
            WorldOffset = config.WorldOffset,
            DeltaTime = deltaTime,
            RandomSeed = _randomSeed
        }.ScheduleParallel(state.Dependency);

        // PASS 2: Apply separation forces (parallel)
        state.Dependency = new ApplySeparationJob().ScheduleParallel(state.Dependency);
    }
}

/// <summary>
/// Pass 1: Compute separation forces from neighbors.
/// Reads neighbor transforms via ComponentLookup, writes to own SeparationForce component.
/// </summary>
[BurstCompile]
public partial struct ComputeSeparationJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialHashMap;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    public float CellSize;
    public float2 WorldOffset;
    public float DeltaTime;
    public uint RandomSeed;

    private const float SeparationRadius = 0.5f;
    private const float SeparationStrength = 2f;

    void Execute(
        Entity entity,
        ref SeparationForce separationForce,
        in LocalTransform transform,
        in CombatTarget combatTarget,
        in Velocity velocity,
        in EnemyUnit enemyTag,
        in Combat combat,
        [EntityIndexInQuery] int entityIndex)
    {
        // Only separate zombies that are in combat
        if (combatTarget.Target == Entity.Null)
        {
            separationForce.Force = float2.zero;
            return;
        }

        // Reduce separation strength when zombie has stopped (in attack position)
        float effectiveStrength = SeparationStrength;
        if (math.lengthsq(velocity.Value) < 0.01f)
        {
            effectiveStrength = SeparationStrength * 0.3f;
        }

        float2 myPos = new float2(transform.Position.x, transform.Position.y);
        int2 myCell = SpatialHashHelper.WorldToCell(myPos, CellSize, WorldOffset);

        float2 separation = float2.zero;
        int neighborCount = 0;
        float separationRadiusSq = SeparationRadius * SeparationRadius;

        // Check neighboring cells (only immediate neighbors)
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

                    // Get neighbor position
                    if (!TransformLookup.HasComponent(neighbor)) continue;
                    var neighborTransform = TransformLookup[neighbor];
                    float2 neighborPos = new float2(neighborTransform.Position.x, neighborTransform.Position.y);

                    float2 diff = myPos - neighborPos;
                    float distSq = math.lengthsq(diff);

                    // Skip far units (use squared distance to avoid sqrt)
                    if (distSq < 0.0001f || distSq > separationRadiusSq) continue;

                    // Push away from neighbor
                    float dist = math.sqrt(distSq);
                    float strength = 1f - (dist / SeparationRadius);
                    separation += (diff / dist) * strength;
                    neighborCount++;

                } while (SpatialHashMap.TryGetNextValue(out neighbor, ref iterator));
            }
        }

        if (neighborCount > 0)
        {
            // Apply separation with some randomness to break symmetry
            var random = Random.CreateFromIndex(RandomSeed + (uint)entityIndex);
            float2 randomOffset = random.NextFloat2Direction() * 0.1f;

            // Compute final force with effective strength
            separationForce.Force = (separation / neighborCount + randomOffset) * effectiveStrength * DeltaTime;
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
/// </summary>
[BurstCompile]
public partial struct ApplySeparationJob : IJobEntity
{
    void Execute(ref LocalTransform transform, in SeparationForce separationForce, in EnemyUnit enemyTag)
    {
        if (math.lengthsq(separationForce.Force) > 0.0001f)
        {
            transform.Position.x += separationForce.Force.x;
            transform.Position.y += separationForce.Force.y;
        }
    }
}
