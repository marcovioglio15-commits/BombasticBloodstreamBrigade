using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles offscreen boss indicator reference recovery, configuration, projection, and visibility.
/// </summary>
internal static class EnemyBossHudOffscreenIndicatorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves missing offscreen-indicator references from the presenter hierarchy.
    /// </summary>
    /// <param name="ownerTransform">Transform that owns the boss HUD presenter.</param>
    /// <param name="offscreenIndicatorRoot">Root RectTransform moved along the screen edge.</param>
    /// <param name="offscreenIndicatorImage">Image used as the directional indicator.</param>
    /// <param name="indicatorParentRect">Cached parent RectTransform used as coordinate space.</param>
    /// <param name="rootCanvas">Cached canvas that owns the presenter.</param>
    public static void ResolveReferences(Transform ownerTransform,
                                         ref RectTransform offscreenIndicatorRoot,
                                         ref Image offscreenIndicatorImage,
                                         ref RectTransform indicatorParentRect,
                                         ref Canvas rootCanvas)
    {
        if (offscreenIndicatorRoot == null && ownerTransform != null)
            offscreenIndicatorRoot = ownerTransform.Find("OffscreenIndicator") as RectTransform;

        indicatorParentRect = offscreenIndicatorRoot != null ? offscreenIndicatorRoot.parent as RectTransform : null;
        rootCanvas = offscreenIndicatorRoot != null ? offscreenIndicatorRoot.GetComponentInParent<Canvas>() : null;

        if (rootCanvas == null && ownerTransform != null)
            rootCanvas = ownerTransform.GetComponentInParent<Canvas>();

        if (offscreenIndicatorImage == null && offscreenIndicatorRoot != null)
            offscreenIndicatorImage = offscreenIndicatorRoot.GetComponentInChildren<Image>(true);
    }

    /// <summary>
    /// Applies sprite, tint, and dimensions for the offscreen indicator.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read optional managed indicator sprite data.</param>
    /// <param name="bossEntity">Active boss entity that supplies managed visual config.</param>
    /// <param name="offscreenIndicatorRoot">Indicator RectTransform sized in screen pixels.</param>
    /// <param name="offscreenIndicatorImage">Image receiving sprite and tint data.</param>
    /// <param name="indicatorColor">Color resolved from the boss visual preset.</param>
    /// <param name="sizePixels">Square indicator size in screen pixels.</param>
    public static void ApplyConfig(EntityManager entityManager,
                                   Entity bossEntity,
                                   RectTransform offscreenIndicatorRoot,
                                   Image offscreenIndicatorImage,
                                   Color indicatorColor,
                                   float sizePixels)
    {
        if (offscreenIndicatorImage == null)
            return;

        EnemyBossHudPresentationUtility.ApplyImageColor(offscreenIndicatorImage, indicatorColor);
        ScreenSpaceOffscreenIndicatorUtility.ApplySize(offscreenIndicatorRoot, offscreenIndicatorImage, sizePixels);

        if (!entityManager.HasComponent<EnemyBossHudManagedConfig>(bossEntity))
            return;

        EnemyBossHudManagedConfig managedConfig = entityManager.GetComponentObject<EnemyBossHudManagedConfig>(bossEntity);

        if (managedConfig == null || managedConfig.OffscreenIndicatorSprite == null)
            return;

        if (offscreenIndicatorImage.sprite != managedConfig.OffscreenIndicatorSprite)
            offscreenIndicatorImage.sprite = managedConfig.OffscreenIndicatorSprite;
    }

    /// <summary>
    /// Updates offscreen indicator placement and visibility for the selected primary boss.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read the boss transform.</param>
    /// <param name="bossEntity">Active boss entity tracked by the indicator.</param>
    /// <param name="hudConfig">Baked HUD config containing edge padding and size.</param>
    /// <param name="targetCamera">Optional camera configured on the presenter.</param>
    /// <param name="offscreenIndicatorRoot">Indicator root to move and rotate.</param>
    /// <param name="cachedCamera">Cached projection camera reused across frames.</param>
    /// <param name="nextCameraResolveTime">Next unscaled time at which fallback camera lookup may run.</param>
    /// <param name="indicatorParentRect">Cached parent RectTransform used as coordinate space.</param>
    /// <param name="rootCanvas">Cached canvas that owns the presenter.</param>
    public static void Sync(EntityManager entityManager,
                            Entity bossEntity,
                            in EnemyBossHudConfig hudConfig,
                            Camera targetCamera,
                            RectTransform offscreenIndicatorRoot,
                            ref Camera cachedCamera,
                            ref float nextCameraResolveTime,
                            ref RectTransform indicatorParentRect,
                            ref Canvas rootCanvas)
    {
        if (offscreenIndicatorRoot == null)
            return;

        Camera camera = ScreenSpaceOffscreenIndicatorUtility.ResolveCamera(Time.unscaledTime,
                                                                           targetCamera,
                                                                           ref cachedCamera,
                                                                           ref nextCameraResolveTime,
                                                                           ScreenSpaceOffscreenIndicatorUtility.DefaultCameraResolveIntervalSeconds);

        if (camera == null)
        {
            SetVisible(offscreenIndicatorRoot, false);
            return;
        }

        LocalTransform bossTransform = entityManager.GetComponentData<LocalTransform>(bossEntity);
        Vector3 bossPosition = new Vector3(bossTransform.Position.x, bossTransform.Position.y, bossTransform.Position.z);
        Vector3 viewportPosition = camera.WorldToViewportPoint(bossPosition);

        if (ScreenSpaceOffscreenIndicatorUtility.IsViewportVisible(viewportPosition))
        {
            SetVisible(offscreenIndicatorRoot, false);
            return;
        }

        if (!ScreenSpaceOffscreenIndicatorUtility.TryApplyEdgeTransform(offscreenIndicatorRoot,
                                                                        viewportPosition,
                                                                        camera,
                                                                        hudConfig.EdgePaddingPixels,
                                                                        hudConfig.OffscreenIndicatorSizePixels,
                                                                        ref indicatorParentRect,
                                                                        ref rootCanvas))
        {
            SetVisible(offscreenIndicatorRoot, false);
            return;
        }

        SetVisible(offscreenIndicatorRoot, true);
    }

    /// <summary>
    /// Toggles the offscreen indicator root only when its active state changes.
    /// </summary>
    /// <param name="offscreenIndicatorRoot">Indicator root to toggle.</param>
    /// <param name="visible">Desired indicator visibility.</param>
    public static void SetVisible(RectTransform offscreenIndicatorRoot, bool visible)
    {
        ScreenSpaceOffscreenIndicatorUtility.SetVisible(offscreenIndicatorRoot, visible);
    }
    #endregion

    #endregion
}
