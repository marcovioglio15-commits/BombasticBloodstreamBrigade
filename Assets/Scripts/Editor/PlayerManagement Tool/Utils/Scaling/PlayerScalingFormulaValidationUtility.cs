using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides editor-side validation helpers for formula strings used by Add Scaling fields.
/// </summary>
public static class PlayerScalingFormulaValidationUtility
{
    #region Constants
    private const double GlobalVariableCacheDurationSeconds = 0.5d;
    #endregion

    #region Fields
    private static readonly HashSet<string> globalVariableCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HashSet<string>> scopedVariableCacheByKey = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> scopedVariableCacheTimeByKey = new Dictionary<string, double>(StringComparer.Ordinal);
    private static double lastGlobalVariableCacheRefreshTime = -1000d;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one formula using parser rules and optional variable whitelist.
    /// </summary>
    /// <param name="formula">Formula text entered by s.</param>
    /// <param name="allowedVariables">Optional variable whitelist (case-insensitive). Null skips unknown-variable checks.</param>
    /// <param name="warningMessage">Validation warning text when formula is invalid.</param>
    /// <returns>True when formula is valid, otherwise false.</returns>
    public static bool TryValidateFormula(string formula,
                                          ISet<string> allowedVariables,
                                          out string warningMessage,
                                          bool requireAtLeastOneVariable = true)
    {
        return TryValidateFormula(formula,
                                  allowedVariables,
                                  null,
                                  PlayerFormulaValueType.Number,
                                  PlayerFormulaValueType.Number,
                                  out warningMessage,
                                  requireAtLeastOneVariable);
    }

    /// <summary>
    /// Validates one formula using parser rules, variable types, and the expected result type for the current field.
    /// </summary>
    /// <param name="formula">Formula text entered by s.</param>
    /// <param name="allowedVariables">Optional variable whitelist (case-insensitive). Null skips unknown-variable checks.</param>
    /// <param name="variableTypes">Known typed variables available in the current authoring scope.</param>
    /// <param name="thisType">Type bound to the reserved [this] token.</param>
    /// <param name="requiredResultType">Expected formula result type for the current authoring context.</param>
    /// <param name="warningMessage">Validation warning text when formula is invalid.</param>
    /// <returns>True when formula is valid, otherwise false.</returns>
    public static bool TryValidateFormula(string formula,
                                          ISet<string> allowedVariables,
                                          IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                          PlayerFormulaValueType thisType,
                                          PlayerFormulaValueType requiredResultType,
                                          out string warningMessage,
                                          bool requireAtLeastOneVariable = true)
    {
        warningMessage = string.Empty;

        ISet<string> resolvedAllowedVariables = ResolveAllowedVariables(allowedVariables);

        if (!TryValidateBracketVariables(formula, resolvedAllowedVariables, out warningMessage))
            return false;

        PlayerStatFormulaCompileResult compileResult = PlayerStatFormulaEngine.Compile(formula, requireAtLeastOneVariable);

        if (!compileResult.IsValid || compileResult.CompiledFormula == null)
        {
            warningMessage = string.IsNullOrWhiteSpace(compileResult.ErrorMessage)
                ? "Formula is invalid."
                : compileResult.ErrorMessage;
            return false;
        }

        IReadOnlyList<string> variableNames = compileResult.CompiledFormula.VariableNames;

        for (int index = 0; index < variableNames.Count; index++)
        {
            string variableName = variableNames[index];

            if (string.Equals(variableName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (resolvedAllowedVariables.Contains(variableName))
                continue;

            warningMessage = string.Format("Unknown scalable stat variable [{0}].", variableName);
            return false;
        }

        if (!compileResult.CompiledFormula.TryInferResultType(thisType,
                                                              variableTypes,
                                                              out PlayerFormulaValueType resultType,
                                                              out warningMessage))
        {
            return false;
        }

        if (requiredResultType != PlayerFormulaValueType.Invalid && resultType != requiredResultType)
        {
            warningMessage = string.Format("Formula resolves to {0} but {1} is required here.",
                                           PlayerScalingFormulaVariableCatalogUtility.BuildTypeLabel(resultType),
                                           PlayerScalingFormulaVariableCatalogUtility.BuildTypeLabel(requiredResultType));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Collects scalable stat names from progression serialized list into a case-insensitive set.
    /// </summary>
    /// <param name="scalableStatsProperty">Serialized List&lt;PlayerScalableStatDefinition&gt; property.</param>
    /// <returns>Case-insensitive set of scalable stat names.</returns>
    public static HashSet<string> BuildVariableSet(SerializedProperty scalableStatsProperty)
    {
        HashSet<string> variableSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (scalableStatsProperty == null)
            return variableSet;

        if (!scalableStatsProperty.isArray)
            return variableSet;

        for (int index = 0; index < scalableStatsProperty.arraySize; index++)
        {
            SerializedProperty scalableStatElement = scalableStatsProperty.GetArrayElementAtIndex(index);

            if (scalableStatElement == null)
                continue;

            SerializedProperty statNameProperty = scalableStatElement.FindPropertyRelative("statName");

            if (statNameProperty == null)
                continue;

            if (statNameProperty.propertyType != SerializedPropertyType.String)
                continue;

            if (string.IsNullOrWhiteSpace(statNameProperty.stringValue))
                continue;

            string statName = statNameProperty.stringValue.Trim();

            if (!PlayerScalableStatNameUtility.IsValid(statName))
                continue;

            variableSet.Add(statName);
        }

        return variableSet;
    }

    /// <summary>
    /// Collects scalable stat names and types from progression serialized list into a case-insensitive dictionary.
    /// </summary>
    /// <param name="scalableStatsProperty">Serialized List&lt;PlayerScalableStatDefinition&gt; property.</param>
    /// <returns>Case-insensitive dictionary of scalable stat types keyed by stat name.</returns>
    public static Dictionary<string, PlayerFormulaValueType> BuildVariableTypeMap(SerializedProperty scalableStatsProperty)
    {
        Dictionary<string, PlayerFormulaValueType> variableTypes = new Dictionary<string, PlayerFormulaValueType>(StringComparer.OrdinalIgnoreCase);

        if (scalableStatsProperty == null)
            return variableTypes;

        if (!scalableStatsProperty.isArray)
            return variableTypes;

        for (int index = 0; index < scalableStatsProperty.arraySize; index++)
        {
            SerializedProperty scalableStatElement = scalableStatsProperty.GetArrayElementAtIndex(index);

            if (scalableStatElement == null)
                continue;

            SerializedProperty statNameProperty = scalableStatElement.FindPropertyRelative("statName");
            SerializedProperty statTypeProperty = scalableStatElement.FindPropertyRelative("statType");

            if (statNameProperty == null || statTypeProperty == null)
                continue;

            if (statNameProperty.propertyType != SerializedPropertyType.String ||
                statTypeProperty.propertyType != SerializedPropertyType.Enum)
            {
                continue;
            }

            string statName = string.IsNullOrWhiteSpace(statNameProperty.stringValue)
                ? string.Empty
                : statNameProperty.stringValue.Trim();

            if (!PlayerScalableStatNameUtility.IsValid(statName))
                continue;

            PlayerScalableStatType statType = (PlayerScalableStatType)statTypeProperty.enumValueIndex;
            variableTypes[statName] = PlayerScalableStatTypeUtility.ToFormulaValueType(statType);
        }

        return variableTypes;
    }

    /// <summary>
    /// Collects scalable stat names and authoring types from progression serialized list into a case-insensitive dictionary.
    /// </summary>
    /// <param name="scalableStatsProperty">Serialized List&lt;PlayerScalableStatDefinition&gt; property.</param>
    /// <returns>Case-insensitive dictionary of scalable-stat types keyed by stat name.</returns>
    public static Dictionary<string, PlayerScalableStatType> BuildScalableStatTypeMap(SerializedProperty scalableStatsProperty)
    {
        Dictionary<string, PlayerScalableStatType> variableTypes = new Dictionary<string, PlayerScalableStatType>(StringComparer.OrdinalIgnoreCase);

        if (scalableStatsProperty == null)
            return variableTypes;

        if (!scalableStatsProperty.isArray)
            return variableTypes;

        for (int index = 0; index < scalableStatsProperty.arraySize; index++)
        {
            SerializedProperty scalableStatElement = scalableStatsProperty.GetArrayElementAtIndex(index);

            if (scalableStatElement == null)
                continue;

            SerializedProperty statNameProperty = scalableStatElement.FindPropertyRelative("statName");
            SerializedProperty statTypeProperty = scalableStatElement.FindPropertyRelative("statType");

            if (statNameProperty == null || statTypeProperty == null)
                continue;

            if (statNameProperty.propertyType != SerializedPropertyType.String ||
                statTypeProperty.propertyType != SerializedPropertyType.Enum)
            {
                continue;
            }

            string statName = string.IsNullOrWhiteSpace(statNameProperty.stringValue)
                ? string.Empty
                : statNameProperty.stringValue.Trim();

            if (!PlayerScalableStatNameUtility.IsValid(statName))
                continue;

            variableTypes[statName] = (PlayerScalableStatType)statTypeProperty.enumValueIndex;
        }

        return variableTypes;
    }

    /// <summary>
    /// Builds a scalable variable scope for the current edited preset, constrained by the active master preset context.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the currently edited scaling rules.</param>
    /// <returns>Case-insensitive set of scoped scalable stat names valid for this preset context.</returns>
    public static HashSet<string> BuildScopedVariableSet(SerializedObject serializedObject)
    {
        HashSet<string> variableSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (serializedObject == null)
            return variableSet;

        UnityEngine.Object targetObject = serializedObject.targetObject;

        if (targetObject == null)
            return variableSet;

        string cacheKey = BuildScopedCacheKey(targetObject, PlayerManagementSelectionContext.ActiveMasterPreset);
        double currentTime = EditorApplication.timeSinceStartup;

        if (scopedVariableCacheByKey.TryGetValue(cacheKey, out HashSet<string> cachedVariables) &&
            scopedVariableCacheTimeByKey.TryGetValue(cacheKey, out double cachedTime) &&
            currentTime - cachedTime < GlobalVariableCacheDurationSeconds)
            return new HashSet<string>(cachedVariables, StringComparer.OrdinalIgnoreCase);

        HashSet<string> scopedVariables = BuildScopedVariableSetInternal(targetObject);
        scopedVariableCacheByKey[cacheKey] = new HashSet<string>(scopedVariables, StringComparer.OrdinalIgnoreCase);
        scopedVariableCacheTimeByKey[cacheKey] = currentTime;

        TrimScopedCache();
        return scopedVariables;
    }

    /// <summary>
    /// Builds a typed scalable variable scope for the current edited preset, constrained by the active master preset context.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the currently edited scaling rules.</param>
    /// <returns>Case-insensitive dictionary of typed scalable stat names valid for this preset context.</returns>
    public static Dictionary<string, PlayerFormulaValueType> BuildScopedVariableTypeMap(SerializedObject serializedObject)
    {
        Dictionary<string, PlayerFormulaValueType> variableTypes = new Dictionary<string, PlayerFormulaValueType>(StringComparer.OrdinalIgnoreCase);

        if (serializedObject == null)
            return variableTypes;

        UnityEngine.Object targetObject = serializedObject.targetObject;

        if (targetObject == null)
            return variableTypes;

        if (TryCollectVariableTypesFromActiveMasterScope(targetObject, variableTypes))
        {
            PlayerScalingFormulaVariableCatalogUtility.AppendDifficultyVariableTypes(variableTypes);
            return variableTypes;
        }

        PlayerProgressionPreset progressionPreset = targetObject as PlayerProgressionPreset;

        if (progressionPreset != null)
            PlayerScalingFormulaVariableCatalogUtility.CollectVariableTypesFromPreset(progressionPreset, variableTypes);

        PlayerScalingFormulaVariableCatalogUtility.AppendDifficultyVariableTypes(variableTypes);

        return variableTypes;
    }

    /// <summary>
    /// Builds a scalable-stat-type scope for the current edited preset, constrained by the active master preset context.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the currently edited scaling rules.</param>
    /// <returns>Case-insensitive dictionary of scalable-stat types keyed by stat name.</returns>
    public static Dictionary<string, PlayerScalableStatType> BuildScopedScalableStatTypeMap(SerializedObject serializedObject)
    {
        Dictionary<string, PlayerScalableStatType> variableTypes = new Dictionary<string, PlayerScalableStatType>(StringComparer.OrdinalIgnoreCase);

        if (serializedObject == null)
            return variableTypes;

        UnityEngine.Object targetObject = serializedObject.targetObject;

        if (targetObject == null)
            return variableTypes;

        if (TryCollectScalableTypesFromActiveMasterScope(targetObject, variableTypes))
        {
            PlayerScalingFormulaVariableCatalogUtility.AppendDifficultyScalableTypes(variableTypes);
            return variableTypes;
        }

        PlayerProgressionPreset progressionPreset = targetObject as PlayerProgressionPreset;

        if (progressionPreset != null)
            PlayerScalingFormulaVariableCatalogUtility.CollectScalableTypesFromPreset(progressionPreset, variableTypes);

        PlayerScalingFormulaVariableCatalogUtility.AppendDifficultyScalableTypes(variableTypes);

        return variableTypes;
    }

    /// <summary>
    /// Formats available variables using unified formula value types.
    /// </summary>
    /// <param name="allowedVariables">Case-insensitive variable set available in the current editor scope.</param>
    /// <param name="variableTypes">Optional formula value-type map used to print precise type labels.</param>
    /// <returns>User-facing label text describing the available variables.</returns>
    public static string BuildAvailableVariablesLabelText(ISet<string> allowedVariables,
                                                          IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes = null)
    {
        return PlayerScalingFormulaVariableCatalogUtility.BuildAvailableVariablesLabelText(allowedVariables,
                                                                                            variableTypes);
    }

    /// <summary>
    /// Formats available variables using authoring-facing scalable-stat subtypes.
    /// </summary>
    /// <param name="allowedVariables">Case-insensitive variable set available in the current editor scope.</param>
    /// <param name="variableTypes">Optional scalable-stat type map used to print precise subtype labels.</param>
    /// <returns>User-facing label text describing the available variables.</returns>
    public static string BuildAvailableVariablesLabelText(ISet<string> allowedVariables,
                                                          IReadOnlyDictionary<string, PlayerScalableStatType> variableTypes)
    {
        return PlayerScalingFormulaVariableCatalogUtility.BuildAvailableVariablesLabelText(allowedVariables,
                                                                                            variableTypes);
    }

    #endregion

    #region Helpers
    private static bool TryValidateBracketVariables(string formula,
                                                    ISet<string> allowedVariables,
                                                    out string warningMessage)
    {
        warningMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(formula))
            return true;

        int parseIndex = 0;

        while (parseIndex < formula.Length)
        {
            int openBracketIndex = formula.IndexOf('[', parseIndex);

            if (openBracketIndex < 0)
                return true;

            int closeBracketIndex = formula.IndexOf(']', openBracketIndex + 1);

            if (closeBracketIndex < 0)
            {
                warningMessage = "Variable token is missing closing ']'.";
                return false;
            }

            string variableName = formula.Substring(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1).Trim();

            if (string.IsNullOrWhiteSpace(variableName))
            {
                warningMessage = "Variable token cannot be empty.";
                return false;
            }

            if (string.Equals(variableName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
            {
                parseIndex = closeBracketIndex + 1;
                continue;
            }

            if (!PlayerScalableStatNameUtility.IsValid(variableName))
            {
                warningMessage = string.Format("Token [{0}] is not a valid variable name.", variableName);
                return false;
            }

            if (allowedVariables != null && !allowedVariables.Contains(variableName))
            {
                warningMessage = string.Format("Unknown scalable stat variable [{0}].", variableName);
                return false;
            }

            parseIndex = closeBracketIndex + 1;
        }

        return true;
    }

    private static ISet<string> ResolveAllowedVariables(ISet<string> inputAllowedVariables)
    {
        if (inputAllowedVariables != null)
            return inputAllowedVariables;

        return BuildGlobalVariableSet();
    }

    private static string BuildScopedCacheKey(UnityEngine.Object targetObject, PlayerMasterPreset activeMasterPreset)
    {
        int targetInstanceId = targetObject != null ? targetObject.GetInstanceID() : 0;
        int masterInstanceId = activeMasterPreset != null ? activeMasterPreset.GetInstanceID() : 0;
        return string.Format("{0}:{1}", targetInstanceId, masterInstanceId);
    }

    private static HashSet<string> BuildScopedVariableSetInternal(UnityEngine.Object targetObject)
    {
        HashSet<string> scopedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (targetObject == null)
            return scopedVariables;

        if (TryCollectFromActiveMasterScope(targetObject, scopedVariables))
        {
            PlayerScalingFormulaVariableCatalogUtility.AppendDifficultyVariables(scopedVariables);
            return scopedVariables;
        }

        PlayerProgressionPreset progressionPreset = targetObject as PlayerProgressionPreset;

        if (progressionPreset != null)
            PlayerScalingFormulaVariableCatalogUtility.CollectVariablesFromPreset(progressionPreset, scopedVariables);

        PlayerScalingFormulaVariableCatalogUtility.AppendDifficultyVariables(scopedVariables);

        return scopedVariables;
    }

    private static bool TryCollectFromActiveMasterScope(UnityEngine.Object targetObject, HashSet<string> variables)
    {
        if (variables == null)
            return false;

        PlayerMasterPreset activeMasterPreset = PlayerManagementSelectionContext.ActiveMasterPreset;

        if (activeMasterPreset == null)
            return false;

        if (!DoesMasterReferenceTarget(activeMasterPreset, targetObject))
            return false;

        PlayerScalingFormulaVariableCatalogUtility.CollectVariablesFromPreset(activeMasterPreset.ProgressionPreset, variables);
        return true;
    }

    private static bool DoesMasterReferenceTarget(PlayerMasterPreset masterPreset, UnityEngine.Object targetObject)
    {
        if (masterPreset == null || targetObject == null)
            return false;

        PlayerControllerPreset controllerPreset = targetObject as PlayerControllerPreset;

        if (controllerPreset != null)
            return masterPreset.ControllerPreset == controllerPreset;

        PlayerProgressionPreset progressionPreset = targetObject as PlayerProgressionPreset;

        if (progressionPreset != null)
            return masterPreset.ProgressionPreset == progressionPreset;

        PlayerPowerUpsPreset powerUpsPreset = targetObject as PlayerPowerUpsPreset;

        if (powerUpsPreset != null)
            return masterPreset.PowerUpsPreset == powerUpsPreset;

        PlayerAnimationBindingsPreset animationPreset = targetObject as PlayerAnimationBindingsPreset;

        if (animationPreset != null)
            return masterPreset.AnimationBindingsPreset == animationPreset;

        return false;
    }

    private static ISet<string> BuildGlobalVariableSet()
    {
        double currentTime = EditorApplication.timeSinceStartup;

        if (currentTime - lastGlobalVariableCacheRefreshTime < GlobalVariableCacheDurationSeconds)
            return globalVariableCache;

        globalVariableCache.Clear();
        CollectGlobalVariablesFromLibrary(globalVariableCache);
        CollectGlobalVariablesFromAssets(globalVariableCache);
        PlayerScalingFormulaVariableCatalogUtility.AppendDifficultyVariables(globalVariableCache);
        lastGlobalVariableCacheRefreshTime = currentTime;
        return globalVariableCache;
    }

    private static void CollectGlobalVariablesFromLibrary(HashSet<string> variables)
    {
        PlayerProgressionPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerProgressionPresetLibrary>(PlayerProgressionPresetLibraryUtility.DefaultLibraryPath);

        if (library == null)
            return;

        IReadOnlyList<PlayerProgressionPreset> presets = library.Presets;

        if (presets == null)
            return;

        for (int index = 0; index < presets.Count; index++)
            PlayerScalingFormulaVariableCatalogUtility.CollectVariablesFromPreset(presets[index], variables);
    }

    private static void CollectGlobalVariablesFromAssets(HashSet<string> variables)
    {
        string[] searchFolders = new string[] { "Assets" };
        string[] presetGuids = AssetDatabase.FindAssets("t:PlayerProgressionPreset", searchFolders);

        for (int guidIndex = 0; guidIndex < presetGuids.Length; guidIndex++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[guidIndex]);

            if (string.IsNullOrWhiteSpace(presetPath))
                continue;

            PlayerProgressionPreset preset = AssetDatabase.LoadAssetAtPath<PlayerProgressionPreset>(presetPath);
            PlayerScalingFormulaVariableCatalogUtility.CollectVariablesFromPreset(preset, variables);
        }
    }


    private static bool TryCollectVariableTypesFromActiveMasterScope(UnityEngine.Object targetObject,
                                                                     Dictionary<string, PlayerFormulaValueType> variableTypes)
    {
        if (variableTypes == null)
            return false;

        PlayerMasterPreset activeMasterPreset = PlayerManagementSelectionContext.ActiveMasterPreset;

        if (activeMasterPreset == null)
            return false;

        if (!DoesMasterReferenceTarget(activeMasterPreset, targetObject))
            return false;

        PlayerProgressionPreset progressionPreset = activeMasterPreset.ProgressionPreset;

        if (progressionPreset == null)
            return false;

        PlayerScalingFormulaVariableCatalogUtility.CollectVariableTypesFromPreset(progressionPreset, variableTypes);

        if (targetObject is PlayerProgressionPreset)
            PlayerScalingFormulaVariableCatalogUtility.CollectVariableTypesFromPreset((PlayerProgressionPreset)targetObject, variableTypes);

        return variableTypes.Count > 0;
    }

    private static bool TryCollectScalableTypesFromActiveMasterScope(UnityEngine.Object targetObject,
                                                                     Dictionary<string, PlayerScalableStatType> variableTypes)
    {
        if (variableTypes == null)
            return false;

        PlayerMasterPreset activeMasterPreset = PlayerManagementSelectionContext.ActiveMasterPreset;

        if (activeMasterPreset == null)
            return false;

        if (!DoesMasterReferenceTarget(activeMasterPreset, targetObject))
            return false;

        PlayerProgressionPreset progressionPreset = activeMasterPreset.ProgressionPreset;

        if (progressionPreset == null)
            return false;

        PlayerScalingFormulaVariableCatalogUtility.CollectScalableTypesFromPreset(progressionPreset, variableTypes);

        if (targetObject is PlayerProgressionPreset)
            PlayerScalingFormulaVariableCatalogUtility.CollectScalableTypesFromPreset((PlayerProgressionPreset)targetObject, variableTypes);

        return variableTypes.Count > 0;
    }

    private static void TrimScopedCache()
    {
        const int MaxScopedCacheEntries = 256;

        if (scopedVariableCacheByKey.Count <= MaxScopedCacheEntries)
            return;

        scopedVariableCacheByKey.Clear();
        scopedVariableCacheTimeByKey.Clear();
    }
    #endregion

    #endregion
}
