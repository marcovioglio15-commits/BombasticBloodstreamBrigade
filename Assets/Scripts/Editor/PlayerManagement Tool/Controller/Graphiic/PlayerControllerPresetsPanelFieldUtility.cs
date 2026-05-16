using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Provides common foldout, binding and default-action helpers for player controller preset panels.
/// </summary>
internal static class PlayerControllerPresetsPanelFieldUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the values foldout for a serialized values block and a fixed set of child field names.
    /// </summary>
    /// <param name="valuesProperty">Serialized values block to inspect.</param>
    /// <param name="scalingRulesProperty">Scaling rules serialized property used by scaling-aware fields.</param>
    /// <param name="fieldNames">Relative child field names to render.</param>
    /// <returns>Returns the configured values foldout.</returns>
    public static Foldout BuildValuesFoldout(SerializedProperty valuesProperty, SerializedProperty scalingRulesProperty, string[] fieldNames)
    {
        Foldout foldout = new Foldout();
        foldout.text = "Values";
        foldout.value = true;

        VisualElement valuesContainer = new VisualElement();
        valuesContainer.style.flexDirection = FlexDirection.Column;

        if (valuesProperty != null)
        {
            for (int index = 0; index < fieldNames.Length; index++)
            {
                SerializedProperty fieldProperty = valuesProperty.FindPropertyRelative(fieldNames[index]);

                if (fieldProperty == null)
                    continue;

                valuesContainer.Add(PlayerScalingFieldElementFactory.CreateField(fieldProperty, scalingRulesProperty));
            }
        }

        foldout.Add(valuesContainer);
        return foldout;
    }

    /// <summary>
    /// Assigns a default input action identifier when the current serialized action reference is missing or invalid.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset and serialized state.</param>
    /// <param name="actionIdProperty">Serialized string property containing the action identifier.</param>
    /// <param name="actionName">Fallback action name to resolve inside the input asset.</param>

    public static void EnsureDefaultActionId(PlayerControllerPresetsPanel panel, SerializedProperty actionIdProperty, string actionName)
    {
        if (panel == null || actionIdProperty == null || panel.InputAsset == null)
            return;

        string currentId = actionIdProperty.stringValue;

        if (!string.IsNullOrWhiteSpace(currentId))
        {
            InputAction existingAction = panel.InputAsset.FindAction(currentId, false);

            if (existingAction != null)
                return;
        }

        InputAction defaultAction = panel.InputAsset.FindAction(actionName, false);

        if (defaultAction == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Assign Default Action");
        panel.PresetSerializedObject.Update();
        actionIdProperty.stringValue = defaultAction.id.ToString();
        panel.PresetSerializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Builds the bindings foldout for an input action selector element.
    /// </summary>
    /// <param name="inputAsset">Input action asset that provides available actions.</param>
    /// <param name="presetSerializedObject">Serialized preset object owning the action id property.</param>
    /// <param name="actionIdProperty">Serialized string property containing the action identifier.</param>
    /// <param name="mode">Selection mode used by the input action selector.</param>
    /// <returns>Returns the configured bindings foldout.</returns>
    public static Foldout BuildBindingsFoldout(InputActionAsset inputAsset,
                                               SerializedObject presetSerializedObject,
                                               SerializedProperty actionIdProperty,
                                               InputActionSelectionElement.SelectionMode mode)
    {
        Foldout foldout = new Foldout();
        foldout.text = "Bindings";
        foldout.value = true;
        foldout.Add(new InputActionSelectionElement(inputAsset, presetSerializedObject, actionIdProperty, mode));
        return foldout;
    }

    /// <summary>
    /// Creates a zoom slider that updates the provided pie chart element.
    /// </summary>
    /// <param name="pieChart">Pie chart whose zoom value is controlled by the slider.</param>
    /// <returns>Returns the configured slider.</returns>
    public static Slider CreatePieZoomSlider(PieChartElement pieChart)
    {
        Slider slider = new Slider("Pie Zoom", 0.6f, 1.6f);
        slider.value = 1f;
        slider.RegisterValueChangedCallback(evt =>
        {
            if (pieChart == null)
                return;

            pieChart.SetZoom(evt.newValue);
        });
        return slider;
    }
    #endregion

    #endregion
}
