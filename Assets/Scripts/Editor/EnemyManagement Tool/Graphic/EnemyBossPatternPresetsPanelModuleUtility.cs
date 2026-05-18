using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Stores one selectable module entry shown by boss pattern assemble selectors.
/// </summary>
internal struct EnemyBossPatternModuleOption
{
    public string ModuleId;
    public string DisplayLabel;
    public EnemyPatternModuleKind Kind;
}

/// <summary>
/// Provides source-catalog module selectors, payload overrides and warnings for boss pattern assemble authoring.
/// </summary>
internal static class EnemyBossPatternPresetsPanelModuleUtility
{
    #region Methods

    #region Module Selectors
    /// <summary>
    /// Adds a source-module selector and payload override UI for one boss pattern slot binding.
    /// </summary>
    /// <param name="panel">Owning panel used for dirty tracking.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="bindingProperty">Serialized module binding.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="section">Catalog section used by the slot.</param>
    /// <param name="label">Selector label.</param>
    /// <param name="tooltip">Selector tooltip.</param>
    public static void AddModuleBindingSelector(EnemyBossPatternPresetsPanel panel,
                                                VisualElement parent,
                                                SerializedProperty bindingProperty,
                                                EnemyModulesAndPatternsPreset sourcePreset,
                                                EnemyPatternModuleCatalogSection section,
                                                string label,
                                                string tooltip)
    {
        if (panel == null || parent == null || bindingProperty == null)
            return;

        SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");
        SerializedProperty isEnabledProperty = bindingProperty.FindPropertyRelative("isEnabled");
        SerializedProperty useOverridePayloadProperty = bindingProperty.FindPropertyRelative("useOverridePayload");
        SerializedProperty overridePayloadProperty = bindingProperty.FindPropertyRelative("overridePayload");

        if (moduleIdProperty == null)
            return;

        List<EnemyBossPatternModuleOption> options = BuildModuleOptions(sourcePreset, section);

        if (options.Count <= 0)
        {
            parent.Add(new HelpBox("The source preset has no modules for " + FormatSection(section) + ".", HelpBoxMessageType.Warning));
            PropertyField moduleIdField = new PropertyField(moduleIdProperty, label);
            moduleIdField.tooltip = tooltip;
            moduleIdField.BindProperty(moduleIdProperty);
            parent.Add(moduleIdField);
            return;
        }

        bool selectedIsValid;
        List<EnemyBossPatternModuleOption> selectorOptions = BuildSelectorOptions(options,
                                                                                  moduleIdProperty.stringValue,
                                                                                  out int selectedIndex,
                                                                                  out selectedIsValid);
        List<string> labels = BuildModuleLabels(selectorOptions);
        PopupField<string> selector = new PopupField<string>(label, labels, selectedIndex);
        selector.tooltip = tooltip;
        selector.RegisterValueChangedCallback(evt =>
        {
            int optionIndex = labels.IndexOf(evt.newValue);

            if (optionIndex < 0 || optionIndex >= selectorOptions.Count)
                return;

            EnemyBossPatternModuleOption option = selectorOptions[optionIndex];

            if (string.IsNullOrWhiteSpace(option.ModuleId))
                return;

            SetBindingModule(panel,
                             moduleIdProperty,
                             isEnabledProperty,
                             useOverridePayloadProperty,
                             overridePayloadProperty,
                             sourcePreset,
                             section,
                             option.ModuleId);
        });
        parent.Add(selector);

        EnemyBossPatternModuleOption selectedOption = selectorOptions[selectedIndex];
        AddModuleBindingWarnings(moduleIdProperty.stringValue, selectedOption, selectedIsValid, section, parent);

        if (!selectedIsValid)
            return;

        AddOverridePayloadFields(panel,
                                 parent,
                                 moduleIdProperty,
                                 useOverridePayloadProperty,
                                 overridePayloadProperty,
                                 sourcePreset,
                                 selectedOption.Kind,
                                 section);
    }

    /// <summary>
    /// Adds override payload controls for module kinds that own editable payload data.
    /// </summary>
    /// <param name="panel">Owning panel used for dirty tracking.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="moduleIdProperty">Serialized selected module ID.</param>
    /// <param name="useOverridePayloadProperty">Serialized override toggle.</param>
    /// <param name="overridePayloadProperty">Serialized override payload root.</param>
    /// <param name="sourcePreset">Source module catalog used to seed override payloads.</param>
    /// <param name="moduleKind">Selected module kind.</param>
    /// <param name="section">Catalog section used to resolve payload visibility.</param>
    private static void AddOverridePayloadFields(EnemyBossPatternPresetsPanel panel,
                                                 VisualElement parent,
                                                 SerializedProperty moduleIdProperty,
                                                 SerializedProperty useOverridePayloadProperty,
                                                 SerializedProperty overridePayloadProperty,
                                                 EnemyModulesAndPatternsPreset sourcePreset,
                                                 EnemyPatternModuleKind moduleKind,
                                                 EnemyPatternModuleCatalogSection section)
    {
        if (parent == null)
            return;

        if (useOverridePayloadProperty == null || overridePayloadProperty == null)
        {
            parent.Add(new HelpBox("Override payload serialized fields are missing for this module binding.", HelpBoxMessageType.Warning));
            return;
        }

        parent.Add(CreateOverridePayloadToggle(panel,
                                               moduleIdProperty,
                                               useOverridePayloadProperty,
                                               overridePayloadProperty,
                                               sourcePreset,
                                               section));

        if (moduleKind == EnemyPatternModuleKind.Grunt)
        {
            parent.Add(new HelpBox("The selected Grunt module has no kind-specific payload fields. Slot range, activation and engagement settings remain available in the surrounding module section.", HelpBoxMessageType.Info));
            return;
        }

        if (!useOverridePayloadProperty.boolValue)
            return;

        VisualElement headerRow = CreatePayloadHeaderRow(panel,
                                                         moduleIdProperty,
                                                         overridePayloadProperty,
                                                         sourcePreset,
                                                         section);
        parent.Add(headerRow);

        VisualElement payloadContainer = new VisualElement();
        payloadContainer.style.marginLeft = 12f;
        parent.Add(payloadContainer);
        EnemyAdvancedPatternDrawerUtility.RefreshPayloadEditor(overridePayloadProperty,
                                                               moduleKind,
                                                               payloadContainer,
                                                               ResolvePayloadEditorMode(section));
    }

    /// <summary>
    /// Creates the override toggle that seeds payload data from the selected source module when it becomes active.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context and rebuild callbacks.</param>
    /// <param name="moduleIdProperty">Serialized selected module ID.</param>
    /// <param name="useOverridePayloadProperty">Serialized override toggle.</param>
    /// <param name="overridePayloadProperty">Serialized override payload root.</param>
    /// <param name="sourcePreset">Source module catalog used to seed payload data.</param>
    /// <param name="section">Catalog section that owns the source module.</param>
    /// <returns>Configured toggle element.</returns>
    private static Toggle CreateOverridePayloadToggle(EnemyBossPatternPresetsPanel panel,
                                                      SerializedProperty moduleIdProperty,
                                                      SerializedProperty useOverridePayloadProperty,
                                                      SerializedProperty overridePayloadProperty,
                                                      EnemyModulesAndPatternsPreset sourcePreset,
                                                      EnemyPatternModuleCatalogSection section)
    {
        Toggle toggle = new Toggle("Use Override Payload");
        toggle.tooltip = "Override this slot payload instead of using the shared source module payload. When enabled, the override starts from the selected source module data.";
        toggle.SetValueWithoutNotify(useOverridePayloadProperty != null && useOverridePayloadProperty.boolValue);
        toggle.RegisterValueChangedCallback(evt =>
        {
            SetOverridePayloadEnabled(panel,
                                      moduleIdProperty,
                                      useOverridePayloadProperty,
                                      overridePayloadProperty,
                                      sourcePreset,
                                      section,
                                      evt.newValue);
        });
        return toggle;
    }

    /// <summary>
    /// Creates the payload header and a source-refresh action for already active overrides.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context and rebuild callbacks.</param>
    /// <param name="moduleIdProperty">Serialized selected module ID.</param>
    /// <param name="overridePayloadProperty">Serialized override payload root.</param>
    /// <param name="sourcePreset">Source module catalog used to seed payload data.</param>
    /// <param name="section">Catalog section that owns the source module.</param>
    /// <returns>Header row shown above the override payload editor.</returns>
    private static VisualElement CreatePayloadHeaderRow(EnemyBossPatternPresetsPanel panel,
                                                        SerializedProperty moduleIdProperty,
                                                        SerializedProperty overridePayloadProperty,
                                                        EnemyModulesAndPatternsPreset sourcePreset,
                                                        EnemyPatternModuleCatalogSection section)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 4f;

        Label payloadHeader = new Label("Override Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(payloadHeader);

        Button refreshButton = new Button(() =>
        {
            RefreshOverridePayloadFromSource(panel,
                                             moduleIdProperty,
                                             overridePayloadProperty,
                                             sourcePreset,
                                             section);
        });
        refreshButton.text = "Refresh From Source";
        refreshButton.tooltip = "Replace the current override payload with the selected source module payload.";
        refreshButton.style.marginLeft = 8f;
        row.Add(refreshButton);
        return row;
    }
    #endregion

    #region Options
    /// <summary>
    /// Builds selectable module options for one source catalog section.
    /// </summary>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="section">Requested catalog section.</param>
    /// <returns>Ordered selectable module options.</returns>
    public static List<EnemyBossPatternModuleOption> BuildModuleOptions(EnemyModulesAndPatternsPreset sourcePreset,
                                                                        EnemyPatternModuleCatalogSection section)
    {
        List<EnemyBossPatternModuleOption> options = new List<EnemyBossPatternModuleOption>();

        if (sourcePreset == null)
            return options;

        IReadOnlyList<EnemyPatternModuleDefinition> definitions = sourcePreset.GetDefinitions(section);

        if (definitions == null)
            return options;

        for (int index = 0; index < definitions.Count; index++)
        {
            EnemyPatternModuleDefinition definition = definitions[index];

            if (definition == null || string.IsNullOrWhiteSpace(definition.ModuleId))
                continue;

            if (!EnemyAdvancedPatternDrawerUtility.IsModuleKindAllowedInCatalogSection(definition.ModuleKind, section))
                continue;

            EnemyBossPatternModuleOption option = new EnemyBossPatternModuleOption();
            option.ModuleId = definition.ModuleId;
            option.DisplayLabel = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.ModuleId
                : definition.DisplayName + " (" + definition.ModuleId + ")";
            option.Kind = definition.ModuleKind;
            options.Add(option);
        }

        return options;
    }

    /// <summary>
    /// Builds popup labels from module options.
    /// </summary>
    /// <param name="options">Module options used by the selector.</param>
    /// <returns>Popup labels preserving option order.</returns>
    public static List<string> BuildModuleLabels(List<EnemyBossPatternModuleOption> options)
    {
        List<string> labels = new List<string>(options.Count);

        for (int index = 0; index < options.Count; index++)
            labels.Add(options[index].DisplayLabel);

        return labels;
    }

    /// <summary>
    /// Builds selector options while preserving invalid serialized values as explicit selectable states.
    /// </summary>
    /// <param name="validOptions">Legal source-catalog options.</param>
    /// <param name="moduleId">Current serialized module ID.</param>
    /// <param name="selectedIndex">Output selected popup index.</param>
    /// <param name="selectedIsValid">Output flag indicating whether the selected option resolves to the source catalog.</param>
    /// <returns>Popup options including a leading invalid state when the serialized module ID is not currently legal.</returns>
    private static List<EnemyBossPatternModuleOption> BuildSelectorOptions(List<EnemyBossPatternModuleOption> validOptions,
                                                                           string moduleId,
                                                                           out int selectedIndex,
                                                                           out bool selectedIsValid)
    {
        selectedIndex = ResolveSelectedModuleIndex(validOptions, moduleId);
        selectedIsValid = selectedIndex >= 0;

        if (selectedIsValid)
            return validOptions;

        List<EnemyBossPatternModuleOption> selectorOptions = new List<EnemyBossPatternModuleOption>(validOptions.Count + 1);
        selectorOptions.Add(CreateInvalidCurrentOption(moduleId));

        for (int index = 0; index < validOptions.Count; index++)
            selectorOptions.Add(validOptions[index]);

        selectedIndex = 0;
        return selectorOptions;
    }

    /// <summary>
    /// Creates the explicit popup entry shown when a binding is empty or points at a missing source module.
    /// </summary>
    /// <param name="moduleId">Current serialized module ID.</param>
    /// <returns>Invalid selector option that cannot be applied back to the binding.</returns>
    private static EnemyBossPatternModuleOption CreateInvalidCurrentOption(string moduleId)
    {
        EnemyBossPatternModuleOption option = new EnemyBossPatternModuleOption();
        option.ModuleId = string.Empty;
        option.DisplayLabel = string.IsNullOrWhiteSpace(moduleId)
            ? "<Select Module>"
            : "<Missing Module> " + moduleId;
        option.Kind = EnemyPatternModuleKind.Grunt;
        return option;
    }

    /// <summary>
    /// Resolves the selected module index without hiding invalid serialized values behind a legal fallback.
    /// </summary>
    /// <param name="options">Module options used by the selector.</param>
    /// <param name="moduleId">Current serialized module ID.</param>
    /// <returns>Selected option index, or -1 when the current module ID is invalid.</returns>
    public static int ResolveSelectedModuleIndex(List<EnemyBossPatternModuleOption> options, string moduleId)
    {
        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index].ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Resolves the first legal module ID in one source catalog section.
    /// </summary>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="section">Requested catalog section.</param>
    /// <param name="moduleId">Output module ID.</param>
    /// <returns>True when a module is available.</returns>
    public static bool TryResolveFirstModuleId(EnemyModulesAndPatternsPreset sourcePreset,
                                               EnemyPatternModuleCatalogSection section,
                                               out string moduleId)
    {
        List<EnemyBossPatternModuleOption> options = BuildModuleOptions(sourcePreset, section);
        moduleId = options.Count > 0 ? options[0].ModuleId : string.Empty;
        return options.Count > 0;
    }

    /// <summary>
    /// Resolves whether a source preset exposes at least one selectable boss module.
    /// </summary>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <returns>True when at least one supported module exists.</returns>
    public static bool HasAnySelectableModule(EnemyModulesAndPatternsPreset sourcePreset)
    {
        if (TryResolveFirstModuleId(sourcePreset, EnemyPatternModuleCatalogSection.CoreMovement, out string _))
            return true;

        if (TryResolveFirstModuleId(sourcePreset, EnemyPatternModuleCatalogSection.ShortRangeInteraction, out string _))
            return true;

        return TryResolveFirstModuleId(sourcePreset, EnemyPatternModuleCatalogSection.WeaponInteraction, out string _);
    }
    #endregion

    #region Serialized Helpers
    /// <summary>
    /// Writes the nested module binding fields used by boss pattern slots.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding being edited.</param>
    /// <param name="moduleId">Module ID to assign.</param>
    public static void ConfigureBinding(SerializedProperty bindingProperty, string moduleId)
    {
        if (bindingProperty == null)
            return;

        SetString(bindingProperty.FindPropertyRelative("moduleId"), moduleId);
        SetBoolean(bindingProperty.FindPropertyRelative("isEnabled"), true);
        SetBoolean(bindingProperty.FindPropertyRelative("useOverridePayload"), false);
    }

    /// <summary>
    /// Writes a string serialized property when available.
    /// </summary>
    /// <param name="property">Serialized property to mutate.</param>
    /// <param name="value">New value.</param>
    public static void SetString(SerializedProperty property, string value)
    {
        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Writes a boolean serialized property when available.
    /// </summary>
    /// <param name="property">Serialized property to mutate.</param>
    /// <param name="value">New value.</param>
    public static void SetBoolean(SerializedProperty property, bool value)
    {
        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Writes an enum index serialized property when available.
    /// </summary>
    /// <param name="property">Serialized property to mutate.</param>
    /// <param name="value">New enum index.</param>
    public static void SetEnumIndex(SerializedProperty property, int value)
    {
        if (property != null)
            property.enumValueIndex = value;
    }

    /// <summary>
    /// Writes a float serialized property when available.
    /// </summary>
    /// <param name="property">Serialized property to mutate.</param>
    /// <param name="value">New value.</param>
    public static void SetFloat(SerializedProperty property, float value)
    {
        if (property != null)
            property.floatValue = value;
    }

    /// <summary>
    /// Writes the selected module ID into a binding and refreshes dependent boss UI.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context and rebuild callbacks.</param>
    /// <param name="moduleIdProperty">Serialized module ID property.</param>
    /// <param name="isEnabledProperty">Serialized binding enabled property.</param>
    /// <param name="useOverridePayloadProperty">Serialized override toggle property.</param>
    /// <param name="overridePayloadProperty">Serialized override payload root.</param>
    /// <param name="sourcePreset">Source module catalog used to seed existing active overrides.</param>
    /// <param name="section">Catalog section that owns the selected module.</param>
    /// <param name="moduleId">Selected module ID.</param>
    private static void SetBindingModule(EnemyBossPatternPresetsPanel panel,
                                         SerializedProperty moduleIdProperty,
                                         SerializedProperty isEnabledProperty,
                                         SerializedProperty useOverridePayloadProperty,
                                         SerializedProperty overridePayloadProperty,
                                         EnemyModulesAndPatternsPreset sourcePreset,
                                         EnemyPatternModuleCatalogSection section,
                                         string moduleId)
    {
        if (panel == null || moduleIdProperty == null)
            return;

        EnemyBossPatternPresetsPanelSharedUtility.RecordSelectedPreset(panel, "Change Boss Pattern Module");
        panel.PresetSerializedObject.Update();
        moduleIdProperty.stringValue = moduleId;

        if (isEnabledProperty != null)
            isEnabledProperty.boolValue = true;

        if (useOverridePayloadProperty != null && useOverridePayloadProperty.boolValue)
            EnemyBossPatternOverridePayloadSeedUtility.SeedOverridePayloadFromSourceModule(sourcePreset, section, moduleId, overridePayloadProperty);

        panel.PresetSerializedObject.ApplyModifiedProperties();
        EnemyBossPatternPresetsPanelSharedUtility.MarkDirtyAndRebuild(panel);
    }

    /// <summary>
    /// Updates the override toggle and seeds the payload from the current source module when enabling it.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context and rebuild callbacks.</param>
    /// <param name="moduleIdProperty">Serialized selected module ID.</param>
    /// <param name="useOverridePayloadProperty">Serialized override toggle property.</param>
    /// <param name="overridePayloadProperty">Serialized override payload root.</param>
    /// <param name="sourcePreset">Source module catalog used to seed payload data.</param>
    /// <param name="section">Catalog section that owns the selected module.</param>
    /// <param name="enabled">New override toggle state.</param>
    private static void SetOverridePayloadEnabled(EnemyBossPatternPresetsPanel panel,
                                                  SerializedProperty moduleIdProperty,
                                                  SerializedProperty useOverridePayloadProperty,
                                                  SerializedProperty overridePayloadProperty,
                                                  EnemyModulesAndPatternsPreset sourcePreset,
                                                  EnemyPatternModuleCatalogSection section,
                                                  bool enabled)
    {
        if (panel == null || useOverridePayloadProperty == null)
            return;

        EnemyBossPatternPresetsPanelSharedUtility.RecordSelectedPreset(panel, "Edit Boss Pattern Override Payload");
        panel.PresetSerializedObject.Update();
        useOverridePayloadProperty.boolValue = enabled;

        if (enabled)
            EnemyBossPatternOverridePayloadSeedUtility.SeedOverridePayloadFromSourceModule(sourcePreset, section, ResolveModuleId(moduleIdProperty), overridePayloadProperty);

        panel.PresetSerializedObject.ApplyModifiedProperties();

        if (panel.SelectedPreset != null)
            EditorUtility.SetDirty(panel.SelectedPreset);

        EnemyBossPatternPresetsPanelSharedUtility.MarkDirtyAndRebuild(panel);
    }

    /// <summary>
    /// Replaces an active override payload with the selected source module payload on user request.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context and rebuild callbacks.</param>
    /// <param name="moduleIdProperty">Serialized selected module ID.</param>
    /// <param name="overridePayloadProperty">Serialized override payload root.</param>
    /// <param name="sourcePreset">Source module catalog used to seed payload data.</param>
    /// <param name="section">Catalog section that owns the selected module.</param>
    private static void RefreshOverridePayloadFromSource(EnemyBossPatternPresetsPanel panel,
                                                         SerializedProperty moduleIdProperty,
                                                         SerializedProperty overridePayloadProperty,
                                                         EnemyModulesAndPatternsPreset sourcePreset,
                                                         EnemyPatternModuleCatalogSection section)
    {
        if (panel == null || overridePayloadProperty == null)
            return;

        EnemyBossPatternPresetsPanelSharedUtility.RecordSelectedPreset(panel, "Refresh Boss Pattern Override Payload");
        panel.PresetSerializedObject.Update();
        EnemyBossPatternOverridePayloadSeedUtility.SeedOverridePayloadFromSourceModule(sourcePreset, section, ResolveModuleId(moduleIdProperty), overridePayloadProperty);
        panel.PresetSerializedObject.ApplyModifiedProperties();

        if (panel.SelectedPreset != null)
            EditorUtility.SetDirty(panel.SelectedPreset);

        EnemyBossPatternPresetsPanelSharedUtility.MarkDirtyAndRebuild(panel);
    }

    /// <summary>
    /// Resolves a safe module ID string from a serialized property.
    /// </summary>
    /// <param name="moduleIdProperty">Serialized module ID property.</param>
    /// <returns>Current module ID, or an empty string when unavailable.</returns>
    private static string ResolveModuleId(SerializedProperty moduleIdProperty)
    {
        if (moduleIdProperty == null)
            return string.Empty;

        return moduleIdProperty.stringValue;
    }
    #endregion

    #region Formatting
    /// <summary>
    /// Resolves payload visibility mode from the selected catalog section.
    /// </summary>
    /// <param name="section">Catalog section used by the slot.</param>
    /// <returns>Payload editor mode for the section.</returns>
    private static EnemyAdvancedPatternPayloadEditorMode ResolvePayloadEditorMode(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return EnemyAdvancedPatternPayloadEditorMode.ShortRangeInteraction;

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return EnemyAdvancedPatternPayloadEditorMode.WeaponInteraction;

            default:
                return EnemyAdvancedPatternPayloadEditorMode.Full;
        }
    }

    /// <summary>
    /// Resolves one serialized weapon activation gate property to a typed flag enum.
    /// </summary>
    /// <param name="activationGatesProperty">Serialized activation gate flags.</param>
    /// <returns>Typed weapon activation gates.</returns>
    public static EnemyWeaponInteractionActivationGate ResolveWeaponActivationGates(SerializedProperty activationGatesProperty)
    {
        if (activationGatesProperty == null)
            return EnemyWeaponInteractionActivationGate.Always;

        return (EnemyWeaponInteractionActivationGate)activationGatesProperty.enumValueFlag;
    }

    /// <summary>
    /// Converts a catalog section into user-facing text.
    /// </summary>
    /// <param name="section">Catalog section to format.</param>
    /// <returns>Human-readable section text.</returns>
    private static string FormatSection(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "Short-Range Interaction";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "Weapon Interaction";

            default:
                return "Core Movement";
        }
    }
    #endregion

    #region Property Helpers
    /// <summary>
    /// Adds module binding warnings for the current serialized module ID.
    /// </summary>
    /// <param name="moduleId">Serialized module ID.</param>
    /// <param name="selectedOption">Selected option shown by the popup.</param>
    /// <param name="selectedIsValid">Whether the selected option resolves to the source catalog.</param>
    /// <param name="section">Catalog section used by the slot.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddModuleBindingWarnings(string moduleId,
                                                 EnemyBossPatternModuleOption selectedOption,
                                                 bool selectedIsValid,
                                                 EnemyPatternModuleCatalogSection section,
                                                 VisualElement parent)
    {
        if (parent == null)
            return;

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            parent.Add(new HelpBox("Select a module from the " + FormatSection(section) + " source catalog.", HelpBoxMessageType.Warning));
            return;
        }

        if (!selectedIsValid)
        {
            parent.Add(new HelpBox("The selected module ID is missing from the " + FormatSection(section) + " source catalog. Choose a valid module from the popup to repair this slot.", HelpBoxMessageType.Warning));
            return;
        }

        if (!string.Equals(selectedOption.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
            parent.Add(new HelpBox("The selected module ID does not match the serialized binding. Choose the intended module again to repair this slot.", HelpBoxMessageType.Warning));
    }

    #endregion

    #endregion
}
