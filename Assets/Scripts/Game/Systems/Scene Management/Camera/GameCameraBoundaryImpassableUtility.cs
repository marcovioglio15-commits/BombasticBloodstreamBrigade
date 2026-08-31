using Unity.Mathematics;

/// <summary>
/// Provides allocation-free ray and constraint math for camera-boundary footprints used as planar obstacles.
/// </summary>
public static class GameCameraBoundaryImpassableUtility
{
    #region Methods

    #region Constraint Methods
    /// <summary>
    /// Brakes a desired camera target before it enters an impassable footprint while preserving tangential motion.
    /// </summary>
    /// <param name="boundary">Static footprint treated as a planar obstacle.</param>
    /// <param name="sourcePosition">Current unshaken camera position.</param>
    /// <param name="desiredPosition">Unconstrained camera target.</param>
    /// <param name="softZoneDistance">World-space braking distance outside the blocking face.</param>
    /// <returns>Target compressed toward the first face approached from outside.</returns>
    public static float3 ResolveSoftBlockedPosition(in GameCameraBoundary boundary,
                                                    float3 sourcePosition,
                                                    float3 desiredPosition,
                                                    float softZoneDistance)
    {
        float2 sourceLocal = GameCameraBoundaryUtility.ToLocal(in boundary, sourcePosition);

        // A camera already inside malformed or newly enabled authoring may leave without being snapped or trapped.
        if (IsStrictlyInside(sourceLocal, boundary.HalfExtents))
            return desiredPosition;

        float2 desiredLocal = GameCameraBoundaryUtility.ToLocal(in boundary, desiredPosition);
        float2 movement = desiredLocal - sourceLocal;

        if (!TryGetRayEntry(sourceLocal,
                            movement,
                            boundary.HalfExtents,
                            out float entryDistance,
                            out float2 outwardNormal) ||
            entryDistance < 0f)
            return desiredPosition;

        float softZone = math.max(0f, softZoneDistance);
        bool blocksLocalX = math.abs(outwardNormal.x) > 0.5f;
        float normalSign = blocksLocalX ? outwardNormal.x : outwardNormal.y;
        float hardEdge = normalSign * (blocksLocalX ? boundary.HalfExtents.x : boundary.HalfExtents.y);
        float desiredAxis = blocksLocalX ? desiredLocal.x : desiredLocal.y;
        float outsideDistance = (desiredAxis - hardEdge) * normalSign;

        if (outsideDistance >= softZone)
            return desiredPosition;

        float resolvedDistance = softZone <= GameCameraBoundaryUtility.BoundaryEpsilon
            ? GameCameraBoundaryUtility.BoundaryEpsilon
            : softZone * math.exp(-(softZone - outsideDistance) / softZone);

        if (blocksLocalX)
            desiredLocal.x = hardEdge + normalSign *
                             math.max(GameCameraBoundaryUtility.BoundaryEpsilon, resolvedDistance);
        else
            desiredLocal.y = hardEdge + normalSign *
                             math.max(GameCameraBoundaryUtility.BoundaryEpsilon, resolvedDistance);

        return GameCameraBoundaryUtility.ToWorldPreservingHeight(in boundary,
                                                                 desiredLocal,
                                                                 desiredPosition.y);
    }

    /// <summary>
    /// Stops an integrated camera step at the first impassable footprint face and removes only inward velocity.
    /// </summary>
    /// <param name="boundary">Static footprint treated as a planar obstacle.</param>
    /// <param name="sourcePosition">Camera position before spring integration.</param>
    /// <param name="candidatePosition">Integrated camera position constrained in place on intersection.</param>
    /// <param name="velocity">Persistent spring velocity stabilized against the reached face.</param>
    public static void ApplyHardConstraint(in GameCameraBoundary boundary,
                                           float3 sourcePosition,
                                           ref float3 candidatePosition,
                                           ref float3 velocity)
    {
        float2 sourceLocal = GameCameraBoundaryUtility.ToLocal(in boundary, sourcePosition);

        if (IsStrictlyInside(sourceLocal, boundary.HalfExtents))
            return;

        float2 candidateLocal = GameCameraBoundaryUtility.ToLocal(in boundary, candidatePosition);
        float2 movement = candidateLocal - sourceLocal;

        if (!TryGetRayEntry(sourceLocal,
                            movement,
                            boundary.HalfExtents,
                            out float entryDistance,
                            out float2 outwardNormal) ||
            entryDistance < 0f ||
            entryDistance > 1f)
            return;

        float2 constrainedLocal = sourceLocal + movement * entryDistance +
                                  outwardNormal * GameCameraBoundaryUtility.BoundaryEpsilon * 2f;
        candidatePosition = GameCameraBoundaryUtility.ToWorldPreservingHeight(in boundary,
                                                                               constrainedLocal,
                                                                               candidatePosition.y);
        float2 planarRight = GameCameraBoundaryUtility.ResolvePlanarRight(in boundary);
        float2 planarForward = GameCameraBoundaryUtility.ResolvePlanarForward(planarRight);
        float2 worldNormal = planarRight * outwardNormal.x + planarForward * outwardNormal.y;
        float inwardSpeed = math.dot(new float2(velocity.x, velocity.z), worldNormal);

        if (inwardSpeed >= 0f)
            return;

        velocity.x -= worldNormal.x * inwardSpeed;
        velocity.z -= worldNormal.y * inwardSpeed;
    }
    #endregion

    #region Intersection Methods
    /// <summary>
    /// Checks whether a local point lies strictly inside an obstacle, leaving edge points on the outside path.
    /// </summary>
    /// <param name="localPosition">Boundary-local point.</param>
    /// <param name="halfExtents">Positive obstacle half extents.</param>
    /// <returns>True when the point must be allowed to escape before blocking resumes.</returns>
    private static bool IsStrictlyInside(float2 localPosition, float2 halfExtents)
    {
        return math.abs(localPosition.x) < halfExtents.x - GameCameraBoundaryUtility.BoundaryEpsilon &&
               math.abs(localPosition.y) < halfExtents.y - GameCameraBoundaryUtility.BoundaryEpsilon;
    }

    /// <summary>
    /// Resolves the first face reached by a ray against a local axis-aligned footprint.
    /// </summary>
    /// <param name="source">Ray origin in boundary-local coordinates.</param>
    /// <param name="direction">Unnormalized ray direction.</param>
    /// <param name="halfExtents">Obstacle half extents.</param>
    /// <param name="entryDistance">Ray parameter at the first blocking face.</param>
    /// <param name="outwardNormal">Local outward normal of the first blocking face.</param>
    /// <returns>True when the ray intersects the footprint in its forward direction.</returns>
    private static bool TryGetRayEntry(float2 source,
                                       float2 direction,
                                       float2 halfExtents,
                                       out float entryDistance,
                                       out float2 outwardNormal)
    {
        entryDistance = -float.MaxValue;
        float exitDistance = float.MaxValue;
        outwardNormal = float2.zero;

        if (!UpdateRaySlab(source.x,
                           direction.x,
                           -halfExtents.x,
                           halfExtents.x,
                           new float2(1f, 0f),
                           ref entryDistance,
                           ref exitDistance,
                           ref outwardNormal))
            return false;

        if (!UpdateRaySlab(source.y,
                           direction.y,
                           -halfExtents.y,
                           halfExtents.y,
                           new float2(0f, 1f),
                           ref entryDistance,
                           ref exitDistance,
                           ref outwardNormal))
            return false;

        return exitDistance >= math.max(0f, entryDistance);
    }

    /// <summary>
    /// Intersects one ray axis and retains the latest near face and earliest far face.
    /// </summary>
    /// <param name="source">Ray origin on the inspected axis.</param>
    /// <param name="direction">Ray direction on the inspected axis.</param>
    /// <param name="minimum">Slab minimum.</param>
    /// <param name="maximum">Slab maximum.</param>
    /// <param name="positiveNormal">Local normal of the positive slab face.</param>
    /// <param name="entryDistance">Current latest near-face parameter.</param>
    /// <param name="exitDistance">Current earliest far-face parameter.</param>
    /// <param name="outwardNormal">Normal replaced when this axis owns the latest near face.</param>
    /// <returns>True when this slab does not reject the ray.</returns>
    private static bool UpdateRaySlab(float source,
                                      float direction,
                                      float minimum,
                                      float maximum,
                                      float2 positiveNormal,
                                      ref float entryDistance,
                                      ref float exitDistance,
                                      ref float2 outwardNormal)
    {
        if (math.abs(direction) <= GameCameraBoundaryUtility.BoundaryEpsilon)
            return source >= minimum && source <= maximum;

        float nearDistance;
        float farDistance;
        float2 nearNormal;

        if (direction > 0f)
        {
            nearDistance = (minimum - source) / direction;
            farDistance = (maximum - source) / direction;
            nearNormal = -positiveNormal;
        }
        else
        {
            nearDistance = (maximum - source) / direction;
            farDistance = (minimum - source) / direction;
            nearNormal = positiveNormal;
        }

        if (nearDistance > entryDistance)
        {
            entryDistance = nearDistance;
            outwardNormal = nearNormal;
        }

        exitDistance = math.min(exitDistance, farDistance);
        return entryDistance <= exitDistance;
    }
    #endregion

    #endregion
}
