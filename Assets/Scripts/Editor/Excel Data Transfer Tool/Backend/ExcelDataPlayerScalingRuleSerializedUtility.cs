using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

/// <summary>
/// Provides reflection-free serialized path and list operations for PlayerStatScalingRule imports.
/// </summary>
internal static class ExcelDataPlayerScalingRuleSerializedUtility
{
    #region Constants
    public const string StatKeyMemberName = "statKey";
    public const string AddScalingMemberName = "addScaling";
    public const string FormulaMemberName = "formula";
    public const string DebugInConsoleMemberName = "debugInConsole";
    public const string DebugColorMemberName = "debugColor";
    private const string TemplateRuleMarker = "scalingRules.Array.data[]";
    private const string ConcreteRuleMarker = "scalingRules.Array.data[";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether one Player field binding targets a direct serialized scaling-rule member.
    /// </summary>
    /// <param name="binding">Workbook field binding.</param>
    /// <param name="resolvedPath">Current concrete path resolved through stable list identities.</param>
    /// <param name="location">Parsed scaling-rule list, element and member paths.</param>
    /// <returns>True when the binding targets a supported direct PlayerStatScalingRule member.</returns>
    public static bool TryResolveLocation(ExcelDataFieldBinding binding,
                                          string resolvedPath,
                                          out ExcelDataPlayerScalingRuleLocation location)
    {
        location = default;

        if (binding == null || binding.Domain != ExcelDataTransferDomain.Player ||
            string.IsNullOrWhiteSpace(binding.PathTemplate) || string.IsNullOrWhiteSpace(resolvedPath))
            return false;

        if (binding.PathTemplate.IndexOf(TemplateRuleMarker, StringComparison.Ordinal) < 0)
            return false;

        int markerIndex = resolvedPath.IndexOf(ConcreteRuleMarker, StringComparison.Ordinal);

        if (markerIndex < 0)
            return false;

        int indexStart = markerIndex + ConcreteRuleMarker.Length;
        int indexEnd = resolvedPath.IndexOf(']', indexStart);

        if (indexEnd < indexStart || indexEnd + 2 > resolvedPath.Length || resolvedPath[indexEnd + 1] != '.')
            return false;

        int parsedIndex;

        if (!int.TryParse(resolvedPath.Substring(indexStart, indexEnd - indexStart),
                          NumberStyles.Integer,
                          CultureInfo.InvariantCulture,
                          out parsedIndex) || parsedIndex < 0)
            return false;

        string memberName = resolvedPath.Substring(indexEnd + 2);

        if (!IsSupportedMember(memberName))
            return false;

        string rulesPropertyPath = resolvedPath.Substring(0, markerIndex + "scalingRules".Length);
        string rulePropertyPath = resolvedPath.Substring(0, indexEnd + 1);
        location = new ExcelDataPlayerScalingRuleLocation(rulesPropertyPath,
                                                          rulePropertyPath,
                                                          memberName);
        return true;
    }

    /// <summary>
    /// Checks whether one direct serialized member belongs to PlayerStatScalingRule import semantics.
    /// </summary>
    /// <param name="memberName">Direct serialized member name.</param>
    /// <returns>True for statKey, addScaling, formula and optional debug members.</returns>
    public static bool IsSupportedMember(string memberName)
    {
        switch (memberName)
        {
            case StatKeyMemberName:
            case AddScalingMemberName:
            case FormulaMemberName:
            case DebugInConsoleMemberName:
            case DebugColorMemberName:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Reports whether one member is mandatory when Merge Rules By Stat Key creates a new rule.
    /// </summary>
    /// <param name="memberName">Direct serialized rule member name.</param>
    /// <returns>True for statKey, addScaling and formula.</returns>
    public static bool IsRequiredCreationMember(string memberName)
    {
        return string.Equals(memberName, StatKeyMemberName, StringComparison.Ordinal) ||
               string.Equals(memberName, AddScalingMemberName, StringComparison.Ordinal) ||
               string.Equals(memberName, FormulaMemberName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds one direct member path under an existing or planned scaling-rule element.
    /// </summary>
    /// <param name="rulePropertyPath">Concrete scaling-rule element path.</param>
    /// <param name="memberName">Direct member name.</param>
    /// <returns>Concrete serialized member path.</returns>
    public static string BuildMemberPath(string rulePropertyPath, string memberName)
    {
        if (string.IsNullOrWhiteSpace(rulePropertyPath) || string.IsNullOrWhiteSpace(memberName))
            return string.Empty;

        return rulePropertyPath + "." + memberName;
    }

    /// <summary>
    /// Builds one concrete scaling-rule element path from a list path and zero-based index.
    /// </summary>
    /// <param name="rulesPropertyPath">Serialized scalingRules list path.</param>
    /// <param name="ruleIndex">Zero-based rule index.</param>
    /// <returns>Concrete serialized rule element path.</returns>
    public static string BuildRulePath(string rulesPropertyPath, int ruleIndex)
    {
        return rulesPropertyPath + ".Array.data[" +
               ruleIndex.ToString(CultureInfo.InvariantCulture) + "]";
    }

    /// <summary>
    /// Finds every current rule whose statKey exactly matches the requested stable target key.
    /// </summary>
    /// <param name="rulesProperty">Serialized scalingRules list.</param>
    /// <param name="statKey">Stable target property key.</param>
    /// <returns>Zero-based matching rule indices in authored order.</returns>
    public static List<int> FindRuleIndicesByStatKey(SerializedProperty rulesProperty, string statKey)
    {
        List<int> matchingIndices = new List<int>();

        if (rulesProperty == null || !rulesProperty.isArray || string.IsNullOrWhiteSpace(statKey))
            return matchingIndices;

        // Compare exact stat keys because they encode case-sensitive Unity property paths.
        for (int ruleIndex = 0; ruleIndex < rulesProperty.arraySize; ruleIndex++)
        {
            SerializedProperty ruleProperty = rulesProperty.GetArrayElementAtIndex(ruleIndex);
            SerializedProperty statKeyProperty = ruleProperty == null
                ? null
                : ruleProperty.FindPropertyRelative(StatKeyMemberName);

            if (statKeyProperty != null &&
                string.Equals(statKeyProperty.stringValue, statKey, StringComparison.Ordinal))
                matchingIndices.Add(ruleIndex);
        }

        return matchingIndices;
    }

    /// <summary>
    /// Appends and fully initializes one PlayerStatScalingRule in pending SerializedObject state.
    /// </summary>
    /// <param name="serializedObject">Pending owner wrapper used by preview or apply.</param>
    /// <param name="creation">Validated append operation.</param>
    /// <param name="rulePropertyPath">Concrete path of the initialized appended rule.</param>
    /// <param name="warning">Blocking diagnostic when the expected list structure changed.</param>
    /// <returns>True when one default rule was appended at the planned index.</returns>
    public static bool TryAppendInitializedRule(SerializedObject serializedObject,
                                                ExcelDataPlayerScalingRuleCreation creation,
                                                out string rulePropertyPath,
                                                out string warning)
    {
        rulePropertyPath = string.Empty;
        warning = string.Empty;

        if (serializedObject == null || creation == null)
        {
            warning = "Scaling-rule creation requires a serialized owner and creation plan.";
            return false;
        }

        SerializedProperty rulesProperty = serializedObject.FindProperty(creation.RulesPropertyPath);

        if (rulesProperty == null || !rulesProperty.isArray ||
            rulesProperty.propertyType == SerializedPropertyType.String)
        {
            warning = "Scaling-rule list no longer exists at '" + creation.RulesPropertyPath + "'.";
            return false;
        }

        if (rulesProperty.arraySize != creation.TargetIndex)
        {
            warning = "Scaling-rule list changed after preflight. Expected append index " +
                      creation.TargetIndex.ToString(CultureInfo.InvariantCulture) +
                      " but current size is " +
                      rulesProperty.arraySize.ToString(CultureInfo.InvariantCulture) + ".";
            return false;
        }

        rulesProperty.InsertArrayElementAtIndex(creation.TargetIndex);
        SerializedProperty ruleProperty = rulesProperty.GetArrayElementAtIndex(creation.TargetIndex);

        if (!TryInitializeRule(ruleProperty, out warning))
            return false;

        rulePropertyPath = BuildRulePath(creation.RulesPropertyPath, creation.TargetIndex);
        return true;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Resets every direct rule member after Unity inserts a potentially duplicated list element.
    /// </summary>
    /// <param name="ruleProperty">Newly inserted rule element.</param>
    /// <param name="warning">Blocking diagnostic when the serialized shape is incompatible.</param>
    /// <returns>True when all mandatory and debug members were initialized.</returns>
    private static bool TryInitializeRule(SerializedProperty ruleProperty, out string warning)
    {
        warning = string.Empty;

        if (ruleProperty == null)
        {
            warning = "New scaling-rule element could not be resolved.";
            return false;
        }

        SerializedProperty statKeyProperty = ruleProperty.FindPropertyRelative(StatKeyMemberName);
        SerializedProperty addScalingProperty = ruleProperty.FindPropertyRelative(AddScalingMemberName);
        SerializedProperty formulaProperty = ruleProperty.FindPropertyRelative(FormulaMemberName);
        SerializedProperty debugInConsoleProperty = ruleProperty.FindPropertyRelative(DebugInConsoleMemberName);
        SerializedProperty debugColorProperty = ruleProperty.FindPropertyRelative(DebugColorMemberName);

        if (statKeyProperty == null || addScalingProperty == null || formulaProperty == null ||
            debugInConsoleProperty == null || debugColorProperty == null)
        {
            warning = "PlayerStatScalingRule serialized members no longer match the import contract.";
            return false;
        }

        statKeyProperty.stringValue = string.Empty;
        addScalingProperty.boolValue = false;
        formulaProperty.stringValue = string.Empty;
        debugInConsoleProperty.boolValue = false;
        debugColorProperty.colorValue = PlayerStatScalingRule.GetDefaultDebugColor();
        return true;
    }
    #endregion

    #endregion
}
