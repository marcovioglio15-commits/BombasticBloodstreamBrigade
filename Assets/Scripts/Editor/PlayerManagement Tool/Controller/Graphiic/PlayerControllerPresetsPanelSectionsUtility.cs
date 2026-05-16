using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds detail sections and section switching flows for player controller preset panels.
/// </summary>
internal static class PlayerControllerPresetsPanelSectionsUtility
{
    #region Constants
    private const float SectionMarginTop = 8f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Selects one preset and rebuilds the detail area.
    /// </summary>
    /// <param name="panel">Owning panel that stores selection and detail roots.</param>
    /// <param name="preset">Preset to select, or null to clear the detail view.</param>

    public static void SelectPreset(PlayerControllerPresetsPanel panel, PlayerControllerPreset preset)
    {
        if (panel == null)
            return;

        panel.SelectedPreset = preset;
        panel.DetailsRoot.Clear();
        panel.SectionButtonsRoot = null;
        panel.SectionContentRoot = null;

        if (panel.SelectedPreset == null)
        {
            Label label = new Label("Select or create a preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            panel.DetailsRoot.Add(label);
            return;
        }

        panel.PresetSerializedObject = new SerializedObject(panel.SelectedPreset);
        panel.SectionButtonsRoot = BuildSectionButtons(panel);
        panel.SectionContentRoot = new VisualElement();
        panel.SectionContentRoot.style.flexDirection = FlexDirection.Column;
        panel.SectionContentRoot.style.flexGrow = 1f;
        panel.DetailsRoot.Add(panel.SectionButtonsRoot);
        panel.DetailsRoot.Add(panel.SectionContentRoot);
        BuildActiveSection(panel);
    }

    /// <summary>
    /// Builds the metadata section for the selected preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and callbacks.</param>

    public static void BuildMetadataSection(PlayerControllerPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateSectionContainer(panel, "Preset Details");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
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
    /// Builds the movement section for the selected preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and helper utilities.</param>

    public static void BuildMovementSection(PlayerControllerPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement section = CreateSectionContainer(panel, "Movement Settings");

        if (section == null)
            return;

        SerializedProperty movementProperty = panel.PresetSerializedObject.FindProperty("movementSettings");

        if (movementProperty == null)
            return;

        SerializedProperty modeProperty = movementProperty.FindPropertyRelative("directionsMode");
        SerializedProperty countProperty = movementProperty.FindPropertyRelative("discreteDirectionCount");
        SerializedProperty offsetProperty = movementProperty.FindPropertyRelative("directionOffsetDegrees");
        SerializedProperty referenceProperty = movementProperty.FindPropertyRelative("movementReference");
        SerializedProperty valuesProperty = movementProperty.FindPropertyRelative("values");
        SerializedProperty scalingRulesProperty = panel.PresetSerializedObject.FindProperty("scalingRules");

        VisualElement modeField = CreateScalingField(modeProperty,
                                                     scalingRulesProperty,
                                                     "Allowed Directions");
        section.Add(modeField);

        VisualElement discreteContainer = new VisualElement();
        discreteContainer.style.marginLeft = 8f;

        VisualElement countFieldElement = PlayerScalingFieldElementFactory.CreateField(countProperty, scalingRulesProperty, "Direction Count");
        discreteContainer.Add(countFieldElement);
        VisualElement offsetFieldElement = PlayerScalingFieldElementFactory.CreateField(offsetProperty, scalingRulesProperty, "Direction Offset");
        discreteContainer.Add(offsetFieldElement);

        VisualElement discreteWarningsRoot = new VisualElement();
        discreteWarningsRoot.style.marginTop = 4f;
        discreteContainer.Add(discreteWarningsRoot);

        PieChartElement pieChart = new PieChartElement();
        Slider movementZoomSlider = PlayerControllerPresetsPanelFieldUtility.CreatePieZoomSlider(pieChart);
        discreteContainer.Add(pieChart);
        discreteContainer.Add(movementZoomSlider);
        section.Add(discreteContainer);

        VisualElement referenceField = CreateScalingField(referenceProperty,
                                                          scalingRulesProperty,
                                                          "Movement Reference");
        section.Add(referenceField);

        SerializedProperty moveActionProperty = panel.PresetSerializedObject.FindProperty("moveActionId");
        PlayerControllerPresetsPanelFieldUtility.EnsureDefaultActionId(panel, moveActionProperty, "Move");

        Foldout bindingsFoldout = PlayerControllerPresetsPanelFieldUtility.BuildBindingsFoldout(panel.InputAsset,
                                                                                                panel.PresetSerializedObject,
                                                                                                moveActionProperty,
                                                                                                InputActionSelectionElement.SelectionMode.Movement);
        section.Add(bindingsFoldout);

        Foldout valuesFoldout = PlayerControllerPresetsPanelFieldUtility.BuildValuesFoldout(valuesProperty,
                                                                                            scalingRulesProperty,
                                                                                            new string[]
        {
            "baseSpeed",
            "maxSpeed",
            "acceleration",
            "deceleration",
            "oppositeDirectionBrakeMultiplier",
            "wallBounceCoefficient",
            "wallCollisionSkinWidth",
            "inputDeadZone",
            "digitalReleaseGraceSeconds"
        });
        section.Add(valuesFoldout);

        System.Action updateView = () =>
        {
            MovementDirectionsMode mode = (MovementDirectionsMode)modeProperty.enumValueIndex;
            bool isDiscrete = mode == MovementDirectionsMode.DiscreteCount;
            discreteContainer.style.display = isDiscrete ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerDirectionWarningUtility.RefreshOffsetWarnings(discreteWarningsRoot,
                                                                         isDiscrete,
                                                                         countProperty.intValue,
                                                                         offsetProperty.floatValue);

            if (isDiscrete)
            {
                PlayerControllerPresetsPanelVisualizationUtility.UpdateDiscretePieChart(pieChart, countProperty.intValue, offsetProperty.floatValue);
            }
        };

        modeField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        countFieldElement.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        countFieldElement.RegisterCallback<ChangeEvent<int>>(evt =>
        {
            ScheduleViewRefresh();
        });

        offsetFieldElement.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            ScheduleViewRefresh();
        });

        offsetFieldElement.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        updateView();

        void ScheduleViewRefresh()
        {
            section.schedule.Execute(() =>
            {
                if (panel.PresetSerializedObject != null)
                    panel.PresetSerializedObject.Update();

                updateView();
            }).ExecuteLater(0);
        }
    }

    /// <summary>
    /// Builds the look section for the selected preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and helper utilities.</param>

    public static void BuildLookSection(PlayerControllerPresetsPanel panel)
    {
        PlayerControllerPresetsPanelLookSectionUtility.BuildLookSection(panel, CreateSectionContainer(panel, "Look Settings"));
    }

    /// <summary>
    /// Builds the shooting section for the selected preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and helper utilities.</param>

    public static void BuildShootingSection(PlayerControllerPresetsPanel panel)
    {
        VisualElement section = CreateSectionContainer(panel, "Shooting Settings");
        PlayerControllerPresetsPanelShootingSectionUtility.BuildShootingSection(panel, section);
    }

    /// <summary>
    /// Builds the camera section for the selected preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and helper utilities.</param>

    public static void BuildCameraSection(PlayerControllerPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement section = CreateSectionContainer(panel, "Camera Settings");

        if (section == null)
            return;

        SerializedProperty cameraProperty = panel.PresetSerializedObject.FindProperty("cameraSettings");

        if (cameraProperty == null)
            return;

        SerializedProperty behaviorProperty = cameraProperty.FindPropertyRelative("behavior");
        SerializedProperty offsetProperty = cameraProperty.FindPropertyRelative("followOffset");
        SerializedProperty anchorProperty = cameraProperty.FindPropertyRelative("roomAnchor");
        SerializedProperty valuesProperty = cameraProperty.FindPropertyRelative("values");
        SerializedProperty scalingRulesProperty = panel.PresetSerializedObject.FindProperty("scalingRules");

        VisualElement behaviorField = CreateScalingField(behaviorProperty,
                                                         scalingRulesProperty,
                                                         "Camera Behavior");
        section.Add(behaviorField);

        VisualElement offsetField = CreateScalingField(offsetProperty,
                                                       scalingRulesProperty,
                                                       "Follow Offset",
                                                       "Local offset applied by the follow camera relative to the tracked player position.");
        section.Add(offsetField);

        ObjectField anchorField = new ObjectField("Room Anchor");
        anchorField.objectType = typeof(Transform);
        anchorField.BindProperty(anchorProperty);
        section.Add(anchorField);

        Foldout valuesFoldout = PlayerControllerPresetsPanelFieldUtility.BuildValuesFoldout(valuesProperty,
                                                                                            scalingRulesProperty,
                                                                                            new string[]
        {
            "followSpeed",
            "cameraLag",
            "damping",
            "maxFollowDistance",
            "deadZoneRadius"
        });
        section.Add(valuesFoldout);

        System.Action updateView = () =>
        {
            CameraBehavior behavior = (CameraBehavior)behaviorProperty.enumValueIndex;
            offsetField.style.display = behavior == CameraBehavior.FollowWithOffset ? DisplayStyle.Flex : DisplayStyle.None;
            anchorField.style.display = behavior == CameraBehavior.RoomFixed ? DisplayStyle.Flex : DisplayStyle.None;
        };

        behaviorField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        updateView();
    }

    /// <summary>
    /// Builds the health statistics section for the selected preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context.</param>

    public static void BuildHealthStatisticsSection(PlayerControllerPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement section = CreateSectionContainer(panel, "Health Statistics");

        if (section == null)
            return;

        SerializedProperty healthStatisticsProperty = panel.PresetSerializedObject.FindProperty("healthStatistics");
        SerializedProperty scalingRulesProperty = panel.PresetSerializedObject.FindProperty("scalingRules");

        if (healthStatisticsProperty == null || scalingRulesProperty == null)
        {
            Label missingLabel = new Label("Health statistics data is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            section.Add(missingLabel);
            return;
        }

        SerializedProperty maxHealthProperty = healthStatisticsProperty.FindPropertyRelative("maxHealth");
        SerializedProperty maxHealthAdjustmentModeProperty = healthStatisticsProperty.FindPropertyRelative("maxHealthAdjustmentMode");
        SerializedProperty maxShieldProperty = healthStatisticsProperty.FindPropertyRelative("maxShield");
        SerializedProperty maxShieldAdjustmentModeProperty = healthStatisticsProperty.FindPropertyRelative("maxShieldAdjustmentMode");
        SerializedProperty graceTimeProperty = healthStatisticsProperty.FindPropertyRelative("graceTimeSeconds");

        if (maxHealthProperty == null ||
            maxHealthAdjustmentModeProperty == null ||
            maxShieldProperty == null ||
            maxShieldAdjustmentModeProperty == null ||
            graceTimeProperty == null)
        {
            Label missingFieldsLabel = new Label("Health statistics fields are missing on this preset.");
            missingFieldsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            section.Add(missingFieldsLabel);
            return;
        }

        section.Add(PlayerScalingFieldElementFactory.CreateField(maxHealthProperty, scalingRulesProperty, "Max Health"));
        section.Add(CreateScalingField(maxHealthAdjustmentModeProperty,
                                       scalingRulesProperty,
                                       "Current Health On Max Change",
                                       "Defines how Current Health is recomputed when Add Scaling changes Max Health at runtime."));
        section.Add(PlayerScalingFieldElementFactory.CreateField(maxShieldProperty, scalingRulesProperty, "Max Shield"));
        section.Add(CreateScalingField(maxShieldAdjustmentModeProperty,
                                       scalingRulesProperty,
                                       "Current Shield On Max Change",
                                       "Defines how Current Shield is recomputed when Add Scaling changes Max Shield at runtime."));
        section.Add(PlayerScalingFieldElementFactory.CreateField(graceTimeProperty, scalingRulesProperty, "Grace Time Seconds"));
    }

    /// <summary>
    /// Sets the active detail section, persists it and rebuilds the visible content.
    /// </summary>
    /// <param name="panel">Owning panel that stores section state.</param>
    /// <param name="sectionType">Section to activate.</param>

    public static void SetActiveSection(PlayerControllerPresetsPanel panel, PlayerControllerPresetsPanel.SectionType sectionType)
    {
        if (panel == null)
            return;

        panel.ActiveSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(PlayerControllerPresetsPanel.ActiveSectionStateKey, panel.ActiveSection);
        BuildActiveSection(panel);
    }

    /// <summary>
    /// Rebuilds the currently active detail section.
    /// </summary>
    /// <param name="panel">Owning panel that stores the active section and serialized preset context.</param>

    public static void BuildActiveSection(PlayerControllerPresetsPanel panel)
    {
        if (panel == null || panel.SectionContentRoot == null)
            return;

        panel.SectionContentRoot.Clear();

        switch (panel.ActiveSection)
        {
            case PlayerControllerPresetsPanel.SectionType.Metadata:
                BuildMetadataSection(panel);
                return;
            case PlayerControllerPresetsPanel.SectionType.Movement:
                BuildMovementSection(panel);
                return;
            case PlayerControllerPresetsPanel.SectionType.Look:
                BuildLookSection(panel);
                return;
            case PlayerControllerPresetsPanel.SectionType.Shooting:
                BuildShootingSection(panel);
                return;
            case PlayerControllerPresetsPanel.SectionType.Camera:
                BuildCameraSection(panel);
                return;
            case PlayerControllerPresetsPanel.SectionType.HealthStatistics:
                BuildHealthStatisticsSection(panel);
                return;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the section tab row.
    /// </summary>
    /// <param name="panel">Owning panel that stores section state.</param>
    /// <returns>Returns the section tab row.</returns>
    private static VisualElement BuildSectionButtons(PlayerControllerPresetsPanel panel)
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(panel, buttonsRoot, PlayerControllerPresetsPanel.SectionType.Metadata, "Metadata");
        AddSectionButton(panel, buttonsRoot, PlayerControllerPresetsPanel.SectionType.Movement, "Movement");
        AddSectionButton(panel, buttonsRoot, PlayerControllerPresetsPanel.SectionType.Look, "Look");
        AddSectionButton(panel, buttonsRoot, PlayerControllerPresetsPanel.SectionType.Shooting, "Shooting");
        AddSectionButton(panel, buttonsRoot, PlayerControllerPresetsPanel.SectionType.Camera, "Camera");
        AddSectionButton(panel, buttonsRoot, PlayerControllerPresetsPanel.SectionType.HealthStatistics, "Health Statistics");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one section button to the provided row.
    /// </summary>
    /// <param name="panel">Owning panel that receives the section activation callback.</param>
    /// <param name="parent">Parent row that receives the button.</param>
    /// <param name="sectionType">Section represented by the button.</param>
    /// <param name="buttonLabel">Visible button label.</param>

    private static void AddSectionButton(PlayerControllerPresetsPanel panel,
                                         VisualElement parent,
                                         PlayerControllerPresetsPanel.SectionType sectionType,
                                         string buttonLabel)
    {
        Button sectionButton = new Button(() => panel.SetActiveSection(sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    /// <summary>
    /// Builds one scaling-aware property field with an optional tooltip override.
    /// </summary>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="label">Displayed field label.</param>
    /// <param name="tooltip">Tooltip shown by the field in the tool UI.</param>
    /// <returns>Returns the configured property field.</returns>
    private static VisualElement CreateScalingField(SerializedProperty property,
                                                    SerializedProperty scalingRulesProperty,
                                                    string label,
                                                    string tooltip = null)
    {
        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);

        if (!string.IsNullOrWhiteSpace(tooltip))
            field.tooltip = tooltip;

        return field;
    }

    /// <summary>
    /// Creates one section container with a header and registers it into the active detail root.
    /// </summary>
    /// <param name="panel">Owning panel that stores the detail root.</param>
    /// <param name="sectionTitle">Visible section title.</param>
    /// <returns>Returns the created section container.</returns>
    private static VisualElement CreateSectionContainer(PlayerControllerPresetsPanel panel, string sectionTitle)
    {
        if (panel == null || panel.SectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = SectionMarginTop;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.Controller.Section." + sectionTitle);
        container.Add(header);
        panel.SectionContentRoot.Add(container);
        return container;
    }
    #endregion

    #endregion
}
