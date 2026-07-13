using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Validates cross-group identities before Player scaling merge planning mutates pending list structure.
/// </summary>
internal static class ExcelDataPlayerScalingRuleGroupValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rejects multiple workbook source groups that resolve to the same desired stat key.
    /// </summary>
    /// <param name="groups">Scaling cells grouped by original rule element.</param>
    /// <param name="plan">Plan receiving coordinate-specific duplicate target diagnostics.</param>
    public static void ValidateUniqueDesiredStatKeys(IReadOnlyList<ExcelDataPlayerScalingRuleGroup> groups,
                                                     ExcelDataPlayerScalingImportPlan plan)
    {
        if (groups == null || plan == null)
            return;

        Dictionary<string, List<ExcelDataPlayerScalingRuleGroup>> groupsByDesiredKey =
            new Dictionary<string, List<ExcelDataPlayerScalingRuleGroup>>(StringComparer.Ordinal);

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ExcelDataPlayerScalingRuleGroup group = groups[groupIndex];
            string desiredStatKey = ResolveDesiredStatKey(group);
            string ownerKey = group.Asset.GetInstanceID() + ":" + desiredStatKey;

            if (!groupsByDesiredKey.TryGetValue(ownerKey,
                                                out List<ExcelDataPlayerScalingRuleGroup> matchingGroups))
            {
                matchingGroups = new List<ExcelDataPlayerScalingRuleGroup>();
                groupsByDesiredKey.Add(ownerKey, matchingGroups);
            }

            matchingGroups.Add(group);
        }

        foreach (KeyValuePair<string, List<ExcelDataPlayerScalingRuleGroup>> groupPair in groupsByDesiredKey)
        {
            if (groupPair.Value.Count <= 1)
                continue;

            string statKey = ResolveDesiredStatKey(groupPair.Value[0]);
            string message = "Workbook contains multiple Player scaling source groups for desired statKey '" +
                             statKey + "'. Merge targets must be unique.";

            for (int groupIndex = 0; groupIndex < groupPair.Value.Count; groupIndex++)
                AddDiagnosticToGroup(groupPair.Value[groupIndex], message, plan);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the incoming stat key when mapped, otherwise the current source rule key.
    /// </summary>
    /// <param name="group">Source scaling-rule group.</param>
    /// <returns>Desired exact stat key, or an empty string when the source shape is invalid.</returns>
    private static string ResolveDesiredStatKey(ExcelDataPlayerScalingRuleGroup group)
    {
        if (group.TryGetCell(ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName,
                             out ExcelDataPlayerScalingResolvedCell statKeyCell))
            return statKeyCell.Cell.IncomingValue.ValueText;

        SerializedProperty ruleProperty = group.SerializedObject.FindProperty(group.SourceRulePropertyPath);
        SerializedProperty statKeyProperty = ruleProperty == null
            ? null
            : ruleProperty.FindPropertyRelative(ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName);
        return statKeyProperty == null ? string.Empty : statKeyProperty.stringValue;
    }

    /// <summary>
    /// Adds one duplicate-target diagnostic to every mapped cell in a source group.
    /// </summary>
    /// <param name="group">Source group receiving the diagnostic.</param>
    /// <param name="message">Blocking duplicate-target message.</param>
    /// <param name="plan">Plan receiving diagnostics.</param>
    private static void AddDiagnosticToGroup(ExcelDataPlayerScalingRuleGroup group,
                                             string message,
                                             ExcelDataPlayerScalingImportPlan plan)
    {
        for (int cellIndex = 0; cellIndex < group.Cells.Count; cellIndex++)
            plan.AddDiagnostic(group.Cells[cellIndex], message);
    }
    #endregion

    #endregion
}
