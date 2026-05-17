using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime pooled UI view used by enemy projectile offscreen warning presentation.
/// </summary>
internal sealed class EnemyProjectileOffscreenWarningView
{
    #region Constants
    private const float ColorEpsilon = 0.0001f;
    private const float SizeEpsilon = 0.0001f;
    #endregion

    #region Fields
    private readonly RectTransform rootTransform;
    private readonly Image indicatorImage;

    private RectTransform parentRect;
    private Canvas rootCanvas;
    private Entity ownerEntity = Entity.Null;
    private Sprite appliedSprite;
    private Color appliedColor = Color.clear;
    private float appliedSizePixels = -1f;
    #endregion

    #region Properties
    public bool IsValid
    {
        get
        {
            return rootTransform != null && indicatorImage != null;
        }
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Creates a pooled view wrapper around the generated UI GameObject.
    /// </summary>
    /// <param name="rootTransform">Root RectTransform moved along the screen edge.</param>
    /// <param name="indicatorImage">Image used as the warning indicator.</param>
    private EnemyProjectileOffscreenWarningView(RectTransform rootTransform, Image indicatorImage)
    {
        this.rootTransform = rootTransform;
        this.indicatorImage = indicatorImage;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a new runtime UI view under the provided canvas transform.
    /// </summary>
    /// <param name="parentTransform">Runtime canvas transform that owns warning views.</param>
    /// <returns>Created view wrapper, or null when the parent is unavailable.</returns>
    public static EnemyProjectileOffscreenWarningView Create(Transform parentTransform)
    {
        if (parentTransform == null)
            return null;

        GameObject viewObject = new GameObject("EnemyProjectileOffscreenWarningView", typeof(RectTransform), typeof(Image));
        viewObject.hideFlags = HideFlags.HideAndDontSave;
        RectTransform rectTransform = viewObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parentTransform, false);
        ConfigureRect(rectTransform);

        Image image = viewObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        return new EnemyProjectileOffscreenWarningView(rectTransform, image);
    }

    /// <summary>
    /// Reparents a pooled view under the active runtime canvas and clears stale coordinate caches.
    /// </summary>
    /// <param name="parentTransform">Runtime canvas transform that owns warning views.</param>
    public void Initialize(Transform parentTransform)
    {
        if (!IsValid || parentTransform == null)
            return;

        if (rootTransform.parent != parentTransform)
        {
            rootTransform.SetParent(parentTransform, false);
            parentRect = null;
            rootCanvas = null;
        }

        ConfigureRect(rootTransform);
    }

    /// <summary>
    /// Applies static visual data, positions the warning on screen edge, and shows the view.
    /// </summary>
    /// <param name="newOwnerEntity">Enemy shooter that owns the projectile currently using this view.</param>
    /// <param name="indicatorSprite">Sprite used by this warning indicator.</param>
    /// <param name="indicatorColor">Tint applied to the warning indicator.</param>
    /// <param name="sizePixels">Square indicator size in pixels.</param>
    /// <param name="edgePaddingPixels">Extra edge margin in screen pixels.</param>
    /// <param name="viewportPosition">Projectile viewport position.</param>
    /// <param name="projectionCamera">Camera used for projectile projection.</param>
    /// <returns>True when the view could be positioned and shown.</returns>
    public bool Render(Entity newOwnerEntity,
                       Sprite indicatorSprite,
                       Color indicatorColor,
                       float sizePixels,
                       float edgePaddingPixels,
                       Vector3 viewportPosition,
                       Camera projectionCamera)
    {
        if (!IsValid)
            return false;

        ApplyConfig(newOwnerEntity, indicatorSprite, indicatorColor, sizePixels);

        if (!ScreenSpaceOffscreenIndicatorUtility.TryApplyEdgeTransform(rootTransform,
                                                                        viewportPosition,
                                                                        projectionCamera,
                                                                        edgePaddingPixels,
                                                                        sizePixels,
                                                                        ref parentRect,
                                                                        ref rootCanvas))
        {
            Deactivate();
            return false;
        }

        ScreenSpaceOffscreenIndicatorUtility.SetVisible(rootTransform, true);
        return true;
    }

    /// <summary>
    /// Hides the view and clears owner-specific state before it returns to the pool.
    /// </summary>
    public void Deactivate()
    {
        if (!IsValid)
            return;

        ownerEntity = Entity.Null;
        ScreenSpaceOffscreenIndicatorUtility.SetVisible(rootTransform, false);
    }

    /// <summary>
    /// Destroys the runtime GameObject owned by this pooled view.
    /// </summary>
    public void Destroy()
    {
        if (rootTransform == null)
            return;

        Object.DestroyImmediate(rootTransform.gameObject);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies sprite, tint and size only when they changed for the current owner.
    /// </summary>
    /// <param name="newOwnerEntity">Enemy shooter that owns the projectile currently using this view.</param>
    /// <param name="indicatorSprite">Sprite used by this warning indicator.</param>
    /// <param name="indicatorColor">Tint applied to the warning indicator.</param>
    /// <param name="sizePixels">Square indicator size in pixels.</param>
    private void ApplyConfig(Entity newOwnerEntity, Sprite indicatorSprite, Color indicatorColor, float sizePixels)
    {
        if (ownerEntity != newOwnerEntity || appliedSprite != indicatorSprite)
        {
            indicatorImage.sprite = indicatorSprite;
            appliedSprite = indicatorSprite;
            ownerEntity = newOwnerEntity;
        }

        if (!ColorsMatch(appliedColor, indicatorColor))
        {
            indicatorImage.color = indicatorColor;
            appliedColor = indicatorColor;
        }

        if (Mathf.Abs(appliedSizePixels - sizePixels) <= SizeEpsilon)
            return;

        ScreenSpaceOffscreenIndicatorUtility.ApplySize(rootTransform, indicatorImage, sizePixels);
        appliedSizePixels = sizePixels;
    }

    /// <summary>
    /// Applies stable anchors and pivot expected by screen-edge indicator placement.
    /// </summary>
    /// <param name="rectTransform">RectTransform to configure.</param>
    private static void ConfigureRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Compares two colors with a small epsilon so UI tint writes are skipped when unchanged.
    /// </summary>
    /// <param name="left">First color value.</param>
    /// <param name="right">Second color value.</param>
    /// <returns>True when both colors are effectively equal.</returns>
    private static bool ColorsMatch(Color left, Color right)
    {
        return Mathf.Abs(left.r - right.r) <= ColorEpsilon &&
               Mathf.Abs(left.g - right.g) <= ColorEpsilon &&
               Mathf.Abs(left.b - right.b) <= ColorEpsilon &&
               Mathf.Abs(left.a - right.a) <= ColorEpsilon;
    }
    #endregion

    #endregion
}
