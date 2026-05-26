using Unity.Mathematics;

/// <summary>
/// Shared geometric helpers for Acid Wanderer trail damage and detached segment lifetime systems.
/// </summary>
public static class EnemyAcidTrailGeometryUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether a world-space point overlaps one acid trail capsule on the XZ plane.
    /// </summary>
    /// <param name="position">World-space point being tested.</param>
    /// <param name="startPosition">World-space start of the trail section.</param>
    /// <param name="endPosition">World-space end of the trail section.</param>
    /// <param name="radius">Planar trail radius.</param>
    /// <returns>True when the point is inside the planar trail capsule.</returns>
    public static bool IsPointOverlappingSection(float3 position,
                                                 float3 startPosition,
                                                 float3 endPosition,
                                                 float radius)
    {
        float safeRadius = math.max(0f, radius);
        return ResolvePlanarDistanceSquaredToSegment(position,
                                                     startPosition,
                                                     endPosition) <= safeRadius * safeRadius;
    }

    /// <summary>
    /// Resolves the squared planar distance between a point and one emitted acid path section.
    /// </summary>
    /// <param name="position">World-space point being tested against the trail section.</param>
    /// <param name="startPosition">World-space start of the emitted trail section.</param>
    /// <param name="endPosition">World-space end of the emitted trail section.</param>
    /// <returns>Squared distance on the XZ plane.</returns>
    public static float ResolvePlanarDistanceSquaredToSegment(float3 position,
                                                              float3 startPosition,
                                                              float3 endPosition)
    {
        float2 sectionStart = startPosition.xz;
        float2 sectionDelta = endPosition.xz - sectionStart;
        float sectionLengthSquared = math.lengthsq(sectionDelta);

        if (sectionLengthSquared <= DirectionEpsilon)
            return math.distancesq(position.xz, sectionStart);

        float normalizedProjection = math.saturate(math.dot(position.xz - sectionStart, sectionDelta) / sectionLengthSquared);
        float2 closestPoint = sectionStart + sectionDelta * normalizedProjection;
        return math.distancesq(position.xz, closestPoint);
    }
    #endregion

    #endregion
}
