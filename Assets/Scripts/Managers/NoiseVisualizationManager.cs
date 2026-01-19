using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Manages noise visualization events for debugging purposes.
/// Captures noise events so they can be rendered as fading circles.
/// </summary>
public static class NoiseVisualizationManager
{
    private static NativeQueue<NoiseVisualizationEvent> PendingVisualizations;

    public static bool IsCreated => PendingVisualizations.IsCreated;

    public struct NoiseVisualizationEvent
    {
        public float2 Position;
        public float MaxRadius;
        public float Intensity;
    }

    public static void Initialize()
    {
        if (!PendingVisualizations.IsCreated)
        {
            PendingVisualizations = new NativeQueue<NoiseVisualizationEvent>(Allocator.Persistent);
        }
    }

    public static void Dispose()
    {
        if (PendingVisualizations.IsCreated)
        {
            PendingVisualizations.Dispose();
        }
    }

    /// <summary>
    /// Queue a noise event for visualization
    /// </summary>
    public static void QueueVisualization(float2 position, float maxRadius, float intensity)
    {
        if (!PendingVisualizations.IsCreated)
            Initialize();

        PendingVisualizations.Enqueue(new NoiseVisualizationEvent
        {
            Position = position,
            MaxRadius = maxRadius,
            Intensity = intensity
        });
    }

    /// <summary>
    /// Try to dequeue a visualization event
    /// </summary>
    public static bool TryDequeue(out NoiseVisualizationEvent viz)
    {
        if (!PendingVisualizations.IsCreated)
        {
            viz = default;
            return false;
        }
        return PendingVisualizations.TryDequeue(out viz);
    }
}
