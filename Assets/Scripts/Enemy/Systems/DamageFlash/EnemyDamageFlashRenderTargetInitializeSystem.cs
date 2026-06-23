using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

/// <summary>
/// Marks one enemy root after its flash render targets have received the runtime material override components.
/// </summary>
public struct EnemyDamageFlashRenderTargetsInitialized : IComponentData
{
}

/// <summary>
/// Initializes hit-flash material override components on every renderer entity referenced by enemy roots.
/// It runs before pool prewarm so instantiated pooled enemies inherit ready-to-use flash properties from initialized prefabs.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(EnemyPoolInitializeSystem))]
public partial struct EnemyDamageFlashRenderTargetInitializeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the minimum data required to run the initialization pass.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DamageFlashConfig>();
        state.RequireForUpdate<OutlineVisualConfig>();
        state.RequireForUpdate<EnemyFaceFlipbookConfig>();
        state.RequireForUpdate<DamageFlashRenderTargetElement>();
    }

    /// <summary>
    /// Adds missing per-renderer material property components to every uninitialized enemy root.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

        try
        {
            foreach ((RefRO<DamageFlashConfig> damageFlashConfig,
                      RefRO<OutlineVisualConfig> outlineVisualConfig,
                      RefRO<EnemyFaceFlipbookConfig> faceFlipbookConfig,
                      DynamicBuffer<DamageFlashRenderTargetElement> renderTargets,
                      Entity enemyEntity)
                     in SystemAPI.Query<RefRO<DamageFlashConfig>,
                                        RefRO<OutlineVisualConfig>,
                                        RefRO<EnemyFaceFlipbookConfig>,
                                        DynamicBuffer<DamageFlashRenderTargetElement>>()
                                 .WithNone<EnemyDamageFlashRenderTargetsInitialized>()
                                 .WithEntityAccess())
            {
                float4 flashColor = damageFlashConfig.ValueRO.FlashColor;
                float4 outlineColor = outlineVisualConfig.ValueRO.Color;
                float outlineThickness = outlineVisualConfig.ValueRO.Enabled != 0
                    ? outlineVisualConfig.ValueRO.Thickness
                    : 0f;

                EnsureRenderableTargets(entityManager, enemyEntity, renderTargets);

                if (renderTargets.Length <= 0)
                    continue;

                for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
                {
                    DamageFlashRenderTargetElement renderTarget = renderTargets[targetIndex];
                    EnemyDamageFlashRenderUtility.EnsureGpuFlashComponents(entityManager,
                                                                           entityCommandBuffer,
                                                                           renderTarget.Value,
                                                                           renderTarget.BaseColor,
                                                                           flashColor);
                    EnemyDamageFlashRenderUtility.EnsureGpuOutlineComponents(entityManager,
                                                                            entityCommandBuffer,
                                                                            renderTarget.Value,
                                                                            outlineColor,
                                                                            outlineThickness);
                    EnemyElasticHitRenderUtility.EnsureGpuComponents(entityManager,
                                                                     entityCommandBuffer,
                                                                     renderTarget.Value);
                    EnemyFaceFlipbookRenderUtility.EnsureGpuFaceComponents(entityManager,
                                                                           entityCommandBuffer,
                                                                           renderTarget.Value,
                                                                           in faceFlipbookConfig.ValueRO);
                }

                entityCommandBuffer.AddComponent<EnemyDamageFlashRenderTargetsInitialized>(enemyEntity);
            }

            entityCommandBuffer.Playback(entityManager);
        }
        finally
        {
            entityCommandBuffer.Dispose();
        }
    }
    #endregion

    #region Render Target Recovery
    /// <summary>
    /// Ensures a GPU enemy has at least one valid render target before flash and outline material overrides are added.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect baked linked entities and hierarchy entities.</param>
    /// <param name="enemyEntity">Root enemy entity that owns the flash render target buffer.</param>
    /// <param name="renderTargets">Mutable target buffer recovered when baked data is empty or stale.</param>
    private static void EnsureRenderableTargets(EntityManager entityManager,
                                                Entity enemyEntity,
                                                DynamicBuffer<DamageFlashRenderTargetElement> renderTargets)
    {
        if (HasUsableRenderTarget(entityManager, renderTargets))
            return;

        renderTargets.Clear();
        AppendLinkedEntityRenderTargets(entityManager, enemyEntity, renderTargets);
        AppendChildHierarchyRenderTargets(entityManager, enemyEntity, renderTargets);
    }

    /// <summary>
    /// Checks whether the current target buffer still points to renderable entities.
    /// </summary>
    /// <param name="entityManager">Entity manager used to validate each target entity.</param>
    /// <param name="renderTargets">Current target buffer to inspect.</param>
    /// <returns>True when at least one target can receive material property overrides.</returns>
    private static bool HasUsableRenderTarget(EntityManager entityManager,
                                              DynamicBuffer<DamageFlashRenderTargetElement> renderTargets)
    {
        for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
        {
            Entity targetEntity = renderTargets[targetIndex].Value;

            if (IsRenderableEntity(entityManager, targetEntity))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds renderable linked entities to a recovered flash target buffer.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read the LinkedEntityGroup buffer.</param>
    /// <param name="enemyEntity">Root enemy entity whose linked render entities are inspected.</param>
    /// <param name="renderTargets">Mutable buffer receiving recovered targets.</param>
    private static void AppendLinkedEntityRenderTargets(EntityManager entityManager,
                                                        Entity enemyEntity,
                                                        DynamicBuffer<DamageFlashRenderTargetElement> renderTargets)
    {
        if (!entityManager.HasBuffer<LinkedEntityGroup>(enemyEntity))
            return;

        DynamicBuffer<LinkedEntityGroup> linkedEntities = entityManager.GetBuffer<LinkedEntityGroup>(enemyEntity);

        for (int entityIndex = 0; entityIndex < linkedEntities.Length; entityIndex++)
            TryAppendRenderTarget(entityManager, linkedEntities[entityIndex].Value, renderTargets);
    }

    /// <summary>
    /// Adds renderable transform children to a recovered flash target buffer when no linked group is available.
    /// </summary>
    /// <param name="entityManager">Entity manager used to walk Child buffers.</param>
    /// <param name="enemyEntity">Root enemy entity whose transform hierarchy is inspected.</param>
    /// <param name="renderTargets">Mutable buffer receiving recovered targets.</param>
    private static void AppendChildHierarchyRenderTargets(EntityManager entityManager,
                                                          Entity enemyEntity,
                                                          DynamicBuffer<DamageFlashRenderTargetElement> renderTargets)
    {
        NativeList<Entity> hierarchyEntities = new NativeList<Entity>(Allocator.Temp);

        try
        {
            hierarchyEntities.Add(enemyEntity);

            for (int entityIndex = 0; entityIndex < hierarchyEntities.Length; entityIndex++)
            {
                Entity currentEntity = hierarchyEntities[entityIndex];
                TryAppendRenderTarget(entityManager, currentEntity, renderTargets);

                if (!entityManager.HasBuffer<Child>(currentEntity))
                    continue;

                DynamicBuffer<Child> children = entityManager.GetBuffer<Child>(currentEntity);

                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    Entity childEntity = children[childIndex].Value;

                    if (entityManager.Exists(childEntity))
                        hierarchyEntities.Add(childEntity);
                }
            }
        }
        finally
        {
            hierarchyEntities.Dispose();
        }
    }

    /// <summary>
    /// Appends one entity when it is renderable and not already present in the target buffer.
    /// </summary>
    /// <param name="entityManager">Entity manager used to validate rendering components.</param>
    /// <param name="candidateEntity">Entity considered for flash and outline overrides.</param>
    /// <param name="renderTargets">Mutable target buffer receiving the candidate.</param>
    /// <returns>True when the candidate was appended.</returns>
    private static bool TryAppendRenderTarget(EntityManager entityManager,
                                              Entity candidateEntity,
                                              DynamicBuffer<DamageFlashRenderTargetElement> renderTargets)
    {
        if (!IsRenderableEntity(entityManager, candidateEntity))
            return false;

        for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
        {
            if (renderTargets[targetIndex].Value == candidateEntity)
                return false;
        }

        renderTargets.Add(new DamageFlashRenderTargetElement
        {
            Value = candidateEntity,
            BaseColor = ResolveRuntimeBaseColor(entityManager, candidateEntity)
        });
        return true;
    }

    /// <summary>
    /// Checks whether an entity exposes render mesh metadata that can be driven by Entities Graphics.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect component presence.</param>
    /// <param name="entity">Candidate render entity.</param>
    /// <returns>True when the entity can receive GPU visual feedback.</returns>
    private static bool IsRenderableEntity(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.Exists(entity))
            return false;

        return entityManager.HasComponent<MaterialMeshInfo>(entity);
    }

    /// <summary>
    /// Resolves a stable runtime base color for recovered targets when the authoring-time renderer color is unavailable.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read existing material override components.</param>
    /// <param name="entity">Render entity whose base color is inspected.</param>
    /// <returns>Base color used to restore the renderer after flash playback.</returns>
    private static float4 ResolveRuntimeBaseColor(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.Exists(entity))
            return new float4(1f, 1f, 1f, 1f);

        if (entityManager.HasComponent<DamageFlashBaseColor>(entity))
            return entityManager.GetComponentData<DamageFlashBaseColor>(entity).Value;

        if (entityManager.HasComponent<URPMaterialPropertyBaseColor>(entity))
            return entityManager.GetComponentData<URPMaterialPropertyBaseColor>(entity).Value;

        if (entityManager.HasComponent<MaterialColor>(entity))
            return entityManager.GetComponentData<MaterialColor>(entity).Value;

        return new float4(1f, 1f, 1f, 1f);
    }
    #endregion

    #endregion
}

/// <summary>
/// Re-synchronizes GPU outline material overrides when one enemy outline config changes after initial render-target setup.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup), OrderFirst = true)]
[UpdateAfter(typeof(EnemyDamageFlashRenderTargetInitializeSystem))]
public partial struct EnemyOutlineRenderTargetSyncSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the minimum data required to run outline synchronization.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<OutlineVisualConfig>();
        state.RequireForUpdate<DamageFlashRenderTargetElement>();
        state.RequireForUpdate<EnemyDamageFlashRenderTargetsInitialized>();
    }

    /// <summary>
    /// Propagates changed outline config values from enemy roots to all registered renderer entities.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;

        foreach ((RefRO<OutlineVisualConfig> outlineVisualConfig,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<OutlineVisualConfig>>()
                             .WithAll<DamageFlashRenderTargetElement, EnemyDamageFlashRenderTargetsInitialized>()
                             .WithChangeFilter<OutlineVisualConfig>()
                             .WithEntityAccess())
        {
            float4 outlineColor = outlineVisualConfig.ValueRO.Color;
            float outlineThickness = outlineVisualConfig.ValueRO.Enabled != 0
                ? outlineVisualConfig.ValueRO.Thickness
                : 0f;

            EnemyDamageFlashRenderUtility.ApplyGpuOutline(entityManager,
                                                          enemyEntity,
                                                          outlineColor,
                                                          outlineThickness);
        }
    }
    #endregion

    #endregion
}
