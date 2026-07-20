using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Authors the persistent fade and player-only overlay cameras required by procedural room transition presentation.
/// </summary>
internal static class GameSceneManagementProjectSetupProceduralTransitionUtility
{
    #region Constants
    private const string FadeCameraObjectName = "Camera_SceneTransitionFade";
    private const string PlayerCameraObjectName = "Camera_PlayerTransition";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the dedicated player transition Unity layer exists before authored cameras resolve their masks.
    /// </summary>
    public static void EnsureProjectLayer()
    {
        GameSceneTransitionLayerUtility.TryCreateLayer(GameSceneCameraLayerUtility.FadeTransitionLayerName);
        GameSceneTransitionLayerUtility.TryCreateLayer(GameSceneCameraLayerUtility.PlayerTransitionLayerName);
    }

    /// <summary>
    /// Extends existing Outline Render Objects filters so the isolated player retains its authored silhouette.
    /// </summary>
    public static void EnsureRendererFeatureLayers()
    {
        int outlineLayerIndex = LayerMask.NameToLayer(GameSceneCameraLayerUtility.OutlineLayerName);
        int playerLayerIndex = LayerMask.NameToLayer(GameSceneCameraLayerUtility.PlayerTransitionLayerName);

        if (outlineLayerIndex < 0 || playerLayerIndex < 0)
            return;

        string[] rendererDataGuids = AssetDatabase.FindAssets("t:ScriptableRendererData");

        for (int assetIndex = 0; assetIndex < rendererDataGuids.Length; assetIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(rendererDataGuids[assetIndex]);
            UnityEngine.Object rendererData = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (rendererData == null)
                continue;

            EnsureRendererFeatureLayers(rendererData, outlineLayerIndex, playerLayerIndex);
        }
    }

    /// <summary>
    /// Creates or updates persistent authored overlay cameras and connects them to the bootstrap fade canvas bridge.
    /// </summary>
    /// <param name="scene">Bootstrap scene that persists through managed room loads.</param>
    /// <param name="view">Authored fade canvas view.</param>
    /// <param name="canvas">Fade and loading progress canvas rendered by the transition overlay camera.</param>
    public static void EnsureBootstrapPresentation(Scene scene,
                                                   GameSceneFadeCanvasView view,
                                                   Canvas canvas)
    {
        if (!scene.IsValid() || view == null || canvas == null)
            return;

        Camera fadeCamera = EnsureOverlayCamera(scene, FadeCameraObjectName, false);
        Camera playerCamera = EnsureOverlayCamera(scene, PlayerCameraObjectName, false);
        GameProceduralTransitionCameraBridge bridge = view.GetComponent<GameProceduralTransitionCameraBridge>();

        if (bridge == null)
            bridge = view.gameObject.AddComponent<GameProceduralTransitionCameraBridge>();

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = fadeCamera;
        canvas.planeDistance = 1f;
        GameSceneCameraLayerUtility.ApplyLayerRecursively(canvas.transform,
                                                          LayerMask.NameToLayer(GameSceneCameraLayerUtility.FadeTransitionLayerName));
        SerializedObject serializedBridge = new SerializedObject(bridge);
        serializedBridge.Update();
        SetObjectReference(serializedBridge, "fadeCanvas", canvas);
        SetObjectReference(serializedBridge, "fadeCamera", fadeCamera);
        SetObjectReference(serializedBridge, "playerCamera", playerCamera);
        SetString(serializedBridge, "fadeLayerName", GameSceneCameraLayerUtility.FadeTransitionLayerName);
        SetString(serializedBridge, "playerLayerName", GameSceneCameraLayerUtility.PlayerTransitionLayerName);
        serializedBridge.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bridge);
        EditorUtility.SetDirty(canvas);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates Render Objects sub-assets whose existing filter includes the canonical Outline layer.
    /// </summary>
    /// <param name="rendererData">Renderer data asset owning serialized renderer feature references.</param>
    /// <param name="outlineLayerIndex">Canonical outline layer index used to identify relevant filters.</param>
    /// <param name="playerLayerIndex">Player transition layer index to add without replacing existing filters.</param>
    private static void EnsureRendererFeatureLayers(UnityEngine.Object rendererData,
                                                    int outlineLayerIndex,
                                                    int playerLayerIndex)
    {
        SerializedObject serializedRendererData = new SerializedObject(rendererData);
        SerializedProperty featuresProperty = serializedRendererData.FindProperty("m_RendererFeatures");

        if (featuresProperty == null || !featuresProperty.isArray)
            return;

        for (int featureIndex = 0; featureIndex < featuresProperty.arraySize; featureIndex++)
        {
            UnityEngine.Object feature = featuresProperty.GetArrayElementAtIndex(featureIndex).objectReferenceValue;

            if (feature == null)
                continue;

            SerializedObject serializedFeature = new SerializedObject(feature);
            SerializedProperty layerMaskProperty = serializedFeature.FindProperty("settings.filterSettings.LayerMask.m_Bits");

            if (layerMaskProperty == null ||
                (layerMaskProperty.intValue & (1 << outlineLayerIndex)) == 0)
            {
                continue;
            }

            int updatedMask = layerMaskProperty.intValue | (1 << playerLayerIndex);

            if (updatedMask == layerMaskProperty.intValue)
                continue;

            layerMaskProperty.intValue = updatedMask;
            serializedFeature.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
        }
    }

    /// <summary>
    /// Creates or normalizes one named bootstrap camera as a URP overlay without runtime instantiation.
    /// </summary>
    /// <param name="scene">Bootstrap scene receiving the camera root.</param>
    /// <param name="objectName">Stable authored camera object name.</param>
    /// <param name="enabled">Initial camera enabled state.</param>
    /// <returns>Authored overlay camera.</returns>
    private static Camera EnsureOverlayCamera(Scene scene, string objectName, bool enabled)
    {
        GameObject cameraObject = FindRootObject(scene, objectName);

        if (cameraObject == null)
        {
            cameraObject = new GameObject(objectName, typeof(Camera), typeof(UniversalAdditionalCameraData));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
        }

        Camera cameraComponent = cameraObject.GetComponent<Camera>();

        if (cameraComponent == null)
            cameraComponent = cameraObject.AddComponent<Camera>();

        UniversalAdditionalCameraData cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();

        if (cameraData == null)
            cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();

        cameraObject.tag = "Untagged";
        cameraComponent.enabled = enabled;
        cameraComponent.clearFlags = CameraClearFlags.Nothing;
        cameraComponent.cullingMask = ResolveCameraMask(objectName);
        cameraData.renderType = CameraRenderType.Overlay;
        cameraData.renderPostProcessing = false;
        EditorUtility.SetDirty(cameraComponent);
        EditorUtility.SetDirty(cameraData);
        return cameraComponent;
    }

    /// <summary>
    /// Finds one exact root object in a scene without performing a global object search.
    /// </summary>
    /// <param name="scene">Scene whose roots are inspected.</param>
    /// <param name="objectName">Exact root object name.</param>
    /// <returns>Matching root object, or null when absent.</returns>
    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            if (string.Equals(rootObjects[rootIndex].name, objectName, StringComparison.Ordinal))
                return rootObjects[rootIndex];
        }

        return null;
    }

    /// <summary>
    /// Resolves the dedicated fade or player transition culling mask from the camera role name.
    /// </summary>
    /// <param name="objectName">Stable authored camera object name.</param>
    /// <returns>Single-purpose Unity culling mask.</returns>
    private static int ResolveCameraMask(string objectName)
    {
        string layerName = string.Equals(objectName, FadeCameraObjectName, StringComparison.Ordinal)
            ? GameSceneCameraLayerUtility.FadeTransitionLayerName
            : GameSceneCameraLayerUtility.PlayerTransitionLayerName;
        int layerIndex = LayerMask.NameToLayer(layerName);
        return layerIndex >= 0 ? 1 << layerIndex : 0;
    }

    /// <summary>
    /// Writes one serialized object reference when the target field exists.
    /// </summary>
    /// <param name="serializedObject">Serialized bridge being updated.</param>
    /// <param name="propertyName">Private serialized field name.</param>
    /// <param name="value">Object reference to assign.</param>
    private static void SetObjectReference(SerializedObject serializedObject,
                                           string propertyName,
                                           UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    /// <summary>
    /// Writes one serialized string when the target field exists.
    /// </summary>
    /// <param name="serializedObject">Serialized bridge being updated.</param>
    /// <param name="propertyName">Private serialized field name.</param>
    /// <param name="value">String value to assign.</param>
    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.stringValue = value;
    }
    #endregion

    #endregion
}
