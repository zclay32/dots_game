# Gunshot Noise Tuning Guide

Quick reference for tuning gunshot noise parameters in the Unity Inspector.

---

## Inspector Fields (SoldierAuthoring)

### Noise Range Multiplier
**Location**: Gunshot Noise → Noise Range Multiplier
**Default**: 15.0

**What it does**: Multiplies the soldier's attack range to determine maximum noise distance.

**Examples** (with attackRange = 5.0):
- `10.0` = 50 units radius (medium range)
- `15.0` = 75 units radius (large range) ⭐ Default
- `20.0` = 100 units radius (very large range)
- `25.0` = 125 units radius (extreme range)

### Noise Intensity
**Location**: Gunshot Noise → Noise Intensity
**Default**: 2.0

**What it does**: Base volume multiplier for gunshots. Values above 1.0 are clamped to 100% activation at close range.

**Examples**:
- `1.0` = Normal gunshot
- `1.5` = Loud gunshot
- `2.0` = Very loud gunshot ⭐ Default
- `2.5` = Extremely loud (guarantees 100% activation within ~60% of max range)

**Note**: Higher values extend the "guaranteed activation zone" further from the gunshot.

### Noise Falloff Exponent
**Location**: Gunshot Noise → Noise Falloff Exponent
**Default**: 1.2

**What it does**: Controls how quickly sound fades with distance. Lower = gentle falloff, Higher = steep falloff.

**Examples**:
- `1.0` = Linear falloff (gentle, distant zombies have good chance)
- `1.2` = Very gentle falloff (almost linear) ⭐ Default
- `1.5` = Moderate falloff
- `2.0` = Quadratic falloff (steep, realistic)
- `3.0` = Cubic falloff (very steep, only close zombies react)

---

## Preset Configurations

### Maximum Chaos (Attract All Zombies)
```
Range Multiplier: 25.0
Intensity: 3.0
Falloff Exponent: 1.0
```
**Result**: 125 unit radius, nearly guaranteed activation even at extreme distance

### Balanced Gameplay (Default)
```
Range Multiplier: 15.0
Intensity: 2.0
Falloff Exponent: 1.2
```
**Result**: 75 unit radius, strong activation up to 40m, decent chance up to 70m

### Stealth-Friendly (Quiet Gunfire)
```
Range Multiplier: 8.0
Intensity: 1.0
Falloff Exponent: 2.0
```
**Result**: 40 unit radius, realistic falloff, only nearby zombies guaranteed to hear

### Suppressed Weapon
```
Range Multiplier: 5.0
Intensity: 0.5
Falloff Exponent: 2.5
```
**Result**: 25 unit radius, low intensity, very steep falloff - minimal zombie attraction

---

## Activation Probability Examples

### Current Default (15x, 2.0 intensity, 1.2 exponent):

| Distance | Probability |
|----------|-------------|
| 0m       | 100% (capped) |
| 20m      | 100% (capped) |
| 40m      | 100% (capped) |
| 50m      | 79% |
| 60m      | 55% |
| 70m      | 27% |
| 75m+     | 0% |

### Stealth-Friendly (8x, 1.0 intensity, 2.0 exponent):

| Distance | Probability |
|----------|-------------|
| 0m       | 100% |
| 10m      | 94% |
| 20m      | 75% |
| 30m      | 44% |
| 40m+     | 0% |

### Maximum Chaos (25x, 3.0 intensity, 1.0 exponent):

| Distance | Probability |
|----------|-------------|
| 0m       | 100% (capped) |
| 30m      | 100% (capped) |
| 60m      | 100% (capped) |
| 90m      | 100% (capped) |
| 100m     | 80% |
| 120m     | 32% |
| 125m+    | 0% |

---

## Probability Formula

```
probability = intensity × (1 - (distance / maxRadius)^exponent)
maxRadius = attackRange × rangeMultiplier
```

Clamped to [0, 1] range (0% to 100%).

---

## Tuning Tips

### To make gunfire attract more zombies:
1. **Increase Range Multiplier** (15 → 20+) - Expands affected area
2. **Increase Intensity** (2.0 → 3.0) - Makes distant zombies more likely to react
3. **Decrease Falloff Exponent** (1.2 → 1.0) - Gentler falloff curve

### To make gunfire more stealthy:
1. **Decrease Range Multiplier** (15 → 8) - Smaller affected area
2. **Decrease Intensity** (2.0 → 1.0) - Lower activation probability
3. **Increase Falloff Exponent** (1.2 → 2.0) - Steeper falloff curve

### To create cascading zombie waves:
- Use current default settings (15x, 2.0, 1.2)
- Nearby zombies react instantly (100% chance)
- Distant zombies gradually notice over multiple shots
- Creates organic, wave-like zombie convergence

---

## Performance Notes

**Range Multiplier Impact on Performance**:
- Higher values check more zombies (wider spatial hash search)
- `15x` (75 units): ~0.1-0.2ms overhead
- `25x` (125 units): ~0.2-0.4ms overhead
- Still well within performance budget for 2000 zombies

**Recommendation**: Keep Range Multiplier ≤ 20 for optimal performance while maintaining large attraction radius.

---

## Testing Workflow

1. **Start with defaults** (15x, 2.0, 1.2)
2. **Observe zombie behavior** during combat
3. **Adjust one parameter at a time**:
   - Too few zombies reacting? → Increase Range or Intensity
   - Too many zombies reacting? → Decrease Range or Intensity
   - Falloff too steep/gentle? → Adjust Exponent
4. **Test with different zombie densities** (100, 500, 1000+ zombies)
5. **Check performance** in Profiler (should be < 0.5ms per frame)

---

## Per-Zombie Sensitivity

Don't forget zombies also have their own sensitivity settings in ZombieAuthoring:

```
Noise Sensitivity Multiplier: 1.0 (normal), 2.0 (sensitive), 0.5 (deaf)
Min Activation Probability: 0.0 (can ignore distant gunfire)
Max Activation Probability: 1.0 (can be fully activated)
```

You can create zombie variants by adjusting these values!

---

## Visual Debug (Future Enhancement)

Consider adding debug visualization:
- Draw noise radius in Scene view (Gizmos)
- Color-code activation probability zones
- Show which zombies are within range
- Display actual probability per zombie

This would make tuning much easier!
