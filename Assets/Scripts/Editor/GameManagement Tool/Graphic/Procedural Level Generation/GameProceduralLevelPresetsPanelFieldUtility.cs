using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Centralizes draft-aware UI Toolkit bindings used throughout the Procedural Level editor.
/// </summary>
internal static class GameProceduralLevelPresetsPanelFieldUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds a serialized property field and reports every committed change to the draft session.
    /// </summary>
    /// <param name="parent">Visual container receiving the field.</param>
    /// <param name="property">Serialized property edited by the field.</param>
    /// <param name="label">Readable field label.</param>
    /// <param name="tooltip">-facing explanation of the setting.</param>
    /// <param name="onChanged">Optional callback used to rebuild conditional UI.</param>
    /// <returns>Created property field, or null when arguments are invalid.</returns>
    public static PropertyField AddBoundProperty(VisualElement parent,
                                                 SerializedProperty property,
                                                 string label,
                                                 string tooltip,
                                                 Action onChanged = null)
    {
        if (parent == null || property == null)
            return null;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        bool acceptsCommittedChanges = false;
        field.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            // Defer activation until UI Toolkit has completed the first binding pass for this attached field.
            field.schedule.Execute(() => acceptsCommittedChanges = true);
        });
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            // Binding can emit a synthetic change while the field is attaching; it is not a  edit.
            if (!acceptsCommittedChanges)
            {
                evt.StopPropagation();
                return;
            }

            if (property.serializedObject.targetObject != null)
                EditorUtility.SetDirty(property.serializedObject.targetObject);

            GameManagementDraftSession.MarkDirty();

            if (onChanged != null)
                onChanged();

            evt.StopPropagation();
        });
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Adds a delayed text field so identifier and asset-name edits commit as one undoable operation.
    /// </summary>
    /// <param name="parent">Visual container receiving the text field.</param>
    /// <param name="property">Serialized string property edited by the field.</param>
    /// <param name="label">Readable field label.</param>
    /// <param name="tooltip">-facing explanation of the text.</param>
    /// <param name="multiline">True when the field should accept multiple lines.</param>
    /// <param name="onChanged">Optional callback invoked after the serialized change.</param>
    /// <returns>Created delayed text field, or null when arguments are invalid.</returns>
    public static TextField AddDelayedText(VisualElement parent,
                                           SerializedProperty property,
                                           string label,
                                           string tooltip,
                                           bool multiline,
                                           Action onChanged = null)
    {
        if (parent == null || property == null)
            return null;

        TextField field = new TextField(label);
        field.tooltip = tooltip;
        field.isDelayed = true;
        field.multiline = multiline;
        field.SetValueWithoutNotify(property.stringValue);
        field.RegisterValueChangedCallback(evt =>
        {
            if (string.Equals(property.stringValue, evt.newValue, StringComparison.Ordinal))
                return;

            UnityEngine.Object targetObject = property.serializedObject.targetObject;

            if (targetObject != null)
                Undo.RecordObject(targetObject, "Edit Procedural Level Preset");

            property.serializedObject.Update();
            property.stringValue = evt.newValue;
            property.serializedObject.ApplyModifiedProperties();

            if (targetObject != null)
                EditorUtility.SetDirty(targetObject);

            GameManagementDraftSession.MarkDirty();

            if (onChanged != null)
                onChanged();
        });
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Applies an explicit serialized mutation with consistent Undo, dirty and draft bookkeeping.
    /// </summary>
    /// <param name="serializedObject">Serialized preset object receiving the mutation.</param>
    /// <param name="undoName">Readable Undo operation label.</param>
    /// <param name="mutation">Mutation performed after the serialized object is refreshed.</param>
    public static void CommitMutation(SerializedObject serializedObject, string undoName, Action mutation)
    {
        if (serializedObject == null || serializedObject.targetObject == null || mutation == null)
            return;

        Undo.RecordObject(serializedObject.targetObject, undoName);
        serializedObject.Update();
        mutation();
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(serializedObject.targetObject);
        GameManagementDraftSession.MarkDirty();
    }
    #endregion

    #endregion
}
