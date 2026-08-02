using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds validation warnings for Player scalable-stat authoring and cross-system dependencies.
/// </summary>
public static class PlayerProgressionScalableStatsWarningsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds warnings for value ranges, types, duplicate names and formula dependency cycles.
    /// </summary>
    /// <param name="warningsRoot">UI Toolkit container receiving the current warning set.</param>
    /// <param name="scalableStatsProperty">Serialized scalable-stat definition collection.</param>
    /// <param name="scalingRulesProperty">Serialized scaling-rule collection used for dependency analysis.</param>
    public static void Refresh(VisualElement warningsRoot,
                               SerializedProperty scalableStatsProperty,
                               SerializedProperty scalingRulesProperty)
    {
        if (warningsRoot == null || scalableStatsProperty == null)
            return;

        warningsRoot.Clear();

        // Validate every authored stat independently before evaluating graph dependencies.
        for (int statIndex = 0; statIndex < scalableStatsProperty.arraySize; statIndex++)
        {
            SerializedProperty statElementProperty = scalableStatsProperty.GetArrayElementAtIndex(statIndex);
            SerializedProperty statNameProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("statName") : null;
            string statName = statNameProperty != null ? statNameProperty.stringValue : string.Empty;
            AddWarning(warningsRoot,
                       ValidateScalableStatEntry(statName, statIndex, scalableStatsProperty),
                       string.Format("Stat {0}: ", statIndex + 1));
        }

        AddWarnings(warningsRoot,
                    PlayerScalingDependencyValidationUtility.BuildScalableStatsDependencyWarnings(scalableStatsProperty,
                                                                                                      scalingRulesProperty));
        AddWarnings(warningsRoot,
                    PlayerScalingDependencyValidationUtility.BuildDifficultyCrossDependencyWarnings(scalableStatsProperty,
                                                                                                       scalingRulesProperty));
    }
    #endregion

    #region Warning Methods
    /// <summary>
    /// Adds every non-empty warning message as a consistently styled help box.
    /// </summary>
    /// <param name="warningsRoot">UI Toolkit container receiving warning boxes.</param>
    /// <param name="warnings">Warning messages to render.</param>
    private static void AddWarnings(VisualElement warningsRoot, IReadOnlyList<string> warnings)
    {
        // Preserve dependency utility ordering so related graph diagnostics remain grouped.
        for (int warningIndex = 0; warningIndex < warnings.Count; warningIndex++)
            AddWarning(warningsRoot, warnings[warningIndex], string.Empty);
    }

    /// <summary>
    /// Adds one non-empty warning with an optional contextual prefix.
    /// </summary>
    /// <param name="warningsRoot">UI Toolkit container receiving the warning box.</param>
    /// <param name="warning">Warning text to render.</param>
    /// <param name="prefix">Context text prepended to the warning.</param>
    private static void AddWarning(VisualElement warningsRoot, string warning, string prefix)
    {
        if (string.IsNullOrWhiteSpace(warning))
            return;

        HelpBox warningBox = new HelpBox(prefix + warning, HelpBoxMessageType.Warning);
        warningBox.style.marginBottom = 2f;
        warningsRoot.Add(warningBox);
    }

    /// <summary>
    /// Validates one scalable-stat entry without mutating its serialized authoring values.
    /// </summary>
    /// <param name="statName">Authored stat identifier.</param>
    /// <param name="statIndex">Index of the stat in the serialized collection.</param>
    /// <param name="scalableStatsProperty">Owning serialized scalable-stat collection.</param>
    /// <returns>Newline-separated warnings, or an empty string when the entry is valid.</returns>
    private static string ValidateScalableStatEntry(string statName,
                                                    int statIndex,
                                                    SerializedProperty scalableStatsProperty)
    {
        List<string> warnings = new List<string>();

        if (!PlayerScalableStatNameUtility.IsValid(statName))
            warnings.Add("Invalid name. Use letters/digits/underscore, start with letter or underscore, and avoid 'this'.");

        SerializedProperty statElementProperty = scalableStatsProperty.GetArrayElementAtIndex(statIndex);
        SerializedProperty statTypeProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("statType") : null;
        SerializedProperty defaultValueProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("defaultValue") : null;
        SerializedProperty minimumValueProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("minimumValue") : null;
        SerializedProperty maximumValueProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("maximumValue") : null;
        SerializedProperty defaultTokenValueProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("defaultTokenValue") : null;
        PlayerScalableStatType statType = statTypeProperty != null
            ? (PlayerScalableStatType)statTypeProperty.enumValueIndex
            : PlayerScalableStatType.Float;

        ValidateNumericRange(warnings,
                             statType,
                             defaultValueProperty,
                             minimumValueProperty,
                             maximumValueProperty);
        ValidateDiscreteValues(warnings,
                               statType,
                               defaultValueProperty,
                               minimumValueProperty,
                               maximumValueProperty);
        ValidateToken(warnings, statType, defaultTokenValueProperty);
        ValidateDuplicateName(warnings, statName, statIndex, scalableStatsProperty);
        return string.Join(Environment.NewLine, warnings);
    }

    /// <summary>
    /// Validates ordered numeric bounds and the default value against the effective runtime range.
    /// </summary>
    /// <param name="warnings">Mutable warning collection.</param>
    /// <param name="statType">Authored scalable-stat type.</param>
    /// <param name="defaultValueProperty">Serialized default numeric value.</param>
    /// <param name="minimumValueProperty">Serialized minimum numeric value.</param>
    /// <param name="maximumValueProperty">Serialized maximum numeric value.</param>
    private static void ValidateNumericRange(List<string> warnings,
                                             PlayerScalableStatType statType,
                                             SerializedProperty defaultValueProperty,
                                             SerializedProperty minimumValueProperty,
                                             SerializedProperty maximumValueProperty)
    {
        if ((statType != PlayerScalableStatType.Float &&
             statType != PlayerScalableStatType.Integer &&
             statType != PlayerScalableStatType.Unsigned) ||
            minimumValueProperty == null ||
            maximumValueProperty == null)
            return;

        float minimumValue = minimumValueProperty.floatValue;
        float maximumValue = maximumValueProperty.floatValue;

        if (minimumValue > maximumValue)
            warnings.Add("Min is above Max. Runtime uses the ordered pair without snapping authoring values.");

        if (defaultValueProperty == null)
            return;

        PlayerScalableStatClampUtility.ResolveOrderedRange(minimumValue,
                                                           maximumValue,
                                                           out float resolvedMinimumValue,
                                                           out float resolvedMaximumValue);

        if (defaultValueProperty.floatValue < resolvedMinimumValue || defaultValueProperty.floatValue > resolvedMaximumValue)
            warnings.Add("Default Value is outside the configured clamp range and will be clamped only at runtime.");
    }

    /// <summary>
    /// Validates integer and unsigned constraints without sanitizing serialized values.
    /// </summary>
    /// <param name="warnings">Mutable warning collection.</param>
    /// <param name="statType">Authored scalable-stat type.</param>
    /// <param name="defaultValueProperty">Serialized default numeric value.</param>
    /// <param name="minimumValueProperty">Serialized minimum numeric value.</param>
    /// <param name="maximumValueProperty">Serialized maximum numeric value.</param>
    private static void ValidateDiscreteValues(List<string> warnings,
                                               PlayerScalableStatType statType,
                                               SerializedProperty defaultValueProperty,
                                               SerializedProperty minimumValueProperty,
                                               SerializedProperty maximumValueProperty)
    {
        if (statType != PlayerScalableStatType.Integer && statType != PlayerScalableStatType.Unsigned)
            return;

        if (statType == PlayerScalableStatType.Unsigned)
        {
            if (defaultValueProperty != null && defaultValueProperty.floatValue < 0f)
                warnings.Add("Default Value is negative on an Unsigned stat and will be clamped only at runtime.");

            if (minimumValueProperty != null && minimumValueProperty.floatValue < 0f)
                warnings.Add("Min is negative on an Unsigned stat. Runtime will still enforce a zero lower bound.");

            if (maximumValueProperty != null && maximumValueProperty.floatValue < 0f)
                warnings.Add("Max is negative on an Unsigned stat and will collapse to zero at runtime.");
        }

        if (defaultValueProperty != null && HasFractionalPart(defaultValueProperty.floatValue))
            warnings.Add("Default Value has decimals on a discrete stat and will be rounded only at runtime.");

        if (minimumValueProperty != null && HasFractionalPart(minimumValueProperty.floatValue))
            warnings.Add("Min has decimals on a discrete stat and may produce ambiguous runtime bounds.");

        if (maximumValueProperty != null && HasFractionalPart(maximumValueProperty.floatValue))
            warnings.Add("Max has decimals on a discrete stat and may produce ambiguous runtime bounds.");
    }

    /// <summary>
    /// Validates FixedString capacity for token-type default values.
    /// </summary>
    /// <param name="warnings">Mutable warning collection.</param>
    /// <param name="statType">Authored scalable-stat type.</param>
    /// <param name="defaultTokenValueProperty">Serialized default token value.</param>
    private static void ValidateToken(List<string> warnings,
                                      PlayerScalableStatType statType,
                                      SerializedProperty defaultTokenValueProperty)
    {
        if (statType != PlayerScalableStatType.Token || defaultTokenValueProperty == null)
            return;

        string tokenValue = string.IsNullOrWhiteSpace(defaultTokenValueProperty.stringValue)
            ? string.Empty
            : defaultTokenValueProperty.stringValue.Trim();

        if (Encoding.UTF8.GetByteCount(tokenValue) > 61)
            warnings.Add("Default token value exceeds runtime FixedString64Bytes capacity and will not be writable at runtime.");
    }

    /// <summary>
    /// Detects case-insensitive duplicate stat identifiers in the owning collection.
    /// </summary>
    /// <param name="warnings">Mutable warning collection.</param>
    /// <param name="statName">Current stat identifier.</param>
    /// <param name="statIndex">Current stat index excluded from comparison.</param>
    /// <param name="scalableStatsProperty">Owning serialized scalable-stat collection.</param>
    private static void ValidateDuplicateName(List<string> warnings,
                                              string statName,
                                              int statIndex,
                                              SerializedProperty scalableStatsProperty)
    {
        // Compare every sibling while excluding the current entry.
        for (int index = 0; index < scalableStatsProperty.arraySize; index++)
        {
            if (index == statIndex)
                continue;

            SerializedProperty otherStatElement = scalableStatsProperty.GetArrayElementAtIndex(index);
            SerializedProperty otherStatNameProperty = otherStatElement != null ? otherStatElement.FindPropertyRelative("statName") : null;

            if (otherStatNameProperty == null)
                continue;

            if (!string.Equals(otherStatNameProperty.stringValue, statName, StringComparison.OrdinalIgnoreCase))
                continue;

            warnings.Add("Duplicate name.");
            return;
        }
    }

    /// <summary>
    /// Detects whether a numeric value contains a meaningful fractional component.
    /// </summary>
    /// <param name="value">Numeric authoring value to inspect.</param>
    /// <returns>True when the value is not effectively integral, otherwise false.</returns>
    private static bool HasFractionalPart(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value)) > 0.0001f;
    }
    #endregion

    #endregion
}
