using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides allocation-free selection and constraint math for horizontal camera-boundary footprints.
/// </summary>
public static class GameCameraBoundaryUtility
{
    #region Constants
    internal const float BoundaryEpsilon = 0.0001f;
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
    /// Checks whether a world-space point lies inside any member of an active compound containment group.
    /// </summary>
    /// <param name="boundaries">Active same-priority overlapping containment group.</param>
    /// <param name="focusPosition">World-space point tested against the group union.</param>
    /// <returns>True when at least one member contains the horizontal point.</returns>
    public static bool Contains(DynamicBuffer<GameCameraBoundaryContainmentElement> boundaries,
                                float3 focusPosition)
    {
        // Stop on the first containing footprint because the group represents their geometric union.
        for (int boundaryIndex = 0; boundaryIndex < boundaries.Length; boundaryIndex++)
        {
            GameCameraBoundary boundary = boundaries[boundaryIndex].Boundary;

            if (Contains(in boundary, focusPosition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether an entity already belongs to an active compound containment group.
    /// </summary>
    /// <param name="boundaries">Active containment group membership buffer.</param>
    /// <param name="boundaryEntity">Boundary entity searched in the group.</param>
    /// <returns>True when the entity is already registered as a group member.</returns>
    public static bool ContainsEntity(DynamicBuffer<GameCameraBoundaryContainmentElement> boundaries,
                                      Entity boundaryEntity)
    {
        // Membership checks run only during selection and rare group rebuilds.
        for (int boundaryIndex = 0; boundaryIndex < boundaries.Length; boundaryIndex++)
        {
            if (boundaries[boundaryIndex].BoundaryEntity == boundaryEntity)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tests whether two oriented footprints share positive planar area and can form one continuous containment group.
    /// Edge-only contact is intentionally excluded because it cannot provide a stable camera passage.
    /// </summary>
    /// <param name="left">First oriented boundary footprint.</param>
    /// <param name="right">Second oriented boundary footprint.</param>
    /// <returns>True when the footprint interiors overlap on every separating axis.</returns>
    public static bool Overlaps(in GameCameraBoundary left, in GameCameraBoundary right)
    {
        float2 leftPlanarRight = ResolvePlanarRight(in left);
        float2 leftPlanarForward = ResolvePlanarForward(leftPlanarRight);
        float2 rightPlanarRight = ResolvePlanarRight(in right);
        float2 rightPlanarForward = ResolvePlanarForward(rightPlanarRight);
        float2 centerDelta = right.Center - left.Center;

        // Oriented rectangles can separate only on one of their four local axes.
        return HasPositiveOverlapOnAxis(centerDelta,
                                        leftPlanarRight,
                                        in left,
                                        leftPlanarRight,
                                        leftPlanarForward,
                                        in right,
                                        rightPlanarRight,
                                        rightPlanarForward) &&
               HasPositiveOverlapOnAxis(centerDelta,
                                        leftPlanarForward,
                                        in left,
                                        leftPlanarRight,
                                        leftPlanarForward,
                                        in right,
                                        rightPlanarRight,
                                        rightPlanarForward) &&
               HasPositiveOverlapOnAxis(centerDelta,
                                        rightPlanarRight,
                                        in left,
                                        leftPlanarRight,
                                        leftPlanarForward,
                                        in right,
                                        rightPlanarRight,
                                        rightPlanarForward) &&
               HasPositiveOverlapOnAxis(centerDelta,
                                        rightPlanarForward,
                                        in left,
                                        leftPlanarRight,
                                        leftPlanarForward,
                                        in right,
                                        rightPlanarRight,
                                        rightPlanarForward);
    }

    /// <summary>
    /// Checks whether two footprints can share one containment group without weakening authored priority overrides.
    /// </summary>
    /// <param name="left">First boundary supplying geometry and selection priority.</param>
    /// <param name="right">Second boundary supplying geometry and selection priority.</param>
    /// <returns>True when both priorities match and the footprint interiors overlap.</returns>
    public static bool CanShareContainmentGroup(in GameCameraBoundary left,
                                                in GameCameraBoundary right)
    {
        return left.Priority == right.Priority && Overlaps(in left, in right);
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
    /// Compresses a desired camera position only against the external edge of a compound containment group.
    /// Internal edges shared by overlapping members remain fully traversable.
    /// </summary>
    /// <param name="boundaries">Active same-priority overlapping containment group.</param>
    /// <param name="desiredPosition">Unconstrained world-space camera target.</param>
    /// <param name="softZoneDistance">Authored braking distance before external edges.</param>
    /// <returns>Soft-constrained target inside the closest group member, or the original target when already inside the union.</returns>
    public static float3 ResolveSoftConstrainedPosition(
        DynamicBuffer<GameCameraBoundaryContainmentElement> boundaries,
        float3 desiredPosition,
        float softZoneDistance)
    {
        if (boundaries.Length == 0)
            return desiredPosition;

        float closestDistanceSquared = float.MaxValue;
        float3 closestPosition = desiredPosition;

        // Select the least-displacing member projection so only the union's nearest external edge brakes the target.
        for (int boundaryIndex = 0; boundaryIndex < boundaries.Length; boundaryIndex++)
        {
            GameCameraBoundary boundary = boundaries[boundaryIndex].Boundary;
            float3 constrainedPosition = ResolveSoftConstrainedPosition(in boundary,
                                                                        desiredPosition,
                                                                        softZoneDistance);
            float distanceSquared = PlanarDistanceSquared(constrainedPosition, desiredPosition);

            if (distanceSquared >= closestDistanceSquared)
                continue;

            closestDistanceSquared = distanceSquared;
            closestPosition = constrainedPosition;
        }

        return closestPosition;
    }

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
    /// Projects a camera position to the closest point of a compound containment union while preserving height.
    /// </summary>
    /// <param name="boundaries">Active same-priority overlapping containment group.</param>
    /// <param name="position">Integrated world-space camera position.</param>
    /// <returns>Original position when it is inside any member, otherwise the closest member projection.</returns>
    public static float3 ResolveHardConstrainedPosition(
        DynamicBuffer<GameCameraBoundaryContainmentElement> boundaries,
        float3 position)
    {
        if (boundaries.Length == 0 || Contains(boundaries, position))
            return position;

        float closestDistanceSquared = float.MaxValue;
        float3 closestPosition = position;

        // The closest member projection is the closest valid point on the compound union.
        for (int boundaryIndex = 0; boundaryIndex < boundaries.Length; boundaryIndex++)
        {
            GameCameraBoundary boundary = boundaries[boundaryIndex].Boundary;
            float3 constrainedPosition = ResolveHardConstrainedPosition(in boundary, position);
            float distanceSquared = PlanarDistanceSquared(constrainedPosition, position);

            if (distanceSquared >= closestDistanceSquared)
                continue;

            closestDistanceSquared = distanceSquared;
            closestPosition = constrainedPosition;
        }

        return closestPosition;
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
    /// Determines when a compound group may enforce its external hard edge without teleporting an outside camera.
    /// </summary>
    /// <param name="boundaries">Active same-priority overlapping containment group.</param>
    /// <param name="sourcePosition">Camera position before the current spring step.</param>
    /// <param name="candidatePosition">Camera position produced by the current spring step.</param>
    /// <returns>True when either end of the spring step has reached the group union.</returns>
    public static bool ShouldApplyHardConstraint(
        DynamicBuffer<GameCameraBoundaryContainmentElement> boundaries,
        float3 sourcePosition,
        float3 candidatePosition)
    {
        return Contains(boundaries, sourcePosition) || Contains(boundaries, candidatePosition);
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
    /// Enforces only the external hard edge of a reached compound containment group and stabilizes its spring velocity.
    /// </summary>
    /// <param name="boundaries">Active same-priority overlapping containment group.</param>
    /// <param name="sourcePosition">Camera position before the current spring step.</param>
    /// <param name="candidatePosition">Spring result constrained in place when the group is reachable.</param>
    /// <param name="velocity">Persistent spring velocity stabilized only at external edges.</param>
    public static void ApplyReachableHardConstraint(
        DynamicBuffer<GameCameraBoundaryContainmentElement> boundaries,
        float3 sourcePosition,
        ref float3 candidatePosition,
        ref float3 velocity)
    {
        // An entering spring remains authoritative, while an already-contained spring stops at its first union exit.
        if (!Contains(boundaries, sourcePosition) || Contains(boundaries, candidatePosition))
            return;

        candidatePosition = ResolveFirstExternalEdge(boundaries, sourcePosition, candidatePosition);
        CancelOutwardVelocity(boundaries, candidatePosition, ref velocity);
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
    /// Removes spring velocity only when every member containing the reached point blocks that direction.
    /// This preserves motion through shared seams and around overlapping corners.
    /// </summary>
    /// <param name="boundaries">Active same-priority overlapping containment group.</param>
    /// <param name="constrainedPosition">Hard-constrained world-space camera position.</param>
    /// <param name="velocity">Persistent world-space spring velocity updated in place.</param>
    public static void CancelOutwardVelocity(
        DynamicBuffer<GameCameraBoundaryContainmentElement> boundaries,
        float3 constrainedPosition,
        ref float3 velocity)
    {
        float3 sourceVelocity = velocity;
        float3 leastConstrainedVelocity = velocity;
        float greatestRetainedPlanarSpeedSquared = -1f;

        // Any member that permits the direction keeps it valid for the compound union.
        for (int boundaryIndex = 0; boundaryIndex < boundaries.Length; boundaryIndex++)
        {
            GameCameraBoundary boundary = boundaries[boundaryIndex].Boundary;

            if (!Contains(in boundary, constrainedPosition))
                continue;

            float3 candidateVelocity = sourceVelocity;
            CancelOutwardVelocity(in boundary, constrainedPosition, ref candidateVelocity);
            float retainedPlanarSpeedSquared = candidateVelocity.x * candidateVelocity.x +
                                               candidateVelocity.z * candidateVelocity.z;

            if (retainedPlanarSpeedSquared <= greatestRetainedPlanarSpeedSquared)
                continue;

            greatestRetainedPlanarSpeedSquared = retainedPlanarSpeedSquared;
            leastConstrainedVelocity = candidateVelocity;
        }

        if (greatestRetainedPlanarSpeedSquared >= 0f)
            velocity = leastConstrainedVelocity;
    }

    /// <summary>
    /// Resolves the first external edge crossed by a segment that starts inside a compound containment group.
    /// Boundary intersection intervals are merged transitively so internal overlap seams never stop the camera.
    /// </summary>
    /// <param name="boundaries">Active same-priority overlapping containment group.</param>
    /// <param name="sourcePosition">Contained camera position before spring integration.</param>
    /// <param name="candidatePosition">Integrated camera position outside the group union.</param>
    /// <returns>World-space position on the first external edge reached along the spring segment.</returns>
    private static float3 ResolveFirstExternalEdge(
        DynamicBuffer<GameCameraBoundaryContainmentElement> boundaries,
        float3 sourcePosition,
        float3 candidatePosition)
    {
        float reachableExit = 0f;

        // Repeated interval expansion follows every overlapping member reachable without crossing an external gap.
        for (int expansionIndex = 0; expansionIndex < boundaries.Length; expansionIndex++)
        {
            float previousReachableExit = reachableExit;

            for (int boundaryIndex = 0; boundaryIndex < boundaries.Length; boundaryIndex++)
            {
                GameCameraBoundary boundary = boundaries[boundaryIndex].Boundary;

                if (!TryGetSegmentInterval(in boundary,
                                           sourcePosition,
                                           candidatePosition,
                                           out float entryDistance,
                                           out float exitDistance) ||
                    entryDistance > reachableExit + BoundaryEpsilon ||
                    exitDistance <= reachableExit)
                    continue;

                reachableExit = math.min(1f, exitDistance);
            }

            if (reachableExit >= 1f - BoundaryEpsilon ||
                reachableExit <= previousReachableExit + BoundaryEpsilon)
                break;
        }

        return math.lerp(sourcePosition, candidatePosition, math.saturate(reachableExit));
    }

    /// <summary>
    /// Calculates the normalized segment interval contained by one oriented boundary footprint.
    /// </summary>
    /// <param name="boundary">Boundary intersected by the world-space segment.</param>
    /// <param name="sourcePosition">World-space segment origin.</param>
    /// <param name="candidatePosition">World-space segment end.</param>
    /// <param name="entryDistance">Normalized parameter at which the segment enters the footprint.</param>
    /// <param name="exitDistance">Normalized parameter at which the segment exits the footprint.</param>
    /// <returns>True when the segment intersects the footprint between its endpoints.</returns>
    private static bool TryGetSegmentInterval(in GameCameraBoundary boundary,
                                              float3 sourcePosition,
                                              float3 candidatePosition,
                                              out float entryDistance,
                                              out float exitDistance)
    {
        float2 sourceLocal = ToLocal(in boundary, sourcePosition);
        float2 candidateLocal = ToLocal(in boundary, candidatePosition);
        float2 direction = candidateLocal - sourceLocal;
        entryDistance = 0f;
        exitDistance = 1f;

        return UpdateSegmentSlab(sourceLocal.x,
                                 direction.x,
                                 -boundary.HalfExtents.x,
                                 boundary.HalfExtents.x,
                                 ref entryDistance,
                                 ref exitDistance) &&
               UpdateSegmentSlab(sourceLocal.y,
                                 direction.y,
                                 -boundary.HalfExtents.y,
                                 boundary.HalfExtents.y,
                                 ref entryDistance,
                                 ref exitDistance);
    }

    /// <summary>
    /// Intersects one segment axis with a local boundary slab and narrows its normalized containment interval.
    /// </summary>
    /// <param name="source">Segment origin on the inspected local axis.</param>
    /// <param name="direction">Segment displacement on the inspected local axis.</param>
    /// <param name="minimum">Local slab minimum.</param>
    /// <param name="maximum">Local slab maximum.</param>
    /// <param name="entryDistance">Latest normalized entry parameter updated in place.</param>
    /// <param name="exitDistance">Earliest normalized exit parameter updated in place.</param>
    /// <returns>True when the inspected slab retains a non-empty segment interval.</returns>
    private static bool UpdateSegmentSlab(float source,
                                          float direction,
                                          float minimum,
                                          float maximum,
                                          ref float entryDistance,
                                          ref float exitDistance)
    {
        if (math.abs(direction) <= BoundaryEpsilon)
            return source >= minimum - BoundaryEpsilon && source <= maximum + BoundaryEpsilon;

        float inverseDirection = 1f / direction;
        float firstDistance = (minimum - source) * inverseDirection;
        float secondDistance = (maximum - source) * inverseDirection;

        if (firstDistance > secondDistance)
        {
            float distanceSwap = firstDistance;
            firstDistance = secondDistance;
            secondDistance = distanceSwap;
        }

        entryDistance = math.max(entryDistance, firstDistance);
        exitDistance = math.min(exitDistance, secondDistance);
        return entryDistance <= exitDistance + BoundaryEpsilon;
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

    /// <summary>
    /// Tests one separating axis using the projected radii of two oriented boundary footprints.
    /// </summary>
    /// <param name="centerDelta">Vector from the first footprint center to the second.</param>
    /// <param name="axis">Normalized axis receiving both footprint projections.</param>
    /// <param name="left">First boundary supplying its half extents.</param>
    /// <param name="leftPlanarRight">Normalized local-right axis of the first boundary.</param>
    /// <param name="leftPlanarForward">Normalized local-forward axis of the first boundary.</param>
    /// <param name="right">Second boundary supplying its half extents.</param>
    /// <param name="rightPlanarRight">Normalized local-right axis of the second boundary.</param>
    /// <param name="rightPlanarForward">Normalized local-forward axis of the second boundary.</param>
    /// <returns>True when the footprint interiors overlap on the inspected axis.</returns>
    private static bool HasPositiveOverlapOnAxis(float2 centerDelta,
                                                 float2 axis,
                                                 in GameCameraBoundary left,
                                                 float2 leftPlanarRight,
                                                 float2 leftPlanarForward,
                                                 in GameCameraBoundary right,
                                                 float2 rightPlanarRight,
                                                 float2 rightPlanarForward)
    {
        float leftRadius = math.abs(math.dot(axis, leftPlanarRight)) * left.HalfExtents.x +
                           math.abs(math.dot(axis, leftPlanarForward)) * left.HalfExtents.y;
        float rightRadius = math.abs(math.dot(axis, rightPlanarRight)) * right.HalfExtents.x +
                            math.abs(math.dot(axis, rightPlanarForward)) * right.HalfExtents.y;
        return math.abs(math.dot(centerDelta, axis)) < leftRadius + rightRadius - BoundaryEpsilon;
    }

    /// <summary>
    /// Calculates horizontal squared distance without allowing preserved camera height to affect footprint selection.
    /// </summary>
    /// <param name="left">First world-space camera position.</param>
    /// <param name="right">Second world-space camera position.</param>
    /// <returns>Squared distance on the world XZ plane.</returns>
    private static float PlanarDistanceSquared(float3 left, float3 right)
    {
        float2 delta = new float2(left.x - right.x, left.z - right.z);
        return math.lengthsq(delta);
    }
    #endregion

    #region Coordinate Methods
    /// <summary>
    /// Converts a world-space position into the boundary's oriented horizontal coordinates.
    /// </summary>
    /// <param name="boundary">Boundary defining the horizontal coordinate frame.</param>
    /// <param name="worldPosition">World-space position to convert.</param>
    /// <returns>Horizontal position relative to the boundary center and yaw.</returns>
    internal static float2 ToLocal(in GameCameraBoundary boundary, float3 worldPosition)
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
    internal static float3 ToWorldPreservingHeight(in GameCameraBoundary boundary,
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
    internal static float2 ResolvePlanarRight(in GameCameraBoundary boundary)
    {
        return math.normalizesafe(boundary.PlanarRight, new float2(1f, 0f));
    }

    /// <summary>
    /// Builds the local-forward axis perpendicular to a normalized horizontal right direction.
    /// </summary>
    /// <param name="planarRight">Normalized local-right direction on world XZ.</param>
    /// <returns>Normalized local-forward direction on world XZ.</returns>
    internal static float2 ResolvePlanarForward(float2 planarRight)
    {
        return new float2(-planarRight.y, planarRight.x);
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
