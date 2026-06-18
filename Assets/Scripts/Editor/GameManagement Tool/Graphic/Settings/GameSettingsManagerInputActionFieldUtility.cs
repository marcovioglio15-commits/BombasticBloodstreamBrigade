using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Builds filtered Input System action selectors for Settings Manager controller navigation fields.
/// </summary>
internal static class GameSettingsManagerInputActionFieldUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one filtered action picker bound to a Settings Manager serialized action reference.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel with selected preset state.</param>
    /// <param name="parent">Parent element receiving the picker.</param>
    /// <param name="property">Serialized string storing the action id or path.</param>
    /// <param name="defaultActionPath">Fallback action path assigned when the stored reference is missing.</param>
    /// <param name="label">Foldout label shown in the tool.</param>
    /// <param name="tooltip">Tooltip describing how the action is used at runtime.</param>
    /// <param name="mode">Selector filter mode matching the expected action value type.</param>
    public static void AddActionPicker(GameSettingsManagerPresetsPanel panel,
                                       VisualElement parent,
                                       SerializedProperty property,
                                       string defaultActionPath,
                                       string label,
                                       string tooltip,
                                       InputActionSelectionElement.SelectionMode mode)
    {
        if (panel == null || parent == null || property == null || panel.PresetSerializedObject == null)
            return;

        InputActionAsset inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        EnsureDefaultActionReference(panel, inputAsset, property, defaultActionPath);

        Foldout foldout = new Foldout();
        foldout.text = label;
        foldout.tooltip = tooltip;
        foldout.value = true;
        InputActionSelectionElement selector = new InputActionSelectionElement(inputAsset, panel.PresetSerializedObject, property, mode);
        selector.ActionChanged += panel.MarkSelectedPresetDirty;
        foldout.Add(selector);
        parent.Add(foldout);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes a valid default Input Action id when the serialized reference is empty or no longer resolves.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel with selected preset state.</param>
    /// <param name="inputAsset">Input Action asset searched for the requested default action.</param>
    /// <param name="property">Serialized string storing the action id or path.</param>
    /// <param name="defaultActionPath">Fallback action path such as UI/Navigate.</param>
    private static void EnsureDefaultActionReference(GameSettingsManagerPresetsPanel panel,
                                                     InputActionAsset inputAsset,
                                                     SerializedProperty property,
                                                     string defaultActionPath)
    {
        if (inputAsset == null || property == null)
            return;

        string currentReference = property.stringValue;

        if (!string.IsNullOrWhiteSpace(currentReference))
        {
            InputAction currentAction = inputAsset.FindAction(currentReference, false);

            if (currentAction != null)
                return;
        }

        InputAction defaultAction = inputAsset.FindAction(defaultActionPath, false);

        if (defaultAction == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Assign Settings Navigation Action");
        panel.PresetSerializedObject.Update();
        property.stringValue = defaultAction.id.ToString();
        panel.PresetSerializedObject.ApplyModifiedProperties();
        panel.MarkSelectedPresetDirty();
    }
    #endregion

    #endregion
}
