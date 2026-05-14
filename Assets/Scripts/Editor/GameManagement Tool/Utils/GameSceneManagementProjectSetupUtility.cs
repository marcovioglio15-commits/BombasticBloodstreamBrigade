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
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneManagementProjectSetupUtility
{
    #region Constants
    public const string BootstrapSceneId = "SCN_Bootstrap";
    public const string MainMenuSceneId = "SCN_MainMenu";
    public const string GameplaySceneId = "SCN_PlayerControllerTesting";
    public const string GameplayUiSceneId = "SCN_PlayerControllerTesting_UI";
    public const string PersistentPlayerSceneId = "SCN_PlayerPersistent";
    public const string BootstrapScenePath = "Assets/Scenes/Testing/Main Scenes/Bootstrap/SCN_Bootstrap.unity";
    public const string MainMenuScenePath = "Assets/Scenes/Testing/Main Scenes/UI/SCN_MainMenu.unity";
    public const string GameplayScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_PlayerControllerTesting/SCN_PlayerControllerTesting.unity";
    public const string GameplayUiScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_PlayerControllerTesting/SCN_PlayerControllerTesting_UI.unity";
    public const string PersistentPlayerScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_PlayerControllerTesting/SUB_Player.unity";

    private const string DefaultMasterPresetPath = "Assets/Scriptable Objects/Game/Master Presets/GameMasterPreset.asset";
    private const string DefaultScenePresetPath = "Assets/Scriptable Objects/Game/Scene Management/GameSceneManagerPreset.asset";
    private const string BootstrapManagerObjectName = "GameSceneManager";
    private const string FadeCanvasObjectName = "Canvas_SceneTransitionFade";
    private const string FadeSurfaceObjectName = "FadeSurface";
    private const int FadeSortingOrder = 32767;
    private const float BootstrapFallbackCameraDepth = -1000f;

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
    /// /params None.
    /// /returns None.
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
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void ExecuteBatchSetup()
    {
        ApplyDefaultProjectSetup(true);
    }

    /// <summary>
    /// Creates or refreshes the default Scene Manager preset, master preset link, bootstrap scene, build order and transition layer.
    /// /params logToConsole True when setup completion should be logged.
    /// /returns None.
    /// </summary>
    public static void ApplyDefaultProjectSetup(bool logToConsole)
    {
        EnsureSceneFolders();
        GameSceneTransitionLayerUtility.TryCreateLayer(GameSceneTriggerSettings.DefaultTransitionLayerName);

        GameSceneManagerPreset scenePreset = EnsureSceneManagerPreset();
        GameMasterPreset masterPreset = EnsureGameMasterPreset(scenePreset);
        GameSceneManagementProjectSetupGameplayUiUtility.EnsureGameplayUiScene();
        SynchronizeSceneManagerPreset(scenePreset);
        ApplyDefaultBuildSettings();
        SynchronizeSceneManagerPreset(scenePreset);
        GameSceneAddressablesEditorUtility.EnsureSceneEntries(scenePreset);
        EnsureBootstrapScene(masterPreset, scenePreset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logToConsole)
            Debug.Log("[GameSceneManagementProjectSetupUtility] Default Scene Manager setup completed.");
    }
    #endregion

    #region Preset Setup
    /// <summary>
    /// Loads or creates the default Scene Manager preset and registers it in the Scene Manager library.
    /// /params None.
    /// /returns Default Scene Manager preset asset.
    /// </summary>
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
    /// Loads or creates the default Game Master preset and links the Scene Manager sub-preset.
    /// /params scenePreset Scene Manager preset assigned as the master sub-preset.
    /// /returns Default Game Master preset asset.
    /// </summary>
    private static GameMasterPreset EnsureGameMasterPreset(GameSceneManagerPreset scenePreset)
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
        SetObjectReference(serializedMaster, "sceneManagerPreset", scenePreset);
        serializedMaster.ApplyModifiedPropertiesWithoutUndo();
        masterPreset.ValidateValues();
        EditorUtility.SetDirty(masterPreset);
        return masterPreset;
    }

    /// <summary>
    /// Writes the default bootstrap, scene table, transitions and runtime defaults to the Scene Manager preset.
    /// /params preset Scene Manager preset to synchronize.
    /// /returns None.
    /// </summary>
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
        SynchronizeFadeSettings(serializedPreset);
        GameSceneManagementProjectSetupLoadingProgressUtility.SynchronizeLoadingProgressSettings(serializedPreset);
        SynchronizeTriggerSettings(serializedPreset);
        SynchronizeSceneDefinitions(serializedPreset);
        SynchronizeTransitionDefinitions(serializedPreset);
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);
    }

    /// <summary>
    /// Writes default fade timing values used by bootstrap transitions.
    /// /params serializedPreset Serialized Scene Manager preset.
    /// /returns None.
    /// </summary>
    private static void SynchronizeFadeSettings(SerializedObject serializedPreset)
    {
        SerializedProperty fadeSettingsProperty = serializedPreset.FindProperty("fadeSettings");

        if (fadeSettingsProperty == null)
            return;

        SetColor(fadeSettingsProperty, "fadeColor", Color.black);
        SetFloat(fadeSettingsProperty, "fadeOutSeconds", 0.35f);
        SetFloat(fadeSettingsProperty, "postLoadReadyExtraSeconds", 0.08f);
        SetFloat(fadeSettingsProperty, "fadeInSeconds", 0.35f);
        SetBool(fadeSettingsProperty, "lockGameplayInput", true);
        SetBool(fadeSettingsProperty, "setTimeScaleDuringTransition", true);
    }

    /// <summary>
    /// Writes default trigger settings and expected layer name used by transition volumes.
    /// /params serializedPreset Serialized Scene Manager preset.
    /// /returns None.
    /// </summary>
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
    /// /params serializedPreset Serialized Scene Manager preset.
    /// /returns None.
    /// </summary>
    private static void SynchronizeSceneDefinitions(SerializedObject serializedPreset)
    {
        SerializedProperty scenesProperty = serializedPreset.FindProperty("sceneDefinitions");

        if (scenesProperty == null)
            return;

        scenesProperty.arraySize = DefaultSceneDefinitions.Length;

        for (int index = 0; index < DefaultSceneDefinitions.Length; index++)
        {
            SerializedProperty sceneProperty = scenesProperty.GetArrayElementAtIndex(index);
            GameSceneDefinitionSetup setup = DefaultSceneDefinitions[index];
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
    /// /params serializedPreset Serialized Scene Manager preset.
    /// /returns None.
    /// </summary>
    private static void SynchronizeTransitionDefinitions(SerializedObject serializedPreset)
    {
        SerializedProperty transitionsProperty = serializedPreset.FindProperty("transitionDefinitions");

        if (transitionsProperty == null)
            return;

        transitionsProperty.arraySize = DefaultTransitionDefinitions.Length;

        for (int index = 0; index < DefaultTransitionDefinitions.Length; index++)
        {
            SerializedProperty transitionProperty = transitionsProperty.GetArrayElementAtIndex(index);
            GameSceneTransitionDefinitionSetup setup = DefaultTransitionDefinitions[index];
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
    /// /params None.
    /// /returns None.
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
    /// /params scenes Mutable build-settings scene list.
    /// /params scenePath Project-relative Unity scene path.
    /// /returns None.
    /// </summary>
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
    /// /params setup Default scene setup entry.
    /// /returns Stable Addressables key, or an empty string for bootstrap and SubScene entries.
    /// </summary>
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
    /// /params scenePath Project-relative scene path.
    /// /returns True when the scene should not remain in Build Settings.
    /// </summary>
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
    /// /params masterPreset Game Master preset assigned to the bootstrap manager authoring component.
    /// /params scenePreset Direct Scene Manager fallback assigned to the bootstrap manager authoring component.
    /// /returns None.
    /// </summary>
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
    /// /params None.
    /// /returns Open bootstrap scene.
    /// </summary>
    private static Scene OpenOrCreateBootstrapScene()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);

        if (sceneAsset != null)
            return EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    /// <summary>
    /// Ensures the bootstrap scene has one camera for standalone play-mode visibility.
    /// /params scene Bootstrap scene being configured.
    /// /returns None.
    /// </summary>
    private static void EnsureBootstrapCamera(Scene scene)
    {
        Camera camera = FindFirstComponentInScene<Camera>(scene);

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Bootstrap Fallback Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.GetComponent<Camera>();
        }

        AudioListener audioListener = EnsureComponent<AudioListener>(camera.gameObject);
        GameSceneBootstrapCameraView bootstrapCameraView = EnsureComponent<GameSceneBootstrapCameraView>(camera.gameObject);

        camera.gameObject.name = "Bootstrap Fallback Camera";
        camera.gameObject.tag = "Untagged";
        camera.depth = BootstrapFallbackCameraDepth;

        SerializedObject serializedView = new SerializedObject(bootstrapCameraView);
        serializedView.Update();
        SetObjectReference(serializedView, "bootstrapCamera", camera);
        SetObjectReference(serializedView, "bootstrapAudioListener", audioListener);
        SetBool(serializedView, "disableWhenManagedCameraExists", true);
        SetFloat(serializedView, "fallbackCameraDepth", BootstrapFallbackCameraDepth);
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(audioListener);
        EditorUtility.SetDirty(bootstrapCameraView);
    }

    /// <summary>
    /// Ensures the bootstrap scene has one light so opening it directly is not visually blank.
    /// /params scene Bootstrap scene being configured.
    /// /returns None.
    /// </summary>
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
    /// /params scene Bootstrap scene being configured.
    /// /params masterPreset Game Master preset assigned to authoring.
    /// /params scenePreset Scene Manager fallback assigned to authoring.
    /// /returns None.
    /// </summary>
    private static void EnsureSceneManagerAuthoring(Scene scene, GameMasterPreset masterPreset, GameSceneManagerPreset scenePreset)
    {
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
        EditorUtility.SetDirty(manager);
    }

    /// <summary>
    /// Ensures the bootstrap scene owns an authored full-screen fade canvas view.
    /// /params scene Bootstrap scene being configured.
    /// /returns None.
    /// </summary>
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
        fadeImage.raycastTarget = true;
        fadeImage.enabled = false;

        SerializedObject serializedView = new SerializedObject(view);
        serializedView.Update();
        SetObjectReference(serializedView, "fadeCanvas", canvas);
        SetObjectReference(serializedView, "canvasGroup", canvasGroup);
        SetObjectReference(serializedView, "fadeImage", fadeImage);
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        GameSceneManagementProjectSetupLoadingProgressUtility.EnsureLoadingProgressView(view.gameObject);
        EditorUtility.SetDirty(view);
    }

    /// <summary>
    /// Creates a new fade canvas root in the bootstrap scene.
    /// /params scene Bootstrap scene receiving the canvas.
    /// /returns Newly created fade canvas view.
    /// </summary>
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
    /// /params parent Fade canvas transform.
    /// /returns Fade surface image component.
    /// </summary>
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
    /// /params None.
    /// /returns None.
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
