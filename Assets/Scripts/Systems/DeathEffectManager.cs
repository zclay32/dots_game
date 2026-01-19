using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// Static manager to track death effect events
/// </summary>
public static class DeathEffectManager
{
    public struct DeathEvent
    {
        public float2 Position;
        public float2 ImpactDirection;  // Direction the killing blow came from (for knockback/particles)
        public bool IsEnemy; // true = zombie (red), false = soldier (blue)
    }
    
    public static NativeQueue<DeathEvent> PendingDeaths;
    
    public static bool IsCreated => PendingDeaths.IsCreated;
    
    public static void Initialize()
    {
        if (!PendingDeaths.IsCreated)
        {
            PendingDeaths = new NativeQueue<DeathEvent>(Allocator.Persistent);
        }
    }
    
    public static void Dispose()
    {
        if (PendingDeaths.IsCreated)
        {
            PendingDeaths.Dispose();
        }
    }
    
    /// <summary>
    /// Create a death effect event (main thread only)
    /// </summary>
    public static void CreateDeathEffect(float2 position, bool isEnemy, float2 impactDirection = default)
    {
        if (!PendingDeaths.IsCreated)
            Initialize();

        PendingDeaths.Enqueue(new DeathEvent
        {
            Position = position,
            ImpactDirection = impactDirection,
            IsEnemy = isEnemy
        });
    }

    /// <summary>
    /// Get a parallel writer for enqueueing death events from Burst jobs
    /// </summary>
    public static NativeQueue<DeathEvent>.ParallelWriter GetParallelWriter()
    {
        if (!PendingDeaths.IsCreated)
            Initialize();
        return PendingDeaths.AsParallelWriter();
    }
}
