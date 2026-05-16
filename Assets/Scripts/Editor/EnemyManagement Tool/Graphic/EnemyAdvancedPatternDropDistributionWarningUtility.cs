using System.Collections.Generic;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine.UIElements;

/// <summary>
/// Builds and refreshes editor warnings for DropItems experience distribution compatibility.
/// </summary>
internal static class EnemyAdvancedPatternDropDistributionWarningUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes warning state for DropItems experience range incompatibility.
    /// </summary>
    /// <param name="dropDefinitionsProperty">Experience drop definitions list property.</param>
    /// <param name="minimumTotalExperienceDropProperty">Minimum total experience property.</param>
    /// <param name="maximumTotalExperienceDropProperty">Maximum total experience property.</param>
    /// <param name="distributionProperty">Distribution slider property.</param>
    /// <param name="warningBox">Warning UI element.</param>

    public static void RefreshDropItemsDistributionWarning(SerializedProperty dropDefinitionsProperty,
                                                           SerializedProperty minimumTotalExperienceDropProperty,
                                                           SerializedProperty maximumTotalExperienceDropProperty,
                                                           SerializedProperty distributionProperty,
                                                           HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        warningBox.style.display = DisplayStyle.None;
        warningBox.text = string.Empty;

        if (dropDefinitionsProperty == null ||
            minimumTotalExperienceDropProperty == null ||
            maximumTotalExperienceDropProperty == null ||
            distributionProperty == null)
            return;

        if (minimumTotalExperienceDropProperty.propertyType != SerializedPropertyType.Float ||
            maximumTotalExperienceDropProperty.propertyType != SerializedPropertyType.Float ||
            distributionProperty.propertyType != SerializedPropertyType.Float)
            return;

        float rawMinimumTotalExperienceDrop = minimumTotalExperienceDropProperty.floatValue;
        float rawMaximumTotalExperienceDrop = maximumTotalExperienceDropProperty.floatValue;
        bool hasInvertedRange = rawMaximumTotalExperienceDrop < rawMinimumTotalExperienceDrop;
        bool hasNegativeRangeValue = rawMinimumTotalExperienceDrop < 0f || rawMaximumTotalExperienceDrop < 0f;
        float minimumTotalExperienceDrop = math.max(0f, rawMinimumTotalExperienceDrop);
        float maximumTotalExperienceDrop = math.max(0f, rawMaximumTotalExperienceDrop);
        float distribution = math.clamp(distributionProperty.floatValue, 0f, 1f);
        List<float> definitionValues = BuildDropDefinitionValues(dropDefinitionsProperty);

        if (hasNegativeRangeValue)
        {
            warningBox.text = "Invalid range: Min and Max must be greater or equal to 0.";
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        if (hasInvertedRange)
        {
            string suggestedRangeSuffix = BuildSuggestedRangeSuffix(definitionValues,
                                                                    minimumTotalExperienceDrop,
                                                                    maximumTotalExperienceDrop,
                                                                    distribution);
            warningBox.text = string.Format("Invalid range: Max must be greater or equal to Min.{0}", suggestedRangeSuffix);
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        if (maximumTotalExperienceDrop <= 0f)
        {
            string suggestedRangeSuffix = BuildSuggestedRangeSuffix(definitionValues,
                                                                    minimumTotalExperienceDrop,
                                                                    math.max(minimumTotalExperienceDrop, 1f),
                                                                    distribution);
            warningBox.text = string.Format("Invalid range: Max must be greater than 0 to drop experience.{0}", suggestedRangeSuffix);
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        if (definitionValues.Count <= 0)
        {
            warningBox.text = "No valid drop definition is available: assign at least one entry with positive Experience Amount.";
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        float resolvedMinimumTotal;
        float resolvedMaximumTotal;

        if (EnemyExperienceDropDistributionUtility.TryResolveCompatiblePreviewRange(definitionValues,
                                                                                    minimumTotalExperienceDrop,
                                                                                    maximumTotalExperienceDrop,
                                                                                    distribution,
                                                                                    out resolvedMinimumTotal,
                                                                                    out resolvedMaximumTotal))
        {
            if (AreRangeEndpointsCompatible(minimumTotalExperienceDrop,
                                            maximumTotalExperienceDrop,
                                            resolvedMinimumTotal,
                                            resolvedMaximumTotal))
                return;

            warningBox.text = BuildRangeNotOptimalWarning(minimumTotalExperienceDrop,
                                                          maximumTotalExperienceDrop,
                                                          resolvedMinimumTotal,
                                                          resolvedMaximumTotal);
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        float suggestedMinimumTotal;
        float suggestedMaximumTotal;

        if (EnemyExperienceDropDistributionUtility.TryResolveSuggestedPreviewRange(definitionValues,
                                                                                   minimumTotalExperienceDrop,
                                                                                   maximumTotalExperienceDrop,
                                                                                   distribution,
                                                                                   out suggestedMinimumTotal,
                                                                                   out suggestedMaximumTotal))
        {
            warningBox.text = string.Format("No compatible total experience value exists in the current Min/Max range. Suggested compatible range: {0:0.###} - {1:0.###} XP.",
                                            suggestedMinimumTotal,
                                            suggestedMaximumTotal);
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        warningBox.text = string.Format("No compatible total experience value exists with the current drop definitions and distribution settings.{0}",
                                        BuildSuggestedRangeSuffix(definitionValues,
                                                                  minimumTotalExperienceDrop,
                                                                  maximumTotalExperienceDrop,
                                                                  distribution));
        warningBox.style.display = DisplayStyle.Flex;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Checks whether requested range endpoints are already compatible with resolved range endpoints.
    /// </summary>
    /// <param name="requestedMinimumTotal">Requested minimum total experience.</param>
    /// <param name="requestedMaximumTotal">Requested maximum total experience.</param>
    /// <param name="resolvedMinimumTotal">Resolved compatible minimum total experience.</param>
    /// <param name="resolvedMaximumTotal">Resolved compatible maximum total experience.</param>
    /// <returns>Returns true when both endpoints are compatible.</returns>
    private static bool AreRangeEndpointsCompatible(float requestedMinimumTotal,
                                                    float requestedMaximumTotal,
                                                    float resolvedMinimumTotal,
                                                    float resolvedMaximumTotal)
    {
        const float EndpointCompatibilityTolerance = 0.0001f;
        bool minimumIsCompatible = math.abs(requestedMinimumTotal - resolvedMinimumTotal) <= EndpointCompatibilityTolerance;
        bool maximumIsCompatible = math.abs(requestedMaximumTotal - resolvedMaximumTotal) <= EndpointCompatibilityTolerance;
        return minimumIsCompatible && maximumIsCompatible;
    }

    /// <summary>
    /// Builds warning text when Min or Max is not directly compatible with current distribution.
    /// </summary>
    /// <param name="requestedMinimumTotal">Requested minimum total experience.</param>
    /// <param name="requestedMaximumTotal">Requested maximum total experience.</param>
    /// <param name="resolvedMinimumTotal">Resolved compatible minimum total experience.</param>
    /// <param name="resolvedMaximumTotal">Resolved compatible maximum total experience.</param>
    /// <returns>Returns one warning string describing which endpoint is incompatible.</returns>
    private static string BuildRangeNotOptimalWarning(float requestedMinimumTotal,
                                                      float requestedMaximumTotal,
                                                      float resolvedMinimumTotal,
                                                      float resolvedMaximumTotal)
    {
        const float EndpointCompatibilityTolerance = 0.0001f;
        bool minimumIsCompatible = math.abs(requestedMinimumTotal - resolvedMinimumTotal) <= EndpointCompatibilityTolerance;
        bool maximumIsCompatible = math.abs(requestedMaximumTotal - resolvedMaximumTotal) <= EndpointCompatibilityTolerance;

        if (!minimumIsCompatible && !maximumIsCompatible)
            return string.Format("Range not optimal: Min and Max are not compatible with current distribution. Compatible range inside selection: {0:0.###} - {1:0.###} XP.",
                                 resolvedMinimumTotal,
                                 resolvedMaximumTotal);

        if (!minimumIsCompatible)
            return string.Format("Range not optimal: Min is not compatible with current distribution. Suggested Min: {0:0.###} XP. Compatible range inside selection: {0:0.###} - {1:0.###} XP.",
                                 resolvedMinimumTotal,
                                 resolvedMaximumTotal);

        return string.Format("Range not optimal: Max is not compatible with current distribution. Suggested Max: {0:0.###} XP. Compatible range inside selection: {1:0.###} - {0:0.###} XP.",
                             resolvedMaximumTotal,
                             resolvedMinimumTotal);
    }

    /// <summary>
    /// Builds warning suffix text containing one suggested compatible range when available.
    /// </summary>
    /// <param name="definitionValues">Positive drop definition values with assigned prefabs.</param>
    /// <param name="requestedMinimumTotal">Requested minimum total experience.</param>
    /// <param name="requestedMaximumTotal">Requested maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <returns>Suggested range suffix text, or empty when unavailable.</returns>
    private static string BuildSuggestedRangeSuffix(IReadOnlyList<float> definitionValues,
                                                    float requestedMinimumTotal,
                                                    float requestedMaximumTotal,
                                                    float distribution)
    {
        if (definitionValues == null || definitionValues.Count <= 0)
            return string.Empty;

        float suggestedMinimumTotal;
        float suggestedMaximumTotal;

        if (!EnemyExperienceDropDistributionUtility.TryResolveSuggestedPreviewRange(definitionValues,
                                                                                   requestedMinimumTotal,
                                                                                   requestedMaximumTotal,
                                                                                   distribution,
                                                                                   out suggestedMinimumTotal,
                                                                                   out suggestedMaximumTotal))
            return string.Empty;

        return string.Format(" Suggested compatible range: {0:0.###} - {1:0.###} XP.",
                             suggestedMinimumTotal,
                             suggestedMaximumTotal);
    }

    /// <summary>
    /// Builds a list of positive Experience Amount values from serialized drop definitions.
    /// </summary>
    /// <param name="dropDefinitionsProperty">Drop definitions serialized array.</param>
    /// <returns>Returns positive definition values used by warning estimation.</returns>
    private static List<float> BuildDropDefinitionValues(SerializedProperty dropDefinitionsProperty)
    {
        List<float> values = new List<float>();

        if (dropDefinitionsProperty == null || !dropDefinitionsProperty.isArray)
            return values;

        for (int definitionIndex = 0; definitionIndex < dropDefinitionsProperty.arraySize; definitionIndex++)
        {
            SerializedProperty definitionProperty = dropDefinitionsProperty.GetArrayElementAtIndex(definitionIndex);

            if (definitionProperty == null)
                continue;

            SerializedProperty amountProperty = definitionProperty.FindPropertyRelative("experienceAmount");
            SerializedProperty prefabProperty = definitionProperty.FindPropertyRelative("dropPrefab");

            if (amountProperty == null || amountProperty.propertyType != SerializedPropertyType.Float)
                continue;

            if (prefabProperty == null || prefabProperty.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            if (prefabProperty.objectReferenceValue == null)
                continue;

            float value = amountProperty.floatValue;

            if (value <= 0f)
                continue;

            values.Add(value);
        }

        return values;
    }
    #endregion

    #endregion
}
