using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// A damage event queued by combat systems for deferred processing.
/// This allows combat systems to run in parallel without race conditions
/// on Health component writes.
/// </summary>
public struct DamageEvent
{
    /// <summary>
    /// The entity receiving damage
    /// </summary>
    public Entity Target;

    /// <summary>
    /// Amount of damage to apply
    /// </summary>
    public float Damage;

    /// <summary>
    /// Direction the damage came from (for knockback/death effects)
    /// </summary>
    public float2 HitDirection;

    /// <summary>
    /// The entity that dealt the damage (for zombie retaliation targeting)
    /// </summary>
    public Entity Attacker;

    /// <summary>
    /// Whether the attacker is a player unit (soldier) - affects zombie alerting
    /// </summary>
    public bool AttackerIsPlayer;
}

/// <summary>
/// Thread-safe damage event queue for deferred damage application.
///
/// Pattern:
/// - Combat systems (parallel jobs) enqueue damage events via GetParallelWriter()
/// - DamageApplicationSystem (main thread) processes the queue sequentially
///
/// This eliminates race conditions when multiple attackers hit the same target.
/// </summary>
public static class DamageEventQueue
{
    private static NativeQueue<DamageEvent> _queue;

    /// <summary>
    /// Whether the queue has been initialized.
    /// Uses NativeContainer.IsCreated which is Burst-compatible.
    /// </summary>
    public static bool IsCreated => _queue.IsCreated;

    /// <summary>
    /// Initialize the damage event queue
    /// </summary>
    public static void Initialize()
    {
        if (_queue.IsCreated)
            return;

        _queue = new NativeQueue<DamageEvent>(Allocator.Persistent);
    }

    /// <summary>
    /// Dispose the damage event queue
    /// </summary>
    public static void Dispose()
    {
        if (!_queue.IsCreated)
            return;

        _queue.Dispose();
    }

    /// <summary>
    /// Get a parallel writer for enqueueing damage events from jobs.
    /// Thread-safe for concurrent writes.
    /// </summary>
    public static NativeQueue<DamageEvent>.ParallelWriter GetParallelWriter()
    {
        if (!_queue.IsCreated)
            Initialize();

        return _queue.AsParallelWriter();
    }

    /// <summary>
    /// Get the queue for sequential processing.
    /// Only call from main thread after completing all jobs that write to the queue.
    /// </summary>
    public static NativeQueue<DamageEvent> GetQueue()
    {
        if (!_queue.IsCreated)
            Initialize();

        return _queue;
    }

    /// <summary>
    /// Try to dequeue a damage event
    /// </summary>
    public static bool TryDequeue(out DamageEvent evt)
    {
        if (!_queue.IsCreated)
        {
            evt = default;
            return false;
        }

        return _queue.TryDequeue(out evt);
    }

    /// <summary>
    /// Get the current count of queued events
    /// </summary>
    public static int Count => _queue.IsCreated ? _queue.Count : 0;
}
