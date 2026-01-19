using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// Component to track noise events that alert zombies
/// </summary>
public struct NoiseEvent : IComponentData
{
    public float2 Position;
    public float Radius;
    public float TimeRemaining;
}

/// <summary>
/// Buffer to store multiple noise events
/// </summary>
[InternalBufferCapacity(16)]
public struct NoiseEventBuffer : IBufferElementData
{
    public float2 Position;
    public float Radius;
    public float TimeRemaining;
}

/// <summary>
/// Singleton to hold noise events that can be accessed by other systems
/// </summary>
public static class NoiseEventManager
{
    // Queue of pending noise events to process
    public static NativeQueue<NoiseEvent> PendingNoises;
    
    public static bool IsCreated => PendingNoises.IsCreated;
    
    public static void Initialize()
    {
        if (!PendingNoises.IsCreated)
        {
            PendingNoises = new NativeQueue<NoiseEvent>(Allocator.Persistent);
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
    /// Create a noise event at a position with a radius (main thread only)
    /// </summary>
    public static void CreateNoise(float2 position, float radius)
    {
        if (!PendingNoises.IsCreated)
            Initialize();

        PendingNoises.Enqueue(new NoiseEvent
        {
            Position = position,
            Radius = radius,
            TimeRemaining = 0.1f // Short duration
        });
    }

    /// <summary>
    /// Get a parallel writer for enqueueing noise events from Burst jobs
    /// </summary>
    public static NativeQueue<NoiseEvent>.ParallelWriter GetParallelWriter()
    {
        if (!PendingNoises.IsCreated)
            Initialize();
        return PendingNoises.AsParallelWriter();
    }
}
