using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the shared Modules &amp; Patterns preset editor used by enemy advanced-pattern presets.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetSectionUtility
{
    #region Constants
    private const string SharedPresetSectionTitle = "Modules & Patterns Preset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the inline shared-preset editor for the selected advanced-pattern preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and refresh callbacks.</param>
    public static void BuildSection(EnemyAdvancedPatternPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = EnemyAdvancedPatternPresetsPanelElementUtility.CreateDetailsSectionContainer(panel, SharedPresetSectionTitle);

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        SerializedProperty sharedPresetProperty = presetSerializedObject.FindProperty("modulesAndPatternsPreset");

        if (sharedPresetProperty == null)
        {
            HelpBox missingPropertyBox = new HelpBox("The shared Modules & Patterns preset reference is missing on this preset.", HelpBoxMessageType.Warning);
            sectionContainer.Add(missingPropertyBox);
            return;
        }

        HelpBox infoBox = new HelpBox("Assign one shared preset so multiple enemies can reuse the same module catalogs and assembled patterns.", HelpBoxMessageType.Info);
        sectionContainer.Add(infoBox);

        ObjectField sharedPresetField = new ObjectField("Shared Preset");
        sharedPresetField.objectType = typeof(EnemyModulesAndPatternsPreset);
        sharedPresetField.allowSceneObjects = false;
        sharedPresetField.tooltip = "Shared asset referenced by this advanced-pattern preset for module catalogs and assembled patterns.";
        sharedPresetField.SetValueWithoutNotify(sharedPresetProperty.objectReferenceValue);
        sharedPresetField.RegisterValueChangedCallback(evt =>
        {
            EnemyAdvancedPatternSharedPresetEditorUtility.AssignSharedPreset(panel,
                                                                            sharedPresetProperty,
                                                                            evt.newValue as EnemyModulesAndPatternsPreset);
        });
        sectionContainer.Add(sharedPresetField);

        VisualElement actionsRow = CreateActionsRow();
        Button createButton = new Button(() =>
        {
            EnemyAdvancedPatternSharedPresetEditorUtility.CreateAndAssignSharedPreset(panel);
        });
        createButton.text = "New Shared Preset";
        createButton.tooltip = "Create one new shared Modules & Patterns preset and assign it to this advanced-pattern preset.";
        actionsRow.Add(createButton);
        sectionContainer.Add(actionsRow);

        EnemyModulesAndPatternsPreset sharedPreset = sharedPresetProperty.objectReferenceValue as EnemyModulesAndPatternsPreset;
        RefreshSharedPresetFieldPresentation(sharedPresetField, sharedPreset);

        if (sharedPreset == null)
        {
            HelpBox missingSharedPresetBox = new HelpBox("No shared preset is assigned. Assign or create one shared preset to author reusable module catalogs and shared assembled patterns.", HelpBoxMessageType.Warning);
            sectionContainer.Add(missingSharedPresetBox);
            return;
        }

        sharedPresetField.tooltip = BuildSharedPresetTooltip(sharedPreset);
        Label sharedPresetSummaryLabel = EnemyAdvancedPatternSharedPresetCardUtility.CreateDescriptionLabel(BuildSharedPresetDisplayName(sharedPreset));
        sharedPresetSummaryLabel.tooltip = BuildSharedPresetTooltip(sharedPreset);
        sharedPresetSummaryLabel.style.marginBottom = 6f;
        sectionContainer.Add(sharedPresetSummaryLabel);

        SerializedObject sharedPresetSerializedObject = new SerializedObject(sharedPreset);
        sharedPresetSerializedObject.Update();
        string sharedPresetStateKey = ManagementToolFoldoutStateUtility.BuildSerializedObjectStateKey(sharedPresetSerializedObject);

        Foldout metadataFoldout = CreateFoldout("Shared Preset Metadata",
                                                sharedPresetStateKey + "|SharedPresetMetadata",
                                                "Group shared preset naming, versioning and identity metadata.");
        sectionContainer.Add(metadataFoldout);
        BuildMetadataFoldoutContent(panel, sharedPresetSerializedObject, sharedPreset, metadataFoldout);

        Foldout modulesFoldout = CreateFoldout("Modules Definition",
                                               sharedPresetStateKey + "|SharedPresetModulesDefinition",
                                               "Group all reusable module catalogs under one shared dropdown.");
        sectionContainer.Add(modulesFoldout);
        BuildModulesFoldoutContent(panel, sharedPresetSerializedObject, sharedPreset, modulesFoldout);

        Foldout patternsFoldout = CreateFoldout("Pattern Assemble",
                                                sharedPresetStateKey + "|SharedPresetPatternAssemble",
                                                "Group all shared assembled patterns under one shared dropdown.");
        sectionContainer.Add(patternsFoldout);
        BuildPatternsFoldoutContent(panel, sharedPresetSerializedObject, sharedPreset, patternsFoldout);

        EnemyAdvancedPatternCompositionWarningUtility.AddWarnings(panel, sectionContainer);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates the row that hosts shared-preset action buttons.
    /// </summary>
    /// <returns>The created actions row.</returns>
    private static VisualElement CreateActionsRow()
    {
        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.flexWrap = Wrap.Wrap;
        actionsRow.style.marginBottom = 6f;
        return actionsRow;
    }

    /// <summary>
    /// Creates one persistent foldout used to group shared-preset subsections.
    /// </summary>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="viewDataKey">Stable view-data key used to preserve the expanded state.</param>
    /// <param name="tooltip">Tooltip shown on the foldout.</param>
    /// <returns>The created foldout.</returns>
    private static Foldout CreateFoldout(string title, string viewDataKey, string tooltip)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout(title, viewDataKey, true);
        foldout.tooltip = tooltip;
        foldout.style.marginTop = 6f;
        return foldout;
    }

    /// <summary>
    /// Builds the metadata foldout content for the shared preset.
    /// </summary>
    /// <param name="panel">Owning panel used for refresh callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="metadataFoldout">Foldout that receives the generated metadata controls.</param>
    private static void BuildMetadataFoldoutContent(EnemyAdvancedPatternPresetsPanel panel,
                                                    SerializedObject sharedPresetSerializedObject,
                                                    EnemyModulesAndPatternsPreset sharedPreset,
                                                    Foldout metadataFoldout)
    {
        EnemyAdvancedPatternSharedPresetEditorUtility.AddSharedPresetTextField(panel,
                                                                               sharedPresetSerializedObject,
                                                                               sharedPreset,
                                                                               metadataFoldout,
                                                                               "presetName",
                                                                               "Shared Preset Name",
                                                                               "Human-readable shared preset name shown in Enemy Management Tool.",
                                                                               false,
                                                                               true);
        EnemyAdvancedPatternSharedPresetEditorUtility.AddSharedPresetTextField(panel,
                                                                               sharedPresetSerializedObject,
                                                                               sharedPreset,
                                                                               metadataFoldout,
                                                                               "version",
                                                                               "Version",
                                                                               "Optional semantic version string for this shared preset.",
                                                                               false,
                                                                               false);
        EnemyAdvancedPatternSharedPresetEditorUtility.AddSharedPresetTextField(panel,
                                                                               sharedPresetSerializedObject,
                                                                               sharedPreset,
                                                                               metadataFoldout,
                                                                               "description",
                                                                               "Description",
                                                                               "Optional editor-facing notes describing the purpose of this shared preset.",
                                                                               true,
                                                                               false);
        EnemyAdvancedPatternSharedPresetEditorUtility.AddSharedPresetIdRow(panel,
                                                                           sharedPresetSerializedObject,
                                                                           sharedPreset,
                                                                           metadataFoldout);
    }

    /// <summary>
    /// Builds the grouped module-catalog foldout content for the shared preset.
    /// </summary>
    /// <param name="panel">Owning panel used for refresh callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="modulesFoldout">Foldout that receives the generated module-catalog controls.</param>
    private static void BuildModulesFoldoutContent(EnemyAdvancedPatternPresetsPanel panel,
                                                   SerializedObject sharedPresetSerializedObject,
                                                   EnemyModulesAndPatternsPreset sharedPreset,
                                                   Foldout modulesFoldout)
    {
        EnemyAdvancedPatternSharedPresetModulesListUtility.BuildModuleCatalogSections(panel,
                                                                                      sharedPresetSerializedObject,
                                                                                      sharedPreset,
                                                                                      modulesFoldout);
    }

    /// <summary>
    /// Builds the grouped assembled-pattern foldout content for the shared preset.
    /// </summary>
    /// <param name="panel">Owning panel used for refresh callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="patternsFoldout">Foldout that receives the generated pattern controls.</param>
    private static void BuildPatternsFoldoutContent(EnemyAdvancedPatternPresetsPanel panel,
                                                    SerializedObject sharedPresetSerializedObject,
                                                    EnemyModulesAndPatternsPreset sharedPreset,
                                                    Foldout patternsFoldout)
    {
        EnemyAdvancedPatternSharedPresetPatternsListUtility.BuildPatternSection(panel,
                                                                                sharedPresetSerializedObject,
                                                                                sharedPreset,
                                                                                patternsFoldout);
    }

    /// <summary>
    /// Builds the visible shared preset display name using the same version formatting used by the main preset lists.
    /// </summary>
    /// <param name="sharedPreset">Shared preset being summarized.</param>
    /// <returns>Display name with optional semantic version suffix.</returns>
    private static string BuildSharedPresetDisplayName(EnemyModulesAndPatternsPreset sharedPreset)
    {
        if (sharedPreset == null)
            return "<Missing Shared Preset>";

        string presetName = string.IsNullOrWhiteSpace(sharedPreset.PresetName) ? sharedPreset.name : sharedPreset.PresetName;
        string version = sharedPreset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return presetName;

        return presetName + " v. " + version;
    }

    /// <summary>
    /// Builds the tooltip shown by shared preset summary controls.
    /// </summary>
    /// <param name="sharedPreset">Shared preset being summarized.</param>
    /// <returns>Tooltip text for the current shared preset.</returns>
    private static string BuildSharedPresetTooltip(EnemyModulesAndPatternsPreset sharedPreset)
    {
        if (sharedPreset == null)
            return "Shared asset referenced by this advanced-pattern preset for module catalogs and assembled patterns.";

        if (!string.IsNullOrWhiteSpace(sharedPreset.Description))
            return sharedPreset.Description;

        return BuildSharedPresetDisplayName(sharedPreset);
    }

    /// <summary>
    /// Refreshes the visible shared-preset object field so its inline text and tooltip mirror the same name/version/description formatting used by the preset lists.
    /// </summary>
    /// <param name="sharedPresetField">Object field showing the current shared preset reference.</param>
    /// <param name="sharedPreset">Shared preset currently assigned to the field.</param>
    private static void RefreshSharedPresetFieldPresentation(ObjectField sharedPresetField,
                                                             EnemyModulesAndPatternsPreset sharedPreset)
    {
        if (sharedPresetField == null)
            return;

        string displayName = sharedPreset == null ? string.Empty : BuildSharedPresetDisplayName(sharedPreset);
        string tooltip = BuildSharedPresetTooltip(sharedPreset);
        sharedPresetField.tooltip = tooltip;
        sharedPresetField.schedule.Execute(() =>
        {
            VisualElement objectDisplayElement = sharedPresetField.Q(className: ObjectField.objectUssClassName);

            if (objectDisplayElement == null)
                return;

            objectDisplayElement.tooltip = tooltip;
            TextElement displayTextElement = objectDisplayElement.Q<TextElement>();

            if (displayTextElement == null)
                return;

            displayTextElement.text = displayName;
            displayTextElement.tooltip = tooltip;
        });
    }
    #endregion

    #endregion
}
