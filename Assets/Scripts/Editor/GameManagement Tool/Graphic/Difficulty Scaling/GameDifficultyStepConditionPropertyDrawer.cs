using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Presents one quantized difficulty condition with constrained variable and comparison controls.
/// </summary>
[CustomPropertyDrawer(typeof(GameDifficultyStepCondition))]
public sealed class GameDifficultyStepConditionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Unity Methods
    /// <summary>
    /// Creates one compact condition row bound to variable, comparison and threshold fields.
    /// </summary>
    /// <param name="property">Serialized step condition being rendered.</param>
    /// <returns>Bound compact condition row.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        root.style.flexDirection = FlexDirection.Row;
        SerializedProperty variable = property.FindPropertyRelative("variableName");
        PopupField<string> variablePopup =
            GameDifficultyEditorVariableFieldUtility.CreateVariablePopup(variable, "Variable");
        PropertyField comparison = new PropertyField(property.FindPropertyRelative("comparison"), "Comparison");
        PropertyField threshold = new PropertyField(property.FindPropertyRelative("threshold"), "Threshold");
        comparison.tooltip = property.FindPropertyRelative("comparison").tooltip;
        threshold.tooltip = property.FindPropertyRelative("threshold").tooltip;
        comparison.BindProperty(property.FindPropertyRelative("comparison"));
        threshold.BindProperty(property.FindPropertyRelative("threshold"));
        root.Add(variablePopup);
        root.Add(comparison);
        root.Add(threshold);
        return root;
    }
    #endregion

    #endregion
}
