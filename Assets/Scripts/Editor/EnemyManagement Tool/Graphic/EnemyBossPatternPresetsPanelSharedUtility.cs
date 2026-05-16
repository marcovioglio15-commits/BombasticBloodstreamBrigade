using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides reusable UI and serialized-array helpers for boss pattern preset panels.
/// </summary>
internal static class EnemyBossPatternPresetsPanelSharedUtility
{
    #region Methods

    #region UI
    /// <summary>
    /// Creates the standard section container under the panel details root.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="sectionTitle">Header label.</param>
    /// <returns>The created container, or null when the panel is not ready.</returns>
    public static VisualElement CreateDetailsSectionContainer(EnemyBossPatternPresetsPanel panel, string sectionTitle)
    {
        if (panel == null)
            return null;

        if (panel.DetailsSectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.EnemyManagement.BossPattern.Section." + sectionTitle);
        container.Add(header);
        panel.DetailsSectionContentRoot.Add(container);
        return container;
    }

    /// <summary>
    /// Creates one card container matching existing Enemy Management Tool styling.
    /// </summary>
    /// <returns>Created card container.</returns>
    public static VisualElement CreateCard()
    {
        VisualElement card = new VisualElement();
        card.style.borderBottomWidth = 1f;
        card.style.borderTopWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderBottomColor = new Color(0f, 0f, 0f, 0.25f);
        card.style.borderTopColor = new Color(0f, 0f, 0f, 0.25f);
        card.style.borderLeftColor = new Color(0f, 0f, 0f, 0.25f);
        card.style.borderRightColor = new Color(0f, 0f, 0f, 0.25f);
        card.style.paddingLeft = 6f;
        card.style.paddingRight = 6f;
        card.style.paddingTop = 4f;
        card.style.paddingBottom = 4f;
        card.style.marginTop = 6f;
        return card;
    }

    /// <summary>
    /// Adds one bound property field that marks the session dirty when edited.
    /// </summary>
    /// <param name="panel">Owning panel used for list refresh.</param>
    /// <param name="parent">Parent receiving the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">Field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    public static void AddTrackedPropertyField(EnemyBossPatternPresetsPanel panel,
                                               VisualElement parent,
                                               SerializedProperty property,
                                               string label,
                                               string tooltip)
    {
        if (panel == null || parent == null || property == null)
            return;

        if (property.propertyType == SerializedPropertyType.ObjectReference)
        {
            ObjectField objectField = new ObjectField(label);
            objectField.objectType = ResolveObjectFieldType(property);
            objectField.allowSceneObjects = false;
            objectField.tooltip = tooltip;
            objectField.SetValueWithoutNotify(property.objectReferenceValue);
            objectField.RegisterValueChangedCallback(evt =>
            {
                RecordSelectedPreset(panel, "Edit Boss Pattern Preset");
                panel.PresetSerializedObject.Update();
                property.objectReferenceValue = evt.newValue;
                ApplyTrackedPropertyChange(panel, false);
            });
            parent.Add(objectField);
            return;
        }

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt =>
        {
            ApplyTrackedPropertyChange(panel, false);
        });
        parent.Add(field);
    }

    /// <summary>
    /// Creates a property field that rebuilds the active section after edits.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">Field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <returns>Configured property field.</returns>
    public static VisualElement CreateReactivePropertyField(EnemyBossPatternPresetsPanel panel,
                                                            SerializedProperty property,
                                                            string label,
                                                            string tooltip)
    {
        if (property == null)
            return new VisualElement();

        if (property.propertyType == SerializedPropertyType.Boolean)
            return CreateReactiveBooleanField(panel, property, label, tooltip);

        if (property.propertyType == SerializedPropertyType.Enum)
            return CreateReactiveEnumField(panel, property, label, tooltip);

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt =>
        {
            ApplyTrackedPropertyChange(panel, true);
        });
        return field;
    }

    /// <summary>
    /// Adds one delayed text field that marks the session dirty when committed.
    /// </summary>
    /// <param name="panel">Owning panel used for list refresh.</param>
    /// <param name="parent">Parent receiving the field.</param>
    /// <param name="property">Serialized string property.</param>
    /// <param name="label">Field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <param name="multiline">True when the field accepts multiline input.</param>
    public static void AddTrackedTextField(EnemyBossPatternPresetsPanel panel,
                                           VisualElement parent,
                                           SerializedProperty property,
                                           string label,
                                           string tooltip,
                                           bool multiline)
    {
        if (panel == null || parent == null || property == null)
            return;

        TextField textField = new TextField(label);
        textField.isDelayed = true;
        textField.multiline = multiline;
        textField.tooltip = tooltip;

        if (multiline)
            textField.style.height = 60f;

        textField.SetValueWithoutNotify(property.stringValue);
        textField.RegisterValueChangedCallback(evt =>
        {
            RecordSelectedPreset(panel, "Edit Boss Pattern Preset");
            panel.PresetSerializedObject.Update();
            property.stringValue = evt.newValue;
            ApplyTrackedPropertyChange(panel, false);
        });
        parent.Add(textField);
    }

    /// <summary>
    /// Adds one tracked float slider bound to a serialized property.
    /// </summary>
    /// <param name="panel">Owning panel used for list refresh.</param>
    /// <param name="parent">Parent receiving the field.</param>
    /// <param name="property">Serialized float property.</param>
    /// <param name="label">Slider label.</param>
    /// <param name="lowValue">Lower slider bound.</param>
    /// <param name="highValue">Upper slider bound.</param>
    /// <param name="tooltip">Slider tooltip.</param>
    public static void AddFloatSliderField(EnemyBossPatternPresetsPanel panel,
                                           VisualElement parent,
                                           SerializedProperty property,
                                           string label,
                                           float lowValue,
                                           float highValue,
                                           string tooltip)
    {
        if (panel == null || parent == null || property == null)
            return;

        Slider slider = new Slider(label, lowValue, highValue);
        slider.showInputField = true;
        slider.tooltip = tooltip;
        slider.SetValueWithoutNotify(property.floatValue);
        slider.RegisterValueChangedCallback(evt =>
        {
            RecordSelectedPreset(panel, "Edit Boss Pattern Preset");
            panel.PresetSerializedObject.Update();
            property.floatValue = evt.newValue;
            ApplyTrackedPropertyChange(panel, false);
        });
        parent.Add(slider);
    }

    /// <summary>
    /// Adds one tracked integer slider bound to a serialized property.
    /// </summary>
    /// <param name="panel">Owning panel used for list refresh.</param>
    /// <param name="parent">Parent receiving the field.</param>
    /// <param name="property">Serialized int property.</param>
    /// <param name="label">Slider label.</param>
    /// <param name="lowValue">Lower slider bound.</param>
    /// <param name="highValue">Upper slider bound.</param>
    /// <param name="tooltip">Slider tooltip.</param>
    public static void AddIntSliderField(EnemyBossPatternPresetsPanel panel,
                                         VisualElement parent,
                                         SerializedProperty property,
                                         string label,
                                         int lowValue,
                                         int highValue,
                                         string tooltip)
    {
        if (panel == null || parent == null || property == null)
            return;

        SliderInt slider = new SliderInt(label, lowValue, highValue);
        slider.showInputField = true;
        slider.tooltip = tooltip;
        slider.SetValueWithoutNotify(property.intValue);
        slider.RegisterValueChangedCallback(evt =>
        {
            RecordSelectedPreset(panel, "Edit Boss Pattern Preset");
            panel.PresetSerializedObject.Update();
            property.intValue = evt.newValue;
            ApplyTrackedPropertyChange(panel, false);
        });
        parent.Add(slider);
    }

    /// <summary>
    /// Resolves the object selector type for explicit ObjectField controls.
    /// </summary>
    /// <param name="property">Serialized object-reference property being edited.</param>
    /// <returns>Object type accepted by the selector.</returns>
    private static Type ResolveObjectFieldType(SerializedProperty property)
    {
        if (property != null && string.Equals(property.name, "minionPrefab", StringComparison.Ordinal))
            return typeof(GameObject);

        return typeof(UnityEngine.Object);
    }

    /// <summary>
    /// Creates a reactive boolean toggle that applies its value before rebuilding dependent controls.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context.</param>
    /// <param name="property">Serialized boolean property.</param>
    /// <param name="label">Visible label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <returns>Configured toggle element.</returns>
    private static Toggle CreateReactiveBooleanField(EnemyBossPatternPresetsPanel panel,
                                                     SerializedProperty property,
                                                     string label,
                                                     string tooltip)
    {
        Toggle toggle = new Toggle(label);
        toggle.tooltip = tooltip;
        toggle.SetValueWithoutNotify(property.boolValue);
        toggle.RegisterValueChangedCallback(evt =>
        {
            RecordSelectedPreset(panel, "Edit Boss Pattern Preset");
            panel.PresetSerializedObject.Update();
            property.boolValue = evt.newValue;
            ApplyTrackedPropertyChange(panel, true);
        });
        return toggle;
    }

    /// <summary>
    /// Creates a reactive enum field for non-flags boss preset options.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context.</param>
    /// <param name="property">Serialized enum property.</param>
    /// <param name="label">Visible label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <returns>Configured enum element.</returns>
    private static EnumField CreateReactiveEnumField(EnemyBossPatternPresetsPanel panel,
                                                     SerializedProperty property,
                                                     string label,
                                                     string tooltip)
    {
        EnumField field = new EnumField(label, ResolveEnumValue(property));
        field.tooltip = tooltip;
        field.RegisterValueChangedCallback(evt =>
        {
            RecordSelectedPreset(panel, "Edit Boss Pattern Preset");
            panel.PresetSerializedObject.Update();
            property.enumValueIndex = Convert.ToInt32(evt.newValue);
            ApplyTrackedPropertyChange(panel, true);
        });
        return field;
    }

    /// <summary>
    /// Resolves a serialized enum property to the matching typed enum value used by EnumField.
    /// </summary>
    /// <param name="property">Serialized enum property.</param>
    /// <returns>Typed enum value for known boss preset enums.</returns>
    private static Enum ResolveEnumValue(SerializedProperty property)
    {
        if (property == null)
            return EnemyBossMinionSpawnTrigger.Interval;

        if (string.Equals(property.name, "interactionType", StringComparison.Ordinal))
            return (EnemyBossPatternInteractionType)property.enumValueIndex;

        return ResolveMinionTrigger(property);
    }

    /// <summary>
    /// Applies serialized changes, marks the preset dirty and optionally rebuilds dependent controls.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized preset context.</param>
    /// <param name="rebuildActiveSection">True when the active section must refresh after this edit.</param>
    private static void ApplyTrackedPropertyChange(EnemyBossPatternPresetsPanel panel, bool rebuildActiveSection)
    {
        if (panel == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.ApplyModifiedProperties();

        if (panel.SelectedPreset != null)
            EditorUtility.SetDirty(panel.SelectedPreset);

        EnemyManagementDraftSession.MarkDirty();

        if (rebuildActiveSection)
            panel.BuildActiveDetailsSection();
    }
    #endregion

    #region Array
    /// <summary>
    /// Builds action buttons for array-owned cards.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="arrayProperty">Serialized array containing the element.</param>
    /// <param name="index">Element index.</param>
    /// <param name="undoLabel">Label used in undo names.</param>
    /// <returns>Row containing duplicate, move and delete buttons.</returns>
    public static VisualElement CreateArrayActionsRow(EnemyBossPatternPresetsPanel panel,
                                                      SerializedProperty arrayProperty,
                                                      int index,
                                                      string undoLabel)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom = 4f;

        Button duplicateButton = new Button(() => DuplicateArrayElement(panel, arrayProperty, index, undoLabel));
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate this " + undoLabel + ".";
        row.Add(duplicateButton);

        Button moveUpButton = new Button(() => MoveArrayElement(panel, arrayProperty, index, index - 1, undoLabel));
        moveUpButton.text = "Up";
        moveUpButton.tooltip = "Move this " + undoLabel + " one slot up.";
        moveUpButton.style.marginLeft = 4f;
        moveUpButton.SetEnabled(index > 0);
        row.Add(moveUpButton);

        Button moveDownButton = new Button(() => MoveArrayElement(panel, arrayProperty, index, index + 1, undoLabel));
        moveDownButton.text = "Down";
        moveDownButton.tooltip = "Move this " + undoLabel + " one slot down.";
        moveDownButton.style.marginLeft = 4f;
        moveDownButton.SetEnabled(arrayProperty != null && index < arrayProperty.arraySize - 1);
        row.Add(moveDownButton);

        Button deleteButton = new Button(() => DeleteArrayElement(panel, arrayProperty, index, undoLabel));
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Delete this " + undoLabel + ".";
        deleteButton.style.marginLeft = 4f;
        row.Add(deleteButton);
        return row;
    }

    /// <summary>
    /// Records the selected preset for undo when available.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="actionName">Undo action name.</param>
    public static void RecordSelectedPreset(EnemyBossPatternPresetsPanel panel, string actionName)
    {
        if (panel == null)
            return;

        if (panel.SelectedPreset == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, actionName);
    }

    /// <summary>
    /// Marks the draft session dirty and rebuilds the active boss preset section.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    public static void MarkDirtyAndRebuild(EnemyBossPatternPresetsPanel panel)
    {
        EnemyManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.BuildActiveDetailsSection();
    }

    /// <summary>
    /// Duplicates one serialized array element and rebuilds the panel section.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="arrayProperty">Serialized array.</param>
    /// <param name="index">Source index.</param>
    /// <param name="undoLabel">Undo label suffix.</param>
    private static void DuplicateArrayElement(EnemyBossPatternPresetsPanel panel,
                                              SerializedProperty arrayProperty,
                                              int index,
                                              string undoLabel)
    {
        if (panel == null || arrayProperty == null || index < 0 || index >= arrayProperty.arraySize)
            return;

        RecordSelectedPreset(panel, "Duplicate " + undoLabel);
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        arrayProperty.InsertArrayElementAtIndex(index);
        presetSerializedObject.ApplyModifiedProperties();
        MarkDirtyAndRebuild(panel);
    }

    /// <summary>
    /// Moves one serialized array element and rebuilds the panel section.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="arrayProperty">Serialized array.</param>
    /// <param name="sourceIndex">Current index.</param>
    /// <param name="destinationIndex">Target index.</param>
    /// <param name="undoLabel">Undo label suffix.</param>
    private static void MoveArrayElement(EnemyBossPatternPresetsPanel panel,
                                         SerializedProperty arrayProperty,
                                         int sourceIndex,
                                         int destinationIndex,
                                         string undoLabel)
    {
        if (panel == null || arrayProperty == null)
            return;

        if (sourceIndex < 0 || sourceIndex >= arrayProperty.arraySize || destinationIndex < 0 || destinationIndex >= arrayProperty.arraySize)
            return;

        RecordSelectedPreset(panel, "Move " + undoLabel);
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        arrayProperty.MoveArrayElement(sourceIndex, destinationIndex);
        presetSerializedObject.ApplyModifiedProperties();
        MarkDirtyAndRebuild(panel);
    }

    /// <summary>
    /// Deletes one serialized array element and rebuilds the panel section.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="arrayProperty">Serialized array.</param>
    /// <param name="index">Element index.</param>
    /// <param name="undoLabel">Undo label suffix.</param>
    private static void DeleteArrayElement(EnemyBossPatternPresetsPanel panel,
                                           SerializedProperty arrayProperty,
                                           int index,
                                           string undoLabel)
    {
        if (panel == null || arrayProperty == null || index < 0 || index >= arrayProperty.arraySize)
            return;

        RecordSelectedPreset(panel, "Delete " + undoLabel);
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        arrayProperty.DeleteArrayElementAtIndex(index);
        presetSerializedObject.ApplyModifiedProperties();
        MarkDirtyAndRebuild(panel);
    }
    #endregion

    #region Enum Helpers
    /// <summary>
    /// Resolves one minion trigger property to a typed enum value.
    /// </summary>
    /// <param name="triggerProperty">Serialized trigger property.</param>
    /// <returns>Typed minion trigger.</returns>
    public static EnemyBossMinionSpawnTrigger ResolveMinionTrigger(SerializedProperty triggerProperty)
    {
        if (triggerProperty == null)
            return EnemyBossMinionSpawnTrigger.Interval;

        return (EnemyBossMinionSpawnTrigger)triggerProperty.enumValueIndex;
    }
    #endregion

    #endregion
}
