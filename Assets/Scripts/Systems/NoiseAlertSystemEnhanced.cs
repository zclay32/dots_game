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

    // Enable to get detailed debug output from the noise system
    public static bool EnableDebug = true;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialHashConfig>();
        NoiseDebugEventQueue.Initialize();
    }

    public void OnDestroy(ref SystemState state)
    {
        NoiseEventManagerEnhanced.Dispose();
        NoiseDebugEventQueue.Dispose();
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

        // Debug: Log noise events being processed
        for (int i = 0; i < noises.Length; i++)
        {
            UnityEngine.Debug.Log($"[NoiseAlert] Processing noise at ({noises[i].Position.x:F1}, {noises[i].Position.y:F1}), radius: {noises[i].MaxRadius:F1}, intensity: {noises[i].Intensity:F1}, falloff: {noises[i].FalloffExponent:F1}");

            // Track for debug system
            if (NoiseDebugSystem.Instance != null)
            {
                NoiseDebugSystem.Instance.TrackNoise(noises[i].Position, noises[i].MaxRadius);
            }
        }

        // Get spatial hash config
        var spatialConfig = SystemAPI.GetSingleton<SpatialHashConfig>();

        // Get current time for random seed
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        // Get debug writer once before the loop
        var debugWriter = NoiseDebugEventQueue.GetParallelWriter();

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
                ElapsedTime = elapsedTime,
                DebugWriter = debugWriter,
                EnableDebug = EnableDebug
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

    // Debug output
    public NativeQueue<NoiseDebugEvent>.ParallelWriter DebugWriter;
    public bool EnableDebug;

    void Execute(
        ref ZombieCombatState combatState,
        in Unity.Transforms.LocalTransform transform,
        in NoiseSensitivity noiseSensitivity,
        in EnemyUnit zombieTag,
        [EntityIndexInQuery] int entityIndex)
    {
        float2 zombiePos = transform.Position.xy;

        // Early exit: Only idle or wandering zombies react to noise
        if (combatState.State != ZombieCombatAIState.Idle && combatState.State != ZombieCombatAIState.Wandering)
        {
            // Don't log this - too many zombies might be in other states
            return;
        }

        // Early exit: Check if zombie is in spatial hash range (approximate check)
        float2 cellToNoise = zombiePos - NoiseEvent.Position;
        if (math.abs(cellToNoise.x) > NoiseEvent.MaxRadius || math.abs(cellToNoise.y) > NoiseEvent.MaxRadius)
            return;

        // Calculate distance to noise
        float distance = math.distance(zombiePos, NoiseEvent.Position);

        // Early exit: Beyond max radius
        if (distance >= NoiseEvent.MaxRadius)
            return;

        // Zombie is in range! Log this
        if (EnableDebug)
        {
            DebugWriter.Enqueue(new NoiseDebugEvent
            {
                ZombiePos = zombiePos,
                NoisePos = NoiseEvent.Position,
                Reason = NoiseDebugReason.ZombieInRange,
                Distance = distance,
                Probability = 0,
                RandomValue = 0
            });
        }

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
        float roll = random.NextFloat();

        // Log probability roll
        if (EnableDebug)
        {
            DebugWriter.Enqueue(new NoiseDebugEvent
            {
                ZombiePos = zombiePos,
                NoisePos = NoiseEvent.Position,
                Reason = NoiseDebugReason.ProbabilityRoll,
                Distance = distance,
                Probability = finalProbability,
                RandomValue = roll
            });
        }

        // Roll the dice - activate if random value is below probability
        if (roll < finalProbability)
        {
            // Second roll: Should the zombie chase (fast) or wander (slow) toward the noise?
            uint aggroSeed = seed + 1000000;
            var aggroRandom = Random.CreateFromIndex(aggroSeed);
            float aggroRoll = aggroRandom.NextFloat();

            bool shouldChase = aggroRoll < noiseSensitivity.AggroProbability;

            if (shouldChase)
            {
                // Chase toward noise position at full speed
                // Zombie will go idle when it arrives if no target is found
                combatState.State = ZombieCombatAIState.Chasing;
                combatState.StateTimer = 15f;  // Chase toward noise for up to 15 seconds
                combatState.WanderTarget = NoiseEvent.Position;  // Store destination in WanderTarget
                combatState.CurrentTarget = Entity.Null;  // No specific entity target
                combatState.HasTarget = false;
                combatState.HasEngagedTarget = false;
            }
            else
            {
                // Investigate - zombie wanders toward sound at slow speed
                combatState.State = ZombieCombatAIState.Wandering;
                combatState.StateTimer = 8f;  // Investigate for 8 seconds
                combatState.WanderTarget = NoiseEvent.Position;
            }

            if (EnableDebug)
            {
                DebugWriter.Enqueue(new NoiseDebugEvent
                {
                    ZombiePos = zombiePos,
                    NoisePos = NoiseEvent.Position,
                    Reason = NoiseDebugReason.Activated,
                    Distance = distance,
                    Probability = finalProbability,
                    RandomValue = aggroRoll
                });
            }
        }
    }
}
