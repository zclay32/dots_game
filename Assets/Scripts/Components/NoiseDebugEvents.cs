using Unity.Collections;
using Unity.Mathematics;

public enum NoiseDebugReason : byte
{
    ZombieInRange,
    ZombieNotIdle,
    ZombieTooFar,
    ProbabilityRoll,
    Activated
}

public struct NoiseDebugEvent
{
    public float2 ZombiePos;
    public float2 NoisePos;
    public NoiseDebugReason Reason;
    public float Distance;
    public float Probability;
    public float RandomValue;
}

public static class NoiseDebugEventQueue
{
    private static NativeQueue<NoiseDebugEvent> _queue;

    public static bool IsCreated => _queue.IsCreated;

    public static void Initialize()
    {
        if (_queue.IsCreated)
            return;
        _queue = new NativeQueue<NoiseDebugEvent>(Allocator.Persistent);
    }

    public static void Dispose()
    {
        if (!_queue.IsCreated)
            return;
        _queue.Dispose();
    }

    public static NativeQueue<NoiseDebugEvent>.ParallelWriter GetParallelWriter()
    {
        if (!_queue.IsCreated)
            Initialize();
        return _queue.AsParallelWriter();
    }

    public static bool TryDequeue(out NoiseDebugEvent evt)
    {
        if (!_queue.IsCreated)
        {
            evt = default;
            return false;
        }
        return _queue.TryDequeue(out evt);
    }

    public static int Count => _queue.IsCreated ? _queue.Count : 0;
}
