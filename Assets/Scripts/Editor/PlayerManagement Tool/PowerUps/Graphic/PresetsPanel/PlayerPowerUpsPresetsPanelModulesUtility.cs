using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds and mutates the modules-management section of the power-ups presets panel.
/// </summary>
public static class PlayerPowerUpsPresetsPanelModulesUtility
{
    #region Methods

    #region Section Builders
    public static void BuildModulesManagementSection(PlayerPowerUpsPresetsPanel panel)
    {
        Label header = new Label("Modules Management");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.PowerUps.Modules.Management");
        panel.sectionContentRoot.Add(header);

        SerializedProperty moduleDefinitionsProperty = panel.presetSerializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
        {
            Label missingLabel = new Label("Module definitions property is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            panel.sectionContentRoot.Add(missingLabel);
            return;
        }

        Label moduleInfoLabel = new Label("Reusable module catalog used by Active and Passive Power Ups.");
        moduleInfoLabel.style.marginBottom = 4f;
        panel.sectionContentRoot.Add(moduleInfoLabel);

        VisualElement filtersRow = new VisualElement();
        filtersRow.style.flexDirection = FlexDirection.Row;
        filtersRow.style.alignItems = Align.FlexEnd;
        filtersRow.style.marginBottom = 4f;

        TextField moduleIdFilterField = new TextField("Filter Module ID");
        moduleIdFilterField.isDelayed = true;
        moduleIdFilterField.value = panel.moduleIdFilterText;
        moduleIdFilterField.style.flexGrow = 1f;
        moduleIdFilterField.style.marginRight = 6f;
        moduleIdFilterField.tooltip = "Show only modules whose Module ID contains this text.";
        filtersRow.Add(moduleIdFilterField);

        TextField moduleDisplayNameFilterField = new TextField("Filter Display Name");
        moduleDisplayNameFilterField.isDelayed = true;
        moduleDisplayNameFilterField.value = panel.moduleDisplayNameFilterText;
        moduleDisplayNameFilterField.style.flexGrow = 1f;
        moduleDisplayNameFilterField.tooltip = "Show only modules whose Display Name contains this text.";
        filtersRow.Add(moduleDisplayNameFilterField);
        panel.sectionContentRoot.Add(filtersRow);

        Label moduleCountLabel = new Label();
        ScrollView moduleCardsContainer = new ScrollView();
        VisualElement moduleActionsRow = new VisualElement();
        moduleActionsRow.style.flexDirection = FlexDirection.Row;
        moduleActionsRow.style.alignItems = Align.Center;
        moduleActionsRow.style.marginBottom = 4f;

        Button addModuleButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                AddModuleDefinition(panel);
            });
        });
        addModuleButton.text = "Add Module";
        addModuleButton.tooltip = "Create a new module definition entry.";
        moduleActionsRow.Add(addModuleButton);

        Button expandAllButton = new Button(() =>
        {
            SetAllModuleFoldoutStates(panel, true);
            RebuildModuleDefinitionCards(panel, moduleCardsContainer, moduleCountLabel);
        });
        expandAllButton.text = "Expand All";
        expandAllButton.tooltip = "Expand every visible module card.";
        expandAllButton.style.marginLeft = 4f;
        moduleActionsRow.Add(expandAllButton);

        Button collapseAllButton = new Button(() =>
        {
            SetAllModuleFoldoutStates(panel, false);
            RebuildModuleDefinitionCards(panel, moduleCardsContainer, moduleCountLabel);
        });
        collapseAllButton.text = "Collapse All";
        collapseAllButton.tooltip = "Collapse every visible module card.";
        collapseAllButton.style.marginLeft = 4f;
        moduleActionsRow.Add(collapseAllButton);

        Button clearFiltersButton = new Button(() =>
        {
            panel.moduleIdFilterText = string.Empty;
            panel.moduleDisplayNameFilterText = string.Empty;
            moduleIdFilterField.SetValueWithoutNotify(string.Empty);
            moduleDisplayNameFilterField.SetValueWithoutNotify(string.Empty);
            RebuildModuleDefinitionCards(panel, moduleCardsContainer, moduleCountLabel);
        });
        clearFiltersButton.text = "Clear Filters";
        clearFiltersButton.tooltip = "Clear both module search filters.";
        clearFiltersButton.style.marginLeft = 4f;
        moduleActionsRow.Add(clearFiltersButton);
        panel.sectionContentRoot.Add(moduleActionsRow);

        moduleCountLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        moduleCountLabel.style.marginBottom = 2f;
        panel.sectionContentRoot.Add(moduleCountLabel);

        moduleCardsContainer.style.maxHeight = 520f;
        moduleCardsContainer.style.paddingRight = 2f;
        moduleCardsContainer.style.marginBottom = 2f;
        panel.sectionContentRoot.Add(moduleCardsContainer);

        moduleIdFilterField.RegisterValueChangedCallback(evt =>
        {
            panel.moduleIdFilterText = evt.newValue;
            RebuildModuleDefinitionCards(panel, moduleCardsContainer, moduleCountLabel);
        });

        moduleDisplayNameFilterField.RegisterValueChangedCallback(evt =>
        {
            panel.moduleDisplayNameFilterText = evt.newValue;
            RebuildModuleDefinitionCards(panel, moduleCardsContainer, moduleCountLabel);
        });

        RebuildModuleDefinitionCards(panel, moduleCardsContainer, moduleCountLabel);
    }

    private static void RebuildModuleDefinitionCards(PlayerPowerUpsPresetsPanel panel, VisualElement moduleCardsContainer, Label moduleCountLabel)
    {
        if (moduleCardsContainer == null)
            return;

        moduleCardsContainer.Clear();

        if (panel.presetSerializedObject == null)
        {
            if (moduleCountLabel != null)
                moduleCountLabel.text = "Visible Modules: 0 / 0";

            return;
        }

        SerializedProperty moduleDefinitionsProperty = panel.presetSerializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
        {
            if (moduleCountLabel != null)
                moduleCountLabel.text = "Visible Modules: 0 / 0";

            HelpBox missingHelpBox = new HelpBox("Module definitions property is missing.", HelpBoxMessageType.Warning);
            moduleCardsContainer.Add(missingHelpBox);
            return;
        }

        int totalModules = moduleDefinitionsProperty.arraySize;
        int visibleModules = 0;
        HashSet<string> validFoldoutStateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int moduleIndex = 0; moduleIndex < moduleDefinitionsProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(moduleIndex);

            if (moduleProperty == null)
                continue;

            string moduleId = ResolveModuleDefinitionId(moduleProperty);
            string displayName = ResolveModuleDefinitionDisplayName(moduleProperty);
            PowerUpModuleKind moduleKind = ResolveModuleDefinitionKind(moduleProperty);
            string foldoutStateKey = BuildModuleFoldoutStateKey(moduleProperty);
            validFoldoutStateKeys.Add(foldoutStateKey);

            if (!IsMatchingModuleFilters(panel, moduleId, displayName))
                continue;

            VisualElement moduleCard = CreateModuleDefinitionCard(panel,
                                                                  moduleDefinitionsProperty,
                                                                  moduleProperty,
                                                                  moduleIndex,
                                                                  moduleId,
                                                                  displayName,
                                                                  moduleKind);
            moduleCardsContainer.Add(moduleCard);
            visibleModules += 1;
        }

        PruneFoldoutStateMap(panel.moduleDefinitionFoldoutStateByKey, validFoldoutStateKeys);

        if (moduleCountLabel != null)
            moduleCountLabel.text = string.Format("Visible Modules: {0} / {1}", visibleModules, totalModules);

        if (visibleModules > 0)
            return;

        HelpBox emptyHelpBox = new HelpBox("No modules match current filters.", HelpBoxMessageType.Info);
        moduleCardsContainer.Add(emptyHelpBox);
    }

    private static VisualElement CreateModuleDefinitionCard(PlayerPowerUpsPresetsPanel panel,
                                                            SerializedProperty moduleDefinitionsProperty,
                                                            SerializedProperty moduleProperty,
                                                            int moduleIndex,
                                                            string moduleId,
                                                            string displayName,
                                                            PowerUpModuleKind moduleKind)
    {
        VisualElement card = new VisualElement();
        card.style.marginBottom = 10f;
        card.style.paddingLeft = 6f;
        card.style.paddingRight = 6f;
        card.style.paddingTop = 4f;
        card.style.paddingBottom = 4f;
        card.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
        card.style.borderBottomWidth = 1f;
        card.style.borderTopWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderBottomColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderTopColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);

        string foldoutStateKey = BuildModuleFoldoutStateKey(moduleProperty);
        Foldout foldout = PlayerManagementFoldoutStateUtility.CreateFoldout(BuildModuleCardTitle(moduleIndex,
                                                                                                moduleId,
                                                                                                displayName,
                                                                                                moduleKind),
                                                                            foldoutStateKey,
                                                                            ResolveModuleFoldoutState(panel, foldoutStateKey));
        card.Add(foldout);

        VisualElement lazyContent = new VisualElement();
        foldout.Add(lazyContent);

        EnsureModuleDefinitionFoldoutContent(panel,
                                             moduleDefinitionsProperty,
                                             moduleProperty,
                                             moduleIndex,
                                             lazyContent,
                                             foldout.value);
        foldout.RegisterValueChangedCallback(evt =>
        {
            EnsureModuleDefinitionFoldoutContent(panel,
                                                 moduleDefinitionsProperty,
                                                 moduleProperty,
                                                 moduleIndex,
                                                 lazyContent,
                                                 evt.newValue);
        });

        return card;
    }

    /// <summary>
    /// Builds the heavy serialized module UI only when a module card is actually expanded.
    /// </summary>
    /// <param name="panel">Power-ups panel that schedules structural mutations.</param>
    /// <param name="moduleDefinitionsProperty">Serialized module definitions array owning the card.</param>
    /// <param name="moduleProperty">Serialized module definition represented by the card.</param>
    /// <param name="moduleIndex">Current array index used by move, duplicate and delete actions.</param>
    /// <param name="contentContainer">Foldout child container receiving the deferred UI subtree.</param>
    /// <param name="expanded">True when the foldout is open and content is needed.</param>
    private static void EnsureModuleDefinitionFoldoutContent(PlayerPowerUpsPresetsPanel panel,
                                                             SerializedProperty moduleDefinitionsProperty,
                                                             SerializedProperty moduleProperty,
                                                             int moduleIndex,
                                                             VisualElement contentContainer,
                                                             bool expanded)
    {
        if (contentContainer == null)
            return;

        if (!expanded)
            return;

        if (contentContainer.childCount > 0)
            return;

        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.marginLeft = 14f;
        actionsRow.style.marginTop = 2f;
        actionsRow.style.marginBottom = 4f;
        contentContainer.Add(actionsRow);

        Button duplicateButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                DuplicateModuleDefinition(panel, moduleIndex);
            });
        });
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate this module definition.";
        actionsRow.Add(duplicateButton);

        Button moveUpButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                MoveModuleDefinition(panel, moduleIndex, moduleIndex - 1);
            });
        });
        moveUpButton.text = "Up";
        moveUpButton.tooltip = "Move this module one position up.";
        moveUpButton.style.marginLeft = 4f;
        moveUpButton.SetEnabled(moduleIndex > 0);
        actionsRow.Add(moveUpButton);

        Button moveDownButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                MoveModuleDefinition(panel, moduleIndex, moduleIndex + 1);
            });
        });
        moveDownButton.text = "Down";
        moveDownButton.tooltip = "Move this module one position down.";
        moveDownButton.style.marginLeft = 4f;
        moveDownButton.SetEnabled(moduleIndex < moduleDefinitionsProperty.arraySize - 1);
        actionsRow.Add(moveDownButton);

        Button deleteButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                DeleteModuleDefinition(panel, moduleIndex);
            });
        });
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Delete this module definition.";
        deleteButton.style.marginLeft = 4f;
        actionsRow.Add(deleteButton);

        PropertyField moduleField = new PropertyField(moduleProperty);
        moduleField.BindProperty(moduleProperty);
        contentContainer.Add(moduleField);
    }
    #endregion

    #region Mutations
    private static void AddModuleDefinition(PlayerPowerUpsPresetsPanel panel)
    {
        ApplyModuleDefinitionsMutation(panel, "Add Module Definition", moduleDefinitionsProperty =>
        {
            int insertIndex = moduleDefinitionsProperty.arraySize;
            moduleDefinitionsProperty.arraySize = insertIndex + 1;
            SerializedProperty moduleProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(insertIndex);
            string uniqueModuleId = GenerateUniqueModuleId(moduleDefinitionsProperty, "ModuleCustom", insertIndex);
            InitializeNewModuleDefinition(moduleProperty, uniqueModuleId, "New Module");
            panel.moduleDefinitionFoldoutStateByKey[BuildModuleFoldoutStateKey(moduleProperty)] = true;
        });
    }

    private static void DuplicateModuleDefinition(PlayerPowerUpsPresetsPanel panel, int moduleIndex)
    {
        ApplyModuleDefinitionsMutation(panel, "Duplicate Module Definition", moduleDefinitionsProperty =>
        {
            if (moduleIndex < 0 || moduleIndex >= moduleDefinitionsProperty.arraySize)
                return;

            moduleDefinitionsProperty.InsertArrayElementAtIndex(moduleIndex);
            moduleDefinitionsProperty.MoveArrayElement(moduleIndex, moduleIndex + 1);
            int duplicatedIndex = moduleIndex + 1;
            SerializedProperty duplicatedProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(duplicatedIndex);
            string baseModuleId = ResolveModuleDefinitionId(duplicatedProperty);
            string copiedDisplayName = ResolveModuleDefinitionDisplayName(duplicatedProperty);
            string uniqueModuleId = GenerateUniqueModuleId(moduleDefinitionsProperty, baseModuleId, duplicatedIndex);
            SetModuleDefinitionId(duplicatedProperty, uniqueModuleId);

            if (string.IsNullOrWhiteSpace(copiedDisplayName))
                copiedDisplayName = "New Module";

            SetModuleDefinitionDisplayName(duplicatedProperty, copiedDisplayName + " Copy");
            panel.moduleDefinitionFoldoutStateByKey[BuildModuleFoldoutStateKey(duplicatedProperty)] = true;
        });
    }

    private static void DeleteModuleDefinition(PlayerPowerUpsPresetsPanel panel, int moduleIndex)
    {
        ApplyModuleDefinitionsMutation(panel, "Delete Module Definition", moduleDefinitionsProperty =>
        {
            if (moduleIndex < 0 || moduleIndex >= moduleDefinitionsProperty.arraySize)
                return;

            moduleDefinitionsProperty.DeleteArrayElementAtIndex(moduleIndex);
        });
    }

    private static void MoveModuleDefinition(PlayerPowerUpsPresetsPanel panel, int fromIndex, int toIndex)
    {
        if (panel == null || panel.selectedPreset == null)
            return;

        Undo.RecordObject(panel.selectedPreset, "Move Module Definition");

        if (!PlayerPowerUpsPresetReorderUtility.MoveModuleDefinition(panel.selectedPreset, fromIndex, toIndex))
            return;

        EditorUtility.SetDirty(panel.selectedPreset);

        if (panel.presetSerializedObject != null)
        {
            panel.presetSerializedObject.Update();

            if (PlayerScalingRuleStatKeyRefreshUtility.RefreshStatKeys(panel.presetSerializedObject))
                panel.presetSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerManagementDraftSession.MarkDirty();
        panel.ScheduleActiveSectionRebuild();
    }

    private static void SetAllModuleFoldoutStates(PlayerPowerUpsPresetsPanel panel, bool expanded)
    {
        if (panel.presetSerializedObject == null)
            return;

        SerializedProperty moduleDefinitionsProperty = panel.presetSerializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
            return;

        for (int moduleIndex = 0; moduleIndex < moduleDefinitionsProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(moduleIndex);

            if (moduleProperty == null)
                continue;

            string moduleId = ResolveModuleDefinitionId(moduleProperty);
            string displayName = ResolveModuleDefinitionDisplayName(moduleProperty);

            if (!IsMatchingModuleFilters(panel, moduleId, displayName))
                continue;

            string stateKey = BuildModuleFoldoutStateKey(moduleProperty);
            PlayerManagementFoldoutStateUtility.SetFoldoutState(stateKey, expanded);
        }
    }

    private static void ApplyModuleDefinitionsMutation(PlayerPowerUpsPresetsPanel panel, string undoLabel, Action<SerializedProperty> mutation)
    {
        if (panel.presetSerializedObject == null || mutation == null)
            return;

        SerializedProperty moduleDefinitionsProperty = panel.presetSerializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
            return;

        if (panel.selectedPreset != null)
            Undo.RecordObject(panel.selectedPreset, undoLabel);

        panel.presetSerializedObject.Update();
        mutation(moduleDefinitionsProperty);
        PlayerScalingRuleStatKeyRefreshUtility.RefreshStatKeys(panel.presetSerializedObject);
        panel.presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        panel.ScheduleActiveSectionRebuild();
    }
    #endregion

    #region Helpers
    private static bool IsMatchingModuleFilters(PlayerPowerUpsPresetsPanel panel, string moduleId, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(panel.moduleIdFilterText))
        {
            string resolvedModuleId = string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId;

            if (resolvedModuleId.IndexOf(panel.moduleIdFilterText, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(panel.moduleDisplayNameFilterText))
        {
            string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;

            if (resolvedDisplayName.IndexOf(panel.moduleDisplayNameFilterText, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    private static string BuildModuleCardTitle(int moduleIndex, string moduleId, string displayName, PowerUpModuleKind moduleKind)
    {
        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "<Unnamed>" : displayName.Trim();
        string resolvedModuleId = string.IsNullOrWhiteSpace(moduleId) ? "<No ID>" : moduleId.Trim();
        return string.Format("#{0:D2}  {1}  ({2})  [{3}]", moduleIndex + 1, resolvedDisplayName, resolvedModuleId, moduleKind);
    }

    private static bool ResolveModuleFoldoutState(PlayerPowerUpsPresetsPanel panel, string foldoutStateKey)
    {
        return PlayerManagementFoldoutStateUtility.ResolveFoldoutState(foldoutStateKey, false);
    }

    private static string BuildModuleFoldoutStateKey(SerializedProperty moduleProperty)
    {
        return PlayerManagementFoldoutStateUtility.BuildPropertyStateKey(moduleProperty, "ModuleDefinitionCard");
    }

    private static string ResolveModuleDefinitionId(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
            return string.Empty;

        SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
            return string.Empty;

        return moduleIdProperty.stringValue;
    }

    private static string ResolveModuleDefinitionDisplayName(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
            return string.Empty;

        SerializedProperty displayNameProperty = moduleProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return string.Empty;

        return displayNameProperty.stringValue;
    }

    private static PowerUpModuleKind ResolveModuleDefinitionKind(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
            return default;

        SerializedProperty moduleKindProperty = moduleProperty.FindPropertyRelative("moduleKind");

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return default;

        return (PowerUpModuleKind)moduleKindProperty.enumValueIndex;
    }

    private static string GenerateUniqueModuleId(SerializedProperty moduleDefinitionsProperty, string baseModuleId, int excludedIndex)
    {
        string sanitizedBaseModuleId = string.IsNullOrWhiteSpace(baseModuleId) ? "ModuleCustom" : baseModuleId.Trim();
        HashSet<string> existingModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int moduleIndex = 0; moduleIndex < moduleDefinitionsProperty.arraySize; moduleIndex++)
        {
            if (moduleIndex == excludedIndex)
                continue;

            SerializedProperty moduleProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(moduleIndex);
            string existingModuleId = ResolveModuleDefinitionId(moduleProperty);

            if (string.IsNullOrWhiteSpace(existingModuleId))
                continue;

            existingModuleIds.Add(existingModuleId.Trim());
        }

        if (!existingModuleIds.Contains(sanitizedBaseModuleId))
            return sanitizedBaseModuleId;

        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidateModuleId = sanitizedBaseModuleId + suffix.ToString();

            if (existingModuleIds.Contains(candidateModuleId))
                continue;

            return candidateModuleId;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static void InitializeNewModuleDefinition(SerializedProperty moduleProperty, string moduleId, string displayName)
    {
        if (moduleProperty == null)
            return;

        SetModuleDefinitionId(moduleProperty, moduleId);
        SetModuleDefinitionDisplayName(moduleProperty, displayName);
        SetModuleDefinitionKind(moduleProperty, PowerUpModuleKind.TriggerPress);
        SetModuleDefinitionStage(moduleProperty, PowerUpModuleStage.Trigger);
        SetModuleDefinitionNotes(moduleProperty, string.Empty);
    }

    private static void SetModuleDefinitionId(SerializedProperty moduleProperty, string moduleId)
    {
        if (moduleProperty == null)
            return;

        SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
            return;

        moduleIdProperty.stringValue = moduleId;
    }

    private static void SetModuleDefinitionDisplayName(SerializedProperty moduleProperty, string displayName)
    {
        if (moduleProperty == null)
            return;

        SerializedProperty displayNameProperty = moduleProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return;

        displayNameProperty.stringValue = displayName;
    }

    private static void SetModuleDefinitionKind(SerializedProperty moduleProperty, PowerUpModuleKind moduleKind)
    {
        if (moduleProperty == null)
            return;

        SerializedProperty moduleKindProperty = moduleProperty.FindPropertyRelative("moduleKind");

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return;

        moduleKindProperty.enumValueIndex = (int)moduleKind;
    }

    private static void SetModuleDefinitionStage(SerializedProperty moduleProperty, PowerUpModuleStage stage)
    {
        if (moduleProperty == null)
            return;

        SerializedProperty stageProperty = moduleProperty.FindPropertyRelative("defaultStage");

        if (stageProperty == null || stageProperty.propertyType != SerializedPropertyType.Enum)
            return;

        stageProperty.enumValueIndex = (int)stage;
    }

    private static void SetModuleDefinitionNotes(SerializedProperty moduleProperty, string notes)
    {
        if (moduleProperty == null)
            return;

        SerializedProperty notesProperty = moduleProperty.FindPropertyRelative("notes");

        if (notesProperty == null)
            return;

        notesProperty.stringValue = notes;
    }

    private static void PruneFoldoutStateMap(Dictionary<string, bool> foldoutStateByKey, HashSet<string> validStateKeys)
    {
        if (foldoutStateByKey == null || validStateKeys == null)
            return;

        List<string> keysToRemove = new List<string>();

        foreach (KeyValuePair<string, bool> entry in foldoutStateByKey)
        {
            if (validStateKeys.Contains(entry.Key))
                continue;

            keysToRemove.Add(entry.Key);
        }

        for (int index = 0; index < keysToRemove.Count; index++)
            foldoutStateByKey.Remove(keysToRemove[index]);
    }
    #endregion

    #endregion
}
