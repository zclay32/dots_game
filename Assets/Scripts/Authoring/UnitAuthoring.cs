using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for unit prefab
/// Add to the unit prefab to make it an ECS entity
/// </summary>
public class UnitAuthoring : MonoBehaviour
{
    [Header("Unit Settings")]
    public float moveSpeed = 3f;
    
    class Baker : Baker<UnitAuthoring>
    {
        public override void Bake(UnitAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Add unit tag
            AddComponent(entity, new Unit());
            
            // Add movement components
            AddComponent(entity, new MoveSpeed { Value = authoring.moveSpeed });
            AddComponent(entity, new Velocity { Value = float2.zero });
            AddComponent(entity, new TargetPosition { Value = float2.zero, HasTarget = false });
            
            // Add spatial hash component
            AddComponent(entity, new SpatialCell { Cell = int2.zero });
        }
    }
}
