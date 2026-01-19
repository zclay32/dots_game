using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

/// <summary>
/// Renders death effects - a burst, blood particles, and a fading blob
/// </summary>
public class DeathEffectRenderer : MonoBehaviour
{
    [Header("Burst Settings")]
    public Sprite burstSprite;
    public Color enemyBurstColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color soldierBurstColor = new Color(0.2f, 0.5f, 1f, 1f);
    public float burstDuration = 0.15f;
    public float burstStartScale = 0.3f;
    public float burstEndScale = 1.2f;

    [Header("Blob Settings")]
    public Sprite blobSprite;
    public Color enemyBlobColor = new Color(0.6f, 0.1f, 0.1f, 0.8f);
    public Color soldierBlobColor = new Color(0.1f, 0.2f, 0.5f, 0.8f);
    public float blobDuration = 2f;
    public float blobScale = 0.5f;

    [Header("Blood Particle Settings")]
    public Sprite particleSprite;
    public Color enemyParticleColor = new Color(0.8f, 0.1f, 0.1f, 1f);
    public Color soldierParticleColor = new Color(0.1f, 0.3f, 0.8f, 1f);
    public int particleCount = 8;
    public float particleSpeed = 8f;
    public float particleSpeedVariance = 3f;
    public float particleDuration = 0.6f;
    public float particleStartScale = 0.4f;
    public float particleEndScale = 0.1f;
    public float particleSpreadAngle = 60f;  // Degrees - how wide the spray is
    public float knockbackStrength = 0.3f;   // How much particles favor the impact direction
    
    // Object pooling for bursts
    private List<GameObject> _burstPool = new List<GameObject>();
    private List<SpriteRenderer> _burstRenderers = new List<SpriteRenderer>();
    private List<float> _burstTimers = new List<float>();
    private List<Color> _burstColors = new List<Color>();
    private int _burstPoolIndex = 0;
    
    // Object pooling for blobs
    private List<GameObject> _blobPool = new List<GameObject>();
    private List<SpriteRenderer> _blobRenderers = new List<SpriteRenderer>();
    private List<float> _blobTimers = new List<float>();
    private List<Color> _blobColors = new List<Color>();
    private int _blobPoolIndex = 0;

    // Object pooling for blood particles
    private List<GameObject> _particlePool = new List<GameObject>();
    private List<SpriteRenderer> _particleRenderers = new List<SpriteRenderer>();
    private List<float> _particleTimers = new List<float>();
    private List<Color> _particleColors = new List<Color>();
    private List<Vector2> _particleVelocities = new List<Vector2>();
    private int _particlePoolIndex = 0;

    private int _initialPoolSize = 50;
    private int _initialParticlePoolSize = 200;  // More particles needed
    
    void Start()
    {
        DeathEffectManager.Initialize();
        CreatePools();
    }
    
    void CreatePools()
    {
        for (int i = 0; i < _initialPoolSize; i++)
        {
            CreateBurstObject();
            CreateBlobObject();
        }

        for (int i = 0; i < _initialParticlePoolSize; i++)
        {
            CreateParticleObject();
        }
    }
    
    GameObject CreateBurstObject()
    {
        GameObject obj = new GameObject($"DeathBurst_{_burstPool.Count}");
        obj.transform.SetParent(transform);
        obj.SetActive(false);
        
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = burstSprite;
        sr.sortingOrder = 90;
        
        _burstPool.Add(obj);
        _burstRenderers.Add(sr);
        _burstTimers.Add(-1f);
        _burstColors.Add(Color.white);
        
        return obj;
    }
    
    GameObject CreateBlobObject()
    {
        GameObject obj = new GameObject($"DeathBlob_{_blobPool.Count}");
        obj.transform.SetParent(transform);
        obj.SetActive(false);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = blobSprite;
        sr.sortingOrder = -1; // Behind units

        _blobPool.Add(obj);
        _blobRenderers.Add(sr);
        _blobTimers.Add(-1f);
        _blobColors.Add(Color.white);

        return obj;
    }

    GameObject CreateParticleObject()
    {
        GameObject obj = new GameObject($"BloodParticle_{_particlePool.Count}");
        obj.transform.SetParent(transform);
        obj.SetActive(false);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        // Use particleSprite if assigned, otherwise fall back to burstSprite
        sr.sprite = particleSprite != null ? particleSprite : burstSprite;
        sr.sortingOrder = 95;  // Above blobs, below burst

        _particlePool.Add(obj);
        _particleRenderers.Add(sr);
        _particleTimers.Add(-1f);
        _particleColors.Add(Color.white);
        _particleVelocities.Add(Vector2.zero);

        return obj;
    }

    void Update()
    {
        // Process pending death events
        if (DeathEffectManager.IsCreated)
        {
            while (DeathEffectManager.PendingDeaths.TryDequeue(out var death))
            {
                Vector3 pos = new Vector3(death.Position.x, death.Position.y, 0);
                Color burstColor = death.IsEnemy ? enemyBurstColor : soldierBurstColor;
                Color blobColor = death.IsEnemy ? enemyBlobColor : soldierBlobColor;
                Color particleColor = death.IsEnemy ? enemyParticleColor : soldierParticleColor;

                SpawnBurst(pos, burstColor);
                SpawnBlob(pos, blobColor);
                SpawnBloodParticles(pos, death.ImpactDirection, particleColor);
            }
        }

        // Update active effects
        UpdateBursts();
        UpdateBlobs();
        UpdateParticles();
    }
    
    void UpdateBursts()
    {
        for (int i = 0; i < _burstPool.Count; i++)
        {
            if (_burstTimers[i] < 0) continue;
            
            _burstTimers[i] += Time.deltaTime;
            float t = _burstTimers[i] / burstDuration;
            
            if (t >= 1f)
            {
                _burstPool[i].SetActive(false);
                _burstTimers[i] = -1f;
                continue;
            }
            
            // Scale up quickly
            float scale = Mathf.Lerp(burstStartScale, burstEndScale, t);
            _burstPool[i].transform.localScale = Vector3.one * scale;
            
            // Fade out
            Color color = _burstColors[i];
            color.a = Mathf.Lerp(1f, 0f, t);
            _burstRenderers[i].color = color;
        }
    }
    
    void UpdateBlobs()
    {
        for (int i = 0; i < _blobPool.Count; i++)
        {
            if (_blobTimers[i] < 0) continue;

            _blobTimers[i] += Time.deltaTime;
            float t = _blobTimers[i] / blobDuration;

            if (t >= 1f)
            {
                _blobPool[i].SetActive(false);
                _blobTimers[i] = -1f;
                continue;
            }

            // Fade out slowly
            Color color = _blobColors[i];
            color.a = Mathf.Lerp(_blobColors[i].a, 0f, t);
            _blobRenderers[i].color = color;
        }
    }

    void UpdateParticles()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < _particlePool.Count; i++)
        {
            if (_particleTimers[i] < 0) continue;

            _particleTimers[i] += dt;
            float t = _particleTimers[i] / particleDuration;

            if (t >= 1f)
            {
                _particlePool[i].SetActive(false);
                _particleTimers[i] = -1f;
                continue;
            }

            // Move particle
            Vector3 pos = _particlePool[i].transform.position;
            Vector2 vel = _particleVelocities[i];

            // Apply drag/slowdown
            vel *= (1f - dt * 3f);
            _particleVelocities[i] = vel;

            pos.x += vel.x * dt;
            pos.y += vel.y * dt;
            _particlePool[i].transform.position = pos;

            // Scale down
            float scale = Mathf.Lerp(particleStartScale, particleEndScale, t);
            _particlePool[i].transform.localScale = Vector3.one * scale;

            // Fade out
            Color color = _particleColors[i];
            color.a = Mathf.Lerp(1f, 0f, t * t);  // Quadratic fade for snappier end
            _particleRenderers[i].color = color;
        }
    }

    void SpawnBurst(Vector3 position, Color color)
    {
        int index = FindInactiveOrCreate(_burstTimers, _burstPool, CreateBurstObject, ref _burstPoolIndex);
        
        _burstPool[index].transform.position = position;
        _burstPool[index].transform.localScale = Vector3.one * burstStartScale;
        _burstPool[index].SetActive(true);
        _burstRenderers[index].color = color;
        _burstColors[index] = color;
        _burstTimers[index] = 0f;
    }
    
    void SpawnBlob(Vector3 position, Color color)
    {
        int index = FindInactiveOrCreate(_blobTimers, _blobPool, CreateBlobObject, ref _blobPoolIndex);

        _blobPool[index].transform.position = position;
        _blobPool[index].transform.localScale = Vector3.one * blobScale;
        _blobPool[index].SetActive(true);
        _blobRenderers[index].color = color;
        _blobColors[index] = color;
        _blobTimers[index] = 0f;
    }

    void SpawnBloodParticles(Vector3 position, float2 impactDirection, Color color)
    {
        // Determine the base direction for particles
        // If no impact direction, spray in all directions
        // If impact direction exists, bias particles in that direction (knockback)
        Vector2 baseDir;
        bool hasImpactDir = math.lengthsq(impactDirection) > 0.01f;

        if (hasImpactDir)
        {
            // Particles spray in the direction of the impact (away from attacker)
            baseDir = new Vector2(impactDirection.x, impactDirection.y);
        }
        else
        {
            // Random base direction if no impact info
            float randomAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            baseDir = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
        }

        float spreadRad = particleSpreadAngle * Mathf.Deg2Rad;

        // Get the sprite to use (refresh in case it was changed in inspector)
        Sprite spriteToUse = particleSprite != null ? particleSprite : burstSprite;

        for (int p = 0; p < particleCount; p++)
        {
            int index = FindInactiveOrCreateParticle();

            // Calculate particle direction with spread
            float angleOffset;
            if (hasImpactDir)
            {
                // Bias toward impact direction with some spread
                // Mix knockback direction with random spray
                float knockbackBias = UnityEngine.Random.Range(0f, 1f) < knockbackStrength ? 1f : 0f;
                angleOffset = UnityEngine.Random.Range(-spreadRad, spreadRad);

                // Some particles go opposite for splash effect
                if (UnityEngine.Random.Range(0f, 1f) < 0.2f)
                {
                    angleOffset += Mathf.PI;
                }
            }
            else
            {
                // Full 360 spray when no impact direction
                angleOffset = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
            }

            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x);
            float finalAngle = baseAngle + angleOffset;

            Vector2 dir = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));
            float speed = particleSpeed + UnityEngine.Random.Range(-particleSpeedVariance, particleSpeedVariance);

            // Set particle state - always update sprite in case it was changed
            _particleRenderers[index].sprite = spriteToUse;
            _particlePool[index].transform.position = position;
            _particlePool[index].transform.localScale = Vector3.one * particleStartScale;
            _particlePool[index].SetActive(true);
            _particleRenderers[index].color = color;
            _particleColors[index] = color;
            _particleVelocities[index] = dir * speed;
            _particleTimers[index] = 0f;
        }
    }

    int FindInactiveOrCreateParticle()
    {
        int startIndex = _particlePoolIndex;
        do
        {
            if (_particleTimers[_particlePoolIndex] < 0)
            {
                int found = _particlePoolIndex;
                _particlePoolIndex = (_particlePoolIndex + 1) % _particlePool.Count;
                return found;
            }
            _particlePoolIndex = (_particlePoolIndex + 1) % _particlePool.Count;
        }
        while (_particlePoolIndex != startIndex);

        // Pool full, create new
        CreateParticleObject();
        return _particlePool.Count - 1;
    }

    int FindInactiveOrCreate(List<float> timers, List<GameObject> pool, System.Func<GameObject> createFunc, ref int poolIndex)
    {
        int startIndex = poolIndex;
        do
        {
            if (timers[poolIndex] < 0)
            {
                int found = poolIndex;
                poolIndex = (poolIndex + 1) % pool.Count;
                return found;
            }
            poolIndex = (poolIndex + 1) % pool.Count;
        }
        while (poolIndex != startIndex);
        
        // Pool full, create new
        createFunc();
        return pool.Count - 1;
    }
    
    void OnDestroy()
    {
        DeathEffectManager.Dispose();
    }
}
