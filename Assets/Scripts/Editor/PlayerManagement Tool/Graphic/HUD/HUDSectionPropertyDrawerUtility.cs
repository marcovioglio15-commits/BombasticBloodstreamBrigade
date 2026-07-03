using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Shared helpers for HUD section property drawers that can be invoked on component references.
/// </summary>
internal static class HUDSectionPropertyDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a normal object-reference field when a custom section drawer is used by a parent reference slot.
    /// </summary>
    /// <param name="property">Serialized property currently being drawn.</param>
    /// <param name="tooltip">Tooltip shown on the generated reference field.</param>
    /// <param name="field">Generated field when the property is an object reference.</param>
    /// <returns>True when the caller should return the generated field directly.</returns>
    public static bool TryCreateObjectReferenceField(SerializedProperty property, string tooltip, out VisualElement field)
    {
        field = null;

        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            return false;

        PropertyField propertyField = new PropertyField(property);
        propertyField.tooltip = tooltip;
        field = propertyField;
        return true;
    }
    #endregion

    #endregion
}
