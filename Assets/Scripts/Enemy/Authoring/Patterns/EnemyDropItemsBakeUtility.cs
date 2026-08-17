using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Compiles Drop Items payloads into ECS-friendly module buffers without coupling them to one single payload kind.
/// </summary>
internal static class EnemyDropItemsBakeUtility
{
    #region Constants
    private const int NormalizedMultiplierCurveSampleCount = 12;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the default summary config used before any Drop Items module is compiled.
    /// </summary>
    /// <returns>Default drop-items summary config.</returns>
    public static EnemyDropItemsConfig CreateDefaultConfig()
    {
        return new EnemyDropItemsConfig
        {
            ModuleCombineMode = EnemyDropItemsModuleCombineMode.AllModules,
            HasExperienceDrops = 0,
            HasExtraComboPoints = 0,
            HasRecoveryDrops = 0,
            ExperienceModuleCount = 0,
            ExtraComboPointsModuleCount = 0,
            RecoveryModuleCount = 0,
            SelectionModuleCount = 0,
            MinimumSelectedModules = 0,
            MaximumSelectedModules = 0,
            EstimatedDropsPerDeath = 0
        };
    }

    /// <summary>
    /// Appends one compiled Drop Items payload to the aggregated bake result.
    /// </summary>
    /// <param name="payload">Effective payload block resolved from a module definition or binding override.</param>
    /// <param name="result">Mutable compiled result receiving module data.</param>
    /// <returns>True when a valid Drop Items module was appended.</returns>
    public static bool TryAppendModule(EnemyPatternModulePayloadData payload,
                                       ref EnemyCompiledPatternBakeResult result)
    {
        return TryAppendModule(payload, 1f, ref result);
    }

    /// <summary>
    /// Appends one compiled Drop Items payload with weighted module-selection metadata.
    /// </summary>
    /// <param name="payload">Effective payload block resolved from a module definition or binding override.</param>
    /// <param name="selectionWeight">Relative weight used by weighted module combine modes.</param>
    /// <param name="result">Mutable compiled result receiving module data.</param>
    /// <returns>True when a valid Drop Items module was appended.</returns>
    public static bool TryAppendModule(EnemyPatternModulePayloadData payload,
                                       float selectionWeight,
                                       ref EnemyCompiledPatternBakeResult result)
    {
        if (payload == null || payload.DropItems == null || result == null)
            return false;

        EnemyDropItemsPayloadKind payloadKind = ResolveDropItemsPayloadKind(payload.DropItems.DropPayloadKind);
        int previousModuleCount = ResolveModuleCount(payloadKind, result);

        switch (payloadKind)
        {
            case EnemyDropItemsPayloadKind.ExtraComboPoints:
                TryAppendExtraComboPointsModule(payload.DropItems.ExtraComboPoints, ref result);
                break;

            case EnemyDropItemsPayloadKind.Recovery:
                TryAppendRecoveryModule(payload.DropItems.Recovery, ref result);
                break;

            default:
                TryAppendExperienceModule(payload.DropItems.Experience, ref result);
                break;
        }

        int currentModuleCount = ResolveModuleCount(payloadKind, result);

        if (currentModuleCount <= previousModuleCount)
            return false;

        ApplyModuleSelectionWeight(payloadKind, currentModuleCount - 1, selectionWeight, ref result);
        return true;
    }

    /// <summary>
    /// Resolves one valid drop-items payload kind authored in the editor.
    /// </summary>
    /// <param name="payloadKind">Authored payload kind candidate.</param>
    /// <returns>Sanitized payload kind used by bake.</returns>
    public static EnemyDropItemsPayloadKind ResolveDropItemsPayloadKind(EnemyDropItemsPayloadKind payloadKind)
    {
        switch (payloadKind)
        {
            case EnemyDropItemsPayloadKind.Experience:
            case EnemyDropItemsPayloadKind.ExtraComboPoints:
            case EnemyDropItemsPayloadKind.Recovery:
                return payloadKind;

            default:
                return EnemyDropItemsPayloadKind.Experience;
        }
    }

    /// <summary>
    /// Resolves one valid Extra Combo Points metric authored in the editor.
    /// </summary>
    /// <param name="metric">Authored metric candidate.</param>
    /// <returns>Sanitized metric used by bake/runtime.</returns>
    public static EnemyExtraComboPointsMetric ResolveMetric(EnemyExtraComboPointsMetric metric)
    {
        switch (metric)
        {
            case EnemyExtraComboPointsMetric.LifetimeSinceSpawnSeconds:
            case EnemyExtraComboPointsMetric.TimeSinceFirstDamageSeconds:
            case EnemyExtraComboPointsMetric.TimeSinceLastDamageSeconds:
            case EnemyExtraComboPointsMetric.DamageWindowSeconds:
            case EnemyExtraComboPointsMetric.SpawnToFirstDamageSeconds:
                return metric;

            default:
                return EnemyExtraComboPointsMetric.LifetimeSinceSpawnSeconds;
        }
    }

    /// <summary>
    /// Resolves one valid Extra Combo Points condition-combine mode authored in the editor.
    /// </summary>
    /// <param name="combineMode">Authored combine mode candidate.</param>
    /// <returns>Sanitized combine mode used by bake/runtime.</returns>
    public static EnemyExtraComboPointsConditionCombineMode ResolveConditionCombineMode(EnemyExtraComboPointsConditionCombineMode combineMode)
    {
        switch (combineMode)
        {
            case EnemyExtraComboPointsConditionCombineMode.MultiplyMatchingConditions:
            case EnemyExtraComboPointsConditionCombineMode.HighestMatchingMultiplier:
            case EnemyExtraComboPointsConditionCombineMode.LowestMatchingMultiplier:
                return combineMode;

            default:
                return EnemyExtraComboPointsConditionCombineMode.MultiplyMatchingConditions;
        }
    }

    /// <summary>
    /// Resolves one valid Drop Items module combine mode authored in shared pattern assemblies.
    /// </summary>
    /// <param name="combineMode">Authored combine mode candidate.</param>
    /// <returns>Sanitized combine mode used by bake/runtime.</returns>
    public static EnemyDropItemsModuleCombineMode ResolveModuleCombineMode(EnemyDropItemsModuleCombineMode combineMode)
    {
        switch (combineMode)
        {
            case EnemyDropItemsModuleCombineMode.AllModules:
            case EnemyDropItemsModuleCombineMode.SingleWeightedModule:
            case EnemyDropItemsModuleCombineMode.WeightedSubset:
                return combineMode;

            default:
                return EnemyDropItemsModuleCombineMode.AllModules;
        }
    }

    /// <summary>
    /// Resolves the vertical drop spawn offset while preserving authored signed values and rejecting non-finite input.
    /// </summary>
    /// <param name="groundHeightOffset">Authored vertical offset in world units.</param>
    /// <returns>Authored offset when finite, otherwise the shared default offset.</returns>
    public static float ResolveGroundHeightOffset(float groundHeightOffset)
    {
        if (!float.IsNaN(groundHeightOffset) && !float.IsInfinity(groundHeightOffset))
            return groundHeightOffset;

        return EnemyDropItemsSpawnSettingsDefaults.DefaultGroundHeightOffset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Returns the current compiled module count for one Drop Items payload kind.
    /// </summary>
    /// <param name="payloadKind">Payload kind whose module list is inspected.</param>
    /// <param name="result">Compiled result owning the module lists.</param>
    /// <returns>Current module count for the requested payload kind.</returns>
    private static int ResolveModuleCount(EnemyDropItemsPayloadKind payloadKind, EnemyCompiledPatternBakeResult result)
    {
        if (result == null)
            return 0;

        switch (payloadKind)
        {
            case EnemyDropItemsPayloadKind.ExtraComboPoints:
                return result.ExtraComboPointsModules.Count;

            case EnemyDropItemsPayloadKind.Recovery:
                return result.RecoveryDropModules.Count;

            default:
                return result.ExperienceDropModules.Count;
        }
    }

    /// <summary>
    /// Writes the weighted-selection value onto the just-appended compiled module.
    /// </summary>
    /// <param name="payloadKind">Payload kind whose module list receives metadata.</param>
    /// <param name="moduleIndex">Type-local module index to update.</param>
    /// <param name="selectionWeight">Authored relative module-selection weight.</param>
    /// <param name="result">Compiled result owning the module lists.</param>
    private static void ApplyModuleSelectionWeight(EnemyDropItemsPayloadKind payloadKind,
                                                   int moduleIndex,
                                                   float selectionWeight,
                                                   ref EnemyCompiledPatternBakeResult result)
    {
        float resolvedSelectionWeight = math.max(0.0001f, selectionWeight);

        switch (payloadKind)
        {
            case EnemyDropItemsPayloadKind.ExtraComboPoints:
                EnemyCompiledExtraComboPointsModule extraComboPointsModule = result.ExtraComboPointsModules[moduleIndex];
                extraComboPointsModule.SelectionWeight = resolvedSelectionWeight;
                result.ExtraComboPointsModules[moduleIndex] = extraComboPointsModule;
                return;

            case EnemyDropItemsPayloadKind.Recovery:
                EnemyCompiledRecoveryDropModule recoveryModule = result.RecoveryDropModules[moduleIndex];
                recoveryModule.SelectionWeight = resolvedSelectionWeight;
                result.RecoveryDropModules[moduleIndex] = recoveryModule;
                return;

            default:
                EnemyCompiledExperienceDropModule experienceModule = result.ExperienceDropModules[moduleIndex];
                experienceModule.SelectionWeight = resolvedSelectionWeight;
                result.ExperienceDropModules[moduleIndex] = experienceModule;
                return;
        }
    }

    /// <summary>
    /// Appends one compiled experience-drop module to the bake result.
    /// </summary>
    /// <param name="experiencePayload">Experience payload block resolved from authoring.</param>
    /// <param name="result">Mutable compiled result receiving module data.</param>
    private static void TryAppendExperienceModule(EnemyExperienceDropPayload experiencePayload,
                                                  ref EnemyCompiledPatternBakeResult result)
    {
        if (experiencePayload == null)
            return;

        float minimumTotalExperienceDrop = math.max(0f, experiencePayload.ComplessiveExperienceDropMinimum);
        float maximumTotalExperienceDrop = math.max(minimumTotalExperienceDrop, experiencePayload.ComplessiveExperienceDropMaximum);

        if (maximumTotalExperienceDrop <= 0f)
            return;

        EnemyExperienceDropCollectionSettings collectionMovement = experiencePayload.CollectionMovement;
        EnemyCompiledExperienceDropModule compiledModule = new EnemyCompiledExperienceDropModule
        {
            MinimumTotalExperienceDrop = minimumTotalExperienceDrop,
            MaximumTotalExperienceDrop = maximumTotalExperienceDrop,
            Distribution = math.clamp(experiencePayload.DropsDistribution, 0f, 1f),
            DropRadius = math.max(0f, experiencePayload.DropRadius),
            GroundHeightOffset = ResolveGroundHeightOffset(experiencePayload.GroundHeightOffset),
            AttractionSpeed = collectionMovement != null ? math.max(0f, collectionMovement.MoveSpeed) : 0f,
            CollectDistance = collectionMovement != null ? math.max(0.01f, collectionMovement.CollectDistance) : 0.3f,
            CollectDistancePerPlayerSpeed = collectionMovement != null ? math.max(0f, collectionMovement.CollectDistancePerPlayerSpeed) : 0.05f,
            SpawnAnimationMinDuration = collectionMovement != null ? math.max(0f, collectionMovement.SpawnAnimationMinDuration) : 0.08f,
            SpawnAnimationMaxDuration = collectionMovement != null
                ? math.max(math.max(0f, collectionMovement.SpawnAnimationMinDuration), collectionMovement.SpawnAnimationMaxDuration)
                : 0.16f,
            DefinitionStartIndex = result.ExperienceDropDefinitions.Count,
            DefinitionCount = 0,
            EstimatedDropsPerDeath = 0,
            SelectionWeight = 1f
        };

        IReadOnlyList<EnemyExperienceDropDefinitionData> definitions = experiencePayload.DropDefinitions;
        List<float> previewValues = new List<float>();

        if (definitions != null)
        {
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                EnemyExperienceDropDefinitionData definition = definitions[definitionIndex];

                if (definition == null)
                    continue;

                float experienceAmount = math.max(0f, definition.ExperienceAmount);

                if (experienceAmount <= 0f)
                    continue;

                result.ExperienceDropDefinitions.Add(new EnemyCompiledExperienceDropDefinition
                {
                    Prefab = definition.DropPrefab,
                    ExperienceAmount = experienceAmount
                });
                previewValues.Add(experienceAmount);
            }
        }

        compiledModule.DefinitionCount = result.ExperienceDropDefinitions.Count - compiledModule.DefinitionStartIndex;

        if (compiledModule.DefinitionCount <= 0)
            return;

        compiledModule.EstimatedDropsPerDeath = math.max(0,
                                                         EnemyExperienceDropDistributionUtility.EstimateDropsForPreview(previewValues,
                                                                                                                        compiledModule.MaximumTotalExperienceDrop,
                                                                                                                        compiledModule.Distribution,
                                                                                                                        out float _,
                                                                                                                        out float _));
        result.ExperienceDropModules.Add(compiledModule);
        result.DropItemsConfig.HasExperienceDrops = 1;
        result.DropItemsConfig.ExperienceModuleCount = result.ExperienceDropModules.Count;
        result.DropItemsConfig.EstimatedDropsPerDeath = AddEstimatedDropCount(result.DropItemsConfig.EstimatedDropsPerDeath,
                                                                              compiledModule.EstimatedDropsPerDeath);
    }

    /// <summary>
    /// Appends one compiled health/shield recovery-drop module to the bake result.
    /// </summary>
    /// <param name="recoveryPayload">Recovery payload block resolved from authoring.</param>
    /// <param name="result">Mutable compiled result receiving module data.</param>
    private static void TryAppendRecoveryModule(EnemyRecoveryDropPayload recoveryPayload,
                                                ref EnemyCompiledPatternBakeResult result)
    {
        if (recoveryPayload == null)
            return;

        float dropChance = math.clamp(recoveryPayload.DropChancePercent * 0.01f, 0f, 1f);

        if (dropChance <= 0f)
            return;

        EnemyExperienceDropCollectionSettings collectionMovement = recoveryPayload.CollectionMovement;
        EnemyCompiledRecoveryDropModule compiledModule = new EnemyCompiledRecoveryDropModule
        {
            DropChance = dropChance,
            DropRadius = math.max(0f, recoveryPayload.DropRadius),
            GroundHeightOffset = ResolveGroundHeightOffset(recoveryPayload.GroundHeightOffset),
            AttractionSpeed = collectionMovement != null ? math.max(0f, collectionMovement.MoveSpeed) : 0f,
            CollectDistance = collectionMovement != null ? math.max(0.01f, collectionMovement.CollectDistance) : 0.3f,
            CollectDistancePerPlayerSpeed = collectionMovement != null ? math.max(0f, collectionMovement.CollectDistancePerPlayerSpeed) : 0.05f,
            SpawnAnimationMinDuration = collectionMovement != null ? math.max(0f, collectionMovement.SpawnAnimationMinDuration) : 0.08f,
            SpawnAnimationMaxDuration = collectionMovement != null
                ? math.max(math.max(0f, collectionMovement.SpawnAnimationMinDuration), collectionMovement.SpawnAnimationMaxDuration)
                : 0.16f,
            DefinitionStartIndex = result.RecoveryDropDefinitions.Count,
            DefinitionCount = 0,
            EstimatedDropsPerDeath = 0,
            SelectionWeight = 1f
        };

        IReadOnlyList<EnemyRecoveryDropDefinitionData> definitions = recoveryPayload.DropDefinitions;

        if (definitions != null)
        {
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                EnemyRecoveryDropDefinitionData definition = definitions[definitionIndex];

                if (definition == null)
                    continue;

                float healthRestoreAmount = math.max(0f, definition.HealthRestoreAmount);
                float shieldRestoreAmount = math.max(0f, definition.ShieldRestoreAmount);

                if (healthRestoreAmount <= 0f && shieldRestoreAmount <= 0f)
                    continue;

                int dropCount = math.max(0, definition.DropCount);

                if (dropCount <= 0)
                    continue;

                result.RecoveryDropDefinitions.Add(new EnemyCompiledRecoveryDropDefinition
                {
                    Prefab = definition.DropPrefab,
                    HealthRestoreAmount = healthRestoreAmount,
                    ShieldRestoreAmount = shieldRestoreAmount,
                    Count = dropCount
                });
                compiledModule.EstimatedDropsPerDeath = AddEstimatedDropCount(compiledModule.EstimatedDropsPerDeath,
                                                                               dropCount);
            }
        }

        compiledModule.DefinitionCount = result.RecoveryDropDefinitions.Count - compiledModule.DefinitionStartIndex;

        if (compiledModule.DefinitionCount <= 0)
            return;

        result.RecoveryDropModules.Add(compiledModule);
        result.DropItemsConfig.HasRecoveryDrops = 1;
        result.DropItemsConfig.RecoveryModuleCount = result.RecoveryDropModules.Count;
        result.DropItemsConfig.EstimatedDropsPerDeath = AddEstimatedDropCount(result.DropItemsConfig.EstimatedDropsPerDeath,
                                                                              compiledModule.EstimatedDropsPerDeath);
    }

    /// <summary>
    /// Appends one compiled Extra Combo Points module to the bake result.
    /// </summary>
    /// <param name="extraComboPointsPayload">Extra Combo Points payload block resolved from authoring.</param>
    /// <param name="result">Mutable compiled result receiving module data.</param>
    private static void TryAppendExtraComboPointsModule(EnemyExtraComboPointsPayload extraComboPointsPayload,
                                                        ref EnemyCompiledPatternBakeResult result)
    {
        if (extraComboPointsPayload == null)
            return;

        EnemyCompiledExtraComboPointsModule compiledModule = new EnemyCompiledExtraComboPointsModule
        {
            BaseMultiplier = extraComboPointsPayload.BaseMultiplier,
            MinimumFinalMultiplier = extraComboPointsPayload.MinimumFinalMultiplier,
            MaximumFinalMultiplier = extraComboPointsPayload.MaximumFinalMultiplier,
            ConditionCombineMode = ResolveConditionCombineMode(extraComboPointsPayload.ConditionCombineMode),
            ConditionStartIndex = result.ExtraComboPointsConditions.Count,
            ConditionCount = 0,
            SelectionWeight = 1f
        };

        IReadOnlyList<EnemyExtraComboPointsConditionData> conditions = extraComboPointsPayload.Conditions;

        if (conditions != null)
        {
            for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
            {
                EnemyExtraComboPointsConditionData condition = conditions[conditionIndex];

                if (condition == null)
                    continue;

                result.ExtraComboPointsConditions.Add(new EnemyCompiledExtraComboPointsCondition
                {
                    Metric = ResolveMetric(condition.Metric),
                    MinimumValue = condition.MinimumValue,
                    UseMaximumValue = condition.UseMaximumValue ? (byte)1 : (byte)0,
                    MaximumValue = condition.MaximumValue,
                    MinimumMultiplier = condition.MinimumMultiplier,
                    MaximumMultiplier = condition.MaximumMultiplier,
                    NormalizedMultiplierCurveSamples = BuildNormalizedMultiplierCurveSamples(condition.NormalizedMultiplierCurve)
                });
            }
        }

        compiledModule.ConditionCount = result.ExtraComboPointsConditions.Count - compiledModule.ConditionStartIndex;
        result.ExtraComboPointsModules.Add(compiledModule);
        result.DropItemsConfig.HasExtraComboPoints = 1;
        result.DropItemsConfig.ExtraComboPointsModuleCount = result.ExtraComboPointsModules.Count;
    }

    /// <summary>
    /// Adds one estimated drop count to the running summary while protecting against integer overflow.
    /// </summary>
    /// <param name="currentEstimatedCount">Current accumulated estimated count.</param>
    /// <param name="additionalEstimatedCount">Additional count to append.</param>
    /// <returns>Saturated estimated drop count.</returns>
    private static int AddEstimatedDropCount(int currentEstimatedCount, int additionalEstimatedCount)
    {
        long resolvedCount = (long)math.max(0, currentEstimatedCount) + math.max(0, additionalEstimatedCount);

        if (resolvedCount >= int.MaxValue)
            return int.MaxValue;

        return (int)resolvedCount;
    }

    /// <summary>
    /// Samples one normalized multiplier curve into a compact fixed list used directly by ECS runtime.
    /// </summary>
    /// <param name="normalizedMultiplierCurve">Authored normalized response curve.</param>
    /// <returns>Fixed-size sampled curve values in the 0..1 range.</returns>
    private static FixedList64Bytes<float> BuildNormalizedMultiplierCurveSamples(AnimationCurve normalizedMultiplierCurve)
    {
        FixedList64Bytes<float> sampledCurve = default;
        AnimationCurve resolvedCurve = ResolveNormalizedMultiplierCurve(normalizedMultiplierCurve);

        for (int sampleIndex = 0; sampleIndex < NormalizedMultiplierCurveSampleCount; sampleIndex++)
        {
            float normalizedTime = NormalizedMultiplierCurveSampleCount <= 1
                ? 0f
                : (float)sampleIndex / (NormalizedMultiplierCurveSampleCount - 1);
            sampledCurve.Add(math.saturate(resolvedCurve.Evaluate(normalizedTime)));
        }

        return sampledCurve;
    }

    /// <summary>
    /// Resolves a valid normalized multiplier curve when authoring data is missing.
    /// </summary>
    /// <param name="normalizedMultiplierCurve">Authored response curve.</param>
    /// <returns>Non-null curve used by bake sampling.</returns>
    private static AnimationCurve ResolveNormalizedMultiplierCurve(AnimationCurve normalizedMultiplierCurve)
    {
        if (normalizedMultiplierCurve != null)
        {
            return normalizedMultiplierCurve;
        }

        return new AnimationCurve(new Keyframe(0f, 1f),
                                  new Keyframe(1f, 0f));
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one compiled experience-drop module before entity conversion in baker.
/// </summary>
public struct EnemyCompiledExperienceDropModule
{
    #region Fields
    public float MinimumTotalExperienceDrop;
    public float MaximumTotalExperienceDrop;
    public float Distribution;
    public float DropRadius;
    public float GroundHeightOffset;
    public float AttractionSpeed;
    public float CollectDistance;
    public float CollectDistancePerPlayerSpeed;
    public float SpawnAnimationMinDuration;
    public float SpawnAnimationMaxDuration;
    public int DefinitionStartIndex;
    public int DefinitionCount;
    public int EstimatedDropsPerDeath;
    public float SelectionWeight;
    #endregion
}

/// <summary>
/// Stores one compiled experience drop-definition entry before entity conversion in baker.
/// </summary>
public struct EnemyCompiledExperienceDropDefinition
{
    #region Fields
    public GameObject Prefab;
    public float ExperienceAmount;
    #endregion
}

/// <summary>
/// Stores one compiled health/shield recovery-drop module before entity conversion in baker.
/// </summary>
public struct EnemyCompiledRecoveryDropModule
{
    #region Fields
    public float DropChance;
    public float DropRadius;
    public float GroundHeightOffset;
    public float AttractionSpeed;
    public float CollectDistance;
    public float CollectDistancePerPlayerSpeed;
    public float SpawnAnimationMinDuration;
    public float SpawnAnimationMaxDuration;
    public int DefinitionStartIndex;
    public int DefinitionCount;
    public int EstimatedDropsPerDeath;
    public float SelectionWeight;
    #endregion
}

/// <summary>
/// Stores one compiled health/shield recovery drop-definition entry before entity conversion in baker.
/// </summary>
public struct EnemyCompiledRecoveryDropDefinition
{
    #region Fields
    public GameObject Prefab;
    public float HealthRestoreAmount;
    public float ShieldRestoreAmount;
    public int Count;
    #endregion
}

/// <summary>
/// Stores one compiled Extra Combo Points module before ECS buffer conversion in baker.
/// </summary>
public struct EnemyCompiledExtraComboPointsModule
{
    #region Fields
    public float BaseMultiplier;
    public float MinimumFinalMultiplier;
    public float MaximumFinalMultiplier;
    public EnemyExtraComboPointsConditionCombineMode ConditionCombineMode;
    public int ConditionStartIndex;
    public int ConditionCount;
    public float SelectionWeight;
    #endregion
}

/// <summary>
/// Stores one compiled Extra Combo Points condition before ECS buffer conversion in baker.
/// </summary>
public struct EnemyCompiledExtraComboPointsCondition
{
    #region Fields
    public EnemyExtraComboPointsMetric Metric;
    public float MinimumValue;
    public byte UseMaximumValue;
    public float MaximumValue;
    public float MinimumMultiplier;
    public float MaximumMultiplier;
    public FixedList64Bytes<float> NormalizedMultiplierCurveSamples;
    #endregion
}
