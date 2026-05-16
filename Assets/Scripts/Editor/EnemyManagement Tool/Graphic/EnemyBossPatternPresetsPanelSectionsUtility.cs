using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds top-level detail sections for the boss pattern preset panel.
/// </summary>
internal static class EnemyBossPatternPresetsPanelSectionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the metadata subsection for one boss pattern preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized preset context.</param>
    public static void BuildMetadataSection(EnemyBossPatternPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = EnemyBossPatternPresetsPanelSharedUtility.CreateDetailsSectionContainer(panel, "Preset Details");

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
        nameField.tooltip = "Human-readable boss pattern preset name shown in Enemy Management Tool.";
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            panel.HandlePresetNameChanged(evt.newValue);
        });
        sectionContainer.Add(nameField);

        EnemyBossPatternPresetsPanelSharedUtility.AddTrackedTextField(panel, sectionContainer, versionProperty, "Version", "Optional semantic version string for this boss pattern preset.", false);
        EnemyBossPatternPresetsPanelSharedUtility.AddTrackedTextField(panel, sectionContainer, descriptionProperty, "Description", "Optional editor-facing notes describing this boss pattern preset.", true);
        AddPresetIdRow(panel, sectionContainer, idProperty);
    }

    /// <summary>
    /// Builds the source normal-pattern preset assignment subsection.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized preset context.</param>
    public static void BuildSourcePatternsSection(EnemyBossPatternPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = EnemyBossPatternPresetsPanelSharedUtility.CreateDetailsSectionContainer(panel, "Module & Patterns Preset");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty sourcePatternsProperty = presetSerializedObject.FindProperty("sourcePatternsPreset");

        HelpBox infoBox = new HelpBox("Boss presets read assembled normal-enemy patterns from this source. Module definitions remain owned by Enemy Advanced Patterns Presets.", HelpBoxMessageType.Info);
        sectionContainer.Add(infoBox);

        ObjectField sourceField = new ObjectField("Source Patterns Preset");
        sourceField.objectType = typeof(EnemyModulesAndPatternsPreset);
        sourceField.allowSceneObjects = false;
        sourceField.tooltip = "Normal enemy Modules & Patterns preset used as the source catalog for boss pattern switching.";
        sourceField.SetValueWithoutNotify(sourcePatternsProperty.objectReferenceValue);
        sourceField.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Assign Boss Source Patterns Preset");
            presetSerializedObject.Update();
            sourcePatternsProperty.objectReferenceValue = evt.newValue as EnemyModulesAndPatternsPreset;
            presetSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel.SelectedPreset);
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
            panel.BuildActiveDetailsSection();
        });
        sectionContainer.Add(sourceField);

        EnemyModulesAndPatternsPreset sourcePreset = sourcePatternsProperty.objectReferenceValue as EnemyModulesAndPatternsPreset;

        if (sourcePreset == null)
        {
            sectionContainer.Add(new HelpBox("Assign a source preset before configuring boss Pattern Assemble slots.", HelpBoxMessageType.Warning));
            return;
        }

        int coreCount = sourcePreset.GetDefinitions(EnemyPatternModuleCatalogSection.CoreMovement).Count;
        int shortRangeCount = sourcePreset.GetDefinitions(EnemyPatternModuleCatalogSection.ShortRangeInteraction).Count;
        int weaponCount = sourcePreset.GetDefinitions(EnemyPatternModuleCatalogSection.WeaponInteraction).Count;
        sectionContainer.Add(new HelpBox("Available boss module catalog entries - Core: " + coreCount + ", Short-Range: " + shortRangeCount + ", Weapon: " + weaponCount + ".", HelpBoxMessageType.Info));
    }

    /// <summary>
    /// Builds the boss pattern assembly subsection.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized preset context.</param>
    public static void BuildPatternAssembleSection(EnemyBossPatternPresetsPanel panel)
    {
        EnemyBossPatternPresetsPanelPatternUtility.BuildPatternAssembleSection(panel);
    }

    /// <summary>
    /// Builds the minion spawning subsection for a boss pattern preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized preset context.</param>
    public static void BuildMinionSpawnSection(EnemyBossPatternPresetsPanel panel)
    {
        EnemyBossPatternPresetsPanelMinionUtility.BuildMinionSpawnSection(panel);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds the read-only preset ID row and regenerate action.
    /// </summary>
    /// <param name="panel">Owning panel that exposes the regenerate callback.</param>
    /// <param name="parent">Parent section container.</param>
    /// <param name="idProperty">Serialized ID property.</param>
    private static void AddPresetIdRow(EnemyBossPatternPresetsPanel panel, VisualElement parent, SerializedProperty idProperty)
    {
        if (parent == null || idProperty == null)
            return;

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
        regenerateButton.tooltip = "Generate a new stable ID for this boss pattern preset.";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);
        parent.Add(idRow);
    }
    #endregion

    #endregion
}
