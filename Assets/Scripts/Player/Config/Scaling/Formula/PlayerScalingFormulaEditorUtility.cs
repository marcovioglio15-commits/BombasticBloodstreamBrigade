#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;

/// <summary>
/// Provides editor-only helpers for typed Add Scaling targets, enum formula constants and compact helper text.
/// </summary>
public static class PlayerScalingFormulaEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether Add Scaling can be offered for the provided serialized property.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>True when the property can host a formula-based scaling rule.</returns>
    public static bool SupportsScalingTarget(SerializedProperty property)
    {
        if (property == null)
            return false;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Float:
            case SerializedPropertyType.Boolean:
            case SerializedPropertyType.String:
            case SerializedPropertyType.Enum:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves the formula result type required by the current serialized target property.
    /// </summary>
    /// <param name="property">Serialized target property.</param>
    /// <returns>Required formula result type for validation.</returns>
    public static PlayerFormulaValueType ResolveRequiredResultType(SerializedProperty property)
    {
        if (property == null)
            return PlayerFormulaValueType.Invalid;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return PlayerFormulaValueType.Boolean;
            case SerializedPropertyType.String:
                return PlayerFormulaValueType.Token;
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Float:
            case SerializedPropertyType.Enum:
                return PlayerFormulaValueType.Number;
            default:
                return PlayerFormulaValueType.Invalid;
        }
    }

    /// <summary>
    /// Normalizes one formula so enum-member bracket tokens become numeric constants for the current enum target.
    /// </summary>
    /// <param name="formula">Raw authored formula.</param>
    /// <param name="targetProperty">Serialized property currently receiving Add Scaling.</param>
    /// <param name="allowedVariables">Known scalable-stat variables that must keep bracket syntax.</param>
    /// <returns>Formula normalized for validation and bake-time/runtime evaluation.</returns>
    public static string NormalizeFormulaForTarget(string formula,
                                                   SerializedProperty targetProperty,
                                                   ISet<string> allowedVariables)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return string.Empty;

        if (PlayerLaserBeamVisualPresetEditorUtility.IsSelectorProperty(targetProperty))
            return PlayerLaserBeamVisualPresetEditorUtility.NormalizeFormulaTokens(formula, targetProperty, allowedVariables);

        if (targetProperty == null || targetProperty.propertyType != SerializedPropertyType.Enum)
            return formula;

        StringBuilder normalizedFormulaBuilder = new StringBuilder(formula.Length + 16);
        int parseIndex = 0;

        while (parseIndex < formula.Length)
        {
            int openBracketIndex = formula.IndexOf('[', parseIndex);

            if (openBracketIndex < 0)
            {
                normalizedFormulaBuilder.Append(formula.Substring(parseIndex));
                break;
            }

            if (openBracketIndex > parseIndex)
                normalizedFormulaBuilder.Append(formula.Substring(parseIndex, openBracketIndex - parseIndex));

            int closeBracketIndex = formula.IndexOf(']', openBracketIndex + 1);

            if (closeBracketIndex < 0)
            {
                normalizedFormulaBuilder.Append(formula.Substring(openBracketIndex));
                break;
            }

            string token = formula.Substring(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1).Trim();

            if (string.Equals(token, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase) ||
                allowedVariables != null && allowedVariables.Contains(token) ||
                !TryResolveEnumConstantValue(targetProperty, token, out int enumValue))
            {
                normalizedFormulaBuilder.Append(formula.Substring(openBracketIndex, closeBracketIndex - openBracketIndex + 1));
            }
            else
            {
                normalizedFormulaBuilder.Append(enumValue.ToString(CultureInfo.InvariantCulture));
            }

            parseIndex = closeBracketIndex + 1;
        }

        return normalizedFormulaBuilder.ToString();
    }

    /// <summary>
    /// Builds the helper text shown below a formula field, including scoped scalable stats and enum constants when relevant.
    /// </summary>
    /// <param name="allowedVariables">Available scalable-stat names in the current scope.</param>
    /// <param name="variableTypes">Optional precise scalable-stat types keyed by stat name.</param>
    /// <param name="targetProperty">Formula target property used to append contextual constants.</param>
    /// <returns>Compact multi-line helper text.</returns>
    public static string BuildHelperText(ISet<string> allowedVariables,
                                         IReadOnlyDictionary<string, PlayerScalableStatType> variableTypes,
                                         SerializedProperty targetProperty)
    {
        string availableVariablesText = BuildAvailableVariablesLabelText(allowedVariables, variableTypes);
        string laserBeamVisualPresetText = BuildLaserBeamVisualPresetLabelText(targetProperty);
        string enumValuesText = BuildEnumValuesLabelText(targetProperty);

        if (string.IsNullOrWhiteSpace(laserBeamVisualPresetText) && string.IsNullOrWhiteSpace(enumValuesText))
            return availableVariablesText;

        if (string.IsNullOrWhiteSpace(laserBeamVisualPresetText))
            return availableVariablesText + Environment.NewLine + enumValuesText;

        if (string.IsNullOrWhiteSpace(enumValuesText))
            return availableVariablesText + Environment.NewLine + laserBeamVisualPresetText;

        return availableVariablesText + Environment.NewLine + laserBeamVisualPresetText + Environment.NewLine + enumValuesText;
    }

    /// <summary>
    /// Builds a compact label describing the enum members available as bracket constants for the current target.
    /// </summary>
    /// <param name="targetProperty">Current target property.</param>
    /// <returns>Empty string when the target is not an enum; otherwise a formatted helper line.</returns>
    public static string BuildEnumValuesLabelText(SerializedProperty targetProperty)
    {
        if (targetProperty == null || targetProperty.propertyType != SerializedPropertyType.Enum)
            return string.Empty;

        string[] enumNames = targetProperty.enumNames;

        if (enumNames == null || enumNames.Length == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder(enumNames.Length * 12);
        builder.Append("Enum Values: ");

        for (int index = 0; index < enumNames.Length; index++)
        {
            if (index > 0)
                builder.Append(", ");

            builder.Append('[');
            builder.Append(enumNames[index]);
            builder.Append(']');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Resolves whether the target property is currently an enum-backed Add Scaling field.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>True when the property is an enum.</returns>
    public static bool IsEnumTarget(SerializedProperty property)
    {
        return property != null && property.propertyType == SerializedPropertyType.Enum;
    }

    /// <summary>
    /// Resolves whether the target property is currently a boolean-backed Add Scaling field.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>True when the property is a boolean.</returns>
    public static bool IsBooleanTarget(SerializedProperty property)
    {
        return property != null && property.propertyType == SerializedPropertyType.Boolean;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the helper line that exposes Laser Beam visual preset constants for selector fields.
    /// </summary>
    /// <param name="targetProperty">Current formula target property.</param>
    /// <returns>Helper label line, or empty when the target is not a Laser Beam visual preset selector.</returns>
    private static string BuildLaserBeamVisualPresetLabelText(SerializedProperty targetProperty)
    {
        if (!PlayerLaserBeamVisualPresetEditorUtility.IsSelectorProperty(targetProperty))
            return string.Empty;

        List<PlayerLaserBeamVisualPresetEditorOption> options = PlayerLaserBeamVisualPresetEditorUtility.BuildOptions();
        return PlayerLaserBeamVisualPresetEditorUtility.BuildHelperText(options);
    }

    private static bool TryResolveEnumConstantValue(SerializedProperty targetProperty,
                                                    string token,
                                                    out int enumValue)
    {
        enumValue = 0;

        if (targetProperty == null || targetProperty.propertyType != SerializedPropertyType.Enum)
            return false;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        string[] enumNames = targetProperty.enumNames;
        string[] enumDisplayNames = targetProperty.enumDisplayNames;

        if (enumNames == null || enumNames.Length == 0)
            return false;

        for (int index = 0; index < enumNames.Length; index++)
        {
            string enumName = enumNames[index];

            if (string.Equals(enumName, token, StringComparison.OrdinalIgnoreCase))
            {
                enumValue = index;
                return true;
            }

            if (enumDisplayNames == null || index >= enumDisplayNames.Length)
                continue;

            string displayName = string.IsNullOrWhiteSpace(enumDisplayNames[index])
                ? string.Empty
                : enumDisplayNames[index].Replace(" ", string.Empty);

            if (!string.Equals(displayName, token, StringComparison.OrdinalIgnoreCase))
                continue;

            enumValue = index;
            return true;
        }

        return false;
    }

    private static string BuildAvailableVariablesLabelText(ISet<string> allowedVariables,
                                                           IReadOnlyDictionary<string, PlayerScalableStatType> variableTypes)
    {
        if (allowedVariables == null || allowedVariables.Count == 0)
            return "Available Variables: [this]";

        List<string> sortedVariables = new List<string>(allowedVariables);
        sortedVariables.Sort(StringComparer.OrdinalIgnoreCase);

        if (sortedVariables.Count == 1)
        {
            string singleVariableName = sortedVariables[0];

            if (variableTypes != null && variableTypes.TryGetValue(singleVariableName, out PlayerScalableStatType singleVariableType))
            {
                return string.Format("Available Variables: [this], [{0}:{1}]",
                                     singleVariableName,
                                     PlayerScalableStatTypeUtility.BuildDisplayLabel(singleVariableType));
            }

            return string.Format("Available Variables: [this], [{0}]", singleVariableName);
        }

        StringBuilder builder = new StringBuilder(sortedVariables.Count * 18);
        builder.Append("Available Variables: [this], ");

        for (int index = 0; index < sortedVariables.Count; index++)
        {
            if (index > 0)
                builder.Append(", ");

            string variableName = sortedVariables[index];

            if (variableTypes != null && variableTypes.TryGetValue(variableName, out PlayerScalableStatType variableType))
            {
                builder.Append('[');
                builder.Append(variableName);
                builder.Append(':');
                builder.Append(PlayerScalableStatTypeUtility.BuildDisplayLabel(variableType));
                builder.Append(']');
                continue;
            }

            builder.Append('[');
            builder.Append(variableName);
            builder.Append(']');
        }

        return builder.ToString();
    }
    #endregion

    #endregion
}
#endif
