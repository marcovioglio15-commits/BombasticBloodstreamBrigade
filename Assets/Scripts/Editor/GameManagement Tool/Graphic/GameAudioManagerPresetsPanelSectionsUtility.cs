using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds Audio Manager preset detail sections, validation output and event-map maintenance actions.
/// </summary>
internal static class GameAudioManagerPresetsPanelSectionsUtility
{
    #region Constants
    private const string ActiveSectionStateKey = "NashCore.GameManagement.Audio.ActiveSection";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the persisted active Audio Manager details section.
    /// </summary>
    /// <returns>Persisted section value or Metadata when none exists.</returns>
    public static GameAudioManagerPresetsPanel.DetailsSectionType LoadActiveSection()
    {
        return ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, GameAudioManagerPresetsPanel.DetailsSectionType.Metadata);
    }

    /// <summary>
    /// Selects one Audio Manager preset and rebuilds details.
    /// </summary>
    /// <param name="panel">Owning panel with detail roots.</param>
    /// <param name="preset">Preset to select, or null to clear details.</param>
    public static void SelectPreset(GameAudioManagerPresetsPanel panel, GameAudioManagerPreset preset)
    {
        if (panel == null || panel.DetailsRoot == null)
            return;

        panel.SelectedPreset = preset;
        panel.DetailsRoot.Clear();

        if (panel.PresetListView != null && panel.SelectedPreset != null)
        {
            int selectedIndex = panel.FilteredPresets.IndexOf(panel.SelectedPreset);

            if (selectedIndex >= 0)
                panel.PresetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        if (panel.SelectedPreset == null)
        {
            panel.DetailsRoot.Add(new Label("Select or create an Audio Manager preset to edit."));
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
    /// Rebuilds the currently selected Audio Manager details section.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    public static void BuildActiveSection(GameAudioManagerPresetsPanel panel)
    {
        if (panel == null || panel.SectionContentRoot == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.Update();
        panel.SectionContentRoot.Clear();

        switch (panel.ActiveSection)
        {
            case GameAudioManagerPresetsPanel.DetailsSectionType.Playback:
                BuildPropertySection(panel, "Playback", "playbackSettings", "Global runtime playback controls.");
                break;
            case GameAudioManagerPresetsPanel.DetailsSectionType.Routing:
                BuildPropertySection(panel, "FMOD Routing", "routingSettings", "FMOD bus paths and default mix values.");
                break;
            case GameAudioManagerPresetsPanel.DetailsSectionType.BackgroundMusic:
                BuildBackgroundMusicSection(panel);
                break;
            case GameAudioManagerPresetsPanel.DetailsSectionType.EventMap:
                BuildEventMapSection(panel);
                break;
            case GameAudioManagerPresetsPanel.DetailsSectionType.RateLimits:
                BuildRateLimitsSection(panel);
                break;
            case GameAudioManagerPresetsPanel.DetailsSectionType.Validation:
                BuildValidationSection(panel);
                break;
            default:
                BuildMetadataSection(panel);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.SectionContentRoot);
    }

    /// <summary>
    /// Marks the selected Audio Manager preset dirty in the draft session.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    public static void MarkSelectedPresetDirty(GameAudioManagerPresetsPanel panel)
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
    /// Builds metadata fields for the selected Audio Manager preset.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildMetadataSection(GameAudioManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Preset Details");
        AddBoundTextField(panel, section, "Preset Name", "presetName", true, false);
        AddBoundTextField(panel, section, "Version", "version", false, false);
        AddBoundTextField(panel, section, "Description", "description", false, true);

        SerializedProperty idProperty = panel.PresetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        PropertyField idField = new PropertyField(idProperty, "Preset ID");
        idField.tooltip = "Stable ID used by Game Management Tool for this Audio Manager preset.";
        idField.BindProperty(idProperty);
        idField.SetEnabled(false);
        section.Add(idField);
    }

    /// <summary>
    /// Builds a details section for one serialized property root.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    /// <param name="title">Section title.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="tooltip">Tooltip applied to the property field.</param>
    private static void BuildPropertySection(GameAudioManagerPresetsPanel panel, string title, string propertyName, string tooltip)
    {
        VisualElement section = CreateSection(panel, title);
        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.isExpanded = true;
        PropertyField field = new PropertyField(property);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt => panel.MarkSelectedPresetDirty());
        section.Add(field);
    }

    /// <summary>
    /// Builds background music controls with dependent options shown only while music management is enabled.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildBackgroundMusicSection(GameAudioManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Background Music");
        SerializedProperty musicProperty = panel.PresetSerializedObject.FindProperty("backgroundMusicSettings");

        if (musicProperty == null)
        {
            panel.SelectedPreset.EnsureInitialized();
            EditorUtility.SetDirty(panel.SelectedPreset);
            panel.PresetSerializedObject.Update();
            musicProperty = panel.PresetSerializedObject.FindProperty("backgroundMusicSettings");
        }

        if (musicProperty == null)
        {
            HelpBox missingSettingsBox = new HelpBox("Background music settings could not be resolved on this Audio Manager preset.", HelpBoxMessageType.Error);
            section.Add(missingSettingsBox);
            return;
        }

        SerializedProperty enabledProperty = musicProperty.FindPropertyRelative("enabled");

        if (enabledProperty == null)
            return;

        AddBooleanToggleProperty(panel,
                                 section,
                                 enabledProperty,
                                 "Enabled",
                                 "Enables background music management from the Audio Manager preset.",
                                 true);
        AddBooleanToggleProperty(panel,
                                 section,
                                 musicProperty.FindPropertyRelative("stopWhenDisabled"),
                                 "Stop When Disabled",
                                 "Stops the currently playing background music when this section or global audio playback is disabled.",
                                 false);

        if (!enabledProperty.boolValue)
        {
            HelpBox disabledBox = new HelpBox("Background music management is disabled. Runtime will stop existing music when Stop When Disabled is enabled.", HelpBoxMessageType.Info);
            section.Add(disabledBox);
            return;
        }

        AddDelayedStringProperty(panel, section, musicProperty.FindPropertyRelative("eventPath"), "FMOD Event Path", "FMOD event path for the background music loop, for example event:/Music/Stage01.");
        AddDelayedStringProperty(panel, section, musicProperty.FindPropertyRelative("bankName"), "FMOD Bank Name", "FMOD bank containing the background music event, for example BankMusic.");
        AddFloatSliderProperty(panel, section, musicProperty.FindPropertyRelative("volume"), "Volume", 0f, 2f, "Volume scalar applied to background music before routing music volume.");
        SerializedProperty autoStartProperty = musicProperty.FindPropertyRelative("autoStart");

        AddBooleanToggleProperty(panel,
                                 section,
                                 autoStartProperty,
                                 "Auto Start",
                                 "Starts background music automatically when runtime audio becomes available.",
                                 true);

        if (autoStartProperty != null && autoStartProperty.boolValue)
        {
            AddBooleanToggleProperty(panel,
                                     section,
                                     musicProperty.FindPropertyRelative("restartWhenPathChanges"),
                                     "Restart When Path Changes",
                                     "Restarts background music when the event path changes after rebake or config reload.",
                                     false);
        }
    }

    /// <summary>
    /// Builds gameplay event to FMOD event-path mapping controls.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    private static void BuildEventMapSection(GameAudioManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Event Sound Map");
        section.Add(BuildEventMapToolbar(panel));

        SerializedProperty eventBindingsProperty = panel.PresetSerializedObject.FindProperty("eventBindings");

        if (eventBindingsProperty == null)
            return;

        if (eventBindingsProperty.arraySize <= 0)
        {
            HelpBox emptyBindingsBox = new HelpBox("No audio event bindings are configured. Use Add Missing Defaults to populate the FMOD event map.", HelpBoxMessageType.Warning);
            section.Add(emptyBindingsBox);
            return;
        }

        eventBindingsProperty.isExpanded = true;
        PropertyField eventBindingsField = new PropertyField(eventBindingsProperty, "FMOD Event Bindings");
        eventBindingsField.tooltip = "Gameplay event entries and FMOD event paths baked into ECS.";
        eventBindingsField.BindProperty(eventBindingsProperty);
        eventBindingsField.RegisterCallback<SerializedPropertyChangeEvent>(evt => panel.MarkSelectedPresetDirty());
        section.Add(eventBindingsField);
    }

    /// <summary>
    /// Builds focused rate-limit controls for every event binding.
    /// </summary>
    /// <param name="panel">Owning panel with serialized event bindings.</param>
    private static void BuildRateLimitsSection(GameAudioManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Rate Limits");
        SerializedProperty eventBindingsProperty = panel.PresetSerializedObject.FindProperty("eventBindings");

        if (eventBindingsProperty == null)
            return;

        if (eventBindingsProperty.arraySize <= 0)
        {
            HelpBox emptyBindingsBox = new HelpBox("No event bindings are configured, so there are no rate limits to edit.", HelpBoxMessageType.Info);
            section.Add(emptyBindingsBox);
            return;
        }

        for (int index = 0; index < eventBindingsProperty.arraySize; index++)
        {
            SerializedProperty bindingProperty = eventBindingsProperty.GetArrayElementAtIndex(index);
            SerializedProperty eventCodeProperty = bindingProperty.FindPropertyRelative("eventCode");
            SerializedProperty rateLimitProperty = bindingProperty.FindPropertyRelative("rateLimit");

            if (rateLimitProperty == null)
                continue;

            string foldoutTitle = eventCodeProperty != null && !string.IsNullOrWhiteSpace(eventCodeProperty.stringValue)
                ? eventCodeProperty.stringValue
                : "Event Binding " + index;

            Foldout foldout = new Foldout();
            foldout.text = foldoutTitle;
            foldout.tooltip = "Rate cap for " + foldoutTitle + ".";
            foldout.value = true;

            rateLimitProperty.isExpanded = true;
            PropertyField field = new PropertyField(rateLimitProperty, "Rate Limit");
            field.BindProperty(rateLimitProperty);
            field.RegisterCallback<SerializedPropertyChangeEvent>(evt => panel.MarkSelectedPresetDirty());
            foldout.Add(field);
            section.Add(foldout);
        }
    }

    /// <summary>
    /// Builds non-mutating validation warning output.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset and warning buffer.</param>
    private static void BuildValidationSection(GameAudioManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Validation");
        Button refreshButton = new Button(panel.BuildActiveSection);
        refreshButton.text = "Refresh";
        refreshButton.tooltip = "Refresh non-mutating validation warnings.";
        section.Add(refreshButton);

        GameAudioManagerPresetValidationUtility.CollectWarnings(panel.SelectedPreset, panel.ValidationWarnings);

        if (panel.ValidationWarnings.Count <= 0)
        {
            Label cleanLabel = new Label("No warnings.");
            cleanLabel.tooltip = "The selected Audio Manager preset has no validation warnings.";
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

    #region Section Helpers
    /// <summary>
    /// Builds buttons for Audio Manager detail sections.
    /// </summary>
    /// <param name="panel">Owning panel that stores the active section.</param>
    /// <returns>Section button row.</returns>
    private static VisualElement BuildSectionButtons(GameAudioManagerPresetsPanel panel)
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;
        AddSectionButton(panel, buttonsRoot, GameAudioManagerPresetsPanel.DetailsSectionType.Metadata, "Metadata");
        AddSectionButton(panel, buttonsRoot, GameAudioManagerPresetsPanel.DetailsSectionType.Playback, "Playback");
        AddSectionButton(panel, buttonsRoot, GameAudioManagerPresetsPanel.DetailsSectionType.Routing, "FMOD Routing");
        AddSectionButton(panel, buttonsRoot, GameAudioManagerPresetsPanel.DetailsSectionType.BackgroundMusic, "Background Music");
        AddSectionButton(panel, buttonsRoot, GameAudioManagerPresetsPanel.DetailsSectionType.EventMap, "Event Sound Map");
        AddSectionButton(panel, buttonsRoot, GameAudioManagerPresetsPanel.DetailsSectionType.RateLimits, "Rate Limits");
        AddSectionButton(panel, buttonsRoot, GameAudioManagerPresetsPanel.DetailsSectionType.Validation, "Validation");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one Audio Manager detail section selector button.
    /// </summary>
    /// <param name="panel">Owning panel receiving the selected section.</param>
    /// <param name="parent">Parent button row.</param>
    /// <param name="sectionType">Section activated by the button.</param>
    /// <param name="label">Visible label.</param>
    private static void AddSectionButton(GameAudioManagerPresetsPanel panel,
                                         VisualElement parent,
                                         GameAudioManagerPresetsPanel.DetailsSectionType sectionType,
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
    /// Builds event-map maintenance buttons.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <returns>Toolbar visual element.</returns>
    private static Toolbar BuildEventMapToolbar(GameAudioManagerPresetsPanel panel)
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button addMissingButton = new Button(() => AddMissingDefaultBindings(panel));
        addMissingButton.text = "Add Missing Defaults";
        addMissingButton.tooltip = "Add missing gameplay event bindings without changing authored FMOD paths.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(addMissingButton, 148f);
        toolbar.Add(addMissingButton);

        Button syncButton = new Button(() => SynchronizeDefaultIdentities(panel));
        syncButton.text = "Sync Default Names";
        syncButton.tooltip = "Synchronize default event names and descriptions without touching FMOD paths.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(syncButton, 140f);
        toolbar.Add(syncButton);

        Button resetButton = new Button(() => ResetEventMap(panel));
        resetButton.text = "Reset Event Map";
        resetButton.tooltip = "Rebuild the event map from defaults and discard authored event paths.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(resetButton, 120f);
        toolbar.Add(resetButton);
        return toolbar;
    }

    /// <summary>
    /// Resolves a stable minimum width for Audio Manager section buttons.
    /// </summary>
    /// <param name="sectionType">Section represented by the selector button.</param>
    /// <returns>Minimum width that keeps the label readable before wrapping to a new row.</returns>
    private static float ResolveSectionButtonWidth(GameAudioManagerPresetsPanel.DetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case GameAudioManagerPresetsPanel.DetailsSectionType.Routing:
                return 104f;
            case GameAudioManagerPresetsPanel.DetailsSectionType.BackgroundMusic:
                return 148f;
            case GameAudioManagerPresetsPanel.DetailsSectionType.EventMap:
                return 136f;
            case GameAudioManagerPresetsPanel.DetailsSectionType.RateLimits:
                return 88f;
            case GameAudioManagerPresetsPanel.DetailsSectionType.Validation:
                return 88f;
            case GameAudioManagerPresetsPanel.DetailsSectionType.Playback:
                return 84f;
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
    private static VisualElement CreateSection(GameAudioManagerPresetsPanel panel, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label label = new Label(title);
        label.tooltip = "Section header: " + title + ".";
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(label, "NashCore.GameManagement.Audio." + title);
        section.Add(label);
        panel.SectionContentRoot.Add(section);
        return section;
    }

    /// <summary>
    /// Adds one bound text field and marks the draft dirty on edit.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="label">Display label.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="refreshList">True when list labels should update after change.</param>
    /// <param name="multiline">True when multiline editing is enabled.</param>
    private static void AddBoundTextField(GameAudioManagerPresetsPanel panel,
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
        field.tooltip = "Edit " + label + " for this Audio Manager preset.";
        field.isDelayed = true;
        field.multiline = multiline;
        field.BindProperty(property);
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Audio Manager Preset");
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
    private static void AddDelayedStringProperty(GameAudioManagerPresetsPanel panel,
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
            Undo.RecordObject(panel.SelectedPreset, "Edit Background Music");
            panel.PresetSerializedObject.Update();
            property.stringValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Adds one explicit boolean toggle and optionally rebuilds the active section after edits.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="property">Serialized boolean property.</param>
    /// <param name="label">Display label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <param name="rebuildOnChange">True when dependent controls must refresh immediately.</param>
    private static void AddBooleanToggleProperty(GameAudioManagerPresetsPanel panel,
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
            Undo.RecordObject(panel.SelectedPreset, "Edit Background Music");
            panel.PresetSerializedObject.Update();
            property.boolValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel.SelectedPreset);
            GameManagementDraftSession.MarkDirty();

            if (rebuildOnChange)
                panel.BuildActiveSection();
        });
        parent.Add(toggle);
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
    private static void AddFloatSliderProperty(GameAudioManagerPresetsPanel panel,
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
            Undo.RecordObject(panel.SelectedPreset, "Edit Background Music");
            panel.PresetSerializedObject.Update();
            property.floatValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();
        });
        parent.Add(slider);
    }
    #endregion

    #region Event Map Actions
    /// <summary>
    /// Adds missing default event bindings to the selected preset.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    private static void AddMissingDefaultBindings(GameAudioManagerPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Add Missing Audio Defaults");
        panel.SelectedPreset.EnsureDefaultEventBindings();
        panel.MarkSelectedPresetDirty();
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Synchronizes default event identity labels and descriptions.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    private static void SynchronizeDefaultIdentities(GameAudioManagerPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Sync Audio Default Names");
        panel.SelectedPreset.SynchronizeDefaultEventIdentities();
        panel.MarkSelectedPresetDirty();
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Resets all event bindings to the current default catalog.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    private static void ResetEventMap(GameAudioManagerPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Reset Event Sound Map",
                                                     "Reset all event bindings to default identity rows and clear authored FMOD event paths?",
                                                     "Reset",
                                                     "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Reset Audio Event Map");
        panel.SelectedPreset.ResetEventBindingsToDefaults();
        panel.MarkSelectedPresetDirty();
        panel.BuildActiveSection();
    }
    #endregion

    #endregion
}
