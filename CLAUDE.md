# TAB DOTS Prototype - Project Overview

A high-performance 2D tactical combat game built with Unity DOTS (Data-Oriented Technology Stack) featuring soldier vs zombie combat with selection, pathfinding, and combat systems.

---

## 🎮 Project Type

**2D Top-Down Tactical Game**

**CRITICAL: This is a 2D game using the XY plane (NOT XZ)**
- All rendering happens in XY plane with Z=0
- X = horizontal position
- Y = vertical position
- Z = always 0 (or used for render ordering)
- When adding visual elements (circles, bars, sprites), always use XY coordinates!

---

## 🏗️ Architecture

### Unity DOTS (ECS)
This project uses Unity's Data-Oriented Technology Stack for maximum performance:
- **Entities**: Game objects (soldiers, zombies)
- **Components**: Pure data structures (Health, Velocity, CombatState)
- **Systems**: Logic that operates on components (MovementSystem, CombatSystem)

### Hot/Cold Component Separation Pattern
Frequently-updated data is separated from static configuration for better cache performance:

**Example:**
```csharp
// HOT (updated every frame)
public struct HealthCurrent : IComponentData
{
    public float Value;
}

// COLD (read-only configuration)
public struct HealthMax : IComponentData
{
    public float Value;
}
```

### Burst Compilation
Performance-critical systems use `[BurstCompile]` for SIMD-optimized native code generation.

---

## 📁 Project Structure

```
Assets/
├── Scripts/
│   ├── Components/          # ECS component definitions
│   │   ├── *Components.cs   # Hot/cold split components
│   │   └── *Config.cs       # Configuration components
│   ├── Systems/             # ECS systems (game logic)
│   │   ├── *System.cs       # Main game systems
│   │   └── *Optimized.cs    # Performance-optimized versions
│   ├── Authoring/           # MonoBehaviour → ECS bakers
│   │   ├── SoldierAuthoring.cs
│   │   └── ZombieAuthoring.cs
│   ├── Managers/            # Non-ECS managers
│   │   └── NoiseEventManager*.cs
│   └── Editor/              # Editor tools and menu items
├── Shaders/                 # Custom shaders
│   └── SelectionCircle.shader
└── Prefabs/                 # Unity prefabs for units
```

---

## 🎯 Core Systems

### 1. Unit Selection System
- **Box selection** with mouse drag
- **Click selection** for individual units
- **Shift-click** to add/remove from selection
- Adds/removes `Selected` component tag

**Files:**
- [UnitSelectionSystem.cs](Assets/Scripts/Systems/UnitSelectionSystem.cs)
- [SelectionComponents.cs](Assets/Scripts/Components/SelectionComponents.cs)

### 2. Movement System
- **Pathfinding** using A* algorithm
- **Flocking behavior** prevents unit overlap
- **Formation movement** for grouped units
- Uses spatial hash for efficient neighbor queries

**Files:**
- [MovementSystemOptimized.cs](Assets/Scripts/Systems/MovementSystemOptimized.cs)
- [PathfindingSystem.cs](Assets/Scripts/Systems/PathfindingSystem.cs)
- [FlockingSystem.cs](Assets/Scripts/Systems/FlockingSystem.cs)

### 3. Combat System
- **Ranged combat** for soldiers (shoots at zombies)
- **Melee combat** for zombies (attacks soldiers)
- **Attack range** and cooldown system
- **Damage calculation** with health tracking

**Files:**
- [CombatSystemOptimized.cs](Assets/Scripts/Systems/CombatSystemOptimized.cs)
- [CombatComponents.cs](Assets/Scripts/Components/CombatComponents.cs)

### 4. Noise/Alert System
- **Gunshot noise** attracts nearby zombies
- **Probabilistic activation** based on distance
- **Exponential falloff** (closer zombies more likely to react)
- **Configurable sensitivity** per zombie type

**Formula:** `probability = intensity × (1 - (distance/maxRadius)^exponent) × sensitivity`

**Files:**
- [NoiseAlertSystemEnhanced.cs](Assets/Scripts/Systems/NoiseAlertSystemEnhanced.cs)
- [NoiseComponentsEnhanced.cs](Assets/Scripts/Components/NoiseComponentsEnhanced.cs)
- [NoiseEventManagerEnhanced.cs](Assets/Scripts/Managers/NoiseEventManagerEnhanced.cs)

### 5. Rendering Systems
All rendering uses **GPU instancing** for minimal draw calls.

#### Health Bars
- **MonoBehaviour-based** renderer (not ECS)
- Updates in `LateUpdate()`
- 2 draw calls total (background + foreground)
- Frustum culling for off-screen units

**Files:**
- [HealthBarRendererOptimized.cs](Assets/Scripts/Systems/HealthBarRendererOptimized.cs)

#### Selection Circles
- **ECS SystemBase** renderer
- Renders circles under selected units
- Custom shader with transparency
- **XY plane** rendering (2D game!)

**Files:**
- [SelectionCircleRenderer.cs](Assets/Scripts/Systems/SelectionCircleRenderer.cs)
- [SelectionCircleSettings.cs](Assets/Scripts/Systems/SelectionCircleSettings.cs)
- [SelectionCircle.shader](Assets/Shaders/SelectionCircle.shader)

---

## 🎨 Visual Feedback

### Health Bars
- **Green** for soldiers at full health
- **Red** for zombies
- **Orange** for low health (< 30%)
- Position: `(unit.x, unit.y + 0.4, 0)` - **above units**

### Selection Circles
- **Green ring** under selected units
- Configurable radius, thickness, color
- Position: `(unit.x, unit.y - 0.5, 0)` - **below units**
- **Setup:** GameObject → TAB Game → Selection Circle Settings

---

## ⚙️ Configuration

### Inspector-Configurable Systems

#### Soldier Settings (SoldierAuthoring)
```csharp
[Header("Movement")]
public float moveSpeed = 3f;

[Header("Combat")]
public float attackRange = 5f;
public float attackDamage = 25f;
public float attackCooldown = 0.5f;

[Header("Gunshot Noise")]
public float noiseRangeMultiplier = 15f;  // 15x attack range
public float noiseIntensity = 2.0f;
public float noiseFalloffExponent = 1.2f;
```

#### Zombie Settings (ZombieAuthoring)
```csharp
[Header("Movement")]
public float moveSpeed = 1.5f;

[Header("Combat")]
public float attackRange = 1f;
public float attackDamage = 10f;

[Header("Noise Sensitivity")]
public float noiseSensitivityMultiplier = 1.0f;  // 2.0 = extra sensitive
public float minActivationProbability = 0.0f;
public float maxActivationProbability = 1.0f;
```

#### Selection Circle Settings
Create via: **GameObject → TAB Game → Selection Circle Settings**

```csharp
Circle Radius: 0.8        // Size of circle
Circle Height: 0.5        // Offset below unit (negative = below)
Circle Thickness: 0.15    // Ring thickness (0.05 - 0.5)
Selection Color: Green    // Color with alpha
Circle Segments: 32       // Smoothness (8-64)
```

---

## 🚀 Performance Optimizations

### 1. Frame Skipping
Non-critical systems update every N frames:
```csharp
if (_frameCount % UPDATE_INTERVAL != 0) return;
```

- **Noise system**: Every 2 frames
- **Health bars**: Every 2 frames
- **Idle rotation**: Every 4 frames

### 2. Spatial Hash
Efficient spatial queries for neighbors:
```csharp
var neighbors = SpatialHash.QueryRadius(position, radius);
```

Used by:
- Flocking system (neighbor avoidance)
- Noise system (zombie activation)
- Combat target acquisition

### 3. GPU Instancing
All rendering uses `Graphics.DrawMeshInstanced`:
- Health bars: 2 draw calls for all units
- Selection circles: 1 draw call for all circles
- Batch limit: 1023 instances per call

### 4. Burst Compilation
Critical systems marked with `[BurstCompile]`:
- MovementSystem
- CombatSystem
- NoiseAlertSystem
- FlockingSystem

**Performance gain:** 10-50x faster than regular C#

---

## 🎯 Game Flow

1. **Spawn Phase**
   - Units spawn via authoring GameObjects
   - Bakers convert MonoBehaviour → ECS components

2. **Idle Phase**
   - Soldiers stand idle with subtle rotation
   - Zombies are dormant until activated

3. **Selection Phase**
   - Player selects soldiers with box/click
   - Selection circles appear under units

4. **Command Phase**
   - Right-click to issue move command
   - Units pathfind to destination

5. **Combat Phase**
   - Soldiers auto-target nearest zombie
   - Gunshots create noise events
   - Zombies activate based on probability
   - Zombies chase and attack soldiers

6. **Death Phase**
   - Units with health ≤ 0 are destroyed
   - Combat system cleans up dead targets

---

## 🛠️ Adding New Features

### Adding a New Visual Indicator

**IMPORTANT: Remember this is a 2D game using XY plane!**

```csharp
// Correct - XY plane for 2D
Vector3 position = new Vector3(unit.x, unit.y + offset, 0f);

// Wrong - XZ plane is for 3D games
Vector3 position = new Vector3(unit.x, 0f, unit.z + offset);
```

### Creating a New System

1. Create system file in `Assets/Scripts/Systems/`
2. Use hot/cold component pattern if data is split
3. Add `[BurstCompile]` for performance-critical systems
4. Update every N frames if not time-critical
5. Use spatial hash for neighbor queries

**Template:**
```csharp
[BurstCompile]
[UpdateAfter(typeof(SomeOtherSystem))]
public partial struct MySystem : ISystem
{
    private int _frameCount;
    private const int UPDATE_INTERVAL = 2;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _frameCount++;
        if (_frameCount % UPDATE_INTERVAL != 0) return;

        // Your logic here
        foreach (var (component, transform) in
            SystemAPI.Query<RefRW<MyComponent>, RefRO<LocalTransform>>())
        {
            // Process entity
        }
    }
}
```

### Adding Inspector Configuration

1. Add field to Authoring MonoBehaviour
2. Bake to ECS component in `Baker<T>`
3. Read from component in system

**Example:**
```csharp
// Authoring
public class MyAuthoring : MonoBehaviour
{
    public float myValue = 1.0f;

    class Baker : Baker<MyAuthoring>
    {
        public override void Bake(MyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new MyConfig { Value = authoring.myValue });
        }
    }
}

// Component
public struct MyConfig : IComponentData
{
    public float Value;
}
```

---

## 📊 Performance Targets

**Current Performance (2000+ units):**
- Movement: ~0.8ms per frame
- Combat: ~0.3ms per frame
- Noise: ~0.06ms per frame
- Rendering: ~0.8ms per frame
- **Total: ~2ms per frame (500+ FPS)**

**Scalability:**
- 5000 units: ~5ms per frame (200 FPS)
- 10000 units: ~12ms per frame (83 FPS)

---

## 🐛 Common Issues

### Selection Circles Not Visible
1. Create settings: **GameObject → TAB Game → Selection Circle Settings**
2. Increase **Circle Height** to -1.0 or -2.0
3. Check shader in console: Should say "Custom/SelectionCircle"
4. Verify circles are in XY plane (not XZ!)

### Units Not Moving
1. Check if pathfinding grid is initialized
2. Verify MoveCommand component is added
3. Check destination is not blocked

### Zombies Not Reacting to Gunshots
1. Increase noise **Range Multiplier** (15+ recommended)
2. Check zombie **Noise Sensitivity** (1.0 = normal)
3. Verify **Falloff Exponent** (1.2 = gentle falloff)

### Performance Issues
1. Enable frame skipping for non-critical systems
2. Reduce health bar update interval
3. Lower selection circle segments (16 instead of 32)
4. Disable showFullHealthBars

---

## 📚 Key Documentation Files

- [NOISE_SYSTEM_ENHANCED.md](NOISE_SYSTEM_ENHANCED.md) - Probabilistic noise system
- [NOISE_TUNING_GUIDE.md](NOISE_TUNING_GUIDE.md) - Inspector parameter guide
- [SELECTION_CIRCLE_SETUP.md](SELECTION_CIRCLE_SETUP.md) - Quick setup guide
- [SELECTION_CIRCLE_GUIDE.md](SELECTION_CIRCLE_GUIDE.md) - Configuration reference

---

## 🎓 Key Learnings

### XY vs XZ Plane
- **XY Plane (2D games)**: X=horizontal, Y=vertical, Z=0
  - Top-down 2D games
  - Side-scrolling platformers
  - This project!

- **XZ Plane (3D games)**: X=horizontal, Y=up/down, Z=depth
  - First-person shooters
  - 3D strategy games
  - Minecraft-like games

### ECS vs MonoBehaviour
- **Use ECS for**: Game logic, high entity counts, performance-critical systems
- **Use MonoBehaviour for**: Rendering, editor tools, singleton managers

### Hot/Cold Pattern Benefits
- Better CPU cache utilization
- Faster iteration over frequently-updated data
- 2-3x performance improvement for large entity counts

---

## 🔧 Development Tools

### Editor Menu Items
- **GameObject → TAB Game → Selection Circle Settings**: Create settings GameObject

### Debug Logging
Systems log important events with `[SystemName]` prefix:
```
[SelectionCircleRenderer] System created. Mesh: True, Material: True
[SelectionCircleRenderer] Using shader: Custom/SelectionCircle
[SelectionCircleRenderer] Rendering 4 circles. Material: True, Mesh: True
```

---

## 🚦 Getting Started

1. **Open Unity** (DOTS packages should be installed)
2. **Load scene** with soldier/zombie prefabs
3. **Enter Play Mode**
4. **Select units** with box drag or click
5. **Move units** with right-click
6. **Watch combat** as soldiers fight zombies

**Optional Setup:**
- Create SelectionCircleSettings for custom circle appearance
- Adjust soldier/zombie prefabs for different combat balance
- Tune noise parameters for different zombie behavior

---

## 📝 Notes for Claude

When working on this project:

1. **Always remember**: This is a **2D game using XY plane**
2. **Rendering**: All visual elements use Z=0 (XY plane)
3. **Health bars**: Render at `(x, y + offset, 0)`
4. **Selection circles**: Render at `(x, y - offset, 0)`
5. **Performance**: Use Burst, GPU instancing, frame skipping
6. **Architecture**: Hot/cold components, ECS systems, MonoBehaviour authoring
7. **Testing**: Check console for `[SystemName]` debug logs

**Before adding rendering:**
- Confirm plane (XY for 2D, not XZ!)
- Use GPU instancing for multiple instances
- Set Z=0 for position
- Use existing patterns (health bars, selection circles)

---

**Last Updated:** 2026-01-18
**Unity Version:** 2022.3+ (DOTS compatible)
**Project Status:** Active Development
