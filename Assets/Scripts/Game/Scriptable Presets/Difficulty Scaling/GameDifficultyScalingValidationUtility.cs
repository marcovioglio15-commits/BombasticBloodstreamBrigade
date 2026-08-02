using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validates difficulty authoring and produces a deterministic dependency-safe coefficient order.
/// </summary>
public static class GameDifficultyScalingValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one preset and returns actionable warnings without modifying its serialized values.
    /// </summary>
    /// <param name="preset">Difficulty preset whose graph and numeric tuning are inspected.</param>
    /// <returns>Ordered validation messages; an empty collection indicates a bake-safe preset.</returns>
    public static List<string> BuildWarnings(GameDifficultyScalingPreset preset)
    {
        List<string> warnings = new List<string>();

        if (preset == null)
        {
            warnings.Add("Difficulty Scaling preset is missing.");
            return warnings;
        }

        Dictionary<string, GameDifficultyCoefficientDefinition> definitions = BuildDefinitionMap(preset, warnings);
        HashSet<string> playerVariables = BuildPlayerVariableSet(preset.PlayerContextPreset);

        foreach (KeyValuePair<string, GameDifficultyCoefficientDefinition> entry in definitions)
            ValidateDefinition(entry.Value, definitions, playerVariables, warnings);

        TryBuildEvaluationOrder(preset, out List<GameDifficultyCoefficientDefinition> ignoredOrder, out string cycleMessage);

        if (!string.IsNullOrWhiteSpace(cycleMessage))
            warnings.Add(cycleMessage);

        return warnings;
    }

    /// <summary>
    /// Topologically orders coefficient definitions so every coefficient dependency is evaluated first.
    /// </summary>
    /// <param name="preset">Difficulty preset containing the coefficient graph.</param>
    /// <param name="orderedDefinitions">Dependency-safe definitions when ordering succeeds.</param>
    /// <param name="errorMessage">Cycle or identity failure preventing deterministic evaluation.</param>
    /// <returns>True when the graph is valid and acyclic.</returns>
    public static bool TryBuildEvaluationOrder(GameDifficultyScalingPreset preset,
                                               out List<GameDifficultyCoefficientDefinition> orderedDefinitions,
                                               out string errorMessage)
    {
        orderedDefinitions = new List<GameDifficultyCoefficientDefinition>();
        errorMessage = string.Empty;

        if (preset == null || preset.Coefficients == null)
            return true;

        Dictionary<string, GameDifficultyCoefficientDefinition> definitions =
            new Dictionary<string, GameDifficultyCoefficientDefinition>(StringComparer.OrdinalIgnoreCase);

        for (int coefficientIndex = 0; coefficientIndex < preset.Coefficients.Count; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinition definition = preset.Coefficients[coefficientIndex];

            if (definition == null || string.IsNullOrWhiteSpace(definition.CoefficientId))
                continue;

            if (definitions.ContainsKey(definition.CoefficientId))
            {
                errorMessage = "Duplicate difficulty coefficient ID '" + definition.CoefficientId + "'.";
                return false;
            }

            definitions.Add(definition.CoefficientId, definition);
        }

        Dictionary<string, byte> visitState = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        List<string> traversalPath = new List<string>();

        for (int coefficientIndex = 0; coefficientIndex < preset.Coefficients.Count; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinition definition = preset.Coefficients[coefficientIndex];

            if (definition == null || string.IsNullOrWhiteSpace(definition.CoefficientId))
                continue;

            if (!TryVisit(definition,
                          definitions,
                          visitState,
                          traversalPath,
                          orderedDefinitions,
                          out errorMessage))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Collects every built-in, player scalable-stat and difficulty coefficient variable available to formulas.
    /// </summary>
    /// <param name="preset">Difficulty preset defining player context and coefficients.</param>
    /// <returns>Case-insensitive set of valid numeric formula variables.</returns>
    public static HashSet<string> BuildAvailableVariableSet(GameDifficultyScalingPreset preset)
    {
        HashSet<string> variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int variableIndex = 0; variableIndex < GameDifficultyVariableNames.All.Count; variableIndex++)
            variables.Add(GameDifficultyVariableNames.All[variableIndex]);

        if (preset == null)
            return variables;

        HashSet<string> playerVariables = BuildPlayerVariableSet(preset.PlayerContextPreset);

        foreach (string playerVariable in playerVariables)
            variables.Add(playerVariable);

        if (preset.Coefficients == null)
            return variables;

        for (int coefficientIndex = 0; coefficientIndex < preset.Coefficients.Count; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinition definition = preset.Coefficients[coefficientIndex];

            if (definition != null && !string.IsNullOrWhiteSpace(definition.CoefficientId))
                variables.Add(definition.CoefficientId.Trim());
        }

        return variables;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Builds a unique coefficient map while reporting invalid or duplicated identities.
    /// </summary>
    /// <param name="preset">Preset supplying definitions.</param>
    /// <param name="warnings">Mutable validation output receiving identity diagnostics.</param>
    /// <returns>Case-insensitive map containing each valid unique definition.</returns>
    private static Dictionary<string, GameDifficultyCoefficientDefinition> BuildDefinitionMap(
        GameDifficultyScalingPreset preset,
        List<string> warnings)
    {
        Dictionary<string, GameDifficultyCoefficientDefinition> definitions =
            new Dictionary<string, GameDifficultyCoefficientDefinition>(StringComparer.OrdinalIgnoreCase);

        if (preset.Coefficients == null)
            return definitions;

        for (int coefficientIndex = 0; coefficientIndex < preset.Coefficients.Count; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinition definition = preset.Coefficients[coefficientIndex];

            if (definition == null)
            {
                warnings.Add("Coefficient[" + coefficientIndex + "] is null.");
                continue;
            }

            string coefficientId = definition.CoefficientId;

            if (!PlayerScalableStatNameUtility.IsValid(coefficientId))
            {
                warnings.Add("Coefficient[" + coefficientIndex + "] has invalid ID '" + coefficientId + "'.");
                continue;
            }

            if (IsBuiltInVariable(coefficientId))
            {
                warnings.Add("Coefficient ID '" + coefficientId + "' conflicts with a reserved difficulty context variable.");
                continue;
            }

            if (definitions.ContainsKey(coefficientId))
            {
                warnings.Add("Duplicate difficulty coefficient ID '" + coefficientId + "'.");
                continue;
            }

            definitions.Add(coefficientId, definition);
        }

        return definitions;
    }

    /// <summary>
    /// Validates one coefficient mode, range and referenced variables.
    /// </summary>
    /// <param name="definition">Coefficient definition being checked.</param>
    /// <param name="definitions">All valid coefficient identities in the preset.</param>
    /// <param name="playerVariables">Player scalable-stat names available to formulas.</param>
    /// <param name="warnings">Mutable validation output receiving diagnostics.</param>
    private static void ValidateDefinition(GameDifficultyCoefficientDefinition definition,
                                           IReadOnlyDictionary<string, GameDifficultyCoefficientDefinition> definitions,
                                           ISet<string> playerVariables,
                                           List<string> warnings)
    {
        string context = "Coefficient '" + definition.CoefficientId + "'";

        if (float.IsNaN(definition.DefaultValue) || float.IsInfinity(definition.DefaultValue))
            warnings.Add(context + " has a non-finite default value.");

        if (definition.MinimumValue > definition.MaximumValue)
            warnings.Add(context + " has Minimum Value greater than Maximum Value.");

        switch (definition.ScalingMode)
        {
            case GameDifficultyScalingMode.Curve:
                ValidateCurveDefinition(definition, definitions, playerVariables, warnings);
                break;
            case GameDifficultyScalingMode.Steps:
                ValidateStepDefinition(definition, definitions, playerVariables, warnings);
                break;
            default:
                ValidateFormulaDefinition(definition, definitions, playerVariables, warnings);
                break;
        }
    }

    /// <summary>
    /// Validates one formula-backed coefficient against the shared numeric variable catalog.
    /// </summary>
    /// <param name="definition">Formula-backed coefficient.</param>
    /// <param name="definitions">All coefficient identities.</param>
    /// <param name="playerVariables">Player scalable-stat identities.</param>
    /// <param name="warnings">Mutable diagnostic output.</param>
    private static void ValidateFormulaDefinition(GameDifficultyCoefficientDefinition definition,
                                                  IReadOnlyDictionary<string, GameDifficultyCoefficientDefinition> definitions,
                                                  ISet<string> playerVariables,
                                                  List<string> warnings)
    {
        PlayerStatFormulaCompileResult compileResult = PlayerStatFormulaEngine.Compile(definition.Formula, true);

        if (!compileResult.IsValid || compileResult.CompiledFormula == null)
        {
            warnings.Add("Coefficient '" + definition.CoefficientId + "' formula is invalid: " + compileResult.ErrorMessage);
            return;
        }

        IReadOnlyList<string> variableNames = compileResult.CompiledFormula.VariableNames;

        for (int variableIndex = 0; variableIndex < variableNames.Count; variableIndex++)
        {
            string variableName = variableNames[variableIndex];

            if (string.Equals(variableName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsKnownVariable(variableName, definitions, playerVariables))
                continue;

            warnings.Add("Coefficient '" + definition.CoefficientId + "' references unknown variable [" + variableName + "].");
        }
    }

    /// <summary>
    /// Validates curve input identity and curve storage.
    /// </summary>
    /// <param name="definition">Curve-backed coefficient.</param>
    /// <param name="definitions">All coefficient identities.</param>
    /// <param name="playerVariables">Player scalable-stat identities.</param>
    /// <param name="warnings">Mutable diagnostic output.</param>
    private static void ValidateCurveDefinition(GameDifficultyCoefficientDefinition definition,
                                                IReadOnlyDictionary<string, GameDifficultyCoefficientDefinition> definitions,
                                                ISet<string> playerVariables,
                                                List<string> warnings)
    {
        if (!IsKnownVariable(definition.CurveInputVariable, definitions, playerVariables))
            warnings.Add("Coefficient '" + definition.CoefficientId + "' curve references unknown variable [" + definition.CurveInputVariable + "].");

        if (definition.ScalingCurve == null || definition.ScalingCurve.length == 0)
            warnings.Add("Coefficient '" + definition.CoefficientId + "' curve has no keys.");
    }

    /// <summary>
    /// Validates ordered step conditions and finite outputs.
    /// </summary>
    /// <param name="definition">Step-backed coefficient.</param>
    /// <param name="definitions">All coefficient identities.</param>
    /// <param name="playerVariables">Player scalable-stat identities.</param>
    /// <param name="warnings">Mutable diagnostic output.</param>
    private static void ValidateStepDefinition(GameDifficultyCoefficientDefinition definition,
                                               IReadOnlyDictionary<string, GameDifficultyCoefficientDefinition> definitions,
                                               ISet<string> playerVariables,
                                               List<string> warnings)
    {
        if (definition.Steps == null || definition.Steps.Count == 0)
        {
            warnings.Add("Coefficient '" + definition.CoefficientId + "' has no quantized steps.");
            return;
        }

        for (int stepIndex = 0; stepIndex < definition.Steps.Count; stepIndex++)
        {
            GameDifficultyStepDefinition step = definition.Steps[stepIndex];

            if (step == null)
            {
                warnings.Add("Coefficient '" + definition.CoefficientId + "' Step[" + stepIndex + "] is null.");
                continue;
            }

            if (float.IsNaN(step.OutputValue) || float.IsInfinity(step.OutputValue))
                warnings.Add("Coefficient '" + definition.CoefficientId + "' Step[" + stepIndex + "] has a non-finite output.");

            if (step.Conditions == null || step.Conditions.Count == 0)
                warnings.Add("Coefficient '" + definition.CoefficientId + "' Step[" + stepIndex + "] has no conditions and always matches.");

            for (int conditionIndex = 0; conditionIndex < step.Conditions.Count; conditionIndex++)
            {
                GameDifficultyStepCondition condition = step.Conditions[conditionIndex];

                if (condition == null)
                {
                    warnings.Add("Coefficient '" + definition.CoefficientId + "' Step[" + stepIndex + "] Condition[" + conditionIndex + "] is null.");
                    continue;
                }

                if (!IsKnownVariable(condition.VariableName, definitions, playerVariables))
                    warnings.Add("Coefficient '" + definition.CoefficientId + "' step references unknown variable [" + condition.VariableName + "].");
            }
        }
    }
    #endregion

    #region Dependency Graph
    /// <summary>
    /// Depth-first visits one coefficient and appends it after all coefficient dependencies.
    /// </summary>
    /// <param name="definition">Current coefficient node.</param>
    /// <param name="definitions">Coefficient lookup.</param>
    /// <param name="visitState">Per-node traversal state where one is visiting and two is complete.</param>
    /// <param name="traversalPath">Current dependency path used for cycle diagnostics.</param>
    /// <param name="orderedDefinitions">Topological output list.</param>
    /// <param name="errorMessage">Cycle diagnostic when traversal fails.</param>
    /// <returns>True when the node and all dependencies are acyclic.</returns>
    private static bool TryVisit(GameDifficultyCoefficientDefinition definition,
                                 IReadOnlyDictionary<string, GameDifficultyCoefficientDefinition> definitions,
                                 IDictionary<string, byte> visitState,
                                 List<string> traversalPath,
                                 List<GameDifficultyCoefficientDefinition> orderedDefinitions,
                                 out string errorMessage)
    {
        errorMessage = string.Empty;
        string coefficientId = definition.CoefficientId;

        if (visitState.TryGetValue(coefficientId, out byte state))
        {
            if (state == 2)
                return true;

            if (state == 1)
            {
                traversalPath.Add(coefficientId);
                errorMessage = "Difficulty coefficient dependency loop: " + string.Join(" -> ", traversalPath) + ".";
                return false;
            }
        }

        visitState[coefficientId] = 1;
        traversalPath.Add(coefficientId);
        HashSet<string> dependencies = CollectCoefficientDependencies(definition, definitions);

        foreach (string dependencyId in dependencies)
        {
            if (!definitions.TryGetValue(dependencyId, out GameDifficultyCoefficientDefinition dependency))
                continue;

            if (!TryVisit(dependency,
                          definitions,
                          visitState,
                          traversalPath,
                          orderedDefinitions,
                          out errorMessage))
            {
                return false;
            }
        }

        traversalPath.RemoveAt(traversalPath.Count - 1);
        visitState[coefficientId] = 2;
        orderedDefinitions.Add(definition);
        return true;
    }

    /// <summary>
    /// Extracts coefficient-to-coefficient references from the active authoring mode.
    /// </summary>
    /// <param name="definition">Coefficient whose inputs are inspected.</param>
    /// <param name="definitions">All coefficient identities.</param>
    /// <returns>Distinct referenced coefficient identifiers.</returns>
    private static HashSet<string> CollectCoefficientDependencies(
        GameDifficultyCoefficientDefinition definition,
        IReadOnlyDictionary<string, GameDifficultyCoefficientDefinition> definitions)
    {
        HashSet<string> dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        switch (definition.ScalingMode)
        {
            case GameDifficultyScalingMode.Curve:
                AddCoefficientDependency(definition.CurveInputVariable, definitions, dependencies);
                break;
            case GameDifficultyScalingMode.Steps:
                CollectStepDependencies(definition, definitions, dependencies);
                break;
            default:
                PlayerStatFormulaCompileResult compileResult = PlayerStatFormulaEngine.Compile(definition.Formula, false);

                if (compileResult.IsValid && compileResult.CompiledFormula != null)
                {
                    IReadOnlyList<string> variableNames = compileResult.CompiledFormula.VariableNames;

                    for (int variableIndex = 0; variableIndex < variableNames.Count; variableIndex++)
                        AddCoefficientDependency(variableNames[variableIndex], definitions, dependencies);
                }
                break;
        }

        return dependencies;
    }

    /// <summary>
    /// Extracts coefficient dependencies from all quantized step conditions.
    /// </summary>
    /// <param name="definition">Step-backed coefficient.</param>
    /// <param name="definitions">All coefficient identities.</param>
    /// <param name="dependencies">Mutable dependency output.</param>
    private static void CollectStepDependencies(GameDifficultyCoefficientDefinition definition,
                                                IReadOnlyDictionary<string, GameDifficultyCoefficientDefinition> definitions,
                                                ISet<string> dependencies)
    {
        if (definition.Steps == null)
            return;

        for (int stepIndex = 0; stepIndex < definition.Steps.Count; stepIndex++)
        {
            GameDifficultyStepDefinition step = definition.Steps[stepIndex];

            if (step == null || step.Conditions == null)
                continue;

            for (int conditionIndex = 0; conditionIndex < step.Conditions.Count; conditionIndex++)
            {
                GameDifficultyStepCondition condition = step.Conditions[conditionIndex];

                if (condition != null)
                    AddCoefficientDependency(condition.VariableName, definitions, dependencies);
            }
        }
    }

    /// <summary>
    /// Adds one variable only when it references a known coefficient definition.
    /// </summary>
    /// <param name="variableName">Candidate dependency variable.</param>
    /// <param name="definitions">All coefficient identities.</param>
    /// <param name="dependencies">Mutable dependency output.</param>
    private static void AddCoefficientDependency(string variableName,
                                                 IReadOnlyDictionary<string, GameDifficultyCoefficientDefinition> definitions,
                                                 ISet<string> dependencies)
    {
        if (!string.IsNullOrWhiteSpace(variableName) && definitions.ContainsKey(variableName))
            dependencies.Add(variableName);
    }
    #endregion

    #region Variables
    /// <summary>
    /// Collects numeric player scalable stats exposed by the selected Player master preset.
    /// </summary>
    /// <param name="playerMasterPreset">Player context preset selected by Difficulty Scaling.</param>
    /// <returns>Case-insensitive set of numeric scalable-stat identifiers.</returns>
    private static HashSet<string> BuildPlayerVariableSet(PlayerMasterPreset playerMasterPreset)
    {
        HashSet<string> variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        PlayerProgressionPreset progressionPreset = playerMasterPreset != null
            ? playerMasterPreset.ProgressionPreset
            : null;

        if (progressionPreset == null || progressionPreset.ScalableStats == null)
            return variables;

        for (int statIndex = 0; statIndex < progressionPreset.ScalableStats.Count; statIndex++)
        {
            PlayerScalableStatDefinition stat = progressionPreset.ScalableStats[statIndex];

            if (stat == null)
                continue;

            if (PlayerScalableStatNameUtility.IsValid(stat.StatName))
                variables.Add(stat.StatName);
        }

        return variables;
    }

    /// <summary>
    /// Checks whether one variable exists in a built-in, player-stat or coefficient namespace.
    /// </summary>
    /// <param name="variableName">Variable identifier to inspect.</param>
    /// <param name="definitions">All coefficient identities.</param>
    /// <param name="playerVariables">Player scalable-stat identities.</param>
    /// <returns>True when the numeric variable is available.</returns>
    private static bool IsKnownVariable(string variableName,
                                        IReadOnlyDictionary<string, GameDifficultyCoefficientDefinition> definitions,
                                        ISet<string> playerVariables)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            return false;

        return IsBuiltInVariable(variableName) ||
               definitions.ContainsKey(variableName) ||
               playerVariables.Contains(variableName);
    }

    /// <summary>
    /// Checks whether one identifier belongs to the reserved built-in difficulty context.
    /// </summary>
    /// <param name="variableName">Variable identifier to inspect.</param>
    /// <returns>True when the identifier is reserved by a built-in numeric variable.</returns>
    private static bool IsBuiltInVariable(string variableName)
    {
        for (int variableIndex = 0; variableIndex < GameDifficultyVariableNames.All.Count; variableIndex++)
        {
            if (string.Equals(GameDifficultyVariableNames.All[variableIndex], variableName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
