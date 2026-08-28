#if UNITY_EDITOR
using System;
using UnityEditor;

/// <summary>
/// Removes unified scaling rules owned by serialized array entries before structural deletion.
/// </summary>
internal static class PlayerScalingArrayEntryRuleUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Deletes rules that resolve below one array entry while the entry and its stable identifier still exist.
    /// </summary>
    /// <param name="serializedObject">Preset containing both the target entry and scaling rules.</param>
    /// <param name="scalingRulesProperty">Serialized unified scaling-rule array.</param>
    /// <param name="entryProperty">Array entry about to be removed.</param>
    public static void RemoveOwnedRules(SerializedObject serializedObject,
                                        SerializedProperty scalingRulesProperty,
                                        SerializedProperty entryProperty)
    {
        if (serializedObject == null ||
            scalingRulesProperty == null ||
            !scalingRulesProperty.isArray ||
            entryProperty == null)
        {
            return;
        }

        string entryPathPrefix = entryProperty.propertyPath + ".";

        // Resolve stable and legacy numeric keys before deleting the entry so rules cannot bind to a neighbor.
        for (int ruleIndex = scalingRulesProperty.arraySize - 1; ruleIndex >= 0; ruleIndex--)
        {
            SerializedProperty ruleProperty = scalingRulesProperty.GetArrayElementAtIndex(ruleIndex);
            SerializedProperty statKeyProperty = ruleProperty != null
                ? ruleProperty.FindPropertyRelative("statKey")
                : null;

            if (statKeyProperty == null ||
                !PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedObject,
                                                                      statKeyProperty.stringValue,
                                                                      out SerializedProperty targetProperty) ||
                !targetProperty.propertyPath.StartsWith(entryPathPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            scalingRulesProperty.DeleteArrayElementAtIndex(ruleIndex);
        }
    }
    #endregion

    #endregion
}
#endif
