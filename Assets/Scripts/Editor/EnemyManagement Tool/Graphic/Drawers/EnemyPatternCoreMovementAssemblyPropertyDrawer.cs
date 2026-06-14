using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternCoreMovementAssembly.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternCoreMovementAssembly))]
public sealed class EnemyPatternCoreMovementAssemblyPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the core-movement assembly UI.
    /// </summary>
    /// <param name="property">Serialized core-movement assembly property.</param>
    /// <returns>The built root visual element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty bindingProperty = property.FindPropertyRelative("binding");

        if (bindingProperty == null)
        {
            Label errorLabel = new Label("Core Movement assembly fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        PropertyField bindingField = new PropertyField(bindingProperty, "Core Movement Module");
        bindingField.BindProperty(bindingProperty);
        bindingField.tooltip = "Core movement binding reused while no short-range override is active.";
        root.Add(bindingField);
        return root;
    }
    #endregion

    #endregion
}
