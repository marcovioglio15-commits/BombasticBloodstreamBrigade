#if UNITY_EDITOR
using System;
using System.Globalization;
using UnityEditor;

/// <summary>
/// Resolves semantic identifiers used to keep Add Scaling array keys stable across authored list reordering.
/// </summary>
internal static class PlayerScalingStableIdUtility
{
    #region Constants
    private static readonly string[] StableStringIdPropertyNames =
    {
        "powerUpId",
        "moduleId",
        "bindingId",
        "presetId",
        "statName",
        "scheduleId",
        "phaseID",
        "rankId",
        "milestoneId",
        "passivePowerUpId",
        "weaponId",
        "animationId"
    };

    private static readonly string[] StableNestedStringIdPropertyPaths =
    {
        "commonData.powerUpId"
    };

    private static readonly string[] StableIntegerIdPropertyNames =
    {
        "milestoneLevel",
        "stepIndex"
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the serialized property used as semantic identifier for one array element.
    /// </summary>
    /// <param name="arrayElement">Array element that owns the identifier field.</param>
    /// <param name="idPropertyName">Stable token name stored inside the scaling key.</param>
    /// <returns>Matching serialized identifier property when found; otherwise null.</returns>
    public static SerializedProperty ResolveIdProperty(SerializedProperty arrayElement, string idPropertyName)
    {
        if (arrayElement == null || string.IsNullOrWhiteSpace(idPropertyName))
            return null;

        SerializedProperty directProperty = arrayElement.FindPropertyRelative(idPropertyName);

        if (directProperty != null)
            return directProperty;

        // Nested IDs are flattened to their terminal field name inside the stable token.
        for (int candidateIndex = 0; candidateIndex < StableNestedStringIdPropertyPaths.Length; candidateIndex++)
        {
            string candidatePath = StableNestedStringIdPropertyPaths[candidateIndex];

            if (!string.Equals(ResolveTokenName(candidatePath), idPropertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            SerializedProperty nestedProperty = arrayElement.FindPropertyRelative(candidatePath);

            if (nestedProperty != null)
                return nestedProperty;
        }

        return null;
    }

    /// <summary>
    /// Builds the first available semantic identifier token for one serialized array element.
    /// </summary>
    /// <param name="arrayElement">Array element inspected for supported identifier fields.</param>
    /// <returns>Identifier token in name:value form, or an empty string when no identifier is authored.</returns>
    public static string ResolveArrayElementToken(SerializedProperty arrayElement)
    {
        if (arrayElement == null)
            return string.Empty;

        // Prefer direct string identifiers because they cover most authored preset lists.
        for (int candidateIndex = 0; candidateIndex < StableStringIdPropertyNames.Length; candidateIndex++)
        {
            string candidateName = StableStringIdPropertyNames[candidateIndex];
            SerializedProperty candidateProperty = arrayElement.FindPropertyRelative(candidateName);

            if (candidateProperty == null ||
                candidateProperty.propertyType != SerializedPropertyType.String ||
                string.IsNullOrWhiteSpace(candidateProperty.stringValue))
            {
                continue;
            }

            return string.Format("{0}:{1}", candidateName, candidateProperty.stringValue.Trim());
        }

        // Resolve the smaller set of identifiers stored below nested payload objects.
        for (int candidateIndex = 0; candidateIndex < StableNestedStringIdPropertyPaths.Length; candidateIndex++)
        {
            string candidatePath = StableNestedStringIdPropertyPaths[candidateIndex];
            SerializedProperty candidateProperty = arrayElement.FindPropertyRelative(candidatePath);

            if (candidateProperty == null ||
                candidateProperty.propertyType != SerializedPropertyType.String ||
                string.IsNullOrWhiteSpace(candidateProperty.stringValue))
            {
                continue;
            }

            return string.Format("{0}:{1}", ResolveTokenName(candidatePath), candidateProperty.stringValue.Trim());
        }

        // Integer identifiers are used only when no semantic string identifier exists.
        for (int candidateIndex = 0; candidateIndex < StableIntegerIdPropertyNames.Length; candidateIndex++)
        {
            string candidateName = StableIntegerIdPropertyNames[candidateIndex];
            SerializedProperty candidateProperty = arrayElement.FindPropertyRelative(candidateName);

            if (candidateProperty != null && candidateProperty.propertyType == SerializedPropertyType.Integer)
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", candidateName, candidateProperty.intValue);
        }

        return string.Empty;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the terminal field name used when a nested identifier path is flattened into a stable token.
    /// </summary>
    /// <param name="propertyPath">Direct or nested serialized property path.</param>
    /// <returns>Trimmed terminal field name, or an empty string for an invalid path.</returns>
    private static string ResolveTokenName(string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return string.Empty;

        int lastSeparatorIndex = propertyPath.LastIndexOf('.');

        if (lastSeparatorIndex < 0 || lastSeparatorIndex >= propertyPath.Length - 1)
            return propertyPath.Trim();

        return propertyPath.Substring(lastSeparatorIndex + 1).Trim();
    }
    #endregion

    #endregion
}
#endif
