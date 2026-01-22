using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that culls (hides) enemy units that are in fog of war hidden areas.
/// Runs after the FogOfWarSystem to use updated visibility data.
///
/// Uses the FogCulled tag component to mark hidden entities.
/// Zombies in Hidden or Explored areas get FogCulled added (hidden).
/// Zombies in Visible areas get FogCulled removed (shown).
///
/// Other systems (health bar renderer, unit renderer) check for this tag
/// to skip rendering hidden entities.
/// </summary>
[UpdateAfter(typeof(FogOfWarSystem))]
public partial struct FogOfWarCullingSystem : ISystem
{
    private int _frameCount;
    private const int UPDATE_INTERVAL = 8;  // Match FogOfWarSystem update rate

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FogOfWarConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        _frameCount++;
        if (_frameCount % UPDATE_INTERVAL != 0) return;

        var fogManager = FogOfWarManager.Instance;
        if (fogManager == null || !fogManager.IsInitialized)
            return;

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // Process all enemy units (zombies)
        foreach (var (transform, entity) in
            SystemAPI.Query<RefRO<LocalTransform>>()
                .WithAll<EnemyUnit>()
                .WithEntityAccess())
        {
            float2 worldPos = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.y);
            int gridIndex = fogManager.WorldToGridIndex(worldPos);

            bool shouldBeVisible = false;
            if (gridIndex >= 0)
            {
                var visibility = fogManager.GetVisibility(gridIndex);
                // Show zombies only in Visible areas, hide in Hidden/Explored
                shouldBeVisible = visibility == VisibilityState.Visible;
            }

            bool isCulled = state.EntityManager.HasComponent<FogCulled>(entity);

            if (shouldBeVisible && isCulled)
            {
                // Remove FogCulled to show the entity
                ecb.RemoveComponent<FogCulled>(entity);
            }
            else if (!shouldBeVisible && !isCulled)
            {
                // Add FogCulled to hide the entity
                ecb.AddComponent<FogCulled>(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
