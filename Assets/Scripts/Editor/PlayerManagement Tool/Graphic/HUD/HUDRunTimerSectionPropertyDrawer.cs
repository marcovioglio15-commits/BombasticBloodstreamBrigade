using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws the run timer HUD section and conditionally exposes countdown-only settings.
/// none.
/// </summary>
[CustomPropertyDrawer(typeof(HUDRunTimerSection))]
public sealed class HUDRunTimerSectionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for the run timer HUD section.
    /// </summary>
    /// <param name="property">Serialized run timer HUD section property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        if (HUDSectionPropertyDrawerUtility.TryCreateObjectReferenceField(property,
                                                                         "Scene HUD run timer section component referenced by HUDManager.",
                                                                         out VisualElement referenceField))
            return referenceField;

        VisualElement root = new VisualElement();
        SerializedProperty isEnabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty timerTextProperty = property.FindPropertyRelative("timerText");
        SerializedProperty directionProperty = property.FindPropertyRelative("direction");
        SerializedProperty initialSecondsProperty = property.FindPropertyRelative("initialSeconds");
        SerializedProperty hideWhenPlayerMissingProperty = property.FindPropertyRelative("hideWhenPlayerMissing");

        if (isEnabledProperty == null ||
            timerTextProperty == null ||
            directionProperty == null ||
            initialSecondsProperty == null ||
            hideWhenPlayerMissingProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Run timer HUD section fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        HelpBox bindingInfoBox = new HelpBox("Assign a dedicated TMP text in the main HUD canvas. Backward mode exposes the authored starting value and triggers a defeat at 00:00.", HelpBoxMessageType.Info);
        root.Add(bindingInfoBox);

        PropertyField isEnabledField = CreateBoundField(isEnabledProperty, "Enabled");
        PropertyField timerTextField = CreateBoundField(timerTextProperty, "Timer Text");
        PropertyField directionField = CreateBoundField(directionProperty, "Direction");
        PropertyField hideWhenPlayerMissingField = CreateBoundField(hideWhenPlayerMissingProperty, "Hide When Player Missing");
        root.Add(isEnabledField);
        root.Add(timerTextField);
        root.Add(directionField);

        VisualElement initialSecondsContainer = new VisualElement();
        initialSecondsContainer.style.marginLeft = 12f;
        initialSecondsContainer.Add(CreateBoundField(initialSecondsProperty, "Initial Seconds"));
        root.Add(initialSecondsContainer);
        root.Add(hideWhenPlayerMissingField);

        HelpBox timerReferenceWarningBox = new HelpBox("Timer Text should be assigned, otherwise the timer still runs authoritatively but cannot be rendered by HUDManager.", HelpBoxMessageType.Warning);
        root.Add(timerReferenceWarningBox);

        void RefreshUi()
        {
            bool isCountdown = ResolveDirection(directionProperty) == PlayerRunTimerDirection.Backward;
            bool hasTimerText = timerTextProperty.objectReferenceValue != null;
            initialSecondsContainer.style.display = isCountdown ? DisplayStyle.Flex : DisplayStyle.None;
            timerReferenceWarningBox.style.display = hasTimerText ? DisplayStyle.None : DisplayStyle.Flex;
        }

        timerTextField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshUi());
        directionField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshUi());
        RefreshUi();
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one bound property field with the requested display label.
    /// </summary>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="label">Inspector label shown for the bound field.</param>
    /// <returns>Configured property field bound to the serialized property.</returns>
    private static PropertyField CreateBoundField(SerializedProperty property, string label)
    {
        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        return propertyField;
    }

    /// <summary>
    /// Resolves the serialized timer direction with a safe fallback.
    /// </summary>
    /// <param name="directionProperty">Serialized direction property.</param>
    /// <returns>Resolved timer direction.</returns>
    private static PlayerRunTimerDirection ResolveDirection(SerializedProperty directionProperty)
    {
        if (directionProperty == null || directionProperty.propertyType != SerializedPropertyType.Enum)
            return PlayerRunTimerDirection.Forward;

        if (directionProperty.enumValueIndex == (int)PlayerRunTimerDirection.Backward)
            return PlayerRunTimerDirection.Backward;

        return PlayerRunTimerDirection.Forward;
    }
    #endregion

    #endregion
}
