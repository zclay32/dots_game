# Zombie Combat System - Product Requirements Document

## Overview

This document defines the zombie combat behavior using a state machine approach. The system controls how zombies move, acquire targets, and attack player units.

---

## State Machine

### States

| State | Description | Movement | Has Target |
|-------|-------------|----------|------------|
| **Idle** | No target, standing still | None | No |
| **Wandering** | No target, slowly moving within wander range | Slow (25% speed) | No |
| **Chasing** | Has target, moving toward it | Full speed | Yes |
| **WindingUp** | Has target, stopped, preparing to attack | None | Yes |
| **Attacking** | Has target, dealing damage | None | Yes |
| **Cooldown** | Has target, recovering from attack | None | Yes |

### State Transitions

```
┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│  ┌──────┐    no target     ┌───────────┐                               │
│  │ Idle │◄────────────────►│ Wandering │                               │
│  └──┬───┘   wander timer   └─────┬─────┘                               │
│     │                            │                                      │
│     │ target acquired            │ target acquired                      │
│     ▼                            ▼                                      │
│  ┌─────────────────────────────────┐                                   │
│  │           Chasing               │◄──────────────────────┐           │
│  └──────────────┬──────────────────┘                       │           │
│                 │                                          │           │
│                 │ in range + facing + stopped              │           │
│                 ▼                                          │           │
│  ┌─────────────────────────────────┐                       │           │
│  │          WindingUp              │                       │           │
│  └──────────────┬──────────────────┘                       │           │
│                 │                                          │           │
│                 │ windup timer complete                    │           │
│                 ▼                                          │           │
│  ┌─────────────────────────────────┐                       │           │
│  │          Attacking              │───── target dead ─────┼───► Idle  │
│  └──────────────┬──────────────────┘      or lost          │           │
│                 │                                          │           │
│                 │ damage dealt                             │           │
│                 ▼                                          │           │
│  ┌─────────────────────────────────┐                       │           │
│  │          Cooldown               │                       │           │
│  └──────────────┬──────────────────┘                       │           │
│                 │                                          │           │
│                 │ cooldown complete                        │           │
│                 │                                          │           │
│                 ├─── target alive + in range ──► Attacking │           │
│                 │                                          │           │
│                 └─── target moved out of range ────────────┘           │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## State Details

### Idle
- **Entry Condition**: No target, not wandering
- **Behavior**: Stand still, slow idle rotation animation
- **Exit Conditions**:
  - Target acquired → **Chasing**
  - Wander timer expires → **Wandering**

### Wandering
- **Entry Condition**: No target, wander timer expired
- **Behavior**:
  - Move slowly (25% of max speed) toward a random point
  - Random point is within `wanderRadius` of spawn position
  - New wander point selected when current one is reached
- **Exit Conditions**:
  - Target acquired → **Chasing**
  - Wander duration expires → **Idle**
- **Configuration**:
  - `wanderRadius`: Maximum distance from spawn point (default: 5 units)
  - `wanderSpeed`: Fraction of max speed (default: 0.25)
  - `wanderDuration`: How long to wander before returning to idle (default: 3-8 seconds)

### Chasing
- **Entry Condition**: Target acquired (via detection or being attacked)
- **Behavior**:
  - Move at full speed toward target
  - Update target position each frame
  - Can switch targets if:
    - Attacked by a different player unit (immediate switch)
    - A closer player unit enters aggro range
- **Exit Conditions**:
  - In attack range + facing target + stopped → **WindingUp**
  - Target dies or is lost → **Idle**
- **Configuration**:
  - `aggroRadius`: Detection range when alert (default: 15 units)
  - `attackRange`: Distance to stop and attack (default: 1 unit)

### WindingUp
- **Entry Condition**: First time reaching attack position after chasing
- **Behavior**:
  - Stand still, face target
  - Countdown windup timer
  - Visual telegraph (future: animation/particle)
- **Exit Conditions**:
  - Windup timer complete → **Attacking**
  - Target moves out of range → **Chasing**
  - Target dies → **Idle**
- **Configuration**:
  - `windupDuration`: Time before first attack (default: 0.5 seconds)
- **Note**: WindingUp only occurs on initial engagement. Subsequent attacks after cooldown skip this state.

### Attacking
- **Entry Condition**: WindingUp complete OR Cooldown complete (if target still in range)
- **Behavior**:
  - Deal damage to entities in attack cone
  - Single frame execution (instant damage)
- **Exit Conditions**:
  - Damage dealt → **Cooldown**
- **Configuration**:
  - `attackDamage`: Damage per hit (default: 10)
  - `attackConeAngle`: Width of damage cone (default: 45 degrees)
  - `attackConeRange`: Depth of damage cone (default: 1 unit, same as attackRange)

### Cooldown
- **Entry Condition**: Attack completed
- **Behavior**:
  - Stand still, face target
  - Countdown cooldown timer
- **Exit Conditions**:
  - Cooldown complete + target in range → **Attacking** (skip WindingUp)
  - Cooldown complete + target out of range → **Chasing**
  - Target dies → **Idle**
- **Configuration**:
  - `cooldownDuration`: Time between attacks (default: 1 second)

---

## Target Acquisition

### Priority System
1. **Attacked by player unit** → Immediately target attacker (highest priority)
2. **Current target still valid** → Keep current target
3. **Player unit in aggro range** → Target closest player unit

### Target Validity
A target is valid if:
- Entity exists
- Entity has Health component
- Entity is not dead (Health.Current > 0)
- Entity is a player unit (has PlayerUnit tag)

### Target Loss
Target is cleared when:
- Target entity is destroyed
- Target dies (Health.Current <= 0)
- Target moves beyond chase range (optional, can be disabled for persistent aggro)

---

## Configuration Summary

### ZombieAuthoring Inspector Fields

```csharp
[Header("Movement")]
public float moveSpeed = 2f;

[Header("Combat")]
public float attackDamage = 10f;
public float attackRange = 1f;
public float attackCooldown = 1f;
public float attackWindup = 0.5f;
public float attackConeAngle = 45f;  // degrees

[Header("AI")]
public float alertRadius = 3f;      // Detection when dormant/idle
public float aggroRadius = 15f;     // Detection when alert/chasing
public float wanderRadius = 5f;     // Max distance from spawn when wandering
public float wanderSpeedMultiplier = 0.25f;
```

---

## Component Structure

### ZombieCombatState (Hot - Updated Every Frame)
```csharp
public struct ZombieCombatState : IComponentData
{
    public ZombieAIState State;      // Current state machine state
    public float StateTimer;          // Countdown timer for current state
    public Entity CurrentTarget;      // Current combat target
    public bool HasTarget;            // Whether target is valid
}
```

### ZombieCombatConfig (Cold - Read Only)
```csharp
public struct ZombieCombatConfig : IComponentData
{
    public float AttackDamage;
    public float AttackRange;
    public float AttackCooldown;
    public float AttackWindup;
    public float AttackConeAngle;
    public float AggroRadius;
    public float AlertRadius;
    public float WanderRadius;
    public float WanderSpeedMultiplier;
    public float2 SpawnPosition;      // For wander range calculation
}
```

### State Enum
```csharp
public enum ZombieAIState : byte
{
    Idle = 0,
    Wandering = 1,
    Chasing = 2,
    WindingUp = 3,
    Attacking = 4,
    Cooldown = 5
}
```

---

## System Execution Order

1. **TargetFindingSystem** - Finds and assigns targets
2. **ZombieStateMachineSystem** - Updates state machine, handles transitions
3. **ZombieMovementSystem** - Moves zombies based on current state
4. **ZombieCombatSystem** - Executes attacks when in Attacking state
5. **FacingRotationSystem** - Rotates zombies to face targets

---

## Future Enhancements

### Phase 2
- Visual telegraph for WindingUp state (red glow, raised arms)
- Attack animation
- Sound effects per state

### Phase 3
- Multiple zombie types with different stats
- Special attacks (lunge, grab, spit)
- Group behavior (flanking, surrounding)

### Phase 4
- Zombie alerting nearby zombies when attacking
- Horde behavior (all zombies in area chase same target)
- Boss zombies with unique state machines

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2026-01-19 | 1.0 | Initial PRD - State machine design |
