using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

/// <summary>
/// MonoBehaviour that handles mouse input for unit selection
/// - Left click + drag: Box select soldiers
/// - Right click: Move selected soldiers to position (spread in cluster)
/// - Left click on empty: Deselect all
/// </summary>
public class UnitSelectionSystem : MonoBehaviour
{
    [Header("Selection Settings")]
    public Color selectionBoxColor = new Color(0, 1, 0, 0.2f);
    public Color selectionBorderColor = new Color(0, 1, 0, 0.8f);
    public float clickSelectionRadius = 0.5f;  // How close you need to click to a unit to select it
    
    [Header("Movement Settings")]
    public float unitSpacing = 1.2f;  // Space between units in cluster
    
    [Header("Move Ping Settings")]
    public Sprite pingSprite;  // Assign a circle sprite in inspector
    public Color pingColor = new Color(0f, 1f, 0f, 0.8f);
    public float pingDuration = 0.5f;
    public float pingStartScale = 0.5f;
    public float pingEndScale = 2.5f;
    
    private bool _isDragging;
    private Vector2 _dragStartScreen;
    private Vector2 _dragEndScreen;
    private Vector3 _dragStartWorld;
    
    private EntityManager _entityManager;
    private EntityQuery _selectableQuery;
    private EntityQuery _selectedQuery;
    
    private Camera _mainCamera;
    
    void Start()
    {
        _mainCamera = Camera.main;
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            _entityManager = world.EntityManager;
            
            // Query for selectable units (soldiers)
            _selectableQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Selectable>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<PlayerUnit>()
            );
            
            // Query for currently selected units
            _selectedQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Selected>(),
                ComponentType.ReadWrite<MoveCommand>()
            );
        }
    }
    
    void Update()
    {
        if (_mainCamera == null) return;
        
        HandleSelectionInput();
        HandleMoveCommand();
    }
    
    void HandleSelectionInput()
    {
        // Start drag
        if (Input.GetMouseButtonDown(0))
        {
            _isDragging = true;
            _dragStartScreen = Input.mousePosition;
            _dragStartWorld = GetMouseWorldPosition();
        }
        
        // Update drag
        if (Input.GetMouseButton(0) && _isDragging)
        {
            _dragEndScreen = Input.mousePosition;
        }
        
        // End drag - perform selection
        if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            _isDragging = false;
            _dragEndScreen = Input.mousePosition;
            
            Vector3 dragEndWorld = GetMouseWorldPosition();
            PerformSelection(_dragStartWorld, dragEndWorld);
        }
    }
    
    void HandleMoveCommand()
    {
        // Right click to move selected units
        if (Input.GetMouseButtonDown(1))
        {
            Vector3 targetWorld = GetMouseWorldPosition();
            float2 targetCenter = new float2(targetWorld.x, targetWorld.y);
            
            // Get all selected entities
            var selectedEntities = _selectedQuery.ToEntityArray(Allocator.Temp);
            int unitCount = selectedEntities.Length;
            
            if (unitCount == 0)
            {
                selectedEntities.Dispose();
                return;
            }
            
            // Spawn move ping
            SpawnMovePing(targetWorld);
            
            // Calculate cluster positions
            var clusterOffsets = CalculateClusterOffsets(unitCount);
            
            for (int i = 0; i < selectedEntities.Length; i++)
            {
                Entity entity = selectedEntities[i];
                
                if (_entityManager.HasComponent<MoveCommand>(entity))
                {
                    float2 unitTarget = targetCenter + clusterOffsets[i];
                    
                    _entityManager.SetComponentData(entity, new MoveCommand
                    {
                        Target = unitTarget,
                        HasCommand = true
                    });
                }
            }
            
            clusterOffsets.Dispose();
            selectedEntities.Dispose();
        }
    }
    
    void SpawnMovePing(Vector3 position)
    {
        GameObject pingObj = new GameObject("MovePing");
        pingObj.transform.SetParent(transform); // Parent to SelectionManager (outside subscene)
        pingObj.transform.position = new Vector3(position.x, position.y, 0);
        
        SpriteRenderer sr = pingObj.AddComponent<SpriteRenderer>();
        sr.sprite = pingSprite;
        sr.color = pingColor;
        sr.sortingOrder = 100; // Draw on top
        
        // Check if sprite is assigned
        if (pingSprite == null)
        {
            Debug.LogWarning("MovePing: No sprite assigned! Assign a circle sprite to SelectionManager.");
        }
        
        MovePing ping = pingObj.AddComponent<MovePing>();
        ping.Initialize(position, pingColor, pingDuration, pingStartScale, pingEndScale);
    }
    
    NativeArray<float2> CalculateClusterOffsets(int unitCount)
    {
        var offsets = new NativeArray<float2>(unitCount, Allocator.Temp);
        
        if (unitCount == 1)
        {
            offsets[0] = float2.zero;
            return offsets;
        }
        
        // Spread units in a spiral pattern from center
        // This creates a natural-looking cluster
        float angleStep = 2.4f; // Golden angle approximation for nice distribution
        float radiusStep = unitSpacing * 0.5f;
        
        for (int i = 0; i < unitCount; i++)
        {
            float angle = i * angleStep;
            float radius = Mathf.Sqrt(i + 1) * radiusStep;
            
            offsets[i] = new float2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * 0.5f  // 0.5f for isometric
            );
        }
        
        return offsets;
    }
    
    void PerformSelection(Vector3 start, Vector3 end)
    {
        // Calculate selection bounds
        float minX = Mathf.Min(start.x, end.x);
        float maxX = Mathf.Max(start.x, end.x);
        float minY = Mathf.Min(start.y, end.y);
        float maxY = Mathf.Max(start.y, end.y);
        
        // If very small drag, treat as click (deselect or single select)
        bool isClick = Vector2.Distance(_dragStartScreen, _dragEndScreen) < 5f;
        
        // First, deselect all currently selected units
        DeselectAll();
        
        // Get all selectable entities
        var entities = _selectableQuery.ToEntityArray(Allocator.Temp);
        var transforms = _selectableQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        int selectedCount = 0;

        if (isClick)
        {
            // Click selection - find closest unit within radius
            Entity closestEntity = Entity.Null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < entities.Length; i++)
            {
                float3 pos = transforms[i].Position;
                float dist = math.distance(new float2(pos.x, pos.y), new float2(start.x, start.y));

                if (dist < clickSelectionRadius && dist < closestDist)
                {
                    closestEntity = entities[i];
                    closestDist = dist;
                }
            }

            // Select only the closest unit
            if (closestEntity != Entity.Null)
            {
                _entityManager.AddComponent<Selected>(closestEntity);
                selectedCount = 1;
            }
        }
        else
        {
            // Box selection - select all units in box
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                float3 pos = transforms[i].Position;

                bool inSelection = pos.x >= minX && pos.x <= maxX &&
                                  pos.y >= minY && pos.y <= maxY;

                if (inSelection)
                {
                    // Add Selected component
                    if (!_entityManager.HasComponent<Selected>(entity))
                    {
                        _entityManager.AddComponent<Selected>(entity);
                    }
                    selectedCount++;
                }
            }
        }

        entities.Dispose();
        transforms.Dispose();
    }
    
    void DeselectAll()
    {
        var selectedEntities = _selectedQuery.ToEntityArray(Allocator.Temp);
        
        for (int i = 0; i < selectedEntities.Length; i++)
        {
            _entityManager.RemoveComponent<Selected>(selectedEntities[i]);
        }
        
        selectedEntities.Dispose();
    }
    
    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -_mainCamera.transform.position.z;
        return _mainCamera.ScreenToWorldPoint(mousePos);
    }
    
    void OnGUI()
    {
        if (_isDragging)
        {
            // Draw selection box
            Rect rect = GetScreenRect(_dragStartScreen, _dragEndScreen);
            DrawScreenRect(rect, selectionBoxColor);
            DrawScreenRectBorder(rect, 2, selectionBorderColor);
        }
        
        // Draw selected unit count
        int count = _selectedQuery.CalculateEntityCount();
        if (count > 0)
        {
            GUI.Label(new Rect(10, 80, 200, 25), $"Selected: {count} soldiers");
        }
    }
    
    Rect GetScreenRect(Vector2 screenPos1, Vector2 screenPos2)
    {
        // Move origin from bottom-left to top-left
        screenPos1.y = Screen.height - screenPos1.y;
        screenPos2.y = Screen.height - screenPos2.y;
        
        // Calculate corners
        Vector2 topLeft = Vector2.Min(screenPos1, screenPos2);
        Vector2 bottomRight = Vector2.Max(screenPos1, screenPos2);
        
        return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
    }
    
    void DrawScreenRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
    
    void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        // Top
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        // Bottom
        DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        // Left
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        // Right
        DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }
}
