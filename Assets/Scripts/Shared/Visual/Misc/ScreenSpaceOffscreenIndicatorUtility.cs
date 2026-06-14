using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides shared projection, sizing and visibility helpers for screen-edge offscreen indicators.
/// </summary>
public static class ScreenSpaceOffscreenIndicatorUtility
{
    #region Constants
    public const float DefaultCameraResolveIntervalSeconds = 0.5f;
    private const float Epsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a camera without calling Camera.main every frame when an explicit target is not assigned.
    /// </summary>
    /// <param name="currentTime">Current unscaled or elapsed time used to throttle fallback lookup.</param>
    /// <param name="targetCamera">Optional explicitly configured projection camera.</param>
    /// <param name="cachedCamera">Cached fallback camera reused between lookups.</param>
    /// <param name="nextCameraResolveTime">Next time at which fallback camera lookup may run.</param>
    /// <param name="resolveIntervalSeconds">Seconds between fallback camera lookup attempts.</param>
    /// <returns>Active projection camera, or null when unavailable.</returns>
    public static Camera ResolveCamera(float currentTime,
                                       Camera targetCamera,
                                       ref Camera cachedCamera,
                                       ref float nextCameraResolveTime,
                                       float resolveIntervalSeconds)
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
            return targetCamera;

        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera;

        if (currentTime < nextCameraResolveTime)
            return null;

        nextCameraResolveTime = currentTime + Mathf.Max(0.05f, resolveIntervalSeconds);
        cachedCamera = Camera.main;

        if (cachedCamera != null)
            return cachedCamera;

        Camera[] cameras = Camera.allCameras;

        // Fallback to the first active camera when the MainCamera tag is not configured.
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
    /// Resolves whether a projected viewport point is visible in front of the camera.
    /// </summary>
    /// <param name="viewportPosition">Viewport-space position returned by Camera.WorldToViewportPoint.</param>
    /// <returns>True when the point is inside the visible viewport and in front of the camera.</returns>
    public static bool IsViewportVisible(Vector3 viewportPosition)
    {
        return viewportPosition.z > 0f &&
               viewportPosition.x >= 0f &&
               viewportPosition.x <= 1f &&
               viewportPosition.y >= 0f &&
               viewportPosition.y <= 1f;
    }

    /// <summary>
    /// Converts a projected viewport point to a clamped screen-edge position.
    /// </summary>
    /// <param name="viewportPosition">Viewport position from camera projection.</param>
    /// <param name="paddingPixels">Edge padding in screen pixels.</param>
    /// <returns>Screen-space indicator position.</returns>
    public static Vector2 ResolveEdgePosition(Vector3 viewportPosition, float paddingPixels)
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 screenPosition = new Vector2(viewportPosition.x * Screen.width, viewportPosition.y * Screen.height);

        if (viewportPosition.z < 0f)
            screenPosition = screenCenter - (screenPosition - screenCenter);

        Vector2 direction = screenPosition - screenCenter;

        if (direction.sqrMagnitude <= Epsilon)
            direction = Vector2.up;

        float halfWidth = Mathf.Max(0f, Screen.width * 0.5f - paddingPixels);
        float halfHeight = Mathf.Max(0f, Screen.height * 0.5f - paddingPixels);
        float widthScale = Mathf.Abs(direction.x) > Epsilon ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float heightScale = Mathf.Abs(direction.y) > Epsilon ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
        float scale = Mathf.Min(widthScale, heightScale);

        if (float.IsInfinity(scale))
            scale = 1f;

        return screenCenter + direction * Mathf.Max(0f, scale);
    }

    /// <summary>
    /// Applies square dimensions to an offscreen indicator root and image rect only when needed.
    /// </summary>
    /// <param name="indicatorRoot">Indicator root receiving the square size.</param>
    /// <param name="indicatorImage">Indicator image receiving the same square size.</param>
    /// <param name="sizePixels">Requested square indicator size in pixels.</param>
    public static void ApplySize(RectTransform indicatorRoot, Image indicatorImage, float sizePixels)
    {
        float resolvedSize = Mathf.Max(1f, sizePixels);
        Vector2 size = new Vector2(resolvedSize, resolvedSize);

        if (indicatorRoot != null &&
            Vector2.SqrMagnitude(indicatorRoot.sizeDelta - size) > Epsilon)
        {
            indicatorRoot.sizeDelta = size;
        }

        if (indicatorImage == null)
            return;

        RectTransform imageTransform = indicatorImage.rectTransform;

        if (imageTransform == null)
            return;

        if (Vector2.SqrMagnitude(imageTransform.sizeDelta - size) <= Epsilon)
            return;

        imageTransform.sizeDelta = size;
    }

    /// <summary>
    /// Places and rotates one indicator on the nearest screen edge for the supplied viewport position.
    /// </summary>
    /// <param name="indicatorRoot">Indicator root moved and rotated in its parent coordinate space.</param>
    /// <param name="viewportPosition">Viewport-space position returned by Camera.WorldToViewportPoint.</param>
    /// <param name="projectionCamera">Camera used to project the tracked world position.</param>
    /// <param name="edgePaddingPixels">Extra edge padding in screen pixels.</param>
    /// <param name="indicatorSizePixels">Square indicator size used to keep the image fully inside the screen.</param>
    /// <param name="indicatorParentRect">Cached parent RectTransform used as coordinate space.</param>
    /// <param name="rootCanvas">Cached canvas that owns the indicator.</param>
    /// <returns>True when the position could be applied.</returns>
    public static bool TryApplyEdgeTransform(RectTransform indicatorRoot,
                                             Vector3 viewportPosition,
                                             Camera projectionCamera,
                                             float edgePaddingPixels,
                                             float indicatorSizePixels,
                                             ref RectTransform indicatorParentRect,
                                             ref Canvas rootCanvas)
    {
        if (indicatorRoot == null)
            return false;

        float indicatorHalfSizePixels = Mathf.Max(0f, indicatorSizePixels) * 0.5f;
        Vector2 edgePosition = ResolveEdgePosition(viewportPosition, Mathf.Max(0f, edgePaddingPixels) + indicatorHalfSizePixels);

        if (!TryApplyPosition(indicatorRoot,
                              edgePosition,
                              projectionCamera,
                              ref indicatorParentRect,
                              ref rootCanvas))
        {
            return false;
        }

        ApplyRotation(indicatorRoot, edgePosition);
        return true;
    }

    /// <summary>
    /// Toggles one indicator root only when its active state changes.
    /// </summary>
    /// <param name="indicatorRoot">Indicator root to toggle.</param>
    /// <param name="visible">Desired indicator visibility.</param>
    public static void SetVisible(RectTransform indicatorRoot, bool visible)
    {
        if (indicatorRoot == null)
            return;

        GameObject indicatorObject = indicatorRoot.gameObject;

        if (indicatorObject.activeSelf == visible)
            return;

        indicatorObject.SetActive(visible);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies a screen-space position in the correct coordinate space for overlay, camera-space and world-space canvases.
    /// </summary>
    /// <param name="indicatorRoot">Indicator root receiving the resolved position.</param>
    /// <param name="screenPosition">Screen-space indicator position in pixels.</param>
    /// <param name="projectionCamera">Camera used to project the tracked world position.</param>
    /// <param name="indicatorParentRect">Cached parent RectTransform used as coordinate space.</param>
    /// <param name="rootCanvas">Cached canvas that owns the indicator.</param>
    /// <returns>True when the indicator position could be applied.</returns>
    private static bool TryApplyPosition(RectTransform indicatorRoot,
                                         Vector2 screenPosition,
                                         Camera projectionCamera,
                                         ref RectTransform indicatorParentRect,
                                         ref Canvas rootCanvas)
    {
        RectTransform parentRect = ResolveParentRect(indicatorRoot, ref indicatorParentRect);

        if (parentRect == null)
        {
            indicatorRoot.position = screenPosition;
            return true;
        }

        Camera eventCamera = ResolveCanvasEventCamera(projectionCamera, indicatorRoot, ref rootCanvas);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, eventCamera, out Vector2 localPoint))
            return false;

        indicatorRoot.anchoredPosition = ResolveAnchoredPosition(indicatorRoot, parentRect, localPoint);
        return true;
    }

    /// <summary>
    /// Converts a parent-local screen point into the anchored position expected by the indicator RectTransform.
    /// </summary>
    /// <param name="indicatorRoot">Indicator root whose anchors define the reference point.</param>
    /// <param name="parentRect">Parent rect used as the coordinate frame.</param>
    /// <param name="localPoint">Local point returned by RectTransformUtility.</param>
    /// <returns>Anchored position corrected for the indicator anchor reference.</returns>
    private static Vector2 ResolveAnchoredPosition(RectTransform indicatorRoot,
                                                   RectTransform parentRect,
                                                   Vector2 localPoint)
    {
        Vector2 anchorCenter = (indicatorRoot.anchorMin + indicatorRoot.anchorMax) * 0.5f;
        Vector2 anchorReference = new Vector2(Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, anchorCenter.x),
                                              Mathf.Lerp(parentRect.rect.yMin, parentRect.rect.yMax, anchorCenter.y));
        return localPoint - anchorReference;
    }

    /// <summary>
    /// Resolves and caches the parent RectTransform used as the indicator coordinate space.
    /// </summary>
    /// <param name="indicatorRoot">Indicator root whose parent is inspected.</param>
    /// <param name="indicatorParentRect">Cached parent RectTransform used as coordinate space.</param>
    /// <returns>Parent RectTransform when available.</returns>
    private static RectTransform ResolveParentRect(RectTransform indicatorRoot,
                                                   ref RectTransform indicatorParentRect)
    {
        if (indicatorParentRect != null)
            return indicatorParentRect;

        if (indicatorRoot == null || indicatorRoot.parent == null)
            return null;

        indicatorParentRect = indicatorRoot.parent as RectTransform;
        return indicatorParentRect;
    }

    /// <summary>
    /// Resolves the event camera required by RectTransformUtility for the active canvas render mode.
    /// </summary>
    /// <param name="projectionCamera">Camera used as a fallback when the canvas has no explicit world camera.</param>
    /// <param name="indicatorRoot">Indicator root used to recover the owning canvas when needed.</param>
    /// <param name="rootCanvas">Cached canvas that owns the indicator.</param>
    /// <returns>Null for overlay canvas, otherwise the canvas world camera or projection fallback.</returns>
    private static Camera ResolveCanvasEventCamera(Camera projectionCamera,
                                                   RectTransform indicatorRoot,
                                                   ref Canvas rootCanvas)
    {
        Canvas canvas = ResolveRootCanvas(indicatorRoot, ref rootCanvas);

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
    /// Resolves and caches the root canvas that owns one offscreen indicator.
    /// </summary>
    /// <param name="indicatorRoot">Indicator root used to recover the owning canvas.</param>
    /// <param name="rootCanvas">Cached canvas that owns the indicator.</param>
    /// <returns>Canvas owning the indicator, or null when unavailable.</returns>
    private static Canvas ResolveRootCanvas(RectTransform indicatorRoot, ref Canvas rootCanvas)
    {
        if (rootCanvas != null)
            return rootCanvas;

        if (indicatorRoot == null)
            return null;

        rootCanvas = indicatorRoot.GetComponentInParent<Canvas>();
        return rootCanvas;
    }

    /// <summary>
    /// Rotates the offscreen indicator toward the clamped edge direction.
    /// </summary>
    /// <param name="indicatorRoot">Indicator root receiving the rotation.</param>
    /// <param name="edgePosition">Current indicator screen position.</param>
    private static void ApplyRotation(RectTransform indicatorRoot, Vector2 edgePosition)
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 direction = edgePosition - screenCenter;

        if (direction.sqrMagnitude <= Epsilon)
            return;

        float angleDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        indicatorRoot.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
    }
    #endregion

    #endregion
}
