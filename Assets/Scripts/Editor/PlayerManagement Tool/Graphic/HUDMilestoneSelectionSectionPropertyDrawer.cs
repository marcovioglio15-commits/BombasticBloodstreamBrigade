using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws the milestone HUD selection section with explicit scene-binding guidance for card-based navigation.
/// </summary>
[CustomPropertyDrawer(typeof(HUDMilestoneSelectionSection))]
public sealed class HUDMilestoneSelectionSectionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for the milestone HUD selection section.
    /// </summary>
    /// <param name="property">Serialized milestone HUD section property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty panelRootProperty = property.FindPropertyRelative("panelRoot");
        SerializedProperty headerTextProperty = property.FindPropertyRelative("headerText");
        SerializedProperty skipButtonProperty = property.FindPropertyRelative("skipButton");
        SerializedProperty autoDiscoverOptionViewsProperty = property.FindPropertyRelative("autoDiscoverOptionViewsFromPanelRoot");
        SerializedProperty navigationInputDeadzoneProperty = property.FindPropertyRelative("navigationInputDeadzone");
        SerializedProperty navigationRepeatCooldownSecondsProperty = property.FindPropertyRelative("navigationRepeatCooldownSeconds");
        SerializedProperty wrapNavigationProperty = property.FindPropertyRelative("wrapNavigation");
        SerializedProperty followPointerHoverSelectionProperty = property.FindPropertyRelative("followPointerHoverSelection");
        SerializedProperty suspendEventSystemNavigationProperty = property.FindPropertyRelative("suspendEventSystemNavigationWhileSelectionActive");
        SerializedProperty autoSelectFallbackProperty = property.FindPropertyRelative("autoSelectFirstOfferWhenUiMissing");
        SerializedProperty lockButtonsAfterSelectionClickProperty = property.FindPropertyRelative("lockButtonsAfterSelectionClick");

        if (!AreRequiredPropertiesValid(panelRootProperty,
                                        headerTextProperty,
                                        skipButtonProperty,
                                        autoDiscoverOptionViewsProperty,
                                        navigationInputDeadzoneProperty,
                                        navigationRepeatCooldownSecondsProperty,
                                        wrapNavigationProperty,
                                        followPointerHoverSelectionProperty,
                                        suspendEventSystemNavigationProperty,
                                        autoSelectFallbackProperty,
                                        lockButtonsAfterSelectionClickProperty))
        {
            HelpBox missingHelpBox = new HelpBox("Milestone HUD selection fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        HelpBox bindingInfoBox = new HelpBox("Scene binding for the card-based milestone menu: assign Panel Root to the PowerUpsPanel instance, Header Text to LevelUpTitle, and Skip Button to SkipButton. Offer cards are auto-discovered under PowerUpList from the existing PowerUp prefab.", HelpBoxMessageType.Info);
        root.Add(bindingInfoBox);

        PropertyField panelRootField = CreateBoundField(panelRootProperty, "Panel Root");
        PropertyField headerTextField = CreateBoundField(headerTextProperty, "Header Text");
        PropertyField skipButtonField = CreateBoundField(skipButtonProperty, "Skip Button");
        root.Add(panelRootField);
        root.Add(headerTextField);
        root.Add(skipButtonField);

        HelpBox requiredReferencesWarningBox = new HelpBox("Panel Root and Header Text should be assigned. Skip Button is strongly recommended because Cancel/skip input depends on it.", HelpBoxMessageType.Warning);
        root.Add(requiredReferencesWarningBox);

        PropertyField autoDiscoverOptionViewsField = CreateBoundField(autoDiscoverOptionViewsProperty, "Auto Discover Option Views");
        root.Add(autoDiscoverOptionViewsField);

        HelpBox autoDiscoveryWarningBox = new HelpBox("Auto Discover Option Views should stay enabled for the current milestone menu. Disable it only if you intentionally want the fallback auto-pick flow with no offer cards.", HelpBoxMessageType.Warning);
        root.Add(autoDiscoveryWarningBox);

        Foldout navigationFoldout = new Foldout
        {
            text = "Navigation Settings",
            value = true
        };
        navigationFoldout.Add(CreateBoundField(navigationInputDeadzoneProperty, "Navigation Deadzone"));
        navigationFoldout.Add(CreateBoundField(navigationRepeatCooldownSecondsProperty, "Navigation Repeat Cooldown"));
        navigationFoldout.Add(CreateBoundField(wrapNavigationProperty, "Wrap Navigation"));
        navigationFoldout.Add(CreateBoundField(followPointerHoverSelectionProperty, "Follow Pointer Hover Selection"));
        navigationFoldout.Add(CreateBoundField(suspendEventSystemNavigationProperty, "Suspend EventSystem Navigation"));
        navigationFoldout.Add(CreateBoundField(lockButtonsAfterSelectionClickProperty, "Lock Inputs After Command"));
        navigationFoldout.Add(CreateBoundField(autoSelectFallbackProperty, "Auto Select Fallback"));
        root.Add(navigationFoldout);

        void RefreshWarnings()
        {
            bool hasPanelRoot = panelRootProperty.objectReferenceValue != null;
            bool hasHeaderText = headerTextProperty.objectReferenceValue != null;
            bool hasSkipButton = skipButtonProperty.objectReferenceValue != null;
            bool autoDiscoverOptionViews = autoDiscoverOptionViewsProperty.boolValue;
            requiredReferencesWarningBox.style.display = hasPanelRoot && hasHeaderText && hasSkipButton
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            autoDiscoveryWarningBox.style.display = autoDiscoverOptionViews
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        panelRootField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshWarnings());
        headerTextField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshWarnings());
        skipButtonField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshWarnings());
        autoDiscoverOptionViewsField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshWarnings());
        RefreshWarnings();
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Returns whether all serialized fields required by the custom drawer are available.
    /// </summary>
    /// <param name="panelRootProperty">Serialized panel root property.</param>
    /// <param name="headerTextProperty">Serialized header text property.</param>
    /// <param name="skipButtonProperty">Serialized skip button property.</param>
    /// <param name="autoDiscoverOptionViewsProperty">Serialized auto-discovery toggle property.</param>
    /// <param name="navigationInputDeadzoneProperty">Serialized navigation deadzone property.</param>
    /// <param name="navigationRepeatCooldownSecondsProperty">Serialized navigation repeat cooldown property.</param>
    /// <param name="wrapNavigationProperty">Serialized wrap navigation property.</param>
    /// <param name="followPointerHoverSelectionProperty">Serialized hover-selection property.</param>
    /// <param name="suspendEventSystemNavigationProperty">Serialized EventSystem navigation suppression property.</param>
    /// <param name="autoSelectFallbackProperty">Serialized auto-select fallback property.</param>
    /// <param name="lockButtonsAfterSelectionClickProperty">Serialized post-command input lock property.</param>
    /// <returns>True when every required serialized field exists; otherwise false.</returns>
    private static bool AreRequiredPropertiesValid(SerializedProperty panelRootProperty,
                                                   SerializedProperty headerTextProperty,
                                                   SerializedProperty skipButtonProperty,
                                                   SerializedProperty autoDiscoverOptionViewsProperty,
                                                   SerializedProperty navigationInputDeadzoneProperty,
                                                   SerializedProperty navigationRepeatCooldownSecondsProperty,
                                                   SerializedProperty wrapNavigationProperty,
                                                   SerializedProperty followPointerHoverSelectionProperty,
                                                   SerializedProperty suspendEventSystemNavigationProperty,
                                                   SerializedProperty autoSelectFallbackProperty,
                                                   SerializedProperty lockButtonsAfterSelectionClickProperty)
    {
        if (panelRootProperty == null || headerTextProperty == null || skipButtonProperty == null)
            return false;

        if (autoDiscoverOptionViewsProperty == null)
            return false;

        if (navigationInputDeadzoneProperty == null || navigationRepeatCooldownSecondsProperty == null)
            return false;

        if (wrapNavigationProperty == null || followPointerHoverSelectionProperty == null)
            return false;

        if (suspendEventSystemNavigationProperty == null || autoSelectFallbackProperty == null)
            return false;

        if (lockButtonsAfterSelectionClickProperty == null)
            return false;

        return true;
    }

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
    #endregion

    #endregion
}
