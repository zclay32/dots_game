using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System.Collections.Generic;

/// <summary>
/// Renders a minimap showing terrain, units, crystal, and camera viewport.
/// Uses OnGUI for rendering with ECS queries for unit positions.
///
/// Features:
/// - Pre-baked terrain texture from FlowFieldData.Walkable
/// - Fog of war overlay (hidden areas are dark, explored are dimmed)
/// - Tile-based unit markers (multiple units on same tile = one marker)
/// - Crystal marker (cyan) - shown once area is explored (static building)
/// - Enemy units only shown in currently visible areas (disappear when fog returns)
/// - Player units always visible on minimap
/// - Camera viewport rectangle
/// - Configurable position and colors
/// </summary>
public class MinimapRenderer : MonoBehaviour
{
    public enum MinimapPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [Header("Minimap Settings")]
    public float minimapSize = 200f;
    public float minimapPadding = 10f;
    public MinimapPosition position = MinimapPosition.BottomLeft;
    public float borderWidth = 2f;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
    public Color obstacleColor = new Color(0.4f, 0.35f, 0.3f, 1f);
    public Color borderColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    public Color playerUnitColor = new Color(0.2f, 0.9f, 0.2f, 1f);
    public Color enemyUnitColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color selectedUnitColor = new Color(1f, 1f, 0.2f, 1f);
    public Color crystalColor = new Color(0.4f, 0.9f, 1f, 1f);
    public Color cameraViewColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("Fog of War")]
    [Tooltip("Color for areas never explored (fully hidden)")]
    public Color fogHiddenColor = new Color(0f, 0f, 0f, 1f);
    [Tooltip("Color overlay for previously explored areas")]
    public Color fogExploredColor = new Color(0f, 0f, 0f, 0.5f);
    [Tooltip("How often to update fog texture (in frames) - higher = better performance")]
    public int fogUpdateInterval = 30;

    [Header("Display Settings")]
    public float cameraViewLineWidth = 2f;

    [Header("Performance")]
    public int updateInterval = 2;

    // Internal state
    private EntityManager _entityManager;
    private EntityQuery _playerUnitQuery;
    private EntityQuery _enemyUnitQuery;
    private EntityQuery _selectedQuery;
    private EntityQuery _crystalQuery;
    private EntityQuery _gameConfigQuery;

    private Camera _mainCamera;
    private Texture2D _terrainTexture;
    private Texture2D _fogTexture;
    private Texture2D _whiteTexture;
    private bool _initialized;
    private int _frameCount;
    private int _lastFogUpdateFrame;
    private int _terrainTextureSize = 128;
    private bool _fogSystemAvailable;

    // Cached occupied tile screen rects (refreshed every updateInterval frames)
    // Pre-calculated during collection to avoid per-frame recalculation in OnGUI
    private List<Rect> _playerTileRects = new List<Rect>();
    private List<Rect> _enemyTileRects = new List<Rect>();
    private List<Rect> _selectedTileRects = new List<Rect>();
    private Rect? _crystalTileRect;

    // Reusable HashSet for deduplication during collection
    private HashSet<Vector3Int> _tempTileSet = new HashSet<Vector3Int>();

    // Pre-allocated pixel array for fog texture updates (much faster than SetPixel)
    private Color32[] _fogPixels;
    private Color32 _fogHiddenColor32;
    private Color32 _fogExploredColor32;
    private Color32 _fogVisibleColor32;

    // Cached references to avoid repeated singleton lookups
    private IsometricGridManager _gridManager;
    private FogOfWarManager _fogManager;

    // World bounds
    private float _worldMinX, _worldMaxX, _worldMinY, _worldMaxY;
    private Rect _minimapScreenRect;

    void Start()
    {
        _mainCamera = Camera.main;

        // Create helper textures
        CreateHelperTextures();

        // Initialize ECS queries
        InitializeECSQueries();
    }

    void CreateHelperTextures()
    {
        // Create a small white texture for drawing rectangles and tiles
        _whiteTexture = new Texture2D(1, 1);
        _whiteTexture.SetPixel(0, 0, Color.white);
        _whiteTexture.Apply();
    }

    void InitializeECSQueries()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        _entityManager = world.EntityManager;

        // Player units (soldiers)
        _playerUnitQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerUnit>(),
            ComponentType.ReadOnly<LocalTransform>()
        );

        // Enemy units (zombies)
        _enemyUnitQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<EnemyUnit>(),
            ComponentType.ReadOnly<LocalTransform>()
        );

        // Selected units (for highlight)
        _selectedQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<LocalTransform>()
        );

        // Crystal (singleton)
        _crystalQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Crystal>(),
            ComponentType.ReadOnly<LocalTransform>()
        );

        // Game config (for map bounds)
        _gameConfigQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GameConfig>()
        );

        _initialized = true;
    }

    void LateUpdate()
    {
        if (!_initialized)
        {
            InitializeECSQueries();
            return;
        }

        _frameCount++;
        if (_frameCount % updateInterval != 0) return;

        // Update world bounds from game config
        UpdateWorldBounds();

        // Generate terrain texture if needed
        if (_terrainTexture == null && FlowFieldData.IsCreated)
        {
            GenerateTerrainTexture();
        }

        // Check if fog system is available
        _fogSystemAvailable = FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized;

        // Create fog texture if needed
        if (_fogTexture == null && _fogSystemAvailable)
        {
            CreateFogTexture();
        }

        // Update fog texture periodically
        if (_fogSystemAvailable && _fogTexture != null && (_frameCount - _lastFogUpdateFrame) >= fogUpdateInterval)
        {
            UpdateFogTexture();
            _lastFogUpdateFrame = _frameCount;
        }

        // Collect occupied tiles
        CollectOccupiedTiles();
    }

    void UpdateWorldBounds()
    {
        if (_gameConfigQuery.CalculateEntityCount() > 0)
        {
            var config = _gameConfigQuery.GetSingleton<GameConfig>();
            _worldMinX = config.MapCenter.x - config.MapRadius;
            _worldMaxX = config.MapCenter.x + config.MapRadius;
            _worldMinY = config.MapCenter.y - config.MapRadius;
            _worldMaxY = config.MapCenter.y + config.MapRadius;
        }
        else
        {
            // Fallback to flow field bounds
            if (FlowFieldData.IsCreated)
            {
                _worldMinX = -FlowFieldData.WorldOffset.x;
                _worldMaxX = FlowFieldData.GridWidth * FlowFieldData.CellSize - FlowFieldData.WorldOffset.x;
                _worldMinY = -FlowFieldData.WorldOffset.y;
                _worldMaxY = FlowFieldData.GridHeight * FlowFieldData.CellSize - FlowFieldData.WorldOffset.y;
            }
        }
    }

    void GenerateTerrainTexture()
    {
        if (!FlowFieldData.IsCreated || !FlowFieldData.Walkable.IsCreated) return;

        _terrainTexture = new Texture2D(_terrainTextureSize, _terrainTextureSize, TextureFormat.RGBA32, false);
        _terrainTexture.filterMode = FilterMode.Point;

        for (int y = 0; y < _terrainTextureSize; y++)
        {
            for (int x = 0; x < _terrainTextureSize; x++)
            {
                // Map texture coords to flow field grid
                int gridX = (int)((float)x / _terrainTextureSize * FlowFieldData.GridWidth);
                int gridY = (int)((float)y / _terrainTextureSize * FlowFieldData.GridHeight);
                int index = gridY * FlowFieldData.GridWidth + gridX;

                if (index >= 0 && index < FlowFieldData.Walkable.Length)
                {
                    bool walkable = FlowFieldData.Walkable[index];
                    _terrainTexture.SetPixel(x, y, walkable ? backgroundColor : obstacleColor);
                }
                else
                {
                    _terrainTexture.SetPixel(x, y, backgroundColor);
                }
            }
        }
        _terrainTexture.Apply();
    }

    void CreateFogTexture()
    {
        _fogTexture = new Texture2D(_terrainTextureSize, _terrainTextureSize, TextureFormat.RGBA32, false);
        _fogTexture.filterMode = FilterMode.Point;

        // Pre-allocate pixel array and cache Color32 values
        int totalPixels = _terrainTextureSize * _terrainTextureSize;
        _fogPixels = new Color32[totalPixels];
        _fogHiddenColor32 = fogHiddenColor;
        _fogExploredColor32 = fogExploredColor;
        _fogVisibleColor32 = new Color32(0, 0, 0, 0);  // Fully transparent

        // Initialize with hidden color
        for (int i = 0; i < totalPixels; i++)
        {
            _fogPixels[i] = _fogHiddenColor32;
        }
        _fogTexture.SetPixels32(_fogPixels);
        _fogTexture.Apply();
    }

    void UpdateFogTexture()
    {
        if (!_fogSystemAvailable || _fogTexture == null || _fogPixels == null) return;
        if (_fogManager == null || _gridManager == null || _gridManager.Grid == null) return;

        float worldWidth = _worldMaxX - _worldMinX;
        float worldHeight = _worldMaxY - _worldMinY;

        if (worldWidth <= 0 || worldHeight <= 0) return;

        // Pre-calculate scaling factors
        float texToWorldX = worldWidth / _terrainTextureSize;
        float texToWorldY = worldHeight / _terrainTextureSize;

        int pixelIndex = 0;
        for (int y = 0; y < _terrainTextureSize; y++)
        {
            float worldY = _worldMinY + y * texToWorldY;

            for (int x = 0; x < _terrainTextureSize; x++)
            {
                float worldX = _worldMinX + x * texToWorldX;

                // Convert world position to fog cell coordinates
                Vector3Int cell = _gridManager.WorldToCell(new Vector3(worldX, worldY, 0));
                VisibilityState visibility = _fogManager.GetVisibility(cell.x, cell.y);

                // Use pre-cached Color32 values
                _fogPixels[pixelIndex++] = visibility switch
                {
                    VisibilityState.Visible => _fogVisibleColor32,
                    VisibilityState.Explored => _fogExploredColor32,
                    _ => _fogHiddenColor32
                };
            }
        }

        // SetPixels32 is much faster than individual SetPixel calls
        _fogTexture.SetPixels32(_fogPixels);
        _fogTexture.Apply();
    }

    void CollectOccupiedTiles()
    {
        _playerTileRects.Clear();
        _enemyTileRects.Clear();
        _selectedTileRects.Clear();
        _crystalTileRect = null;

        // Cache singleton references once per update
        _gridManager = IsometricGridManager.Instance;
        _fogManager = FogOfWarManager.Instance;

        if (_gridManager == null || _gridManager.Grid == null) return;

        // Calculate minimap rect first (needed for tile rect conversion)
        CalculateMinimapRect();

        // Player units (always visible on minimap)
        if (_playerUnitQuery.CalculateEntityCount() > 0)
        {
            _tempTileSet.Clear();
            var transforms = _playerUnitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < transforms.Length; i++)
            {
                var pos = transforms[i].Position;
                Vector3Int tile = _gridManager.WorldToCell(new Vector3(pos.x, pos.y, 0));
                _tempTileSet.Add(tile);
            }
            transforms.Dispose();

            // Convert tiles to screen rects (once, not every OnGUI frame)
            foreach (var tile in _tempTileSet)
            {
                Rect rect = TileToMinimapRect(tile);
                if (rect.width > 0 && rect.height > 0)
                    _playerTileRects.Add(rect);
            }
        }

        // Enemy units (only show in currently visible areas - not explored)
        if (_enemyUnitQuery.CalculateEntityCount() > 0)
        {
            _tempTileSet.Clear();
            var transforms = _enemyUnitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < transforms.Length; i++)
            {
                var pos = transforms[i].Position;
                float2 worldPos = new float2(pos.x, pos.y);

                // Only add enemies in currently visible areas (not explored)
                if (!_fogSystemAvailable || IsPositionVisible(worldPos))
                {
                    Vector3Int tile = _gridManager.WorldToCell(new Vector3(pos.x, pos.y, 0));
                    _tempTileSet.Add(tile);
                }
            }
            transforms.Dispose();

            foreach (var tile in _tempTileSet)
            {
                Rect rect = TileToMinimapRect(tile);
                if (rect.width > 0 && rect.height > 0)
                    _enemyTileRects.Add(rect);
            }
        }

        // Selected units (always visible - they're player units)
        if (_selectedQuery.CalculateEntityCount() > 0)
        {
            _tempTileSet.Clear();
            var transforms = _selectedQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < transforms.Length; i++)
            {
                var pos = transforms[i].Position;
                Vector3Int tile = _gridManager.WorldToCell(new Vector3(pos.x, pos.y, 0));
                _tempTileSet.Add(tile);
            }
            transforms.Dispose();

            foreach (var tile in _tempTileSet)
            {
                Rect rect = TileToMinimapRect(tile);
                if (rect.width > 0 && rect.height > 0)
                    _selectedTileRects.Add(rect);
            }
        }

        // Crystal (show if area has been explored - it's a static building)
        if (_crystalQuery.CalculateEntityCount() > 0)
        {
            var transforms = _crystalQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (transforms.Length > 0)
            {
                var pos = transforms[0].Position;
                float2 crystalPos = new float2(pos.x, pos.y);

                // Show crystal if area has been explored (static building stays on map)
                if (!_fogSystemAvailable || IsPositionExplored(crystalPos))
                {
                    Vector3Int tile = _gridManager.WorldToCell(new Vector3(pos.x, pos.y, 0));
                    Rect rect = TileToMinimapRect(tile);
                    if (rect.width > 0 && rect.height > 0)
                        _crystalTileRect = rect;
                }
            }
            transforms.Dispose();
        }
    }

    /// <summary>
    /// Check if a world position is currently visible (for enemies - must be fully visible, not just explored)
    /// Uses cached references - must be called after _gridManager and _fogManager are set
    /// </summary>
    bool IsPositionVisible(float2 worldPos)
    {
        if (!_fogSystemAvailable || _fogManager == null) return true;
        if (_gridManager == null || _gridManager.Grid == null) return true;

        Vector3Int cell = _gridManager.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0));
        VisibilityState visibility = _fogManager.GetVisibility(cell.x, cell.y);

        return visibility == VisibilityState.Visible;
    }

    /// <summary>
    /// Check if a world position has been explored (for static objects like crystal)
    /// Uses cached references - must be called after _gridManager and _fogManager are set
    /// </summary>
    bool IsPositionExplored(float2 worldPos)
    {
        if (!_fogSystemAvailable || _fogManager == null) return true;
        if (_gridManager == null || _gridManager.Grid == null) return true;

        Vector3Int cell = _gridManager.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0));
        VisibilityState visibility = _fogManager.GetVisibility(cell.x, cell.y);

        return visibility == VisibilityState.Visible || visibility == VisibilityState.Explored;
    }

    void OnGUI()
    {
        if (!_initialized || _terrainTexture == null) return;

        // Note: CalculateMinimapRect is called in CollectOccupiedTiles during LateUpdate
        // so _minimapScreenRect is already up to date

        // Draw terrain background
        GUI.DrawTexture(_minimapScreenRect, _terrainTexture);

        // Draw fog overlay (if available)
        if (_fogTexture != null && _fogSystemAvailable)
        {
            GUI.DrawTexture(_minimapScreenRect, _fogTexture);
        }

        // Draw pre-calculated tile rects (enemies first, then players, then selected on top)
        DrawTileRects(_enemyTileRects, enemyUnitColor);
        DrawTileRects(_playerTileRects, playerUnitColor);
        DrawTileRects(_selectedTileRects, selectedUnitColor);

        // Draw crystal tile
        if (_crystalTileRect.HasValue)
        {
            GUI.color = crystalColor;
            GUI.DrawTexture(_crystalTileRect.Value, _whiteTexture);
            GUI.color = Color.white;
        }

        // Draw camera viewport
        DrawCameraViewport();

        // Draw border
        DrawBorder();
    }

    void CalculateMinimapRect()
    {
        float x, y;

        switch (position)
        {
            case MinimapPosition.TopLeft:
                x = minimapPadding;
                y = minimapPadding;
                break;
            case MinimapPosition.TopRight:
                x = Screen.width - minimapSize - minimapPadding;
                y = minimapPadding;
                break;
            case MinimapPosition.BottomRight:
                x = Screen.width - minimapSize - minimapPadding;
                y = Screen.height - minimapSize - minimapPadding;
                break;
            case MinimapPosition.BottomLeft:
            default:
                x = minimapPadding;
                y = Screen.height - minimapSize - minimapPadding;
                break;
        }

        _minimapScreenRect = new Rect(x, y, minimapSize, minimapSize);
    }

    Vector2 WorldToMinimap(float2 worldPos)
    {
        float worldWidth = _worldMaxX - _worldMinX;
        float worldHeight = _worldMaxY - _worldMinY;

        if (worldWidth <= 0 || worldHeight <= 0) return Vector2.zero;

        float normalizedX = (worldPos.x - _worldMinX) / worldWidth;
        float normalizedY = (worldPos.y - _worldMinY) / worldHeight;

        // Clamp to minimap bounds
        normalizedX = math.clamp(normalizedX, 0f, 1f);
        normalizedY = math.clamp(normalizedY, 0f, 1f);

        return new Vector2(
            _minimapScreenRect.x + normalizedX * _minimapScreenRect.width,
            _minimapScreenRect.y + (1f - normalizedY) * _minimapScreenRect.height  // Flip Y for screen coords
        );
    }

    /// <summary>
    /// Convert a tile to its minimap screen rectangle
    /// Uses cached _gridManager reference - must be called after it's set
    /// </summary>
    Rect TileToMinimapRect(Vector3Int tile)
    {
        if (_gridManager == null || _gridManager.Grid == null)
            return Rect.zero;

        // Get the world bounds of this tile
        Vector3 cellCenter = _gridManager.CellToWorld(tile);
        Vector3 cellSize = _gridManager.Grid.cellSize;

        // For isometric tiles, we need the corners in world space
        // Cell center is the middle of the tile
        float halfWidth = cellSize.x / 2f;
        float halfHeight = cellSize.y / 2f;

        // Get min/max world positions for the tile
        float2 tileMin = new float2(cellCenter.x - halfWidth, cellCenter.y - halfHeight);
        float2 tileMax = new float2(cellCenter.x + halfWidth, cellCenter.y + halfHeight);

        // Convert to minimap screen coordinates
        Vector2 screenMin = WorldToMinimap(tileMin);
        Vector2 screenMax = WorldToMinimap(tileMax);

        // Calculate rect (accounting for Y flip)
        float width = screenMax.x - screenMin.x;
        float height = screenMin.y - screenMax.y;

        // Ensure minimum size of 2 pixels so tiles are visible
        width = math.max(width, 2f);
        height = math.max(height, 2f);

        return new Rect(screenMin.x, screenMax.y, width, height);
    }

    /// <summary>
    /// Draw pre-calculated tile rects (fast - no conversion needed)
    /// </summary>
    void DrawTileRects(List<Rect> tileRects, Color color)
    {
        if (tileRects.Count == 0) return;

        GUI.color = color;

        for (int i = 0; i < tileRects.Count; i++)
        {
            GUI.DrawTexture(tileRects[i], _whiteTexture);
        }

        GUI.color = Color.white;
    }

    void DrawCameraViewport()
    {
        if (_mainCamera == null) return;

        float camHeight = _mainCamera.orthographicSize * 2f;
        float camWidth = camHeight * _mainCamera.aspect;
        Vector3 camPos = _mainCamera.transform.position;

        // Calculate viewport corners in world space
        float2 bottomLeft = new float2(camPos.x - camWidth / 2f, camPos.y - camHeight / 2f);
        float2 topRight = new float2(camPos.x + camWidth / 2f, camPos.y + camHeight / 2f);

        // Convert to minimap screen coordinates
        Vector2 minScreen = WorldToMinimap(bottomLeft);
        Vector2 maxScreen = WorldToMinimap(topRight);

        // Draw rectangle outline
        float width = maxScreen.x - minScreen.x;
        float height = minScreen.y - maxScreen.y;  // Inverted because of Y flip

        if (width > 0 && height > 0)
        {
            Rect viewportRect = new Rect(minScreen.x, maxScreen.y, width, height);
            DrawRectOutline(viewportRect, cameraViewColor, cameraViewLineWidth);
        }
    }

    void DrawRectOutline(Rect rect, Color color, float lineWidth)
    {
        GUI.color = color;

        // Top
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, lineWidth), _whiteTexture);
        // Bottom
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - lineWidth, rect.width, lineWidth), _whiteTexture);
        // Left
        GUI.DrawTexture(new Rect(rect.x, rect.y, lineWidth, rect.height), _whiteTexture);
        // Right
        GUI.DrawTexture(new Rect(rect.x + rect.width - lineWidth, rect.y, lineWidth, rect.height), _whiteTexture);

        GUI.color = Color.white;
    }

    void DrawBorder()
    {
        DrawRectOutline(_minimapScreenRect, borderColor, borderWidth);
    }

    void OnDestroy()
    {
        if (_terrainTexture != null) Destroy(_terrainTexture);
        if (_fogTexture != null) Destroy(_fogTexture);
        if (_whiteTexture != null) Destroy(_whiteTexture);
    }
}
