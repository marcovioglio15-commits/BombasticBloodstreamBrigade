using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns pooled reward drops for enemies killed in the current frame.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyFinalizeDespawnSystem))]
[UpdateBefore(typeof(EnemyExperienceDropCollectSystem))]
public partial struct EnemyExperienceDropSpawnSystem : ISystem
{
    #region Constants
    private const int MaxDropSpawnInstancesPerFrame = 512;
    private const int MaxRuntimeDropPoolExpansionPerFrame = 256;
    #endregion

    #region Fields
    private EntityQuery playerProgressionQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures required components for drop spawning.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyKilledEventElement>();
        state.RequireForUpdate<EnemyDropItemsConfig>();
        state.RequireForUpdate<EnemyExperienceDropPoolRegistry>();
        playerProgressionQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<PlayerProgressionConfig, PlayerRuntimeGamePhaseElement, PlayerLevel, PlayerExperience, PlayerRunOutcomeState>()
            .Build(ref state);
        state.RequireForUpdate(playerProgressionQuery);
    }

    /// <summary>
    /// Processes killed events and spawns experience drops from configured pools.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<EnemyExperienceDropPoolRegistry>(out Entity registryEntity))
            return;

        EntityManager entityManager = state.EntityManager;

        if (!entityManager.HasBuffer<EnemyExperienceDropPoolMapElement>(registryEntity))
            return;

        DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);

        if (poolMap.Length <= 0)
            return;

        if (!SystemAPI.TryGetSingletonBuffer<EnemyKilledEventElement>(out DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer))
            return;

        if (killedEventsBuffer.Length <= 0)
            return;

        if (playerProgressionQuery.CalculateEntityCount() != 1)
            return;

        Entity playerEntity = playerProgressionQuery.GetSingletonEntity();
        PlayerRunOutcomeState runOutcomeState = entityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);

        if (runOutcomeState.IsFinalized != 0)
            return;

        PlayerProgressionConfig progressionConfig = entityManager.GetComponentData<PlayerProgressionConfig>(playerEntity);
        DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases = entityManager.GetBuffer<PlayerRuntimeGamePhaseElement>(playerEntity);
        PlayerLevel playerLevel = entityManager.GetComponentData<PlayerLevel>(playerEntity);
        PlayerExperience playerExperience = entityManager.GetComponentData<PlayerExperience>(playerEntity);
        float remainingExperienceCapacity = PlayerProgressionPhaseUtility.ResolveRemainingExperienceUntilLevelCap(progressionConfig,
                                                                                                                   runtimeGamePhases,
                                                                                                                   playerLevel.Current,
                                                                                                                   playerExperience.Current);
        float remainingSpawnBudget = 0f;

        if (remainingExperienceCapacity > EnemyDropSpawnRuntimeUtility.PrecisionEpsilon)
        {
            float activeWorldDropExperience = ResolveActiveWorldDropExperience(ref state, remainingExperienceCapacity);
            remainingSpawnBudget = math.max(0f, remainingExperienceCapacity - activeWorldDropExperience);
        }

        Allocator frameAllocator = state.WorldUpdateAllocator;

        // Snapshot buffers to keep iteration stable even if pool expansion performs structural changes.
        NativeArray<EnemyExperienceDropPoolMapElement> poolMapSnapshot = CollectionHelper.CreateNativeArray(poolMap.AsNativeArray(), frameAllocator);
        NativeArray<EnemyKilledEventElement> killedEventsSnapshot = CollectionHelper.CreateNativeArray(killedEventsBuffer.AsNativeArray(), frameAllocator);
        int remainingFrameDropSpawnBudget = MaxDropSpawnInstancesPerFrame;
        int remainingRuntimeDropPoolExpansionBudget = MaxRuntimeDropPoolExpansionPerFrame;

        for (int killedIndex = 0; killedIndex < killedEventsSnapshot.Length; killedIndex++)
        {
            if (remainingFrameDropSpawnBudget <= 0)
                break;

            EnemyRecoveryDropSpawnUtility.SpawnRecoveryDropsForKilledEnemy(entityManager,
                                                                           poolMapSnapshot,
                                                                           killedEventsSnapshot[killedIndex],
                                                                           killedIndex,
                                                                           frameAllocator,
                                                                           ref remainingFrameDropSpawnBudget,
                                                                           ref remainingRuntimeDropPoolExpansionBudget);

            if (remainingSpawnBudget <= EnemyDropSpawnRuntimeUtility.PrecisionEpsilon || remainingFrameDropSpawnBudget <= 0)
                continue;

            remainingSpawnBudget -= SpawnExperienceDropsForKilledEnemy(entityManager,
                                                                       poolMapSnapshot,
                                                                       killedEventsSnapshot[killedIndex],
                                                                       killedIndex,
                                                                       remainingSpawnBudget,
                                                                       frameAllocator,
                                                                       ref remainingFrameDropSpawnBudget,
                                                                       ref remainingRuntimeDropPoolExpansionBudget);
        }
    }
    #endregion

    #region Spawn
    /// <summary>
    /// Sums active uncollected experience value so spawn budget never exceeds the player's remaining level-cap capacity.
    /// </summary>
    /// <param name="state">Current ECS system state used to query active world drops.</param>
    /// <param name="stopAtExperience">Early-out budget threshold for the scan.</param>
    /// <returns>Total active world experience up to the requested threshold.</returns>
    private float ResolveActiveWorldDropExperience(ref SystemState state, float stopAtExperience)
    {
        float totalActiveWorldDropExperience = 0f;
        float sanitizedStopAtExperience = math.max(0f, stopAtExperience);

        foreach ((RefRO<EnemyExperienceDrop> dropData, EnabledRefRO<EnemyExperienceDropActive> dropActive)
                 in SystemAPI.Query<RefRO<EnemyExperienceDrop>, EnabledRefRO<EnemyExperienceDropActive>>()
                             .WithAll<EnemyExperienceDropActive>())
        {
            if (!dropActive.ValueRO)
                continue;

            totalActiveWorldDropExperience += math.max(0f, dropData.ValueRO.ExperienceAmount);

            if (sanitizedStopAtExperience > EnemyDropSpawnRuntimeUtility.PrecisionEpsilon &&
                totalActiveWorldDropExperience >= sanitizedStopAtExperience)
                break;
        }

        return totalActiveWorldDropExperience;
    }

    /// <summary>
    /// Spawns all experience drop modules attached to one killed enemy while respecting frame and level-cap budgets.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read enemy buffers and activate pooled drops.</param>
    /// <param name="poolMap">Snapshot prefab-to-pool map.</param>
    /// <param name="killedEvent">Killed enemy event being processed.</param>
    /// <param name="killEventIndex">Index of the kill event inside the current frame snapshot.</param>
    /// <param name="remainingSpawnBudget">Remaining experience value that may still be spawned this frame.</param>
    /// <param name="frameAllocator">Frame allocator used to snapshot module buffers safely.</param>
    /// <param name="remainingFrameDropSpawnBudget">Mutable frame budget shared by all drop kinds.</param>
    /// <param name="remainingRuntimeDropPoolExpansionBudget">Mutable runtime pool expansion budget.</param>
    /// <returns>Total experience value spawned for this killed enemy.</returns>
    private static float SpawnExperienceDropsForKilledEnemy(EntityManager entityManager,
                                                            NativeArray<EnemyExperienceDropPoolMapElement> poolMap,
                                                            EnemyKilledEventElement killedEvent,
                                                            int killEventIndex,
                                                            float remainingSpawnBudget,
                                                            Allocator frameAllocator,
                                                            ref int remainingFrameDropSpawnBudget,
                                                            ref int remainingRuntimeDropPoolExpansionBudget)
    {
        if (remainingFrameDropSpawnBudget <= 0)
            return 0f;

        Entity enemyEntity = killedEvent.EnemyEntity;

        if (enemyEntity == Entity.Null || !entityManager.Exists(enemyEntity))
            return 0f;

        if (!entityManager.HasComponent<EnemyDropItemsConfig>(enemyEntity))
            return 0f;

        EnemyDropItemsConfig dropItemsConfig = entityManager.GetComponentData<EnemyDropItemsConfig>(enemyEntity);

        if (dropItemsConfig.HasExperienceDrops == 0 || dropItemsConfig.ExperienceModuleCount <= 0)
            return 0f;

        if (!entityManager.HasBuffer<EnemyExperienceDropModuleElement>(enemyEntity))
            return 0f;

        if (!entityManager.HasBuffer<EnemyExperienceDropDefinitionElement>(enemyEntity))
            return 0f;

        NativeArray<EnemyExperienceDropModuleElement> experienceModules = CollectionHelper.CreateNativeArray(entityManager.GetBuffer<EnemyExperienceDropModuleElement>(enemyEntity).AsNativeArray(),
                                                                                                             frameAllocator);
        NativeArray<EnemyExperienceDropDefinitionElement> definitions = CollectionHelper.CreateNativeArray(entityManager.GetBuffer<EnemyExperienceDropDefinitionElement>(enemyEntity).AsNativeArray(),
                                                                                                           frameAllocator);
        NativeArray<EnemyDropItemsModuleSelectionElement> selectionModules = default;

        if (entityManager.HasBuffer<EnemyDropItemsModuleSelectionElement>(enemyEntity))
        {
            selectionModules = CollectionHelper.CreateNativeArray(entityManager.GetBuffer<EnemyDropItemsModuleSelectionElement>(enemyEntity).AsNativeArray(),
                                                                  frameAllocator);
        }

        if (experienceModules.Length <= 0 || definitions.Length <= 0)
            return 0f;

        float rewardMultiplier = ResolveExperienceRewardMultiplier(entityManager, enemyEntity);

        if (rewardMultiplier <= EnemyDropSpawnRuntimeUtility.PrecisionEpsilon)
            return 0f;

        float spawnedExperience = 0f;

        for (int moduleIndex = 0; moduleIndex < experienceModules.Length; moduleIndex++)
        {
            if (!EnemyDropItemsModuleSelectionUtility.ShouldResolveModule(enemyEntity,
                                                                          killEventIndex,
                                                                          in dropItemsConfig,
                                                                          selectionModules,
                                                                          EnemyDropItemsPayloadKind.Experience,
                                                                          moduleIndex))
            {
                continue;
            }

            float remainingModuleSpawnBudget = math.max(0f, remainingSpawnBudget - spawnedExperience);

            if (remainingModuleSpawnBudget <= EnemyDropSpawnRuntimeUtility.PrecisionEpsilon || remainingFrameDropSpawnBudget <= 0)
                break;

            spawnedExperience += SpawnDropsForExperienceModule(entityManager,
                                                               poolMap,
                                                               killedEvent,
                                                               killEventIndex,
                                                               moduleIndex,
                                                               experienceModules[moduleIndex],
                                                               definitions,
                                                               rewardMultiplier,
                                                               remainingModuleSpawnBudget,
                                                               ref remainingFrameDropSpawnBudget,
                                                               ref remainingRuntimeDropPoolExpansionBudget);
        }

        return spawnedExperience;
    }

    /// <summary>
    /// Spawns concrete pickup entities for one experience module by resolving a compatible drop composition.
    /// </summary>
    /// <param name="entityManager">Entity manager used to activate pooled drops.</param>
    /// <param name="poolMap">Snapshot prefab-to-pool map.</param>
    /// <param name="killedEvent">Killed enemy event being processed.</param>
    /// <param name="killEventIndex">Index of the kill event inside the current frame snapshot.</param>
    /// <param name="moduleIndex">Experience module index used for deterministic seeds.</param>
    /// <param name="experienceModule">Baked experience module settings.</param>
    /// <param name="definitions">Baked experience definitions snapshot.</param>
    /// <param name="rewardMultiplier">Resolved reward multiplier from optional enemy runtime components.</param>
    /// <param name="remainingSpawnBudget">Remaining experience value that may still be spawned for this module.</param>
    /// <param name="remainingFrameDropSpawnBudget">Mutable frame budget shared by all drop kinds.</param>
    /// <param name="remainingRuntimeDropPoolExpansionBudget">Mutable runtime pool expansion budget.</param>
    /// <returns>Total experience value spawned by this module.</returns>
    private static float SpawnDropsForExperienceModule(EntityManager entityManager,
                                                       NativeArray<EnemyExperienceDropPoolMapElement> poolMap,
                                                       EnemyKilledEventElement killedEvent,
                                                       int killEventIndex,
                                                       int moduleIndex,
                                                       EnemyExperienceDropModuleElement experienceModule,
                                                       NativeArray<EnemyExperienceDropDefinitionElement> definitions,
                                                       float rewardMultiplier,
                                                       float remainingSpawnBudget,
                                                       ref int remainingFrameDropSpawnBudget,
                                                       ref int remainingRuntimeDropPoolExpansionBudget)
    {
        if (remainingFrameDropSpawnBudget <= 0)
            return 0f;

        int definitionCount = math.max(0, experienceModule.DefinitionCount);

        if (definitionCount <= 0)
            return 0f;

        float minimumTotalExperienceDrop = math.max(0f, experienceModule.MinimumTotalExperienceDrop) * math.max(0f, rewardMultiplier);
        float maximumTotalExperienceDrop = math.max(experienceModule.MinimumTotalExperienceDrop, experienceModule.MaximumTotalExperienceDrop) * math.max(0f, rewardMultiplier);
        maximumTotalExperienceDrop = math.max(minimumTotalExperienceDrop, maximumTotalExperienceDrop);
        maximumTotalExperienceDrop = math.min(maximumTotalExperienceDrop, math.max(0f, remainingSpawnBudget));
        minimumTotalExperienceDrop = math.min(minimumTotalExperienceDrop, maximumTotalExperienceDrop);

        if (maximumTotalExperienceDrop <= 0f || !EnemyDropSpawnRuntimeUtility.IsFinite(maximumTotalExperienceDrop))
            return 0f;

        uint randomSeed = EnemyDropSpawnRuntimeUtility.ResolveDropTotalRandomSeed(killedEvent.EnemyEntity,
                                                                                  killEventIndex,
                                                                                  moduleIndex);
        float resolvedTotalExperienceDrop;

        if (!EnemyExperienceDropDistributionUtility.TryResolveRandomCompatibleTotal(definitions,
                                                                                   experienceModule.DefinitionStartIndex,
                                                                                   definitionCount,
                                                                                   minimumTotalExperienceDrop,
                                                                                   maximumTotalExperienceDrop,
                                                                                   experienceModule.Distribution,
                                                                                   randomSeed,
                                                                                   out resolvedTotalExperienceDrop))
            return 0f;

        float remainingExperience = resolvedTotalExperienceDrop;

        if (remainingExperience <= EnemyDropSpawnRuntimeUtility.PrecisionEpsilon ||
            !EnemyDropSpawnRuntimeUtility.IsFinite(remainingExperience))
            return 0f;

        float spawnedExperience = 0f;
        float dropRadius = math.max(0f, experienceModule.DropRadius);
        float attractionSpeed = math.max(0f, experienceModule.AttractionSpeed);
        float collectDistance = math.max(0.01f, experienceModule.CollectDistance);
        float collectDistancePerPlayerSpeed = math.max(0f, experienceModule.CollectDistancePerPlayerSpeed);
        float spawnAnimationMinDuration = math.max(0f, experienceModule.SpawnAnimationMinDuration);
        float spawnAnimationMaxDuration = math.max(spawnAnimationMinDuration, experienceModule.SpawnAnimationMaxDuration);

        for (int stepIndex = 0; stepIndex < EnemyDropSpawnRuntimeUtility.MaxSpawnStepsPerEnemy; stepIndex++)
        {
            if (remainingExperience <= EnemyDropSpawnRuntimeUtility.PrecisionEpsilon || remainingFrameDropSpawnBudget <= 0)
                break;

            int definitionIndex;
            float definitionAmount;
            bool fitsRemaining;

            if (!EnemyExperienceDropDistributionUtility.TryResolveNextDefinition(definitions,
                                                                                 experienceModule.DefinitionStartIndex,
                                                                                 definitionCount,
                                                                                 remainingExperience,
                                                                                 experienceModule.Distribution,
                                                                                 out definitionIndex,
                                                                                 out definitionAmount,
                                                                                 out fitsRemaining))
                break;

            if (definitionIndex < 0 ||
                definitionAmount <= 0f ||
                !EnemyDropSpawnRuntimeUtility.IsFinite(definitionAmount))
                break;

            EnemyExperienceDropDefinitionElement definition = definitions[definitionIndex];
            Entity poolEntity;

            if (!EnemyExperienceDropPoolUtility.TryResolvePoolEntity(poolMap, definition.PrefabEntity, out poolEntity))
                break;

            Entity dropEntity;

            if (!EnemyExperienceDropPoolUtility.TryAcquireDrop(entityManager,
                                                               poolEntity,
                                                               out dropEntity,
                                                               ref remainingRuntimeDropPoolExpansionBudget))
                break;

            float3 spawnCenterPosition = killedEvent.Position;
            float3 spawnTargetPosition = EnemyDropSpawnRuntimeUtility.ResolveDropSpawnPosition(spawnCenterPosition,
                                                                                               moduleIndex * EnemyDropSpawnRuntimeUtility.MaxSpawnStepsPerEnemy + stepIndex,
                                                                                               dropRadius);
            float spawnAnimationDuration = EnemyDropSpawnRuntimeUtility.ResolveSpawnAnimationDuration(moduleIndex * EnemyDropSpawnRuntimeUtility.MaxSpawnStepsPerEnemy + stepIndex,
                                                                                                      spawnAnimationMinDuration,
                                                                                                      spawnAnimationMaxDuration);
            LocalTransform dropTransform = entityManager.GetComponentData<LocalTransform>(dropEntity);
            dropTransform.Position = spawnCenterPosition;
            entityManager.SetComponentData(dropEntity, dropTransform);

            EnemyExperienceDrop dropData = entityManager.GetComponentData<EnemyExperienceDrop>(dropEntity);
            dropData.RewardKind = EnemyDropPickupRewardKind.Experience;
            dropData.ExperienceAmount = definitionAmount;
            dropData.HealthRestoreAmount = 0f;
            dropData.ShieldRestoreAmount = 0f;
            dropData.AttractionSpeed = attractionSpeed;
            dropData.CollectDistance = collectDistance;
            dropData.CollectDistancePerPlayerSpeed = collectDistancePerPlayerSpeed;
            dropData.SpawnStartPosition = spawnCenterPosition;
            dropData.SpawnTargetPosition = spawnTargetPosition;
            dropData.SpawnAnimationDuration = spawnAnimationDuration;
            dropData.SpawnAnimationElapsed = 0f;
            dropData.PoolEntity = poolEntity;
            dropData.IsAttracting = 0;
            entityManager.SetComponentData(dropEntity, dropData);
            entityManager.SetComponentEnabled<EnemyExperienceDropActive>(dropEntity, true);
            spawnedExperience += definitionAmount;
            remainingFrameDropSpawnBudget--;

            if (!fitsRemaining)
                break;

            remainingExperience -= definitionAmount;
        }

        return spawnedExperience;
    }

    /// <summary>
    /// Resolves the experience reward multiplier attached to special enemies such as boss-spawned minions.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read optional reward component.</param>
    /// <param name="enemyEntity">Enemy entity being killed.</param>
    /// <returns>Non-negative experience multiplier.</returns>
    private static float ResolveExperienceRewardMultiplier(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasComponent<EnemyDropRewardMultiplier>(enemyEntity))
            return 1f;

        EnemyDropRewardMultiplier rewardMultiplier = entityManager.GetComponentData<EnemyDropRewardMultiplier>(enemyEntity);
        return math.max(0f, rewardMultiplier.ExperienceMultiplier);
    }

    #endregion

    #endregion
}
