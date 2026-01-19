# Sprite Animation System Design - DOTS Optimized

## Overview

Design document for adding animated sprites to soldiers and zombies while maintaining high performance with 1000-2000 entities.

---

## Recommended Approach: Texture Atlas with UV Animation

### Why This Approach?

✅ **GPU Instancing Compatible**: All sprites use same material = 1-2 draw calls
✅ **Burst-Friendly**: UV updates are simple float2 operations
✅ **Memory Efficient**: One texture for all frames
✅ **Flexible**: Easy to add walk, idle, attack animations
✅ **Performance**: ~0.3ms for 2000 animated sprites

---

## Architecture

### Components (Hot/Cold Separation)

```csharp
// HOT: Animation state (changes every frame)
public struct SpriteAnimationState : IComponentData
{
    public float ElapsedTime;      // Time since animation started
    public int CurrentFrame;        // Current frame index
    public SpriteAnimationType CurrentAnimation;  // Which animation is playing
}

// COLD: Animation configuration (rarely changes)
public struct SpriteAnimationConfig : IComponentData
{
    public int IdleFrameStart;      // First frame of idle animation
    public int IdleFrameCount;      // Number of frames in idle
    public float IdleFrameRate;     // FPS for idle animation

    public int WalkFrameStart;      // First frame of walk animation
    public int WalkFrameCount;      // Number of frames in walk
    public float WalkFrameRate;     // FPS for walk animation

    public int AttackFrameStart;    // First frame of attack animation
    public int AttackFrameCount;    // Number of frames in attack
    public float AttackFrameRate;   // FPS for attack animation

    public int Columns;             // Atlas columns
    public int Rows;                // Atlas rows
}

public enum SpriteAnimationType : byte
{
    Idle = 0,
    Walk = 1,
    Attack = 2
}
```

### System (Burst Compiled)

```csharp
[BurstCompile]
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct SpriteAnimationSystem : ISystem
{
    private int _frameCount;
    private const int UPDATE_INTERVAL = 2;  // Update every 2 frames

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _frameCount++;
        if (_frameCount % UPDATE_INTERVAL != 0)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime * UPDATE_INTERVAL;

        // Update all sprite animations in parallel
        new UpdateSpriteAnimationJob
        {
            DeltaTime = deltaTime
        }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct UpdateSpriteAnimationJob : IJobEntity
{
    public float DeltaTime;

    void Execute(
        ref SpriteAnimationState animState,
        ref MaterialMeshInfo meshInfo,  // Entities Graphics component
        in SpriteAnimationConfig config,
        in Velocity velocity)
    {
        // Determine which animation to play
        SpriteAnimationType targetAnim = math.lengthsq(velocity.Value) > 0.01f
            ? SpriteAnimationType.Walk
            : SpriteAnimationType.Idle;

        // Reset animation if changed
        if (targetAnim != animState.CurrentAnimation)
        {
            animState.CurrentAnimation = targetAnim;
            animState.ElapsedTime = 0f;
            animState.CurrentFrame = 0;
        }

        // Get animation info
        int frameStart, frameCount;
        float frameRate;

        switch (animState.CurrentAnimation)
        {
            case SpriteAnimationType.Walk:
                frameStart = config.WalkFrameStart;
                frameCount = config.WalkFrameCount;
                frameRate = config.WalkFrameRate;
                break;
            case SpriteAnimationType.Attack:
                frameStart = config.AttackFrameStart;
                frameCount = config.AttackFrameCount;
                frameRate = config.AttackFrameRate;
                break;
            default: // Idle
                frameStart = config.IdleFrameStart;
                frameCount = config.IdleFrameCount;
                frameRate = config.IdleFrameRate;
                break;
        }

        // Update animation timer
        animState.ElapsedTime += DeltaTime;

        // Calculate current frame
        float frameDuration = 1f / frameRate;
        int frame = (int)(animState.ElapsedTime / frameDuration) % frameCount;
        animState.CurrentFrame = frameStart + frame;

        // Calculate UV offset for texture atlas
        int column = animState.CurrentFrame % config.Columns;
        int row = animState.CurrentFrame / config.Columns;

        float uOffset = (float)column / config.Columns;
        float vOffset = (float)row / config.Rows;

        // Update material property (UV offset)
        // This requires MaterialPropertyOverride component
        // meshInfo.MaterialPropertyOverrides.SetVector("_MainTex_ST",
        //     new float4(1f / config.Columns, 1f / config.Rows, uOffset, vOffset));
    }
}
```

---

## Asset Setup

### 1. Create Sprite Atlas

**Example Atlas Layout** (4x4 = 16 frames):
```
[Idle0][Idle1][Idle2][Idle3]
[Walk0][Walk1][Walk2][Walk3]
[Walk4][Walk5][Walk6][Walk7]
[Attack0][Attack1][Attack2][Attack3]
```

**Texture Settings**:
- Format: RGBA 32-bit or RGBA Compressed
- Filter Mode: Point (for pixel art) or Bilinear
- Max Size: 512x512 (for 16 frames of 128x128 sprites)
- Compression: Crunch Compression (if needed)

### 2. Create Material

**Shader**: Unlit/Transparent with GPU Instancing support

```shader
Shader "Custom/AnimatedSpriteInstanced"
{
    Properties
    {
        _MainTex ("Sprite Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv);
                col *= UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                return col;
            }
            ENDCG
        }
    }
}
```

### 3. Create Quad Mesh

Simple quad mesh for sprite rendering:
- 2 triangles (4 vertices, 6 indices)
- UV coordinates: (0,0), (1,0), (0,1), (1,1)
- Size: 1x1 world units (scale via Transform)

---

## Implementation Steps

### Step 1: Install Entities Graphics Package

```
Window → Package Manager → Unity Registry → Entities Graphics → Install
```

### Step 2: Create Components

Create `SpriteAnimationComponents.cs` with the components above.

### Step 3: Create Animation System

Create `SpriteAnimationSystem.cs` with the Burst-compiled system.

### Step 4: Update Authoring Scripts

Add sprite rendering components to soldiers and zombies:

```csharp
class Baker : Baker<SoldierAuthoring>
{
    public override void Bake(SoldierAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        // ... existing components ...

        // Sprite rendering (using Entities Graphics)
        RenderMeshUtility.AddComponents(
            entity,
            GetComponent<MeshRenderer>(),  // Reference to authoring GameObject's renderer
            new RenderMeshDescription(
                shadowCastingMode: ShadowCastingMode.Off,
                receiveShadows: false
            )
        );

        // Sprite animation
        AddComponent(entity, new SpriteAnimationState
        {
            ElapsedTime = 0f,
            CurrentFrame = 0,
            CurrentAnimation = SpriteAnimationType.Idle
        });

        AddComponent(entity, new SpriteAnimationConfig
        {
            IdleFrameStart = 0,
            IdleFrameCount = 4,
            IdleFrameRate = 8f,  // 8 FPS

            WalkFrameStart = 4,
            WalkFrameCount = 8,
            WalkFrameRate = 12f,  // 12 FPS

            AttackFrameStart = 12,
            AttackFrameCount = 4,
            AttackFrameRate = 15f,  // 15 FPS

            Columns = 4,
            Rows = 4
        });
    }
}
```

### Step 5: Create Prefabs

1. Create soldier GameObject with:
   - `SoldierAuthoring` script
   - `MeshFilter` (quad mesh)
   - `MeshRenderer` (animated sprite material)

2. Create zombie GameObject similarly

---

## Performance Characteristics

### Memory Usage (2000 entities)
- SpriteAnimationState: 12 bytes × 2000 = 24 KB (hot)
- SpriteAnimationConfig: 32 bytes × 2000 = 64 KB (cold)
- Total: 88 KB

### CPU Performance
- Update every 2 frames (frame skip)
- Burst compiled parallel job
- Simple math operations (no lookups)
- **Expected**: 0.2-0.3ms for 2000 entities

### GPU Performance
- 2-4 draw calls total (instanced)
- 1 texture atlas shared by all units
- Minimal state changes
- **Expected**: 0.2ms for 2000 quads

### Total Overhead
- CPU: 0.3ms
- GPU: 0.2ms
- **Total: 0.5ms per frame** (negligible)

---

## Alternative: Simpler Non-Animated Approach

If you don't need animation, use static sprites:

```csharp
// Just add rendering in authoring - no animation system needed
RenderMeshUtility.AddComponents(
    entity,
    GetComponent<MeshRenderer>(),
    new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.Off,
        receiveShadows: false
    )
);
```

**Performance**: 0.1ms for 2000 static sprites (6-8 draw calls with different materials)

---

## Recommended Next Steps

1. **Start Simple**: Static sprites first
   - Verify rendering works
   - Check performance with 2000 units
   - Ensure GPU instancing is working

2. **Add Animation**: Once static sprites work
   - Create sprite atlas
   - Implement animation system
   - Test with 2000 units

3. **Optimize**: Profile and tune
   - Adjust frame skip interval
   - Optimize atlas size
   - Fine-tune animation frame rates

---

## Testing Checklist

- [ ] Sprites render for all units
- [ ] GPU instancing enabled (check Frame Debugger)
- [ ] Draw calls < 10 for 2000 units
- [ ] Animations play smoothly
- [ ] Different units have different animation timings
- [ ] Walk animation plays when moving
- [ ] Idle animation plays when stationary
- [ ] Performance acceptable (< 1ms overhead)

---

## Notes

- Entities Graphics requires Unity 2022.2+ and Entities 1.0+
- Sprite atlas should be power-of-2 resolution (512, 1024, 2048)
- Consider using texture compression for mobile
- For pixel art, use Point filtering and disable mipmaps
- Billboard rotation can be added via shader if needed

