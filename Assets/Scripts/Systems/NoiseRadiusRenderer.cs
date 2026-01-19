using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders noise radius circles as expanding/fading rings for debugging.
/// Similar pattern to DeathEffectRenderer - uses object pooling with SpriteRenderer.
/// </summary>
public class NoiseRadiusRenderer : MonoBehaviour
{
    [Header("Circle Settings")]
    [Tooltip("Enable/disable noise visualization")]
    public bool showNoiseRadius = true;

    [Tooltip("Color of the noise radius circle")]
    public Color noiseColor = new Color(1f, 0.8f, 0.2f, 0.5f);

    [Tooltip("How long the circle is visible")]
    public float fadeDuration = 0.5f;

    [Tooltip("Circle starts at this fraction of max radius")]
    [Range(0f, 1f)]
    public float startRadiusScale = 0.2f;

    [Tooltip("Ring thickness as fraction of radius")]
    [Range(0.01f, 0.2f)]
    public float ringThickness = 0.05f;

    [Header("Performance")]
    [Tooltip("Maximum circles to render at once")]
    public int maxCircles = 20;

    // Object pool for circles
    private List<GameObject> _circlePool = new List<GameObject>();
    private List<SpriteRenderer> _circleRenderers = new List<SpriteRenderer>();
    private List<float> _circleTimers = new List<float>();
    private List<float> _circleMaxRadii = new List<float>();
    private List<Color> _circleColors = new List<Color>();
    private int _poolIndex = 0;

    // Ring mesh for rendering (generated procedurally)
    private Mesh _ringMesh;
    private Material _ringMaterial;

    void Start()
    {
        NoiseVisualizationManager.Initialize();
        CreateRingMesh();
        CreatePool();
    }

    void CreateRingMesh()
    {
        // Create a simple quad mesh - we'll use a shader or sprite for the ring
        _ringMesh = new Mesh();

        // Create a ring mesh with vertices
        int segments = 64;
        float innerRadius = 1f - ringThickness;
        float outerRadius = 1f;

        Vector3[] vertices = new Vector3[segments * 2];
        int[] triangles = new int[segments * 6];
        Vector2[] uvs = new Vector2[segments * 2];

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Inner vertex
            vertices[i * 2] = new Vector3(cos * innerRadius, sin * innerRadius, 0);
            uvs[i * 2] = new Vector2(0, (float)i / segments);

            // Outer vertex
            vertices[i * 2 + 1] = new Vector3(cos * outerRadius, sin * outerRadius, 0);
            uvs[i * 2 + 1] = new Vector2(1, (float)i / segments);

            // Triangles
            int ti = i * 6;
            int vi = i * 2;
            int nextVi = ((i + 1) % segments) * 2;

            triangles[ti] = vi;
            triangles[ti + 1] = vi + 1;
            triangles[ti + 2] = nextVi;

            triangles[ti + 3] = nextVi;
            triangles[ti + 4] = vi + 1;
            triangles[ti + 5] = nextVi + 1;
        }

        _ringMesh.vertices = vertices;
        _ringMesh.triangles = triangles;
        _ringMesh.uv = uvs;
        _ringMesh.RecalculateNormals();
        _ringMesh.RecalculateBounds();

        // Create material - use a simple unlit transparent shader
        _ringMaterial = new Material(Shader.Find("Sprites/Default"));
        _ringMaterial.color = noiseColor;
    }

    void CreatePool()
    {
        for (int i = 0; i < maxCircles; i++)
        {
            CreateCircleObject();
        }
    }

    GameObject CreateCircleObject()
    {
        GameObject obj = new GameObject($"NoiseRadius_{_circlePool.Count}");
        obj.transform.SetParent(transform);
        obj.SetActive(false);

        // Add mesh filter and renderer for ring
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.mesh = _ringMesh;

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.material = new Material(_ringMaterial);
        mr.sortingOrder = -2;  // Behind blood pools

        // We'll store the MeshRenderer in place of SpriteRenderer
        // Using a wrapper approach for compatibility
        _circlePool.Add(obj);
        _circleRenderers.Add(null);  // Not using SpriteRenderer
        _circleTimers.Add(-1f);
        _circleMaxRadii.Add(0f);
        _circleColors.Add(noiseColor);

        return obj;
    }

    void Update()
    {
        if (!showNoiseRadius)
            return;

        // Process pending visualization events
        while (NoiseVisualizationManager.TryDequeue(out var viz))
        {
            SpawnCircle(new Vector3(viz.Position.x, viz.Position.y, 0), viz.MaxRadius, viz.Intensity);
        }

        // Update active circles
        UpdateCircles();
    }

    void UpdateCircles()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < _circlePool.Count; i++)
        {
            if (_circleTimers[i] < 0) continue;

            _circleTimers[i] += dt;
            float t = _circleTimers[i] / fadeDuration;

            if (t >= 1f)
            {
                _circlePool[i].SetActive(false);
                _circleTimers[i] = -1f;
                continue;
            }

            // Expand from startRadiusScale to 1.0
            float radiusScale = Mathf.Lerp(startRadiusScale, 1f, t);
            float currentRadius = _circleMaxRadii[i] * radiusScale;
            _circlePool[i].transform.localScale = new Vector3(currentRadius, currentRadius, 1f);

            // Fade out alpha
            var mr = _circlePool[i].GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Color color = _circleColors[i];
                color.a = Mathf.Lerp(_circleColors[i].a, 0f, t * t);  // Quadratic fade
                mr.material.color = color;
            }
        }
    }

    void SpawnCircle(Vector3 position, float maxRadius, float intensity)
    {
        int index = FindInactiveOrCreate();

        _circlePool[index].transform.position = position;
        float startRadius = maxRadius * startRadiusScale;
        _circlePool[index].transform.localScale = new Vector3(startRadius, startRadius, 1f);
        _circlePool[index].SetActive(true);

        // Set color based on intensity
        Color color = noiseColor;
        color.a = Mathf.Clamp(noiseColor.a * intensity, 0.2f, 0.8f);
        _circleColors[index] = color;
        _circleMaxRadii[index] = maxRadius;
        _circleTimers[index] = 0f;

        var mr = _circlePool[index].GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.material.color = color;
        }
    }

    int FindInactiveOrCreate()
    {
        int startIndex = _poolIndex;
        do
        {
            if (_circleTimers[_poolIndex] < 0)
            {
                int found = _poolIndex;
                _poolIndex = (_poolIndex + 1) % _circlePool.Count;
                return found;
            }
            _poolIndex = (_poolIndex + 1) % _circlePool.Count;
        }
        while (_poolIndex != startIndex);

        // Pool full - reuse oldest (or create if under limit)
        if (_circlePool.Count < maxCircles)
        {
            CreateCircleObject();
            return _circlePool.Count - 1;
        }

        // Reuse oldest
        return _poolIndex;
    }

    void OnDestroy()
    {
        NoiseVisualizationManager.Dispose();

        if (_ringMesh != null)
            Destroy(_ringMesh);
        if (_ringMaterial != null)
            Destroy(_ringMaterial);
    }
}
