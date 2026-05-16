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
            HasExperienceDrops = 0,
            HasExtraComboPoints = 0,
            ExperienceModuleCount = 0,
            ExtraComboPointsModuleCount = 0,
            EstimatedDropsPerDeath = 0
        };
    }

    /// <summary>
    /// Appends one compiled Drop Items payload to the aggregated bake result.
    /// </summary>
    /// <param name="payload">Effective payload block resolved from a module definition or binding override.</param>
    /// <param name="result">Mutable compiled result receiving module data.</param>
    public static void TryAppendModule(EnemyPatternModulePayloadData payload,
                                       ref EnemyCompiledPatternBakeResult result)
    {
        if (payload == null || payload.DropItems == null)
            return;

        EnemyDropItemsPayloadKind payloadKind = ResolveDropItemsPayloadKind(payload.DropItems.DropPayloadKind);

        switch (payloadKind)
        {
            case EnemyDropItemsPayloadKind.ExtraComboPoints:
                TryAppendExtraComboPointsModule(payload.DropItems.ExtraComboPoints, ref result);
                return;

            default:
                TryAppendExperienceModule(payload.DropItems.Experience, ref result);
                return;
        }
    }

    /// <summary>
    /// Resolves one legal drop-items payload kind authored in the editor.
    /// </summary>
    /// <param name="payloadKind">Authored payload kind candidate.</param>
    /// <returns>Sanitized payload kind used by bake.</returns>
    public static EnemyDropItemsPayloadKind ResolveDropItemsPayloadKind(EnemyDropItemsPayloadKind payloadKind)
    {
        switch (payloadKind)
        {
            case EnemyDropItemsPayloadKind.Experience:
            case EnemyDropItemsPayloadKind.ExtraComboPoints:
                return payloadKind;

            default:
                return EnemyDropItemsPayloadKind.Experience;
        }
    }

    /// <summary>
    /// Resolves one legal Extra Combo Points metric authored in the editor.
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
    /// Resolves one legal Extra Combo Points condition-combine mode authored in the editor.
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
    #endregion

    #region Private Methods
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
            AttractionSpeed = collectionMovement != null ? math.max(0f, collectionMovement.MoveSpeed) : 0f,
            CollectDistance = collectionMovement != null ? math.max(0.01f, collectionMovement.CollectDistance) : 0.3f,
            CollectDistancePerPlayerSpeed = collectionMovement != null ? math.max(0f, collectionMovement.CollectDistancePerPlayerSpeed) : 0.05f,
            SpawnAnimationMinDuration = collectionMovement != null ? math.max(0f, collectionMovement.SpawnAnimationMinDuration) : 0.08f,
            SpawnAnimationMaxDuration = collectionMovement != null
                ? math.max(math.max(0f, collectionMovement.SpawnAnimationMinDuration), collectionMovement.SpawnAnimationMaxDuration)
                : 0.16f,
            DefinitionStartIndex = result.ExperienceDropDefinitions.Count,
            DefinitionCount = 0,
            EstimatedDropsPerDeath = 0
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
            ConditionCount = 0
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
    public float AttractionSpeed;
    public float CollectDistance;
    public float CollectDistancePerPlayerSpeed;
    public float SpawnAnimationMinDuration;
    public float SpawnAnimationMaxDuration;
    public int DefinitionStartIndex;
    public int DefinitionCount;
    public int EstimatedDropsPerDeath;
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
