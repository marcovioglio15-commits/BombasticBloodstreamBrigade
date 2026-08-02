using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;

/// <summary>
/// Collects typed Player and Difficulty formula variables and formats their editor-facing catalog labels.
/// </summary>
public static class PlayerScalingFormulaVariableCatalogUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Formats available variables using unified formula value types.
    /// </summary>
    /// <param name="allowedVariables">Case-insensitive variable set available in the current editor scope.</param>
    /// <param name="variableTypes">Optional formula value-type map used to print precise type labels.</param>
    /// <returns>User-facing label text describing the available variables.</returns>
    public static string BuildAvailableVariablesLabelText(ISet<string> allowedVariables,
                                                          IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes = null)
    {
        return BuildAvailableVariablesLabelText(allowedVariables, variableTypes, BuildTypeLabel);
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
        return BuildAvailableVariablesLabelText(allowedVariables,
                                                variableTypes,
                                                PlayerScalableStatTypeUtility.BuildDisplayLabel);
    }

    /// <summary>
    /// Appends every authored difficulty coefficient so Player Management formulas can consume shared game scaling.
    /// </summary>
    /// <param name="variables">Mutable case-insensitive variable set.</param>
    public static void AppendDifficultyVariables(ISet<string> variables)
    {
        if (variables == null)
            return;

        string[] presetGuids = AssetDatabase.FindAssets("t:GameDifficultyScalingPreset", new string[] { "Assets" });

        // Scan authored presets once and expose only valid unified formula identifiers.
        for (int guidIndex = 0; guidIndex < presetGuids.Length; guidIndex++)
        {
            GameDifficultyScalingPreset preset = AssetDatabase.LoadAssetAtPath<GameDifficultyScalingPreset>(
                AssetDatabase.GUIDToAssetPath(presetGuids[guidIndex]));

            if (preset == null || preset.Coefficients == null)
                continue;

            for (int coefficientIndex = 0; coefficientIndex < preset.Coefficients.Count; coefficientIndex++)
            {
                GameDifficultyCoefficientDefinition definition = preset.Coefficients[coefficientIndex];

                if (definition != null && PlayerScalableStatNameUtility.IsValid(definition.CoefficientId))
                    variables.Add(definition.CoefficientId);
            }
        }
    }

    /// <summary>
    /// Appends numeric formula types for every authored difficulty coefficient.
    /// </summary>
    /// <param name="variableTypes">Mutable typed formula variable map.</param>
    public static void AppendDifficultyVariableTypes(IDictionary<string, PlayerFormulaValueType> variableTypes)
    {
        if (variableTypes == null)
            return;

        HashSet<string> variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AppendDifficultyVariables(variables);

        // Difficulty coefficients are numeric throughout bake and runtime evaluation.
        foreach (string variableName in variables)
            variableTypes[variableName] = PlayerFormulaValueType.Number;
    }

    /// <summary>
    /// Appends Float authoring types for every difficulty coefficient exposed to Player Management formulas.
    /// </summary>
    /// <param name="variableTypes">Mutable scalable-stat type map.</param>
    public static void AppendDifficultyScalableTypes(IDictionary<string, PlayerScalableStatType> variableTypes)
    {
        if (variableTypes == null)
            return;

        HashSet<string> variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AppendDifficultyVariables(variables);

        // Float is the canonical Player authoring representation for difficulty coefficients.
        foreach (string variableName in variables)
            variableTypes[variableName] = PlayerScalableStatType.Float;
    }

    /// <summary>
    /// Collects valid scalable-stat identifiers from one progression preset.
    /// </summary>
    /// <param name="preset">Progression preset that owns scalable-stat definitions.</param>
    /// <param name="variables">Mutable case-insensitive variable set.</param>
    public static void CollectVariablesFromPreset(PlayerProgressionPreset preset, HashSet<string> variables)
    {
        if (variables == null)
            return;

        ForEachValidStat(preset, (statName, statDefinition) => variables.Add(statName));
    }

    /// <summary>
    /// Collects unified formula value types for valid scalable stats in one progression preset.
    /// </summary>
    /// <param name="preset">Progression preset that owns scalable-stat definitions.</param>
    /// <param name="variableTypes">Mutable typed formula variable map.</param>
    public static void CollectVariableTypesFromPreset(PlayerProgressionPreset preset,
                                                      Dictionary<string, PlayerFormulaValueType> variableTypes)
    {
        if (variableTypes == null)
            return;

        ForEachValidStat(preset,
                         (statName, statDefinition) =>
                             variableTypes[statName] = PlayerScalableStatTypeUtility.ToFormulaValueType(statDefinition.StatType));
    }

    /// <summary>
    /// Collects authoring-facing scalable-stat types for valid stats in one progression preset.
    /// </summary>
    /// <param name="preset">Progression preset that owns scalable-stat definitions.</param>
    /// <param name="variableTypes">Mutable scalable-stat type map.</param>
    public static void CollectScalableTypesFromPreset(PlayerProgressionPreset preset,
                                                      Dictionary<string, PlayerScalableStatType> variableTypes)
    {
        if (variableTypes == null)
            return;

        ForEachValidStat(preset,
                         (statName, statDefinition) => variableTypes[statName] = statDefinition.StatType);
    }

    /// <summary>
    /// Converts a unified formula type to its compact editor label.
    /// </summary>
    /// <param name="type">Unified formula value type.</param>
    /// <returns>Compact user-facing type label.</returns>
    public static string BuildTypeLabel(PlayerFormulaValueType type)
    {
        switch (type)
        {
            case PlayerFormulaValueType.Number:
                return "Number";
            case PlayerFormulaValueType.Boolean:
                return "Boolean";
            case PlayerFormulaValueType.Token:
                return "Token";
            default:
                return "Invalid";
        }
    }
    #endregion

    #region Catalog Methods
    /// <summary>
    /// Formats a typed available-variable catalog through one shared implementation.
    /// </summary>
    /// <typeparam name="TValueType">Variable type stored by the supplied map.</typeparam>
    /// <param name="allowedVariables">Variable identifiers available in the current editor scope.</param>
    /// <param name="variableTypes">Optional type map used to annotate identifiers.</param>
    /// <param name="typeLabelResolver">Resolver converting one variable type to its display label.</param>
    /// <returns>User-facing label text describing the available variables.</returns>
    private static string BuildAvailableVariablesLabelText<TValueType>(
        ISet<string> allowedVariables,
        IReadOnlyDictionary<string, TValueType> variableTypes,
        Func<TValueType, string> typeLabelResolver)
    {
        if (allowedVariables == null || allowedVariables.Count == 0)
            return "Available Variables: [this]";

        List<string> sortedVariables = new List<string>(allowedVariables);
        sortedVariables.Sort(StringComparer.OrdinalIgnoreCase);
        StringBuilder labelBuilder = new StringBuilder("Available Variables: [this]");

        // Append variables in a stable order so the inspector does not visually churn.
        for (int index = 0; index < sortedVariables.Count; index++)
        {
            string variableName = sortedVariables[index];
            labelBuilder.Append(", [");
            labelBuilder.Append(variableName);

            if (variableTypes != null && variableTypes.TryGetValue(variableName, out TValueType variableType))
            {
                labelBuilder.Append(':');
                labelBuilder.Append(typeLabelResolver(variableType));
            }

            labelBuilder.Append(']');
        }

        return labelBuilder.ToString();
    }

    /// <summary>
    /// Invokes one collector for every valid scalable-stat definition in a progression preset.
    /// </summary>
    /// <param name="preset">Progression preset to enumerate.</param>
    /// <param name="collector">Callback receiving each normalized identifier and its definition.</param>
    private static void ForEachValidStat(PlayerProgressionPreset preset,
                                         Action<string, PlayerScalableStatDefinition> collector)
    {
        if (preset == null || collector == null || preset.ScalableStats == null)
            return;

        IReadOnlyList<PlayerScalableStatDefinition> scalableStats = preset.ScalableStats;

        // Normalize and validate names once before forwarding them to typed collectors.
        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatDefinition statDefinition = scalableStats[statIndex];

            if (statDefinition == null || string.IsNullOrWhiteSpace(statDefinition.StatName))
                continue;

            string statName = statDefinition.StatName.Trim();

            if (PlayerScalableStatNameUtility.IsValid(statName))
                collector(statName, statDefinition);
        }
    }
    #endregion

    #endregion
}
