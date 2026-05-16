using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws scalable stat entries with type-aware default fields and unified numeric-type support.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerScalableStatDefinition))]
public sealed class PlayerScalableStatDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit tree used to edit one scalable stat entry inside progression presets.
    /// </summary>
    /// <param name="property">Serialized scalable stat entry.</param>
    /// <returns>Configured visual tree for the scalable stat entry.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty statNameProperty = property.FindPropertyRelative("statName");
        SerializedProperty statTypeProperty = property.FindPropertyRelative("statType");
        SerializedProperty defaultValueProperty = property.FindPropertyRelative("defaultValue");
        SerializedProperty minimumValueProperty = property.FindPropertyRelative("minimumValue");
        SerializedProperty maximumValueProperty = property.FindPropertyRelative("maximumValue");
        SerializedProperty defaultBooleanValueProperty = property.FindPropertyRelative("defaultBooleanValue");
        SerializedProperty defaultTokenValueProperty = property.FindPropertyRelative("defaultTokenValue");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        SerializedProperty scalableStatsProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalableStats")
            : null;
        HashSet<string> allowedVariables = PlayerScalingFormulaValidationUtility.BuildVariableSet(scalableStatsProperty);

        if (statNameProperty == null ||
            statTypeProperty == null ||
            defaultValueProperty == null ||
            minimumValueProperty == null ||
            maximumValueProperty == null ||
            defaultBooleanValueProperty == null ||
            defaultTokenValueProperty == null)
        {
            Label missingLabel = new Label("Scalable stat entry is missing serialized fields.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(missingLabel);
            return root;
        }

        AddField(root, statNameProperty, null, "Name", allowedVariables);
        AddField(root, statTypeProperty, null, "Type", allowedVariables);

        VisualElement numericContainer = new VisualElement();
        VisualElement booleanContainer = new VisualElement();
        VisualElement tokenContainer = new VisualElement();
        root.Add(numericContainer);
        root.Add(booleanContainer);
        root.Add(tokenContainer);

        AddField(numericContainer, defaultValueProperty, scalingRulesProperty, "Default Value", allowedVariables);
        AddField(numericContainer, minimumValueProperty, null, "Min", allowedVariables);
        AddField(numericContainer, maximumValueProperty, null, "Max", allowedVariables);
        AddField(booleanContainer, defaultBooleanValueProperty, null, "Default Value", allowedVariables);
        AddField(tokenContainer, defaultTokenValueProperty, null, "Default Value", allowedVariables);

        RefreshTypeContainers();
        root.TrackPropertyValue(statTypeProperty, evt => RefreshTypeContainers());
        return root;

        void RefreshTypeContainers()
        {
            PlayerScalableStatType statType = (PlayerScalableStatType)statTypeProperty.enumValueIndex;
            bool showNumericFields = statType == PlayerScalableStatType.Float ||
                                     statType == PlayerScalableStatType.Integer ||
                                     statType == PlayerScalableStatType.Unsigned;
            bool showBooleanField = statType == PlayerScalableStatType.Boolean;
            bool showTokenField = statType == PlayerScalableStatType.Token;
            numericContainer.style.display = showNumericFields ? DisplayStyle.Flex : DisplayStyle.None;
            booleanContainer.style.display = showBooleanField ? DisplayStyle.Flex : DisplayStyle.None;
            tokenContainer.style.display = showTokenField ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates and adds one reusable editor field to the destination container.
    /// </summary>
    /// <param name="parent">Destination container.</param>
    /// <param name="property">Serialized property to render.</param>
    /// <param name="scalingRulesProperty">Optional scaling-rules list used for Add Scaling controls.</param>
    /// <param name="label">Field label override.</param>
    /// <param name="allowedVariables">Allowed scalable stat variables for formula validation.</param>
    /// <returns>Void.</returns>
    private static void AddField(VisualElement parent,
                                 SerializedProperty property,
                                 SerializedProperty scalingRulesProperty,
                                 string label,
                                 ISet<string> allowedVariables)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRulesProperty,
                                                                           label,
                                                                           allowedVariables);
        parent.Add(field);
    }
    #endregion

    #endregion
}
