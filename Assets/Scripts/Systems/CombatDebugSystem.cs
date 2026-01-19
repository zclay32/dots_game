using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Debug MonoBehaviour to diagnose zombie combat issues
/// Add this to any GameObject in the scene and enable it to see debug logs
/// Now shows the new state machine states
/// </summary>
public class CombatDebugSystem : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool enableLogging = true;
    public float logInterval = 0.5f; // Log 2 times per second

    private float _logTimer;
    private EntityManager _entityManager;
    private EntityQuery _zombieQuery;
    private bool _initialized;

    void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        _entityManager = world.EntityManager;
        _zombieQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ZombieCombatState>(),
            ComponentType.ReadOnly<ZombieCombatConfig>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<Velocity>(),
            ComponentType.ReadOnly<EnemyUnit>()
        );
        _initialized = true;
    }

    void Update()
    {
        if (!enableLogging || !_initialized) return;

        _logTimer -= Time.deltaTime;
        if (_logTimer > 0) return;
        _logTimer = logInterval;

        var zombies = _zombieQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

        // Count zombies by state
        int idleCount = 0;
        int wanderingCount = 0;
        int chasingCount = 0;
        int windingUpCount = 0;
        int attackingCount = 0;
        int cooldownCount = 0;

        Entity sampleZombie = Entity.Null;
        ZombieCombatAIState sampleState = ZombieCombatAIState.Idle;

        foreach (var zombie in zombies)
        {
            var combatState = _entityManager.GetComponentData<ZombieCombatState>(zombie);

            switch (combatState.State)
            {
                case ZombieCombatAIState.Idle: idleCount++; break;
                case ZombieCombatAIState.Wandering: wanderingCount++; break;
                case ZombieCombatAIState.Chasing: chasingCount++; break;
                case ZombieCombatAIState.WindingUp: windingUpCount++; break;
                case ZombieCombatAIState.Attacking: attackingCount++; break;
                case ZombieCombatAIState.Cooldown: cooldownCount++; break;
            }

            // Save first zombie with target for detailed logging
            if (sampleZombie == Entity.Null && combatState.HasTarget)
            {
                sampleZombie = zombie;
                sampleState = combatState.State;
            }
        }

        int total = zombies.Length;
        Debug.Log($"[ZombieState] Total: {total} | Idle: {idleCount} | Wander: {wanderingCount} | Chase: {chasingCount} | WindUp: {windingUpCount} | Attack: {attackingCount} | Cooldown: {cooldownCount}");

        // Log details for sample zombie with target
        if (sampleZombie != Entity.Null)
        {
            var combatState = _entityManager.GetComponentData<ZombieCombatState>(sampleZombie);
            var combatConfig = _entityManager.GetComponentData<ZombieCombatConfig>(sampleZombie);
            var transform = _entityManager.GetComponentData<LocalTransform>(sampleZombie);
            var velocity = _entityManager.GetComponentData<Velocity>(sampleZombie);

            float2 myPos = new float2(transform.Position.x, transform.Position.y);
            float speed = math.length(velocity.Value);

            string targetInfo = "none";
            if (combatState.HasTarget && _entityManager.Exists(combatState.CurrentTarget) &&
                _entityManager.HasComponent<LocalTransform>(combatState.CurrentTarget))
            {
                var targetTransform = _entityManager.GetComponentData<LocalTransform>(combatState.CurrentTarget);
                float2 tgtPos = new float2(targetTransform.Position.x, targetTransform.Position.y);
                float distance = math.distance(myPos, tgtPos);

                // Check facing
                float2 toTarget = math.normalizesafe(tgtPos - myPos);
                quaternion rot = transform.Rotation;
                float3 forward3 = math.mul(rot, new float3(0, 1, 0));
                float2 facing = new float2(forward3.x, forward3.y);
                float dot = math.dot(facing, toTarget);

                targetInfo = $"dist={distance:F2}, facing={dot:F2}";
            }

            string stateName = combatState.State.ToString();
            Debug.Log($"[ZombieState] Sample: state={stateName} | timer={combatState.StateTimer:F2} | speed={speed:F3} | engaged={combatState.HasEngagedTarget} | target=({targetInfo})");
        }

        zombies.Dispose();
    }
}
