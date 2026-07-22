using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Reconnects an additive UI camera to the active URP base camera without serialized cross-scene references.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GameSceneUiCameraStackBridge : MonoBehaviour
{
    #region Fields

    #region Static Fields
    private static readonly HashSet<GameSceneUiCameraStackBridge> activeBridges = new HashSet<GameSceneUiCameraStackBridge>();
    #endregion

    #region Serialized Fields
    [Header("Camera Stack")]
    [Tooltip("Overlay camera owned by the additive UI scene. When empty, the local Camera component is used.")]
    [SerializeField] private Camera uiCamera;

    [Tooltip("Tag used to resolve the gameplay base camera that should receive the UI camera stack entry.")]
    [SerializeField] private string baseCameraTag = "MainCamera";

    [Tooltip("When enabled, this bridge removes its UI camera from the base camera stack before the UI scene unloads.")]
    [SerializeField] private bool removeFromStackOnDisable = true;
    #endregion

    #region Runtime
    private Camera currentBaseCamera;
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds every loaded UI overlay after persistent base-camera ownership changes during scene replacement.
    /// </summary>
    internal static void RefreshLoadedCameraStacks()
    {
        foreach (GameSceneUiCameraStackBridge bridge in activeBridges)
        {
            if (bridge != null && bridge.isActiveAndEnabled)
                bridge.ApplyCameraStack();
        }
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Registers scene-change callbacks and applies the camera stack once the additive UI scene is enabled.
    /// </summary>
    private void OnEnable()
    {
        if (uiCamera == null)
            uiCamera = GetComponent<Camera>();

        activeBridges.Add(this);
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        ApplyCameraStack();
    }

    /// <summary>
    /// Removes transient stack wiring and unregisters scene callbacks before the UI scene unloads.
    /// </summary>
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        activeBridges.Remove(this);

        if (removeFromStackOnDisable)
            RemoveCameraStack();
    }
    #endregion

    #region Events
    /// <summary>
    /// Re-applies stacking when the active gameplay scene changes.
    /// </summary>
    /// <param name="previousScene">Scene that was active before the change.</param>
    /// <param name="nextScene">Scene that became active.</param>
    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (GameProceduralRoomStreamingRuntimeUtility.IsOwnedManagedScene(previousScene) ||
            GameProceduralRoomStreamingRuntimeUtility.IsOwnedManagedScene(nextScene))
        {
            return;
        }

        ApplyCameraStack();
    }

    /// <summary>
    /// Re-applies stacking when a scene load may have introduced the gameplay base camera.
    /// </summary>
    /// <param name="loadedScene">Scene loaded by Unity.</param>
    /// <param name="loadMode">Mode used for the scene load.</param>
    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadMode)
    {
        if (GameProceduralRoomStreamingRuntimeUtility.IsOwnedManagedScene(loadedScene))
            return;

        ApplyCameraStack();
    }

    /// <summary>
    /// Rebinds the overlay when unloading a menu or previous owner exposes the persistent gameplay base camera.
    /// </summary>
    /// <param name="unloadedScene">Scene whose base camera may have owned this overlay.</param>
    private void HandleSceneUnloaded(Scene unloadedScene)
    {
        ApplyCameraStack();
    }
    #endregion

    #region Stack Management
    /// <summary>
    /// Adds the UI overlay camera to the resolved base camera stack when URP camera data is available.
    /// </summary>
    private void ApplyCameraStack()
    {
        if (uiCamera == null)
            return;

        Camera baseCamera = ResolveBaseCamera();

        if (baseCamera == null)
            return;

        UniversalAdditionalCameraData baseCameraData = baseCamera.GetComponent<UniversalAdditionalCameraData>();
        UniversalAdditionalCameraData uiCameraData = uiCamera.GetComponent<UniversalAdditionalCameraData>();

        if (baseCameraData == null || uiCameraData == null)
            return;

        if (currentBaseCamera != null && currentBaseCamera != baseCamera)
            GameSceneUrpCameraStackUtility.RemoveOverlayCameraFromLoadedBaseStacks(uiCamera);

        uiCameraData.renderType = CameraRenderType.Overlay;
        uiCameraData.renderPostProcessing = false;
        GameSceneUrpCameraStackUtility.AppendOverlayCamera(baseCameraData, uiCamera);

        currentBaseCamera = baseCamera;
        GameProceduralTransitionCameraBridge.RefreshStackOrder();
    }

    /// <summary>
    /// Removes the UI overlay camera from the last resolved base camera stack.
    /// </summary>
    private void RemoveCameraStack()
    {
        if (uiCamera == null)
            return;

        GameSceneUrpCameraStackUtility.RemoveOverlayCameraFromLoadedBaseStacks(uiCamera);
        currentBaseCamera = null;
    }
    #endregion

    #region Camera Resolution
    /// <summary>
    /// Resolves the gameplay base camera by tag first and then by enabled URP base camera data.
    /// </summary>
    /// <returns>Base camera that should render the UI overlay camera, or null when unavailable.</returns>
    private Camera ResolveBaseCamera()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (TryResolveSceneCamera(activeScene, true, out Camera activeTaggedCamera))
            return activeTaggedCamera;

        if (TryResolveSceneCamera(activeScene, false, out Camera activeBaseCamera))
            return activeBaseCamera;

        if (!string.IsNullOrWhiteSpace(baseCameraTag))
        {
            GameObject taggedObject = GameObject.FindGameObjectWithTag(baseCameraTag);

            if (taggedObject != null)
            {
                Camera taggedCamera = taggedObject.GetComponent<Camera>();

                if (IsValidBaseCamera(taggedCamera))
                    return taggedCamera;
            }
        }

        Camera[] cameras = Camera.allCameras;

        for (int index = 0; index < cameras.Length; index++)
        {
            Camera candidateCamera = cameras[index];

            if (IsValidBaseCamera(candidateCamera))
                return candidateCamera;
        }

        return null;
    }

    /// <summary>
    /// Resolves a valid base camera from a specific loaded scene before falling back to global tag lookup.
    /// </summary>
    /// <param name="scene">Scene whose root hierarchy should be searched.</param>
    /// <param name="requireTag">True when the camera must match the configured base camera tag.</param>
    /// <param name="resolvedCamera">Base camera found in the scene when available.</param>
    /// <returns>True when a valid camera was found.</returns>
    private bool TryResolveSceneCamera(Scene scene, bool requireTag, out Camera resolvedCamera)
    {
        resolvedCamera = null;

        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            Camera[] sceneCameras = rootObjects[rootIndex].GetComponentsInChildren<Camera>(true);

            for (int cameraIndex = 0; cameraIndex < sceneCameras.Length; cameraIndex++)
            {
                Camera candidateCamera = sceneCameras[cameraIndex];

                if (requireTag && !CameraMatchesBaseTag(candidateCamera))
                    continue;

                if (!IsValidBaseCamera(candidateCamera))
                    continue;

                resolvedCamera = candidateCamera;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether one camera carries the configured base camera tag.
    /// </summary>
    /// <param name="candidateCamera">Camera being inspected.</param>
    /// <returns>True when tag filtering is disabled or the camera has the configured tag.</returns>
    private bool CameraMatchesBaseTag(Camera candidateCamera)
    {
        if (candidateCamera == null)
            return false;

        if (string.IsNullOrWhiteSpace(baseCameraTag))
            return true;

        return candidateCamera.CompareTag(baseCameraTag);
    }

    /// <summary>
    /// Checks whether one camera can be used as the URP base camera for additive UI stacking.
    /// </summary>
    /// <param name="candidateCamera">Camera being inspected.</param>
    /// <returns>True when the camera is enabled, distinct from the UI camera and marked as a base camera.</returns>
    private bool IsValidBaseCamera(Camera candidateCamera)
    {
        if (candidateCamera == null)
            return false;

        if (candidateCamera == uiCamera)
            return false;

        if (!candidateCamera.isActiveAndEnabled)
            return false;

        UniversalAdditionalCameraData cameraData = candidateCamera.GetComponent<UniversalAdditionalCameraData>();
        return cameraData != null && cameraData.renderType == CameraRenderType.Base;
    }
    #endregion

    #endregion
}
