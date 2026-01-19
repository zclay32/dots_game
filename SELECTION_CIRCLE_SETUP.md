# Selection Circle Setup Guide

Quick 2-minute setup to add configurable selection circles to your game.

---

## Step 1: Create Settings GameObject

1. In Unity, go to **Hierarchy** window
2. Right-click → **Create Empty**
3. Name it **"SelectionCircleSettings"**
4. In the Inspector, click **Add Component**
5. Search for **"Selection Circle Settings"**
6. Add the component

**Done!** The selection circles will now render with default settings.

---

## Step 2: Configure Appearance (Optional)

With the SelectionCircleSettings GameObject selected, you'll see these fields in the Inspector:

### Circle Appearance

**Circle Radius**: `0.8` (default)
- How big the circle is
- Try 1.0-1.5 if too small

**Circle Height**: `0.5` (default)
- **Most Important!** Vertical offset from unit
- **If circles not visible**: Increase to 1.0 or 2.0
- Adjusts height above ground

**Circle Thickness**: `0.15` (default)
- How thick the ring is
- 0.1 = thin, 0.3 = thick

**Selection Color**: Bright Green (default)
- Click the color box to change
- Adjust alpha for transparency

### Performance

**Circle Segments**: `32` (default)
- Smoothness of circle
- 16 = better performance, 64 = smoother

---

## Step 3: Test It Out

1. Enter Play Mode
2. Select some soldiers (box select or click)
3. You should see green circles under selected units
4. **If not visible**: Increase "Circle Height" to 1.0 or 2.0

---

## Troubleshooting

### Circles not showing at all

**Fix**: Increase **Circle Height** to 1.0 or higher
- Circles may be rendering under the soldier sprites
- Try values: 0.5, 1.0, 1.5, 2.0

### Circles too small

**Fix**: Increase **Circle Radius** to 1.0-1.5

### Circles look blocky

**Fix**: Increase **Circle Segments** to 48 or 64

### Can't find the settings

Make sure you:
1. Created a GameObject in the scene
2. Added the **SelectionCircleSettings** component
3. The component should appear in the Inspector

---

## Real-Time Tuning

All settings update **live in Play Mode**!

1. Enter Play Mode
2. Select some units
3. Adjust settings in Inspector
4. See changes immediately
5. When happy, exit Play Mode
6. **Important**: Settings persist on the GameObject

---

## Default Settings (If No GameObject Exists)

The system will still work with these defaults:
- Radius: 0.8
- Height: 0.5
- Thickness: 0.15
- Color: Bright Green
- Segments: 32

But you won't be able to change them without the settings GameObject!

---

## Advanced: Multiple Configurations

Want different settings for different scenes?

1. Create SelectionCircleSettings GameObject in each scene
2. Configure each one differently
3. Each scene will have its own appearance

---

## Quick Start TL;DR

```
1. Create Empty GameObject
2. Add "Selection Circle Settings" component
3. If circles not visible → Increase "Circle Height" to 1.0
4. Done!
```

That's it! Selection circles should now appear under selected soldiers.
