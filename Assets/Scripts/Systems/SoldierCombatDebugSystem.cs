using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Debug system for soldier combat - processes debug events from the Burst-compiled SoldierAttackJob.
/// Reads from CombatDebugEventQueue and logs the results.
/// Runs AFTER DamageApplicationSystem since that's where jobs are completed.
/// </summary>
[UpdateAfter(typeof(DamageApplicationSystem))]
public partial struct SoldierCombatDebugSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!CombatDebugEventQueue.IsCreated)
            return;

        // DamageApplicationSystem already called Complete(), so queue is safe to read
        while (CombatDebugEventQueue.TryDequeue(out var evt))
        {
            string message = evt.Reason switch
            {
                SoldierAttackFailReason.NoTarget => "No target assigned",
                SoldierAttackFailReason.OnCooldown => $"On cooldown ({evt.Value1:F2}s) / windup ({evt.Value2:F2}s)",
                SoldierAttackFailReason.Moving => $"Moving (speed²: {evt.Value1:F4})",
                SoldierAttackFailReason.TargetNoTransform => "Target has no transform",
                SoldierAttackFailReason.TargetNoHealth => "Target has no health",
                SoldierAttackFailReason.TargetDead => "Target is dead",
                SoldierAttackFailReason.OutOfRange => $"Out of range (dist: {evt.Value1:F2}, range: {evt.Value2:F2})",
                SoldierAttackFailReason.NotFacing => $"Not facing target (dot: {evt.Value1:F3}, need: {evt.Value2:F2})",
                SoldierAttackFailReason.AttackSuccess => $"ATTACK SUCCESS (damage: {evt.Value1:F1})",
                _ => "Unknown"
            };

            if (evt.Reason == SoldierAttackFailReason.AttackSuccess)
            {
                Debug.Log($"[SoldierCombat] Soldier {evt.Soldier.Index} -> Target {evt.Target.Index}: {message}");
            }
            else
            {
                Debug.LogWarning($"[SoldierCombat] Soldier {evt.Soldier.Index} -> Target {evt.Target.Index}: BLOCKED - {message}");
            }
        }
    }
}
