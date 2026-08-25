using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Presents one summary statistic with a selectable built-in value or scoped scalable-stat dropdown.
/// </summary>
[CustomPropertyDrawer(typeof(GameHudStatisticDisplayDefinition))]
public sealed class GameHudStatisticDisplayDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Unity Methods
    /// <summary>
    /// Creates a foldout and conditionally exposes the scalable-stat selector and format-specific text fields.
    /// </summary>
    /// <param name="property">Serialized statistic display definition being rendered.</param>
    /// <returns>Bound conditional UI Toolkit hierarchy.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        Foldout root = new Foldout();
        SerializedProperty statisticProperty = property.FindPropertyRelative("statistic");
        SerializedProperty labelProperty = property.FindPropertyRelative("labelOverride");
        SerializedProperty formatProperty = property.FindPropertyRelative("valueFormat");
        VisualElement selectorRoot = new VisualElement();
        VisualElement formatRoot = new VisualElement();
        root.text = ResolveLabel(statisticProperty, labelProperty);
        AddProperty(root, statisticProperty, "Player Statistic");
        root.Add(selectorRoot);
        AddProperty(root, labelProperty, "Label Override");
        AddProperty(root, formatProperty, "Value Format");
        root.Add(formatRoot);
        AddProperty(root, property.FindPropertyRelative("font"), "Font");
        AddProperty(root, property.FindPropertyRelative("fontSize"), "Font Size");
        AddProperty(root, property.FindPropertyRelative("fontStyle"), "Font Style");
        AddProperty(root, property.FindPropertyRelative("color"), "Color");

        root.TrackPropertyValue(statisticProperty, changedProperty =>
        {
            BuildScalableStatSelector(selectorRoot, property, (GameHudPlayerStatistic)changedProperty.enumValueIndex);
            root.text = ResolveLabel(statisticProperty, labelProperty);
            GameManagementDraftSession.MarkDirty();
        });
        root.TrackPropertyValue(formatProperty, changedProperty =>
        {
            BuildFormatFields(formatRoot, property, (GameHudStatisticValueFormat)changedProperty.enumValueIndex);
            GameManagementDraftSession.MarkDirty();
        });
        root.TrackPropertyValue(labelProperty, changedProperty => root.text = ResolveLabel(statisticProperty, labelProperty));
        root.RegisterCallback<SerializedPropertyChangeEvent>(evt => GameManagementDraftSession.MarkDirty());
        BuildScalableStatSelector(selectorRoot, property, (GameHudPlayerStatistic)statisticProperty.enumValueIndex);
        BuildFormatFields(formatRoot, property, (GameHudStatisticValueFormat)formatProperty.enumValueIndex);
        return root;
    }
    #endregion

    #region Conditional Fields
    /// <summary>
    /// Shows the shared Player progression stat selector only for Custom Scalable Stat rows.
    /// </summary>
    /// <param name="root">Container receiving the selector.</param>
    /// <param name="property">Serialized row definition.</param>
    /// <param name="statistic">Currently selected built-in statistic kind.</param>
    private static void BuildScalableStatSelector(VisualElement root,
                                                  SerializedProperty property,
                                                  GameHudPlayerStatistic statistic)
    {
        root.Clear();

        if (statistic != GameHudPlayerStatistic.CustomScalableStat)
            return;

        SerializedProperty statNameProperty = property.FindPropertyRelative("scalableStatName");
        root.Add(PlayerConditionalWeaponSwitchStatSelectorUtility.BuildSelector(
            statNameProperty,
            "Scalable Stat",
            "Choose a stat exposed by the active Player Progression preset. The selected name is baked into ECS without runtime reflection."));
    }

    /// <summary>
    /// Shows only the formatting fields that affect the selected output mode.
    /// </summary>
    /// <param name="root">Container receiving format-specific controls.</param>
    /// <param name="property">Serialized row definition.</param>
    /// <param name="format">Current output format.</param>
    private static void BuildFormatFields(VisualElement root,
                                          SerializedProperty property,
                                          GameHudStatisticValueFormat format)
    {
        root.Clear();

        switch (format)
        {
            case GameHudStatisticValueFormat.Boolean:
                AddProperty(root, property.FindPropertyRelative("trueText"), "True Text");
                AddProperty(root, property.FindPropertyRelative("falseText"), "False Text");
                break;
            case GameHudStatisticValueFormat.Token:
                break;
            default:
                AddProperty(root, property.FindPropertyRelative("decimalPlaces"), "Decimal Places");
                AddProperty(root, property.FindPropertyRelative("displayMultiplier"), "Display Multiplier");
                break;
        }

        AddProperty(root, property.FindPropertyRelative("suffix"), "Suffix");
        AddProperty(root, property.FindPropertyRelative("showLabel"), "Show Label");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds one bound property while preserving its serialized tooltip.
    /// </summary>
    /// <param name="root">Container receiving the field.</param>
    /// <param name="property">Serialized property to render.</param>
    /// <param name="label">Visible field label.</param>
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
    /// Resolves the row foldout title from an override or the selected statistic enum.
    /// </summary>
    /// <param name="statisticProperty">Serialized statistic enum.</param>
    /// <param name="labelProperty">Optional label override.</param>
    /// <returns>Readable row title.</returns>
    private static string ResolveLabel(SerializedProperty statisticProperty, SerializedProperty labelProperty)
    {
        if (labelProperty != null && !string.IsNullOrWhiteSpace(labelProperty.stringValue))
            return labelProperty.stringValue;

        if (statisticProperty == null)
            return "Player Statistic";

        return statisticProperty.enumDisplayNames[statisticProperty.enumValueIndex];
    }
    #endregion

    #endregion
}
