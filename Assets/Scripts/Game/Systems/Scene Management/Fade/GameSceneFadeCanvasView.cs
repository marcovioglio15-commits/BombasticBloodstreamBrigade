using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Authored full-screen fade overlay view controlled by the Game Scene Manager presentation system.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSceneFadeCanvasView : MonoBehaviour
{
    #region Constants
    private const int MaxFadeSortingOrder = 32767;
    private const float OpaqueThreshold = 0.9999f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("References")]
    [Tooltip("Canvas that renders the full-screen fade above all additive scene UI.")]
    [SerializeField]
    private Canvas fadeCanvas;

    [Tooltip("CanvasGroup that receives fade alpha and blocks raycasts while the overlay is visible.")]
    [SerializeField]
    private CanvasGroup canvasGroup;

    [Tooltip("Image used as the full-screen fade surface.")]
    [SerializeField]
    private Image fadeImage;

    [Tooltip("Authored UI material using the Paint Reveal shader. The runtime updates only transition parameters and never creates UI or materials.")]
    [SerializeField]
    private Material fadeMaterial;
    #endregion

    #region Static
    private static GameSceneFadeCanvasView activeView;
    private static int activeViewVersion;
    #endregion

    #region Render State
    private bool opaqueCoverageApplied;
    private bool opaqueCoverageAwaitingRender;
    private bool opaqueCoverageRendered;
    private bool fadeMaterialStateCaptured;
    private Material capturedFadeMaterial;
    private GameUiPaintRevealMaterialState capturedFadeMaterialState;
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the latest fade state to the active authored view when one exists.
    /// </summary>
    /// <param name="state">Complete ECS fade state containing progress, direction, color, and shader shaping.</param>
    /// <returns>True when a fade view received the state.</returns>
    public static bool TryApply(in GameSceneFadePresentationState state)
    {
        GameProceduralTransitionCameraBridge.SetFadePresentationVisible(state.Visible != 0 || state.Alpha > 0.001f);

        if (activeView == null)
            return false;

        activeView.Apply(state.Alpha,
                         state.Visible != 0,
                         new Color(state.Color.x, state.Color.y, state.Color.z, state.Color.w),
                         state.Mode,
                         state.WipeDirection,
                         state.Operation,
                         state.Easing,
                         state.DirectionalEdgeSoftness,
                         state.DirectionalNoiseStrength,
                         state.DirectionalNoiseScale,
                         state.PaintEdgeSoftness,
                         state.PaintNoiseStrength,
                         state.PaintNoiseScale,
                         state.PaintBristleStrength,
                         state.PaintBristleScale);
        return true;
    }

    /// <summary>
    /// Version number incremented whenever a fade view registers, allowing ECS presentation to reapply unchanged state.
    /// </summary>
    /// <returns>Active view version.</returns>
    public static int ActiveViewVersion
    {
        get
        {
            return activeViewVersion;
        }
    }
    /// <summary>
    /// Reports whether an authored fade surface is currently registered.
    /// </summary>
    /// <returns>True when presentation can cover the outgoing scene.</returns>
    public static bool HasActiveView
    {
        get
        {
            return activeView != null;
        }
    }

    /// <summary>
    /// Reports whether complete fade coverage has passed through a Canvas render callback.
    /// </summary>
    /// <returns>True only after at least one render submission with complete coverage.</returns>
    public static bool HasRenderedOpaqueCoverage
    {
        get
        {
            return activeView != null && activeView.opaqueCoverageRendered;
        }
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Caches component references and registers this authored overlay as the active fade view.
    /// </summary>
    private void OnEnable()
    {
        if (fadeCanvas == null)
            fadeCanvas = GetComponent<Canvas>();

        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (fadeImage == null)
            fadeImage = GetComponentInChildren<Image>(true);

        ConfigureFadeSurface();
        CaptureFadeMaterialState();
        ConfigureCanvas();
        activeView = this;
        activeViewVersion++;
        Canvas.willRenderCanvases += HandleCanvasWillRender;

        if (!TryApplyCurrentRuntimeState())
            Apply(0f,
                  false,
                  Color.black,
                  GameSceneFadeMode.DirectionalGradient,
                  GameSceneFadeWipeDirection.LeftToRight,
                  GameUiPaintRevealOperation.Deposit,
                  GameSceneFadeEasing.SmoothStep,
                  0.16f,
                  0.035f,
                  5.5f,
                  0.025f,
                  0.22f,
                  2.4f,
                  0.075f,
                  48f);
    }

    /// <summary>
    /// Clears the active view reference when this overlay is disabled or unloaded.
    /// </summary>
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleCanvasWillRender;
        ResetOpaqueCoverageState();
        RestoreFadeMaterialState();

        if (activeView == this)
        {
            activeView = null;
            GameProceduralTransitionCameraBridge.SetFadePresentationVisible(false);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes alpha, color and raycast state to the authored overlay components.
    /// </summary>
    /// <param name="alpha">Fade alpha in the 0..1 range.</param>
    /// <param name="visible">True when the overlay should be enabled.</param>
    /// <param name="color">Fade surface color.</param>
    /// <param name="mode">Coverage mode applied to the authored image.</param>
    /// <param name="wipeDirection">Screen-space direction applied to directional coverage.</param>
    /// <param name="operation">Deposit while covering or remove while revealing the scene.</param>
    /// <param name="easing">Interpolation applied to raw transition progress.</param>
    /// <param name="directionalEdgeSoftness">Normalized half-width of the shader gradient boundary.</param>
    /// <param name="directionalNoiseStrength">Maximum normalized procedural displacement of the boundary.</param>
    /// <param name="directionalNoiseScale">Spatial frequency of the procedural boundary variation.</param>
    /// <param name="paintEdgeSoftness">Normalized half-width of the paint boundary.</param>
    /// <param name="paintNoiseStrength">Broad paint-boundary displacement.</param>
    /// <param name="paintNoiseScale">Broad paint-boundary frequency.</param>
    /// <param name="paintBristleStrength">Fine bristle displacement.</param>
    /// <param name="paintBristleScale">Fine bristle frequency.</param>
    private void Apply(float alpha,
                       bool visible,
                       Color color,
                       GameSceneFadeMode mode,
                       GameSceneFadeWipeDirection wipeDirection,
                       GameUiPaintRevealOperation operation,
                       GameSceneFadeEasing easing,
                       float directionalEdgeSoftness,
                       float directionalNoiseStrength,
                       float directionalNoiseScale,
                       float paintEdgeSoftness,
                       float paintNoiseStrength,
                       float paintNoiseScale,
                       float paintBristleStrength,
                       float paintBristleScale)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        float easedAlpha = easing == GameSceneFadeEasing.SmoothStep
            ? clampedAlpha * clampedAlpha * (3f - 2f * clampedAlpha)
            : clampedAlpha;
        float operationProgress = operation == GameUiPaintRevealOperation.Remove
            ? 1f - easedAlpha
            : easedAlpha;

        bool shaderApplied = false;

        if (fadeImage != null)
        {
            fadeImage.color = color;
            fadeImage.enabled = visible || easedAlpha > 0.001f;
            shaderApplied = ApplyShaderParameters(operationProgress,
                                                  mode,
                                                  wipeDirection,
                                                  operation,
                                                  directionalEdgeSoftness,
                                                  directionalNoiseStrength,
                                                  directionalNoiseScale,
                                                  paintEdgeSoftness,
                                                  paintNoiseStrength,
                                                  paintNoiseScale,
                                                  paintBristleStrength,
                                                  paintBristleScale);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = shaderApplied ? 1f : easedAlpha;
            canvasGroup.blocksRaycasts = visible && easedAlpha > 0.001f;
            canvasGroup.interactable = false;
        }

        TrackOpaqueCoverage(visible && easedAlpha >= OpaqueThreshold);
    }

    /// <summary>
    /// Writes fade progression and directional shaping values to the authored shader material.
    /// </summary>
    /// <param name="progress">Eased transition progress in the 0..1 range.</param>
    /// <param name="mode">Uniform or directional shader coverage mode.</param>
    /// <param name="wipeDirection">Direction used to advance the shader gradient.</param>
    /// <param name="operation">Deposit or removal operation applied to normalized progress.</param>
    /// <param name="directionalEdgeSoftness">Normalized half-width of the shader gradient boundary.</param>
    /// <param name="directionalNoiseStrength">Maximum normalized procedural displacement of the boundary.</param>
    /// <param name="directionalNoiseScale">Spatial frequency of the procedural boundary variation.</param>
    /// <param name="paintEdgeSoftness">Normalized half-width of the paint boundary.</param>
    /// <param name="paintNoiseStrength">Broad paint-boundary displacement.</param>
    /// <param name="paintNoiseScale">Broad paint-boundary frequency.</param>
    /// <param name="paintBristleStrength">Fine bristle displacement.</param>
    /// <param name="paintBristleScale">Fine bristle frequency.</param>
    /// <returns>True when a compatible authored material received every property.</returns>
    private bool ApplyShaderParameters(float progress,
                                       GameSceneFadeMode mode,
                                       GameSceneFadeWipeDirection wipeDirection,
                                       GameUiPaintRevealOperation operation,
                                       float directionalEdgeSoftness,
                                       float directionalNoiseStrength,
                                       float directionalNoiseScale,
                                       float paintEdgeSoftness,
                                       float paintNoiseStrength,
                                       float paintNoiseScale,
                                       float paintBristleStrength,
                                       float paintBristleScale)
    {
        if (!GameUiPaintRevealMaterialUtility.IsCompatible(fadeMaterial))
            return false;

        if (fadeImage.material != fadeMaterial)
            fadeImage.material = fadeMaterial;

        GameUiPaintRevealMaterialUtility.SetProgress(fadeMaterial, progress);
        GameUiPaintRevealMaterialUtility.ConfigureSceneTransition(
            fadeMaterial,
            mode,
            wipeDirection,
            operation,
            directionalEdgeSoftness,
            directionalNoiseStrength,
            directionalNoiseScale,
            paintEdgeSoftness,
            paintNoiseStrength,
            paintNoiseScale,
            paintBristleStrength,
            paintBristleScale,
            (float)Screen.width / Mathf.Max(1f, Screen.height));

        return true;
    }

    /// <summary>
    /// Normalizes the authored Image once and resolves its explicitly assigned gradient material.
    /// </summary>
    private void ConfigureFadeSurface()
    {
        if (fadeImage == null)
            return;

        fadeImage.type = Image.Type.Simple;
        fadeImage.fillAmount = 1f;

        if (fadeMaterial == null)
            fadeMaterial = fadeImage.material;

        if (fadeMaterial != null && fadeImage.material != fadeMaterial)
            fadeImage.material = fadeMaterial;
    }

    /// <summary>
    /// Captures authored shader values before presentation writes transient transition state.
    /// </summary>
    private void CaptureFadeMaterialState()
    {
        if (!GameUiPaintRevealMaterialUtility.IsCompatible(fadeMaterial))
            return;

        capturedFadeMaterial = fadeMaterial;
        capturedFadeMaterialState = GameUiPaintRevealMaterialUtility.Capture(fadeMaterial);
        fadeMaterialStateCaptured = true;
    }

    /// <summary>
    /// Restores authored shader values so Editor execution cannot persist transient fade parameters.
    /// </summary>
    private void RestoreFadeMaterialState()
    {
        if (!fadeMaterialStateCaptured || capturedFadeMaterial == null)
            return;

        GameUiPaintRevealMaterialUtility.Restore(capturedFadeMaterial,
                                                 in capturedFadeMaterialState);

        capturedFadeMaterial = null;
        fadeMaterialStateCaptured = false;
    }

    /// <summary>
    /// Starts a one-render acknowledgement when complete coverage is first submitted and clears it on reveal.
    /// </summary>
    /// <param name="fullyCovered">True when the current shader progress covers the full screen.</param>
    private void TrackOpaqueCoverage(bool fullyCovered)
    {
        if (!fullyCovered)
        {
            ResetOpaqueCoverageState();
            return;
        }

        if (opaqueCoverageApplied)
            return;

        opaqueCoverageApplied = true;
        opaqueCoverageAwaitingRender = true;
        opaqueCoverageRendered = false;
    }

    /// <summary>
    /// Confirms coverage after Unity has rebuilt and submitted the active Canvas for rendering.
    /// </summary>
    private void HandleCanvasWillRender()
    {
        if (!opaqueCoverageAwaitingRender ||
            activeView != this ||
            fadeCanvas == null ||
            !fadeCanvas.isActiveAndEnabled ||
            fadeImage == null ||
            !fadeImage.isActiveAndEnabled)
            return;

        opaqueCoverageAwaitingRender = false;
        opaqueCoverageRendered = true;
    }

    /// <summary>
    /// Clears the render acknowledgement whenever the overlay is revealed, disabled or replaced.
    /// </summary>
    private void ResetOpaqueCoverageState()
    {
        opaqueCoverageApplied = false;
        opaqueCoverageAwaitingRender = false;
        opaqueCoverageRendered = false;
    }

    /// <summary>
    /// Reads the current ECS fade singleton so enabling the view during a transition never resets it to transparent.
    /// </summary>
    /// <returns>True when an ECS fade state was applied.</returns>
    private bool TryApplyCurrentRuntimeState()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        EntityManager entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameSceneFadePresentationState>());
        int entityCount = query.CalculateEntityCount();

        if (entityCount != 1)
        {
            query.Dispose();
            return false;
        }

        Entity entity = query.GetSingletonEntity();
        GameSceneFadePresentationState fadeState = entityManager.GetComponentData<GameSceneFadePresentationState>(entity);
        float4 fadeColor = fadeState.Color;
        query.Dispose();
        Apply(fadeState.Alpha,
              fadeState.Visible != 0,
              new Color(fadeColor.x, fadeColor.y, fadeColor.z, fadeColor.w),
              fadeState.Mode,
              fadeState.WipeDirection,
              fadeState.Operation,
              fadeState.Easing,
              fadeState.DirectionalEdgeSoftness,
              fadeState.DirectionalNoiseStrength,
              fadeState.DirectionalNoiseScale,
              fadeState.PaintEdgeSoftness,
              fadeState.PaintNoiseStrength,
              fadeState.PaintNoiseScale,
              fadeState.PaintBristleStrength,
              fadeState.PaintBristleScale);
        return true;
    }

    /// <summary>
    /// Normalizes the authored overlay canvas so additive scene UI cannot render above the fade.
    /// </summary>
    private void ConfigureCanvas()
    {
        if (transform is RectTransform rectTransform)
            rectTransform.localScale = Vector3.one;

        if (fadeCanvas == null)
            return;

        if (fadeCanvas.worldCamera != null)
            fadeCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        else
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        fadeCanvas.overrideSorting = true;
        fadeCanvas.sortingOrder = MaxFadeSortingOrder;
    }
    #endregion

    #endregion
}
