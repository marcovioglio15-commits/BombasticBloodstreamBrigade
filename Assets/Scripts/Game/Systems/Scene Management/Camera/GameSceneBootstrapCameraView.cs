using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
#if NASHCORE_FMOD || UNITY_EDITOR
using FMODUnity;
#endif

/// <summary>
/// Owns the persistent gameplay camera rig and yields rendering only to non-gameplay managed scenes such as menus.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GameSceneBootstrapCameraView : MonoBehaviour
{
    #region Fields

    #region Static Fields
    private static Camera activePersistentCamera;
    #endregion

    #region Serialized Fields
    [Header("References")]
    [Tooltip("Persistent base camera that survives every gameplay and procedural room scene transition.")]
    [FormerlySerializedAs("bootstrapCamera")]
    [SerializeField]
    private Camera persistentCamera;

    [Tooltip("Persistent gameplay overlay camera parented to the base camera for synchronized ECS follow and shake presentation.")]
    [SerializeField]
    private Camera gameplayOverlayCamera;

    [Tooltip("Audio listener paired with the persistent base camera and disabled while a menu or other external camera owns rendering.")]
    [FormerlySerializedAs("bootstrapAudioListener")]
    [SerializeField]
    private AudioListener persistentAudioListener;

    [Header("Runtime Policy")]
    [Tooltip("When enabled, the persistent gameplay rig is disabled while a loaded non-bootstrap scene owns a renderable base camera, such as the main menu.")]
    [SerializeField]
    private bool disableWhenManagedCameraExists = true;

    [Tooltip("Depth assigned to the persistent gameplay base camera while it owns rendering.")]
    [FormerlySerializedAs("fallbackCameraDepth")]
    [SerializeField]
    private float persistentCameraDepth = -1f;
    #endregion

    #region Runtime
#if NASHCORE_FMOD || UNITY_EDITOR
    private StudioListener persistentStudioListener;
#endif
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether a camera is the persistent gameplay render owner stored in the bootstrap scene.
    /// </summary>
    /// <param name="candidateCamera">Camera candidate resolved by a runtime camera consumer.</param>
    /// <returns>True when the candidate is the active persistent gameplay base camera.</returns>
    public static bool IsPersistentGameplayCamera(Camera candidateCamera)
    {
        return candidateCamera != null && candidateCamera == activePersistentCamera;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Registers scene callbacks and immediately applies persistent gameplay camera ownership.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        activePersistentCamera = persistentCamera;
        ConfigurePersistentCamera();
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        RefreshCameraState();
    }

    /// <summary>
    /// Unregisters scene callbacks when the bootstrap camera object is disabled or unloaded.
    /// </summary>
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;

        if (activePersistentCamera == persistentCamera)
            activePersistentCamera = null;
    }
    #endregion

    #region Scene Events
    /// <summary>
    /// Refreshes camera ownership when Unity's active scene changes.
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

        RefreshCameraState();
    }

    /// <summary>
    /// Refreshes camera ownership when an additive managed scene finishes loading.
    /// </summary>
    /// <param name="loadedScene">Scene that Unity loaded.</param>
    /// <param name="loadMode">Load mode used for the scene.</param>
    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadMode)
    {
        if (GameProceduralRoomStreamingRuntimeUtility.IsOwnedManagedScene(loadedScene))
            return;

        RefreshCameraState();
    }

    /// <summary>
    /// Refreshes camera ownership when a managed scene unloads and persistent gameplay rendering may resume.
    /// </summary>
    /// <param name="unloadedScene">Scene that Unity unloaded.</param>
    private void HandleSceneUnloaded(Scene unloadedScene)
    {
        if (GameProceduralRoomStreamingRuntimeUtility.IsOwnedManagedScene(unloadedScene))
            return;

        RefreshCameraState();
    }
    #endregion

    #region Camera Policy
    /// <summary>
    /// Enables the persistent gameplay rig only while no non-bootstrap renderable base camera is loaded.
    /// </summary>
    private void RefreshCameraState()
    {
        if (persistentCamera == null)
            return;

        bool hasExternalCamera = TryResolveExternalRenderableBaseCamera(out Camera externalCamera);
        bool shouldEnablePersistentCamera = !disableWhenManagedCameraExists || !hasExternalCamera;
        SetPersistentCameraState(shouldEnablePersistentCamera);

#if NASHCORE_FMOD || UNITY_EDITOR
        RefreshFmodListenerState(externalCamera, shouldEnablePersistentCamera);
#endif

        // Scene callbacks have no guaranteed listener order. Rebuild transition overlays only after this
        // ownership state is final so the persistent player always has the same base camera between room scenes.
        GameSceneUiCameraStackBridge.RefreshLoadedCameraStacks();
        GameProceduralTransitionCameraBridge.RefreshStackOrder();
    }

    /// <summary>
    /// Enables or disables the complete persistent camera rig and maintains unambiguous MainCamera ownership.
    /// </summary>
    /// <param name="enabled">True when gameplay rendering belongs to the persistent bootstrap rig.</param>
    private void SetPersistentCameraState(bool enabled)
    {
        persistentCamera.enabled = enabled;

        if (gameplayOverlayCamera != null)
            gameplayOverlayCamera.enabled = enabled;

        if (persistentAudioListener != null)
            persistentAudioListener.enabled = enabled;

        string targetTag = enabled ? "MainCamera" : "Untagged";

        if (!persistentCamera.CompareTag(targetTag))
            persistentCamera.gameObject.tag = targetTag;
    }

    /// <summary>
    /// Resolves the preferred renderable base camera from any loaded scene other than the bootstrap scene.
    /// </summary>
    /// <param name="externalCamera">Preferred external render camera when found.</param>
    /// <returns>True when another scene should own rendering and Camera.main lookup.</returns>
    private bool TryResolveExternalRenderableBaseCamera(out Camera externalCamera)
    {
        externalCamera = null;
        Scene bootstrapScene = gameObject.scene;
        Scene activeScene = SceneManager.GetActiveScene();

        if (ShouldInspectScene(activeScene, bootstrapScene) &&
            TryResolveSceneRenderableBaseCamera(activeScene, out externalCamera))
        {
            return true;
        }

        int loadedSceneCount = SceneManager.sceneCount;

        for (int sceneIndex = 0; sceneIndex < loadedSceneCount; sceneIndex++)
        {
            Scene candidateScene = SceneManager.GetSceneAt(sceneIndex);

            if (!ShouldInspectScene(candidateScene, bootstrapScene))
                continue;

            if (candidateScene == activeScene)
                continue;

            if (TryResolveSceneRenderableBaseCamera(candidateScene, out externalCamera))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves whether one loaded scene should be inspected for a gameplay or UI base camera.
    /// </summary>
    /// <param name="candidateScene">Scene being inspected.</param>
    /// <param name="bootstrapScene">Scene that owns the persistent gameplay camera.</param>
    /// <returns>True when the scene can contain an external render owner.</returns>
    private static bool ShouldInspectScene(Scene candidateScene, Scene bootstrapScene)
    {
        if (!candidateScene.IsValid() || !candidateScene.isLoaded)
            return false;

        return candidateScene != bootstrapScene;
    }

    /// <summary>
    /// Resolves one scene hierarchy's first renderable non-overlay camera.
    /// </summary>
    /// <param name="scene">Loaded scene being inspected.</param>
    /// <param name="resolvedCamera">Renderable camera when found.</param>
    /// <returns>True when the scene owns a camera that should temporarily replace persistent gameplay rendering.</returns>
    private bool TryResolveSceneRenderableBaseCamera(Scene scene, out Camera resolvedCamera)
    {
        resolvedCamera = null;
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            Camera[] sceneCameras = rootObjects[rootIndex].GetComponentsInChildren<Camera>(true);

            for (int cameraIndex = 0; cameraIndex < sceneCameras.Length; cameraIndex++)
            {
                Camera candidateCamera = sceneCameras[cameraIndex];

                if (IsRenderableBaseCamera(candidateCamera))
                {
                    resolvedCamera = candidateCamera;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether one camera can temporarily own rendering instead of the persistent gameplay rig.
    /// </summary>
    /// <param name="candidateCamera">Camera being inspected.</param>
    /// <returns>True when the camera is active, enabled and not a URP overlay camera.</returns>
    private bool IsRenderableBaseCamera(Camera candidateCamera)
    {
        if (candidateCamera == null)
            return false;

        if (candidateCamera == persistentCamera)
            return false;

        if (!candidateCamera.isActiveAndEnabled)
            return false;

        UniversalAdditionalCameraData cameraData = candidateCamera.GetComponent<UniversalAdditionalCameraData>();

        if (cameraData == null)
            return true;

        return cameraData.renderType == CameraRenderType.Base;
    }

#if NASHCORE_FMOD || UNITY_EDITOR
    /// <summary>
    /// Keeps one FMOD listener on the active persistent or external render owner.
    /// </summary>
    /// <param name="externalCamera">Preferred managed-scene camera when available.</param>
    /// <param name="persistentCameraEnabled">True when the persistent gameplay rig currently owns rendering.</param>
    private void RefreshFmodListenerState(Camera externalCamera, bool persistentCameraEnabled)
    {
        if (persistentCamera == null)
            return;

        if (persistentStudioListener == null)
            persistentStudioListener = EnsureStudioListener(persistentCamera);

        if (persistentStudioListener != null)
            persistentStudioListener.enabled = persistentCameraEnabled;

        if (externalCamera == null || persistentCameraEnabled)
            return;

        StudioListener externalStudioListener = EnsureStudioListener(externalCamera);

        if (externalStudioListener != null)
            externalStudioListener.enabled = true;
    }

    /// <summary>
    /// Ensures the provided camera owns an FMOD Studio Listener component.
    /// </summary>
    /// <param name="targetCamera">Camera that should report 3D listener position to FMOD.</param>
    /// <returns>Existing or newly added listener component.</returns>
    private static StudioListener EnsureStudioListener(Camera targetCamera)
    {
        if (targetCamera == null)
            return null;

        StudioListener studioListener = targetCamera.GetComponent<StudioListener>();

        if (studioListener != null)
            return studioListener;

        return targetCamera.gameObject.AddComponent<StudioListener>();
    }
#endif
    #endregion

    #region Setup
    /// <summary>
    /// Resolves local camera and listener references when the authored fields are empty.
    /// </summary>
    private void ResolveReferences()
    {
        if (persistentCamera == null)
            persistentCamera = GetComponent<Camera>();

        if (gameplayOverlayCamera == null)
            gameplayOverlayCamera = ResolveGameplayOverlayCamera();

        if (persistentAudioListener == null)
            persistentAudioListener = GetComponent<AudioListener>();
    }

    /// <summary>
    /// Normalizes the persistent base camera and its parented gameplay overlay without replacing either object.
    /// </summary>
    private void ConfigurePersistentCamera()
    {
        if (persistentCamera == null)
            return;

        // A deterministic black clear prevents the bootstrap camera from exposing an uninitialized render target
        // while the menu has unloaded and the first procedural environment has not completed streaming yet.
        persistentCamera.clearFlags = CameraClearFlags.SolidColor;
        persistentCamera.backgroundColor = Color.black;
        persistentCamera.depth = persistentCameraDepth;

        if (gameplayOverlayCamera != null)
        {
            UniversalAdditionalCameraData overlayData = gameplayOverlayCamera.GetComponent<UniversalAdditionalCameraData>();

            if (overlayData != null)
                overlayData.renderType = CameraRenderType.Overlay;
        }
    }

    /// <summary>
    /// Resolves the parented gameplay overlay once during initialization instead of scanning cameras every frame.
    /// </summary>
    /// <returns>First child URP overlay camera, or null when the authored persistent rig is incomplete.</returns>
    private Camera ResolveGameplayOverlayCamera()
    {
        Camera[] childCameras = GetComponentsInChildren<Camera>(true);

        for (int cameraIndex = 0; cameraIndex < childCameras.Length; cameraIndex++)
        {
            Camera candidateCamera = childCameras[cameraIndex];

            if (candidateCamera == null || candidateCamera == persistentCamera)
                continue;

            UniversalAdditionalCameraData cameraData = candidateCamera.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData != null && cameraData.renderType == CameraRenderType.Overlay)
                return candidateCamera;
        }

        return null;
    }
    #endregion

    #endregion
}
