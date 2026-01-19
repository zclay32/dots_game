using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

/// <summary>
/// Double-buffered spatial hash map for lock-free parallel access.
///
/// Pattern:
/// - Frame N: Build hash to WriteBuffer, systems read from ReadBuffer (frame N-1 data)
/// - Frame N+1: Swap buffers, repeat
///
/// This eliminates the need for Complete() calls since ReadBuffer is always
/// guaranteed to be complete (it was built last frame).
///
/// Trade-off: 1-frame latency for spatial queries. Acceptable because units
/// don't move far in a single frame.
///
/// IMPORTANT: Systems accessing this static class must NOT have [BurstCompile]
/// on their OnUpdate methods. The parallel jobs themselves can be Burst-compiled.
/// </summary>
public static class SpatialHashDoubleBuffer
{
    private static NativeParallelMultiHashMap<int, Entity> _bufferA;
    private static NativeParallelMultiHashMap<int, Entity> _bufferB;
    private static JobHandle _bufferAJobHandle;
    private static JobHandle _bufferBJobHandle;
    private static bool _writeToA;

    /// <summary>
    /// Whether the double buffer system has been initialized.
    /// </summary>
    public static bool IsCreated => _bufferA.IsCreated && _bufferB.IsCreated;

    /// <summary>
    /// The buffer being read from (contains last frame's data, guaranteed complete)
    /// </summary>
    public static NativeParallelMultiHashMap<int, Entity> ReadBuffer =>
        _writeToA ? _bufferB : _bufferA;

    /// <summary>
    /// The buffer being written to (current frame's data, may be incomplete)
    /// </summary>
    public static NativeParallelMultiHashMap<int, Entity> WriteBuffer =>
        _writeToA ? _bufferA : _bufferB;

    /// <summary>
    /// Job handle for the read buffer (should be default/completed)
    /// </summary>
    public static JobHandle ReadBufferJobHandle =>
        _writeToA ? _bufferBJobHandle : _bufferAJobHandle;

    /// <summary>
    /// Job handle for the write buffer (current frame's build job)
    /// </summary>
    public static JobHandle WriteBufferJobHandle =>
        _writeToA ? _bufferAJobHandle : _bufferBJobHandle;

    /// <summary>
    /// Set the job handle for the current write buffer
    /// </summary>
    public static void SetWriteBufferJobHandle(JobHandle handle)
    {
        if (_writeToA)
            _bufferAJobHandle = handle;
        else
            _bufferBJobHandle = handle;
    }

    /// <summary>
    /// Swap read and write buffers. Call at the start of each frame.
    /// After swapping, ReadBuffer contains the previous frame's completed data.
    /// </summary>
    public static void SwapBuffers()
    {
        // Before swapping, complete the job that was writing to what will become the read buffer
        // This ensures the "new" read buffer is fully built
        if (_writeToA)
            _bufferAJobHandle.Complete();
        else
            _bufferBJobHandle.Complete();

        _writeToA = !_writeToA;
    }

    /// <summary>
    /// Initialize the double buffer system with given capacity
    /// </summary>
    public static void Create(int initialCapacity)
    {
        if (_bufferA.IsCreated && _bufferB.IsCreated)
            return;

        _bufferA = new NativeParallelMultiHashMap<int, Entity>(initialCapacity, Allocator.Persistent);
        _bufferB = new NativeParallelMultiHashMap<int, Entity>(initialCapacity, Allocator.Persistent);
        _bufferAJobHandle = default;
        _bufferBJobHandle = default;
        _writeToA = true;
    }

    /// <summary>
    /// Dispose both buffers
    /// </summary>
    public static void Dispose()
    {
        if (!_bufferA.IsCreated && !_bufferB.IsCreated)
            return;

        // Complete any pending jobs before disposing
        _bufferAJobHandle.Complete();
        _bufferBJobHandle.Complete();

        if (_bufferA.IsCreated)
            _bufferA.Dispose();
        if (_bufferB.IsCreated)
            _bufferB.Dispose();
    }

    /// <summary>
    /// Ensure the write buffer has sufficient capacity
    /// </summary>
    public static void EnsureWriteBufferCapacity(int requiredCapacity)
    {
        if (!IsCreated)
            return;

        if (_writeToA)
        {
            if (_bufferA.Capacity < requiredCapacity)
                _bufferA.Capacity = requiredCapacity;
        }
        else
        {
            if (_bufferB.Capacity < requiredCapacity)
                _bufferB.Capacity = requiredCapacity;
        }
    }

    /// <summary>
    /// Clear the write buffer for a new frame
    /// </summary>
    public static void ClearWriteBuffer()
    {
        if (!IsCreated)
            return;

        if (_writeToA)
            _bufferA.Clear();
        else
            _bufferB.Clear();
    }

    /// <summary>
    /// Get the parallel writer for the write buffer
    /// </summary>
    public static NativeParallelMultiHashMap<int, Entity>.ParallelWriter GetWriteBufferParallelWriter()
    {
        return WriteBuffer.AsParallelWriter();
    }
}
