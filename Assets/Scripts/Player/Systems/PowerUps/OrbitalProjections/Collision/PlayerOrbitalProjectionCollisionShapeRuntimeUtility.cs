using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves overlap tests for orbital projections that adapt their collision area to the prefab
/// model silhouette. Uses the per-instance counter-clockwise hull buffer copied at spawn: a cheap
/// bounding-circle broad phase rejects distant targets, then a circle-vs-convex-polygon narrow
/// phase in projection-local XZ space decides the precise hit. Falls back to the plain authored
/// Collision Radius when the silhouette is unavailable.
/// </summary>
public static class PlayerOrbitalProjectionCollisionShapeRuntimeUtility
{
    #region Constants
    private const int MinimumHullVertices = 3;
    private const float MinimumTransformScale = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the broad-phase radius used to gate collision work for one projection.
    /// </summary>
    /// <param name="config">Projection config carrying both radius models.</param>
    /// <returns>Bounding-circle radius in local units (model hull or authored radius).</returns>
    public static float ResolveBroadPhaseRadius(in OrbitalProjectionConfig config)
    {
        if (config.AdaptCollisionToModel != 0 && config.ModelCollisionBoundingRadius > 0f)
            return config.ModelCollisionBoundingRadius;

        return config.CollisionRadius;
    }

    /// <summary>
    /// Checks whether one circle (enemy body, projectile, bomb) overlaps the projection collision
    /// area, using the model silhouette when available and the authored radius otherwise.
    /// </summary>
    /// <param name="config">Projection config carrying collision settings.</param>
    /// <param name="projectionTransform">Projection world transform (position, rotation, scale).</param>
    /// <param name="hullVertices">Per-instance silhouette buffer copied at spawn (may be empty).</param>
    /// <param name="otherPosition">World position of the tested circle center.</param>
    /// <param name="otherRadius">Radius of the tested circle.</param>
    /// <returns>True when the circle overlaps the projection collision area.</returns>
    public static bool OverlapsCircle(in OrbitalProjectionConfig config,
                                      in LocalTransform projectionTransform,
                                      DynamicBuffer<PlayerOrbitalProjectionCollisionVertexElement> hullVertices,
                                      float3 otherPosition,
                                      float otherRadius)
    {
        float scale = math.max(MinimumTransformScale, projectionTransform.Scale);
        float3 delta = otherPosition - projectionTransform.Position;
        delta.y = 0f;

        // Broad phase: bounding circle (hull radius or authored radius) against the other circle.
        float broadRadius = ResolveBroadPhaseRadius(in config) * (config.AdaptCollisionToModel != 0 ? scale : 1f);
        float combinedRadius = math.max(0f, broadRadius) + math.max(0f, otherRadius);

        if (math.lengthsq(delta) > combinedRadius * combinedRadius)
            return false;

        if (config.AdaptCollisionToModel == 0 || hullVertices.Length < MinimumHullVertices)
            return true;

        // Narrow phase: bring the circle center into projection-local XZ space (undo yaw + scale).
        float3 localDelta = math.mul(math.inverse(projectionTransform.Rotation), delta) / scale;
        float2 localCenter = new float2(localDelta.x, localDelta.z);
        float localRadius = math.max(0f, otherRadius) / scale;
        return OverlapsConvexPolygon(hullVertices, localCenter, localRadius);
    }
    #endregion

    #region Polygon Geometry
    /// <summary>
    /// Checks whether one circle overlaps a counter-clockwise convex polygon in 2D.
    /// </summary>
    /// <param name="hullVertices">Counter-clockwise convex hull vertices.</param>
    /// <param name="circleCenter">Circle center in polygon space.</param>
    /// <param name="circleRadius">Circle radius in polygon space.</param>
    /// <returns>True when the circle center lies inside or within radius of any edge.</returns>
    private static bool OverlapsConvexPolygon(DynamicBuffer<PlayerOrbitalProjectionCollisionVertexElement> hullVertices,
                                              float2 circleCenter,
                                              float circleRadius)
    {
        bool isInside = true;
        float closestEdgeDistanceSq = float.MaxValue;

        for (int vertexIndex = 0; vertexIndex < hullVertices.Length; vertexIndex++)
        {
            float2 edgeStart = hullVertices[vertexIndex].LocalPositionXZ;
            float2 edgeEnd = hullVertices[(vertexIndex + 1) % hullVertices.Length].LocalPositionXZ;
            float2 edgeDirection = edgeEnd - edgeStart;
            float2 toCenter = circleCenter - edgeStart;

            // A counter-clockwise polygon keeps interior points on the positive cross side of
            // every edge; one negative side is enough to mark the center as outside.
            if (edgeDirection.x * toCenter.y - edgeDirection.y * toCenter.x < 0f)
                isInside = false;

            // Track squared distance to this edge segment for the outside case.
            float edgeLengthSq = math.lengthsq(edgeDirection);
            float projectionFactor = edgeLengthSq > 0f
                ? math.saturate(math.dot(toCenter, edgeDirection) / edgeLengthSq)
                : 0f;
            float2 closestPoint = edgeStart + edgeDirection * projectionFactor;
            closestEdgeDistanceSq = math.min(closestEdgeDistanceSq, math.lengthsq(circleCenter - closestPoint));
        }

        if (isInside)
            return true;

        return closestEdgeDistanceSq <= circleRadius * circleRadius;
    }
    #endregion

    #endregion
}
