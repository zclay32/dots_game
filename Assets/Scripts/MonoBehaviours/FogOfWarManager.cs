using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Manages the fog of war visibility grid and syncs it to the tilemap overlay.
/// Holds the NativeArray of visibility states that the ECS FogOfWarSystem updates.
/// </summary>
public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    [Header("Tile References")]
    [Tooltip("Tile for fully hidden areas (never seen)")]
    public TileBase hiddenTile;

    [Tooltip("Tile for explored areas (previously seen, darkened)")]
    public TileBase exploredTile;

    [Header("Grid Settings")]
    [Tooltip("Reference to the IsometricGridManager")]
    public IsometricGridManager gridManager;

    // Visibility grid data
    private NativeArray<VisibilityState> _visibilityGrid;
    private NativeArray<VisibilityState> _previousVisibilityGrid;
    private int _gridWidth;
    private int _gridHeight;
    private float _cellSizeX;
    private float _cellSizeY;
    private bool _isInitialized;

    // Tilemap reference
    private Tilemap _fogTilemap;

    // Frame counting for updates
    private int _frameCount;
    private const int SYNC_INTERVAL = 8; // Sync tilemap every 8 frames (match FogOfWarSystem)

    public NativeArray<VisibilityState> VisibilityGrid => _visibilityGrid;
    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;
    public float CellSizeX => _cellSizeX;
    public float CellSizeY => _cellSizeY;
    public bool IsInitialized => _isInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Wait for grid manager to initialize
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<IsometricGridManager>();
        }

        // Delay initialization to let ECS world set up
        Invoke(nameof(Initialize), 0.5f);
    }

    private void Initialize()
    {
        // Try to get config from ECS
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            Debug.LogWarning("[FogOfWarManager] ECS world not ready, retrying...");
            Invoke(nameof(Initialize), 0.5f);
            return;
        }

        // Get FogOfWarConfig singleton
        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(FogOfWarConfig));

        if (query.IsEmpty)
        {
            Debug.LogWarning("[FogOfWarManager] FogOfWarConfig not found, retrying...");
            Invoke(nameof(Initialize), 0.5f);
            return;
        }

        var config = query.GetSingleton<FogOfWarConfig>();

        // Initialize grid from config
        _gridWidth = config.GridWidth;
        _gridHeight = config.GridHeight;

        // Get cell size from the grid manager (isometric cell dimensions)
        if (gridManager != null)
        {
            _cellSizeX = gridManager.cellSize.x;
            _cellSizeY = gridManager.cellSize.y;
        }
        else
        {
            _cellSizeX = config.TileSize;
            _cellSizeY = config.TileSize * 0.5f; // Default 2:1 isometric ratio
        }

        // Allocate visibility grids
        int totalCells = _gridWidth * _gridHeight;
        _visibilityGrid = new NativeArray<VisibilityState>(totalCells, Allocator.Persistent);
        _previousVisibilityGrid = new NativeArray<VisibilityState>(totalCells, Allocator.Persistent);

        // Initialize all cells to Hidden
        for (int i = 0; i < totalCells; i++)
        {
            _visibilityGrid[i] = VisibilityState.Hidden;
            _previousVisibilityGrid[i] = VisibilityState.Hidden;
        }

        // Get fog tilemap reference
        if (gridManager != null)
        {
            _fogTilemap = gridManager.FogOverlayTilemap;
        }

        // Initial fill of fog tiles
        FillFogTilemap();

        _isInitialized = true;
        Debug.Log($"[FogOfWarManager] Initialized: {_gridWidth}x{_gridHeight} grid ({totalCells} cells)");
    }

    private void FillFogTilemap()
    {
        if (_fogTilemap == null || hiddenTile == null)
        {
            Debug.LogWarning("[FogOfWarManager] Cannot fill fog tilemap - missing references");
            return;
        }

        // Fill entire grid with hidden tiles
        int halfWidth = _gridWidth / 2;
        int halfHeight = _gridHeight / 2;

        for (int x = -halfWidth; x < halfWidth; x++)
        {
            for (int y = -halfHeight; y < halfHeight; y++)
            {
                _fogTilemap.SetTile(new Vector3Int(x, y, 0), hiddenTile);
            }
        }

        Debug.Log($"[FogOfWarManager] Filled fog tilemap with {_gridWidth * _gridHeight} hidden tiles");
    }

    private void LateUpdate()
    {
        if (!_isInitialized || _fogTilemap == null) return;

        _frameCount++;
        if (_frameCount % SYNC_INTERVAL != 0) return;

        SyncVisibilityToTilemap();
    }

    private void SyncVisibilityToTilemap()
    {
        int halfWidth = _gridWidth / 2;
        int halfHeight = _gridHeight / 2;
        int changedCount = 0;

        for (int i = 0; i < _visibilityGrid.Length; i++)
        {
            // Only update tiles that changed
            if (_visibilityGrid[i] != _previousVisibilityGrid[i])
            {
                int x = i % _gridWidth - halfWidth;
                int y = i / _gridWidth - halfHeight;
                var cellPos = new Vector3Int(x, y, 0);

                TileBase tile = _visibilityGrid[i] switch
                {
                    VisibilityState.Hidden => hiddenTile,
                    VisibilityState.Explored => exploredTile,
                    VisibilityState.Visible => null, // No tile = transparent, shows terrain
                    _ => hiddenTile
                };

                _fogTilemap.SetTile(cellPos, tile);
                _previousVisibilityGrid[i] = _visibilityGrid[i];
                changedCount++;
            }
        }

        // Debug logging removed for performance - uncomment if needed
        // if (changedCount > 0)
        // {
        //     Debug.Log($"[FogOfWarManager] Synced {changedCount} tile changes");
        // }
    }

    /// <summary>
    /// Converts a world position to a grid cell index using isometric coordinate transformation.
    /// Returns -1 if out of bounds.
    /// </summary>
    public int WorldToGridIndex(float2 worldPos)
    {
        int2 cell = WorldToCell(worldPos);

        if (cell.x < -_gridWidth / 2 || cell.x >= _gridWidth / 2 ||
            cell.y < -_gridHeight / 2 || cell.y >= _gridHeight / 2)
            return -1;

        // Convert from centered coordinates to array index
        int arrayX = cell.x + _gridWidth / 2;
        int arrayY = cell.y + _gridHeight / 2;

        return arrayY * _gridWidth + arrayX;
    }

    /// <summary>
    /// Converts a world position to grid cell coordinates.
    /// Uses Unity's Grid.WorldToCell for accurate isometric coordinate conversion.
    /// </summary>
    public int2 WorldToCell(float2 worldPos)
    {
        if (gridManager != null && gridManager.Grid != null)
        {
            // Use Unity's built-in isometric conversion
            Vector3Int cell = gridManager.Grid.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0));
            return new int2(cell.x, cell.y);
        }

        // Fallback to manual calculation (should match Unity's isometric grid)
        float halfCellX = _cellSizeX * 0.5f;
        float halfCellY = _cellSizeY * 0.5f;

        float isoX = worldPos.x / halfCellX;
        float isoY = worldPos.y / halfCellY;

        int cellX = (int)math.floor((isoX - isoY) * 0.5f);
        int cellY = (int)math.floor((isoX + isoY) * 0.5f);

        return new int2(cellX, cellY);
    }

    /// <summary>
    /// Converts grid cell coordinates to world position (center of cell).
    /// Uses Unity's Grid.GetCellCenterWorld for accurate isometric coordinate conversion.
    /// </summary>
    public float2 CellToWorld(int2 cell)
    {
        if (gridManager != null && gridManager.Grid != null)
        {
            // Use Unity's built-in isometric conversion
            Vector3 worldPos = gridManager.Grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            return new float2(worldPos.x, worldPos.y);
        }

        // Fallback to manual calculation
        float worldX = (cell.x + cell.y) * (_cellSizeX * 0.5f);
        float worldY = (cell.y - cell.x) * (_cellSizeY * 0.5f);

        return new float2(worldX, worldY);
    }

    /// <summary>
    /// Checks if a cell coordinate is within grid bounds.
    /// Cell coordinates are centered (negative to positive range).
    /// </summary>
    public bool IsCellInBounds(int2 cell)
    {
        int halfWidth = _gridWidth / 2;
        int halfHeight = _gridHeight / 2;
        return cell.x >= -halfWidth && cell.x < halfWidth &&
               cell.y >= -halfHeight && cell.y < halfHeight;
    }

    /// <summary>
    /// Converts centered cell coordinates to array index.
    /// </summary>
    public int CellToIndex(int2 cell)
    {
        int arrayX = cell.x + _gridWidth / 2;
        int arrayY = cell.y + _gridHeight / 2;
        return arrayY * _gridWidth + arrayX;
    }

    /// <summary>
    /// Sets the visibility state for a cell at the given index.
    /// Called by FogOfWarSystem.
    /// </summary>
    public void SetVisibility(int index, VisibilityState state)
    {
        if (index >= 0 && index < _visibilityGrid.Length)
        {
            _visibilityGrid[index] = state;
        }
    }

    /// <summary>
    /// Gets the visibility state for a cell at the given index.
    /// </summary>
    public VisibilityState GetVisibility(int index)
    {
        if (index >= 0 && index < _visibilityGrid.Length)
        {
            return _visibilityGrid[index];
        }
        return VisibilityState.Hidden;
    }

    /// <summary>
    /// Decays all Visible cells to Explored.
    /// Called at the start of each visibility update frame.
    /// </summary>
    public void DecayVisibility()
    {
        for (int i = 0; i < _visibilityGrid.Length; i++)
        {
            if (_visibilityGrid[i] == VisibilityState.Visible)
            {
                _visibilityGrid[i] = VisibilityState.Explored;
            }
        }
    }

    private void OnDestroy()
    {
        if (_visibilityGrid.IsCreated)
        {
            _visibilityGrid.Dispose();
        }
        if (_previousVisibilityGrid.IsCreated)
        {
            _previousVisibilityGrid.Dispose();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
