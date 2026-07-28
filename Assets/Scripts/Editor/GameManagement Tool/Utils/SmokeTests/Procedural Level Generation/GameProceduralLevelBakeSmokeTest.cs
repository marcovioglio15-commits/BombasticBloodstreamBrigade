#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies that authored procedural levels and cached room signatures flatten into stable ECS buffer ranges.
/// </summary>
public static class GameProceduralLevelBakeSmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes deterministic bake-buffer checks from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        GameProceduralLevelPreset preset = CreatePreset();
        World world = new World("GameProceduralLevelBakeSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = entityManager.CreateEntity();
            entityManager.AddBuffer<GameProceduralLevelDefinitionElement>(managerEntity);
            entityManager.AddBuffer<GameProceduralRoomTileElement>(managerEntity);
            entityManager.AddBuffer<GameProceduralRoomMetadataElement>(managerEntity);
            entityManager.AddBuffer<GameProceduralRoomPortalDefinitionElement>(managerEntity);
            DynamicBuffer<GameProceduralLevelDefinitionElement> levels = entityManager.GetBuffer<GameProceduralLevelDefinitionElement>(managerEntity);
            DynamicBuffer<GameProceduralRoomTileElement> tiles = entityManager.GetBuffer<GameProceduralRoomTileElement>(managerEntity);
            DynamicBuffer<GameProceduralRoomMetadataElement> metadata = entityManager.GetBuffer<GameProceduralRoomMetadataElement>(managerEntity);
            DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portals = entityManager.GetBuffer<GameProceduralRoomPortalDefinitionElement>(managerEntity);

            GameProceduralLevelBakeUtility.PopulateLevelBuffers(preset, levels, tiles);
            GameProceduralLevelBakeUtility.PopulateMetadataBuffers(preset, metadata, portals);

            ValidateLevelRanges(levels, tiles);
            ValidateMetadataRanges(metadata, portals);
            ValidateTileMetadataIndices(tiles);
            ValidateTileDepthConstraints(tiles);
            GameProceduralLevelGraphPreviewSmokeUtility.Validate();

            GameProceduralLevelConfig config = GameProceduralLevelBakeUtility.BuildConfig(preset);
            Require(config.PresetId.ToString() == "PRESET_BAKE_SMOKE",
                    "The baked global config did not preserve the preset ID.");
            Require(config.HideLoadingProgressDuringRoomTransitions != 0,
                    "The baked global config did not preserve room-transition loading suppression.");
            ValidateTransactionalStreamingConfig(config);
            ValidateFixedStringBakeGuards(preset,
                                          levels,
                                          tiles,
                                          metadata,
                                          portals);
            ValidateRuntimeCatalogMismatch(preset);

            Debug.Log("[GameProceduralLevelBakeSmokeTest] All flattened bake-buffer checks passed.");
        }
        finally
        {
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }
    #endregion

    #region Fixture Methods
    /// <summary>
    /// Creates an unsaved preset containing two ordered levels and two deduplicated metadata signatures.
    /// </summary>
    /// <returns>Transient configured procedural preset.</returns>
    private static GameProceduralLevelPreset CreatePreset()
    {
        GameProceduralLevelPreset preset = ScriptableObject.CreateInstance<GameProceduralLevelPreset>();
        preset.EnsureInitialized();
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SetString(serializedPreset, "presetId", "PRESET_BAKE_SMOKE");
        SerializedProperty transitionSettings = RequireProperty(serializedPreset, "transitionSettings");
        SetEnum(transitionSettings,
                "roomStreamingMode",
                (int)GameProceduralRoomStreamingMode.TransactionalDualSlot);
        SetEnum(transitionSettings,
                "adjacentPreloadPolicy",
                (int)GameProceduralAdjacentPreloadPolicy.AllOutgoingUpToBudget);
        SetInteger(transitionSettings, "maximumStagedRooms", 3);
        SetBoolean(transitionSettings, "requireReadyBeforePortalCommit", true);
        SetInteger(transitionSettings, "retiredRoomBudget", 1);
        SetFloat(transitionSettings, "retirementWorkBudgetMilliseconds", 1.5f);
        SetBoolean(transitionSettings, "hideLoadingProgressDuringRoomTransitions", true);

        SerializedProperty levels = RequireProperty(serializedPreset, "levels");
        levels.arraySize = 2;
        ConfigureLevel(levels.GetArrayElementAtIndex(0), "LEVEL_TECH_A", "LEVEL_A", 0);
        ConfigureLevel(levels.GetArrayElementAtIndex(1), "LEVEL_TECH_B", "LEVEL_B", 1);

        SerializedProperty metadata = RequireProperty(serializedPreset, "roomMetadata");
        metadata.arraySize = 2;
        ConfigureFirstMetadata(metadata.GetArrayElementAtIndex(0));
        ConfigureSecondMetadata(metadata.GetArrayElementAtIndex(1));
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        return preset;
    }

    /// <summary>
    /// Configures one level and its expected contiguous room-tile slice.
    /// </summary>
    /// <param name="level">Serialized level definition.</param>
    /// <param name="technicalId">Stable level technical ID.</param>
    /// <param name="levelId">-facing level ID.</param>
    /// <param name="levelIndex">Ordered fixture level index.</param>
    private static void ConfigureLevel(SerializedProperty level,
                                       string technicalId,
                                       string levelId,
                                       int levelIndex)
    {
        SetString(level, "technicalId", technicalId);
        SetString(level, "levelId", levelId);
        SetBoolean(level, "enabled", true);
        SerializedProperty tiles = RequireRelativeProperty(level, "roomTiles");
        tiles.arraySize = levelIndex == 0 ? 2 : 1;

        if (levelIndex == 0)
        {
            ConfigureTile(tiles.GetArrayElementAtIndex(0), "TILE_TECH_A0", "TILE_A0", "SCN_ROOM_A", GameProceduralRoomRole.Start);
            ConfigureTile(tiles.GetArrayElementAtIndex(1), "TILE_TECH_A1", "TILE_A1", "SCN_ROOM_A", GameProceduralRoomRole.Regular);
            return;
        }

        ConfigureTile(tiles.GetArrayElementAtIndex(0), "TILE_TECH_B0", "TILE_B0", "SCN_ROOM_B", GameProceduralRoomRole.Boss);
    }

    /// <summary>
    /// Configures one reusable tile fixture.
    /// </summary>
    /// <param name="tile">Serialized tile definition.</param>
    /// <param name="technicalId">Stable tile technical ID.</param>
    /// <param name="tileId">-facing tile ID.</param>
    /// <param name="sceneId">Canonical scene ID.</param>
    /// <param name="role">Structural tile role.</param>
    private static void ConfigureTile(SerializedProperty tile,
                                      string technicalId,
                                      string tileId,
                                      string sceneId,
                                      GameProceduralRoomRole role)
    {
        SetString(tile, "technicalId", technicalId);
        SetString(tile, "tileId", tileId);
        SetString(tile, "sceneId", sceneId);
        SetString(tile, "sceneGuid", sceneId + "_GUID");
        SetEnum(tile, "role", (int)role);
        SetInteger(tile, "maximumCopies", 2);
        SetVector2Int(tile, "preferredDepthRange", new Vector2Int(0, 4));
        SetBoolean(tile, "useExactDepthConstraint", role != GameProceduralRoomRole.Start);
        SetInteger(tile, "exactDepth", role == GameProceduralRoomRole.Regular ? 2 : 3);
        SetFloat(tile, "baseSelectionWeight", 1f);
    }

    /// <summary>
    /// Configures metadata containing two independent exits on the same side plus one entrance.
    /// </summary>
    /// <param name="metadata">Serialized room metadata definition.</param>
    private static void ConfigureFirstMetadata(SerializedProperty metadata)
    {
        SetString(metadata, "sceneId", "SCN_ROOM_A");
        SetString(metadata, "sceneGuid", "SCN_ROOM_A_GUID");
        SetString(metadata, "dependencyHash", "HASH_A");
        SetBoolean(metadata, "cacheStale", false);
        SetInteger(metadata, "centerAnchorCount", 1);
        SerializedProperty portals = RequireRelativeProperty(metadata, "portals");
        portals.arraySize = 3;
        ConfigurePortal(portals.GetArrayElementAtIndex(0),
                        "A_EAST_REQUIRED",
                        GameRoomPortalSide.East,
                        GameRoomPortalCapability.Exit,
                        GameRoomPortalConnectionPolicy.Required);
        ConfigurePortal(portals.GetArrayElementAtIndex(1),
                        "A_EAST_OPTIONAL",
                        GameRoomPortalSide.East,
                        GameRoomPortalCapability.Exit,
                        GameRoomPortalConnectionPolicy.Optional);
        ConfigurePortal(portals.GetArrayElementAtIndex(2),
                        "A_WEST_ENTRANCE",
                        GameRoomPortalSide.West,
                        GameRoomPortalCapability.Entrance,
                        GameRoomPortalConnectionPolicy.Required);
    }

    /// <summary>
    /// Configures a second metadata record so cross-record portal ranges and indices can be checked.
    /// </summary>
    /// <param name="metadata">Serialized room metadata definition.</param>
    private static void ConfigureSecondMetadata(SerializedProperty metadata)
    {
        SetString(metadata, "sceneId", "SCN_ROOM_B");
        SetString(metadata, "sceneGuid", "SCN_ROOM_B_GUID");
        SetString(metadata, "dependencyHash", "HASH_B");
        SetBoolean(metadata, "cacheStale", false);
        SetInteger(metadata, "centerAnchorCount", 1);
        SerializedProperty portals = RequireRelativeProperty(metadata, "portals");
        portals.arraySize = 1;
        ConfigurePortal(portals.GetArrayElementAtIndex(0),
                        "B_WEST_BOTH",
                        GameRoomPortalSide.West,
                        GameRoomPortalCapability.Both,
                        GameRoomPortalConnectionPolicy.Optional);
    }

    /// <summary>
    /// Configures one individual cached portal signature.
    /// </summary>
    /// <param name="portal">Serialized portal metadata.</param>
    /// <param name="portalId">Stable portal ID.</param>
    /// <param name="side">Authored room side.</param>
    /// <param name="capability">Authored traversal capability.</param>
    /// <param name="policy">Authored connection policy.</param>
    private static void ConfigurePortal(SerializedProperty portal,
                                        string portalId,
                                        GameRoomPortalSide side,
                                        GameRoomPortalCapability capability,
                                        GameRoomPortalConnectionPolicy policy)
    {
        SetString(portal, "portalId", portalId);
        SetEnum(portal, "side", (int)side);
        SetEnum(portal, "capability", (int)capability);
        SetEnum(portal, "connectionPolicy", (int)policy);
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Verifies level ordering, flattened tile ownership and contiguous slice boundaries.
    /// </summary>
    /// <param name="levels">Baked ordered level buffer.</param>
    /// <param name="tiles">Baked flattened tile buffer.</param>
    private static void ValidateLevelRanges(DynamicBuffer<GameProceduralLevelDefinitionElement> levels,
                                            DynamicBuffer<GameProceduralRoomTileElement> tiles)
    {
        Require(levels.Length == 2, "Expected two baked level definitions.");
        Require(tiles.Length == 3, "Expected three flattened room tiles.");
        Require(levels[0].TileStartIndex == 0 && levels[0].TileCount == 2,
                "The first level tile slice is not [0, 2).");
        Require(levels[1].TileStartIndex == 2 && levels[1].TileCount == 1,
                "The second level tile slice is not [2, 3).");
        Require(levels[0].RequiresLevelExit != 0 && levels[1].RequiresLevelExit == 0,
                "Derived RequiresLevelExit flags do not match enabled authored progression order.");
        Require(tiles[0].LevelIndex == 0 && tiles[1].LevelIndex == 0 && tiles[2].LevelIndex == 1,
                "One or more flattened tiles lost their ordered level ownership.");
    }

    /// <summary>
    /// Verifies metadata portal slices and preservation of same-side portal multiplicity.
    /// </summary>
    /// <param name="metadata">Baked deduplicated scene metadata buffer.</param>
    /// <param name="portals">Baked flattened portal buffer.</param>
    private static void ValidateMetadataRanges(DynamicBuffer<GameProceduralRoomMetadataElement> metadata,
                                               DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portals)
    {
        Require(metadata.Length == 2, "Expected two baked room metadata records.");
        Require(portals.Length == 4, "Expected every individual portal signature to be preserved.");
        Require(metadata[0].PortalStartIndex == 0 && metadata[0].PortalCount == 3,
                "The first metadata portal slice is not [0, 3).");
        Require(metadata[1].PortalStartIndex == 3 && metadata[1].PortalCount == 1,
                "The second metadata portal slice is not [3, 4).");
        Require(portals[0].Side == GameRoomPortalSide.East && portals[1].Side == GameRoomPortalSide.East,
                "Multiple independent exits on the same room side were collapsed.");
        Require(portals[0].MetadataIndex == 0 && portals[1].MetadataIndex == 0 &&
                portals[2].MetadataIndex == 0 && portals[3].MetadataIndex == 1,
                "One or more portal signatures reference the wrong metadata record.");
    }

    /// <summary>
    /// Verifies every tile resolves its canonical scene ID to the expected metadata record.
    /// </summary>
    /// <param name="tiles">Baked flattened room tile buffer.</param>
    private static void ValidateTileMetadataIndices(DynamicBuffer<GameProceduralRoomTileElement> tiles)
    {
        Require(tiles[0].MetadataIndex == 0 && tiles[1].MetadataIndex == 0,
                "Tiles sharing SCN_ROOM_A did not share metadata index zero.");
        Require(tiles[2].MetadataIndex == 1,
                "SCN_ROOM_B did not resolve metadata index one.");
    }

    /// <summary>
    /// Verifies optional hard depth constraints survive flattening without changing unconstrained Start authoring.
    /// </summary>
    /// <param name="tiles">Baked flattened room tile buffer.</param>
    private static void ValidateTileDepthConstraints(DynamicBuffer<GameProceduralRoomTileElement> tiles)
    {
        Require(tiles[0].UseExactDepthConstraint == 0,
                "The unconstrained Start tile gained a hard depth constraint during baking.");
        Require(tiles[1].UseExactDepthConstraint != 0 && tiles[1].ExactDepth == 2,
                "The Regular tile did not preserve its authored Exact Depth.");
        Require(tiles[2].UseExactDepthConstraint != 0 && tiles[2].ExactDepth == 3,
                "The Boss tile did not preserve its authored Exact Depth.");
    }

    /// <summary>
    /// Verifies the complete transactional preload and deferred-retirement policy survives the authoring-to-ECS bake path.
    /// </summary>
    /// <param name="config">Baked procedural runtime configuration.</param>
    private static void ValidateTransactionalStreamingConfig(GameProceduralLevelConfig config)
    {
        Require(config.RoomStreamingMode == GameProceduralRoomStreamingMode.TransactionalDualSlot,
                "The baked config did not preserve transactional dual-slot streaming.");
        Require(config.AdjacentPreloadPolicy == GameProceduralAdjacentPreloadPolicy.AllOutgoingUpToBudget,
                "The baked config did not preserve the outgoing-room preload policy.");
        Require(config.MaximumStagedRooms == 3,
                "The baked config did not preserve the staged-room budget.");
        Require(config.RequireReadyBeforePortalCommit != 0,
                "The baked config did not preserve the portal readiness gate.");
        Require(config.RetiredRoomBudget == 1,
                "The baked config did not preserve the retired-room budget.");
        Require(Mathf.Approximately(config.RetirementWorkBudgetMilliseconds, 1.5f),
                "The baked config did not preserve the retirement work budget.");
    }

    /// <summary>
    /// Verifies oversized UTF-8 authoring values produce diagnostics and empty defensive bake values without throwing
    /// or truncating the serialized source data.
    /// </summary>
    /// <param name="preset">Transient preset whose identity and display name are exercised.</param>
    /// <param name="levels">Level buffer repopulated through the defensive conversion path.</param>
    /// <param name="tiles">Tile buffer paired with the flattened level output.</param>
    /// <param name="metadata">Metadata buffer repopulated through the defensive conversion path.</param>
    /// <param name="portals">Portal buffer paired with the flattened metadata output.</param>
    private static void ValidateFixedStringBakeGuards(GameProceduralLevelPreset preset,
                                                       DynamicBuffer<GameProceduralLevelDefinitionElement> levels,
                                                       DynamicBuffer<GameProceduralRoomTileElement> tiles,
                                                       DynamicBuffer<GameProceduralRoomMetadataElement> metadata,
                                                       DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portals)
    {
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SerializedProperty presetId = RequireProperty(serializedPreset, "presetId");
        SerializedProperty displayName = RequireRelativeProperty(RequireProperty(serializedPreset, "levels").GetArrayElementAtIndex(0),
                                                                  "displayName");
        SerializedProperty dependencyHash = RequireRelativeProperty(RequireProperty(serializedPreset, "roomMetadata").GetArrayElementAtIndex(0),
                                                                     "dependencyHash");
        string originalPresetId = presetId.stringValue;
        string originalDisplayName = displayName.stringValue;
        string originalDependencyHash = dependencyHash.stringValue;
        presetId.stringValue = new string('X', 80);
        displayName.stringValue = new string('Y', 140);
        dependencyHash.stringValue = new string('Z', 140);
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();

        GameProceduralLevelConfig invalidConfig = GameProceduralLevelBakeUtility.BuildConfig(preset);
        GameProceduralLevelBakeUtility.PopulateLevelBuffers(preset, levels, tiles);
        GameProceduralLevelBakeUtility.PopulateMetadataBuffers(preset, metadata, portals);
        GameProceduralLevelValidationReport report = GameProceduralLevelValidator.ValidatePreset(preset);
        Require(invalidConfig.PresetId.Length == 0,
                "An oversized preset ID was truncated or allowed into FixedString64Bytes.");
        Require(levels[0].DisplayName.Length == 0,
                "An oversized level label was truncated or allowed into FixedString128Bytes.");
        Require(metadata[0].DependencyHash.Length == 0,
                "An oversized dependency hash was truncated or allowed into FixedString128Bytes.");
        Require(ContainsDiagnostic(report, GameProceduralLevelValidationCode.IdentifierTooLong),
                "Oversized preset identity did not produce its validation diagnostic.");
        Require(ContainsDiagnostic(report, GameProceduralLevelValidationCode.LevelDisplayNameTooLong),
                "Oversized display name did not produce its validation diagnostic.");
        Require(ContainsDiagnostic(report, GameProceduralLevelValidationCode.RuntimeTextTooLong),
                "Oversized runtime metadata did not produce its validation diagnostic.");
        Require(!GameProceduralLevelBakeUtility.TryValidateRuntimeConfiguration(preset,
                                                                                preset.SceneCatalogPreset,
                                                                                out string failureMessage) &&
                !string.IsNullOrWhiteSpace(failureMessage),
                "The runtime bake guard accepted invalid fixed-string authoring.");

        serializedPreset.Update();
        presetId.stringValue = originalPresetId;
        displayName.stringValue = originalDisplayName;
        dependencyHash.stringValue = originalDependencyHash;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        GameProceduralLevelBakeUtility.PopulateLevelBuffers(preset, levels, tiles);
        GameProceduralLevelBakeUtility.PopulateMetadataBuffers(preset, metadata, portals);
    }

    /// <summary>
    /// Verifies an editor catalog that differs from the effective runtime catalog is reported before baking.
    /// </summary>
    /// <param name="preset">Transient preset receiving two distinct Scene Manager catalog references.</param>
    private static void ValidateRuntimeCatalogMismatch(GameProceduralLevelPreset preset)
    {
        GameSceneManagerPreset authoredCatalog = ScriptableObject.CreateInstance<GameSceneManagerPreset>();
        GameSceneManagerPreset runtimeCatalog = ScriptableObject.CreateInstance<GameSceneManagerPreset>();

        try
        {
            authoredCatalog.EnsureInitialized();
            runtimeCatalog.EnsureInitialized();
            SerializedObject serializedPreset = new SerializedObject(preset);
            serializedPreset.Update();
            SerializedProperty sceneCatalog = RequireProperty(serializedPreset, "sceneCatalogPreset");
            sceneCatalog.objectReferenceValue = authoredCatalog;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            GameProceduralLevelValidationReport report = GameProceduralLevelRuntimeValidationUtility.ValidateCompatibility(preset,
                                                                                                                            runtimeCatalog);
            Require(ContainsDiagnostic(report, GameProceduralLevelValidationCode.SceneCatalogMismatch),
                    "Different editor and runtime scene catalogs did not produce a mismatch diagnostic.");
            Require(!GameProceduralLevelGraphPreviewUtility.TryValidateRuntimeCompatibility(preset,
                                                                                             runtimeCatalog,
                                                                                             out GameProceduralLevelValidationReport previewReport) &&
                    ContainsDiagnostic(previewReport, GameProceduralLevelValidationCode.SceneCatalogMismatch),
                    "The graph preview guard accepted a preset selected against a different runtime catalog.");
            Require(!GameProceduralLevelGraphPreviewUtility.TryValidateRuntimeCompatibility(preset,
                                                                                             null,
                                                                                             out GameProceduralLevelValidationReport missingRuntimeReport) &&
                    ContainsDiagnostic(missingRuntimeReport, GameProceduralLevelValidationCode.SceneCatalogMismatch),
                    "The graph preview guard accepted a preset without a current Game Master runtime catalog.");
            GameProceduralLevelGraphPreviewCompatibilityGuard previewGuard = new GameProceduralLevelGraphPreviewCompatibilityGuard();
            Require(!previewGuard.Refresh(preset,
                                          runtimeCatalog,
                                          true,
                                          out bool initiallyRefreshed) &&
                    initiallyRefreshed,
                    "The graph preview cache did not reject its initial incompatible runtime catalog.");
            EditorUtility.SetDirty(runtimeCatalog);
            Require(!previewGuard.Refresh(preset,
                                          runtimeCatalog,
                                          false,
                                          out bool refreshedAfterCatalogEdit) &&
                    refreshedAfterCatalogEdit,
                    "An already-open graph preview did not revalidate after its runtime catalog content changed.");
        }
        finally
        {
            SerializedObject serializedPreset = new SerializedObject(preset);
            serializedPreset.Update();
            RequireProperty(serializedPreset, "sceneCatalogPreset").objectReferenceValue = null;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(authoredCatalog);
            UnityEngine.Object.DestroyImmediate(runtimeCatalog);
        }
    }

    /// <summary>
    /// Resolves whether a validation report contains one stable diagnostic code.
    /// </summary>
    /// <param name="report">Validation report to inspect.</param>
    /// <param name="code">Stable diagnostic code expected by the smoke test.</param>
    /// <returns>True when at least one diagnostic matches the requested code.</returns>
    private static bool ContainsDiagnostic(GameProceduralLevelValidationReport report,
                                           GameProceduralLevelValidationCode code)
    {
        for (int index = 0; index < report.Diagnostics.Count; index++)
        {
            if (report.Diagnostics[index].Code == code)
                return true;
        }

        return false;
    }
    #endregion

    #region Serialized Property Methods
    /// <summary>
    /// Resolves one root serialized property or fails with its exact field name.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing the field.</param>
    /// <param name="propertyName">Root field name.</param>
    /// <returns>Resolved serialized property.</returns>
    private static SerializedProperty RequireProperty(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Require(property != null, "Serialized property '" + propertyName + "' was not found.");
        return property;
    }

    /// <summary>
    /// Resolves one nested serialized property or fails with its exact field name.
    /// </summary>
    /// <param name="owner">Serialized property containing the nested field.</param>
    /// <param name="propertyName">Nested field name.</param>
    /// <returns>Resolved nested serialized property.</returns>
    private static SerializedProperty RequireRelativeProperty(SerializedProperty owner, string propertyName)
    {
        SerializedProperty property = owner.FindPropertyRelative(propertyName);
        Require(property != null, "Nested serialized property '" + propertyName + "' was not found.");
        return property;
    }

    /// <summary>
    /// Writes one root string field used by the transient fixture.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing the field.</param>
    /// <param name="propertyName">Root field name.</param>
    /// <param name="value">String value to write.</param>
    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        RequireProperty(serializedObject, propertyName).stringValue = value;
    }

    /// <summary>
    /// Writes one nested string field used by the transient fixture.
    /// </summary>
    /// <param name="owner">Serialized property containing the field.</param>
    /// <param name="propertyName">Nested field name.</param>
    /// <param name="value">String value to write.</param>
    private static void SetString(SerializedProperty owner, string propertyName, string value)
    {
        RequireRelativeProperty(owner, propertyName).stringValue = value;
    }

    /// <summary>
    /// Writes one nested Boolean field used by the transient fixture.
    /// </summary>
    /// <param name="owner">Serialized property containing the field.</param>
    /// <param name="propertyName">Nested field name.</param>
    /// <param name="value">Boolean value to write.</param>
    private static void SetBoolean(SerializedProperty owner, string propertyName, bool value)
    {
        RequireRelativeProperty(owner, propertyName).boolValue = value;
    }

    /// <summary>
    /// Writes one nested integer field used by the transient fixture.
    /// </summary>
    /// <param name="owner">Serialized property containing the field.</param>
    /// <param name="propertyName">Nested field name.</param>
    /// <param name="value">Integer value to write.</param>
    private static void SetInteger(SerializedProperty owner, string propertyName, int value)
    {
        RequireRelativeProperty(owner, propertyName).intValue = value;
    }

    /// <summary>
    /// Writes one nested enum field used by the transient fixture.
    /// </summary>
    /// <param name="owner">Serialized property containing the field.</param>
    /// <param name="propertyName">Nested field name.</param>
    /// <param name="value">Enum numeric value to write.</param>
    private static void SetEnum(SerializedProperty owner, string propertyName, int value)
    {
        RequireRelativeProperty(owner, propertyName).enumValueIndex = value;
    }

    /// <summary>
    /// Writes one nested floating-point field used by the transient fixture.
    /// </summary>
    /// <param name="owner">Serialized property containing the field.</param>
    /// <param name="propertyName">Nested field name.</param>
    /// <param name="value">Floating-point value to write.</param>
    private static void SetFloat(SerializedProperty owner, string propertyName, float value)
    {
        RequireRelativeProperty(owner, propertyName).floatValue = value;
    }

    /// <summary>
    /// Writes one nested integer range field used by the transient fixture.
    /// </summary>
    /// <param name="owner">Serialized property containing the field.</param>
    /// <param name="propertyName">Nested field name.</param>
    /// <param name="value">Integer range value to write.</param>
    private static void SetVector2Int(SerializedProperty owner, string propertyName, Vector2Int value)
    {
        RequireRelativeProperty(owner, propertyName).vector2IntValue = value;
    }
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Throws one actionable smoke-test failure when an invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure message describing the violated invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameProceduralLevelBakeSmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
