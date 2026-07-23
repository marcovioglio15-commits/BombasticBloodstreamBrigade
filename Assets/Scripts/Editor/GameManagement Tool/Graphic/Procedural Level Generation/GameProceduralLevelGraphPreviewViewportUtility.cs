using UnityEngine;

/// <summary>
/// Contains deterministic graph-to-canvas transforms shared by preview rendering and editor smoke checks.
/// </summary>
internal static class GameProceduralLevelGraphPreviewViewportUtility
{
    #region Constants
    private const float MinimumUsableViewportSize = 100f;
    private const float FirstAnchorPosition = 0.24f;
    private const float LastAnchorPosition = 0.76f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a centered fit only after IMGUI has supplied a usable canvas size.
    /// </summary>
    /// <param name="graphBounds">Complete graph bounds in preview world coordinates.</param>
    /// <param name="canvasSize">Current clipped canvas size in local coordinates.</param>
    /// <param name="reservedRightWidth">Width reserved for a visible node inspector.</param>
    /// <param name="padding">Clearance retained around the fitted graph.</param>
    /// <param name="minimumZoom">Smallest permitted preview zoom.</param>
    /// <param name="maximumZoom">Largest permitted preview zoom.</param>
    /// <param name="zoom">Resolved zoom when fitting succeeds.</param>
    /// <param name="panOffset">Resolved local canvas offset when fitting succeeds.</param>
    /// <returns>True when both graph and final canvas dimensions permit a reliable fit.</returns>
    public static bool TryResolveFit(Rect graphBounds,
                                     Vector2 canvasSize,
                                     float reservedRightWidth,
                                     float padding,
                                     float minimumZoom,
                                     float maximumZoom,
                                     out float zoom,
                                     out Vector2 panOffset)
    {
        float availableWidth = canvasSize.x - reservedRightWidth - padding * 2f;
        float availableHeight = canvasSize.y - padding * 2f;

        // Defer fitting during IMGUI layout passes that expose placeholder dimensions.
        if (!IsFinite(graphBounds.width) ||
            !IsFinite(graphBounds.height) ||
            !IsFinite(availableWidth) ||
            !IsFinite(availableHeight) ||
            graphBounds.width <= 0f ||
            graphBounds.height <= 0f ||
            availableWidth < MinimumUsableViewportSize ||
            availableHeight < MinimumUsableViewportSize)
        {
            zoom = 1f;
            panOffset = Vector2.zero;
            return false;
        }

        // Center the complete graph in the unobstructed portion of the local canvas.
        zoom = Mathf.Clamp(Mathf.Min(availableWidth / graphBounds.width,
                                    availableHeight / graphBounds.height),
                           minimumZoom,
                           maximumZoom);
        Vector2 contentCenter = new Vector2(padding + availableWidth * 0.5f,
                                            padding + availableHeight * 0.5f);
        panOffset = contentCenter - graphBounds.center * zoom;
        return true;
    }

    /// <summary>
    /// Transforms one graph world rectangle into the current local canvas coordinate system.
    /// </summary>
    /// <param name="worldRect">Graph world rectangle.</param>
    /// <param name="panOffset">Current local canvas pan offset.</param>
    /// <param name="zoom">Current preview zoom.</param>
    /// <returns>Zoomed and panned local canvas rectangle.</returns>
    public static Rect TransformRect(Rect worldRect, Vector2 panOffset, float zoom)
    {
        return new Rect(panOffset + worldRect.position * zoom, worldRect.size * zoom);
    }

    /// <summary>
    /// Resolves one vertically distributed node-side anchor without merging adjacent edge arrowheads.
    /// </summary>
    /// <param name="nodeRect">Transformed local canvas node rectangle.</param>
    /// <param name="source">True for the right source side; false for the left target side.</param>
    /// <param name="ordinal">Stable edge ordinal on the selected node side.</param>
    /// <param name="count">Total edges sharing the selected node side.</param>
    /// <returns>Local canvas point used by the curve and arrowhead.</returns>
    public static Vector3 ResolveConnectionPoint(Rect nodeRect, bool source, int ordinal, int count)
    {
        float normalizedPosition = count <= 1
            ? 0.5f
            : Mathf.Lerp(FirstAnchorPosition,
                         LastAnchorPosition,
                         Mathf.Clamp(ordinal, 0, count - 1) / (count - 1f));
        return new Vector3(source ? nodeRect.xMax : nodeRect.xMin,
                           Mathf.Lerp(nodeRect.yMin, nodeRect.yMax, normalizedPosition),
                           0f);
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Reports whether one floating-point input can safely participate in viewport arithmetic.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinite.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
