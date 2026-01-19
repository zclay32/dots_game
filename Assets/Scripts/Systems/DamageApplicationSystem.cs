using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// System that processes all queued damage events sequentially.
/// This is the ONLY sync point for damage application in the entire frame.
///
/// Pattern:
/// - Combat systems run in parallel, enqueueing damage to DamageEventQueue
/// - This system completes all jobs, then processes damage sequentially
/// - Eliminates race conditions when multiple attackers hit the same target
///
/// Update order:
/// - Runs AFTER all combat systems (CombatSystem, ZombieCombatExecutionSystem)
/// - Runs BEFORE DeathSystem (so deaths are detected after all damage applied)
/// </summary>
[UpdateAfter(typeof(CombatSystem))]
[UpdateAfter(typeof(ZombieCombatExecutionSystem))]
[UpdateBefore(typeof(DeathSystem))]
public partial struct DamageApplicationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // Initialize the damage queue
        DamageEventQueue.Initialize();
    }

    public void OnDestroy(ref SystemState state)
    {
        DamageEventQueue.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Skip if queue not initialized
        if (!DamageEventQueue.IsCreated)
            return;

        // SYNC POINT: Complete all jobs that write to the damage queue
        // This MUST happen before reading Count or dequeuing
        state.Dependency.Complete();

        // Now safe to check count after jobs have completed
        if (DamageEventQueue.Count == 0)
            return;

        // Get component lookups for damage application
        // Use HealthCurrent (hot component) as the source of truth
        var healthCurrentLookup = SystemAPI.GetComponentLookup<HealthCurrent>(false);
        var healthLookup = SystemAPI.GetComponentLookup<Health>(false); // For IsDead check
        var lastHitLookup = SystemAPI.GetComponentLookup<LastHitDirection>(false);

        // Zombie alerting lookups
        var zombieStateLookup = SystemAPI.GetComponentLookup<ZombieState>(false);
        var zombieCombatStateLookup = SystemAPI.GetComponentLookup<ZombieCombatState>(false);
        var combatTargetLookup = SystemAPI.GetComponentLookup<CombatTarget>(false);
        var transformLookup = SystemAPI.GetComponentLookup<Unity.Transforms.LocalTransform>(true);

        // Process all damage events
        while (DamageEventQueue.TryDequeue(out DamageEvent evt))
        {
            // Skip if target no longer exists or has no health
            if (!healthCurrentLookup.HasComponent(evt.Target))
                continue;

            var healthCurrent = healthCurrentLookup[evt.Target];

            // Skip if already dead (health <= 0)
            if (healthCurrent.Value <= 0)
                continue;

            // Apply damage to HealthCurrent (the source of truth)
            healthCurrent.Value -= evt.Damage;
            healthCurrentLookup[evt.Target] = healthCurrent;

            // Record hit direction for death effects
            if (lastHitLookup.HasComponent(evt.Target))
            {
                lastHitLookup[evt.Target] = new LastHitDirection { Direction = evt.HitDirection };
            }

            // Alert zombie if hit by player (soldier)
            if (evt.AttackerIsPlayer)
            {
                AlertZombie(
                    evt.Target,
                    evt.Attacker,
                    ref zombieCombatStateLookup,
                    ref zombieStateLookup,
                    ref combatTargetLookup,
                    ref transformLookup
                );
            }
        }
    }

    /// <summary>
    /// Alert a zombie that was hit by a player unit, causing it to chase the attacker
    /// </summary>
    private void AlertZombie(
        Entity zombie,
        Entity attacker,
        ref ComponentLookup<ZombieCombatState> zombieCombatStateLookup,
        ref ComponentLookup<ZombieState> zombieStateLookup,
        ref ComponentLookup<CombatTarget> combatTargetLookup,
        ref ComponentLookup<Unity.Transforms.LocalTransform> transformLookup)
    {
        // Get attacker position for caching
        float2 attackerPos = float2.zero;
        if (transformLookup.HasComponent(attacker))
        {
            var attackerTransform = transformLookup[attacker];
            attackerPos = new float2(attackerTransform.Position.x, attackerTransform.Position.y);
        }

        // Try new state machine first (ZombieCombatState)
        if (zombieCombatStateLookup.HasComponent(zombie))
        {
            var combatState = zombieCombatStateLookup[zombie];
            combatState.State = ZombieCombatAIState.Chasing;
            combatState.CurrentTarget = attacker;
            combatState.HasTarget = true;
            combatState.HasEngagedTarget = false;
            combatState.CachedTargetPos = attackerPos;
            zombieCombatStateLookup[zombie] = combatState;
        }
        // Legacy fallback (ZombieState)
        else if (zombieStateLookup.HasComponent(zombie))
        {
            var zombieState = zombieStateLookup[zombie];
            zombieState.State = ZombieAIState.Chasing;
            zombieState.AlertTimer = 5f;
            zombieStateLookup[zombie] = zombieState;

            // Also set combat target
            if (combatTargetLookup.HasComponent(zombie))
            {
                var combatTarget = combatTargetLookup[zombie];
                combatTarget.Target = attacker;
                combatTargetLookup[zombie] = combatTarget;
            }
        }
    }
}
