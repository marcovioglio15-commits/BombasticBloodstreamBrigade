using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;

/// <summary>
/// Provides editor-only validation for scalable stat dependency graphs built from Add Scaling formulas.
/// </summary>
public static class PlayerScalingDependencyValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds dependency warnings for scalable stat formulas, including circular dependency groups.
    /// </summary>
    /// <param name="scalableStatsProperty">Serialized scalable stats list used to resolve stat names.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules list used to read Add Scaling formulas.</param>
    /// <returns>List of warning messages. Empty list when no dependency issues are found.</returns>
    public static List<string> BuildScalableStatsDependencyWarnings(SerializedProperty scalableStatsProperty,
                                                                    SerializedProperty scalingRulesProperty)
    {
        List<string> warnings = new List<string>();

        if (scalableStatsProperty == null || scalingRulesProperty == null)
            return warnings;

        if (!scalableStatsProperty.isArray || !scalingRulesProperty.isArray)
            return warnings;

        Dictionary<string, string> statNameByStatKey = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> canonicalStatNameByLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        BuildStatMaps(scalableStatsProperty, statNameByStatKey, canonicalStatNameByLookup);

        if (statNameByStatKey.Count == 0 || canonicalStatNameByLookup.Count == 0)
            return warnings;

        Dictionary<string, HashSet<string>> dependencyGraph = BuildDependencyGraph(scalingRulesProperty,
                                                                                    statNameByStatKey,
                                                                                    canonicalStatNameByLookup);
        List<List<string>> circularGroups = FindCircularDependencyGroups(dependencyGraph);

        for (int groupIndex = 0; groupIndex < circularGroups.Count; groupIndex++)
        {
            string warning = BuildCircularGroupWarning(circularGroups[groupIndex]);

            if (string.IsNullOrWhiteSpace(warning))
                continue;

            warnings.Add(warning);
        }

        return warnings;
    }

    /// <summary>
    /// Builds cross-system cycle warnings for Player scalable stats and Difficulty Scaling coefficients.
    /// </summary>
    /// <param name="scalableStatsProperty">Serialized scalable stats list used to resolve player variable names.</param>
    /// <param name="scalingRulesProperty">Serialized Add Scaling rules that may consume difficulty coefficients.</param>
    /// <returns>Cross-system circular dependency warnings for the current Player progression context.</returns>
    public static List<string> BuildDifficultyCrossDependencyWarnings(SerializedProperty scalableStatsProperty,
                                                                      SerializedProperty scalingRulesProperty)
    {
        List<string> warnings = new List<string>();

        if (scalableStatsProperty == null || scalingRulesProperty == null ||
            !scalableStatsProperty.isArray || !scalingRulesProperty.isArray)
        {
            return warnings;
        }

        Dictionary<string, string> statNameByStatKey = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> canonicalStatNameByLookup =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        BuildStatMaps(scalableStatsProperty, statNameByStatKey, canonicalStatNameByLookup);
        Dictionary<string, HashSet<string>> dependencyGraph = BuildDependencyGraph(scalingRulesProperty,
                                                                                    statNameByStatKey,
                                                                                    canonicalStatNameByLookup);
        PlayerProgressionPreset progressionPreset =
            scalableStatsProperty.serializedObject.targetObject as PlayerProgressionPreset;
        List<GameDifficultyScalingPreset> difficultyPresets = FindDifficultyPresets(progressionPreset);

        // Build an independent graph per compatible preset so unrelated coefficient namespaces cannot collide.
        for (int presetIndex = 0; presetIndex < difficultyPresets.Count; presetIndex++)
        {
            GameDifficultyScalingPreset difficultyPreset = difficultyPresets[presetIndex];
            Dictionary<string, HashSet<string>> combinedGraph = CloneDependencyGraph(dependencyGraph);
            Dictionary<string, string> coefficientNodeById = BuildCoefficientNodeMap(difficultyPreset,
                                                                                      combinedGraph);
            AppendPlayerCoefficientDependencies(scalingRulesProperty,
                                                statNameByStatKey,
                                                coefficientNodeById,
                                                combinedGraph);
            AppendDifficultyDependencies(difficultyPreset,
                                         coefficientNodeById,
                                         canonicalStatNameByLookup,
                                         combinedGraph);
            List<List<string>> circularGroups = FindCircularDependencyGroups(combinedGraph);

            for (int groupIndex = 0; groupIndex < circularGroups.Count; groupIndex++)
            {
                if (!ContainsDifficultyNode(circularGroups[groupIndex]))
                    continue;

                warnings.Add(BuildCrossSystemWarning(difficultyPreset, circularGroups[groupIndex]));
            }
        }

        return warnings;
    }
    #endregion

    #region Difficulty Graph Construction
    /// <summary>
    /// Finds difficulty presets that explicitly use the current Player progression context.
    /// </summary>
    /// <param name="progressionPreset">Current Player progression preset.</param>
    /// <returns>Compatible Difficulty Scaling presets sorted by asset discovery order.</returns>
    private static List<GameDifficultyScalingPreset> FindDifficultyPresets(PlayerProgressionPreset progressionPreset)
    {
        List<GameDifficultyScalingPreset> presets = new List<GameDifficultyScalingPreset>();
        string[] presetGuids = AssetDatabase.FindAssets("t:GameDifficultyScalingPreset", new string[] { "Assets" });

        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            GameDifficultyScalingPreset preset = AssetDatabase.LoadAssetAtPath<GameDifficultyScalingPreset>(
                AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]));

            if (preset == null || preset.PlayerContextPreset == null ||
                preset.PlayerContextPreset.ProgressionPreset != progressionPreset)
            {
                continue;
            }

            presets.Add(preset);
        }

        return presets;
    }

    /// <summary>
    /// Clones a dependency graph so every Difficulty Scaling preset is validated independently.
    /// </summary>
    /// <param name="source">Player-only dependency graph.</param>
    /// <returns>Deep clone of graph nodes and dependency sets.</returns>
    private static Dictionary<string, HashSet<string>> CloneDependencyGraph(
        Dictionary<string, HashSet<string>> source)
    {
        Dictionary<string, HashSet<string>> clone =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, HashSet<string>> entry in source)
            clone.Add(entry.Key, new HashSet<string>(entry.Value, StringComparer.OrdinalIgnoreCase));

        return clone;
    }

    /// <summary>
    /// Adds uniquely namespaced difficulty coefficient nodes to one combined dependency graph.
    /// </summary>
    /// <param name="preset">Difficulty Scaling preset supplying coefficient definitions.</param>
    /// <param name="graph">Combined graph receiving nodes.</param>
    /// <returns>Coefficient node keys indexed by formula identifier.</returns>
    private static Dictionary<string, string> BuildCoefficientNodeMap(GameDifficultyScalingPreset preset,
                                                                      Dictionary<string, HashSet<string>> graph)
    {
        Dictionary<string, string> nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int coefficientIndex = 0; coefficientIndex < preset.Coefficients.Count; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinition coefficient = preset.Coefficients[coefficientIndex];

            if (coefficient == null || string.IsNullOrWhiteSpace(coefficient.CoefficientId) ||
                nodes.ContainsKey(coefficient.CoefficientId))
            {
                continue;
            }

            string node = "difficulty::" + coefficient.CoefficientId;
            nodes.Add(coefficient.CoefficientId, node);
            graph[node] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return nodes;
    }

    /// <summary>
    /// Adds Player Add Scaling references to difficulty coefficients as graph edges.
    /// </summary>
    /// <param name="scalingRulesProperty">Serialized Player Add Scaling rules.</param>
    /// <param name="statNameByStatKey">Player stat names indexed by stable stat key.</param>
    /// <param name="coefficientNodeById">Difficulty graph nodes indexed by coefficient ID.</param>
    /// <param name="graph">Combined dependency graph receiving edges.</param>
    private static void AppendPlayerCoefficientDependencies(SerializedProperty scalingRulesProperty,
                                                            IReadOnlyDictionary<string, string> statNameByStatKey,
                                                            IReadOnlyDictionary<string, string> coefficientNodeById,
                                                            Dictionary<string, HashSet<string>> graph)
    {
        for (int ruleIndex = 0; ruleIndex < scalingRulesProperty.arraySize; ruleIndex++)
        {
            SerializedProperty rule = scalingRulesProperty.GetArrayElementAtIndex(ruleIndex);
            SerializedProperty statKey = rule.FindPropertyRelative("statKey");
            SerializedProperty addScaling = rule.FindPropertyRelative("addScaling");
            SerializedProperty formula = rule.FindPropertyRelative("formula");

            if (statKey == null || addScaling == null || formula == null || !addScaling.boolValue ||
                !statNameByStatKey.TryGetValue(statKey.stringValue, out string statName) ||
                !graph.TryGetValue(statName, out HashSet<string> dependencies))
            {
                continue;
            }

            PlayerStatFormulaCompileResult result = PlayerStatFormulaEngine.Compile(formula.stringValue, true);

            if (!result.IsValid || result.CompiledFormula == null)
                continue;

            for (int variableIndex = 0; variableIndex < result.CompiledFormula.VariableNames.Count; variableIndex++)
            {
                if (coefficientNodeById.TryGetValue(result.CompiledFormula.VariableNames[variableIndex],
                                                    out string coefficientNode))
                {
                    dependencies.Add(coefficientNode);
                }
            }
        }
    }

    /// <summary>
    /// Adds coefficient references to Player stats and sibling coefficients for every scaling mode.
    /// </summary>
    /// <param name="preset">Difficulty Scaling preset supplying definitions.</param>
    /// <param name="coefficientNodeById">Difficulty graph nodes indexed by coefficient ID.</param>
    /// <param name="canonicalStatNameByLookup">Canonical Player stat names.</param>
    /// <param name="graph">Combined dependency graph receiving edges.</param>
    private static void AppendDifficultyDependencies(GameDifficultyScalingPreset preset,
                                                     IReadOnlyDictionary<string, string> coefficientNodeById,
                                                     IReadOnlyDictionary<string, string> canonicalStatNameByLookup,
                                                     Dictionary<string, HashSet<string>> graph)
    {
        for (int coefficientIndex = 0; coefficientIndex < preset.Coefficients.Count; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinition coefficient = preset.Coefficients[coefficientIndex];

            if (coefficient == null ||
                !coefficientNodeById.TryGetValue(coefficient.CoefficientId, out string coefficientNode))
            {
                continue;
            }

            HashSet<string> variableNames = CollectDifficultyVariableNames(coefficient);

            foreach (string variableName in variableNames)
            {
                if (canonicalStatNameByLookup.TryGetValue(variableName, out string statNode))
                    graph[coefficientNode].Add(statNode);
                else if (coefficientNodeById.TryGetValue(variableName, out string dependencyCoefficientNode))
                    graph[coefficientNode].Add(dependencyCoefficientNode);
            }
        }
    }

    /// <summary>
    /// Collects referenced variables from formula, curve and ordered step coefficient modes.
    /// </summary>
    /// <param name="coefficient">Difficulty coefficient being inspected.</param>
    /// <returns>Case-insensitive referenced variable set.</returns>
    private static HashSet<string> CollectDifficultyVariableNames(GameDifficultyCoefficientDefinition coefficient)
    {
        HashSet<string> variableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        switch (coefficient.ScalingMode)
        {
            case GameDifficultyScalingMode.Curve:
                variableNames.Add(coefficient.CurveInputVariable);
                break;
            case GameDifficultyScalingMode.Steps:
                for (int stepIndex = 0; stepIndex < coefficient.Steps.Count; stepIndex++)
                {
                    GameDifficultyStepDefinition step = coefficient.Steps[stepIndex];

                    if (step == null)
                        continue;

                    for (int conditionIndex = 0; conditionIndex < step.Conditions.Count; conditionIndex++)
                    {
                        GameDifficultyStepCondition condition = step.Conditions[conditionIndex];

                        if (condition != null)
                            variableNames.Add(condition.VariableName);
                    }
                }
                break;
            default:
                PlayerStatFormulaCompileResult result = PlayerStatFormulaEngine.Compile(coefficient.Formula, true);

                if (!result.IsValid || result.CompiledFormula == null)
                    break;

                for (int variableIndex = 0; variableIndex < result.CompiledFormula.VariableNames.Count; variableIndex++)
                    variableNames.Add(result.CompiledFormula.VariableNames[variableIndex]);
                break;
        }

        variableNames.Remove(PlayerScalableStatNameUtility.ReservedThisName);
        variableNames.RemoveWhere(string.IsNullOrWhiteSpace);
        return variableNames;
    }

    /// <summary>
    /// Checks whether one strongly connected component crosses into Difficulty Scaling.
    /// </summary>
    /// <param name="nodes">Strongly connected component node keys.</param>
    /// <returns>True when at least one coefficient node participates.</returns>
    private static bool ContainsDifficultyNode(IReadOnlyList<string> nodes)
    {
        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            if (nodes[nodeIndex].StartsWith("difficulty::", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Formats one cross-system strongly connected component into an actionable warning.
    /// </summary>
    /// <param name="preset">Difficulty preset participating in the loop.</param>
    /// <param name="nodes">Circular Player and difficulty nodes.</param>
    /// <returns>Designer-facing cross-system cycle warning.</returns>
    private static string BuildCrossSystemWarning(GameDifficultyScalingPreset preset, IReadOnlyList<string> nodes)
    {
        List<string> labels = new List<string>();

        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            string node = nodes[nodeIndex];
            labels.Add(node.StartsWith("difficulty::", StringComparison.OrdinalIgnoreCase)
                ? "Difficulty [" + node.Substring("difficulty::".Length) + "]"
                : "Player [" + node + "]");
        }

        labels.Sort(StringComparer.OrdinalIgnoreCase);
        return "Cross-system scaling loop in Difficulty preset '" + preset.name + "': " +
               string.Join(" <-> ", labels) + ". Break one formula dependency to keep rebuild order deterministic.";
    }
    #endregion

    #region Graph Construction
    private static void BuildStatMaps(SerializedProperty scalableStatsProperty,
                                      Dictionary<string, string> statNameByStatKey,
                                      Dictionary<string, string> canonicalStatNameByLookup)
    {
        for (int statIndex = 0; statIndex < scalableStatsProperty.arraySize; statIndex++)
        {
            SerializedProperty statElement = scalableStatsProperty.GetArrayElementAtIndex(statIndex);

            if (statElement == null)
                continue;

            SerializedProperty statNameProperty = statElement.FindPropertyRelative("statName");
            SerializedProperty defaultValueProperty = statElement.FindPropertyRelative("defaultValue");

            if (statNameProperty == null || defaultValueProperty == null)
                continue;

            if (statNameProperty.propertyType != SerializedPropertyType.String)
                continue;

            string statName = string.IsNullOrWhiteSpace(statNameProperty.stringValue)
                ? string.Empty
                : statNameProperty.stringValue.Trim();

            if (!PlayerScalableStatNameUtility.IsValid(statName))
                continue;

            if (!canonicalStatNameByLookup.ContainsKey(statName))
                canonicalStatNameByLookup[statName] = statName;

            string statKey = PlayerScalingStatKeyUtility.BuildStatKey(defaultValueProperty);

            if (string.IsNullOrWhiteSpace(statKey))
                continue;

            statNameByStatKey[statKey] = statName;
        }
    }

    private static Dictionary<string, HashSet<string>> BuildDependencyGraph(SerializedProperty scalingRulesProperty,
                                                                            IReadOnlyDictionary<string, string> statNameByStatKey,
                                                                            IReadOnlyDictionary<string, string> canonicalStatNameByLookup)
    {
        Dictionary<string, HashSet<string>> dependencyGraph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, string> entry in canonicalStatNameByLookup)
            dependencyGraph[entry.Value] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int ruleIndex = 0; ruleIndex < scalingRulesProperty.arraySize; ruleIndex++)
        {
            SerializedProperty ruleProperty = scalingRulesProperty.GetArrayElementAtIndex(ruleIndex);

            if (ruleProperty == null)
                continue;

            SerializedProperty statKeyProperty = ruleProperty.FindPropertyRelative("statKey");
            SerializedProperty addScalingProperty = ruleProperty.FindPropertyRelative("addScaling");
            SerializedProperty formulaProperty = ruleProperty.FindPropertyRelative("formula");

            if (statKeyProperty == null || addScalingProperty == null || formulaProperty == null)
                continue;

            if (statKeyProperty.propertyType != SerializedPropertyType.String ||
                addScalingProperty.propertyType != SerializedPropertyType.Boolean ||
                formulaProperty.propertyType != SerializedPropertyType.String)
                continue;

            if (!addScalingProperty.boolValue)
                continue;

            string statKey = statKeyProperty.stringValue;
            string formula = formulaProperty.stringValue;

            if (string.IsNullOrWhiteSpace(statKey) || string.IsNullOrWhiteSpace(formula))
                continue;

            if (!statNameByStatKey.TryGetValue(statKey, out string sourceStatName))
                continue;

            if (!dependencyGraph.TryGetValue(sourceStatName, out HashSet<string> dependencies))
                continue;

            PlayerStatFormulaCompileResult compileResult = PlayerStatFormulaEngine.Compile(formula, true);

            if (!compileResult.IsValid || compileResult.CompiledFormula == null)
                continue;

            IReadOnlyList<string> variableNames = compileResult.CompiledFormula.VariableNames;

            for (int variableIndex = 0; variableIndex < variableNames.Count; variableIndex++)
            {
                string variableName = variableNames[variableIndex];

                if (string.Equals(variableName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!canonicalStatNameByLookup.TryGetValue(variableName, out string targetStatName))
                    continue;

                dependencies.Add(targetStatName);
            }
        }

        return dependencyGraph;
    }
    #endregion

    #region Strongly Connected Components
    private static List<List<string>> FindCircularDependencyGroups(Dictionary<string, HashSet<string>> dependencyGraph)
    {
        List<List<string>> circularGroups = new List<List<string>>();

        if (dependencyGraph == null || dependencyGraph.Count == 0)
            return circularGroups;

        Dictionary<string, int> discoveryIndexByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> lowLinkByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Stack<string> recursionStack = new Stack<string>();
        HashSet<string> nodesInStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int nextDiscoveryIndex = 0;

        foreach (KeyValuePair<string, HashSet<string>> node in dependencyGraph)
        {
            if (discoveryIndexByNode.ContainsKey(node.Key))
                continue;

            StrongConnect(node.Key,
                          dependencyGraph,
                          ref nextDiscoveryIndex,
                          discoveryIndexByNode,
                          lowLinkByNode,
                          recursionStack,
                          nodesInStack,
                          circularGroups);
        }

        return circularGroups;
    }


    private static void StrongConnect(string node,
                                      Dictionary<string, HashSet<string>> dependencyGraph,
                                      ref int nextDiscoveryIndex,
                                      Dictionary<string, int> discoveryIndexByNode,
                                      Dictionary<string, int> lowLinkByNode,
                                      Stack<string> recursionStack,
                                      HashSet<string> nodesInStack,
                                      List<List<string>> circularGroups)
    {
        discoveryIndexByNode[node] = nextDiscoveryIndex;
        lowLinkByNode[node] = nextDiscoveryIndex;
        nextDiscoveryIndex += 1;
        recursionStack.Push(node);
        nodesInStack.Add(node);

        HashSet<string> dependencies = dependencyGraph[node];

        foreach (string dependencyNode in dependencies)
        {
            if (!discoveryIndexByNode.ContainsKey(dependencyNode))
            {
                StrongConnect(dependencyNode,
                              dependencyGraph,
                              ref nextDiscoveryIndex,
                              discoveryIndexByNode,
                              lowLinkByNode,
                              recursionStack,
                              nodesInStack,
                              circularGroups);
                lowLinkByNode[node] = Math.Min(lowLinkByNode[node], lowLinkByNode[dependencyNode]);
                continue;
            }

            if (nodesInStack.Contains(dependencyNode))
                lowLinkByNode[node] = Math.Min(lowLinkByNode[node], discoveryIndexByNode[dependencyNode]);
        }

        if (lowLinkByNode[node] != discoveryIndexByNode[node])
            return;

        List<string> stronglyConnectedComponent = new List<string>();

        while (recursionStack.Count > 0)
        {
            string poppedNode = recursionStack.Pop();
            nodesInStack.Remove(poppedNode);
            stronglyConnectedComponent.Add(poppedNode);

            if (string.Equals(poppedNode, node, StringComparison.OrdinalIgnoreCase))
                break;
        }

        if (stronglyConnectedComponent.Count > 1)
        {
            stronglyConnectedComponent.Sort(StringComparer.OrdinalIgnoreCase);
            circularGroups.Add(stronglyConnectedComponent);
            return;
        }

        if (stronglyConnectedComponent.Count == 1 && HasSelfDependency(dependencyGraph, stronglyConnectedComponent[0]))
            circularGroups.Add(stronglyConnectedComponent);
    }

    private static bool HasSelfDependency(Dictionary<string, HashSet<string>> dependencyGraph, string nodeName)
    {
        if (!dependencyGraph.TryGetValue(nodeName, out HashSet<string> dependencies))
            return false;

        return dependencies.Contains(nodeName);
    }
    #endregion

    #region Messages
    private static string BuildCircularGroupWarning(IReadOnlyList<string> circularGroup)
    {
        if (circularGroup == null || circularGroup.Count == 0)
            return string.Empty;

        if (circularGroup.Count == 1)
            return string.Format("Self dependency detected on scalable stat [{0}].", circularGroup[0]);

        StringBuilder warningBuilder = new StringBuilder();
        warningBuilder.Append("Circular dependency detected among scalable stats: ");

        for (int index = 0; index < circularGroup.Count; index++)
        {
            if (index > 0)
                warningBuilder.Append(" <-> ");

            warningBuilder.Append('[');
            warningBuilder.Append(circularGroup[index]);
            warningBuilder.Append(']');
        }

        warningBuilder.Append('.');
        return warningBuilder.ToString();
    }
    #endregion

    #endregion
}
