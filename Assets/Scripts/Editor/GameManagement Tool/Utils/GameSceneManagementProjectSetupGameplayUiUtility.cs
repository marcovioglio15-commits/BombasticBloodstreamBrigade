using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static GameSceneManagementProjectSetupSceneUtility;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Builds and maintains the additive gameplay UI scene used by the default Scene Manager setup.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneManagementProjectSetupGameplayUiUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the separate gameplay UI scene and moves authored HUD/menu roots out of the gameplay scene.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void EnsureGameplayUiScene()
    {
        Scene gameplayScene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayScenePath, OpenSceneMode.Single);
        Scene gameplayUiScene = OpenOrCreateGameplayUiScene();
        MoveGameplayUiRoots(gameplayScene, gameplayUiScene);
        Canvas uiCanvas = EnsureGameplayUiCanvas(gameplayScene, gameplayUiScene);
        EnsureGameplayUiHudManager(gameplayScene, gameplayUiScene);
        EnsureGameplayUiEventSystem(gameplayScene, gameplayUiScene);
        EnsureGameplayUiCameraStackBridge(gameplayUiScene);
        EnsureGameplayUiCameraReferences(gameplayUiScene);
        ConfigureGameplayUiCanvas(uiCanvas);
        CleanCameraStacks(gameplayScene);
        CleanCameraStacks(gameplayUiScene);
        EditorSceneManager.MarkSceneDirty(gameplayScene);
        EditorSceneManager.MarkSceneDirty(gameplayUiScene);
        EditorSceneManager.SaveScene(gameplayScene, GameSceneManagementProjectSetupUtility.GameplayScenePath);
        EditorSceneManager.SaveScene(gameplayUiScene, GameSceneManagementProjectSetupUtility.GameplayUiScenePath);
    }
    #endregion

    #region Scene Opening
    /// <summary>
    /// Opens the existing gameplay UI scene or creates an empty additive scene at the expected path.
    /// /params None.
    /// /returns Open gameplay UI scene.
    /// </summary>
    private static Scene OpenOrCreateGameplayUiScene()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameSceneManagementProjectSetupUtility.GameplayUiScenePath);

        if (sceneAsset != null)
            return EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayUiScenePath, OpenSceneMode.Additive);

        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
    }
    #endregion

    #region Root Migration
    /// <summary>
    /// Moves every top-level UI root from gameplay to the additive UI scene before references are serialized.
    /// /params gameplayScene Scene currently holding simulation and possibly authored UI roots.
    /// /params gameplayUiScene Scene that should own gameplay UI roots.
    /// /returns None.
    /// </summary>
    private static void MoveGameplayUiRoots(Scene gameplayScene, Scene gameplayUiScene)
    {
        GameObject[] rootObjects = gameplayScene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            GameObject rootObject = rootObjects[index];

            if (!ShouldMoveGameplayUiRoot(rootObject))
                continue;

            SceneManager.MoveGameObjectToScene(rootObject, gameplayUiScene);
        }
    }

    /// <summary>
    /// Resolves whether one gameplay root belongs to authored UI and should be separated.
    /// /params rootObject Gameplay scene root inspected for UI components.
    /// /returns True when the root should move into the gameplay UI scene.
    /// </summary>
    private static bool ShouldMoveGameplayUiRoot(GameObject rootObject)
    {
        if (rootObject == null)
            return false;

        if (rootObject.GetComponentInChildren<Canvas>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<HUDManager>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<EventSystem>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<GameplayMenuController>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<MenuSelectionController>(true) != null)
            return true;

        if (IsNamedUiCameraRoot(rootObject))
            return true;

        return rootObject.GetComponent<RectTransform>() != null &&
               rootObject.GetComponentInChildren<Selectable>(true) != null;
    }

    /// <summary>
    /// Resolves whether one root is the authored camera used by screen-space camera UI canvases.
    /// /params rootObject Gameplay scene root inspected for camera ownership.
    /// /returns True when this root is the authored UI camera.
    /// </summary>
    private static bool IsNamedUiCameraRoot(GameObject rootObject)
    {
        if (!string.Equals(rootObject.name, "UI Camera", StringComparison.Ordinal))
            return false;

        return rootObject.GetComponentInChildren<Camera>(true) != null;
    }
    #endregion

    #region Canvas
    /// <summary>
    /// Ensures the gameplay UI scene owns the gameplay canvas and removes duplicate gameplay copies.
    /// /params gameplayScene Scene currently holding gameplay simulation content.
    /// /params gameplayUiScene Scene that should own gameplay UI roots.
    /// /returns Gameplay UI canvas or null when no authored canvas exists yet.
    /// </summary>
    private static Canvas EnsureGameplayUiCanvas(Scene gameplayScene, Scene gameplayUiScene)
    {
        Canvas uiSceneCanvas = FindGameplayCanvas(gameplayUiScene);
        Canvas gameplaySceneCanvas = FindGameplayCanvas(gameplayScene);

        if (uiSceneCanvas == null && gameplaySceneCanvas != null)
        {
            SceneManager.MoveGameObjectToScene(gameplaySceneCanvas.transform.root.gameObject, gameplayUiScene);
            return gameplaySceneCanvas;
        }

        if (uiSceneCanvas != null && gameplaySceneCanvas != null)
            UnityEngine.Object.DestroyImmediate(gameplaySceneCanvas.transform.root.gameObject);

        return uiSceneCanvas;
    }

    /// <summary>
    /// Applies stable screen-space settings to the separated gameplay UI canvas.
    /// /params canvas Gameplay UI canvas to configure.
    /// /returns None.
    /// </summary>
    private static void ConfigureGameplayUiCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        CanvasScaler canvasScaler = EnsureComponent<CanvasScaler>(canvas.gameObject);
        GraphicRaycaster graphicRaycaster = EnsureComponent<GraphicRaycaster>(canvas.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;
        graphicRaycaster.enabled = true;
        EditorUtility.SetDirty(canvas);
    }

    /// <summary>
    /// Finds the authored gameplay canvas by HUDManager first, CanvasMain second and any root Canvas last.
    /// /params scene Scene searched by root hierarchy.
    /// /returns Gameplay canvas when available.
    /// </summary>
    private static Canvas FindGameplayCanvas(Scene scene)
    {
        HUDManager hudManager = FindFirstComponentInScene<HUDManager>(scene);

        if (hudManager != null)
        {
            Canvas parentCanvas = hudManager.GetComponentInParent<Canvas>(true);

            if (parentCanvas != null)
                return parentCanvas;
        }

        Canvas fallbackCanvas = null;
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            Canvas rootCanvas = rootObjects[index].GetComponent<Canvas>();

            if (rootCanvas == null)
                continue;

            if (string.Equals(rootObjects[index].name, "CanvasMain", StringComparison.Ordinal))
                return rootCanvas;

            if (fallbackCanvas == null)
                fallbackCanvas = rootCanvas;
        }

        return fallbackCanvas;
    }
    #endregion

    #region HUD
    /// <summary>
    /// Ensures the gameplay UI scene owns the authored HUD manager root and removes duplicate gameplay copies.
    /// /params gameplayScene Scene currently holding gameplay simulation content.
    /// /params gameplayUiScene Scene that should own gameplay UI roots.
    /// /returns None.
    /// </summary>
    private static void EnsureGameplayUiHudManager(Scene gameplayScene, Scene gameplayUiScene)
    {
        HUDManager uiSceneHudManager = FindFirstComponentInScene<HUDManager>(gameplayUiScene);
        HUDManager gameplaySceneHudManager = FindFirstComponentInScene<HUDManager>(gameplayScene);

        if (uiSceneHudManager == null && gameplaySceneHudManager != null)
        {
            SceneManager.MoveGameObjectToScene(gameplaySceneHudManager.transform.root.gameObject, gameplayUiScene);
            return;
        }

        if (uiSceneHudManager != null && gameplaySceneHudManager != null)
            UnityEngine.Object.DestroyImmediate(gameplaySceneHudManager.transform.root.gameObject);
    }
    #endregion

    #region Camera References
    /// <summary>
    /// Ensures the additive UI camera can rebuild URP camera stacking at runtime without cross-scene references.
    /// /params gameplayUiScene Scene that owns gameplay UI roots.
    /// /returns None.
    /// </summary>
    private static void EnsureGameplayUiCameraStackBridge(Scene gameplayUiScene)
    {
        Camera uiCamera = FindCameraByName(gameplayUiScene, "UI Camera");

        if (uiCamera == null)
            return;

        ConfigureGameplayUiCamera(uiCamera);

        GameSceneUiCameraStackBridge bridge = EnsureComponent<GameSceneUiCameraStackBridge>(uiCamera.gameObject);
        SerializedObject serializedBridge = new SerializedObject(bridge);
        serializedBridge.Update();
        SetObjectReference(serializedBridge, "uiCamera", uiCamera);
        SetString(serializedBridge, "baseCameraTag", "MainCamera");
        SetBool(serializedBridge, "removeFromStackOnDisable", true);
        serializedBridge.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bridge);
    }

    /// <summary>
    /// Configures the additive UI camera as a post-process-free URP overlay camera.
    /// /params uiCamera Camera owned by the gameplay UI scene.
    /// /returns None.
    /// </summary>
    private static void ConfigureGameplayUiCamera(Camera uiCamera)
    {
        // UI cameras must be overlays and must not contribute post-processing to the base stack.
        UniversalAdditionalCameraData uiCameraData = EnsureComponent<UniversalAdditionalCameraData>(uiCamera.gameObject);
        uiCameraData.renderType = CameraRenderType.Overlay;
        uiCameraData.renderPostProcessing = false;

        // Keep the setup aligned with the shared camera layer contract when the UI layer exists.
        if (GameSceneCameraLayerUtility.TryResolveLayerMask(GameSceneCameraLayerUtility.UiLayerName, out int uiLayerMask))
            uiCamera.cullingMask = uiLayerMask;

        // Persist both camera and URP metadata changes in the edited scene.
        EditorUtility.SetDirty(uiCamera);
        EditorUtility.SetDirty(uiCameraData);
    }

    /// <summary>
    /// Restores screen-space camera canvas references after UI roots have moved into the companion scene.
    /// /params gameplayUiScene Scene that owns gameplay UI roots.
    /// /returns None.
    /// </summary>
    private static void EnsureGameplayUiCameraReferences(Scene gameplayUiScene)
    {
        Camera uiCamera = FindCameraByName(gameplayUiScene, "UI Camera");

        if (uiCamera == null)
            return;

        System.Collections.Generic.List<Canvas> canvases = FindComponentsInScene<Canvas>(gameplayUiScene);

        for (int index = 0; index < canvases.Count; index++)
        {
            Canvas canvas = canvases[index];

            if (canvas == null)
                continue;

            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
                continue;

            if (canvas.worldCamera != null)
                continue;

            canvas.worldCamera = uiCamera;
            EditorUtility.SetDirty(canvas);
        }
    }

    /// <summary>
    /// Finds one camera by GameObject name inside an opened scene.
    /// /params scene Scene searched for the camera.
    /// /params cameraName Exact GameObject name expected for the camera.
    /// /returns Matching camera or null when missing.
    /// </summary>
    private static Camera FindCameraByName(Scene scene, string cameraName)
    {
        System.Collections.Generic.List<Camera> cameras = FindComponentsInScene<Camera>(scene);

        for (int index = 0; index < cameras.Count; index++)
        {
            Camera camera = cameras[index];

            if (camera == null)
                continue;

            if (string.Equals(camera.gameObject.name, cameraName, StringComparison.Ordinal))
                return camera;
        }

        return null;
    }
    #endregion

    #region Camera Stack Cleanup
    /// <summary>
    /// Removes null or cross-scene entries from URP camera stacks after the UI camera has been separated.
    /// /params scene Scene whose camera stack references should be normalized before saving.
    /// /returns None.
    /// </summary>
    private static void CleanCameraStacks(Scene scene)
    {
        System.Collections.Generic.List<UniversalAdditionalCameraData> cameraDataList = FindComponentsInScene<UniversalAdditionalCameraData>(scene);

        for (int dataIndex = 0; dataIndex < cameraDataList.Count; dataIndex++)
        {
            UniversalAdditionalCameraData cameraData = cameraDataList[dataIndex];

            if (cameraData == null)
                continue;

            if (cameraData.renderType != CameraRenderType.Base)
                continue;

            System.Collections.Generic.List<Camera> cameraStack = cameraData.cameraStack;

            if (cameraStack == null)
                continue;

            for (int stackIndex = cameraStack.Count - 1; stackIndex >= 0; stackIndex--)
            {
                Camera stackedCamera = cameraStack[stackIndex];

                if (stackedCamera == null || stackedCamera.gameObject.scene != scene)
                    cameraStack.RemoveAt(stackIndex);
            }

            EditorUtility.SetDirty(cameraData);
        }
    }
    #endregion

    #region Event System
    /// <summary>
    /// Ensures the gameplay UI scene owns one EventSystem and removes the gameplay-scene duplicate.
    /// /params gameplayScene Scene currently holding gameplay simulation content.
    /// /params gameplayUiScene Scene that should own gameplay UI roots.
    /// /returns None.
    /// </summary>
    private static void EnsureGameplayUiEventSystem(Scene gameplayScene, Scene gameplayUiScene)
    {
        EventSystem uiEventSystem = FindFirstComponentInScene<EventSystem>(gameplayUiScene);
        EventSystem gameplayEventSystem = FindFirstComponentInScene<EventSystem>(gameplayScene);

        if (uiEventSystem == null && gameplayEventSystem != null)
        {
            SceneManager.MoveGameObjectToScene(gameplayEventSystem.transform.root.gameObject, gameplayUiScene);
            EnsureEventSystemCoordinator(gameplayEventSystem);
            return;
        }

        if (uiEventSystem == null)
        {
            CreateGameplayUiEventSystem(gameplayUiScene);
            return;
        }

        EnsureEventSystemCoordinator(uiEventSystem);

        if (gameplayEventSystem != null)
            UnityEngine.Object.DestroyImmediate(gameplayEventSystem.transform.root.gameObject);
    }

    /// <summary>
    /// Creates an Input System backed EventSystem when the migrated UI scene had none.
    /// /params gameplayUiScene Scene receiving the generated EventSystem.
    /// /returns None.
    /// </summary>
    private static void CreateGameplayUiEventSystem(Scene gameplayUiScene)
    {
        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
        EnsureEventSystemCoordinator(eventSystemObject.GetComponent<EventSystem>());
        SceneManager.MoveGameObjectToScene(eventSystemObject, gameplayUiScene);
    }

    /// <summary>
    /// Ensures one EventSystem has the additive-transition coordinator attached and wired.
    /// /params eventSystem EventSystem that should be coordinated.
    /// /returns None.
    /// </summary>
    private static void EnsureEventSystemCoordinator(EventSystem eventSystem)
    {
        if (eventSystem == null)
            return;

        GameSceneEventSystemCoordinator coordinator = EnsureComponent<GameSceneEventSystemCoordinator>(eventSystem.gameObject);
        SerializedObject serializedCoordinator = new SerializedObject(coordinator);
        serializedCoordinator.Update();
        SetObjectReference(serializedCoordinator, "eventSystem", eventSystem);
        serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(coordinator);
    }
    #endregion

    #endregion
}
