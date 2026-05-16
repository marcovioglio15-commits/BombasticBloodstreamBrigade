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
