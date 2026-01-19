using UnityEngine;

/// <summary>
/// Singleton settings for selection circle appearance.
/// Add this to a GameObject in your scene to configure selection circles.
/// </summary>
[AddComponentMenu("TAB Game/Selection Circle Settings")]
public class SelectionCircleSettings : MonoBehaviour
{
    public static SelectionCircleSettings Instance { get; private set; }

    [Header("Circle Appearance")]
    [Tooltip("Radius of the selection circle")]
    public float CircleRadius = 0.8f;

    [Tooltip("Height offset from unit position (increase if circle renders under sprite)")]
    public float CircleHeight = 0.5f;

    [Tooltip("Thickness of the ring (0.1 = thin, 0.5 = thick)")]
    [Range(0.05f, 0.5f)]
    public float CircleThickness = 0.15f;

    [Tooltip("Color of the selection circle")]
    public Color SelectionColor = new Color(0.2f, 1.0f, 0.2f, 0.8f);  // Bright green

    [Header("Performance")]
    [Tooltip("Number of segments in the circle (higher = smoother, lower = better performance)")]
    [Range(8, 64)]
    public int CircleSegments = 32;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
