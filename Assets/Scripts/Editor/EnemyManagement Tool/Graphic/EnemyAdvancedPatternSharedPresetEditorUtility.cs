using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides commit-aware field helpers for the shared enemy Modules &amp; Patterns preset editor.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetEditorUtility
{
    #region Constants
    private const float DescriptionFieldHeight = 60f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one delayed text field bound to shared preset metadata with explicit undo-aware commit.
    /// </summary>
    /// <param name="panel">Owning panel used for refresh callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="parent">Parent element that receives the field.</param>
    /// <param name="propertyName">Serialized property name on the shared preset.</param>
    /// <param name="label">Inspector label shown by the field.</param>
    /// <param name="tooltip">Tooltip shown on the field.</param>
    /// <param name="multiline">True when the text field should be multiline.</param>
    /// <param name="synchronizeAssetName">True when the ScriptableObject name must follow the field value.</param>
    public static void AddSharedPresetTextField(EnemyAdvancedPatternPresetsPanel panel,
                                                SerializedObject sharedPresetSerializedObject,
                                                EnemyModulesAndPatternsPreset sharedPreset,
                                                VisualElement parent,
                                                string propertyName,
                                                string label,
                                                string tooltip,
                                                bool multiline,
                                                bool synchronizeAssetName)
    {
        if (sharedPresetSerializedObject == null ||
            sharedPreset == null ||
            parent == null ||
            string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        SerializedProperty property = sharedPresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        TextField textField = new TextField(label);
        textField.isDelayed = true;
        textField.multiline = multiline;
        textField.tooltip = tooltip;
        textField.SetValueWithoutNotify(property.stringValue);

        if (multiline)
            textField.style.height = DescriptionFieldHeight;

        textField.RegisterValueChangedCallback(evt =>
        {
            ApplySharedPresetTextChange(panel,
                                        sharedPresetSerializedObject,
                                        sharedPreset,
                                        propertyName,
                                        evt.newValue,
                                        textField,
                                        synchronizeAssetName);
        });
        parent.Add(textField);
    }

    /// <summary>
    /// Adds the shared preset ID row with read-only display and regeneration action.
    /// </summary>
    /// <param name="panel">Owning panel used for refresh callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="parent">Parent element that receives the row.</param>
    public static void AddSharedPresetIdRow(EnemyAdvancedPatternPresetsPanel panel,
                                            SerializedObject sharedPresetSerializedObject,
                                            EnemyModulesAndPatternsPreset sharedPreset,
                                            VisualElement parent)
    {
        if (sharedPresetSerializedObject == null || sharedPreset == null || parent == null)
            return;

        SerializedProperty idProperty = sharedPresetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Shared Preset ID");
        idField.isReadOnly = true;
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.SetValueWithoutNotify(idProperty.stringValue);
        idRow.Add(idField);

        Button regenerateButton = new Button(() =>
        {
            RegenerateSharedPresetId(panel, sharedPresetSerializedObject, sharedPreset);
        });
        regenerateButton.text = "Regenerate";
        regenerateButton.tooltip = "Regenerate the unique ID used by this shared Modules & Patterns preset.";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);

        parent.Add(idRow);
    }

    /// <summary>
    /// Adds one shared-preset property field with explicit commit logic so list mutations remain persisted and undoable.
    /// </summary>
    /// <param name="panel">Owning panel used for refresh callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="parent">Parent element that receives the field.</param>
    /// <param name="propertyName">Serialized property name on the shared preset.</param>
    /// <param name="label">Inspector label shown by the field.</param>
    /// <param name="tooltip">Tooltip shown on the field.</param>
    public static void AddSharedPresetPropertyField(EnemyAdvancedPatternPresetsPanel panel,
                                                    SerializedObject sharedPresetSerializedObject,
                                                    EnemyModulesAndPatternsPreset sharedPreset,
                                                    VisualElement parent,
                                                    string propertyName,
                                                    string label,
                                                    string tooltip)
    {
        if (sharedPresetSerializedObject == null ||
            sharedPreset == null ||
            parent == null ||
            string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        SerializedProperty property = sharedPresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.tooltip = tooltip;
        propertyField.BindProperty(property);
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            CommitSharedPresetSerializedChanges(panel,
                                               sharedPresetSerializedObject,
                                               sharedPreset,
                                               "Edit Enemy Modules And Patterns Shared Preset",
                                               false,
                                               false);
        });
        parent.Add(propertyField);
    }

    /// <summary>
    /// Assigns one shared Modules &amp; Patterns preset to the active advanced-pattern preset and rebuilds the details panel.
    /// </summary>
    /// <param name="panel">Owning panel that exposes the selected preset and serialized context.</param>
    /// <param name="sharedPresetProperty">Serialized property storing the shared preset reference.</param>
    /// <param name="sharedPreset">New shared preset reference to assign.</param>
    public static void AssignSharedPreset(EnemyAdvancedPatternPresetsPanel panel,
                                          SerializedProperty sharedPresetProperty,
                                          EnemyModulesAndPatternsPreset sharedPreset)
    {
        if (panel == null || sharedPresetProperty == null)
            return;

        EnemyAdvancedPatternPreset selectedPreset = panel.SelectedPreset;

        if (selectedPreset == null)
            return;

        if (sharedPresetProperty.objectReferenceValue == sharedPreset)
            return;

        SerializedObject presetSerializedObject = sharedPresetProperty.serializedObject;

        if (presetSerializedObject == null)
            return;

        Undo.RecordObject(selectedPreset, "Assign Enemy Modules And Patterns Preset");
        presetSerializedObject.Update();
        sharedPresetProperty.objectReferenceValue = sharedPreset;
        presetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(selectedPreset);
        EnemyManagementDraftSession.MarkDirty();
        SynchronizeSelectedPresetLoadoutWithSharedPreset(panel, sharedPreset);
        panel.RefreshPresetList();
        panel.BuildActiveDetailsSection();
    }

    /// <summary>
    /// Creates one empty shared preset asset and assigns it to the current advanced-pattern preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides selection and serialized context.</param>
    public static void CreateAndAssignSharedPreset(EnemyAdvancedPatternPresetsPanel panel)
    {
        if (panel == null)
            return;

        EnemyAdvancedPatternPreset selectedPreset = panel.SelectedPreset;

        if (selectedPreset == null)
            return;

        string requestedName = selectedPreset.PresetName + "_ModulesAndPatterns";
        EnemyModulesAndPatternsPreset sharedPreset = EnemyModulesAndPatternsPresetAssetUtility.CreatePresetAsset(requestedName);

        if (sharedPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(sharedPreset, "Create Enemy Modules And Patterns Preset");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty sharedPresetProperty = presetSerializedObject.FindProperty("modulesAndPatternsPreset");

        if (sharedPresetProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Assign Enemy Modules And Patterns Preset");
        presetSerializedObject.Update();
        sharedPresetProperty.objectReferenceValue = sharedPreset;
        presetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(sharedPreset);
        EditorUtility.SetDirty(selectedPreset);
        EnemyManagementDraftSession.MarkDirty();
        SynchronizeSelectedPresetLoadoutWithSharedPreset(panel, sharedPreset);
        panel.RefreshPresetList();
        panel.BuildActiveDetailsSection();
        Selection.activeObject = sharedPreset;
        EditorGUIUtility.PingObject(sharedPreset);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one committed text change to the shared preset metadata.
    /// </summary>
    /// <param name="panel">Owning panel used for refresh callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="propertyName">Serialized property name to update.</param>
    /// <param name="rawValue">Newly committed text value.</param>
    /// <param name="textField">Text field that should reflect the normalized value.</param>
    /// <param name="synchronizeAssetName">True when the ScriptableObject name must follow the field value.</param>
    private static void ApplySharedPresetTextChange(EnemyAdvancedPatternPresetsPanel panel,
                                                    SerializedObject sharedPresetSerializedObject,
                                                    EnemyModulesAndPatternsPreset sharedPreset,
                                                    string propertyName,
                                                    string rawValue,
                                                    TextField textField,
                                                    bool synchronizeAssetName)
    {
        if (sharedPresetSerializedObject == null ||
            sharedPreset == null ||
            textField == null ||
            string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        SerializedProperty property = sharedPresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        string nextValue = rawValue ?? string.Empty;

        if (synchronizeAssetName)
        {
            nextValue = EnemyManagementDraftSession.NormalizeAssetName(nextValue);

            if (string.IsNullOrWhiteSpace(nextValue))
            {
                textField.SetValueWithoutNotify(property.stringValue);
                return;
            }
        }

        if (string.Equals(property.stringValue, nextValue, StringComparison.Ordinal) &&
            (!synchronizeAssetName || string.Equals(sharedPreset.name, nextValue, StringComparison.Ordinal)))
        {
            textField.SetValueWithoutNotify(nextValue);
            return;
        }

        Undo.RecordObject(sharedPreset, synchronizeAssetName
            ? "Rename Enemy Modules And Patterns Shared Preset"
            : "Edit Enemy Modules And Patterns Shared Preset Metadata");
        sharedPresetSerializedObject.Update();
        property.stringValue = nextValue;

        if (synchronizeAssetName)
            sharedPreset.name = nextValue;

        bool applied = sharedPresetSerializedObject.ApplyModifiedProperties();
        sharedPresetSerializedObject.Update();
        textField.SetValueWithoutNotify(nextValue);

        if (!applied && !synchronizeAssetName)
            return;

        EditorUtility.SetDirty(sharedPreset);
        EnemyManagementDraftSession.MarkDirty();

        if (panel != null)
        {
            panel.RefreshPresetList();
            panel.BuildActiveDetailsSection();
        }
    }

    /// <summary>
    /// Regenerates the shared preset ID with undo support.
    /// </summary>
    /// <param name="panel">Owning panel used for refresh callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    private static void RegenerateSharedPresetId(EnemyAdvancedPatternPresetsPanel panel,
                                                 SerializedObject sharedPresetSerializedObject,
                                                 EnemyModulesAndPatternsPreset sharedPreset)
    {
        if (sharedPresetSerializedObject == null || sharedPreset == null)
            return;

        SerializedProperty idProperty = sharedPresetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(sharedPreset, "Regenerate Enemy Modules And Patterns Shared Preset ID");
        sharedPresetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        sharedPresetSerializedObject.ApplyModifiedProperties();
        sharedPresetSerializedObject.Update();
        EditorUtility.SetDirty(sharedPreset);
        EnemyManagementDraftSession.MarkDirty();

        if (panel != null)
            panel.RefreshPresetList();
    }

    /// <summary>
    /// Commits pending serialized-object edits for the shared preset using the normal undo pipeline.
    /// </summary>
    /// <param name="panel">Owning panel used for refresh callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="undoLabel">Undo label used for the recorded operation.</param>
    /// <returns>True when at least one serialized change is committed.</returns>
    internal static bool CommitSharedPresetSerializedChanges(EnemyAdvancedPatternPresetsPanel panel,
                                                             SerializedObject sharedPresetSerializedObject,
                                                             EnemyModulesAndPatternsPreset sharedPreset,
                                                             string undoLabel,
                                                             bool synchronizeSelectedPresetLoadout,
                                                             bool rebuildActiveSection)
    {
        if (sharedPresetSerializedObject == null || sharedPreset == null)
            return false;

        if (!sharedPresetSerializedObject.hasModifiedProperties)
        {
            EditorUtility.SetDirty(sharedPreset);
            EnemyManagementDraftSession.MarkDirty();

            if (panel != null)
            {
                if (synchronizeSelectedPresetLoadout)
                    SynchronizeSelectedPresetLoadoutWithSharedPreset(panel, sharedPreset);

                panel.RefreshPresetList();
                if (rebuildActiveSection)
                    panel.BuildActiveDetailsSection();
            }

            return false;
        }

        Undo.RecordObject(sharedPreset, undoLabel);
        bool applied = sharedPresetSerializedObject.ApplyModifiedProperties();
        sharedPresetSerializedObject.Update();

        if (!applied)
            return false;

        EditorUtility.SetDirty(sharedPreset);
        EnemyManagementDraftSession.MarkDirty();

        if (panel != null)
        {
            if (synchronizeSelectedPresetLoadout)
                SynchronizeSelectedPresetLoadoutWithSharedPreset(panel, sharedPreset);

            panel.RefreshPresetList();
            if (rebuildActiveSection)
                panel.BuildActiveDetailsSection();
        }

        return true;
    }

    /// <summary>
    /// Normalizes the selected preset loadout against the currently assigned shared preset after shared pattern edits or assignment changes.
    /// </summary>
    /// <param name="panel">Owning panel that exposes the selected preset and serialized loadout.</param>
    /// <param name="sharedPreset">Shared preset whose pattern IDs define the valid loadout space.</param>
    /// <returns>True when the selected preset loadout changes.</returns>
    internal static bool SynchronizeSelectedPresetLoadoutWithSharedPreset(EnemyAdvancedPatternPresetsPanel panel,
                                                                          EnemyModulesAndPatternsPreset sharedPreset)
    {
        if (panel == null || sharedPreset == null)
            return false;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return false;

        SerializedProperty sharedPresetProperty = presetSerializedObject.FindProperty("modulesAndPatternsPreset");

        if (sharedPresetProperty == null)
            return false;

        if (sharedPresetProperty.objectReferenceValue != sharedPreset)
            return false;

        SerializedProperty activePatternIdsProperty = presetSerializedObject.FindProperty("activePatternIds");

        if (activePatternIdsProperty == null)
            return false;

        HashSet<string> validPatternIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string firstPatternId = string.Empty;
        IReadOnlyList<EnemyModulesPatternDefinition> patterns = sharedPreset.Patterns;

        for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
        {
            EnemyModulesPatternDefinition pattern = patterns[patternIndex];

            if (pattern == null)
                continue;

            if (string.IsNullOrWhiteSpace(pattern.PatternId))
                continue;

            string patternId = pattern.PatternId.Trim();

            if (string.IsNullOrWhiteSpace(patternId))
                continue;

            if (string.IsNullOrWhiteSpace(firstPatternId))
                firstPatternId = patternId;

            validPatternIds.Add(patternId);
        }

        if (validPatternIds.Count <= 0)
            return false;

        HashSet<string> visitedPatternIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool changed = false;
        EnemyAdvancedPatternPreset selectedPreset = panel.SelectedPreset;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, "Synchronize Enemy Pattern Loadout");

        presetSerializedObject.Update();

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

            if (string.IsNullOrWhiteSpace(patternId))
            {
                activePatternIdsProperty.DeleteArrayElementAtIndex(index);
                index--;
                changed = true;
                continue;
            }

            string normalizedPatternId = patternId.Trim();

            if (!validPatternIds.Contains(normalizedPatternId) || !visitedPatternIds.Add(normalizedPatternId))
            {
                activePatternIdsProperty.DeleteArrayElementAtIndex(index);
                index--;
                changed = true;
                continue;
            }

            if (!string.Equals(entryProperty.stringValue, normalizedPatternId, StringComparison.Ordinal))
            {
                entryProperty.stringValue = normalizedPatternId;
                changed = true;
            }
        }

        if (activePatternIdsProperty.arraySize <= 0)
        {
            activePatternIdsProperty.InsertArrayElementAtIndex(0);
            SerializedProperty firstEntryProperty = activePatternIdsProperty.GetArrayElementAtIndex(0);

            if (firstEntryProperty != null)
            {
                firstEntryProperty.stringValue = firstPatternId;
                changed = true;
            }
        }

        if (!changed)
            return false;

        presetSerializedObject.ApplyModifiedProperties();

        if (selectedPreset != null)
            EditorUtility.SetDirty(selectedPreset);

        EnemyManagementDraftSession.MarkDirty();
        return true;
    }
    #endregion

    #endregion
}
