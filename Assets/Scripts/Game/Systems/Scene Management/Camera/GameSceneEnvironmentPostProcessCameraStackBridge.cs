using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Routes URP camera stacking so post-processing is applied only to the authored environment pass.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GameSceneEnvironmentPostProcessCameraStackBridge : MonoBehaviour
{
    #region Constants
    private const float MinimumGizmoFarClip = 0.5f;
    private const string GameplayCameraFallbackName = "Gameplay Overlay Camera";
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Camera References")]
    [Tooltip("Base camera that renders only environment layers and owns the URP camera stack.")]
    [SerializeField] private Camera baseCamera;

    [Tooltip("Overlay camera that renders player, enemies, projectiles, drops and gameplay VFX after the environment post-process pass.")]
    [SerializeField] private Camera gameplayCamera;

    [Header("Layer Routing")]
    [Tooltip("Layers rendered by the base camera before URP post-processing. Keep this limited to Environment and Walls.")]
    [SerializeField] private LayerMask environmentCullingMask = GameSceneCameraLayerUtility.DefaultEnvironmentCullingMask;

    [Tooltip("When enabled, the gameplay overlay renders every layer except Environment Culling Mask and Additional Gameplay Excluded Layers.")]
    [SerializeField] private bool deriveGameplayCullingMask = true;

    [Tooltip("Explicit layers rendered by the gameplay overlay when derived routing is disabled.")]
    [SerializeField] private LayerMask gameplayCullingMask = GameSceneCameraLayerUtility.DefaultGameplayCullingMask;

    [Tooltip("Layers removed from the derived gameplay overlay mask, normally UI so the additive UI camera remains the only UI renderer.")]
    [SerializeField] private LayerMask additionalGameplayExcludedLayers = GameSceneCameraLayerUtility.DefaultUiCullingMask;

    [Header("URP Behavior")]
    [Tooltip("When enabled, URP post-processing runs on the base environment camera.")]
    [SerializeField] private bool enableEnvironmentPostProcessing = true;

    [Tooltip("When enabled, URP post-processing is forcibly disabled on the gameplay overlay camera.")]
    [SerializeField] private bool disableGameplayPostProcessing = true;

    [Tooltip("When enabled, the gameplay overlay keeps the environment depth buffer so walls can still occlude gameplay visuals.")]
    [SerializeField] private bool preserveEnvironmentDepth = true;

    [Tooltip("When enabled, scene load and active-scene changes re-apply the stack order without polling every frame.")]
    [SerializeField] private bool reapplyOnSceneChanges = true;

    [Tooltip("When enabled, this bridge removes its gameplay overlay camera from the base stack before it is disabled.")]
    [SerializeField] private bool removeGameplayCameraFromStackOnDisable = true;

    [Header("Debug Gizmos")]
    [Tooltip("Draws selected-scene frustum gizmos for the environment base camera and gameplay overlay camera.")]
    [SerializeField] private bool drawDebugGizmos = true;

    [Tooltip("Maximum far clip used by selected-scene debug frustums so large camera ranges do not flood the Scene view.")]
    [SerializeField] private float debugGizmoFarClip = 24f;

    [Tooltip("Scene-view gizmo color used for the environment base camera frustum.")]
    [SerializeField] private Color environmentGizmoColor = new Color(0.2f, 0.75f, 1f, 0.34f);

    [Tooltip("Scene-view gizmo color used for the gameplay overlay camera frustum.")]
    [SerializeField] private Color gameplayGizmoColor = new Color(1f, 0.85f, 0.18f, 0.34f);
    #endregion

    #region Runtime
    private Camera currentBaseCamera;
    private Camera currentGameplayCamera;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Applies the environment/gameplay camera split and registers scene callbacks when requested.
    /// </summary>
    private void OnEnable()
    {
        if (reapplyOnSceneChanges)
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        ApplyCameraStack();
    }

    /// <summary>
    /// Removes transient stack wiring and unregisters scene callbacks before this bridge unloads.
    /// </summary>
    private void OnDisable()
    {
        if (reapplyOnSceneChanges)
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        if (removeGameplayCameraFromStackOnDisable)
            RemoveGameplayCameraFromStack();
    }

    /// <summary>
    /// Assigns the local camera as the default base camera when the component is first added.
    /// </summary>
    private void Reset()
    {
        baseCamera = GetComponent<Camera>();
        gameplayCamera = ResolveChildGameplayCamera(baseCamera);
    }

    /// <summary>
    /// Draws selected-scene frustums for quick authoring verification.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Camera resolvedBaseCamera = baseCamera != null ? baseCamera : GetComponent<Camera>();
        Camera resolvedGameplayCamera = gameplayCamera != null ? gameplayCamera : ResolveChildGameplayCamera(resolvedBaseCamera);
        DrawCameraFrustum(resolvedBaseCamera, environmentGizmoColor);
        DrawCameraFrustum(resolvedGameplayCamera, gameplayGizmoColor);
    }
    #endregion

    #region Scene Events
    /// <summary>
    /// Re-applies the stack when Unity changes the active scene during managed scene transitions.
    /// </summary>
    /// <param name="previousScene">Scene that was active before the change.</param>
    /// <param name="nextScene">Scene that became active.</param>
    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        ApplyCameraStack();
    }

    /// <summary>
    /// Re-applies the stack when an additive scene may have contributed extra overlay cameras.
    /// </summary>
    /// <param name="loadedScene">Scene loaded by Unity.</param>
    /// <param name="loadMode">Mode used by Unity for the scene load.</param>
    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadMode)
    {
        ApplyCameraStack();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Applies the configured environment/gameplay split immediately.
    /// </summary>
    [ContextMenu("Apply Camera Stack Now")]
    public void ApplyCameraStack()
    {
        if (!TryResolveCameras(out Camera resolvedBaseCamera, out Camera resolvedGameplayCamera))
            return;

        if (!TryResolveCameraData(resolvedBaseCamera, resolvedGameplayCamera, out UniversalAdditionalCameraData baseCameraData, out UniversalAdditionalCameraData gameplayCameraData))
            return;

        ConfigureBaseCamera(resolvedBaseCamera, baseCameraData);
        ConfigureGameplayCamera(resolvedBaseCamera, resolvedGameplayCamera, gameplayCameraData);
        InsertGameplayCamera(baseCameraData, resolvedGameplayCamera);

        currentBaseCamera = resolvedBaseCamera;
        currentGameplayCamera = resolvedGameplayCamera;
    }
    #endregion

    #region Camera Resolution
    /// <summary>
    /// Resolves the serialized base and gameplay cameras, falling back to the local camera and named child camera.
    /// </summary>
    /// <param name="resolvedBaseCamera">Base camera resolved for environment rendering.</param>
    /// <param name="resolvedGameplayCamera">Overlay camera resolved for gameplay rendering.</param>
    /// <returns>True when both cameras are valid and distinct.</returns>
    private bool TryResolveCameras(out Camera resolvedBaseCamera, out Camera resolvedGameplayCamera)
    {
        resolvedBaseCamera = baseCamera != null ? baseCamera : GetComponent<Camera>();
        resolvedGameplayCamera = gameplayCamera != null ? gameplayCamera : ResolveChildGameplayCamera(resolvedBaseCamera);

        if (resolvedBaseCamera == null)
        {
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Missing base camera.", this);
            return false;
        }

        if (resolvedGameplayCamera == null)
        {
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Missing gameplay overlay camera.", this);
            return false;
        }

        if (ReferenceEquals(resolvedBaseCamera, resolvedGameplayCamera))
        {
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Base camera and gameplay overlay camera must be different cameras.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Finds a child camera named for the gameplay overlay setup utility.
    /// </summary>
    /// <param name="parentCamera">Base camera whose direct children are searched.</param>
    /// <returns>Child gameplay camera when available.</returns>
    private static Camera ResolveChildGameplayCamera(Camera parentCamera)
    {
        if (parentCamera == null)
            return null;

        Transform parentTransform = parentCamera.transform;

        for (int childIndex = 0; childIndex < parentTransform.childCount; childIndex++)
        {
            Transform childTransform = parentTransform.GetChild(childIndex);

            if (!string.Equals(childTransform.name, GameplayCameraFallbackName, System.StringComparison.Ordinal))
                continue;

            return childTransform.GetComponent<Camera>();
        }

        return null;
    }

    /// <summary>
    /// Resolves URP camera data components and reports invalid stack configuration early.
    /// </summary>
    /// <param name="resolvedBaseCamera">Base camera being configured.</param>
    /// <param name="resolvedGameplayCamera">Gameplay overlay camera being configured.</param>
    /// <param name="baseCameraData">URP data for the base camera when available.</param>
    /// <param name="gameplayCameraData">URP data for the gameplay overlay camera when available.</param>
    /// <returns>True when both cameras can participate in URP camera stacking.</returns>
    private bool TryResolveCameraData(Camera resolvedBaseCamera,
                                      Camera resolvedGameplayCamera,
                                      out UniversalAdditionalCameraData baseCameraData,
                                      out UniversalAdditionalCameraData gameplayCameraData)
    {
        baseCameraData = resolvedBaseCamera.GetComponent<UniversalAdditionalCameraData>();
        gameplayCameraData = resolvedGameplayCamera.GetComponent<UniversalAdditionalCameraData>();

        if (baseCameraData == null)
        {
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Base camera is missing UniversalAdditionalCameraData.", this);
            return false;
        }

        if (gameplayCameraData == null)
        {
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Gameplay overlay camera is missing UniversalAdditionalCameraData.", this);
            return false;
        }

        return true;
    }
    #endregion

    #region Camera Configuration
    /// <summary>
    /// Configures the base camera as the post-processed environment-only render pass.
    /// </summary>
    /// <param name="resolvedBaseCamera">Camera that owns the URP stack.</param>
    /// <param name="baseCameraData">URP data paired with the base camera.</param>
    private void ConfigureBaseCamera(Camera resolvedBaseCamera, UniversalAdditionalCameraData baseCameraData)
    {
        baseCameraData.renderType = CameraRenderType.Base;
        baseCameraData.renderPostProcessing = enableEnvironmentPostProcessing;
        resolvedBaseCamera.cullingMask = environmentCullingMask.value;

        ValidateEnvironmentMask();
    }

    /// <summary>
    /// Configures the overlay camera to render gameplay layers after the environment post-process pass.
    /// </summary>
    /// <param name="resolvedBaseCamera">Base camera whose projection settings are mirrored.</param>
    /// <param name="resolvedGameplayCamera">Gameplay overlay camera being configured.</param>
    /// <param name="gameplayCameraData">URP data paired with the gameplay overlay camera.</param>
    private void ConfigureGameplayCamera(Camera resolvedBaseCamera,
                                         Camera resolvedGameplayCamera,
                                         UniversalAdditionalCameraData gameplayCameraData)
    {
        MirrorCameraProjection(resolvedBaseCamera, resolvedGameplayCamera);
        resolvedGameplayCamera.clearFlags = CameraClearFlags.Nothing;
        resolvedGameplayCamera.cullingMask = ResolveGameplayCullingMask();
        resolvedGameplayCamera.depth = resolvedBaseCamera.depth + 0.01f;

        gameplayCameraData.renderType = CameraRenderType.Overlay;

        if (disableGameplayPostProcessing)
            gameplayCameraData.renderPostProcessing = false;

        ValidateGameplayMask(resolvedGameplayCamera.cullingMask);
        ValidateGameplayDepthMode(gameplayCameraData);
        ValidateGameplayCameraParent(resolvedBaseCamera, resolvedGameplayCamera);
    }

    /// <summary>
    /// Copies static projection and clipping values from the base camera to the gameplay overlay camera.
    /// </summary>
    /// <param name="resolvedBaseCamera">Source camera.</param>
    /// <param name="resolvedGameplayCamera">Destination overlay camera.</param>
    private static void MirrorCameraProjection(Camera resolvedBaseCamera, Camera resolvedGameplayCamera)
    {
        resolvedGameplayCamera.nearClipPlane = resolvedBaseCamera.nearClipPlane;
        resolvedGameplayCamera.farClipPlane = resolvedBaseCamera.farClipPlane;
        resolvedGameplayCamera.fieldOfView = resolvedBaseCamera.fieldOfView;
        resolvedGameplayCamera.orthographic = resolvedBaseCamera.orthographic;
        resolvedGameplayCamera.orthographicSize = resolvedBaseCamera.orthographicSize;
        resolvedGameplayCamera.allowHDR = resolvedBaseCamera.allowHDR;
        resolvedGameplayCamera.allowMSAA = resolvedBaseCamera.allowMSAA;
        resolvedGameplayCamera.useOcclusionCulling = resolvedBaseCamera.useOcclusionCulling;
    }

    /// <summary>
    /// Resolves the gameplay overlay mask from the chosen routing mode.
    /// </summary>
    /// <returns>Culling mask assigned to the gameplay overlay camera.</returns>
    private int ResolveGameplayCullingMask()
    {
        if (deriveGameplayCullingMask)
            return GameSceneCameraLayerUtility.BuildGameplayCullingMask(environmentCullingMask.value, additionalGameplayExcludedLayers.value);

        return gameplayCullingMask.value;
    }
    #endregion

    #region Stack Management
    /// <summary>
    /// Inserts the gameplay overlay camera at the start of the base camera stack while preserving other overlays.
    /// </summary>
    /// <param name="baseCameraData">URP data whose camera stack is edited.</param>
    /// <param name="resolvedGameplayCamera">Gameplay overlay camera that must render before UI overlays.</param>
    private static void InsertGameplayCamera(UniversalAdditionalCameraData baseCameraData, Camera resolvedGameplayCamera)
    {
        GameSceneUrpCameraStackUtility.InsertOverlayCamera(baseCameraData, resolvedGameplayCamera, 0);
    }

    /// <summary>
    /// Removes the last configured gameplay overlay camera from the last configured base stack.
    /// </summary>
    private void RemoveGameplayCameraFromStack()
    {
        if (currentGameplayCamera == null)
            return;

        GameSceneUrpCameraStackUtility.RemoveOverlayCameraFromLoadedBaseStacks(currentGameplayCamera);
        currentBaseCamera = null;
        currentGameplayCamera = null;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Reports environment mask issues without mutating serialized values.
    /// </summary>
    private void ValidateEnvironmentMask()
    {
        if (environmentCullingMask.value == 0)
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Environment Culling Mask is empty, so the post-processed base pass will render nothing.", this);
    }

    /// <summary>
    /// Reports gameplay mask issues without mutating serialized values.
    /// </summary>
    /// <param name="gameplayMask">Final gameplay overlay mask.</param>
    private void ValidateGameplayMask(int gameplayMask)
    {
        if (gameplayMask == 0)
        {
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Gameplay overlay culling mask is empty.", this);
            return;
        }

        if (!deriveGameplayCullingMask)
            ValidateExplicitGameplayMask(gameplayMask);
    }

    /// <summary>
    /// Reports explicit gameplay mask overlap with environment layers.
    /// </summary>
    /// <param name="gameplayMask">Explicit gameplay overlay mask.</param>
    private void ValidateExplicitGameplayMask(int gameplayMask)
    {
        if (GameSceneCameraLayerUtility.HasLayerOverlap(gameplayMask, environmentCullingMask.value))
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Explicit Gameplay Culling Mask overlaps Environment Culling Mask and may double-render environment geometry.", this);
    }

    /// <summary>
    /// Reports overlay parenting problems that would desynchronize camera follow without a per-frame sync.
    /// </summary>
    /// <param name="resolvedBaseCamera">Base camera expected to own the gameplay overlay transform.</param>
    /// <param name="resolvedGameplayCamera">Gameplay overlay camera being inspected.</param>
    private void ValidateGameplayCameraParent(Camera resolvedBaseCamera, Camera resolvedGameplayCamera)
    {
        if (resolvedGameplayCamera.transform.parent == resolvedBaseCamera.transform)
            return;

        Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Gameplay overlay camera should be a child of the base camera so runtime camera follow moves both cameras without a per-frame sync.", this);
    }

    /// <summary>
    /// Reports overlay depth-clear mismatches without using runtime reflection against URP internals.
    /// </summary>
    /// <param name="gameplayCameraData">URP data paired with the gameplay overlay camera.</param>
    private void ValidateGameplayDepthMode(UniversalAdditionalCameraData gameplayCameraData)
    {
        if (preserveEnvironmentDepth && gameplayCameraData.clearDepth)
        {
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Preserve Environment Depth is enabled, but the gameplay overlay camera is still configured to clear depth. Re-run the environment post-process setup utility to update the serialized URP camera data.", this);
            return;
        }

        if (!preserveEnvironmentDepth && !gameplayCameraData.clearDepth)
            Debug.LogWarning("[GameSceneEnvironmentPostProcessCameraStackBridge] Preserve Environment Depth is disabled, but the gameplay overlay camera is configured to preserve depth. Re-run the environment post-process setup utility to update the serialized URP camera data.", this);
    }
    #endregion

    #region Gizmos
    /// <summary>
    /// Draws one camera frustum in the Scene view when a camera is assigned.
    /// </summary>
    /// <param name="camera">Camera whose frustum should be drawn.</param>
    /// <param name="color">Gizmo color used for the frustum.</param>
    private void DrawCameraFrustum(Camera camera, Color color)
    {
        if (camera == null)
            return;

        Gizmos.color = color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(camera.transform.position, camera.transform.rotation, Vector3.one);

        if (camera.orthographic)
            DrawOrthographicGizmo(camera);
        else
            Gizmos.DrawFrustum(Vector3.zero, camera.fieldOfView, ResolveGizmoFarClip(camera), camera.nearClipPlane, camera.aspect);

        Gizmos.matrix = previousMatrix;
    }

    /// <summary>
    /// Draws an orthographic camera volume using the same capped depth as perspective frustums.
    /// </summary>
    /// <param name="camera">Orthographic camera being drawn.</param>
    private void DrawOrthographicGizmo(Camera camera)
    {
        float height = camera.orthographicSize * 2f;
        float depth = ResolveGizmoFarClip(camera);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, depth * 0.5f), new Vector3(height * camera.aspect, height, depth));
    }

    /// <summary>
    /// Resolves the capped far clip used for scene debug gizmos.
    /// </summary>
    /// <param name="camera">Camera whose clipping distance is considered.</param>
    /// <returns>Positive far clip distance for gizmo rendering.</returns>
    private float ResolveGizmoFarClip(Camera camera)
    {
        float configuredFarClip = Mathf.Max(MinimumGizmoFarClip, debugGizmoFarClip);
        return Mathf.Min(camera.farClipPlane, configuredFarClip);
    }
    #endregion

    #endregion
}
