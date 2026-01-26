using Unity.Entities;

/// <summary>
/// Marks an entity as an obstacle that blocks unit movement.
/// Used by ObstacleRegistrationSystem to mark flow field cells as unwalkable,
/// and by ObstacleAvoidanceSystem for direct collision resolution.
///
/// Obstacles are tile-based and centered on the entity position.
/// A 4x4 obstacle centered at (0,0) would block tiles from (-2,-2) to (1,1).
/// </summary>
public struct Obstacle : IComponentData
{
    /// <summary>
    /// Width of the obstacle in tiles.
    /// </summary>
    public int TileWidth;

    /// <summary>
    /// Height of the obstacle in tiles.
    /// </summary>
    public int TileHeight;
}
