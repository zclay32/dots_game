using UnityEngine;

/// <summary>
/// Visual ping effect that shows where units were commanded to move
/// Expands and fades out over time
/// </summary>
public class MovePing : MonoBehaviour
{
    [Header("Ping Settings")]
    public float duration = 0.5f;
    public float startScale = 0.5f;
    public float endScale = 2f;
    public Color pingColor = new Color(0f, 1f, 0f, 0.8f);
    
    private float _timer;
    private SpriteRenderer _spriteRenderer;
    private float _initialAlpha;
    private bool _initialized;
    
    public void Initialize(Vector3 position, Color color, float dur, float start, float end)
    {
        transform.position = position;
        pingColor = color;
        duration = dur;
        startScale = start;
        endScale = end;
        _initialAlpha = color.a;
        _timer = 0f;
        
        // Get or add sprite renderer
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
        {
            _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        // Set initial state
        transform.localScale = Vector3.one * startScale;
        _spriteRenderer.color = pingColor;
        
        _initialized = true;
    }
    
    void Update()
    {
        if (!_initialized) return;
        
        _timer += Time.deltaTime;
        float t = _timer / duration;
        
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        
        // Scale up
        float scale = Mathf.Lerp(startScale, endScale, t);
        transform.localScale = Vector3.one * scale;
        
        // Fade out
        if (_spriteRenderer != null)
        {
            Color color = pingColor;
            color.a = Mathf.Lerp(_initialAlpha, 0f, t);
            _spriteRenderer.color = color;
        }
    }
    
    void OnDestroy()
    {
        // Cleanup
    }
    
    // Safety: destroy after max time in case something goes wrong
    void Start()
    {
        Destroy(gameObject, duration + 0.5f);
    }
}
