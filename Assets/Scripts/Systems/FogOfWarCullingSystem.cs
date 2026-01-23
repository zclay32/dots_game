using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that hides/shows enemy units based on fog of war visibility.
/// Adds FogHidden component and sets scale to 0 for enemies in hidden/explored areas.
/// Restores original scale when they become visible.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FogOfWarSystem))]
public partial class FogOfWarCullingSystem : SystemBase
{
    private const int UPDATE_INTERVAL = 4;  // Match FogOfWarSystem update rate
    private int _frameCount;

    protected override void OnUpdate()
    {
        _frameCount++;
        if (_frameCount % UPDATE_INTERVAL != 0)
            return;

        var fogManager = FogOfWarManager.Instance;
        if (fogManager == null || !fogManager.IsInitialized)
            return;

        var gridManager = IsometricGridManager.Instance;
        if (gridManager == null || gridManager.Grid == null)
            return;

        var grid = gridManager.Grid;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // Process enemies that should be hidden (not currently hidden, but in non-visible cell)
        foreach (var (transform, entity) in
            SystemAPI.Query<RefRW<LocalTransform>>()
            .WithAll<EnemyUnit>()
            .WithNone<FogHidden>()
            .WithEntityAccess())
        {
            float3 worldPos = transform.ValueRO.Position;
            var cellPos = grid.WorldToCell(new UnityEngine.Vector3(worldPos.x, worldPos.y, 0));

            VisibilityState visibility = fogManager.GetVisibility(cellPos.x, cellPos.y);

            // Hide if not currently visible
            if (visibility != VisibilityState.Visible)
            {
                // Save original scale before hiding
                ecb.AddComponent(entity, new FogHiddenScale { Value = transform.ValueRO.Scale });
                ecb.AddComponent<FogHidden>(entity);

                // Set scale to 0 to hide the sprite
                transform.ValueRW.Scale = 0f;
            }
        }

        // Process enemies that should be shown (currently hidden, but in visible cell)
        foreach (var (transform, savedScale, entity) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<FogHiddenScale>>()
            .WithAll<EnemyUnit, FogHidden>()
            .WithEntityAccess())
        {
            float3 worldPos = transform.ValueRO.Position;
            var cellPos = grid.WorldToCell(new UnityEngine.Vector3(worldPos.x, worldPos.y, 0));

            VisibilityState visibility = fogManager.GetVisibility(cellPos.x, cellPos.y);

            // Show if now visible
            if (visibility == VisibilityState.Visible)
            {
                // Restore original scale
                transform.ValueRW.Scale = savedScale.ValueRO.Value;

                ecb.RemoveComponent<FogHidden>(entity);
                ecb.RemoveComponent<FogHiddenScale>(entity);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
