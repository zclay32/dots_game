using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Spawner for combat test - spawns soldiers and zombies
/// </summary>
public class CombatSpawnerAuthoring : MonoBehaviour
{
    [Header("Soldier Settings")]
    public GameObject soldierPrefab;
    public int soldierCount = 100;
    public float soldierSpawnRadius = 10f;
    public Vector2 soldierSpawnCenter = Vector2.zero;
    
    [Header("Zombie Settings")]
    public GameObject zombiePrefab;
    public int zombieCount = 5000;
    public float zombieSpawnRadius = 50f;
    public float zombieMinDistance = 20f; // Minimum distance from center (ring spawn)
    
    class Baker : Baker<CombatSpawnerAuthoring>
    {
        public override void Bake(CombatSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new CombatSpawner
            {
                SoldierPrefab = GetEntity(authoring.soldierPrefab, TransformUsageFlags.Dynamic),
                SoldierCount = authoring.soldierCount,
                SoldierSpawnRadius = authoring.soldierSpawnRadius,
                SoldierSpawnCenter = new float2(authoring.soldierSpawnCenter.x, authoring.soldierSpawnCenter.y),
                
                ZombiePrefab = GetEntity(authoring.zombiePrefab, TransformUsageFlags.Dynamic),
                ZombieCount = authoring.zombieCount,
                ZombieSpawnRadius = authoring.zombieSpawnRadius,
                ZombieMinDistance = authoring.zombieMinDistance
            });
        }
    }
}

public struct CombatSpawner : IComponentData
{
    public Entity SoldierPrefab;
    public int SoldierCount;
    public float SoldierSpawnRadius;
    public float2 SoldierSpawnCenter;
    
    public Entity ZombiePrefab;
    public int ZombieCount;
    public float ZombieSpawnRadius;
    public float ZombieMinDistance;
}
