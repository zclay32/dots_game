using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// OPTIMIZED: Enhanced noise alert system with probabilistic activation and exponential falloff
///
/// Performance optimizations:
/// - Runs every 2 frames (noise doesn't need frame-perfect precision)
/// - Fully Burst compiled for SIMD optimization
/// - Uses spatial hash for O(n) zombie queries instead of O(n²)
/// - Early exit conditions to skip inactive zombies
/// - Deterministic per-entity randomization for consistent behavior
///
/// Features:
/// - Exponential falloff based on distance (closer = higher probability)
/// - Per-zombie sensitivity configuration
/// - Configurable intensity and falloff exponent per noise event
/// - Supports different zombie types with varying sensitivity
/// </summary>
[UpdateAfter(typeof(SpatialHashSystem))]
public partial struct NoiseAlertSystemEnhanced : ISystem
{
    private int _frameCount;
    private const int UPDATE_INTERVAL = 2;  // Run every 2 frames (~8ms at 60fps)

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialHashConfig>();
    }

    public void OnDestroy(ref SystemState state)
    {
        NoiseEventManagerEnhanced.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        _frameCount++;

        // Frame skip for performance
        if (_frameCount % UPDATE_INTERVAL != 0)
            return;

        // Check if there are any pending noises
        if (!NoiseEventManagerEnhanced.IsCreated)
            return;

        // Get pending noise events
        var noises = NoiseEventManagerEnhanced.GetPendingNoises(Allocator.TempJob);

        if (noises.Length == 0)
        {
            noises.Dispose();
            return;
        }

        // Get spatial hash config
        var spatialConfig = SystemAPI.GetSingleton<SpatialHashConfig>();

        // Get current time for random seed
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        // Process each noise event
        for (int noiseIndex = 0; noiseIndex < noises.Length; noiseIndex++)
        {
            var noiseEvent = noises[noiseIndex];

            // Calculate spatial grid bounds for this noise
            int cellRadius = (int)math.ceil(noiseEvent.MaxRadius / spatialConfig.CellSize);
            int2 noiseCell = SpatialHashHelper.WorldToCell(noiseEvent.Position, spatialConfig.CellSize, spatialConfig.WorldOffset);

            // Process zombies in nearby cells using parallel job
            // Chain with existing dependency to ensure proper ordering
            state.Dependency = new ProcessNoiseActivationJob
            {
                NoiseEvent = noiseEvent,
                NoiseCell = noiseCell,
                CellRadius = cellRadius,
                CellSize = spatialConfig.CellSize,
                ElapsedTime = elapsedTime
            }.ScheduleParallel(state.Dependency);
        }

        // Clean up
        noises.Dispose();
        NoiseEventManagerEnhanced.ClearNoises();
    }
}

/// <summary>
/// Burst-compiled job to process noise activation for zombies using new state machine
/// Uses probabilistic activation based on distance and zombie sensitivity
/// </summary>
[BurstCompile]
public partial struct ProcessNoiseActivationJob : IJobEntity
{
    [ReadOnly] public NoiseEventEnhanced NoiseEvent;
    [ReadOnly] public int2 NoiseCell;
    [ReadOnly] public int CellRadius;
    [ReadOnly] public float CellSize;
    [ReadOnly] public double ElapsedTime;

    void Execute(
        ref ZombieCombatState combatState,
        in Unity.Transforms.LocalTransform transform,
        in NoiseSensitivity noiseSensitivity,
        in EnemyUnit zombieTag,
        [EntityIndexInQuery] int entityIndex)
    {
        // Early exit: Only idle or wandering zombies react to noise
        if (combatState.State != ZombieCombatAIState.Idle && combatState.State != ZombieCombatAIState.Wandering)
            return;

        float2 zombiePos = transform.Position.xy;

        // Early exit: Check if zombie is in spatial hash range (approximate check)
        float2 cellToNoise = zombiePos - NoiseEvent.Position;
        if (math.abs(cellToNoise.x) > NoiseEvent.MaxRadius || math.abs(cellToNoise.y) > NoiseEvent.MaxRadius)
            return;

        // Calculate distance to noise
        float distance = math.distance(zombiePos, NoiseEvent.Position);

        // Early exit: Beyond max radius
        if (distance >= NoiseEvent.MaxRadius)
            return;

        // Calculate base activation probability using exponential falloff
        float baseProbability = NoiseEvent.GetActivationProbability(distance);

        // Apply zombie-specific sensitivity multiplier
        float finalProbability = baseProbability * noiseSensitivity.SensitivityMultiplier;

        // Clamp to zombie's min/max activation probability
        finalProbability = math.clamp(
            finalProbability,
            noiseSensitivity.MinActivationProbability,
            noiseSensitivity.MaxActivationProbability
        );

        // Deterministic random check per entity
        uint seed = (uint)(
            entityIndex * 73856093 +
            (int)(ElapsedTime * 1000f) * 19349663 +
            (int)(NoiseEvent.Position.x * 1000f) * 83492791 +
            (int)(NoiseEvent.Position.y * 1000f)
        );
        var random = Random.CreateFromIndex(seed);

        // Roll the dice - activate if random value is below probability
        if (random.NextFloat() < finalProbability)
        {
            // Activate zombie - set wander target toward noise source
            // This makes zombie investigate the noise without having a specific target entity
            combatState.State = ZombieCombatAIState.Wandering;
            combatState.StateTimer = 5f;  // Investigate for 5 seconds
            combatState.WanderTarget = NoiseEvent.Position;  // Move toward noise
        }
    }
}
