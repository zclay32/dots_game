using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Enhanced noise event with falloff parameters
/// </summary>
public struct NoiseEventEnhanced
{
    public float2 Position;
    public float MaxRadius;          // Maximum detection radius
    public float Intensity;          // Base intensity (1.0 = normal gunshot)
    public float FalloffExponent;    // Exponential falloff rate (2.0 = quadratic, 3.0 = cubic)

    /// <summary>
    /// Calculate activation probability at a given distance
    /// Uses exponential falloff: probability = intensity * (1 - (distance/maxRadius)^exponent)
    /// </summary>
    public float GetActivationProbability(float distance)
    {
        if (distance >= MaxRadius)
            return 0f;

        // Normalized distance (0 to 1)
        float normalizedDist = distance / MaxRadius;

        // Exponential falloff
        float falloff = 1f - math.pow(normalizedDist, FalloffExponent);

        // Scale by intensity
        return math.clamp(Intensity * falloff, 0f, 1f);
    }
}

/// <summary>
/// COLD: Noise sensitivity configuration per zombie type
/// Allows different zombie types to react differently to noise
/// </summary>
public struct NoiseSensitivity : IComponentData
{
    public float SensitivityMultiplier;  // 1.0 = normal, 2.0 = extra sensitive, 0.5 = deaf
    public float MinActivationProbability;  // Minimum chance to react (even at max range)
    public float MaxActivationProbability;  // Maximum chance to react (caps probability)
}

/// <summary>
/// COLD: Gunshot noise configuration per soldier
/// Controls how loud gunfire is and how far it travels
/// </summary>
public struct GunshotNoiseConfig : IComponentData
{
    public float RangeMultiplier;    // Multiplier on attack range (15x = very loud)
    public float Intensity;          // Base intensity (1.0 = normal, 2.0 = very loud)
    public float FalloffExponent;    // How fast sound fades (1.0 = linear, 2.0 = quadratic)
}

/// <summary>
/// Enhanced noise event manager with probabilistic activation.
/// Uses NativeQueue for thread-safe parallel writes from Burst jobs.
/// </summary>
public static class NoiseEventManagerEnhanced
{
    private static Unity.Collections.NativeQueue<NoiseEventEnhanced> PendingNoises;

    public static bool IsCreated => PendingNoises.IsCreated;

    public static void Initialize()
    {
        if (!PendingNoises.IsCreated)
        {
            PendingNoises = new Unity.Collections.NativeQueue<NoiseEventEnhanced>(
                Unity.Collections.Allocator.Persistent
            );
        }
    }

    public static void Dispose()
    {
        if (PendingNoises.IsCreated)
        {
            PendingNoises.Dispose();
        }
    }

    /// <summary>
    /// Create a noise event with custom parameters (main thread only)
    /// </summary>
    public static void CreateNoise(float2 position, float maxRadius, float intensity = 1.0f, float falloffExponent = 2.0f)
    {
        if (!PendingNoises.IsCreated)
            Initialize();

        PendingNoises.Enqueue(new NoiseEventEnhanced
        {
            Position = position,
            MaxRadius = maxRadius,
            Intensity = intensity,
            FalloffExponent = falloffExponent
        });
    }

    /// <summary>
    /// Get a parallel writer for enqueueing noise events from Burst jobs
    /// </summary>
    public static Unity.Collections.NativeQueue<NoiseEventEnhanced>.ParallelWriter GetParallelWriter()
    {
        if (!PendingNoises.IsCreated)
            Initialize();
        return PendingNoises.AsParallelWriter();
    }

    /// <summary>
    /// Get pending noise events for processing
    /// </summary>
    public static Unity.Collections.NativeArray<NoiseEventEnhanced> GetPendingNoises(Unity.Collections.Allocator allocator)
    {
        if (!PendingNoises.IsCreated || PendingNoises.Count == 0)
            return new Unity.Collections.NativeArray<NoiseEventEnhanced>(0, allocator);

        var result = new Unity.Collections.NativeArray<NoiseEventEnhanced>(PendingNoises.Count, allocator);
        int index = 0;
        while (PendingNoises.TryDequeue(out var noise))
        {
            result[index++] = noise;
        }
        return result;
    }

    /// <summary>
    /// Try to dequeue a single noise event
    /// </summary>
    public static bool TryDequeue(out NoiseEventEnhanced noise)
    {
        if (!PendingNoises.IsCreated)
        {
            noise = default;
            return false;
        }
        return PendingNoises.TryDequeue(out noise);
    }

    /// <summary>
    /// Clear all pending noise events
    /// </summary>
    public static void ClearNoises()
    {
        if (PendingNoises.IsCreated)
        {
            while (PendingNoises.TryDequeue(out _)) { }
        }
    }
}
