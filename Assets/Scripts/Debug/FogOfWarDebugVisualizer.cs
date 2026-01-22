using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Debug visualizer for fog of war system.
/// Shows the vision radius and cell positions in the scene view.
/// Works in both edit mode and play mode.
/// </summary>
public class FogOfWarDebugVisualizer : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool showVisionCircle = true;
    public bool showCellCenters = true;
    public float visionRadius = 10f;
    public int cellsToShow = 10;

    [Header("Grid Reference")]
    [Tooltip("Reference to IsometricGridManager - assign for accurate visualization")]
    public IsometricGridManager gridManager;

    [Header("Grid Settings (fallback for edit mode)")]
    [Tooltip("Cell size X - should match IsometricGridManager")]
    public float cellSizeX = 2f;
    [Tooltip("Cell size Y - should match IsometricGridManager")]
    public float cellSizeY = 1f;

    [Header("Colors")]
    public Color visionCircleColor = new Color(0f, 1f, 0f, 0.5f);
    public Color cellCenterColor = new Color(1f, 1f, 0f, 0.8f);
    public Color visibleCellColor = new Color(0f, 1f, 0f, 0.3f);

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;
        float2 worldPos = new float2(pos.x, pos.y);

        // Draw vision ellipse in world space (accounts for isometric aspect ratio)
        if (showVisionCircle)
        {
            // Get aspect ratio for isometric scaling
            float useCellSizeX = cellSizeX;
            float useCellSizeY = cellSizeY;
            if (gridManager == null)
            {
                gridManager = FindFirstObjectByType<IsometricGridManager>();
            }
            if (gridManager != null)
            {
                useCellSizeX = gridManager.cellSize.x;
                useCellSizeY = gridManager.cellSize.y;
            }
            float aspectRatio = useCellSizeX / useCellSizeY;

            Gizmos.color = visionCircleColor;
            // Draw ellipse: full radius horizontally, compressed radius vertically
            DrawWorldSpaceEllipse(pos, visionRadius, visionRadius / aspectRatio, 64);
        }

        // Draw cell centers and which ones are within vision
        if (showCellCenters)
        {
            // Try to find grid manager if not assigned
            if (gridManager == null)
            {
                gridManager = FindFirstObjectByType<IsometricGridManager>();
            }

            // Get aspect ratio for isometric scaling
            float useCellSizeX = cellSizeX;
            float useCellSizeY = cellSizeY;
            if (gridManager != null)
            {
                useCellSizeX = gridManager.cellSize.x;
                useCellSizeY = gridManager.cellSize.y;
            }
            float aspectRatio = useCellSizeX / useCellSizeY;  // 2.0 for standard isometric

            int2 centerCell = WorldToCell(worldPos);
            float visionRadiusSq = visionRadius * visionRadius;

            for (int dy = -cellsToShow; dy <= cellsToShow; dy++)
            {
                for (int dx = -cellsToShow; dx <= cellsToShow; dx++)
                {
                    int2 cell = new int2(centerCell.x + dx, centerCell.y + dy);
                    float2 cellCenter = CellToWorld(cell);
                    Vector3 cellWorldPos = new Vector3(cellCenter.x, cellCenter.y, 0);

                    // Use scaled distance to match FogOfWarSystem calculation
                    float2 delta = cellCenter - worldPos;
                    float scaledDistSq = delta.x * delta.x + (delta.y * aspectRatio) * (delta.y * aspectRatio);
                    bool isVisible = scaledDistSq <= visionRadiusSq;

                    // Draw cell center point
                    Gizmos.color = isVisible ? visibleCellColor : cellCenterColor;
                    Gizmos.DrawSphere(cellWorldPos, isVisible ? 0.15f : 0.08f);

                    // Draw line from this object to cell center for center cell
                    if (dx == 0 && dy == 0)
                    {
                        Gizmos.color = Color.white;
                        Gizmos.DrawLine(pos, cellWorldPos);
                    }
                }
            }
        }
    }

    private int2 WorldToCell(float2 worldPos)
    {
        // Use Unity's Grid if available
        if (gridManager != null && gridManager.Grid != null)
        {
            Vector3Int cell = gridManager.Grid.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0));
            return new int2(cell.x, cell.y);
        }

        // Fallback to manual calculation
        float halfCellX = cellSizeX * 0.5f;
        float halfCellY = cellSizeY * 0.5f;

        float isoX = worldPos.x / halfCellX;
        float isoY = worldPos.y / halfCellY;

        int cellX = (int)math.floor((isoX - isoY) * 0.5f);
        int cellY = (int)math.floor((isoX + isoY) * 0.5f);

        return new int2(cellX, cellY);
    }

    private float2 CellToWorld(int2 cell)
    {
        // Use Unity's Grid if available
        if (gridManager != null && gridManager.Grid != null)
        {
            Vector3 worldPos = gridManager.Grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            return new float2(worldPos.x, worldPos.y);
        }

        // Fallback to manual calculation
        float worldX = (cell.x + cell.y) * (cellSizeX * 0.5f);
        float worldY = (cell.y - cell.x) * (cellSizeY * 0.5f);

        return new float2(worldX, worldY);
    }

    private void DrawWorldSpaceEllipse(Vector3 center, float radiusX, float radiusY, int segments)
    {
        float angleStep = 2f * Mathf.PI / segments;
        Vector3 prevPoint = center + new Vector3(radiusX, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY,
                0
            );
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    private void DrawWorldSpaceCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 2f * Mathf.PI / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0
            );
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
