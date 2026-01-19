# DOTS Game Engine

A high-performance 2D tactical combat engine built with Unity DOTS (Data-Oriented Technology Stack), designed to handle thousands of simultaneous units with smooth performance.

## Purpose

This project is a **learning exercise** exploring how to build a game engine capable of supporting massive numbers of entities (5,000-10,000+ units) while maintaining high frame rates. It serves as a foundation that could be extended into various game types:

- Tower defense games
- Real-time strategy (RTS) games
- Survival games with large enemy hordes
- Colony management simulations
- Any game requiring large-scale unit management

The codebase is intentionally **engine-agnostic in design philosophy** - while built on Unity DOTS, the architectural patterns and optimization strategies can be applied to other engines or custom implementations.

## What You'll Learn

This project demonstrates:

- **Entity Component System (ECS) architecture** - Separating data (components) from logic (systems)
- **Data-oriented design** - Optimizing for CPU cache performance with hot/cold data separation
- **Burst compilation** - Leveraging SIMD and native code generation for 10-50x performance gains
- **GPU instancing** - Rendering thousands of units with minimal draw calls
- **Spatial partitioning** - Efficient neighbor queries using spatial hashing
- **Scalable AI systems** - Probabilistic and distance-based decision making that scales

## Core Systems

| System | Description |
|--------|-------------|
| **Unit Selection** | Box and click selection with multi-select support |
| **Movement & Pathfinding** | A* pathfinding with flocking behavior to prevent overlap |
| **Combat** | Ranged and melee combat with configurable damage, range, and cooldowns |
| **Noise/Alert** | Probabilistic alert system where actions (like gunshots) attract nearby enemies |
| **Health & Death** | Entity health tracking with automatic cleanup |
| **Rendering** | GPU-instanced health bars and selection indicators |

## Performance Targets

The engine is optimized for large unit counts:

| Unit Count | Frame Time | FPS |
|------------|------------|-----|
| 2,000 | ~2ms | 500+ |
| 5,000 | ~5ms | 200 |
| 10,000 | ~12ms | 83 |

## Architecture Highlights

### Hot/Cold Component Pattern

Frequently-updated data is separated from static configuration for better cache utilization:

```csharp
// HOT - updated every frame
public struct HealthCurrent : IComponentData { public float Value; }

// COLD - read-only configuration
public struct HealthMax : IComponentData { public float Value; }
```

### Frame Skipping

Non-critical systems update every N frames to reduce CPU load:

```csharp
if (_frameCount % UPDATE_INTERVAL != 0) return;
```

### Spatial Hashing

Efficient O(1) neighbor queries for flocking, combat targeting, and alert propagation.

## Getting Started

### Prerequisites

- Unity 2022.3 or later with DOTS packages
- Basic understanding of Unity and C#

### Running the Project

1. Clone the repository
2. Open the project in Unity
3. Open `Assets/Scenes/SampleScene.unity`
4. Enter Play Mode
5. Select units with left-click drag, command them with right-click

## Project Structure

```
Assets/
├── Scripts/
│   ├── Components/     # ECS data structures
│   ├── Systems/        # ECS game logic
│   ├── Authoring/      # MonoBehaviour → ECS conversion
│   └── Managers/       # Non-ECS singleton managers
├── Shaders/            # Custom rendering shaders
└── Prefabs/            # Unit prefabs
```

## Extending the Engine

The modular architecture makes it straightforward to add:

- New unit types (add Authoring + Components)
- New behaviors (add Systems that query relevant components)
- New visual feedback (follow GPU instancing patterns)
- New AI behaviors (extend the noise/alert system)

See `CLAUDE.md` for detailed documentation on the codebase architecture and patterns.

## Roadmap

This project is in active development. Below is the feature roadmap showing current progress and planned additions.

### Phase 1: Core Unit Mechanics (Current)
- [x] Unit selection (box select, click select, multi-select)
- [x] Movement and pathfinding (A* with flocking)
- [x] Combat system (ranged and melee)
- [x] Health system with visual feedback
- [x] Noise/alert system for enemy AI
- [x] GPU-instanced rendering for large unit counts
- [ ] Unit formations and group behaviors
- [ ] Line of sight and fog of war

### Phase 2: World & Environment
- [ ] Isometric grid layout
- [ ] Terrain types with movement modifiers
- [ ] Obstacles and environmental hazards
- [ ] Day/night cycle affecting gameplay
- [ ] Minimap with unit indicators

### Phase 3: Buildings & Defenses
- [ ] Building placement system
- [ ] Defensive structures (walls, towers, traps)
- [ ] Unit production buildings
- [ ] Building health and destruction
- [ ] Repair mechanics

### Phase 4: Economy & Resources
- [ ] Resource nodes (wood, stone, iron, etc.)
- [ ] Worker units and resource gathering
- [ ] Resource storage and management
- [ ] Supply chains and logistics

### Phase 5: Progression Systems
- [ ] Technology/research tree
- [ ] Unit upgrades and veterancy
- [ ] Unlockable buildings and units
- [ ] Difficulty scaling

### Phase 6: Wave & Threat Systems
- [ ] Wave spawning system
- [ ] Escalating difficulty between waves
- [ ] Special enemy types and bosses
- [ ] Directional threat indicators
- [ ] Victory and defeat conditions

### Phase 7: Polish & Quality of Life
- [ ] Save/load system
- [ ] Audio system (music, SFX)
- [ ] Particle effects for combat and abilities
- [ ] UI/UX improvements
- [ ] Tutorial and onboarding

## Documentation

- [CLAUDE.md](CLAUDE.md) - Comprehensive codebase documentation
- [NOISE_SYSTEM_ENHANCED.md](NOISE_SYSTEM_ENHANCED.md) - Probabilistic alert system details
- [OPTIMIZATION_SUMMARY.md](OPTIMIZATION_SUMMARY.md) - Performance optimization techniques

## License

This project is available under the MIT License. Feel free to use it as a learning resource or foundation for your own projects.

## Acknowledgments

This project is heavily inspired by [They Are Billions](https://www.intensecoregames.com/en/theyarebillions), a real-time strategy survival game by Numantian Games. TAB's ability to render thousands of zombies swarming a colony while maintaining smooth gameplay is a technical achievement that sparked the curiosity behind this project.

The goal here is to understand and implement the technical foundations that make such large-scale games possible - not to recreate TAB, but to learn from its design and build a flexible engine that others can use as a starting point for their own projects.
