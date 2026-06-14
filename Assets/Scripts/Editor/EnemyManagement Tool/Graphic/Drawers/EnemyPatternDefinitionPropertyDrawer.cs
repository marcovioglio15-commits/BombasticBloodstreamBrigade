using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternDefinition.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternDefinition))]
public sealed class EnemyPatternDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the assembled-pattern editor UI with delayed text input so section refreshes happen only after commit.
    /// </summary>
    /// <param name="property">Serialized assembled pattern property.</param>
    /// <returns>Built root visual element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty patternIdProperty = property.FindPropertyRelative("patternId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty descriptionProperty = property.FindPropertyRelative("description");
        SerializedProperty unreplaceableProperty = property.FindPropertyRelative("unreplaceable");
        SerializedProperty moduleBindingsProperty = property.FindPropertyRelative("moduleBindings");

        if (patternIdProperty == null ||
            displayNameProperty == null ||
            descriptionProperty == null ||
            unreplaceableProperty == null ||
            moduleBindingsProperty == null)
        {
            Label errorLabel = new Label("EnemyPatternDefinition serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        AddDelayedTextField(root,
                            patternIdProperty,
                            "Pattern ID",
                            "Stable identifier used by Pattern Loadout.");
        AddDelayedTextField(root,
                            displayNameProperty,
                            "Display Name",
                            "Human-readable label shown in Pattern Assemble and Pattern Loadout.");
        AddDelayedTextField(root,
                            descriptionProperty,
                            "Description",
                            "Optional editor-facing notes for this assembled pattern.",
                            true);
        EnemyAdvancedPatternDrawerUtility.AddField(root, unreplaceableProperty, "Unreplaceable");
        EnemyAdvancedPatternDrawerUtility.AddField(root, moduleBindingsProperty, "Module Bindings");
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one delayed bound text field so serialized changes are committed only when editing is confirmed.
    /// </summary>
    /// <param name="parent">Parent visual element that receives the field.</param>
    /// <param name="property">Serialized string property bound to the field.</param>
    /// <param name="label">UI label shown above the field.</param>
    /// <param name="tooltip">Tooltip shown on the field.</param>
    /// <param name="multiline">True when the field should accept multiline input.</param>
    private static void AddDelayedTextField(VisualElement parent,
                                            SerializedProperty property,
                                            string label,
                                            string tooltip,
                                            bool multiline = false)
    {
        if (parent == null)
            return;

        if (property == null)
            return;

        TextField textField = new TextField(label);
        textField.isDelayed = true;
        textField.multiline = multiline;
        textField.tooltip = tooltip;

        if (multiline)
            textField.style.height = 60f;

        textField.BindProperty(property);
        parent.Add(textField);
    }
    #endregion

    #endregion
}
