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
    /// </summary>
    /// <param name="entityManager">Entity manager used to query current component presence.</param>
    /// <param name="entityCommandBuffer">Deferred writer used to avoid structural changes while iterating ECS queries.</param>
    /// <param name="renderEntity">Concrete render entity to initialize.</param>
    /// <param name="baseColor">Original material color restored when the flash ends.</param>
    /// <param name="flashColor">Flash color written into shader overrides.</param>
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
    /// </summary>
    /// <param name="entityManager">Entity manager used to query current component presence.</param>
    /// <param name="entityCommandBuffer">Deferred writer used to avoid structural changes while iterating ECS queries.</param>
    /// <param name="renderEntity">Concrete render entity to initialize.</param>
    /// <param name="outlineColor">Outline color written into shader overrides.</param>
    /// <param name="outlineThickness">Outline thickness written into shader overrides.</param>
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
    /// </summary>
    /// <param name="entityManager">Entity manager used to access flash render targets.</param>
    /// <param name="enemyEntity">Enemy root entity that owns the flash config and render target buffer.</param>
    /// <param name="flashColor">Linear-space overlay tint selected for the current frame.</param>
    /// <param name="targetBlend">Flash blend to write this frame.</param>
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
    /// </summary>
    /// <param name="entityManager">Entity manager used to access flash render targets.</param>
    /// <param name="enemyEntity">Enemy root entity that owns the flash render target buffer.</param>
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
    /// </summary>
    /// <param name="entityManager">Entity manager used to access outline render targets.</param>
    /// <param name="enemyEntity">Enemy root entity that owns the outline config and render target buffer.</param>
    /// <param name="outlineColor">Linear-space outline color selected for the current state.</param>
    /// <param name="outlineThickness">Outline thickness selected for the current state.</param>
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
    /// </summary>
    /// <param name="entityManager">Entity manager used to write component data.</param>
    /// <param name="renderEntity">Concrete renderer entity to update.</param>
    /// <param name="flashColor">Linear-space overlay tint selected for the current frame.</param>
    /// <param name="targetBlend">Flash blend to write this frame.</param>
    /// <returns>True when at least one material override component was written.</returns>
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
    /// </summary>
    /// <param name="entityManager">Entity manager used to write component data.</param>
    /// <param name="renderEntity">Concrete renderer entity to reset.</param>
    /// <returns>True when at least one material override component was restored.</returns>
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
    /// </summary>
    /// <param name="entityManager">Entity manager used to write component data.</param>
    /// <param name="renderEntity">Concrete renderer entity to update.</param>
    /// <param name="outlineColor">Linear-space outline color selected for the current state.</param>
    /// <param name="outlineThickness">Outline thickness selected for the current state.</param>
    /// <returns>True when at least one outline material override component was written.</returns>
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
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect current component presence.</param>
    /// <param name="entityCommandBuffer">Deferred writer used to record add/set operations safely.</param>
    /// <param name="entity">Target entity that must receive the component value.</param>
    /// <param name="componentData">Value to write.</param>
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
