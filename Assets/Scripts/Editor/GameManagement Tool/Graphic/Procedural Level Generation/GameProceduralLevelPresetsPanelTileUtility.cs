using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds reusable room tile cards and catalog-backed scene selection without exposing raw Scene IDs.
/// </summary>
internal static class GameProceduralLevelPresetsPanelTileUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds tile mutation actions and one editable card for every room tile in the selected level.
    /// </summary>
    /// <param name="panel">Panel supplying the selected preset and serialized object.</param>
    /// <param name="parent">Selected-level container receiving the tile section.</param>
    /// <param name="levelProperty">Serialized selected-level definition.</param>
    public static void BuildTileCards(GameProceduralLevelPresetsPanel panel, VisualElement parent, SerializedProperty levelProperty)
    {
        if (panel == null || parent == null || levelProperty == null)
            return;

        SerializedProperty tilesProperty = levelProperty.FindPropertyRelative("roomTiles");

        if (tilesProperty == null)
            return;

        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label heading = new Label("Room Tiles");
        heading.tooltip = "Reusable room scenes and per-level placement limits available to graph generation.";
        heading.style.unityFontStyleAndWeight = FontStyle.Bold;
        section.Add(heading);

        Button addButton = new Button(() => AddTile(panel, levelProperty));
        addButton.text = "Add Room Tile";
        addButton.tooltip = "Add a clean reusable room tile to the selected level.";
        addButton.style.width = 112f;
        addButton.style.marginTop = 4f;
        addButton.style.marginBottom = 6f;
        section.Add(addButton);

        if (tilesProperty.arraySize == 0)
        {
            Label emptyLabel = new Label("No room tiles are configured for this level.");
            emptyLabel.tooltip = "Add Start, Regular and Boss room tiles before generating a graph preview.";
            emptyLabel.style.whiteSpace = WhiteSpace.Normal;
            section.Add(emptyLabel);
            parent.Add(section);
            return;
        }

        for (int index = 0; index < tilesProperty.arraySize; index++)
            section.Add(BuildTileCard(panel, levelProperty, tilesProperty, index));

        parent.Add(section);
    }
    #endregion

    #region Card Methods
    /// <summary>
    /// Builds one responsive tile card with enum-like scene selection and ordered list actions.
    /// </summary>
    /// <param name="panel">Panel supplying preset and draft context.</param>
    /// <param name="levelProperty">Serialized owning level.</param>
    /// <param name="tilesProperty">Serialized room tile array.</param>
    /// <param name="tileIndex">Index of the tile represented by the card.</param>
    /// <returns>Configured tile card.</returns>
    private static VisualElement BuildTileCard(GameProceduralLevelPresetsPanel panel,
                                               SerializedProperty levelProperty,
                                               SerializedProperty tilesProperty,
                                               int tileIndex)
    {
        SerializedProperty tileProperty = tilesProperty.GetArrayElementAtIndex(tileIndex);
        SerializedProperty tileIdProperty = tileProperty.FindPropertyRelative("tileId");
        string tileLabel = tileIdProperty != null && !string.IsNullOrWhiteSpace(tileIdProperty.stringValue)
            ? tileIdProperty.stringValue
            : "Room Tile " + (tileIndex + 1);

        VisualElement card = new VisualElement();
        card.style.marginBottom = 8f;
        card.style.paddingLeft = 8f;
        card.style.paddingRight = 8f;
        card.style.paddingTop = 6f;
        card.style.paddingBottom = 6f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderTopWidth = 1f;
        card.style.borderBottomWidth = 1f;
        card.style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f);
        card.style.borderRightColor = new Color(0.35f, 0.35f, 0.35f);
        card.style.borderTopColor = new Color(0.35f, 0.35f, 0.35f);
        card.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);

        Label heading = new Label((tileIndex + 1) + ". " + tileLabel);
        heading.tooltip = "Room tile card ordered within the selected level's candidate set.";
        heading.style.unityFontStyleAndWeight = FontStyle.Bold;
        card.Add(heading);
        card.Add(BuildTileActionRow(panel, levelProperty, tileIndex, tilesProperty.arraySize));

        GameProceduralLevelPresetsPanelFieldUtility.AddDelayedText(card,
                                                                  tileIdProperty,
                                                                  "Tile ID",
                                                                  "Designer-facing identifier shown in validation diagnostics and graph preview nodes.",
                                                                  false,
                                                                  panel.BuildActiveSection);
        AddSceneSelector(panel, card, tileProperty);
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(card,
                                                                    tileProperty.FindPropertyRelative("role"),
                                                                    "Room Role",
                                                                    "Structural role used to enforce one Start room and one terminal Boss room per generated level.");
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(card,
                                                                    tileProperty.FindPropertyRelative("maximumCopies"),
                                                                    "Maximum Copies",
                                                                    "Maximum logical graph nodes that may reference this room scene in the current level.");
        GameProceduralLevelPresetsPanelTileDepthUtility.AddDepthFields(card, tileProperty);
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(card,
                                                                    tileProperty.FindPropertyRelative("baseSelectionWeight"),
                                                                    "Base Selection Weight",
                                                                    "Base candidate weight applied before enabled level rule scores are evaluated.");
        AddMetadataRecap(panel, card, tileProperty.FindPropertyRelative("sceneId"), levelProperty.FindPropertyRelative("useCenterArrival"));
        return card;
    }

    /// <summary>
    /// Builds duplicate, remove and reorder actions for one tile card.
    /// </summary>
    /// <param name="panel">Panel handling each action.</param>
    /// <param name="levelProperty">Serialized owning level.</param>
    /// <param name="tileIndex">Current tile index.</param>
    /// <param name="tileCount">Current number of tiles.</param>
    /// <returns>Wrapping tile action row.</returns>
    private static VisualElement BuildTileActionRow(GameProceduralLevelPresetsPanel panel,
                                                    SerializedProperty levelProperty,
                                                    int tileIndex,
                                                    int tileCount)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginBottom = 4f;

        Button duplicateButton = CreateActionButton("Duplicate", "Duplicate this tile with a fresh technical ID.", () => DuplicateTile(panel, levelProperty, tileIndex));
        row.Add(duplicateButton);

        Button removeButton = CreateActionButton("Remove", "Remove this tile from the selected level.", () => RemoveTile(panel, levelProperty, tileIndex));
        row.Add(removeButton);

        Button previousButton = CreateActionButton("<", "Move this tile earlier in the candidate list.", () => MoveTile(panel, levelProperty, tileIndex, -1));
        previousButton.SetEnabled(tileIndex > 0);
        row.Add(previousButton);

        Button nextButton = CreateActionButton(">", "Move this tile later in the candidate list.", () => MoveTile(panel, levelProperty, tileIndex, 1));
        nextButton.SetEnabled(tileIndex + 1 < tileCount);
        row.Add(nextButton);
        return row;
    }

    /// <summary>
    /// Creates one compact tile action button.
    /// </summary>
    /// <param name="text">Visible button text.</param>
    /// <param name="tooltip">Designer-facing action explanation.</param>
    /// <param name="action">Action invoked when clicked.</param>
    /// <returns>Configured action button.</returns>
    private static Button CreateActionButton(string text, string tooltip, Action action)
    {
        Button button = new Button(action);
        button.text = text;
        button.tooltip = tooltip;
        button.style.flexShrink = 0f;
        button.style.marginRight = 4f;
        return button;
    }
    #endregion

    #region Scene Selector Methods
    /// <summary>
    /// Builds a catalog-backed room scene popup and synchronizes stable Scene ID and GUID together.
    /// </summary>
    /// <param name="panel">Panel supplying the selected scene catalog.</param>
    /// <param name="parent">Tile card receiving the popup.</param>
    /// <param name="tileProperty">Serialized tile whose scene reference is edited.</param>
    private static void AddSceneSelector(GameProceduralLevelPresetsPanel panel, VisualElement parent, SerializedProperty tileProperty)
    {
        SerializedProperty sceneIdProperty = tileProperty.FindPropertyRelative("sceneId");
        SerializedProperty sceneGuidProperty = tileProperty.FindPropertyRelative("sceneGuid");

        if (sceneIdProperty == null || sceneGuidProperty == null)
            return;

        List<SceneChoice> choices = BuildSceneChoices(panel.SelectedPreset.SceneCatalogPreset, sceneIdProperty.stringValue, sceneGuidProperty.stringValue);
        List<string> labels = new List<string>(choices.Count);
        int selectedIndex = 0;

        for (int index = 0; index < choices.Count; index++)
        {
            labels.Add(choices[index].Label);

            if (string.Equals(choices[index].SceneId, sceneIdProperty.stringValue, StringComparison.Ordinal))
                selectedIndex = index;
        }

        PopupField<string> popup = new PopupField<string>("Room Scene", labels, selectedIndex);
        popup.tooltip = "Select a Gameplay scene from the assigned Scene Manager catalog. Raw Scene IDs cannot be typed manually.";
        popup.RegisterValueChangedCallback(evt =>
        {
            SceneChoice selectedChoice = FindChoice(choices, evt.newValue);

            if (selectedChoice == null)
                return;

            GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(panel.PresetSerializedObject, "Select Procedural Room Scene", () =>
            {
                sceneIdProperty.stringValue = selectedChoice.SceneId;
                sceneGuidProperty.stringValue = selectedChoice.SceneGuid;
            });
            panel.BuildActiveSection();
        });
        popup.SetEnabled(panel.SelectedPreset.SceneCatalogPreset != null);
        parent.Add(popup);

        Button refreshButton = new Button(() => GameProceduralLevelMetadataRefreshUiUtility.RefreshRoom(panel, sceneIdProperty.stringValue));
        refreshButton.text = "Refresh Room Metadata";
        refreshButton.tooltip = "Scan this selected room and its nested SubScenes, then update the cached portal and center-anchor recap.";
        refreshButton.style.width = 148f;
        refreshButton.style.marginTop = 2f;
        refreshButton.SetEnabled(!string.IsNullOrWhiteSpace(sceneIdProperty.stringValue));
        parent.Add(refreshButton);

        if (panel.SelectedPreset.SceneCatalogPreset == null)
        {
            Label catalogWarning = new Label("Assign a Scene Catalog Preset in Metadata to populate room choices.");
            catalogWarning.tooltip = "The scene selector remains disabled until a Scene Manager catalog is assigned.";
            catalogWarning.style.color = new Color(1f, 0.72f, 0.2f);
            catalogWarning.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(catalogWarning);
        }
    }

    /// <summary>
    /// Creates enum-like scene choices from valid Gameplay definitions and preserves stale references visibly.
    /// </summary>
    /// <param name="catalog">Scene Manager catalog supplying canonical definitions.</param>
    /// <param name="currentSceneId">Currently serialized Scene ID.</param>
    /// <param name="currentSceneGuid">Currently serialized Scene GUID.</param>
    /// <returns>Popup choices beginning with an explicit unassigned option.</returns>
    private static List<SceneChoice> BuildSceneChoices(GameSceneManagerPreset catalog, string currentSceneId, string currentSceneGuid)
    {
        List<SceneChoice> choices = new List<SceneChoice>();
        choices.Add(new SceneChoice("<Unassigned>", string.Empty, string.Empty));
        bool foundCurrentScene = string.IsNullOrWhiteSpace(currentSceneId);

        if (catalog != null)
        {
            for (int index = 0; index < catalog.SceneDefinitions.Count; index++)
            {
                GameSceneDefinition definition = catalog.SceneDefinitions[index];

                if (definition == null || definition.SceneKind != GameSceneKind.Gameplay || string.IsNullOrWhiteSpace(definition.SceneId))
                    continue;

                string readableName = string.IsNullOrWhiteSpace(definition.SceneName) ? definition.SceneId : definition.SceneName;
                choices.Add(new SceneChoice(readableName + " [" + definition.SceneId + "]", definition.SceneId, definition.SceneGuid));

                if (string.Equals(definition.SceneId, currentSceneId, StringComparison.Ordinal))
                    foundCurrentScene = true;
            }
        }

        if (!foundCurrentScene)
            choices.Add(new SceneChoice("<Missing Scene: " + currentSceneId + ">", currentSceneId, currentSceneGuid));

        return choices;
    }

    /// <summary>
    /// Resolves one popup label back to its immutable Scene ID and GUID choice.
    /// </summary>
    /// <param name="choices">Choice collection created for the current tile.</param>
    /// <param name="label">Selected popup label.</param>
    /// <returns>Matching scene choice, or null when the label is stale.</returns>
    private static SceneChoice FindChoice(List<SceneChoice> choices, string label)
    {
        for (int index = 0; index < choices.Count; index++)
        {
            if (string.Equals(choices[index].Label, label, StringComparison.Ordinal))
                return choices[index];
        }

        return null;
    }
    #endregion

    #region Metadata Methods
    /// <summary>
    /// Adds a read-only center-anchor or per-side portal multiplicity recap from cached scene metadata.
    /// </summary>
    /// <param name="panel">Panel supplying the selected preset metadata cache.</param>
    /// <param name="parent">Tile card receiving the recap.</param>
    /// <param name="sceneIdProperty">Serialized tile Scene ID.</param>
    /// <param name="centerArrivalProperty">Serialized level center-arrival toggle.</param>
    private static void AddMetadataRecap(GameProceduralLevelPresetsPanel panel,
                                         VisualElement parent,
                                         SerializedProperty sceneIdProperty,
                                         SerializedProperty centerArrivalProperty)
    {
        if (sceneIdProperty == null || string.IsNullOrWhiteSpace(sceneIdProperty.stringValue))
            return;

        bool hasMetadata = panel.SelectedPreset.TryFindRoomMetadata(sceneIdProperty.stringValue, out GameRoomSceneMetadata metadata);
        Label recap = new Label();
        recap.style.whiteSpace = WhiteSpace.Normal;
        recap.style.marginTop = 4f;

        if (!hasMetadata || metadata == null)
        {
            recap.text = "Room metadata: not cached.";
            recap.tooltip = "Portal scanner metadata is unavailable for the selected room scene.";
            recap.style.color = new Color(1f, 0.72f, 0.2f);
            parent.Add(recap);
            return;
        }

        if (centerArrivalProperty != null && centerArrivalProperty.boolValue)
        {
            recap.text = "Center anchors: " + metadata.CenterAnchorCount;
            recap.tooltip = "Center-arrival mode ignores portal fitting and requires exactly one center anchor in this room.";
            parent.Add(recap);
            return;
        }

        recap.text = BuildPortalRecap(metadata);
        recap.tooltip = "Cached individual portal multiplicity by side and capability. Same-side portals remain independent graph exits.";
        parent.Add(recap);
    }

    /// <summary>
    /// Formats cached per-side Entrance, Exit and Both counts without collapsing same-side physical portals.
    /// </summary>
    /// <param name="metadata">Room metadata snapshot being summarized.</param>
    /// <returns>Compact multiline side-capability recap.</returns>
    private static string BuildPortalRecap(GameRoomSceneMetadata metadata)
    {
        return "Portals by side (Entrance / Exit / Both)\n" +
               FormatSide(metadata, GameRoomPortalSide.North, "North") + "\n" +
               FormatSide(metadata, GameRoomPortalSide.South, "South") + "\n" +
               FormatSide(metadata, GameRoomPortalSide.East, "East") + "\n" +
               FormatSide(metadata, GameRoomPortalSide.West, "West");
    }

    /// <summary>
    /// Formats one side's three independent portal capability counts.
    /// </summary>
    /// <param name="metadata">Room metadata snapshot being counted.</param>
    /// <param name="side">Logical room side.</param>
    /// <param name="label">Readable side label.</param>
    /// <returns>One compact side summary line.</returns>
    private static string FormatSide(GameRoomSceneMetadata metadata, GameRoomPortalSide side, string label)
    {
        return label + ": " +
               metadata.CountPortals(side, GameRoomPortalCapability.Entrance) + " / " +
               metadata.CountPortals(side, GameRoomPortalCapability.Exit) + " / " +
               metadata.CountPortals(side, GameRoomPortalCapability.Both);
    }
    #endregion

    #region Mutation Methods
    /// <summary>
    /// Appends a clean room tile with stable identity and non-destructive authored defaults.
    /// </summary>
    /// <param name="panel">Panel whose preset receives the tile.</param>
    /// <param name="levelProperty">Serialized selected level.</param>
    private static void AddTile(GameProceduralLevelPresetsPanel panel, SerializedProperty levelProperty)
    {
        SerializedProperty tilesProperty = levelProperty.FindPropertyRelative("roomTiles");

        if (tilesProperty == null)
            return;

        GameProceduralLevelDefinition level = FindSelectedLevel(panel);
        string tileId = CreateUniqueTileId(level, "ROOM_TILE_" + (tilesProperty.arraySize + 1).ToString("00"));

        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(panel.PresetSerializedObject, "Add Procedural Room Tile", () =>
        {
            int newIndex = tilesProperty.arraySize;
            tilesProperty.InsertArrayElementAtIndex(newIndex);
            ResetTile(tilesProperty.GetArrayElementAtIndex(newIndex), Guid.NewGuid().ToString("N"), tileId);
        });
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Duplicates one room tile and regenerates its immutable technical and designer-facing IDs.
    /// </summary>
    /// <param name="panel">Panel whose preset receives the duplicate.</param>
    /// <param name="levelProperty">Serialized selected level.</param>
    /// <param name="tileIndex">Source tile index.</param>
    private static void DuplicateTile(GameProceduralLevelPresetsPanel panel, SerializedProperty levelProperty, int tileIndex)
    {
        SerializedProperty tilesProperty = levelProperty.FindPropertyRelative("roomTiles");
        GameProceduralLevelDefinition level = FindSelectedLevel(panel);

        if (tilesProperty == null || level == null || tileIndex < 0 || tileIndex >= level.RoomTiles.Count)
            return;

        GameProceduralRoomTileDefinition sourceTile = level.RoomTiles[tileIndex];

        if (sourceTile == null)
            return;

        string tileId = CreateUniqueTileId(level, sourceTile.TileId + "_COPY");

        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(panel.PresetSerializedObject, "Duplicate Procedural Room Tile", () =>
        {
            tilesProperty.InsertArrayElementAtIndex(tileIndex);
            SerializedProperty duplicateProperty = tilesProperty.GetArrayElementAtIndex(tileIndex);
            SetString(duplicateProperty, "technicalId", Guid.NewGuid().ToString("N"));
            SetString(duplicateProperty, "tileId", tileId);
            tilesProperty.MoveArrayElement(tileIndex, tileIndex + 1);
        });
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Removes one room tile after designer confirmation.
    /// </summary>
    /// <param name="panel">Panel whose preset loses the tile.</param>
    /// <param name="levelProperty">Serialized selected level.</param>
    /// <param name="tileIndex">Tile index to remove.</param>
    private static void RemoveTile(GameProceduralLevelPresetsPanel panel, SerializedProperty levelProperty, int tileIndex)
    {
        bool confirmed = EditorUtility.DisplayDialog("Remove Room Tile",
                                                     "Remove this room tile from the selected level?",
                                                     "Remove",
                                                     "Cancel");

        if (!confirmed)
            return;

        SerializedProperty tilesProperty = levelProperty.FindPropertyRelative("roomTiles");

        if (tilesProperty == null || tileIndex < 0 || tileIndex >= tilesProperty.arraySize)
            return;

        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(panel.PresetSerializedObject, "Remove Procedural Room Tile", () =>
        {
            tilesProperty.DeleteArrayElementAtIndex(tileIndex);
        });
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Reorders one room tile without changing its technical identity or authored configuration.
    /// </summary>
    /// <param name="panel">Panel owning the serialized tile list.</param>
    /// <param name="levelProperty">Serialized selected level.</param>
    /// <param name="tileIndex">Current tile index.</param>
    /// <param name="direction">Negative for earlier, positive for later.</param>
    private static void MoveTile(GameProceduralLevelPresetsPanel panel, SerializedProperty levelProperty, int tileIndex, int direction)
    {
        SerializedProperty tilesProperty = levelProperty.FindPropertyRelative("roomTiles");
        int targetIndex = tileIndex + direction;

        if (tilesProperty == null || targetIndex < 0 || targetIndex >= tilesProperty.arraySize)
            return;

        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(panel.PresetSerializedObject, "Reorder Procedural Room Tile", () =>
        {
            tilesProperty.MoveArrayElement(tileIndex, targetIndex);
        });
        panel.BuildActiveSection();
    }
    #endregion

    #region Identity and Serialization Methods
    /// <summary>
    /// Finds the selected level definition by its immutable technical ID.
    /// </summary>
    /// <param name="panel">Panel supplying preset and selected technical ID.</param>
    /// <returns>Selected level definition, or null when state is stale.</returns>
    private static GameProceduralLevelDefinition FindSelectedLevel(GameProceduralLevelPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return null;

        for (int index = 0; index < panel.SelectedPreset.Levels.Count; index++)
        {
            GameProceduralLevelDefinition level = panel.SelectedPreset.Levels[index];

            if (level != null && string.Equals(level.TechnicalId, panel.SelectedLevelTechnicalId, StringComparison.Ordinal))
                return level;
        }

        return null;
    }

    /// <summary>
    /// Produces a unique designer-facing tile ID within the selected level.
    /// </summary>
    /// <param name="level">Level whose tile IDs must remain unique.</param>
    /// <param name="requestedId">Preferred base tile ID.</param>
    /// <returns>Requested ID or a numbered unique derivative.</returns>
    private static string CreateUniqueTileId(GameProceduralLevelDefinition level, string requestedId)
    {
        string baseId = string.IsNullOrWhiteSpace(requestedId) ? "ROOM_TILE" : requestedId;
        string candidateId = baseId;
        int suffix = 2;

        while (ContainsTileId(level, candidateId))
        {
            candidateId = baseId + "_" + suffix;
            suffix++;
        }

        return candidateId;
    }

    /// <summary>
    /// Checks whether a selected level already contains one exact tile ID.
    /// </summary>
    /// <param name="level">Level inspected for duplicates.</param>
    /// <param name="tileId">Exact ordinal tile ID.</param>
    /// <returns>True when a tile already owns the ID.</returns>
    private static bool ContainsTileId(GameProceduralLevelDefinition level, string tileId)
    {
        if (level == null)
            return false;

        for (int index = 0; index < level.RoomTiles.Count; index++)
        {
            GameProceduralRoomTileDefinition tile = level.RoomTiles[index];

            if (tile != null && string.Equals(tile.TileId, tileId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resets a newly appended tile so array insertion never leaks values from the previous element.
    /// </summary>
    /// <param name="tileProperty">Serialized new tile element.</param>
    /// <param name="technicalId">Fresh immutable tile technical identity.</param>
    /// <param name="tileId">Unique designer-facing tile ID.</param>
    private static void ResetTile(SerializedProperty tileProperty, string technicalId, string tileId)
    {
        SetString(tileProperty, "technicalId", technicalId);
        SetString(tileProperty, "tileId", tileId);
        SetString(tileProperty, "sceneId", string.Empty);
        SetString(tileProperty, "sceneGuid", string.Empty);
        SetInteger(tileProperty, "role", (int)GameProceduralRoomRole.Regular);
        SetInteger(tileProperty, "maximumCopies", 1);
        SetBool(tileProperty, "useExactDepthConstraint", false);
        SetInteger(tileProperty, "exactDepth", 1);
        SetVector2Int(tileProperty, "preferredDepthRange", new Vector2Int(1, 8));
        SetFloat(tileProperty, "baseSelectionWeight", 1f);
    }

    /// <summary>
    /// Sets a relative serialized string when the field exists.
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
    /// Sets a relative serialized enum or integer when the field exists.
    /// </summary>
    /// <param name="parent">Serialized parent object.</param>
    /// <param name="propertyName">Relative integer field name.</param>
    /// <param name="value">Integer value to assign.</param>
    private static void SetInteger(SerializedProperty parent, string propertyName, int value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;

        if (property == null)
            return;

        if (property.propertyType == SerializedPropertyType.Enum)
            property.enumValueIndex = value;
        else
            property.intValue = value;
    }

    /// <summary>
    /// Sets a relative serialized boolean when the field exists.
    /// </summary>
    /// <param name="parent">Serialized parent object.</param>
    /// <param name="propertyName">Relative boolean field name.</param>
    /// <param name="value">Boolean value to assign.</param>
    private static void SetBool(SerializedProperty parent, string propertyName, bool value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Sets a relative serialized float when the field exists.
    /// </summary>
    /// <param name="parent">Serialized parent object.</param>
    /// <param name="propertyName">Relative float field name.</param>
    /// <param name="value">Float value to assign.</param>
    private static void SetFloat(SerializedProperty parent, string propertyName, float value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>
    /// Sets a relative serialized Vector2Int when the field exists.
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

    #region Nested Types
    /// <summary>
    /// Maps one readable popup label to its canonical Scene Manager ID and Unity GUID.
    /// </summary>
    private sealed class SceneChoice
    {
        #region Properties
        public string Label { get; }
        public string SceneId { get; }
        public string SceneGuid { get; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates one immutable enum-like room scene choice.
        /// </summary>
        /// <param name="label">Readable popup label.</param>
        /// <param name="sceneId">Canonical Scene Manager scene ID.</param>
        /// <param name="sceneGuid">Unity scene asset GUID.</param>
        public SceneChoice(string label, string sceneId, string sceneGuid)
        {
            Label = label;
            SceneId = sceneId;
            SceneGuid = sceneGuid;
        }
        #endregion
    }
    #endregion
}
