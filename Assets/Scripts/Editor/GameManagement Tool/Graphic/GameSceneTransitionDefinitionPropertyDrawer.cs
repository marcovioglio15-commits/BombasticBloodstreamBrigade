using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit drawer that hides trigger and fade override fields unless the selected transition mode requires them.
/// /params None.
/// /returns None.
/// </summary>
[CustomPropertyDrawer(typeof(GameSceneTransitionDefinition))]
public sealed class GameSceneTransitionDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region UI
    /// <summary>
    /// Builds the transition definition editor UI.
    /// /params property Serialized GameSceneTransitionDefinition property.
    /// /returns Configured visual tree for the property.
    /// </summary>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        root.style.marginBottom = 6f;

        AddProperty(root, property, "transitionId");
        AddProperty(root, property, "fromSceneId");
        AddProperty(root, property, "toSceneId");
        AddProperty(root, property, "priority");
        SerializedProperty modeProperty = property.FindPropertyRelative("transitionMode");
        PropertyField modeField = AddProperty(root, property, "transitionMode");
        VisualElement triggerContainer = BuildTriggerContainer(property);
        VisualElement fadeContainer = BuildFadeContainer(property);
        SerializedProperty overrideFadeProperty = property.FindPropertyRelative("overrideFadeSettings");
        PropertyField overrideFadeField = AddProperty(root, property, "overrideFadeSettings");
        root.Add(triggerContainer);
        root.Add(fadeContainer);
        AddProperty(root, property, "allowDuringPause");
        AddProperty(root, property, "allowWhenRunFinalized");
        RefreshVisibility(modeProperty, overrideFadeProperty, triggerContainer, fadeContainer);

        if (modeField != null)
            modeField.RegisterCallback<SerializedPropertyChangeEvent>(evt => RefreshVisibility(modeProperty, overrideFadeProperty, triggerContainer, fadeContainer));

        if (overrideFadeField != null)
            overrideFadeField.RegisterCallback<SerializedPropertyChangeEvent>(evt => RefreshVisibility(modeProperty, overrideFadeProperty, triggerContainer, fadeContainer));

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one relative property field to the root.
    /// /params root Parent visual element.
    /// /params parentProperty Serialized transition property.
    /// /params propertyName Relative property name.
    /// /returns Created property field, or null when missing.
    /// </summary>
    private static PropertyField AddProperty(VisualElement root, SerializedProperty parentProperty, string propertyName)
    {
        SerializedProperty childProperty = parentProperty.FindPropertyRelative(propertyName);

        if (childProperty == null)
            return null;

        PropertyField field = new PropertyField(childProperty);
        field.tooltip = "Transition field: " + ObjectNames.NicifyVariableName(propertyName) + ".";
        field.BindProperty(childProperty);
        root.Add(field);
        return field;
    }

    /// <summary>
    /// Builds trigger-only transition fields.
    /// /params property Serialized transition property.
    /// /returns Container for trigger-only fields.
    /// </summary>
    private static VisualElement BuildTriggerContainer(SerializedProperty property)
    {
        VisualElement container = new VisualElement();
        AddProperty(container, property, "triggerId");
        AddProperty(container, property, "triggerCooldownOverrideSeconds");
        AddProperty(container, property, "oneShotTrigger");
        return container;
    }

    /// <summary>
    /// Builds fade override fields.
    /// /params property Serialized transition property.
    /// /returns Container for fade override fields.
    /// </summary>
    private static VisualElement BuildFadeContainer(SerializedProperty property)
    {
        VisualElement container = new VisualElement();
        AddProperty(container, property, "fadeOutSeconds");
        AddProperty(container, property, "holdBlackSeconds");
        AddProperty(container, property, "fadeInSeconds");
        return container;
    }

    /// <summary>
    /// Applies dependent visibility for trigger and fade override options.
    /// /params modeProperty Transition mode property.
    /// /params overrideFadeProperty Override fade toggle property.
    /// /params triggerContainer Trigger-only field container.
    /// /params fadeContainer Fade override field container.
    /// /returns None.
    /// </summary>
    private static void RefreshVisibility(SerializedProperty modeProperty,
                                          SerializedProperty overrideFadeProperty,
                                          VisualElement triggerContainer,
                                          VisualElement fadeContainer)
    {
        bool showTrigger = modeProperty != null &&
                           (GameSceneTransitionMode)modeProperty.enumValueIndex == GameSceneTransitionMode.TriggerVolume;
        bool showFade = overrideFadeProperty != null && overrideFadeProperty.boolValue;
        triggerContainer.style.display = showTrigger ? DisplayStyle.Flex : DisplayStyle.None;
        fadeContainer.style.display = showFade ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
