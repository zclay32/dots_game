using Unity.Collections;
using Unity.Entities;

public enum SoldierAttackFailReason : byte
{
    None = 0,
    NoTarget,
    OnCooldown,
    Moving,
    TargetNoTransform,
    TargetNoHealth,
    TargetDead,
    OutOfRange,
    NotFacing,
    AttackSuccess
}

public struct SoldierCombatDebugEvent
{
    public Entity Soldier;
    public Entity Target;
    public SoldierAttackFailReason Reason;
    public float Value1;  // Context-dependent (e.g., distance, cooldown, dot product)
    public float Value2;  // Context-dependent (e.g., range, threshold)
}

public static class CombatDebugEventQueue
{
    private static NativeQueue<SoldierCombatDebugEvent> _queue;

    public static bool IsCreated => _queue.IsCreated;

    public static void Initialize()
    {
        if (_queue.IsCreated)
            return;
        _queue = new NativeQueue<SoldierCombatDebugEvent>(Allocator.Persistent);
    }

    public static void Dispose()
    {
        if (!_queue.IsCreated)
            return;
        _queue.Dispose();
    }

    public static NativeQueue<SoldierCombatDebugEvent>.ParallelWriter GetParallelWriter()
    {
        if (!_queue.IsCreated)
            Initialize();
        return _queue.AsParallelWriter();
    }

    public static bool TryDequeue(out SoldierCombatDebugEvent evt)
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
