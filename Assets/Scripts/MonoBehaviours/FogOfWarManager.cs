using Unity.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Manages the fog of war visibility grid and fog tilemap.
/// Uses NativeArray<byte> for Burst-compatible visibility data.
/// Integrates with IsometricGridManager for coordinate conversion.
/// </summary>
public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    [Header("Tile References")]
    [Tooltip("Fully opaque tile for hidden (never seen) areas")]
    public TileBase hiddenTile;

    [Tooltip("Semi-transparent tile for explored (previously seen) areas")]
    public TileBase exploredTile;

    [Header("Rendering")]
    [Tooltip("Optional material for fog tilemap (use stencil shader to prevent alpha stacking)")]
    public Material fogMaterial;

    [Header("Runtime References")]
    public Tilemap FogTilemap { get; private set; }

    // Visibility grid - NativeArray for Burst compatibility
    private NativeArray<byte> _visibilityGrid;

    // Grid dimensions (cached from IsometricGridManager)
    private int _gridWidth;
    private int _gridHeight;
    private int _minCellX;
    private int _minCellY;

    // Dirty tracking for efficient tilemap updates
    private NativeArray<bool> _dirtyFlags;
    private bool _hasDirtyTiles;

    public bool IsInitialized { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Wait for IsometricGridManager to initialize first
        StartCoroutine(InitializeAfterGrid());
    }

    private System.Collections.IEnumerator InitializeAfterGrid()
    {
        // Wait until IsometricGridManager is ready
        while (IsometricGridManager.Instance == null || IsometricGridManager.Instance.Grid == null)
        {
            yield return null;
        }

        Initialize();
    }

    private void Initialize()
    {
        var gridManager = IsometricGridManager.Instance;

        // Cache grid dimensions
        _minCellX = gridManager.MinCellX;
        _minCellY = gridManager.MinCellY;
        _gridWidth = gridManager.MaxCellX - gridManager.MinCellX + 1;
        _gridHeight = gridManager.MaxCellY - gridManager.MinCellY + 1;

        int totalCells = _gridWidth * _gridHeight;

        // Allocate visibility grid (persistent for ECS access)
        _visibilityGrid = new NativeArray<byte>(totalCells, Allocator.Persistent);
        _dirtyFlags = new NativeArray<bool>(totalCells, Allocator.Persistent);

        // Initialize all cells as Hidden
        for (int i = 0; i < totalCells; i++)
        {
            _visibilityGrid[i] = (byte)VisibilityState.Hidden;
        }

        // Create fog tilemap
        CreateFogTilemap(gridManager);

        // Fill initial fog (all hidden)
        FillInitialFog(gridManager);

        IsInitialized = true;
        Debug.Log($"[FogOfWarManager] Initialized {_gridWidth}x{_gridHeight} grid ({totalCells} cells)");
    }

    private void CreateFogTilemap(IsometricGridManager gridManager)
    {
        // Create fog tilemap as sibling to ground tilemap
        var fogObj = new GameObject("FogTilemap");
        fogObj.transform.SetParent(gridManager.transform);
        fogObj.transform.localPosition = Vector3.zero;

        FogTilemap = fogObj.AddComponent<Tilemap>();
        var fogRenderer = fogObj.AddComponent<TilemapRenderer>();

        // Render above ground (-100) but below units (default 0)
        fogRenderer.sortingOrder = -50;

        // Apply custom material if assigned (stencil shader prevents alpha stacking)
        if (fogMaterial != null)
        {
            fogRenderer.material = fogMaterial;
            Debug.Log("[FogOfWarManager] Fog tilemap using custom material");
        }

        Debug.Log("[FogOfWarManager] Fog tilemap created with sorting order -50");
    }

    private void FillInitialFog(IsometricGridManager gridManager)
    {
        if (hiddenTile == null)
        {
            Debug.LogWarning("[FogOfWarManager] No hidden tile assigned - fog will be invisible!");
            return;
        }

        int tilesPlaced = 0;
        int mapRadius = gridManager.mapRadiusInTiles;

        for (int y = _minCellY; y <= gridManager.MaxCellY; y++)
        {
            for (int x = _minCellX; x <= gridManager.MaxCellX; x++)
            {
                // Match diamond shape from IsometricGridManager
                int manhattanDist = Mathf.Abs(x) + Mathf.Abs(y);
                if (manhattanDist <= mapRadius)
                {
                    Vector3Int cellPos = new Vector3Int(x, y, 0);
                    FogTilemap.SetTile(cellPos, hiddenTile);
                    tilesPlaced++;
                }
            }
        }

        Debug.Log($"[FogOfWarManager] Placed {tilesPlaced} initial fog tiles");
    }

    /// <summary>
    /// Get read-only access to the visibility grid for ECS systems.
    /// </summary>
    public NativeArray<byte>.ReadOnly GetVisibilityGridReadOnly()
    {
        return _visibilityGrid.AsReadOnly();
    }

    /// <summary>
    /// Get the visibility grid for writing (used by FogOfWarSystem).
    /// </summary>
    public NativeArray<byte> GetVisibilityGrid()
    {
        return _visibilityGrid;
    }

    /// <summary>
    /// Get dirty flags array for tracking changes (used by FogOfWarSystem).
    /// </summary>
    public NativeArray<bool> GetDirtyFlags()
    {
        return _dirtyFlags;
    }

    /// <summary>
    /// Mark that there are dirty tiles to sync.
    /// </summary>
    public void MarkDirty()
    {
        _hasDirtyTiles = true;
    }

    /// <summary>
    /// Convert cell coordinates to grid index.
    /// </summary>
    public int CellToIndex(int cellX, int cellY)
    {
        int localX = cellX - _minCellX;
        int localY = cellY - _minCellY;
        return localY * _gridWidth + localX;
    }

    /// <summary>
    /// Convert grid index to cell coordinates.
    /// </summary>
    public (int x, int y) IndexToCell(int index)
    {
        int localY = index / _gridWidth;
        int localX = index % _gridWidth;
        return (localX + _minCellX, localY + _minCellY);
    }

    /// <summary>
    /// Check if cell coordinates are within bounds.
    /// </summary>
    public bool IsCellInBounds(int cellX, int cellY)
    {
        int localX = cellX - _minCellX;
        int localY = cellY - _minCellY;
        return localX >= 0 && localX < _gridWidth &&
               localY >= 0 && localY < _gridHeight;
    }

    /// <summary>
    /// Get visibility state at cell coordinates.
    /// </summary>
    public VisibilityState GetVisibility(int cellX, int cellY)
    {
        if (!IsCellInBounds(cellX, cellY))
            return VisibilityState.Hidden;

        int index = CellToIndex(cellX, cellY);
        return (VisibilityState)_visibilityGrid[index];
    }

    /// <summary>
    /// Set visibility state at cell coordinates.
    /// </summary>
    public void SetVisibility(int cellX, int cellY, VisibilityState state)
    {
        if (!IsCellInBounds(cellX, cellY))
            return;

        int index = CellToIndex(cellX, cellY);
        byte newState = (byte)state;

        if (_visibilityGrid[index] != newState)
        {
            _visibilityGrid[index] = newState;
            _dirtyFlags[index] = true;
            _hasDirtyTiles = true;
        }
    }

    /// <summary>
    /// Grid width in cells.
    /// </summary>
    public int GridWidth => _gridWidth;

    /// <summary>
    /// Grid height in cells.
    /// </summary>
    public int GridHeight => _gridHeight;

    /// <summary>
    /// Minimum cell X coordinate.
    /// </summary>
    public int MinCellX => _minCellX;

    /// <summary>
    /// Minimum cell Y coordinate.
    /// </summary>
    public int MinCellY => _minCellY;

    void LateUpdate()
    {
        // Sync dirty tiles to tilemap
        if (_hasDirtyTiles && IsInitialized)
        {
            SyncDirtyTilesToTilemap();
        }
    }

    private void SyncDirtyTilesToTilemap()
    {
        int syncedCount = 0;

        for (int i = 0; i < _dirtyFlags.Length; i++)
        {
            if (_dirtyFlags[i])
            {
                var (cellX, cellY) = IndexToCell(i);
                Vector3Int cellPos = new Vector3Int(cellX, cellY, 0);
                VisibilityState state = (VisibilityState)_visibilityGrid[i];

                TileBase tile = state switch
                {
                    VisibilityState.Hidden => hiddenTile,
                    VisibilityState.Explored => exploredTile,
                    VisibilityState.Visible => null,  // No tile = fully visible
                    _ => hiddenTile
                };

                FogTilemap.SetTile(cellPos, tile);
                _dirtyFlags[i] = false;
                syncedCount++;
            }
        }

        _hasDirtyTiles = false;

        if (syncedCount > 0)
        {
            Debug.Log($"[FogOfWarManager] Synced {syncedCount} fog tiles");
        }
    }

    void OnDestroy()
    {
        if (_visibilityGrid.IsCreated)
        {
            _visibilityGrid.Dispose();
        }
        if (_dirtyFlags.IsCreated)
        {
            _dirtyFlags.Dispose();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
