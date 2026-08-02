using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Presents one difficulty coefficient with mode-specific formula, curve or quantized-step controls.
/// </summary>
[CustomPropertyDrawer(typeof(GameDifficultyCoefficientDefinition))]
public sealed class GameDifficultyCoefficientDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Unity Methods
    /// <summary>
    /// Creates a foldout whose specialized fields react to the selected scaling mode.
    /// </summary>
    /// <param name="property">Serialized coefficient definition being rendered.</param>
    /// <returns>Bound UI Toolkit root for the coefficient.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        Foldout root = new Foldout();
        SerializedProperty idProperty = property.FindPropertyRelative("coefficientId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty modeProperty = property.FindPropertyRelative("scalingMode");
        VisualElement specializedFields = new VisualElement();
        root.text = ResolveFoldoutLabel(displayNameProperty, idProperty);
        AddProperty(root, idProperty, "Coefficient ID");
        AddProperty(root, displayNameProperty, "Display Name");
        AddProperty(root, property.FindPropertyRelative("description"), "Description");
        AddProperty(root, modeProperty, "Scaling Mode");
        AddProperty(root, property.FindPropertyRelative("defaultValue"), "Default Value");
        AddProperty(root, property.FindPropertyRelative("minimumValue"), "Minimum Value");
        AddProperty(root, property.FindPropertyRelative("maximumValue"), "Maximum Value");
        root.Add(specializedFields);
        AddProperty(root, property.FindPropertyRelative("debugInConsole"), "Debug In Console");

        // Rebuild only mode-dependent controls when the enum changes.
        root.TrackPropertyValue(modeProperty, changedProperty =>
        {
            BuildSpecializedFields(specializedFields, property, (GameDifficultyScalingMode)changedProperty.enumValueIndex);
            GameManagementDraftSession.MarkDirty();
        });
        BuildSpecializedFields(specializedFields,
                               property,
                               (GameDifficultyScalingMode)modeProperty.enumValueIndex);
        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            root.text = ResolveFoldoutLabel(displayNameProperty, idProperty);
            GameManagementDraftSession.MarkDirty();
        });
        return root;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Populates only controls used by the selected coefficient calculation mode.
    /// </summary>
    /// <param name="root">Container receiving specialized controls.</param>
    /// <param name="property">Serialized coefficient definition.</param>
    /// <param name="scalingMode">Current authoring strategy.</param>
    private static void BuildSpecializedFields(VisualElement root,
                                               SerializedProperty property,
                                               GameDifficultyScalingMode scalingMode)
    {
        root.Clear();

        switch (scalingMode)
        {
            case GameDifficultyScalingMode.Curve:
                root.Add(GameDifficultyEditorVariableFieldUtility.CreateVariablePopup(
                    property.FindPropertyRelative("curveInputVariable"),
                    "Input Variable"));
                AddProperty(root, property.FindPropertyRelative("scalingCurve"), "Scaling Curve");
                break;
            case GameDifficultyScalingMode.Steps:
                AddProperty(root, property.FindPropertyRelative("steps"), "Ordered Quantized Steps");
                break;
            default:
                GameDifficultyEditorVariableFieldUtility.AddFormulaEditor(root,
                                                                          property.FindPropertyRelative("formula"),
                                                                          "Unified Formula");
                break;
        }
    }

    /// <summary>
    /// Adds one bound property field and forwards the serialized tooltip.
    /// </summary>
    /// <param name="root">Container receiving the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">Designer-facing label.</param>
    private static void AddProperty(VisualElement root, SerializedProperty property, string label)
    {
        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        root.Add(field);
    }

    /// <summary>
    /// Resolves a readable coefficient foldout title from display and technical identifiers.
    /// </summary>
    /// <param name="displayNameProperty">Serialized designer-facing label.</param>
    /// <param name="idProperty">Serialized stable coefficient identifier.</param>
    /// <returns>Best available readable foldout title.</returns>
    private static string ResolveFoldoutLabel(SerializedProperty displayNameProperty,
                                              SerializedProperty idProperty)
    {
        if (displayNameProperty != null && !string.IsNullOrWhiteSpace(displayNameProperty.stringValue))
            return displayNameProperty.stringValue;

        if (idProperty != null && !string.IsNullOrWhiteSpace(idProperty.stringValue))
            return idProperty.stringValue;

        return "Difficulty Coefficient";
    }
    #endregion

    #endregion
}
