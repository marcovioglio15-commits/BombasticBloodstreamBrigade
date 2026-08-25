using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Builds inline summary, Settings navigation, and independently configured menu-button HUD tabs.
/// </summary>
internal static class GameHudManagerSupplementalPanelUtility
{
    #region Methods

    #region Power-Up Summary
    /// <summary>
    /// Builds the inline summary settings as thematic foldout submenus.
    /// </summary>
    /// <param name="root">HUD details root receiving the summary controls.</param>
    /// <param name="serializedObject">Serialized HUD preset containing inline summary settings.</param>
    public static void BuildPowerUpSummarySection(VisualElement root, SerializedObject serializedObject)
    {
        string prefix = "powerUpSummarySettings.";
        Foldout availability = CreateFoldout("Availability", "Controls when the preauthored summary is available.");
        PropertyField enabledField = AddProperty(availability, serializedObject, prefix + "isEnabled", "Enabled");
        AddProperty(availability, serializedObject, prefix + "startsExpanded", "Starts Expanded");
        AddProperty(availability, serializedObject, prefix + "hideWhenPlayerMissing", "Hide When Player Missing");
        root.Add(availability);

        VisualElement enabledOptions = new VisualElement();
        AddLayoutFoldout(enabledOptions, serializedObject, prefix);
        AddMotionAndInputFoldout(enabledOptions, serializedObject, prefix);
        AddGridFoldout(enabledOptions, serializedObject, prefix);
        AddCounterFoldout(enabledOptions, serializedObject, prefix);
        AddTitlesFoldout(enabledOptions, serializedObject, prefix);
        AddSeparatorsFoldout(enabledOptions, serializedObject, prefix);
        AddPanelStyleFoldout(enabledOptions, serializedObject, prefix);
        AddStatisticsFoldout(enabledOptions, serializedObject, prefix);
        root.Add(enabledOptions);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(enabledField,
                                                                    enabledOptions,
                                                                    serializedObject,
                                                                    prefix + "isEnabled",
                                                                    true);
    }

    /// <summary>
    /// Adds panel edge, sizing, spacing, and power-up column order controls.
    /// </summary>
    /// <param name="root">Container receiving the foldout.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="prefix">Serialized summary property prefix.</param>
    private static void AddLayoutFoldout(VisualElement root, SerializedObject serializedObject, string prefix)
    {
        Foldout foldout = CreateFoldout("Panel Layout", "Controls edge anchoring, dimensions, spacing, and active/passive column order.");
        AddProperty(foldout, serializedObject, prefix + "panelSide", "Panel Side");
        AddProperty(foldout, serializedObject, prefix + "powerUpOrder", "Power-Up Order");
        AddProperty(foldout, serializedObject, prefix + "expandedWidth", "Expanded Width");
        AddProperty(foldout, serializedObject, prefix + "collapsedHandleWidth", "Collapsed Handle Width");
        AddProperty(foldout, serializedObject, prefix + "contentPadding", "Content Padding");
        AddProperty(foldout, serializedObject, prefix + "powerUpColumnSpacing", "Power-Up Column Spacing");
        AddProperty(foldout, serializedObject, prefix + "sectionSpacing", "Section Spacing");
        AddProperty(foldout, serializedObject, prefix + "powerUpAreaHeightNormalized", "Power-Up Area Height");
        root.Add(foldout);
    }

    /// <summary>
    /// Adds slide timing and independently optional action or pointer toggle controls.
    /// </summary>
    /// <param name="root">Container receiving the foldout.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="prefix">Serialized summary property prefix.</param>
    private static void AddMotionAndInputFoldout(VisualElement root, SerializedObject serializedObject, string prefix)
    {
        Foldout foldout = CreateFoldout("Panel Motion And Input", "Controls slide feedback and the two independent ways to toggle the panel.");
        AddProperty(foldout, serializedObject, prefix + "slideDurationSeconds", "Slide Duration");
        AddProperty(foldout, serializedObject, prefix + "slideEasing", "Slide Easing");
        AddProperty(foldout, serializedObject, prefix + "useUnscaledTime", "Use Unscaled Time");
        PropertyField inputToggleField = AddProperty(foldout, serializedObject, prefix + "enableInputToggle", "Enable Input Toggle");
        VisualElement inputRoot = new VisualElement();
        AddActionPicker(inputRoot,
                        serializedObject,
                        prefix + "toggleActionId",
                        "Player/PowerUpSummaryToggle",
                        "Toggle Action",
                        "Action used to expand or collapse the summary while gameplay input is active.",
                        InputActionSelectionElement.SelectionMode.PowerUps);
        foldout.Add(inputRoot);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(inputToggleField,
                                                                    inputRoot,
                                                                    serializedObject,
                                                                    prefix + "enableInputToggle",
                                                                    true);
        AddProperty(foldout, serializedObject, prefix + "enableClickToggle", "Enable Click Toggle");
        root.Add(foldout);
    }

    /// <summary>
    /// Adds fixed pool limits and shared icon presentation controls.
    /// </summary>
    /// <param name="root">Container receiving the foldout.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="prefix">Serialized summary property prefix.</param>
    private static void AddGridFoldout(VisualElement root, SerializedObject serializedObject, string prefix)
    {
        Foldout foldout = CreateFoldout("Power-Up Grid", "Controls the preauthored active and passive icon pools.");
        AddProperty(foldout, serializedObject, prefix + "maximumVisibleActivePowerUps", "Maximum Visible Active Power-Ups");
        AddProperty(foldout, serializedObject, prefix + "maximumVisiblePassivePowerUps", "Maximum Visible Passive Power-Ups");
        AddProperty(foldout, serializedObject, prefix + "iconSize", "Icon Size");
        AddProperty(foldout, serializedObject, prefix + "iconSpacing", "Icon Spacing");
        AddProperty(foldout, serializedObject, prefix + "iconTint", "Icon Tint");
        AddProperty(foldout, serializedObject, prefix + "iconBackgroundSprite", "Icon Background Sprite");
        AddProperty(foldout, serializedObject, prefix + "iconBackgroundTint", "Icon Background Tint");
        AddProperty(foldout, serializedObject, prefix + "hideEmptyActiveColumn", "Hide Empty Active Column");
        AddProperty(foldout, serializedObject, prefix + "hideEmptyPassiveColumn", "Hide Empty Passive Column");
        root.Add(foldout);
    }

    /// <summary>
    /// Adds icon collection-count typography and visibility controls.
    /// </summary>
    /// <param name="root">Container receiving the foldout.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="prefix">Serialized summary property prefix.</param>
    private static void AddCounterFoldout(VisualElement root, SerializedObject serializedObject, string prefix)
    {
        Foldout foldout = CreateFoldout("Power-Up Counter", "Controls the collected quantity rendered on each populated icon.");
        AddProperty(foldout, serializedObject, prefix + "counterFont", "Counter Font");
        AddProperty(foldout, serializedObject, prefix + "counterFontSize", "Counter Font Size");
        AddProperty(foldout, serializedObject, prefix + "counterColor", "Counter Color");
        AddProperty(foldout, serializedObject, prefix + "counterPrefix", "Counter Prefix");
        AddProperty(foldout, serializedObject, prefix + "showSingleCollectionCount", "Show Single Collection Count");
        root.Add(foldout);
    }

    /// <summary>
    /// Adds section title text and shared typography controls.
    /// </summary>
    /// <param name="root">Container receiving the foldout.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="prefix">Serialized summary property prefix.</param>
    private static void AddTitlesFoldout(VisualElement root, SerializedObject serializedObject, string prefix)
    {
        Foldout foldout = CreateFoldout("Section Titles", "Controls Active, Passive, and Player Stats headings.");
        AddProperty(foldout, serializedObject, prefix + "activeTitle", "Active Title");
        AddProperty(foldout, serializedObject, prefix + "passiveTitle", "Passive Title");
        AddProperty(foldout, serializedObject, prefix + "statisticsTitle", "Statistics Title");
        AddProperty(foldout, serializedObject, prefix + "titleFont", "Title Font");
        AddProperty(foldout, serializedObject, prefix + "titleFontSize", "Title Font Size");
        AddProperty(foldout, serializedObject, prefix + "titleColor", "Title Color");
        root.Add(foldout);
    }

    /// <summary>
    /// Adds column and statistic separator visibility and style controls.
    /// </summary>
    /// <param name="root">Container receiving the foldout.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="prefix">Serialized summary property prefix.</param>
    private static void AddSeparatorsFoldout(VisualElement root, SerializedObject serializedObject, string prefix)
    {
        Foldout foldout = CreateFoldout("Separators", "Controls the active/passive divider and the player-stat divider.");
        AddProperty(foldout, serializedObject, prefix + "showPowerUpColumnSeparator", "Show Power-Up Column Separator");
        AddProperty(foldout, serializedObject, prefix + "showStatisticsSeparator", "Show Statistics Separator");
        AddProperty(foldout, serializedObject, prefix + "separatorColor", "Separator Color");
        AddProperty(foldout, serializedObject, prefix + "separatorThickness", "Separator Thickness");
        root.Add(foldout);
    }

    /// <summary>
    /// Adds summary background and persistent handle presentation controls.
    /// </summary>
    /// <param name="root">Container receiving the foldout.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="prefix">Serialized summary property prefix.</param>
    private static void AddPanelStyleFoldout(VisualElement root, SerializedObject serializedObject, string prefix)
    {
        Foldout foldout = CreateFoldout("Panel Style", "Controls the panel background and expand/collapse handle.");
        AddProperty(foldout, serializedObject, prefix + "backgroundSprite", "Background Sprite");
        AddProperty(foldout, serializedObject, prefix + "backgroundTint", "Background Tint");
        AddProperty(foldout, serializedObject, prefix + "toggleSprite", "Toggle Sprite");
        AddProperty(foldout, serializedObject, prefix + "toggleTint", "Toggle Tint");
        root.Add(foldout);
    }

    /// <summary>
    /// Adds refresh cadence and the ordered, dropdown-driven player statistic list.
    /// </summary>
    /// <param name="root">Container receiving the foldout.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="prefix">Serialized summary property prefix.</param>
    private static void AddStatisticsFoldout(VisualElement root, SerializedObject serializedObject, string prefix)
    {
        Foldout foldout = CreateFoldout("Player Statistics", "Controls refresh cadence and the ordered rows rendered in the lower panel.");
        AddProperty(foldout, serializedObject, prefix + "statisticRefreshIntervalSeconds", "Refresh Interval");
        AddProperty(foldout, serializedObject, prefix + "statistics", "Displayed Statistics");
        root.Add(foldout);
    }
    #endregion

    #region Settings Navigation
    /// <summary>
    /// Builds direct Settings menu navigation with separate macro-tab and content Input Actions.
    /// </summary>
    /// <param name="root">HUD details root receiving the navigation controls.</param>
    /// <param name="serializedObject">Serialized HUD preset containing navigation settings.</param>
    public static void BuildSettingsNavigationSection(VisualElement root, SerializedObject serializedObject)
    {
        string prefix = "settingsNavigationSettings.";
        Foldout availability = CreateFoldout("Availability", "Enables direct Settings navigation without a virtual mouse.");
        PropertyField enabledField = AddProperty(availability, serializedObject, prefix + "isEnabled", "Enabled");
        root.Add(availability);
        VisualElement enabledOptions = new VisualElement();

        Foldout tabs = CreateFoldout("Macro Tabs", "Configures independent previous and next macro-tab actions.");
        AddProperty(tabs, serializedObject, prefix + "wrapTabs", "Wrap Tabs");
        AddActionPicker(tabs, serializedObject, prefix + "previousTabActionId", "UI/SettingsPreviousTab", "Previous Tab", "Action selecting the previous Settings macro tab.", InputActionSelectionElement.SelectionMode.Generic);
        AddActionPicker(tabs, serializedObject, prefix + "nextTabActionId", "UI/SettingsNextTab", "Next Tab", "Action selecting the next Settings macro tab.", InputActionSelectionElement.SelectionMode.Generic);
        enabledOptions.Add(tabs);

        Foldout content = CreateFoldout("Content Navigation", "Configures vertical movement, horizontal adjustment, submit, and cancel actions.");
        AddActionPicker(content, serializedObject, prefix + "verticalNavigationActionId", "UI/SettingsNavigateVertical", "Vertical Navigation", "One-dimensional action moving between Settings rows.", InputActionSelectionElement.SelectionMode.Generic);
        AddActionPicker(content, serializedObject, prefix + "horizontalNavigationActionId", "UI/SettingsNavigateHorizontal", "Horizontal Navigation", "One-dimensional action moving horizontally or adjusting supported values.", InputActionSelectionElement.SelectionMode.Generic);
        AddActionPicker(content, serializedObject, prefix + "submitActionId", "UI/Submit", "Submit", "Action activating the selected Settings control.", InputActionSelectionElement.SelectionMode.UISubmit);
        AddActionPicker(content, serializedObject, prefix + "cancelActionId", "UI/Cancel", "Cancel", "Action discarding the draft and closing Settings.", InputActionSelectionElement.SelectionMode.UICancel);
        AddProperty(content, serializedObject, prefix + "includeDropdownHeadersInNavigation", "Navigate Dropdown Headers");
        enabledOptions.Add(content);

        Foldout selection = CreateFoldout("Selection Presentation", "Configures distinct unselected and selected states for every navigable Settings option.");
        PropertyField customizeSelectionField = AddProperty(selection, serializedObject, prefix + "customizeSelectionPresentation", "Customize Selection Presentation");
        VisualElement selectionOptions = new VisualElement();

        PropertyField graphicOverrideField = AddProperty(selectionOptions, serializedObject, prefix + "overrideSelectionGraphicColors", "Override Graphic Colors");
        VisualElement graphicOptions = new VisualElement();
        AddProperty(graphicOptions, serializedObject, prefix + "unselectedGraphicColor", "Unselected Graphic Color");
        AddProperty(graphicOptions, serializedObject, prefix + "selectedGraphicColor", "Selected Graphic Color");
        selectionOptions.Add(graphicOptions);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(graphicOverrideField,
                                                                    graphicOptions,
                                                                    serializedObject,
                                                                    prefix + "overrideSelectionGraphicColors",
                                                                    true);

        PropertyField textOverrideField = AddProperty(selectionOptions, serializedObject, prefix + "overrideSelectionTextStyle", "Override Text Style");
        VisualElement textOptions = new VisualElement();
        AddProperty(textOptions, serializedObject, prefix + "unselectedTextColor", "Unselected Text Color");
        AddProperty(textOptions, serializedObject, prefix + "selectedTextColor", "Selected Text Color");
        AddProperty(textOptions, serializedObject, prefix + "unselectedFontStyle", "Unselected Font Style");
        AddProperty(textOptions, serializedObject, prefix + "selectedFontStyle", "Selected Font Style");
        selectionOptions.Add(textOptions);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(textOverrideField,
                                                                    textOptions,
                                                                    serializedObject,
                                                                    prefix + "overrideSelectionTextStyle",
                                                                    true);

        PropertyField scaleOverrideField = AddProperty(selectionOptions, serializedObject, prefix + "overrideSelectionScale", "Override Scale");
        VisualElement scaleOptions = new VisualElement();
        AddProperty(scaleOptions, serializedObject, prefix + "unselectedScale", "Unselected Scale");
        AddProperty(scaleOptions, serializedObject, prefix + "selectedScale", "Selected Scale");
        selectionOptions.Add(scaleOptions);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(scaleOverrideField,
                                                                    scaleOptions,
                                                                    serializedObject,
                                                                    prefix + "overrideSelectionScale",
                                                                    true);

        PropertyField outlineField = AddProperty(selectionOptions, serializedObject, prefix + "showSelectionOutline", "Show Selection Outline");
        VisualElement outlineOptions = new VisualElement();
        AddProperty(outlineOptions, serializedObject, prefix + "selectionOutlineColor", "Outline Color");
        AddProperty(outlineOptions, serializedObject, prefix + "selectionOutlineDistance", "Outline Distance");
        selectionOptions.Add(outlineOptions);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(outlineField,
                                                                    outlineOptions,
                                                                    serializedObject,
                                                                    prefix + "showSelectionOutline",
                                                                    true);
        selection.Add(selectionOptions);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(customizeSelectionField,
                                                                    selectionOptions,
                                                                    serializedObject,
                                                                    prefix + "customizeSelectionPresentation",
                                                                    true);
        enabledOptions.Add(selection);

        Foldout repeat = CreateFoldout("Held Input", "Controls deadzone, initial delay, and repeat cadence for held navigation axes.");
        AddProperty(repeat, serializedObject, prefix + "inputDeadzone", "Input Deadzone");
        AddProperty(repeat, serializedObject, prefix + "repeatDelaySeconds", "Repeat Delay");
        AddProperty(repeat, serializedObject, prefix + "repeatIntervalSeconds", "Repeat Interval");
        enabledOptions.Add(repeat);
        root.Add(enabledOptions);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(enabledField,
                                                                    enabledOptions,
                                                                    serializedObject,
                                                                    prefix + "isEnabled",
                                                                    true);
    }
    #endregion

    #region Button Interactions
    /// <summary>
    /// Builds the dedicated per-menu button interaction tab using conditional profile foldouts.
    /// </summary>
    /// <param name="root">HUD details root receiving the settings field.</param>
    /// <param name="serializedObject">Serialized HUD preset containing button settings.</param>
    public static void BuildButtonInteractionSection(VisualElement root, SerializedObject serializedObject)
    {
        AddProperty(root, serializedObject, "buttonInteractionSettings", "Independent Menu Profiles");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds a stable Input Action picker and assigns its named default when the current reference is empty.
    /// </summary>
    /// <param name="root">Container receiving the action foldout.</param>
    /// <param name="serializedObject">Serialized HUD preset storing the selected action ID.</param>
    /// <param name="propertyPath">Serialized string property path.</param>
    /// <param name="defaultActionPath">Default action path resolved from the shared Input Action asset.</param>
    /// <param name="label">Visible action foldout label.</param>
    /// <param name="tooltip">Explanation of the action's runtime use.</param>
    /// <param name="mode">Selector filters matching the expected action control type.</param>
    private static void AddActionPicker(VisualElement root,
                                        SerializedObject serializedObject,
                                        string propertyPath,
                                        string defaultActionPath,
                                        string label,
                                        string tooltip,
                                        InputActionSelectionElement.SelectionMode mode)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            return;

        InputActionAsset inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        InputAction currentAction = inputAsset != null ? inputAsset.FindAction(property.stringValue, false) : null;

        if (currentAction == null && inputAsset != null)
        {
            InputAction defaultAction = inputAsset.FindAction(defaultActionPath, false);

            if (defaultAction != null)
            {
                serializedObject.Update();
                property.stringValue = defaultAction.id.ToString();
                serializedObject.ApplyModifiedProperties();
                GameManagementDraftSession.MarkDirty();
            }
        }

        Foldout foldout = CreateFoldout(label, tooltip);
        InputActionSelectionElement selector = new InputActionSelectionElement(inputAsset, serializedObject, property, mode);
        selector.ActionChanged += GameManagementDraftSession.MarkDirty;
        foldout.Add(selector);
        root.Add(foldout);
    }

    /// <summary>
    /// Adds one bound property while preserving its serialized tooltip.
    /// </summary>
    /// <param name="root">Container receiving the property.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized property path.</param>
    /// <param name="label">Visible property label.</param>
    /// <returns>Created field, or null when the path is unavailable.</returns>
    private static PropertyField AddProperty(VisualElement root,
                                             SerializedObject serializedObject,
                                             string propertyPath,
                                             string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            return null;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.Bind(serializedObject);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt => GameManagementDraftSession.MarkDirty());
        root.Add(field);
        return field;
    }

    /// <summary>
    /// Creates one expanded thematic submenu used by supplemental HUD tabs.
    /// </summary>
    /// <param name="title">Visible submenu title.</param>
    /// <param name="tooltip">Explanation of the grouped controls.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreateFoldout(string title, string tooltip)
    {
        return GameHudManagerPresetsPanelUtility.CreateFoldout(title, tooltip);
    }
    #endregion

    #endregion
}
