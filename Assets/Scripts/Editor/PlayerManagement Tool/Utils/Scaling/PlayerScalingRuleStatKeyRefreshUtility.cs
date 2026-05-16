#if UNITY_EDITOR
using System;
using UnityEditor;

/// <summary>
/// Repairs serialized scaling-rule stat keys after structural preset mutations so editor redraws do not need to write during field refresh.
/// </summary>
internal static class PlayerScalingRuleStatKeyRefreshUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds every resolvable scaling rule to the current normalized stat key emitted by the mutated serialized object.
    /// </summary>
    /// <param name="serializedObject">Serialized preset object whose scaling rules should be synchronized after list reorders or similar structural edits.</param>
    /// <returns>True when at least one rule stat key was rewritten, otherwise false.</returns>
    public static bool RefreshStatKeys(SerializedObject serializedObject)
    {
        if (serializedObject == null)
            return false;

        SerializedProperty scalingRulesProperty = serializedObject.FindProperty("scalingRules");

        if (scalingRulesProperty == null || !scalingRulesProperty.isArray)
            return false;

        bool changed = false;

        // Walk every rule and rebuild the key from the property currently resolved by the old token.
        for (int ruleIndex = 0; ruleIndex < scalingRulesProperty.arraySize; ruleIndex++)
        {
            SerializedProperty ruleProperty = scalingRulesProperty.GetArrayElementAtIndex(ruleIndex);

            if (ruleProperty == null)
                continue;

            SerializedProperty statKeyProperty = ruleProperty.FindPropertyRelative("statKey");

            if (statKeyProperty == null || string.IsNullOrWhiteSpace(statKeyProperty.stringValue))
                continue;

            if (!TryResolveUpdatedStatKey(serializedObject, statKeyProperty.stringValue, out string updatedStatKey))
                continue;

            if (string.Equals(statKeyProperty.stringValue, updatedStatKey, StringComparison.Ordinal))
                continue;

            statKeyProperty.stringValue = updatedStatKey;
            changed = true;
        }

        return changed;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves one legacy or stale stat key back to its target property and emits the current normalized key for that property.
    /// </summary>
    /// <param name="serializedObject">Serialized object used to resolve the incoming stat key.</param>
    /// <param name="currentStatKey">Stored stat key before the structural mutation repair pass.</param>
    /// <param name="updatedStatKey">Current normalized key for the same numeric property when found.</param>
    /// <returns>True when the incoming key still resolves to a numeric property, otherwise false.</returns>
    private static bool TryResolveUpdatedStatKey(SerializedObject serializedObject,
                                                 string currentStatKey,
                                                 out string updatedStatKey)
    {
        updatedStatKey = string.Empty;

        if (serializedObject == null || string.IsNullOrWhiteSpace(currentStatKey))
            return false;

        // Resolve through the stable stat-key helper so reordered array elements still map to their original target.
        if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedObject, currentStatKey, out SerializedProperty targetProperty))
            return false;

        updatedStatKey = PlayerScalingStatKeyUtility.BuildStatKey(targetProperty);
        return !string.IsNullOrWhiteSpace(updatedStatKey);
    }
    #endregion

    #endregion
}
#endif
