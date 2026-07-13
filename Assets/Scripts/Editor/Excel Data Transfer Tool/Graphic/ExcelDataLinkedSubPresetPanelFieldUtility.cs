using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds bound UI Toolkit fields shared by linked Excel transfer sub-preset panels.
/// </summary>
internal static class ExcelDataLinkedSubPresetPanelFieldUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one bound property field and marks the transfer draft dirty when edited.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="serializedObject">Serialized object that owns the field.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    public static void AddPropertyField(VisualElement parent,
                                        SerializedObject serializedObject,
                                        string propertyName,
                                        string label,
                                        string tooltip)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt => ExcelDataTransferDraftSession.MarkDirty());
        parent.Add(field);
    }

    /// <summary>
    /// Adds one disabled property field for stable metadata.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="serializedObject">Serialized object that owns the field.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    public static void AddDisabledPropertyField(VisualElement parent,
                                                SerializedObject serializedObject,
                                                string propertyName,
                                                string label,
                                                string tooltip)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.SetEnabled(false);
        parent.Add(field);
    }

    /// <summary>
    /// Adds a workbook profile field that can refresh dependent custom-path visibility after edits.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="serializedObject">Serialized object that owns the field.</param>
    /// <param name="propertyName">Workbook profile property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <param name="scheduleRefresh">Callback that schedules a panel rebuild after the current UI event.</param>
    public static void AddWorkbookProfileField(VisualElement parent,
                                               SerializedObject serializedObject,
                                               string propertyName,
                                               string label,
                                               string tooltip,
                                               Action scheduleRefresh)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            ExcelDataTransferDraftSession.MarkDirty();

            if (scheduleRefresh != null)
                scheduleRefresh();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Adds a custom path field only when the selected workbook profile requires it.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="serializedObject">Serialized object that owns the field.</param>
    /// <param name="profilePropertyName">Workbook profile property name.</param>
    /// <param name="pathPropertyName">Custom path property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    public static void AddCustomWorkbookPathFieldIfNeeded(VisualElement parent,
                                                          SerializedObject serializedObject,
                                                          string profilePropertyName,
                                                          string pathPropertyName,
                                                          string label,
                                                          string tooltip)
    {
        SerializedProperty profileProperty = serializedObject.FindProperty(profilePropertyName);

        if (profileProperty == null ||
            profileProperty.enumValueIndex != (int)ExcelDataWorkbookPathProfile.CustomPath)
            return;

        AddPropertyField(parent, serializedObject, pathPropertyName, label, tooltip);
    }

    /// <summary>
    /// Adds a popup field for an enum property without relying on PropertyField enum menu rebinding.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="serializedObject">Serialized object that owns the enum field.</param>
    /// <param name="propertyName">Serialized enum property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <param name="valueChanged">Optional callback receiving the selected enum index after persistence.</param>
    /// <returns>Configured popup field, or null when the property is missing.</returns>
    public static PopupField<string> AddEnumPopupField(VisualElement parent,
                                                       SerializedObject serializedObject,
                                                       string propertyName,
                                                       string label,
                                                       string tooltip,
                                                       Action<int> valueChanged)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return null;

        List<string> options = BuildEnumOptions(property);
        int selectedIndex = ResolveSafeEnumIndex(property, options);
        PopupField<string> field = new PopupField<string>(label, options, selectedIndex);
        field.tooltip = tooltip;
        field.RegisterValueChangedCallback(evt =>
        {
            int newIndex = options.IndexOf(evt.newValue);

            if (newIndex < 0)
                return;

            WriteEnumProperty(serializedObject, propertyName, newIndex);

            if (valueChanged != null)
                valueChanged(newIndex);
        });
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Adds a text field persisted manually to avoid rebinding the whole Workbook section.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="serializedObject">Serialized object that owns the string field.</param>
    /// <param name="propertyName">Serialized string property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <returns>Configured text field, or null when the property is missing.</returns>
    public static TextField AddStringField(VisualElement parent,
                                           SerializedObject serializedObject,
                                           string propertyName,
                                           string label,
                                           string tooltip)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return null;

        TextField field = new TextField(label);
        field.tooltip = tooltip;
        field.SetValueWithoutNotify(property.stringValue);
        field.RegisterValueChangedCallback(evt => WriteStringProperty(serializedObject, propertyName, evt.newValue));
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Resolves a safe enum index for a serialized property.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the enum field.</param>
    /// <param name="propertyName">Serialized enum property name.</param>
    /// <returns>Current enum index, or zero when missing or out of range.</returns>
    public static int ResolveEnumValueIndex(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return 0;

        return ResolveSafeEnumIndex(property, BuildEnumOptions(property));
    }
    #endregion

    #region Manual Field Persistence
    /// <summary>
    /// Builds display options from Unity's serialized enum metadata.
    /// </summary>
    /// <param name="property">Enum property to inspect.</param>
    /// <returns>Display labels for popup options.</returns>
    private static List<string> BuildEnumOptions(SerializedProperty property)
    {
        List<string> options = new List<string>();
        string[] displayNames = property.enumDisplayNames;

        for (int index = 0; index < displayNames.Length; index++)
            options.Add(displayNames[index]);

        return options;
    }

    /// <summary>
    /// Resolves an enum index that is valid for the provided popup option list.
    /// </summary>
    /// <param name="property">Enum property to inspect.</param>
    /// <param name="options">Popup options generated from the property.</param>
    /// <returns>Safe enum index.</returns>
    private static int ResolveSafeEnumIndex(SerializedProperty property, List<string> options)
    {
        if (options.Count <= 0)
            return 0;

        if (property.enumValueIndex < 0)
            return 0;

        if (property.enumValueIndex >= options.Count)
            return 0;

        return property.enumValueIndex;
    }

    /// <summary>
    /// Writes an enum property through SerializedObject and marks the edited preset dirty.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the enum field.</param>
    /// <param name="propertyName">Serialized enum property name.</param>
    /// <param name="newIndex">Enum index selected by the user.</param>
    private static void WriteEnumProperty(SerializedObject serializedObject, string propertyName, int newIndex)
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        if (property.enumValueIndex == newIndex)
            return;

        property.enumValueIndex = newIndex;
        serializedObject.ApplyModifiedProperties();
        MarkDirty(serializedObject.targetObject);
    }

    /// <summary>
    /// Writes a string property through SerializedObject and marks the edited preset dirty.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the string field.</param>
    /// <param name="propertyName">Serialized string property name.</param>
    /// <param name="newValue">String value entered by the user.</param>
    private static void WriteStringProperty(SerializedObject serializedObject, string propertyName, string newValue)
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        if (property.stringValue == newValue)
            return;

        property.stringValue = newValue;
        serializedObject.ApplyModifiedProperties();
        MarkDirty(serializedObject.targetObject);
    }

    /// <summary>
    /// Marks the edited object and transfer draft state dirty after manual field persistence.
    /// </summary>
    /// <param name="targetObject">Edited Unity object.</param>
    private static void MarkDirty(UnityEngine.Object targetObject)
    {
        if (targetObject != null)
            EditorUtility.SetDirty(targetObject);

        ExcelDataTransferDraftSession.MarkDirty();
    }
    #endregion

    #endregion
}
