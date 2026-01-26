# Game Direction PRD

## Overview

**Working Title:** Crystal Souls (TAB DOTS Prototype)

**Genre:** 2D Top-Down Tactical Survival

**Core Fantasy:** A mysterious crystal meteor has crashed into a land overrun by the undead. The player commands a band of survivors who must harness the crystal's power to reclaim the world—using the very souls of the zombies they destroy.

---

## Core Gameplay Loop

```
Kill Zombies → Harvest Souls → Empower Crystal → Summon Units/Heroes → Kill More Zombies
```

### Player-Paced Progression

Unlike wave-based survival games with timers, the player controls the pace:

- **No countdown timers** forcing expansion
- **Zombies exist on the map** from the start (dormant until disturbed)
- **Player chooses when to push outward** and clear new areas
- **Crystal power attracts attention** — more power means more zombie aggression
- **Risk/reward balance:** Aggressive play speeds progression but increases danger

---

## The Crystal Meteor

### Physical Presence
- **Location:** Center of the map
- **Size:** 4x4 tile footprint
- **Appearance:** Glowing crystalline meteor with pulsing energy
- **Indestructible:** If zombies reach it, the game ends (failure condition)

### Crystal Power System

| Mechanic | Description |
|----------|-------------|
| Soul Absorption | Zombies killed within range automatically feed souls to the crystal |
| Power Level | Accumulated souls increase the crystal's power level |
| Summoning | Spend power to summon soldiers and fallen heroes |
| Threat Scaling | Higher power levels attract more/stronger zombie hordes |

### Power Thresholds (Example)

| Power Level | Unlocks | Threat Increase |
|-------------|---------|-----------------|
| 0-100 | Basic soldiers | Dormant zombies only |
| 100-500 | Veteran soldiers | Nearby zombies become alert |
| 500-1000 | Fallen Heroes (Tier 1) | Zombies actively patrol |
| 1000-2500 | Fallen Heroes (Tier 2) | Zombie hordes form |
| 2500+ | Legendary Heroes | Boss-tier zombies spawn |

---

## Factions

### The Survivors (Player Units)

**Soldiers**
- Basic ranged units
- Spawn from crystal using soul power
- Limited ammunition or stamina (future consideration)

**Fallen Heroes**
- Powerful unique units summoned at high power costs
- Each hero has distinct abilities
- Limited number can exist at once
- Examples:
  - *The Warden* — Tank with area taunt
  - *The Huntress* — Long-range sniper
  - *The Revenant* — Former zombie lord, melee devastator

### The Undead (Enemy Units)

**Zombies (Basic)**
- Slow, melee attackers
- Dormant until disturbed by noise or proximity
- Drawn to crystal energy

**Zombie Variants (Future)**
- *Runners* — Fast but fragile
- *Brutes* — Slow but heavily armored
- *Screechers* — Alert nearby zombies when they spot survivors
- *Abominations* — Boss-tier threats at high power levels

---

## Map Structure

### Zones

```
┌─────────────────────────────────────┐
│           OUTER WASTES              │
│  ┌─────────────────────────────┐    │
│  │       CORRUPTED ZONE        │    │
│  │  ┌─────────────────────┐    │    │
│  │  │    FRONTIER         │    │    │
│  │  │  ┌─────────────┐    │    │    │
│  │  │  │   HAVEN     │    │    │    │
│  │  │  │  [CRYSTAL]  │    │    │    │
│  │  │  └─────────────┘    │    │    │
│  │  └─────────────────────┘    │    │
│  └─────────────────────────────┘    │
└─────────────────────────────────────┘
```

| Zone | Description |
|------|-------------|
| Haven | Safe zone around crystal, no zombie spawns |
| Frontier | Cleared areas, occasional wanderers |
| Corrupted Zone | Active zombie presence, needs clearing |
| Outer Wastes | Dense zombie population, high-tier threats |

### Fog of War

- **Hidden:** Never explored, fully obscured
- **Explored:** Previously seen, darkened, zombies not visible
- **Visible:** Currently in soldier sight range, full visibility

---

## Win/Lose Conditions

### Victory
- Clear all zombies from the map
- (Future: Complete story objectives, defeat final boss)

### Defeat
- Crystal is destroyed (zombies reach it)
- All soldiers and heroes are killed with no power to summon more

---

## Noise & Attention System

### Current Implementation
- Gunshots create noise events
- Noise has range and intensity
- Zombies have probability-based activation
- Distance affects activation chance (exponential falloff)

### Future Enhancements
- Crystal pulses create periodic noise based on power level
- Different weapons have different noise profiles
- Silenced weapons for stealth gameplay
- Noise traps to lure zombies

---

## Future Features (Roadmap)

### Phase 1: Core Crystal (Current)
- [ ] Crystal entity at map center
- [ ] Basic soul harvesting on zombie death
- [ ] Power display UI
- [ ] Game over when crystal is reached

### Phase 2: Summoning System
- [ ] Summon soldiers from crystal
- [ ] Power cost for summoning
- [ ] Summon cooldowns
- [ ] Unit cap based on power level

### Phase 3: Heroes
- [ ] Hero unit types with unique abilities
- [ ] Hero unlock thresholds
- [ ] Hero permadeath or respawn mechanics

### Phase 4: Threat Scaling
- [ ] Power-based zombie aggression
- [ ] Dynamic spawning based on power level
- [ ] Zombie variant types
- [ ] Boss encounters

### Phase 5: Map Progression
- [ ] Zone-based map structure
- [ ] Clearable areas that stay safe
- [ ] Buildable structures (walls, towers)
- [ ] Resource nodes

---

## Technical Considerations

### ECS Architecture
- Crystal as singleton entity with power components
- Soul harvesting via combat death events
- Threat level affecting zombie AI behavior
- Summoning system queuing spawn requests

### Performance
- Soul particles/effects should be GPU instanced
- Threat calculations can run every N frames
- Zombie activation queries use spatial hash

---

## Design Principles

1. **Player Agency** — The player decides when to push, not a timer
2. **Risk/Reward** — Power brings strength but also danger
3. **Emergent Gameplay** — Systems interact to create unique situations
4. **Clear Feedback** — Crystal state and threat level always visible
5. **Satisfying Loop** — Killing zombies should feel rewarding and purposeful

---

## Open Questions

- Should heroes persist between play sessions?
- How does multiplayer factor in (if at all)?
- What's the story behind the crystal meteor?
- Should there be a "final push" objective or is map clear enough?
- How do we handle save/load for player-paced gameplay?

---

*Last Updated: 2026-01-25*
