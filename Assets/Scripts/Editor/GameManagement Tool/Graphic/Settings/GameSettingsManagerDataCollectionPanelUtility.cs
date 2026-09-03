using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds context-sensitive automatic data-collection controls for one Settings Manager preset.
/// </summary>
internal static class GameSettingsManagerDataCollectionPanelUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds runtime-context, HTTPS, consent, Input Action, sampling, batching, and retry controls.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel with serialized preset state.</param>
    public static void Build(GameSettingsManagerPresetsPanel panel)
    {
        if (panel == null || panel.PresetSerializedObject == null || panel.SectionContentRoot == null)
            return;

        SerializedProperty settingsProperty = panel.PresetSerializedObject.FindProperty("dataCollectionSettings");
        VisualElement section = CreateSection("Data Collection",
                                              "Configures consent-aware telemetry, developer access, and the versioned alwaysdata HTTPS service.");
        panel.SectionContentRoot.Add(section);

        if (settingsProperty == null)
        {
            section.Add(new HelpBox("Data Collection settings are missing on this preset.", HelpBoxMessageType.Error));
            return;
        }

        Foldout availability = CreateFoldout("Runtime Context",
                                             "Controls editor collection and the deployment label sent with telemetry.");
        AddProperty(panel,
                    availability,
                    settingsProperty,
                    "collectInEditor",
                    "Collect In Editor",
                    "Allows consented uploads while running inside the Unity Editor.");
        AddProperty(panel,
                    availability,
                    settingsProperty,
                    "environment",
                    "Environment",
                    "Separates development, staging, and production telemetry.");
        section.Add(availability);

        BuildService(panel, section, settingsProperty);
        BuildContracts(panel, section, settingsProperty);
        BuildDeveloperAccess(panel, section, settingsProperty);
        BuildSampling(panel, section, settingsProperty);
        BuildBatching(panel, section, settingsProperty);
        PropertyField persistenceField = BuildRetry(panel,
                                                    section,
                                                    settingsProperty,
                                                    out VisualElement persistenceOptions);

        Action refreshVisibility = () =>
        {
            panel.PresetSerializedObject.Update();
            SerializedProperty persistenceProperty = settingsProperty.FindPropertyRelative("persistPendingEvents");
            persistenceOptions.style.display = persistenceProperty != null && persistenceProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        };

        if (persistenceField != null)
            persistenceField.RegisterCallback<SerializedPropertyChangeEvent>(evt => refreshVisibility.Invoke());

        refreshVisibility.Invoke();
    }
    #endregion

    #region Groups
    /// <summary>
    /// Builds HTTPS root and bounded request controls.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel.</param>
    /// <param name="parent">Parent options root.</param>
    /// <param name="settingsProperty">Serialized Data Collection root.</param>
    private static void BuildService(GameSettingsManagerPresetsPanel panel,
                                     VisualElement parent,
                                     SerializedProperty settingsProperty)
    {
        Foldout foldout = CreateFoldout("HTTPS Service",
                                       "Configures the alwaysdata API root and client-side request limits.");
        AddProperty(panel, foldout, settingsProperty, "serviceBaseUrl", "Service Base URL", "Versioned HTTPS API root hosted by alwaysdata.");
        AddProperty(panel, foldout, settingsProperty, "requestTimeoutSeconds", "Request Timeout", "Maximum unscaled seconds allowed for one request.");
        AddProperty(panel, foldout, settingsProperty, "maximumPayloadBytes", "Maximum Payload Bytes", "Client-side UTF-8 body limit applied before upload.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds telemetry schema and consent-notice version controls.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel.</param>
    /// <param name="parent">Parent options root.</param>
    /// <param name="settingsProperty">Serialized Data Collection root.</param>
    private static void BuildContracts(GameSettingsManagerPresetsPanel panel,
                                       VisualElement parent,
                                       SerializedProperty settingsProperty)
    {
        Foldout foldout = CreateFoldout("Contracts",
                                       "Versions persisted with telemetry and consent decisions for reproducible analysis.");
        AddProperty(panel, foldout, settingsProperty, "schemaVersion", "Schema Version", "Version checked by the PHP collector before MySQL insertion.");
        AddProperty(panel, foldout, settingsProperty, "consentPolicyVersion", "Consent Policy Version", "Version shown and persisted with the user's explicit choices.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds the configurable cheat Input Action and developer query page size.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel.</param>
    /// <param name="parent">Parent options root.</param>
    /// <param name="settingsProperty">Serialized Data Collection root.</param>
    private static void BuildDeveloperAccess(GameSettingsManagerPresetsPanel panel,
                                             VisualElement parent,
                                             SerializedProperty settingsProperty)
    {
        Foldout foldout = CreateFoldout("Developer Access",
                                       "The Input Action reveals Dev controls; server authentication still grants every privilege.");
        GameSettingsManagerInputActionFieldUtility.AddActionPicker(
            panel,
            foldout,
            settingsProperty.FindPropertyRelative("revealDevActionsActionId"),
            GameDataCollectionSettings.DefaultRevealDevActionsActionName,
            "Reveal Dev Actions",
            "Configurable action that reveals Register As Dev and Login As Dev in the Settings Dev tab.",
            InputActionSelectionElement.SelectionMode.Generic);
        AddProperty(panel, foldout, settingsProperty, "dashboardPageSize", "Dashboard Page Size", "Maximum aggregate rows returned per developer query page.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds low-frequency programming and 3D sample cadence controls.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel.</param>
    /// <param name="parent">Parent options root.</param>
    /// <param name="settingsProperty">Serialized Data Collection root.</param>
    private static void BuildSampling(GameSettingsManagerPresetsPanel panel,
                                      VisualElement parent,
                                      SerializedProperty settingsProperty)
    {
        Foldout foldout = CreateFoldout("Sampling",
                                       "Controls aggregation cadence without generating one event per entity or rendered object.");
        AddProperty(panel, foldout, settingsProperty, "performanceSampleIntervalSeconds", "Performance Sample Interval", "Seconds between programming performance aggregates.");
        AddProperty(panel, foldout, settingsProperty, "renderingSampleIntervalSeconds", "Rendering Sample Interval", "Seconds between 3D workload aggregates.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds upload cadence and bounded queue controls.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel.</param>
    /// <param name="parent">Parent options root.</param>
    /// <param name="settingsProperty">Serialized Data Collection root.</param>
    private static void BuildBatching(GameSettingsManagerPresetsPanel panel,
                                      VisualElement parent,
                                      SerializedProperty settingsProperty)
    {
        Foldout foldout = CreateFoldout("Batching",
                                       "Bounds request frequency, event count, and memory use during dense gameplay.");
        AddProperty(panel, foldout, settingsProperty, "uploadIntervalSeconds", "Upload Interval", "Seconds between automatic upload attempts while events are pending.");
        AddProperty(panel, foldout, settingsProperty, "maximumEventsPerBatch", "Maximum Events Per Batch", "Maximum telemetry records serialized into one request.");
        AddProperty(panel, foldout, settingsProperty, "maximumPendingEvents", "Maximum Pending Events", "Maximum consented records retained before oldest-first eviction.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds retry and conditionally visible offline persistence controls.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel.</param>
    /// <param name="parent">Parent options root.</param>
    /// <param name="settingsProperty">Serialized Data Collection root.</param>
    /// <param name="persistenceOptions">Receives the controls used only when persistence is enabled.</param>
    /// <returns>Persistence toggle driving conditional visibility.</returns>
    private static PropertyField BuildRetry(GameSettingsManagerPresetsPanel panel,
                                            VisualElement parent,
                                            SerializedProperty settingsProperty,
                                            out VisualElement persistenceOptions)
    {
        Foldout foldout = CreateFoldout("Retry and Offline Queue",
                                       "Configures bounded persistence and exponential retry after transient failures.");
        PropertyField persistenceField = AddProperty(panel,
                                                     foldout,
                                                     settingsProperty,
                                                     "persistPendingEvents",
                                                     "Persist Pending Events",
                                                     "Keeps pseudonymous pending events across launches.");
        persistenceOptions = new VisualElement();
        AddProperty(panel, persistenceOptions, settingsProperty, "pendingEventRetentionDays", "Pending Event Retention Days", "Maximum age of locally persisted pending events.");
        foldout.Add(persistenceOptions);
        AddProperty(panel, foldout, settingsProperty, "initialRetryDelaySeconds", "Initial Retry Delay", "Initial delay after a failed upload.");
        AddProperty(panel, foldout, settingsProperty, "maximumRetryDelaySeconds", "Maximum Retry Delay", "Upper bound for exponential retry delay.");
        parent.Add(foldout);
        return persistenceField;
    }
    #endregion

    #region UI Helpers
    /// <summary>
    /// Creates one styled data-collection section root.
    /// </summary>
    /// <param name="title">Visible section title.</param>
    /// <param name="tooltip">Section purpose shown on hover.</param>
    /// <returns>Configured section root.</returns>
    private static VisualElement CreateSection(string title, string tooltip)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;
        Label label = new Label(title);
        label.tooltip = tooltip;
        label.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(label, "NashCore.GameManagement.Settings.DataCollection");
        section.Add(label);
        return section;
    }

    /// <summary>
    /// Creates one expanded foldout with a concise purpose tooltip.
    /// </summary>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="tooltip">Foldout purpose shown on hover.</param>
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
    /// Adds one bound property field and reports edits to the Settings Manager draft session.
    /// </summary>
    /// <param name="panel">Owning Settings Manager panel.</param>
    /// <param name="parent">Parent UI element.</param>
    /// <param name="settingsProperty">Serialized Data Collection root.</param>
    /// <param name="propertyName">Relative serialized field name.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field purpose shown on hover.</param>
    /// <returns>Created field, or null when the property is unavailable.</returns>
    private static PropertyField AddProperty(GameSettingsManagerPresetsPanel panel,
                                             VisualElement parent,
                                             SerializedProperty settingsProperty,
                                             string propertyName,
                                             string label,
                                             string tooltip)
    {
        SerializedProperty property = settingsProperty.FindPropertyRelative(propertyName);

        if (property == null)
            return null;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt => panel.MarkSelectedPresetDirty());
        parent.Add(field);
        return field;
    }
    #endregion

    #endregion
}
