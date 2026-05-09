using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authored full-screen fade overlay view controlled by the Game Scene Manager presentation system.
/// /params None.
/// /returns None.
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
    [SerializeField] private Canvas fadeCanvas;

    [Tooltip("CanvasGroup that receives fade alpha and blocks raycasts while the overlay is visible.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Image used as the full-screen fade surface.")]
    [SerializeField] private Image fadeImage;
    #endregion

    #region Static
    private static GameSceneFadeCanvasView activeView;
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the latest fade state to the active authored view when one exists.
    /// /params alpha Fade alpha in the 0..1 range.
    /// /params visible True when the overlay GameObject should stay active.
    /// /params color Fade surface color.
    /// /returns True when a fade view received the state.
    /// </summary>
    public static bool TryApply(float alpha, bool visible, Color color)
    {
        if (activeView == null)
            return false;

        activeView.Apply(alpha, visible, color);
        return true;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Caches component references and registers this authored overlay as the active fade view.
    /// /params None.
    /// /returns None.
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
        Apply(0f, false, Color.black);
    }

    /// <summary>
    /// Clears the active view reference when this overlay is disabled or unloaded.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnDisable()
    {
        if (activeView == this)
            activeView = null;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes alpha, color and raycast state to the authored overlay components.
    /// /params alpha Fade alpha in the 0..1 range.
    /// /params visible True when the overlay should be enabled.
    /// /params color Fade surface color.
    /// /returns None.
    /// </summary>
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
    /// Normalizes the authored overlay canvas so additive scene UI cannot render above the fade.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ConfigureCanvas()
    {
        if (transform is RectTransform rectTransform)
            rectTransform.localScale = Vector3.one;

        if (fadeCanvas == null)
            return;

        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.overrideSorting = true;
        fadeCanvas.sortingOrder = MaxFadeSortingOrder;
    }
    #endregion

    #endregion
}
