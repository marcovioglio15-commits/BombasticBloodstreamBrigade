using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles offscreen boss indicator reference recovery, configuration, projection, and visibility.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyBossHudOffscreenIndicatorUtility
{
    #region Constants
    private const float CameraResolveIntervalSeconds = 0.5f;
    private const float Epsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves missing offscreen-indicator references from the presenter hierarchy.
    /// /params ownerTransform Transform that owns the boss HUD presenter.
    /// /params offscreenIndicatorRoot Root RectTransform moved along the screen edge.
    /// /params offscreenIndicatorImage Image used as the directional indicator.
    /// /params indicatorParentRect Cached parent RectTransform used as coordinate space.
    /// /params rootCanvas Cached canvas that owns the presenter.
    /// /returns void.
    /// </summary>
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
    /// /params entityManager Entity manager used to read optional managed indicator sprite data.
    /// /params bossEntity Active boss entity that supplies managed visual config.
    /// /params offscreenIndicatorRoot Indicator RectTransform sized in screen pixels.
    /// /params offscreenIndicatorImage Image receiving sprite and tint data.
    /// /params indicatorColor Color resolved from the boss visual preset.
    /// /params sizePixels Square indicator size in screen pixels.
    /// /returns void.
    /// </summary>
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
        ApplySize(offscreenIndicatorRoot, offscreenIndicatorImage, sizePixels);

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
    /// /params entityManager Entity manager used to read the boss transform.
    /// /params bossEntity Active boss entity tracked by the indicator.
    /// /params hudConfig Baked HUD config containing edge padding and size.
    /// /params targetCamera Optional camera configured on the presenter.
    /// /params offscreenIndicatorRoot Indicator root to move and rotate.
    /// /params cachedCamera Cached projection camera reused across frames.
    /// /params nextCameraResolveTime Next unscaled time at which fallback camera lookup may run.
    /// /params indicatorParentRect Cached parent RectTransform used as coordinate space.
    /// /params rootCanvas Cached canvas that owns the presenter.
    /// /returns void.
    /// </summary>
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

        Camera camera = ResolveCamera(Time.unscaledTime, targetCamera, ref cachedCamera, ref nextCameraResolveTime);

        if (camera == null)
        {
            SetVisible(offscreenIndicatorRoot, false);
            return;
        }

        LocalTransform bossTransform = entityManager.GetComponentData<LocalTransform>(bossEntity);
        Vector3 bossPosition = new Vector3(bossTransform.Position.x, bossTransform.Position.y, bossTransform.Position.z);
        Vector3 viewportPosition = camera.WorldToViewportPoint(bossPosition);
        bool bossIsVisible = viewportPosition.z > 0f &&
                             viewportPosition.x >= 0f &&
                             viewportPosition.x <= 1f &&
                             viewportPosition.y >= 0f &&
                             viewportPosition.y <= 1f;

        if (bossIsVisible)
        {
            SetVisible(offscreenIndicatorRoot, false);
            return;
        }

        float indicatorHalfSizePixels = Mathf.Max(0f, hudConfig.OffscreenIndicatorSizePixels) * 0.5f;
        Vector2 edgePosition = EnemyBossHudPresentationUtility.ResolveEdgePosition(viewportPosition,
                                                                                   Mathf.Max(0f, hudConfig.EdgePaddingPixels) + indicatorHalfSizePixels);
        if (!TryApplyPosition(offscreenIndicatorRoot,
                              edgePosition,
                              camera,
                              ref indicatorParentRect,
                              ref rootCanvas))
        {
            SetVisible(offscreenIndicatorRoot, false);
            return;
        }

        ApplyRotation(offscreenIndicatorRoot, edgePosition);
        SetVisible(offscreenIndicatorRoot, true);
    }

    /// <summary>
    /// Toggles the offscreen indicator root only when its active state changes.
    /// /params offscreenIndicatorRoot Indicator root to toggle.
    /// /params visible Desired indicator visibility.
    /// /returns void.
    /// </summary>
    public static void SetVisible(RectTransform offscreenIndicatorRoot, bool visible)
    {
        if (offscreenIndicatorRoot == null)
            return;

        GameObject indicatorObject = offscreenIndicatorRoot.gameObject;

        if (indicatorObject.activeSelf == visible)
            return;

        indicatorObject.SetActive(visible);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies square dimensions to the offscreen indicator root and image rect only when needed.
    /// /params offscreenIndicatorRoot Indicator root receiving the square size.
    /// /params offscreenIndicatorImage Indicator image receiving the same square size.
    /// /params sizePixels Requested square indicator size in pixels.
    /// /returns void.
    /// </summary>
    private static void ApplySize(RectTransform offscreenIndicatorRoot, Image offscreenIndicatorImage, float sizePixels)
    {
        float resolvedSize = Mathf.Max(1f, sizePixels);
        Vector2 size = new Vector2(resolvedSize, resolvedSize);

        if (offscreenIndicatorRoot != null &&
            Vector2.SqrMagnitude(offscreenIndicatorRoot.sizeDelta - size) > Epsilon)
        {
            offscreenIndicatorRoot.sizeDelta = size;
        }

        if (offscreenIndicatorImage == null)
            return;

        RectTransform imageTransform = offscreenIndicatorImage.rectTransform;

        if (imageTransform == null)
            return;

        if (Vector2.SqrMagnitude(imageTransform.sizeDelta - size) <= Epsilon)
            return;

        imageTransform.sizeDelta = size;
    }

    /// <summary>
    /// Resolves a camera for boss projection without calling Camera.main every frame.
    /// /params currentTime Current unscaled time used to throttle camera lookup.
    /// /params targetCamera Optional explicitly configured projection camera.
    /// /params cachedCamera Cached fallback camera reused between lookups.
    /// /params nextCameraResolveTime Next unscaled time at which fallback camera lookup may run.
    /// /returns Active projection camera, or null when unavailable.
    /// </summary>
    private static Camera ResolveCamera(float currentTime,
                                        Camera targetCamera,
                                        ref Camera cachedCamera,
                                        ref float nextCameraResolveTime)
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
            return targetCamera;

        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera;

        if (currentTime < nextCameraResolveTime)
            return null;

        nextCameraResolveTime = currentTime + CameraResolveIntervalSeconds;
        cachedCamera = Camera.main;

        if (cachedCamera != null)
            return cachedCamera;

        Camera[] cameras = Camera.allCameras;

        for (int index = 0; index < cameras.Length; index++)
        {
            Camera camera = cameras[index];

            if (camera == null || !camera.isActiveAndEnabled)
                continue;

            cachedCamera = camera;
            return cachedCamera;
        }

        return null;
    }

    /// <summary>
    /// Applies the screen-edge position in the correct coordinate space for overlay, camera-space and world-space canvases.
    /// /params offscreenIndicatorRoot Indicator root receiving the resolved position.
    /// /params screenPosition Screen-space indicator position in pixels.
    /// /params projectionCamera Camera used to project the boss into viewport space.
    /// /params indicatorParentRect Cached parent RectTransform used as coordinate space.
    /// /params rootCanvas Cached canvas that owns the presenter.
    /// /returns True when the indicator position could be applied.
    /// </summary>
    private static bool TryApplyPosition(RectTransform offscreenIndicatorRoot,
                                         Vector2 screenPosition,
                                         Camera projectionCamera,
                                         ref RectTransform indicatorParentRect,
                                         ref Canvas rootCanvas)
    {
        RectTransform parentRect = ResolveParentRect(offscreenIndicatorRoot, ref indicatorParentRect);

        if (parentRect == null)
        {
            offscreenIndicatorRoot.position = screenPosition;
            return true;
        }

        Camera eventCamera = ResolveCanvasEventCamera(projectionCamera, offscreenIndicatorRoot, ref rootCanvas);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, eventCamera, out Vector2 localPoint))
            return false;

        offscreenIndicatorRoot.anchoredPosition = ResolveAnchoredPosition(offscreenIndicatorRoot, parentRect, localPoint);
        return true;
    }

    /// <summary>
    /// Converts a parent-local screen point into the anchored position expected by the indicator RectTransform.
    /// /params offscreenIndicatorRoot Indicator root whose anchors define the reference point.
    /// /params parentRect Parent rect used as the coordinate frame.
    /// /params localPoint Local point returned by RectTransformUtility.
    /// /returns Anchored position corrected for the indicator anchor reference.
    /// </summary>
    private static Vector2 ResolveAnchoredPosition(RectTransform offscreenIndicatorRoot,
                                                   RectTransform parentRect,
                                                   Vector2 localPoint)
    {
        Vector2 anchorCenter = (offscreenIndicatorRoot.anchorMin + offscreenIndicatorRoot.anchorMax) * 0.5f;
        Vector2 anchorReference = new Vector2(Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, anchorCenter.x),
                                              Mathf.Lerp(parentRect.rect.yMin, parentRect.rect.yMax, anchorCenter.y));
        return localPoint - anchorReference;
    }

    /// <summary>
    /// Resolves and caches the parent RectTransform used as the indicator coordinate space.
    /// /params offscreenIndicatorRoot Indicator root whose parent is inspected.
    /// /params indicatorParentRect Cached parent RectTransform used as coordinate space.
    /// /returns Parent RectTransform when available.
    /// </summary>
    private static RectTransform ResolveParentRect(RectTransform offscreenIndicatorRoot,
                                                   ref RectTransform indicatorParentRect)
    {
        if (indicatorParentRect != null)
            return indicatorParentRect;

        if (offscreenIndicatorRoot == null || offscreenIndicatorRoot.parent == null)
            return null;

        indicatorParentRect = offscreenIndicatorRoot.parent as RectTransform;
        return indicatorParentRect;
    }

    /// <summary>
    /// Resolves the event camera required by RectTransformUtility for the active canvas render mode.
    /// /params projectionCamera Camera used as a fallback when the canvas has no explicit world camera.
    /// /params offscreenIndicatorRoot Indicator root used to recover the owning canvas when needed.
    /// /params rootCanvas Cached canvas that owns the presenter.
    /// /returns Null for overlay canvas, otherwise the canvas world camera or projection fallback.
    /// </summary>
    private static Camera ResolveCanvasEventCamera(Camera projectionCamera,
                                                   RectTransform offscreenIndicatorRoot,
                                                   ref Canvas rootCanvas)
    {
        Canvas canvas = ResolveRootCanvas(offscreenIndicatorRoot, ref rootCanvas);

        if (canvas == null)
            return projectionCamera;

        switch (canvas.renderMode)
        {
            case RenderMode.ScreenSpaceOverlay:
                return null;
            case RenderMode.ScreenSpaceCamera:
            case RenderMode.WorldSpace:
                if (canvas.worldCamera != null)
                    return canvas.worldCamera;

                return projectionCamera;
            default:
                return projectionCamera;
        }
    }

    /// <summary>
    /// Resolves and caches the root canvas that owns the boss HUD presentation.
    /// /params offscreenIndicatorRoot Indicator root used to recover the owning canvas.
    /// /params rootCanvas Cached canvas that owns the presenter.
    /// /returns Canvas owning the presenter, or null when unavailable.
    /// </summary>
    private static Canvas ResolveRootCanvas(RectTransform offscreenIndicatorRoot, ref Canvas rootCanvas)
    {
        if (rootCanvas != null)
            return rootCanvas;

        if (offscreenIndicatorRoot == null)
            return null;

        rootCanvas = offscreenIndicatorRoot.GetComponentInParent<Canvas>();
        return rootCanvas;
    }

    /// <summary>
    /// Rotates the offscreen indicator toward the clamped edge direction.
    /// /params offscreenIndicatorRoot Indicator root receiving the rotation.
    /// /params edgePosition Current indicator screen position.
    /// /returns void.
    /// </summary>
    private static void ApplyRotation(RectTransform offscreenIndicatorRoot, Vector2 edgePosition)
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 direction = edgePosition - screenCenter;

        if (direction.sqrMagnitude <= Epsilon)
            return;

        float angleDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        offscreenIndicatorRoot.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
    }
    #endregion

    #endregion
}
