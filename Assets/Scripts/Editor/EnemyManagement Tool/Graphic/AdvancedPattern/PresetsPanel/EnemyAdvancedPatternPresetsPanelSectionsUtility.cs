using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds detail sections for enemy advanced pattern preset panels and manages shared Modules & Patterns authoring.
/// </summary>
internal static class EnemyAdvancedPatternPresetsPanelSectionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the metadata section for the selected advanced-pattern preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and refresh callbacks.</param>
    public static void BuildMetadataSection(EnemyAdvancedPatternPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = EnemyAdvancedPatternPresetsPanelElementUtility.CreateDetailsSectionContainer(panel, "Preset Details");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");
        SerializedProperty nameProperty = presetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = presetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = presetSerializedObject.FindProperty("version");

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            panel.HandlePresetNameChanged(evt.newValue);
        });
        sectionContainer.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(descriptionField);

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Preset ID");
        idField.isReadOnly = true;
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.BindProperty(idProperty);
        idRow.Add(idField);

        Button regenerateButton = new Button(panel.RegeneratePresetId);
        regenerateButton.text = "Regenerate";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);

        sectionContainer.Add(idRow);
    }

    /// <summary>
    /// Builds the shared Modules & Patterns preset section and inlines the referenced shared asset when assigned.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and refresh callbacks.</param>
    public static void BuildModulesAndPatternsPresetSection(EnemyAdvancedPatternPresetsPanel panel)
    {
        EnemyAdvancedPatternSharedPresetSectionUtility.BuildSection(panel);
    }

    /// <summary>
    /// Builds the pattern loadout section that exposes active shared pattern IDs.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and refresh callbacks.</param>
    public static void BuildPatternLoadoutSection(EnemyAdvancedPatternPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = EnemyAdvancedPatternPresetsPanelElementUtility.CreateDetailsSectionContainer(panel, "Pattern Loadout");

        if (sectionContainer == null)
            return;

        Label loadoutHeader = new Label("Active Pattern");
        loadoutHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        loadoutHeader.style.marginTop = 2f;
        loadoutHeader.style.marginBottom = 2f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(loadoutHeader, "NashCore.EnemyManagement.AdvancedPattern.Loadout.ActivePatternId");
        sectionContainer.Add(loadoutHeader);

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        SerializedProperty activePatternIdsProperty = presetSerializedObject.FindProperty("activePatternIds");

        if (activePatternIdsProperty == null)
        {
            HelpBox missingPropertiesBox = new HelpBox("Active Pattern Loadout is missing on this preset.", HelpBoxMessageType.Warning);
            sectionContainer.Add(missingPropertiesBox);
            return;
        }

        SerializedProperty sharedPresetProperty = presetSerializedObject.FindProperty("modulesAndPatternsPreset");
        EnemyModulesAndPatternsPreset sharedPreset = sharedPresetProperty != null
            ? sharedPresetProperty.objectReferenceValue as EnemyModulesAndPatternsPreset
            : null;

        SerializedProperty patternsProperty = null;
        string infoMessage = string.Empty;

        if (sharedPreset != null)
        {
            SerializedObject sharedPresetSerializedObject = new SerializedObject(sharedPreset);
            sharedPresetSerializedObject.Update();
            patternsProperty = sharedPresetSerializedObject.FindProperty("patterns");
            infoMessage = "Select one shared assembled pattern from the assigned Modules & Patterns preset.";
        }
        else
        {
            patternsProperty = presetSerializedObject.FindProperty("patterns");
            infoMessage = "No shared preset is assigned yet. Loadout is still reading hidden legacy patterns for backward compatibility.";
        }

        HelpBox loadoutInfoBox = new HelpBox(infoMessage, HelpBoxMessageType.Info);
        loadoutInfoBox.style.marginBottom = 4f;
        sectionContainer.Add(loadoutInfoBox);

        if (patternsProperty == null)
        {
            HelpBox missingPatternsBox = new HelpBox("Pattern definitions are missing for the current loadout source.", HelpBoxMessageType.Warning);
            sectionContainer.Add(missingPatternsBox);
            return;
        }

        List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> loadoutOptions = BuildPatternLoadoutOptions(patternsProperty);

        if (loadoutOptions.Count <= 0)
        {
            HelpBox noOptionsBox = new HelpBox("No valid patterns are available. Add patterns to the assigned Modules & Patterns preset first.", HelpBoxMessageType.Warning);
            sectionContainer.Add(noOptionsBox);
            return;
        }

        BuildPatternLoadoutArray(panel, activePatternIdsProperty, loadoutOptions, sectionContainer);
        EnemyAdvancedPatternCompositionWarningUtility.AddWarnings(panel, sectionContainer);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the editable active pattern loadout UI rows.
    /// </summary>
    /// <param name="panel">Owning panel used for undo, refresh and rebuild callbacks.</param>
    /// <param name="activePatternIdsProperty">Serialized active pattern ID array.</param>
    /// <param name="loadoutOptions">Distinct assembled pattern options.</param>
    /// <param name="sectionContainer">Section container that receives the generated rows.</param>
    private static void BuildPatternLoadoutArray(EnemyAdvancedPatternPresetsPanel panel,
                                                 SerializedProperty activePatternIdsProperty,
                                                 List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> loadoutOptions,
                                                 VisualElement sectionContainer)
    {
        if (panel == null ||
            activePatternIdsProperty == null ||
            loadoutOptions == null ||
            loadoutOptions.Count <= 0 ||
            sectionContainer == null)
        {
            return;
        }

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        bool normalized = NormalizePatternLoadoutArray(activePatternIdsProperty, loadoutOptions);

        if (normalized)
        {
            presetSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            presetSerializedObject.Update();
        }

        List<string> optionLabels = new List<string>(loadoutOptions.Count);

        for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
            optionLabels.Add(loadoutOptions[optionIndex].DisplayLabel);

        for (int index = 0; index < activePatternIdsProperty.arraySize; index++)
        {
            SerializedProperty patternIdProperty = activePatternIdsProperty.GetArrayElementAtIndex(index);

            if (patternIdProperty == null)
                continue;

            string selectedPatternId = ResolveSelectedPatternId(patternIdProperty.stringValue, loadoutOptions);
            int selectedOptionIndex = 0;

            for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
            {
                if (!string.Equals(loadoutOptions[optionIndex].PatternId, selectedPatternId, StringComparison.OrdinalIgnoreCase))
                    continue;

                selectedOptionIndex = optionIndex;
                break;
            }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;

            PopupField<string> selector = new PopupField<string>("Pattern " + (index + 1), optionLabels, selectedOptionIndex);
            selector.tooltip = "Select one shared assembled pattern ID for the active runtime loadout.";
            selector.style.flexGrow = 1f;
            int capturedIndex = index;
            selector.RegisterValueChangedCallback(evt =>
            {
                int optionIndex = optionLabels.IndexOf(evt.newValue);

                if (optionIndex < 0 || optionIndex >= loadoutOptions.Count)
                    return;

                string patternId = loadoutOptions[optionIndex].PatternId;
                SetPatternLoadoutEntry(panel, activePatternIdsProperty, capturedIndex, patternId, loadoutOptions);
            });
            row.Add(selector);

            Button removeButton = new Button(() =>
            {
                RemovePatternLoadoutEntry(panel, activePatternIdsProperty, capturedIndex, loadoutOptions);
            });
            removeButton.text = "Remove";
            removeButton.tooltip = "Remove this pattern entry from the active loadout.";
            removeButton.style.marginLeft = 6f;
            removeButton.SetEnabled(activePatternIdsProperty.arraySize > 1);
            row.Add(removeButton);

            sectionContainer.Add(row);
        }

        if (activePatternIdsProperty.arraySize <= 0)
        {
            HelpBox emptyLoadoutBox = new HelpBox("No active patterns in loadout. Add one entry.", HelpBoxMessageType.Info);
            sectionContainer.Add(emptyLoadoutBox);
        }

        Button addButton = new Button(() =>
        {
            AddPatternLoadoutEntry(panel, activePatternIdsProperty, loadoutOptions);
        });
        addButton.text = "Add Pattern";
        addButton.tooltip = "Add one active assembled pattern to the runtime loadout.";
        addButton.style.marginTop = 2f;
        addButton.SetEnabled(CanAddPatternLoadoutEntry(activePatternIdsProperty, loadoutOptions));
        sectionContainer.Add(addButton);
    }

    /// <summary>
    /// Adds one new active pattern loadout entry using the next compatible pattern ID.
    /// </summary>
    /// <param name="panel">Owning panel used for undo, refresh and rebuild callbacks.</param>
    /// <param name="activePatternIdsProperty">Serialized active pattern ID array.</param>
    /// <param name="loadoutOptions">Distinct assembled pattern options.</param>
    private static void AddPatternLoadoutEntry(EnemyAdvancedPatternPresetsPanel panel,
                                               SerializedProperty activePatternIdsProperty,
                                               List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> loadoutOptions)
    {
        if (panel == null || activePatternIdsProperty == null || loadoutOptions == null || loadoutOptions.Count <= 0)
            return;

        string nextPatternId = ResolveNextPatternLoadoutId(activePatternIdsProperty, loadoutOptions);

        if (string.IsNullOrWhiteSpace(nextPatternId))
            return;

        EnemyAdvancedPatternPreset selectedPreset = panel.SelectedPreset;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, "Add Enemy Pattern Loadout Entry");

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        int insertIndex = activePatternIdsProperty.arraySize;
        activePatternIdsProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty insertedProperty = activePatternIdsProperty.GetArrayElementAtIndex(insertIndex);

        if (insertedProperty != null)
            insertedProperty.stringValue = nextPatternId;

        NormalizePatternLoadoutArray(activePatternIdsProperty, loadoutOptions);
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.BuildActiveDetailsSection();
    }

    /// <summary>
    /// Removes one active pattern loadout entry and rebuilds the section.
    /// </summary>
    /// <param name="panel">Owning panel used for undo, refresh and rebuild callbacks.</param>
    /// <param name="activePatternIdsProperty">Serialized active pattern ID array.</param>
    /// <param name="entryIndex">Index of the entry to remove.</param>
    /// <param name="loadoutOptions">Distinct assembled pattern options.</param>
    private static void RemovePatternLoadoutEntry(EnemyAdvancedPatternPresetsPanel panel,
                                                  SerializedProperty activePatternIdsProperty,
                                                  int entryIndex,
                                                  List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> loadoutOptions)
    {
        if (panel == null || activePatternIdsProperty == null)
            return;

        if (entryIndex < 0 || entryIndex >= activePatternIdsProperty.arraySize)
            return;

        EnemyAdvancedPatternPreset selectedPreset = panel.SelectedPreset;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, "Remove Enemy Pattern Loadout Entry");

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        activePatternIdsProperty.DeleteArrayElementAtIndex(entryIndex);
        NormalizePatternLoadoutArray(activePatternIdsProperty, loadoutOptions);
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.BuildActiveDetailsSection();
    }

    /// <summary>
    /// Replaces one active pattern loadout entry with another valid pattern ID.
    /// </summary>
    /// <param name="panel">Owning panel used for undo, refresh and rebuild callbacks.</param>
    /// <param name="activePatternIdsProperty">Serialized active pattern ID array.</param>
    /// <param name="entryIndex">Target entry index to replace.</param>
    /// <param name="patternId">Replacement pattern ID.</param>
    /// <param name="loadoutOptions">Distinct assembled pattern options.</param>
    private static void SetPatternLoadoutEntry(EnemyAdvancedPatternPresetsPanel panel,
                                               SerializedProperty activePatternIdsProperty,
                                               int entryIndex,
                                               string patternId,
                                               List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> loadoutOptions)
    {
        if (panel == null || activePatternIdsProperty == null)
            return;

        if (entryIndex < 0 || entryIndex >= activePatternIdsProperty.arraySize)
            return;

        if (string.IsNullOrWhiteSpace(patternId))
            return;

        SerializedProperty entryProperty = activePatternIdsProperty.GetArrayElementAtIndex(entryIndex);

        if (entryProperty == null)
            return;

        if (string.Equals(entryProperty.stringValue, patternId, StringComparison.Ordinal))
            return;

        EnemyAdvancedPatternPreset selectedPreset = panel.SelectedPreset;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, "Change Enemy Pattern Loadout Entry");

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        entryProperty.stringValue = patternId;
        NormalizePatternLoadoutArray(activePatternIdsProperty, loadoutOptions);
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.BuildActiveDetailsSection();
    }

    /// <summary>
    /// Builds one distinct list of assembled pattern IDs and labels from the supplied patterns array.
    /// </summary>
    /// <param name="patternsProperty">Serialized patterns array.</param>
    /// <returns>The distinct assembled pattern options in authoring order.</returns>
    private static List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> BuildPatternLoadoutOptions(SerializedProperty patternsProperty)
    {
        List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> options = new List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption>();

        if (patternsProperty == null)
            return options;

        for (int index = 0; index < patternsProperty.arraySize; index++)
        {
            SerializedProperty patternProperty = patternsProperty.GetArrayElementAtIndex(index);

            if (patternProperty == null)
                continue;

            SerializedProperty patternIdProperty = patternProperty.FindPropertyRelative("patternId");
            SerializedProperty displayNameProperty = patternProperty.FindPropertyRelative("displayName");

            if (patternIdProperty == null)
                continue;

            string patternId = patternIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(patternId))
                continue;

            if (ContainsPatternOption(options, patternId))
                continue;

            string displayName = displayNameProperty != null ? displayNameProperty.stringValue : string.Empty;

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = patternId;

            EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption option = new EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption();
            option.PatternId = patternId;
            option.DisplayLabel = displayName + " (" + patternId + ")";
            options.Add(option);
        }

        return options;
    }

    /// <summary>
    /// Normalizes the active pattern loadout array by removing invalid or duplicated entries.
    /// </summary>
    /// <param name="activePatternIdsProperty">Serialized active pattern ID array.</param>
    /// <param name="loadoutOptions">Distinct assembled pattern options.</param>
    /// <returns>True when the array content changes.</returns>
    private static bool NormalizePatternLoadoutArray(SerializedProperty activePatternIdsProperty,
                                                     List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> loadoutOptions)
    {
        if (activePatternIdsProperty == null || loadoutOptions == null || loadoutOptions.Count <= 0)
            return false;

        HashSet<string> validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
            validIds.Add(loadoutOptions[optionIndex].PatternId);

        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool changed = false;

        for (int index = 0; index < activePatternIdsProperty.arraySize; index++)
        {
            SerializedProperty entryProperty = activePatternIdsProperty.GetArrayElementAtIndex(index);

            if (entryProperty == null)
            {
                activePatternIdsProperty.DeleteArrayElementAtIndex(index);
                index--;
                changed = true;
                continue;
            }

            string patternId = entryProperty.stringValue;

            if (string.IsNullOrWhiteSpace(patternId) || !validIds.Contains(patternId) || !visited.Add(patternId))
            {
                activePatternIdsProperty.DeleteArrayElementAtIndex(index);
                index--;
                changed = true;
            }
        }

        if (activePatternIdsProperty.arraySize > 0)
            return changed;

        activePatternIdsProperty.InsertArrayElementAtIndex(0);
        SerializedProperty firstEntry = activePatternIdsProperty.GetArrayElementAtIndex(0);

        if (firstEntry != null)
            firstEntry.stringValue = loadoutOptions[0].PatternId;

        return true;
    }

    /// <summary>
    /// Checks whether the active loadout can receive one pattern entry.
    /// </summary>
    /// <param name="activePatternIdsProperty">Serialized active pattern ID array.</param>
    /// <param name="loadoutOptions">Distinct assembled pattern options.</param>
    /// <returns>True when the loadout is empty and at least one option exists.</returns>
    private static bool CanAddPatternLoadoutEntry(SerializedProperty activePatternIdsProperty,
                                                  List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> loadoutOptions)
    {
        if (activePatternIdsProperty == null || loadoutOptions == null || loadoutOptions.Count <= 0)
            return false;

        if (activePatternIdsProperty.arraySize > 0)
            return false;

        for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
        {
            string patternId = loadoutOptions[optionIndex].PatternId;

            if (!ContainsPatternLoadoutId(activePatternIdsProperty, patternId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the next unused pattern ID that can be inserted into the loadout.
    /// </summary>
    /// <param name="activePatternIdsProperty">Serialized active pattern ID array.</param>
    /// <param name="loadoutOptions">Distinct assembled pattern options.</param>
    /// <returns>One unused pattern ID, or an empty string when none are available.</returns>
    private static string ResolveNextPatternLoadoutId(SerializedProperty activePatternIdsProperty,
                                                      List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> loadoutOptions)
    {
        if (activePatternIdsProperty == null || loadoutOptions == null || loadoutOptions.Count <= 0)
            return string.Empty;

        for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
        {
            string patternId = loadoutOptions[optionIndex].PatternId;

            if (ContainsPatternLoadoutId(activePatternIdsProperty, patternId))
                continue;

            return patternId;
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks whether a specific pattern ID already exists in the serialized active loadout array.
    /// </summary>
    /// <param name="activePatternIdsProperty">Serialized active pattern ID array.</param>
    /// <param name="patternId">Pattern ID to search for.</param>
    /// <returns>True when the pattern ID is already present.</returns>
    private static bool ContainsPatternLoadoutId(SerializedProperty activePatternIdsProperty, string patternId)
    {
        if (activePatternIdsProperty == null || string.IsNullOrWhiteSpace(patternId))
            return false;

        for (int index = 0; index < activePatternIdsProperty.arraySize; index++)
        {
            SerializedProperty entryProperty = activePatternIdsProperty.GetArrayElementAtIndex(index);

            if (entryProperty == null)
                continue;

            if (string.Equals(entryProperty.stringValue, patternId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the currently selected pattern ID to a safe option value.
    /// </summary>
    /// <param name="selectedPatternId">Current serialized pattern ID value.</param>
    /// <param name="loadoutOptions">Distinct assembled pattern options.</param>
    /// <returns>One pattern ID guaranteed to exist in the options list when the list is not empty.</returns>
    private static string ResolveSelectedPatternId(string selectedPatternId,
                                                   List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> loadoutOptions)
    {
        if (loadoutOptions == null || loadoutOptions.Count <= 0)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(selectedPatternId))
        {
            for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
            {
                if (string.Equals(loadoutOptions[optionIndex].PatternId, selectedPatternId, StringComparison.OrdinalIgnoreCase))
                    return loadoutOptions[optionIndex].PatternId;
            }
        }

        return loadoutOptions[0].PatternId;
    }

    /// <summary>
    /// Checks whether a distinct pattern option list already contains one pattern ID.
    /// </summary>
    /// <param name="options">Current distinct option list.</param>
    /// <param name="patternId">Pattern ID to search for.</param>
    /// <returns>True when the list already contains the ID.</returns>
    private static bool ContainsPatternOption(List<EnemyAdvancedPatternPresetsPanel.PatternLoadoutOption> options, string patternId)
    {
        if (options == null || string.IsNullOrWhiteSpace(patternId))
            return false;

        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index].PatternId, patternId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
