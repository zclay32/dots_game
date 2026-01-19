using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// Static manager to track muzzle flash events from soldier attacks
/// </summary>
public static class MuzzleFlashManager
{
    public struct FlashEvent
    {
        public float2 Position;
        public float2 Direction;
    }
    
    public static NativeQueue<FlashEvent> PendingFlashes;
    
    public static bool IsCreated => PendingFlashes.IsCreated;
    
    public static void Initialize()
    {
        if (!PendingFlashes.IsCreated)
        {
            PendingFlashes = new NativeQueue<FlashEvent>(Allocator.Persistent);
        }
    }
    
    public static void Dispose()
    {
        if (PendingFlashes.IsCreated)
        {
            PendingFlashes.Dispose();
        }
    }
    
    /// <summary>
    /// Create a muzzle flash event (main thread only)
    /// </summary>
    public static void CreateFlash(float2 position, float2 direction)
    {
        if (!PendingFlashes.IsCreated)
            Initialize();

        PendingFlashes.Enqueue(new FlashEvent
        {
            Position = position,
            Direction = direction
        });
    }

    /// <summary>
    /// Get a parallel writer for enqueueing flash events from Burst jobs
    /// </summary>
    public static NativeQueue<FlashEvent>.ParallelWriter GetParallelWriter()
    {
        if (!PendingFlashes.IsCreated)
            Initialize();
        return PendingFlashes.AsParallelWriter();
    }
}
