using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Consumes baked wave events and activates pooled enemies when their scheduled time becomes due.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
public partial struct EnemySpawnSystem : ISystem
{
    #region Constants
    private const float MinimumWarningRadius = 0.12f;
    #endregion

    #region Fields
    private EntityQuery spawnerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the query used to iterate initialized wave spawners.
    /// state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        spawnerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemySpawner,
                     EnemySpawnerState,
                     EnemySpawnWarningConfig,
                     EnemySpawnerWaveDefinitionElement,
                     EnemySpawnerWaveRuntimeElement,
                     EnemySpawnerWaveEventElement>()
            .WithAll<EnemySpawnerPrefabPoolMapElement,
                     LocalToWorld>()
            .Build(ref state);
        state.RequireForUpdate(spawnerQuery);
    }

    /// <summary>
    /// Schedules wave starts, emits due events and activates pooled enemies at their baked positions.
    /// state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        entityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        NativeArray<Entity> spawnerEntities = spawnerQuery.ToEntityArray(state.WorldUpdateAllocator);

        for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
            ProcessSpawner(entityManager, spawnerEntities[spawnerIndex], elapsedTime);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Processes one spawner by updating wave scheduling and consuming due events.
    /// entityManager: Entity manager used to access components and buffers.
    /// spawnerEntity: Spawner entity being processed.
    /// elapsedTime: Current elapsed world time.
    /// returns None.
    /// </summary>
    private static void ProcessSpawner(EntityManager entityManager,
                                       Entity spawnerEntity,
                                       float elapsedTime)
    {
        if (!entityManager.Exists(spawnerEntity))
            return;

        EnemySpawnerState spawnerState = entityManager.GetComponentData<EnemySpawnerState>(spawnerEntity);

        if (spawnerState.Initialized == 0)
            return;

        EnemySpawnWarningConfig warningConfig = entityManager.GetComponentData<EnemySpawnWarningConfig>(spawnerEntity);
        float4x4 localToWorld = entityManager.GetComponentData<LocalToWorld>(spawnerEntity).Value;
        int waveCount = entityManager.GetBuffer<EnemySpawnerWaveDefinitionElement>(spawnerEntity).Length;

        for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
        {
            DynamicBuffer<EnemySpawnerWaveDefinitionElement> waveDefinitions = entityManager.GetBuffer<EnemySpawnerWaveDefinitionElement>(spawnerEntity);
            DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntime = entityManager.GetBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);

            if (waveIndex >= waveDefinitions.Length || waveIndex >= waveRuntime.Length)
                break;

            EnemySpawnerWaveDefinitionElement definition = waveDefinitions[waveIndex];
            EnemySpawnerWaveRuntimeElement runtime = waveRuntime[waveIndex];

            if (runtime.Completed != 0)
                continue;

            TryScheduleWaveStart(spawnerState, waveDefinitions, waveRuntime, waveIndex, ref runtime);
            TryStartWave(elapsedTime, definition, ref runtime);

            if (runtime.StartScheduled != 0 && runtime.SpawnFinished == 0)
            {
                ReserveUpcomingEvents(entityManager,
                                     spawnerEntity,
                                     localToWorld,
                                     elapsedTime,
                                     definition,
                                     warningConfig,
                                     ref runtime);

                if (runtime.Started != 0)
                {
                    ActivateDueEvents(entityManager,
                                      spawnerEntity,
                                      localToWorld,
                                      elapsedTime,
                                      definition,
                                      warningConfig,
                                      ref runtime,
                                      ref spawnerState);
                }
            }

            TryFinalizeWave(elapsedTime, definition, ref runtime);
            SetWaveRuntime(entityManager, spawnerEntity, waveIndex, runtime);
        }

        entityManager.SetComponentData(spawnerEntity, spawnerState);
    }

    /// <summary>
    /// Assigns a deterministic scheduled start time to a wave as soon as its prerequisite state is available.
    /// spawnerState: Global mutable spawner state.
    /// waveDefinitions: Immutable wave definition buffer.
    /// waveRuntime: Mutable wave runtime buffer.
    /// waveIndex: Wave index to schedule.
    /// runtime: Current wave runtime state to update.
    /// returns None.
    /// </summary>
    private static void TryScheduleWaveStart(EnemySpawnerState spawnerState,
                                             DynamicBuffer<EnemySpawnerWaveDefinitionElement> waveDefinitions,
                                             DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntime,
                                             int waveIndex,
                                             ref EnemySpawnerWaveRuntimeElement runtime)
    {
        if (runtime.StartScheduled != 0)
            return;

        EnemySpawnerWaveDefinitionElement definition = waveDefinitions[waveIndex];
        float referenceTime = spawnerState.StartTime;

        if (waveIndex > 0)
        {
            EnemySpawnerWaveRuntimeElement previousWaveRuntime = waveRuntime[waveIndex - 1];
            EnemySpawnerWaveDefinitionElement previousWaveDefinition = waveDefinitions[waveIndex - 1];

            if (!TryResolveReferenceTime(spawnerState,
                                         previousWaveDefinition,
                                         previousWaveRuntime,
                                         definition.StartMode,
                                         out referenceTime))
                return;
        }

        runtime.ScheduledStartTime = referenceTime + math.max(0f, definition.StartDelaySeconds);
        runtime.StartScheduled = 1;
    }

    /// <summary>
    /// Starts a wave once the current world time reaches its scheduled start timestamp.
    /// /params elapsedTime Current elapsed world time.
    /// /params definition Immutable definition of the wave.
    /// /params runtime Current wave runtime state to update.
    /// /returns None.
    /// </summary>
    private static void TryStartWave(float elapsedTime,
                                     EnemySpawnerWaveDefinitionElement definition,
                                     ref EnemySpawnerWaveRuntimeElement runtime)
    {
        if (runtime.StartScheduled == 0)
            return;

        if (runtime.Started != 0)
            return;

        float actualSpawnStartTime = ResolveWaveActualSpawnStartTime(runtime, definition);

        if (elapsedTime < actualSpawnStartTime)
            return;

        runtime.Started = 1;
        runtime.SpawnStartTime = actualSpawnStartTime;
        runtime.SpawnEndTime = runtime.SpawnStartTime + math.max(0f, definition.SpawnDurationSeconds);

        if (definition.EventCount <= 0 && runtime.SpawnEndTime <= elapsedTime)
            runtime.SpawnFinished = 1;
    }

    /// <summary>
    /// Reserves every upcoming spawn event whose warning lead window has opened.
    /// entityManager: Entity manager used to access pools and enemy instances.
    /// spawnerEntity: Spawner that owns the wave.
    /// localToWorld: Current spawner local-to-world matrix.
    /// elapsedTime: Current elapsed world time.
    /// definition: Immutable definition of the wave.
    /// warningConfig: Spawner-level fallback warning tuning.
    /// runtime: Mutable runtime state for the wave.
    /// returns None.
    /// </summary>
    private static void ReserveUpcomingEvents(EntityManager entityManager,
                                              Entity spawnerEntity,
                                              float4x4 localToWorld,
                                              float elapsedTime,
                                              EnemySpawnerWaveDefinitionElement definition,
                                              EnemySpawnWarningConfig warningConfig,
                                              ref EnemySpawnerWaveRuntimeElement runtime)
    {
        float actualWaveSpawnStartTime = ResolveWaveActualSpawnStartTime(runtime, definition);

        while (runtime.NextWarningEventIndex < definition.EventCount)
        {
            int eventIndex = definition.FirstEventIndex + runtime.NextWarningEventIndex;
            DynamicBuffer<EnemySpawnerWaveEventElement> waveEvents = entityManager.GetBuffer<EnemySpawnerWaveEventElement>(spawnerEntity);

            if (eventIndex < 0 || eventIndex >= waveEvents.Length)
            {
                runtime.NextWarningEventIndex = definition.EventCount;
                break;
            }

            EnemySpawnerWaveEventElement waveEvent = waveEvents[eventIndex];
            EnemySpawnWarningConfig eventWarningConfig = EnemySpawnWarningConfigUtility.ResolveEventWarningConfig(in waveEvent, in warningConfig);
            float leadTimeSeconds = EnemySpawnWarningConfigUtility.ResolveEffectiveLeadTimeSeconds(in eventWarningConfig);
            float spawnTime = actualWaveSpawnStartTime + math.max(0f, waveEvent.RelativeTime);
            float reservationTime = spawnTime - leadTimeSeconds;

            if (elapsedTime < reservationTime)
                break;

            if (waveEvent.ReservedEnemyEntity != Entity.Null)
            {
                runtime.NextWarningEventIndex++;
                continue;
            }

            Entity poolEntity;
            DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);

            if (!TryGetPoolEntity(poolMap, waveEvent.PrefabEntity, out poolEntity))
                break;

            Entity enemyEntity;

            if (!TryAcquireEnemy(entityManager, poolEntity, spawnerEntity, waveEvent.PrefabEntity, out enemyEntity))
                break;

            float3 worldPosition = math.transform(localToWorld, waveEvent.LocalSpawnPosition);
            EnemySpawnWarningState warningState = CreateWarningState(entityManager,
                                                                    waveEvent.PrefabEntity,
                                                                    worldPosition,
                                                                    spawnTime,
                                                                    eventWarningConfig);
            EnemyPoolUtility.ReserveEnemyForSpawn(entityManager,
                                                  enemyEntity,
                                                  spawnerEntity,
                                                  poolEntity,
                                                  waveEvent.WaveIndex,
                                                  worldPosition,
                                                  warningState);
            waveEvent.ReservedEnemyEntity = enemyEntity;
            SetWaveEvent(entityManager, spawnerEntity, eventIndex, waveEvent);
            runtime.NextWarningEventIndex++;
        }
    }

    /// <summary>
    /// Activates every reserved event whose due time has been reached at the current frame.
    /// entityManager: Entity manager used to access pools and enemy instances.
    /// spawnerEntity: Spawner that owns the wave.
    /// localToWorld: Current spawner local-to-world matrix.
    /// elapsedTime: Current elapsed world time.
    /// definition: Immutable definition of the wave.
    /// warningConfig: Immutable warning tuning baked from authoring.
    /// runtime: Mutable runtime state for the processed wave.
    /// spawnerState: Mutable global spawner state.
    /// returns None.
    /// </summary>
    private static void ActivateDueEvents(EntityManager entityManager,
                                          Entity spawnerEntity,
                                          float4x4 localToWorld,
                                          float elapsedTime,
                                          EnemySpawnerWaveDefinitionElement definition,
                                          EnemySpawnWarningConfig warningConfig,
                                          ref EnemySpawnerWaveRuntimeElement runtime,
                                          ref EnemySpawnerState spawnerState)
    {
        while (runtime.NextEventIndex < definition.EventCount)
        {
            int eventIndex = definition.FirstEventIndex + runtime.NextEventIndex;
            DynamicBuffer<EnemySpawnerWaveEventElement> waveEvents = entityManager.GetBuffer<EnemySpawnerWaveEventElement>(spawnerEntity);

            if (eventIndex < 0 || eventIndex >= waveEvents.Length)
            {
                runtime.NextEventIndex = definition.EventCount;
                break;
            }

            EnemySpawnerWaveEventElement waveEvent = waveEvents[eventIndex];
            float dueTime = runtime.SpawnStartTime + math.max(0f, waveEvent.RelativeTime);

            if (elapsedTime < dueTime)
                break;

            float3 worldPosition = math.transform(localToWorld, waveEvent.LocalSpawnPosition);
            Entity enemyEntity = waveEvent.ReservedEnemyEntity;

            if (enemyEntity == Entity.Null || !entityManager.Exists(enemyEntity))
            {
                Entity poolEntity;
                DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);

                if (!TryGetPoolEntity(poolMap, waveEvent.PrefabEntity, out poolEntity))
                    break;

                if (!TryAcquireEnemy(entityManager, poolEntity, spawnerEntity, waveEvent.PrefabEntity, out enemyEntity))
                    break;

                EnemySpawnWarningConfig eventWarningConfig = EnemySpawnWarningConfigUtility.ResolveEventWarningConfig(in waveEvent, in warningConfig);
                EnemySpawnWarningState warningState = CreateWarningState(entityManager,
                                                                        waveEvent.PrefabEntity,
                                                                        worldPosition,
                                                                        dueTime,
                                                                        eventWarningConfig);
                EnemyPoolUtility.ReserveEnemyForSpawn(entityManager,
                                                      enemyEntity,
                                                      spawnerEntity,
                                                      poolEntity,
                                                      waveEvent.WaveIndex,
                                                      worldPosition,
                                                      warningState);
            }

            EnemyPoolUtility.ActivateReservedEnemy(entityManager, enemyEntity, worldPosition);
            waveEvent.ReservedEnemyEntity = Entity.Null;
            SetWaveEvent(entityManager, spawnerEntity, eventIndex, waveEvent);
            runtime.NextEventIndex++;
            runtime.AliveCount++;
            runtime.SpawnedCount++;
            spawnerState.AliveCount++;

            if (runtime.NextWarningEventIndex < runtime.NextEventIndex)
                runtime.NextWarningEventIndex = runtime.NextEventIndex;
        }
    }

    /// <summary>
    /// Marks a wave as spawn-finished or completed once its conditions are satisfied.
    /// elapsedTime: Current elapsed world time.
    /// definition: Immutable definition of the wave.
    /// runtime: Mutable runtime state for the wave.
    /// returns None.
    /// </summary>
    private static void TryFinalizeWave(float elapsedTime,
                                        EnemySpawnerWaveDefinitionElement definition,
                                        ref EnemySpawnerWaveRuntimeElement runtime)
    {
        if (runtime.Started == 0)
            return;

        if (runtime.SpawnFinished == 0)
        {
            bool allEventsConsumed = runtime.NextEventIndex >= definition.EventCount;
            bool spawnWindowFinished = elapsedTime >= runtime.SpawnEndTime;

            if (allEventsConsumed && spawnWindowFinished)
                runtime.SpawnFinished = 1;
        }

        if (runtime.Completed != 0)
            return;

        if (runtime.SpawnFinished == 0)
            return;

        if (runtime.AliveCount > 0)
            return;

        runtime.Completed = 1;
        runtime.CompletionTime = math.max(runtime.SpawnEndTime, elapsedTime);
    }

    /// <summary>
    /// Writes one wave runtime element after any spawn-side structural changes have completed.
    /// entityManager: Entity manager used to reacquire the runtime buffer.
    /// spawnerEntity: Spawner that owns the runtime buffer.
    /// waveIndex: Wave index to update.
    /// runtime: Runtime data to store.
    /// returns None.
    /// </summary>
    private static void SetWaveRuntime(EntityManager entityManager,
                                       Entity spawnerEntity,
                                       int waveIndex,
                                       EnemySpawnerWaveRuntimeElement runtime)
    {
        DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntime = entityManager.GetBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);

        if (waveIndex >= 0 && waveIndex < waveRuntime.Length)
            waveRuntime[waveIndex] = runtime;
    }

    /// <summary>
    /// Writes one staged wave event after enemy reservation or activation has potentially changed archetypes.
    /// entityManager: Entity manager used to reacquire the event buffer.
    /// spawnerEntity: Spawner that owns the event buffer.
    /// eventIndex: Event index to update.
    /// waveEvent: Event data to store.
    /// returns None.
    /// </summary>
    private static void SetWaveEvent(EntityManager entityManager,
                                     Entity spawnerEntity,
                                     int eventIndex,
                                     EnemySpawnerWaveEventElement waveEvent)
    {
        DynamicBuffer<EnemySpawnerWaveEventElement> waveEvents = entityManager.GetBuffer<EnemySpawnerWaveEventElement>(spawnerEntity);

        if (eventIndex >= 0 && eventIndex < waveEvents.Length)
            waveEvents[eventIndex] = waveEvent;
    }

    /// <summary>
    /// Resolves the prerequisite reference time that drives scheduling for a non-first wave.
    /// spawnerState: Global mutable spawner state.
    /// previousWaveDefinition: Immutable definition of the previous wave.
    /// previousWaveRuntime: Mutable runtime state of the previous wave.
    /// startMode: Requested start mode for the current wave.
    /// referenceTime: Resolved reference time when the prerequisite is satisfied.
    /// returns True when the prerequisite is satisfied and the reference time is valid, otherwise false.
    /// </summary>
    private static bool TryResolveReferenceTime(EnemySpawnerState spawnerState,
                                                EnemySpawnerWaveDefinitionElement previousWaveDefinition,
                                                EnemySpawnerWaveRuntimeElement previousWaveRuntime,
                                                EnemyWaveStartMode startMode,
                                                out float referenceTime)
    {
        switch (startMode)
        {
            case EnemyWaveStartMode.FromSpawnerStart:
                referenceTime = spawnerState.StartTime;
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveStart:
                if (previousWaveRuntime.Started == 0)
                    break;

                referenceTime = previousWaveRuntime.SpawnStartTime;
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveSpawnEnd:
                if (previousWaveRuntime.Started == 0)
                    break;

                referenceTime = previousWaveRuntime.SpawnStartTime + math.max(0f, previousWaveDefinition.SpawnDurationSeconds);
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveCompleted:
                if (previousWaveRuntime.Completed == 0)
                    break;

                referenceTime = previousWaveRuntime.CompletionTime;
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveFirstKill:
                if (previousWaveRuntime.FirstKillRegistered == 0)
                    break;

                referenceTime = previousWaveRuntime.FirstKillTime;
                return true;
        }

        referenceTime = 0f;
        return false;
    }

    /// <summary>
    /// Resolves the concrete pool entity associated with the provided prefab.
    /// poolMap: Prefab-to-pool map buffer stored on the spawner.
    /// prefabEntity: Referenced prefab to resolve.
    /// poolEntity: Resolved pool entity when found.
    /// returns True when the mapping exists, otherwise false.
    /// </summary>
    private static bool TryGetPoolEntity(DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap,
                                         Entity prefabEntity,
                                         out Entity poolEntity)
    {
        for (int mapIndex = 0; mapIndex < poolMap.Length; mapIndex++)
        {
            EnemySpawnerPrefabPoolMapElement mapEntry = poolMap[mapIndex];

            if (mapEntry.PrefabEntity != prefabEntity)
                continue;

            poolEntity = mapEntry.PoolEntity;
            return true;
        }

        poolEntity = Entity.Null;
        return false;
    }

    /// <summary>
    /// Acquires one pooled enemy instance from the requested pool, expanding it on demand.
    /// entityManager: Entity manager used to access the pool entity.
    /// poolEntity: Concrete pool that should provide an enemy instance.
    /// spawnerEntity: Spawner that owns the pool.
    /// prefabEntity: Prefab associated with the pool.
    /// enemyEntity: Resolved pooled enemy instance when acquisition succeeds.
    /// returns True when an instance was acquired, otherwise false.
    /// </summary>
    private static bool TryAcquireEnemy(EntityManager entityManager,
                                        Entity poolEntity,
                                        Entity spawnerEntity,
                                        Entity prefabEntity,
                                        out Entity enemyEntity)
    {
        enemyEntity = Entity.Null;

        if (!entityManager.Exists(poolEntity))
            return false;

        if (!entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
            return false;

        DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);

        if (poolBuffer.Length <= 0)
        {
            if (!entityManager.HasComponent<EnemyPoolState>(poolEntity))
                return false;

            EnemyPoolState poolState = entityManager.GetComponentData<EnemyPoolState>(poolEntity);
            int expandCount = math.max(1, poolState.ExpandBatch);
            EnemyPoolUtility.ExpandPool(entityManager,
                                        poolEntity,
                                        spawnerEntity,
                                        prefabEntity,
                                        expandCount);
            poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);
        }

        while (poolBuffer.Length > 0)
        {
            int lastPoolIndex = poolBuffer.Length - 1;
            enemyEntity = poolBuffer[lastPoolIndex].EnemyEntity;
            poolBuffer.RemoveAt(lastPoolIndex);

            if (entityManager.Exists(enemyEntity))
                return true;
        }

        enemyEntity = Entity.Null;
        return false;
    }

    /// <summary>
    /// Builds the warning payload stored on a reserved enemy and later consumed by presentation.
    /// entityManager: Entity manager used to inspect prefab data for body-aware sizing.
    /// prefabEntity: Enemy prefab baked on the staged event.
    /// spawnTime: Absolute world time when the reserved enemy becomes active.
    /// warningConfig: Immutable warning tuning baked from spawner authoring.
    /// returns Resolved warning payload for one reserved enemy.
    /// </summary>
    private static EnemySpawnWarningState CreateWarningState(EntityManager entityManager,
                                                            Entity prefabEntity,
                                                            float3 worldPosition,
                                                            float spawnTime,
                                                            EnemySpawnWarningConfig warningConfig)
    {
        bool warningEnabled = warningConfig.Enabled != 0;
        float leadTimeSeconds = EnemySpawnWarningConfigUtility.ResolveEffectiveLeadTimeSeconds(in warningConfig);
        float fadeOutSeconds = warningEnabled
            ? math.max(0f, warningConfig.FadeOutSeconds)
            : 0f;

        return new EnemySpawnWarningState
        {
            WorldPosition = worldPosition,
            WarningStartTime = spawnTime - leadTimeSeconds,
            SpawnTime = spawnTime,
            FadeOutEndTime = spawnTime + fadeOutSeconds,
            LeadTimeSeconds = leadTimeSeconds,
            FadeOutSeconds = fadeOutSeconds,
            Radius = warningEnabled
                ? ResolveWarningRadius(entityManager, prefabEntity, warningConfig)
                : 0f,
            RingWidth = warningEnabled
                ? math.max(0.01f, warningConfig.RingWidth)
                : 0f,
            HeightOffset = warningEnabled
                ? math.max(0f, warningConfig.HeightOffset)
                : 0f,
            MaximumAlpha = warningEnabled
                ? math.saturate(warningConfig.MaximumAlpha)
                : 0f,
            Color = warningConfig.Color
        };
    }

    /// <summary>
    /// Resolves a compact body-aware warning radius so each enemy keeps its own readable telegraph.
    /// entityManager: Entity manager used to inspect the baked prefab data.
    /// prefabEntity: Enemy prefab baked on the staged event.
    /// warningConfig: Immutable warning tuning baked from spawner authoring.
    /// returns Resolved world-space warning radius for the reserved enemy.
    /// </summary>
    private static float ResolveWarningRadius(EntityManager entityManager,
                                              Entity prefabEntity,
                                              EnemySpawnWarningConfig warningConfig)
    {
        float configuredRadius = math.max(MinimumWarningRadius,
                                          warningConfig.CellSize * math.max(0.01f, warningConfig.RadiusScale));

        if (!entityManager.Exists(prefabEntity) || !entityManager.HasComponent<EnemyData>(prefabEntity))
            return configuredRadius;

        float bodyRadius = math.max(0.05f, entityManager.GetComponentData<EnemyData>(prefabEntity).BodyRadius);
        float bodyAwareMinimumRadius = bodyRadius + math.max(0.02f, warningConfig.RingWidth * 0.5f);
        float bodyAwareMaximumRadius = math.max(bodyAwareMinimumRadius,
                                                bodyRadius * (1f + math.max(0.01f, warningConfig.RadiusScale) * 0.35f));
        return math.max(bodyAwareMinimumRadius, math.min(configuredRadius, bodyAwareMaximumRadius));
    }

    /// <summary>
    /// Resolves the absolute wave start time used by staged events before and after the wave starts.
    /// /params runtime Mutable runtime state of the processed wave.
    /// /params definition Immutable wave definition carrying the maximum event warning lead time.
    /// /returns Scheduled start time before activation, otherwise the effective spawn start time.
    /// </summary>
    private static float ResolveWaveActualSpawnStartTime(EnemySpawnerWaveRuntimeElement runtime,
                                                         EnemySpawnerWaveDefinitionElement definition)
    {
        if (runtime.Started != 0)
            return runtime.SpawnStartTime;

        return runtime.ScheduledStartTime + math.max(0f, definition.MaximumSpawnWarningLeadTimeSeconds);
    }
    #endregion

    #endregion
}
