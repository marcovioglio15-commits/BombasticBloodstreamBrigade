using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static GameSceneManagementProjectSetupSceneUtility;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Applies the default bootstrap-driven Scene Manager setup for the current project scenes and presets.
/// </summary>
public static class GameSceneManagementProjectSetupUtility
{
    #region Constants
    public const string BootstrapSceneId = "SCN_Bootstrap";
    public const string MainMenuSceneId = "SCN_MainMenu";
    public const string GameplaySceneId = "SCN_MainScene";
    public const string GameplayUiSceneId = "SCN_MainScene_UI";
    public const string PersistentPlayerSceneId = "SCN_PlayerPersistent";
    public const string BootstrapScenePath = "Assets/Scenes/Testing/Main Scenes/Bootstrap/SCN_Bootstrap.unity";
    public const string MainMenuScenePath = "Assets/Scenes/Testing/Main Scenes/UI/SCN_MainMenu.unity";
    public const string GameplayScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SCN_MainScene.unity";
    public const string GameplayUiScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SCN_MainScene_UI.unity";
    public const string PersistentPlayerScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SUB_Player.unity";

    private const string DefaultMasterPresetPath = "Assets/Scriptable Objects/Game/Master Presets/GameMasterPreset.asset";
    private const string DefaultScenePresetPath = "Assets/Scriptable Objects/Game/Scene Management/GameSceneManagerPreset.asset";
    private const string DefaultSettingsPresetPath = "Assets/Scriptable Objects/Game/Settings/GameSettingsManagerPreset.asset";
    private const string DefaultHudPresetPath = "Assets/Scriptable Objects/Game/HUD/GameHudManagerPreset.asset";
    private const string FadeMaterialPath = "Assets/2D/Materials/M_UI_PaintRevealSceneTransition.mat";
    private const string BootstrapManagerObjectName = "GameSceneManager";
    private const string FadeCanvasObjectName = "Canvas_SceneTransitionFade";
    private const string FadeSurfaceObjectName = "FadeSurface";
    private const int FadeSortingOrder = 32767;

    private static readonly GameSceneDefinitionSetup[] DefaultSceneDefinitions =
    {
        new GameSceneDefinitionSetup(BootstrapSceneId, BootstrapSceneId, BootstrapScenePath, GameSceneKind.Bootstrap, GameSceneUnloadPolicy.Persistent, string.Empty),
        new GameSceneDefinitionSetup(MainMenuSceneId, MainMenuSceneId, MainMenuScenePath, GameSceneKind.MainMenu, GameSceneUnloadPolicy.UnloadOnTransition, string.Empty),
        new GameSceneDefinitionSetup(GameplaySceneId, GameplaySceneId, GameplayScenePath, GameSceneKind.Gameplay, GameSceneUnloadPolicy.UnloadOnTransition, GameplayUiSceneId),
        new GameSceneDefinitionSetup(GameplayUiSceneId, GameplayUiSceneId, GameplayUiScenePath, GameSceneKind.PersistentUi, GameSceneUnloadPolicy.UnloadOnTransition, string.Empty),
        new GameSceneDefinitionSetup(PersistentPlayerSceneId, "SUB_Player", PersistentPlayerScenePath, GameSceneKind.PersistentPlayer, GameSceneUnloadPolicy.Persistent, string.Empty)
    };

    private static readonly GameSceneTransitionDefinitionSetup[] DefaultTransitionDefinitions =
    {
        new GameSceneTransitionDefinitionSetup("TRN_MainMenu_To_Gameplay", MainMenuSceneId, GameplaySceneId, GameSceneTransitionMode.MenuCommand),
        new GameSceneTransitionDefinitionSetup("TRN_Gameplay_To_MainMenu", GameplaySceneId, MainMenuSceneId, GameSceneTransitionMode.MenuCommand)
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the project setup from the Unity menu after giving the editor a chance to save open scenes.
    /// </summary>
    //[MenuItem("Tools/Game/Scene Manager/Apply Default Setup")]
    public static void ExecuteSetupFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ApplyDefaultProjectSetup(true);
    }

    /// <summary>
    /// Executes the setup from batch mode without opening any confirmation dialogs.
    /// </summary>
    public static void ExecuteBatchSetup()
    {
        ApplyDefaultProjectSetup(true);
    }

    /// <summary>
    /// Creates or refreshes the default Scene Manager preset, master preset link, bootstrap scene, build order and transition layer.
    /// </summary>
    /// <param name="logToConsole">True when setup completion should be logged.</param>
    public static void ApplyDefaultProjectSetup(bool logToConsole)
    {
        EnsureSceneFolders();
        GameUiAerosolPaintProjectSetupUtility.EnsureAssets(false);
        GameSceneTransitionLayerUtility.TryCreateLayer(GameSceneTriggerSettings.DefaultTransitionLayerName);
        GameSceneManagementProjectSetupProceduralTransitionUtility.EnsureProjectLayer();
        GameSceneManagementProjectSetupProceduralTransitionUtility.EnsureRendererFeatureLayers();

        GameSettingsManagerPreset settingsPreset = EnsureSettingsManagerPreset();
        GameHudManagerPreset hudPreset = EnsureHudManagerPreset();
        GameHudSupplementalProjectSetupUtility.EnsureDefaultSettings(hudPreset);
        GameSceneManagerPreset scenePreset = EnsureSceneManagerPreset();
        GameMasterPreset existingMasterPreset = AssetDatabase.LoadAssetAtPath<GameMasterPreset>(DefaultMasterPresetPath);
        GameProceduralLevelPreset proceduralLevelPreset = GameProceduralLevelProjectSetupUtility.EnsurePreset(existingMasterPreset != null
                                                                                                                ? existingMasterPreset.ProceduralLevelPreset
                                                                                                                : null,
                                                                                                            scenePreset);
        GameMasterPreset masterPreset = EnsureGameMasterPreset(scenePreset, settingsPreset, hudPreset, proceduralLevelPreset);
        EnsureBootstrapScene(masterPreset, scenePreset);
        GameHudSupplementalProjectSetupUtility.EnsureLoadedGameplayUiAndMenus();
        GameSceneEnvironmentPostProcessSetupUtility.ApplyDefaultGameplaySceneSetup(false);
        scenePreset = EnsureSceneManagerPreset();
        GameScenePersistentGameplayCameraSetupUtility.Apply(scenePreset, false);
        scenePreset = EnsureSceneManagerPreset();
        masterPreset = AssetDatabase.LoadAssetAtPath<GameMasterPreset>(DefaultMasterPresetPath);
        proceduralLevelPreset = GameProceduralLevelProjectSetupUtility.EnsurePreset(masterPreset != null
                                                                                       ? masterPreset.ProceduralLevelPreset
                                                                                       : null,
                                                                                   scenePreset);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        GameRoomMetadataRefreshReport metadataRefreshReport = GameRoomMetadataScannerUtility.RefreshReferencedRooms(proceduralLevelPreset);

        if (!metadataRefreshReport.Succeeded)
            throw new InvalidOperationException("Persistent camera migration could not refresh room metadata: " +
                                                string.Join("; ", metadataRefreshReport.Errors));

        SynchronizeSceneManagerPreset(scenePreset);
        ApplyDefaultBuildSettings();
        SynchronizeSceneManagerPreset(scenePreset);
        GameSceneAddressablesEditorUtility.EnsureSceneEntries(scenePreset);
        AssetDatabase.SaveAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logToConsole)
            Debug.Log("[GameSceneManagementProjectSetupUtility] Default Scene Manager setup completed.");
    }

    /// <summary>
    /// Loads or creates the default Settings Manager preset and registers it in the Settings Manager library.
    /// </summary>
    /// <returns>Default Settings Manager preset asset.</returns>
    private static GameSettingsManagerPreset EnsureSettingsManagerPreset()
    {
        GameSettingsManagerPreset preset = AssetDatabase.LoadAssetAtPath<GameSettingsManagerPreset>(DefaultSettingsPresetPath);

        if (preset == null)
            preset = GameSettingsManagerPresetLibraryUtility.CreatePresetAsset("GameSettingsManagerPreset");

        if (preset == null)
            throw new InvalidOperationException("Unable to create the default GameSettingsManagerPreset asset.");

        GameSettingsManagerPresetLibrary library = GameSettingsManagerPresetLibraryUtility.GetOrCreateLibrary();
        library.AddPreset(preset);
        EditorUtility.SetDirty(library);
        return preset;
    }

    /// <summary>
    /// Loads or creates the default HUD Manager preset and registers it in the HUD Manager library.
    /// </summary>
    /// <returns>Default HUD Manager preset asset.</returns>
    private static GameHudManagerPreset EnsureHudManagerPreset()
    {
        GameHudManagerPreset preset = AssetDatabase.LoadAssetAtPath<GameHudManagerPreset>(DefaultHudPresetPath);

        if (preset == null)
            preset = GameHudManagerPresetLibraryUtility.CreatePresetAsset("GameHudManagerPreset");

        if (preset == null)
            throw new InvalidOperationException("Unable to create the default GameHudManagerPreset asset.");

        preset.EnsureInitialized();
        EditorUtility.SetDirty(preset);

        GameHudManagerPresetLibrary library = GameHudManagerPresetLibraryUtility.GetOrCreateLibrary();
        library.AddPreset(preset);
        EditorUtility.SetDirty(library);
        return preset;
    }
    #endregion

    #region Preset Setup
    /// <summary>
    /// Loads or creates the default Scene Manager preset and registers it in the Scene Manager library.
    /// </summary>
    /// <returns>Default Scene Manager preset asset.</returns>
    private static GameSceneManagerPreset EnsureSceneManagerPreset()
    {
        GameSceneManagerPreset preset = AssetDatabase.LoadAssetAtPath<GameSceneManagerPreset>(DefaultScenePresetPath);

        if (preset == null)
            preset = GameSceneManagerPresetLibraryUtility.CreatePresetAsset("GameSceneManagerPreset");

        if (preset == null)
            throw new InvalidOperationException("Unable to create the default GameSceneManagerPreset asset.");

        GameSceneManagerPresetLibrary library = GameSceneManagerPresetLibraryUtility.GetOrCreateLibrary();
        library.AddPreset(preset);
        EditorUtility.SetDirty(library);
        return preset;
    }

    /// <summary>
    /// Loads or creates the default Game Master preset and links the Settings and Scene Manager sub-presets.
    /// </summary>
    /// <param name="settingsPreset">Settings Manager preset assigned as the master sub-preset.</param>
    /// <param name="hudPreset">HUD Manager preset assigned as the master sub-preset.</param>
    /// <param name="scenePreset">Scene Manager preset assigned as the master sub-preset.</param>
    /// <param name="proceduralLevelPreset">Procedural Level preset assigned as the master sub-preset.</param>
    /// <returns>Default Game Master preset asset.</returns>
    private static GameMasterPreset EnsureGameMasterPreset(GameSceneManagerPreset scenePreset,
                                                           GameSettingsManagerPreset settingsPreset,
                                                           GameHudManagerPreset hudPreset,
                                                           GameProceduralLevelPreset proceduralLevelPreset)
    {
        GameMasterPreset masterPreset = AssetDatabase.LoadAssetAtPath<GameMasterPreset>(DefaultMasterPresetPath);

        if (masterPreset == null)
            masterPreset = GameMasterPresetLibraryUtility.CreatePresetAsset("GameMasterPreset");

        if (masterPreset == null)
            throw new InvalidOperationException("Unable to create the default GameMasterPreset asset.");

        GameMasterPresetLibrary library = GameMasterPresetLibraryUtility.GetOrCreateLibrary();
        library.AddPreset(masterPreset);
        EditorUtility.SetDirty(library);

        SerializedObject serializedMaster = new SerializedObject(masterPreset);
        serializedMaster.Update();
        SetObjectReference(serializedMaster, "settingsManagerPreset", settingsPreset);
        SetObjectReference(serializedMaster, "hudManagerPreset", hudPreset);
        SetObjectReference(serializedMaster, "sceneManagerPreset", scenePreset);
        SetObjectReference(serializedMaster, "proceduralLevelPreset", proceduralLevelPreset);
        serializedMaster.ApplyModifiedPropertiesWithoutUndo();
        masterPreset.ValidateValues();
        EditorUtility.SetDirty(masterPreset);
        return masterPreset;
    }

    /// <summary>
    /// Writes the default bootstrap, scene table, transitions and runtime defaults to the Scene Manager preset.
    /// </summary>
    /// <param name="preset">Scene Manager preset to synchronize.</param>
    private static void SynchronizeSceneManagerPreset(GameSceneManagerPreset preset)
    {
        if (preset == null)
            return;

        preset.EnsureInitialized();
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SetString(serializedPreset, "presetName", "GameSceneManagerPreset");
        SetString(serializedPreset, "bootstrapSceneId", BootstrapSceneId);
        SetString(serializedPreset, "initialSceneId", MainMenuSceneId);
        SetString(serializedPreset, "mainMenuSceneId", MainMenuSceneId);
        SetString(serializedPreset, "defaultGameplaySceneId", GameplaySceneId);
        SetBool(serializedPreset, "autoLoadInitialScene", true);
        SetInt(serializedPreset, "loadBackend", (int)GameSceneLoadBackend.Addressables);
        SetBool(serializedPreset, "logTransitions", true);
        GameSceneFadeProjectSetupUtility.Synchronize(serializedPreset);
        GameSceneManagementProjectSetupLoadingProgressUtility.SynchronizeLoadingProgressSettings(serializedPreset);
        SynchronizeTriggerSettings(serializedPreset);
        SynchronizeSceneDefinitions(serializedPreset);
        SynchronizeTransitionDefinitions(serializedPreset);
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);
    }

    /// <summary>
    /// Writes default trigger settings and expected layer name used by transition volumes.
    /// </summary>
    /// <param name="serializedPreset">Serialized Scene Manager preset.</param>
    private static void SynchronizeTriggerSettings(SerializedObject serializedPreset)
    {
        SerializedProperty triggerSettingsProperty = serializedPreset.FindProperty("triggerSettings");

        if (triggerSettingsProperty == null)
            return;

        SetString(triggerSettingsProperty, "transitionLayerName", GameSceneTriggerSettings.DefaultTransitionLayerName);
        SetFloat(triggerSettingsProperty, "defaultCooldownSeconds", 0.75f);
        SetBool(triggerSettingsProperty, "requirePlayer", true);
        SetBool(triggerSettingsProperty, "oneShotByDefault", true);
        SetColor(triggerSettingsProperty, "gizmoColor", new Color(0.1f, 0.55f, 1f, 0.28f));
    }

    /// <summary>
    /// Rebuilds the default managed scene table with synchronized path, GUID, scene asset and build index metadata.
    /// </summary>
    /// <param name="serializedPreset">Serialized Scene Manager preset.</param>
    private static void SynchronizeSceneDefinitions(SerializedObject serializedPreset)
    {
        SerializedProperty scenesProperty = serializedPreset.FindProperty("sceneDefinitions");

        if (scenesProperty == null)
            return;

        for (int index = 0; index < DefaultSceneDefinitions.Length; index++)
        {
            GameSceneDefinitionSetup setup = DefaultSceneDefinitions[index];
            SerializedProperty sceneProperty = FindOrAppendArrayElement(scenesProperty, "sceneId", setup.SceneId);

            if (sceneProperty == null)
                continue;

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(setup.ScenePath);
            SetString(sceneProperty, "sceneId", setup.SceneId);
            SetString(sceneProperty, "sceneName", setup.SceneName);
            SetString(sceneProperty, "scenePath", setup.ScenePath);
            SetString(sceneProperty, "sceneGuid", AssetDatabase.AssetPathToGUID(setup.ScenePath));
            SetInt(sceneProperty, "buildIndex", GameSceneManagementBuildSettingsUtility.ResolveBuildIndex(setup.ScenePath));
            SetInt(sceneProperty, "sceneKind", (int)setup.SceneKind);
            SetInt(sceneProperty, "unloadPolicy", (int)setup.UnloadPolicy);
            SetString(sceneProperty, "companionUiSceneId", setup.CompanionUiSceneId);
            SetString(sceneProperty, "roomTags", string.Empty);
            SetString(sceneProperty, "addressableKey", ResolveDefaultAddressableKey(setup));
            SetObjectReference(sceneProperty, "sceneAsset", sceneAsset);
        }
    }

    /// <summary>
    /// Rebuilds the default menu-command transition graph for menu and gameplay scene flow.
    /// </summary>
    /// <param name="serializedPreset">Serialized Scene Manager preset.</param>
    private static void SynchronizeTransitionDefinitions(SerializedObject serializedPreset)
    {
        SerializedProperty transitionsProperty = serializedPreset.FindProperty("transitionDefinitions");

        if (transitionsProperty == null)
            return;

        for (int index = 0; index < DefaultTransitionDefinitions.Length; index++)
        {
            GameSceneTransitionDefinitionSetup setup = DefaultTransitionDefinitions[index];
            SerializedProperty transitionProperty = FindOrAppendArrayElement(transitionsProperty, "transitionId", setup.TransitionId);

            if (transitionProperty == null)
                continue;

            SetString(transitionProperty, "transitionId", setup.TransitionId);
            SetString(transitionProperty, "fromSceneId", setup.FromSceneId);
            SetString(transitionProperty, "toSceneId", setup.ToSceneId);
            SetInt(transitionProperty, "priority", 0);
            SetInt(transitionProperty, "transitionMode", (int)setup.TransitionMode);
            SetString(transitionProperty, "triggerId", string.Empty);
            SetFloat(transitionProperty, "triggerCooldownOverrideSeconds", -1f);
            SetBool(transitionProperty, "oneShotTrigger", true);
            SetBool(transitionProperty, "overrideFadeSettings", false);
            SetFloat(transitionProperty, "fadeOutSeconds", 0.35f);
            SetFloat(transitionProperty, "postLoadReadyExtraSeconds", 0.08f);
            SetFloat(transitionProperty, "fadeInSeconds", 0.35f);
            SetBool(transitionProperty, "allowDuringPause", true);
            SetBool(transitionProperty, "allowWhenRunFinalized", true);
        }
    }
    #endregion

    #region Build Settings
    /// <summary>
    /// Applies the bootstrap scene as the only required Build Settings entry for Addressables-driven scene loading.
    /// </summary>
    private static void ApplyDefaultBuildSettings()
    {
        List<EditorBuildSettingsScene> orderedScenes = new List<EditorBuildSettingsScene>();
        AddBuildSceneIfMissing(orderedScenes, BootstrapScenePath);

        EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;

        for (int index = 0; index < existingScenes.Length; index++)
        {
            EditorBuildSettingsScene existingScene = existingScenes[index];

            if (string.IsNullOrWhiteSpace(existingScene.path))
                continue;

            if (IsDefaultAddressableScenePath(existingScene.path))
                continue;

            AddBuildSceneIfMissing(orderedScenes, existingScene.path);
        }

        EditorBuildSettings.scenes = orderedScenes.ToArray();
    }

    /// <summary>
    /// Adds one enabled build scene only when its path is not already present.
    /// </summary>
    /// <param name="scenes">Mutable build-settings scene list.</param>
    /// <param name="scenePath">Project-relative Unity scene path.</param>
    private static void AddBuildSceneIfMissing(List<EditorBuildSettingsScene> scenes, string scenePath)
    {
        for (int index = 0; index < scenes.Count; index++)
        {
            if (string.Equals(scenes[index].path, scenePath, StringComparison.Ordinal))
                return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
    }

    /// <summary>
    /// Resolves the default Addressables key for one managed top-level scene.
    /// </summary>
    /// <param name="setup">Default scene setup entry.</param>
    /// <returns>Stable Addressables key, or an empty string for bootstrap and SubScene entries.</returns>
    private static string ResolveDefaultAddressableKey(GameSceneDefinitionSetup setup)
    {
        if (setup.SceneKind == GameSceneKind.Bootstrap)
            return string.Empty;

        if (setup.SceneKind == GameSceneKind.SubScene)
            return string.Empty;

        if (setup.SceneKind == GameSceneKind.PersistentPlayer)
            return string.Empty;

        return setup.SceneId;
    }

    /// <summary>
    /// Resolves whether a scene path is loaded through Addressables by the default Scene Manager setup.
    /// </summary>
    /// <param name="scenePath">Project-relative scene path.</param>
    /// <returns>True when the scene should not remain in Build Settings.</returns>
    private static bool IsDefaultAddressableScenePath(string scenePath)
    {
        return string.Equals(scenePath, MainMenuScenePath, StringComparison.Ordinal) ||
               string.Equals(scenePath, GameplayScenePath, StringComparison.Ordinal) ||
               string.Equals(scenePath, GameplayUiScenePath, StringComparison.Ordinal);
    }
    #endregion

    #region Bootstrap Scene
    /// <summary>
    /// Creates or refreshes the persistent bootstrap scene with the manager authoring object and fade overlay.
    /// </summary>
    /// <param name="masterPreset">Game Master preset assigned to the bootstrap manager authoring component.</param>
    /// <param name="scenePreset">Direct Scene Manager fallback assigned to the bootstrap manager authoring component.</param>
    private static void EnsureBootstrapScene(GameMasterPreset masterPreset, GameSceneManagerPreset scenePreset)
    {
        Scene bootstrapScene = OpenOrCreateBootstrapScene();
        EnsureBootstrapCamera(bootstrapScene);
        EnsureBootstrapLight(bootstrapScene);
        EnsureSceneManagerAuthoring(bootstrapScene, masterPreset, scenePreset);
        EnsureFadeCanvas(bootstrapScene);
        EditorSceneManager.MarkSceneDirty(bootstrapScene);
        EditorSceneManager.SaveScene(bootstrapScene, BootstrapScenePath);
    }

    /// <summary>
    /// Opens the existing bootstrap scene or creates an empty one at the expected path.
    /// </summary>
    /// <returns>Open bootstrap scene.</returns>
    private static Scene OpenOrCreateBootstrapScene()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);

        if (sceneAsset != null)
            return EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    /// <summary>
    /// Ensures the bootstrap scene has one persistent gameplay camera owner or a migration-ready placeholder.
    /// </summary>
    /// <param name="scene">Bootstrap scene being configured.</param>
    private static void EnsureBootstrapCamera(Scene scene)
    {
        GameScenePersistentGameplayCameraSetupUtility.EnsureBootstrapPlaceholder(scene);
    }

    /// <summary>
    /// Ensures the bootstrap scene has one light so opening it directly is not visually blank.
    /// </summary>
    /// <param name="scene">Bootstrap scene being configured.</param>
    private static void EnsureBootstrapLight(Scene scene)
    {
        Light light = FindFirstComponentInScene<Light>(scene);

        if (light != null)
            return;

        GameObject lightObject = new GameObject("Directional Light", typeof(Light));
        Light lightComponent = lightObject.GetComponent<Light>();
        lightComponent.type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        SceneManager.MoveGameObjectToScene(lightObject, scene);
    }

    /// <summary>
    /// Ensures exactly one bootstrap GameSceneManagerAuthoring object exists and references the default presets.
    /// </summary>
    /// <param name="scene">Bootstrap scene being configured.</param>
    /// <param name="masterPreset">Game Master preset assigned to authoring.</param>
    /// <param name="scenePreset">Scene Manager fallback assigned to authoring.</param>
    private static void EnsureSceneManagerAuthoring(Scene scene, GameMasterPreset masterPreset, GameSceneManagerPreset scenePreset)
    {
        masterPreset = ResolveDefaultSetupAsset(masterPreset, DefaultMasterPresetPath, "Game Master preset");
        scenePreset = ResolveDefaultSetupAsset(scenePreset, DefaultScenePresetPath, "Scene Manager preset");

        List<GameSceneManagerAuthoring> managers = FindComponentsInScene<GameSceneManagerAuthoring>(scene);
        GameSceneManagerAuthoring manager = managers.Count > 0 ? managers[0] : null;

        for (int index = 1; index < managers.Count; index++)
            UnityEngine.Object.DestroyImmediate(managers[index].gameObject);

        if (manager == null)
        {
            GameObject managerObject = new GameObject(BootstrapManagerObjectName, typeof(GameSceneManagerAuthoring));
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            manager = managerObject.GetComponent<GameSceneManagerAuthoring>();
        }

        manager.gameObject.name = BootstrapManagerObjectName;
        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.Update();
        SetObjectReference(serializedManager, "masterPreset", masterPreset);
        SetObjectReference(serializedManager, "sceneManagerPreset", scenePreset);
        SetBool(serializedManager, "createRuntimeSingletonWhenNotBaked", true);
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
        ValidateSceneManagerAuthoringReferences(manager, masterPreset, scenePreset);
        EditorUtility.SetDirty(manager);
    }

    /// <summary>
    /// Resolves a default setup asset from its canonical project path before scene references are written.
    /// </summary>
    /// <param name="candidate">Candidate object already resolved by the setup pipeline.</param>
    /// <param name="assetPath">Project-relative canonical asset path.</param>
    /// <param name="assetLabel">Clear asset label used in failure messages.</param>
    /// <returns>Resolved persistent asset reference.</returns>
    private static TAsset ResolveDefaultSetupAsset<TAsset>(TAsset candidate,
                                                           string assetPath,
                                                           string assetLabel) where TAsset : UnityEngine.Object
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TAsset asset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);

        if (asset != null)
            return asset;

        if (candidate != null)
            return candidate;

        throw new InvalidOperationException(assetLabel + " could not be resolved at " + assetPath + ".");
    }

    /// <summary>
    /// Fails setup immediately when bootstrap authoring references were not serialized correctly.
    /// </summary>
    /// <param name="manager">Bootstrap authoring component being validated.</param>
    /// <param name="masterPreset">Expected Game Master preset reference.</param>
    /// <param name="scenePreset">Expected Scene Manager preset reference.</param>
    private static void ValidateSceneManagerAuthoringReferences(GameSceneManagerAuthoring manager,
                                                                GameMasterPreset masterPreset,
                                                                GameSceneManagerPreset scenePreset)
    {
        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.Update();
        SerializedProperty masterProperty = serializedManager.FindProperty("masterPreset");
        SerializedProperty sceneProperty = serializedManager.FindProperty("sceneManagerPreset");

        if (masterProperty == null || sceneProperty == null)
            throw new InvalidOperationException("Bootstrap GameSceneManagerAuthoring preset fields could not be found.");

        if (masterProperty.objectReferenceValue != masterPreset || sceneProperty.objectReferenceValue != scenePreset)
            throw new InvalidOperationException("Bootstrap GameSceneManagerAuthoring preset references were not serialized correctly.");
    }

    /// <summary>
    /// Ensures the bootstrap scene owns an authored full-screen fade canvas view.
    /// </summary>
    /// <param name="scene">Bootstrap scene being configured.</param>
    private static void EnsureFadeCanvas(Scene scene)
    {
        List<GameSceneFadeCanvasView> views = FindComponentsInScene<GameSceneFadeCanvasView>(scene);
        GameSceneFadeCanvasView view = views.Count > 0 ? views[0] : null;

        for (int index = 1; index < views.Count; index++)
            UnityEngine.Object.DestroyImmediate(views[index].gameObject);

        if (view == null)
            view = CreateFadeCanvas(scene);

        Canvas canvas = EnsureComponent<Canvas>(view.gameObject);
        CanvasScaler canvasScaler = EnsureComponent<CanvasScaler>(view.gameObject);
        GraphicRaycaster raycaster = EnsureComponent<GraphicRaycaster>(view.gameObject);
        CanvasGroup canvasGroup = EnsureComponent<CanvasGroup>(view.gameObject);
        Image fadeImage = EnsureFadeSurface(view.transform);
        Material fadeMaterial = AssetDatabase.LoadAssetAtPath<Material>(FadeMaterialPath);

        if (fadeMaterial == null)
            throw new InvalidOperationException("Scene fade paint material is missing: " + FadeMaterialPath + ".");

        RectTransform canvasRect = EnsureComponent<RectTransform>(view.gameObject);
        StretchToParent(canvasRect);

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = FadeSortingOrder;
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;
        raycaster.enabled = true;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        fadeImage.color = Color.black;
        fadeImage.material = fadeMaterial;
        fadeImage.raycastTarget = true;
        fadeImage.enabled = false;

        SerializedObject serializedView = new SerializedObject(view);
        serializedView.Update();
        SetObjectReference(serializedView, "fadeCanvas", canvas);
        SetObjectReference(serializedView, "canvasGroup", canvasGroup);
        SetObjectReference(serializedView, "fadeImage", fadeImage);
        SetObjectReference(serializedView, "fadeMaterial", fadeMaterial);
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        GameSceneManagementProjectSetupProceduralTransitionUtility.EnsureBootstrapPresentation(scene, view, canvas);
        GameSceneManagementProjectSetupLoadingProgressUtility.EnsureLoadingProgressView(view.gameObject);
        EditorUtility.SetDirty(view);
    }

    /// <summary>
    /// Creates a new fade canvas root in the bootstrap scene.
    /// </summary>
    /// <param name="scene">Bootstrap scene receiving the canvas.</param>
    /// <returns>Newly created fade canvas view.</returns>
    private static GameSceneFadeCanvasView CreateFadeCanvas(Scene scene)
    {
        GameObject canvasObject = new GameObject(FadeCanvasObjectName,
                                                 typeof(RectTransform),
                                                 typeof(Canvas),
                                                 typeof(CanvasScaler),
                                                 typeof(GraphicRaycaster),
                                                 typeof(CanvasGroup),
                                                 typeof(GameSceneFadeCanvasView));
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        StretchToParent(canvasRect);
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        return canvasObject.GetComponent<GameSceneFadeCanvasView>();
    }

    /// <summary>
    /// Ensures the fade canvas has one full-screen image child used as the opaque fade surface.
    /// </summary>
    /// <param name="parent">Fade canvas transform.</param>
    /// <returns>Fade surface image component.</returns>
    private static Image EnsureFadeSurface(Transform parent)
    {
        Transform existingChild = parent.Find(FadeSurfaceObjectName);
        GameObject imageObject = existingChild != null ? existingChild.gameObject : null;

        if (imageObject == null)
        {
            imageObject = new GameObject(FadeSurfaceObjectName, typeof(RectTransform), typeof(Image));
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(parent, false);
        }

        RectTransform rectTransform = EnsureComponent<RectTransform>(imageObject);
        StretchToParent(rectTransform);
        return EnsureComponent<Image>(imageObject);
    }
    #endregion

    #region Generic Helpers
    /// <summary>
    /// Ensures every project folder needed by the default scene setup exists.
    /// </summary>
    private static void EnsureSceneFolders()
    {
        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(BootstrapScenePath));
        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(MainMenuScenePath));
        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(GameplayScenePath));
        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(GameplayUiScenePath));
    }

    #endregion

    #endregion
}
