using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Migrates scene-owned gameplay camera rigs into one persistent bootstrap render owner and removes room duplicates.
/// </summary>
internal static class GameScenePersistentGameplayCameraSetupUtility
{
    #region Constants
    private const string MainCameraObjectName = "Main Camera";
    private const string PersistentCameraObjectName = "Persistent Gameplay Camera";
    private const string MainCameraTag = "MainCamera";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the bootstrap contains a persistent camera placeholder before the authored gameplay rig is migrated.
    /// </summary>
    /// <param name="bootstrapScene">Persistent bootstrap scene receiving or refreshing the camera owner.</param>
    public static void EnsureBootstrapPlaceholder(Scene bootstrapScene)
    {
        GameSceneBootstrapCameraView existingView = FindComponentInScene<GameSceneBootstrapCameraView>(bootstrapScene);
        Camera persistentCamera = existingView != null ? existingView.GetComponent<Camera>() : null;

        if (persistentCamera == null)
        {
            GameObject cameraObject = new GameObject(PersistentCameraObjectName, typeof(Camera), typeof(AudioListener));
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            SceneManager.MoveGameObjectToScene(cameraObject, bootstrapScene);
            persistentCamera = cameraObject.GetComponent<Camera>();
        }

        if (!IsCompleteCameraRig(persistentCamera))
            persistentCamera.depth = -1f;

        ConfigurePersistentCameraView(persistentCamera);
    }

    /// <summary>
    /// Ensures the bootstrap owns the complete gameplay camera rig, then strips every catalogued gameplay scene camera.
    /// </summary>
    /// <param name="scenePreset">Canonical scene catalog whose Gameplay definitions are migrated.</param>
    /// <param name="logToConsole">True when the completed migration should emit a concise editor log.</param>
    public static void Apply(GameSceneManagerPreset scenePreset, bool logToConsole)
    {
        if (scenePreset == null)
            throw new ArgumentNullException(nameof(scenePreset));

        List<string> gameplayScenePaths = CollectGameplayScenePaths(scenePreset);
        Scene sourceScene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayScenePath,
                                                         OpenSceneMode.Single);
        Camera sourceCamera = FindGameplayBaseCamera(sourceScene);
        Scene bootstrapScene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.BootstrapScenePath,
                                                            OpenSceneMode.Additive);
        Camera persistentCamera = EnsurePersistentCameraRig(bootstrapScene, sourceCamera);
        ConfigurePersistentCameraView(persistentCamera);
        EditorSceneManager.MarkSceneDirty(bootstrapScene);
        EditorSceneManager.SaveScene(bootstrapScene, GameSceneManagementProjectSetupUtility.BootstrapScenePath);
        int migratedSceneCount = StripGameplaySceneCameraRigs(gameplayScenePaths);
        AssetDatabase.SaveAssets();

        if (logToConsole)
            Debug.Log("[GameScenePersistentGameplayCameraSetupUtility] Persistent gameplay camera ready; stripped " +
                      migratedSceneCount +
                      " gameplay scene camera rig(s).");
    }
    #endregion

    #region Persistent Rig Methods
    /// <summary>
    /// Reuses a complete persistent rig or clones the last scene-owned authored rig into the bootstrap scene.
    /// </summary>
    /// <param name="bootstrapScene">Persistent scene receiving the gameplay camera root.</param>
    /// <param name="sourceCamera">Optional scene-owned camera used as the first migration template.</param>
    /// <returns>Persistent gameplay base camera.</returns>
    private static Camera EnsurePersistentCameraRig(Scene bootstrapScene, Camera sourceCamera)
    {
        GameSceneBootstrapCameraView existingView = FindComponentInScene<GameSceneBootstrapCameraView>(bootstrapScene);
        Camera existingCamera = existingView != null ? existingView.GetComponent<Camera>() : null;

        if (IsCompleteCameraRig(existingCamera))
            return existingCamera;

        if (sourceCamera == null || !IsCompleteCameraRig(sourceCamera))
            throw new InvalidOperationException("A complete gameplay camera rig is required for the initial persistent-camera migration.");

        if (existingView != null)
            UnityEngine.Object.DestroyImmediate(existingView.transform.root.gameObject);

        GameObject persistentRoot = UnityEngine.Object.Instantiate(sourceCamera.transform.root.gameObject);
        persistentRoot.name = PersistentCameraObjectName;
        SceneManager.MoveGameObjectToScene(persistentRoot, bootstrapScene);
        Camera persistentCamera = FindGameplayBaseCamera(persistentRoot);

        if (persistentCamera == null)
            throw new InvalidOperationException("The cloned persistent camera hierarchy contains no URP base camera.");

        return persistentCamera;
    }

    /// <summary>
    /// Configures persistent camera ownership references without recreating any runtime camera or presentation object.
    /// </summary>
    /// <param name="persistentCamera">Bootstrap base camera that owns the complete persistent rig.</param>
    private static void ConfigurePersistentCameraView(Camera persistentCamera)
    {
        persistentCamera.gameObject.name = PersistentCameraObjectName;
        persistentCamera.gameObject.tag = MainCameraTag;
        persistentCamera.clearFlags = CameraClearFlags.SolidColor;
        persistentCamera.backgroundColor = Color.black;
        Camera gameplayOverlayCamera = FindGameplayOverlayCamera(persistentCamera);
        AudioListener audioListener = persistentCamera.GetComponent<AudioListener>();

        if (audioListener == null)
            audioListener = persistentCamera.gameObject.AddComponent<AudioListener>();

        GameSceneBootstrapCameraView view = persistentCamera.GetComponent<GameSceneBootstrapCameraView>();

        if (view == null)
            view = persistentCamera.gameObject.AddComponent<GameSceneBootstrapCameraView>();

        SerializedObject serializedView = new SerializedObject(view);
        serializedView.Update();
        SetObjectReference(serializedView, "persistentCamera", persistentCamera);
        SetObjectReference(serializedView, "gameplayOverlayCamera", gameplayOverlayCamera);
        SetObjectReference(serializedView, "persistentAudioListener", audioListener);
        SetBool(serializedView, "disableWhenManagedCameraExists", true);
        SetFloat(serializedView, "persistentCameraDepth", persistentCamera.depth);
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(persistentCamera);
        EditorUtility.SetDirty(audioListener);
        EditorUtility.SetDirty(view);
    }

    /// <summary>
    /// Resolves whether a base camera owns the environment split bridge and its required child gameplay overlay.
    /// </summary>
    /// <param name="baseCamera">Camera rig candidate.</param>
    /// <returns>True when the candidate contains the complete persistent render stack.</returns>
    private static bool IsCompleteCameraRig(Camera baseCamera)
    {
        return baseCamera != null &&
               baseCamera.GetComponent<GameSceneEnvironmentPostProcessCameraStackBridge>() != null &&
               FindGameplayOverlayCamera(baseCamera) != null;
    }

    /// <summary>
    /// Finds the child URP overlay used to render gameplay layers after the environment pass.
    /// </summary>
    /// <param name="baseCamera">Base camera whose descendants are inspected.</param>
    /// <returns>First child URP overlay camera, or null when the rig is incomplete.</returns>
    private static Camera FindGameplayOverlayCamera(Camera baseCamera)
    {
        if (baseCamera == null)
            return null;

        Camera[] cameras = baseCamera.GetComponentsInChildren<Camera>(true);

        for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
        {
            Camera candidateCamera = cameras[cameraIndex];

            if (candidateCamera == baseCamera)
                continue;

            UniversalAdditionalCameraData cameraData = candidateCamera.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData != null && cameraData.renderType == CameraRenderType.Overlay)
                return candidateCamera;
        }

        return null;
    }
    #endregion

    #region Scene Migration Methods
    /// <summary>
    /// Collects unique Gameplay scene paths before scene loading can release the catalog asset.
    /// </summary>
    /// <param name="scenePreset">Scene catalog supplying gameplay scene paths.</param>
    /// <returns>Unique non-empty Gameplay scene paths in catalog order.</returns>
    private static List<string> CollectGameplayScenePaths(GameSceneManagerPreset scenePreset)
    {
        List<string> gameplayScenePaths = new List<string>();
        HashSet<string> processedPaths = new HashSet<string>(StringComparer.Ordinal);

        for (int sceneIndex = 0; sceneIndex < scenePreset.SceneDefinitions.Count; sceneIndex++)
        {
            GameSceneDefinition definition = scenePreset.SceneDefinitions[sceneIndex];

            if (definition == null ||
                definition.SceneKind != GameSceneKind.Gameplay ||
                string.IsNullOrWhiteSpace(definition.ScenePath) ||
                !processedPaths.Add(definition.ScenePath))
            {
                continue;
            }

            gameplayScenePaths.Add(definition.ScenePath);
        }

        return gameplayScenePaths;
    }

    /// <summary>
    /// Removes scene-owned main camera roots from every collected Gameplay scene.
    /// </summary>
    /// <param name="gameplayScenePaths">Unique Gameplay scene paths captured from the canonical catalog.</param>
    /// <returns>Number of scene camera roots removed.</returns>
    private static int StripGameplaySceneCameraRigs(IReadOnlyList<string> gameplayScenePaths)
    {
        int removedRigCount = 0;

        for (int sceneIndex = 0; sceneIndex < gameplayScenePaths.Count; sceneIndex++)
        {
            string gameplayScenePath = gameplayScenePaths[sceneIndex];
            Scene gameplayScene = EditorSceneManager.OpenScene(gameplayScenePath, OpenSceneMode.Single);
            int sceneRemovedCount = RemoveSceneCameraRigs(gameplayScene);

            if (sceneRemovedCount <= 0)
                continue;

            removedRigCount += sceneRemovedCount;
            EditorSceneManager.MarkSceneDirty(gameplayScene);
            EditorSceneManager.SaveScene(gameplayScene, gameplayScenePath);
        }

        return removedRigCount;
    }

    /// <summary>
    /// Collects and removes only authored gameplay base-camera roots while preserving cinematic and overlay cameras.
    /// </summary>
    /// <param name="scene">Gameplay scene whose obsolete camera ownership is removed.</param>
    /// <returns>Number of unique root objects removed.</returns>
    private static int RemoveSceneCameraRigs(Scene scene)
    {
        HashSet<GameObject> cameraRoots = new HashSet<GameObject>();
        Camera[] cameras = FindComponentsInScene<Camera>(scene);

        for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
        {
            Camera camera = cameras[cameraIndex];

            if (camera == null || !IsObsoleteGameplayBaseCamera(camera))
                continue;

            cameraRoots.Add(camera.transform.root.gameObject);
        }

        foreach (GameObject cameraRoot in cameraRoots)
            UnityEngine.Object.DestroyImmediate(cameraRoot);

        return cameraRoots.Count;
    }

    /// <summary>
    /// Identifies the old scene-owned main rig by environment bridge or exact authored Main Camera identity.
    /// </summary>
    /// <param name="camera">Scene camera candidate.</param>
    /// <returns>True when the camera is an obsolete gameplay base render owner.</returns>
    private static bool IsObsoleteGameplayBaseCamera(Camera camera)
    {
        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        bool isBaseCamera = cameraData == null || cameraData.renderType == CameraRenderType.Base;

        if (!isBaseCamera)
            return false;

        if (camera.GetComponent<GameSceneEnvironmentPostProcessCameraStackBridge>() != null)
            return true;

        return string.Equals(camera.gameObject.name, MainCameraObjectName, StringComparison.Ordinal) &&
               camera.CompareTag(MainCameraTag);
    }
    #endregion

    #region Lookup Methods
    /// <summary>
    /// Finds the preferred gameplay base camera in one scene.
    /// </summary>
    /// <param name="scene">Scene searched for the camera rig.</param>
    /// <returns>Environment bridge camera or exact Main Camera base camera.</returns>
    private static Camera FindGameplayBaseCamera(Scene scene)
    {
        GameSceneEnvironmentPostProcessCameraStackBridge bridge = FindComponentInScene<GameSceneEnvironmentPostProcessCameraStackBridge>(scene);

        if (bridge != null)
            return bridge.GetComponent<Camera>();

        Camera[] cameras = FindComponentsInScene<Camera>(scene);

        for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
        {
            Camera camera = cameras[cameraIndex];

            if (camera != null && string.Equals(camera.gameObject.name, MainCameraObjectName, StringComparison.Ordinal))
                return camera;
        }

        return null;
    }

    /// <summary>
    /// Finds the preferred gameplay base camera inside one cloned hierarchy.
    /// </summary>
    /// <param name="root">Cloned camera hierarchy root.</param>
    /// <returns>Environment bridge camera or first base camera descendant.</returns>
    private static Camera FindGameplayBaseCamera(GameObject root)
    {
        GameSceneEnvironmentPostProcessCameraStackBridge bridge = root.GetComponentInChildren<GameSceneEnvironmentPostProcessCameraStackBridge>(true);

        if (bridge != null)
            return bridge.GetComponent<Camera>();

        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);

        for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
        {
            UniversalAdditionalCameraData cameraData = cameras[cameraIndex].GetComponent<UniversalAdditionalCameraData>();

            if (cameraData == null || cameraData.renderType == CameraRenderType.Base)
                return cameras[cameraIndex];
        }

        return null;
    }

    /// <summary>
    /// Finds the first component of one type owned by a scene hierarchy.
    /// </summary>
    /// <typeparam name="T">Component type to resolve.</typeparam>
    /// <param name="scene">Scene whose roots are inspected.</param>
    /// <returns>First matching component, or null when absent.</returns>
    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            T component = rootObjects[rootIndex].GetComponentInChildren<T>(true);

            if (component != null)
                return component;
        }

        return null;
    }

    /// <summary>
    /// Collects every component of one type owned by a scene without retaining editor allocations.
    /// </summary>
    /// <typeparam name="T">Component type to collect.</typeparam>
    /// <param name="scene">Scene whose root hierarchies are inspected.</param>
    /// <returns>Flat component array.</returns>
    private static T[] FindComponentsInScene<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            components.AddRange(rootObjects[rootIndex].GetComponentsInChildren<T>(true));

        return components.ToArray();
    }
    #endregion

    #region Serialized Methods
    /// <summary>
    /// Assigns one serialized object reference when the target field exists.
    /// </summary>
    /// <param name="serializedObject">Serialized component receiving the value.</param>
    /// <param name="propertyName">Serialized field name.</param>
    /// <param name="value">Object reference to assign.</param>
    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    /// <summary>
    /// Assigns one serialized boolean when the target field exists.
    /// </summary>
    /// <param name="serializedObject">Serialized component receiving the value.</param>
    /// <param name="propertyName">Serialized field name.</param>
    /// <param name="value">Boolean value to assign.</param>
    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Assigns one serialized float when the target field exists.
    /// </summary>
    /// <param name="serializedObject">Serialized component receiving the value.</param>
    /// <param name="propertyName">Serialized field name.</param>
    /// <param name="value">Float value to assign.</param>
    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.floatValue = value;
    }
    #endregion

    #endregion
}
