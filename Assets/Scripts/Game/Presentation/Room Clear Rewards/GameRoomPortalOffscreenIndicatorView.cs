using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents one preauthored screen-edge indicator for a traversable portal outside the camera view.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameRoomPortalOffscreenIndicatorView : MonoBehaviour
{
    #region Constants
    private const float ColorEpsilon = 0.0001f;
    private const float SizeEpsilon = 0.0001f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Preauthored screen-space canvas that owns the portal indicator.")]
    [SerializeField]
    private Canvas indicatorCanvas;

    [Tooltip("Preauthored RectTransform moved and rotated along the nearest screen edge.")]
    [SerializeField]
    private RectTransform indicatorRoot;

    [Tooltip("Preauthored image that displays the configured open-portal indicator sprite.")]
    [SerializeField]
    private Image indicatorImage;
    #endregion

    #region Runtime Fields
    private RectTransform parentRect;
    private Canvas rootCanvas;
    private Sprite appliedSprite;
    private Color appliedColor = Color.clear;
    private float appliedSizePixels = -1f;
    private int appliedSortingOrder = int.MaxValue;
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns the fixed canvas, transform and image created by the explicit presentation setup workflow.
    /// </summary>
    /// <param name="resolvedCanvas">Preauthored screen-space canvas.</param>
    /// <param name="resolvedIndicatorRoot">Preauthored edge-positioned transform.</param>
    /// <param name="resolvedIndicatorImage">Preauthored indicator image.</param>
    public void ConfigureAuthoring(Canvas resolvedCanvas,
                                   RectTransform resolvedIndicatorRoot,
                                   Image resolvedIndicatorImage)
    {
        indicatorCanvas = resolvedCanvas;
        indicatorRoot = resolvedIndicatorRoot;
        indicatorImage = resolvedIndicatorImage;
        parentRect = resolvedIndicatorRoot != null
            ? resolvedIndicatorRoot.parent as RectTransform
            : null;
        rootCanvas = resolvedCanvas;
        Hide();
    }

    /// <summary>
    /// Projects one portal, applies changed visual settings and displays its indicator only while offscreen.
    /// </summary>
    /// <param name="worldPosition">Portal world position including the configured projection offset.</param>
    /// <param name="projectionCamera">Active gameplay camera used for viewport projection.</param>
    /// <param name="config">Baked portal indicator presentation settings.</param>
    /// <returns>True when the portal is outside the camera view and the preauthored indicator is shown.</returns>
    public bool Render(Vector3 worldPosition,
                       Camera projectionCamera,
                       in GameRoomRewardConfig config)
    {
        if (projectionCamera == null ||
            indicatorRoot == null ||
            indicatorImage == null ||
            config.PortalIndicatorsEnabled == 0 ||
            config.PortalIndicatorSprite.Value == null)
        {
            Hide();
            return false;
        }

        Vector3 viewportPosition = projectionCamera.WorldToViewportPoint(worldPosition);

        if (ScreenSpaceOffscreenIndicatorUtility.IsViewportVisible(viewportPosition))
        {
            Hide();
            return false;
        }

        ApplyConfig(config.PortalIndicatorSprite.Value,
                    new Color(config.PortalIndicatorColor.x,
                              config.PortalIndicatorColor.y,
                              config.PortalIndicatorColor.z,
                              config.PortalIndicatorColor.w),
                    config.PortalIndicatorSizePixels,
                    config.PortalIndicatorSortingOrder);

        if (!ScreenSpaceOffscreenIndicatorUtility.TryApplyEdgeTransform(
                indicatorRoot,
                viewportPosition,
                projectionCamera,
                config.PortalIndicatorEdgePaddingPixels,
                config.PortalIndicatorSizePixels,
                ref parentRect,
                ref rootCanvas))
        {
            Hide();
            return false;
        }

        ScreenSpaceOffscreenIndicatorUtility.SetVisible(indicatorRoot, true);
        return true;
    }

    /// <summary>
    /// Hides the preauthored image without disabling its owning canvas or discarding cached visual settings.
    /// </summary>
    public void Hide()
    {
        ScreenSpaceOffscreenIndicatorUtility.SetVisible(indicatorRoot, false);
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Hides stale indicator state whenever the containing managed room instance is enabled.
    /// </summary>
    private void OnEnable()
    {
        Hide();
    }

    /// <summary>
    /// Hides stale indicator state whenever the containing managed room instance is disabled.
    /// </summary>
    private void OnDisable()
    {
        Hide();
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Applies sprite, tint, square dimensions, and behind-HUD sorting only when baked values change.
    /// </summary>
    /// <param name="indicatorSprite">Sprite displayed by the indicator image.</param>
    /// <param name="indicatorColor">Tint applied to the indicator image.</param>
    /// <param name="sizePixels">Square indicator size in screen pixels.</param>
    /// <param name="sortingOrder">Canvas sorting order kept below the primary gameplay HUD.</param>
    private void ApplyConfig(Sprite indicatorSprite,
                             Color indicatorColor,
                             float sizePixels,
                             int sortingOrder)
    {
        if (indicatorCanvas != null && appliedSortingOrder != sortingOrder)
        {
            indicatorCanvas.overrideSorting = true;
            indicatorCanvas.sortingOrder = sortingOrder;
            appliedSortingOrder = sortingOrder;
        }

        if (appliedSprite != indicatorSprite)
        {
            indicatorImage.sprite = indicatorSprite;
            appliedSprite = indicatorSprite;
        }

        if (!ColorsMatch(appliedColor, indicatorColor))
        {
            indicatorImage.color = indicatorColor;
            appliedColor = indicatorColor;
        }

        if (Mathf.Abs(appliedSizePixels - sizePixels) <= SizeEpsilon)
            return;

        ScreenSpaceOffscreenIndicatorUtility.ApplySize(indicatorRoot,
                                                        indicatorImage,
                                                        sizePixels);
        appliedSizePixels = sizePixels;
    }

    /// <summary>
    /// Compares color channels with a small epsilon to avoid repeated UI writes.
    /// </summary>
    /// <param name="left">Previously applied color.</param>
    /// <param name="right">Requested baked color.</param>
    /// <returns>True when all channels are effectively equal.</returns>
    private static bool ColorsMatch(Color left, Color right)
    {
        return math.abs(left.r - right.r) <= ColorEpsilon &&
               math.abs(left.g - right.g) <= ColorEpsilon &&
               math.abs(left.b - right.b) <= ColorEpsilon &&
               math.abs(left.a - right.a) <= ColorEpsilon;
    }
    #endregion

    #endregion
}
