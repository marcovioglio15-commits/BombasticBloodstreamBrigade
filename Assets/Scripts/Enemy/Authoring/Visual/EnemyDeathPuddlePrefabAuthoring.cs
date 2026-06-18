using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Marks the shared render-only death puddle prefab and bakes its pooled runtime component surface.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDeathPuddlePrefabAuthoring : MonoBehaviour
{
    #region Nested Types
    /// <summary>
    /// Bakes pooled puddle state and material properties onto the render prefab entity.
    /// </summary>
    private sealed class EnemyDeathPuddlePrefabBaker : Baker<EnemyDeathPuddlePrefabAuthoring>
    {
        #region Methods

        #region Public Methods
        /// <summary>
        /// Adds the runtime state required by the dedicated death puddle pool.
        /// </summary>
        /// <param name="authoring">Source marker component.</param>
        public override void Bake(EnemyDeathPuddlePrefabAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EnemyDeathPuddleRuntimeState
            {
                PrefabEntity = Entity.Null,
                RemainingSeconds = 0f
            });
            AddComponent<EnemyDeathPuddleActive>(entity);
            SetComponentEnabled<EnemyDeathPuddleActive>(entity, false);
            AddComponent(entity, new PostTransformMatrix
            {
                Value = float4x4.Scale(float3.zero)
            });
            AddComponent(entity, new MaterialDeathPuddlePrimaryColor
            {
                Value = new float4(1f, 1f, 1f, 1f)
            });
            AddComponent(entity, new MaterialDeathPuddleSecondaryColor
            {
                Value = new float4(1f, 1f, 1f, 1f)
            });
            AddComponent(entity, new MaterialDeathPuddleTiming
            {
                Value = new float4(0f, 1f, 0.2f, 0.08f)
            });
            AddComponent(entity, new MaterialDeathPuddleShape
            {
                Value = new float4(0.28f, 0.1f, 0.04f, 1f)
            });
            AddComponent(entity, new MaterialDeathPuddleStyle
            {
                Value = new float4(0.55f, 0f, 0f, 0f)
            });
            AddComponent(entity, new MaterialDeathPuddleFluid
            {
                Value = new float4(0.35f, 0.7f, 0.08f, 0.18f)
            });
        }
        #endregion

        #endregion
    }
    #endregion
}
