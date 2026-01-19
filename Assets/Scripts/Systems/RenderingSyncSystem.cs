using Unity.Entities;

/// <summary>
/// System that provides the ONLY sync point before rendering.
/// All game logic systems chain their jobs via state.Dependency without calling Complete().
/// This system runs at the start of PresentationSystemGroup and completes all pending jobs.
///
/// This architecture means:
/// - Main thread is free during SimulationSystemGroup (all work is parallel)
/// - Single sync point right before rendering needs the final state
/// - Renderers in PresentationSystemGroup can safely read all entity data
///
/// Performance benefit: Main thread doesn't stall multiple times per frame,
/// only once at the rendering boundary.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
public partial struct RenderingSyncSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // This is the ONLY Complete() call needed in the entire frame.
        // All simulation systems chain their jobs via state.Dependency.
        // Here we wait for all jobs to finish before rendering reads the state.
        state.Dependency.Complete();
    }
}
