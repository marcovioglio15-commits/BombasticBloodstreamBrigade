using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bakes EnemySpawnerAuthoring data into finite wave buffers and prefab-specific pool requirements.
/// </summary>
public sealed class EnemySpawnerAuthoringBaker : Baker<EnemySpawnerAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Converts the authored wave grid into ECS wave definitions, events and pool requirements.
    /// </summary>
    /// <param name="authoring">Spawner authoring source component.</param>
    public override void Bake(EnemySpawnerAuthoring authoring)
    {
        if (authoring == null)
            return;

        DependsOn(authoring.WavePreset);
#if UNITY_EDITOR && NASHCORE_RUNTIME_SPAWNER_TOOL
        EnemySpawnerRuntimeCatalog runtimeCatalog = EnemySpawnerRuntimeBakeMetadataUtility.ResolveRuntimeCatalogAsset();

        if (runtimeCatalog != null)
            DependsOn(runtimeCatalog);
#endif

        Entity spawnerEntity = GetEntity(TransformUsageFlags.Dynamic);
        List<EnemySpawnerWaveDefinitionElement> stagedWaveDefinitions = new List<EnemySpawnerWaveDefinitionElement>();
        List<EnemySpawnerWaveRuntimeElement> stagedWaveRuntime = new List<EnemySpawnerWaveRuntimeElement>();
        List<EnemySpawnerWaveEventElement> stagedWaveEvents = new List<EnemySpawnerWaveEventElement>();
        Dictionary<Entity, int> plannedCountByPrefab = new Dictionary<Entity, int>();
        EnemySpawnWarningConfig spawnerWarningConfig = EnemySpawnerWaveBakePresentationUtility.BuildSpawnerWarningConfig(authoring);
        bool runtimeEnabledByDefault = authoring.RuntimeEnabledByDefault;

        StageWaves(authoring,
                   authoring.Waves,
                   authoring.WavePreset != null ? authoring.WavePreset.WavesPreset : null,
                   spawnerWarningConfig,
                   stagedWaveDefinitions,
                   stagedWaveRuntime,
                   stagedWaveEvents,
                   plannedCountByPrefab);

        AddComponent(spawnerEntity, new EnemySpawner
        {
            InitialPoolCapacityPerPrefab = math.max(0, authoring.InitialPoolCapacityPerPrefab),
            ExpandBatchPerPrefab = math.max(1, authoring.ExpandBatchPerPrefab),
            DespawnDistance = math.max(0f, authoring.DespawnDistance),
            MaximumSpawnDistanceFromCenter = ResolveMaximumSpawnDistanceFromCenter(stagedWaveEvents, authoring.CellSize),
            TotalPlannedEnemyCount = CountTotalPlannedEnemies(plannedCountByPrefab)
        });
#if UNITY_EDITOR && NASHCORE_RUNTIME_SPAWNER_TOOL
        AddComponent(spawnerEntity, new EnemySpawnerRuntimeIdentity
        {
            SceneGuid = new FixedString64Bytes(EnemySpawnerRuntimeBakeMetadataUtility.ResolveAuthoringSceneGuid(authoring)),
            SpawnerGuid = new FixedString128Bytes(EnemySpawnerRuntimeBakeMetadataUtility.ResolveAuthoringSpawnerGuid(authoring)),
            DisplayName = new FixedString128Bytes(authoring.name),
            DefaultWavePresetGuid = new FixedString64Bytes(EnemySpawnerRuntimeBakeMetadataUtility.ResolveAssetGuid(authoring.WavePreset)),
            DefaultEnabled = runtimeEnabledByDefault ? (byte)1 : (byte)0
        });
        AddComponent(spawnerEntity, new EnemySpawnerRuntimeOverrideState
        {
            AppliedStoreVersion = 0u,
            AppliedWavePresetGuid = new FixedString64Bytes(EnemySpawnerRuntimeBakeMetadataUtility.ResolveAssetGuid(authoring.WavePreset)),
            FailedStoreVersion = 0u,
            FailedWavePresetGuid = default,
            AppliedEnabled = runtimeEnabledByDefault ? (byte)1 : (byte)0
        });
#endif

        if (!runtimeEnabledByDefault)
            AddComponent<Disabled>(spawnerEntity);

        AddComponent(spawnerEntity, spawnerWarningConfig);
        AddComponent(spawnerEntity, new EnemySpawnerState
        {
            StartTime = 0f,
            AliveCount = 0,
            Initialized = 0,
            StartTimeInitialized = 0
        });

        DynamicBuffer<EnemySpawnerWaveDefinitionElement> waveDefinitionBuffer = AddBuffer<EnemySpawnerWaveDefinitionElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntimeBuffer = AddBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWaveEventElement> waveEventBuffer = AddBuffer<EnemySpawnerWaveEventElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerPrefabRequirementElement> prefabRequirementBuffer = AddBuffer<EnemySpawnerPrefabRequirementElement>(spawnerEntity);
        AddBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);
#if UNITY_EDITOR && NASHCORE_RUNTIME_SPAWNER_TOOL
        DynamicBuffer<EnemySpawnerWavePresetVariantElement> variantBuffer = AddBuffer<EnemySpawnerWavePresetVariantElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWavePresetVariantDefinitionElement> variantDefinitionBuffer = AddBuffer<EnemySpawnerWavePresetVariantDefinitionElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWavePresetVariantEventElement> variantEventBuffer = AddBuffer<EnemySpawnerWavePresetVariantEventElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWavePresetVariantRequirementElement> variantRequirementBuffer = AddBuffer<EnemySpawnerWavePresetVariantRequirementElement>(spawnerEntity);
#endif

        for (int definitionIndex = 0; definitionIndex < stagedWaveDefinitions.Count; definitionIndex++)
            waveDefinitionBuffer.Add(stagedWaveDefinitions[definitionIndex]);

        for (int runtimeIndex = 0; runtimeIndex < stagedWaveRuntime.Count; runtimeIndex++)
            waveRuntimeBuffer.Add(stagedWaveRuntime[runtimeIndex]);

        for (int eventIndex = 0; eventIndex < stagedWaveEvents.Count; eventIndex++)
            waveEventBuffer.Add(stagedWaveEvents[eventIndex]);

        foreach (KeyValuePair<Entity, int> pair in plannedCountByPrefab)
        {
            prefabRequirementBuffer.Add(new EnemySpawnerPrefabRequirementElement
            {
                PrefabEntity = pair.Key,
                TotalPlannedCount = pair.Value
            });
        }

#if UNITY_EDITOR && NASHCORE_RUNTIME_SPAWNER_TOOL
        BakeRuntimeWavePresetVariants(authoring,
                                      spawnerWarningConfig,
                                      variantBuffer,
                                      variantDefinitionBuffer,
                                      variantEventBuffer,
                                      variantRequirementBuffer);
#endif
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Stages wave definitions, runtime defaults and exact spawn events from the authored spawner data.
    /// </summary>
    /// <param name="authoring">Spawner authoring source.</param>
    /// <param name="waves">Wave list being converted for this spawner.</param>
    /// <param name="wavesPreset">Brush category library referenced by the current wave preset.</param>
    /// <param name="spawnerWarningConfig">Spawner-level fallback warning config used when enemy visuals do not override warning settings.</param>
    /// <param name="stagedWaveDefinitions">Target wave definition list.</param>
    /// <param name="stagedWaveRuntime">Target wave runtime default list.</param>
    /// <param name="stagedWaveEvents">Target exact spawn event list.</param>
    /// <param name="plannedCountByPrefab">Target prefab usage count map.</param>
    private void StageWaves(EnemySpawnerAuthoring authoring,
                            List<EnemySpawnWaveAuthoring> waves,
                            GameWavesPreset wavesPreset,
                            EnemySpawnWarningConfig spawnerWarningConfig,
                            List<EnemySpawnerWaveDefinitionElement> stagedWaveDefinitions,
                            List<EnemySpawnerWaveRuntimeElement> stagedWaveRuntime,
                            List<EnemySpawnerWaveEventElement> stagedWaveEvents,
                            Dictionary<Entity, int> plannedCountByPrefab)
    {
        if (waves == null)
            return;

        if (wavesPreset != null)
            DependsOn(wavesPreset);
        int nextCategorySelectionKey = 1;

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = waves[waveIndex];

            if (wave == null)
                continue;

            List<EnemySpawnerWaveEventElement> stagedEventsForWave = new List<EnemySpawnerWaveEventElement>();
            StageWaveCells(authoring,
                           wave,
                           waveIndex,
                           wavesPreset,
                           spawnerWarningConfig,
                           stagedEventsForWave,
                           plannedCountByPrefab,
                           ref nextCategorySelectionKey);
            EnemySpawnerWaveBakeUtility.SortWaveEvents(stagedEventsForWave);
            int firstEventIndex = stagedWaveEvents.Count;
            float maximumSpawnWarningLeadTimeSeconds =
                EnemySpawnerWaveBakePresentationUtility.ResolveMaximumWaveWarningLeadTime(stagedEventsForWave,
                                                                                            spawnerWarningConfig);

            for (int eventIndex = 0; eventIndex < stagedEventsForWave.Count; eventIndex++)
                stagedWaveEvents.Add(stagedEventsForWave[eventIndex]);

            int referenceWaveIndex = EnemySpawnerWaveSequenceBakeUtility.ResolveReferenceWaveIndex(waves, wave);
            int referenceSequenceStepIndex =
                EnemySpawnerWaveSequenceBakeUtility.ResolveReferenceSequenceStepIndex(waves, wave);
            string selectionGroupId = wave.UseDifficultySelection
                ? wave.DifficultySelectionGroupId
                : string.Empty;
            stagedWaveDefinitions.Add(new EnemySpawnerWaveDefinitionElement
            {
                SequenceStepIndex = wave.SequenceStepIndex,
                StartMode = referenceWaveIndex < 0 && referenceSequenceStepIndex < 0
                    ? EnemyWaveStartMode.FromSpawnerStart
                    : wave.StartMode,
                ReferenceWaveIndex = referenceWaveIndex,
                ReferenceSequenceStepIndex = referenceSequenceStepIndex,
                StartDelaySeconds = math.max(0f, wave.StartDelaySeconds),
                SpawnDurationSeconds = math.max(0f, wave.SpawnDurationSeconds),
                MaximumSpawnWarningLeadTimeSeconds = maximumSpawnWarningLeadTimeSeconds,
                FirstEventIndex = firstEventIndex,
                EventCount = stagedEventsForWave.Count,
                DifficultySelectionGroupId = new FixedString64Bytes(selectionGroupId ?? string.Empty),
                DifficultyCoefficientId = new FixedString64Bytes(wave.DifficultyCoefficientId ?? string.Empty),
                MinimumDifficulty = wave.MinimumDifficulty,
                MaximumDifficulty = wave.MaximumDifficulty,
                SelectionWeight = math.max(0f, wave.SelectionWeight)
            });
            stagedWaveRuntime.Add(EnemySpawnerWaveBakePresentationUtility.CreateDefaultWaveRuntime());
        }
    }

    /// <summary>
    /// Stages exact spawn events for all painted cells of one wave.
    /// </summary>
    /// <param name="authoring">Spawner authoring source.</param>
    /// <param name="wave">Wave being converted.</param>
    /// <param name="waveIndex">Current wave index.</param>
    /// <param name="wavesPreset">Brush category library used to resolve category-painted cells.</param>
    /// <param name="spawnerWarningConfig">Spawner-level fallback warning config used for non-overridden events.</param>
    /// <param name="stagedEventsForWave">Target event list for the current wave.</param>
    /// <param name="plannedCountByPrefab">Target prefab usage count map.</param>
    /// <param name="nextCategorySelectionKey">Monotonic logical-spawn selection key shared by all waves.</param>
    private void StageWaveCells(EnemySpawnerAuthoring authoring,
                                EnemySpawnWaveAuthoring wave,
                                int waveIndex,
                                GameWavesPreset wavesPreset,
                                EnemySpawnWarningConfig spawnerWarningConfig,
                                List<EnemySpawnerWaveEventElement> stagedEventsForWave,
                                Dictionary<Entity, int> plannedCountByPrefab,
                                ref int nextCategorySelectionKey)
    {
        if (wave.PaintedCells == null)
            return;

        for (int cellIndex = 0; cellIndex < wave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = wave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            if (!EnemySpawnerWaveBakeUtility.IsCellInsideGrid(cell.CellCoordinate,
                                                              authoring.GridSizeX,
                                                              authoring.GridSizeZ))
            {
                continue;
            }

            int enemyCount = math.max(0, cell.EnemyCount);

            if (enemyCount <= 0)
                continue;

            if (wavesPreset != null &&
                wavesPreset.TryFindBrushCategory(cell.BrushCategoryId, out EnemyBrushCategoryDefinition category))
            {
                StageCategoryCell(authoring,
                                  wave,
                                  waveIndex,
                                  cell,
                                  category,
                                  enemyCount,
                                  spawnerWarningConfig,
                                  stagedEventsForWave,
                                  plannedCountByPrefab,
                                  ref nextCategorySelectionKey);
            }
        }
    }

    /// <summary>
    /// Stages one category-painted cell as deterministic weighted candidate events sharing logical spawn keys.
    /// </summary>
    /// <param name="authoring">Spawner authoring source used to resolve local positions and prefab entities.</param>
    /// <param name="wave">Wave owning the painted cell.</param>
    /// <param name="waveIndex">Owning wave index.</param>
    /// <param name="cell">Category-painted cell being flattened.</param>
    /// <param name="category">Resolved reusable brush category.</param>
    /// <param name="enemyCount">Logical enemy count emitted by the painted cell.</param>
    /// <param name="spawnerWarningConfig">Spawner-level warning fallback.</param>
    /// <param name="stagedEventsForWave">Mutable wave event output.</param>
    /// <param name="plannedCountByPrefab">Mutable pool requirement count map.</param>
    /// <param name="nextCategorySelectionKey">Monotonic logical-spawn selection key.</param>
    private void StageCategoryCell(EnemySpawnerAuthoring authoring,
                                   EnemySpawnWaveAuthoring wave,
                                   int waveIndex,
                                   EnemySpawnWaveCellAuthoring cell,
                                   EnemyBrushCategoryDefinition category,
                                   int enemyCount,
                                   EnemySpawnWarningConfig spawnerWarningConfig,
                                   List<EnemySpawnerWaveEventElement> stagedEventsForWave,
                                   Dictionary<Entity, int> plannedCountByPrefab,
                                   ref int nextCategorySelectionKey)
    {
        if (category.Entries == null || category.Entries.Count == 0)
            return;

        int firstLogicalSelectionKey = nextCategorySelectionKey;
        AnimationCurve distributionCurve = cell.UseWaveDefaultDistribution
            ? wave.DefaultDistributionCurve
            : cell.DistributionCurveOverride;

        for (int entryIndex = 0; entryIndex < category.Entries.Count; entryIndex++)
        {
            EnemyBrushCategoryEntry entry = category.Entries[entryIndex];

            if (entry == null || entry.MasterPreset == null)
                continue;

            DependsOn(entry.MasterPreset);

            if (!TryResolveCellPrefab(authoring, entry.MasterPreset, out Entity prefabEntity))
                continue;

            EnemyVisualSpawnOverridesSettings spawnOverrides =
                EnemySpawnerWaveBakePresentationUtility.ResolveSpawnOverrides(entry.MasterPreset);
            float3 localSpawnPosition = authoring.ResolveCellLocalCenter(cell.CellCoordinate) +
                                        EnemySpawnerWaveBakePresentationUtility.ResolveSpawnOffset(spawnOverrides);
            int firstInsertedEventIndex = stagedEventsForWave.Count;
            EnemySpawnerWaveBakeUtility.BuildCellEvents(waveIndex,
                                                        prefabEntity,
                                                        wave.SpawnDurationSeconds,
                                                        localSpawnPosition,
                                                        authoring.CellSize,
                                                        cell.CellCoordinate,
                                                        enemyCount,
                                                        distributionCurve,
                                                        stagedEventsForWave);
            EnemySpawnerWaveBakePresentationUtility.ApplySpawnWarningOverrides(stagedEventsForWave,
                                                                                firstInsertedEventIndex,
                                                                                spawnOverrides,
                                                                                spawnerWarningConfig);
            ApplyCategorySelectionMetadata(stagedEventsForWave,
                                           firstInsertedEventIndex,
                                           firstLogicalSelectionKey,
                                           category.DifficultyCoefficientId,
                                           entry);
            AddPlannedCount(plannedCountByPrefab, prefabEntity, enemyCount);
        }

        nextCategorySelectionKey += enemyCount;
    }

    /// <summary>
    /// Writes shared weighted category metadata onto all events inserted for one candidate enemy preset.
    /// </summary>
    /// <param name="events">Mutable staged wave event list.</param>
    /// <param name="firstEventIndex">First event inserted for this category candidate.</param>
    /// <param name="firstSelectionKey">Logical key assigned to the first inserted spawn.</param>
    /// <param name="coefficientId">Difficulty coefficient used by the category.</param>
    /// <param name="entry">Candidate entry supplying range and weight.</param>
    private static void ApplyCategorySelectionMetadata(List<EnemySpawnerWaveEventElement> events,
                                                       int firstEventIndex,
                                                       int firstSelectionKey,
                                                       string coefficientId,
                                                       EnemyBrushCategoryEntry entry)
    {
        for (int eventIndex = firstEventIndex; eventIndex < events.Count; eventIndex++)
        {
            EnemySpawnerWaveEventElement waveEvent = events[eventIndex];
            waveEvent.CategorySelectionKey = firstSelectionKey + eventIndex - firstEventIndex;
            waveEvent.DifficultyCoefficientId = new FixedString64Bytes(coefficientId ?? string.Empty);
            waveEvent.MinimumDifficulty = entry.MinimumDifficulty;
            waveEvent.MaximumDifficulty = entry.MaximumDifficulty;
            waveEvent.SelectionWeight = math.max(0f, entry.SelectionWeight);
            waveEvent.CategorySelectionState = 0;
            events[eventIndex] = waveEvent;
        }
    }

    /// <summary>
    /// Adds one prefab's candidate usage to the shared pool requirement map.
    /// </summary>
    /// <param name="plannedCountByPrefab">Mutable prefab requirement map.</param>
    /// <param name="prefabEntity">Resolved enemy prefab entity.</param>
    /// <param name="enemyCount">Candidate event count contributed by this entry.</param>
    private static void AddPlannedCount(Dictionary<Entity, int> plannedCountByPrefab,
                                        Entity prefabEntity,
                                        int enemyCount)
    {
        if (plannedCountByPrefab.TryGetValue(prefabEntity, out int plannedCount))
            plannedCountByPrefab[prefabEntity] = plannedCount + enemyCount;
        else
            plannedCountByPrefab[prefabEntity] = enemyCount;
    }

#if UNITY_EDITOR && NASHCORE_RUNTIME_SPAWNER_TOOL
    /// <summary>
    /// Bakes every project wave preset as a selectable runtime variant for one spawner.
    /// </summary>
    /// <param name="authoring">Spawner authoring source.</param>
    /// <param name="spawnerWarningConfig">Spawner-level fallback warning config.</param>
    /// <param name="variantBuffer">Target variant slice table.</param>
    /// <param name="variantDefinitionBuffer">Target flattened definition storage.</param>
    /// <param name="variantEventBuffer">Target flattened event storage.</param>
    /// <param name="variantRequirementBuffer">Target flattened prefab requirement storage.</param>
    private void BakeRuntimeWavePresetVariants(EnemySpawnerAuthoring authoring,
                                               EnemySpawnWarningConfig spawnerWarningConfig,
                                               DynamicBuffer<EnemySpawnerWavePresetVariantElement> variantBuffer,
                                               DynamicBuffer<EnemySpawnerWavePresetVariantDefinitionElement> variantDefinitionBuffer,
                                               DynamicBuffer<EnemySpawnerWavePresetVariantEventElement> variantEventBuffer,
                                               DynamicBuffer<EnemySpawnerWavePresetVariantRequirementElement> variantRequirementBuffer)
    {
        List<EnemyWavePreset> candidatePresets = EnemySpawnerRuntimeBakeMetadataUtility.CollectRuntimeWavePresetCandidates(authoring.WavePreset);

        for (int presetIndex = 0; presetIndex < candidatePresets.Count; presetIndex++)
        {
            EnemyWavePreset preset = candidatePresets[presetIndex];

            if (preset == null)
                continue;

            DependsOn(preset);
            StageRuntimeWavePresetVariant(authoring,
                                          preset,
                                          spawnerWarningConfig,
                                          variantBuffer,
                                          variantDefinitionBuffer,
                                          variantEventBuffer,
                                          variantRequirementBuffer);
        }
    }

    /// <summary>
    /// Stages one pre-baked wave-preset variant into flattened runtime override buffers.
    /// </summary>
    /// <param name="authoring">Spawner authoring source.</param>
    /// <param name="preset">EnemyWavePreset being converted for runtime selection.</param>
    /// <param name="spawnerWarningConfig">Spawner-level fallback warning config.</param>
    /// <param name="variantBuffer">Target variant slice table.</param>
    /// <param name="variantDefinitionBuffer">Target flattened definition storage.</param>
    /// <param name="variantEventBuffer">Target flattened event storage.</param>
    /// <param name="variantRequirementBuffer">Target flattened prefab requirement storage.</param>
    private void StageRuntimeWavePresetVariant(EnemySpawnerAuthoring authoring,
                                               EnemyWavePreset preset,
                                               EnemySpawnWarningConfig spawnerWarningConfig,
                                               DynamicBuffer<EnemySpawnerWavePresetVariantElement> variantBuffer,
                                               DynamicBuffer<EnemySpawnerWavePresetVariantDefinitionElement> variantDefinitionBuffer,
                                               DynamicBuffer<EnemySpawnerWavePresetVariantEventElement> variantEventBuffer,
                                               DynamicBuffer<EnemySpawnerWavePresetVariantRequirementElement> variantRequirementBuffer)
    {
        List<EnemySpawnerWaveDefinitionElement> stagedWaveDefinitions = new List<EnemySpawnerWaveDefinitionElement>();
        List<EnemySpawnerWaveRuntimeElement> stagedWaveRuntime = new List<EnemySpawnerWaveRuntimeElement>();
        List<EnemySpawnerWaveEventElement> stagedWaveEvents = new List<EnemySpawnerWaveEventElement>();
        Dictionary<Entity, int> plannedCountByPrefab = new Dictionary<Entity, int>();
        FixedString64Bytes wavePresetGuid = new FixedString64Bytes(EnemySpawnerRuntimeBakeMetadataUtility.ResolveAssetGuid(preset));
        int firstDefinitionIndex = variantDefinitionBuffer.Length;
        int firstEventIndex = variantEventBuffer.Length;
        int firstRequirementIndex = variantRequirementBuffer.Length;

        StageWaves(authoring,
                   preset.Waves,
                   preset.WavesPreset,
                   spawnerWarningConfig,
                   stagedWaveDefinitions,
                   stagedWaveRuntime,
                   stagedWaveEvents,
                   plannedCountByPrefab);

        // Store wave definitions with event indices relative to this variant slice.
        for (int definitionIndex = 0; definitionIndex < stagedWaveDefinitions.Count; definitionIndex++)
        {
            EnemySpawnerWaveDefinitionElement definition = stagedWaveDefinitions[definitionIndex];
            variantDefinitionBuffer.Add(new EnemySpawnerWavePresetVariantDefinitionElement
            {
                SequenceStepIndex = definition.SequenceStepIndex,
                StartMode = definition.StartMode,
                ReferenceWaveIndex = definition.ReferenceWaveIndex,
                ReferenceSequenceStepIndex = definition.ReferenceSequenceStepIndex,
                StartDelaySeconds = definition.StartDelaySeconds,
                SpawnDurationSeconds = definition.SpawnDurationSeconds,
                MaximumSpawnWarningLeadTimeSeconds = definition.MaximumSpawnWarningLeadTimeSeconds,
                FirstEventIndex = definition.FirstEventIndex,
                EventCount = definition.EventCount,
                DifficultySelectionGroupId = definition.DifficultySelectionGroupId,
                DifficultyCoefficientId = definition.DifficultyCoefficientId,
                MinimumDifficulty = definition.MinimumDifficulty,
                MaximumDifficulty = definition.MaximumDifficulty,
                SelectionWeight = definition.SelectionWeight
            });
        }

        // Store exact events with runtime reservation state reset.
        for (int eventIndex = 0; eventIndex < stagedWaveEvents.Count; eventIndex++)
        {
            EnemySpawnerWaveEventElement waveEvent = stagedWaveEvents[eventIndex];
            variantEventBuffer.Add(new EnemySpawnerWavePresetVariantEventElement
            {
                WaveIndex = waveEvent.WaveIndex,
                RelativeTime = waveEvent.RelativeTime,
                LocalSpawnPosition = waveEvent.LocalSpawnPosition,
                PrefabEntity = waveEvent.PrefabEntity,
                HasSpawnWarningOverride = waveEvent.HasSpawnWarningOverride,
                SpawnWarningOverride = waveEvent.SpawnWarningOverride,
                CategorySelectionKey = waveEvent.CategorySelectionKey,
                DifficultyCoefficientId = waveEvent.DifficultyCoefficientId,
                MinimumDifficulty = waveEvent.MinimumDifficulty,
                MaximumDifficulty = waveEvent.MaximumDifficulty,
                SelectionWeight = waveEvent.SelectionWeight
            });
        }

        // Store prefab requirements used by pool initialization after override application.
        foreach (KeyValuePair<Entity, int> pair in plannedCountByPrefab)
        {
            variantRequirementBuffer.Add(new EnemySpawnerWavePresetVariantRequirementElement
            {
                WavePresetGuid = wavePresetGuid,
                PrefabEntity = pair.Key,
                TotalPlannedCount = pair.Value
            });
        }

        variantBuffer.Add(new EnemySpawnerWavePresetVariantElement
        {
            WavePresetGuid = wavePresetGuid,
            FirstDefinitionIndex = firstDefinitionIndex,
            DefinitionCount = stagedWaveDefinitions.Count,
            FirstEventIndex = firstEventIndex,
            EventCount = stagedWaveEvents.Count,
            FirstRequirementIndex = firstRequirementIndex,
            RequirementCount = plannedCountByPrefab.Count,
            MaximumSpawnDistanceFromCenter = ResolveMaximumSpawnDistanceFromCenter(stagedWaveEvents, authoring.CellSize),
            TotalPlannedEnemyCount = CountTotalPlannedEnemies(plannedCountByPrefab)
        });
    }
#endif

    /// <summary>
    /// Resolves the prefab entity used by one painted cell through its master and visual presets.
    /// </summary>
    /// <param name="authoring">Spawner authoring component used only for warning context.</param>
    /// <param name="masterPreset">Enemy master preset painted on the cell.</param>
    /// <param name="prefabEntity">Resolved prefab entity when successful.</param>
    /// <returns>True when the cell references a valid enemy prefab, otherwise false.</returns>
    private bool TryResolveCellPrefab(EnemySpawnerAuthoring authoring,
                                      EnemyMasterPreset masterPreset,
                                      out Entity prefabEntity)
    {
        prefabEntity = Entity.Null;

        if (masterPreset == null)
            return false;

        EnemyVisualPreset visualPreset = masterPreset.VisualPreset;
        DependsOn(visualPreset);

        GameObject enemyPrefab = EnemySpawnerWaveBakeUtility.ResolveEnemyPrefab(masterPreset);

        if (enemyPrefab == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[EnemySpawnerAuthoringBaker] Master preset '{0}' does not resolve an enemy prefab through its visual preset. The painted cell will be ignored.",
                                           masterPreset.name),
                             authoring);
#endif
            return false;
        }

        DependsOn(enemyPrefab);

        if (enemyPrefab.scene.IsValid())
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[EnemySpawnerAuthoringBaker] Enemy prefab '{0}' referenced by master preset '{1}' is a scene object. Assign a prefab asset instead.",
                                           enemyPrefab.name,
                                           masterPreset.name),
                             authoring);
#endif
            return false;
        }

        if (enemyPrefab.GetComponentInChildren<EnemyAuthoring>(true) == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[EnemySpawnerAuthoringBaker] Enemy prefab '{0}' referenced by master preset '{1}' does not contain EnemyAuthoring in hierarchy.",
                                           enemyPrefab.name,
                                           masterPreset.name),
                             authoring);
#endif
            return false;
        }

        prefabEntity = GetEntity(enemyPrefab, TransformUsageFlags.Dynamic);
        return prefabEntity != Entity.Null;
    }

    /// <summary>
    /// Counts the total planned enemies across all unique prefab requirements.
    /// </summary>
    /// <param name="plannedCountByPrefab">Prefab usage count map.</param>
    /// <returns>Total planned enemy count for the spawner.</returns>
    private static int CountTotalPlannedEnemies(Dictionary<Entity, int> plannedCountByPrefab)
    {
        int totalPlannedEnemies = 0;

        foreach (KeyValuePair<Entity, int> pair in plannedCountByPrefab)
            totalPlannedEnemies += math.max(0, pair.Value);

        return totalPlannedEnemies;
    }

    /// <summary>
    /// Resolves the maximum planar spawn distance authored by the staged wave events.
    /// The returned radius includes half a cell diagonal so the full painted cell area stays inside the envelope.
    /// </summary>
    /// <param name="stagedWaveEvents">Fully staged exact spawn events of the spawner.</param>
    /// <param name="cellSize">Authored square cell size used by the spawn grid.</param>
    /// <returns>Maximum planar spawn distance from the spawner center.</returns>
    private static float ResolveMaximumSpawnDistanceFromCenter(List<EnemySpawnerWaveEventElement> stagedWaveEvents, float cellSize)
    {
        if (stagedWaveEvents == null || stagedWaveEvents.Count == 0)
            return 0f;

        const float HalfCellDiagonalFactor = 0.70710677f;
        float cellEnvelopePadding = math.max(0f, cellSize) * HalfCellDiagonalFactor;
        float maximumDistance = 0f;

        for (int eventIndex = 0; eventIndex < stagedWaveEvents.Count; eventIndex++)
        {
            float3 localSpawnPosition = stagedWaveEvents[eventIndex].LocalSpawnPosition;
            float planarDistance = math.length(localSpawnPosition.xz) + cellEnvelopePadding;

            if (planarDistance > maximumDistance)
                maximumDistance = planarDistance;
        }

        return maximumDistance;
    }
    #endregion

    #endregion
}
