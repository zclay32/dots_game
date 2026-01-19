using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for spawning units
/// Place on a GameObject in the scene to configure spawning
/// </summary>
public class UnitSpawnerAuthoring : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject unitPrefab;
    public int spawnCount = 10000;
    public float spawnRadius = 50f;
    
    [Header("Unit Settings")]
    public float minSpeed = 2f;
    public float maxSpeed = 5f;
    
    class Baker : Baker<UnitSpawnerAuthoring>
    {
        public override void Bake(UnitSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new UnitSpawner
            {
                UnitPrefab = GetEntity(authoring.unitPrefab, TransformUsageFlags.Dynamic),
                SpawnCount = authoring.spawnCount,
                SpawnRadius = authoring.spawnRadius,
                MinSpeed = authoring.minSpeed,
                MaxSpeed = authoring.maxSpeed
            });
        }
    }
}

/// <summary>
/// ECS component for unit spawner data
/// </summary>
public struct UnitSpawner : IComponentData
{
    public Entity UnitPrefab;
    public int SpawnCount;
    public float SpawnRadius;
    public float MinSpeed;
    public float MaxSpeed;
}
