using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Verifies the authored Dev UI, configurable reveal action, bake path, backend package, and consent-aware ECS queue.
/// </summary>
public static class GameDataCollectionSmokeTest
{
    #region Constants
    private const string SettingsPresetPath =
        "Assets/Scriptable Objects/Game/Settings/GameSettingsManagerPreset.asset";
    private const string MasterPresetPath =
        "Assets/Scriptable Objects/Game/Master Presets/GameMasterPreset.asset";
    private const string SettingsPrefabPath = "Assets/Prefabs/UI/PF_SettingsMenu.prefab";
    private const string BootstrapScenePath =
        "Assets/Scenes/Testing/Main Scenes/Bootstrap/SCN_Bootstrap.unity";

    private static readonly string[] RequiredDevControllerReferences =
    {
        "tabButton",
        "panelRoot",
        "accountActionsRoot",
        "developerActionsRoot",
        "authenticatedRoot",
        "authenticationFormRoot",
        "consentWarningRoot",
        "dashboardRoot",
        "registerUserButton",
        "loginUserButton",
        "registerDeveloperButton",
        "loginDeveloperButton",
        "logoutButton",
        "emailInput",
        "passwordInput",
        "formContinueButton",
        "formCancelButton",
        "noticeAcknowledgementToggle",
        "programmingConsentToggle",
        "designConsentToggle",
        "art3DConsentToggle",
        "consentConfirmButton",
        "consentCancelButton",
        "statusLabel",
        "accountLabel"
    };

    private static readonly string[] RequiredBackendFiles =
    {
        "Backend/TelemetryApi/private/config.example.php",
        "Backend/TelemetryApi/database/001_accounts.sql",
        "Backend/TelemetryApi/database/002_sessions_and_consent.sql",
        "Backend/TelemetryApi/database/003_telemetry.sql",
        "Backend/TelemetryApi/public/api/v1/health.php",
        "Backend/TelemetryApi/public/api/v1/events.php",
        "Backend/TelemetryApi/public/api/v1/dashboard.php",
        "Backend/TelemetryApi/tools/create_developer_invite.php",
        "Backend/TelemetryApi/tools/purge_expired_data.php"
    };
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs deterministic local checks without contacting alwaysdata or creating production accounts.
    /// </summary>
    // [MenuItem("Tools/Game/Data Collection/Run Smoke Test")]
    public static void Run()
    {
        GameSettingsManagerPreset preset = AssetDatabase.LoadAssetAtPath<GameSettingsManagerPreset>(
            SettingsPresetPath);
        GameDataCollectionManagerPreset dataPreset =
            AssetDatabase.LoadAssetAtPath<GameDataCollectionManagerPreset>(
                GameDataCollectionProjectSetupUtility.DefaultPresetPath);
        Require(preset != null, "The default Settings Manager preset is missing.");
        Require(dataPreset != null, "The default Data Collection Manager preset is missing.");
        ValidateInputAction(preset);
        ValidateBake(preset, dataPreset);
        ValidateMasterPreset(dataPreset);
        ValidateRuntimeArchetypeIsolation();
        ValidateAuthoredUi();
        ValidateBootstrapScene();
        ValidateBackendPackage();
        ValidateConsentQueue(preset, dataPreset);
        Debug.Log("[GameDataCollectionSmokeTest] All checks passed.");
    }
    #endregion

    #region Asset Validation
    /// <summary>
    /// Verifies that the preset reference resolves to the project-owned reveal action and binding.
    /// </summary>
    /// <param name="preset">Default Settings Manager preset.</param>
    private static void ValidateInputAction(GameSettingsManagerPreset preset)
    {
        InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            PlayerInputActionsAssetUtility.DefaultInputAssetPath);
        Require(inputAsset != null, "The shared Input Action asset is missing.");
        InputAction action = inputAsset.FindAction(preset.DataCollectionSettings.RevealDevActionsActionId, false);
        Require(action != null && action.actionMap.name == "UI" && action.name == "RevealDevActions",
                "The configured developer reveal Input Action does not resolve to UI/RevealDevActions.");
        Require(action.bindings.Count > 0, "UI/RevealDevActions has no configurable binding.");
    }

    /// <summary>
    /// Verifies that authored values propagate into the compact ECS runtime config.
    /// </summary>
    /// <param name="preset">Default Settings Manager preset.</param>
    /// <param name="dataPreset">Default global Data Collection Manager preset.</param>
    private static void ValidateBake(GameSettingsManagerPreset preset,
                                     GameDataCollectionManagerPreset dataPreset)
    {
        GameDataCollectionRuntimeConfig config =
            GameAudioManagerPresetBakeUtility.BuildDataCollectionRuntimeConfig(preset, dataPreset);
        Require(config.Enabled != 0, "The default data-collection runtime config is disabled.");
        Require(config.MaximumEventsPerBatch == preset.DataCollectionSettings.MaximumEventsPerBatch,
                "Maximum Events Per Batch did not propagate through bake.");
        Require(config.MaximumPendingEvents == preset.DataCollectionSettings.MaximumPendingEvents,
                "Maximum Pending Events did not propagate through bake.");
        Require(config.RevealDevActionsActionId.ToString() ==
                preset.DataCollectionSettings.RevealDevActionsActionId,
                "Reveal Dev Actions did not propagate through bake.");

        GameDataCollectionManagerPreset disabledPreset =
            ScriptableObject.CreateInstance<GameDataCollectionManagerPreset>();

        try
        {
            SerializedObject serializedPreset = new SerializedObject(disabledPreset);
            serializedPreset.FindProperty("dataCollectionEnabled").boolValue = false;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            config = GameAudioManagerPresetBakeUtility.BuildDataCollectionRuntimeConfig(preset,
                                                                                         disabledPreset);
            Require(config.Enabled == 0, "The global data-collection switch did not disable bake output.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(disabledPreset);
        }
    }

    /// <summary>
    /// Verifies that the default Game Master preset owns the dedicated global data preset.
    /// </summary>
    /// <param name="dataPreset">Expected Data Collection Manager preset.</param>
    private static void ValidateMasterPreset(GameDataCollectionManagerPreset dataPreset)
    {
        GameMasterPreset masterPreset = AssetDatabase.LoadAssetAtPath<GameMasterPreset>(MasterPresetPath);
        Require(masterPreset != null, "The default Game Master preset is missing.");
        Require(masterPreset.DataCollectionManagerPreset == dataPreset,
                "The Game Master preset does not reference the default Data Collection Manager preset.");
    }

    /// <summary>
    /// Builds the complete runtime bootstrap and verifies that variable telemetry storage remains isolated.
    /// </summary>
    private static void ValidateRuntimeArchetypeIsolation()
    {
        GameMasterPreset masterPreset = AssetDatabase.LoadAssetAtPath<GameMasterPreset>(MasterPresetPath);
        Require(masterPreset != null && masterPreset.AudioManagerPreset != null,
                "The default runtime bootstrap presets are incomplete.");
        World previousWorld = World.DefaultGameObjectInjectionWorld;
        World smokeWorld = new World("Game Data Collection Archetype Smoke World", WorldFlags.Game);
        GameObject authoringObject = new GameObject("Game Data Collection Archetype Smoke Authoring");

        try
        {
            World.DefaultGameObjectInjectionWorld = smokeWorld;
            GameAudioManagerAuthoring authoring = authoringObject.AddComponent<GameAudioManagerAuthoring>();
            SerializedObject serializedAuthoring = new SerializedObject(authoring);
            serializedAuthoring.FindProperty("masterPreset").objectReferenceValue = masterPreset;
            serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
            Require(GameAudioManagerRuntimeBootstrapUtility.TryCreate(authoring),
                    "The complete runtime bootstrap could not be created.");

            EntityQuery managerQuery = smokeWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GameAudioRuntimeConfig>());
            EntityQuery telemetryQuery = smokeWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GameDataCollectionRuntimeConfig>(),
                ComponentType.ReadOnly<GameTelemetryEvent>());
            Require(managerQuery.CalculateEntityCount() == 1,
                    "The runtime bootstrap did not create exactly one manager entity.");
            Require(telemetryQuery.CalculateEntityCount() == 1,
                    "The runtime bootstrap did not create exactly one telemetry entity.");
            Entity managerEntity = managerQuery.GetSingletonEntity();
            Entity telemetryEntity = telemetryQuery.GetSingletonEntity();
            Require(managerEntity != telemetryEntity,
                    "Telemetry was added to the dense Audio, Settings, and HUD archetype.");
            Require(!smokeWorld.EntityManager.HasBuffer<GameTelemetryEvent>(managerEntity),
                    "The manager archetype still contains the variable telemetry buffer.");
            managerQuery.Dispose();
            telemetryQuery.Dispose();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(authoringObject);
            World.DefaultGameObjectInjectionWorld = previousWorld;
            smokeWorld.Dispose();
        }
    }

    /// <summary>
    /// Verifies that every runtime control and the three fixed dashboard views are authored in the prefab.
    /// </summary>
    private static void ValidateAuthoredUi()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SettingsPrefabPath);
        Require(prefabRoot != null, "The Settings menu prefab could not be loaded.");

        try
        {
            SettingsDevSectionController controller = prefabRoot.GetComponent<SettingsDevSectionController>();
            Require(controller != null, "The Settings prefab has no Dev section controller.");
            SerializedObject serializedController = new SerializedObject(controller);

            // Check every singular authored dependency through Unity serialization.
            for (int referenceIndex = 0;
                 referenceIndex < RequiredDevControllerReferences.Length;
                 referenceIndex++)
            {
                SerializedProperty property = serializedController.FindProperty(
                    RequiredDevControllerReferences[referenceIndex]);
                Require(property != null && property.objectReferenceValue != null,
                        "Dev controller reference is missing: " + RequiredDevControllerReferences[referenceIndex]);
            }

            SerializedProperty dashboardViews = serializedController.FindProperty("dashboardViews");
            Require(dashboardViews != null && dashboardViews.arraySize == 3,
                    "The Dev section must contain Programming, Design, and 3D dashboard views.");
            Require(prefabRoot.transform.Find("DevPanel") != null ||
                    controller.PanelRoot != null && controller.PanelRoot.name == "DevPanel",
                    "The authored Dev panel is missing.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>
    /// Verifies that the bootstrap scene owns the managed HTTPS boundary.
    /// </summary>
    private static void ValidateBootstrapScene()
    {
        Require(File.Exists(BootstrapScenePath), "The bootstrap scene is missing.");
        string sceneText = File.ReadAllText(BootstrapScenePath);
        Require(sceneText.Contains("Assembly-CSharp::GameDataCollectionApiClient"),
                "The bootstrap scene does not contain GameDataCollectionApiClient.");
    }

    /// <summary>
    /// Verifies the deployable MariaDB API package and excludes the private production config.
    /// </summary>
    private static void ValidateBackendPackage()
    {
        for (int fileIndex = 0; fileIndex < RequiredBackendFiles.Length; fileIndex++)
            Require(File.Exists(RequiredBackendFiles[fileIndex]),
                    "Required backend file is missing: " + RequiredBackendFiles[fileIndex]);

        Require(!File.Exists("Backend/TelemetryApi/private/config.php"),
                "A private backend config is present in the repository and could expose credentials.");
        string healthEndpoint = File.ReadAllText("Backend/TelemetryApi/public/api/v1/health.php");
        Require(healthEndpoint.Contains("MariaDB"), "The health endpoint does not enforce MariaDB.");
        string telemetrySchema = File.ReadAllText("Backend/TelemetryApi/database/003_telemetry.sql");
        Require(telemetrySchema.Contains("integer_a") && telemetrySchema.Contains("integer_b") &&
                telemetrySchema.Contains("integer_c") && telemetrySchema.Contains("integer_d"),
                "The MariaDB telemetry schema does not preserve every ECS integer metric.");
        string telemetryWriter = File.ReadAllText("Backend/TelemetryApi/src/telemetry.php");
        Require(telemetryWriter.Contains("integerA") && telemetryWriter.Contains("integerB") &&
                telemetryWriter.Contains("integerC") && telemetryWriter.Contains("integerD"),
                "The PHP telemetry writer does not validate every ECS integer metric.");
    }
    #endregion

    #region ECS Validation
    /// <summary>
    /// Verifies category gates, bounded queue eviction, UUID creation, and consent-revocation purging.
    /// </summary>
    /// <param name="preset">Default Settings Manager preset supplying technical values.</param>
    /// <param name="dataPreset">Global availability preset supplying the bake gate.</param>
    private static void ValidateConsentQueue(GameSettingsManagerPreset preset,
                                               GameDataCollectionManagerPreset dataPreset)
    {
        World previousWorld = World.DefaultGameObjectInjectionWorld;
        World smokeWorld = new World("Game Data Collection Smoke World", WorldFlags.Game);

        try
        {
            World.DefaultGameObjectInjectionWorld = smokeWorld;
            Entity entity = smokeWorld.EntityManager.CreateEntity(
                typeof(GameDataCollectionRuntimeConfig),
                typeof(GameDataCollectionSessionState));
            GameDataCollectionRuntimeConfig config =
                GameAudioManagerPresetBakeUtility.BuildDataCollectionRuntimeConfig(preset, dataPreset);
            config.Enabled = 1;
            config.CollectInEditor = 1;
            config.MaximumPendingEvents = 2;
            smokeWorld.EntityManager.SetComponentData(entity, config);
            DynamicBuffer<GameTelemetryEvent> events =
                smokeWorld.EntityManager.AddBuffer<GameTelemetryEvent>(entity);
            GameDataCollectionSessionState state = smokeWorld.EntityManager.GetComponentData<
                GameDataCollectionSessionState>(entity);
            Require(!TryEnqueueProgramming(events, ref state, in config),
                    "An event was collected before authentication and consent.");
            Require(GameDataCollectionSessionRuntimeUtility.TryApplyAuthenticatedUser(
                        "11111111-1111-4111-8111-111111111111",
                        GameDataCollectionUserRole.User),
                    "The ECS singleton rejected a server-issued identity.");
            Require(GameDataCollectionSessionRuntimeUtility.TryApplyConsent(true, true, false, true),
                    "Consent could not be applied to the ECS singleton.");
            state = smokeWorld.EntityManager.GetComponentData<GameDataCollectionSessionState>(entity);
            Require(Guid.TryParse(state.SessionId.ToString(), out Guid sessionId) && sessionId != Guid.Empty,
                    "Consent did not create a valid stable game-session UUID.");
            Require(TryEnqueueProgramming(events, ref state, in config),
                    "A consented Programming event was rejected.");
            Require(!TryEnqueueDesign(events, ref state, in config),
                    "A declined Design event was collected.");
            Require(TryEnqueueArt3D(events, ref state, in config),
                    "A consented 3D event was rejected.");
            Require(TryEnqueueProgramming(events, ref state, in config) && events.Length == 2,
                    "The bounded queue did not evict exactly its oldest event.");
            smokeWorld.EntityManager.SetComponentData(entity, state);
            Require(GameDataCollectionSessionRuntimeUtility.TryApplyConsent(true, false, true, true),
                    "Updated consent could not be applied.");
            state = smokeWorld.EntityManager.GetComponentData<GameDataCollectionSessionState>(entity);
            Require(events.Length == 1 && events[0].Department == GameTelemetryDepartment.Art3D,
                    "Revoked Programming events were not purged from the pending queue.");
            Require(TryEnqueueDesign(events, ref state, in config),
                    "A newly consented Design event was rejected.");
            smokeWorld.EntityManager.SetComponentData(entity, state);
            Require(GameDataCollectionSessionRuntimeUtility.TryApplyConsent(true, false, false, false) &&
                    events.IsEmpty,
                    "Revoking all categories did not purge pending telemetry.");
            Require(GameDataCollectionSessionRuntimeUtility.TryClearAuthentication(),
                    "Logout state could not be cleared.");
            state = smokeWorld.EntityManager.GetComponentData<GameDataCollectionSessionState>(entity);
            Require(state.UserId.IsEmpty && state.SessionId.IsEmpty &&
                    state.NoticeAcknowledged == 0 && events.IsEmpty,
                    "Logout retained identity, consent, session, or pending telemetry.");
        }
        finally
        {
            World.DefaultGameObjectInjectionWorld = previousWorld;
            smokeWorld.Dispose();
        }
    }

    /// <summary>
    /// Attempts to enqueue one representative Programming event.
    /// </summary>
    /// <param name="events">Pending event buffer.</param>
    /// <param name="state">Mutable session state.</param>
    /// <param name="config">Bounded runtime config.</param>
    /// <returns>True when the event passes the active consent gate.</returns>
    private static bool TryEnqueueProgramming(DynamicBuffer<GameTelemetryEvent> events,
                                              ref GameDataCollectionSessionState state,
                                              in GameDataCollectionRuntimeConfig config)
    {
        return GameTelemetryEventRuntimeUtility.TryEnqueue(
            events,
            ref state,
            in config,
            GameTelemetryEventType.PerformanceSample,
            GameTelemetryDepartment.Programming,
            "smoke",
            GameTelemetryEventRuntimeUtility.GetUtcNowUnixMilliseconds());
    }

    /// <summary>
    /// Attempts to enqueue one representative Design event.
    /// </summary>
    /// <param name="events">Pending event buffer.</param>
    /// <param name="state">Mutable session state.</param>
    /// <param name="config">Bounded runtime config.</param>
    /// <returns>True when the event passes the active consent gate.</returns>
    private static bool TryEnqueueDesign(DynamicBuffer<GameTelemetryEvent> events,
                                         ref GameDataCollectionSessionState state,
                                         in GameDataCollectionRuntimeConfig config)
    {
        return GameTelemetryEventRuntimeUtility.TryEnqueue(
            events,
            ref state,
            in config,
            GameTelemetryEventType.RoomCleared,
            GameTelemetryDepartment.Design,
            "smoke",
            GameTelemetryEventRuntimeUtility.GetUtcNowUnixMilliseconds());
    }

    /// <summary>
    /// Attempts to enqueue one representative 3D event.
    /// </summary>
    /// <param name="events">Pending event buffer.</param>
    /// <param name="state">Mutable session state.</param>
    /// <param name="config">Bounded runtime config.</param>
    /// <returns>True when the event passes the active consent gate.</returns>
    private static bool TryEnqueueArt3D(DynamicBuffer<GameTelemetryEvent> events,
                                        ref GameDataCollectionSessionState state,
                                        in GameDataCollectionRuntimeConfig config)
    {
        return GameTelemetryEventRuntimeUtility.TryEnqueue(
            events,
            ref state,
            in config,
            GameTelemetryEventType.RenderingLoadSample,
            GameTelemetryDepartment.Art3D,
            "smoke",
            GameTelemetryEventRuntimeUtility.GetUtcNowUnixMilliseconds());
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Throws one actionable smoke-test failure when a required condition is not met.
    /// </summary>
    /// <param name="condition">Condition required to continue.</param>
    /// <param name="message">Failure describing the invalid path.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameDataCollectionSmokeTest: " + message);
    }
    #endregion

    #endregion
}
