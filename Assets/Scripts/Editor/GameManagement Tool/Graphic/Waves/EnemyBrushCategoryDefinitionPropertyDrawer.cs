using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Presents one reusable brush category with a protected identity and difficulty-aware weighted entries.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyBrushCategoryDefinition))]
public sealed class EnemyBrushCategoryDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Unity Methods
    /// <summary>
    /// Creates a focused category foldout used by the dedicated Brush Categories tab.
    /// </summary>
    /// <param name="property">Serialized category definition being rendered.</param>
    /// <returns>Bound category authoring foldout.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        SerializedProperty displayName = property.FindPropertyRelative("displayName");
        Foldout root = new Foldout
        {
            text = string.IsNullOrWhiteSpace(displayName.stringValue)
                ? "Enemy Brush Category"
                : displayName.stringValue
        };
        AddProperty(root, displayName, "Display Name", true);
        AddProperty(root, property.FindPropertyRelative("technicalId"), "Stable Technical ID", false);
        AddProperty(root, property.FindPropertyRelative("description"), "Description", true);
        AddProperty(root, property.FindPropertyRelative("brushColor"), "Brush Color", true);
        SerializedProperty coefficient = property.FindPropertyRelative("difficultyCoefficientId");
        root.Add(GameDifficultyEditorVariableFieldUtility.CreateCoefficientPopup(coefficient,
                                                                                 "Difficulty Coefficient",
                                                                                 true));
        AddProperty(root, property.FindPropertyRelative("entries"), "Weighted Enemy Presets", true);
        root.TrackPropertyValue(displayName, changedProperty =>
        {
            root.text = string.IsNullOrWhiteSpace(changedProperty.stringValue)
                ? "Enemy Brush Category"
                : changedProperty.stringValue;
            GameManagementDraftSession.MarkDirty();
        });
        return root;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Adds one bound category property and optionally permits designer edits.
    /// </summary>
    /// <param name="root">Category foldout receiving the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">Designer-facing label.</param>
    /// <param name="editable">Whether the field can be modified.</param>
    private static void AddProperty(VisualElement root,
                                    SerializedProperty property,
                                    string label,
                                    bool editable)
    {
        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        field.SetEnabled(editable);
        root.Add(field);
    }
    #endregion

    #endregion
}
