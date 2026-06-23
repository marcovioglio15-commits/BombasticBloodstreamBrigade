using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies enemy face flipbook material overrides to every render entity owned by one enemy root.
/// </summary>
public static class EnemyFaceFlipbookRenderUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures one render entity exposes the material override components required by face flipbook presentation.
    /// </summary>
    /// <param name="entityManager">Entity manager used to query current component presence.</param>
    /// <param name="entityCommandBuffer">Deferred writer used to avoid structural changes while iterating ECS queries.</param>
    /// <param name="renderEntity">Concrete render entity to initialize.</param>
    /// <param name="config">Face flipbook config baked on the enemy root.</param>
    public static void EnsureGpuFaceComponents(EntityManager entityManager,
                                               EntityCommandBuffer entityCommandBuffer,
                                               Entity renderEntity,
                                               in EnemyFaceFlipbookConfig config)
    {
        if (!entityManager.Exists(renderEntity))
            return;

        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialFaceFlipbookEnabled
                              {
                                  Value = config.Enabled != 0 && config.IdleEnabled != 0 ? 1f : 0f
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialFaceFlipbookState
                              {
                                  Value = (float)EnemyFaceFlipbookState.Idle
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialFaceFlipbookPlayback
                              {
                                  Value = new float4(config.IdleFramesPerSecond, 0f, config.IdleStartFrame, 0f)
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialFaceIdleGrid
                              {
                                  Value = config.IdleGrid
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialFaceAttackGrid
                              {
                                  Value = config.AttackGrid
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialFaceDamageGrid
                              {
                                  Value = config.DamageGrid
                              });
    }

    /// <summary>
    /// Writes the current face state and playback values to all registered renderer entities of one enemy.
    /// </summary>
    /// <param name="entityManager">Entity manager used to access render target buffers.</param>
    /// <param name="enemyEntity">Enemy root entity that owns face config and render targets.</param>
    /// <param name="config">Face flipbook config baked on the enemy root.</param>
    /// <param name="state">Face state selected for the current frame.</param>
    /// <param name="playback">Playback vector containing frames per second, phase seconds, start frame and reserved data.</param>
    public static void ApplyGpuFace(EntityManager entityManager,
                                    Entity enemyEntity,
                                    in EnemyFaceFlipbookConfig config,
                                    EnemyFaceFlipbookState state,
                                    float4 playback)
    {
        if (!entityManager.Exists(enemyEntity))
            return;

        if (entityManager.HasBuffer<DamageFlashRenderTargetElement>(enemyEntity))
        {
            DynamicBuffer<DamageFlashRenderTargetElement> renderTargets = entityManager.GetBuffer<DamageFlashRenderTargetElement>(enemyEntity);
            bool appliedAnyTarget = false;

            for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
                appliedAnyTarget |= ApplyGpuFaceToEntity(entityManager,
                                                         renderTargets[targetIndex].Value,
                                                         in config,
                                                         state,
                                                         playback);

            if (appliedAnyTarget)
                return;
        }

        ApplyGpuFaceToEntity(entityManager, enemyEntity, in config, state, playback);
    }

    /// <summary>
    /// Resets all registered renderer entities to idle face playback.
    /// </summary>
    /// <param name="entityManager">Entity manager used to access render target buffers.</param>
    /// <param name="enemyEntity">Enemy root entity whose face state should be reset.</param>
    public static void ResetGpuFace(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.Exists(enemyEntity))
            return;

        if (!entityManager.HasComponent<EnemyFaceFlipbookConfig>(enemyEntity))
            return;

        EnemyFaceFlipbookConfig config = entityManager.GetComponentData<EnemyFaceFlipbookConfig>(enemyEntity);
        ApplyGpuFace(entityManager,
                     enemyEntity,
                     in config,
                     EnemyFaceFlipbookState.Idle,
                     new float4(config.IdleFramesPerSecond, 0f, config.IdleStartFrame, 0f));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes per-instance face material overrides for one concrete render entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to write component data.</param>
    /// <param name="renderEntity">Concrete renderer entity to update.</param>
    /// <param name="config">Face flipbook config baked on the enemy root.</param>
    /// <param name="state">Face state selected for the current frame.</param>
    /// <param name="playback">Playback vector containing frames per second, phase seconds, start frame and reserved data.</param>
    /// <returns>True when at least one face material override component was written.</returns>
    private static bool ApplyGpuFaceToEntity(EntityManager entityManager,
                                             Entity renderEntity,
                                             in EnemyFaceFlipbookConfig config,
                                             EnemyFaceFlipbookState state,
                                             float4 playback)
    {
        if (!entityManager.Exists(renderEntity))
            return false;

        bool appliedAnyProperty = false;

        if (entityManager.HasComponent<MaterialFaceFlipbookEnabled>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialFaceFlipbookEnabled
            {
                Value = config.Enabled != 0 && config.IdleEnabled != 0 ? 1f : 0f
            });
            appliedAnyProperty = true;
        }

        if (entityManager.HasComponent<MaterialFaceFlipbookState>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialFaceFlipbookState
            {
                Value = (float)state
            });
            appliedAnyProperty = true;
        }

        if (entityManager.HasComponent<MaterialFaceFlipbookPlayback>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialFaceFlipbookPlayback
            {
                Value = playback
            });
            appliedAnyProperty = true;
        }

        if (entityManager.HasComponent<MaterialFaceIdleGrid>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialFaceIdleGrid
            {
                Value = config.IdleGrid
            });
            appliedAnyProperty = true;
        }

        if (entityManager.HasComponent<MaterialFaceAttackGrid>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialFaceAttackGrid
            {
                Value = config.AttackGrid
            });
            appliedAnyProperty = true;
        }

        if (entityManager.HasComponent<MaterialFaceDamageGrid>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialFaceDamageGrid
            {
                Value = config.DamageGrid
            });
            appliedAnyProperty = true;
        }

        return appliedAnyProperty;
    }

    /// <summary>
    /// Writes one component value, adding the component first when it is still missing on the target entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect current component presence.</param>
    /// <param name="entityCommandBuffer">Deferred writer used to record add and set operations safely.</param>
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
