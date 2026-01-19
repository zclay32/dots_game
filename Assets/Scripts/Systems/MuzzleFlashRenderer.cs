using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;

/// <summary>
/// Renders muzzle flash pings when soldiers fire
/// </summary>
public class MuzzleFlashRenderer : MonoBehaviour
{
    [Header("Flash Settings")]
    public Sprite flashSprite;
    public Color flashColor = new Color(1f, 0.9f, 0.2f, 0.9f);
    public float flashDuration = 0.15f;
    public float flashStartScale = 0.3f;
    public float flashEndScale = 0.6f;
    public float flashOffset = 0.4f; // Distance in front of unit
    
    // Object pooling
    private List<GameObject> _flashPool = new List<GameObject>();
    private List<SpriteRenderer> _flashRenderers = new List<SpriteRenderer>();
    private List<float> _flashTimers = new List<float>();
    private int _poolIndex = 0;
    private int _initialPoolSize = 100;
    
    void Start()
    {
        MuzzleFlashManager.Initialize();
        CreatePool();
    }
    
    void CreatePool()
    {
        for (int i = 0; i < _initialPoolSize; i++)
        {
            CreateFlashObject();
        }
    }
    
    GameObject CreateFlashObject()
    {
        GameObject flashObj = new GameObject($"MuzzleFlash_{_flashPool.Count}");
        flashObj.transform.SetParent(transform);
        flashObj.SetActive(false);
        
        SpriteRenderer sr = flashObj.AddComponent<SpriteRenderer>();
        sr.sprite = flashSprite;
        sr.color = flashColor;
        sr.sortingOrder = 101;
        
        _flashPool.Add(flashObj);
        _flashRenderers.Add(sr);
        _flashTimers.Add(-1f);
        
        return flashObj;
    }
    
    void Update()
    {
        // Process pending flash events
        if (MuzzleFlashManager.IsCreated)
        {
            while (MuzzleFlashManager.PendingFlashes.TryDequeue(out var flash))
            {
                // Offset position in facing direction
                Vector3 pos = new Vector3(flash.Position.x, flash.Position.y, 0);
                Vector3 offset = new Vector3(flash.Direction.x, flash.Direction.y, 0) * flashOffset;
                SpawnFlash(pos + offset);
            }
        }
        
        // Update active flashes
        for (int i = 0; i < _flashPool.Count; i++)
        {
            if (_flashTimers[i] < 0) continue;
            
            _flashTimers[i] += Time.deltaTime;
            float t = _flashTimers[i] / flashDuration;
            
            if (t >= 1f)
            {
                _flashPool[i].SetActive(false);
                _flashTimers[i] = -1f;
                continue;
            }
            
            // Scale up
            float scale = Mathf.Lerp(flashStartScale, flashEndScale, t);
            _flashPool[i].transform.localScale = Vector3.one * scale;
            
            // Fade out
            Color color = flashColor;
            color.a = Mathf.Lerp(flashColor.a, 0f, t);
            _flashRenderers[i].color = color;
        }
    }
    
    void SpawnFlash(Vector3 position)
    {
        // Find an inactive flash or create a new one
        int startIndex = _poolIndex;
        do
        {
            if (_flashTimers[_poolIndex] < 0)
            {
                // Found an inactive one
                ActivateFlash(_poolIndex, position);
                _poolIndex = (_poolIndex + 1) % _flashPool.Count;
                return;
            }
            _poolIndex = (_poolIndex + 1) % _flashPool.Count;
        }
        while (_poolIndex != startIndex);
        
        // Pool full, create a new one
        int newIndex = _flashPool.Count;
        CreateFlashObject();
        ActivateFlash(newIndex, position);
    }
    
    void ActivateFlash(int index, Vector3 position)
    {
        _flashPool[index].transform.position = position;
        _flashPool[index].transform.localScale = Vector3.one * flashStartScale;
        _flashPool[index].SetActive(true);
        _flashRenderers[index].color = flashColor;
        _flashTimers[index] = 0f;
    }
    
    void OnDestroy()
    {
        MuzzleFlashManager.Dispose();
    }
}
