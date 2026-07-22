using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps authored transition and player-only overlay cameras ordered above loaded gameplay and UI camera passes.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameProceduralTransitionCameraBridge : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("References")]
    [Tooltip("Authored screen-space camera canvas containing the fade surface and loading progress presentation.")]
    [SerializeField]
    private Canvas fadeCanvas;

    [Tooltip("URP overlay camera rendering the fade canvas after gameplay and additive UI passes.")]
    [SerializeField]
    private Camera fadeCamera;

    [Tooltip("URP overlay camera rendering only the temporarily isolated persistent player above the fade.")]
    [SerializeField]
    private Camera playerCamera;

    [Header("Layers")]
    [Tooltip("Unity layer containing the transition canvas rendered by the authored fade camera.")]
    [SerializeField]
    private string fadeLayerName = GameSceneCameraLayerUtility.FadeTransitionLayerName;

    [Tooltip("Unity layer temporarily assigned to persistent player renderers during an intra-level transition.")]
    [SerializeField]
    private string playerLayerName = GameSceneCameraLayerUtility.PlayerTransitionLayerName;
    #endregion

    #region Static Fields
    private static GameProceduralTransitionCameraBridge activeBridge;
    #endregion

    #region Runtime Fields
    private readonly Dictionary<Camera, int> originalCameraMasks = new Dictionary<Camera, int>();
    private Camera activeBaseCamera;
    private CameraRenderSnapshot playerCameraSnapshot;
    private Transform playerTrackingTransform;
    private Vector3 playerTrackingStartPosition;
    private bool fadePresentationVisible;
    private bool hasPlayerCameraSnapshot;
    private bool hasPlayerTrackingStartPosition;
    private bool playerPresentationVisible;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Gets the configured player-only Unity layer index used by managed renderer isolation.
    /// </summary>
    public static int PlayerLayerIndex
    {
        get
        {
            if (activeBridge == null)
                return -1;

            return LayerMask.NameToLayer(activeBridge.playerLayerName);
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Enables the authored fade pass only while the ECS fade presentation is visible.
    /// </summary>
    /// <param name="visible">True while the fade surface can contribute to the rendered frame.</param>
    public static void SetFadePresentationVisible(bool visible)
    {
        if (activeBridge == null)
            return;

        if (activeBridge.fadePresentationVisible == visible &&
            (activeBridge.fadeCamera == null || activeBridge.fadeCamera.enabled == visible))
        {
            return;
        }

        activeBridge.fadePresentationVisible = visible;

        if (activeBridge.fadeCamera != null)
            activeBridge.fadeCamera.enabled = visible;

        activeBridge.ApplyCachedPresentationState();
    }

    /// <summary>
    /// Enables or disables the authored player-only overlay and normalizes camera stack order immediately.
    /// </summary>
    /// <param name="visible">True while an intra-level transition should keep player presentation above black.</param>
    public static void SetPlayerPresentationVisible(bool visible)
    {
        if (activeBridge == null)
            return;

        if (activeBridge.playerPresentationVisible == visible &&
            (activeBridge.playerCamera == null || activeBridge.playerCamera.enabled == visible))
        {
            return;
        }

        if (visible && !activeBridge.playerPresentationVisible)
        {
            activeBridge.hasPlayerCameraSnapshot = false;
            activeBridge.playerTrackingTransform = null;
            activeBridge.hasPlayerTrackingStartPosition = false;
        }

        activeBridge.playerPresentationVisible = visible;

        if (activeBridge.playerCamera != null)
            activeBridge.playerCamera.enabled = visible;

        activeBridge.ApplyCachedPresentationState();

        if (!visible)
        {
            activeBridge.hasPlayerCameraSnapshot = false;
            activeBridge.playerTrackingTransform = null;
            activeBridge.hasPlayerTrackingStartPosition = false;
        }
    }

    /// <summary>
    /// Captures the persistent player transform used to translate the camera snapshot by the exact room-arrival delta.
    /// </summary>
    /// <param name="playerTransform">Managed persistent player transform synchronized from the ECS LocalTransform.</param>
    public static void SetPlayerTrackingTransform(Transform playerTransform)
    {
        if (activeBridge == null || !activeBridge.playerPresentationVisible)
            return;

        activeBridge.playerTrackingTransform = playerTransform;
        activeBridge.hasPlayerTrackingStartPosition = playerTransform != null;

        if (playerTransform != null)
            activeBridge.playerTrackingStartPosition = playerTransform.position;
    }

    /// <summary>
    /// Reapplies overlay order after another scene bridge changes the active base camera stack.
    /// </summary>
    public static void RefreshStackOrder()
    {
        if (activeBridge != null)
            activeBridge.RefreshCameraStack();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Registers the authored bridge, configures both overlays and subscribes to additive scene changes.
    /// </summary>
    private void OnEnable()
    {
        activeBridge = this;
        ConfigureAuthoredCameras();
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        RefreshCameraStack();
    }

    /// <summary>
    /// Removes event handlers and stale stack entries when the persistent bootstrap bridge is disabled.
    /// </summary>
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;

        if (fadeCamera != null)
            GameSceneUrpCameraStackUtility.RemoveOverlayCameraFromLoadedBaseStacks(fadeCamera);

        if (playerCamera != null)
            GameSceneUrpCameraStackUtility.RemoveOverlayCameraFromLoadedBaseStacks(playerCamera);

        RestoreCameraMasks();

        if (activeBridge == this)
            activeBridge = null;
    }

    /// <summary>
    /// Synchronizes the player-only camera with the active gameplay camera only while its transition pass is visible.
    /// </summary>
    private void LateUpdate()
    {
        if (!playerPresentationVisible || playerCamera == null || activeBaseCamera == null)
            return;

        if (hasPlayerCameraSnapshot)
            playerCameraSnapshot.Apply(playerCamera, ResolvePlayerTrackingOffset());
        else
            SynchronizePlayerCamera(activeBaseCamera, playerCamera);
    }
    #endregion

    #region Scene Events
    /// <summary>
    /// Refreshes stack ownership when Unity changes the active additive scene.
    /// </summary>
    /// <param name="previousScene">Previously active scene.</param>
    /// <param name="nextScene">New active scene.</param>
    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (GameProceduralRoomStreamingRuntimeUtility.IsOwnedManagedScene(previousScene) ||
            GameProceduralRoomStreamingRuntimeUtility.IsOwnedManagedScene(nextScene))
        {
            return;
        }

        RefreshCameraStack();
    }

    /// <summary>
    /// Refreshes stack ownership after a target or companion scene is loaded.
    /// </summary>
    /// <param name="loadedScene">Newly loaded scene.</param>
    /// <param name="loadMode">Load mode used by Scene Management.</param>
    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadMode)
    {
        if (GameProceduralRoomStreamingRuntimeUtility.IsOwnedManagedScene(loadedScene))
            return;

        RefreshCameraStack();
    }

    /// <summary>
    /// Refreshes stack ownership after a previous room releases its base camera.
    /// </summary>
    /// <param name="unloadedScene">Scene removed from the loaded set.</param>
    private void HandleSceneUnloaded(Scene unloadedScene)
    {
        if (GameProceduralRoomStreamingRuntimeUtility.IsOwnedManagedScene(unloadedScene))
            return;

        RefreshCameraStack();
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Applies URP overlay metadata, culling layers and screen-space camera ownership to authored references.
    /// </summary>
    private void ConfigureAuthoredCameras()
    {
        ConfigureOverlayCamera(fadeCamera, ResolveLayerMask(fadeLayerName), false);
        ConfigureOverlayCamera(playerCamera, ResolveLayerMask(playerLayerName), false);

        if (fadeCanvas != null && fadeCamera != null)
        {
            fadeCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            fadeCanvas.worldCamera = fadeCamera;
            fadeCanvas.planeDistance = 1f;
            GameSceneCameraLayerUtility.ApplyLayerRecursively(fadeCanvas.transform,
                                                              LayerMask.NameToLayer(fadeLayerName));
        }
    }

    /// <summary>
    /// Configures one authored camera as a non-post-processed URP overlay with a fixed culling mask.
    /// </summary>
    /// <param name="cameraComponent">Camera to configure.</param>
    /// <param name="cullingMask">Single-purpose culling mask.</param>
    /// <param name="enabled">Initial enabled state.</param>
    private static void ConfigureOverlayCamera(Camera cameraComponent, int cullingMask, bool enabled)
    {
        if (cameraComponent == null)
            return;

        cameraComponent.cullingMask = cullingMask;
        cameraComponent.enabled = enabled;
        cameraComponent.clearFlags = CameraClearFlags.Nothing;
        cameraComponent.gameObject.tag = "Untagged";
        UniversalAdditionalCameraData cameraData = cameraComponent.GetComponent<UniversalAdditionalCameraData>();

        if (cameraData == null)
            return;

        cameraData.renderType = CameraRenderType.Overlay;
        cameraData.renderPostProcessing = false;
    }

    #endregion

    #region Stack
    /// <summary>
    /// Finds the active base camera and places enabled transition passes after every gameplay and UI overlay.
    /// </summary>
    private void RefreshCameraStack()
    {
        if (!playerPresentationVisible)
            RestoreCameraMasks();

        if (!TryResolveBaseCamera(out activeBaseCamera))
            return;

        UniversalAdditionalCameraData baseCameraData = activeBaseCamera.GetComponent<UniversalAdditionalCameraData>();

        if (baseCameraData == null || baseCameraData.renderType != CameraRenderType.Base)
            return;

        if (fadeCamera != null)
            GameSceneUrpCameraStackUtility.RemoveOverlayCameraFromLoadedBaseStacks(fadeCamera);

        if (playerCamera != null)
            GameSceneUrpCameraStackUtility.RemoveOverlayCameraFromLoadedBaseStacks(playerCamera);

        if (fadeCamera != null)
            GameSceneUrpCameraStackUtility.AppendOverlayCamera(baseCameraData, fadeCamera);

        if (playerCamera != null)
            GameSceneUrpCameraStackUtility.AppendOverlayCamera(baseCameraData, playerCamera);

        ApplyCachedPresentationState();
    }

    /// <summary>
    /// Updates transition-only masks and framing against the already wired persistent base-camera stack.
    /// </summary>
    private void ApplyCachedPresentationState()
    {
        if (activeBaseCamera == null)
        {
            RefreshCameraStack();
            return;
        }

        UniversalAdditionalCameraData baseCameraData = activeBaseCamera.GetComponent<UniversalAdditionalCameraData>();

        if (baseCameraData == null || baseCameraData.renderType != CameraRenderType.Base)
            return;

        if (!playerPresentationVisible)
        {
            RestoreCameraMasks();
            return;
        }

        if (playerCamera == null)
            return;

        ExcludePlayerLayerFromCameraStack(activeBaseCamera, baseCameraData);

        // Capture the persistent gameplay view once so the isolated player overlay preserves identical framing
        // while the same bootstrap-owned camera follows the relocated player behind the black environment fade.
        if (!hasPlayerCameraSnapshot)
        {
            playerCameraSnapshot = CameraRenderSnapshot.Capture(activeBaseCamera);
            hasPlayerCameraSnapshot = true;
        }

        if (hasPlayerCameraSnapshot)
            playerCameraSnapshot.Apply(playerCamera, ResolvePlayerTrackingOffset());
        else
            SynchronizePlayerCamera(activeBaseCamera, playerCamera);

    }

    /// <summary>
    /// Excludes the temporary transition layer from every camera pass rendered before the player-only overlay.
    /// </summary>
    /// <param name="baseCamera">Active base camera that owns the current URP stack.</param>
    /// <param name="baseCameraData">URP metadata containing gameplay and UI overlay passes.</param>
    private void ExcludePlayerLayerFromCameraStack(Camera baseCamera,
                                                   UniversalAdditionalCameraData baseCameraData)
    {
        int playerLayerIndex = LayerMask.NameToLayer(playerLayerName);

        if (baseCamera == null || baseCameraData == null || playerLayerIndex < 0)
            return;

        ExcludeLayerFromCamera(baseCamera, playerLayerIndex);

        for (int cameraIndex = 0; cameraIndex < baseCameraData.cameraStack.Count; cameraIndex++)
        {
            Camera stackedCamera = baseCameraData.cameraStack[cameraIndex];

            if (stackedCamera == fadeCamera || stackedCamera == playerCamera)
                continue;

            ExcludeLayerFromCamera(stackedCamera, playerLayerIndex);
        }
    }

    /// <summary>
    /// Removes one layer from a camera while retaining its original authored culling mask for exact restoration.
    /// </summary>
    /// <param name="cameraComponent">Camera pass that could otherwise render the isolated player twice.</param>
    /// <param name="layerIndex">Temporary player transition layer index.</param>
    private void ExcludeLayerFromCamera(Camera cameraComponent, int layerIndex)
    {
        if (cameraComponent == null)
            return;

        if (!originalCameraMasks.ContainsKey(cameraComponent))
            originalCameraMasks.Add(cameraComponent, cameraComponent.cullingMask);

        cameraComponent.cullingMask &= ~(1 << layerIndex);
    }

    /// <summary>
    /// Restores every camera mask changed during the current persistent-player transition.
    /// </summary>
    private void RestoreCameraMasks()
    {
        foreach (KeyValuePair<Camera, int> cameraMask in originalCameraMasks)
        {
            if (cameraMask.Key != null)
                cameraMask.Key.cullingMask = cameraMask.Value;
        }

        originalCameraMasks.Clear();
    }

    /// <summary>
    /// Resolves the world-space relocation delta required to preserve the player's source-screen position.
    /// </summary>
    /// <returns>Player displacement since transition start, or zero before managed transform tracking is available.</returns>
    private Vector3 ResolvePlayerTrackingOffset()
    {
        if (!hasPlayerTrackingStartPosition || playerTrackingTransform == null)
            return Vector3.zero;

        return playerTrackingTransform.position - playerTrackingStartPosition;
    }

    /// <summary>
    /// Resolves the preferred live URP base camera while excluding both authored overlays.
    /// </summary>
    /// <param name="baseCamera">Resolved active base camera.</param>
    /// <returns>True when one enabled base camera is available.</returns>
    private bool TryResolveBaseCamera(out Camera baseCamera)
    {
        Camera mainCamera = Camera.main;

        if (IsEligibleBaseCamera(mainCamera))
        {
            baseCamera = mainCamera;
            return true;
        }

        Camera[] cameras = Camera.allCameras;

        for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
        {
            if (!IsEligibleBaseCamera(cameras[cameraIndex]))
                continue;

            baseCamera = cameras[cameraIndex];
            return true;
        }

        baseCamera = null;
        return false;
    }

    /// <summary>
    /// Resolves whether one enabled camera can own a URP overlay stack.
    /// </summary>
    /// <param name="candidate">Camera candidate.</param>
    /// <returns>True when the candidate is a live base camera outside this bridge.</returns>
    private bool IsEligibleBaseCamera(Camera candidate)
    {
        if (candidate == null || candidate == fadeCamera || candidate == playerCamera || !candidate.isActiveAndEnabled)
            return false;

        UniversalAdditionalCameraData cameraData = candidate.GetComponent<UniversalAdditionalCameraData>();
        return cameraData != null && cameraData.renderType == CameraRenderType.Base;
    }

    /// <summary>
    /// Copies the active base camera pose and projection required by the player-only overlay pass.
    /// </summary>
    /// <param name="source">Active gameplay or bootstrap base camera.</param>
    /// <param name="destination">Authored player-only overlay camera.</param>
    private static void SynchronizePlayerCamera(Camera source, Camera destination)
    {
        destination.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        destination.orthographic = source.orthographic;
        destination.orthographicSize = source.orthographicSize;
        destination.fieldOfView = source.fieldOfView;
        destination.nearClipPlane = source.nearClipPlane;
        destination.farClipPlane = source.farClipPlane;
        destination.rect = source.rect;
    }

    /// <summary>
    /// Resolves a named Unity layer as a culling mask without silently falling back to a broad mask.
    /// </summary>
    /// <param name="layerName">Configured Unity layer name.</param>
    /// <returns>Single-layer mask, or zero when the layer is missing.</returns>
    private static int ResolveLayerMask(string layerName)
    {
        int layerIndex = LayerMask.NameToLayer(layerName);
        return layerIndex >= 0 ? 1 << layerIndex : 0;
    }
    #endregion

    #endregion

    #region Types
    /// <summary>
    /// Stores the source gameplay view used by the isolated player overlay throughout one room transition.
    /// </summary>
    private struct CameraRenderSnapshot
    {
        #region Fields
        private Vector3 position;
        private Quaternion rotation;
        private Rect rect;
        private bool orthographic;
        private float orthographicSize;
        private float fieldOfView;
        private float nearClipPlane;
        private float farClipPlane;
        #endregion

        #region Methods
        /// <summary>
        /// Captures the pose and projection that frame the player when an intra-level transition starts.
        /// </summary>
        /// <param name="source">Source gameplay base camera.</param>
        /// <returns>Standalone snapshot safe to retain after the source scene unloads.</returns>
        public static CameraRenderSnapshot Capture(Camera source)
        {
            return new CameraRenderSnapshot
            {
                position = source.transform.position,
                rotation = source.transform.rotation,
                rect = source.rect,
                orthographic = source.orthographic,
                orthographicSize = source.orthographicSize,
                fieldOfView = source.fieldOfView,
                nearClipPlane = source.nearClipPlane,
                farClipPlane = source.farClipPlane
            };
        }

        /// <summary>
        /// Applies the captured source view plus player relocation delta without changing the active room camera.
        /// </summary>
        /// <param name="destination">Persistent overlay camera rendering isolated player renderers.</param>
        /// <param name="positionOffset">World-space player displacement used to preserve its source-screen placement.</param>
        public void Apply(Camera destination, Vector3 positionOffset)
        {
            if (destination == null)
                return;

            destination.transform.SetPositionAndRotation(position + positionOffset, rotation);
            destination.rect = rect;
            destination.orthographic = orthographic;
            destination.orthographicSize = orthographicSize;
            destination.fieldOfView = fieldOfView;
            destination.nearClipPlane = nearClipPlane;
            destination.farClipPlane = farClipPlane;
        }
        #endregion
    }
    #endregion
}
