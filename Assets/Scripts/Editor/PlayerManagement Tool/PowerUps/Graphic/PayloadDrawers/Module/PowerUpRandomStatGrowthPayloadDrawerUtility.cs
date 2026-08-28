using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the lazy scaling-aware Random Stat Growth pool editor and contextual validation warnings.
/// </summary>
public static class PowerUpRandomStatGrowthPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds lightweight candidate headers immediately and defers formula-heavy controls until one entry is opened.
    /// </summary>
    /// <param name="parent">Container receiving the payload form.</param>
    /// <param name="payloadProperty">Serialized Random Stat Growth payload.</param>
    public static void Build(VisualElement parent, SerializedProperty payloadProperty)
    {
        if (parent == null || payloadProperty == null)
            return;

        SerializedProperty weightedSelectionProperty = payloadProperty.FindPropertyRelative("useWeightedSelection");
        SerializedProperty entriesProperty = payloadProperty.FindPropertyRelative("entries");

        if (weightedSelectionProperty == null || entriesProperty == null || !entriesProperty.isArray)
        {
            parent.Add(new HelpBox("Random Stat Growth settings are incomplete.", HelpBoxMessageType.Warning));
            return;
        }

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(parent,
                                                              weightedSelectionProperty,
                                                              "Weighted Selection");
        VisualElement entriesContainer = new VisualElement();
        parent.Add(entriesContainer);
        HelpBox poolWarning = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        poolWarning.style.display = DisplayStyle.None;
        parent.Add(poolWarning);
        Action refreshPoolWarning = () => RefreshPoolWarning(weightedSelectionProperty,
                                                              entriesProperty,
                                                              poolWarning);
        Action rebuild = null;
        rebuild = () => RebuildEntries(entriesContainer,
                                       entriesProperty,
                                       weightedSelectionProperty,
                                       rebuild,
                                       refreshPoolWarning);
        Button addButton = new Button(() => AddEntry(entriesProperty, rebuild))
        {
            text = "Add Statistic"
        };
        addButton.tooltip = "Adds one statistic candidate to the growth pool.";
        parent.Add(addButton);
        parent.TrackPropertyValue(weightedSelectionProperty, changedProperty => refreshPoolWarning());
        rebuild();
    }
    #endregion

    #region Construction
    /// <summary>
    /// Rebuilds only candidate headers after structural list edits.
    /// </summary>
    /// <param name="container">Candidate-list host.</param>
    /// <param name="entriesProperty">Serialized candidate array.</param>
    /// <param name="weightedSelectionProperty">Module-level weighted-selection toggle.</param>
    /// <param name="rebuild">Callback used after removals.</param>
    /// <param name="refreshPoolWarning">Callback refreshing the aggregate pool warning.</param>
    private static void RebuildEntries(VisualElement container,
                                       SerializedProperty entriesProperty,
                                       SerializedProperty weightedSelectionProperty,
                                       Action rebuild,
                                       Action refreshPoolWarning)
    {
        PlayerManagementFoldoutStateUtility.CaptureFoldoutStates(container);
        container.Clear();

        for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
        {
            BuildEntryHeader(container,
                             entriesProperty,
                             weightedSelectionProperty,
                             entryIndex,
                             rebuild,
                             refreshPoolWarning);
        }

        refreshPoolWarning.Invoke();
    }

    /// <summary>
    /// Builds one inexpensive candidate header and attaches deferred content construction.
    /// </summary>
    /// <param name="container">Candidate-list host.</param>
    /// <param name="entriesProperty">Serialized candidate array.</param>
    /// <param name="weightedSelectionProperty">Module-level weighted-selection toggle.</param>
    /// <param name="entryIndex">Candidate index.</param>
    /// <param name="rebuild">Callback used after removals.</param>
    /// <param name="refreshPoolWarning">Callback refreshing the aggregate pool warning.</param>
    private static void BuildEntryHeader(VisualElement container,
                                         SerializedProperty entriesProperty,
                                         SerializedProperty weightedSelectionProperty,
                                         int entryIndex,
                                         Action rebuild,
                                         Action refreshPoolWarning)
    {
        SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex).Copy();
        SerializedProperty targetProperty = entryProperty.FindPropertyRelative("target");
        SerializedProperty customStatProperty = entryProperty.FindPropertyRelative("customScalableStatName");

        if (targetProperty == null || customStatProperty == null)
        {
            container.Add(new HelpBox("A Random Stat Growth entry is incomplete.", HelpBoxMessageType.Warning));
            return;
        }

        Foldout foldout = PlayerManagementFoldoutStateUtility.CreateFoldout(ResolveEntryTitle(targetProperty,
                                                                                               customStatProperty,
                                                                                               entryIndex),
                                                                            BuildEntryFoldoutStateKey(entriesProperty,
                                                                                                      entryProperty,
                                                                                                      entryIndex),
                                                                            false);
        foldout.style.marginBottom = 4f;
        container.Add(foldout);
        PlayerManagementFoldoutStateUtility.AttachLazyFoldout(foldout,
                                                               () => BuildEntryDetails(foldout,
                                                                                       entriesProperty,
                                                                                       weightedSelectionProperty,
                                                                                       entryIndex,
                                                                                       rebuild,
                                                                                       refreshPoolWarning));
        Action refreshTitle = () => foldout.text = ResolveEntryTitle(targetProperty,
                                                                      customStatProperty,
                                                                      entryIndex);
        foldout.TrackPropertyValue(targetProperty, changedProperty => refreshTitle());
        foldout.TrackPropertyValue(customStatProperty, changedProperty => refreshTitle());
    }

    /// <summary>
    /// Builds formula-aware controls and warnings after a candidate foldout is first opened.
    /// </summary>
    /// <param name="foldout">Candidate foldout receiving the deferred body.</param>
    /// <param name="entriesProperty">Serialized candidate array.</param>
    /// <param name="weightedSelectionProperty">Module-level weighted-selection toggle.</param>
    /// <param name="entryIndex">Candidate index.</param>
    /// <param name="rebuild">Callback used after removals.</param>
    /// <param name="refreshPoolWarning">Callback refreshing the aggregate pool warning.</param>
    private static void BuildEntryDetails(Foldout foldout,
                                          SerializedProperty entriesProperty,
                                          SerializedProperty weightedSelectionProperty,
                                          int entryIndex,
                                          Action rebuild,
                                          Action refreshPoolWarning)
    {
        if (entryIndex < 0 || entryIndex >= entriesProperty.arraySize)
            return;

        SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);
        SerializedProperty targetProperty = entryProperty.FindPropertyRelative("target");
        SerializedProperty customStatProperty = entryProperty.FindPropertyRelative("customScalableStatName");
        SerializedProperty minimumProperty = entryProperty.FindPropertyRelative("minimumIncrease");
        SerializedProperty maximumProperty = entryProperty.FindPropertyRelative("maximumIncrease");
        SerializedProperty weightProperty = entryProperty.FindPropertyRelative("selectionWeight");
        SerializedProperty useColorProperty = entryProperty.FindPropertyRelative("useCustomPresentationColor");
        SerializedProperty colorProperty = entryProperty.FindPropertyRelative("presentationColor");

        if (targetProperty == null ||
            customStatProperty == null ||
            minimumProperty == null ||
            maximumProperty == null ||
            weightProperty == null ||
            useColorProperty == null ||
            colorProperty == null)
        {
            foldout.Add(new HelpBox("A Random Stat Growth entry is incomplete.", HelpBoxMessageType.Warning));
            return;
        }

        SerializedProperty scalingRulesProperty = entryProperty.serializedObject.FindProperty("scalingRules");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(foldout, targetProperty, "Statistic");
        VisualElement customStatContainer = new VisualElement();
        foldout.Add(customStatContainer);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(foldout, minimumProperty, "Minimum Increase");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(foldout, maximumProperty, "Maximum Increase");
        VisualElement weightContainer = new VisualElement();
        foldout.Add(weightContainer);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(foldout,
                                                              useColorProperty,
                                                              "Use Custom Presentation Color");
        VisualElement colorContainer = new VisualElement();
        foldout.Add(colorContainer);
        HelpBox warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warning.style.display = DisplayStyle.None;
        foldout.Add(warning);
        Button removeButton = new Button(() => RemoveEntry(entriesProperty, entryIndex, rebuild))
        {
            text = "Remove Statistic"
        };
        removeButton.tooltip = "Removes this statistic candidate and its scaling bindings.";
        foldout.Add(removeButton);

        Action refresh = () => RefreshEntryUi(targetProperty,
                                               customStatProperty,
                                               minimumProperty,
                                               maximumProperty,
                                               weightProperty,
                                               weightedSelectionProperty,
                                               useColorProperty,
                                               colorProperty,
                                               scalingRulesProperty,
                                               customStatContainer,
                                               weightContainer,
                                               colorContainer,
                                               warning,
                                               refreshPoolWarning);
        foldout.TrackPropertyValue(targetProperty, changedProperty => refresh());
        foldout.TrackPropertyValue(customStatProperty, changedProperty => refresh());
        foldout.TrackPropertyValue(minimumProperty, changedProperty => refresh());
        foldout.TrackPropertyValue(maximumProperty, changedProperty => refresh());
        foldout.TrackPropertyValue(weightProperty, changedProperty => refresh());
        foldout.TrackPropertyValue(weightedSelectionProperty, changedProperty => refresh());
        foldout.TrackPropertyValue(useColorProperty, changedProperty => refresh());
        foldout.TrackPropertyValue(colorProperty, changedProperty => refresh());
        refresh.Invoke();
    }

    /// <summary>
    /// Shows context-sensitive controls, creates expensive fields once, and reports candidate inconsistencies.
    /// </summary>
    /// <param name="targetProperty">Serialized statistic target.</param>
    /// <param name="customStatProperty">Serialized custom scalable-stat name.</param>
    /// <param name="minimumProperty">Serialized minimum increase.</param>
    /// <param name="maximumProperty">Serialized maximum increase.</param>
    /// <param name="weightProperty">Serialized selection weight.</param>
    /// <param name="weightedSelectionProperty">Module-level weighted-selection toggle.</param>
    /// <param name="useColorProperty">Serialized custom-color toggle.</param>
    /// <param name="colorProperty">Serialized custom presentation color.</param>
    /// <param name="scalingRulesProperty">Unified scaling-rule collection.</param>
    /// <param name="customStatContainer">Lazy custom-stat selector host.</param>
    /// <param name="weightContainer">Lazy weight-field host.</param>
    /// <param name="colorContainer">Lazy color-field host.</param>
    /// <param name="warning">Candidate warning box.</param>
    /// <param name="refreshPoolWarning">Callback refreshing the aggregate pool warning.</param>
    private static void RefreshEntryUi(SerializedProperty targetProperty,
                                       SerializedProperty customStatProperty,
                                       SerializedProperty minimumProperty,
                                       SerializedProperty maximumProperty,
                                       SerializedProperty weightProperty,
                                       SerializedProperty weightedSelectionProperty,
                                       SerializedProperty useColorProperty,
                                       SerializedProperty colorProperty,
                                       SerializedProperty scalingRulesProperty,
                                       VisualElement customStatContainer,
                                       VisualElement weightContainer,
                                       VisualElement colorContainer,
                                       HelpBox warning,
                                       Action refreshPoolWarning)
    {
        bool invalidTarget = targetProperty.enumValueIndex < 0 ||
                             targetProperty.enumValueIndex > (int)PlayerRandomStatGrowthTarget.CustomScalableStat;
        PlayerRandomStatGrowthTarget target = (PlayerRandomStatGrowthTarget)targetProperty.enumValueIndex;
        bool usesCustomStat = target == PlayerRandomStatGrowthTarget.CustomScalableStat;
        bool usesWeightedSelection = weightedSelectionProperty.boolValue;
        bool usesCustomColor = useColorProperty.boolValue;

        EnsureConditionalField(customStatContainer,
                               usesCustomStat,
                               () => PowerUpRandomStatGrowthStatSelectorUtility.Create(customStatProperty,
                                                                                        scalingRulesProperty));
        EnsureConditionalField(weightContainer,
                               usesWeightedSelection,
                               () => CreateDetachedScalingField(weightProperty, "Selection Weight"));
        EnsureConditionalField(colorContainer,
                               usesCustomColor,
                               () => CreateDetachedScalingField(colorProperty, "Presentation Color"));

        bool invalidRange = !float.IsFinite(minimumProperty.floatValue) ||
                            !float.IsFinite(maximumProperty.floatValue) ||
                            minimumProperty.floatValue < 0f ||
                            maximumProperty.floatValue < minimumProperty.floatValue;
        bool invalidCustomStat = usesCustomStat &&
                                 (string.IsNullOrWhiteSpace(customStatProperty.stringValue) ||
                                  !PowerUpRandomStatGrowthStatSelectorUtility.ContainsNumericStat(customStatProperty.stringValue.Trim()));
        bool invalidWeight = usesWeightedSelection &&
                             (!float.IsFinite(weightProperty.floatValue) || weightProperty.floatValue < 0f);
        bool invalidColor = usesCustomColor && !IsFinite(colorProperty.colorValue);
        string warningText = string.Empty;

        if (invalidTarget)
            warningText = "Select a supported player statistic.";
        else if (invalidRange)
            warningText = "Increase values must be finite and non-negative, with Maximum Increase greater than or equal to Minimum Increase.";
        else if (invalidCustomStat)
            warningText = "Select an existing Float, Integer, or Unsigned scalable stat.";
        else if (invalidWeight)
            warningText = "Selection Weight must be finite and zero or greater.";
        else if (invalidColor)
            warningText = "Presentation Color channels must contain finite values.";

        warning.text = warningText;
        warning.style.display = string.IsNullOrEmpty(warningText) ? DisplayStyle.None : DisplayStyle.Flex;
        refreshPoolWarning.Invoke();
    }

    /// <summary>
    /// Creates one scaling-aware field without retaining a temporary parent container.
    /// </summary>
    /// <param name="property">Serialized property rendered by the field.</param>
    /// <param name="label">Visible field label.</param>
    /// <returns>Detached scaling-aware field root.</returns>
    private static VisualElement CreateDetachedScalingField(SerializedProperty property, string label)
    {
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        return PlayerScalingFieldElementFactory.CreateField(property,
                                                             scalingRulesProperty,
                                                             label);
    }

    /// <summary>
    /// Creates one conditional field at most once and updates its visibility without rebuilding formula controls.
    /// </summary>
    /// <param name="container">Conditional field host.</param>
    /// <param name="visible">Whether the field is currently relevant.</param>
    /// <param name="createField">Deferred field factory.</param>
    private static void EnsureConditionalField(VisualElement container,
                                               bool visible,
                                               Func<VisualElement> createField)
    {
        container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible || container.childCount > 0 || createField == null)
            return;

        VisualElement field = createField.Invoke();

        if (field != null)
            container.Add(field);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Reports empty pools and weighted configurations that cannot select any candidate.
    /// </summary>
    /// <param name="weightedSelectionProperty">Module-level weighted-selection toggle.</param>
    /// <param name="entriesProperty">Serialized candidate array.</param>
    /// <param name="warning">Aggregate warning box.</param>
    private static void RefreshPoolWarning(SerializedProperty weightedSelectionProperty,
                                           SerializedProperty entriesProperty,
                                           HelpBox warning)
    {
        string warningText = string.Empty;

        if (entriesProperty.arraySize <= 0)
        {
            warningText = "Add at least one statistic candidate. Empty pools cannot execute.";
        }
        else if (weightedSelectionProperty.boolValue && !HasPositiveWeight(entriesProperty))
        {
            warningText = "Weighted Selection requires at least one candidate with a finite positive weight.";
        }

        warning.text = warningText;
        warning.style.display = string.IsNullOrEmpty(warningText) ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /// <summary>
    /// Checks whether a candidate pool contains at least one finite positive selection weight.
    /// </summary>
    /// <param name="entriesProperty">Serialized candidate array.</param>
    /// <returns>True when weighted selection has one eligible authored weight.</returns>
    private static bool HasPositiveWeight(SerializedProperty entriesProperty)
    {
        for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
        {
            SerializedProperty weightProperty = entriesProperty.GetArrayElementAtIndex(entryIndex)
                                                               .FindPropertyRelative("selectionWeight");

            if (weightProperty != null &&
                float.IsFinite(weightProperty.floatValue) &&
                weightProperty.floatValue > 0f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether every color channel contains a finite value.
    /// </summary>
    /// <param name="color">Color value to inspect.</param>
    /// <returns>True when all channels are finite.</returns>
    private static bool IsFinite(Color color)
    {
        return float.IsFinite(color.r) &&
               float.IsFinite(color.g) &&
               float.IsFinite(color.b) &&
               float.IsFinite(color.a);
    }
    #endregion

    #region Mutations
    /// <summary>
    /// Appends one initialized candidate after recording the preset undo state.
    /// </summary>
    /// <param name="entriesProperty">Serialized candidate array.</param>
    /// <param name="rebuild">Callback rebuilding candidate controls.</param>
    private static void AddEntry(SerializedProperty entriesProperty, Action rebuild)
    {
        SerializedObject serializedObject = entriesProperty.serializedObject;

        if (serializedObject.targetObject != null)
            Undo.RecordObject(serializedObject.targetObject, "Add Random Growth Statistic");

        serializedObject.Update();
        int entryIndex = entriesProperty.arraySize;
        entriesProperty.InsertArrayElementAtIndex(entryIndex);
        SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);
        entryProperty.FindPropertyRelative("entryId").stringValue = Guid.NewGuid().ToString("N");
        entryProperty.FindPropertyRelative("target").enumValueIndex = (int)PlayerRandomStatGrowthTarget.ProjectileDamage;
        entryProperty.FindPropertyRelative("customScalableStatName").stringValue = string.Empty;
        entryProperty.FindPropertyRelative("minimumIncrease").floatValue = 1f;
        entryProperty.FindPropertyRelative("maximumIncrease").floatValue = 1f;
        entryProperty.FindPropertyRelative("selectionWeight").floatValue = 1f;
        entryProperty.FindPropertyRelative("useCustomPresentationColor").boolValue = false;
        entryProperty.FindPropertyRelative("presentationColor").colorValue = Color.white;
        serializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        rebuild.Invoke();
    }

    /// <summary>
    /// Removes one candidate after recording the preset undo state.
    /// </summary>
    /// <param name="entriesProperty">Serialized candidate array.</param>
    /// <param name="entryIndex">Candidate index to remove.</param>
    /// <param name="rebuild">Callback rebuilding candidate controls.</param>
    private static void RemoveEntry(SerializedProperty entriesProperty, int entryIndex, Action rebuild)
    {
        if (entryIndex < 0 || entryIndex >= entriesProperty.arraySize)
            return;

        SerializedObject serializedObject = entriesProperty.serializedObject;
        SerializedProperty scalingRulesProperty = serializedObject.FindProperty("scalingRules");

        if (serializedObject.targetObject != null)
            Undo.RecordObject(serializedObject.targetObject, "Remove Random Growth Statistic");

        serializedObject.Update();
        PlayerScalingArrayEntryRuleUtility.RemoveOwnedRules(serializedObject,
                                                            scalingRulesProperty,
                                                            entriesProperty.GetArrayElementAtIndex(entryIndex));
        entriesProperty.DeleteArrayElementAtIndex(entryIndex);
        PlayerScalingRuleStatKeyRefreshUtility.RefreshStatKeys(serializedObject);
        serializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        rebuild.Invoke();
    }
    #endregion

    #region Labels
    /// <summary>
    /// Builds a compact candidate title without constructing the candidate's editor body.
    /// </summary>
    /// <param name="targetProperty">Serialized statistic target.</param>
    /// <param name="customStatProperty">Serialized custom scalable-stat name.</param>
    /// <param name="entryIndex">Candidate array index.</param>
    /// <returns>Readable foldout title.</returns>
    private static string ResolveEntryTitle(SerializedProperty targetProperty,
                                            SerializedProperty customStatProperty,
                                            int entryIndex)
    {
        if (targetProperty.enumValueIndex < 0 ||
            targetProperty.enumValueIndex >= targetProperty.enumDisplayNames.Length)
        {
            return string.Format("Statistic {0}", entryIndex + 1);
        }

        string targetLabel = targetProperty.enumDisplayNames[targetProperty.enumValueIndex];

        if ((PlayerRandomStatGrowthTarget)targetProperty.enumValueIndex == PlayerRandomStatGrowthTarget.CustomScalableStat &&
            !string.IsNullOrWhiteSpace(customStatProperty.stringValue))
        {
            targetLabel = customStatProperty.stringValue.Trim();
        }

        return string.Format("Statistic {0} - {1}", entryIndex + 1, targetLabel);
    }

    /// <summary>
    /// Builds a stable foldout key from the entry ID so list reordering preserves expansion state.
    /// </summary>
    /// <param name="entriesProperty">Serialized candidate array.</param>
    /// <param name="entryProperty">Serialized candidate entry.</param>
    /// <param name="entryIndex">Fallback candidate index.</param>
    /// <returns>Stable foldout state key.</returns>
    private static string BuildEntryFoldoutStateKey(SerializedProperty entriesProperty,
                                                    SerializedProperty entryProperty,
                                                    int entryIndex)
    {
        SerializedProperty entryIdProperty = entryProperty.FindPropertyRelative("entryId");
        string entryId = entryIdProperty != null && !string.IsNullOrWhiteSpace(entryIdProperty.stringValue)
            ? entryIdProperty.stringValue
            : entryIndex.ToString();
        return string.Format("{0}|RandomStatGrowth:{1}",
                             PlayerManagementFoldoutStateUtility.BuildPropertyContextKey(entriesProperty),
                             entryId);
    }
    #endregion

    #endregion
}
