using Unity.Mathematics;

/// <summary>
/// Provides allocation-free selection and constraint math for horizontal camera-boundary footprints.
/// </summary>
public static class GameCameraBoundaryUtility
{
    #region Constants
    private const float BoundaryEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Selection Methods
    /// <summary>
    /// Checks whether a world-space focus point lies inside the boundary footprint while ignoring its height.
    /// </summary>
    /// <param name="boundary">Boundary footprint being tested.</param>
    /// <param name="focusPosition">World-space focus position, normally the local player.</param>
    /// <returns>True when the focus point lies inside the horizontal footprint.</returns>
    public static bool Contains(in GameCameraBoundary boundary, float3 focusPosition)
    {
        float2 localPosition = ToLocal(in boundary, focusPosition);
        return math.abs(localPosition.x) <= boundary.HalfExtents.x + BoundaryEpsilon &&
               math.abs(localPosition.y) <= boundary.HalfExtents.y + BoundaryEpsilon;
    }

    /// <summary>
    /// Calculates footprint area for deterministic overlap selection; smaller volumes win equal-priority ties.
    /// </summary>
    /// <param name="boundary">Boundary whose horizontal footprint is measured.</param>
    /// <returns>Non-negative world-space footprint area.</returns>
    public static float CalculatePlanarArea(in GameCameraBoundary boundary)
    {
        return math.max(0f, boundary.HalfExtents.x * boundary.HalfExtents.y * 4f);
    }
    #endregion

    #region Constraint Methods
    /// <summary>
    /// Compresses a desired camera position through the boundary braking zones while preserving world-space height.
    /// </summary>
    /// <param name="boundary">Active horizontal boundary footprint.</param>
    /// <param name="desiredPosition">Unconstrained world-space camera target.</param>
    /// <param name="softZoneDistance">Authored braking distance before each edge.</param>
    /// <returns>Soft-constrained world-space target.</returns>
    public static float3 ResolveSoftConstrainedPosition(in GameCameraBoundary boundary,
                                                        float3 desiredPosition,
                                                        float softZoneDistance)
    {
        float2 localPosition = ToLocal(in boundary, desiredPosition);
        localPosition.x = SoftClampAxis(localPosition.x,
                                        -boundary.HalfExtents.x,
                                        boundary.HalfExtents.x,
                                        softZoneDistance);
        localPosition.y = SoftClampAxis(localPosition.y,
                                        -boundary.HalfExtents.y,
                                        boundary.HalfExtents.y,
                                        softZoneDistance);
        return ToWorldPreservingHeight(in boundary, localPosition, desiredPosition.y);
    }

    /// <summary>
    /// Applies the non-negotiable horizontal limit after spring integration while preserving world-space height.
    /// </summary>
    /// <param name="boundary">Active horizontal boundary footprint.</param>
    /// <param name="position">Integrated world-space camera position.</param>
    /// <returns>World-space position guaranteed to remain inside the footprint.</returns>
    public static float3 ResolveHardConstrainedPosition(in GameCameraBoundary boundary, float3 position)
    {
        float2 localPosition = ToLocal(in boundary, position);
        localPosition.x = math.clamp(localPosition.x, -boundary.HalfExtents.x, boundary.HalfExtents.x);
        localPosition.y = math.clamp(localPosition.y, -boundary.HalfExtents.y, boundary.HalfExtents.y);
        return ToWorldPreservingHeight(in boundary, localPosition, position.y);
    }

    /// <summary>
    /// Determines when a newly selected boundary may enforce its hard edge without teleporting an outside camera.
    /// The spring remains authoritative until the camera reaches the new footprint, then hard containment resumes.
    /// </summary>
    /// <param name="boundary">Newly selected or currently active horizontal boundary.</param>
    /// <param name="sourcePosition">Camera position before the current spring step.</param>
    /// <param name="candidatePosition">Camera position produced by the current spring step.</param>
    /// <returns>True when hard containment can be applied without an entrance snap.</returns>
    public static bool ShouldApplyHardConstraint(in GameCameraBoundary boundary,
                                                 float3 sourcePosition,
                                                 float3 candidatePosition)
    {
        return Contains(in boundary, sourcePosition) || Contains(in boundary, candidatePosition);
    }

    /// <summary>
    /// Enforces and stabilizes a hard edge only after the camera spring has reached the selected footprint.
    /// </summary>
    /// <param name="boundary">Active horizontal boundary footprint.</param>
    /// <param name="sourcePosition">Camera position before the current spring step.</param>
    /// <param name="candidatePosition">Spring result constrained in place when the footprint is reachable.</param>
    /// <param name="velocity">Persistent spring velocity stabilized in place at reached edges.</param>
    public static void ApplyReachableHardConstraint(in GameCameraBoundary boundary,
                                                    float3 sourcePosition,
                                                    ref float3 candidatePosition,
                                                    ref float3 velocity)
    {
        if (!ShouldApplyHardConstraint(in boundary, sourcePosition, candidatePosition))
            return;

        candidatePosition = ResolveHardConstrainedPosition(in boundary, candidatePosition);
        CancelOutwardVelocity(in boundary, candidatePosition, ref velocity);
    }

    /// <summary>
    /// Removes only spring velocity directed out of an edge already reached by the constrained camera.
    /// </summary>
    /// <param name="boundary">Active horizontal boundary footprint.</param>
    /// <param name="constrainedPosition">Hard-constrained world-space camera position.</param>
    /// <param name="velocity">Persistent world-space spring velocity updated in place.</param>
    public static void CancelOutwardVelocity(in GameCameraBoundary boundary,
                                             float3 constrainedPosition,
                                             ref float3 velocity)
    {
        float2 planarRight = ResolvePlanarRight(in boundary);
        float2 planarForward = ResolvePlanarForward(planarRight);
        float2 localPosition = ToLocal(in boundary, constrainedPosition);
        float2 planarVelocity = new float2(velocity.x, velocity.z);
        float2 localVelocity = new float2(math.dot(planarVelocity, planarRight),
                                          math.dot(planarVelocity, planarForward));

        // Cancel only the component that would push farther through a reached local X edge.
        if (localPosition.x <= -boundary.HalfExtents.x + BoundaryEpsilon && localVelocity.x < 0f)
            localVelocity.x = 0f;
        else if (localPosition.x >= boundary.HalfExtents.x - BoundaryEpsilon && localVelocity.x > 0f)
            localVelocity.x = 0f;

        // Cancel only the component that would push farther through a reached local Z edge.
        if (localPosition.y <= -boundary.HalfExtents.y + BoundaryEpsilon && localVelocity.y < 0f)
            localVelocity.y = 0f;
        else if (localPosition.y >= boundary.HalfExtents.y - BoundaryEpsilon && localVelocity.y > 0f)
            localVelocity.y = 0f;

        planarVelocity = planarRight * localVelocity.x + planarForward * localVelocity.y;
        velocity.x = planarVelocity.x;
        velocity.z = planarVelocity.y;
    }

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
        float2 sourceLocal = ToLocal(in boundary, sourcePosition);

        // A camera already inside malformed or newly enabled authoring may leave without being snapped or trapped.
        if (IsStrictlyInside(sourceLocal, boundary.HalfExtents))
            return desiredPosition;

        float2 desiredLocal = ToLocal(in boundary, desiredPosition);
        float2 movement = desiredLocal - sourceLocal;

        if (!TryGetRayEntry(sourceLocal,
                            movement,
                            boundary.HalfExtents,
                            out float entryDistance,
                            out float2 outwardNormal) ||
            entryDistance < 0f)
        {
            return desiredPosition;
        }

        float softZone = math.max(0f, softZoneDistance);
        bool blocksLocalX = math.abs(outwardNormal.x) > 0.5f;
        float normalSign = blocksLocalX ? outwardNormal.x : outwardNormal.y;
        float hardEdge = normalSign * (blocksLocalX ? boundary.HalfExtents.x : boundary.HalfExtents.y);
        float desiredAxis = blocksLocalX ? desiredLocal.x : desiredLocal.y;
        float outsideDistance = (desiredAxis - hardEdge) * normalSign;

        if (outsideDistance >= softZone)
            return desiredPosition;

        float resolvedDistance = softZone <= BoundaryEpsilon
            ? BoundaryEpsilon
            : softZone * math.exp(-(softZone - outsideDistance) / softZone);

        if (blocksLocalX)
            desiredLocal.x = hardEdge + normalSign * math.max(BoundaryEpsilon, resolvedDistance);
        else
            desiredLocal.y = hardEdge + normalSign * math.max(BoundaryEpsilon, resolvedDistance);

        return ToWorldPreservingHeight(in boundary, desiredLocal, desiredPosition.y);
    }

    /// <summary>
    /// Stops an integrated camera step at the first impassable footprint face and removes only inward velocity.
    /// </summary>
    /// <param name="boundary">Static footprint treated as a planar obstacle.</param>
    /// <param name="sourcePosition">Camera position before spring integration.</param>
    /// <param name="candidatePosition">Integrated camera position constrained in place on intersection.</param>
    /// <param name="velocity">Persistent spring velocity stabilized against the reached face.</param>
    public static void ApplyImpassableHardConstraint(in GameCameraBoundary boundary,
                                                     float3 sourcePosition,
                                                     ref float3 candidatePosition,
                                                     ref float3 velocity)
    {
        float2 sourceLocal = ToLocal(in boundary, sourcePosition);

        if (IsStrictlyInside(sourceLocal, boundary.HalfExtents))
            return;

        float2 candidateLocal = ToLocal(in boundary, candidatePosition);
        float2 movement = candidateLocal - sourceLocal;

        if (!TryGetRayEntry(sourceLocal,
                            movement,
                            boundary.HalfExtents,
                            out float entryDistance,
                            out float2 outwardNormal) ||
            entryDistance < 0f ||
            entryDistance > 1f)
        {
            return;
        }

        float2 constrainedLocal = sourceLocal + movement * entryDistance + outwardNormal * BoundaryEpsilon * 2f;
        candidatePosition = ToWorldPreservingHeight(in boundary, constrainedLocal, candidatePosition.y);
        float2 planarRight = ResolvePlanarRight(in boundary);
        float2 planarForward = ResolvePlanarForward(planarRight);
        float2 worldNormal = planarRight * outwardNormal.x + planarForward * outwardNormal.y;
        float inwardSpeed = math.dot(new float2(velocity.x, velocity.z), worldNormal);

        if (inwardSpeed >= 0f)
            return;

        velocity.x -= worldNormal.x * inwardSpeed;
        velocity.z -= worldNormal.y * inwardSpeed;
    }
    #endregion

    #region Comparison Methods
    /// <summary>
    /// Compares immutable boundary data with a small tolerance so runtime state is rewritten only after real changes.
    /// </summary>
    /// <param name="left">First boundary value.</param>
    /// <param name="right">Second boundary value.</param>
    /// <returns>True when both values describe the same effective footprint.</returns>
    public static bool ApproximatelyEquals(in GameCameraBoundary left, in GameCameraBoundary right)
    {
        return left.Priority == right.Priority &&
               math.distancesq(left.Center, right.Center) <= BoundaryEpsilon * BoundaryEpsilon &&
               math.distancesq(left.HalfExtents, right.HalfExtents) <= BoundaryEpsilon * BoundaryEpsilon &&
               math.distancesq(ResolvePlanarRight(in left), ResolvePlanarRight(in right)) <=
               BoundaryEpsilon * BoundaryEpsilon;
    }
    #endregion

    #region Coordinate Methods
    /// <summary>
    /// Converts a world-space position into the boundary's oriented horizontal coordinates.
    /// </summary>
    /// <param name="boundary">Boundary defining the horizontal coordinate frame.</param>
    /// <param name="worldPosition">World-space position to convert.</param>
    /// <returns>Horizontal position relative to the boundary center and yaw.</returns>
    private static float2 ToLocal(in GameCameraBoundary boundary, float3 worldPosition)
    {
        float2 planarRight = ResolvePlanarRight(in boundary);
        float2 planarForward = ResolvePlanarForward(planarRight);
        float2 delta = new float2(worldPosition.x - boundary.Center.x,
                                  worldPosition.z - boundary.Center.y);
        return new float2(math.dot(delta, planarRight), math.dot(delta, planarForward));
    }

    /// <summary>
    /// Converts boundary-local coordinates to world space and restores the supplied world-space height.
    /// </summary>
    /// <param name="boundary">Boundary defining the horizontal coordinate frame.</param>
    /// <param name="localPosition">Boundary-local horizontal position to convert.</param>
    /// <param name="worldHeight">World-space Y value retained by the planar constraint.</param>
    /// <returns>World-space constrained position.</returns>
    private static float3 ToWorldPreservingHeight(in GameCameraBoundary boundary,
                                                  float2 localPosition,
                                                  float worldHeight)
    {
        float2 planarRight = ResolvePlanarRight(in boundary);
        float2 planarForward = ResolvePlanarForward(planarRight);
        float2 worldPosition = boundary.Center +
                               planarRight * localPosition.x +
                               planarForward * localPosition.y;
        return new float3(worldPosition.x, worldHeight, worldPosition.y);
    }

    /// <summary>
    /// Resolves a normalized local-right axis and provides a deterministic fallback for malformed authored data.
    /// </summary>
    /// <param name="boundary">Boundary containing the baked horizontal orientation.</param>
    /// <returns>Normalized local-right direction on world XZ.</returns>
    private static float2 ResolvePlanarRight(in GameCameraBoundary boundary)
    {
        return math.normalizesafe(boundary.PlanarRight, new float2(1f, 0f));
    }

    /// <summary>
    /// Builds the local-forward axis perpendicular to a normalized horizontal right direction.
    /// </summary>
    /// <param name="planarRight">Normalized local-right direction on world XZ.</param>
    /// <returns>Normalized local-forward direction on world XZ.</returns>
    private static float2 ResolvePlanarForward(float2 planarRight)
    {
        return new float2(-planarRight.y, planarRight.x);
    }

    /// <summary>
    /// Checks whether a local point lies strictly inside an obstacle, leaving edge points on the outside path.
    /// </summary>
    /// <param name="localPosition">Boundary-local point.</param>
    /// <param name="halfExtents">Positive obstacle half extents.</param>
    /// <returns>True when the point must be allowed to escape before blocking resumes.</returns>
    private static bool IsStrictlyInside(float2 localPosition, float2 halfExtents)
    {
        return math.abs(localPosition.x) < halfExtents.x - BoundaryEpsilon &&
               math.abs(localPosition.y) < halfExtents.y - BoundaryEpsilon;
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
        {
            return false;
        }

        if (!UpdateRaySlab(source.y,
                           direction.y,
                           -halfExtents.y,
                           halfExtents.y,
                           new float2(0f, 1f),
                           ref entryDistance,
                           ref exitDistance,
                           ref outwardNormal))
        {
            return false;
        }

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
        if (math.abs(direction) <= BoundaryEpsilon)
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

    /// <summary>
    /// Applies an exponential braking curve continuous at the inner soft-zone boundary and approaching the hard edge.
    /// </summary>
    /// <param name="value">Unconstrained scalar target.</param>
    /// <param name="minimum">Hard minimum edge.</param>
    /// <param name="maximum">Hard maximum edge.</param>
    /// <param name="softZoneDistance">Requested braking distance.</param>
    /// <returns>Soft-constrained scalar target.</returns>
    private static float SoftClampAxis(float value,
                                       float minimum,
                                       float maximum,
                                       float softZoneDistance)
    {
        float softZone = math.min(math.max(0f, softZoneDistance), (maximum - minimum) * 0.5f);

        if (softZone <= BoundaryEpsilon)
            return math.clamp(value, minimum, maximum);

        if (value < minimum + softZone)
        {
            float outwardDistance = minimum + softZone - value;
            return minimum + softZone * math.exp(-outwardDistance / softZone);
        }

        if (value > maximum - softZone)
        {
            float outwardDistance = value - (maximum - softZone);
            return maximum - softZone * math.exp(-outwardDistance / softZone);
        }

        return value;
    }
    #endregion

    #endregion
}
