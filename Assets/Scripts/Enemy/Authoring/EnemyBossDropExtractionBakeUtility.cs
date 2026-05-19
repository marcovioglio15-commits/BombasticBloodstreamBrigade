using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Compiles boss death drop extraction candidates from common Drop Items modules.
/// </summary>
internal static class EnemyBossDropExtractionBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles boss drop candidates and a union pattern used for pool sizing before death-time selection.
    /// </summary>
    /// <param name="sharedPreset">Source shared preset containing Drop Items module definitions.</param>
    /// <param name="settings">Authored boss drop extraction settings.</param>
    /// <param name="prefabResolver">Callback that converts a drop prefab GameObject to an entity prefab.</param>
    /// <param name="result">Mutable boss compile result.</param>
    public static void Compile(EnemyModulesAndPatternsPreset sharedPreset,
                               EnemyBossDropExtractionSettings settings,
                               System.Func<GameObject, Entity> prefabResolver,
                               EnemyCompiledBossPatternBakeResult result)
    {
        if (sharedPreset == null || settings == null || result == null || !settings.Enabled)
            return;

        result.BossDropExtractionEnabled = true;
        result.BossDropExtractionMode = settings.ExtractionMode;
        IReadOnlyList<EnemyBossDropCandidateDefinition> candidates = settings.Candidates;

        if (candidates == null)
            return;

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            EnemyBossDropCandidateDefinition candidate = candidates[candidateIndex];

            if (candidate == null || !candidate.Enabled)
                continue;

            EnemyCompiledPatternBakeResult compiledCandidate = CompileDropCandidate(sharedPreset, candidate.DropItems);
            int firstExperienceModuleIndex = result.BossDropExperienceModules.Count;
            int firstExtraComboPointsModuleIndex = result.BossDropExtraComboPointsModules.Count;
            int firstRecoveryModuleIndex = result.BossDropRecoveryModules.Count;
            AppendCompiledDropSource(compiledCandidate, prefabResolver, result);
            AppendCompiledDropUnion(compiledCandidate, result.BossDropUnionPattern);
            result.DropCandidates.Add(new EnemyBossDropCandidateElement
            {
                CandidateIndex = math.max(0, candidateIndex),
                Enabled = 1,
                SelectionWeight = EnemyBossPatternBakeUtility.ResolveSelectionWeight(candidate.SelectionWeight),
                FirstExperienceModuleIndex = firstExperienceModuleIndex,
                ExperienceModuleCount = result.BossDropExperienceModules.Count - firstExperienceModuleIndex,
                FirstExtraComboPointsModuleIndex = firstExtraComboPointsModuleIndex,
                ExtraComboPointsModuleCount = result.BossDropExtraComboPointsModules.Count - firstExtraComboPointsModuleIndex,
                FirstRecoveryModuleIndex = firstRecoveryModuleIndex,
                RecoveryModuleCount = result.BossDropRecoveryModules.Count - firstRecoveryModuleIndex,
                ModuleCombineMode = compiledCandidate.DropItemsConfig.ModuleCombineMode,
                MinimumSelectedModules = compiledCandidate.DropItemsConfig.MinimumSelectedModules,
                MaximumSelectedModules = compiledCandidate.DropItemsConfig.MaximumSelectedModules
            });
        }
    }

    /// <summary>
    /// Copies the compiled boss drop union into the initial pattern so existing pool sizing sees every possible boss drop prefab.
    /// </summary>
    /// <param name="result">Compiled boss pattern result.</param>
    public static void CopyBossDropUnionToInitialPattern(EnemyCompiledBossPatternBakeResult result)
    {
        if (result == null || result.InitialPattern == null || result.BossDropUnionPattern == null)
            return;

        AppendCompiledDropUnion(result.BossDropUnionPattern, result.InitialPattern);
    }
    #endregion

    #region Candidate Compile
    /// <summary>
    /// Compiles one boss drop candidate from a Drop Items assembly.
    /// </summary>
    /// <param name="sharedPreset">Source shared preset containing Drop Items modules.</param>
    /// <param name="dropItems">Authored Drop Items assembly.</param>
    /// <returns>Compiled candidate drop modules.</returns>
    private static EnemyCompiledPatternBakeResult CompileDropCandidate(EnemyModulesAndPatternsPreset sharedPreset,
                                                                       EnemyPatternDropItemsAssembly dropItems)
    {
        EnemyCompiledPatternBakeResult result = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);

        if (sharedPreset == null || dropItems == null || !dropItems.IsEnabled || dropItems.Modules == null)
            return result;

        result.DropItemsConfig.ModuleCombineMode = EnemyDropItemsBakeUtility.ResolveModuleCombineMode(dropItems.ModuleCombineMode);
        result.DropItemsConfig.MinimumSelectedModules = math.max(0, dropItems.MinimumSelectedModules);
        result.DropItemsConfig.MaximumSelectedModules = math.max(result.DropItemsConfig.MinimumSelectedModules,
                                                                 dropItems.MaximumSelectedModules);
        IReadOnlyList<EnemyPatternModuleBinding> moduleBindings = dropItems.Modules;

        for (int moduleIndex = 0; moduleIndex < moduleBindings.Count; moduleIndex++)
        {
            EnemyPatternModuleBinding binding = moduleBindings[moduleIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

            if (moduleDefinition == null)
                continue;

            if (EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind) != EnemyPatternModuleKind.DropItems)
                continue;

            EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);
            EnemyDropItemsBakeUtility.TryAppendModule(resolvedPayload, binding.SelectionWeight, ref result);
        }

        return result;
    }
    #endregion

    #region Source Conversion
    /// <summary>
    /// Converts one compiled candidate into boss-owned ECS source buffers used by death-time selection.
    /// </summary>
    /// <param name="compiledCandidate">Compiled candidate drop modules.</param>
    /// <param name="prefabResolver">Callback that converts a drop prefab GameObject to an entity prefab.</param>
    /// <param name="result">Mutable boss compile result.</param>
    private static void AppendCompiledDropSource(EnemyCompiledPatternBakeResult compiledCandidate,
                                                 System.Func<GameObject, Entity> prefabResolver,
                                                 EnemyCompiledBossPatternBakeResult result)
    {
        if (compiledCandidate == null || result == null)
            return;

        AppendExperienceSource(compiledCandidate, prefabResolver, result);
        AppendRecoverySource(compiledCandidate, prefabResolver, result);
        AppendExtraComboPointsSource(compiledCandidate, result);
    }

    /// <summary>
    /// Converts compiled experience drop modules into boss-owned source buffers.
    /// </summary>
    /// <param name="compiledCandidate">Compiled candidate drop modules.</param>
    /// <param name="prefabResolver">Callback that converts a drop prefab GameObject to an entity prefab.</param>
    /// <param name="result">Mutable boss compile result.</param>
    private static void AppendExperienceSource(EnemyCompiledPatternBakeResult compiledCandidate,
                                               System.Func<GameObject, Entity> prefabResolver,
                                               EnemyCompiledBossPatternBakeResult result)
    {
        for (int moduleIndex = 0; moduleIndex < compiledCandidate.ExperienceDropModules.Count; moduleIndex++)
        {
            EnemyCompiledExperienceDropModule compiledModule = compiledCandidate.ExperienceDropModules[moduleIndex];
            int sourceDefinitionStartIndex = math.max(0, compiledModule.DefinitionStartIndex);
            int sourceDefinitionEndIndex = math.min(compiledCandidate.ExperienceDropDefinitions.Count,
                                                    sourceDefinitionStartIndex + math.max(0, compiledModule.DefinitionCount));
            int targetDefinitionStartIndex = result.BossDropExperienceDefinitions.Count;

            for (int definitionIndex = sourceDefinitionStartIndex; definitionIndex < sourceDefinitionEndIndex; definitionIndex++)
            {
                EnemyCompiledExperienceDropDefinition definition = compiledCandidate.ExperienceDropDefinitions[definitionIndex];

                if (definition.Prefab == null || definition.ExperienceAmount <= 0f)
                    continue;

                Entity prefabEntity = prefabResolver != null
                    ? prefabResolver(definition.Prefab)
                    : Entity.Null;

                if (prefabEntity == Entity.Null)
                    continue;

                result.BossDropExperienceDefinitions.Add(new EnemyExperienceDropDefinitionElement
                {
                    PrefabEntity = prefabEntity,
                    ExperienceAmount = math.max(0f, definition.ExperienceAmount)
                });
            }

            int targetDefinitionCount = result.BossDropExperienceDefinitions.Count - targetDefinitionStartIndex;

            if (targetDefinitionCount <= 0)
                continue;

            result.BossDropExperienceModules.Add(new EnemyExperienceDropModuleElement
            {
                MinimumTotalExperienceDrop = math.max(0f, compiledModule.MinimumTotalExperienceDrop),
                MaximumTotalExperienceDrop = math.max(math.max(0f, compiledModule.MinimumTotalExperienceDrop), compiledModule.MaximumTotalExperienceDrop),
                Distribution = math.clamp(compiledModule.Distribution, 0f, 1f),
                DropRadius = math.max(0f, compiledModule.DropRadius),
                AttractionSpeed = math.max(0f, compiledModule.AttractionSpeed),
                CollectDistance = math.max(0.01f, compiledModule.CollectDistance),
                CollectDistancePerPlayerSpeed = math.max(0f, compiledModule.CollectDistancePerPlayerSpeed),
                SpawnAnimationMinDuration = math.max(0f, compiledModule.SpawnAnimationMinDuration),
                SpawnAnimationMaxDuration = math.max(math.max(0f, compiledModule.SpawnAnimationMinDuration), compiledModule.SpawnAnimationMaxDuration),
                DefinitionStartIndex = targetDefinitionStartIndex,
                DefinitionCount = targetDefinitionCount,
                EstimatedDropsPerDeath = math.max(0, compiledModule.EstimatedDropsPerDeath),
                SelectionWeight = math.max(0.0001f, compiledModule.SelectionWeight)
            });
        }
    }

    /// <summary>
    /// Converts compiled recovery drop modules into boss-owned source buffers.
    /// </summary>
    /// <param name="compiledCandidate">Compiled candidate drop modules.</param>
    /// <param name="prefabResolver">Callback that converts a drop prefab GameObject to an entity prefab.</param>
    /// <param name="result">Mutable boss compile result.</param>
    private static void AppendRecoverySource(EnemyCompiledPatternBakeResult compiledCandidate,
                                             System.Func<GameObject, Entity> prefabResolver,
                                             EnemyCompiledBossPatternBakeResult result)
    {
        for (int moduleIndex = 0; moduleIndex < compiledCandidate.RecoveryDropModules.Count; moduleIndex++)
        {
            EnemyCompiledRecoveryDropModule compiledModule = compiledCandidate.RecoveryDropModules[moduleIndex];
            int sourceDefinitionStartIndex = math.max(0, compiledModule.DefinitionStartIndex);
            int sourceDefinitionEndIndex = math.min(compiledCandidate.RecoveryDropDefinitions.Count,
                                                    sourceDefinitionStartIndex + math.max(0, compiledModule.DefinitionCount));
            int targetDefinitionStartIndex = result.BossDropRecoveryDefinitions.Count;

            for (int definitionIndex = sourceDefinitionStartIndex; definitionIndex < sourceDefinitionEndIndex; definitionIndex++)
            {
                EnemyCompiledRecoveryDropDefinition definition = compiledCandidate.RecoveryDropDefinitions[definitionIndex];

                if (definition.Prefab == null ||
                    (definition.HealthRestoreAmount <= 0f && definition.ShieldRestoreAmount <= 0f))
                {
                    continue;
                }

                Entity prefabEntity = prefabResolver != null
                    ? prefabResolver(definition.Prefab)
                    : Entity.Null;

                if (prefabEntity == Entity.Null)
                    continue;

                result.BossDropRecoveryDefinitions.Add(new EnemyRecoveryDropDefinitionElement
                {
                    PrefabEntity = prefabEntity,
                    HealthRestoreAmount = math.max(0f, definition.HealthRestoreAmount),
                    ShieldRestoreAmount = math.max(0f, definition.ShieldRestoreAmount)
                });
            }

            int targetDefinitionCount = result.BossDropRecoveryDefinitions.Count - targetDefinitionStartIndex;

            if (targetDefinitionCount <= 0)
                continue;

            result.BossDropRecoveryModules.Add(new EnemyRecoveryDropModuleElement
            {
                MinimumDropCount = math.max(0, compiledModule.MinimumDropCount),
                MaximumDropCount = math.max(math.max(0, compiledModule.MinimumDropCount), compiledModule.MaximumDropCount),
                Distribution = math.clamp(compiledModule.Distribution, 0f, 1f),
                DropRadius = math.max(0f, compiledModule.DropRadius),
                AttractionSpeed = math.max(0f, compiledModule.AttractionSpeed),
                CollectDistance = math.max(0.01f, compiledModule.CollectDistance),
                CollectDistancePerPlayerSpeed = math.max(0f, compiledModule.CollectDistancePerPlayerSpeed),
                SpawnAnimationMinDuration = math.max(0f, compiledModule.SpawnAnimationMinDuration),
                SpawnAnimationMaxDuration = math.max(math.max(0f, compiledModule.SpawnAnimationMinDuration), compiledModule.SpawnAnimationMaxDuration),
                DefinitionStartIndex = targetDefinitionStartIndex,
                DefinitionCount = targetDefinitionCount,
                EstimatedDropsPerDeath = math.max(0, compiledModule.EstimatedDropsPerDeath),
                SelectionWeight = math.max(0.0001f, compiledModule.SelectionWeight)
            });
        }
    }

    /// <summary>
    /// Converts compiled Extra Combo Points modules into boss-owned source buffers.
    /// </summary>
    /// <param name="compiledCandidate">Compiled candidate drop modules.</param>
    /// <param name="result">Mutable boss compile result.</param>
    private static void AppendExtraComboPointsSource(EnemyCompiledPatternBakeResult compiledCandidate,
                                                     EnemyCompiledBossPatternBakeResult result)
    {
        for (int moduleIndex = 0; moduleIndex < compiledCandidate.ExtraComboPointsModules.Count; moduleIndex++)
        {
            EnemyCompiledExtraComboPointsModule compiledModule = compiledCandidate.ExtraComboPointsModules[moduleIndex];
            int sourceConditionStartIndex = math.max(0, compiledModule.ConditionStartIndex);
            int sourceConditionEndIndex = math.min(compiledCandidate.ExtraComboPointsConditions.Count,
                                                   sourceConditionStartIndex + math.max(0, compiledModule.ConditionCount));
            int targetConditionStartIndex = result.BossDropExtraComboPointsConditions.Count;

            for (int conditionIndex = sourceConditionStartIndex; conditionIndex < sourceConditionEndIndex; conditionIndex++)
            {
                EnemyCompiledExtraComboPointsCondition condition = compiledCandidate.ExtraComboPointsConditions[conditionIndex];
                result.BossDropExtraComboPointsConditions.Add(new EnemyExtraComboPointsConditionElement
                {
                    Metric = condition.Metric,
                    MinimumValue = condition.MinimumValue,
                    UseMaximumValue = condition.UseMaximumValue,
                    MaximumValue = condition.MaximumValue,
                    MinimumMultiplier = condition.MinimumMultiplier,
                    MaximumMultiplier = condition.MaximumMultiplier,
                    NormalizedMultiplierCurveSamples = condition.NormalizedMultiplierCurveSamples
                });
            }

            result.BossDropExtraComboPointsModules.Add(new EnemyExtraComboPointsModuleElement
            {
                BaseMultiplier = compiledModule.BaseMultiplier,
                MinimumFinalMultiplier = compiledModule.MinimumFinalMultiplier,
                MaximumFinalMultiplier = compiledModule.MaximumFinalMultiplier,
                ConditionCombineMode = compiledModule.ConditionCombineMode,
                ConditionStartIndex = targetConditionStartIndex,
                ConditionCount = result.BossDropExtraComboPointsConditions.Count - targetConditionStartIndex,
                SelectionWeight = math.max(0.0001f, compiledModule.SelectionWeight)
            });
        }
    }
    #endregion

    #region Union Copy
    /// <summary>
    /// Appends one compiled candidate into another compiled result while fixing nested slice indices.
    /// </summary>
    /// <param name="source">Compiled source drop modules.</param>
    /// <param name="target">Compiled target result receiving modules.</param>
    private static void AppendCompiledDropUnion(EnemyCompiledPatternBakeResult source, EnemyCompiledPatternBakeResult target)
    {
        if (source == null || target == null)
            return;

        AppendExperienceUnion(source, target);
        AppendRecoveryUnion(source, target);
        AppendExtraComboPointsUnion(source, target);
    }

    /// <summary>
    /// Appends compiled experience modules into the union target.
    /// </summary>
    /// <param name="source">Compiled source drop modules.</param>
    /// <param name="target">Compiled target result receiving modules.</param>
    private static void AppendExperienceUnion(EnemyCompiledPatternBakeResult source, EnemyCompiledPatternBakeResult target)
    {
        for (int moduleIndex = 0; moduleIndex < source.ExperienceDropModules.Count; moduleIndex++)
        {
            EnemyCompiledExperienceDropModule sourceModule = source.ExperienceDropModules[moduleIndex];
            int sourceDefinitionStartIndex = math.max(0, sourceModule.DefinitionStartIndex);
            int sourceDefinitionEndIndex = math.min(source.ExperienceDropDefinitions.Count,
                                                    sourceDefinitionStartIndex + math.max(0, sourceModule.DefinitionCount));
            int targetDefinitionStartIndex = target.ExperienceDropDefinitions.Count;

            for (int definitionIndex = sourceDefinitionStartIndex; definitionIndex < sourceDefinitionEndIndex; definitionIndex++)
                target.ExperienceDropDefinitions.Add(source.ExperienceDropDefinitions[definitionIndex]);

            sourceModule.DefinitionStartIndex = targetDefinitionStartIndex;
            sourceModule.DefinitionCount = target.ExperienceDropDefinitions.Count - targetDefinitionStartIndex;
            target.ExperienceDropModules.Add(sourceModule);
            target.DropItemsConfig.HasExperienceDrops = 1;
            target.DropItemsConfig.ExperienceModuleCount = target.ExperienceDropModules.Count;
            target.DropItemsConfig.EstimatedDropsPerDeath = EnemyAuthoringValidationUtility.AddEstimatedCount(target.DropItemsConfig.EstimatedDropsPerDeath,
                                                                                                              sourceModule.EstimatedDropsPerDeath);
        }
    }

    /// <summary>
    /// Appends compiled recovery modules into the union target.
    /// </summary>
    /// <param name="source">Compiled source drop modules.</param>
    /// <param name="target">Compiled target result receiving modules.</param>
    private static void AppendRecoveryUnion(EnemyCompiledPatternBakeResult source, EnemyCompiledPatternBakeResult target)
    {
        for (int moduleIndex = 0; moduleIndex < source.RecoveryDropModules.Count; moduleIndex++)
        {
            EnemyCompiledRecoveryDropModule sourceModule = source.RecoveryDropModules[moduleIndex];
            int sourceDefinitionStartIndex = math.max(0, sourceModule.DefinitionStartIndex);
            int sourceDefinitionEndIndex = math.min(source.RecoveryDropDefinitions.Count,
                                                    sourceDefinitionStartIndex + math.max(0, sourceModule.DefinitionCount));
            int targetDefinitionStartIndex = target.RecoveryDropDefinitions.Count;

            for (int definitionIndex = sourceDefinitionStartIndex; definitionIndex < sourceDefinitionEndIndex; definitionIndex++)
                target.RecoveryDropDefinitions.Add(source.RecoveryDropDefinitions[definitionIndex]);

            sourceModule.DefinitionStartIndex = targetDefinitionStartIndex;
            sourceModule.DefinitionCount = target.RecoveryDropDefinitions.Count - targetDefinitionStartIndex;
            target.RecoveryDropModules.Add(sourceModule);
            target.DropItemsConfig.HasRecoveryDrops = 1;
            target.DropItemsConfig.RecoveryModuleCount = target.RecoveryDropModules.Count;
            target.DropItemsConfig.EstimatedDropsPerDeath = EnemyAuthoringValidationUtility.AddEstimatedCount(target.DropItemsConfig.EstimatedDropsPerDeath,
                                                                                                              sourceModule.EstimatedDropsPerDeath);
        }
    }

    /// <summary>
    /// Appends compiled Extra Combo Points modules into the union target.
    /// </summary>
    /// <param name="source">Compiled source drop modules.</param>
    /// <param name="target">Compiled target result receiving modules.</param>
    private static void AppendExtraComboPointsUnion(EnemyCompiledPatternBakeResult source, EnemyCompiledPatternBakeResult target)
    {
        for (int moduleIndex = 0; moduleIndex < source.ExtraComboPointsModules.Count; moduleIndex++)
        {
            EnemyCompiledExtraComboPointsModule sourceModule = source.ExtraComboPointsModules[moduleIndex];
            int sourceConditionStartIndex = math.max(0, sourceModule.ConditionStartIndex);
            int sourceConditionEndIndex = math.min(source.ExtraComboPointsConditions.Count,
                                                   sourceConditionStartIndex + math.max(0, sourceModule.ConditionCount));
            int targetConditionStartIndex = target.ExtraComboPointsConditions.Count;

            for (int conditionIndex = sourceConditionStartIndex; conditionIndex < sourceConditionEndIndex; conditionIndex++)
                target.ExtraComboPointsConditions.Add(source.ExtraComboPointsConditions[conditionIndex]);

            sourceModule.ConditionStartIndex = targetConditionStartIndex;
            sourceModule.ConditionCount = target.ExtraComboPointsConditions.Count - targetConditionStartIndex;
            target.ExtraComboPointsModules.Add(sourceModule);
            target.DropItemsConfig.HasExtraComboPoints = 1;
            target.DropItemsConfig.ExtraComboPointsModuleCount = target.ExtraComboPointsModules.Count;
        }
    }
    #endregion

    #endregion
}
