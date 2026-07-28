using System;
using System.Collections.Generic;

/// <summary>
/// Validates room-reward formulas against the linked scalable-stat catalog using the unified formula compiler.
/// </summary>
public static class GameRoomRewardFormulaValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates assignment shape, variables and result type for one formula-backed reward module.
    /// </summary>
    /// <param name="preset">Room reward preset supplying the player scalable-stat catalog.</param>
    /// <param name="module">Formula-backed module to validate.</param>
    /// <param name="failureMessage">Actionable validation failure.</param>
    /// <returns>True when the formula is valid for its target domain and type.</returns>
    public static bool TryValidate(GameRoomClearRewardsPreset preset,
                                   GameRoomRewardModuleDefinition module,
                                   out string failureMessage)
    {
        failureMessage = string.Empty;

        if (module == null || module.ValueSource != GameRoomRewardValueSource.Formula)
            return true;

        return TryValidate(preset,
                           module.TargetDomain,
                           module.ValueSource,
                           module.TargetStatName,
                           module.Formula,
                           out failureMessage);
    }

    /// <summary>
    /// Validates one resolved module payload, including binding-local overrides, against the unified formula system.
    /// </summary>
    /// <param name="preset">Room reward preset supplying the player scalable-stat catalog.</param>
    /// <param name="targetDomain">Player data domain modified by the resolved module.</param>
    /// <param name="valueSource">Resolved value source determining whether a formula is required.</param>
    /// <param name="targetStatName">Resolved scalable-stat target when the module modifies a stat.</param>
    /// <param name="formula">Resolved unified formula payload.</param>
    /// <param name="failureMessage">Actionable validation failure.</param>
    /// <returns>True when the resolved payload is valid for its target domain and result type.</returns>
    public static bool TryValidate(GameRoomClearRewardsPreset preset,
                                   GameRoomRewardTargetDomain targetDomain,
                                   GameRoomRewardValueSource valueSource,
                                   string targetStatName,
                                   string formula,
                                   out string failureMessage)
    {
        failureMessage = string.Empty;

        if (valueSource != GameRoomRewardValueSource.Formula)
            return true;

        if (preset == null ||
            preset.PlayerContextPreset == null ||
            preset.PlayerContextPreset.ProgressionPreset == null)
        {
            failureMessage = "A Player Context with Progression is required for formula validation.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(formula))
        {
            failureMessage = "A unified formula is required.";
            return false;
        }

        string expression = formula;
        PlayerFormulaValueType thisType = PlayerFormulaValueType.Number;
        PlayerFormulaValueType requiredResultType = PlayerFormulaValueType.Number;

        if (targetDomain == GameRoomRewardTargetDomain.ScalableStat)
        {
            if (!TryResolveStatType(preset,
                                    targetStatName,
                                    out PlayerScalableStatType statType))
            {
                failureMessage = string.Format("Unknown scalable stat target [{0}].",
                                               targetStatName);
                return false;
            }

            if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(
                    formula,
                    out string targetName,
                    out expression,
                    out failureMessage))
            {
                return false;
            }

            if (!string.Equals(targetName,
                               targetStatName,
                               StringComparison.OrdinalIgnoreCase))
            {
                failureMessage = string.Format(
                    "Formula target [{0}] does not match the selected stat [{1}].",
                    targetName,
                    targetStatName);
                return false;
            }

            requiredResultType =
                PlayerScalableStatTypeUtility.ToFormulaValueType(statType);
            thisType = requiredResultType;
        }

        return TryValidateExpression(preset,
                                     expression,
                                     thisType,
                                     requiredResultType,
                                     out failureMessage);
    }
    #endregion

    #region Expression Validation
    /// <summary>
    /// Compiles one expression, rejects unknown variables and verifies its inferred result type.
    /// </summary>
    /// <param name="preset">Room reward preset supplying allowed scalable stats.</param>
    /// <param name="expression">Unified expression without Character Tuning assignment syntax.</param>
    /// <param name="thisType">Type bound to the reserved this variable.</param>
    /// <param name="requiredResultType">Required expression result type.</param>
    /// <param name="failureMessage">Compiler, variable or type failure.</param>
    /// <returns>True when the expression is runtime-safe for this module.</returns>
    private static bool TryValidateExpression(GameRoomClearRewardsPreset preset,
                                              string expression,
                                              PlayerFormulaValueType thisType,
                                              PlayerFormulaValueType requiredResultType,
                                              out string failureMessage)
    {
        PlayerStatFormulaCompileResult compileResult =
            PlayerStatFormulaEngine.Compile(expression, false);

        if (!compileResult.IsValid || compileResult.CompiledFormula == null)
        {
            failureMessage = string.IsNullOrWhiteSpace(compileResult.ErrorMessage)
                ? "Formula compilation failed."
                : compileResult.ErrorMessage;
            return false;
        }

        BuildVariableCatalog(preset,
                             out HashSet<string> allowedVariables,
                             out Dictionary<string, PlayerFormulaValueType> variableTypes);
        IReadOnlyList<string> variableNames =
            compileResult.CompiledFormula.VariableNames;

        for (int index = 0; index < variableNames.Count; index++)
        {
            string variableName = variableNames[index];

            if (string.Equals(variableName,
                              PlayerScalableStatNameUtility.ReservedThisName,
                              StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (allowedVariables.Contains(variableName))
                continue;

            failureMessage = string.Format("Unknown scalable stat variable [{0}].",
                                           variableName);
            return false;
        }

        if (!compileResult.CompiledFormula.TryInferResultType(thisType,
                                                              variableTypes,
                                                              out PlayerFormulaValueType resultType,
                                                              out failureMessage))
        {
            return false;
        }

        if (resultType == requiredResultType)
            return true;

        failureMessage = string.Format("Formula resolves to {0}, but the target requires {1}.",
                                       resultType,
                                       requiredResultType);
        return false;
    }

    /// <summary>
    /// Builds case-insensitive allowed-variable and type catalogs from linked scalable stats.
    /// </summary>
    /// <param name="preset">Room reward preset supplying the player context.</param>
    /// <param name="allowedVariables">Output variable whitelist.</param>
    /// <param name="variableTypes">Output variable type map.</param>
    private static void BuildVariableCatalog(
        GameRoomClearRewardsPreset preset,
        out HashSet<string> allowedVariables,
        out Dictionary<string, PlayerFormulaValueType> variableTypes)
    {
        allowedVariables =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        variableTypes =
            new Dictionary<string, PlayerFormulaValueType>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<PlayerScalableStatDefinition> stats =
            preset.PlayerContextPreset.ProgressionPreset.ScalableStats;

        for (int index = 0; index < stats.Count; index++)
        {
            PlayerScalableStatDefinition stat = stats[index];

            if (stat == null || string.IsNullOrWhiteSpace(stat.StatName))
                continue;

            allowedVariables.Add(stat.StatName);
            variableTypes[stat.StatName] =
                PlayerScalableStatTypeUtility.ToFormulaValueType(stat.StatType);
        }
    }
    #endregion

    #region Stat Resolution
    /// <summary>
    /// Resolves one scalable-stat type by formula name semantics.
    /// </summary>
    /// <param name="preset">Room reward preset supplying the player context.</param>
    /// <param name="statName">Selected scalable-stat name.</param>
    /// <param name="statType">Resolved stat type.</param>
    /// <returns>True when the stat exists.</returns>
    private static bool TryResolveStatType(GameRoomClearRewardsPreset preset,
                                           string statName,
                                           out PlayerScalableStatType statType)
    {
        IReadOnlyList<PlayerScalableStatDefinition> stats =
            preset.PlayerContextPreset.ProgressionPreset.ScalableStats;

        for (int index = 0; index < stats.Count; index++)
        {
            PlayerScalableStatDefinition stat = stats[index];

            if (stat == null ||
                !string.Equals(stat.StatName,
                               statName,
                               StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            statType = stat.StatType;
            return true;
        }

        statType = PlayerScalableStatType.Float;
        return false;
    }
    #endregion

    #endregion
}
