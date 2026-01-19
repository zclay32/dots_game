# Selection Circle Configuration Guide

Quick reference for tuning selection circle appearance in the Unity Inspector.

---

## Finding the System

1. Enter Play Mode in Unity
2. Open the **Hierarchy** window
3. Look for the system GameObject (auto-created by ECS)
4. Find **SelectionCircleRenderer** in the Inspector

---

## Inspector Parameters

### Circle Radius
**Default**: 0.8

**What it does**: Controls the size of the selection circle.

**Examples**:
- `0.5` = Small circle (tight around unit)
- `0.8` = Medium circle ⭐ Default
- `1.2` = Large circle (very visible)
- `1.5` = Extra large circle

**Recommendation**: Start with 0.8, adjust if circles are too small/large.

---

### Circle Height
**Default**: 0.5

**What it does**: Vertical offset from unit position. **IMPORTANT**: Increase this if circles render under the unit sprite!

**Examples**:
- `0.01` = Ground level (may render under sprite)
- `0.5` = Above most sprites ⭐ Default
- `1.0` = Well above units
- `2.0` = Very high (floating circle)

**Troubleshooting**:
- Circle not visible? → **Increase this value to 1.0 or higher**
- Circle too high? → Decrease to 0.3-0.5

---

### Circle Thickness
**Default**: 0.15

**Range**: 0.05 to 0.5

**What it does**: Controls how thick the ring is.

**Examples**:
- `0.05` = Very thin line
- `0.15` = Medium thickness ⭐ Default
- `0.3` = Thick ring
- `0.5` = Very thick (almost filled circle)

---

### Selection Color
**Default**: Bright Green (R: 0.2, G: 1.0, B: 0.2, A: 0.8)

**What it does**: Color and transparency of the selection circle.

**Common Presets**:
- **Bright Green** (default): R: 0.2, G: 1.0, B: 0.2, A: 0.8
- **Bright Blue**: R: 0.2, G: 0.5, B: 1.0, A: 0.8
- **Yellow**: R: 1.0, G: 1.0, B: 0.2, A: 0.8
- **White**: R: 1.0, G: 1.0, B: 1.0, A: 0.8

**Tips**:
- Alpha < 0.5: Very transparent (subtle)
- Alpha > 0.8: Very opaque (bold)

---

### Circle Segments
**Default**: 32

**Range**: 8 to 64

**What it does**: Number of segments in the circle mesh. Higher = smoother circle.

**Examples**:
- `8` = Octagon (angular, best performance)
- `16` = Visible segments but acceptable
- `32` = Smooth circle ⭐ Default
- `64` = Very smooth (slight performance cost)

**Performance Impact**:
- 8 segments: ~0.05ms for 100 circles
- 32 segments: ~0.1ms for 100 circles
- 64 segments: ~0.15ms for 100 circles

**Recommendation**: Keep at 32 unless you need better performance (use 16) or smoother circles (use 48).

---

## Troubleshooting

### Problem: Circles not visible at all

**Solutions**:
1. **Increase Circle Height to 1.0 or higher** (most common fix)
2. Check Selection Color alpha is not 0
3. Verify units are actually selected (Selected component added)
4. Check if camera can see the height where circles render

### Problem: Circles render under unit sprites

**Solution**: Increase **Circle Height** to 0.5 or higher until circles appear above sprites.

### Problem: Circles too small/hard to see

**Solutions**:
1. Increase **Circle Radius** to 1.0-1.5
2. Increase **Circle Thickness** to 0.25-0.4
3. Make **Selection Color** more opaque (Alpha = 1.0)
4. Change to brighter color (white or yellow)

### Problem: Circles look blocky/angular

**Solution**: Increase **Circle Segments** to 48 or 64.

### Problem: Performance issues with many selected units

**Solutions**:
1. Decrease **Circle Segments** to 16 or 8
2. System already uses GPU instancing (minimal overhead)

---

## Recommended Configurations

### Default (Visible & Performant)
```
Circle Radius: 0.8
Circle Height: 0.5
Circle Thickness: 0.15
Selection Color: Green (0.2, 1.0, 0.2, 0.8)
Circle Segments: 32
```

### High Visibility (Easy to See)
```
Circle Radius: 1.2
Circle Height: 0.8
Circle Thickness: 0.25
Selection Color: Yellow (1.0, 1.0, 0.2, 1.0)
Circle Segments: 32
```

### Performance Optimized (Maximum FPS)
```
Circle Radius: 0.6
Circle Height: 0.5
Circle Thickness: 0.15
Selection Color: Green (0.2, 1.0, 0.2, 0.8)
Circle Segments: 16
```

### Subtle (Minimal Visual)
```
Circle Radius: 0.5
Circle Height: 0.3
Circle Thickness: 0.1
Selection Color: White (1.0, 1.0, 1.0, 0.4)
Circle Segments: 24
```

---

## Real-time Tuning

**Good News**: All parameters update in real-time during Play Mode!

**Workflow**:
1. Enter Play Mode
2. Select some soldiers
3. Find SelectionCircleRenderer in Hierarchy
4. Adjust parameters in Inspector
5. See changes immediately
6. Once happy, copy values to a note
7. Exit Play Mode
8. Create a MonoBehaviour with these defaults (if desired)

---

## Technical Notes

- **Rendering Method**: GPU instancing (1-2 draw calls for all circles)
- **Performance**: ~0.1ms for 100 selected units
- **Transparency**: Uses alpha blending (transparent queue)
- **Z-Ordering**: Renders in transparent pass (after opaque geometry)

---

## Future Enhancements

Possible improvements:
- Pulsing animation (scale up/down over time)
- Different colors for different selection groups
- Show unit facing direction with a small arrow
- Animated rotation for extra visibility
- Health bar integration (change color based on health)
