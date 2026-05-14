using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static GameSceneManagementProjectSetupSceneUtility;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Creates and refreshes the optimized URP camera stack used for environment-only post-processing.
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneEnvironmentPostProcessSetupUtility
{
    #region Constants
    private const string EnvironmentPrefabFolder = "Assets/3D/3D prefabs";
    private const string EnvironmentModulesRootName = "Environment_modules";
    private const string GameplayOverlayCameraName = "Gameplay Overlay Camera";
    private const string MainCameraObjectName = "Main Camera";
    private const string MainCameraTag = "MainCamera";
    private const int FirstFallbackLayerIndex = 13;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the camera stack setup from the Unity menu after saving modified scenes.
    /// /params None.
    /// /returns None.
    /// </summary>
    //[MenuItem("Tools/Game/Rendering/Apply Environment Post Process Camera Stack")]
    public static void ExecuteSetupFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ApplyDefaultGameplaySceneSetup(true);
    }

    /// <summary>
    /// Applies the camera stack setup from Unity batch mode.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void ExecuteBatchSetup()
    {
        ApplyDefaultGameplaySceneSetup(true);
    }

    /// <summary>
    /// Applies the environment-only post-processing setup to the default gameplay scene.
    /// /params logToConsole True when setup completion should be logged.
    /// /returns None.
    /// </summary>
    public static void ApplyDefaultGameplaySceneSetup(bool logToConsole)
    {
        int environmentLayerIndex = EnsureEnvironmentLayer();
        int wallsLayerIndex = LayerMask.NameToLayer(GameSceneCameraLayerUtility.WallsLayerName);
        int uiLayerIndex = LayerMask.NameToLayer(GameSceneCameraLayerUtility.UiLayerName);
        EnsureEnvironmentPrefabLayers(environmentLayerIndex, wallsLayerIndex);

        Scene gameplayScene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayScenePath, OpenSceneMode.Single);
        EnsureSceneEnvironmentLayers(gameplayScene, environmentLayerIndex, wallsLayerIndex);
        EnsureSceneCameraStack(gameplayScene, environmentLayerIndex, wallsLayerIndex, uiLayerIndex);
        EditorSceneManager.MarkSceneDirty(gameplayScene);
        EditorSceneManager.SaveScene(gameplayScene, GameSceneManagementProjectSetupUtility.GameplayScenePath);
        AssetDatabase.SaveAssets();

        if (logToConsole)
            Debug.Log("[GameSceneEnvironmentPostProcessSetupUtility] Environment-only post-process camera stack completed.");
    }
    #endregion

    #region Layer Setup
    /// <summary>
    /// Ensures the Environment layer exists without stealing layer slots already used by gameplay assets.
    /// /params None.
    /// /returns Layer index assigned to the Environment layer.
    /// </summary>
    private static int EnsureEnvironmentLayer()
    {
        int existingLayerIndex = LayerMask.NameToLayer(GameSceneCameraLayerUtility.EnvironmentLayerName);

        if (existingLayerIndex >= 0)
            return existingLayerIndex;

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProperty = tagManager.FindProperty("layers");

        if (layersProperty == null)
            throw new InvalidOperationException("Unable to resolve TagManager layers property.");

        int preferredLayerIndex = GameSceneCameraLayerUtility.EnvironmentLayerIndex;

        if (TryAssignLayer(layersProperty, preferredLayerIndex, GameSceneCameraLayerUtility.EnvironmentLayerName))
        {
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return preferredLayerIndex;
        }

        for (int layerIndex = FirstFallbackLayerIndex; layerIndex < layersProperty.arraySize; layerIndex++)
        {
            if (!TryAssignLayer(layersProperty, layerIndex, GameSceneCameraLayerUtility.EnvironmentLayerName))
                continue;

            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.LogWarning("[GameSceneEnvironmentPostProcessSetupUtility] Environment layer was created outside the preferred slot because layer 11 was unavailable.");
            return layerIndex;
        }

        throw new InvalidOperationException("No available Unity layer slot for Environment.");
    }

    /// <summary>
    /// Writes one layer name only when the slot is empty.
    /// /params layersProperty Serialized TagManager layers array.
    /// /params layerIndex Layer index to write.
    /// /params layerName Layer name assigned to the slot.
    /// /returns True when the layer was assigned.
    /// </summary>
    private static bool TryAssignLayer(SerializedProperty layersProperty, int layerIndex, string layerName)
    {
        if (layerIndex < 0 || layerIndex >= layersProperty.arraySize)
            return false;

        SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(layerIndex);

        if (!string.IsNullOrWhiteSpace(layerProperty.stringValue))
            return false;

        layerProperty.stringValue = layerName;
        return true;
    }
    #endregion

    #region Environment Layers
    /// <summary>
    /// Applies environment and wall layers to reusable environment prefabs.
    /// /params environmentLayerIndex Layer index used by non-blocking environment visuals.
    /// /params wallsLayerIndex Layer index used by blocking wall-like environment pieces.
    /// /returns None.
    /// </summary>
    private static void EnsureEnvironmentPrefabLayers(int environmentLayerIndex, int wallsLayerIndex)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EnvironmentPrefabFolder });

        for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);

            if (string.IsNullOrWhiteSpace(prefabPath))
                continue;

            if (!IsEnvironmentPrefabPath(prefabPath))
                continue;

            ApplyPrefabEnvironmentLayer(prefabPath, environmentLayerIndex, wallsLayerIndex);
        }
    }

    /// <summary>
    /// Filters reusable environment prefabs and excludes utility/transition prefabs that only share the folder.
    /// /params prefabPath Project-relative prefab path to inspect.
    /// /returns True when the prefab should inherit environment camera routing by default.
    /// </summary>
    private static bool IsEnvironmentPrefabPath(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath))
            return false;

        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);

        if (prefabPath.IndexOf("/Prefabs Decals/", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        switch (prefabName)
        {
            case "PF_Column":
            case "PF_Floor_A":
            case "PF_Floor_A_Railway":
            case "PF_Gate_Small":
            case "PF_Grate_LevelExit":
            case "PF_Railway_Rails_A":
            case "PF_Railway_Rails_B":
            case "PF_Stairs":
            case "PF_Train":
            case "PF_Tunnel_LevelExit":
            case "PF_Tunnel_Railway":
            case "PF_Wall_A":
            case "PF_Wall_A_Railway":
            case "PF_Wall_B":
                return true;
        }

        return false;
    }

    /// <summary>
    /// Applies environment routing layers to one prefab and saves it only when a layer changed.
    /// /params prefabPath Project-relative prefab path.
    /// /params environmentLayerIndex Layer index used by non-blocking environment visuals.
    /// /params wallsLayerIndex Layer index used by blocking wall-like environment pieces.
    /// /returns None.
    /// </summary>
    private static void ApplyPrefabEnvironmentLayer(string prefabPath, int environmentLayerIndex, int wallsLayerIndex)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = ApplyEnvironmentLayerRecursively(prefabRoot.transform, wallsLayerIndex, ResolveEnvironmentTargetLayer(prefabRoot.name, environmentLayerIndex, wallsLayerIndex));

        if (changed)
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    /// <summary>
    /// Applies environment and wall layers to the authored environment root in a scene.
    /// /params scene Scene whose environment hierarchy should be routed.
    /// /params environmentLayerIndex Layer index used by non-blocking environment visuals.
    /// /params wallsLayerIndex Layer index used by blocking wall-like environment pieces.
    /// /returns None.
    /// </summary>
    private static void EnsureSceneEnvironmentLayers(Scene scene, int environmentLayerIndex, int wallsLayerIndex)
    {
        GameObject environmentRoot = FindRootObject(scene, EnvironmentModulesRootName);

        if (environmentRoot == null)
        {
            Debug.LogWarning("[GameSceneEnvironmentPostProcessSetupUtility] Environment root '" + EnvironmentModulesRootName + "' was not found in " + scene.path + ".");
            return;
        }

        bool changed = ApplyEnvironmentLayerRecursively(environmentRoot.transform, wallsLayerIndex, environmentLayerIndex);

        if (changed)
            EditorUtility.SetDirty(environmentRoot);
    }

    /// <summary>
    /// Applies the inherited environment layer to one transform hierarchy while preserving wall-like branches.
    /// /params targetTransform Root transform being processed.
    /// /params environmentLayerIndex Layer index used by non-blocking environment visuals.
    /// /params wallsLayerIndex Layer index used by blocking wall-like environment pieces.
    /// /params inheritedLayer Layer inherited from the parent branch.
    /// /returns True when at least one GameObject layer changed.
    /// </summary>
    private static bool ApplyEnvironmentLayerRecursively(Transform targetTransform, int wallsLayerIndex, int inheritedLayer)
    {
        if (targetTransform == null)
            return false;

        int targetLayer = ResolveEnvironmentTargetLayer(targetTransform.gameObject.name, inheritedLayer, wallsLayerIndex);
        bool changed = false;

        if (targetTransform.gameObject.layer != targetLayer)
        {
            targetTransform.gameObject.layer = targetLayer;
            changed = true;
        }

        for (int childIndex = 0; childIndex < targetTransform.childCount; childIndex++)
        {
            Transform childTransform = targetTransform.GetChild(childIndex);

            if (ApplyEnvironmentLayerRecursively(childTransform, wallsLayerIndex, targetLayer))
                changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Resolves the layer that should be applied to one environment object branch.
    /// /params objectName GameObject or prefab root name.
    /// /params fallbackLayer Layer inherited from the parent branch.
    /// /params wallsLayerIndex Layer index used by wall-like blockers.
    /// /returns Target layer index for this branch.
    /// </summary>
    private static int ResolveEnvironmentTargetLayer(string objectName, int fallbackLayer, int wallsLayerIndex)
    {
        if (wallsLayerIndex < 0)
            return fallbackLayer;

        if (IsWallLikeEnvironmentName(objectName))
            return wallsLayerIndex;

        return fallbackLayer;
    }

    /// <summary>
    /// Resolves whether an environment branch should keep wall collision/render routing.
    /// /params objectName GameObject or prefab name to inspect.
    /// /returns True when the name represents static blocking environment.
    /// </summary>
    private static bool IsWallLikeEnvironmentName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        return objectName.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Gate", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Tunnel", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Column", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Train", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #region Camera Stack Setup
    /// <summary>
    /// Ensures the gameplay scene owns a base environment camera and a child gameplay overlay camera.
    /// /params scene Gameplay scene to configure.
    /// /params environmentLayerIndex Layer index used by the base camera environment pass.
    /// /params wallsLayerIndex Layer index used by wall-like environment objects.
    /// /params uiLayerIndex Layer index used by UI.
    /// /returns None.
    /// </summary>
    private static void EnsureSceneCameraStack(Scene scene, int environmentLayerIndex, int wallsLayerIndex, int uiLayerIndex)
    {
        Camera baseCamera = FindGameplayBaseCamera(scene);

        if (baseCamera == null)
            throw new InvalidOperationException("Gameplay base camera was not found in " + scene.path + ".");

        Camera gameplayCamera = EnsureGameplayOverlayCamera(baseCamera);
        UniversalAdditionalCameraData baseCameraData = EnsureComponent<UniversalAdditionalCameraData>(baseCamera.gameObject);
        UniversalAdditionalCameraData gameplayCameraData = EnsureComponent<UniversalAdditionalCameraData>(gameplayCamera.gameObject);
        int environmentMask = BuildEnvironmentMask(environmentLayerIndex, wallsLayerIndex);
        int gameplayExcludedMask = BuildLayerMask(uiLayerIndex);
        int gameplayMask = GameSceneCameraLayerUtility.BuildGameplayCullingMask(environmentMask, gameplayExcludedMask);

        ConfigureBaseCamera(baseCamera, baseCameraData, environmentMask);
        ConfigureGameplayCamera(baseCamera, gameplayCamera, gameplayCameraData, gameplayMask);
        ConfigureEnvironmentStackBridge(baseCamera, gameplayCamera, environmentMask, gameplayMask, gameplayExcludedMask);
        ConfigureRuntimeGizmoTarget(baseCamera, gameplayCamera);
        InsertGameplayCamera(baseCameraData, gameplayCamera);
    }

    /// <summary>
    /// Resolves the gameplay base camera by tag and name before using the first base camera in the scene.
    /// /params scene Scene searched for the camera.
    /// /returns Gameplay base camera when available.
    /// </summary>
    private static Camera FindGameplayBaseCamera(Scene scene)
    {
        Camera namedCamera = FindCameraByName(scene, MainCameraObjectName);

        if (namedCamera != null)
            return namedCamera;

        System.Collections.Generic.List<Camera> cameras = FindComponentsInScene<Camera>(scene);

        for (int index = 0; index < cameras.Count; index++)
        {
            Camera camera = cameras[index];

            if (camera == null || !camera.CompareTag(MainCameraTag))
                continue;

            return camera;
        }

        for (int index = 0; index < cameras.Count; index++)
        {
            Camera camera = cameras[index];

            if (camera == null)
                continue;

            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData == null || cameraData.renderType == CameraRenderType.Base)
                return camera;
        }

        return null;
    }

    /// <summary>
    /// Creates or resolves the child gameplay overlay camera under the base camera.
    /// /params baseCamera Base camera that owns the child overlay camera.
    /// /returns Gameplay overlay camera.
    /// </summary>
    private static Camera EnsureGameplayOverlayCamera(Camera baseCamera)
    {
        Transform existingTransform = baseCamera.transform.Find(GameplayOverlayCameraName);

        if (existingTransform != null)
        {
            Camera existingCamera = existingTransform.GetComponent<Camera>();

            if (existingCamera != null)
                return existingCamera;
        }

        GameObject cameraObject = new GameObject(GameplayOverlayCameraName, typeof(Camera), typeof(UniversalAdditionalCameraData));
        SceneManager.MoveGameObjectToScene(cameraObject, baseCamera.gameObject.scene);
        cameraObject.transform.SetParent(baseCamera.transform, false);
        cameraObject.transform.localPosition = Vector3.zero;
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.transform.localScale = Vector3.one;
        return cameraObject.GetComponent<Camera>();
    }

    /// <summary>
    /// Configures the existing base camera as the environment post-process pass.
    /// /params baseCamera Camera that renders environment layers.
    /// /params baseCameraData URP data paired with the base camera.
    /// /params environmentMask Environment culling mask.
    /// /returns None.
    /// </summary>
    private static void ConfigureBaseCamera(Camera baseCamera, UniversalAdditionalCameraData baseCameraData, int environmentMask)
    {
        baseCamera.cullingMask = environmentMask;
        baseCameraData.renderType = CameraRenderType.Base;
        baseCameraData.renderPostProcessing = true;
        EditorUtility.SetDirty(baseCamera);
        EditorUtility.SetDirty(baseCameraData);
    }

    /// <summary>
    /// Configures the child overlay camera as the gameplay pass.
    /// /params baseCamera Base camera whose projection settings are mirrored.
    /// /params gameplayCamera Overlay camera rendered after the environment post-process pass.
    /// /params gameplayCameraData URP data paired with the overlay camera.
    /// /params gameplayMask Gameplay culling mask.
    /// /returns None.
    /// </summary>
    private static void ConfigureGameplayCamera(Camera baseCamera,
                                                Camera gameplayCamera,
                                                UniversalAdditionalCameraData gameplayCameraData,
                                                int gameplayMask)
    {
        CopyCameraSettings(baseCamera, gameplayCamera);
        gameplayCamera.clearFlags = CameraClearFlags.Nothing;
        gameplayCamera.cullingMask = gameplayMask;
        gameplayCamera.depth = baseCamera.depth + 0.01f;
        gameplayCameraData.renderType = CameraRenderType.Overlay;
        gameplayCameraData.renderPostProcessing = false;
        ConfigureOverlayDepthPreservation(gameplayCameraData, false);
        EditorUtility.SetDirty(gameplayCamera);
        EditorUtility.SetDirty(gameplayCameraData);
    }

    /// <summary>
    /// Writes URP overlay clear-depth data through serialization because Unity exposes it as read-only at runtime.
    /// /params gameplayCameraData URP data paired with the overlay camera.
    /// /params clearDepth True when the overlay should clear depth before rendering.
    /// /returns None.
    /// </summary>
    private static void ConfigureOverlayDepthPreservation(UniversalAdditionalCameraData gameplayCameraData, bool clearDepth)
    {
        SerializedObject serializedCameraData = new SerializedObject(gameplayCameraData);
        serializedCameraData.Update();
        SetBool(serializedCameraData, "m_ClearDepth", clearDepth);
        serializedCameraData.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Mirrors stable projection settings from the base camera to the child gameplay overlay camera.
    /// /params sourceCamera Base camera used by gameplay follow systems.
    /// /params targetCamera Gameplay overlay camera receiving projection settings.
    /// /returns None.
    /// </summary>
    private static void CopyCameraSettings(Camera sourceCamera, Camera targetCamera)
    {
        targetCamera.nearClipPlane = sourceCamera.nearClipPlane;
        targetCamera.farClipPlane = sourceCamera.farClipPlane;
        targetCamera.fieldOfView = sourceCamera.fieldOfView;
        targetCamera.orthographic = sourceCamera.orthographic;
        targetCamera.orthographicSize = sourceCamera.orthographicSize;
        targetCamera.allowHDR = sourceCamera.allowHDR;
        targetCamera.allowMSAA = sourceCamera.allowMSAA;
        targetCamera.useOcclusionCulling = sourceCamera.useOcclusionCulling;
    }

    /// <summary>
    /// Writes bridge references and masks so runtime scene transitions rebuild the stack without manual hooks.
    /// /params baseCamera Camera that owns the bridge.
    /// /params gameplayCamera Gameplay overlay camera assigned to the bridge.
    /// /params environmentMask Environment culling mask.
    /// /params gameplayMask Explicit gameplay mask used as fallback/debug data.
    /// /params gameplayExcludedMask Extra excluded layers used by derived routing.
    /// /returns None.
    /// </summary>
    private static void ConfigureEnvironmentStackBridge(Camera baseCamera,
                                                       Camera gameplayCamera,
                                                       int environmentMask,
                                                       int gameplayMask,
                                                       int gameplayExcludedMask)
    {
        GameSceneEnvironmentPostProcessCameraStackBridge bridge = EnsureComponent<GameSceneEnvironmentPostProcessCameraStackBridge>(baseCamera.gameObject);
        SerializedObject serializedBridge = new SerializedObject(bridge);
        serializedBridge.Update();
        SetObjectReference(serializedBridge, "baseCamera", baseCamera);
        SetObjectReference(serializedBridge, "gameplayCamera", gameplayCamera);
        SetInt(serializedBridge, "environmentCullingMask", environmentMask);
        SetBool(serializedBridge, "deriveGameplayCullingMask", true);
        SetInt(serializedBridge, "gameplayCullingMask", gameplayMask);
        SetInt(serializedBridge, "additionalGameplayExcludedLayers", gameplayExcludedMask);
        SetBool(serializedBridge, "enableEnvironmentPostProcessing", true);
        SetBool(serializedBridge, "disableGameplayPostProcessing", true);
        SetBool(serializedBridge, "preserveEnvironmentDepth", true);
        SetBool(serializedBridge, "reapplyOnSceneChanges", true);
        SetBool(serializedBridge, "removeGameplayCameraFromStackOnDisable", true);
        serializedBridge.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bridge);
    }

    /// <summary>
    /// Retargets game-view ECS debug gizmos to the gameplay overlay so they render after gameplay geometry and before UI.
    /// /params baseCamera Base camera currently hosting the gizmo renderer.
    /// /params gameplayCamera Gameplay overlay camera used for final gameplay projection.
    /// /returns None.
    /// </summary>
    private static void ConfigureRuntimeGizmoTarget(Camera baseCamera, Camera gameplayCamera)
    {
        RuntimeEntityGizmoGameViewRenderer gizmoRenderer = baseCamera.GetComponent<RuntimeEntityGizmoGameViewRenderer>();

        if (gizmoRenderer == null)
            return;

        SerializedObject serializedRenderer = new SerializedObject(gizmoRenderer);
        serializedRenderer.Update();
        SetObjectReference(serializedRenderer, "targetCamera", gameplayCamera);
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gizmoRenderer);
    }

    /// <summary>
    /// Inserts the gameplay overlay before additive UI overlays in the base camera stack.
    /// /params baseCameraData URP base camera data.
    /// /params gameplayCamera Gameplay overlay camera.
    /// /returns None.
    /// </summary>
    private static void InsertGameplayCamera(UniversalAdditionalCameraData baseCameraData, Camera gameplayCamera)
    {
        GameSceneUrpCameraStackUtility.InsertOverlayCamera(baseCameraData, gameplayCamera, 0);
        EditorUtility.SetDirty(baseCameraData);
    }
    #endregion

    #region Lookup Helpers
    /// <summary>
    /// Finds one root object by exact name in an opened scene.
    /// /params scene Scene searched for the root object.
    /// /params rootName Exact root object name.
    /// /returns Matching root object or null.
    /// </summary>
    private static GameObject FindRootObject(Scene scene, string rootName)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            GameObject rootObject = rootObjects[index];

            if (string.Equals(rootObject.name, rootName, StringComparison.Ordinal))
                return rootObject;
        }

        return null;
    }

    /// <summary>
    /// Finds one camera by exact GameObject name in an opened scene.
    /// /params scene Scene searched for the camera.
    /// /params cameraName Exact camera GameObject name.
    /// /returns Matching camera or null.
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

    /// <summary>
    /// Builds an environment mask from environment and walls layers.
    /// /params environmentLayerIndex Environment layer index.
    /// /params wallsLayerIndex Walls layer index.
    /// /returns Culling mask for the base environment camera.
    /// </summary>
    private static int BuildEnvironmentMask(int environmentLayerIndex, int wallsLayerIndex)
    {
        return BuildLayerMask(environmentLayerIndex) | BuildLayerMask(wallsLayerIndex);
    }

    /// <summary>
    /// Builds a single-layer mask while tolerating missing optional layers.
    /// /params layerIndex Unity layer index.
    /// /returns Single-layer mask or 0 when the layer index is invalid.
    /// </summary>
    private static int BuildLayerMask(int layerIndex)
    {
        if (layerIndex < 0)
            return 0;

        return 1 << layerIndex;
    }
    #endregion

    #endregion
}
