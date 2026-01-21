using UnityEngine;
using Unity.Entities;

/// <summary>
/// Displays wave information: current wave, countdown timer, spawn direction, and game state.
/// Uses OnGUI for immediate mode rendering, positioned at top-center of screen.
/// </summary>
public class WaveUIManager : MonoBehaviour
{
    [Header("Display Settings")]
    public int fontSize = 28;
    public int announcementFontSize = 36;

    private GUIStyle normalStyle;
    private GUIStyle announcementStyle;
    private GUIStyle victoryStyle;
    private GUIStyle defeatStyle;
    private EntityManager entityManager;
    private EntityQuery waveConfigQuery;
    private EntityQuery waveStateQuery;
    private bool initialized;

    void Start()
    {
        // Normal text style
        normalStyle = new GUIStyle();
        normalStyle.fontSize = fontSize;
        normalStyle.normal.textColor = Color.white;
        normalStyle.fontStyle = FontStyle.Bold;
        normalStyle.alignment = TextAnchor.MiddleCenter;

        // Announcement style (larger, for wave direction)
        announcementStyle = new GUIStyle();
        announcementStyle.fontSize = announcementFontSize;
        announcementStyle.normal.textColor = Color.yellow;
        announcementStyle.fontStyle = FontStyle.Bold;
        announcementStyle.alignment = TextAnchor.MiddleCenter;

        // Victory style
        victoryStyle = new GUIStyle();
        victoryStyle.fontSize = 48;
        victoryStyle.normal.textColor = Color.green;
        victoryStyle.fontStyle = FontStyle.Bold;
        victoryStyle.alignment = TextAnchor.MiddleCenter;

        // Defeat style
        defeatStyle = new GUIStyle();
        defeatStyle.fontSize = 48;
        defeatStyle.normal.textColor = Color.red;
        defeatStyle.fontStyle = FontStyle.Bold;
        defeatStyle.alignment = TextAnchor.MiddleCenter;
    }

    void Update()
    {
        if (!initialized && World.DefaultGameObjectInjectionWorld != null)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            waveConfigQuery = entityManager.CreateEntityQuery(typeof(WaveConfig));
            waveStateQuery = entityManager.CreateEntityQuery(typeof(WaveState));
            initialized = true;
        }
    }

    void OnGUI()
    {
        if (!initialized) return;
        if (waveConfigQuery.CalculateEntityCount() == 0) return;
        if (waveStateQuery.CalculateEntityCount() == 0) return;

        var config = waveConfigQuery.GetSingleton<WaveConfig>();
        var state = waveStateQuery.GetSingleton<WaveState>();

        float centerX = Screen.width / 2f;
        float boxWidth = 400f;
        float boxX = centerX - boxWidth / 2f;

        switch (state.Phase)
        {
            case WavePhase.Countdown:
                DrawCountdown(boxX, boxWidth, config, state);
                break;

            case WavePhase.Spawning:
                DrawSpawning(boxX, boxWidth, config, state);
                break;

            case WavePhase.Active:
                DrawActive(boxX, boxWidth, config, state);
                break;

            case WavePhase.Victory:
                DrawVictory(boxX, boxWidth);
                break;

            case WavePhase.Defeat:
                DrawDefeat(boxX, boxWidth);
                break;
        }
    }

    void DrawCountdown(float boxX, float boxWidth, WaveConfig config, WaveState state)
    {
        int nextWave = state.CurrentWave + 1;

        // Wave info
        string waveText = $"Wave {nextWave} / {config.TotalWaves}";
        DrawTextWithShadow(new Rect(boxX, 10, boxWidth, 40), waveText, normalStyle, Color.white);

        // Countdown timer
        string timerText = $"Starting in {state.Timer:F0}s";
        DrawTextWithShadow(new Rect(boxX, 50, boxWidth, 40), timerText, normalStyle, Color.white);

        // Only show direction after wave 1 has started (direction is pre-picked at end of each wave)
        // During initial countdown (wave 0 -> 1), direction hasn't been picked yet
        if (state.CurrentWave > 0)
        {
            string directionName = GetDirectionName(state.Direction);
            string directionText = $"From the {directionName}!";
            DrawTextWithShadow(new Rect(boxX, 90, boxWidth, 50), directionText, announcementStyle, Color.yellow);
        }
        else
        {
            // Initial countdown - no direction yet
            string prepareText = "Prepare for battle!";
            DrawTextWithShadow(new Rect(boxX, 90, boxWidth, 50), prepareText, announcementStyle, Color.yellow);
        }
    }

    void DrawSpawning(float boxX, float boxWidth, WaveConfig config, WaveState state)
    {
        string directionName = GetDirectionName(state.Direction);

        // Wave info
        string waveText = $"Wave {state.CurrentWave} / {config.TotalWaves}";
        DrawTextWithShadow(new Rect(boxX, 10, boxWidth, 40), waveText, normalStyle, Color.white);

        // Spawning status
        string spawnText = $"Incoming from {directionName}!";
        DrawTextWithShadow(new Rect(boxX, 50, boxWidth, 50), spawnText, announcementStyle, new Color(1f, 0.5f, 0f)); // Orange

        // Zombie count
        string zombieText = $"Zombies: {state.ZombiesAlive}";
        DrawTextWithShadow(new Rect(boxX, 100, boxWidth, 40), zombieText, normalStyle, Color.red);
    }

    void DrawActive(float boxX, float boxWidth, WaveConfig config, WaveState state)
    {
        // Wave info
        string waveText = $"Wave {state.CurrentWave} / {config.TotalWaves}";
        DrawTextWithShadow(new Rect(boxX, 10, boxWidth, 40), waveText, normalStyle, Color.white);

        // Zombie count
        string zombieText = $"Zombies Remaining: {state.ZombiesAlive}";
        Color zombieColor = state.ZombiesAlive <= 10 ? Color.yellow : Color.red;
        DrawTextWithShadow(new Rect(boxX, 50, boxWidth, 40), zombieText, normalStyle, zombieColor);
    }

    void DrawVictory(float boxX, float boxWidth)
    {
        float centerY = Screen.height / 2f - 50f;
        DrawTextWithShadow(new Rect(boxX, centerY, boxWidth, 60), "VICTORY!", victoryStyle, Color.green);
        DrawTextWithShadow(new Rect(boxX, centerY + 60, boxWidth, 40), "All waves survived!", normalStyle, Color.white);
    }

    void DrawDefeat(float boxX, float boxWidth)
    {
        float centerY = Screen.height / 2f - 50f;
        DrawTextWithShadow(new Rect(boxX, centerY, boxWidth, 60), "DEFEAT", defeatStyle, Color.red);
        DrawTextWithShadow(new Rect(boxX, centerY + 60, boxWidth, 40), "All soldiers lost!", normalStyle, Color.white);
    }

    void DrawTextWithShadow(Rect rect, string text, GUIStyle style, Color textColor)
    {
        // Draw shadow
        Rect shadowRect = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
        GUI.color = Color.black;
        GUI.Label(shadowRect, text, style);

        // Draw text
        GUI.color = textColor;
        GUI.Label(rect, text, style);
    }

    string GetDirectionName(SpawnDirection direction)
    {
        return direction switch
        {
            SpawnDirection.North => "NORTH",
            SpawnDirection.South => "SOUTH",
            SpawnDirection.East => "EAST",
            SpawnDirection.West => "WEST",
            _ => "UNKNOWN"
        };
    }
}
