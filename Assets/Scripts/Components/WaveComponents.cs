using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Wave phase state machine
/// </summary>
public enum WavePhase : byte
{
    Countdown,   // Timer counting down to wave start
    Spawning,    // Actively spawning zombies
    Active,      // All spawned, waiting for kills
    Victory,     // All waves completed
    Defeat       // All soldiers dead
}

/// <summary>
/// Spawn direction for UI display
/// </summary>
public enum SpawnDirection : byte
{
    North,
    South,
    East,
    West
}

/// <summary>
/// Wave system configuration (set once via authoring)
/// </summary>
public struct WaveConfig : IComponentData
{
    public Entity ZombiePrefab;
    public int TotalWaves;
    public float TimeBetweenWaves;
    public int BaseZombiesPerWave;
    public int ZombiesPerWaveIncrease;
    public int SpawnBatchSize;
    public float SpawnBatchInterval;
    public float MapRadius;
    public float2 MapCenter;
    public float SpawnSpread;
}

/// <summary>
/// Wave system runtime state (updated each frame)
/// </summary>
public struct WaveState : IComponentData
{
    public int CurrentWave;
    public WavePhase Phase;
    public float Timer;
    public int ZombiesRemaining;
    public int ZombiesAlive;        // Wave-spawned zombies still alive
    public int TotalZombiesAlive;   // All zombies on map (for UI)
    public float2 SpawnPosition;
    public SpawnDirection Direction;
    public float SpawnTimer;
}

/// <summary>
/// Tag component for zombies spawned by the wave system.
/// Used to track wave completion separately from initial zombies.
/// </summary>
public struct WaveSpawnedZombie : IComponentData { }
