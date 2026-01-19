using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor menu helper to easily create Selection Circle Settings
/// </summary>
public static class SelectionCircleMenu
{
    [MenuItem("GameObject/TAB Game/Selection Circle Settings", false, 10)]
    public static void CreateSelectionCircleSettings(MenuCommand menuCommand)
    {
        // Check if one already exists
        var existing = Object.FindFirstObjectByType<SelectionCircleSettings>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog(
                "Selection Circle Settings Already Exists",
                "A SelectionCircleSettings component already exists in the scene.\n\nYou only need one per scene.",
                "OK"
            );
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            return;
        }

        // Create new GameObject with the component
        GameObject go = new GameObject("SelectionCircleSettings");
        go.AddComponent<SelectionCircleSettings>();

        // Register for undo
        Undo.RegisterCreatedObjectUndo(go, "Create Selection Circle Settings");

        // Select it
        Selection.activeGameObject = go;

        Debug.Log("SelectionCircleSettings created! Adjust the settings in the Inspector.");
    }
}
