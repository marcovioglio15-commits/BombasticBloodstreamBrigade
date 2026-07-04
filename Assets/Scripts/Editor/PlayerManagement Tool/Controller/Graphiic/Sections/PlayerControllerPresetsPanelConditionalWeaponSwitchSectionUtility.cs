using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Conditional Weapon Switches dropdown shown directly below the shooting Values block. Designers
/// can append, remove and reorder entries; each entry binds a defined Weapon Id, a priority, an override flag
/// and a list of stat-driven inclusive range conditions. The serialized fields use the standard scalable field
/// factory so Add Scaling formulas are honored end-to-end alongside the rest of the shooting tool.
/// </summary>
internal static class PlayerControllerPresetsPanelConditionalWeaponSwitchSectionUtility
{
    #region Constants
    private const string EntriesPropertyName = "entries";
    private const string ConditionsPropertyName = "conditions";
    private const string WeaponIdPropertyName = "weaponId";
    private const string PriorityPropertyName = "priority";
    private const string OverridePropertyName = "overridePowerUpSwitch";
    private const string StatNamePropertyName = "statName";
    private const string MinimumValuePropertyName = "minimumValue";
    private const string MaximumValuePropertyName = "maximumValue";
    private const string RequirementPropertyName = "requirement";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the conditional weapon switches foldout and returns it so the shooting section utility can append
    /// it directly under the Values block. Returns an empty container with a warning when the supplied property
    /// is missing so the rest of the shooting section keeps rendering safely.
    /// </summary>
    /// <param name="conditionalSwitchesProperty">Serialized conditional weapon switches settings property.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    /// <returns>Configured foldout ready to be appended to the shooting section.</returns>
    public static Foldout Build(SerializedProperty conditionalSwitchesProperty, SerializedProperty scalingRulesProperty)
    {
        Foldout foldout = new Foldout();
        foldout.text = "Conditional Weapon Switches";
        foldout.value = false;
        foldout.tooltip = "Stat-driven weapon switches evaluated independently from the Switch Weapon power-up. Each entry can opt to override the equipped power-up via a per-entry flag.";
        foldout.style.marginTop = 4f;
        foldout.style.marginBottom = 4f;

        if (conditionalSwitchesProperty == null)
        {
            foldout.Add(new HelpBox("Conditional Weapon Switches container is missing on the active preset.", HelpBoxMessageType.Warning));
            return foldout;
        }

        SerializedProperty entriesProperty = conditionalSwitchesProperty.FindPropertyRelative(EntriesPropertyName);

        if (entriesProperty == null || !entriesProperty.isArray)
        {
            foldout.Add(new HelpBox("Conditional Weapon Switches entries array is missing on the active preset.", HelpBoxMessageType.Warning));
            return foldout;
        }

        VisualElement entriesContainer = new VisualElement();
        foldout.Add(entriesContainer);
        Button addButton = new Button(() => AppendEntry(entriesProperty, entriesContainer, scalingRulesProperty));
        addButton.text = "Add Conditional Weapon Switch";
        addButton.tooltip = "Adds one conditional entry below the existing list. Authored order breaks ties between entries with equal priority.";
        addButton.style.marginTop = 4f;
        foldout.Add(addButton);
        RebuildEntries(entriesProperty, entriesContainer, scalingRulesProperty);
        return foldout;
    }
    #endregion

    #region Entry Construction
    /// <summary>
    /// Rebuilds the visual list of entries in place from the current serialized entries array. The container is
    /// fully cleared and re-populated so insert/remove operations preserve consistent property paths.
    /// </summary>
    /// <param name="entriesProperty">Serialized entries array.</param>
    /// <param name="entriesContainer">Visual container hosting every entry foldout.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    private static void RebuildEntries(SerializedProperty entriesProperty,
                                       VisualElement entriesContainer,
                                       SerializedProperty scalingRulesProperty)
    {
        entriesContainer.Clear();

        if (entriesProperty.arraySize <= 0)
        {
            entriesContainer.Add(new HelpBox("No conditional weapon switches authored. Click \"Add Conditional Weapon Switch\" to create one.", HelpBoxMessageType.Info));
            return;
        }

        for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);
            entriesContainer.Add(BuildEntryFoldout(entriesProperty,
                                                    entryProperty,
                                                    entryIndex,
                                                    entriesContainer,
                                                    scalingRulesProperty));
        }
    }

    /// <summary>
    /// Builds one entry foldout with its weapon-id, priority and override controls plus the nested conditions
    /// section. Reuses the existing scalable Weapon Id selector so designers benefit from the same Add Scaling
    /// machinery as the Switch Weapon power-up module.
    /// </summary>
    /// <param name="entriesProperty">Parent entries array.</param>
    /// <param name="entryProperty">Serialized entry property being drawn.</param>
    /// <param name="entryIndex">Authored index of the entry in the array.</param>
    /// <param name="entriesContainer">Visual container hosting every entry foldout, used to rebuild on remove.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    /// <returns>Configured entry foldout.</returns>
    private static Foldout BuildEntryFoldout(SerializedProperty entriesProperty,
                                             SerializedProperty entryProperty,
                                             int entryIndex,
                                             VisualElement entriesContainer,
                                             SerializedProperty scalingRulesProperty)
    {
        Foldout entryFoldout = new Foldout();
        entryFoldout.text = ResolveEntryHeader(entryProperty, entryIndex);
        entryFoldout.value = true;
        entryFoldout.style.marginTop = 2f;
        entryFoldout.style.marginBottom = 2f;
        entryFoldout.style.paddingLeft = 6f;
        SerializedProperty weaponIdProperty = entryProperty.FindPropertyRelative(WeaponIdPropertyName);
        SerializedProperty priorityProperty = entryProperty.FindPropertyRelative(PriorityPropertyName);
        SerializedProperty overrideProperty = entryProperty.FindPropertyRelative(OverridePropertyName);
        SerializedProperty conditionsProperty = entryProperty.FindPropertyRelative(ConditionsPropertyName);
        VisualElement weaponIdField = PlayerWeaponIdSelectorUtility.CreateScalableSelector(weaponIdProperty,
                                                                                            scalingRulesProperty,
                                                                                            "Weapon Id",
                                                                                            "Defined Weapon Id from the resolved Player Visual Preset. <Use Visual Default> keeps the preset default attachment.",
                                                                                            PlayerWeaponIdSelectorUtility.UseVisualDefaultLabel,
                                                                                            () => PlayerWeaponIdSelectorUtility.BuildScopedSwitchWeaponOptions(weaponIdProperty));
        VisualElement priorityField = PlayerScalingFieldElementFactory.CreateField(priorityProperty,
                                                                                    scalingRulesProperty,
                                                                                    "Priority");
        priorityField.tooltip = "Higher priority entries win when multiple conditional entries match simultaneously. Authored order breaks ties.";
        VisualElement overrideField = PlayerScalingFieldElementFactory.CreateField(overrideProperty,
                                                                                    scalingRulesProperty,
                                                                                    "Override Power Up Switch");
        overrideField.tooltip = "When enabled, this conditional entry replaces the equipped Switch Weapon power-up selection. When disabled, the power-up keeps priority.";

        entryFoldout.Add(weaponIdField);
        entryFoldout.Add(priorityField);
        entryFoldout.Add(overrideField);
        entryFoldout.Add(BuildEntryWarnings(weaponIdProperty));
        entryFoldout.Add(BuildConditionsSection(conditionsProperty, scalingRulesProperty));
        entryFoldout.Add(BuildEntryToolbar(entriesProperty,
                                            entryIndex,
                                            entriesContainer,
                                            scalingRulesProperty));
        entryFoldout.TrackPropertyValue(weaponIdProperty, changedProperty =>
        {
            entryFoldout.text = ResolveEntryHeader(entryProperty, entryIndex);
        });
        return entryFoldout;
    }

    /// <summary>
    /// Builds the per-entry toolbar containing remove and reorder buttons. The reorder buttons rebuild the
    /// owning container after mutating the array so all subsequent foldouts pick up their new indices.
    /// </summary>
    /// <param name="entriesProperty">Parent entries array.</param>
    /// <param name="entryIndex">Authored index of the entry in the array.</param>
    /// <param name="entriesContainer">Visual container hosting every entry foldout.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    /// <returns>Configured toolbar.</returns>
    private static VisualElement BuildEntryToolbar(SerializedProperty entriesProperty,
                                                   int entryIndex,
                                                   VisualElement entriesContainer,
                                                   SerializedProperty scalingRulesProperty)
    {
        VisualElement toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.marginTop = 2f;
        Button moveUpButton = new Button(() => MoveEntry(entriesProperty, entryIndex, -1, entriesContainer, scalingRulesProperty));
        moveUpButton.text = "▲";
        moveUpButton.tooltip = "Move this entry up in the authored order.";
        moveUpButton.SetEnabled(entryIndex > 0);
        Button moveDownButton = new Button(() => MoveEntry(entriesProperty, entryIndex, 1, entriesContainer, scalingRulesProperty));
        moveDownButton.text = "▼";
        moveDownButton.tooltip = "Move this entry down in the authored order.";
        moveDownButton.SetEnabled(entryIndex < entriesProperty.arraySize - 1);
        Button removeButton = new Button(() => RemoveEntry(entriesProperty, entryIndex, entriesContainer, scalingRulesProperty));
        removeButton.text = "Remove";
        removeButton.tooltip = "Removes this conditional weapon switch entry from the authored list.";
        removeButton.style.marginLeft = 8f;
        toolbar.Add(moveUpButton);
        toolbar.Add(moveDownButton);
        toolbar.Add(removeButton);
        return toolbar;
    }

    /// <summary>
    /// Builds the per-entry warning box. Currently surfaces missing Weapon Id and oversized fixed-string
    /// payload diagnostics; entry-level warnings update reactively on weapon-id changes.
    /// </summary>
    /// <param name="weaponIdProperty">Serialized Weapon Id property.</param>
    /// <returns>Configured warning box.</returns>
    private static HelpBox BuildEntryWarnings(SerializedProperty weaponIdProperty)
    {
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.display = DisplayStyle.None;
        warningBox.TrackPropertyValue(weaponIdProperty, changedProperty => RefreshEntryWarnings(weaponIdProperty, warningBox));
        RefreshEntryWarnings(weaponIdProperty, warningBox);
        return warningBox;
    }

    /// <summary>
    /// Builds the conditions container nested inside one entry. Mirrors the entry list pattern so designers can
    /// append, remove and reorder conditions; each condition exposes a stat dropdown, an inclusive range and a
    /// requirement selector.
    /// </summary>
    /// <param name="conditionsProperty">Serialized entry conditions array.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    /// <returns>Configured conditions foldout.</returns>
    private static Foldout BuildConditionsSection(SerializedProperty conditionsProperty, SerializedProperty scalingRulesProperty)
    {
        Foldout conditionsFoldout = new Foldout();
        conditionsFoldout.text = "Conditions";
        conditionsFoldout.value = true;
        conditionsFoldout.style.marginLeft = 8f;

        if (conditionsProperty == null || !conditionsProperty.isArray)
        {
            conditionsFoldout.Add(new HelpBox("Conditions array is missing.", HelpBoxMessageType.Warning));
            return conditionsFoldout;
        }

        VisualElement conditionsContainer = new VisualElement();
        conditionsFoldout.Add(conditionsContainer);
        Button addConditionButton = new Button(() => AppendCondition(conditionsProperty, conditionsContainer, scalingRulesProperty));
        addConditionButton.text = "Add Condition";
        addConditionButton.tooltip = "Adds one new range condition. Each condition compares a scalable stat against an inclusive numeric range.";
        addConditionButton.style.marginTop = 4f;
        conditionsFoldout.Add(addConditionButton);
        RebuildConditions(conditionsProperty, conditionsContainer, scalingRulesProperty);
        return conditionsFoldout;
    }

    /// <summary>
    /// Rebuilds the visual list of conditions in place from the current serialized conditions array.
    /// </summary>
    /// <param name="conditionsProperty">Serialized conditions array.</param>
    /// <param name="conditionsContainer">Visual container hosting every condition.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    private static void RebuildConditions(SerializedProperty conditionsProperty,
                                          VisualElement conditionsContainer,
                                          SerializedProperty scalingRulesProperty)
    {
        conditionsContainer.Clear();

        if (conditionsProperty.arraySize <= 0)
        {
            conditionsContainer.Add(new HelpBox("No conditions authored. An entry with no conditions matches unconditionally and can serve as a stat-independent fallback.", HelpBoxMessageType.Info));
            return;
        }

        for (int conditionIndex = 0; conditionIndex < conditionsProperty.arraySize; conditionIndex++)
        {
            SerializedProperty conditionProperty = conditionsProperty.GetArrayElementAtIndex(conditionIndex);
            conditionsContainer.Add(BuildConditionRow(conditionsProperty,
                                                       conditionProperty,
                                                       conditionIndex,
                                                       conditionsContainer,
                                                       scalingRulesProperty));
        }
    }

    /// <summary>
    /// Builds one condition row. The row hosts the stat selector, the inclusive range fields, the requirement
    /// enum and a per-condition warning box for incoherent ranges or unsupported stat types.
    /// </summary>
    /// <param name="conditionsProperty">Parent conditions array.</param>
    /// <param name="conditionProperty">Serialized condition property being drawn.</param>
    /// <param name="conditionIndex">Authored index of the condition in the array.</param>
    /// <param name="conditionsContainer">Visual container hosting every condition.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    /// <returns>Configured condition row.</returns>
    private static VisualElement BuildConditionRow(SerializedProperty conditionsProperty,
                                                   SerializedProperty conditionProperty,
                                                   int conditionIndex,
                                                   VisualElement conditionsContainer,
                                                   SerializedProperty scalingRulesProperty)
    {
        VisualElement row = new VisualElement();
        row.style.marginTop = 2f;
        row.style.marginBottom = 2f;
        row.style.paddingLeft = 4f;
        SerializedProperty statNameProperty = conditionProperty.FindPropertyRelative(StatNamePropertyName);
        SerializedProperty minimumProperty = conditionProperty.FindPropertyRelative(MinimumValuePropertyName);
        SerializedProperty maximumProperty = conditionProperty.FindPropertyRelative(MaximumValuePropertyName);
        SerializedProperty requirementProperty = conditionProperty.FindPropertyRelative(RequirementPropertyName);
        VisualElement statSelector = PlayerConditionalWeaponSwitchStatSelectorUtility.BuildSelector(statNameProperty,
                                                                                                     "Scalable Stat",
                                                                                                     "Scalable stat declared in Level-Up & Progression. Token-typed stats are not supported by this numeric range gate.");
        VisualElement minimumField = PlayerScalingFieldElementFactory.CreateField(minimumProperty,
                                                                                   scalingRulesProperty,
                                                                                   "Minimum");
        minimumField.tooltip = "Inclusive lower bound. The condition is true when the resolved stat value is greater than or equal to this minimum.";
        VisualElement maximumField = PlayerScalingFieldElementFactory.CreateField(maximumProperty,
                                                                                   scalingRulesProperty,
                                                                                   "Maximum");
        maximumField.tooltip = "Inclusive upper bound. The condition is true when the resolved stat value is less than or equal to this maximum.";
        EnumField requirementField = new EnumField("Requirement", (PlayerConditionalWeaponSwitchConditionRequirement)requirementProperty.enumValueIndex);
        requirementField.tooltip = "Sufficient: any-match contribution. Necessary: must be true. Necessary And Sufficient: must be true and counts toward the sufficient group.";
        requirementField.RegisterValueChangedCallback(evt =>
        {
            if (requirementProperty.serializedObject == null)
                return;

            requirementProperty.serializedObject.Update();
            requirementProperty.enumValueIndex = (int)(PlayerConditionalWeaponSwitchConditionRequirement)evt.newValue;
            requirementProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.display = DisplayStyle.None;
        row.Add(statSelector);
        row.Add(minimumField);
        row.Add(maximumField);
        row.Add(requirementField);
        row.Add(warningBox);
        row.Add(BuildConditionToolbar(conditionsProperty,
                                       conditionIndex,
                                       conditionsContainer,
                                       scalingRulesProperty));
        row.TrackPropertyValue(statNameProperty, changedProperty => RefreshConditionWarnings(statNameProperty, minimumProperty, maximumProperty, warningBox));
        row.TrackPropertyValue(minimumProperty, changedProperty => RefreshConditionWarnings(statNameProperty, minimumProperty, maximumProperty, warningBox));
        row.TrackPropertyValue(maximumProperty, changedProperty => RefreshConditionWarnings(statNameProperty, minimumProperty, maximumProperty, warningBox));
        RefreshConditionWarnings(statNameProperty, minimumProperty, maximumProperty, warningBox);
        return row;
    }

    /// <summary>
    /// Builds the per-condition toolbar containing remove and reorder buttons.
    /// </summary>
    /// <param name="conditionsProperty">Parent conditions array.</param>
    /// <param name="conditionIndex">Authored index of the condition in the array.</param>
    /// <param name="conditionsContainer">Visual container hosting every condition.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    /// <returns>Configured toolbar.</returns>
    private static VisualElement BuildConditionToolbar(SerializedProperty conditionsProperty,
                                                       int conditionIndex,
                                                       VisualElement conditionsContainer,
                                                       SerializedProperty scalingRulesProperty)
    {
        VisualElement toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.marginTop = 2f;
        Button moveUpButton = new Button(() => MoveCondition(conditionsProperty, conditionIndex, -1, conditionsContainer, scalingRulesProperty));
        moveUpButton.text = "▲";
        moveUpButton.tooltip = "Move this condition up in the authored order.";
        moveUpButton.SetEnabled(conditionIndex > 0);
        Button moveDownButton = new Button(() => MoveCondition(conditionsProperty, conditionIndex, 1, conditionsContainer, scalingRulesProperty));
        moveDownButton.text = "▼";
        moveDownButton.tooltip = "Move this condition down in the authored order.";
        moveDownButton.SetEnabled(conditionIndex < conditionsProperty.arraySize - 1);
        Button removeButton = new Button(() => RemoveCondition(conditionsProperty, conditionIndex, conditionsContainer, scalingRulesProperty));
        removeButton.text = "Remove";
        removeButton.tooltip = "Removes this condition.";
        removeButton.style.marginLeft = 8f;
        toolbar.Add(moveUpButton);
        toolbar.Add(moveDownButton);
        toolbar.Add(removeButton);
        return toolbar;
    }
    #endregion

    #region Mutations
    /// <summary>
    /// Appends one new entry pre-populated with a placeholder weapon id and an empty condition list, then
    /// rebuilds the visual container so the new entry appears immediately.
    /// </summary>
    /// <param name="entriesProperty">Serialized entries array.</param>
    /// <param name="entriesContainer">Visual container hosting every entry foldout.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    private static void AppendEntry(SerializedProperty entriesProperty,
                                    VisualElement entriesContainer,
                                    SerializedProperty scalingRulesProperty)
    {
        entriesProperty.serializedObject.Update();
        int insertIndex = entriesProperty.arraySize;
        entriesProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty newEntry = entriesProperty.GetArrayElementAtIndex(insertIndex);
        SerializedProperty newWeaponId = newEntry.FindPropertyRelative(WeaponIdPropertyName);
        SerializedProperty newPriority = newEntry.FindPropertyRelative(PriorityPropertyName);
        SerializedProperty newOverride = newEntry.FindPropertyRelative(OverridePropertyName);
        SerializedProperty newConditions = newEntry.FindPropertyRelative(ConditionsPropertyName);

        if (newWeaponId != null)
            newWeaponId.stringValue = string.Empty;

        if (newPriority != null)
            newPriority.intValue = 0;

        if (newOverride != null)
            newOverride.boolValue = false;

        if (newConditions != null && newConditions.isArray)
            newConditions.ClearArray();

        entriesProperty.serializedObject.ApplyModifiedProperties();
        RefreshScalingKeysAndMarkDirty(entriesProperty.serializedObject);
        RebuildEntries(entriesProperty, entriesContainer, scalingRulesProperty);
    }

    /// <summary>
    /// Removes one entry at the given index and rebuilds the visual container.
    /// </summary>
    /// <param name="entriesProperty">Serialized entries array.</param>
    /// <param name="entryIndex">Index of the entry being removed.</param>
    /// <param name="entriesContainer">Visual container hosting every entry foldout.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    private static void RemoveEntry(SerializedProperty entriesProperty,
                                    int entryIndex,
                                    VisualElement entriesContainer,
                                    SerializedProperty scalingRulesProperty)
    {
        if (entryIndex < 0 || entryIndex >= entriesProperty.arraySize)
            return;

        entriesProperty.serializedObject.Update();
        entriesProperty.DeleteArrayElementAtIndex(entryIndex);
        entriesProperty.serializedObject.ApplyModifiedProperties();
        RefreshScalingKeysAndMarkDirty(entriesProperty.serializedObject);
        RebuildEntries(entriesProperty, entriesContainer, scalingRulesProperty);
    }

    /// <summary>
    /// Moves one entry by the supplied delta and rebuilds the visual container so labels and indices refresh.
    /// </summary>
    /// <param name="entriesProperty">Serialized entries array.</param>
    /// <param name="entryIndex">Index of the entry being moved.</param>
    /// <param name="delta">Direction and distance to move (positive moves down).</param>
    /// <param name="entriesContainer">Visual container hosting every entry foldout.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    private static void MoveEntry(SerializedProperty entriesProperty,
                                  int entryIndex,
                                  int delta,
                                  VisualElement entriesContainer,
                                  SerializedProperty scalingRulesProperty)
    {
        int targetIndex = entryIndex + delta;

        if (targetIndex < 0 || targetIndex >= entriesProperty.arraySize)
            return;

        entriesProperty.serializedObject.Update();
        entriesProperty.MoveArrayElement(entryIndex, targetIndex);
        entriesProperty.serializedObject.ApplyModifiedProperties();
        RefreshScalingKeysAndMarkDirty(entriesProperty.serializedObject);
        RebuildEntries(entriesProperty, entriesContainer, scalingRulesProperty);
    }

    /// <summary>
    /// Appends one new condition pre-populated with default range bounds and a Necessary And Sufficient
    /// requirement, then rebuilds the visual container so the new row appears immediately.
    /// </summary>
    /// <param name="conditionsProperty">Serialized conditions array.</param>
    /// <param name="conditionsContainer">Visual container hosting every condition.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    private static void AppendCondition(SerializedProperty conditionsProperty,
                                        VisualElement conditionsContainer,
                                        SerializedProperty scalingRulesProperty)
    {
        conditionsProperty.serializedObject.Update();
        int insertIndex = conditionsProperty.arraySize;
        conditionsProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty newCondition = conditionsProperty.GetArrayElementAtIndex(insertIndex);
        SerializedProperty newStatName = newCondition.FindPropertyRelative(StatNamePropertyName);
        SerializedProperty newMinimum = newCondition.FindPropertyRelative(MinimumValuePropertyName);
        SerializedProperty newMaximum = newCondition.FindPropertyRelative(MaximumValuePropertyName);
        SerializedProperty newRequirement = newCondition.FindPropertyRelative(RequirementPropertyName);

        if (newStatName != null)
            newStatName.stringValue = string.Empty;

        if (newMinimum != null)
            newMinimum.floatValue = 0f;

        if (newMaximum != null)
            newMaximum.floatValue = 1f;

        if (newRequirement != null)
            newRequirement.enumValueIndex = (int)PlayerConditionalWeaponSwitchConditionRequirement.NecessaryAndSufficient;

        conditionsProperty.serializedObject.ApplyModifiedProperties();
        RefreshScalingKeysAndMarkDirty(conditionsProperty.serializedObject);
        RebuildConditions(conditionsProperty, conditionsContainer, scalingRulesProperty);
    }

    /// <summary>
    /// Removes one condition at the given index and rebuilds the visual container.
    /// </summary>
    /// <param name="conditionsProperty">Serialized conditions array.</param>
    /// <param name="conditionIndex">Index of the condition being removed.</param>
    /// <param name="conditionsContainer">Visual container hosting every condition.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    private static void RemoveCondition(SerializedProperty conditionsProperty,
                                        int conditionIndex,
                                        VisualElement conditionsContainer,
                                        SerializedProperty scalingRulesProperty)
    {
        if (conditionIndex < 0 || conditionIndex >= conditionsProperty.arraySize)
            return;

        conditionsProperty.serializedObject.Update();
        conditionsProperty.DeleteArrayElementAtIndex(conditionIndex);
        conditionsProperty.serializedObject.ApplyModifiedProperties();
        RefreshScalingKeysAndMarkDirty(conditionsProperty.serializedObject);
        RebuildConditions(conditionsProperty, conditionsContainer, scalingRulesProperty);
    }

    /// <summary>
    /// Moves one condition by the supplied delta and rebuilds the visual container so labels and indices refresh.
    /// </summary>
    /// <param name="conditionsProperty">Serialized conditions array.</param>
    /// <param name="conditionIndex">Index of the condition being moved.</param>
    /// <param name="delta">Direction and distance to move (positive moves down).</param>
    /// <param name="conditionsContainer">Visual container hosting every condition.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property used by Add Scaling fields.</param>
    private static void MoveCondition(SerializedProperty conditionsProperty,
                                      int conditionIndex,
                                      int delta,
                                      VisualElement conditionsContainer,
                                      SerializedProperty scalingRulesProperty)
    {
        int targetIndex = conditionIndex + delta;

        if (targetIndex < 0 || targetIndex >= conditionsProperty.arraySize)
            return;

        conditionsProperty.serializedObject.Update();
        conditionsProperty.MoveArrayElement(conditionIndex, targetIndex);
        conditionsProperty.serializedObject.ApplyModifiedProperties();
        RefreshScalingKeysAndMarkDirty(conditionsProperty.serializedObject);
        RebuildConditions(conditionsProperty, conditionsContainer, scalingRulesProperty);
    }

    /// <summary>
    /// Repairs stable Add Scaling keys after a nested conditional-switch list mutation and marks the draft dirty.
    /// </summary>
    /// <param name="serializedObject">Controller preset serialized object containing the nested entries and scaling rules.</param>
    private static void RefreshScalingKeysAndMarkDirty(SerializedObject serializedObject)
    {
        if (serializedObject == null)
            return;

        serializedObject.Update();
        PlayerScalingRuleStatKeyRefreshUtility.RefreshStatKeys(serializedObject);
        serializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
    }
    #endregion

    #region Diagnostics
    /// <summary>
    /// Resolves the visible foldout header for one entry so designers can spot the bound weapon id at a glance.
    /// </summary>
    /// <param name="entryProperty">Serialized entry property.</param>
    /// <param name="entryIndex">Authored entry index.</param>
    /// <returns>Foldout header label.</returns>
    private static string ResolveEntryHeader(SerializedProperty entryProperty, int entryIndex)
    {
        SerializedProperty weaponIdProperty = entryProperty.FindPropertyRelative(WeaponIdPropertyName);
        string weaponId = weaponIdProperty != null && !string.IsNullOrWhiteSpace(weaponIdProperty.stringValue)
            ? weaponIdProperty.stringValue
            : "<Use Visual Default>";
        return "[" + entryIndex + "] " + weaponId;
    }

    /// <summary>
    /// Refreshes the per-entry warning box with weapon-id integrity diagnostics. Empty weapon ids are tolerated
    /// because the runtime falls back to the visual preset default attachment; oversized strings cannot be baked
    /// and surface a warning.
    /// </summary>
    /// <param name="weaponIdProperty">Serialized weapon-id property.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void RefreshEntryWarnings(SerializedProperty weaponIdProperty, HelpBox warningBox)
    {
        if (weaponIdProperty == null)
        {
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        string weaponId = string.IsNullOrWhiteSpace(weaponIdProperty.stringValue)
            ? string.Empty
            : weaponIdProperty.stringValue.Trim();

        if (weaponId.Length <= 0)
        {
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        if (Encoding.UTF8.GetByteCount(weaponId) > PlayerWeaponVisualSettings.MaximumWeaponIdUtf8Bytes)
        {
            warningBox.text = "Weapon Id exceeds the ECS fixed-string capacity and cannot be baked.";
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        List<string> availableWeaponIds = PlayerWeaponIdSelectorUtility.BuildScopedSwitchWeaponOptions(weaponIdProperty);

        if (!PlayerWeaponIdSelectorUtility.ContainsWeaponId(availableWeaponIds, weaponId))
        {
            warningBox.text = "Weapon Id does not match any mountable entry in the registered Gameplay Visual Presets.";
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        warningBox.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Refreshes the per-condition warning box. Reports unsupported stat types, missing stat references and
    /// inverted ranges without snapping the authored values back to safe defaults.
    /// </summary>
    /// <param name="statNameProperty">Serialized stat-name property.</param>
    /// <param name="minimumProperty">Serialized minimum property.</param>
    /// <param name="maximumProperty">Serialized maximum property.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void RefreshConditionWarnings(SerializedProperty statNameProperty,
                                                 SerializedProperty minimumProperty,
                                                 SerializedProperty maximumProperty,
                                                 HelpBox warningBox)
    {
        StringBuilder warningBuilder = new StringBuilder(128);
        List<PlayerConditionalWeaponSwitchStatOption> statOptions = PlayerConditionalWeaponSwitchStatSelectorUtility.BuildScopedStatOptions();
        string statName = statNameProperty != null && !string.IsNullOrWhiteSpace(statNameProperty.stringValue)
            ? statNameProperty.stringValue.Trim()
            : string.Empty;

        if (statName.Length <= 0)
        {
            warningBuilder.Append("No scalable stat selected. This condition will always fail at runtime.");
        }
        else if (!PlayerConditionalWeaponSwitchStatSelectorUtility.ContainsStat(statOptions, statName))
        {
            warningBuilder.Append("Stat \"");
            warningBuilder.Append(statName);
            warningBuilder.Append("\" does not exist in the resolved Progression Preset. Rename or remove the reference.");
        }
        else
        {
            PlayerScalableStatType? resolvedType = PlayerConditionalWeaponSwitchStatSelectorUtility.TryGetStatType(statOptions, statName);

            if (resolvedType.HasValue && resolvedType.Value == PlayerScalableStatType.Token)
                warningBuilder.Append("Token-typed scalable stats are not supported by inclusive numeric range conditions.");
        }

        if (minimumProperty != null && maximumProperty != null && minimumProperty.floatValue > maximumProperty.floatValue)
        {
            if (warningBuilder.Length > 0)
                warningBuilder.AppendLine();

            warningBuilder.Append("Minimum is greater than Maximum. The runtime tolerates inverted bounds but designers usually mean the natural order.");
        }

        if (warningBuilder.Length <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = warningBuilder.ToString();
        warningBox.style.display = DisplayStyle.Flex;
    }
    #endregion

    #endregion
}
