using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A simple tile that renders as a solid color.
/// Used for ground terrain and fog of war overlays.
/// </summary>
[CreateAssetMenu(fileName = "NewColoredTile", menuName = "TAB Game/Colored Tile")]
public class ColoredTile : TileBase
{
    [Tooltip("The color to fill this tile with")]
    public Color color = Color.white;

    [Tooltip("Optional sprite to use (if null, uses a simple quad)")]
    public Sprite sprite;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.color = color;

        if (sprite != null)
        {
            tileData.sprite = sprite;
        }
        else
        {
            // Use Unity's default white square sprite
            tileData.sprite = GetDefaultSprite();
        }
    }

    private static Sprite _defaultSprite;

    private static Sprite GetDefaultSprite()
    {
        if (_defaultSprite == null)
        {
            // Create a simple 1x1 white texture
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _defaultSprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f
            );
        }
        return _defaultSprite;
    }
}
