using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns pooled experience drops for enemies killed in the current frame.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyFinalizeDespawnSystem))]
[UpdateBefore(typeof(EnemyExperienceDropCollectSystem))]
public partial struct EnemyExperienceDropSpawnSystem : ISystem
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    private const float TwoPi = 6.283185307179586f;
    private const int MaxSpawnStepsPerEnemy = 4096;
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
        if (SystemAPI.TryGetSingletonEntity<EnemyExperienceDropPoolRegistry>(out Entity registryEntity) == false)
            return;

        EntityManager entityManager = state.EntityManager;

        if (entityManager.HasBuffer<EnemyExperienceDropPoolMapElement>(registryEntity) == false)
            return;

        DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);

        if (poolMap.Length <= 0)
            return;

        if (SystemAPI.TryGetSingletonBuffer<EnemyKilledEventElement>(out DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer) == false)
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

        if (remainingExperienceCapacity <= PrecisionEpsilon)
            return;

        float activeWorldDropExperience = ResolveActiveWorldDropExperience(ref state, remainingExperienceCapacity);
        float remainingSpawnBudget = math.max(0f, remainingExperienceCapacity - activeWorldDropExperience);

        if (remainingSpawnBudget <= PrecisionEpsilon)
            return;

        Allocator frameAllocator = state.WorldUpdateAllocator;

        // Snapshot buffers to keep iteration stable even if pool expansion performs structural changes.
        NativeArray<EnemyExperienceDropPoolMapElement> poolMapSnapshot = CollectionHelper.CreateNativeArray(poolMap.AsNativeArray(), frameAllocator);
        NativeArray<EnemyKilledEventElement> killedEventsSnapshot = CollectionHelper.CreateNativeArray(killedEventsBuffer.AsNativeArray(), frameAllocator);
        int remainingFrameDropSpawnBudget = MaxDropSpawnInstancesPerFrame;
        int remainingRuntimeDropPoolExpansionBudget = MaxRuntimeDropPoolExpansionPerFrame;

        for (int killedIndex = 0; killedIndex < killedEventsSnapshot.Length; killedIndex++)
        {
            if (remainingSpawnBudget <= PrecisionEpsilon || remainingFrameDropSpawnBudget <= 0)
                break;

            remainingSpawnBudget -= SpawnDropsForKilledEnemy(entityManager,
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

            if (sanitizedStopAtExperience > PrecisionEpsilon && totalActiveWorldDropExperience >= sanitizedStopAtExperience)
                break;
        }

        return totalActiveWorldDropExperience;
    }

    private static float SpawnDropsForKilledEnemy(EntityManager entityManager,
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

        if (enemyEntity == Entity.Null || entityManager.Exists(enemyEntity) == false)
            return 0f;

        if (entityManager.HasComponent<EnemyDropItemsConfig>(enemyEntity) == false)
            return 0f;

        EnemyDropItemsConfig dropItemsConfig = entityManager.GetComponentData<EnemyDropItemsConfig>(enemyEntity);

        if (dropItemsConfig.HasExperienceDrops == 0 || dropItemsConfig.ExperienceModuleCount <= 0)
            return 0f;

        if (entityManager.HasBuffer<EnemyExperienceDropModuleElement>(enemyEntity) == false)
            return 0f;

        if (entityManager.HasBuffer<EnemyExperienceDropDefinitionElement>(enemyEntity) == false)
            return 0f;

        NativeArray<EnemyExperienceDropModuleElement> experienceModules = CollectionHelper.CreateNativeArray(entityManager.GetBuffer<EnemyExperienceDropModuleElement>(enemyEntity).AsNativeArray(),
                                                                                                             frameAllocator);
        NativeArray<EnemyExperienceDropDefinitionElement> definitions = CollectionHelper.CreateNativeArray(entityManager.GetBuffer<EnemyExperienceDropDefinitionElement>(enemyEntity).AsNativeArray(),
                                                                                                           frameAllocator);

        if (experienceModules.Length <= 0 || definitions.Length <= 0)
            return 0f;

        float rewardMultiplier = ResolveExperienceRewardMultiplier(entityManager, enemyEntity);

        if (rewardMultiplier <= PrecisionEpsilon)
            return 0f;

        float spawnedExperience = 0f;

        for (int moduleIndex = 0; moduleIndex < experienceModules.Length; moduleIndex++)
        {
            float remainingModuleSpawnBudget = math.max(0f, remainingSpawnBudget - spawnedExperience);

            if (remainingModuleSpawnBudget <= PrecisionEpsilon || remainingFrameDropSpawnBudget <= 0)
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

        if (maximumTotalExperienceDrop <= 0f || !IsFinite(maximumTotalExperienceDrop))
            return 0f;

        uint randomSeed = ResolveDropTotalRandomSeed(killedEvent.EnemyEntity, killEventIndex, moduleIndex);
        float resolvedTotalExperienceDrop;

        if (!EnemyExperienceDropDistributionUtility.TryResolveRandomCompatibleTotal(definitions,
                                                                                    experienceModule.DefinitionStartIndex,
                                                                                    definitionCount,
                                                                                    minimumTotalExperienceDrop,
                                                                                    maximumTotalExperienceDrop,
                                                                                    experienceModule.Distribution,
                                                                                    randomSeed,
                                                                                    out resolvedTotalExperienceDrop))
        {
            return 0f;
        }

        float remainingExperience = resolvedTotalExperienceDrop;

        if (remainingExperience <= PrecisionEpsilon || !IsFinite(remainingExperience))
            return 0f;

        float spawnedExperience = 0f;
        float dropRadius = math.max(0f, experienceModule.DropRadius);
        float attractionSpeed = math.max(0f, experienceModule.AttractionSpeed);
        float collectDistance = math.max(0.01f, experienceModule.CollectDistance);
        float collectDistancePerPlayerSpeed = math.max(0f, experienceModule.CollectDistancePerPlayerSpeed);
        float spawnAnimationMinDuration = math.max(0f, experienceModule.SpawnAnimationMinDuration);
        float spawnAnimationMaxDuration = math.max(spawnAnimationMinDuration, experienceModule.SpawnAnimationMaxDuration);

        for (int stepIndex = 0; stepIndex < MaxSpawnStepsPerEnemy; stepIndex++)
        {
            if (remainingExperience <= PrecisionEpsilon || remainingFrameDropSpawnBudget <= 0)
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
            {
                break;
            }

            if (definitionIndex < 0 || definitionAmount <= 0f || !IsFinite(definitionAmount))
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
            float3 spawnTargetPosition = ResolveDropSpawnPosition(spawnCenterPosition,
                                                                  moduleIndex * MaxSpawnStepsPerEnemy + stepIndex,
                                                                  dropRadius);
            float spawnAnimationDuration = ResolveSpawnAnimationDuration(moduleIndex * MaxSpawnStepsPerEnemy + stepIndex,
                                                                        spawnAnimationMinDuration,
                                                                        spawnAnimationMaxDuration);
            LocalTransform dropTransform = entityManager.GetComponentData<LocalTransform>(dropEntity);
            dropTransform.Position = spawnCenterPosition;
            entityManager.SetComponentData(dropEntity, dropTransform);

            EnemyExperienceDrop dropData = entityManager.GetComponentData<EnemyExperienceDrop>(dropEntity);
            dropData.ExperienceAmount = definitionAmount;
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
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the experience reward multiplier attached to special enemies such as boss-spawned minions.
    /// /params entityManager Entity manager used to read optional reward component.
    /// /params enemyEntity Enemy entity being killed.
    /// /returns Non-negative experience multiplier.
    /// </summary>
    private static float ResolveExperienceRewardMultiplier(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasComponent<EnemyDropRewardMultiplier>(enemyEntity))
            return 1f;

        EnemyDropRewardMultiplier rewardMultiplier = entityManager.GetComponentData<EnemyDropRewardMultiplier>(enemyEntity);
        return math.max(0f, rewardMultiplier.ExperienceMultiplier);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static uint ResolveDropTotalRandomSeed(Entity enemyEntity, int killEventIndex, int moduleIndex)
    {
        int sanitizedKillEventIndex = math.max(0, killEventIndex);
        int sanitizedModuleIndex = math.max(0, moduleIndex);
        uint seed = math.hash(new int4(enemyEntity.Index,
                                       enemyEntity.Version,
                                       sanitizedKillEventIndex + 1,
                                       sanitizedModuleIndex + 19349663));

        if (seed == 0u)
            return 1u;

        return seed;
    }

    private static float3 ResolveDropSpawnPosition(float3 centerPosition, int dropIndex, float dropRadius)
    {
        if (dropRadius <= PrecisionEpsilon)
            return centerPosition;

        // Deterministic low-discrepancy samples spread drops without clustering and without per-frame RNG state.
        int sampleIndex = dropIndex + 1;
        float radialSample = ResolveHaltonSequence(sampleIndex, 2);
        float angularSample = ResolveHaltonSequence(sampleIndex, 3);
        float radius = dropRadius * math.sqrt(math.saturate(radialSample));
        float angleRadians = TwoPi * angularSample;
        float2 offset = new float2(math.cos(angleRadians), math.sin(angleRadians)) * radius;
        float3 position = centerPosition;
        position.x += offset.x;
        position.z += offset.y;
        return position;
    }

    private static float ResolveHaltonSequence(int index, int radix)
    {
        float result = 0f;
        float fraction = 1f / radix;
        int remaining = index;

        while (remaining > 0)
        {
            int digit = remaining % radix;
            result += fraction * digit;
            remaining /= radix;
            fraction /= radix;
        }

        return result;
    }

    private static float ResolveSpawnAnimationDuration(int dropIndex, float minimumDuration, float maximumDuration)
    {
        int sampleIndex = dropIndex + 1;
        float durationSample = ResolveHaltonSequence(sampleIndex, 5);
        return math.lerp(minimumDuration, maximumDuration, durationSample);
    }
    #endregion

    #endregion
}
