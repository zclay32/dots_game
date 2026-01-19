using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Debug system to log noise events and zombie states.
/// Enable/disable via the showDebugLogs field.
/// </summary>
[UpdateAfter(typeof(NoiseAlertSystemEnhanced))]
public partial class NoiseDebugSystem : SystemBase
{
    private int _frameCount;
    private float2 _lastNoisePos;
    private float _lastNoiseRadius;
    private bool _hasRecentNoise;

    // Set to true in Inspector or via code to see debug output
    public static bool ShowDebugLogs = true;

    // Set to true to see detailed per-zombie debug info
    public static bool ShowDetailedDebug = true;

    protected override void OnUpdate()
    {
        if (!ShowDebugLogs) return;

        // Complete any pending jobs before reading from the debug queue
        Dependency.Complete();

        _frameCount++;

        // Process debug events from the noise alert job
        int inRangeEvents = 0;
        int probabilityEvents = 0;
        int activatedEvents = 0;

        while (NoiseDebugEventQueue.TryDequeue(out var evt))
        {
            switch (evt.Reason)
            {
                case NoiseDebugReason.ZombieInRange:
                    inRangeEvents++;
                    if (ShowDetailedDebug && inRangeEvents <= 3)
                    {
                        Debug.Log($"[NoiseJob] Zombie IN RANGE at ({evt.ZombiePos.x:F1}, {evt.ZombiePos.y:F1}), dist={evt.Distance:F1}");
                    }
                    break;

                case NoiseDebugReason.ProbabilityRoll:
                    probabilityEvents++;
                    if (ShowDetailedDebug && probabilityEvents <= 3)
                    {
                        string result = evt.RandomValue < evt.Probability ? "PASS" : "FAIL";
                        Debug.Log($"[NoiseJob] Prob roll: prob={evt.Probability:F2}, roll={evt.RandomValue:F2} -> {result}");
                    }
                    break;

                case NoiseDebugReason.Activated:
                    activatedEvents++;
                    Debug.Log($"[NoiseJob] ZOMBIE ACTIVATED at ({evt.ZombiePos.x:F1}, {evt.ZombiePos.y:F1})!");
                    break;
            }
        }

        if (inRangeEvents > 0 && ShowDetailedDebug)
        {
            Debug.Log($"[NoiseJob] Total: {inRangeEvents} in range, {activatedEvents} activated");
        }

        // Count zombies in each state
        int idleCount = 0;
        int wanderingCount = 0;
        int chasingCount = 0;
        int otherCount = 0;
        int inRangeCount = 0;

        // Check if we should do detailed logging (only when there's been a recent noise)
        bool doDetailedLog = ShowDetailedDebug && _hasRecentNoise && _frameCount % 30 == 0;

        foreach (var (combatState, transform, sensitivity) in
            SystemAPI.Query<RefRO<ZombieCombatState>, RefRO<LocalTransform>, RefRO<NoiseSensitivity>>()
                .WithAll<EnemyUnit>())
        {
            switch (combatState.ValueRO.State)
            {
                case ZombieCombatAIState.Idle: idleCount++; break;
                case ZombieCombatAIState.Wandering: wanderingCount++; break;
                case ZombieCombatAIState.Chasing: chasingCount++; break;
                default: otherCount++; break;
            }

            // Check if zombie is in range of last noise
            if (_hasRecentNoise)
            {
                float2 zombiePos = transform.ValueRO.Position.xy;
                float distance = math.distance(zombiePos, _lastNoisePos);
                if (distance < _lastNoiseRadius)
                {
                    inRangeCount++;

                    // Log first few zombies in range for debugging
                    if (doDetailedLog && inRangeCount <= 3)
                    {
                        float normalizedDist = distance / _lastNoiseRadius;
                        Debug.Log($"[NoiseDebug] Zombie in range: dist={distance:F1}, state={combatState.ValueRO.State}, sensitivity={sensitivity.ValueRO.SensitivityMultiplier:F1}");
                    }
                }
            }
        }

        // Only log every 60 frames (about once per second)
        if (_frameCount % 60 == 0)
        {
            string rangeInfo = _hasRecentNoise ? $", InNoiseRange: {inRangeCount}" : "";
            Debug.Log($"[NoiseDebug] Zombies - Idle: {idleCount}, Wandering: {wanderingCount}, Chasing: {chasingCount}, Other: {otherCount}{rangeInfo}");
        }

        // Clear noise tracking after a bit
        if (_frameCount % 120 == 0)
        {
            _hasRecentNoise = false;
        }
    }

    // Call this from NoiseAlertSystemEnhanced to track the last noise
    public static NoiseDebugSystem Instance;

    protected override void OnCreate()
    {
        Instance = this;
    }

    public void TrackNoise(float2 position, float radius)
    {
        _lastNoisePos = position;
        _lastNoiseRadius = radius;
        _hasRecentNoise = true;
    }
}
