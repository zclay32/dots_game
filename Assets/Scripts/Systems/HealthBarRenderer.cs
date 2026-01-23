using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System.Collections.Generic;

/// <summary>
/// Performant health bar renderer using pooled sprite renderers
/// Shows health bars for all units within camera view
/// Updates positions every frame but only queries ECS every 2 frames
/// </summary>
public class HealthBarRenderer : MonoBehaviour
{
    [Header("Health Bar Settings")]
    public float barWidth = 0.6f;
    public float barHeight = 0.08f;
    public float barYOffset = 0.4f;
    public Color backgroundBarColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    public Color soldierHealthColor = new Color(0f, 0.9f, 0f, 1f);
    public Color zombieHealthColor = new Color(0.9f, 0f, 0f, 1f);
    public Color lowHealthColor = new Color(1f, 0.4f, 0f, 1f);
    
    [Header("Display Settings")]
    public bool showFullHealthBars = true;
    public float lowHealthThreshold = 0.3f;
    
    [Header("References")]
    public Sprite barSprite; // Assign a white square sprite
    
    private EntityManager _entityManager;
    private EntityQuery _healthQuery;
    private Camera _mainCamera;
    
    // Object pooling - grows as needed
    private List<GameObject> _healthBarPool = new List<GameObject>();
    private List<SpriteRenderer> _bgRenderers = new List<SpriteRenderer>();
    private List<SpriteRenderer> _fgRenderers = new List<SpriteRenderer>();
    private int _activeBarCount = 0;
    
    // Cached data for interpolation between updates
    private struct CachedHealthBar
    {
        public float3 Position;
        public float HealthPercent;
        public bool IsPlayer;
        public bool IsActive;
    }
    private List<CachedHealthBar> _cachedBars = new List<CachedHealthBar>();
    private int _frameCount;
    
    void Start()
    {
        _mainCamera = Camera.main;
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            _entityManager = world.EntityManager;
            
            _healthQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<Faction>()
            );
        }
    }
    
    GameObject CreateHealthBar(int index)
    {
        GameObject barObj = new GameObject($"HealthBar_{index}");
        barObj.transform.SetParent(transform);
        barObj.SetActive(false);
        
        // Background (dark)
        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(barObj.transform);
        bgObj.transform.localPosition = Vector3.zero;
        bgObj.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        SpriteRenderer bgSr = bgObj.AddComponent<SpriteRenderer>();
        bgSr.sprite = barSprite;
        bgSr.color = backgroundBarColor;
        bgSr.sortingOrder = 99;
        
        // Foreground (health)
        GameObject fgObj = new GameObject("FG");
        fgObj.transform.SetParent(barObj.transform);
        fgObj.transform.localPosition = Vector3.zero;
        fgObj.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        SpriteRenderer fgSr = fgObj.AddComponent<SpriteRenderer>();
        fgSr.sprite = barSprite;
        fgSr.color = soldierHealthColor;
        fgSr.sortingOrder = 100;
        
        _healthBarPool.Add(barObj);
        _bgRenderers.Add(bgSr);
        _fgRenderers.Add(fgSr);
        
        return barObj;
    }
    
    void LateUpdate()
    {
        if (_mainCamera == null || _entityManager == default) return;
        
        _frameCount++;
        
        // Only query ECS every 2 frames
        if (_frameCount % 2 == 0)
        {
            UpdateHealthBarData();
        }
        
        // Always update positions from cached data
        RenderHealthBars();
    }
    
    void UpdateHealthBarData()
    {
        // Get camera bounds for culling
        float camHeight = _mainCamera.orthographicSize * 2f;
        float camWidth = camHeight * _mainCamera.aspect;
        Vector3 camPos = _mainCamera.transform.position;
        float minX = camPos.x - camWidth / 2f - 1f; // Small buffer
        float maxX = camPos.x + camWidth / 2f + 1f;
        float minY = camPos.y - camHeight / 2f - 1f;
        float maxY = camPos.y + camHeight / 2f + 1f;
        
        var healths = _healthQuery.ToComponentDataArray<Health>(Allocator.Temp);
        var transforms = _healthQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var factions = _healthQuery.ToComponentDataArray<Faction>(Allocator.Temp);
        
        // Resize cached bars list if needed
        while (_cachedBars.Count < healths.Length)
        {
            _cachedBars.Add(new CachedHealthBar());
        }
        
        int activeCount = 0;
        
        for (int i = 0; i < healths.Length; i++)
        {
            Health health = healths[i];

            // Skip dead units
            if (health.IsDead) continue;

            // Skip entities hidden by fog of war (scale set to 0)
            if (transforms[i].Scale < 0.01f) continue;

            // Skip full health unless enabled
            float healthPercent = health.Current / health.Max;
            if (!showFullHealthBars && healthPercent >= 1f) continue;

            // Camera frustum culling
            float3 worldPos = transforms[i].Position;
            if (worldPos.x < minX || worldPos.x > maxX ||
                worldPos.y < minY || worldPos.y > maxY) continue;
            
            // Cache this health bar data
            _cachedBars[activeCount] = new CachedHealthBar
            {
                Position = worldPos,
                HealthPercent = healthPercent,
                IsPlayer = factions[i].Value == FactionType.Player,
                IsActive = true
            };
            activeCount++;
        }
        
        // Mark remaining as inactive
        for (int i = activeCount; i < _cachedBars.Count; i++)
        {
            var bar = _cachedBars[i];
            bar.IsActive = false;
            _cachedBars[i] = bar;
        }
        
        _activeBarCount = activeCount;
        
        healths.Dispose();
        transforms.Dispose();
        factions.Dispose();
    }
    
    void RenderHealthBars()
    {
        // Hide all bars first
        for (int i = 0; i < _healthBarPool.Count; i++)
        {
            if (i >= _activeBarCount)
            {
                if (_healthBarPool[i].activeSelf)
                    _healthBarPool[i].SetActive(false);
            }
        }
        
        // Render active bars
        for (int i = 0; i < _activeBarCount; i++)
        {
            var cached = _cachedBars[i];
            if (!cached.IsActive) continue;
            
            // Expand pool if needed
            if (i >= _healthBarPool.Count)
            {
                CreateHealthBar(_healthBarPool.Count);
            }
            
            GameObject bar = _healthBarPool[i];
            SpriteRenderer fgSr = _fgRenderers[i];
            
            // Position
            bar.transform.position = new Vector3(cached.Position.x, cached.Position.y + barYOffset, 0);
            if (!bar.activeSelf) bar.SetActive(true);
            
            // Scale foreground based on health
            Transform fgTransform = fgSr.transform;
            fgTransform.localScale = new Vector3(barWidth * cached.HealthPercent, barHeight, 1f);
            
            // Offset foreground so it shrinks from right
            float offset = (barWidth - barWidth * cached.HealthPercent) * 0.5f;
            fgTransform.localPosition = new Vector3(-offset, 0, 0);
            
            // Color based on faction and health
            Color healthColor;
            if (cached.HealthPercent <= lowHealthThreshold)
            {
                healthColor = lowHealthColor;
            }
            else if (cached.IsPlayer)
            {
                healthColor = soldierHealthColor;
            }
            else
            {
                healthColor = zombieHealthColor;
            }
            fgSr.color = healthColor;
        }
    }
}
