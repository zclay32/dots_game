using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that assigns new random targets when units reach their destination
/// DISABLED - not needed during combat, units use CombatTarget instead
/// </summary>
[BurstCompile]
[DisableAutoCreation] // Disabled for performance
public partial struct TargetSelectionSystem : ISystem
{
    private Random _random;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _random = new Random(98765);
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Disabled
    }
}
