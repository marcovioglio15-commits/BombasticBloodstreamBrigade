using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides reusable distribution helpers for experience drop planning and estimation.
/// </summary>
public static class EnemyExperienceDropDistributionUtility
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    private const float CompatibilityTolerance = 0.0001f;
    private const int CompatibilityQuantizationScale = 100;
    private const int MaxCompatibilitySearchSteps = 262144;
    private const int MaxSuggestedRangeExpansionIterations = 10;
    private const int MaxPlanSteps = 4096;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the next runtime drop-definition choice for a remaining experience budget.
    /// </summary>
    /// <param name="definitions">Available runtime drop definitions.</param>
    /// <param name="remainingExperience">Remaining experience budget to distribute.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="definitionIndex">Resolved definition index when successful.</param>
    /// <param name="definitionExperienceAmount">Resolved definition experience amount when successful.</param>
    /// <param name="fitsRemaining">True when selected amount is lower or equal to remaining budget.</param>
    /// <returns>True when a valid definition is selected, otherwise false.</returns>
    public static bool TryResolveNextDefinition(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                float remainingExperience,
                                                float distribution,
                                                out int definitionIndex,
                                                out float definitionExperienceAmount,
                                                out bool fitsRemaining)
    {
        return TryResolveNextDefinition(definitions,
                                        0,
                                        definitions.Length,
                                        remainingExperience,
                                        distribution,
                                        out definitionIndex,
                                        out definitionExperienceAmount,
                                        out fitsRemaining);
    }

    /// <summary>
    /// Resolves the next runtime drop-definition choice for one module slice inside a shared definitions buffer.
    /// </summary>
    /// <param name="definitions">Shared runtime drop definitions buffer.</param>
    /// <param name="definitionStartIndex">Inclusive module-slice start index.</param>
    /// <param name="definitionCount">Definition count owned by the module slice.</param>
    /// <param name="remainingExperience">Remaining experience budget to distribute.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="definitionIndex">Resolved absolute definition index when successful.</param>
    /// <param name="definitionExperienceAmount">Resolved definition experience amount when successful.</param>
    /// <param name="fitsRemaining">True when selected amount is lower or equal to remaining budget.</param>
    /// <returns>True when a valid definition is selected, otherwise false.</returns>
    public static bool TryResolveNextDefinition(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                int definitionStartIndex,
                                                int definitionCount,
                                                float remainingExperience,
                                                float distribution,
                                                out int definitionIndex,
                                                out float definitionExperienceAmount,
                                                out bool fitsRemaining)
    {
        definitionIndex = -1;
        definitionExperienceAmount = 0f;
        fitsRemaining = false;

        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        if (definitionEndIndex <= sanitizedDefinitionStartIndex)
            return false;

        float minimumValue;
        float maximumValue;

        if (TryResolveRuntimeBounds(definitions,
                                    sanitizedDefinitionStartIndex,
                                    definitionEndIndex - sanitizedDefinitionStartIndex,
                                    out minimumValue,
                                    out maximumValue) == false)
            return false;

        float clampedDistribution = math.clamp(distribution, 0f, 1f);
        float targetValue = math.lerp(minimumValue, maximumValue, clampedDistribution);
        int bestIndex = -1;
        float bestAmount = 0f;
        float bestScore = float.MinValue;

        for (int index = sanitizedDefinitionStartIndex; index < definitionEndIndex; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];
            float amount = definition.ExperienceAmount;

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (amount <= 0f)
                continue;

            if (amount > remainingExperience + PrecisionEpsilon)
                continue;

            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution) == false)
                continue;

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
        }

        if (bestIndex >= 0)
        {
            definitionIndex = bestIndex;
            definitionExperienceAmount = bestAmount;
            fitsRemaining = true;
            return true;
        }

        float bestAbsoluteDifference = float.MaxValue;

        for (int index = sanitizedDefinitionStartIndex; index < definitionEndIndex; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];
            float amount = definition.ExperienceAmount;

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (amount <= 0f)
                continue;

            float absoluteDifference = math.abs(amount - remainingExperience);
            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (absoluteDifference > bestAbsoluteDifference + PrecisionEpsilon)
                continue;

            if (math.abs(absoluteDifference - bestAbsoluteDifference) <= PrecisionEpsilon &&
                IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution) == false)
                continue;

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
            bestAbsoluteDifference = absoluteDifference;
        }

        if (bestIndex < 0)
            return false;

        definitionIndex = bestIndex;
        definitionExperienceAmount = bestAmount;
        fitsRemaining = bestAmount <= remainingExperience + PrecisionEpsilon;
        return true;
    }

    public static bool TryResolveNextDefinition(NativeArray<EnemyExperienceDropDefinitionElement> definitions,
                                                int definitionStartIndex,
                                                int definitionCount,
                                                float remainingExperience,
                                                float distribution,
                                                out int definitionIndex,
                                                out float definitionExperienceAmount,
                                                out bool fitsRemaining)
    {
        definitionIndex = -1;
        definitionExperienceAmount = 0f;
        fitsRemaining = false;

        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        if (definitionEndIndex <= sanitizedDefinitionStartIndex)
            return false;

        float minimumValue;
        float maximumValue;

        if (TryResolveRuntimeBounds(definitions,
                                    sanitizedDefinitionStartIndex,
                                    definitionEndIndex - sanitizedDefinitionStartIndex,
                                    out minimumValue,
                                    out maximumValue) == false)
            return false;

        float clampedDistribution = math.clamp(distribution, 0f, 1f);
        float targetValue = math.lerp(minimumValue, maximumValue, clampedDistribution);
        int bestIndex = -1;
        float bestAmount = 0f;
        float bestScore = float.MinValue;

        for (int index = sanitizedDefinitionStartIndex; index < definitionEndIndex; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];
            float amount = definition.ExperienceAmount;

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (amount <= 0f)
                continue;

            if (amount > remainingExperience + PrecisionEpsilon)
                continue;

            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution) == false)
                continue;

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
        }

        if (bestIndex >= 0)
        {
            definitionIndex = bestIndex;
            definitionExperienceAmount = bestAmount;
            fitsRemaining = true;
            return true;
        }

        float bestAbsoluteDifference = float.MaxValue;

        for (int index = sanitizedDefinitionStartIndex; index < definitionEndIndex; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];
            float amount = definition.ExperienceAmount;

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (amount <= 0f)
                continue;

            float absoluteDifference = math.abs(amount - remainingExperience);
            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (absoluteDifference > bestAbsoluteDifference + PrecisionEpsilon)
                continue;

            if (math.abs(absoluteDifference - bestAbsoluteDifference) <= PrecisionEpsilon &&
                IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution) == false)
                continue;

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
            bestAbsoluteDifference = absoluteDifference;
        }

        if (bestIndex < 0)
            return false;

        definitionIndex = bestIndex;
        definitionExperienceAmount = bestAmount;
        fitsRemaining = bestAmount <= remainingExperience + PrecisionEpsilon;
        return true;
    }

    /// <summary>
    /// Estimates drop count and delivery error for runtime definitions using the same distribution logic used at spawn.
    /// </summary>
    /// <param name="definitions">Available runtime drop definitions.</param>
    /// <param name="totalExperienceDrop">Total experience budget to distribute.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="deliveredExperience">Estimated delivered experience by planned drops.</param>
    /// <param name="absoluteError">Absolute difference between requested and delivered experience.</param>
    /// <returns>Estimated planned drop count.</returns>
    public static int EstimateDropsPerDeath(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                            float totalExperienceDrop,
                                            float distribution,
                                            out float deliveredExperience,
                                            out float absoluteError)
    {
        return EstimateDropsPerDeath(definitions,
                                     0,
                                     definitions.Length,
                                     totalExperienceDrop,
                                     distribution,
                                     out deliveredExperience,
                                     out absoluteError);
    }

    /// <summary>
    /// Estimates drop count and delivery error for one module slice inside a shared runtime definitions buffer.
    /// </summary>
    /// <param name="definitions">Shared runtime drop definitions buffer.</param>
    /// <param name="definitionStartIndex">Inclusive module-slice start index.</param>
    /// <param name="definitionCount">Definition count owned by the module slice.</param>
    /// <param name="totalExperienceDrop">Total experience budget to distribute.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="deliveredExperience">Estimated delivered experience by planned drops.</param>
    /// <param name="absoluteError">Absolute difference between requested and delivered experience.</param>
    /// <returns>Estimated planned drop count.</returns>
    public static int EstimateDropsPerDeath(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                            int definitionStartIndex,
                                            int definitionCount,
                                            float totalExperienceDrop,
                                            float distribution,
                                            out float deliveredExperience,
                                            out float absoluteError)
    {
        deliveredExperience = 0f;
        absoluteError = math.max(0f, totalExperienceDrop);

        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        if (definitionEndIndex <= sanitizedDefinitionStartIndex)
            return 0;

        float remainingExperience = math.max(0f, totalExperienceDrop);

        if (remainingExperience <= PrecisionEpsilon)
        {
            absoluteError = 0f;
            return 0;
        }

        int dropCount = 0;

        for (int stepIndex = 0; stepIndex < MaxPlanSteps; stepIndex++)
        {
            if (remainingExperience <= PrecisionEpsilon)
                break;

            int definitionIndex;
            float definitionAmount;
            bool fitsRemaining;

            if (TryResolveNextDefinition(definitions,
                                         sanitizedDefinitionStartIndex,
                                         definitionEndIndex - sanitizedDefinitionStartIndex,
                                         remainingExperience,
                                         distribution,
                                         out definitionIndex,
                                         out definitionAmount,
                                         out fitsRemaining) == false)
                break;

            if (definitionIndex < 0 || definitionAmount <= 0f)
                break;

            deliveredExperience += definitionAmount;
            dropCount += 1;

            if (fitsRemaining == false)
                break;

            remainingExperience -= definitionAmount;
        }

        absoluteError = math.abs(math.max(0f, totalExperienceDrop) - deliveredExperience);
        return dropCount;
    }

    public static int EstimateDropsPerDeath(NativeArray<EnemyExperienceDropDefinitionElement> definitions,
                                            int definitionStartIndex,
                                            int definitionCount,
                                            float totalExperienceDrop,
                                            float distribution,
                                            out float deliveredExperience,
                                            out float absoluteError)
    {
        deliveredExperience = 0f;
        absoluteError = math.max(0f, totalExperienceDrop);

        int sanitizedDefinitionStartIndex = math.max(0, definitionStartIndex);
        int definitionEndIndex = math.min(definitions.Length, sanitizedDefinitionStartIndex + math.max(0, definitionCount));

        if (definitionEndIndex <= sanitizedDefinitionStartIndex)
            return 0;

        float remainingExperience = math.max(0f, totalExperienceDrop);

        if (remainingExperience <= PrecisionEpsilon)
        {
            absoluteError = 0f;
            return 0;
        }

        int dropCount = 0;

        for (int stepIndex = 0; stepIndex < MaxPlanSteps; stepIndex++)
        {
            if (remainingExperience <= PrecisionEpsilon)
                break;

            int definitionIndex;
            float definitionAmount;
            bool fitsRemaining;

            if (TryResolveNextDefinition(definitions,
                                         sanitizedDefinitionStartIndex,
                                         definitionEndIndex - sanitizedDefinitionStartIndex,
                                         remainingExperience,
                                         distribution,
                                         out definitionIndex,
                                         out definitionAmount,
                                         out fitsRemaining) == false)
                break;

            if (definitionIndex < 0 || definitionAmount <= 0f)
                break;

            deliveredExperience += definitionAmount;
            dropCount += 1;

            if (fitsRemaining == false)
                break;

            remainingExperience -= definitionAmount;
        }

        absoluteError = math.abs(math.max(0f, totalExperienceDrop) - deliveredExperience);
        return dropCount;
    }

    /// <summary>
    /// Estimates drop count and delivery error for editor preview values.
    /// </summary>
    /// <param name="definitionExperienceValues">Preview definition values collected from serialized properties.</param>
    /// <param name="totalExperienceDrop">Total experience budget to distribute.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="deliveredExperience">Estimated delivered experience by planned drops.</param>
    /// <param name="absoluteError">Absolute difference between requested and delivered experience.</param>
    /// <returns>Estimated planned drop count.</returns>
    public static int EstimateDropsForPreview(IReadOnlyList<float> definitionExperienceValues,
                                              float totalExperienceDrop,
                                              float distribution,
                                              out float deliveredExperience,
                                              out float absoluteError)
    {
        deliveredExperience = 0f;
        absoluteError = math.max(0f, totalExperienceDrop);

        if (definitionExperienceValues == null || definitionExperienceValues.Count <= 0)
            return 0;

        float remainingExperience = math.max(0f, totalExperienceDrop);

        if (remainingExperience <= PrecisionEpsilon)
        {
            absoluteError = 0f;
            return 0;
        }

        int dropCount = 0;

        for (int stepIndex = 0; stepIndex < MaxPlanSteps; stepIndex++)
        {
            if (remainingExperience <= PrecisionEpsilon)
                break;

            int definitionIndex;
            float definitionAmount;
            bool fitsRemaining;

            if (TryResolveNextPreviewDefinition(definitionExperienceValues,
                                                remainingExperience,
                                                distribution,
                                                out definitionIndex,
                                                out definitionAmount,
                                                out fitsRemaining) == false)
                break;

            if (definitionIndex < 0 || definitionAmount <= 0f)
                break;

            deliveredExperience += definitionAmount;
            dropCount += 1;

            if (fitsRemaining == false)
                break;

            remainingExperience -= definitionAmount;
        }

        absoluteError = math.abs(math.max(0f, totalExperienceDrop) - deliveredExperience);
        return dropCount;
    }

    /// <summary>
    /// Resolves the nearest compatible preview range endpoints contained inside a requested range.
    /// </summary>
    /// <param name="definitionExperienceValues">Preview definition values collected from serialized properties.</param>
    /// <param name="requestedMinimumTotal">Requested minimum total experience.</param>
    /// <param name="requestedMaximumTotal">Requested maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="resolvedMinimumTotal">Resolved compatible minimum total inside the requested range.</param>
    /// <param name="resolvedMaximumTotal">Resolved compatible maximum total inside the requested range.</param>
    /// <returns>True when at least one compatible value exists in the requested range.</returns>
    public static bool TryResolveCompatiblePreviewRange(IReadOnlyList<float> definitionExperienceValues,
                                                        float requestedMinimumTotal,
                                                        float requestedMaximumTotal,
                                                        float distribution,
                                                        out float resolvedMinimumTotal,
                                                        out float resolvedMaximumTotal)
    {
        return EnemyExperienceDropDistributionCompatibilityUtility.TryResolveCompatiblePreviewRange(definitionExperienceValues,
                                                                                                    requestedMinimumTotal,
                                                                                                    requestedMaximumTotal,
                                                                                                    distribution,
                                                                                                    out resolvedMinimumTotal,
                                                                                                    out resolvedMaximumTotal);
    }

    /// <summary>
    /// Resolves one suggested compatible preview range near the requested range without mutating inputs.
    /// </summary>
    /// <param name="definitionExperienceValues">Preview definition values collected from serialized properties.</param>
    /// <param name="requestedMinimumTotal">Requested minimum total experience.</param>
    /// <param name="requestedMaximumTotal">Requested maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="suggestedMinimumTotal">Suggested compatible minimum total.</param>
    /// <param name="suggestedMaximumTotal">Suggested compatible maximum total.</param>
    /// <returns>True when a nearby compatible range is found, otherwise false.</returns>
    public static bool TryResolveSuggestedPreviewRange(IReadOnlyList<float> definitionExperienceValues,
                                                       float requestedMinimumTotal,
                                                       float requestedMaximumTotal,
                                                       float distribution,
                                                       out float suggestedMinimumTotal,
                                                       out float suggestedMaximumTotal)
    {
        return EnemyExperienceDropDistributionCompatibilityUtility.TryResolveSuggestedPreviewRange(definitionExperienceValues,
                                                                                                   requestedMinimumTotal,
                                                                                                   requestedMaximumTotal,
                                                                                                   distribution,
                                                                                                   out suggestedMinimumTotal,
                                                                                                   out suggestedMaximumTotal);
    }

    /// <summary>
    /// Picks one random compatible total experience value inside the requested runtime range.
    /// </summary>
    /// <param name="definitions">Available runtime drop definitions.</param>
    /// <param name="minimumTotal">Requested minimum total experience.</param>
    /// <param name="maximumTotal">Requested maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="randomSeed">Deterministic random seed used for total-value selection.</param>
    /// <param name="resolvedTotal">Resolved compatible random total when successful.</param>
    /// <returns>True when a compatible random total is resolved, otherwise false.</returns>
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
    /// Picks one random compatible total experience value inside the requested runtime range for one module slice.
    /// </summary>
    /// <param name="definitions">Shared runtime drop definitions buffer.</param>
    /// <param name="definitionStartIndex">Inclusive module-slice start index.</param>
    /// <param name="definitionCount">Definition count owned by the module slice.</param>
    /// <param name="minimumTotal">Requested minimum total experience.</param>
    /// <param name="maximumTotal">Requested maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="randomSeed">Deterministic random seed used for total-value selection.</param>
    /// <param name="resolvedTotal">Resolved compatible random total when successful.</param>
    /// <returns>True when a compatible random total is resolved, otherwise false.</returns>
    public static bool TryResolveRandomCompatibleTotal(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                       int definitionStartIndex,
                                                       int definitionCount,
                                                       float minimumTotal,
                                                       float maximumTotal,
                                                       float distribution,
                                                       uint randomSeed,
                                                       out float resolvedTotal)
    {
        return EnemyExperienceDropDistributionCompatibilityUtility.TryResolveRandomCompatibleTotal(definitions,
                                                                                                   definitionStartIndex,
                                                                                                   definitionCount,
                                                                                                   minimumTotal,
                                                                                                   maximumTotal,
                                                                                                   distribution,
                                                                                                   randomSeed,
                                                                                                   out resolvedTotal);
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
        return EnemyExperienceDropDistributionCompatibilityUtility.TryResolveRandomCompatibleTotal(definitions,
                                                                                                   definitionStartIndex,
                                                                                                   definitionCount,
                                                                                                   minimumTotal,
                                                                                                   maximumTotal,
                                                                                                   distribution,
                                                                                                   randomSeed,
                                                                                                   out resolvedTotal);
    }
    #endregion

    #region Selection Helpers
    internal static bool TryResolveRuntimeBounds(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                 out float minimumValue,
                                                 out float maximumValue)
    {
        return TryResolveRuntimeBounds(definitions,
                                       0,
                                       definitions.Length,
                                       out minimumValue,
                                       out maximumValue);
    }

    internal static bool TryResolveRuntimeBounds(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                 int definitionStartIndex,
                                                 int definitionCount,
                                                 out float minimumValue,
                                                 out float maximumValue)
    {
        return EnemyExperienceDropDistributionCompatibilityUtility.TryResolveRuntimeBounds(definitions,
                                                                                           definitionStartIndex,
                                                                                           definitionCount,
                                                                                           out minimumValue,
                                                                                           out maximumValue);
    }

    internal static bool TryResolveRuntimeBounds(NativeArray<EnemyExperienceDropDefinitionElement> definitions,
                                                 int definitionStartIndex,
                                                 int definitionCount,
                                                 out float minimumValue,
                                                 out float maximumValue)
    {
        return EnemyExperienceDropDistributionCompatibilityUtility.TryResolveRuntimeBounds(definitions,
                                                                                           definitionStartIndex,
                                                                                           definitionCount,
                                                                                           out minimumValue,
                                                                                           out maximumValue);
    }

    internal static bool TryResolveNextPreviewDefinition(IReadOnlyList<float> definitionExperienceValues,
                                                         float remainingExperience,
                                                         float distribution,
                                                         out int definitionIndex,
                                                         out float definitionExperienceAmount,
                                                         out bool fitsRemaining)
    {
        definitionIndex = -1;
        definitionExperienceAmount = 0f;
        fitsRemaining = false;

        if (definitionExperienceValues == null || definitionExperienceValues.Count <= 0)
            return false;

        float minimumValue;
        float maximumValue;

        if (!EnemyExperienceDropDistributionCompatibilityUtility.TryResolvePreviewBounds(definitionExperienceValues,
                                                                                         out minimumValue,
                                                                                         out maximumValue))
        {
            return false;
        }

        float clampedDistribution = math.clamp(distribution, 0f, 1f);
        float targetValue = math.lerp(minimumValue, maximumValue, clampedDistribution);
        int bestIndex = -1;
        float bestAmount = 0f;
        float bestScore = float.MinValue;

        for (int index = 0; index < definitionExperienceValues.Count; index++)
        {
            float amount = definitionExperienceValues[index];

            if (amount <= 0f)
                continue;

            if (amount > remainingExperience + PrecisionEpsilon)
                continue;

            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (!IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution))
                continue;

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
        }

        if (bestIndex >= 0)
        {
            definitionIndex = bestIndex;
            definitionExperienceAmount = bestAmount;
            fitsRemaining = true;
            return true;
        }

        float bestAbsoluteDifference = float.MaxValue;

        for (int index = 0; index < definitionExperienceValues.Count; index++)
        {
            float amount = definitionExperienceValues[index];

            if (amount <= 0f)
                continue;

            float absoluteDifference = math.abs(amount - remainingExperience);
            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (absoluteDifference > bestAbsoluteDifference + PrecisionEpsilon)
                continue;

            if (math.abs(absoluteDifference - bestAbsoluteDifference) <= PrecisionEpsilon &&
                !IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution))
            {
                continue;
            }

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
            bestAbsoluteDifference = absoluteDifference;
        }

        if (bestIndex < 0)
            return false;

        definitionIndex = bestIndex;
        definitionExperienceAmount = bestAmount;
        fitsRemaining = bestAmount <= remainingExperience + PrecisionEpsilon;
        return true;
    }

    private static float ComputeSelectionScore(float value,
                                               float minimumValue,
                                               float maximumValue,
                                               float targetValue,
                                               float clampedDistribution)
    {
        float valueRange = math.max(PrecisionEpsilon, maximumValue - minimumValue);
        float normalizedValue = math.clamp((value - minimumValue) / valueRange, 0f, 1f);
        float directionalScore = math.lerp(1f - normalizedValue, normalizedValue, clampedDistribution);
        float proximityScore = 1f - math.clamp(math.abs(value - targetValue) / valueRange, 0f, 1f);
        return directionalScore * 0.75f + proximityScore * 0.25f;
    }

    private static bool IsBetterSelection(float candidateScore,
                                          float candidateAmount,
                                          float currentScore,
                                          float currentAmount,
                                          float clampedDistribution)
    {
        if (candidateScore > currentScore + PrecisionEpsilon)
            return true;

        if (math.abs(candidateScore - currentScore) > PrecisionEpsilon)
            return false;

        if (clampedDistribution >= 0.5f)
            return candidateAmount > currentAmount + PrecisionEpsilon;

        return candidateAmount < currentAmount - PrecisionEpsilon || currentAmount <= 0f;
    }
    #endregion

    #endregion
}
