using System;
using UnityEditor;

/// <summary>
/// Resolves shared pattern-definition state keys, identity helpers and serialized field accessors.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetPatternDefinitionUtility
{
    #region Constants
    private const string CardStateSuffix = "EnemySharedPatternDefinitionCard";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the foldout title shown by one shared pattern card.
    /// </summary>
    /// <param name="patternIndex">Current pattern index.</param>
    /// <param name="patternId">Resolved pattern ID.</param>
    /// <param name="displayName">Resolved display name.</param>
    /// <param name="unreplaceable">Resolved unreplaceable flag.</param>
    /// <returns>Card title text.</returns>
    public static string BuildPatternCardTitle(int patternIndex,
                                               string patternId,
                                               string displayName,
                                               bool unreplaceable)
    {
        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "<Unnamed>" : displayName.Trim();
        string resolvedPatternId = string.IsNullOrWhiteSpace(patternId) ? "<No ID>" : patternId.Trim();
        string policyLabel = unreplaceable ? "Locked" : "Replaceable";
        return string.Format("#{0:D2}  {1}  ({2})  [{3}]",
                             patternIndex + 1,
                             resolvedDisplayName,
                             resolvedPatternId,
                             policyLabel);
    }

    /// <summary>
    /// Builds the persistent foldout-state key for one shared pattern card.
    /// </summary>
    /// <param name="patternProperty">Serialized pattern property that owns the card.</param>
    /// <returns>Foldout-state key.</returns>
    public static string BuildPatternCardStateKey(SerializedProperty patternProperty)
    {
        return ManagementToolFoldoutStateUtility.BuildPropertyStateKey(patternProperty, CardStateSuffix);
    }

    /// <summary>
    /// Generates a unique pattern ID inside the shared pattern list.
    /// </summary>
    /// <param name="patternsProperty">Serialized patterns array.</param>
    /// <param name="basePatternId">Preferred pattern ID prefix.</param>
    /// <param name="excludedPropertyPath">Property path excluded from duplicate checks.</param>
    /// <returns>Unique pattern ID.</returns>
    public static string GenerateUniquePatternId(SerializedProperty patternsProperty,
                                                 string basePatternId,
                                                 string excludedPropertyPath)
    {
        string sanitizedBasePatternId = string.IsNullOrWhiteSpace(basePatternId) ? "Pattern_Custom" : basePatternId.Trim();

        if (!ContainsPatternId(patternsProperty, sanitizedBasePatternId, excludedPropertyPath))
            return sanitizedBasePatternId;

        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidatePatternId = sanitizedBasePatternId + suffix.ToString();

            if (ContainsPatternId(patternsProperty, candidatePatternId, excludedPropertyPath))
                continue;

            return candidatePatternId;
        }

        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Initializes a newly inserted shared pattern with one default identity and one best-effort core movement binding.
    /// </summary>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset used to resolve the first core movement module.</param>
    /// <param name="patternProperty">Serialized pattern property being initialized.</param>
    /// <param name="patternId">Unique pattern ID assigned to the new pattern.</param>
    public static void InitializeNewPatternDefinition(SerializedObject sharedPresetSerializedObject,
                                                      SerializedProperty patternProperty,
                                                      string patternId)
    {
        if (patternProperty == null)
            return;

        SetPatternId(patternProperty, patternId);
        SetPatternDisplayName(patternProperty, "New Pattern");
        SetPatternDescription(patternProperty, string.Empty);
        SetPatternUnreplaceable(patternProperty, false);

        SerializedProperty coreMovementProperty = patternProperty.FindPropertyRelative("coreMovement");

        if (coreMovementProperty == null)
            return;

        SerializedProperty bindingProperty = coreMovementProperty.FindPropertyRelative("binding");

        if (bindingProperty == null)
            return;

        SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");
        SerializedProperty enabledProperty = bindingProperty.FindPropertyRelative("isEnabled");
        SerializedProperty useOverridePayloadProperty = bindingProperty.FindPropertyRelative("useOverridePayload");

        if (moduleIdProperty != null)
            moduleIdProperty.stringValue = ResolveFirstModuleId(sharedPresetSerializedObject, "coreMovementDefinitions");

        if (enabledProperty != null)
            enabledProperty.boolValue = true;

        if (useOverridePayloadProperty != null)
            useOverridePayloadProperty.boolValue = false;
    }

    /// <summary>
    /// Resolves the pattern ID of one serialized shared pattern.
    /// </summary>
    /// <param name="patternProperty">Serialized pattern property.</param>
    /// <returns>Pattern ID string, or an empty string when unavailable.</returns>
    public static string ResolvePatternId(SerializedProperty patternProperty)
    {
        if (patternProperty == null)
            return string.Empty;

        SerializedProperty patternIdProperty = patternProperty.FindPropertyRelative("patternId");

        if (patternIdProperty == null)
            return string.Empty;

        return patternIdProperty.stringValue;
    }

    /// <summary>
    /// Resolves the display name of one serialized shared pattern.
    /// </summary>
    /// <param name="patternProperty">Serialized pattern property.</param>
    /// <returns>Display name string, or an empty string when unavailable.</returns>
    public static string ResolvePatternDisplayName(SerializedProperty patternProperty)
    {
        if (patternProperty == null)
            return string.Empty;

        SerializedProperty displayNameProperty = patternProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return string.Empty;

        return displayNameProperty.stringValue;
    }

    /// <summary>
    /// Resolves the unreplaceable flag of one serialized shared pattern.
    /// </summary>
    /// <param name="patternProperty">Serialized pattern property.</param>
    /// <returns>Unreplaceable flag value.</returns>
    public static bool ResolvePatternUnreplaceable(SerializedProperty patternProperty)
    {
        if (patternProperty == null)
            return false;

        SerializedProperty unreplaceableProperty = patternProperty.FindPropertyRelative("unreplaceable");

        if (unreplaceableProperty == null)
            return false;

        return unreplaceableProperty.boolValue;
    }

    /// <summary>
    /// Sets the pattern ID of one serialized shared pattern.
    /// </summary>
    /// <param name="patternProperty">Serialized pattern property.</param>
    /// <param name="patternId">New pattern ID.</param>
    public static void SetPatternId(SerializedProperty patternProperty, string patternId)
    {
        if (patternProperty == null)
            return;

        SerializedProperty patternIdProperty = patternProperty.FindPropertyRelative("patternId");

        if (patternIdProperty == null)
            return;

        patternIdProperty.stringValue = patternId;
    }

    /// <summary>
    /// Sets the display name of one serialized shared pattern.
    /// </summary>
    /// <param name="patternProperty">Serialized pattern property.</param>
    /// <param name="displayName">New display name.</param>
    public static void SetPatternDisplayName(SerializedProperty patternProperty, string displayName)
    {
        if (patternProperty == null)
            return;

        SerializedProperty displayNameProperty = patternProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return;

        displayNameProperty.stringValue = displayName;
    }

    /// <summary>
    /// Sets the description of one serialized shared pattern.
    /// </summary>
    /// <param name="patternProperty">Serialized pattern property.</param>
    /// <param name="description">New description text.</param>
    public static void SetPatternDescription(SerializedProperty patternProperty, string description)
    {
        if (patternProperty == null)
            return;

        SerializedProperty descriptionProperty = patternProperty.FindPropertyRelative("description");

        if (descriptionProperty == null)
            return;

        descriptionProperty.stringValue = description;
    }

    /// <summary>
    /// Sets the unreplaceable flag of one serialized shared pattern.
    /// </summary>
    /// <param name="patternProperty">Serialized pattern property.</param>
    /// <param name="unreplaceable">New unreplaceable flag.</param>
    public static void SetPatternUnreplaceable(SerializedProperty patternProperty, bool unreplaceable)
    {
        if (patternProperty == null)
            return;

        SerializedProperty unreplaceableProperty = patternProperty.FindPropertyRelative("unreplaceable");

        if (unreplaceableProperty == null)
            return;

        unreplaceableProperty.boolValue = unreplaceable;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Returns whether the shared pattern list already contains one pattern ID.
    /// </summary>
    /// <param name="patternsProperty">Serialized patterns array.</param>
    /// <param name="patternId">Candidate pattern ID.</param>
    /// <param name="excludedPropertyPath">Property path excluded from duplicate checks.</param>
    /// <returns>True when the candidate ID already exists.</returns>
    private static bool ContainsPatternId(SerializedProperty patternsProperty,
                                          string patternId,
                                          string excludedPropertyPath)
    {
        if (patternsProperty == null || string.IsNullOrWhiteSpace(patternId))
            return false;

        for (int patternIndex = 0; patternIndex < patternsProperty.arraySize; patternIndex++)
        {
            SerializedProperty patternProperty = patternsProperty.GetArrayElementAtIndex(patternIndex);

            if (patternProperty == null)
                continue;

            if (!string.IsNullOrWhiteSpace(excludedPropertyPath) &&
                string.Equals(patternProperty.propertyPath, excludedPropertyPath, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(ResolvePatternId(patternProperty), patternId, StringComparison.OrdinalIgnoreCase))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the first authored module ID from one shared catalog section.
    /// </summary>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset that owns the catalog.</param>
    /// <param name="definitionsPropertyName">Serialized property name of the catalog.</param>
    /// <returns>First authored module ID, or an empty string when unavailable.</returns>
    private static string ResolveFirstModuleId(SerializedObject sharedPresetSerializedObject, string definitionsPropertyName)
    {
        if (sharedPresetSerializedObject == null)
            return string.Empty;

        SerializedProperty definitionsProperty = sharedPresetSerializedObject.FindProperty(definitionsPropertyName);

        if (definitionsProperty == null)
            return string.Empty;

        for (int moduleIndex = 0; moduleIndex < definitionsProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = definitionsProperty.GetArrayElementAtIndex(moduleIndex);
            string moduleId = ResolveModuleIdFromProperty(moduleProperty);

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            return moduleId;
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves the module ID of one serialized module property.
    /// </summary>
    /// <param name="moduleProperty">Serialized module property.</param>
    /// <returns>Module ID string, or an empty string when unavailable.</returns>
    private static string ResolveModuleIdFromProperty(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
            return string.Empty;

        SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
            return string.Empty;

        return moduleIdProperty.stringValue;
    }
    #endregion

    #endregion
}
