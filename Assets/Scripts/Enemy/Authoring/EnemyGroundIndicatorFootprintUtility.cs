using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Immutable layout resolved for one ground-indicator refresh. The values are expressed in
/// world-space half-axes so the view can drive both the transform scale and the shader inputs
/// from a single computation.
/// </summary>
public readonly struct EnemyGroundIndicatorLayout
{
    #region Fields
    public readonly float ShadowRadiusX;
    public readonly float ShadowRadiusZ;
    public readonly float OuterRadius;
    public readonly float HeightOffset;
    #endregion

    #region Constructor
    /// <summary>
    /// Creates a resolved ground-indicator layout used by the managed view.
    /// </summary>
    /// <param name="shadowRadiusX">Shadow half-axis along world X used by the disc.</param>
    /// <param name="shadowRadiusZ">Shadow half-axis along world Z used by the disc.</param>
    /// <param name="outerRadius">Outermost ring half-axis used to size the mesh and the shader.</param>
    /// <param name="heightOffset">World-space vertical offset applied to the indicator transform.</param>
    public EnemyGroundIndicatorLayout(float shadowRadiusX,
                                      float shadowRadiusZ,
                                      float outerRadius,
                                      float heightOffset)
    {
        ShadowRadiusX = shadowRadiusX;
        ShadowRadiusZ = shadowRadiusZ;
        OuterRadius = outerRadius;
        HeightOffset = heightOffset;
    }
    #endregion
}

/// <summary>
/// Provides footprint sizing and camera-angle resolution helpers used by the enemy ground indicator.
/// Kept separate from the view component so the per-enemy MonoBehaviour stays compact and focused on
/// MaterialPropertyBlock plumbing only.
/// </summary>
public static class EnemyGroundIndicatorFootprintUtility
{
    #region Constants
    private const float MinimumRadius = 0.01f;
    private const float MeshSizeEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Layout
    /// <summary>
    /// Resolves shadow half-axes and the outermost ring radius from baked contact-damage axes and footprint configuration.
    /// The outer radius drives both the mesh transform scale and the shader-side ring radius reference.
    /// </summary>
    /// <param name="contactRadiusX">Contact-damage hit half-axis along world X (the player-damage area).</param>
    /// <param name="contactRadiusZ">Contact-damage hit half-axis along world Z (the player-damage area).</param>
    /// <param name="config">Baked ground-indicator configuration.</param>
    /// <param name="hasShieldRing">True when the shield ring should reserve outer footprint space.</param>
    /// <returns>Resolved ground-indicator layout used by the managed view.</returns>
    public static EnemyGroundIndicatorLayout ResolveLayout(float contactRadiusX,
                                                           float contactRadiusZ,
                                                           in EnemyGroundIndicatorConfig config,
                                                           bool hasShieldRing)
    {
        // Clamp axes so degenerate authored values still produce a visible widget.
        float hitRadiusX = math.max(MinimumRadius, contactRadiusX);
        float hitRadiusZ = math.max(MinimumRadius, contactRadiusZ);
        float ringDistance = math.max(0f, config.RingDistanceFromShadow);
        float ringThickness = math.max(0f, config.RingThickness);
        float ringSpacing = math.max(0f, config.RingSpacing);
        float ringPadding = ResolveOuterRingPadding(ringDistance, ringThickness, ringSpacing, hasShieldRing);

        float shadowRadiusX = hitRadiusX;
        float shadowRadiusZ = hitRadiusZ;

        // ShadowAndSpatialUi mode shrinks the shadow so the outer ring stays within the baked hit footprint.
        if (config.CoverageMode == EnemyShadowCoverageMode.ShadowAndSpatialUi && ringPadding > 0f)
        {
            shadowRadiusX = math.max(MinimumRadius, hitRadiusX - ringPadding);
            shadowRadiusZ = math.max(MinimumRadius, hitRadiusZ - ringPadding);
        }

        // The outermost world-space radius is required to size the mesh quad so the shader UV covers all rings.
        float averageShadowRadius = 0.5f * (shadowRadiusX + shadowRadiusZ);
        float outerRadius = math.max(MinimumRadius, averageShadowRadius + ringPadding);

        return new EnemyGroundIndicatorLayout(shadowRadiusX,
                                              shadowRadiusZ,
                                              outerRadius,
                                              math.max(0f, config.HeightOffset));
    }

    /// <summary>
    /// Resolves the local scale that makes a flat ground-indicator mesh cover the outermost ring diameter.
    /// </summary>
    /// <param name="meshFilter">Mesh filter authored on the indicator transform.</param>
    /// <param name="ownerTransform">Transform receiving the resolved local scale.</param>
    /// <param name="outerRadius">Outer ring half-axis the mesh must span.</param>
    /// <param name="preservedLocalZ">Local Z scale preserved to keep mesh authored thickness on the local up axis.</param>
    /// <returns>Local scale that makes the mesh extents match the outer ring diameter.</returns>
    public static Vector3 ResolveLocalScale(MeshFilter meshFilter,
                                            Transform ownerTransform,
                                            float outerRadius,
                                            float preservedLocalZ)
    {
        Vector3 fallbackScale = new Vector3(math.max(MinimumRadius, outerRadius * 2f),
                                            math.max(MinimumRadius, outerRadius * 2f),
                                            preservedLocalZ);

        if (meshFilter == null || meshFilter.sharedMesh == null)
            return fallbackScale;

        Bounds meshBounds = meshFilter.sharedMesh.bounds;
        Vector3 parentLossyScale = ResolveParentLossyScale(ownerTransform);
        float worldDiameter = math.max(MinimumRadius, outerRadius * 2f);
        float localScaleX = ResolveAxisScale(worldDiameter, meshBounds.size.x, parentLossyScale.x);
        float localScaleY = ResolveAxisScale(worldDiameter, meshBounds.size.y, parentLossyScale.y);
        return new Vector3(localScaleX, localScaleY, preservedLocalZ);
    }

    /// <summary>
    /// Resolves an authored ring arc into a runtime-safe degree value without mutating the source preset.
    /// Non-positive or non-finite values fall back to the full-ring default to keep older presets visible.
    /// </summary>
    /// <param name="authoredRingArcDegrees">Authored angular width in degrees read from the visual footprint preset.</param>
    /// <returns>Runtime ring arc width in degrees, clamped to the supported full-ring maximum.</returns>
    public static float ResolveRuntimeRingArcDegrees(float authoredRingArcDegrees)
    {
        if (!math.isfinite(authoredRingArcDegrees) || authoredRingArcDegrees <= 0f)
            return EnemyVisualFootprintSettings.DefaultRingArcDegrees;

        return math.min(authoredRingArcDegrees, EnemyVisualFootprintSettings.DefaultRingArcDegrees);
    }
    #endregion

    #region Camera Angle
    /// <summary>
    /// Resolves the camera-facing angle in radians on the world XZ plane used by the shader to keep the
    /// depleting ring gap anchored on the away-from-camera side.
    /// </summary>
    /// <param name="enemyPosition">World-space enemy position.</param>
    /// <param name="cameraTransform">Active camera transform.</param>
    /// <param name="fallbackAngleRadians">Angle returned when the camera or vector projection is invalid.</param>
    /// <returns>Camera-facing angle in radians measured on the world XZ plane.</returns>
    public static float ResolveCameraFacingAngleRadians(Vector3 enemyPosition,
                                                        Transform cameraTransform,
                                                        float fallbackAngleRadians)
    {
        if (cameraTransform == null)
            return fallbackAngleRadians;

        Vector3 toCamera = cameraTransform.position - enemyPosition;
        toCamera.y = 0f;

        if (toCamera.sqrMagnitude <= MeshSizeEpsilon)
            return fallbackAngleRadians;

        return math.atan2(toCamera.z, toCamera.x);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Computes how much world-space space the visible rings add outside the shadow outer edge.
    /// </summary>
    /// <param name="ringDistance">Gap between the shadow outer edge and the first ring inner edge.</param>
    /// <param name="ringThickness">World-space radial thickness of each ring band.</param>
    /// <param name="ringSpacing">Gap between health and shield rings.</param>
    /// <param name="hasShieldRing">True when the shield ring contributes to the displayed footprint.</param>
    /// <returns>Outer world-space coverage added by the displayed rings.</returns>
    private static float ResolveOuterRingPadding(float ringDistance, float ringThickness, float ringSpacing, bool hasShieldRing)
    {
        float padding = ringDistance + ringThickness;

        if (hasShieldRing)
            padding += ringSpacing + ringThickness;

        return padding;
    }

    /// <summary>
    /// Resolves the parent lossy scale that affects child local scale in world space.
    /// </summary>
    /// <param name="ownerTransform">Transform whose parent lossy scale is queried.</param>
    /// <returns>Absolute parent lossy scale, or Vector3.one when no parent exists.</returns>
    private static Vector3 ResolveParentLossyScale(Transform ownerTransform)
    {
        if (ownerTransform == null || ownerTransform.parent == null)
            return Vector3.one;

        Vector3 lossyScale = ownerTransform.parent.lossyScale;
        return new Vector3(math.abs(lossyScale.x),
                           math.abs(lossyScale.y),
                           math.abs(lossyScale.z));
    }

    /// <summary>
    /// Converts a desired world-space size into a local scale value using mesh bounds and parent scale.
    /// </summary>
    /// <param name="worldSize">Desired world-space size along the resolved axis.</param>
    /// <param name="meshSize">Mesh-local full size along the same axis.</param>
    /// <param name="parentScale">Absolute parent scale along the same axis.</param>
    /// <returns>Local scale value that produces the desired world-space size.</returns>
    private static float ResolveAxisScale(float worldSize, float meshSize, float parentScale)
    {
        if (meshSize <= MeshSizeEpsilon || parentScale <= MeshSizeEpsilon)
            return math.max(MinimumRadius, worldSize);

        return math.max(MinimumRadius, worldSize / (meshSize * parentScale));
    }
    #endregion

    #endregion
}
