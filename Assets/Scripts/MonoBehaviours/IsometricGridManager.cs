using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Manages the isometric grid setup for terrain visualization.
/// Creates a Grid GameObject with terrain and fog overlay tilemaps.
/// The fog overlay is managed by FogOfWarManager.
/// </summary>
public class IsometricGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Size of each cell in world units (X, Y, Z) - Isometric uses 2:1 aspect ratio")]
    public Vector3 cellSize = new Vector3(2f, 1f, 0f);

    [Tooltip("Number of tiles in each direction from center")]
    public int gridRadius = 40;

    [Header("Tile References")]
    [Tooltip("Tile to use for the ground terrain")]
    public TileBase groundTile;

    [Header("Layer Settings")]
    [Tooltip("Sorting layer for terrain tilemap")]
    public string terrainSortingLayer = "Default";

    [Tooltip("Order in layer for terrain (lower = behind)")]
    public int terrainOrderInLayer = -100;

    [Tooltip("Sorting layer for fog overlay tilemap")]
    public string fogSortingLayer = "Default";

    [Tooltip("Order in layer for fog overlay (higher = in front)")]
    public int fogOrderInLayer = 100;

    [Header("Gizmo Settings")]
    [Tooltip("Color for grid lines in Scene view")]
    public Color gizmoGridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

    [Tooltip("Color for grid boundary in Scene view")]
    public Color gizmoBoundaryColor = new Color(1f, 1f, 0f, 0.8f);

    [Tooltip("Show grid lines every N cells (0 = no grid lines, just boundary)")]
    public int gizmoGridStep = 5;

    [Tooltip("Preview sprite to show in gizmo for tile sizing (assign same sprite as ground tile)")]
    public Sprite gizmoPreviewSprite;

    [Tooltip("Number of preview tiles to show in each direction from center")]
    public int gizmoPreviewRadius = 3;

    // References to created objects
    private Grid _grid;
    private Tilemap _terrainTilemap;
    private Tilemap _fogOverlayTilemap;

    public Tilemap TerrainTilemap => _terrainTilemap;
    public Tilemap FogOverlayTilemap => _fogOverlayTilemap;
    public Grid Grid => _grid;

    private void Start()
    {
        SetupGrid();
        SetupTerrainTilemap();
        SetupFogOverlayTilemap();

        if (groundTile != null)
        {
            FillTerrainWithGroundTiles();
        }
        else
        {
            Debug.LogWarning("[IsometricGridManager] No ground tile assigned. Terrain will be empty.");
        }

        Debug.Log($"[IsometricGridManager] Grid setup complete. Size: {gridRadius * 2}x{gridRadius * 2} tiles");
    }

    private void SetupGrid()
    {
        // Create Grid GameObject as child
        var gridObject = new GameObject("Grid");
        gridObject.transform.SetParent(transform);
        gridObject.transform.localPosition = Vector3.zero;

        _grid = gridObject.AddComponent<Grid>();
        _grid.cellSize = cellSize;
        // Use Isometric layout for proper tile-based gameplay (building placement, click detection)
        _grid.cellLayout = GridLayout.CellLayout.Isometric;
        _grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

        Debug.Log($"[IsometricGridManager] Grid created with cell size: {cellSize}, layout: Isometric");
    }

    private void SetupTerrainTilemap()
    {
        // Create Terrain Tilemap as child of Grid
        var terrainObject = new GameObject("TerrainTilemap");
        terrainObject.transform.SetParent(_grid.transform);
        terrainObject.transform.localPosition = Vector3.zero;

        _terrainTilemap = terrainObject.AddComponent<Tilemap>();
        var renderer = terrainObject.AddComponent<TilemapRenderer>();

        // Set sorting
        renderer.sortingLayerName = terrainSortingLayer;
        renderer.sortingOrder = terrainOrderInLayer;

        Debug.Log("[IsometricGridManager] Terrain tilemap created");
    }

    private void SetupFogOverlayTilemap()
    {
        // Create Fog Overlay Tilemap as child of Grid
        var fogObject = new GameObject("FogOverlayTilemap");
        fogObject.transform.SetParent(_grid.transform);
        fogObject.transform.localPosition = Vector3.zero;

        _fogOverlayTilemap = fogObject.AddComponent<Tilemap>();
        var renderer = fogObject.AddComponent<TilemapRenderer>();

        // Set sorting (on top of terrain)
        renderer.sortingLayerName = fogSortingLayer;
        renderer.sortingOrder = fogOrderInLayer;

        Debug.Log("[IsometricGridManager] Fog overlay tilemap created");
    }

    private void FillTerrainWithGroundTiles()
    {
        // Fill the terrain tilemap with ground tiles
        // For isometric grids, cell coordinates still work in a regular grid pattern
        // Unity handles the visual transformation to isometric projection
        for (int x = -gridRadius; x < gridRadius; x++)
        {
            for (int y = -gridRadius; y < gridRadius; y++)
            {
                _terrainTilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
        }

        int totalTiles = gridRadius * 2 * gridRadius * 2;
        Debug.Log($"[IsometricGridManager] Filled terrain with {totalTiles} ground tiles");
    }

    /// <summary>
    /// Converts a world position to a cell position.
    /// </summary>
    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        if (_grid == null) return Vector3Int.zero;
        return _grid.WorldToCell(worldPosition);
    }

    /// <summary>
    /// Converts a cell position to world position (center of cell).
    /// </summary>
    public Vector3 CellToWorld(Vector3Int cellPosition)
    {
        if (_grid == null) return Vector3.zero;
        return _grid.GetCellCenterWorld(cellPosition);
    }

    /// <summary>
    /// Gets the grid dimensions.
    /// </summary>
    public Vector2Int GetGridDimensions()
    {
        return new Vector2Int(gridRadius * 2, gridRadius * 2);
    }

    /// <summary>
    /// Draws the isometric grid as gizmos when the object is selected.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        DrawIsometricGridGizmo();
    }

    /// <summary>
    /// Draws the isometric grid boundary even when not selected (optional).
    /// Uncomment to always show boundary.
    /// </summary>
    // private void OnDrawGizmos()
    // {
    //     Gizmos.color = gizmoBoundaryColor;
    //     DrawGridBoundary();
    // }

    private void DrawIsometricGridGizmo()
    {
        // For isometric grids, cells are diamond-shaped
        // Cell (0,0) is at origin, cells extend in X and Y directions
        // The visual position of a cell is transformed by the isometric projection

        // Draw preview tiles first (so grid lines draw on top)
        if (gizmoPreviewSprite != null)
        {
            DrawPreviewTiles();
        }

        // Draw boundary
        Gizmos.color = gizmoBoundaryColor;
        DrawGridBoundary();

        // Draw grid lines
        if (gizmoGridStep > 0)
        {
            Gizmos.color = gizmoGridColor;
            DrawGridLines();
        }
    }

    private void DrawGridBoundary()
    {
        // Get the four corners of the grid in cell coordinates
        int minCell = -gridRadius;
        int maxCell = gridRadius;

        // Convert corner cells to world positions
        // For isometric: world position is a function of cell x and y
        Vector3 bottomLeft = CellToWorldGizmo(minCell, minCell);
        Vector3 bottomRight = CellToWorldGizmo(maxCell, minCell);
        Vector3 topRight = CellToWorldGizmo(maxCell, maxCell);
        Vector3 topLeft = CellToWorldGizmo(minCell, maxCell);

        // Draw boundary lines
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }

    private void DrawGridLines()
    {
        int minCell = -gridRadius;
        int maxCell = gridRadius;

        // Draw vertical grid lines (constant X)
        for (int x = minCell; x <= maxCell; x += gizmoGridStep)
        {
            Vector3 start = CellToWorldGizmo(x, minCell);
            Vector3 end = CellToWorldGizmo(x, maxCell);
            Gizmos.DrawLine(start, end);
        }

        // Draw horizontal grid lines (constant Y)
        for (int y = minCell; y <= maxCell; y += gizmoGridStep)
        {
            Vector3 start = CellToWorldGizmo(minCell, y);
            Vector3 end = CellToWorldGizmo(maxCell, y);
            Gizmos.DrawLine(start, end);
        }
    }

    /// <summary>
    /// Converts cell coordinates to world position for gizmo drawing.
    /// Uses the configured cell size to compute isometric projection.
    /// </summary>
    private Vector3 CellToWorldGizmo(int cellX, int cellY)
    {
        // Isometric projection formula:
        // worldX = (cellX + cellY) * (cellSize.x / 2)
        // worldY = (cellY - cellX) * (cellSize.y / 2)
        // This creates the diamond pattern typical of isometric grids
        float worldX = (cellX + cellY) * (cellSize.x / 2f);
        float worldY = (cellY - cellX) * (cellSize.y / 2f);
        return transform.position + new Vector3(worldX, worldY, 0f);
    }

    /// <summary>
    /// Draws preview tiles using the assigned sprite to verify sizing.
    /// Shows diamond outlines for expected cell boundaries and the actual sprite size.
    /// </summary>
    private void DrawPreviewTiles()
    {
        if (gizmoPreviewSprite == null) return;

        // Calculate the world size of the sprite based on its pixels per unit
        float spriteWorldWidth = gizmoPreviewSprite.rect.width / gizmoPreviewSprite.pixelsPerUnit;
        float spriteWorldHeight = gizmoPreviewSprite.rect.height / gizmoPreviewSprite.pixelsPerUnit;

        // Draw a small grid of preview tiles around origin
        for (int x = -gizmoPreviewRadius; x <= gizmoPreviewRadius; x++)
        {
            for (int y = -gizmoPreviewRadius; y <= gizmoPreviewRadius; y++)
            {
                Vector3 cellCenter = CellToWorldGizmo(x, y);

                // Draw expected cell diamond outline (cyan)
                DrawCellDiamondOutline(cellCenter, new Color(0f, 1f, 1f, 0.8f));

                // Draw sprite bounds rectangle (magenta) to compare
                DrawSpriteBoundsOutline(cellCenter, spriteWorldWidth, spriteWorldHeight, new Color(1f, 0f, 1f, 0.6f));
            }
        }

        // Log sizing info for debugging
        Debug.Log($"[IsometricGridManager Gizmo] Cell size: {cellSize.x}x{cellSize.y}, " +
                  $"Sprite size: {spriteWorldWidth:F2}x{spriteWorldHeight:F2} " +
                  $"(PPU: {gizmoPreviewSprite.pixelsPerUnit}, pixels: {gizmoPreviewSprite.rect.width}x{gizmoPreviewSprite.rect.height})");
    }

    /// <summary>
    /// Draws diamond outline showing where an isometric cell should be.
    /// </summary>
    private void DrawCellDiamondOutline(Vector3 center, Color color)
    {
        Gizmos.color = color;

        // Diamond vertices based on cell size
        float halfWidth = cellSize.x * 0.5f;
        float halfHeight = cellSize.y * 0.5f;

        Vector3 top = center + new Vector3(0, halfHeight, 0);
        Vector3 right = center + new Vector3(halfWidth, 0, 0);
        Vector3 bottom = center + new Vector3(0, -halfHeight, 0);
        Vector3 left = center + new Vector3(-halfWidth, 0, 0);

        Gizmos.DrawLine(top, right);
        Gizmos.DrawLine(right, bottom);
        Gizmos.DrawLine(bottom, left);
        Gizmos.DrawLine(left, top);
    }

    /// <summary>
    /// Draws a rectangle outline showing the sprite's actual world bounds.
    /// </summary>
    private void DrawSpriteBoundsOutline(Vector3 center, float width, float height, Color color)
    {
        Gizmos.color = color;

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        Vector3 topLeft = center + new Vector3(-halfWidth, halfHeight, 0);
        Vector3 topRight = center + new Vector3(halfWidth, halfHeight, 0);
        Vector3 bottomRight = center + new Vector3(halfWidth, -halfHeight, 0);
        Vector3 bottomLeft = center + new Vector3(-halfWidth, -halfHeight, 0);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }
}
