using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders one pooled world-space ring used to preview an upcoming enemy spawn location.
/// </summary>
public sealed class EnemySpawnWarningRingView : MonoBehaviour
{
    #region Constants
    private const int CircleSegmentCount = 40;
    private const float RadiusRebuildEpsilon = 0.001f;
    private const int SortingOrder = 5000;
    #endregion

    #region Fields
    private LineRenderer lineRenderer;
    private float currentRadius = -1f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the internal LineRenderer exists and is configured for runtime warning rendering.
    /// </summary>
    /// <param name="sharedMaterial">Shared material assigned to the pooled LineRenderer.</param>
    public void Initialize(Material sharedMaterial)
    {
        EnsureLineRenderer();

        if (sharedMaterial != null && lineRenderer.sharedMaterial != sharedMaterial)
            lineRenderer.sharedMaterial = sharedMaterial;
    }

    /// <summary>
    /// Renders the warning ring using externally resolved position, scale and opacity.
    /// </summary>
    /// <param name="worldPosition">World position where the warning ring should be displayed.</param>
    /// <param name="radius">Target ring radius in world units.</param>
    /// <param name="ringWidth">Base ring width in world units.</param>
    /// <param name="ringColor">Base ring tint without the final opacity applied.</param>
    /// <param name="opacity">Final alpha applied during this frame.</param>
    /// <param name="widthScale">Additional width multiplier applied during this frame.</param>
    public void Render(Vector3 worldPosition,
                       float radius,
                       float ringWidth,
                       Color ringColor,
                       float opacity,
                       float widthScale)
    {
        EnsureLineRenderer();
        transform.position = worldPosition;
        UpdateCircleGeometry(Mathf.Max(0.05f, radius));
        ApplyVisualState(Mathf.Max(0.01f, ringWidth),
                         ringColor,
                         Mathf.Clamp01(opacity),
                         Mathf.Max(0.01f, widthScale));

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides the pooled view without destroying its cached renderer.
    /// </summary>
    public void Deactivate()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates and configures the internal LineRenderer the first time the pooled view is used.
    /// </summary>
    private void EnsureLineRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = CircleSegmentCount;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 0;
        lineRenderer.numCornerVertices = 3;
        lineRenderer.sortingLayerName = "Default";
        lineRenderer.sortingOrder = SortingOrder;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.lightProbeUsage = LightProbeUsage.Off;
        lineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        lineRenderer.allowOcclusionWhenDynamic = false;
    }

    /// <summary>
    /// Rebuilds the ring geometry only when the target radius actually changes.
    /// </summary>
    /// <param name="radius">Target circle radius in world units.</param>
    private void UpdateCircleGeometry(float radius)
    {
        if (Mathf.Abs(currentRadius - radius) <= RadiusRebuildEpsilon)
            return;

        currentRadius = radius;
        float stepRadians = Mathf.PI * 2f / CircleSegmentCount;

        for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
        {
            float angleRadians = stepRadians * segmentIndex;
            float x = Mathf.Cos(angleRadians) * radius;
            float z = Mathf.Sin(angleRadians) * radius;
            lineRenderer.SetPosition(segmentIndex, new Vector3(x, 0f, z));
        }
    }

    /// <summary>
    /// Applies the final width and opacity for the current frame.
    /// </summary>
    /// <param name="ringWidth">Base ring width in world units.</param>
    /// <param name="ringColor">Base ring tint without the final opacity applied.</param>
    /// <param name="opacity">Final alpha applied during this frame.</param>
    /// <param name="widthScale">Additional width multiplier applied during this frame.</param>
    private void ApplyVisualState(float ringWidth,
                                  Color ringColor,
                                  float opacity,
                                  float widthScale)
    {
        Color currentColor = ringColor;
        currentColor.a = opacity;
        lineRenderer.widthMultiplier = ringWidth * widthScale;
        lineRenderer.startColor = currentColor;
        lineRenderer.endColor = currentColor;
    }
    #endregion

    #endregion
}
