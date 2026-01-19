# Hot/Cold Component Data Optimization

## Overview

This optimization separates frequently-accessed "hot" data from rarely-accessed "cold" data into separate components. This improves CPU cache locality and reduces memory bandwidth, leading to better performance in DOTS systems.

## The Problem

The original `Combat` and `Health` components mixed hot and cold data:

```csharp
// BAD: Mixed hot and cold data
public struct Combat : IComponentData
{
    // COLD (config - rarely changes)
    public float AttackDamage;
    public float AttackRange;
    public float AttackCooldown;
    public float AttackWindup;

    // HOT (state - changes every frame)
    public float CurrentCooldown;
    public float CurrentWindup;
}
```

When a system iterates over entities to update `CurrentCooldown`, the CPU must load the entire component into cache, including the rarely-used config values. This wastes cache lines and memory bandwidth.

## The Solution

Split components into hot and cold variants:

```csharp
// GOOD: Hot data in separate component
public struct CombatState : IComponentData
{
    public float CurrentCooldown;  // Updated every frame
    public float CurrentWindup;    // Updated every frame
}

// GOOD: Cold data in separate component
public struct CombatConfig : IComponentData
{
    public float AttackDamage;     // Rarely accessed
    public float AttackRange;      // Rarely accessed
    public float AttackCooldown;   // Rarely accessed
    public float AttackWindup;     // Rarely accessed
}
```

## Performance Benefits

### Before (Mixed Data)
- **Cache line usage**: ~24 bytes (6 floats × 4 bytes)
- **Useful data per iteration**: ~8 bytes (2 floats)
- **Cache efficiency**: 33% (wasting 16 bytes per entity)
- **Entities per 64-byte cache line**: ~2.6 entities

### After (Separated Data)
- **Cache line usage**: ~8 bytes (2 floats × 4 bytes)
- **Useful data per iteration**: ~8 bytes (2 floats)
- **Cache efficiency**: 100% (no wasted bytes)
- **Entities per 64-byte cache line**: 8 entities

**Result**: 3x more entities fit in the same cache, reducing cache misses and memory bandwidth.

## Expected Performance Gains

- **UpdateCombatTimersJob**: 2-3x faster (only touches CombatState)
- **Overall combat system**: 20-30% faster (better cache locality)
- **Scalability**: Linear improvement with entity count (1000+ entities)

## Migration Strategy

The optimization uses a gradual migration approach:

1. **Both old and new components exist** on all entities
2. **ComponentMigrationSystem** adds new components at startup
3. **ComponentSyncSystem** keeps old and new components in sync
4. **Systems can be migrated one at a time** to use new components
5. **Once all systems are migrated**, remove old components and sync system

### Current Status

- ✅ New components defined: `CombatState`, `CombatConfig`, `HealthCurrent`, `HealthMax`
- ✅ Migration system created
- ✅ Sync system created (keeps old and new in sync)
- ✅ Authoring scripts updated (add both old and new components)
- ✅ Optimized combat system created (`CombatSystemOptimized`)
- ⏳ Other systems still use legacy components

### How to Enable

1. **Test the optimized system**:
   - Open Unity Inspector
   - Find `CombatSystemOptimized` in the Systems window
   - Enable it
   - Disable the old `CombatSystem`

2. **Verify it works**:
   - Both systems should behave identically
   - ComponentSyncSystem keeps them in sync
   - Check performance using Unity Profiler

3. **Migrate other systems** (optional):
   - Update systems one at a time to use new components
   - Test after each migration
   - Once all migrated, remove old components

## Component Reference

### Combat Components

| Component | Type | Size | Accessed By |
|-----------|------|------|-------------|
| `CombatState` | Hot | 8 bytes | Every frame (timer updates) |
| `CombatConfig` | Cold | 16 bytes | Only when attacking |
| `Combat` (legacy) | Mixed | 24 bytes | Deprecated |

### Health Components

| Component | Type | Size | Accessed By |
|-----------|------|------|-------------|
| `HealthCurrent` | Hot | 4 bytes | When taking damage, death checks |
| `HealthMax` | Cold | 4 bytes | Only for health bar rendering |
| `Health` (legacy) | Mixed | 8 bytes | Deprecated |

## Code Examples

### Updating Combat Timers (Hot Path)

```csharp
// OPTIMIZED: Only accesses hot CombatState (8 bytes per entity)
[BurstCompile]
public partial struct UpdateCombatTimersOptimizedJob : IJobEntity
{
    public float DeltaTime;

    void Execute(ref CombatState combatState)
    {
        // Perfect cache locality - all data is hot
        if (combatState.CurrentCooldown > 0)
            combatState.CurrentCooldown -= DeltaTime;

        if (combatState.CurrentWindup > 0)
            combatState.CurrentWindup -= DeltaTime;
    }
}
```

### Processing Attacks (Mixed Hot/Cold)

```csharp
// Access hot state first, cold config only when needed
foreach (var (combatState, combatConfig, target) in
    SystemAPI.Query<RefRW<CombatState>, RefRO<CombatConfig>, RefRO<CombatTarget>>())
{
    // Check hot state first (fast path)
    if (!combatState.ValueRO.CanAttack)
        continue;

    // Only access cold config when actually attacking (rare)
    if (inRange)
    {
        damage = combatConfig.ValueRO.AttackDamage;
        combatState.ValueRW.CurrentCooldown = combatConfig.ValueRO.AttackCooldown;
    }
}
```

## Further Reading

- [Data-Oriented Design](https://www.dataorienteddesign.com/dodbook/)
- [CPU Cache and Memory Hierarchy](https://mechanical-sympathy.blogspot.com/)
- [Unity DOTS Best Practices](https://docs.unity3d.com/Packages/com.unity.entities@latest)
