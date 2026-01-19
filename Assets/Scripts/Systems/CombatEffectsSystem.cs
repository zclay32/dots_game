using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that creates combat effects (noise, muzzle flash) for soldier attacks.
/// Runs after DamageApplicationSystem to ensure effects only occur for successful attacks.
///
/// Note: This runs on the main thread because noise and muzzle flash managers
/// use managed code that can't run in Burst jobs. This is acceptable because
/// it only processes entities that successfully attacked this frame (a small subset).
/// </summary>
[UpdateAfter(typeof(DamageApplicationSystem))]
[UpdateBefore(typeof(DeathSystem))]
public partial struct CombatEffectsSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialHashConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Ensure managers are ready
        if (!NoiseEventManagerEnhanced.IsCreated)
            NoiseEventManagerEnhanced.Initialize();
        if (!MuzzleFlashManager.IsCreated)
            MuzzleFlashManager.Initialize();

        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        // Process soldiers that just attacked (cooldown was just set)
        // We identify "just attacked" by cooldown being near the max value
        foreach (var (combat, noiseConfig, target, transform) in
            SystemAPI.Query<RefRO<Combat>, RefRO<GunshotNoiseConfig>, RefRO<CombatTarget>, RefRO<LocalTransform>>()
                .WithAll<PlayerUnit>())
        {
            // Check if this soldier just attacked (cooldown was just reset)
            // A freshly reset cooldown will be within deltaTime of the max cooldown
            float deltaTime = SystemAPI.Time.DeltaTime;
            bool justAttacked = combat.ValueRO.CurrentCooldown > (combat.ValueRO.AttackCooldown - deltaTime * 2f) &&
                               combat.ValueRO.CurrentCooldown <= combat.ValueRO.AttackCooldown;

            if (!justAttacked)
                continue;

            if (target.ValueRO.Target == Entity.Null)
                continue;

            // Get target position for direction calculation
            if (!transformLookup.HasComponent(target.ValueRO.Target))
                continue;

            var targetTransform = transformLookup[target.ValueRO.Target];

            float2 myPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);
            float2 targetPos = new float2(targetTransform.Position.x, targetTransform.Position.y);
            float2 toTarget = math.normalizesafe(targetPos - myPos);

            // Create noise events using per-soldier noise configuration
            float noiseRadius = combat.ValueRO.AttackRange * noiseConfig.ValueRO.RangeMultiplier;
            float intensity = noiseConfig.ValueRO.Intensity;
            float falloff = noiseConfig.ValueRO.FalloffExponent;

            NoiseEventManagerEnhanced.CreateNoise(myPos, noiseRadius, intensity, falloff);

            // Queue visualization for debugging
            NoiseVisualizationManager.QueueVisualization(myPos, noiseRadius, intensity);

            // Create muzzle flash
            MuzzleFlashManager.CreateFlash(myPos, toTarget);
        }
    }
}
