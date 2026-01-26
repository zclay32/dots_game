using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component that bakes prefab references into ECS Entity references.
/// Add to a GameObject in your subscene to provide prefab access for spawning systems.
/// </summary>
public class PrefabLibraryAuthoring : MonoBehaviour
{
    [Header("Unit Prefabs")]
    [Tooltip("Soldier unit prefab")]
    public GameObject soldierPrefab;

    [Tooltip("Zombie unit prefab")]
    public GameObject zombiePrefab;

    [Header("Building Prefabs")]
    [Tooltip("Crystal building prefab")]
    public GameObject crystalPrefab;

    class Baker : Baker<PrefabLibraryAuthoring>
    {
        public override void Bake(PrefabLibraryAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new PrefabLibrary
            {
                SoldierPrefab = GetEntity(authoring.soldierPrefab, TransformUsageFlags.Dynamic),
                ZombiePrefab = GetEntity(authoring.zombiePrefab, TransformUsageFlags.Dynamic),
                CrystalPrefab = GetEntity(authoring.crystalPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}
