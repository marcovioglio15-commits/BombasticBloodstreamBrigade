using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if NASHCORE_FMOD
using FMODUnity;
#endif

/// <summary>
/// Keeps the bootstrap fallback camera from rendering over managed additive scenes.
/// /params None.
/// /returns None.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GameSceneBootstrapCameraView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("References")]
    [Tooltip("Fallback camera used only while the bootstrap scene is the only loaded scene with a renderable camera.")]
    [SerializeField] private Camera bootstrapCamera;

    [Tooltip("Audio listener paired with the bootstrap fallback camera and disabled together with it.")]
    [SerializeField] private AudioListener bootstrapAudioListener;

    [Header("Runtime Policy")]
    [Tooltip("When enabled, the fallback camera is disabled as soon as another loaded scene owns a renderable base camera.")]
    [SerializeField] private bool disableWhenManagedCameraExists = true;

    [Tooltip("Depth assigned to the fallback camera so authored gameplay cameras render after it if both are temporarily enabled.")]
    [SerializeField] private float fallbackCameraDepth = -1000f;
    #endregion

    #region Runtime
#if NASHCORE_FMOD
    private StudioListener bootstrapStudioListener;
#endif
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Registers scene callbacks and immediately applies the fallback camera policy.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        ConfigureFallbackCamera();
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        RefreshCameraState();
    }

    /// <summary>
    /// Unregisters scene callbacks when the bootstrap camera object is disabled or unloaded.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }
    #endregion

    #region Scene Events
    /// <summary>
    /// Refreshes camera ownership when Unity's active scene changes.
    /// /params previousScene Scene that was active before the change.
    /// /params nextScene Scene that became active.
    /// /returns None.
    /// </summary>
    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        RefreshCameraState();
    }

    /// <summary>
    /// Refreshes camera ownership when an additive managed scene finishes loading.
    /// /params loadedScene Scene that Unity loaded.
    /// /params loadMode Load mode used for the scene.
    /// /returns None.
    /// </summary>
    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadMode)
    {
        RefreshCameraState();
    }

    /// <summary>
    /// Refreshes camera ownership when a managed scene unloads and the fallback may be needed again.
    /// /params unloadedScene Scene that Unity unloaded.
    /// /returns None.
    /// </summary>
    private void HandleSceneUnloaded(Scene unloadedScene)
    {
        RefreshCameraState();
    }
    #endregion

    #region Camera Policy
    /// <summary>
    /// Enables the fallback camera only while no non-bootstrap renderable camera is loaded.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void RefreshCameraState()
    {
        if (bootstrapCamera == null)
            return;

        bool hasExternalCamera = TryResolveExternalRenderableBaseCamera(out Camera externalCamera);
        bool shouldEnableFallback = !disableWhenManagedCameraExists || !hasExternalCamera;
        bootstrapCamera.enabled = shouldEnableFallback;

        if (bootstrapAudioListener != null)
            bootstrapAudioListener.enabled = shouldEnableFallback;

#if NASHCORE_FMOD
        RefreshFmodListenerState(externalCamera);
#endif
    }

    /// <summary>
    /// Resolves the preferred renderable base camera from any loaded scene other than the bootstrap scene.
    /// /params None.
    /// /params externalCamera Preferred external render camera when found.
    /// /returns True when another scene should own rendering and Camera.main lookup.
    /// </summary>
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
    /// /params candidateScene Scene being inspected.
    /// /params bootstrapScene Scene that owns this fallback camera.
    /// /returns True when the scene can contain an external render owner.
    /// </summary>
    private static bool ShouldInspectScene(Scene candidateScene, Scene bootstrapScene)
    {
        if (!candidateScene.IsValid() || !candidateScene.isLoaded)
            return false;

        return candidateScene != bootstrapScene;
    }

    /// <summary>
    /// Resolves one scene hierarchy's first renderable non-overlay camera.
    /// /params scene Loaded scene being inspected.
    /// /params resolvedCamera Renderable camera when found.
    /// /returns True when the scene owns a camera that should replace the bootstrap fallback camera.
    /// </summary>
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
    /// Checks whether one camera can own scene rendering instead of the bootstrap fallback.
    /// /params candidateCamera Camera being inspected.
    /// /returns True when the camera is active, enabled and not a URP overlay camera.
    /// </summary>
    private bool IsRenderableBaseCamera(Camera candidateCamera)
    {
        if (candidateCamera == null)
            return false;

        if (candidateCamera == bootstrapCamera)
            return false;

        if (!candidateCamera.isActiveAndEnabled)
            return false;

        UniversalAdditionalCameraData cameraData = candidateCamera.GetComponent<UniversalAdditionalCameraData>();

        if (cameraData == null)
            return true;

        return cameraData.renderType == CameraRenderType.Base;
    }

#if NASHCORE_FMOD
    /// <summary>
    /// Keeps an FMOD listener on the active render camera, falling back to the bootstrap camera while loading.
    /// /params externalCamera Preferred managed-scene camera when available.
    /// /returns None.
    /// </summary>
    private void RefreshFmodListenerState(Camera externalCamera)
    {
        if (bootstrapCamera == null)
            return;

        if (bootstrapStudioListener == null)
            bootstrapStudioListener = EnsureStudioListener(bootstrapCamera);

        bool useBootstrapListener = externalCamera == null;

        if (bootstrapStudioListener != null)
            bootstrapStudioListener.enabled = useBootstrapListener;

        if (externalCamera == null)
            return;

        StudioListener externalStudioListener = EnsureStudioListener(externalCamera);

        if (externalStudioListener != null)
            externalStudioListener.enabled = true;
    }

    /// <summary>
    /// Ensures the provided camera owns an FMOD Studio Listener component.
    /// /params targetCamera Camera that should report 3D listener position to FMOD.
    /// /returns Existing or newly added listener component.
    /// </summary>
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
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ResolveReferences()
    {
        if (bootstrapCamera == null)
            bootstrapCamera = GetComponent<Camera>();

        if (bootstrapAudioListener == null)
            bootstrapAudioListener = GetComponent<AudioListener>();
    }

    /// <summary>
    /// Normalizes the bootstrap camera so it cannot be selected by gameplay Camera.main lookups.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ConfigureFallbackCamera()
    {
        if (bootstrapCamera == null)
            return;

        bootstrapCamera.depth = fallbackCameraDepth;

        if (!bootstrapCamera.CompareTag("Untagged"))
            bootstrapCamera.gameObject.tag = "Untagged";
    }
    #endregion

    #endregion
}
