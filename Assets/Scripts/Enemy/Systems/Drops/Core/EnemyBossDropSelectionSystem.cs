using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Selects boss death drop candidates before killed events and standard drop spawning consume drop buffers.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyDespawnSystem))]
[UpdateBefore(typeof(EnemyKilledEventsSystem))]
public partial struct EnemyBossDropSelectionSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares boss drop extraction as the only required runtime dependency.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyBossDropExtractionConfig>();
    }

    /// <summary>
    /// Rebuilds standard drop buffers from selected boss drop candidates for killed bosses.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRO<EnemyBossDropExtractionConfig> extractionConfig,
                  RefRW<EnemyBossDropRuntimeState> runtimeState,
                  RefRO<EnemyDespawnRequest> despawnRequest,
                  RefRO<EnemyRuntimeState> enemyRuntime,
                  Entity bossEntity)
                 in SystemAPI.Query<RefRO<EnemyBossDropExtractionConfig>,
                                    RefRW<EnemyBossDropRuntimeState>,
                                    RefRO<EnemyDespawnRequest>,
                                    RefRO<EnemyRuntimeState>>()
                             .WithAll<EnemyBossTag>()
                             .WithEntityAccess())
        {
            if (extractionConfig.ValueRO.Enabled == 0 ||
                runtimeState.ValueRO.SelectionResolved != 0 ||
                despawnRequest.ValueRO.Reason != EnemyDespawnReason.Killed)
            {
                continue;
            }

            DynamicBuffer<EnemyBossDropCandidateElement> candidates = entityManager.GetBuffer<EnemyBossDropCandidateElement>(bossEntity);
            DynamicBuffer<EnemyBossSelectedDropCandidateElement> selectedCandidates = entityManager.GetBuffer<EnemyBossSelectedDropCandidateElement>(bossEntity);
            DynamicBuffer<EnemyBossDropExperienceModuleElement> sourceExperienceModules = entityManager.GetBuffer<EnemyBossDropExperienceModuleElement>(bossEntity);
            DynamicBuffer<EnemyBossDropExperienceDefinitionElement> sourceExperienceDefinitions = entityManager.GetBuffer<EnemyBossDropExperienceDefinitionElement>(bossEntity);
            DynamicBuffer<EnemyBossDropRecoveryModuleElement> sourceRecoveryModules = entityManager.GetBuffer<EnemyBossDropRecoveryModuleElement>(bossEntity);
            DynamicBuffer<EnemyBossDropRecoveryDefinitionElement> sourceRecoveryDefinitions = entityManager.GetBuffer<EnemyBossDropRecoveryDefinitionElement>(bossEntity);
            DynamicBuffer<EnemyBossDropExtraComboPointsModuleElement> sourceExtraComboPointsModules = entityManager.GetBuffer<EnemyBossDropExtraComboPointsModuleElement>(bossEntity);
            DynamicBuffer<EnemyBossDropExtraComboPointsConditionElement> sourceExtraComboPointsConditions = entityManager.GetBuffer<EnemyBossDropExtraComboPointsConditionElement>(bossEntity);
            DynamicBuffer<EnemyExperienceDropModuleElement> targetExperienceModules = ResolveWritableBuffer<EnemyExperienceDropModuleElement>(entityManager, commandBuffer, bossEntity);
            DynamicBuffer<EnemyExperienceDropDefinitionElement> targetExperienceDefinitions = ResolveWritableBuffer<EnemyExperienceDropDefinitionElement>(entityManager, commandBuffer, bossEntity);
            DynamicBuffer<EnemyRecoveryDropModuleElement> targetRecoveryModules = ResolveWritableBuffer<EnemyRecoveryDropModuleElement>(entityManager, commandBuffer, bossEntity);
            DynamicBuffer<EnemyRecoveryDropDefinitionElement> targetRecoveryDefinitions = ResolveWritableBuffer<EnemyRecoveryDropDefinitionElement>(entityManager, commandBuffer, bossEntity);
            DynamicBuffer<EnemyExtraComboPointsModuleElement> targetExtraComboPointsModules = ResolveWritableBuffer<EnemyExtraComboPointsModuleElement>(entityManager, commandBuffer, bossEntity);
            DynamicBuffer<EnemyExtraComboPointsConditionElement> targetExtraComboPointsConditions = ResolveWritableBuffer<EnemyExtraComboPointsConditionElement>(entityManager, commandBuffer, bossEntity);
            DynamicBuffer<EnemyDropItemsModuleSelectionElement> targetSelectionModules = ResolveWritableBuffer<EnemyDropItemsModuleSelectionElement>(entityManager, commandBuffer, bossEntity);
            EnemyDropItemsConfig dropItemsConfig = EnemyDropItemsBakeUtility.CreateDefaultConfig();

            selectedCandidates.Clear();
            targetExperienceModules.Clear();
            targetExperienceDefinitions.Clear();
            targetRecoveryModules.Clear();
            targetRecoveryDefinitions.Clear();
            targetExtraComboPointsModules.Clear();
            targetExtraComboPointsConditions.Clear();
            targetSelectionModules.Clear();
            SelectCandidates(candidates,
                             selectedCandidates,
                             extractionConfig.ValueRO.ExtractionMode,
                             bossEntity,
                             enemyRuntime.ValueRO);
            CopySelectedCandidates(candidates,
                                   selectedCandidates,
                                   sourceExperienceModules,
                                   sourceExperienceDefinitions,
                                   sourceRecoveryModules,
                                   sourceRecoveryDefinitions,
                                   sourceExtraComboPointsModules,
                                   sourceExtraComboPointsConditions,
                                   targetExperienceModules,
                                   targetExperienceDefinitions,
                                   targetRecoveryModules,
                                   targetRecoveryDefinitions,
                                   targetExtraComboPointsModules,
                                   targetExtraComboPointsConditions,
                                   targetSelectionModules,
                                   ref dropItemsConfig);
            FinalizeDropSelectionConfig(targetSelectionModules.Length, ref dropItemsConfig);
            ApplyDropItemsConfig(entityManager, commandBuffer, bossEntity, dropItemsConfig);

            EnemyBossDropRuntimeState resolvedState = runtimeState.ValueRO;
            resolvedState.SelectionResolved = 1;
            runtimeState.ValueRW = resolvedState;
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Selection
    /// <summary>
    /// Selects one or more candidate indices according to the configured extraction mode.
    /// </summary>
    /// <param name="candidates">Available boss drop candidates.</param>
    /// <param name="selectedCandidates">Output selected candidate buffer.</param>
    /// <param name="extractionMode">Configured death drop extraction mode.</param>
    /// <param name="bossEntity">Boss entity used to seed deterministic selection.</param>
    /// <param name="enemyRuntime">Runtime state used to vary the death-time random seed.</param>
    private static void SelectCandidates(DynamicBuffer<EnemyBossDropCandidateElement> candidates,
                                         DynamicBuffer<EnemyBossSelectedDropCandidateElement> selectedCandidates,
                                         EnemyBossDropExtractionMode extractionMode,
                                         Entity bossEntity,
                                         in EnemyRuntimeState enemyRuntime)
    {
        if (extractionMode == EnemyBossDropExtractionMode.SumAllCandidates)
        {
            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                if (IsSelectableCandidate(candidates[candidateIndex]))
                    selectedCandidates.Add(new EnemyBossSelectedDropCandidateElement { CandidateIndex = candidateIndex });
            }

            return;
        }

        int selectedCandidateIndex = ResolveWeightedCandidateIndex(candidates, bossEntity, in enemyRuntime);

        if (selectedCandidateIndex >= 0)
            selectedCandidates.Add(new EnemyBossSelectedDropCandidateElement { CandidateIndex = selectedCandidateIndex });
    }

    /// <summary>
    /// Resolves one weighted candidate index for Single Candidate extraction.
    /// </summary>
    /// <param name="candidates">Available boss drop candidates.</param>
    /// <param name="bossEntity">Boss entity used to seed deterministic selection.</param>
    /// <param name="enemyRuntime">Runtime state used to vary the death-time random seed.</param>
    /// <returns>Selected candidate buffer index, or -1 when no candidate can be selected.</returns>
    private static int ResolveWeightedCandidateIndex(DynamicBuffer<EnemyBossDropCandidateElement> candidates,
                                                     Entity bossEntity,
                                                     in EnemyRuntimeState enemyRuntime)
    {
        float totalWeight = 0f;

        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            EnemyBossDropCandidateElement candidate = candidates[candidateIndex];

            if (!IsSelectableCandidate(candidate))
                continue;

            totalWeight += math.max(0.0001f, candidate.SelectionWeight);
        }

        if (totalWeight <= 0f)
            return -1;

        Unity.Mathematics.Random random = new Unity.Mathematics.Random(ResolveSelectionSeed(bossEntity, in enemyRuntime, candidates.Length));
        float roll = random.NextFloat(0f, totalWeight);
        float cumulativeWeight = 0f;

        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            EnemyBossDropCandidateElement candidate = candidates[candidateIndex];

            if (!IsSelectableCandidate(candidate))
                continue;

            cumulativeWeight += math.max(0.0001f, candidate.SelectionWeight);

            if (roll <= cumulativeWeight)
                return candidateIndex;
        }

        return -1;
    }
    #endregion

    #region Buffer Copy
    /// <summary>
    /// Copies selected candidate source slices into the standard enemy drop buffers.
    /// </summary>
    /// <param name="candidates">Available boss drop candidates.</param>
    /// <param name="selectedCandidates">Selected candidate indices.</param>
    /// <param name="sourceExperienceModules">Boss-owned source experience modules.</param>
    /// <param name="sourceExperienceDefinitions">Boss-owned source experience definitions.</param>
    /// <param name="sourceRecoveryModules">Boss-owned source recovery modules.</param>
    /// <param name="sourceRecoveryDefinitions">Boss-owned source recovery definitions.</param>
    /// <param name="sourceExtraComboPointsModules">Boss-owned source Extra Combo Points modules.</param>
    /// <param name="sourceExtraComboPointsConditions">Boss-owned source Extra Combo Points conditions.</param>
    /// <param name="targetExperienceModules">Standard target experience modules.</param>
    /// <param name="targetExperienceDefinitions">Standard target experience definitions.</param>
    /// <param name="targetRecoveryModules">Standard target recovery modules.</param>
    /// <param name="targetRecoveryDefinitions">Standard target recovery definitions.</param>
    /// <param name="targetExtraComboPointsModules">Standard target Extra Combo Points modules.</param>
    /// <param name="targetExtraComboPointsConditions">Standard target Extra Combo Points conditions.</param>
    /// <param name="targetSelectionModules">Standard target Drop Items module-selection entries.</param>
    /// <param name="dropItemsConfig">Mutable standard drop summary config.</param>
    private static void CopySelectedCandidates(DynamicBuffer<EnemyBossDropCandidateElement> candidates,
                                               DynamicBuffer<EnemyBossSelectedDropCandidateElement> selectedCandidates,
                                               DynamicBuffer<EnemyBossDropExperienceModuleElement> sourceExperienceModules,
                                               DynamicBuffer<EnemyBossDropExperienceDefinitionElement> sourceExperienceDefinitions,
                                               DynamicBuffer<EnemyBossDropRecoveryModuleElement> sourceRecoveryModules,
                                               DynamicBuffer<EnemyBossDropRecoveryDefinitionElement> sourceRecoveryDefinitions,
                                               DynamicBuffer<EnemyBossDropExtraComboPointsModuleElement> sourceExtraComboPointsModules,
                                               DynamicBuffer<EnemyBossDropExtraComboPointsConditionElement> sourceExtraComboPointsConditions,
                                               DynamicBuffer<EnemyExperienceDropModuleElement> targetExperienceModules,
                                               DynamicBuffer<EnemyExperienceDropDefinitionElement> targetExperienceDefinitions,
                                               DynamicBuffer<EnemyRecoveryDropModuleElement> targetRecoveryModules,
                                               DynamicBuffer<EnemyRecoveryDropDefinitionElement> targetRecoveryDefinitions,
                                               DynamicBuffer<EnemyExtraComboPointsModuleElement> targetExtraComboPointsModules,
                                               DynamicBuffer<EnemyExtraComboPointsConditionElement> targetExtraComboPointsConditions,
                                               DynamicBuffer<EnemyDropItemsModuleSelectionElement> targetSelectionModules,
                                               ref EnemyDropItemsConfig dropItemsConfig)
    {
        ApplySelectedCandidateSelectionMode(candidates, selectedCandidates, ref dropItemsConfig);

        for (int selectedIndex = 0; selectedIndex < selectedCandidates.Length; selectedIndex++)
        {
            int candidateIndex = selectedCandidates[selectedIndex].CandidateIndex;

            if (candidateIndex < 0 || candidateIndex >= candidates.Length)
                continue;

            EnemyBossDropCandidateElement candidate = candidates[candidateIndex];
            CopyExperienceCandidate(candidate,
                                    sourceExperienceModules,
                                    sourceExperienceDefinitions,
                                    targetExperienceModules,
                                    targetExperienceDefinitions,
                                    targetSelectionModules,
                                    ref dropItemsConfig);
            CopyRecoveryCandidate(candidate,
                                  sourceRecoveryModules,
                                  sourceRecoveryDefinitions,
                                  targetRecoveryModules,
                                  targetRecoveryDefinitions,
                                  targetSelectionModules,
                                  ref dropItemsConfig);
            CopyExtraComboPointsCandidate(candidate,
                                          sourceExtraComboPointsModules,
                                          sourceExtraComboPointsConditions,
                                          targetExtraComboPointsModules,
                                          targetExtraComboPointsConditions,
                                          targetSelectionModules,
                                          ref dropItemsConfig);
        }
    }

    /// <summary>
    /// Copies one candidate's recovery module slice into the standard target buffers.
    /// </summary>
    /// <param name="candidate">Selected boss drop candidate.</param>
    /// <param name="sourceModules">Boss-owned source recovery modules.</param>
    /// <param name="sourceDefinitions">Boss-owned source recovery definitions.</param>
    /// <param name="targetModules">Standard target recovery modules.</param>
    /// <param name="targetDefinitions">Standard target recovery definitions.</param>
    /// <param name="targetSelectionModules">Standard target Drop Items module-selection entries.</param>
    /// <param name="dropItemsConfig">Mutable standard drop summary config.</param>
    private static void CopyRecoveryCandidate(EnemyBossDropCandidateElement candidate,
                                              DynamicBuffer<EnemyBossDropRecoveryModuleElement> sourceModules,
                                              DynamicBuffer<EnemyBossDropRecoveryDefinitionElement> sourceDefinitions,
                                              DynamicBuffer<EnemyRecoveryDropModuleElement> targetModules,
                                              DynamicBuffer<EnemyRecoveryDropDefinitionElement> targetDefinitions,
                                              DynamicBuffer<EnemyDropItemsModuleSelectionElement> targetSelectionModules,
                                              ref EnemyDropItemsConfig dropItemsConfig)
    {
        int firstModuleIndex = math.max(0, candidate.FirstRecoveryModuleIndex);
        int moduleEndIndex = math.min(sourceModules.Length, firstModuleIndex + math.max(0, candidate.RecoveryModuleCount));

        for (int moduleIndex = firstModuleIndex; moduleIndex < moduleEndIndex; moduleIndex++)
        {
            EnemyRecoveryDropModuleElement sourceModule = sourceModules[moduleIndex].Module;
            int sourceDefinitionStartIndex = math.max(0, sourceModule.DefinitionStartIndex);
            int sourceDefinitionEndIndex = math.min(sourceDefinitions.Length,
                                                    sourceDefinitionStartIndex + math.max(0, sourceModule.DefinitionCount));
            int targetDefinitionStartIndex = targetDefinitions.Length;

            for (int definitionIndex = sourceDefinitionStartIndex; definitionIndex < sourceDefinitionEndIndex; definitionIndex++)
                targetDefinitions.Add(sourceDefinitions[definitionIndex].Definition);

            sourceModule.DefinitionStartIndex = targetDefinitionStartIndex;
            sourceModule.DefinitionCount = targetDefinitions.Length - targetDefinitionStartIndex;

            if (sourceModule.DefinitionCount <= 0)
                continue;

            int targetModuleIndex = targetModules.Length;
            targetModules.Add(sourceModule);
            AddDropItemsSelectionModule(targetSelectionModules,
                                        EnemyDropItemsPayloadKind.Recovery,
                                        targetModuleIndex,
                                        sourceModule.SelectionWeight);
            dropItemsConfig.HasRecoveryDrops = 1;
            dropItemsConfig.RecoveryModuleCount = targetModules.Length;
            dropItemsConfig.EstimatedDropsPerDeath = EnemyAuthoringValidationUtility.AddEstimatedCount(dropItemsConfig.EstimatedDropsPerDeath,
                                                                                                       sourceModule.EstimatedDropsPerDeath);
        }
    }

    /// <summary>
    /// Copies one candidate's experience module slice into the standard target buffers.
    /// </summary>
    /// <param name="candidate">Selected boss drop candidate.</param>
    /// <param name="sourceModules">Boss-owned source experience modules.</param>
    /// <param name="sourceDefinitions">Boss-owned source experience definitions.</param>
    /// <param name="targetModules">Standard target experience modules.</param>
    /// <param name="targetDefinitions">Standard target experience definitions.</param>
    /// <param name="targetSelectionModules">Standard target Drop Items module-selection entries.</param>
    /// <param name="dropItemsConfig">Mutable standard drop summary config.</param>
    private static void CopyExperienceCandidate(EnemyBossDropCandidateElement candidate,
                                                DynamicBuffer<EnemyBossDropExperienceModuleElement> sourceModules,
                                                DynamicBuffer<EnemyBossDropExperienceDefinitionElement> sourceDefinitions,
                                                DynamicBuffer<EnemyExperienceDropModuleElement> targetModules,
                                                DynamicBuffer<EnemyExperienceDropDefinitionElement> targetDefinitions,
                                                DynamicBuffer<EnemyDropItemsModuleSelectionElement> targetSelectionModules,
                                                ref EnemyDropItemsConfig dropItemsConfig)
    {
        int firstModuleIndex = math.max(0, candidate.FirstExperienceModuleIndex);
        int moduleEndIndex = math.min(sourceModules.Length, firstModuleIndex + math.max(0, candidate.ExperienceModuleCount));

        for (int moduleIndex = firstModuleIndex; moduleIndex < moduleEndIndex; moduleIndex++)
        {
            EnemyExperienceDropModuleElement sourceModule = sourceModules[moduleIndex].Module;
            int sourceDefinitionStartIndex = math.max(0, sourceModule.DefinitionStartIndex);
            int sourceDefinitionEndIndex = math.min(sourceDefinitions.Length,
                                                    sourceDefinitionStartIndex + math.max(0, sourceModule.DefinitionCount));
            int targetDefinitionStartIndex = targetDefinitions.Length;

            for (int definitionIndex = sourceDefinitionStartIndex; definitionIndex < sourceDefinitionEndIndex; definitionIndex++)
                targetDefinitions.Add(sourceDefinitions[definitionIndex].Definition);

            sourceModule.DefinitionStartIndex = targetDefinitionStartIndex;
            sourceModule.DefinitionCount = targetDefinitions.Length - targetDefinitionStartIndex;

            if (sourceModule.DefinitionCount <= 0)
                continue;

            int targetModuleIndex = targetModules.Length;
            targetModules.Add(sourceModule);
            AddDropItemsSelectionModule(targetSelectionModules,
                                        EnemyDropItemsPayloadKind.Experience,
                                        targetModuleIndex,
                                        sourceModule.SelectionWeight);
            dropItemsConfig.HasExperienceDrops = 1;
            dropItemsConfig.ExperienceModuleCount = targetModules.Length;
            dropItemsConfig.EstimatedDropsPerDeath = EnemyAuthoringValidationUtility.AddEstimatedCount(dropItemsConfig.EstimatedDropsPerDeath,
                                                                                                       sourceModule.EstimatedDropsPerDeath);
        }
    }

    /// <summary>
    /// Copies one candidate's Extra Combo Points module slice into the standard target buffers.
    /// </summary>
    /// <param name="candidate">Selected boss drop candidate.</param>
    /// <param name="sourceModules">Boss-owned source Extra Combo Points modules.</param>
    /// <param name="sourceConditions">Boss-owned source Extra Combo Points conditions.</param>
    /// <param name="targetModules">Standard target Extra Combo Points modules.</param>
    /// <param name="targetConditions">Standard target Extra Combo Points conditions.</param>
    /// <param name="targetSelectionModules">Standard target Drop Items module-selection entries.</param>
    /// <param name="dropItemsConfig">Mutable standard drop summary config.</param>
    private static void CopyExtraComboPointsCandidate(EnemyBossDropCandidateElement candidate,
                                                      DynamicBuffer<EnemyBossDropExtraComboPointsModuleElement> sourceModules,
                                                      DynamicBuffer<EnemyBossDropExtraComboPointsConditionElement> sourceConditions,
                                                      DynamicBuffer<EnemyExtraComboPointsModuleElement> targetModules,
                                                      DynamicBuffer<EnemyExtraComboPointsConditionElement> targetConditions,
                                                      DynamicBuffer<EnemyDropItemsModuleSelectionElement> targetSelectionModules,
                                                      ref EnemyDropItemsConfig dropItemsConfig)
    {
        int firstModuleIndex = math.max(0, candidate.FirstExtraComboPointsModuleIndex);
        int moduleEndIndex = math.min(sourceModules.Length, firstModuleIndex + math.max(0, candidate.ExtraComboPointsModuleCount));

        for (int moduleIndex = firstModuleIndex; moduleIndex < moduleEndIndex; moduleIndex++)
        {
            EnemyExtraComboPointsModuleElement sourceModule = sourceModules[moduleIndex].Module;
            int sourceConditionStartIndex = math.max(0, sourceModule.ConditionStartIndex);
            int sourceConditionEndIndex = math.min(sourceConditions.Length,
                                                   sourceConditionStartIndex + math.max(0, sourceModule.ConditionCount));
            int targetConditionStartIndex = targetConditions.Length;

            for (int conditionIndex = sourceConditionStartIndex; conditionIndex < sourceConditionEndIndex; conditionIndex++)
                targetConditions.Add(sourceConditions[conditionIndex].Condition);

            sourceModule.ConditionStartIndex = targetConditionStartIndex;
            sourceModule.ConditionCount = targetConditions.Length - targetConditionStartIndex;
            int targetModuleIndex = targetModules.Length;
            targetModules.Add(sourceModule);
            AddDropItemsSelectionModule(targetSelectionModules,
                                        EnemyDropItemsPayloadKind.ExtraComboPoints,
                                        targetModuleIndex,
                                        sourceModule.SelectionWeight);
            dropItemsConfig.HasExtraComboPoints = 1;
            dropItemsConfig.ExtraComboPointsModuleCount = targetModules.Length;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Applies per-candidate Drop Items module selection only when one candidate owns the final drop set.
    /// </summary>
    /// <param name="candidates">Available boss drop candidates.</param>
    /// <param name="selectedCandidates">Selected candidate indices.</param>
    /// <param name="dropItemsConfig">Mutable standard drop summary config.</param>
    private static void ApplySelectedCandidateSelectionMode(DynamicBuffer<EnemyBossDropCandidateElement> candidates,
                                                           DynamicBuffer<EnemyBossSelectedDropCandidateElement> selectedCandidates,
                                                           ref EnemyDropItemsConfig dropItemsConfig)
    {
        if (selectedCandidates.Length != 1)
            return;

        int candidateIndex = selectedCandidates[0].CandidateIndex;

        if (candidateIndex < 0 || candidateIndex >= candidates.Length)
            return;

        EnemyBossDropCandidateElement candidate = candidates[candidateIndex];
        dropItemsConfig.ModuleCombineMode = EnemyDropItemsBakeUtility.ResolveModuleCombineMode(candidate.ModuleCombineMode);
        dropItemsConfig.MinimumSelectedModules = math.max(0, candidate.MinimumSelectedModules);
        dropItemsConfig.MaximumSelectedModules = math.max(dropItemsConfig.MinimumSelectedModules,
                                                          candidate.MaximumSelectedModules);
    }

    /// <summary>
    /// Appends one standard Drop Items module-selection entry while copying selected boss drop modules.
    /// </summary>
    /// <param name="selectionModules">Target selection buffer receiving the entry.</param>
    /// <param name="payloadKind">Runtime payload kind owned by the copied module.</param>
    /// <param name="moduleIndex">Type-local module index inside the matching target payload buffer.</param>
    /// <param name="selectionWeight">Relative module-selection weight copied from the source module.</param>
    private static void AddDropItemsSelectionModule(DynamicBuffer<EnemyDropItemsModuleSelectionElement> selectionModules,
                                                    EnemyDropItemsPayloadKind payloadKind,
                                                    int moduleIndex,
                                                    float selectionWeight)
    {
        selectionModules.Add(new EnemyDropItemsModuleSelectionElement
        {
            PayloadKind = payloadKind,
            ModuleIndex = math.max(0, moduleIndex),
            SelectionWeight = math.max(0.0001f, selectionWeight)
        });
    }

    /// <summary>
    /// Finalizes selected boss drop module-selection counts after target buffers have been rebuilt.
    /// </summary>
    /// <param name="selectionModuleCount">Amount of copied module-selection entries.</param>
    /// <param name="dropItemsConfig">Mutable standard drop summary config.</param>
    private static void FinalizeDropSelectionConfig(int selectionModuleCount, ref EnemyDropItemsConfig dropItemsConfig)
    {
        int sanitizedSelectionModuleCount = math.max(0, selectionModuleCount);
        dropItemsConfig.SelectionModuleCount = sanitizedSelectionModuleCount;

        if (dropItemsConfig.ModuleCombineMode == EnemyDropItemsModuleCombineMode.AllModules)
        {
            dropItemsConfig.MinimumSelectedModules = sanitizedSelectionModuleCount;
            dropItemsConfig.MaximumSelectedModules = sanitizedSelectionModuleCount;
            return;
        }

        if (dropItemsConfig.ModuleCombineMode == EnemyDropItemsModuleCombineMode.SingleWeightedModule)
        {
            int selectedModuleCount = sanitizedSelectionModuleCount > 0 ? 1 : 0;
            dropItemsConfig.MinimumSelectedModules = selectedModuleCount;
            dropItemsConfig.MaximumSelectedModules = selectedModuleCount;
            return;
        }

        dropItemsConfig.MinimumSelectedModules = math.clamp(dropItemsConfig.MinimumSelectedModules,
                                                            0,
                                                            sanitizedSelectionModuleCount);
        dropItemsConfig.MaximumSelectedModules = math.clamp(math.max(dropItemsConfig.MinimumSelectedModules,
                                                                     dropItemsConfig.MaximumSelectedModules),
                                                            0,
                                                            sanitizedSelectionModuleCount);
    }

    /// <summary>
    /// Resolves one writable dynamic buffer, deferring structural creation until the entity query has finished.
    /// </summary>
    /// <param name="entityManager">Entity manager used to resolve or add the buffer.</param>
    /// <param name="commandBuffer">Command buffer used when the buffer is missing on legacy baked entities.</param>
    /// <param name="entity">Entity that owns the buffer.</param>
    /// <returns>Resolved dynamic buffer.</returns>
    private static DynamicBuffer<T> ResolveWritableBuffer<T>(EntityManager entityManager,
                                                             EntityCommandBuffer commandBuffer,
                                                             Entity entity) where T : unmanaged, IBufferElementData
    {
        if (entityManager.HasBuffer<T>(entity))
            return entityManager.GetBuffer<T>(entity);

        return commandBuffer.AddBuffer<T>(entity);
    }

    /// <summary>
    /// Applies the selected drop summary config to the boss entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to set or add the component.</param>
    /// <param name="commandBuffer">Command buffer used when the config is missing on legacy baked entities.</param>
    /// <param name="bossEntity">Boss entity receiving the config.</param>
    /// <param name="dropItemsConfig">Selected drop summary config.</param>
    private static void ApplyDropItemsConfig(EntityManager entityManager,
                                             EntityCommandBuffer commandBuffer,
                                             Entity bossEntity,
                                             EnemyDropItemsConfig dropItemsConfig)
    {
        if (entityManager.HasComponent<EnemyDropItemsConfig>(bossEntity))
        {
            entityManager.SetComponentData(bossEntity, dropItemsConfig);
            return;
        }

        commandBuffer.AddComponent(bossEntity, dropItemsConfig);
    }

    /// <summary>
    /// Resolves whether one candidate has at least one source module to apply.
    /// </summary>
    /// <param name="candidate">Candidate to inspect.</param>
    /// <returns>True when the candidate can affect boss drops.</returns>
    private static bool IsSelectableCandidate(EnemyBossDropCandidateElement candidate)
    {
        if (candidate.Enabled == 0)
            return false;

        return candidate.ExperienceModuleCount > 0 ||
               candidate.ExtraComboPointsModuleCount > 0 ||
               candidate.RecoveryModuleCount > 0;
    }

    /// <summary>
    /// Builds a deterministic non-zero random seed for boss drop selection.
    /// </summary>
    /// <param name="bossEntity">Boss entity used to seed selection.</param>
    /// <param name="enemyRuntime">Runtime state used to vary the death-time seed.</param>
    /// <param name="candidateCount">Current candidate count.</param>
    /// <returns>Non-zero random seed.</returns>
    private static uint ResolveSelectionSeed(Entity bossEntity, in EnemyRuntimeState enemyRuntime, int candidateCount)
    {
        uint seed = math.hash(new int4(bossEntity.Index,
                                       bossEntity.Version,
                                       (int)math.round(enemyRuntime.LifetimeSeconds * 1000f),
                                       math.max(1, candidateCount)));

        if (seed == 0u)
            return 1u;

        return seed;
    }
    #endregion

    #endregion
}
