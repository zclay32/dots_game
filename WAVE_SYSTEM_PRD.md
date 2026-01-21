# Wave System PRD

## Overview
Add a wave-based zombie spawning system with 8 waves of escalating difficulty. Zombies spawn from random map edges in staggered groups and aggro toward the map center.

## Requirements Summary
- 8 waves with linear scaling
- Random spawn point along map edge per wave (N/S/E/W)
- Staggered spawning (configurable: X zombies every Y seconds)
- Zombies aggro toward map center on spawn
- UI: Wave timer countdown + direction announcement
- Keep existing initial zombie spawn (CombatSpawnerSystem)

---

## Architecture

### New Files to Create

| File | Type | Purpose |
|------|------|---------|
| `WaveSpawnerSystem.cs` | ISystem | Core wave logic, spawning, state management |
| `WaveComponents.cs` | Components | WaveConfig, WaveState data structures |
| `WaveSpawnerAuthoring.cs` | MonoBehaviour | Inspector configuration + baking |
| `WaveUIManager.cs` | MonoBehaviour | OnGUI display for timer + announcements |

### Component Design

```csharp
// Configuration (set once via authoring)
public struct WaveConfig : IComponentData
{
    public Entity ZombiePrefab;
    public int TotalWaves;              // 8
    public float TimeBetweenWaves;      // seconds before first/between waves
    public int BaseZombiesPerWave;      // starting count (e.g., 20)
    public int ZombiesPerWaveIncrease;  // linear scaling (e.g., +15 per wave)
    public int SpawnBatchSize;          // zombies per batch (e.g., 4)
    public float SpawnBatchInterval;    // seconds between batches (e.g., 0.5)
    public float MapRadius;             // distance from center to spawn edge
    public float2 MapCenter;            // center point zombies aggro toward
    public float SpawnSpread;           // randomize positions within this radius
}

// Runtime state (updated each frame)
public struct WaveState : IComponentData
{
    public int CurrentWave;             // 0 = pre-game, 1-8 = active waves
    public WavePhase Phase;             // Countdown, Spawning, Active, Victory, Defeat
    public float Timer;                 // countdown or spawn timer
    public int ZombiesRemaining;        // left to spawn this wave
    public int ZombiesAlive;            // currently alive (for wave completion)
    public float2 SpawnPosition;        // current wave spawn point
    public SpawnDirection Direction;    // N/S/E/W for UI
    public float SpawnTimer;            // time until next batch
}

public enum WavePhase : byte
{
    Countdown,   // Timer counting down to wave start
    Spawning,    // Actively spawning zombies
    Active,      // All spawned, waiting for kills
    Victory,     // All 8 waves completed
    Defeat       // All soldiers dead
}

public enum SpawnDirection : byte
{
    North, South, East, West
}
```

---

## Implementation Details

### 1. WaveSpawnerSystem.cs

**Responsibilities:**
- Manage wave state machine (Countdown → Spawning → Active → next wave)
- Pick random edge spawn point at wave start
- Spawn zombie batches at configured intervals
- Track zombies alive for wave completion
- Check victory/defeat conditions

**Key Logic:**

```csharp
// Spawn point selection (random edge)
SpawnDirection dir = (SpawnDirection)random.NextInt(0, 4);
float2 spawnPos = dir switch
{
    North => new float2(random.NextFloat(-mapRadius, mapRadius), mapRadius),
    South => new float2(random.NextFloat(-mapRadius, mapRadius), -mapRadius),
    East  => new float2(mapRadius, random.NextFloat(-mapRadius, mapRadius)),
    West  => new float2(-mapRadius, random.NextFloat(-mapRadius, mapRadius)),
};

// Zombie spawning with aggro toward center
var entity = ecb.Instantiate(config.ZombiePrefab);
ecb.SetComponent(entity, LocalTransform.FromPosition(new float3(pos.x, pos.y, 0)));

// Set zombie to chase toward map center
ecb.SetComponent(entity, new ZombieCombatState
{
    State = ZombieCombatAIState.Chasing,
    StateTimer = 30f,  // Long chase duration
    WanderTarget = config.MapCenter,  // Aggro toward center
    CurrentTarget = Entity.Null,
    HasTarget = false,
    HasEngagedTarget = false
});
```

**Staggered Spawning:**
```csharp
// In OnUpdate, during Spawning phase:
state.SpawnTimer -= deltaTime;
if (state.SpawnTimer <= 0 && state.ZombiesRemaining > 0)
{
    int batchSize = math.min(config.SpawnBatchSize, state.ZombiesRemaining);
    SpawnZombieBatch(ecb, config, ref state, batchSize);
    state.ZombiesRemaining -= batchSize;
    state.SpawnTimer = config.SpawnBatchInterval;

    if (state.ZombiesRemaining == 0)
        state.Phase = WavePhase.Active;
}
```

**Wave Completion Check:**
```csharp
// Count alive zombies (has EnemyUnit tag)
int aliveCount = 0;
foreach (var _ in SystemAPI.Query<RefRO<EnemyUnit>>())
    aliveCount++;

state.ZombiesAlive = aliveCount;

if (state.Phase == WavePhase.Active && aliveCount == 0)
{
    if (state.CurrentWave >= config.TotalWaves)
        state.Phase = WavePhase.Victory;
    else
        StartNextWaveCountdown(ref state, config);
}
```

**Defeat Check:**
```csharp
int soldierCount = 0;
foreach (var _ in SystemAPI.Query<RefRO<PlayerUnit>>())
    soldierCount++;

if (soldierCount == 0)
    state.Phase = WavePhase.Defeat;
```

### 2. WaveUIManager.cs (MonoBehaviour)

**Pattern:** Follow existing `PerformanceMonitor.cs` using `OnGUI()`

**Display Elements:**
- Wave number: "Wave 3/8"
- Phase-dependent text:
  - Countdown: "Next wave in: 15s - From the NORTH!"
  - Spawning/Active: "Zombies remaining: 45"
  - Victory: "VICTORY! All waves survived!"
  - Defeat: "DEFEAT - All soldiers lost"

**Position:** Top-center of screen (different from performance monitor)

```csharp
void OnGUI()
{
    // Query ECS for wave state (via SystemAPI or cached reference)
    // Display based on phase

    string text = phase switch
    {
        WavePhase.Countdown => $"Wave {wave}/{total} in {timer:F0}s - From the {direction}!",
        WavePhase.Spawning => $"Wave {wave}/{total} - Spawning... ({alive} zombies)",
        WavePhase.Active => $"Wave {wave}/{total} - {alive} zombies remaining",
        WavePhase.Victory => "VICTORY!",
        WavePhase.Defeat => "DEFEAT",
    };

    // Center at top of screen
    GUI.Label(new Rect(Screen.width/2 - 150, 10, 300, 30), text, style);
}
```

### 3. WaveSpawnerAuthoring.cs

**Inspector Fields:**
```csharp
[Header("Wave Settings")]
public int totalWaves = 8;
public float timeBetweenWaves = 15f;

[Header("Zombie Scaling")]
public GameObject zombiePrefab;
public int baseZombiesPerWave = 20;
public int zombiesPerWaveIncrease = 15;  // Wave 1: 20, Wave 2: 35, etc.

[Header("Spawn Timing")]
public int spawnBatchSize = 4;
public float spawnBatchInterval = 0.5f;

[Header("Map Settings")]
public float mapRadius = 40f;
public Vector2 mapCenter = Vector2.zero;
public float spawnSpread = 2f;  // Random offset for each zombie in batch
```

---

## Integration Points

### Keep Existing Combat Spawner
The existing `CombatSpawnerSystem` spawns zombies at game start. **Keep this behavior** - initial zombies provide early game challenge while waves add escalating difficulty. No changes needed to `CombatSpawnerSystem`.

### Zombie Spawn Position Fixup
`ZombieSpawnPositionFixupSystem` runs once on frame 2. For wave-spawned zombies:
- They need `SpawnPosition` set to their spawn location (not center)
- **Solution:** Set `SpawnPosition` directly in `WaveSpawnerSystem` when spawning, OR run fixup system every frame (check if zombie has default spawn position)

**Recommendation:** Set components correctly during spawn in `WaveSpawnerSystem` using ECB.

---

## File Modifications

| File | Change |
|------|--------|
| `ZombieSpawnPositionFixupSystem.cs` | Run continuously (not just frame 2) to handle wave spawns |

---

## Wave Scaling (8 Waves)

| Wave | Zombies | Batches (size 4) | Spawn Duration |
|------|---------|------------------|----------------|
| 1 | 20 | 5 | 2.5s |
| 2 | 35 | 9 | 4.5s |
| 3 | 50 | 13 | 6.5s |
| 4 | 65 | 17 | 8.5s |
| 5 | 80 | 20 | 10s |
| 6 | 95 | 24 | 12s |
| 7 | 110 | 28 | 14s |
| 8 | 125 | 32 | 16s |

**Total:** 580 zombies across all waves

---

## Verification Plan

1. **Basic spawning**: Zombies appear at map edge
2. **Direction correctness**: Spawn position matches announced direction
3. **Staggered spawn**: Zombies appear in batches, not all at once
4. **Aggro behavior**: Zombies move toward center immediately
5. **Wave progression**: Next wave starts after all zombies killed
6. **Scaling**: Later waves have more zombies
7. **Victory condition**: Game shows victory after wave 8
8. **Defeat condition**: Game shows defeat when all soldiers die
9. **UI display**: Timer, wave number, and direction show correctly

---

## Implementation Order

1. Create `WaveComponents.cs` - data structures
2. Create `WaveSpawnerAuthoring.cs` - Inspector config
3. Create `WaveSpawnerSystem.cs` - core logic
4. Update `ZombieSpawnPositionFixupSystem.cs` - handle wave spawns (run continuously)
5. Create `WaveUIManager.cs` - UI display
6. Test and tune parameters
