using UnityEngine;
using Unity.Entities;

/// <summary>
/// Displays a loading screen while game systems initialize.
/// Uses OnGUI for immediate mode rendering, covers entire screen.
/// Tracks: IsometricGridManager, FogOfWarManager, ECS World, unit spawning.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    [Header("Display Settings")]
    [Tooltip("Duration of fade-out transition in seconds")]
    public float fadeDuration = 0.5f;

    [Tooltip("Font size for main loading text")]
    public int mainFontSize = 48;

    [Tooltip("Font size for status text")]
    public int statusFontSize = 24;

    [Header("Colors")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.95f);
    public Color textColor = Color.white;

    private enum LoadingPhase
    {
        WaitingForGrid,
        WaitingForFog,
        WaitingForECS,
        WaitingForConfig,
        WaitingForUnits,
        FadingOut,
        Complete
    }

    private LoadingPhase _phase = LoadingPhase.WaitingForGrid;
    private float _fadeTimer;
    private Texture2D _backgroundTexture;
    private GUIStyle _mainStyle;
    private GUIStyle _statusStyle;

    // ECS references (cached)
    private EntityManager _entityManager;
    private EntityQuery _gameConfigQuery;
    private EntityQuery _playerUnitQuery;
    private bool _ecsInitialized;

    void Start()
    {
        // Create background texture (1x1 white pixel)
        _backgroundTexture = new Texture2D(1, 1);
        _backgroundTexture.SetPixel(0, 0, Color.white);
        _backgroundTexture.Apply();

        // Main loading text style
        _mainStyle = new GUIStyle();
        _mainStyle.fontSize = mainFontSize;
        _mainStyle.fontStyle = FontStyle.Bold;
        _mainStyle.alignment = TextAnchor.MiddleCenter;
        _mainStyle.normal.textColor = Color.white;

        // Status text style
        _statusStyle = new GUIStyle();
        _statusStyle.fontSize = statusFontSize;
        _statusStyle.fontStyle = FontStyle.Normal;
        _statusStyle.alignment = TextAnchor.MiddleCenter;
        _statusStyle.normal.textColor = Color.white;

        Debug.Log("[LoadingScreenManager] Initialized");
    }

    void Update()
    {
        if (_phase == LoadingPhase.Complete) return;

        // Initialize ECS queries once world is available
        if (!_ecsInitialized && World.DefaultGameObjectInjectionWorld != null)
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _gameConfigQuery = _entityManager.CreateEntityQuery(typeof(GameConfig));
            _playerUnitQuery = _entityManager.CreateEntityQuery(typeof(PlayerUnit));
            _ecsInitialized = true;
            Debug.Log("[LoadingScreenManager] ECS queries initialized");
        }

        // State machine for loading phases
        switch (_phase)
        {
            case LoadingPhase.WaitingForGrid:
                if (IsometricGridManager.Instance != null && IsometricGridManager.Instance.Grid != null)
                {
                    Debug.Log("[LoadingScreenManager] Grid ready");
                    _phase = LoadingPhase.WaitingForFog;
                }
                break;

            case LoadingPhase.WaitingForFog:
                if (FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized)
                {
                    Debug.Log("[LoadingScreenManager] Fog of war ready");
                    _phase = LoadingPhase.WaitingForECS;
                }
                break;

            case LoadingPhase.WaitingForECS:
                if (_ecsInitialized)
                {
                    Debug.Log("[LoadingScreenManager] ECS world ready");
                    _phase = LoadingPhase.WaitingForConfig;
                }
                break;

            case LoadingPhase.WaitingForConfig:
                if (_gameConfigQuery.CalculateEntityCount() > 0)
                {
                    Debug.Log("[LoadingScreenManager] GameConfig ready");
                    _phase = LoadingPhase.WaitingForUnits;
                }
                break;

            case LoadingPhase.WaitingForUnits:
                if (_playerUnitQuery.CalculateEntityCount() > 0)
                {
                    Debug.Log("[LoadingScreenManager] Units spawned, starting fade out");
                    _phase = LoadingPhase.FadingOut;
                    _fadeTimer = fadeDuration;
                }
                break;

            case LoadingPhase.FadingOut:
                _fadeTimer -= Time.deltaTime;
                if (_fadeTimer <= 0f)
                {
                    Debug.Log("[LoadingScreenManager] Loading complete");
                    _phase = LoadingPhase.Complete;
                }
                break;
        }
    }

    void OnGUI()
    {
        if (_phase == LoadingPhase.Complete) return;

        // Ensure loading screen renders on top
        GUI.depth = -1000;

        // Calculate alpha for fade
        float alpha = _phase == LoadingPhase.FadingOut
            ? _fadeTimer / fadeDuration
            : 1f;

        // Draw full-screen background
        Color bgColor = backgroundColor;
        bgColor.a *= alpha;
        GUI.color = bgColor;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _backgroundTexture);

        // Only draw text if still reasonably visible
        if (alpha > 0.2f)
        {
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;
            float boxWidth = 600f;
            float boxX = centerX - boxWidth / 2f;

            // Main "Loading..." text
            string loadingText = GetLoadingText();
            Color mainColor = textColor;
            mainColor.a = alpha;
            DrawTextWithShadow(new Rect(boxX, centerY - 40, boxWidth, 60), loadingText, _mainStyle, mainColor, alpha);

            // Status text below
            string statusText = GetStatusText();
            Color statusColor = new Color(0.7f, 0.7f, 0.7f, alpha);
            DrawTextWithShadow(new Rect(boxX, centerY + 30, boxWidth, 40), statusText, _statusStyle, statusColor, alpha);
        }
    }

    private string GetLoadingText()
    {
        return _phase switch
        {
            LoadingPhase.FadingOut => "Ready!",
            _ => "Loading..."
        };
    }

    private string GetStatusText()
    {
        return _phase switch
        {
            LoadingPhase.WaitingForGrid => "Initializing grid...",
            LoadingPhase.WaitingForFog => "Generating fog of war...",
            LoadingPhase.WaitingForECS => "Starting game world...",
            LoadingPhase.WaitingForConfig => "Loading configuration...",
            LoadingPhase.WaitingForUnits => "Spawning units...",
            LoadingPhase.FadingOut => "",
            _ => ""
        };
    }

    private void DrawTextWithShadow(Rect rect, string text, GUIStyle style, Color textColor, float alpha)
    {
        // Draw shadow
        Rect shadowRect = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
        GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
        GUI.Label(shadowRect, text, style);

        // Draw text
        GUI.color = textColor;
        GUI.Label(rect, text, style);
    }

    void OnDestroy()
    {
        // Clean up texture
        if (_backgroundTexture != null)
        {
            Destroy(_backgroundTexture);
        }
    }
}
