using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Manages the isometric grid and tilemaps for the game.
/// Creates and configures the grid hierarchy at runtime.
///
/// Cell size is (2, 1.5, 0) to match They Are Billions style.
/// </summary>
public class IsometricGridManager : MonoBehaviour
{
    public static IsometricGridManager Instance { get; private set; }

    [Header("Grid Settings")]
    [Tooltip("Size of each cell in world units")]
    public Vector3 cellSize = new Vector3(2f, 1.5f, 0f);

    [Header("Map Settings")]
    [Tooltip("Radius of the map in tiles (cells) from center")]
    public int mapRadiusInTiles = 80;

    [Header("Tile References")]
    [Tooltip("The tile to use for ground")]
    public TileBase groundTile;

    [Header("Runtime References (Auto-created)")]
    public Grid Grid { get; private set; }
    public Tilemap GroundTilemap { get; private set; }

    // Calculated grid bounds in cell coordinates
    public int MinCellX { get; private set; }
    public int MaxCellX { get; private set; }
    public int MinCellY { get; private set; }
    public int MaxCellY { get; private set; }

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
        CreateGridHierarchy();
        CalculateGridBounds();
        FillGroundTiles();
    }

    private void CreateGridHierarchy()
    {
        // Create Grid component on this GameObject
        Grid = gameObject.AddComponent<Grid>();
        Grid.cellLayout = GridLayout.CellLayout.Isometric;
        Grid.cellSize = cellSize;
        Grid.cellGap = Vector3.zero;
        Grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

        // Create Ground Tilemap as child
        var groundObj = new GameObject("GroundTilemap");
        groundObj.transform.SetParent(transform);
        groundObj.transform.localPosition = Vector3.zero;

        GroundTilemap = groundObj.AddComponent<Tilemap>();
        var groundRenderer = groundObj.AddComponent<TilemapRenderer>();
        groundRenderer.sortingOrder = -100;  // Render behind units

        Debug.Log($"[IsometricGridManager] Grid created with cell size {cellSize}");
    }

    private void CalculateGridBounds()
    {
        // Use tile-based radius for cell bounds
        // This creates a diamond shape with mapRadiusInTiles cells in each cardinal direction
        MinCellX = -mapRadiusInTiles;
        MaxCellX = mapRadiusInTiles;
        MinCellY = -mapRadiusInTiles;
        MaxCellY = mapRadiusInTiles;

        int width = MaxCellX - MinCellX + 1;
        int height = MaxCellY - MinCellY + 1;

        Debug.Log($"[IsometricGridManager] Grid bounds: X[{MinCellX}, {MaxCellX}] Y[{MinCellY}, {MaxCellY}] ({width}x{height} cells)");
    }

    private void FillGroundTiles()
    {
        if (groundTile == null)
        {
            Debug.LogWarning("[IsometricGridManager] No ground tile assigned!");
            return;
        }

        int tilesPlaced = 0;

        for (int y = MinCellY; y <= MaxCellY; y++)
        {
            for (int x = MinCellX; x <= MaxCellX; x++)
            {
                // Use Manhattan distance (diamond shape) in cell space
                // This gives us mapRadiusInTiles cells in each cardinal direction
                int manhattanDist = Mathf.Abs(x) + Mathf.Abs(y);
                if (manhattanDist <= mapRadiusInTiles)
                {
                    Vector3Int cellPos = new Vector3Int(x, y, 0);
                    GroundTilemap.SetTile(cellPos, groundTile);
                    tilesPlaced++;
                }
            }
        }

        Debug.Log($"[IsometricGridManager] Placed {tilesPlaced} ground tiles");
    }

    /// <summary>
    /// Convert world position to cell coordinates
    /// </summary>
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        return Grid.WorldToCell(worldPos);
    }

    /// <summary>
    /// Convert cell coordinates to world position (cell center)
    /// </summary>
    public Vector3 CellToWorld(Vector3Int cell)
    {
        return Grid.GetCellCenterWorld(cell);
    }

    /// <summary>
    /// Check if a world position is within the map bounds
    /// </summary>
    public bool IsInMapBounds(Vector3 worldPos)
    {
        Vector3Int cell = Grid.WorldToCell(worldPos);
        int manhattanDist = Mathf.Abs(cell.x) + Mathf.Abs(cell.y);
        return manhattanDist <= mapRadiusInTiles;
    }

    /// <summary>
    /// Check if a cell coordinate is within the grid bounds
    /// </summary>
    public bool IsCellInBounds(Vector3Int cell)
    {
        return cell.x >= MinCellX && cell.x <= MaxCellX &&
               cell.y >= MinCellY && cell.y <= MaxCellY;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
