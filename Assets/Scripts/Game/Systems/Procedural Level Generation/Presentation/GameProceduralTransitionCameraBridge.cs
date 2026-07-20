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
    private bool fadePresentationVisible;
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

        activeBridge.RefreshCameraStack();
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

        activeBridge.playerPresentationVisible = visible;

        if (activeBridge.playerCamera != null)
            activeBridge.playerCamera.enabled = visible;

        activeBridge.RefreshCameraStack();
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
        RefreshCameraStack();
    }

    /// <summary>
    /// Refreshes stack ownership after a target or companion scene is loaded.
    /// </summary>
    /// <param name="loadedScene">Newly loaded scene.</param>
    /// <param name="loadMode">Load mode used by Scene Management.</param>
    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadMode)
    {
        RefreshCameraStack();
    }

    /// <summary>
    /// Refreshes stack ownership after a previous room releases its base camera.
    /// </summary>
    /// <param name="unloadedScene">Scene removed from the loaded set.</param>
    private void HandleSceneUnloaded(Scene unloadedScene)
    {
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

        if (fadePresentationVisible && fadeCamera != null)
            GameSceneUrpCameraStackUtility.AppendOverlayCamera(baseCameraData, fadeCamera);

        if (!playerPresentationVisible || playerCamera == null)
            return;

        ExcludePlayerLayerFromCameraStack(activeBaseCamera, baseCameraData);
        SynchronizePlayerCamera(activeBaseCamera, playerCamera);
        GameSceneUrpCameraStackUtility.AppendOverlayCamera(baseCameraData, playerCamera);
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
}
