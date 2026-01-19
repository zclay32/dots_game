using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// System that updates zombie visuals based on their AI state
/// DISABLED - per-instance color not working with 2D sprites in Entities Graphics
/// The ZombieState logic still works for AI behavior, just no visual feedback
/// </summary>
[BurstCompile]
[DisableAutoCreation] // Disabled - color not working
public partial struct ZombieStateColorSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Disabled
    }
}
