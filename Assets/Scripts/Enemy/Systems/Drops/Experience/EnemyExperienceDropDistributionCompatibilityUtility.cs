using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes compatibility, quantization and preview-selection helpers for enemy experience drop planning.
/// </summary>
public static class EnemyExperienceDropDistributionCompatibilityUtility
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    private const float CompatibilityTolerance = 0.0001f;
    private const int CompatibilityQuantizationScale = 100;
    private const int MaxCompatibilitySearchSteps = 262144;
    private const int MaxSuggestedRangeExpansionIterations = 10;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the nearest compatible preview range that fits inside the requested interval.
    /// </summary>
    /// <param name="definitionExperienceValues">Preview drop values collected from serialized data.</param>
    /// <param name="requestedMinimumTotal">Requested inclusive minimum total experience.</param>
    /// <param name="requestedMaximumTotal">Requested inclusive maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors lower definitions and 1 favors higher ones.</param>
    /// <param name="resolvedMinimumTotal">Resolved compatible minimum total.</param>
    /// <param name="resolvedMaximumTotal">Resolved compatible maximum total.</param>
    /// <returns>True when at least one compatible total exists inside the requested range.</returns>
    public static bool TryResolveCompatiblePreviewRange(IReadOnlyList<float> definitionExperienceValues,
                                                        float requestedMinimumTotal,
                                                        float requestedMaximumTotal,
                                                        float distribution,
                                                        out float resolvedMinimumTotal,
                                                        out float resolvedMaximumTotal)
    {
        resolvedMinimumTotal = 0f;
        resolvedMaximumTotal = 0f;

        if (definitionExperienceValues == null || definitionExperienceValues.Count <= 0)
            return false;

        float sanitizedMinimumTotal = math.max(0f, requestedMinimumTotal);
        float sanitizedMaximumTotal = math.max(sanitizedMinimumTotal, requestedMaximumTotal);

        if (sanitizedMaximumTotal <= PrecisionEpsilon)
            return false;

        int quantizationStepUnits = ResolvePreviewQuantizationStepUnits(definitionExperienceValues);

        if (quantizationStepUnits <= 0)
            return false;

        int minimumUnits = ConvertMinimumTotalToUnits(sanitizedMinimumTotal, quantizationStepUnits);
        int maximumUnits = ConvertMaximumTotalToUnits(sanitizedMaximumTotal, quantizationStepUnits);

        if (maximumUnits < minimumUnits)
            return false;

        int compatibleMinimumUnits;

        if (!TryFindFirstCompatiblePreviewTotalUnits(definitionExperienceValues,
                                                     minimumUnits,
                                                     maximumUnits,
                                                     quantizationStepUnits,
                                                     distribution,
                                                     out compatibleMinimumUnits))
        {
            return false;
        }

        int compatibleMaximumUnits;

        if (!TryFindLastCompatiblePreviewTotalUnits(definitionExperienceValues,
                                                    compatibleMinimumUnits,
                                                    maximumUnits,
                                                    quantizationStepUnits,
                                                    distribution,
                                                    out compatibleMaximumUnits))
        {
            return false;
        }

        resolvedMinimumTotal = ConvertUnitsToTotal(compatibleMinimumUnits, quantizationStepUnits);
        resolvedMaximumTotal = ConvertUnitsToTotal(compatibleMaximumUnits, quantizationStepUnits);
        return true;
    }

    /// <summary>
    /// Suggests a nearby compatible preview range when the requested range is not already compatible.
    /// </summary>
    /// <param name="definitionExperienceValues">Preview drop values collected from serialized data.</param>
    /// <param name="requestedMinimumTotal">Requested inclusive minimum total experience.</param>
    /// <param name="requestedMaximumTotal">Requested inclusive maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors lower definitions and 1 favors higher ones.</param>
    /// <param name="suggestedMinimumTotal">Suggested compatible minimum total.</param>
    /// <param name="suggestedMaximumTotal">Suggested compatible maximum total.</param>
    /// <returns>True when a compatible range is found near the requested interval.</returns>
    public static bool TryResolveSuggestedPreviewRange(IReadOnlyList<float> definitionExperienceValues,
                                                       float requestedMinimumTotal,
                                                       float requestedMaximumTotal,
                                                       float distribution,
                                                       out float suggestedMinimumTotal,
                                                       out float suggestedMaximumTotal)
    {
        suggestedMinimumTotal = 0f;
        suggestedMaximumTotal = 0f;

        if (definitionExperienceValues == null || definitionExperienceValues.Count <= 0)
            return false;

        float sanitizedMinimumTotal = math.max(0f, requestedMinimumTotal);
        float sanitizedMaximumTotal = math.max(sanitizedMinimumTotal, requestedMaximumTotal);
        float clampedDistribution = math.clamp(distribution, 0f, 1f);

        if (TryResolveCompatiblePreviewRange(definitionExperienceValues,
                                             sanitizedMinimumTotal,
                                             sanitizedMaximumTotal,
                                             clampedDistribution,
                                             out suggestedMinimumTotal,
                                             out suggestedMaximumTotal))
        {
            return true;
        }

        float minimumDefinitionValue;
        float maximumDefinitionValue;

        if (!TryResolvePreviewBounds(definitionExperienceValues, out minimumDefinitionValue, out maximumDefinitionValue))
            return false;

        float expansionStep = math.max(0.01f, math.min(minimumDefinitionValue, maximumDefinitionValue));

        for (int expansionIndex = 0; expansionIndex < MaxSuggestedRangeExpansionIterations; expansionIndex++)
        {
            int expansionMultiplier = 1 << expansionIndex;
            float expansionAmount = expansionStep * expansionMultiplier;
            float expandedMinimumTotal = math.max(0f, sanitizedMinimumTotal - expansionAmount);
            float expandedMaximumTotal = sanitizedMaximumTotal + expansionAmount;

            if (!TryResolveCompatiblePreviewRange(definitionExperienceValues,
                                                  expandedMinimumTotal,
                                                  expandedMaximumTotal,
                                                  clampedDistribution,
                                                  out suggestedMinimumTotal,
                                                  out suggestedMaximumTotal))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Picks a random compatible runtime total inside the requested interval.
    /// </summary>
    /// <param name="definitions">Runtime drop definitions.</param>
    /// <param name="minimumTotal">Requested inclusive minimum total experience.</param>
    /// <param name="maximumTotal">Requested inclusive maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors lower definitions and 1 favors higher ones.</param>
    /// <param name="randomSeed">Deterministic seed used for the target pick.</param>
    /// <param name="resolvedTotal">Resolved compatible total experience.</param>
    /// <returns>True when a compatible runtime total is found.</returns>
    public static bool TryResolveRandomCompatibleTotal(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                       float minimumTotal,
                                                       float maximumTotal,
                                                       float distribution,
                                                       uint randomSeed,
                                                       out float resolvedTotal)
    {
        return TryResolveRandomCompatibleTotal(definitions,
                                              0,
                                              definitions.Length,
                                              minimumTotal,
                                              maximumTotal,
                                              distribution,
                                              randomSeed,
                                              out resolvedTotal);
    }

    /// <summary>
    /// Resolves one compatible total inside the requested runtime range for one module slice.
    /// </summary>
    /// <param name="definitions">Shared runtime drop definitions buffer.</param>
    /// <param name="definitionStartIndex">Inclusive module-slice start index.</param>
    /// <param name="definitionCount">Definition count owned by the module slice.</param>
    /// <param name="minimumTotal">Requested inclusive minimum total experience.</param>
    /// <param name="maximumTotal">Requested inclusive maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors lower definitions and 1 favors higher ones.</param>
    /// <param name="randomSeed">Deterministic seed used for the target pick.</param>
    /// <param name="resolvedTotal">Resolved compatible total experience.</param>
    /// <returns>True when a compatible runtime total is found.</returns>
    public static bool TryResolveRandomCompatibleTotal(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                       int definitionStartIndex,
                                                       int definitionCount,
                                                       float minimumTotal,
                                                       float maximumTotal,
                                                       float distribution,
                                                       uint randomSeed,
                                                       out float resolvedTotal)
    {
        resolvedTotal = 0f;

        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        if (definitionEndIndex <= sanitizedDefinitionStartIndex)
            return false;

        float sanitizedMinimumTotal = math.max(0f, minimumTotal);
        float sanitizedMaximumTotal = math.max(sanitizedMinimumTotal, maximumTotal);

        if (sanitizedMaximumTotal <= PrecisionEpsilon)
            return false;

        int quantizationStepUnits = ResolveRuntimeQuantizationStepUnits(definitions,
                                                                       sanitizedDefinitionStartIndex,
                                                                       definitionEndIndex - sanitizedDefinitionStartIndex);

        if (quantizationStepUnits <= 0)
            return false;

        int minimumUnits = ConvertMinimumTotalToUnits(sanitizedMinimumTotal, quantizationStepUnits);
        int maximumUnits = ConvertMaximumTotalToUnits(sanitizedMaximumTotal, quantizationStepUnits);

        if (maximumUnits < minimumUnits)
            return false;

        uint sanitizedSeed = randomSeed;

        if (sanitizedSeed == 0u)
            sanitizedSeed = 1u;

        Unity.Mathematics.Random random = new Unity.Mathematics.Random(sanitizedSeed);
        int targetUnits = random.NextInt(minimumUnits, maximumUnits + 1);
        int compatibleUnits;

        if (!TryFindNearestCompatibleRuntimeTotalUnits(definitions,
                                                       sanitizedDefinitionStartIndex,
                                                       definitionEndIndex - sanitizedDefinitionStartIndex,
                                                       minimumUnits,
                                                       maximumUnits,
                                                       targetUnits,
                                                       quantizationStepUnits,
                                                       distribution,
                                                       out compatibleUnits))
        {
            return false;
        }

        resolvedTotal = ConvertUnitsToTotal(compatibleUnits, quantizationStepUnits);
        return true;
    }

    public static bool TryResolveRandomCompatibleTotal(NativeArray<EnemyExperienceDropDefinitionElement> definitions,
                                                       int definitionStartIndex,
                                                       int definitionCount,
                                                       float minimumTotal,
                                                       float maximumTotal,
                                                       float distribution,
                                                       uint randomSeed,
                                                       out float resolvedTotal)
    {
        resolvedTotal = 0f;

        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        if (definitionEndIndex <= sanitizedDefinitionStartIndex)
            return false;

        float sanitizedMinimumTotal = math.max(0f, minimumTotal);
        float sanitizedMaximumTotal = math.max(sanitizedMinimumTotal, maximumTotal);

        if (sanitizedMaximumTotal <= PrecisionEpsilon)
            return false;

        int quantizationStepUnits = ResolveRuntimeQuantizationStepUnits(definitions,
                                                                       sanitizedDefinitionStartIndex,
                                                                       definitionEndIndex - sanitizedDefinitionStartIndex);

        if (quantizationStepUnits <= 0)
            return false;

        int minimumUnits = ConvertMinimumTotalToUnits(sanitizedMinimumTotal, quantizationStepUnits);
        int maximumUnits = ConvertMaximumTotalToUnits(sanitizedMaximumTotal, quantizationStepUnits);

        if (maximumUnits < minimumUnits)
            return false;

        uint sanitizedSeed = randomSeed;

        if (sanitizedSeed == 0u)
            sanitizedSeed = 1u;

        Unity.Mathematics.Random random = new Unity.Mathematics.Random(sanitizedSeed);
        int targetUnits = random.NextInt(minimumUnits, maximumUnits + 1);
        int compatibleUnits;

        if (!TryFindNearestCompatibleRuntimeTotalUnits(definitions,
                                                       sanitizedDefinitionStartIndex,
                                                       definitionEndIndex - sanitizedDefinitionStartIndex,
                                                       minimumUnits,
                                                       maximumUnits,
                                                       targetUnits,
                                                       quantizationStepUnits,
                                                       distribution,
                                                       out compatibleUnits))
        {
            return false;
        }

        resolvedTotal = ConvertUnitsToTotal(compatibleUnits, quantizationStepUnits);
        return true;
    }

    /// <summary>
    /// Resolves valid runtime bounds from authored runtime definitions.
    /// </summary>
    /// <param name="definitions">Runtime drop definitions.</param>
    /// <param name="minimumValue">Minimum valid amount.</param>
    /// <param name="maximumValue">Maximum valid amount.</param>
    /// <returns>True when at least one valid runtime definition exists.</returns>
    public static bool TryResolveRuntimeBounds(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                               out float minimumValue,
                                               out float maximumValue)
    {
        return TryResolveRuntimeBounds(definitions,
                                       0,
                                       definitions.Length,
                                       out minimumValue,
                                       out maximumValue);
    }

    /// <summary>
    /// Resolves valid runtime bounds from authored runtime definitions contained in one module slice.
    /// </summary>
    /// <param name="definitions">Shared runtime drop definitions buffer.</param>
    /// <param name="definitionStartIndex">Inclusive module-slice start index.</param>
    /// <param name="definitionCount">Definition count owned by the module slice.</param>
    /// <param name="minimumValue">Minimum valid amount.</param>
    /// <param name="maximumValue">Maximum valid amount.</param>
    /// <returns>True when at least one valid runtime definition exists.</returns>
    public static bool TryResolveRuntimeBounds(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                               int definitionStartIndex,
                                               int definitionCount,
                                               out float minimumValue,
                                               out float maximumValue)
    {
        minimumValue = float.MaxValue;
        maximumValue = 0f;
        bool hasValidDefinition = false;
        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        for (int index = sanitizedDefinitionStartIndex; index < definitionEndIndex; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];
            float amount = definition.ExperienceAmount;

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (amount <= 0f)
                continue;

            hasValidDefinition = true;

            if (amount < minimumValue)
                minimumValue = amount;

            if (amount > maximumValue)
                maximumValue = amount;
        }

        if (hasValidDefinition)
            return true;

        minimumValue = 0f;
        maximumValue = 0f;
        return false;
    }

    public static bool TryResolveRuntimeBounds(NativeArray<EnemyExperienceDropDefinitionElement> definitions,
                                               int definitionStartIndex,
                                               int definitionCount,
                                               out float minimumValue,
                                               out float maximumValue)
    {
        minimumValue = float.MaxValue;
        maximumValue = 0f;
        bool hasValidDefinition = false;
        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        for (int index = sanitizedDefinitionStartIndex; index < definitionEndIndex; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];
            float amount = definition.ExperienceAmount;

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (amount <= 0f)
                continue;

            hasValidDefinition = true;

            if (amount < minimumValue)
                minimumValue = amount;

            if (amount > maximumValue)
                maximumValue = amount;
        }

        if (hasValidDefinition)
            return true;

        minimumValue = 0f;
        maximumValue = 0f;
        return false;
    }

    #endregion

    #region Private Methods
    public static bool TryResolvePreviewBounds(IReadOnlyList<float> definitionExperienceValues,
                                               out float minimumValue,
                                               out float maximumValue)
    {
        minimumValue = float.MaxValue;
        maximumValue = 0f;
        bool hasValidDefinition = false;

        for (int index = 0; index < definitionExperienceValues.Count; index++)
        {
            float amount = definitionExperienceValues[index];

            if (amount <= 0f)
                continue;

            hasValidDefinition = true;

            if (amount < minimumValue)
                minimumValue = amount;

            if (amount > maximumValue)
                maximumValue = amount;
        }

        if (hasValidDefinition)
            return true;

        minimumValue = 0f;
        maximumValue = 0f;
        return false;
    }

    private static bool TryFindNearestCompatibleRuntimeTotalUnits(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                                  int definitionStartIndex,
                                                                  int definitionCount,
                                                                  int minimumUnits,
                                                                  int maximumUnits,
                                                                  int targetUnits,
                                                                  int quantizationStepUnits,
                                                                  float distribution,
                                                                  out int compatibleUnits)
    {
        compatibleUnits = -1;

        if (minimumUnits > maximumUnits)
            return false;

        int clampedTargetUnits = math.clamp(targetUnits, minimumUnits, maximumUnits);
        int totalSpan = maximumUnits - minimumUnits;
        int maximumSearchSteps = math.min(totalSpan, MaxCompatibilitySearchSteps);
        float clampedDistribution = math.clamp(distribution, 0f, 1f);
        bool preferHigherTotals = clampedDistribution >= 0.5f;

        for (int offset = 0; offset <= maximumSearchSteps; offset++)
        {
            int lowerCandidateUnits = clampedTargetUnits - offset;
            int upperCandidateUnits = clampedTargetUnits + offset;

            if (preferHigherTotals)
            {
                if (upperCandidateUnits <= maximumUnits)
                {
                    float upperCandidateTotal = ConvertUnitsToTotal(upperCandidateUnits, quantizationStepUnits);

                    if (IsRuntimeTotalCompatible(definitions,
                                                 definitionStartIndex,
                                                 definitionCount,
                                                 upperCandidateTotal,
                                                 distribution))
                    {
                        compatibleUnits = upperCandidateUnits;
                        return true;
                    }
                }

                if (offset > 0 && lowerCandidateUnits >= minimumUnits)
                {
                    float lowerCandidateTotal = ConvertUnitsToTotal(lowerCandidateUnits, quantizationStepUnits);

                    if (IsRuntimeTotalCompatible(definitions,
                                                 definitionStartIndex,
                                                 definitionCount,
                                                 lowerCandidateTotal,
                                                 distribution))
                    {
                        compatibleUnits = lowerCandidateUnits;
                        return true;
                    }
                }

                continue;
            }

            if (lowerCandidateUnits >= minimumUnits)
            {
                float lowerCandidateTotal = ConvertUnitsToTotal(lowerCandidateUnits, quantizationStepUnits);

                if (IsRuntimeTotalCompatible(definitions,
                                             definitionStartIndex,
                                             definitionCount,
                                             lowerCandidateTotal,
                                             distribution))
                {
                    compatibleUnits = lowerCandidateUnits;
                    return true;
                }
            }

            if (offset > 0 && upperCandidateUnits <= maximumUnits)
            {
                float upperCandidateTotal = ConvertUnitsToTotal(upperCandidateUnits, quantizationStepUnits);

                if (IsRuntimeTotalCompatible(definitions,
                                             definitionStartIndex,
                                             definitionCount,
                                             upperCandidateTotal,
                                             distribution))
                {
                    compatibleUnits = upperCandidateUnits;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindNearestCompatibleRuntimeTotalUnits(NativeArray<EnemyExperienceDropDefinitionElement> definitions,
                                                                  int definitionStartIndex,
                                                                  int definitionCount,
                                                                  int minimumUnits,
                                                                  int maximumUnits,
                                                                  int targetUnits,
                                                                  int quantizationStepUnits,
                                                                  float distribution,
                                                                  out int compatibleUnits)
    {
        compatibleUnits = -1;

        if (minimumUnits > maximumUnits)
            return false;

        int clampedTargetUnits = math.clamp(targetUnits, minimumUnits, maximumUnits);
        int totalSpan = maximumUnits - minimumUnits;
        int maximumSearchSteps = math.min(totalSpan, MaxCompatibilitySearchSteps);
        float clampedDistribution = math.clamp(distribution, 0f, 1f);
        bool preferHigherTotals = clampedDistribution >= 0.5f;

        for (int offset = 0; offset <= maximumSearchSteps; offset++)
        {
            int lowerCandidateUnits = clampedTargetUnits - offset;
            int upperCandidateUnits = clampedTargetUnits + offset;

            if (preferHigherTotals)
            {
                if (upperCandidateUnits <= maximumUnits)
                {
                    float upperCandidateTotal = ConvertUnitsToTotal(upperCandidateUnits, quantizationStepUnits);

                    if (IsRuntimeTotalCompatible(definitions,
                                                 definitionStartIndex,
                                                 definitionCount,
                                                 upperCandidateTotal,
                                                 distribution))
                    {
                        compatibleUnits = upperCandidateUnits;
                        return true;
                    }
                }

                if (offset > 0 && lowerCandidateUnits >= minimumUnits)
                {
                    float lowerCandidateTotal = ConvertUnitsToTotal(lowerCandidateUnits, quantizationStepUnits);

                    if (IsRuntimeTotalCompatible(definitions,
                                                 definitionStartIndex,
                                                 definitionCount,
                                                 lowerCandidateTotal,
                                                 distribution))
                    {
                        compatibleUnits = lowerCandidateUnits;
                        return true;
                    }
                }

                continue;
            }

            if (lowerCandidateUnits >= minimumUnits)
            {
                float lowerCandidateTotal = ConvertUnitsToTotal(lowerCandidateUnits, quantizationStepUnits);

                if (IsRuntimeTotalCompatible(definitions,
                                             definitionStartIndex,
                                             definitionCount,
                                             lowerCandidateTotal,
                                             distribution))
                {
                    compatibleUnits = lowerCandidateUnits;
                    return true;
                }
            }

            if (offset > 0 && upperCandidateUnits <= maximumUnits)
            {
                float upperCandidateTotal = ConvertUnitsToTotal(upperCandidateUnits, quantizationStepUnits);

                if (IsRuntimeTotalCompatible(definitions,
                                             definitionStartIndex,
                                             definitionCount,
                                             upperCandidateTotal,
                                             distribution))
                {
                    compatibleUnits = upperCandidateUnits;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindFirstCompatiblePreviewTotalUnits(IReadOnlyList<float> definitionExperienceValues,
                                                                int minimumUnits,
                                                                int maximumUnits,
                                                                int quantizationStepUnits,
                                                                float distribution,
                                                                out int compatibleUnits)
    {
        compatibleUnits = -1;

        if (minimumUnits > maximumUnits)
            return false;

        int totalSpan = maximumUnits - minimumUnits;
        int maximumSearchSteps = math.min(totalSpan, MaxCompatibilitySearchSteps);

        for (int offset = 0; offset <= maximumSearchSteps; offset++)
        {
            int candidateUnits = minimumUnits + offset;
            float candidateTotal = ConvertUnitsToTotal(candidateUnits, quantizationStepUnits);

            if (!IsPreviewTotalCompatible(definitionExperienceValues, candidateTotal, distribution))
                continue;

            compatibleUnits = candidateUnits;
            return true;
        }

        return false;
    }

    private static bool TryFindLastCompatiblePreviewTotalUnits(IReadOnlyList<float> definitionExperienceValues,
                                                               int minimumUnits,
                                                               int maximumUnits,
                                                               int quantizationStepUnits,
                                                               float distribution,
                                                               out int compatibleUnits)
    {
        compatibleUnits = -1;

        if (minimumUnits > maximumUnits)
            return false;

        int totalSpan = maximumUnits - minimumUnits;
        int maximumSearchSteps = math.min(totalSpan, MaxCompatibilitySearchSteps);

        for (int offset = 0; offset <= maximumSearchSteps; offset++)
        {
            int candidateUnits = maximumUnits - offset;
            float candidateTotal = ConvertUnitsToTotal(candidateUnits, quantizationStepUnits);

            if (!IsPreviewTotalCompatible(definitionExperienceValues, candidateTotal, distribution))
                continue;

            compatibleUnits = candidateUnits;
            return true;
        }

        return false;
    }

    private static bool IsRuntimeTotalCompatible(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                 int definitionStartIndex,
                                                 int definitionCount,
                                                 float totalExperienceDrop,
                                                 float distribution)
    {
        if (totalExperienceDrop <= PrecisionEpsilon)
            return false;

        float deliveredExperience;
        float absoluteError;
        int estimatedDropCount = EnemyExperienceDropDistributionUtility.EstimateDropsPerDeath(definitions,
                                                                                              definitionStartIndex,
                                                                                              definitionCount,
                                                                                              totalExperienceDrop,
                                                                                              distribution,
                                                                                              out deliveredExperience,
                                                                                              out absoluteError);

        if (estimatedDropCount <= 0)
            return false;

        return absoluteError <= CompatibilityTolerance;
    }

    private static bool IsRuntimeTotalCompatible(NativeArray<EnemyExperienceDropDefinitionElement> definitions,
                                                 int definitionStartIndex,
                                                 int definitionCount,
                                                 float totalExperienceDrop,
                                                 float distribution)
    {
        if (totalExperienceDrop <= PrecisionEpsilon)
            return false;

        float deliveredExperience;
        float absoluteError;
        int estimatedDropCount = EnemyExperienceDropDistributionUtility.EstimateDropsPerDeath(definitions,
                                                                                              definitionStartIndex,
                                                                                              definitionCount,
                                                                                              totalExperienceDrop,
                                                                                              distribution,
                                                                                              out deliveredExperience,
                                                                                              out absoluteError);

        if (estimatedDropCount <= 0)
            return false;

        return absoluteError <= CompatibilityTolerance;
    }

    private static bool IsPreviewTotalCompatible(IReadOnlyList<float> definitionExperienceValues,
                                                 float totalExperienceDrop,
                                                 float distribution)
    {
        if (totalExperienceDrop <= PrecisionEpsilon)
            return false;

        float deliveredExperience;
        float absoluteError;
        int estimatedDropCount = EnemyExperienceDropDistributionUtility.EstimateDropsForPreview(definitionExperienceValues,
                                                                                                totalExperienceDrop,
                                                                                                distribution,
                                                                                                out deliveredExperience,
                                                                                                out absoluteError);

        if (estimatedDropCount <= 0)
            return false;

        return absoluteError <= CompatibilityTolerance;
    }

    private static int ResolveRuntimeQuantizationStepUnits(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                           int definitionStartIndex,
                                                           int definitionCount)
    {
        int quantizationStepUnits = 0;
        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        for (int index = sanitizedDefinitionStartIndex; index < definitionEndIndex; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];

            if (definition.PrefabEntity == Entity.Null)
                continue;

            float amount = definition.ExperienceAmount;

            if (amount <= PrecisionEpsilon)
                continue;

            int amountUnits = ConvertExperienceAmountToUnits(amount);

            if (amountUnits <= 0)
                continue;

            if (quantizationStepUnits <= 0)
            {
                quantizationStepUnits = amountUnits;
                continue;
            }

            quantizationStepUnits = ComputeGreatestCommonDivisor(quantizationStepUnits, amountUnits);

            if (quantizationStepUnits <= 1)
                return 1;
        }

        return quantizationStepUnits;
    }

    private static int ResolveRuntimeQuantizationStepUnits(NativeArray<EnemyExperienceDropDefinitionElement> definitions,
                                                           int definitionStartIndex,
                                                           int definitionCount)
    {
        int quantizationStepUnits = 0;
        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        for (int index = sanitizedDefinitionStartIndex; index < definitionEndIndex; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];

            if (definition.PrefabEntity == Entity.Null)
                continue;

            float amount = definition.ExperienceAmount;

            if (amount <= PrecisionEpsilon)
                continue;

            int amountUnits = ConvertExperienceAmountToUnits(amount);

            if (amountUnits <= 0)
                continue;

            if (quantizationStepUnits <= 0)
            {
                quantizationStepUnits = amountUnits;
                continue;
            }

            quantizationStepUnits = ComputeGreatestCommonDivisor(quantizationStepUnits, amountUnits);

            if (quantizationStepUnits <= 1)
                return 1;
        }

        return quantizationStepUnits;
    }

    private static int ResolvePreviewQuantizationStepUnits(IReadOnlyList<float> definitionExperienceValues)
    {
        int quantizationStepUnits = 0;

        if (definitionExperienceValues == null)
            return quantizationStepUnits;

        for (int index = 0; index < definitionExperienceValues.Count; index++)
        {
            float amount = definitionExperienceValues[index];

            if (amount <= PrecisionEpsilon)
                continue;

            int amountUnits = ConvertExperienceAmountToUnits(amount);

            if (amountUnits <= 0)
                continue;

            if (quantizationStepUnits <= 0)
            {
                quantizationStepUnits = amountUnits;
                continue;
            }

            quantizationStepUnits = ComputeGreatestCommonDivisor(quantizationStepUnits, amountUnits);

            if (quantizationStepUnits <= 1)
                return 1;
        }

        return quantizationStepUnits;
    }

    private static int ConvertExperienceAmountToUnits(float amount)
    {
        float clampedAmount = math.max(0f, amount);
        int amountUnits = (int)math.round(clampedAmount * CompatibilityQuantizationScale);
        return math.max(1, amountUnits);
    }

    private static int ConvertMinimumTotalToUnits(float totalExperienceDrop, int quantizationStepUnits)
    {
        float clampedTotalExperienceDrop = math.max(0f, totalExperienceDrop);
        int scaledTotalUnits = (int)math.ceil(clampedTotalExperienceDrop * CompatibilityQuantizationScale - PrecisionEpsilon);

        if (scaledTotalUnits <= 0)
            return 0;

        return (scaledTotalUnits + quantizationStepUnits - 1) / quantizationStepUnits;
    }

    private static int ConvertMaximumTotalToUnits(float totalExperienceDrop, int quantizationStepUnits)
    {
        float clampedTotalExperienceDrop = math.max(0f, totalExperienceDrop);
        int scaledTotalUnits = (int)math.floor(clampedTotalExperienceDrop * CompatibilityQuantizationScale + PrecisionEpsilon);

        if (scaledTotalUnits < 0)
            return -1;

        return scaledTotalUnits / quantizationStepUnits;
    }

    private static float ConvertUnitsToTotal(int unitCount, int quantizationStepUnits)
    {
        if (unitCount <= 0 || quantizationStepUnits <= 0)
            return 0f;

        int scaledTotalUnits = unitCount * quantizationStepUnits;
        return scaledTotalUnits / (float)CompatibilityQuantizationScale;
    }

    private static int ComputeGreatestCommonDivisor(int leftValue, int rightValue)
    {
        int left = math.abs(leftValue);
        int right = math.abs(rightValue);

        if (left == 0)
            return right;

        if (right == 0)
            return left;

        while (right != 0)
        {
            int remainder = left % right;
            left = right;
            right = remainder;
        }

        return math.max(1, left);
    }
    #endregion

    #endregion
}
