using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds Settings Manager audio-preview event controls from the FMOD editor catalog.
/// </summary>
internal static class GameSettingsManagerPresetsPanelPreviewEventsUtility
{
    #region Constants
    private const string EmptyEventChoice = "<None>";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds preview event controls using the FMOD editor event catalog when available.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    /// <param name="section">Parent Audio section.</param>
    /// <param name="audioProperty">Serialized audio settings root.</param>
    public static void Build(GameSettingsManagerPresetsPanel panel,
                             VisualElement section,
                             SerializedProperty audioProperty)
    {
        Foldout foldout = CreateFoldout("Preview Events", "FMOD events played while runtime volume sliders are adjusted.");
        IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog = GameAudioFmodEventCatalogUtility.LoadEventCatalog(out string catalogWarning);

        if (!string.IsNullOrWhiteSpace(catalogWarning))
            foldout.Add(new HelpBox(catalogWarning, HelpBoxMessageType.Warning));

        bool masterPlaysAllPreviews = AddMasterPlaysAllToggle(panel, foldout, audioProperty);

        // The Master event only drives playback when it previews its own event, so its controls are shown only then.
        if (masterPlaysAllPreviews)
            foldout.Add(new HelpBox("Master previews the SFX and Music events together. Its own event is ignored in this mode.", HelpBoxMessageType.Info));
        else
            AddPreviewEventControls(panel,
                                    foldout,
                                    audioProperty.FindPropertyRelative("masterVolumePreview"),
                                    "Master Slider Preview",
                                    eventCatalog);

        AddPreviewEventControls(panel,
                                foldout,
                                audioProperty.FindPropertyRelative("sfxVolumePreview"),
                                "SFX Slider Preview",
                                eventCatalog);
        AddPreviewEventControls(panel,
                                foldout,
                                audioProperty.FindPropertyRelative("musicVolumePreview"),
                                "Music Slider Preview",
                                eventCatalog);
        section.Add(foldout);
    }
    #endregion

    #region Preview Event Controls
    /// <summary>
    /// Adds the Master "play all other previews" toggle and rebuilds the section so the Master event controls appear
    /// only while they are actually used.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent foldout.</param>
    /// <param name="audioProperty">Serialized audio settings root.</param>
    /// <returns>Current value of the Master "play all other previews" toggle.</returns>
    private static bool AddMasterPlaysAllToggle(GameSettingsManagerPresetsPanel panel,
                                                VisualElement parent,
                                                SerializedProperty audioProperty)
    {
        SerializedProperty property = audioProperty.FindPropertyRelative("masterPlaysAllPreviews");

        if (property == null)
            return false;

        Toggle toggle = new Toggle("Master Plays All Previews");
        toggle.tooltip = "When enabled the Master slider previews the SFX and Music events together instead of its own event. Recommended because a Master event matching the live background music is silently suppressed at runtime.";
        toggle.SetValueWithoutNotify(property.boolValue);
        toggle.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Master Preview Mode");
            panel.PresetSerializedObject.Update();
            property.boolValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();
            panel.BuildActiveSection();
        });
        parent.Add(toggle);
        return property.boolValue;
    }

    /// <summary>
    /// Adds direct fields and a catalog dropdown for one preview event reference.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent foldout.</param>
    /// <param name="previewProperty">Serialized preview event settings.</param>
    /// <param name="label">Display label for this preview slot.</param>
    /// <param name="eventCatalog">FMOD event catalog read from editor metadata.</param>
    private static void AddPreviewEventControls(GameSettingsManagerPresetsPanel panel,
                                                VisualElement parent,
                                                SerializedProperty previewProperty,
                                                string label,
                                                IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog)
    {
        if (previewProperty == null)
            return;

        Foldout foldout = CreateFoldout(label, "Preview event used by " + label + ".");
        SerializedProperty eventPathProperty = previewProperty.FindPropertyRelative("eventPath");
        SerializedProperty bankNameProperty = previewProperty.FindPropertyRelative("bankName");

        if (eventCatalog != null && eventCatalog.Count > 0)
            AddEventCatalogDropdown(panel, foldout, eventPathProperty, bankNameProperty, eventCatalog);
        else
            foldout.Add(new HelpBox("FMOD catalog is empty. Event Path and Bank Name can still be edited manually.", HelpBoxMessageType.Info));

        AddDelayedStringProperty(panel,
                                 foldout,
                                 eventPathProperty,
                                 "Event Path",
                                 "FMOD event path played while this slider is adjusted.");
        AddDelayedStringProperty(panel,
                                 foldout,
                                 bankNameProperty,
                                 "Bank Name",
                                 "FMOD bank loaded before the preview event is resolved. Leave empty only if another system already loads it.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Adds an FMOD event dropdown that writes event path and bank name together.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent foldout.</param>
    /// <param name="eventPathProperty">Serialized event path property.</param>
    /// <param name="bankNameProperty">Serialized bank name property.</param>
    /// <param name="eventCatalog">FMOD event catalog read from editor metadata.</param>
    private static void AddEventCatalogDropdown(GameSettingsManagerPresetsPanel panel,
                                                VisualElement parent,
                                                SerializedProperty eventPathProperty,
                                                SerializedProperty bankNameProperty,
                                                IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog)
    {
        if (eventPathProperty == null)
            return;

        List<string> choices = BuildEventPathChoices(eventCatalog);
        int selectedIndex = ResolveEventChoiceIndex(choices, eventPathProperty.stringValue);
        DropdownField dropdown = new DropdownField("FMOD Event", choices, selectedIndex);
        dropdown.tooltip = "Assign an FMOD event from the editor cache and copy its bank name.";
        parent.Add(dropdown);

        Label infoLabel = new Label();
        infoLabel.tooltip = "Bank, folder and FMOD metadata ID for the selected event.";
        UpdateEventInfoLabel(infoLabel, dropdown.value, eventCatalog);
        parent.Add(infoLabel);

        dropdown.RegisterValueChangedCallback(evt =>
        {
            AssignCatalogEvent(panel, eventPathProperty, bankNameProperty, evt.newValue, eventCatalog);
            UpdateEventInfoLabel(infoLabel, evt.newValue, eventCatalog);
        });
    }

    /// <summary>
    /// Assigns the selected catalog event to serialized event path and bank properties.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="eventPathProperty">Serialized event path property.</param>
    /// <param name="bankNameProperty">Serialized bank name property.</param>
    /// <param name="selectedPath">Selected dropdown value.</param>
    /// <param name="eventCatalog">FMOD event catalog read from editor metadata.</param>
    private static void AssignCatalogEvent(GameSettingsManagerPresetsPanel panel,
                                           SerializedProperty eventPathProperty,
                                           SerializedProperty bankNameProperty,
                                           string selectedPath,
                                           IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog)
    {
        if (panel == null || eventPathProperty == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Assign Settings Preview Event");
        panel.PresetSerializedObject.Update();

        if (string.Equals(selectedPath, EmptyEventChoice, System.StringComparison.Ordinal))
        {
            eventPathProperty.stringValue = string.Empty;

            if (bankNameProperty != null)
                bankNameProperty.stringValue = string.Empty;
        }
        else if (TryFindCatalogEntry(eventCatalog, selectedPath, out GameAudioFmodEventCatalogEntry entry))
        {
            eventPathProperty.stringValue = entry.EventPath;

            if (bankNameProperty != null)
                bankNameProperty.stringValue = ResolvePrimaryBankName(entry.BankName);
        }

        panel.PresetSerializedObject.ApplyModifiedProperties();
        panel.MarkSelectedPresetDirty();
    }
    #endregion

    #region Property Helpers
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
            Undo.RecordObject(panel.SelectedPreset, "Edit Settings Preview Event");
            panel.PresetSerializedObject.Update();
            property.stringValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();
        });
        parent.Add(field);
    }
    #endregion

    #region FMOD Catalog Helpers
    /// <summary>
    /// Builds dropdown choices for the FMOD event catalog.
    /// </summary>
    /// <param name="eventCatalog">FMOD event catalog read from editor metadata.</param>
    /// <returns>Dropdown choices with an empty first entry.</returns>
    private static List<string> BuildEventPathChoices(IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog)
    {
        List<string> choices = new List<string>();
        choices.Add(EmptyEventChoice);

        if (eventCatalog == null)
            return choices;

        for (int index = 0; index < eventCatalog.Count; index++)
            choices.Add(eventCatalog[index].EventPath);

        return choices;
    }

    /// <summary>
    /// Resolves the dropdown index for the current serialized event path.
    /// </summary>
    /// <param name="choices">Dropdown choices.</param>
    /// <param name="eventPath">Current serialized event path.</param>
    /// <returns>Matching choice index or zero when not found.</returns>
    private static int ResolveEventChoiceIndex(IReadOnlyList<string> choices, string eventPath)
    {
        if (choices == null || string.IsNullOrWhiteSpace(eventPath))
            return 0;

        for (int index = 0; index < choices.Count; index++)
        {
            if (!string.Equals(choices[index], eventPath, System.StringComparison.Ordinal))
                continue;

            return index;
        }

        return 0;
    }

    /// <summary>
    /// Updates the selected FMOD event info label.
    /// </summary>
    /// <param name="label">Label to update.</param>
    /// <param name="selectedPath">Selected dropdown event path.</param>
    /// <param name="eventCatalog">FMOD event catalog read from editor metadata.</param>
    private static void UpdateEventInfoLabel(Label label, string selectedPath, IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog)
    {
        if (label == null)
            return;

        if (!TryFindCatalogEntry(eventCatalog, selectedPath, out GameAudioFmodEventCatalogEntry entry))
        {
            label.text = "No FMOD event selected.";
            return;
        }

        label.text = "Bank: " + ResolvePrimaryBankName(entry.BankName) + " | Folder: " + entry.FolderPath + " | ID: " + entry.EventId;
    }

    /// <summary>
    /// Finds one FMOD catalog entry by path.
    /// </summary>
    /// <param name="eventCatalog">FMOD event catalog read from editor metadata.</param>
    /// <param name="eventPath">Event path to find.</param>
    /// <param name="entry">Matched entry when found.</param>
    /// <returns>True when the event path exists in the catalog.</returns>
    private static bool TryFindCatalogEntry(IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog,
                                            string eventPath,
                                            out GameAudioFmodEventCatalogEntry entry)
    {
        entry = default;

        if (eventCatalog == null || string.IsNullOrWhiteSpace(eventPath))
            return false;

        for (int index = 0; index < eventCatalog.Count; index++)
        {
            if (!string.Equals(eventCatalog[index].EventPath, eventPath, System.StringComparison.Ordinal))
                continue;

            entry = eventCatalog[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the first bank name from the catalog string because runtime bank loading expects one bank at a time.
    /// </summary>
    /// <param name="bankName">Catalog bank name string.</param>
    /// <returns>First trimmed bank name or an empty string.</returns>
    private static string ResolvePrimaryBankName(string bankName)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            return string.Empty;

        int separatorIndex = bankName.IndexOf(',');

        if (separatorIndex < 0)
            return bankName.Trim();

        return bankName.Substring(0, separatorIndex).Trim();
    }

    /// <summary>
    /// Creates a foldout used as a themed sub-section inside the preview event block.
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
    #endregion

    #endregion
}
