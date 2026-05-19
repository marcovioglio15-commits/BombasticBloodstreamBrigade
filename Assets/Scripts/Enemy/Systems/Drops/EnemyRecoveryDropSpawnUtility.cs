using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns pooled health and shield recovery pickups from enemy Drop Items modules.
/// </summary>
internal static class EnemyRecoveryDropSpawnUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Spawns health/shield recovery drops for one killed enemy.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read enemy buffers and activate pooled drops.</param>
    /// <param name="poolMap">Snapshot prefab-to-pool map.</param>
    /// <param name="killedEvent">Killed enemy event being processed.</param>
    /// <param name="killEventIndex">Index of the kill event inside the current frame snapshot.</param>
    /// <param name="frameAllocator">Frame allocator used to snapshot module buffers safely.</param>
    /// <param name="remainingFrameDropSpawnBudget">Mutable frame budget shared by all drop kinds.</param>
    /// <param name="remainingRuntimeDropPoolExpansionBudget">Mutable runtime pool expansion budget.</param>
    public static void SpawnRecoveryDropsForKilledEnemy(EntityManager entityManager,
                                                        NativeArray<EnemyExperienceDropPoolMapElement> poolMap,
                                                        EnemyKilledEventElement killedEvent,
                                                        int killEventIndex,
                                                        Allocator frameAllocator,
                                                        ref int remainingFrameDropSpawnBudget,
                                                        ref int remainingRuntimeDropPoolExpansionBudget)
    {
        if (remainingFrameDropSpawnBudget <= 0)
            return;

        Entity enemyEntity = killedEvent.EnemyEntity;

        if (enemyEntity == Entity.Null || !entityManager.Exists(enemyEntity))
            return;

        if (!entityManager.HasComponent<EnemyDropItemsConfig>(enemyEntity))
            return;

        EnemyDropItemsConfig dropItemsConfig = entityManager.GetComponentData<EnemyDropItemsConfig>(enemyEntity);

        if (dropItemsConfig.HasRecoveryDrops == 0 || dropItemsConfig.RecoveryModuleCount <= 0)
            return;

        if (!entityManager.HasBuffer<EnemyRecoveryDropModuleElement>(enemyEntity))
            return;

        if (!entityManager.HasBuffer<EnemyRecoveryDropDefinitionElement>(enemyEntity))
            return;

        NativeArray<EnemyRecoveryDropModuleElement> recoveryModules = CollectionHelper.CreateNativeArray(entityManager.GetBuffer<EnemyRecoveryDropModuleElement>(enemyEntity).AsNativeArray(),
                                                                                                          frameAllocator);
        NativeArray<EnemyRecoveryDropDefinitionElement> definitions = CollectionHelper.CreateNativeArray(entityManager.GetBuffer<EnemyRecoveryDropDefinitionElement>(enemyEntity).AsNativeArray(),
                                                                                                         frameAllocator);
        NativeArray<EnemyDropItemsModuleSelectionElement> selectionModules = default;

        if (entityManager.HasBuffer<EnemyDropItemsModuleSelectionElement>(enemyEntity))
        {
            selectionModules = CollectionHelper.CreateNativeArray(entityManager.GetBuffer<EnemyDropItemsModuleSelectionElement>(enemyEntity).AsNativeArray(),
                                                                  frameAllocator);
        }

        if (recoveryModules.Length <= 0 || definitions.Length <= 0)
            return;

        for (int moduleIndex = 0; moduleIndex < recoveryModules.Length; moduleIndex++)
        {
            if (remainingFrameDropSpawnBudget <= 0)
                break;

            if (!EnemyDropItemsModuleSelectionUtility.ShouldResolveModule(enemyEntity,
                                                                          killEventIndex,
                                                                          in dropItemsConfig,
                                                                          selectionModules,
                                                                          EnemyDropItemsPayloadKind.Recovery,
                                                                          moduleIndex))
            {
                continue;
            }

            SpawnDropsForRecoveryModule(entityManager,
                                        poolMap,
                                        killedEvent,
                                        killEventIndex,
                                        moduleIndex,
                                        recoveryModules[moduleIndex],
                                        definitions,
                                        ref remainingFrameDropSpawnBudget,
                                        ref remainingRuntimeDropPoolExpansionBudget);
        }
    }
    #endregion

    #region Spawn Helpers
    /// <summary>
    /// Spawns pickup instances for one recovery module.
    /// </summary>
    /// <param name="entityManager">Entity manager used to activate pooled drops.</param>
    /// <param name="poolMap">Snapshot prefab-to-pool map.</param>
    /// <param name="killedEvent">Killed enemy event being processed.</param>
    /// <param name="killEventIndex">Index of the kill event inside the current frame snapshot.</param>
    /// <param name="moduleIndex">Recovery module index used for deterministic random seeds.</param>
    /// <param name="recoveryModule">Baked recovery module settings.</param>
    /// <param name="definitions">Baked recovery definitions snapshot.</param>
    /// <param name="remainingFrameDropSpawnBudget">Mutable frame budget shared by all drop kinds.</param>
    /// <param name="remainingRuntimeDropPoolExpansionBudget">Mutable runtime pool expansion budget.</param>
    private static void SpawnDropsForRecoveryModule(EntityManager entityManager,
                                                    NativeArray<EnemyExperienceDropPoolMapElement> poolMap,
                                                    EnemyKilledEventElement killedEvent,
                                                    int killEventIndex,
                                                    int moduleIndex,
                                                    EnemyRecoveryDropModuleElement recoveryModule,
                                                    NativeArray<EnemyRecoveryDropDefinitionElement> definitions,
                                                    ref int remainingFrameDropSpawnBudget,
                                                    ref int remainingRuntimeDropPoolExpansionBudget)
    {
        int definitionCount = math.max(0, recoveryModule.DefinitionCount);

        if (definitionCount <= 0 || remainingFrameDropSpawnBudget <= 0)
            return;

        int dropCount = ResolveRecoveryDropCount(killedEvent.EnemyEntity,
                                                 killEventIndex,
                                                 moduleIndex,
                                                 recoveryModule.MinimumDropCount,
                                                 recoveryModule.MaximumDropCount);
        dropCount = math.min(dropCount, EnemyDropSpawnRuntimeUtility.MaxSpawnStepsPerEnemy);

        if (dropCount <= 0)
            return;

        float dropRadius = math.max(0f, recoveryModule.DropRadius);
        float attractionSpeed = math.max(0f, recoveryModule.AttractionSpeed);
        float collectDistance = math.max(0.01f, recoveryModule.CollectDistance);
        float collectDistancePerPlayerSpeed = math.max(0f, recoveryModule.CollectDistancePerPlayerSpeed);
        float spawnAnimationMinDuration = math.max(0f, recoveryModule.SpawnAnimationMinDuration);
        float spawnAnimationMaxDuration = math.max(spawnAnimationMinDuration, recoveryModule.SpawnAnimationMaxDuration);

        for (int dropIndex = 0; dropIndex < dropCount; dropIndex++)
        {
            if (remainingFrameDropSpawnBudget <= 0)
                break;

            int definitionIndex = ResolveRecoveryDefinitionIndex(definitions,
                                                                 recoveryModule.DefinitionStartIndex,
                                                                 definitionCount,
                                                                 recoveryModule.Distribution,
                                                                 ResolveRecoveryDefinitionSeed(killedEvent.EnemyEntity,
                                                                                               killEventIndex,
                                                                                               moduleIndex,
                                                                                               dropIndex));

            if (definitionIndex < 0)
                break;

            EnemyRecoveryDropDefinitionElement definition = definitions[definitionIndex];

            if (definition.HealthRestoreAmount <= 0f && definition.ShieldRestoreAmount <= 0f)
                continue;

            Entity poolEntity;

            if (!EnemyExperienceDropPoolUtility.TryResolvePoolEntity(poolMap, definition.PrefabEntity, out poolEntity))
                break;

            Entity dropEntity;

            if (!EnemyExperienceDropPoolUtility.TryAcquireDrop(entityManager,
                                                               poolEntity,
                                                               out dropEntity,
                                                               ref remainingRuntimeDropPoolExpansionBudget))
                break;

            ActivateRecoveryDrop(entityManager,
                                 dropEntity,
                                 poolEntity,
                                 in definition,
                                 killedEvent.Position,
                                 moduleIndex,
                                 dropIndex,
                                 dropRadius,
                                 attractionSpeed,
                                 collectDistance,
                                 collectDistancePerPlayerSpeed,
                                 spawnAnimationMinDuration,
                                 spawnAnimationMaxDuration);
            remainingFrameDropSpawnBudget--;
        }
    }

    /// <summary>
    /// Writes recovery payload data onto one pooled pickup and enables it for collection simulation.
    /// </summary>
    /// <param name="entityManager">Entity manager used to write pooled entity data.</param>
    /// <param name="dropEntity">Pooled drop entity being activated.</param>
    /// <param name="poolEntity">Pool entity that owns the drop.</param>
    /// <param name="definition">Recovery definition payload selected for this pickup.</param>
    /// <param name="spawnCenterPosition">Killed enemy position used as the initial drop position.</param>
    /// <param name="moduleIndex">Recovery module index used for deterministic spawn spread.</param>
    /// <param name="dropIndex">Pickup index inside the module.</param>
    /// <param name="dropRadius">Maximum radial spread distance.</param>
    /// <param name="attractionSpeed">Speed used while the pickup moves toward the player.</param>
    /// <param name="collectDistance">Base collection distance.</param>
    /// <param name="collectDistancePerPlayerSpeed">Additional collection distance per unit of player speed.</param>
    /// <param name="spawnAnimationMinDuration">Minimum spawn spread animation duration.</param>
    /// <param name="spawnAnimationMaxDuration">Maximum spawn spread animation duration.</param>
    private static void ActivateRecoveryDrop(EntityManager entityManager,
                                             Entity dropEntity,
                                             Entity poolEntity,
                                             in EnemyRecoveryDropDefinitionElement definition,
                                             float3 spawnCenterPosition,
                                             int moduleIndex,
                                             int dropIndex,
                                             float dropRadius,
                                             float attractionSpeed,
                                             float collectDistance,
                                             float collectDistancePerPlayerSpeed,
                                             float spawnAnimationMinDuration,
                                             float spawnAnimationMaxDuration)
    {
        int resolvedDropIndex = moduleIndex * EnemyDropSpawnRuntimeUtility.MaxSpawnStepsPerEnemy + dropIndex;
        float3 spawnTargetPosition = EnemyDropSpawnRuntimeUtility.ResolveDropSpawnPosition(spawnCenterPosition,
                                                                                          resolvedDropIndex,
                                                                                          dropRadius);
        float spawnAnimationDuration = EnemyDropSpawnRuntimeUtility.ResolveSpawnAnimationDuration(resolvedDropIndex,
                                                                                                 spawnAnimationMinDuration,
                                                                                                 spawnAnimationMaxDuration);
        LocalTransform dropTransform = entityManager.GetComponentData<LocalTransform>(dropEntity);
        dropTransform.Position = spawnCenterPosition;
        entityManager.SetComponentData(dropEntity, dropTransform);

        EnemyExperienceDrop dropData = entityManager.GetComponentData<EnemyExperienceDrop>(dropEntity);
        dropData.RewardKind = EnemyDropPickupRewardKind.Recovery;
        dropData.ExperienceAmount = 0f;
        dropData.HealthRestoreAmount = math.max(0f, definition.HealthRestoreAmount);
        dropData.ShieldRestoreAmount = math.max(0f, definition.ShieldRestoreAmount);
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
    }
    #endregion

    #region Selection
    /// <summary>
    /// Resolves a deterministic recovery drop count inside the authored module range.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed selection.</param>
    /// <param name="killEventIndex">Index of the kill event inside the current frame snapshot.</param>
    /// <param name="moduleIndex">Recovery module index used to decorrelate sibling modules.</param>
    /// <param name="minimumDropCount">Minimum authored drop count.</param>
    /// <param name="maximumDropCount">Maximum authored drop count.</param>
    /// <returns>Resolved non-negative pickup count.</returns>
    private static int ResolveRecoveryDropCount(Entity enemyEntity,
                                                int killEventIndex,
                                                int moduleIndex,
                                                int minimumDropCount,
                                                int maximumDropCount)
    {
        int sanitizedMinimumDropCount = math.max(0, minimumDropCount);
        int sanitizedMaximumDropCount = math.max(sanitizedMinimumDropCount, maximumDropCount);

        if (sanitizedMaximumDropCount <= sanitizedMinimumDropCount)
            return sanitizedMinimumDropCount;

        Unity.Mathematics.Random random = new Unity.Mathematics.Random(EnemyDropSpawnRuntimeUtility.ResolveDropTotalRandomSeed(enemyEntity,
                                                                                                                                killEventIndex,
                                                                                                                                moduleIndex + 7919));
        return random.NextInt(sanitizedMinimumDropCount, sanitizedMaximumDropCount + 1);
    }

    /// <summary>
    /// Selects one recovery definition using a two-pass weighted scan without temporary allocations.
    /// </summary>
    /// <param name="definitions">Recovery definitions snapshot.</param>
    /// <param name="definitionStartIndex">First definition index owned by the module.</param>
    /// <param name="definitionCount">Number of definitions owned by the module.</param>
    /// <param name="distribution">Bias where 0 favors low restorative values and 1 favors high restorative values.</param>
    /// <param name="seed">Deterministic non-zero random seed.</param>
    /// <returns>Selected definition index, or -1 when no valid definition exists.</returns>
    private static int ResolveRecoveryDefinitionIndex(NativeArray<EnemyRecoveryDropDefinitionElement> definitions,
                                                      int definitionStartIndex,
                                                      int definitionCount,
                                                      float distribution,
                                                      uint seed)
    {
        int startIndex = math.max(0, definitionStartIndex);
        int endIndex = math.min(definitions.Length, startIndex + math.max(0, definitionCount));
        float clampedDistribution = math.clamp(distribution, 0f, 1f);
        float totalWeight = 0f;

        for (int definitionIndex = startIndex; definitionIndex < endIndex; definitionIndex++)
        {
            EnemyRecoveryDropDefinitionElement definition = definitions[definitionIndex];
            totalWeight += ResolveRecoveryDefinitionWeight(in definition, clampedDistribution);
        }

        if (totalWeight <= EnemyDropSpawnRuntimeUtility.PrecisionEpsilon)
            return -1;

        Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
        float roll = random.NextFloat(0f, totalWeight);
        float cumulativeWeight = 0f;

        for (int definitionIndex = startIndex; definitionIndex < endIndex; definitionIndex++)
        {
            EnemyRecoveryDropDefinitionElement definition = definitions[definitionIndex];
            cumulativeWeight += ResolveRecoveryDefinitionWeight(in definition, clampedDistribution);

            if (roll <= cumulativeWeight)
                return definitionIndex;
        }

        return endIndex - 1;
    }

    /// <summary>
    /// Resolves one definition selection weight from its combined restorative value.
    /// </summary>
    /// <param name="definition">Recovery definition being weighted.</param>
    /// <param name="distribution">Bias where 0 favors low restorative values and 1 favors high restorative values.</param>
    /// <returns>Positive selection weight for valid recovery definitions, otherwise zero.</returns>
    private static float ResolveRecoveryDefinitionWeight(in EnemyRecoveryDropDefinitionElement definition, float distribution)
    {
        float combinedRestoreAmount = math.max(0f, definition.HealthRestoreAmount) + math.max(0f, definition.ShieldRestoreAmount);

        if (combinedRestoreAmount <= 0f)
            return 0f;

        float lowValueWeight = 1f / math.max(0.01f, combinedRestoreAmount);
        float highValueWeight = math.max(0.01f, combinedRestoreAmount);
        return math.lerp(lowValueWeight, highValueWeight, math.saturate(distribution));
    }

    /// <summary>
    /// Builds a deterministic non-zero seed for one recovery definition draw.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed selection.</param>
    /// <param name="killEventIndex">Index of the kill event inside the current frame snapshot.</param>
    /// <param name="moduleIndex">Recovery module index.</param>
    /// <param name="dropIndex">Pickup index inside the module.</param>
    /// <returns>Non-zero deterministic random seed.</returns>
    private static uint ResolveRecoveryDefinitionSeed(Entity enemyEntity,
                                                      int killEventIndex,
                                                      int moduleIndex,
                                                      int dropIndex)
    {
        uint seed = math.hash(new int4(enemyEntity.Index,
                                       enemyEntity.Version,
                                       math.max(0, killEventIndex) + moduleIndex * 397,
                                       math.max(0, dropIndex) + 297121507));

        if (seed == 0u)
            return 1u;

        return seed;
    }
    #endregion

    #endregion
}
