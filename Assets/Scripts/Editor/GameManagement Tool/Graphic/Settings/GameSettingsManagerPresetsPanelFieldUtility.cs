using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Field helpers shared by Settings Manager preset editor sections.
/// </summary>
internal static class GameSettingsManagerPresetsPanelFieldUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one enum popup bound to a serialized enum and optionally rebuilds dependent controls after a real value change.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="property">Serialized enum property.</param>
    /// <param name="label">Display label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <param name="choices">Explicit enum choices shown in the popup.</param>
    /// <param name="formatValue">Formatter used for selected and listed values.</param>
    /// <param name="fallbackValue">Fallback value used when the serialized enum is invalid.</param>
    /// <param name="rebuildOnChange">True when dependent controls must refresh immediately after the value changes.</param>
    public static void AddEnumPopupProperty<TEnum>(GameSettingsManagerPresetsPanel panel,
                                                   VisualElement parent,
                                                   SerializedProperty property,
                                                   string label,
                                                   string tooltip,
                                                   List<TEnum> choices,
                                                   Func<TEnum, string> formatValue,
                                                   TEnum fallbackValue,
                                                   bool rebuildOnChange) where TEnum : struct, Enum
    {
        if (panel == null || parent == null || property == null || choices == null || choices.Count <= 0)
            return;

        TEnum currentValue = ResolveEnumPropertyValue(property, fallbackValue);
        PopupField<TEnum> field = new PopupField<TEnum>(label, choices, currentValue, formatValue, formatValue);
        field.tooltip = tooltip;
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Settings Manager Enum");
            panel.PresetSerializedObject.Update();
            AssignEnumPropertyValue(property, evt.newValue);
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();

            if (rebuildOnChange)
                panel.BuildActiveSection();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Resolves a typed enum value from a serialized enum property without assuming contiguous enum values.
    /// </summary>
    /// <param name="property">Serialized enum property to inspect.</param>
    /// <param name="fallbackValue">Fallback returned when the serialized value is invalid.</param>
    /// <returns>Resolved typed enum value.</returns>
    public static TEnum ResolveEnumPropertyValue<TEnum>(SerializedProperty property, TEnum fallbackValue) where TEnum : struct, Enum
    {
        if (property == null)
            return fallbackValue;

        Array values = Enum.GetValues(typeof(TEnum));
        int rawValue = property.intValue;

        for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
        {
            object valueObject = values.GetValue(valueIndex);

            if (valueObject == null)
                continue;

            if (Convert.ToInt32(valueObject) == rawValue)
                return (TEnum)valueObject;
        }

        int enumValueIndex = property.enumValueIndex;

        if (enumValueIndex >= 0 && enumValueIndex < values.Length)
            return (TEnum)values.GetValue(enumValueIndex);

        return fallbackValue;
    }

    /// <summary>
    /// Adds one float property field.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="property">Serialized float property.</param>
    /// <param name="label">Display label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    public static void AddFloatProperty(GameSettingsManagerPresetsPanel panel,
                                        VisualElement parent,
                                        SerializedProperty property,
                                        string label,
                                        string tooltip)
    {
        if (property == null)
            return;

        FloatField field = new FloatField(label);
        field.tooltip = tooltip;
        field.SetValueWithoutNotify(property.floatValue);
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Settings Manager Float");
            panel.PresetSerializedObject.Update();
            property.floatValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();
        });
        parent.Add(field);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Assigns a typed enum value to a serialized enum property by name first, then by raw integer value.
    /// </summary>
    /// <param name="property">Serialized enum property to mutate.</param>
    /// <param name="enumValue">Typed enum value selected by the user.</param>
    private static void AssignEnumPropertyValue<TEnum>(SerializedProperty property, TEnum enumValue) where TEnum : struct, Enum
    {
        if (property == null)
            return;

        string enumName = enumValue.ToString();
        string[] enumNames = property.enumNames;

        for (int nameIndex = 0; nameIndex < enumNames.Length; nameIndex++)
        {
            if (!string.Equals(enumNames[nameIndex], enumName, StringComparison.Ordinal))
                continue;

            property.enumValueIndex = nameIndex;
            return;
        }

        property.intValue = Convert.ToInt32(enumValue);
    }
    #endregion

    #endregion
}
