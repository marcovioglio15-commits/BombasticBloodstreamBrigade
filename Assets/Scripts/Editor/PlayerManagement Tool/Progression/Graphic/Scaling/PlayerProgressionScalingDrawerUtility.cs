using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shared UI Toolkit helpers for progression property drawers that expose scalable numeric fields.
/// </summary>
public static class PlayerProgressionScalingDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Populates one container with direct child fields of one serialized parent property.
    /// Numeric children are rendered through scaling-aware UI controls.
    /// </summary>
    /// <param name="container">UI container that receives generated fields.</param>
    /// <param name="parentProperty">Serialized parent property whose direct children are rendered.</param>
    /// <param name="scalingRulesProperty">Scaling rules list used to resolve Add Scale state.</param>

    public static void PopulateDirectChildFields(VisualElement container,
                                                 SerializedProperty parentProperty,
                                                 SerializedProperty scalingRulesProperty)
    {
        if (container == null)
            return;

        if (parentProperty == null)
        {
            Label missingLabel = new Label("Missing serialized property.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            container.Add(missingLabel);
            return;
        }

        SerializedProperty iterator = parentProperty.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();
        bool enterChildren = true;
        bool hasVisibleChild = false;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (SerializedProperty.EqualContents(iterator, endProperty))
                break;

            if (iterator.depth != parentProperty.depth + 1)
                continue;

            SerializedProperty childProperty = iterator.Copy();
            VisualElement scalableField = PlayerScalingFieldElementFactory.CreateField(childProperty,
                                                                                       scalingRulesProperty,
                                                                                       childProperty.displayName);
            container.Add(scalableField);
            hasVisibleChild = true;
        }

        if (hasVisibleChild)
            return;

        Label noFieldsLabel = new Label("No editable fields available.");
        noFieldsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        container.Add(noFieldsLabel);
    }
    #endregion

    #endregion
}
