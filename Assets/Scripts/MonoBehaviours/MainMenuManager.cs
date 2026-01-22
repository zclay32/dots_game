using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu UI controller using OnGUI for simple UI rendering.
/// Handles New Game and Exit button clicks.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Game Configuration")]
    [Tooltip("Name of the game scene to load")]
    public string gameSceneName = "SampleScene";

    [Tooltip("Number of soldiers to spawn")]
    public int soldierCount = 100;

    [Tooltip("Number of initial zombies to spawn")]
    public int zombieCount = 10000;

    [Tooltip("Distance from center to map edge")]
    public float mapRadius = 80f;

    [Tooltip("Minimum distance from center for zombie spawns")]
    public float zombieMinDistance = 15f;

    // UI styling
    private GUIStyle _titleStyle;
    private GUIStyle _buttonStyle;
    private bool _stylesInitialized;

    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 72,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _titleStyle.normal.textColor = Color.white;

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold
        };

        _stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitStyles();

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // Title - "DOTS"
        float titleWidth = 400f;
        float titleHeight = 100f;
        float titleX = (screenWidth - titleWidth) / 2f;
        float titleY = screenHeight * 0.2f;

        GUI.Label(new Rect(titleX, titleY, titleWidth, titleHeight), "DOTS", _titleStyle);

        // Buttons
        float buttonWidth = 300f;
        float buttonHeight = 60f;
        float buttonX = (screenWidth - buttonWidth) / 2f;
        float buttonSpacing = 20f;

        // New Game button
        float newGameY = screenHeight * 0.5f;
        if (GUI.Button(new Rect(buttonX, newGameY, buttonWidth, buttonHeight), "New Game", _buttonStyle))
        {
            OnNewGameClicked();
        }

        // Exit button
        float exitY = newGameY + buttonHeight + buttonSpacing;
        if (GUI.Button(new Rect(buttonX, exitY, buttonWidth, buttonHeight), "Exit", _buttonStyle))
        {
            OnExitClicked();
        }

        // Display configuration info at bottom
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.LowerCenter
        };
        infoStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        string configInfo = $"Soldiers: {soldierCount} | Zombies: {zombieCount} | Map Radius: {mapRadius}";
        GUI.Label(new Rect(0, screenHeight - 40f, screenWidth, 30f), configInfo, infoStyle);
    }

    private void OnNewGameClicked()
    {
        // Store configuration in static bridge
        GameConfigBridge.HasConfig = true;
        GameConfigBridge.SoldierCount = soldierCount;
        GameConfigBridge.ZombieCount = zombieCount;
        GameConfigBridge.MapRadius = mapRadius;
        GameConfigBridge.ZombieMinDistance = zombieMinDistance;

        Debug.Log($"[MainMenu] Starting new game: {soldierCount} soldiers, {zombieCount} zombies, radius {mapRadius}");

        // Load game scene
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnExitClicked()
    {
        Debug.Log("[MainMenu] Exit clicked");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
