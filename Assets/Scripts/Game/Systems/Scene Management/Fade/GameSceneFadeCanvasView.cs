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
    #endregion

    #region Static
    private static GameSceneFadeCanvasView activeView;
    private static int activeViewVersion;
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the latest fade state to the active authored view when one exists.
    /// </summary>
    /// <param name="alpha">Fade alpha in the 0..1 range.</param>
    /// <param name="visible">True when the overlay GameObject should stay active.</param>
    /// <param name="color">Fade surface color.</param>
    /// <returns>True when a fade view received the state.</returns>
    public static bool TryApply(float alpha, bool visible, Color color)
    {
        GameProceduralTransitionCameraBridge.SetFadePresentationVisible(visible || alpha > 0.001f);

        if (activeView == null)
            return false;

        activeView.Apply(alpha, visible, color);
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

        ConfigureCanvas();
        activeView = this;
        activeViewVersion++;

        if (!TryApplyCurrentRuntimeState())
            Apply(0f, false, Color.black);
    }

    /// <summary>
    /// Clears the active view reference when this overlay is disabled or unloaded.
    /// </summary>
    private void OnDisable()
    {
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
    private void Apply(float alpha, bool visible, Color color)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);

        if (fadeImage != null)
        {
            fadeImage.color = color;
            fadeImage.enabled = visible || clampedAlpha > 0.001f;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = clampedAlpha;
            canvasGroup.blocksRaycasts = visible && clampedAlpha > 0.001f;
            canvasGroup.interactable = false;
        }
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
              new Color(fadeColor.x, fadeColor.y, fadeColor.z, fadeColor.w));
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
        {
            fadeCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        }
        else
        {
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        fadeCanvas.overrideSorting = true;
        fadeCanvas.sortingOrder = MaxFadeSortingOrder;
    }
    #endregion

    #endregion
}
