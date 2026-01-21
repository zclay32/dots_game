using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for wave spawner configuration.
/// Add to a GameObject in your subscene to enable the wave system.
/// </summary>
public class WaveSpawnerAuthoring : MonoBehaviour
{
    [Header("Wave Settings")]
    [Tooltip("Total number of waves to survive")]
    public int totalWaves = 8;

    [Tooltip("Seconds before first wave and between waves")]
    public float timeBetweenWaves = 15f;

    [Header("Zombie Scaling")]
    [Tooltip("Zombie prefab to spawn")]
    public GameObject zombiePrefab;

    [Tooltip("Number of zombies in wave 1")]
    public int baseZombiesPerWave = 20;

    [Tooltip("Additional zombies added each wave (linear scaling)")]
    public int zombiesPerWaveIncrease = 15;

    [Header("Spawn Timing")]
    [Tooltip("Number of zombies spawned per batch")]
    public int spawnBatchSize = 4;

    [Tooltip("Seconds between each batch spawn")]
    public float spawnBatchInterval = 0.5f;

    [Header("Map Settings")]
    [Tooltip("Distance from center to map edge (spawn location)")]
    public float mapRadius = 40f;

    [Tooltip("Center point that zombies aggro toward")]
    public Vector2 mapCenter = Vector2.zero;

    [Tooltip("Random spread for zombie positions within batch")]
    public float spawnSpread = 2f;

    class Baker : Baker<WaveSpawnerAuthoring>
    {
        public override void Bake(WaveSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Add configuration component
            AddComponent(entity, new WaveConfig
            {
                ZombiePrefab = GetEntity(authoring.zombiePrefab, TransformUsageFlags.Dynamic),
                TotalWaves = authoring.totalWaves,
                TimeBetweenWaves = authoring.timeBetweenWaves,
                BaseZombiesPerWave = authoring.baseZombiesPerWave,
                ZombiesPerWaveIncrease = authoring.zombiesPerWaveIncrease,
                SpawnBatchSize = authoring.spawnBatchSize,
                SpawnBatchInterval = authoring.spawnBatchInterval,
                MapRadius = authoring.mapRadius,
                MapCenter = new float2(authoring.mapCenter.x, authoring.mapCenter.y),
                SpawnSpread = authoring.spawnSpread
            });

            // Add initial state component
            AddComponent(entity, new WaveState
            {
                CurrentWave = 0,
                Phase = WavePhase.Countdown,
                Timer = authoring.timeBetweenWaves,
                ZombiesRemaining = 0,
                ZombiesAlive = 0,
                SpawnPosition = float2.zero,
                Direction = SpawnDirection.North,
                SpawnTimer = 0f
            });
        }
    }
}
