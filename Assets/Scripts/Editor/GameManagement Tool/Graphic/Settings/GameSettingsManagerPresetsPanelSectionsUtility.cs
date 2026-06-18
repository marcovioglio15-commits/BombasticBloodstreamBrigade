using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static GameSettingsManagerPresetsPanelFieldUtility;

/// <summary>
/// Builds Settings Manager preset detail sections, validation output and FMOD preview assignments.
/// </summary>
internal static class GameSettingsManagerPresetsPanelSectionsUtility
{
    #region Constants
    private const string ActiveSectionStateKey = "NashCore.GameManagement.Settings.ActiveSection";
    #endregion

    #region Fields
    private static readonly List<GameFrameRateLimit> FrameRateLimitChoices = new List<GameFrameRateLimit>
    {
        GameFrameRateLimit.Fps60,
        GameFrameRateLimit.Fps120,
        GameFrameRateLimit.Fps180
    };

    private static readonly List<RuntimeMenuGamepadNavigationMode> GamepadNavigationModeChoices = new List<RuntimeMenuGamepadNavigationMode>
    {
        RuntimeMenuGamepadNavigationMode.VirtualMouse,
        RuntimeMenuGamepadNavigationMode.DirectSelection,
        RuntimeMenuGamepadNavigationMode.Hybrid
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the persisted active Settings Manager details section.
    /// </summary>
    /// <returns>Persisted section value or Metadata when none exists.</returns>
    public static GameSettingsManagerPresetsPanel.DetailsSectionType LoadActiveSection()
    {
        return ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, GameSettingsManagerPresetsPanel.DetailsSectionType.Metadata);
    }

    /// <summary>
    /// Selects one Settings Manager preset and rebuilds details.
    /// </summary>
    /// <param name="panel">Owning panel with detail roots.</param>
    /// <param name="preset">Preset to select, or null to clear details.</param>
    public static void SelectPreset(GameSettingsManagerPresetsPanel panel, GameSettingsManagerPreset preset)
    {
        if (panel == null || panel.DetailsRoot == null)
            return;

        panel.SelectedPreset = preset;
        ManagementToolStateUtility.SaveAssetPath(GameSettingsManagerPresetsPanel.SelectedPresetPathStateKey, preset);
        panel.DetailsRoot.Clear();

        if (panel.PresetListView != null && panel.SelectedPreset != null)
        {
            int selectedIndex = panel.FilteredPresets.IndexOf(panel.SelectedPreset);

            if (selectedIndex >= 0)
                panel.PresetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        if (panel.SelectedPreset == null)
        {
            panel.DetailsRoot.Add(new Label("Select or create a Settings Manager preset to edit."));
            return;
        }

        panel.SelectedPreset.EnsureInitialized();
        panel.PresetSerializedObject = new SerializedObject(panel.SelectedPreset);
        panel.SectionButtonsRoot = BuildSectionButtons(panel);
        panel.SectionContentRoot = new VisualElement();
        panel.SectionContentRoot.style.flexGrow = 1f;
        panel.DetailsRoot.Add(panel.SectionButtonsRoot);
        panel.DetailsRoot.Add(panel.SectionContentRoot);
        BuildActiveSection(panel);
    }

    /// <summary>
    /// Rebuilds the currently selected Settings Manager details section.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    public static void BuildActiveSection(GameSettingsManagerPresetsPanel panel)
    {
        if (panel == null || panel.SectionContentRoot == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.Update();
        panel.SectionContentRoot.Clear();

        switch (panel.ActiveSection)
        {
            case GameSettingsManagerPresetsPanel.DetailsSectionType.Audio:
                BuildAudioSection(panel);
                break;
            case GameSettingsManagerPresetsPanel.DetailsSectionType.Gameplay:
                BuildGameplaySection(panel);
                break;
            case GameSettingsManagerPresetsPanel.DetailsSectionType.ControllerNavigation:
                BuildControllerNavigationSection(panel);
                break;
            case GameSettingsManagerPresetsPanel.DetailsSectionType.Validation:
                BuildValidationSection(panel);
                break;
            default:
                BuildMetadataSection(panel);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.SectionContentRoot);
    }

    /// <summary>
    /// Marks the selected Settings Manager preset dirty in the draft session.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    public static void MarkSelectedPresetDirty(GameSettingsManagerPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(panel.SelectedPreset);
        GameManagementDraftSession.MarkDirty();
    }
    #endregion

    #region Section Builders
    /// <summary>
    /// Builds metadata fields for the selected Settings Manager preset.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildMetadataSection(GameSettingsManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Preset Details");
        AddBoundTextField(panel, section, "Preset Name", "presetName", true, false);
        AddBoundTextField(panel, section, "Version", "version", false, false);
        AddBoundTextField(panel, section, "Description", "description", false, true);

        SerializedProperty idProperty = panel.PresetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        PropertyField idField = new PropertyField(idProperty, "Preset ID");
        idField.tooltip = "Stable ID used by Game Management Tool for this Settings Manager preset.";
        idField.BindProperty(idProperty);
        idField.SetEnabled(false);
        section.Add(idField);
    }

    /// <summary>
    /// Builds the Settings menu Audio panel bus paths and preview events.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildAudioSection(GameSettingsManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Audio");
        SerializedProperty audioProperty = panel.PresetSerializedObject.FindProperty("audioSettings");

        if (audioProperty == null)
        {
            AddMissingSettingsBox(section, "Audio settings could not be resolved on this Settings Manager preset.");
            return;
        }

        BuildBusFoldout(panel, section, audioProperty);
        GameSettingsManagerPresetsPanelPreviewEventsUtility.Build(panel, section, audioProperty);
    }

    /// <summary>
    /// Builds gameplay defaults and windowed display values used by runtime Settings.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildGameplaySection(GameSettingsManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Gameplay");
        SerializedProperty audioProperty = panel.PresetSerializedObject.FindProperty("audioSettings");
        SerializedProperty experienceProperty = panel.PresetSerializedObject.FindProperty("experienceSettings");

        if (experienceProperty == null)
        {
            AddMissingSettingsBox(section, "Gameplay settings could not be resolved on this Settings Manager preset.");
            return;
        }

        Foldout defaultsFoldout = CreateFoldout("Reset Defaults", "Defaults restored by the runtime Settings menu Reset button.");

        if (audioProperty != null)
        {
            AddFloatSliderProperty(panel,
                                   defaultsFoldout,
                                   audioProperty.FindPropertyRelative("defaultMasterVolume"),
                                   "Master Volume",
                                   0f,
                                   1f,
                                   "Default Master volume restored by Reset Defaults.");
            AddFloatSliderProperty(panel,
                                   defaultsFoldout,
                                   audioProperty.FindPropertyRelative("defaultSfxVolume"),
                                   "SFX Volume",
                                   0f,
                                   1f,
                                   "Default SFX volume restored by Reset Defaults.");
            AddFloatSliderProperty(panel,
                                   defaultsFoldout,
                                   audioProperty.FindPropertyRelative("defaultMusicVolume"),
                                   "Music Volume",
                                   0f,
                                   1f,
                                   "Default Music volume restored by Reset Defaults.");
        }

        AddBooleanToggleProperty(panel,
                                 defaultsFoldout,
                                 experienceProperty.FindPropertyRelative("defaultVisualPointerEnabled"),
                                 "Visual Pointer Enabled",
                                 "Default Visual Pointer state restored by Reset Defaults.",
                                 false);
        AddBooleanToggleProperty(panel,
                                 defaultsFoldout,
                                 experienceProperty.FindPropertyRelative("defaultFullscreenEnabled"),
                                 "Fullscreen Enabled",
                                 "Default fullscreen state restored by Reset Defaults.",
                                 false);
        AddEnumPopupProperty(panel,
                             defaultsFoldout,
                             experienceProperty.FindPropertyRelative("defaultFrameRateLimit"),
                             "Frame Rate Cap",
                             "Default frame-rate cap restored by Reset Defaults.",
                             FrameRateLimitChoices,
                             FormatFrameRateLimit,
                             GameFrameRateLimit.Fps60,
                             false);
        AddFloatSliderProperty(panel,
                               defaultsFoldout,
                               experienceProperty.FindPropertyRelative("defaultDamageRumbleMultiplier"),
                               "Damage Rumble Multiplier",
                               0f,
                               2f,
                               "Default damage feedback rumble multiplier restored by Reset Defaults.");
        AddFloatSliderProperty(panel,
                               defaultsFoldout,
                               experienceProperty.FindPropertyRelative("defaultFireRumbleMultiplier"),
                               "Fire Rumble Multiplier",
                               0f,
                               2f,
                               "Default fire feedback rumble multiplier restored by Reset Defaults.");
        section.Add(defaultsFoldout);

        Foldout windowedFoldout = CreateFoldout("Windowed Display Defaults", "Window size applied when the runtime Settings menu disables fullscreen.");
        AddIntegerProperty(panel,
                           windowedFoldout,
                           experienceProperty.FindPropertyRelative("windowedWidth"),
                           "Width",
                           "Window width in pixels used when fullscreen is disabled.");
        AddIntegerProperty(panel,
                           windowedFoldout,
                           experienceProperty.FindPropertyRelative("windowedHeight"),
                           "Height",
                           "Window height in pixels used when fullscreen is disabled.");
        section.Add(windowedFoldout);
    }

    /// <summary>
    /// Builds controller navigation mode and action settings used by runtime Settings.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildControllerNavigationSection(GameSettingsManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Controller Navigation");
        SerializedProperty navigationProperty = panel.PresetSerializedObject.FindProperty("controllerNavigationSettings");

        if (navigationProperty == null)
        {
            AddMissingSettingsBox(section, "Controller Navigation settings could not be resolved on this Settings Manager preset.");
            return;
        }

        SerializedProperty modeProperty = navigationProperty.FindPropertyRelative("gamepadNavigationMode");
        AddEnumPopupProperty(panel,
                             section,
                             modeProperty,
                             "Controller Navigation Mode",
                             "Controls whether connected gamepads use only the virtual mouse, only configured direct actions, or both.",
                             GamepadNavigationModeChoices,
                             FormatGamepadNavigationMode,
                             RuntimeMenuGamepadNavigationMode.Hybrid,
                             true);

        RuntimeMenuGamepadNavigationMode mode = ResolveEnumPropertyValue(modeProperty, RuntimeMenuGamepadNavigationMode.Hybrid);

        bool usesDirectNavigation = mode == RuntimeMenuGamepadNavigationMode.DirectSelection ||
                                    mode == RuntimeMenuGamepadNavigationMode.Hybrid;

        if (usesDirectNavigation)
        {
            Foldout actionsFoldout = CreateFoldout("Direct Navigation Actions", "Input System actions used by direct controller focus.");
            GameSettingsManagerInputActionFieldUtility.AddActionPicker(panel,
                                                                       actionsFoldout,
                                                                       navigationProperty.FindPropertyRelative("navigateActionName"),
                                                                       "UI/Navigate",
                                                                       "Navigate Action",
                                                                       "Moves focus between tabs, dropdown headers and setting rows.",
                                                                       InputActionSelectionElement.SelectionMode.UINavigate);
            GameSettingsManagerInputActionFieldUtility.AddActionPicker(panel,
                                                                       actionsFoldout,
                                                                       navigationProperty.FindPropertyRelative("submitActionName"),
                                                                       "UI/Submit",
                                                                       "Submit Action",
                                                                       "Activates the focused tab, dropdown header or setting.",
                                                                       InputActionSelectionElement.SelectionMode.UISubmit);
            GameSettingsManagerInputActionFieldUtility.AddActionPicker(panel,
                                                                       actionsFoldout,
                                                                       navigationProperty.FindPropertyRelative("cancelActionName"),
                                                                       "UI/Cancel",
                                                                       "Cancel Action",
                                                                       "Closes the Settings menu from direct controller navigation.",
                                                                       InputActionSelectionElement.SelectionMode.UICancel);
            section.Add(actionsFoldout);

            Foldout repeatFoldout = CreateFoldout("Direct Navigation Repeat", "Deadzone and repeat timing used while holding a navigation input.");
            AddFloatSliderProperty(panel,
                                   repeatFoldout,
                                   navigationProperty.FindPropertyRelative("navigateDeadzone"),
                                   "Navigate Deadzone",
                                   0f,
                                   1f,
                                   "Minimum absolute navigate input required before direct focus moves.");
            AddFloatProperty(panel,
                             repeatFoldout,
                             navigationProperty.FindPropertyRelative("repeatDelaySeconds"),
                             "Repeat Delay",
                             "Unscaled seconds before a held navigate input repeats.");
            AddFloatProperty(panel,
                             repeatFoldout,
                             navigationProperty.FindPropertyRelative("repeatIntervalSeconds"),
                             "Repeat Interval",
                             "Unscaled seconds between repeated focus moves after the initial delay.");
            section.Add(repeatFoldout);
            return;
        }

        Foldout cursorFoldout = CreateFoldout("Virtual Mouse Cancel", "The virtual mouse still uses Cancel to close the overlay without relying on UI focus.");
        GameSettingsManagerInputActionFieldUtility.AddActionPicker(panel,
                                                                   cursorFoldout,
                                                                   navigationProperty.FindPropertyRelative("cancelActionName"),
                                                                   "UI/Cancel",
                                                                   "Cancel Action",
                                                                   "Closes the Settings menu while virtual mouse navigation is active.",
                                                                   InputActionSelectionElement.SelectionMode.UICancel);
        section.Add(cursorFoldout);
    }

    /// <summary>
    /// Builds non-mutating validation warning output.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset and warning buffer.</param>
    private static void BuildValidationSection(GameSettingsManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Validation");
        Button refreshButton = new Button(panel.BuildActiveSection);
        refreshButton.text = "Refresh";
        refreshButton.tooltip = "Refresh non-mutating validation warnings.";
        section.Add(refreshButton);

        GameSettingsManagerPresetValidationUtility.CollectWarnings(panel.SelectedPreset, panel.ValidationWarnings);

        if (panel.ValidationWarnings.Count <= 0)
        {
            Label cleanLabel = new Label("No warnings.");
            cleanLabel.tooltip = "The selected Settings Manager preset has no validation warnings.";
            section.Add(cleanLabel);
            return;
        }

        for (int index = 0; index < panel.ValidationWarnings.Count; index++)
        {
            HelpBox warningBox = new HelpBox(panel.ValidationWarnings[index], HelpBoxMessageType.Warning);
            section.Add(warningBox);
        }
    }
    #endregion

    #region Audio Foldouts
    /// <summary>
    /// Builds bus path controls for Settings menu volume sliders.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    /// <param name="section">Parent Audio section.</param>
    /// <param name="audioProperty">Serialized audio settings root.</param>
    private static void BuildBusFoldout(GameSettingsManagerPresetsPanel panel,
                                        VisualElement section,
                                        SerializedProperty audioProperty)
    {
        Foldout foldout = CreateFoldout("FMOD Buses", "FMOD buses controlled by the runtime Settings menu sliders.");
        AddDelayedStringProperty(panel,
                                 foldout,
                                 audioProperty.FindPropertyRelative("masterBusPath"),
                                 "Master Bus Path",
                                 "FMOD master bus path controlled by the Master volume slider.");
        AddDelayedStringProperty(panel,
                                 foldout,
                                 audioProperty.FindPropertyRelative("sfxBusPath"),
                                 "SFX Bus Path",
                                 "FMOD SFX bus path controlled by the SFX volume slider.");
        AddDelayedStringProperty(panel,
                                 foldout,
                                 audioProperty.FindPropertyRelative("musicBusPath"),
                                 "Music Bus Path",
                                 "FMOD music bus path controlled by the Music volume slider.");
        section.Add(foldout);
    }

    #endregion

    #region UI Helpers
    /// <summary>
    /// Formats one frame-rate cap enum value for the Settings Manager popup.
    /// </summary>
    /// <param name="frameRateLimit">Frame-rate cap value being displayed.</param>
    /// <returns>User-facing frame-rate label.</returns>
    private static string FormatFrameRateLimit(GameFrameRateLimit frameRateLimit)
    {
        return string.Format("{0} FPS", (int)frameRateLimit);
    }

    /// <summary>
    /// Formats one controller navigation mode for the Settings Manager popup.
    /// </summary>
    /// <param name="mode">Controller navigation mode being displayed.</param>
    /// <returns>User-facing mode label.</returns>
    private static string FormatGamepadNavigationMode(RuntimeMenuGamepadNavigationMode mode)
    {
        switch (mode)
        {
            case RuntimeMenuGamepadNavigationMode.VirtualMouse:
                return "Virtual Mouse Only";

            case RuntimeMenuGamepadNavigationMode.DirectSelection:
                return "Configured Actions Only";

            case RuntimeMenuGamepadNavigationMode.Hybrid:
                return "Virtual Mouse + Configured Actions";

            default:
                return mode.ToString();
        }
    }

    /// <summary>
    /// Builds the detail section selector button row.
    /// </summary>
    /// <param name="panel">Owning panel that stores the active section state.</param>
    /// <returns>Detail section button row.</returns>
    private static VisualElement BuildSectionButtons(GameSettingsManagerPresetsPanel panel)
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;
        AddSectionButton(panel, buttonsRoot, GameSettingsManagerPresetsPanel.DetailsSectionType.Metadata, "Metadata");
        AddSectionButton(panel, buttonsRoot, GameSettingsManagerPresetsPanel.DetailsSectionType.Audio, "Audio");
        AddSectionButton(panel, buttonsRoot, GameSettingsManagerPresetsPanel.DetailsSectionType.Gameplay, "Gameplay");
        AddSectionButton(panel, buttonsRoot, GameSettingsManagerPresetsPanel.DetailsSectionType.ControllerNavigation, "Controller Navigation");
        AddSectionButton(panel, buttonsRoot, GameSettingsManagerPresetsPanel.DetailsSectionType.Validation, "Validation");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one Settings Manager detail section selector button.
    /// </summary>
    /// <param name="panel">Owning panel receiving the selected section.</param>
    /// <param name="parent">Parent button row.</param>
    /// <param name="sectionType">Section activated by the button.</param>
    /// <param name="label">Visible label.</param>
    private static void AddSectionButton(GameSettingsManagerPresetsPanel panel,
                                         VisualElement parent,
                                         GameSettingsManagerPresetsPanel.DetailsSectionType sectionType,
                                         string label)
    {
        Button button = new Button(() =>
        {
            panel.ActiveSection = sectionType;
            ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, panel.ActiveSection);
            BuildActiveSection(panel);
        });
        button.text = label;
        button.tooltip = "Show the " + label + " section.";
        button.style.flexShrink = 0f;
        button.style.minWidth = ResolveSectionButtonWidth(sectionType);
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Resolves a stable minimum width for Settings Manager section buttons.
    /// </summary>
    /// <param name="sectionType">Section represented by the selector button.</param>
    /// <returns>Minimum width that keeps the label readable before wrapping to a new row.</returns>
    private static float ResolveSectionButtonWidth(GameSettingsManagerPresetsPanel.DetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case GameSettingsManagerPresetsPanel.DetailsSectionType.Gameplay:
                return 92f;
            case GameSettingsManagerPresetsPanel.DetailsSectionType.ControllerNavigation:
                return 172f;
            case GameSettingsManagerPresetsPanel.DetailsSectionType.Validation:
                return 88f;
            case GameSettingsManagerPresetsPanel.DetailsSectionType.Audio:
                return 72f;
            default:
                return 84f;
        }
    }

    /// <summary>
    /// Creates a styled section container and registers its heading for recolor utilities.
    /// </summary>
    /// <param name="panel">Owning panel with active details content root.</param>
    /// <param name="title">Section title.</param>
    /// <returns>Section container.</returns>
    private static VisualElement CreateSection(GameSettingsManagerPresetsPanel panel, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label label = new Label(title);
        label.tooltip = "Section header: " + title + ".";
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(label, "NashCore.GameManagement.Settings." + title);
        section.Add(label);
        panel.SectionContentRoot.Add(section);
        return section;
    }

    /// <summary>
    /// Creates a foldout used as a themed sub-section inside a Settings panel.
    /// </summary>
    /// <param name="title">Foldout title.</param>
    /// <param name="tooltip">Foldout tooltip.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreateFoldout(string title, string tooltip)
    {
        Foldout foldout = new Foldout();
        foldout.text = title;
        foldout.tooltip = tooltip;
        foldout.value = true;
        return foldout;
    }

    /// <summary>
    /// Adds a warning box for missing serialized settings roots.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="message">Warning text shown to the user.</param>
    private static void AddMissingSettingsBox(VisualElement parent, string message)
    {
        HelpBox missingSettingsBox = new HelpBox(message, HelpBoxMessageType.Error);
        parent.Add(missingSettingsBox);
    }
    #endregion

    #region Property Helpers
    /// <summary>
    /// Adds one bound text field and marks the draft dirty on edit.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="label">Display label.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="refreshList">True when list labels should update after change.</param>
    /// <param name="multiline">True when multiline editing is enabled.</param>
    private static void AddBoundTextField(GameSettingsManagerPresetsPanel panel,
                                          VisualElement parent,
                                          string label,
                                          string propertyName,
                                          bool refreshList,
                                          bool multiline)
    {
        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        TextField field = new TextField(label);
        field.tooltip = "Edit " + label + " for this Settings Manager preset.";
        field.isDelayed = true;
        field.multiline = multiline;
        field.BindProperty(property);
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Settings Manager Preset");
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();

            if (refreshList)
                panel.RefreshPresetList();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Adds one delayed string property field.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="property">Serialized string property.</param>
    /// <param name="label">Display label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    private static void AddDelayedStringProperty(GameSettingsManagerPresetsPanel panel,
                                                 VisualElement parent,
                                                 SerializedProperty property,
                                                 string label,
                                                 string tooltip)
    {
        if (property == null)
            return;

        TextField field = new TextField(label);
        field.tooltip = tooltip;
        field.isDelayed = true;
        field.SetValueWithoutNotify(property.stringValue);
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Settings Manager Text");
            panel.PresetSerializedObject.Update();
            property.stringValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Adds one boolean toggle property.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="property">Serialized boolean property.</param>
    /// <param name="label">Display label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <param name="rebuildOnChange">True when dependent controls must refresh immediately.</param>
    private static void AddBooleanToggleProperty(GameSettingsManagerPresetsPanel panel,
                                                 VisualElement parent,
                                                 SerializedProperty property,
                                                 string label,
                                                 string tooltip,
                                                 bool rebuildOnChange)
    {
        if (property == null)
            return;

        Toggle toggle = new Toggle(label);
        toggle.tooltip = tooltip;
        toggle.SetValueWithoutNotify(property.boolValue);
        toggle.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Settings Manager Toggle");
            panel.PresetSerializedObject.Update();
            property.boolValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();

            if (rebuildOnChange)
                panel.BuildActiveSection();
        });
        parent.Add(toggle);
    }

    /// <summary>
    /// Adds one integer property field.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="property">Serialized integer property.</param>
    /// <param name="label">Display label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    private static void AddIntegerProperty(GameSettingsManagerPresetsPanel panel,
                                           VisualElement parent,
                                           SerializedProperty property,
                                           string label,
                                           string tooltip)
    {
        if (property == null)
            return;

        IntegerField field = new IntegerField(label);
        field.tooltip = tooltip;
        field.SetValueWithoutNotify(property.intValue);
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Settings Manager Integer");
            panel.PresetSerializedObject.Update();
            property.intValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Adds one float slider property field.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="property">Serialized float property.</param>
    /// <param name="label">Display label.</param>
    /// <param name="lowValue">Lower slider value.</param>
    /// <param name="highValue">Upper slider value.</param>
    /// <param name="tooltip">Field tooltip.</param>
    private static void AddFloatSliderProperty(GameSettingsManagerPresetsPanel panel,
                                               VisualElement parent,
                                               SerializedProperty property,
                                               string label,
                                               float lowValue,
                                               float highValue,
                                               string tooltip)
    {
        if (property == null)
            return;

        Slider slider = new Slider(label, lowValue, highValue);
        slider.showInputField = true;
        slider.tooltip = tooltip;
        slider.SetValueWithoutNotify(property.floatValue);
        slider.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Settings Manager Slider");
            panel.PresetSerializedObject.Update();
            property.floatValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();
        });
        parent.Add(slider);
    }
    #endregion

    #endregion
}
