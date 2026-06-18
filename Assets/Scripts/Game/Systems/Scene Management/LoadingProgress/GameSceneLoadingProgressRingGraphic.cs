using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a discontinuous circular UI ring with a runtime-controlled normalized fill amount.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class GameSceneLoadingProgressRingGraphic : MaskableGraphic
{
    #region Constants
    private const float MinSegmentDegrees = 0.05f;
    private const float MaxSliceDegrees = 8f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Ring")]
    [Tooltip("Normalized 0..1 fill amount rendered by this segmented ring.")]
    [SerializeField] private float progressNormalized = 1f;

    [Tooltip("Number of disconnected segments used around the ring.")]
    [SerializeField] private int segmentCount = GameSceneLoadingProgressSettings.DefaultSegmentCount;

    [Tooltip("Angular gap in degrees between consecutive segments.")]
    [SerializeField] private float segmentGapDegrees = GameSceneLoadingProgressSettings.DefaultSegmentGapDegrees;

    [Tooltip("Ring thickness in UI pixels.")]
    [SerializeField] private float ringThickness = GameSceneLoadingProgressSettings.DefaultRingThickness;

    [Tooltip("When enabled, the ring fills clockwise from the top of the circle.")]
    [SerializeField] private bool fillClockwise = true;
    #endregion

    #endregion

    #region Properties
    public float ProgressNormalized
    {
        get
        {
            return progressNormalized;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies runtime ring settings and marks geometry dirty only when values actually change.
    /// </summary>
    /// <param name="progressValue">Normalized 0..1 fill amount.</param>
    /// <param name="segmentCountValue">Segment count requested by the Scene Manager preset.</param>
    /// <param name="segmentGapDegreesValue">Segment gap requested by the Scene Manager preset.</param>
    /// <param name="ringThicknessValue">Ring thickness requested by the Scene Manager preset.</param>
    /// <param name="colorValue">Graphic color.</param>
    public void SetPresentation(float progressValue,
                                int segmentCountValue,
                                float segmentGapDegreesValue,
                                float ringThicknessValue,
                                Color colorValue)
    {
        bool geometryChanged = false;
        float clampedProgress = Mathf.Clamp01(progressValue);

        if (Mathf.Abs(progressNormalized - clampedProgress) > 0.0001f)
        {
            progressNormalized = clampedProgress;
            geometryChanged = true;
        }

        if (segmentCount != segmentCountValue)
        {
            segmentCount = segmentCountValue;
            geometryChanged = true;
        }

        if (Mathf.Abs(segmentGapDegrees - segmentGapDegreesValue) > 0.0001f)
        {
            segmentGapDegrees = segmentGapDegreesValue;
            geometryChanged = true;
        }

        if (Mathf.Abs(ringThickness - ringThicknessValue) > 0.0001f)
        {
            ringThickness = ringThicknessValue;
            geometryChanged = true;
        }

        if (color != colorValue)
            color = colorValue;

        if (geometryChanged)
            SetVerticesDirty();
    }
    #endregion

    #region Graphic
    /// <summary>
    /// Generates segmented ring mesh geometry for the current rect transform.
    /// </summary>
    /// <param name="vertexHelper">Unity UI vertex helper receiving mesh data.</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;

        if (outerRadius <= 0f)
            return;

        float resolvedThickness = Mathf.Clamp(ringThickness, 1f, outerRadius);
        float innerRadius = Mathf.Max(0f, outerRadius - resolvedThickness);
        int resolvedSegmentCount = Mathf.Max(3, segmentCount);
        float segmentStepDegrees = 360f / resolvedSegmentCount;
        float resolvedGapDegrees = Mathf.Clamp(segmentGapDegrees, 0f, Mathf.Max(0f, segmentStepDegrees - MinSegmentDegrees));
        float segmentVisibleDegrees = Mathf.Max(MinSegmentDegrees, segmentStepDegrees - resolvedGapDegrees);
        float totalFillDegrees = 360f * Mathf.Clamp01(progressNormalized);
        Vector2 center = rect.center;
        Color32 vertexColor = color;

        for (int index = 0; index < resolvedSegmentCount; index++)
        {
            float segmentStartDegrees = index * segmentStepDegrees + resolvedGapDegrees * 0.5f;
            float remainingFillDegrees = totalFillDegrees - index * segmentStepDegrees;

            if (remainingFillDegrees <= 0f)
                break;

            float segmentDrawDegrees = Mathf.Min(segmentVisibleDegrees, remainingFillDegrees);

            if (segmentDrawDegrees <= 0f)
                continue;

            AddSegment(vertexHelper,
                       center,
                       innerRadius,
                       outerRadius,
                       segmentStartDegrees,
                       segmentStartDegrees + segmentDrawDegrees,
                       vertexColor);
        }
    }
    #endregion

    #region Mesh Helpers
    /// <summary>
    /// Adds one curved ring segment as a set of small quads.
    /// </summary>
    /// <param name="vertexHelper">Unity UI vertex helper receiving mesh data.</param>
    /// <param name="center">Local-space rect center.</param>
    /// <param name="innerRadius">Inner ring radius.</param>
    /// <param name="outerRadius">Outer ring radius.</param>
    /// <param name="startDegrees">Segment start angle in degrees.</param>
    /// <param name="endDegrees">Segment end angle in degrees.</param>
    /// <param name="vertexColor">Color assigned to generated vertices.</param>
    private void AddSegment(VertexHelper vertexHelper,
                            Vector2 center,
                            float innerRadius,
                            float outerRadius,
                            float startDegrees,
                            float endDegrees,
                            Color32 vertexColor)
    {
        float angleSpan = Mathf.Abs(endDegrees - startDegrees);
        int sliceCount = Mathf.Max(1, Mathf.CeilToInt(angleSpan / MaxSliceDegrees));

        for (int sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
        {
            float sliceStart = Mathf.Lerp(startDegrees, endDegrees, (float)sliceIndex / sliceCount);
            float sliceEnd = Mathf.Lerp(startDegrees, endDegrees, (float)(sliceIndex + 1) / sliceCount);
            AddQuad(vertexHelper, center, innerRadius, outerRadius, sliceStart, sliceEnd, vertexColor);
        }
    }

    /// <summary>
    /// Adds one ring-slice quad with consistent winding for Unity UI.
    /// </summary>
    /// <param name="vertexHelper">Unity UI vertex helper receiving mesh data.</param>
    /// <param name="center">Local-space rect center.</param>
    /// <param name="innerRadius">Inner ring radius.</param>
    /// <param name="outerRadius">Outer ring radius.</param>
    /// <param name="startDegrees">Slice start angle in degrees.</param>
    /// <param name="endDegrees">Slice end angle in degrees.</param>
    /// <param name="vertexColor">Color assigned to generated vertices.</param>
    private void AddQuad(VertexHelper vertexHelper,
                         Vector2 center,
                         float innerRadius,
                         float outerRadius,
                         float startDegrees,
                         float endDegrees,
                         Color32 vertexColor)
    {
        int vertexStartIndex = vertexHelper.currentVertCount;
        Vector2 outerStart = ResolvePoint(center, outerRadius, startDegrees);
        Vector2 outerEnd = ResolvePoint(center, outerRadius, endDegrees);
        Vector2 innerEnd = ResolvePoint(center, innerRadius, endDegrees);
        Vector2 innerStart = ResolvePoint(center, innerRadius, startDegrees);
        vertexHelper.AddVert(outerStart, vertexColor, Vector2.zero);
        vertexHelper.AddVert(outerEnd, vertexColor, Vector2.zero);
        vertexHelper.AddVert(innerEnd, vertexColor, Vector2.zero);
        vertexHelper.AddVert(innerStart, vertexColor, Vector2.zero);
        vertexHelper.AddTriangle(vertexStartIndex, vertexStartIndex + 1, vertexStartIndex + 2);
        vertexHelper.AddTriangle(vertexStartIndex, vertexStartIndex + 2, vertexStartIndex + 3);
    }

    /// <summary>
    /// Resolves a point on the ring using top-origin degrees and the configured fill direction.
    /// </summary>
    /// <param name="center">Local-space rect center.</param>
    /// <param name="radius">Circle radius.</param>
    /// <param name="degrees">Top-origin angle in degrees.</param>
    /// <returns>Local-space point on the circle.</returns>
    private Vector2 ResolvePoint(Vector2 center, float radius, float degrees)
    {
        float orientedDegrees = fillClockwise ? 90f - degrees : 90f + degrees;
        float radians = orientedDegrees * Mathf.Deg2Rad;
        return center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
    }
    #endregion

    #endregion
}
