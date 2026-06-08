using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies and resets GPU elastic hit material overrides on every renderer entity owned by one enemy root.
/// </summary>
public static class EnemyElasticHitRenderUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures one render entity exposes the material property components required by elastic hit deformation.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect current component presence.</param>
    /// <param name="commandBuffer">Deferred writer used during render target initialization.</param>
    /// <param name="renderEntity">Concrete render entity receiving missing material properties.</param>
    public static void EnsureGpuComponents(EntityManager entityManager,
                                           EntityCommandBuffer commandBuffer,
                                           Entity renderEntity)
    {
        if (!entityManager.Exists(renderEntity))
            return;

        SetOrAddComponentData(entityManager,
                              commandBuffer,
                              renderEntity,
                              new MaterialElasticHitDirection
                              {
                                  Value = new float4(0f, 0f, 1f, 0f)
                              });
        SetOrAddComponentData(entityManager,
                              commandBuffer,
                              renderEntity,
                              new MaterialElasticHitTiming
                              {
                                  Value = float4.zero
                              });
        SetOrAddComponentData(entityManager,
                              commandBuffer,
                              renderEntity,
                              new MaterialElasticHitMotion
                              {
                                  Value = float4.zero
                              });
    }

    /// <summary>
    /// Writes one elastic hit response to all registered renderer entities of an enemy root.
    /// </summary>
    /// <param name="entityManager">Entity manager used to access renderer material properties.</param>
    /// <param name="enemyEntity">Enemy root owning the render target buffer.</param>
    /// <param name="direction">Normalized world-space impact direction.</param>
    /// <param name="timing">Trigger time, duration, compression and volume compensation.</param>
    /// <param name="motion">Oscillation count, damping, directionality and ground anchor flag.</param>
    public static void ApplyGpuElasticHit(EntityManager entityManager,
                                          Entity enemyEntity,
                                          float4 direction,
                                          float4 timing,
                                          float4 motion)
    {
        if (!entityManager.Exists(enemyEntity))
            return;

        if (entityManager.HasBuffer<DamageFlashRenderTargetElement>(enemyEntity))
        {
            DynamicBuffer<DamageFlashRenderTargetElement> renderTargets = entityManager.GetBuffer<DamageFlashRenderTargetElement>(enemyEntity);
            bool appliedAnyTarget = false;

            for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
                appliedAnyTarget |= ApplyToEntity(entityManager, renderTargets[targetIndex].Value, direction, timing, motion);

            if (appliedAnyTarget)
                return;
        }

        ApplyToEntity(entityManager, enemyEntity, direction, timing, motion);
    }

    /// <summary>
    /// Restores all compatible renderer entities to a neutral elastic hit state.
    /// </summary>
    /// <param name="entityManager">Entity manager used to access renderer material properties.</param>
    /// <param name="enemyEntity">Enemy root owning the render target buffer.</param>
    public static void ResetGpuElasticHit(EntityManager entityManager, Entity enemyEntity)
    {
        ApplyGpuElasticHit(entityManager,
                           enemyEntity,
                           new float4(0f, 0f, 1f, 0f),
                           float4.zero,
                           float4.zero);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes elastic hit properties to one concrete render entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to write material properties.</param>
    /// <param name="renderEntity">Concrete renderer entity.</param>
    /// <param name="direction">World-space impact direction.</param>
    /// <param name="timing">Elastic timing values.</param>
    /// <param name="motion">Elastic motion values.</param>
    /// <returns>True when at least one compatible property was written.</returns>
    private static bool ApplyToEntity(EntityManager entityManager,
                                      Entity renderEntity,
                                      float4 direction,
                                      float4 timing,
                                      float4 motion)
    {
        if (!entityManager.Exists(renderEntity))
            return false;

        bool appliedAnyProperty = false;

        if (entityManager.HasComponent<MaterialElasticHitDirection>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialElasticHitDirection
            {
                Value = direction
            });
            appliedAnyProperty = true;
        }

        if (entityManager.HasComponent<MaterialElasticHitTiming>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialElasticHitTiming
            {
                Value = timing
            });
            appliedAnyProperty = true;
        }

        if (entityManager.HasComponent<MaterialElasticHitMotion>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialElasticHitMotion
            {
                Value = motion
            });
            appliedAnyProperty = true;
        }

        return appliedAnyProperty;
    }

    /// <summary>
    /// Writes one component value, adding the component first when it is missing.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect current component presence.</param>
    /// <param name="commandBuffer">Deferred writer used during initialization.</param>
    /// <param name="entity">Target renderer entity.</param>
    /// <param name="componentData">Value to write.</param>
    private static void SetOrAddComponentData<T>(EntityManager entityManager,
                                                 EntityCommandBuffer commandBuffer,
                                                 Entity entity,
                                                 T componentData)
        where T : unmanaged, IComponentData
    {
        if (entityManager.HasComponent<T>(entity))
        {
            commandBuffer.SetComponent(entity, componentData);
            return;
        }

        commandBuffer.AddComponent(entity, componentData);
    }
    #endregion

    #endregion
}
