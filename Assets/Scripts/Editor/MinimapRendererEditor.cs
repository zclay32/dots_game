using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for MinimapRenderer that draws a preview gizmo in the Scene view.
/// Shows the minimap bounds as a rectangle with position label.
/// Useful for planning UI artwork around the minimap.
/// </summary>
[CustomEditor(typeof(MinimapRenderer))]
public class MinimapRendererEditor : Editor
{
    private void OnSceneGUI()
    {
        MinimapRenderer minimap = (MinimapRenderer)target;

        // Draw screen-space overlay in Scene view
        Handles.BeginGUI();

        // Calculate minimap rect (same logic as MinimapRenderer)
        float x, y;
        float size = minimap.minimapSize;
        float padding = minimap.minimapPadding;

        // Get the Scene view's screen dimensions
        SceneView sceneView = SceneView.currentDrawingSceneView;
        if (sceneView == null)
        {
            Handles.EndGUI();
            return;
        }

        float screenWidth = sceneView.position.width;
        float screenHeight = sceneView.position.height;

        switch (minimap.position)
        {
            case MinimapRenderer.MinimapPosition.TopLeft:
                x = padding;
                y = padding;
                break;
            case MinimapRenderer.MinimapPosition.TopRight:
                x = screenWidth - size - padding;
                y = padding;
                break;
            case MinimapRenderer.MinimapPosition.BottomRight:
                x = screenWidth - size - padding;
                y = screenHeight - size - padding;
                break;
            case MinimapRenderer.MinimapPosition.BottomLeft:
            default:
                x = padding;
                y = screenHeight - size - padding;
                break;
        }

        Rect minimapRect = new Rect(x, y, size, size);

        // Draw semi-transparent background
        Color bgColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        EditorGUI.DrawRect(minimapRect, bgColor);

        // Draw border
        Color borderColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        float borderWidth = minimap.borderWidth;

        // Top
        EditorGUI.DrawRect(new Rect(x, y, size, borderWidth), borderColor);
        // Bottom
        EditorGUI.DrawRect(new Rect(x, y + size - borderWidth, size, borderWidth), borderColor);
        // Left
        EditorGUI.DrawRect(new Rect(x, y, borderWidth, size), borderColor);
        // Right
        EditorGUI.DrawRect(new Rect(x + size - borderWidth, y, borderWidth, size), borderColor);

        // Draw label
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
        labelStyle.normal.textColor = Color.white;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        Rect labelRect = new Rect(x, y + size / 2 - 20, size, 20);
        GUI.Label(labelRect, "MINIMAP", labelStyle);

        // Draw size info
        GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel);
        infoStyle.normal.textColor = Color.gray;
        infoStyle.alignment = TextAnchor.MiddleCenter;

        Rect infoRect = new Rect(x, y + size / 2, size, 20);
        GUI.Label(infoRect, $"{size}x{size}px", infoStyle);

        // Draw position indicator
        Rect posRect = new Rect(x, y + size / 2 + 15, size, 20);
        GUI.Label(posRect, minimap.position.ToString(), infoStyle);

        Handles.EndGUI();
    }
}
