using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds detail sections and subsection tabs for enemy brain preset panels.
/// </summary>
internal static class EnemyBrainPresetsPanelSectionsUtility
{
    #region Constants
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the metadata section for the selected enemy brain preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and callbacks.</param>

    public static void BuildMetadataSection(EnemyBrainPresetsPanel panel)
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

    /// <summary>
    /// Builds the brain section shell and all subsection tabs.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and callbacks.</param>

    public static void BuildBrainSection(EnemyBrainPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Brain");

        if (sectionContainer == null)
            return;

        panel.BrainSubSectionTabs.Clear();

        VisualElement tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.flexWrap = Wrap.Wrap;
        tabBar.style.marginBottom = 6f;
        tabBar.style.paddingTop = 4f;
        tabBar.style.paddingBottom = 4f;
        tabBar.style.paddingLeft = 2f;
        panel.BrainSubSectionTabBar = tabBar;

        VisualElement contentHost = new VisualElement();
        contentHost.style.flexDirection = FlexDirection.Column;
        contentHost.style.flexGrow = 1f;
        panel.BrainSubSectionContentHost = contentHost;

        sectionContainer.Add(tabBar);
        sectionContainer.Add(contentHost);

        AddBrainSubSectionTab(panel, EnemyBrainPresetsPanel.BrainSubSectionType.Movement, "Movement", BuildMovementSubSection(panel));
        AddBrainSubSectionTab(panel, EnemyBrainPresetsPanel.BrainSubSectionType.Steering, "Steering", BuildSteeringSubSection(panel));
        AddBrainSubSectionTab(panel, EnemyBrainPresetsPanel.BrainSubSectionType.TacticalNavigation, "Tactical Navigation", BuildTacticalNavigationSubSection(panel));
        AddBrainSubSectionTab(panel, EnemyBrainPresetsPanel.BrainSubSectionType.Damage, "Damage", BuildDamageSubSection(panel));
        AddBrainSubSectionTab(panel, EnemyBrainPresetsPanel.BrainSubSectionType.HealthStatistics, "Health Statistics", BuildHealthStatisticsSubSection(panel));

        if (!panel.BrainSubSectionTabs.ContainsKey(panel.ActiveBrainSubSection))
            panel.ActiveBrainSubSection = EnemyBrainPresetsPanel.BrainSubSectionType.Movement;

        panel.SetActiveBrainSubSection(panel.ActiveBrainSubSection);
    }

    /// <summary>
    /// Shows the currently active brain subsection content and refreshes tab styles.
    /// </summary>
    /// <param name="panel">Owning panel that provides tab state and content host.</param>

    public static void ShowActiveBrainSubSection(EnemyBrainPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement contentHost = panel.BrainSubSectionContentHost;

        if (contentHost == null)
            return;

        EnemyBrainPresetsPanel.BrainSubSectionTabEntry tabEntry;

        if (!panel.BrainSubSectionTabs.TryGetValue(panel.ActiveBrainSubSection, out tabEntry))
            return;

        if (tabEntry == null || tabEntry.Content == null)
            return;

        contentHost.Clear();
        contentHost.Add(tabEntry.Content);
        UpdateBrainSubSectionTabStyles(panel);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(contentHost);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates a standard details section container attached to the panel content root.
    /// </summary>
    /// <param name="panel">Owning panel that provides the content root.</param>
    /// <param name="sectionTitle">Header text for the section.</param>
    /// <returns>Returns the created section container, or null when the panel is not ready.</returns>
    private static VisualElement CreateDetailsSectionContainer(EnemyBrainPresetsPanel panel, string sectionTitle)
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
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.EnemyManagement.Brain.Section." + sectionTitle);
        container.Add(header);
        detailsSectionContentRoot.Add(container);
        return container;
    }

    /// <summary>
    /// Creates a subsection content foldout without duplicating nested drawer labels.
    /// </summary>
    /// <param name="sectionTitle">Subsection title shown in the header.</param>
    /// <returns>Returns the ready-to-fill subsection container.</returns>
    private static VisualElement CreateBrainSubSectionContainer(string sectionTitle)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout(sectionTitle,
                                                                           "NashCore.EnemyManagement.Brain.SubSection." + sectionTitle,
                                                                           true);
        foldout.style.marginTop = 4f;
        return foldout;
    }

    /// <summary>
    /// Adds one bound property field with tooltip and draft-dirty tracking.
    /// </summary>
    /// <param name="panel">Owning panel used only to ensure a valid serialized context exists.</param>
    /// <param name="target">Target container that receives the field.</param>
    /// <param name="parentProperty">Serialized parent property that owns the relative field.</param>
    /// <param name="relativePropertyName">Relative field name under the parent property.</param>
    /// <param name="label">Display label used by the property field.</param>
    /// <param name="tooltip">Tooltip text shown by the property field.</param>

    private static void AddPropertyField(EnemyBrainPresetsPanel panel,
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
        });
        target.Add(propertyField);
    }

    /// <summary>
    /// Adds one bound float slider with input field, tooltip, and draft-dirty tracking.
    /// </summary>
    /// <param name="panel">Owning panel used only to ensure a valid serialized context exists.</param>
    /// <param name="target">Target container that receives the slider.</param>
    /// <param name="parentProperty">Serialized parent property that owns the relative field.</param>
    /// <param name="relativePropertyName">Relative float field name under the parent property.</param>
    /// <param name="label">Display label used by the slider.</param>
    /// <param name="lowValue">Minimum slider value.</param>
    /// <param name="highValue">Maximum slider value.</param>
    /// <param name="tooltip">Tooltip text shown by the slider.</param>
    private static void AddFloatSliderField(EnemyBrainPresetsPanel panel,
                                            VisualElement target,
                                            SerializedProperty parentProperty,
                                            string relativePropertyName,
                                            string label,
                                            float lowValue,
                                            float highValue,
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

        Slider slider = new Slider(label, lowValue, highValue);
        slider.showInputField = true;
        slider.tooltip = tooltip;
        slider.BindProperty(property);
        slider.RegisterValueChangedCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
        });
        target.Add(slider);
    }

    /// <summary>
    /// Creates one toggle-bound foldout used by damage subsections.
    /// </summary>
    /// <param name="toggleProperty">Boolean property that drives the foldout state.</param>
    /// <param name="title">Foldout title.</param>
    /// <returns>Returns the configured foldout.</returns>
    private static Foldout CreateToggleableDamageFoldout(SerializedProperty toggleProperty, string title)
    {
        Foldout foldout = new Foldout();
        foldout.text = title;
        foldout.BindProperty(toggleProperty);
        foldout.value = toggleProperty.boolValue;
        foldout.style.marginBottom = 4f;
        foldout.RegisterValueChangedCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
        });
        return foldout;
    }

    /// <summary>
    /// Adds one subsection tab button and stores its content entry in the panel dictionary.
    /// </summary>
    /// <param name="panel">Owning panel that stores tabs and handles activation.</param>
    /// <param name="subSectionType">Subsection enum key.</param>
    /// <param name="tabLabel">Button label shown in the tab bar.</param>
    /// <param name="content">Prepared content visual element for the subsection.</param>

    private static void AddBrainSubSectionTab(EnemyBrainPresetsPanel panel,
                                              EnemyBrainPresetsPanel.BrainSubSectionType subSectionType,
                                              string tabLabel,
                                              VisualElement content)
    {
        if (panel == null)
            return;

        if (panel.BrainSubSectionTabBar == null)
            return;

        if (content == null)
            return;

        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;
        tabContainer.style.marginBottom = 4f;

        Button tabButton = new Button(() => panel.SetActiveBrainSubSection(subSectionType));
        tabButton.text = tabLabel;
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabContainer.Add(tabButton);

        panel.BrainSubSectionTabBar.Add(tabContainer);

        EnemyBrainPresetsPanel.BrainSubSectionTabEntry tabEntry = new EnemyBrainPresetsPanel.BrainSubSectionTabEntry();
        tabEntry.TabContainer = tabContainer;
        tabEntry.TabButton = tabButton;
        tabEntry.Content = content;
        panel.BrainSubSectionTabs[subSectionType] = tabEntry;
    }

    /// <summary>
    /// Refreshes tab button styles so the active brain subsection is visually emphasized.
    /// </summary>
    /// <param name="panel">Owning panel that provides the current active tab state.</param>

    private static void UpdateBrainSubSectionTabStyles(EnemyBrainPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<EnemyBrainPresetsPanel.BrainSubSectionType, EnemyBrainPresetsPanel.BrainSubSectionTabEntry> tabEntry in panel.BrainSubSectionTabs)
        {
            if (tabEntry.Value == null || tabEntry.Value.TabButton == null)
                continue;

            bool isActive = tabEntry.Key == panel.ActiveBrainSubSection;
            tabEntry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            tabEntry.Value.TabButton.style.backgroundColor = isActive ? ActiveTabColor : Color.clear;
        }
    }

    /// <summary>
    /// Builds the movement subsection content.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context.</param>
    /// <returns>Returns the movement subsection content.</returns>
    private static VisualElement BuildMovementSubSection(EnemyBrainPresetsPanel panel)
    {
        SerializedProperty movementProperty = panel.PresetSerializedObject.FindProperty("movement");
        VisualElement container = CreateBrainSubSectionContainer("Movement");

        AddPropertyField(panel, container, movementProperty, "moveSpeed", "Move Speed", "Meters per second used as baseline enemy movement speed toward the player.");
        AddPropertyField(panel, container, movementProperty, "maxSpeed", "Max Speed", "Hard cap applied to the enemy velocity magnitude.");
        AddPropertyField(panel, container, movementProperty, "acceleration", "Acceleration", "Meters per second squared used to accelerate toward desired velocity.");
        AddPropertyField(panel, container, movementProperty, "deceleration", "Deceleration", "Reserved deceleration value for future braking behaviors. Currently unused at runtime.");
        AddPropertyField(panel, container, movementProperty, "inactivityTime", "Inactivity Time", "Seconds after spawn during which the enemy stays fully idle while still remaining damageable.");
        AddPropertyField(panel, container, movementProperty, "rotationSpeedDegreesPerSecond", "Rotation Speed (Deg/Sec)", "Self-rotation speed around Y in degrees per second. Positive rotates clockwise, negative counter-clockwise.");
        AddPropertyField(panel, container, movementProperty, "priorityTier", "Priority Tier", "General enemy priority tier used by steering and visual overlap rules. Higher values keep right-of-way over lower tiers.");
        AddPropertyField(panel, container, movementProperty, "steeringAggressiveness", "Steering Aggressiveness", "Scales steering and clearance reactivity. Higher values produce stronger side-step and avoidance corrections.");
        AddPropertyField(panel, container, movementProperty, "minimumWallDistance", "Minimum Wall Distance", "Extra distance in meters kept from static wall colliders by standard steering-driven enemies.");
        AddPropertyField(panel, container, movementProperty, "disablePlayerKnockback", "Disable Player Knockback", "When enabled, player projectile knockback is ignored regardless of player stats or projectile payloads.");
        return container;
    }

    /// <summary>
    /// Builds the steering subsection content.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context.</param>
    /// <returns>Returns the steering subsection content.</returns>
    private static VisualElement BuildSteeringSubSection(EnemyBrainPresetsPanel panel)
    {
        SerializedProperty steeringProperty = panel.PresetSerializedObject.FindProperty("steering");
        VisualElement container = CreateBrainSubSectionContainer("Steering");

        AddPropertyField(panel, container, steeringProperty, "separationRadius", "Separation Radius", "Radius used to search neighboring enemies for separation steering.");
        AddPropertyField(panel, container, steeringProperty, "separationWeight", "Separation Weight", "Weight applied to the separation vector before velocity clamping.");
        AddPropertyField(panel, container, steeringProperty, "bodyRadius", "Body Radius", "Base physical body radius used for projectile hit checks and overlap handling.");
        AddPropertyField(panel, container, steeringProperty, "bodyRadiusXScale", "Body Radius X Scale", "Horizontal X scale applied to Body Radius when resolving the projectile hit ellipse.");
        AddPropertyField(panel, container, steeringProperty, "bodyRadiusZScale", "Body Radius Z Scale", "Horizontal Z scale applied to Body Radius when resolving the projectile hit ellipse.");
        AddSteeringWarnings(steeringProperty, container);
        return container;
    }

    /// <summary>
    /// Builds the tactical navigation subsection content.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context.</param>
    /// <returns>Returns the tactical navigation subsection content.</returns>
    private static VisualElement BuildTacticalNavigationSubSection(EnemyBrainPresetsPanel panel)
    {
        SerializedProperty tacticalProperty = panel.PresetSerializedObject.FindProperty("tacticalNavigation");
        VisualElement container = CreateBrainSubSectionContainer("Tactical Navigation");

        AddPropertyField(panel, container, tacticalProperty, "candidateBudget", "Candidate Budget", "Candidate budget used before LOD clamps the tactical scorer.");
        AddFloatSliderField(panel, container, tacticalProperty, "navigationInfluence", "Navigation Influence", 0f, 1f, "Weight applied to shared flow-field directions when direct movement is blocked or tactically worse.");
        AddFloatSliderField(panel, container, tacticalProperty, "predictionHorizonSeconds", "Prediction Horizon Seconds", 0f, 2f, "Seconds used to predict player and neighbor positions while scoring movement candidates.");
        AddFloatSliderField(panel, container, tacticalProperty, "sidePassPreference", "Side-Pass Preference", 0f, 1f, "Weight for trajectories that approach the player and pass beside them instead of only chasing the current position.");
        AddFloatSliderField(panel, container, tacticalProperty, "crowdLanePreference", "Crowd Lane Preference", 0f, 1f, "Weight for deterministic crowd lanes that reduce enemy-to-enemy indecision and pileups.");
        AddFloatSliderField(panel, container, tacticalProperty, "wallTangentPreference", "Wall Tangent Preference", 0f, 1f, "Weight for wall-tangent candidates when movement is blocked or stuck recovery is active.");
        AddFloatSliderField(panel, container, tacticalProperty, "oscillationDamping", "Oscillation Damping", 0f, 1f, "Penalty applied to candidates that reverse the last committed movement direction.");
        AddFloatSliderField(panel, container, tacticalProperty, "stuckRecoverySeconds", "Stuck Recovery Seconds", 0.05f, 2f, "Seconds of poor displacement before tangent and flow-field alternatives receive stronger weight.");

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.marginTop = 4f;
        container.Add(warningBox);
        RefreshTacticalNavigationWarning(tacticalProperty, warningBox);

        if (panel.PresetSerializedObject != null)
        {
            container.TrackSerializedObjectValue(panel.PresetSerializedObject, changedObject =>
            {
                RefreshTacticalNavigationWarning(tacticalProperty, warningBox);
            });
        }

        return container;
    }

    /// <summary>
    /// Builds the damage subsection content.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context.</param>
    /// <returns>Returns the damage subsection content.</returns>
    private static VisualElement BuildDamageSubSection(EnemyBrainPresetsPanel panel)
    {
        SerializedProperty damageProperty = panel.PresetSerializedObject.FindProperty("damage");
        VisualElement container = CreateBrainSubSectionContainer("Damage");

        if (damageProperty == null)
            return container;

        SerializedProperty contactToggleProperty = damageProperty.FindPropertyRelative("contactDamageEnabled");
        SerializedProperty areaToggleProperty = damageProperty.FindPropertyRelative("areaDamageEnabled");

        if (contactToggleProperty != null)
        {
            Foldout contactFoldout = CreateToggleableDamageFoldout(contactToggleProperty, "Contact Damage");
            AddPropertyField(panel, contactFoldout, damageProperty, "contactRadius", "Contact Radius", "Distance from enemy center used to trigger contact damage ticks.");
            AddPropertyField(panel, contactFoldout, damageProperty, "contactAmountPerTick", "Amount Per Tick", "Flat damage amount subtracted from player health at each contact tick.");
            AddPropertyField(panel, contactFoldout, damageProperty, "contactTickInterval", "Tick Interval", "Interval in seconds between two contact damage ticks.");
            container.Add(contactFoldout);
        }

        if (areaToggleProperty != null)
        {
            Foldout areaFoldout = CreateToggleableDamageFoldout(areaToggleProperty, "Area Damage");
            AddPropertyField(panel, areaFoldout, damageProperty, "areaRadius", "Area Radius", "Distance from enemy center used to trigger area damage ticks.");
            AddPropertyField(panel, areaFoldout, damageProperty, "areaAmountPerTickPercent", "Amount Per Tick", "Percent of player max health applied per area damage tick.");
            AddPropertyField(panel, areaFoldout, damageProperty, "areaTickInterval", "Tick Interval", "Interval in seconds between two area damage ticks.");
            container.Add(areaFoldout);
        }

        return container;
    }

    /// <summary>
    /// Builds the health statistics subsection content.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context.</param>
    /// <returns>Returns the health statistics subsection content.</returns>
    private static VisualElement BuildHealthStatisticsSubSection(EnemyBrainPresetsPanel panel)
    {
        SerializedProperty healthStatisticsProperty = panel.PresetSerializedObject.FindProperty("healthStatistics");
        VisualElement container = CreateBrainSubSectionContainer("Health Statistics");

        AddPropertyField(panel, container, healthStatisticsProperty, "maxHealth", "Max Health", "Maximum and initial health assigned to this enemy when spawned from pool.");
        AddPropertyField(panel, container, healthStatisticsProperty, "maxShield", "Max Shield", "Maximum shield reserve assigned to this enemy at spawn. Shield absorbs incoming damage before health.");
        return container;
    }

    /// <summary>
    /// Refreshes tactical navigation warnings without mutating authored values.
    /// </summary>
    /// <param name="tacticalProperty">Serialized tactical navigation settings property.</param>
    /// <param name="warningBox">Help box receiving warning text.</param>
    private static void RefreshTacticalNavigationWarning(SerializedProperty tacticalProperty, HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();

        if (tacticalProperty != null)
        {
            AddNegativeWarning(tacticalProperty, "navigationInfluence", "Navigation Influence", warningLines);
            AddNegativeWarning(tacticalProperty, "predictionHorizonSeconds", "Prediction Horizon Seconds", warningLines);
            AddNegativeWarning(tacticalProperty, "sidePassPreference", "Side-Pass Preference", warningLines);
            AddNegativeWarning(tacticalProperty, "crowdLanePreference", "Crowd Lane Preference", warningLines);
            AddNegativeWarning(tacticalProperty, "wallTangentPreference", "Wall Tangent Preference", warningLines);
            AddNegativeWarning(tacticalProperty, "oscillationDamping", "Oscillation Damping", warningLines);

            SerializedProperty stuckRecoverySecondsProperty = tacticalProperty.FindPropertyRelative("stuckRecoverySeconds");

            if (stuckRecoverySecondsProperty != null && stuckRecoverySecondsProperty.floatValue <= 0f)
                warningLines.Add("Stuck Recovery Seconds is zero or negative. Wall-stuck recovery will use a runtime minimum.");
        }

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Adds steering warnings for authored values that bake to clamped or invisible hit areas.
    /// </summary>
    /// <param name="steeringProperty">Serialized steering settings block.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    private static void AddSteeringWarnings(SerializedProperty steeringProperty, VisualElement container)
    {
        if (steeringProperty == null || container == null)
            return;

        AddNonPositiveWarning(steeringProperty, "separationRadius", "Separation Radius", "runtime bake clamps it to the minimum neighbor-search radius.", container);
        AddNegativeWarning(steeringProperty, "separationWeight", "Separation Weight", container);
        AddNonPositiveWarning(steeringProperty, "bodyRadius", "Body Radius", "runtime bake clamps the body hitbox to the minimum supported size.", container);
        AddNonPositiveWarning(steeringProperty, "bodyRadiusXScale", "Body Radius X Scale", "the X half-axis would be zero or negative, so runtime bake clamps it.", container);
        AddNonPositiveWarning(steeringProperty, "bodyRadiusZScale", "Body Radius Z Scale", "the Z half-axis would be zero or negative, so runtime bake clamps it.", container);
    }

    /// <summary>
    /// Adds one warning when a tactical float value is negative.
    /// </summary>
    /// <param name="parentProperty">Serialized tactical navigation parent.</param>
    /// <param name="relativePropertyName">Relative float field name.</param>
    /// <param name="displayName">Display name used in warning text.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddNegativeWarning(SerializedProperty parentProperty,
                                           string relativePropertyName,
                                           string displayName,
                                           List<string> warningLines)
    {
        if (parentProperty == null || warningLines == null)
            return;

        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && property.floatValue < 0f)
            warningLines.Add(displayName + " is negative. Runtime bake clamps it to a safe range.");
    }

    /// <summary>
    /// Adds one immediate warning box when a float value is negative.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="displayName">Display name used in warning text.</param>
    /// <param name="container">Parent element receiving the warning.</param>
    private static void AddNegativeWarning(SerializedProperty parentProperty,
                                           string relativePropertyName,
                                           string displayName,
                                           VisualElement container)
    {
        if (parentProperty == null || container == null)
            return;

        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && property.floatValue < 0f)
            container.Add(new HelpBox(displayName + " is negative. Runtime bake clamps it to a safe range.", HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds one immediate warning box when a float value is zero or negative.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="displayName">Display name used in warning text.</param>
    /// <param name="impact">Short description of the runtime consequence.</param>
    /// <param name="container">Parent element receiving the warning.</param>
    private static void AddNonPositiveWarning(SerializedProperty parentProperty,
                                              string relativePropertyName,
                                              string displayName,
                                              string impact,
                                              VisualElement container)
    {
        if (parentProperty == null || container == null)
            return;

        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && property.floatValue <= 0f)
            container.Add(new HelpBox(displayName + " is zero or negative: " + impact, HelpBoxMessageType.Warning));
    }
    #endregion

    #endregion
}
