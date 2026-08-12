using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Shared list, search and metadata helpers for the HUD Manager presets panel.
/// </summary>
internal static class GameHudManagerPresetsPanelUtility
{
    #region Methods

    #region Search
    /// <summary>
    /// Checks whether one preset matches the current search text.
    /// </summary>
    /// <param name="preset">Preset to inspect.</param>
    /// <param name="searchText">Current search text.</param>
    /// <returns>True when visible in the preset browser.</returns>
    public static bool MatchesSearch(GameHudManagerPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        if (preset == null || string.IsNullOrWhiteSpace(preset.PresetName))
            return false;

        return preset.PresetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #region Display
    /// <summary>
    /// Resolves display text for one HUD Manager preset browser row.
    /// </summary>
    /// <param name="preset">Preset to display.</param>
    /// <returns>Display text for list rows.</returns>
    public static string GetPresetDisplayName(GameHudManagerPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string presetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;

        if (string.IsNullOrWhiteSpace(preset.Version))
            return presetName;

        return presetName + " v. " + preset.Version;
    }
    #endregion

    #region Metadata
    /// <summary>
    /// Updates duplicated preset metadata and optionally regenerates the stable ID.
    /// </summary>
    /// <param name="preset">Preset to update.</param>
    /// <param name="name">New preset name.</param>
    /// <param name="regenerateId">True when a fresh ID should be assigned.</param>
    public static void SynchronizePresetMetadata(GameHudManagerPreset preset, string name, bool regenerateId)
    {
        SerializedObject serializedObject = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedObject.FindProperty("presetName");
        SerializedProperty idProperty = serializedObject.FindProperty("presetId");
        serializedObject.Update();

        if (nameProperty != null)
            nameProperty.stringValue = name;

        if (regenerateId && idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        preset.EnsureInitialized();
        EditorUtility.SetDirty(preset);
    }
    #endregion

    #region Section Builders
    /// <summary>
    /// Builds the Level & Experience preset section split into label, bar, liquid, and piston controls.
    /// </summary>
    /// <param name="section">Section root receiving themed foldouts.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    public static void BuildLevelExperienceSection(VisualElement section, SerializedObject serializedObject)
    {
        Foldout labelFoldout = CreateFoldout("Player Level Label", "Level text visibility while runtime player data is missing.");
        AddProperty(labelFoldout, serializedObject, "levelExperienceSettings.hideLevelTextWhenPlayerMissing", "Hide Level Text When Player Missing");
        section.Add(labelFoldout);

        Foldout experienceBarFoldout = CreateFoldout("Experience Bar", "Legacy experience bar smoothing and fallback visibility.");
        AddProperty(experienceBarFoldout, serializedObject, "levelExperienceSettings.experienceBarSmoothingSeconds", "Smoothing Seconds");
        AddProperty(experienceBarFoldout, serializedObject, "levelExperienceSettings.hideExperienceBarWhenPlayerMissing", "Hide When Player Missing");
        section.Add(experienceBarFoldout);

        Foldout liquidFoldout = CreateFoldout("Liquid Shader", "Legacy experience liquid shader and value-delta motion controls.");
        AddProperty(liquidFoldout, serializedObject, "levelExperienceSettings.enableLegacyExperienceLiquidShader", "Enable Liquid Shader");
        PropertyField valueDeltaMotionField = AddProperty(liquidFoldout, serializedObject, "levelExperienceSettings.enableLegacyExperienceValueDeltaMotion", "Enable Value Delta Motion");

        VisualElement deltaMotionOptionsRoot = CreateConditionalOptionsRoot();
        AddProperty(deltaMotionOptionsRoot, serializedObject, "levelExperienceSettings.legacyExperienceDeltaTriggerThreshold", "Delta Trigger Threshold");
        AddProperty(deltaMotionOptionsRoot, serializedObject, "levelExperienceSettings.legacyExperienceDeltaMotionStrength", "Delta Motion Strength");
        AddProperty(deltaMotionOptionsRoot, serializedObject, "levelExperienceSettings.legacyExperienceDeltaMotionDecaySeconds", "Delta Motion Decay Seconds");
        liquidFoldout.Add(deltaMotionOptionsRoot);
        TrackConditionalVisibility(valueDeltaMotionField,
                                   deltaMotionOptionsRoot,
                                   serializedObject,
                                   "levelExperienceSettings.enableLegacyExperienceValueDeltaMotion",
                                   true);

        section.Add(liquidFoldout);

        Foldout pistonFoldout = CreateFoldout("Piston", "Optional legacy experience plunger offsets used when a scene reference is assigned.");
        PropertyField pistonEnabledField = AddProperty(pistonFoldout, serializedObject, "levelExperienceSettings.enableLegacyExperiencePiston", "Enable Piston");

        VisualElement pistonOptionsRoot = CreateConditionalOptionsRoot();
        AddProperty(pistonOptionsRoot, serializedObject, "levelExperienceSettings.legacyExperiencePistonLocalOffsetX", "Local Offset X");
        AddProperty(pistonOptionsRoot, serializedObject, "levelExperienceSettings.legacyExperiencePistonLocalOffsetY", "Local Offset Y");
        pistonFoldout.Add(pistonOptionsRoot);
        TrackConditionalVisibility(pistonEnabledField,
                                   pistonOptionsRoot,
                                   serializedObject,
                                   "levelExperienceSettings.enableLegacyExperiencePiston",
                                   false);

        section.Add(pistonFoldout);
    }

    /// <summary>
    /// Builds the Active Power-Ups preset section split by energy and charge presentation.
    /// </summary>
    /// <param name="section">Section root receiving themed foldouts.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    public static void BuildActivePowerUpsSection(VisualElement section, SerializedObject serializedObject)
    {
        Foldout energyFoldout = CreateFoldout("Energy Bars", "Energy bar smoothing and fallback visibility for active power-up slots.");
        AddProperty(energyFoldout, serializedObject, "activePowerUpSettings.energyBarSmoothingSeconds", "Smoothing Seconds");
        AddProperty(energyFoldout, serializedObject, "activePowerUpSettings.hideEnergyBarsWhenPlayerMissing", "Hide When Player Missing");
        AddProperty(energyFoldout, serializedObject, "activePowerUpSettings.hideEnergyBarsWhenModuleMissing", "Hide When Module Missing");
        section.Add(energyFoldout);

        Foldout chargeFoldout = CreateFoldout("Charge Bars", "Charge bar smoothing and fallback visibility for active power-up slots.");
        AddProperty(chargeFoldout, serializedObject, "activePowerUpSettings.chargeBarSmoothingSeconds", "Smoothing Seconds");
        AddProperty(chargeFoldout, serializedObject, "activePowerUpSettings.hideChargeBarsWhenPlayerMissing", "Hide When Player Missing");
        AddProperty(chargeFoldout, serializedObject, "activePowerUpSettings.hideChargeBarsWhenModuleMissing", "Hide When Module Missing");
        section.Add(chargeFoldout);
    }

    /// <summary>
    /// Builds the Run Timer preset section with countdown-only controls hidden until relevant.
    /// </summary>
    /// <param name="section">Section root receiving property fields.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    public static void BuildRunTimerSection(VisualElement section, SerializedObject serializedObject)
    {
        Foldout activationFoldout = CreateFoldout("Activation", "Master run timer toggle.");
        PropertyField enabledField = AddProperty(activationFoldout, serializedObject, "runTimerSettings.isEnabled", "Enabled");
        section.Add(activationFoldout);

        VisualElement timerOptionsRoot = CreateConditionalOptionsRoot();
        Foldout clockFoldout = CreateFoldout("Clock", "Timer direction and countdown start value.");
        PropertyField directionField = AddProperty(clockFoldout, serializedObject, "runTimerSettings.direction", "Direction");
        VisualElement countdownOptionsRoot = CreateConditionalOptionsRoot();
        AddProperty(countdownOptionsRoot, serializedObject, "runTimerSettings.initialSeconds", "Initial Seconds");
        clockFoldout.Add(countdownOptionsRoot);
        TrackRunTimerDirectionVisibility(directionField, countdownOptionsRoot, serializedObject, "runTimerSettings.direction");
        timerOptionsRoot.Add(clockFoldout);

        Foldout visibilityFoldout = CreateFoldout("Visibility", "Fallback visibility while no runtime player entity is available.");
        AddProperty(visibilityFoldout, serializedObject, "runTimerSettings.hideWhenPlayerMissing", "Hide When Player Missing");
        timerOptionsRoot.Add(visibilityFoldout);
        section.Add(timerOptionsRoot);
        TrackConditionalVisibility(enabledField, timerOptionsRoot, serializedObject, "runTimerSettings.isEnabled", true);
    }

    /// <summary>
    /// Builds the Synchro Meter preset section with wave and visibility controls hidden when the section is disabled.
    /// </summary>
    /// <param name="section">Section root receiving property fields.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    public static void BuildSynchroMeterSection(VisualElement section, SerializedObject serializedObject)
    {
        GameHudManagerSynchroMeterPanelUtility.Build(section, serializedObject);
    }

    /// <summary>
    /// Builds the Milestone Selection preset section with named skip-fill lookup hidden when automatic image configuration is disabled.
    /// </summary>
    /// <param name="section">Section root receiving property fields.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    public static void BuildMilestoneSelectionSection(VisualElement section, SerializedObject serializedObject)
    {
        Foldout optionsFoldout = CreateFoldout("Option Cards", "Generated option-card title formatting.");
        AddProperty(optionsFoldout, serializedObject, "milestoneSelectionSettings.hideOptionTitleNumbers", "Hide Option Title Numbers");
        section.Add(optionsFoldout);

        Foldout skipFillFoldout = CreateFoldout("Skip Hold Fill", "Skip hold-confirmation fill image lookup and runtime configuration.");
        PropertyField configureFillField = AddProperty(skipFillFoldout, serializedObject, "milestoneSelectionSettings.configureSkipHoldFillImage", "Configure Skip Hold Fill Image");

        VisualElement fillImageOptionsRoot = CreateConditionalOptionsRoot();
        AddProperty(fillImageOptionsRoot, serializedObject, "milestoneSelectionSettings.skipHoldFillImageName", "Skip Hold Fill Image Name");
        skipFillFoldout.Add(fillImageOptionsRoot);
        TrackConditionalVisibility(configureFillField,
                                   fillImageOptionsRoot,
                                   serializedObject,
                                   "milestoneSelectionSettings.configureSkipHoldFillImage",
                                   true);

        section.Add(skipFillFoldout);

        Foldout discoveryFoldout = CreateFoldout("Card Discovery", "Automatic discovery of milestone card views under the panel root.");
        AddProperty(discoveryFoldout, serializedObject, "milestoneSelectionSettings.autoDiscoverOptionViewsFromPanelRoot", "Auto Discover Option Views From Panel Root");
        section.Add(discoveryFoldout);

        Foldout navigationFoldout = CreateFoldout("Navigation", "Custom keyboard and gamepad navigation tuning.");
        AddProperty(navigationFoldout, serializedObject, "milestoneSelectionSettings.navigationInputDeadzone", "Navigation Input Deadzone");
        AddProperty(navigationFoldout, serializedObject, "milestoneSelectionSettings.navigationRepeatCooldownSeconds", "Navigation Repeat Cooldown Seconds");
        AddProperty(navigationFoldout, serializedObject, "milestoneSelectionSettings.wrapNavigation", "Wrap Navigation");
        section.Add(navigationFoldout);

        Foldout eventSystemFoldout = CreateFoldout("Pointer And EventSystem", "Pointer hover selection and EventSystem suspension behavior.");
        AddProperty(eventSystemFoldout, serializedObject, "milestoneSelectionSettings.followPointerHoverSelection", "Follow Pointer Hover Selection");
        AddProperty(eventSystemFoldout, serializedObject, "milestoneSelectionSettings.suspendEventSystemNavigationWhileSelectionActive", "Suspend EventSystem Navigation While Selection Active");
        section.Add(eventSystemFoldout);

        Foldout behaviorFoldout = CreateFoldout("Selection Behavior", "Fallback auto-selection and post-command interaction locking.");
        AddProperty(behaviorFoldout, serializedObject, "milestoneSelectionSettings.autoSelectFirstOfferWhenUiMissing", "Auto Select First Offer When UI Missing");
        AddProperty(behaviorFoldout, serializedObject, "milestoneSelectionSettings.lockButtonsAfterSelectionClick", "Lock Buttons After Selection Click");
        section.Add(behaviorFoldout);
    }

    /// <summary>
    /// Builds the Damage Vignette preset section with missing-player behavior hidden when the section is disabled.
    /// </summary>
    /// <param name="section">Section root receiving property fields.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    public static void BuildDamageVignetteSection(VisualElement section, SerializedObject serializedObject)
    {
        Foldout activationFoldout = CreateFoldout("Activation", "Master damage vignette toggle.");
        PropertyField enabledField = AddProperty(activationFoldout, serializedObject, "damageVignetteSettings.isEnabled", "Enabled");
        section.Add(activationFoldout);

        VisualElement vignetteOptionsRoot = CreateConditionalOptionsRoot();
        Foldout visibilityFoldout = CreateFoldout("Visibility", "Fallback visibility while no runtime player entity is available.");
        AddProperty(visibilityFoldout, serializedObject, "damageVignetteSettings.hideWhenPlayerMissing", "Hide When Player Missing");
        vignetteOptionsRoot.Add(visibilityFoldout);
        section.Add(vignetteOptionsRoot);
        TrackConditionalVisibility(enabledField, vignetteOptionsRoot, serializedObject, "damageVignetteSettings.isEnabled", true);
    }
    #endregion

    #region Property Fields
    /// <summary>
    /// Adds every direct child property in a serialized settings object.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="rootProperty">Root settings property.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    public static void AddChildProperties(VisualElement parent, SerializedProperty rootProperty, SerializedObject serializedObject)
    {
        SerializedProperty iterator = rootProperty.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            if (SerializedProperty.EqualContents(iterator, endProperty))
                break;

            enterChildren = false;
            PropertyField field = new PropertyField(iterator.Copy());
            field.Bind(serializedObject);
            parent.Add(field);
        }
    }

    /// <summary>
    /// Adds one serialized property field when the property path exists.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized property path.</param>
    /// <param name="label">Displayed field label.</param>
    public static PropertyField AddProperty(VisualElement parent, SerializedObject serializedObject, string propertyPath, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            return null;

        PropertyField field = new PropertyField(property, label);
        field.Bind(serializedObject);
        parent.Add(field);
        return field;
    }
    #endregion

    #region Foldouts
    /// <summary>
    /// Creates one standard themed foldout used inside HUD Manager preset sections.
    /// </summary>
    /// <param name="title">Group title shown in the tool.</param>
    /// <param name="tooltip">Tooltip explaining the grouped fields.</param>
    /// <returns>Configured foldout.</returns>
    internal static Foldout CreateFoldout(string title, string tooltip)
    {
        Foldout foldout = new Foldout();
        foldout.text = title;
        foldout.tooltip = tooltip;
        foldout.value = true;
        foldout.style.marginTop = 4f;
        foldout.style.marginBottom = 4f;
        return foldout;
    }
    #endregion

    #region Conditional Visibility
    /// <summary>
    /// Creates a child container whose visibility can be refreshed without rebuilding the owning section.
    /// </summary>
    /// <returns>Configured child container.</returns>
    internal static VisualElement CreateConditionalOptionsRoot()
    {
        VisualElement root = new VisualElement();
        root.style.flexDirection = FlexDirection.Column;
        return root;
    }

    /// <summary>
    /// Refreshes one conditional options container from a Boolean serialized property and tracks future edits.
    /// </summary>
    /// <param name="driverField">Field that emits serialized-property changes for the controlling property.</param>
    /// <param name="targetRoot">Container shown or hidden according to the controlling property.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized Boolean property path.</param>
    /// <param name="fallback">Fallback visibility when the controlling property is missing.</param>
    internal static void TrackConditionalVisibility(PropertyField driverField,
                                                    VisualElement targetRoot,
                                                    SerializedObject serializedObject,
                                                    string propertyPath,
                                                    bool fallback)
    {
        RefreshConditionalVisibility(targetRoot, serializedObject, propertyPath, fallback);

        if (driverField == null)
            return;

        driverField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            RefreshConditionalVisibilityFromEvent(targetRoot, evt, serializedObject, propertyPath, fallback));
    }

    /// <summary>
    /// Refreshes one container from a Boolean serialized property without mutating the asset.
    /// </summary>
    /// <param name="targetRoot">Container shown or hidden according to the property value.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized Boolean property path.</param>
    /// <param name="fallback">Fallback visibility when the property is missing.</param>
    private static void RefreshConditionalVisibility(VisualElement targetRoot,
                                                     SerializedObject serializedObject,
                                                     string propertyPath,
                                                     bool fallback)
    {
        if (targetRoot == null)
            return;

        targetRoot.style.display = ReadBool(serializedObject, propertyPath, fallback)
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Refreshes one Boolean-driven container from the current property event when available.
    /// </summary>
    /// <param name="targetRoot">Container shown or hidden according to the property value.</param>
    /// <param name="evt">Property change event emitted by the controlling field.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized Boolean property path used as fallback lookup.</param>
    /// <param name="fallback">Fallback visibility when the property is missing.</param>
    private static void RefreshConditionalVisibilityFromEvent(VisualElement targetRoot,
                                                              SerializedPropertyChangeEvent evt,
                                                              SerializedObject serializedObject,
                                                              string propertyPath,
                                                              bool fallback)
    {
        if (targetRoot == null)
            return;

        SerializedProperty changedProperty = evt != null ? evt.changedProperty : null;

        if (changedProperty != null && changedProperty.propertyType == SerializedPropertyType.Boolean)
        {
            targetRoot.style.display = changedProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            return;
        }

        RefreshConditionalVisibility(targetRoot, serializedObject, propertyPath, fallback);
    }

    /// <summary>
    /// Refreshes countdown-only timer options from the Run Timer direction and tracks future edits.
    /// </summary>
    /// <param name="driverField">Field that emits serialized-property changes for timer direction.</param>
    /// <param name="targetRoot">Container shown only when the timer counts backward.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized timer direction property path.</param>
    private static void TrackRunTimerDirectionVisibility(PropertyField driverField,
                                                         VisualElement targetRoot,
                                                         SerializedObject serializedObject,
                                                         string propertyPath)
    {
        RefreshRunTimerDirectionVisibility(targetRoot, serializedObject, propertyPath);

        if (driverField == null)
            return;

        driverField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            RefreshRunTimerDirectionVisibilityFromEvent(targetRoot, evt, serializedObject, propertyPath);
        });
    }

    /// <summary>
    /// Refreshes countdown-only timer options without rebuilding the active HUD section.
    /// </summary>
    /// <param name="targetRoot">Container shown only when the timer counts backward.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized timer direction property path.</param>
    private static void RefreshRunTimerDirectionVisibility(VisualElement targetRoot,
                                                           SerializedObject serializedObject,
                                                           string propertyPath)
    {
        if (targetRoot == null)
            return;

        targetRoot.style.display = ReadRunTimerDirection(serializedObject, propertyPath) == PlayerRunTimerDirection.Backward
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Refreshes countdown-only timer options from the current enum property event when available.
    /// </summary>
    /// <param name="targetRoot">Container shown only when the timer counts backward.</param>
    /// <param name="evt">Property change event emitted by the timer direction field.</param>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized timer direction property path used as fallback lookup.</param>
    private static void RefreshRunTimerDirectionVisibilityFromEvent(VisualElement targetRoot,
                                                                    SerializedPropertyChangeEvent evt,
                                                                    SerializedObject serializedObject,
                                                                    string propertyPath)
    {
        if (targetRoot == null)
            return;

        SerializedProperty changedProperty = evt != null ? evt.changedProperty : null;

        if (changedProperty != null && changedProperty.propertyType == SerializedPropertyType.Enum)
        {
            targetRoot.style.display = changedProperty.enumValueIndex == (int)PlayerRunTimerDirection.Backward
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            return;
        }

        RefreshRunTimerDirectionVisibility(targetRoot, serializedObject, propertyPath);
    }
    #endregion

    #region Readers
    /// <summary>
    /// Reads one Boolean serialized property with a fallback used while the property is missing during asset migrations.
    /// </summary>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized property path.</param>
    /// <param name="fallback">Fallback value used when the property is missing.</param>
    /// <returns>Serialized Boolean value, or fallback when unresolved.</returns>
    private static bool ReadBool(SerializedObject serializedObject, string propertyPath, bool fallback)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            return fallback;

        return property.boolValue;
    }

    /// <summary>
    /// Reads the Run Timer direction enum from a serialized property.
    /// </summary>
    /// <param name="serializedObject">Serialized HUD preset being edited.</param>
    /// <param name="propertyPath">Serialized direction property path.</param>
    /// <returns>Resolved run timer direction.</returns>
    private static PlayerRunTimerDirection ReadRunTimerDirection(SerializedObject serializedObject, string propertyPath)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            return PlayerRunTimerDirection.Forward;

        if (property.enumValueIndex == (int)PlayerRunTimerDirection.Backward)
            return PlayerRunTimerDirection.Backward;

        return PlayerRunTimerDirection.Forward;
    }
    #endregion

    #endregion
}

/// <summary>
/// Detail sections shown for a HUD Manager preset.
/// </summary>
internal enum DetailsSectionType
{
    Metadata = 0,
    LevelExperience = 1,
    ActivePowerUps = 2,
    RunTimer = 3,
    SynchroMeter = 6,
    Milestone = 7,
    Damage = 8,
    Validation = 9
}
