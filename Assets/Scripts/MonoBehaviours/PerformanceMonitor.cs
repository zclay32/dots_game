using UnityEngine;
using Unity.Entities;

/// <summary>
/// Displays FPS and entity count for performance monitoring
/// </summary>
public class PerformanceMonitor : MonoBehaviour
{
    [Header("Display Settings")]
    public bool showFPS = true;
    public bool showEntityCount = true;
    public int fontSize = 24;
    
    private float deltaTime;
    private GUIStyle style;
    private EntityManager entityManager;
    private bool entityManagerInitialized;
    
    void Start()
    {
        style = new GUIStyle();
        style.fontSize = fontSize;
        style.normal.textColor = Color.white;
        
        // Add shadow/outline for readability
        style.fontStyle = FontStyle.Bold;
    }
    
    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        
        // Try to get EntityManager
        if (!entityManagerInitialized && World.DefaultGameObjectInjectionWorld != null)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            entityManagerInitialized = true;
        }
    }
    
    void OnGUI()
    {
        int yOffset = 10;
        
        if (showFPS)
        {
            float fps = 1.0f / deltaTime;
            float ms = deltaTime * 1000f;
            string fpsText = $"FPS: {fps:F1} ({ms:F2}ms)";
            
            // Draw shadow
            GUI.color = Color.black;
            GUI.Label(new Rect(12, yOffset + 2, 300, 30), fpsText, style);
            
            // Draw text
            GUI.color = GetFPSColor(fps);
            GUI.Label(new Rect(10, yOffset, 300, 30), fpsText, style);
            
            yOffset += 30;
        }
        
        if (showEntityCount && entityManagerInitialized)
        {
            // Count soldiers
            var soldierQuery = entityManager.CreateEntityQuery(typeof(PlayerUnit));
            int soldierCount = soldierQuery.CalculateEntityCount();
            
            // Count zombies
            var zombieQuery = entityManager.CreateEntityQuery(typeof(EnemyUnit));
            int zombieCount = zombieQuery.CalculateEntityCount();
            
            // Draw soldier count
            string soldierText = $"Soldiers: {soldierCount:N0}";
            GUI.color = Color.black;
            GUI.Label(new Rect(12, yOffset + 2, 300, 30), soldierText, style);
            GUI.color = Color.green;
            GUI.Label(new Rect(10, yOffset, 300, 30), soldierText, style);
            yOffset += 30;
            
            // Draw zombie count
            string zombieText = $"Zombies: {zombieCount:N0}";
            GUI.color = Color.black;
            GUI.Label(new Rect(12, yOffset + 2, 300, 30), zombieText, style);
            GUI.color = Color.red;
            GUI.Label(new Rect(10, yOffset, 300, 30), zombieText, style);
        }
    }
    
    Color GetFPSColor(float fps)
    {
        if (fps >= 60) return Color.green;
        if (fps >= 30) return Color.yellow;
        return Color.red;
    }
}
