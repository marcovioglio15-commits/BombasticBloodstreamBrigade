using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Builds and mutates the loadout and input section for the power-ups presets panel.
/// </summary>
public static class PlayerPowerUpsPresetsPanelLoadoutUtility
{
    #region Methods

    #region Section Builder
    public static void BuildLoadoutInputSection(PlayerPowerUpsPresetsPanel panel)
    {
        Label header = new Label("Loadout & Inputs");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.PowerUps.Loadout.Main");
        panel.sectionContentRoot.Add(header);

        SerializedProperty primaryToolActionIdProperty = panel.presetSerializedObject.FindProperty("primaryToolActionId");
        SerializedProperty secondaryToolActionIdProperty = panel.presetSerializedObject.FindProperty("secondaryToolActionId");
        SerializedProperty swapSlotsActionIdProperty = panel.presetSerializedObject.FindProperty("swapSlotsActionId");
        SerializedProperty primaryActivePowerUpIdProperty = panel.presetSerializedObject.FindProperty("primaryActivePowerUpId");
        SerializedProperty secondaryActivePowerUpIdProperty = panel.presetSerializedObject.FindProperty("secondaryActivePowerUpId");
        SerializedProperty equippedPassivePowerUpIdsProperty = panel.presetSerializedObject.FindProperty("equippedPassivePowerUpIds");
        SerializedProperty activePowerUpsProperty = panel.presetSerializedObject.FindProperty("activePowerUps");
        SerializedProperty passivePowerUpsProperty = panel.presetSerializedObject.FindProperty("passivePowerUps");

        if (primaryToolActionIdProperty == null ||
            secondaryToolActionIdProperty == null ||
            swapSlotsActionIdProperty == null ||
            primaryActivePowerUpIdProperty == null ||
            secondaryActivePowerUpIdProperty == null)
        {
            Label missingLabel = new Label("Loadout/input properties are missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            panel.sectionContentRoot.Add(missingLabel);
            return;
        }

        if (activePowerUpsProperty == null)
        {
            Label missingLabel = new Label("Active power ups list is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            panel.sectionContentRoot.Add(missingLabel);
            return;
        }

        if (equippedPassivePowerUpIdsProperty == null || passivePowerUpsProperty == null)
        {
            Label missingLabel = new Label("Passive loadout properties are missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            panel.sectionContentRoot.Add(missingLabel);
            return;
        }

        EnsureDefaultActionId(panel, primaryToolActionIdProperty, "PowerUpPrimary");
        EnsureDefaultActionId(panel, secondaryToolActionIdProperty, "PowerUpSecondary");
        EnsureDefaultActionId(panel, swapSlotsActionIdProperty, "PowerUpSwapSlots");

        Label bindingsHeader = new Label("Bindings");
        bindingsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        bindingsHeader.style.marginTop = 6f;
        bindingsHeader.style.marginBottom = 2f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(bindingsHeader, "NashCore.PlayerManagement.PowerUps.Loadout.Bindings");
        panel.sectionContentRoot.Add(bindingsHeader);

        panel.sectionContentRoot.Add(BuildBindingsFoldout(panel, "Primary Tool Input", primaryToolActionIdProperty));
        panel.sectionContentRoot.Add(BuildBindingsFoldout(panel, "Secondary Tool Input", secondaryToolActionIdProperty));
        panel.sectionContentRoot.Add(BuildBindingsFoldout(panel, "Swap Active Slots Input", swapSlotsActionIdProperty));

        Label loadoutHeader = new Label("Loadout");
        loadoutHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        loadoutHeader.style.marginTop = 6f;
        loadoutHeader.style.marginBottom = 2f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(loadoutHeader, "NashCore.PlayerManagement.PowerUps.Loadout.Active");
        panel.sectionContentRoot.Add(loadoutHeader);

        HelpBox loadoutInfoBox = new HelpBox("Select <None> to keep a startup active slot empty. Empty slots stay hidden in the HUD and are filled only after the player obtains an active power up during gameplay.", HelpBoxMessageType.Info);
        loadoutInfoBox.style.marginBottom = 4f;
        panel.sectionContentRoot.Add(loadoutInfoBox);

        List<PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption> loadoutOptions = BuildLoadoutOptions(activePowerUpsProperty);
        BuildLoadoutSelector(panel, "Primary Active Power Up", primaryActivePowerUpIdProperty, loadoutOptions);
        BuildLoadoutSelector(panel, "Secondary Active Power Up", secondaryActivePowerUpIdProperty, loadoutOptions);

        string primaryId = ResolveSelectedToolId(primaryActivePowerUpIdProperty.stringValue, loadoutOptions);
        string secondaryId = ResolveSelectedToolId(secondaryActivePowerUpIdProperty.stringValue, loadoutOptions);

        if (!string.IsNullOrWhiteSpace(primaryId) &&
            !string.IsNullOrWhiteSpace(secondaryId) &&
            string.Equals(primaryId, secondaryId, StringComparison.OrdinalIgnoreCase))
        {
            HelpBox sameSlotWarning = new HelpBox("Primary and Secondary currently reference the same active power up.", HelpBoxMessageType.Warning);
            panel.sectionContentRoot.Add(sameSlotWarning);
        }

        Label passiveLoadoutHeader = new Label("Passive Power Ups Loadout (IDs)");
        passiveLoadoutHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        passiveLoadoutHeader.style.marginTop = 8f;
        passiveLoadoutHeader.style.marginBottom = 2f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(passiveLoadoutHeader, "NashCore.PlayerManagement.PowerUps.Loadout.Passive");
        panel.sectionContentRoot.Add(passiveLoadoutHeader);

        List<string> passiveToolIds = BuildPassiveLoadoutOptions(passivePowerUpsProperty);

        if (passiveToolIds.Count == 0)
        {
            HelpBox missingPassiveToolsHelpBox = new HelpBox("No valid passive power ups found. Add passive power ups first.", HelpBoxMessageType.Warning);
            panel.sectionContentRoot.Add(missingPassiveToolsHelpBox);
            return;
        }

        BuildPassiveLoadoutArray(panel, equippedPassivePowerUpIdsProperty, passiveToolIds);
    }
    #endregion

    #region Input Bindings
    private static Foldout BuildBindingsFoldout(PlayerPowerUpsPresetsPanel panel, string foldoutTitle, SerializedProperty actionIdProperty)
    {
        Foldout foldout = new Foldout();
        foldout.text = foldoutTitle;
        foldout.value = true;

        if (actionIdProperty == null)
        {
            Label missingLabel = new Label("Missing action binding property.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            foldout.Add(missingLabel);
            return foldout;
        }

        if (panel.presetSerializedObject == null)
        {
            Label missingLabel = new Label("Serialized preset object is not available.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            foldout.Add(missingLabel);
            return foldout;
        }

        if (panel.inputAsset == null)
        {
            Label missingLabel = new Label("Input Action Asset is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            foldout.Add(missingLabel);
            return foldout;
        }

        InputActionSelectionElement bindingsElement = new InputActionSelectionElement(panel.inputAsset, panel.presetSerializedObject, actionIdProperty, InputActionSelectionElement.SelectionMode.PowerUps);
        foldout.Add(bindingsElement);
        return foldout;
    }

    private static void EnsureDefaultActionId(PlayerPowerUpsPresetsPanel panel, SerializedProperty actionIdProperty, string actionName)
    {
        if (actionIdProperty == null || panel.inputAsset == null)
            return;

        string currentActionId = actionIdProperty.stringValue;

        if (!string.IsNullOrWhiteSpace(currentActionId))
        {
            InputAction existingAction = panel.inputAsset.FindAction(currentActionId, false);

            if (existingAction != null)
                return;
        }

        InputAction defaultAction = panel.inputAsset.FindAction(actionName, false);

        if (defaultAction == null)
            return;

        if (panel.selectedPreset != null)
            Undo.RecordObject(panel.selectedPreset, "Assign Default Power Up Action");

        panel.presetSerializedObject.Update();
        actionIdProperty.stringValue = defaultAction.id.ToString();
        panel.presetSerializedObject.ApplyModifiedProperties();
    }
    #endregion

    #region Active Loadout
    private static void BuildLoadoutSelector(PlayerPowerUpsPresetsPanel panel,
                                             string label,
                                             SerializedProperty slotToolIdProperty,
                                             List<PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption> loadoutOptions)
    {
        if (slotToolIdProperty == null || loadoutOptions == null || loadoutOptions.Count == 0)
            return;

        List<string> optionLabels = new List<string>();

        for (int index = 0; index < loadoutOptions.Count; index++)
            optionLabels.Add(loadoutOptions[index].DisplayLabel);

        PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption selectedOption = ResolveSelectedOption(slotToolIdProperty.stringValue, loadoutOptions);
        int selectedIndex = 0;

        for (int index = 0; index < loadoutOptions.Count; index++)
        {
            if (string.Equals(loadoutOptions[index].PowerUpId, selectedOption.PowerUpId, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
                break;
            }
        }

        PopupField<string> selector = new PopupField<string>(label, optionLabels, selectedIndex);
        selector.tooltip = "Select the active power up assigned to this slot. Choose <None> to leave the slot empty until the player obtains one at runtime.";
        selector.RegisterValueChangedCallback(evt =>
        {
            int optionIndex = optionLabels.IndexOf(evt.newValue);

            if (optionIndex < 0 || optionIndex >= loadoutOptions.Count)
                return;

            string selectedToolId = loadoutOptions[optionIndex].PowerUpId;

            if (string.Equals(slotToolIdProperty.stringValue, selectedToolId, StringComparison.Ordinal))
                return;

            if (panel.selectedPreset != null)
                Undo.RecordObject(panel.selectedPreset, "Change Power Ups Loadout Slot");

            panel.presetSerializedObject.Update();
            slotToolIdProperty.stringValue = selectedToolId;
            panel.presetSerializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
            panel.ScheduleActiveSectionRebuild();
        });
        panel.sectionContentRoot.Add(selector);
    }

    private static string ResolveSelectedToolId(string selectedToolId, List<PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption> options)
    {
        if (options == null || options.Count == 0)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(selectedToolId))
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index].PowerUpId, selectedToolId, StringComparison.OrdinalIgnoreCase))
                    return options[index].PowerUpId;
            }
        }

        return options[0].PowerUpId;
    }

    private static PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption ResolveSelectedOption(string selectedToolId, List<PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption> options)
    {
        if (options == null || options.Count == 0)
            return default;

        if (!string.IsNullOrWhiteSpace(selectedToolId))
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index].PowerUpId, selectedToolId, StringComparison.OrdinalIgnoreCase))
                    return options[index];
            }
        }

        return options[0];
    }

    private static List<PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption> BuildLoadoutOptions(SerializedProperty activePowerUpsProperty)
    {
        List<PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption> options = new List<PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption>();
        options.Add(new PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption
        {
            PowerUpId = string.Empty,
            DisplayLabel = "<None>"
        });

        if (activePowerUpsProperty == null)
            return options;

        for (int index = 0; index < activePowerUpsProperty.arraySize; index++)
        {
            SerializedProperty activePowerUpProperty = activePowerUpsProperty.GetArrayElementAtIndex(index);

            if (activePowerUpProperty == null)
                continue;

            SerializedProperty commonDataProperty = activePowerUpProperty.FindPropertyRelative("commonData");

            if (commonDataProperty == null)
                continue;

            SerializedProperty toolIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");
            SerializedProperty displayNameProperty = commonDataProperty.FindPropertyRelative("displayName");

            if (toolIdProperty == null)
                continue;

            string toolId = toolIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(toolId))
                continue;

            string displayName = displayNameProperty != null ? displayNameProperty.stringValue : string.Empty;

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = toolId;

            options.Add(new PlayerPowerUpsPresetsPanel.LoadoutPowerUpOption
            {
                PowerUpId = toolId,
                DisplayLabel = displayName + " (" + toolId + ")"
            });
        }

        return options;
    }
    #endregion

    #region Passive Loadout
    private static List<string> BuildPassiveLoadoutOptions(SerializedProperty passivePowerUpsProperty)
    {
        List<string> options = new List<string>();

        if (passivePowerUpsProperty == null)
            return options;

        for (int index = 0; index < passivePowerUpsProperty.arraySize; index++)
        {
            SerializedProperty passivePowerUpProperty = passivePowerUpsProperty.GetArrayElementAtIndex(index);

            if (passivePowerUpProperty == null)
                continue;

            SerializedProperty commonDataProperty = passivePowerUpProperty.FindPropertyRelative("commonData");

            if (commonDataProperty == null)
                continue;

            SerializedProperty toolIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");

            if (toolIdProperty == null)
                continue;

            string toolId = toolIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(toolId) || ContainsStringIgnoreCase(options, toolId))
                continue;

            options.Add(toolId);
        }

        return options;
    }

    private static bool ContainsStringIgnoreCase(List<string> values, string candidate)
    {
        if (values == null || string.IsNullOrWhiteSpace(candidate))
            return false;

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void BuildPassiveLoadoutArray(PlayerPowerUpsPresetsPanel panel, SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null || passiveToolIds == null || passiveToolIds.Count == 0)
            return;

        bool normalized = NormalizePassiveLoadoutArray(equippedPassiveToolIdsProperty, passiveToolIds);

        if (normalized)
        {
            panel.presetSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            panel.presetSerializedObject.Update();
        }

        for (int index = 0; index < equippedPassiveToolIdsProperty.arraySize; index++)
        {
            SerializedProperty passiveToolIdProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(index);

            if (passiveToolIdProperty == null)
                continue;

            string selectedToolId = ResolveSelectedPassiveToolId(passiveToolIdProperty.stringValue, passiveToolIds);
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;

            PopupField<string> passiveSelector = new PopupField<string>("Passive Power Up " + (index + 1), passiveToolIds, selectedToolId);
            passiveSelector.tooltip = "Select a passive tool by its PowerUpId.";
            passiveSelector.style.flexGrow = 1f;
            int capturedIndex = index;
            passiveSelector.RegisterValueChangedCallback(evt =>
            {
                SetPassiveLoadoutEntry(panel, equippedPassiveToolIdsProperty, capturedIndex, evt.newValue, passiveToolIds);
            });
            row.Add(passiveSelector);

            Button removeButton = new Button(() =>
            {
                RemovePassiveLoadoutEntry(panel, equippedPassiveToolIdsProperty, capturedIndex, passiveToolIds);
            });
            removeButton.text = "Remove";
            removeButton.tooltip = "Remove this passive tool from the startup loadout.";
            removeButton.style.marginLeft = 6f;
            row.Add(removeButton);

            panel.sectionContentRoot.Add(row);
        }

        if (equippedPassiveToolIdsProperty.arraySize == 0)
        {
            HelpBox emptyLoadoutHelpBox = new HelpBox("No passive tools are currently equipped in startup loadout.", HelpBoxMessageType.Info);
            panel.sectionContentRoot.Add(emptyLoadoutHelpBox);
        }

        Button addButton = new Button(() =>
        {
            AddPassiveLoadoutEntry(panel, equippedPassiveToolIdsProperty, passiveToolIds);
        });
        addButton.text = "Add Passive Power Up";
        addButton.tooltip = "Add one passive tool ID to the startup loadout.";
        addButton.style.marginTop = 2f;
        addButton.SetEnabled(CanAddPassiveLoadoutEntry(equippedPassiveToolIdsProperty, passiveToolIds));
        panel.sectionContentRoot.Add(addButton);
    }

    private static void AddPassiveLoadoutEntry(PlayerPowerUpsPresetsPanel panel, SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null || passiveToolIds == null || passiveToolIds.Count == 0)
            return;

        string nextPassiveToolId = ResolveNextPassiveLoadoutId(equippedPassiveToolIdsProperty, passiveToolIds);

        if (string.IsNullOrWhiteSpace(nextPassiveToolId))
            return;

        if (panel.selectedPreset != null)
            Undo.RecordObject(panel.selectedPreset, "Add Passive Power Up Loadout Entry");

        panel.presetSerializedObject.Update();
        int insertIndex = equippedPassiveToolIdsProperty.arraySize;
        equippedPassiveToolIdsProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty insertedProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(insertIndex);

        if (insertedProperty != null)
            insertedProperty.stringValue = nextPassiveToolId;

        NormalizePassiveLoadoutArray(equippedPassiveToolIdsProperty, passiveToolIds);
        panel.presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        panel.ScheduleActiveSectionRebuild();
    }

    private static void RemovePassiveLoadoutEntry(PlayerPowerUpsPresetsPanel panel, SerializedProperty equippedPassiveToolIdsProperty, int entryIndex, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null)
            return;

        if (entryIndex < 0 || entryIndex >= equippedPassiveToolIdsProperty.arraySize)
            return;

        if (panel.selectedPreset != null)
            Undo.RecordObject(panel.selectedPreset, "Remove Passive Power Up Loadout Entry");

        panel.presetSerializedObject.Update();
        equippedPassiveToolIdsProperty.DeleteArrayElementAtIndex(entryIndex);
        NormalizePassiveLoadoutArray(equippedPassiveToolIdsProperty, passiveToolIds);
        panel.presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        panel.ScheduleActiveSectionRebuild();
    }

    private static void SetPassiveLoadoutEntry(PlayerPowerUpsPresetsPanel panel,
                                               SerializedProperty equippedPassiveToolIdsProperty,
                                               int entryIndex,
                                               string passiveToolId,
                                               List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null)
            return;

        if (entryIndex < 0 || entryIndex >= equippedPassiveToolIdsProperty.arraySize)
            return;

        if (string.IsNullOrWhiteSpace(passiveToolId))
            return;

        SerializedProperty passiveToolIdProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(entryIndex);

        if (passiveToolIdProperty == null)
            return;

        if (string.Equals(passiveToolIdProperty.stringValue, passiveToolId, StringComparison.Ordinal))
            return;

        if (panel.selectedPreset != null)
            Undo.RecordObject(panel.selectedPreset, "Change Passive Power Up Loadout Entry");

        panel.presetSerializedObject.Update();
        passiveToolIdProperty.stringValue = passiveToolId;
        NormalizePassiveLoadoutArray(equippedPassiveToolIdsProperty, passiveToolIds);
        panel.presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        panel.ScheduleActiveSectionRebuild();
    }

    private static bool NormalizePassiveLoadoutArray(SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null || passiveToolIds == null || passiveToolIds.Count == 0)
            return false;

        bool changed = false;
        HashSet<string> uniqueToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < equippedPassiveToolIdsProperty.arraySize; index++)
        {
            SerializedProperty passiveToolIdProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(index);
            string passiveToolId = passiveToolIdProperty != null ? passiveToolIdProperty.stringValue : string.Empty;

            if (string.IsNullOrWhiteSpace(passiveToolId) || !ContainsPassiveToolId(passiveToolIds, passiveToolId))
            {
                equippedPassiveToolIdsProperty.DeleteArrayElementAtIndex(index);
                changed = true;
                index--;
                continue;
            }

            if (uniqueToolIds.Add(passiveToolId))
                continue;

            equippedPassiveToolIdsProperty.DeleteArrayElementAtIndex(index);
            changed = true;
            index--;
        }

        return changed;
    }

    private static string ResolveNextPassiveLoadoutId(SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null || passiveToolIds == null || passiveToolIds.Count == 0)
            return string.Empty;

        for (int passiveToolIndex = 0; passiveToolIndex < passiveToolIds.Count; passiveToolIndex++)
        {
            string passiveToolId = passiveToolIds[passiveToolIndex];

            if (!ContainsPassiveLoadoutId(equippedPassiveToolIdsProperty, passiveToolId))
                return passiveToolId;
        }

        return string.Empty;
    }

    private static bool CanAddPassiveLoadoutEntry(SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null || passiveToolIds == null || passiveToolIds.Count == 0)
            return false;

        for (int passiveToolIndex = 0; passiveToolIndex < passiveToolIds.Count; passiveToolIndex++)
        {
            if (!ContainsPassiveLoadoutId(equippedPassiveToolIdsProperty, passiveToolIds[passiveToolIndex]))
                return true;
        }

        return false;
    }

    private static bool ContainsPassiveToolId(List<string> passiveToolIds, string passiveToolId)
    {
        if (passiveToolIds == null || string.IsNullOrWhiteSpace(passiveToolId))
            return false;

        for (int index = 0; index < passiveToolIds.Count; index++)
        {
            if (string.Equals(passiveToolIds[index], passiveToolId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ContainsPassiveLoadoutId(SerializedProperty equippedPassiveToolIdsProperty, string passiveToolId)
    {
        if (equippedPassiveToolIdsProperty == null || string.IsNullOrWhiteSpace(passiveToolId))
            return false;

        for (int index = 0; index < equippedPassiveToolIdsProperty.arraySize; index++)
        {
            SerializedProperty passiveToolIdProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(index);

            if (passiveToolIdProperty != null &&
                string.Equals(passiveToolIdProperty.stringValue, passiveToolId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveSelectedPassiveToolId(string selectedToolId, List<string> passiveToolIds)
    {
        if (passiveToolIds == null || passiveToolIds.Count == 0)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(selectedToolId))
        {
            for (int index = 0; index < passiveToolIds.Count; index++)
            {
                if (string.Equals(passiveToolIds[index], selectedToolId, StringComparison.OrdinalIgnoreCase))
                    return passiveToolIds[index];
            }
        }

        return passiveToolIds[0];
    }
    #endregion

    #endregion
}
