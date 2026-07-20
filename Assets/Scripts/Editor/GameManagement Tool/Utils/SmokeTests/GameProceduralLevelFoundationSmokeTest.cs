using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Verifies the initial Procedural Level preset model, default asset links and non-destructive setup merge behavior.
/// </summary>
public static class GameProceduralLevelFoundationSmokeTest
{
    #region Constants
    private const string DefaultMasterPresetPath = "Assets/Scriptable Objects/Game/Master Presets/GameMasterPreset.asset";
    private const string DefaultScenePresetPath = "Assets/Scriptable Objects/Game/Scene Management/GameSceneManagerPreset.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs deterministic editor-only assertions without saving or changing project assets.
    /// </summary>
    public static void Run()
    {
        ValidateRuntimeModelInitialization();
        ValidateAuthoredProgressionDiagnostics();
        ValidatePortalBakeReadinessGuard();
        ValidateSerializedMergePreservesCustomEntries();
        ValidateDefaultAssetReferences();
        ValidatePresetPanelConditionalSections();
        Debug.Log("[GameProceduralLevelFoundationSmokeTest] Procedural Level foundation checks passed.");
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Verifies that stable technical identifiers and required configuration objects initialize without sanitizing tuning.
    /// </summary>
    private static void ValidateRuntimeModelInitialization()
    {
        GameProceduralLevelPreset preset = ScriptableObject.CreateInstance<GameProceduralLevelPreset>();

        try
        {
            preset.EnsureInitialized();
            Require(!string.IsNullOrWhiteSpace(preset.PresetId), "Procedural preset ID was not initialized.");
            Require(preset.GenerationSettings != null, "Generation settings were not initialized.");
            Require(preset.TransitionSettings != null, "Transition settings were not initialized.");
            Require(preset.Levels != null, "Level collection was not initialized.");
            Require(preset.RoomMetadata != null, "Room metadata collection was not initialized.");

            GameProceduralLevelDefinition level = new GameProceduralLevelDefinition();
            level.EnsureInitialized();
            Require(!string.IsNullOrWhiteSpace(level.TechnicalId), "Level technical ID was not initialized.");
            Require(level.RuleSettings != null, "Level rule settings were not initialized.");
            Require(level.RoomTiles != null, "Room tile collection was not initialized.");

            GameProceduralRoomTileDefinition tile = new GameProceduralRoomTileDefinition();
            tile.EnsureInitialized();
            Require(!string.IsNullOrWhiteSpace(tile.TechnicalId), "Room tile technical ID was not initialized.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    /// <summary>
    /// Verifies authored validation reports Start-anchor, non-final Boss LevelExit and UTF-8 display-name capacity errors without mutating data.
    /// </summary>
    private static void ValidateAuthoredProgressionDiagnostics()
    {
        GameProceduralLevelPreset preset = ScriptableObject.CreateInstance<GameProceduralLevelPreset>();

        try
        {
            preset.EnsureInitialized();
            SerializedObject serializedPreset = new SerializedObject(preset);
            serializedPreset.Update();
            SerializedProperty levels = serializedPreset.FindProperty("levels");
            SerializedProperty metadata = serializedPreset.FindProperty("roomMetadata");
            Require(levels != null && metadata != null, "Procedural validation fixture fields were not found.");

            // Configure one enabled non-final level and one later enabled level to derive RequiresLevelExit.
            levels.arraySize = 2;
            ConfigureValidationLevel(levels.GetArrayElementAtIndex(0),
                                     "LEVEL_TECH_VALIDATION_A",
                                     "LEVEL_VALIDATION_A",
                                     new string('\u00E9', 63),
                                     true);
            ConfigureValidationLevel(levels.GetArrayElementAtIndex(1),
                                     "LEVEL_TECH_VALIDATION_B",
                                     "LEVEL_VALIDATION_B",
                                     "Validation B",
                                     false);

            // Attach distinct Start and Boss metadata so each new structural diagnostic has one exact source.
            metadata.arraySize = 2;
            ConfigureValidationMetadata(metadata.GetArrayElementAtIndex(0),
                                        "SCN_VALIDATION_START",
                                        0,
                                        false);
            ConfigureValidationMetadata(metadata.GetArrayElementAtIndex(1),
                                        "SCN_VALIDATION_BOSS",
                                        1,
                                        true);
            SetBool(metadata.GetArrayElementAtIndex(0), "cacheStale", true);
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            preset.EnsureInitialized();

            GameProceduralLevelValidationReport report = GameProceduralLevelValidator.ValidatePreset(preset);
            Require(ContainsDiagnostic(report, GameProceduralLevelValidationCode.MissingCenterAnchor),
                    "Portal-arrival Start tile without a center anchor did not report MissingCenterAnchor.");
            Require(ContainsDiagnostic(report, GameProceduralLevelValidationCode.BossMissingLevelExit),
                    "A non-final Boss without a usable LevelExit did not report BossMissingLevelExit.");
            Require(ContainsDiagnostic(report, GameProceduralLevelValidationCode.LevelDisplayNameTooLong),
                    "An over-capacity multibyte display name did not report LevelDisplayNameTooLong.");
            Require(ContainsDiagnostic(report, GameProceduralLevelValidationCode.RoomMetadataCacheStale),
                    "A structurally stale room cache did not block preset validation.");

            // Verify inactive fitting data is ignored and a preset without enabled levels is rejected explicitly.
            serializedPreset.Update();
            SerializedProperty firstLevel = levels.GetArrayElementAtIndex(0);
            SerializedProperty firstRules = firstLevel.FindPropertyRelative("ruleSettings");
            Require(firstRules != null, "Validation fixture rule settings were not found.");
            SetBool(firstLevel, "useCenterArrival", true);
            SetFloat(firstRules, "fittingScore", float.NaN);
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            GameProceduralLevelValidationReport centerArrivalReport = GameProceduralLevelValidator.ValidatePreset(preset);
            Require(!ContainsDiagnostic(centerArrivalReport, GameProceduralLevelValidationCode.InvalidFittingScore),
                    "Center-arrival validation evaluated the inactive Fitting Score.");

            serializedPreset.Update();
            SetBool(levels.GetArrayElementAtIndex(0), "enabled", false);
            SetBool(levels.GetArrayElementAtIndex(1), "enabled", false);
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            GameProceduralLevelValidationReport disabledReport = GameProceduralLevelValidator.ValidatePreset(preset);
            Require(ContainsDiagnostic(disabledReport, GameProceduralLevelValidationCode.MissingEnabledLevel),
                    "A preset without enabled levels did not report MissingEnabledLevel.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    /// <summary>
    /// Verifies scanner and Baker parity for every authored condition that would omit runtime portal data.
    /// </summary>
    private static void ValidatePortalBakeReadinessGuard()
    {
        GameObject portalObject = new GameObject("Procedural Portal Bake Readiness Smoke")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        try
        {
            BoxCollider volume = portalObject.AddComponent<BoxCollider>();
            GameRoomPortalAuthoring portal = portalObject.AddComponent<GameRoomPortalAuthoring>();
            SerializedObject serializedPortal = new SerializedObject(portal);
            serializedPortal.Update();
            SetString(serializedPortal, "portalId", "PORTAL_BAKE_READINESS_SMOKE");
            SetObjectReference(serializedPortal, "portalVolume", volume);
            serializedPortal.ApplyModifiedPropertiesWithoutUndo();
            Require(portal.TryValidateBakeReadiness(out string validFailure) && string.IsNullOrEmpty(validFailure),
                    "A complete active portal was rejected by the shared bake-readiness guard.");

            // Missing serialized volume references must block metadata publication even when RequireComponent exists.
            serializedPortal.Update();
            SetObjectReference(serializedPortal, "portalVolume", null);
            serializedPortal.ApplyModifiedPropertiesWithoutUndo();
            Require(!portal.TryValidateBakeReadiness(out string missingVolumeFailure) &&
                    !string.IsNullOrWhiteSpace(missingVolumeFailure),
                    "A portal with no assigned volume remained eligible for metadata and baking.");

            // Degenerate effective world dimensions cannot create the independent Unity Physics blocker.
            serializedPortal.Update();
            SetObjectReference(serializedPortal, "portalVolume", volume);
            serializedPortal.ApplyModifiedPropertiesWithoutUndo();
            volume.size = new Vector3(1f, 0f, 1f);
            Require(!portal.TryValidateBakeReadiness(out string degenerateVolumeFailure) &&
                    !string.IsNullOrWhiteSpace(degenerateVolumeFailure),
                    "A portal with a degenerate volume remained eligible for metadata and baking.");

            // Disabled and inactive authoring must never be represented as an available runtime portal signature.
            volume.size = Vector3.one;
            portal.enabled = false;
            Require(!portal.TryValidateBakeReadiness(out string disabledFailure) &&
                    !string.IsNullOrWhiteSpace(disabledFailure),
                    "A disabled portal authoring component remained eligible for metadata and baking.");
            portal.enabled = true;
            portalObject.SetActive(false);
            Require(!portal.TryValidateBakeReadiness(out string inactiveFailure) &&
                    !string.IsNullOrWhiteSpace(inactiveFailure),
                    "An inactive portal GameObject remained eligible for metadata and baking.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(portalObject);
        }
    }

    /// <summary>
    /// Configures one transient level used to validate derived progression requirements.
    /// </summary>
    /// <param name="level">Serialized level definition to configure.</param>
    /// <param name="technicalId">Stable fixture technical ID.</param>
    /// <param name="levelId">Designer-facing fixture level ID.</param>
    /// <param name="displayName">Display name whose UTF-8 capacity is validated.</param>
    /// <param name="includeTiles">Whether this level owns the Start and Boss fixture tiles.</param>
    private static void ConfigureValidationLevel(SerializedProperty level,
                                                 string technicalId,
                                                 string levelId,
                                                 string displayName,
                                                 bool includeTiles)
    {
        SetString(level, "technicalId", technicalId);
        SetString(level, "levelId", levelId);
        SetString(level, "displayName", displayName);
        SetBool(level, "enabled", true);
        SetBool(level, "useCenterArrival", false);
        SerializedProperty tiles = level.FindPropertyRelative("roomTiles");
        Require(tiles != null, "Validation fixture room tile array was not found.");
        tiles.arraySize = includeTiles ? 2 : 0;

        if (!includeTiles)
            return;

        ConfigureValidationTile(tiles.GetArrayElementAtIndex(0),
                                "TILE_TECH_VALIDATION_START",
                                "TILE_VALIDATION_START",
                                "SCN_VALIDATION_START",
                                GameProceduralRoomRole.Start);
        ConfigureValidationTile(tiles.GetArrayElementAtIndex(1),
                                "TILE_TECH_VALIDATION_BOSS",
                                "TILE_VALIDATION_BOSS",
                                "SCN_VALIDATION_BOSS",
                                GameProceduralRoomRole.Boss);
    }

    /// <summary>
    /// Configures one transient room tile with valid identity and deterministic tuning.
    /// </summary>
    /// <param name="tile">Serialized room tile to configure.</param>
    /// <param name="technicalId">Stable fixture technical ID.</param>
    /// <param name="tileId">Designer-facing fixture tile ID.</param>
    /// <param name="sceneId">Metadata scene ID referenced by the tile.</param>
    /// <param name="role">Structural role assigned to the tile.</param>
    private static void ConfigureValidationTile(SerializedProperty tile,
                                                string technicalId,
                                                string tileId,
                                                string sceneId,
                                                GameProceduralRoomRole role)
    {
        SetString(tile, "technicalId", technicalId);
        SetString(tile, "tileId", tileId);
        SetString(tile, "sceneId", sceneId);
        SetInt(tile, "maximumCopies", 1);
        SetFloat(tile, "baseSelectionWeight", 1f);
        SerializedProperty roleProperty = tile.FindPropertyRelative("role");
        Require(roleProperty != null, "Validation fixture tile role was not found.");
        roleProperty.enumValueIndex = (int)role;
    }

    /// <summary>
    /// Configures one transient metadata snapshot with an optional entrance-only LevelExit used to verify capability filtering.
    /// </summary>
    /// <param name="metadata">Serialized room metadata to configure.</param>
    /// <param name="sceneId">Canonical metadata scene ID.</param>
    /// <param name="centerAnchorCount">Authored center-anchor count exposed to validation.</param>
    /// <param name="includeEntranceOnlyLevelExit">Whether to add a LevelExit that cannot serve outgoing traversal.</param>
    private static void ConfigureValidationMetadata(SerializedProperty metadata,
                                                    string sceneId,
                                                    int centerAnchorCount,
                                                    bool includeEntranceOnlyLevelExit)
    {
        SetString(metadata, "sceneId", sceneId);
        SetBool(metadata, "cacheStale", false);
        SetInt(metadata, "centerAnchorCount", centerAnchorCount);
        SerializedProperty portals = metadata.FindPropertyRelative("portals");
        Require(portals != null, "Validation fixture portal array was not found.");
        portals.arraySize = includeEntranceOnlyLevelExit ? 1 : 0;

        if (!includeEntranceOnlyLevelExit)
            return;

        SerializedProperty portal = portals.GetArrayElementAtIndex(0);
        SetString(portal, "portalId", "BOSS_ENTRANCE_ONLY_LEVEL_EXIT");
        SerializedProperty capability = portal.FindPropertyRelative("capability");
        SerializedProperty policy = portal.FindPropertyRelative("connectionPolicy");
        Require(capability != null && policy != null, "Validation fixture LevelExit fields were not found.");
        capability.enumValueIndex = (int)GameRoomPortalCapability.Entrance;
        policy.enumValueIndex = (int)GameRoomPortalConnectionPolicy.LevelExit;
    }

    /// <summary>
    /// Verifies that default setup entries are merged by ID and never truncate a custom scene definition.
    /// </summary>
    private static void ValidateSerializedMergePreservesCustomEntries()
    {
        GameSceneManagerPreset scenePreset = ScriptableObject.CreateInstance<GameSceneManagerPreset>();

        try
        {
            scenePreset.EnsureInitialized();
            SerializedObject serializedPreset = new SerializedObject(scenePreset);
            serializedPreset.Update();
            SerializedProperty scenesProperty = serializedPreset.FindProperty("sceneDefinitions");
            Require(scenesProperty != null, "Scene definition array was not found.");

            scenesProperty.arraySize = 1;
            SerializedProperty customScene = scenesProperty.GetArrayElementAtIndex(0);
            SetString(customScene, "sceneId", "SCN_CustomRoom");

            SerializedProperty firstDefault = FindOrAppendArrayElement(scenesProperty, "sceneId", "SCN_Bootstrap");
            SerializedProperty repeatedDefault = FindOrAppendArrayElement(scenesProperty, "sceneId", "SCN_Bootstrap");
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();

            Require(firstDefault != null, "Default scene definition was not appended.");
            Require(repeatedDefault != null, "Repeated default scene lookup failed.");
            Require(scenesProperty.arraySize == 2, "Repeated setup merge appended a duplicate default scene.");
            Require(string.Equals(scenesProperty.GetArrayElementAtIndex(0).FindPropertyRelative("sceneId").stringValue,
                                  "SCN_CustomRoom",
                                  StringComparison.Ordinal),
                    "Setup merge replaced the custom scene definition.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(scenePreset);
        }
    }

    /// <summary>
    /// Verifies that setup created and linked the canonical preset, library and scene catalog assets.
    /// </summary>
    private static void ValidateDefaultAssetReferences()
    {
        GameMasterPreset masterPreset = AssetDatabase.LoadAssetAtPath<GameMasterPreset>(DefaultMasterPresetPath);
        GameSceneManagerPreset scenePreset = AssetDatabase.LoadAssetAtPath<GameSceneManagerPreset>(DefaultScenePresetPath);
        GameProceduralLevelPreset proceduralPreset = AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(GameProceduralLevelProjectSetupUtility.DefaultPresetPath);
        GameProceduralLevelPresetLibrary library = AssetDatabase.LoadAssetAtPath<GameProceduralLevelPresetLibrary>(GameProceduralLevelPresetLibraryUtility.DefaultLibraryPath);

        Require(masterPreset != null, "Default Game Master preset is missing.");
        Require(scenePreset != null, "Default Scene Manager preset is missing.");
        Require(proceduralPreset != null, "Default Procedural Level preset is missing.");
        Require(library != null, "Default Procedural Level preset library is missing.");
        Require(masterPreset.ProceduralLevelPreset == proceduralPreset, "Game Master does not reference the default Procedural Level preset.");
        Require(proceduralPreset.SceneCatalogPreset == scenePreset, "Procedural Level preset does not reference the default scene catalog.");
        Require(LibraryContains(library, proceduralPreset), "Procedural Level preset is not registered in its library.");
    }

    /// <summary>
    /// Verifies that every top-level conditional section creates its bound controls instead of leaving only a heading visible.
    /// </summary>
    private static void ValidatePresetPanelConditionalSections()
    {
        GameProceduralLevelPreset preset = AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(GameProceduralLevelProjectSetupUtility.DefaultPresetPath);
        Require(preset != null, "The Procedural Level preset required by the panel rendering check is missing.");

        GameProceduralLevelPresetsPanel panel = new GameProceduralLevelPresetsPanel(null);
        panel.SelectPresetFromExternal(preset);

        // Generation must always expose its seed policy and three bounded solver limits.
        panel.ActiveSection = GameProceduralLevelPresetsPanel.DetailsSectionType.Generation;
        panel.BuildActiveSection();
        Require(CountPropertyFields(panel.SectionContentRoot) >= 4,
                "The Generation section did not create its required serialized controls.");

        // Transition defaults keep the player visible and therefore expose the optional animation selector.
        panel.ActiveSection = GameProceduralLevelPresetsPanel.DetailsSectionType.Transition;
        panel.BuildActiveSection();
        Require(CountPropertyFields(panel.SectionContentRoot) >= 3,
                "The Transition section did not create its required serialized controls.");
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Checks whether one report contains a diagnostic with the requested stable validation code.
    /// </summary>
    /// <param name="report">Validation report to inspect.</param>
    /// <param name="code">Stable diagnostic code to find.</param>
    /// <returns>True when at least one matching diagnostic exists.</returns>
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

    /// <summary>
    /// Checks whether one Procedural Level preset is registered in the supplied library.
    /// </summary>
    /// <param name="library">Library whose preset references are inspected.</param>
    /// <param name="preset">Expected registered preset.</param>
    /// <returns>True when the exact preset reference is present.</returns>
    private static bool LibraryContains(GameProceduralLevelPresetLibrary library, GameProceduralLevelPreset preset)
    {
        for (int index = 0; index < library.Presets.Count; index++)
        {
            if (library.Presets[index] == preset)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Counts bound property fields below one section root for editor rendering regression checks.
    /// </summary>
    /// <param name="root">Section root whose descendant controls are counted.</param>
    /// <returns>Number of property fields currently present in the visual tree.</returns>
    private static int CountPropertyFields(VisualElement root)
    {
        Require(root != null, "The Procedural Level section root was not created.");
        return root.Query<PropertyField>().ToList().Count;
    }

    /// <summary>
    /// Throws one actionable smoke-test failure when a required invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure message describing the violated invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameProceduralLevelFoundationSmokeTest: " + message);
    }
    #endregion

    #endregion
}
