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

        if (!ShouldDropRecoveryModule(killedEvent.EnemyEntity,
                                      killEventIndex,
                                      moduleIndex,
                                      recoveryModule.DropChance))
        {
            return;
        }

        float dropRadius = math.max(0f, recoveryModule.DropRadius);
        float attractionSpeed = math.max(0f, recoveryModule.AttractionSpeed);
        float collectDistance = math.max(0.01f, recoveryModule.CollectDistance);
        float collectDistancePerPlayerSpeed = math.max(0f, recoveryModule.CollectDistancePerPlayerSpeed);
        float spawnAnimationMinDuration = math.max(0f, recoveryModule.SpawnAnimationMinDuration);
        float spawnAnimationMaxDuration = math.max(spawnAnimationMinDuration, recoveryModule.SpawnAnimationMaxDuration);
        int definitionStartIndex = math.max(0, recoveryModule.DefinitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, definitionStartIndex + definitionCount);
        int spawnedDropIndex = 0;

        for (int definitionIndex = definitionStartIndex; definitionIndex < definitionEndIndex; definitionIndex++)
        {
            if (remainingFrameDropSpawnBudget <= 0)
                break;

            if (spawnedDropIndex >= EnemyDropSpawnRuntimeUtility.MaxSpawnStepsPerEnemy)
                break;

            EnemyRecoveryDropDefinitionElement definition = definitions[definitionIndex];
            int definitionDropCount = math.max(0, definition.Count);

            if (definitionDropCount <= 0 ||
                (definition.HealthRestoreAmount <= 0f && definition.ShieldRestoreAmount <= 0f))
            {
                continue;
            }

            Entity poolEntity;

            if (!EnemyExperienceDropPoolUtility.TryResolvePoolEntity(poolMap, definition.PrefabEntity, out poolEntity))
                continue;

            for (int dropIndex = 0; dropIndex < definitionDropCount; dropIndex++)
            {
                if (remainingFrameDropSpawnBudget <= 0)
                    break;

                if (spawnedDropIndex >= EnemyDropSpawnRuntimeUtility.MaxSpawnStepsPerEnemy)
                    break;

                Entity dropEntity;

                if (!EnemyExperienceDropPoolUtility.TryAcquireDrop(entityManager,
                                                                   poolEntity,
                                                                   out dropEntity,
                                                                   ref remainingRuntimeDropPoolExpansionBudget))
                {
                    return;
                }

                ActivateRecoveryDrop(entityManager,
                                     dropEntity,
                                     poolEntity,
                                     in definition,
                                     killedEvent.Position,
                                     moduleIndex,
                                     spawnedDropIndex,
                                     dropRadius,
                                     attractionSpeed,
                                     collectDistance,
                                     collectDistancePerPlayerSpeed,
                                     spawnAnimationMinDuration,
                                     spawnAnimationMaxDuration);
                remainingFrameDropSpawnBudget--;
                spawnedDropIndex++;
            }
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
    /// Resolves whether one recovery module should emit its fixed definition counts for this kill event.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed the drop chance.</param>
    /// <param name="killEventIndex">Index of the kill event inside the current frame snapshot.</param>
    /// <param name="moduleIndex">Recovery module index used to decorrelate sibling modules.</param>
    /// <param name="dropChance">Normalized 0-1 module chance.</param>
    /// <returns>True when the fixed recovery payload should be spawned.</returns>
    private static bool ShouldDropRecoveryModule(Entity enemyEntity,
                                                 int killEventIndex,
                                                 int moduleIndex,
                                                 float dropChance)
    {
        float clampedDropChance = math.clamp(dropChance, 0f, 1f);

        if (clampedDropChance <= 0f)
            return false;

        if (clampedDropChance >= 1f)
            return true;

        Unity.Mathematics.Random random = new Unity.Mathematics.Random(ResolveRecoveryModuleChanceSeed(enemyEntity,
                                                                                                       killEventIndex,
                                                                                                       moduleIndex));
        return random.NextFloat() <= clampedDropChance;
    }

    /// <summary>
    /// Builds a deterministic non-zero seed for one recovery module chance roll.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed the module chance.</param>
    /// <param name="killEventIndex">Index of the kill event inside the current frame snapshot.</param>
    /// <param name="moduleIndex">Recovery module index.</param>
    /// <returns>Non-zero deterministic random seed.</returns>
    private static uint ResolveRecoveryModuleChanceSeed(Entity enemyEntity,
                                                       int killEventIndex,
                                                       int moduleIndex)
    {
        uint seed = math.hash(new int4(enemyEntity.Index,
                                       enemyEntity.Version,
                                       math.max(0, killEventIndex) + moduleIndex * 397,
                                       297121507));

        if (seed == 0u)
            return 1u;

        return seed;
    }
    #endregion

    #endregion
}
