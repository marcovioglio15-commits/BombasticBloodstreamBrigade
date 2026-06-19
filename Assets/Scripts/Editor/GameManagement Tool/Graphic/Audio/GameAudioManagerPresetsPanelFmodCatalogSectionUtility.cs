using System;
using System.Collections.Generic;
using System.Linq;
using FMODUnity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Audio Manager FMOD catalog picker and applies selected Studio event paths to gameplay bindings.
/// </summary>
internal static class GameAudioManagerPresetsPanelFmodCatalogSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a compact FMOD event catalog selector for assigning paths to gameplay bindings.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="section">Parent Event Map section.</param>
    /// <param name="eventBindingsProperty">Serialized event bindings array.</param>
    public static void Build(GameAudioManagerPresetsPanel panel,
                             VisualElement section,
                             SerializedProperty eventBindingsProperty)
    {
        IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog = GameAudioFmodEventCatalogUtility.LoadEventCatalog(out string catalogWarning);

        VisualElement catalogRoot = new VisualElement();
        catalogRoot.style.marginTop = 6f;
        catalogRoot.style.marginBottom = 8f;

        Label titleLabel = new Label("FMOD Event Catalog");
        titleLabel.tooltip = "FMOD events discovered from Assets/BBB_FMOD/Metadata.";
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        catalogRoot.Add(titleLabel);

        if (!string.IsNullOrWhiteSpace(catalogWarning))
            catalogRoot.Add(new HelpBox(catalogWarning, HelpBoxMessageType.Warning));

        if (eventCatalog.Count <= 0)
        {
            catalogRoot.Add(new HelpBox("No FMOD events were discovered from the project metadata.", HelpBoxMessageType.Info));
            section.Add(catalogRoot);
            return;
        }

        List<string> bindingChoices = BuildBindingChoices(eventBindingsProperty);
        List<string> eventPathChoices = BuildEventPathChoices(eventCatalog);

        if (bindingChoices.Count <= 0 || eventPathChoices.Count <= 0)
        {
            catalogRoot.Add(new HelpBox("FMOD event assignment is unavailable because no selectable bindings or events were found.", HelpBoxMessageType.Info));
            section.Add(catalogRoot);
            return;
        }

        BuildCatalogControls(panel,
                             catalogRoot,
                             eventBindingsProperty,
                             eventCatalog,
                             bindingChoices,
                             eventPathChoices);
        section.Add(catalogRoot);
    }
    #endregion

    #region Control Builders
    /// <summary>
    /// Adds dropdowns and actions for choosing one gameplay binding and one FMOD event path.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="catalogRoot">Root visual element for catalog controls.</param>
    /// <param name="eventBindingsProperty">Serialized event bindings array.</param>
    /// <param name="eventCatalog">FMOD event catalog.</param>
    /// <param name="bindingChoices">Dropdown labels for gameplay bindings.</param>
    /// <param name="eventPathChoices">Dropdown labels for FMOD event paths.</param>
    private static void BuildCatalogControls(GameAudioManagerPresetsPanel panel,
                                             VisualElement catalogRoot,
                                             SerializedProperty eventBindingsProperty,
                                             IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog,
                                             List<string> bindingChoices,
                                             List<string> eventPathChoices)
    {
        int selectedBindingIndex = 0;
        int selectedEventIndex = ResolveCatalogIndexForBinding(eventBindingsProperty, selectedBindingIndex, eventCatalog);

        if (selectedEventIndex < 0)
            selectedEventIndex = 0;

        DropdownField bindingDropdown = new DropdownField("Gameplay Binding", bindingChoices, selectedBindingIndex);
        bindingDropdown.tooltip = "Gameplay binding that will receive the selected FMOD event path.";
        catalogRoot.Add(bindingDropdown);

        DropdownField eventDropdown = new DropdownField("FMOD Event", eventPathChoices, selectedEventIndex);
        eventDropdown.tooltip = "FMOD event path read from Studio metadata.";
        catalogRoot.Add(eventDropdown);

        Label selectedEventInfoLabel = new Label();
        selectedEventInfoLabel.tooltip = "Bank, folder and FMOD metadata ID for the selected event.";
        UpdateFmodEventInfoLabel(selectedEventInfoLabel, eventDropdown.value, eventCatalog);
        catalogRoot.Add(selectedEventInfoLabel);

        bindingDropdown.RegisterValueChangedCallback(evt =>
        {
            int bindingIndex = Mathf.Max(0, bindingDropdown.index);
            int catalogIndex = ResolveCatalogIndexForBinding(eventBindingsProperty, bindingIndex, eventCatalog);

            if (catalogIndex < 0)
                catalogIndex = 0;

            eventDropdown.SetValueWithoutNotify(eventPathChoices[catalogIndex]);
            UpdateFmodEventInfoLabel(selectedEventInfoLabel, eventDropdown.value, eventCatalog);
        });

        eventDropdown.RegisterValueChangedCallback(evt =>
        {
            UpdateFmodEventInfoLabel(selectedEventInfoLabel, evt.newValue, eventCatalog);
        });

        AddCatalogActions(panel, catalogRoot, bindingDropdown, eventDropdown);
    }

    /// <summary>
    /// Adds catalog action buttons that write or copy the selected FMOD path.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="catalogRoot">Root visual element for catalog controls.</param>
    /// <param name="bindingDropdown">Dropdown selecting the gameplay binding.</param>
    /// <param name="eventDropdown">Dropdown selecting the FMOD event path.</param>
    private static void AddCatalogActions(GameAudioManagerPresetsPanel panel,
                                          VisualElement catalogRoot,
                                          DropdownField bindingDropdown,
                                          DropdownField eventDropdown)
    {
        Toolbar actionToolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(actionToolbar);

        Button applyButton = new Button(() =>
        {
            ApplyFmodEventPathToBinding(panel, bindingDropdown.index, eventDropdown.value);
        });
        applyButton.text = "Apply Event Path";
        applyButton.tooltip = "Writes the selected FMOD event path into the selected gameplay binding.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(applyButton, 140f);
        actionToolbar.Add(applyButton);

        Button copyButton = new Button(() =>
        {
            EditorGUIUtility.systemCopyBuffer = eventDropdown.value ?? string.Empty;
        });
        copyButton.text = "Copy Path";
        copyButton.tooltip = "Copies the selected FMOD event path to the editor clipboard.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(copyButton, 92f);
        actionToolbar.Add(copyButton);

        catalogRoot.Add(actionToolbar);
    }
    #endregion

    #region Choice Builders
    /// <summary>
    /// Builds dropdown labels for all serialized gameplay bindings.
    /// </summary>
    /// <param name="eventBindingsProperty">Serialized event bindings array.</param>
    /// <returns>Dropdown labels.</returns>
    private static List<string> BuildBindingChoices(SerializedProperty eventBindingsProperty)
    {
        List<string> choices = new List<string>();

        for (int index = 0; index < eventBindingsProperty.arraySize; index++)
        {
            SerializedProperty bindingProperty = eventBindingsProperty.GetArrayElementAtIndex(index);
            choices.Add((index + 1).ToString("00") + " - " + ResolveBindingLabel(bindingProperty, index));
        }

        return choices;
    }

    /// <summary>
    /// Builds dropdown path choices from FMOD event catalog entries.
    /// </summary>
    /// <param name="eventCatalog">FMOD event catalog.</param>
    /// <returns>Event path labels.</returns>
    private static List<string> BuildEventPathChoices(IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog)
    {
        List<string> choices = new List<string>();

        for (int index = 0; index < eventCatalog.Count; index++)
            choices.Add(eventCatalog[index].EventPath);

        return choices;
    }
    #endregion

    #region Binding Matching
    /// <summary>
    /// Resolves a readable label for one serialized binding.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding element.</param>
    /// <param name="bindingIndex">Fallback binding index.</param>
    /// <returns>Readable binding label.</returns>
    private static string ResolveBindingLabel(SerializedProperty bindingProperty, int bindingIndex)
    {
        SerializedProperty eventIdProperty = bindingProperty.FindPropertyRelative("eventId");
        SerializedProperty eventCodeProperty = bindingProperty.FindPropertyRelative("eventCode");
        string idLabel = ResolveEventIdLabel(eventIdProperty, bindingIndex);

        if (eventCodeProperty != null && !string.IsNullOrWhiteSpace(eventCodeProperty.stringValue))
            return idLabel + " - " + eventCodeProperty.stringValue;

        SerializedProperty displayNameProperty = bindingProperty.FindPropertyRelative("displayName");

        if (displayNameProperty != null && !string.IsNullOrWhiteSpace(displayNameProperty.stringValue))
            return idLabel + " - " + displayNameProperty.stringValue;

        return idLabel;
    }

    /// <summary>
    /// Resolves the FMOD catalog index for the currently authored path on one binding.
    /// </summary>
    /// <param name="eventBindingsProperty">Serialized event bindings array.</param>
    /// <param name="bindingIndex">Binding index.</param>
    /// <param name="eventCatalog">FMOD event catalog.</param>
    /// <returns>Catalog index, or -1 when the authored path is empty or absent from the catalog.</returns>
    private static int ResolveCatalogIndexForBinding(SerializedProperty eventBindingsProperty,
                                                     int bindingIndex,
                                                     IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog)
    {
        if (bindingIndex < 0 || bindingIndex >= eventBindingsProperty.arraySize)
            return -1;

        SerializedProperty bindingProperty = eventBindingsProperty.GetArrayElementAtIndex(bindingIndex);
        SerializedProperty eventPathProperty = bindingProperty.FindPropertyRelative("eventPath");

        if (eventPathProperty == null || string.IsNullOrWhiteSpace(eventPathProperty.stringValue))
            return ResolveSuggestedCatalogIndexForBinding(bindingProperty, eventCatalog);

        int authoredPathIndex = FindEventCatalogIndex(eventCatalog, eventPathProperty.stringValue);

        if (authoredPathIndex >= 0)
            return authoredPathIndex;

        return ResolveSuggestedCatalogIndexForBinding(bindingProperty, eventCatalog);
    }

    /// <summary>
    /// Finds a likely FMOD event for a binding by comparing event code and display name tokens.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding element.</param>
    /// <param name="eventCatalog">FMOD event catalog.</param>
    /// <returns>Catalog index, or -1 when no likely match exists.</returns>
    private static int ResolveSuggestedCatalogIndexForBinding(SerializedProperty bindingProperty,
                                                              IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog)
    {
        List<string> bindingKeys = new List<string>();
        AddNormalizedBindingKey(bindingKeys, bindingProperty.FindPropertyRelative("eventCode"));
        AddNormalizedBindingKey(bindingKeys, bindingProperty.FindPropertyRelative("displayName"));

        if (bindingKeys.Count <= 0)
            return -1;

        for (int catalogIndex = 0; catalogIndex < eventCatalog.Count; catalogIndex++)
        {
            string eventNameKey = NormalizeEventKey(eventCatalog[catalogIndex].EventName);

            if (string.IsNullOrWhiteSpace(eventNameKey))
                continue;

            for (int keyIndex = 0; keyIndex < bindingKeys.Count; keyIndex++)
            {
                string bindingKey = bindingKeys[keyIndex];

                if (string.Equals(bindingKey, eventNameKey, StringComparison.Ordinal))
                    return catalogIndex;

                if (bindingKey.Contains(eventNameKey) || eventNameKey.Contains(bindingKey))
                    return catalogIndex;
            }
        }

        return -1;
    }

    /// <summary>
    /// Adds one normalized serialized string value to a matching key list.
    /// </summary>
    /// <param name="keys">Mutable normalized key list.</param>
    /// <param name="property">Serialized string property.</param>
    private static void AddNormalizedBindingKey(List<string> keys, SerializedProperty property)
    {
        if (property == null || string.IsNullOrWhiteSpace(property.stringValue))
            return;

        string normalizedKey = NormalizeEventKey(property.stringValue);

        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        if (!keys.Contains(normalizedKey))
            keys.Add(normalizedKey);
    }

    /// <summary>
    /// Normalizes event names for loose catalog matching.
    /// </summary>
    /// <param name="value">Source event name.</param>
    /// <returns>Lowercase alphanumeric event key.</returns>
    private static string NormalizeEventKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
    #endregion

    #region Event Info
    /// <summary>
    /// Finds one event path in the FMOD catalog.
    /// </summary>
    /// <param name="eventCatalog">FMOD event catalog.</param>
    /// <param name="eventPath">Event path to find.</param>
    /// <returns>Catalog index, or -1 when not found.</returns>
    private static int FindEventCatalogIndex(IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog, string eventPath)
    {
        for (int index = 0; index < eventCatalog.Count; index++)
        {
            if (string.Equals(eventCatalog[index].EventPath, eventPath, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Updates the selected FMOD event metadata label.
    /// </summary>
    /// <param name="label">Label to update.</param>
    /// <param name="eventPath">Selected event path.</param>
    /// <param name="eventCatalog">FMOD event catalog.</param>
    private static void UpdateFmodEventInfoLabel(Label label,
                                                 string eventPath,
                                                 IReadOnlyList<GameAudioFmodEventCatalogEntry> eventCatalog)
    {
        int eventIndex = FindEventCatalogIndex(eventCatalog, eventPath);

        if (eventIndex < 0)
        {
            label.text = "Selected path is not present in the current FMOD metadata.";
            return;
        }

        GameAudioFmodEventCatalogEntry entry = eventCatalog[eventIndex];
        string bankText = string.IsNullOrWhiteSpace(entry.BankName) ? "<No Bank>" : entry.BankName;
        string folderText = string.IsNullOrWhiteSpace(entry.FolderPath) ? "<Root>" : entry.FolderPath;
        label.text = "Bank: " + bankText + " | Folder: " + folderText + " | ID: " + entry.EventId;
    }

    /// <summary>
    /// Resolves a serialized GameAudioEventId as its enum name for compact Audio Manager lists.
    /// </summary>
    /// <param name="eventIdProperty">Serialized enum property.</param>
    /// <param name="bindingIndex">Fallback binding index kept for call-site compatibility.</param>
    /// <returns>Readable event ID label.</returns>
    private static string ResolveEventIdLabel(SerializedProperty eventIdProperty, int bindingIndex)
    {
        if (eventIdProperty == null)
            return "Event ID <Missing>";

        GameAudioEventId eventId = (GameAudioEventId)eventIdProperty.intValue;
        return eventId.ToString();
    }
    #endregion

    #region Apply Actions
    /// <summary>
    /// Applies a selected FMOD event path to one serialized gameplay binding.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="bindingIndex">Binding index to edit.</param>
    /// <param name="eventPath">FMOD event path to write.</param>
    private static void ApplyFmodEventPathToBinding(GameAudioManagerPresetsPanel panel, int bindingIndex, string eventPath)
    {
        if (panel == null || panel.SelectedPreset == null || panel.PresetSerializedObject == null)
            return;

        if (bindingIndex < 0 || string.IsNullOrWhiteSpace(eventPath))
            return;

        Undo.RecordObject(panel.SelectedPreset, "Apply FMOD Event Path");
        panel.PresetSerializedObject.Update();
        SerializedProperty eventBindingsProperty = panel.PresetSerializedObject.FindProperty("eventBindings");

        if (eventBindingsProperty == null || bindingIndex >= eventBindingsProperty.arraySize)
            return;

        SerializedProperty bindingProperty = eventBindingsProperty.GetArrayElementAtIndex(bindingIndex);
        SerializedProperty eventPathProperty = bindingProperty.FindPropertyRelative("eventPath");

        if (eventPathProperty == null)
            return;

        eventPathProperty.stringValue = eventPath;
        panel.PresetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(panel.SelectedPreset);
        GameManagementDraftSession.MarkDirty();
        panel.BuildActiveSection();
    }
    #endregion

    #endregion
}

/// <summary>
/// Reads the FMOD editor event cache and exposes authored event paths for tool panels.
/// </summary>
internal static class GameAudioFmodEventCatalogUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the current FMOD event catalog from the FMOD editor cache.
    /// </summary>
    /// <param name="warning">Optional warning describing missing or stale FMOD cache state.</param>
    /// <returns>Sorted FMOD event entries.</returns>
    public static IReadOnlyList<GameAudioFmodEventCatalogEntry> LoadEventCatalog(out string warning)
    {
        warning = string.Empty;
        List<EditorEventRef> fmodEvents;

        try
        {
            fmodEvents = EventManager.Events;
        }
        catch (Exception exception)
        {
            warning = "FMOD event cache could not be read: " + exception.Message;
            return Array.Empty<GameAudioFmodEventCatalogEntry>();
        }

        if (fmodEvents == null)
        {
            warning = "FMOD event cache is unavailable. Use FMOD/Refresh Banks to rebuild it.";
            return Array.Empty<GameAudioFmodEventCatalogEntry>();
        }

        List<GameAudioFmodEventCatalogEntry> entries = new List<GameAudioFmodEventCatalogEntry>();

        for (int eventIndex = 0; eventIndex < fmodEvents.Count; eventIndex++)
        {
            EditorEventRef eventRef = fmodEvents[eventIndex];

            if (eventRef == null || string.IsNullOrWhiteSpace(eventRef.Path))
                continue;

            if (!eventRef.Path.StartsWith("event:/", StringComparison.Ordinal))
                continue;

            entries.Add(new GameAudioFmodEventCatalogEntry(eventRef.Path,
                                                           ResolveEventName(eventRef.Path),
                                                           ResolveFolderPath(eventRef.Path),
                                                           ResolveBankNames(eventRef),
                                                           eventRef.Guid.ToString()));
        }

        entries.Sort((left, right) => string.Compare(left.EventPath, right.EventPath, StringComparison.OrdinalIgnoreCase));

        if (entries.Count <= 0)
            warning = "FMOD event cache contains no event:/ entries. Use FMOD/Refresh Banks after building banks in Studio.";

        return entries;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the event leaf name from one FMOD event path.
    /// </summary>
    /// <param name="eventPath">FMOD event path.</param>
    /// <returns>Event leaf name.</returns>
    private static string ResolveEventName(string eventPath)
    {
        int slashIndex = eventPath.LastIndexOf('/');

        if (slashIndex < 0 || slashIndex >= eventPath.Length - 1)
            return eventPath;

        return eventPath.Substring(slashIndex + 1);
    }

    /// <summary>
    /// Resolves the folder path from one FMOD event path.
    /// </summary>
    /// <param name="eventPath">FMOD event path.</param>
    /// <returns>Folder path without the event leaf name.</returns>
    private static string ResolveFolderPath(string eventPath)
    {
        const string EventPrefix = "event:/";

        if (!eventPath.StartsWith(EventPrefix, StringComparison.Ordinal))
            return string.Empty;

        string relativePath = eventPath.Substring(EventPrefix.Length);
        int slashIndex = relativePath.LastIndexOf('/');

        if (slashIndex <= 0)
            return string.Empty;

        return relativePath.Substring(0, slashIndex);
    }

    /// <summary>
    /// Resolves comma-separated bank names from one FMOD editor event reference.
    /// </summary>
    /// <param name="eventRef">FMOD editor event reference.</param>
    /// <returns>Comma-separated bank names.</returns>
    private static string ResolveBankNames(EditorEventRef eventRef)
    {
        if (eventRef.Banks == null || eventRef.Banks.Count <= 0)
            return string.Empty;

        List<string> bankNames = new List<string>();

        for (int bankIndex = 0; bankIndex < eventRef.Banks.Count; bankIndex++)
        {
            EditorBankRef bankRef = eventRef.Banks[bankIndex];

            if (bankRef == null || string.IsNullOrWhiteSpace(bankRef.Name))
                continue;

            bankNames.Add(bankRef.Name);
        }

        return string.Join(", ", bankNames);
    }
    #endregion

    #endregion
}

/// <summary>
/// One FMOD event discovered from the editor cache.
/// </summary>
internal readonly struct GameAudioFmodEventCatalogEntry
{
    #region Fields
    public readonly string EventPath;
    public readonly string EventName;
    public readonly string FolderPath;
    public readonly string BankName;
    public readonly string EventId;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable FMOD catalog entry for editor dropdowns.
    /// </summary>
    /// <param name="eventPath">Full FMOD event path.</param>
    /// <param name="eventName">Leaf event name.</param>
    /// <param name="folderPath">FMOD folder path without the leaf event name.</param>
    /// <param name="bankName">Comma-separated bank names that contain the event.</param>
    /// <param name="eventId">FMOD metadata GUID as a string.</param>
    public GameAudioFmodEventCatalogEntry(string eventPath,
                                          string eventName,
                                          string folderPath,
                                          string bankName,
                                          string eventId)
    {
        EventPath = eventPath;
        EventName = eventName;
        FolderPath = folderPath;
        BankName = bankName;
        EventId = eventId;
    }
    #endregion
}
