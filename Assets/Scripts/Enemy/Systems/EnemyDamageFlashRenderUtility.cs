using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

/// <summary>
/// Applies and resets GPU hit-flash material overrides on every renderer entity owned by one enemy root.
/// </summary>
public static class EnemyDamageFlashRenderUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures one render entity exposes the component set required by GPU hit-flash presentation.
    /// entityManager: Entity manager used to query current component presence.
    /// entityCommandBuffer: Deferred writer used to avoid structural changes while iterating ECS queries.
    /// renderEntity: Concrete render entity to initialize.
    /// baseColor: Original material color restored when the flash ends.
    /// flashColor: Flash color written into shader overrides.
    /// returns None.
    /// </summary>
    public static void EnsureGpuFlashComponents(EntityManager entityManager,
                                                EntityCommandBuffer entityCommandBuffer,
                                                Entity renderEntity,
                                                float4 baseColor,
                                                float4 flashColor)
    {
        if (!entityManager.Exists(renderEntity))
            return;

        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new DamageFlashBaseColor
                              {
                                  Value = baseColor
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new URPMaterialPropertyBaseColor
                              {
                                  Value = baseColor
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialColor
                              {
                                  Value = baseColor
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialHitFlashColor
                              {
                                  Value = flashColor
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialHitFlashBlend
                              {
                                  Value = 0f
                              });
    }

    /// <summary>
    /// Ensures one render entity exposes the component set required by GPU outline presentation.
    /// entityManager: Entity manager used to query current component presence.
    /// entityCommandBuffer: Deferred writer used to avoid structural changes while iterating ECS queries.
    /// renderEntity: Concrete render entity to initialize.
    /// outlineColor: Outline color written into shader overrides.
    /// outlineThickness: Outline thickness written into shader overrides.
    /// returns None.
    /// </summary>
    public static void EnsureGpuOutlineComponents(EntityManager entityManager,
                                                  EntityCommandBuffer entityCommandBuffer,
                                                  Entity renderEntity,
                                                  float4 outlineColor,
                                                  float outlineThickness)
    {
        if (!entityManager.Exists(renderEntity))
            return;

        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialOutlineColor
                              {
                                  Value = outlineColor
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialOutlineThickness
                              {
                                  Value = outlineThickness
                              });
    }

    /// <summary>
    /// Writes the current flash blend to all registered renderer entities of one enemy.
    /// entityManager: Entity manager used to access flash render targets.
    /// enemyEntity: Enemy root entity that owns the flash config and render target buffer.
    /// flashColor: Linear-space overlay tint selected for the current frame.
    /// targetBlend: Flash blend to write this frame.
    /// returns None.
    /// </summary>
    public static void ApplyGpuFlash(EntityManager entityManager,
                                     Entity enemyEntity,
                                     float4 flashColor,
                                     float targetBlend)
    {
        if (!entityManager.Exists(enemyEntity))
            return;

        if (entityManager.HasBuffer<DamageFlashRenderTargetElement>(enemyEntity))
        {
            DynamicBuffer<DamageFlashRenderTargetElement> renderTargets = entityManager.GetBuffer<DamageFlashRenderTargetElement>(enemyEntity);
            bool appliedAnyTarget = false;

            for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
                appliedAnyTarget |= ApplyGpuFlashToEntity(entityManager, renderTargets[targetIndex].Value, flashColor, targetBlend);

            if (appliedAnyTarget)
                return;
        }

        ApplyGpuFlashToEntity(entityManager, enemyEntity, flashColor, targetBlend);
    }

    /// <summary>
    /// Restores all registered renderer entities to their baked non-flashing state.
    /// entityManager: Entity manager used to access flash render targets.
    /// enemyEntity: Enemy root entity that owns the flash render target buffer.
    /// returns None.
    /// </summary>
    public static void ResetGpuFlash(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.Exists(enemyEntity))
            return;

        if (entityManager.HasBuffer<DamageFlashRenderTargetElement>(enemyEntity))
        {
            DynamicBuffer<DamageFlashRenderTargetElement> renderTargets = entityManager.GetBuffer<DamageFlashRenderTargetElement>(enemyEntity);
            bool resetAnyTarget = false;

            for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
                resetAnyTarget |= ResetGpuFlashOnEntity(entityManager, renderTargets[targetIndex].Value);

            if (resetAnyTarget)
                return;
        }

        ResetGpuFlashOnEntity(entityManager, enemyEntity);
    }

    /// <summary>
    /// Writes the current outline color and thickness to all registered renderer entities of one enemy.
    /// entityManager: Entity manager used to access outline render targets.
    /// enemyEntity: Enemy root entity that owns the outline config and render target buffer.
    /// outlineColor: Linear-space outline color selected for the current state.
    /// outlineThickness: Outline thickness selected for the current state.
    /// returns None.
    /// </summary>
    public static void ApplyGpuOutline(EntityManager entityManager,
                                       Entity enemyEntity,
                                       float4 outlineColor,
                                       float outlineThickness)
    {
        if (!entityManager.Exists(enemyEntity))
            return;

        if (entityManager.HasBuffer<DamageFlashRenderTargetElement>(enemyEntity))
        {
            DynamicBuffer<DamageFlashRenderTargetElement> renderTargets = entityManager.GetBuffer<DamageFlashRenderTargetElement>(enemyEntity);
            bool appliedAnyTarget = false;

            for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
                appliedAnyTarget |= ApplyGpuOutlineToEntity(entityManager, renderTargets[targetIndex].Value, outlineColor, outlineThickness);

            if (appliedAnyTarget)
                return;
        }

        ApplyGpuOutlineToEntity(entityManager, enemyEntity, outlineColor, outlineThickness);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes per-instance material overrides for one concrete render entity.
    /// entityManager: Entity manager used to write component data.
    /// renderEntity: Concrete renderer entity to update.
    /// flashColor: Linear-space overlay tint selected for the current frame.
    /// targetBlend: Flash blend to write this frame.
    /// returns True when at least one material override component was written.
    /// </summary>
    private static bool ApplyGpuFlashToEntity(EntityManager entityManager,
                                              Entity renderEntity,
                                              float4 flashColor,
                                              float targetBlend)
    {
        if (!entityManager.Exists(renderEntity))
            return false;

        bool appliedAnyProperty = false;

        if (entityManager.HasComponent<DamageFlashBaseColor>(renderEntity))
        {
            DamageFlashBaseColor baseColor = entityManager.GetComponentData<DamageFlashBaseColor>(renderEntity);
            float4 blendedBaseColor = DamageFlashRuntimeUtility.ResolveBaseColor(in baseColor,
                                                                                 flashColor,
                                                                                 targetBlend);

            if (entityManager.HasComponent<URPMaterialPropertyBaseColor>(renderEntity))
            {
                entityManager.SetComponentData(renderEntity, new URPMaterialPropertyBaseColor
                {
                    Value = blendedBaseColor
                });
                appliedAnyProperty = true;
            }

            if (entityManager.HasComponent<MaterialColor>(renderEntity))
            {
                entityManager.SetComponentData(renderEntity, new MaterialColor
                {
                    Value = blendedBaseColor
                });
                appliedAnyProperty = true;
            }
        }

        if (entityManager.HasComponent<MaterialHitFlashColor>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialHitFlashColor
            {
                Value = flashColor
            });
            appliedAnyProperty = true;
        }

        if (entityManager.HasComponent<MaterialHitFlashBlend>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialHitFlashBlend
            {
                Value = targetBlend
            });
            appliedAnyProperty = true;
        }

        return appliedAnyProperty;
    }

    /// <summary>
    /// Restores one concrete render entity to its baked base color and zero flash blend.
    /// entityManager: Entity manager used to write component data.
    /// renderEntity: Concrete renderer entity to reset.
    /// returns True when at least one material override component was restored.
    /// </summary>
    private static bool ResetGpuFlashOnEntity(EntityManager entityManager, Entity renderEntity)
    {
        if (!entityManager.Exists(renderEntity))
            return false;

        bool resetAnyProperty = false;

        if (entityManager.HasComponent<DamageFlashBaseColor>(renderEntity))
        {
            float4 baseColor = entityManager.GetComponentData<DamageFlashBaseColor>(renderEntity).Value;

            if (entityManager.HasComponent<URPMaterialPropertyBaseColor>(renderEntity))
            {
                entityManager.SetComponentData(renderEntity, new URPMaterialPropertyBaseColor
                {
                    Value = baseColor
                });
                resetAnyProperty = true;
            }

            if (entityManager.HasComponent<MaterialColor>(renderEntity))
            {
                entityManager.SetComponentData(renderEntity, new MaterialColor
                {
                    Value = baseColor
                });
                resetAnyProperty = true;
            }
        }

        if (entityManager.HasComponent<MaterialHitFlashBlend>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialHitFlashBlend
            {
                Value = 0f
            });
            resetAnyProperty = true;
        }

        return resetAnyProperty;
    }

    /// <summary>
    /// Writes outline property overrides to one concrete render entity.
    /// entityManager: Entity manager used to write component data.
    /// renderEntity: Concrete renderer entity to update.
    /// outlineColor: Linear-space outline color selected for the current state.
    /// outlineThickness: Outline thickness selected for the current state.
    /// returns True when at least one outline material override component was written.
    /// </summary>
    private static bool ApplyGpuOutlineToEntity(EntityManager entityManager,
                                                Entity renderEntity,
                                                float4 outlineColor,
                                                float outlineThickness)
    {
        if (!entityManager.Exists(renderEntity))
            return false;

        bool appliedAnyProperty = false;

        if (entityManager.HasComponent<MaterialOutlineColor>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialOutlineColor
            {
                Value = outlineColor
            });
            appliedAnyProperty = true;
        }

        if (entityManager.HasComponent<MaterialOutlineThickness>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialOutlineThickness
            {
                Value = outlineThickness
            });
            appliedAnyProperty = true;
        }

        return appliedAnyProperty;
    }

    /// <summary>
    /// Writes one component value, adding the component first when it is still missing on the target entity.
    /// entityManager: Entity manager used to inspect current component presence.
    /// entityCommandBuffer: Deferred writer used to record add/set operations safely.
    /// entity: Target entity that must receive the component value.
    /// componentData: Value to write.
    /// returns None.
    /// </summary>
    private static void SetOrAddComponentData<T>(EntityManager entityManager,
                                                 EntityCommandBuffer entityCommandBuffer,
                                                 Entity entity,
                                                 T componentData)
        where T : unmanaged, IComponentData
    {
        if (entityManager.HasComponent<T>(entity))
        {
            entityCommandBuffer.SetComponent(entity, componentData);
            return;
        }

        entityCommandBuffer.AddComponent(entity, componentData);
    }
    #endregion

    #endregion
}
