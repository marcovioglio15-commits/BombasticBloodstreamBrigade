using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds detail sections and subsection tabs for enemy visual preset panels.
/// </summary>
internal static class EnemyVisualPresetsPanelSectionsUtility
{
    #region Constants
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    public static void BuildMetadataSection(EnemyVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Preset Details");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");
        SerializedProperty nameProperty = presetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = presetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = presetSerializedObject.FindProperty("version");

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            panel.HandlePresetNameChanged(evt.newValue);
        });
        sectionContainer.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(descriptionField);

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Preset ID");
        idField.isReadOnly = true;
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.BindProperty(idProperty);
        idRow.Add(idField);

        Button regenerateButton = new Button(panel.RegeneratePresetId);
        regenerateButton.text = "Regenerate";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);
        sectionContainer.Add(idRow);
    }

    public static void BuildVisualSection(EnemyVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Visual");

        if (sectionContainer == null)
            return;

        panel.VisualSubSectionTabs.Clear();

        VisualElement tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.flexWrap = Wrap.Wrap;
        tabBar.style.marginBottom = 6f;
        tabBar.style.paddingTop = 4f;
        tabBar.style.paddingBottom = 4f;
        tabBar.style.paddingLeft = 2f;
        panel.VisualSubSectionTabBar = tabBar;

        VisualElement contentHost = new VisualElement();
        contentHost.style.flexDirection = FlexDirection.Column;
        contentHost.style.flexGrow = 1f;
        panel.VisualSubSectionContentHost = contentHost;

        sectionContainer.Add(tabBar);
        sectionContainer.Add(contentHost);

        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.Visibility,
                               "Visibility",
                               BuildVisibilitySubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.Outline,
                               "Outline",
                               BuildOutlineSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.Footprint,
                               "Footprint UI",
                               EnemyVisualPresetsPanelFootprintSectionUtility.BuildFootprintSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.DamageFeedback,
                               "Damage Feedback",
                               BuildDamageFeedbackSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.OffensiveEngagementFeedback,
                               "Offensive Engagement Feedback",
                               BuildOffensiveEngagementFeedbackSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.BossPatternChangeFeedback,
                               "Pattern Change Feedback",
                               BuildBossPatternChangeFeedbackSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.Prefabs,
                               "Prefabs & VFX",
                               EnemyVisualPresetsPanelPrefabsSectionUtility.BuildPrefabsSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.SpawnOverrides,
                               "Spawn Overrides",
                               BuildSpawnOverridesSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.BossUi,
                               "Boss UI",
                               BuildBossUiSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.ProjectileOffscreenWarning,
                               "Projectile Warnings",
                               BuildProjectileOffscreenWarningSubSection(panel));

        if (!panel.VisualSubSectionTabs.ContainsKey(panel.ActiveVisualSubSection))
            panel.ActiveVisualSubSection = EnemyVisualPresetsPanel.VisualSubSectionType.Visibility;

        panel.SetActiveVisualSubSection(panel.ActiveVisualSubSection);
    }

    public static void ShowActiveVisualSubSection(EnemyVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement contentHost = panel.VisualSubSectionContentHost;

        if (contentHost == null)
            return;

        EnemyVisualPresetsPanel.VisualSubSectionTabEntry tabEntry;

        if (!panel.VisualSubSectionTabs.TryGetValue(panel.ActiveVisualSubSection, out tabEntry))
            return;

        if (tabEntry == null || tabEntry.Content == null)
            return;

        contentHost.Clear();
        contentHost.Add(tabEntry.Content);
        UpdateVisualSubSectionTabStyles(panel);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(contentHost);
    }
    #endregion

    #region Private Methods
    private static VisualElement CreateDetailsSectionContainer(EnemyVisualPresetsPanel panel, string sectionTitle)
    {
        if (panel == null)
            return null;

        VisualElement detailsSectionContentRoot = panel.DetailsSectionContentRoot;

        if (detailsSectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.EnemyManagement.Visual.Section." + sectionTitle);
        container.Add(header);
        detailsSectionContentRoot.Add(container);
        return container;
    }

    internal static VisualElement CreateSubSectionContainer(string sectionTitle)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout(sectionTitle,
                                                                           "NashCore.EnemyManagement.Visual.SubSection." + sectionTitle,
                                                                           true);
        foldout.style.marginTop = 4f;
        return foldout;
    }

    internal static void AddPropertyField(EnemyVisualPresetsPanel panel,
                                          VisualElement target,
                                          SerializedProperty parentProperty,
                                          string relativePropertyName,
                                          string label,
                                          string tooltip)
    {
        if (panel == null)
            return;

        if (target == null)
            return;

        if (parentProperty == null)
            return;

        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        propertyField.tooltip = tooltip;
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        target.Add(propertyField);
    }

    private static void AddVisualSubSectionTab(EnemyVisualPresetsPanel panel,
                                               EnemyVisualPresetsPanel.VisualSubSectionType subSectionType,
                                               string tabLabel,
                                               VisualElement content)
    {
        if (panel == null)
            return;

        if (panel.VisualSubSectionTabBar == null)
            return;

        if (content == null)
            return;

        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;
        tabContainer.style.marginBottom = 4f;

        Button tabButton = new Button(() => panel.SetActiveVisualSubSection(subSectionType));
        tabButton.text = tabLabel;
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabContainer.Add(tabButton);
        panel.VisualSubSectionTabBar.Add(tabContainer);

        EnemyVisualPresetsPanel.VisualSubSectionTabEntry tabEntry = new EnemyVisualPresetsPanel.VisualSubSectionTabEntry();
        tabEntry.TabContainer = tabContainer;
        tabEntry.TabButton = tabButton;
        tabEntry.Content = content;
        panel.VisualSubSectionTabs[subSectionType] = tabEntry;
    }

    private static void UpdateVisualSubSectionTabStyles(EnemyVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<EnemyVisualPresetsPanel.VisualSubSectionType, EnemyVisualPresetsPanel.VisualSubSectionTabEntry> tabEntry in panel.VisualSubSectionTabs)
        {
            if (tabEntry.Value == null || tabEntry.Value.TabButton == null)
                continue;

            bool isActive = tabEntry.Key == panel.ActiveVisualSubSection;
            tabEntry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            tabEntry.Value.TabButton.style.backgroundColor = isActive ? ActiveTabColor : Color.clear;
        }
    }

    private static VisualElement BuildVisibilitySubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty visibilityProperty = panel.PresetSerializedObject.FindProperty("visibility");
        VisualElement container = CreateSubSectionContainer("Visibility");
        AddPropertyField(panel, container, visibilityProperty, "visualMode", "Visual Mode", "Visual runtime path used by this enemy type.");
        AddPropertyField(panel, container, visibilityProperty, "visualAnimationSpeed", "Visual Animation Speed", "Playback speed multiplier used by both companion and GPU-baked visual paths.");
        AddPropertyField(panel, container, visibilityProperty, "gpuAnimationLoopDuration", "GPU Animation Loop Duration", "Loop duration in seconds used by GPU-baked playback time wrapping.");
        AddPropertyField(panel, container, visibilityProperty, "enableDistanceCulling", "Enable Distance Culling", "Enable distance-based visual culling while gameplay simulation remains fully active.");
        AddPropertyField(panel, container, visibilityProperty, "maxVisibleDistance", "Max Visible Distance", "Maximum planar distance from player where visuals stay visible. Set to 0 to keep always visible.");
        AddPropertyField(panel, container, visibilityProperty, "visibleDistanceHysteresis", "Visible Distance Hysteresis", "Additional distance band used to avoid visual popping when crossing the culling boundary.");
        return container;
    }

    private static VisualElement BuildOutlineSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty outlineProperty = panel.PresetSerializedObject.FindProperty("outline");
        VisualElement container = CreateSubSectionContainer("Outline");
        AddPropertyField(panel, container, outlineProperty, "enableOutline", "Enable Outline", "When enabled, compatible enemy renderers receive outline property overrides from this preset.");
        AddPropertyField(panel, container, outlineProperty, "outlineThickness", "Outline Thickness", "Outline thickness written to compatible enemy materials exposing _OutlineThickness. Enemy presets support stronger values up to 25.");
        AddPropertyField(panel, container, outlineProperty, "outlineColor", "Outline Color", "Outline color written to compatible enemy materials exposing _OutlineColor.");
        return container;
    }

    /// <summary>
    /// Builds the spawn override subsection with dependent offset and warning controls.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset context.</param>
    /// <returns>Spawn override subsection content.</returns>
    private static VisualElement BuildSpawnOverridesSubSection(EnemyVisualPresetsPanel panel)
    {
        // Resolve the optional override block before showing dependent controls.
        SerializedProperty spawnOverridesProperty = panel.PresetSerializedObject.FindProperty("spawnOverrides");
        VisualElement container = CreateSubSectionContainer("Spawn Overrides");

        if (spawnOverridesProperty == null)
            return container;

        SerializedProperty overrideSpawnOffsetProperty = spawnOverridesProperty.FindPropertyRelative("overrideSpawnOffset");
        SerializedProperty overrideSpawnWarningProperty = spawnOverridesProperty.FindPropertyRelative("overrideSpawnWarning");
        SerializedProperty enableSpawnWarningProperty = spawnOverridesProperty.FindPropertyRelative("enableSpawnWarning");

        AddReactiveToggleField(panel, container, overrideSpawnOffsetProperty, "Override Spawn Offset", "When enabled, this enemy type adds its own local-space offset to spawner-authored spawn positions.");

        if (overrideSpawnOffsetProperty != null && overrideSpawnOffsetProperty.boolValue)
            AddPropertyField(panel, container, spawnOverridesProperty, "spawnOffset", "Spawn Offset", "Local-space offset added after spawner grid and cell placement.");

        AddReactiveToggleField(panel, container, overrideSpawnWarningProperty, "Override Spawn Warning", "When enabled, this enemy type replaces the owning spawner warning settings for its own spawn events.");

        if (overrideSpawnWarningProperty == null || !overrideSpawnWarningProperty.boolValue)
            return container;

        AddReactiveToggleField(panel, container, enableSpawnWarningProperty, "Enable Spawn Warning", "Enables warning rings for this enemy type when Spawn Warning override is active.");

        if (enableSpawnWarningProperty != null && !enableSpawnWarningProperty.boolValue)
        {
            container.Add(new HelpBox("Spawn warnings are disabled for this enemy type override.", HelpBoxMessageType.Info));
            return container;
        }

        AddSliderField(panel, container, spawnOverridesProperty.FindPropertyRelative("spawnWarningLeadTimeSeconds"), "Lead Time Seconds", 0f, 3f, "Seconds of anticipation shown before this enemy type becomes active.");
        AddSliderField(panel, container, spawnOverridesProperty.FindPropertyRelative("spawnWarningRadiusScale"), "Radius Scale", 0.1f, 2f, "Ring world radius resolved as Cell Size multiplied by this scale.");
        AddSliderField(panel, container, spawnOverridesProperty.FindPropertyRelative("spawnWarningRingWidth"), "Ring Width", 0.02f, 1f, "World-space line width used by this enemy type warning ring.");
        AddSliderField(panel, container, spawnOverridesProperty.FindPropertyRelative("spawnWarningHeightOffset"), "Height Offset", 0f, 1f, "Extra vertical lift applied to the warning ring above the spawn plane.");
        AddSliderField(panel, container, spawnOverridesProperty.FindPropertyRelative("spawnWarningMaximumAlpha"), "Maximum Alpha", 0f, 1f, "Maximum opacity reached by the warning ring right before spawning.");
        AddSliderField(panel, container, spawnOverridesProperty.FindPropertyRelative("spawnWarningFadeOutSeconds"), "Fade Out Seconds", 0f, 1f, "Seconds used to softly fade the ring after the enemy has spawned.");
        AddPropertyField(panel, container, spawnOverridesProperty, "spawnWarningColor", "Warning Color", "Tint color used by this enemy type spawn warning ring.");
        AddSpawnOverrideWarnings(spawnOverridesProperty, container);
        return container;
    }

    private static VisualElement BuildDamageFeedbackSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty damageFeedbackProperty = panel.PresetSerializedObject.FindProperty("damageFeedback");
        VisualElement container = CreateSubSectionContainer("Damage Feedback");
        AddPropertyField(panel, container, damageFeedbackProperty, "flashColor", "Flash Color", "Tint color applied during the brief damage flash.");
        AddPropertyField(panel, container, damageFeedbackProperty, "flashDurationSeconds", "Flash Duration Seconds", "Flash duration in seconds. Use very small values for a 1-3 frame reaction.");
        AddPropertyField(panel, container, damageFeedbackProperty, "flashMaximumBlend", "Flash Maximum Blend", "Maximum overlay strength reached immediately after a valid hit.");
        container.Add(EnemyVisualPresetsPanelElasticHitSectionUtility.Build(panel,
                                                                            panel.PresetSerializedObject.FindProperty("elasticHit")));
        return container;
    }

    private static VisualElement BuildOffensiveEngagementFeedbackSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty feedbackProperty = panel.PresetSerializedObject.FindProperty("offensiveEngagementFeedback");
        VisualElement container = CreateSubSectionContainer("Offensive Engagement Feedback");
        VisualElement feedbackEditor = EnemyOffensiveEngagementFeedbackDrawerUtility.BuildSettingsEditor(feedbackProperty, () =>
        {
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        container.Add(feedbackEditor);
        return container;
    }

    /// <summary>
    /// Builds the boss pattern-change visual feedback subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <returns>Subsection container with pattern-change feedback controls.</returns>
    private static VisualElement BuildBossPatternChangeFeedbackSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty feedbackProperty = panel.PresetSerializedObject.FindProperty("bossPatternChangeFeedback");
        VisualElement container = CreateSubSectionContainer("Boss Pattern Change Feedback");
        container.Add(new HelpBox("These settings are shown immediately after a boss pattern extraction changes the active pattern. Color and billboard lead-time fields are used as post-extraction display durations.", HelpBoxMessageType.Info));
        VisualElement feedbackEditor = EnemyOffensiveEngagementFeedbackDrawerUtility.BuildSettingsEditor(feedbackProperty, () =>
        {
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        container.Add(feedbackEditor);
        return container;
    }

    private static VisualElement BuildBossUiSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty bossUiProperty = panel.PresetSerializedObject.FindProperty("bossUi");
        VisualElement container = CreateSubSectionContainer("Boss UI");

        if (bossUiProperty == null)
            return container;

        SerializedProperty enabledProperty = bossUiProperty.FindPropertyRelative("enabled");
        SerializedProperty showHealthBarProperty = bossUiProperty.FindPropertyRelative("showHealthBar");
        SerializedProperty showOffscreenIndicatorProperty = bossUiProperty.FindPropertyRelative("showOffscreenIndicator");

        if (enabledProperty == null)
            return container;

        AddReactiveToggleField(panel, container, enabledProperty, "Enabled", "Enables the dedicated boss HUD for enemies using a Boss Pattern Preset.", "Edit Enemy Boss UI Settings");

        if (!enabledProperty.boolValue)
        {
            container.Add(new HelpBox("Boss UI is disabled for this visual preset.", HelpBoxMessageType.Info));
            return container;
        }

        AddReactiveToggleField(panel, container, showHealthBarProperty, "Show Health Bar", "Shows the mirrored boss health and shield bars when the dedicated boss HUD is enabled.", "Edit Enemy Boss UI Settings");
        AddReactiveToggleField(panel, container, showOffscreenIndicatorProperty, "Show Offscreen Indicator", "Shows the screen-edge indicator for an active boss that is outside the camera view.", "Edit Enemy Boss UI Settings");

        bool showHealthBar = showHealthBarProperty == null || showHealthBarProperty.boolValue;
        bool showOffscreenIndicator = showOffscreenIndicatorProperty == null || showOffscreenIndicatorProperty.boolValue;

        if (!showHealthBar && !showOffscreenIndicator)
            container.Add(new HelpBox("Both Boss UI outputs are disabled. Enable at least one output to show boss runtime UI.", HelpBoxMessageType.Warning));

        if (showHealthBar)
        {
            AddSectionLabel(container, "Mirrored Boss Bars");
            AddPropertyField(panel, container, bossUiProperty, "bossDisplayName", "Boss Display Name", "Optional boss name shown near the mirrored top-right boss bars. Empty falls back to the visual preset name.");
            AddSectionLabel(container, "Boss Syringe Bars");
            AddPropertyField(panel, container, bossUiProperty, "syringeBars", "Syringe Bars", "Procedural syringe settings used by the mirrored boss health and shield bars. Health drives boss health; Shield drives boss shield.");
        }

        if (showOffscreenIndicator)
        {
            AddSectionLabel(container, "Offscreen Indicator");
            AddPropertyField(panel, container, bossUiProperty, "offscreenIndicatorSprite", "Offscreen Indicator Sprite", "Sprite used by the off-screen indicator that slides along screen edges.");
            AddPropertyField(panel, container, bossUiProperty, "offscreenIndicatorColor", "Offscreen Indicator Color", "Tint color applied to the off-screen boss indicator.");
            AddSliderField(panel, container, bossUiProperty.FindPropertyRelative("offscreenIndicatorSizePixels"), "Indicator Size", 16f, 192f, "Square size in pixels used by the off-screen boss indicator image.");
            AddSliderField(panel, container, bossUiProperty.FindPropertyRelative("edgePaddingPixels"), "Screen Margin", 0f, 160f, "Extra screen-edge margin in pixels kept outside the off-screen indicator half size.");
            AddNonPositiveValueWarning(bossUiProperty, container, "offscreenIndicatorSizePixels", "Boss offscreen indicator size should be greater than zero.");
            AddNegativeValueWarning(bossUiProperty, container, "edgePaddingPixels", "Boss offscreen indicator screen margin must be zero or positive.");
        }

        return container;
    }

    /// <summary>
    /// Builds projectile offscreen-warning controls with dependent fields shown only while warnings are enabled.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset context.</param>
    /// <returns>Projectile offscreen-warning subsection content.</returns>
    private static VisualElement BuildProjectileOffscreenWarningSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty warningProperty = panel.PresetSerializedObject.FindProperty("projectileOffscreenWarning");
        VisualElement container = CreateSubSectionContainer("Projectile Warnings");

        if (warningProperty == null)
            return container;

        SerializedProperty enabledProperty = warningProperty.FindPropertyRelative("enabled");
        AddReactiveToggleField(panel, container, enabledProperty, "Enabled", "Shows a screen-edge warning for enemy-owned projectiles that are fired while still outside camera view.", "Edit Enemy Projectile Warning Settings");

        if (enabledProperty != null && !enabledProperty.boolValue)
        {
            container.Add(new HelpBox("Projectile offscreen warnings are disabled for this visual preset.", HelpBoxMessageType.Info));
            return container;
        }

        AddPropertyField(panel, container, warningProperty, "indicatorSprite", "Indicator Sprite", "Optional sprite used by offscreen projectile warnings. Empty uses the built-in triangular warning sprite.");
        AddPropertyField(panel, container, warningProperty, "indicatorColor", "Indicator Color", "Tint applied to offscreen projectile warning indicators.");
        AddSliderField(panel, container, warningProperty.FindPropertyRelative("indicatorSizePixels"), "Indicator Size", 12f, 128f, "Square size in pixels used by each projectile warning indicator.");
        AddSliderField(panel, container, warningProperty.FindPropertyRelative("edgePaddingPixels"), "Screen Margin", 0f, 160f, "Extra screen-edge margin in pixels kept outside the projectile warning half size.");
        AddProjectileOffscreenWarningWarnings(warningProperty, container);
        return container;
    }

    /// <summary>
    /// Adds a compact section label used to separate dense boss UI controls inside the visual preset tool.
    /// </summary>
    /// <param name="target">Parent receiving the label.</param>
    /// <param name="label">User-facing label text.</param>
    private static void AddSectionLabel(VisualElement target, string label)
    {
        if (target == null)
            return;

        Label sectionLabel = new Label(label);
        sectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        sectionLabel.style.marginTop = 6f;
        sectionLabel.style.marginBottom = 2f;
        target.Add(sectionLabel);
    }

    internal static void AddSliderField(EnemyVisualPresetsPanel panel,
                                        VisualElement target,
                                        SerializedProperty property,
                                        string label,
                                        float lowValue,
                                        float highValue,
                                        string tooltip)
    {
        if (panel == null)
            return;

        if (target == null)
            return;

        if (property == null)
            return;

        Slider slider = new Slider(label, lowValue, highValue);
        slider.showInputField = true;
        slider.tooltip = tooltip;
        slider.SetValueWithoutNotify(property.floatValue);
        slider.RegisterValueChangedCallback(evt =>
        {
            Object targetObject = panel.PresetSerializedObject.targetObject;
            Undo.RecordObject(targetObject, "Edit Enemy Visual Settings");
            panel.PresetSerializedObject.Update();
            property.floatValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        target.Add(slider);
    }

    /// <summary>
    /// Adds a toggle that rebuilds the active details section when dependent settings should appear or hide.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset context.</param>
    /// <param name="target">Parent element receiving the toggle.</param>
    /// <param name="property">Serialized boolean property.</param>
    /// <param name="label">Visible control label.</param>
    /// <param name="tooltip">Tooltip explaining the setting.</param>
    internal static void AddReactiveToggleField(EnemyVisualPresetsPanel panel,
                                                VisualElement target,
                                                SerializedProperty property,
                                                string label,
                                                string tooltip)
    {
        AddReactiveToggleField(panel, target, property, label, tooltip, "Edit Enemy Visual Settings");
    }

    /// <summary>
    /// Adds a toggle that rebuilds the active details section when dependent settings should appear or hide.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset context.</param>
    /// <param name="target">Parent element receiving the toggle.</param>
    /// <param name="property">Serialized boolean property.</param>
    /// <param name="label">Visible control label.</param>
    /// <param name="tooltip">Tooltip explaining the setting.</param>
    /// <param name="undoLabel">Undo label used for the serialized edit.</param>
    internal static void AddReactiveToggleField(EnemyVisualPresetsPanel panel,
                                                VisualElement target,
                                                SerializedProperty property,
                                                string label,
                                                string tooltip,
                                                string undoLabel)
    {
        if (panel == null || target == null || property == null)
            return;

        Toggle toggle = new Toggle(label);
        toggle.tooltip = tooltip;
        toggle.SetValueWithoutNotify(property.boolValue);
        toggle.RegisterValueChangedCallback(evt =>
        {
            Object targetObject = panel.PresetSerializedObject.targetObject;
            Undo.RecordObject(targetObject, undoLabel);
            panel.PresetSerializedObject.Update();
            property.boolValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
            panel.RebuildActiveDetailsSection();
        });
        target.Add(toggle);
    }

    /// <summary>
    /// Adds authored-value warnings for projectile offscreen indicators without mutating the serialized values.
    /// </summary>
    /// <param name="warningProperty">Serialized projectile warning settings.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    private static void AddProjectileOffscreenWarningWarnings(SerializedProperty warningProperty, VisualElement container)
    {
        if (warningProperty == null || container == null)
            return;

        AddNonPositiveValueWarning(warningProperty, container, "indicatorSizePixels", "Projectile warning indicator size should be greater than zero.");
        AddNegativeValueWarning(warningProperty, container, "edgePaddingPixels", "Projectile warning screen margin must be zero or positive.");
    }

    /// <summary>
    /// Adds authored-value warnings for spawn warning overrides without mutating the serialized values.
    /// </summary>
    /// <param name="spawnOverridesProperty">Serialized spawn override settings.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    private static void AddSpawnOverrideWarnings(SerializedProperty spawnOverridesProperty, VisualElement container)
    {
        if (spawnOverridesProperty == null || container == null)
            return;

        AddNegativeValueWarning(spawnOverridesProperty, container, "spawnWarningLeadTimeSeconds", "Lead Time Seconds must be zero or positive.");
        AddNonPositiveValueWarning(spawnOverridesProperty, container, "spawnWarningRadiusScale", "Radius Scale should be greater than zero.");
        AddNonPositiveValueWarning(spawnOverridesProperty, container, "spawnWarningRingWidth", "Ring Width should be greater than zero.");
        AddNegativeValueWarning(spawnOverridesProperty, container, "spawnWarningHeightOffset", "Height Offset must be zero or positive.");
        AddRangeWarning(spawnOverridesProperty, container, "spawnWarningMaximumAlpha", 0f, 1f, "Maximum Alpha should stay between 0 and 1.");
        AddNegativeValueWarning(spawnOverridesProperty, container, "spawnWarningFadeOutSeconds", "Fade Out Seconds must be zero or positive.");
    }

    /// <summary>
    /// Adds a warning when a float property contains a negative value.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="message">Warning text.</param>
    internal static void AddNegativeValueWarning(SerializedProperty parentProperty,
                                                 VisualElement container,
                                                 string relativePropertyName,
                                                 string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && property.floatValue < 0f)
            container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds a warning when a float property contains a zero or negative value.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="message">Warning text.</param>
    internal static void AddNonPositiveValueWarning(SerializedProperty parentProperty,
                                                    VisualElement container,
                                                    string relativePropertyName,
                                                    string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && property.floatValue <= 0f)
            container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds a warning when a float property falls outside the expected authored range.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="minimum">Expected inclusive minimum.</param>
    /// <param name="maximum">Expected inclusive maximum.</param>
    /// <param name="message">Warning text.</param>
    private static void AddRangeWarning(SerializedProperty parentProperty,
                                        VisualElement container,
                                        string relativePropertyName,
                                        float minimum,
                                        float maximum,
                                        string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && (property.floatValue < minimum || property.floatValue > maximum))
            container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }
    #endregion

    #endregion
}
