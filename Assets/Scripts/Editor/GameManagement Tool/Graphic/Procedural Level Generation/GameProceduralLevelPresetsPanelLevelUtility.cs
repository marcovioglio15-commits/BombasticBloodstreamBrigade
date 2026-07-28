using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds stable-ID level chips and performs draft-aware add, duplicate, remove and reorder operations.
/// </summary>
internal static class GameProceduralLevelPresetsPanelLevelUtility
{
    #region Colors
    private static readonly Color selectedChipColor = new Color(0.20f, 0.42f, 0.64f, 0.8f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current nested level selection by technical ID and falls back to the first valid level.
    /// </summary>
    /// <param name="panel">Panel whose stable level selection is validated.</param>
    public static void ResolveSelectedLevel(GameProceduralLevelPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        // Preserve the current technical ID only while its level still exists.
        for (int index = 0; index < panel.SelectedPreset.Levels.Count; index++)
        {
            GameProceduralLevelDefinition level = panel.SelectedPreset.Levels[index];

            if (level == null || !string.Equals(level.TechnicalId, panel.SelectedLevelTechnicalId, StringComparison.Ordinal))
                continue;

            GameProceduralLevelPresetsPanelStateUtility.SaveSelectedLevelTechnicalId(panel.SelectedPreset, panel.SelectedLevelTechnicalId);
            return;
        }

        // Select the first initialized level when persisted state is missing or stale.
        panel.SelectedLevelTechnicalId = string.Empty;

        for (int index = 0; index < panel.SelectedPreset.Levels.Count; index++)
        {
            GameProceduralLevelDefinition level = panel.SelectedPreset.Levels[index];

            if (level == null)
                continue;

            panel.SelectedLevelTechnicalId = level.TechnicalId;
            break;
        }

        GameProceduralLevelPresetsPanelStateUtility.SaveSelectedLevelTechnicalId(panel.SelectedPreset, panel.SelectedLevelTechnicalId);
    }

    /// <summary>
    /// Builds level navigation chips, mutation controls, selected-level settings and tile cards.
    /// </summary>
    /// <param name="panel">Panel supplying serialized level data and stable selection state.</param>
    public static void BuildLevelsSection(GameProceduralLevelPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null || panel.PresetSerializedObject == null || panel.SectionContentRoot == null)
            return;

        ResolveSelectedLevel(panel);
        SerializedProperty levelsProperty = panel.PresetSerializedObject.FindProperty("levels");

        if (levelsProperty == null)
            return;

        VisualElement root = new VisualElement();
        root.style.flexGrow = 1f;
        root.Add(BuildLevelActionRow(panel));
        root.Add(BuildLevelChips(panel));

        int selectedIndex = FindLevelIndex(panel, panel.SelectedLevelTechnicalId);

        if (selectedIndex < 0 || selectedIndex >= levelsProperty.arraySize)
        {
            Label emptyLabel = new Label("Add a level to configure its rules and reusable room tiles.");
            emptyLabel.tooltip = "Each Procedural Level preset may contain an ordered list of independently generated levels.";
            emptyLabel.style.whiteSpace = WhiteSpace.Normal;
            emptyLabel.style.marginTop = 8f;
            root.Add(emptyLabel);
            panel.SectionContentRoot.Add(root);
            return;
        }

        SerializedProperty levelProperty = levelsProperty.GetArrayElementAtIndex(selectedIndex);
        root.Add(BuildSelectedLevelHeader(panel, selectedIndex));
        BuildSelectedLevelFields(panel, root, levelProperty);
        GameProceduralLevelPresetsPanelTileUtility.BuildTileCards(panel, root, levelProperty);
        panel.SectionContentRoot.Add(root);
    }
    #endregion

    #region Level Mutation Methods
    /// <summary>
    /// Appends a clean level definition with a unique  ID and stable technical identity.
    /// </summary>
    /// <param name="panel">Panel whose selected preset receives the level.</param>
    private static void AddLevel(GameProceduralLevelPresetsPanel panel)
    {
        if (panel == null || panel.PresetSerializedObject == null)
            return;

        SerializedProperty levelsProperty = panel.PresetSerializedObject.FindProperty("levels");

        if (levelsProperty == null)
            return;

        string technicalId = Guid.NewGuid().ToString("N");
        string levelId = CreateUniqueLevelId(panel.SelectedPreset, "LEVEL_" + (levelsProperty.arraySize + 1).ToString("00"));

        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(panel.PresetSerializedObject, "Add Procedural Level", () =>
        {
            int newIndex = levelsProperty.arraySize;
            levelsProperty.InsertArrayElementAtIndex(newIndex);
            ResetLevel(levelsProperty.GetArrayElementAtIndex(newIndex), technicalId, levelId, "Level " + (newIndex + 1).ToString("00"));
        });

        panel.SelectedLevelTechnicalId = technicalId;
        GameProceduralLevelPresetsPanelStateUtility.SaveSelectedLevelTechnicalId(panel.SelectedPreset, technicalId);
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Duplicates the selected level while assigning fresh level and nested tile technical identities.
    /// </summary>
    /// <param name="panel">Panel whose selected preset receives the duplicate.</param>
    private static void DuplicateSelectedLevel(GameProceduralLevelPresetsPanel panel)
    {
        int sourceIndex = FindLevelIndex(panel, panel.SelectedLevelTechnicalId);

        if (sourceIndex < 0 || panel.PresetSerializedObject == null)
            return;

        GameProceduralLevelDefinition sourceLevel = panel.SelectedPreset.Levels[sourceIndex];
        SerializedProperty levelsProperty = panel.PresetSerializedObject.FindProperty("levels");

        if (sourceLevel == null || levelsProperty == null)
            return;

        string technicalId = Guid.NewGuid().ToString("N");
        string levelId = CreateUniqueLevelId(panel.SelectedPreset, sourceLevel.LevelId + "_COPY");

        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(panel.PresetSerializedObject, "Duplicate Procedural Level", () =>
        {
            levelsProperty.InsertArrayElementAtIndex(sourceIndex);
            SerializedProperty duplicateProperty = levelsProperty.GetArrayElementAtIndex(sourceIndex);
            SetString(duplicateProperty, "technicalId", technicalId);
            SetString(duplicateProperty, "levelId", levelId);
            SetString(duplicateProperty, "displayName", sourceLevel.DisplayName + " Copy");
            RegenerateTileTechnicalIds(duplicateProperty.FindPropertyRelative("roomTiles"));
            levelsProperty.MoveArrayElement(sourceIndex, sourceIndex + 1);
        });

        panel.SelectedLevelTechnicalId = technicalId;
        GameProceduralLevelPresetsPanelStateUtility.SaveSelectedLevelTechnicalId(panel.SelectedPreset, technicalId);
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Removes the selected level after confirmation and resolves a neighboring stable selection.
    /// </summary>
    /// <param name="panel">Panel whose selected preset loses the level.</param>
    private static void RemoveSelectedLevel(GameProceduralLevelPresetsPanel panel)
    {
        int selectedIndex = FindLevelIndex(panel, panel.SelectedLevelTechnicalId);

        if (selectedIndex < 0 || panel.PresetSerializedObject == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Remove Procedural Level",
                                                     "Remove the selected level and all of its room tiles from this preset?",
                                                     "Remove",
                                                     "Cancel");

        if (!confirmed)
            return;

        SerializedProperty levelsProperty = panel.PresetSerializedObject.FindProperty("levels");

        if (levelsProperty == null)
            return;

        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(panel.PresetSerializedObject, "Remove Procedural Level", () =>
        {
            levelsProperty.DeleteArrayElementAtIndex(selectedIndex);
        });

        panel.SelectedLevelTechnicalId = string.Empty;
        panel.SelectedPreset.EnsureInitialized();
        ResolveSelectedLevel(panel);
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Moves the selected level by one ordered run-progression position without changing its identity.
    /// </summary>
    /// <param name="panel">Panel owning the serialized level list.</param>
    /// <param name="direction">Negative for earlier, positive for later.</param>
    private static void MoveSelectedLevel(GameProceduralLevelPresetsPanel panel, int direction)
    {
        int currentIndex = FindLevelIndex(panel, panel.SelectedLevelTechnicalId);

        if (currentIndex < 0 || panel.PresetSerializedObject == null)
            return;

        SerializedProperty levelsProperty = panel.PresetSerializedObject.FindProperty("levels");
        int targetIndex = currentIndex + direction;

        if (levelsProperty == null || targetIndex < 0 || targetIndex >= levelsProperty.arraySize)
            return;

        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(panel.PresetSerializedObject, "Reorder Procedural Level", () =>
        {
            levelsProperty.MoveArrayElement(currentIndex, targetIndex);
        });
        panel.BuildActiveSection();
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Builds level add, duplicate, remove and reorder actions.
    /// </summary>
    /// <param name="panel">Panel handling level actions.</param>
    /// <returns>Wrapping action row.</returns>
    private static VisualElement BuildLevelActionRow(GameProceduralLevelPresetsPanel panel)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginBottom = 4f;
        AddActionButton(row, "Add Level", "Add a clean ordered level with a unique Level ID.", 76f, () => AddLevel(panel));
        AddActionButton(row, "Duplicate", "Duplicate the selected level and regenerate all technical IDs.", 72f, () => DuplicateSelectedLevel(panel));
        AddActionButton(row, "Remove", "Remove the selected level after confirmation.", 64f, () => RemoveSelectedLevel(panel));
        AddActionButton(row, "<", "Move the selected level earlier in run progression.", 30f, () => MoveSelectedLevel(panel, -1));
        AddActionButton(row, ">", "Move the selected level later in run progression.", 30f, () => MoveSelectedLevel(panel, 1));
        return row;
    }

    /// <summary>
    /// Builds horizontally scrollable chips bound to immutable level technical IDs.
    /// </summary>
    /// <param name="panel">Panel receiving chip selection callbacks.</param>
    /// <returns>Horizontal level chip scroll view.</returns>
    private static VisualElement BuildLevelChips(GameProceduralLevelPresetsPanel panel)
    {
        ScrollView scrollView = new ScrollView(ScrollViewMode.Horizontal);
        scrollView.tooltip = "Ordered procedural levels. Selection persists by technical ID across reorder and rename operations.";
        scrollView.style.maxHeight = 44f;
        scrollView.style.marginBottom = 8f;

        for (int index = 0; index < panel.SelectedPreset.Levels.Count; index++)
        {
            GameProceduralLevelDefinition level = panel.SelectedPreset.Levels[index];

            if (level == null)
                continue;

            string technicalId = level.TechnicalId;
            string label = string.IsNullOrWhiteSpace(level.DisplayName) ? level.LevelId : level.DisplayName;
            Button chip = new Button(() => SelectLevel(panel, technicalId));
            chip.text = label;
            chip.tooltip = "Select " + level.LevelId + " by its immutable technical ID.";
            chip.style.flexShrink = 0f;
            chip.style.marginRight = 4f;
            chip.style.backgroundColor = string.Equals(technicalId, panel.SelectedLevelTechnicalId, StringComparison.Ordinal)
                ? selectedChipColor
                : Color.clear;
            scrollView.Add(chip);
        }

        return scrollView;
    }

    /// <summary>
    /// Builds selected-level ordering and graph-preview actions.
    /// </summary>
    /// <param name="panel">Panel supplying preview context.</param>
    /// <param name="selectedIndex">Current ordered level index.</param>
    /// <returns>Header containing level order and preview action.</returns>
    private static VisualElement BuildSelectedLevelHeader(GameProceduralLevelPresetsPanel panel, int selectedIndex)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 6f;

        Label orderLabel = new Label("Level Order " + (selectedIndex + 1));
        orderLabel.tooltip = "One-based traversal order of the selected level in this preset.";
        orderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(orderLabel);

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.flexWrap = Wrap.Wrap;
        actions.style.justifyContent = Justify.FlexEnd;
        AddActionButton(actions,
                        "Refresh Level Metadata",
                        "Scan every unique room referenced by this level and update its portal and center-anchor recap.",
                        152f,
                        () => GameProceduralLevelMetadataRefreshUiUtility.RefreshSelectedLevelRooms(panel));

        Button previewButton = new Button(() => GameProceduralLevelGraphPreviewWindow.Open(panel,
                                                                                            panel.SelectedPreset,
                                                                                            panel.SelectedLevelTechnicalId));
        previewButton.text = "Open Graph Preview";
        bool hasCompatibleRuntimeSceneCatalog = panel.HasCompatibleRuntimeSceneCatalog();
        previewButton.tooltip = hasCompatibleRuntimeSceneCatalog
            ? "Open the zoomable deterministic graph example for this level and regenerate it without entering Play Mode."
            : "Preview is disabled until the Procedural Level catalog matches the Scene Manager preset assigned to the current Game Master.";
        previewButton.style.flexShrink = 0f;
        previewButton.SetEnabled(hasCompatibleRuntimeSceneCatalog);
        actions.Add(previewButton);
        row.Add(actions);
        return row;
    }

    /// <summary>
    /// Builds selected-level identity, node ranges, rule weights and context-sensitive center-arrival controls.
    /// </summary>
    /// <param name="panel">Panel supplying serialized context.</param>
    /// <param name="parent">Container receiving selected-level controls.</param>
    /// <param name="levelProperty">Serialized selected-level definition.</param>
    private static void BuildSelectedLevelFields(GameProceduralLevelPresetsPanel panel, VisualElement parent, SerializedProperty levelProperty)
    {
        VisualElement identitySection = CreateSubSection(parent, "Level Identity and Progression");
        GameProceduralLevelPresetsPanelFieldUtility.AddDelayedText(identitySection,
                                                                  levelProperty.FindPropertyRelative("levelId"),
                                                                  "Level ID",
                                                                  "-authored stable runtime progression and diagnostics identifier.",
                                                                  false,
                                                                  panel.BuildActiveSection);
        GameProceduralLevelPresetsPanelFieldUtility.AddDelayedText(identitySection,
                                                                  levelProperty.FindPropertyRelative("displayName"),
                                                                  "Display Name",
                                                                  "Readable label shown by level chips and graph preview nodes.",
                                                                  false,
                                                                  panel.BuildActiveSection);
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(identitySection,
                                                                    levelProperty.FindPropertyRelative("enabled"),
                                                                    "Enabled",
                                                                    "Includes this level in ordered run progression and graph generation.");
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(identitySection,
                                                                    levelProperty.FindPropertyRelative("targetNodeCountRange"),
                                                                    "Target Node Count Range",
                                                                    "Inclusive preferred range for the total logical rooms generated in this level.");
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(identitySection,
                                                                    levelProperty.FindPropertyRelative("preferredBossDepthRange"),
                                                                    "Preferred Boss Depth Range",
                                                                    "Inclusive terminal depth range ranked by the Boss depth rule score.");
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(identitySection,
                                                                    levelProperty.FindPropertyRelative("requireRoomClearBeforeExit"),
                                                                    "Require Room Clear Before Exit",
                                                                    "Keeps traversable exits blocked until this room reports its one-shot completion event.");

        SerializedProperty centerArrivalProperty = levelProperty.FindPropertyRelative("useCenterArrival");
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(identitySection,
                                                                    centerArrivalProperty,
                                                                    "Use Center Arrival",
                                                                    "Places the player at the target room center anchor and skips every portal-side compatibility check.",
                                                                    panel.BuildActiveSection);

        VisualElement ruleSection = CreateSubSection(parent, "Rule Scores");
        SerializedProperty ruleSettingsProperty = levelProperty.FindPropertyRelative("ruleSettings");

        if (ruleSettingsProperty != null)
        {
            GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(ruleSection,
                                                                        ruleSettingsProperty.FindPropertyRelative("roomDepthScore"),
                                                                        "Room Depth Score",
                                                                        "Weight applied when candidates are ranked against each tile's preferred depth range.");
            GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(ruleSection,
                                                                        ruleSettingsProperty.FindPropertyRelative("bossDepthScore"),
                                                                        "Boss Depth Score",
                                                                        "Weight applied when valid terminal Boss depths are ranked.");

            if (centerArrivalProperty != null && !centerArrivalProperty.boolValue)
            {
                GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(ruleSection,
                                                                            ruleSettingsProperty.FindPropertyRelative("fittingScore"),
                                                                            "Fitting Score",
                                                                            "Weight applied to valid opposite-side portal capacity and future frontier quality.");
            }
            else
            {
                Label fittingInfo = new Label("Fitting is disabled because this level uses center arrival.");
                fittingInfo.tooltip = "Center-arrival generation intentionally skips exit-to-entrance compatibility and portal fitting scoring.";
                fittingInfo.style.whiteSpace = WhiteSpace.Normal;
                ruleSection.Add(fittingInfo);
            }
        }
    }

    /// <summary>
    /// Adds one compact action button with a predictable minimum width.
    /// </summary>
    /// <param name="parent">Action row receiving the button.</param>
    /// <param name="text">Visible button text.</param>
    /// <param name="tooltip">-facing action explanation.</param>
    /// <param name="width">Fixed readable width.</param>
    /// <param name="action">Action invoked by the button.</param>
    private static void AddActionButton(VisualElement parent, string text, string tooltip, float width, Action action)
    {
        Button button = new Button(action);
        button.text = text;
        button.tooltip = tooltip;
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(button, width);
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Creates a visually separated selected-level subsection.
    /// </summary>
    /// <param name="parent">Container receiving the subsection.</param>
    /// <param name="title">Visible subsection title.</param>
    /// <returns>Subsection container ready for controls.</returns>
    private static VisualElement CreateSubSection(VisualElement parent, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label heading = new Label(title);
        heading.tooltip = "Selected level section: " + title + ".";
        heading.style.unityFontStyleAndWeight = FontStyle.Bold;
        section.Add(heading);
        parent.Add(section);
        return section;
    }
    #endregion

    #region Identity Methods
    /// <summary>
    /// Changes the selected nested level and persists its immutable technical ID.
    /// </summary>
    /// <param name="panel">Panel receiving the nested selection.</param>
    /// <param name="technicalId">Technical ID of the selected level.</param>
    private static void SelectLevel(GameProceduralLevelPresetsPanel panel, string technicalId)
    {
        if (panel == null || string.Equals(panel.SelectedLevelTechnicalId, technicalId, StringComparison.Ordinal))
            return;

        panel.SelectedLevelTechnicalId = technicalId;
        GameProceduralLevelPresetsPanelStateUtility.SaveSelectedLevelTechnicalId(panel.SelectedPreset, technicalId);
        int selectedIndex = FindLevelIndex(panel, technicalId);

        if (selectedIndex >= 0)
        {
            GameRoomMetadataRefreshReport report = GameRoomMetadataAutomaticRefreshUtility.RefreshStaleLevelRooms(panel.SelectedPreset,
                                                                                                                   panel.SelectedPreset.Levels[selectedIndex]);
            GameRoomMetadataAutomaticRefreshUtility.MarkDraftDirtyWhenChanged(report);

            if (!report.Succeeded)
                Debug.LogWarning("[GameRoomMetadata] Level-load automatic refresh kept invalid rooms stale: " + string.Join(" | ", report.Errors));
        }

        panel.BuildActiveSection();
    }

    /// <summary>
    /// Finds a level index by immutable technical ID rather than list position or mutable display ID.
    /// </summary>
    /// <param name="panel">Panel owning the level list.</param>
    /// <param name="technicalId">Technical ID to locate.</param>
    /// <returns>Level index, or -1 when no exact match exists.</returns>
    private static int FindLevelIndex(GameProceduralLevelPresetsPanel panel, string technicalId)
    {
        if (panel == null || panel.SelectedPreset == null || string.IsNullOrWhiteSpace(technicalId))
            return -1;

        for (int index = 0; index < panel.SelectedPreset.Levels.Count; index++)
        {
            GameProceduralLevelDefinition level = panel.SelectedPreset.Levels[index];

            if (level != null && string.Equals(level.TechnicalId, technicalId, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Produces a unique -facing Level ID without mutating existing definitions.
    /// </summary>
    /// <param name="preset">Preset whose Level IDs must remain unique.</param>
    /// <param name="requestedId">Preferred base ID.</param>
    /// <returns>Requested ID or a numbered unique derivative.</returns>
    private static string CreateUniqueLevelId(GameProceduralLevelPreset preset, string requestedId)
    {
        string baseId = string.IsNullOrWhiteSpace(requestedId) ? "LEVEL" : requestedId;
        string candidateId = baseId;
        int suffix = 2;

        while (ContainsLevelId(preset, candidateId))
        {
            candidateId = baseId + "_" + suffix;
            suffix++;
        }

        return candidateId;
    }

    /// <summary>
    /// Checks whether the preset already contains one exact -facing Level ID.
    /// </summary>
    /// <param name="preset">Preset inspected for duplicates.</param>
    /// <param name="levelId">Exact ordinal Level ID candidate.</param>
    /// <returns>True when the ID is already present.</returns>
    private static bool ContainsLevelId(GameProceduralLevelPreset preset, string levelId)
    {
        if (preset == null)
            return false;

        for (int index = 0; index < preset.Levels.Count; index++)
        {
            GameProceduralLevelDefinition level = preset.Levels[index];

            if (level != null && string.Equals(level.LevelId, levelId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
    #endregion

    #region Serialization Methods
    /// <summary>
    /// Resets a newly appended serialized level to authored defaults rather than retaining duplicated array data.
    /// </summary>
    /// <param name="levelProperty">Serialized new level element.</param>
    /// <param name="technicalId">Fresh immutable technical identity.</param>
    /// <param name="levelId">Unique -facing Level ID.</param>
    /// <param name="displayName">Initial chip and preview label.</param>
    private static void ResetLevel(SerializedProperty levelProperty, string technicalId, string levelId, string displayName)
    {
        SetString(levelProperty, "technicalId", technicalId);
        SetString(levelProperty, "levelId", levelId);
        SetString(levelProperty, "displayName", displayName);
        SetBoolean(levelProperty, "enabled", true);
        SetVector2Int(levelProperty, "targetNodeCountRange", new Vector2Int(8, 14));
        SetVector2Int(levelProperty, "preferredBossDepthRange", new Vector2Int(5, 8));
        SetBoolean(levelProperty, "requireRoomClearBeforeExit", true);
        SetBoolean(levelProperty, "useCenterArrival", false);

        SerializedProperty ruleProperty = levelProperty.FindPropertyRelative("ruleSettings");

        if (ruleProperty != null)
        {
            SetFloat(ruleProperty, "roomDepthScore", 1f);
            SetFloat(ruleProperty, "bossDepthScore", 1f);
            SetFloat(ruleProperty, "fittingScore", 1f);
        }

        SerializedProperty tilesProperty = levelProperty.FindPropertyRelative("roomTiles");

        if (tilesProperty != null)
            tilesProperty.arraySize = 0;
    }

    /// <summary>
    /// Assigns fresh technical identities to every tile copied with a duplicated level.
    /// </summary>
    /// <param name="tilesProperty">Serialized duplicated tile array.</param>
    private static void RegenerateTileTechnicalIds(SerializedProperty tilesProperty)
    {
        if (tilesProperty == null)
            return;

        for (int index = 0; index < tilesProperty.arraySize; index++)
            SetString(tilesProperty.GetArrayElementAtIndex(index), "technicalId", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Sets a relative serialized string when the expected field exists.
    /// </summary>
    /// <param name="parent">Serialized parent object.</param>
    /// <param name="propertyName">Relative string field name.</param>
    /// <param name="value">String value to assign.</param>
    private static void SetString(SerializedProperty parent, string propertyName, string value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;

        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Sets a relative serialized boolean when the expected field exists.
    /// </summary>
    /// <param name="parent">Serialized parent object.</param>
    /// <param name="propertyName">Relative boolean field name.</param>
    /// <param name="value">Boolean value to assign.</param>
    private static void SetBoolean(SerializedProperty parent, string propertyName, bool value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Sets a relative serialized float when the expected field exists.
    /// </summary>
    /// <param name="parent">Serialized parent object.</param>
    /// <param name="propertyName">Relative float field name.</param>
    /// <param name="value">Floating-point value to assign.</param>
    private static void SetFloat(SerializedProperty parent, string propertyName, float value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>
    /// Sets a relative serialized Vector2Int when the expected field exists.
    /// </summary>
    /// <param name="parent">Serialized parent object.</param>
    /// <param name="propertyName">Relative vector field name.</param>
    /// <param name="value">Vector value to assign.</param>
    private static void SetVector2Int(SerializedProperty parent, string propertyName, Vector2Int value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;

        if (property != null)
            property.vector2IntValue = value;
    }
    #endregion

    #endregion
}
