using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MonoBehaviour that syncs the FogCulled ECS component to SpriteRenderer visibility.
///
/// Since zombies use companion GameObjects with SpriteRenderers for rendering (not Entities Graphics),
/// we need to manually toggle SpriteRenderer.enabled based on the FogCulled component.
///
/// This system finds all zombie GameObjects in the scene and maps them to their entities,
/// then toggles visibility based on the FogCulled component.
/// </summary>
public class FogOfWarSpriteSync : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _enemyQuery;

    // Cache zombie GameObjects and their SpriteRenderers
    private Dictionary<Entity, SpriteRenderer> _entityToSpriteRenderer = new Dictionary<Entity, SpriteRenderer>();

    // Frame throttling
    private int _frameCount;
    private const int UPDATE_INTERVAL = 8;  // Match FogOfWarCullingSystem rate
    private const int CACHE_REFRESH_INTERVAL = 60;  // Refresh cache every 60 frames

    void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            _entityManager = world.EntityManager;

            // Query for all enemies
            _enemyQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyUnit>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }
    }

    void LateUpdate()
    {
        if (_entityManager == default || _enemyQuery.IsEmpty)
            return;

        _frameCount++;

        // Refresh cache periodically to catch newly spawned zombies
        if (_frameCount % CACHE_REFRESH_INTERVAL == 0)
        {
            RefreshSpriteRendererCache();
        }

        // Only update visibility every N frames
        if (_frameCount % UPDATE_INTERVAL != 0) return;

        UpdateSpriteVisibility();
    }

    private void RefreshSpriteRendererCache()
    {
        // Clear stale entries (destroyed entities)
        var entitiesToRemove = new List<Entity>();
        foreach (var kvp in _entityToSpriteRenderer)
        {
            if (!_entityManager.Exists(kvp.Key) || kvp.Value == null)
            {
                entitiesToRemove.Add(kvp.Key);
            }
        }
        foreach (var entity in entitiesToRemove)
        {
            _entityToSpriteRenderer.Remove(entity);
        }

        // Find new zombie SpriteRenderers by position matching
        var entities = _enemyQuery.ToEntityArray(Allocator.Temp);
        var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        // Find all SpriteRenderers with red-ish color (zombies)
        var allRenderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];

            // Skip if already cached
            if (_entityToSpriteRenderer.ContainsKey(entity))
                continue;

            var entityPos = transforms[i].Position;

            // Find matching SpriteRenderer by position
            foreach (var renderer in allRenderers)
            {
                if (renderer == null) continue;

                // Check if this renderer is close to the entity position
                var rendererPos = renderer.transform.position;
                float distSq = (rendererPos.x - entityPos.x) * (rendererPos.x - entityPos.x) +
                              (rendererPos.y - entityPos.y) * (rendererPos.y - entityPos.y);

                // If very close (within 0.1 units), assume it's the same object
                if (distSq < 0.01f)
                {
                    // Check if this is a zombie (red-ish color)
                    if (renderer.color.r > 0.5f && renderer.color.g < 0.5f)
                    {
                        _entityToSpriteRenderer[entity] = renderer;
                        break;
                    }
                }
            }
        }

        entities.Dispose();
        transforms.Dispose();
    }

    private void UpdateSpriteVisibility()
    {
        foreach (var kvp in _entityToSpriteRenderer)
        {
            var entity = kvp.Key;
            var spriteRenderer = kvp.Value;

            if (spriteRenderer == null || !_entityManager.Exists(entity))
                continue;

            // Check if entity has FogCulled component
            bool isCulled = _entityManager.HasComponent<FogCulled>(entity);
            bool shouldBeEnabled = !isCulled;

            // Only update if changed
            if (spriteRenderer.enabled != shouldBeEnabled)
            {
                spriteRenderer.enabled = shouldBeEnabled;
            }
        }
    }

    void OnDestroy()
    {
        _entityToSpriteRenderer.Clear();
    }
}
