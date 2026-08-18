using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Provides shared geometry and runtime helpers for the player Laser Beam override.
/// </summary>
public static class PlayerLaserBeamUtility
{
    #region Constants
    internal const float BaseProjectileRadius = 0.05f;
    internal const float MinimumTravelDistance = 0.02f;
    internal const float MinimumCollisionRadius = 0.01f;
    internal const float SurfacePushDistance = 0.01f;
    internal const float DefaultUnboundedBeamDistance = 80f;
    public const float MaximumSupportedTravelDistance = 256f;
    public const float MaximumSupportedCollisionRadius = 8f;
    public const float MaximumSupportedBodyWidth = 12f;
    public const int MaximumSupportedBounceSegments = 12;
    private const float DirectionEpsilon = 1e-6f;
    private const int MaximumMuzzleParentHops = 24;
    #endregion

    #region Methods

    #region Public Methods
    #region Current-Frame Origin
    /// <summary>
    /// Resolves the Laser Beam origin from the latest player transform and baked muzzle hierarchy data.
    /// This avoids reading child LocalToWorld inside the controller group, where transform-system output can still
    /// represent the previous frame while the player LocalTransform has already advanced.
    /// </summary>
    /// <param name="playerEntity">Player entity that owns the active Laser Beam.</param>
    /// <param name="playerTransform">Current player transform after controller movement and look rotation.</param>
    /// <param name="runtimeShootingConfig">Runtime shooting config that provides the authored local shoot offset.</param>
    /// <param name="muzzleLookup">Lookup used to read the baked muzzle anchor entity.</param>
    /// <param name="transformLookup">Lookup used to read local transforms along the muzzle hierarchy.</param>
    /// <param name="parentLookup">Lookup used to climb from the muzzle anchor back to the player entity.</param>
    /// <returns>Current-frame world-space Laser Beam spawn position.</returns>
    internal static float3 ResolveCurrentFrameSpawnPosition(Entity playerEntity,
                                                            in LocalTransform playerTransform,
                                                            in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                            in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                                            in ComponentLookup<LocalTransform> transformLookup,
                                                            in ComponentLookup<Parent> parentLookup)
    {
        float3 shootOffset = runtimeShootingConfig.ShootOffset;

        if (TryResolveCurrentFrameMuzzlePose(playerEntity,
                                             in playerTransform,
                                             in muzzleLookup,
                                             in transformLookup,
                                             in parentLookup,
                                             out float3 muzzlePosition,
                                             out quaternion muzzleRotation))
            return muzzlePosition + math.rotate(muzzleRotation, shootOffset);

        return playerTransform.Position + math.rotate(playerTransform.Rotation, shootOffset);
    }
    #endregion

    #region Runtime Geometry
    /// <summary>
    /// Resolves the current Laser Beam travel budget from active time, virtual projectile speed and base range or lifetime limits.
    /// </summary>
    /// <param name="activeSeconds">Consecutive active time accumulated by the beam.</param>
    /// <param name="projectileSpeed">Current effective projectile speed inherited by the beam.</param>
    /// <param name="rangeLimit">Current effective range limit.</param>
    /// <param name="lifetimeLimit">Current effective lifetime limit.</param>
    /// <returns>The clamped travel budget used to build the current beam geometry.</returns>
    internal static float ResolveTravelDistance(float activeSeconds,
                                                float projectileSpeed,
                                                float rangeLimit,
                                                float lifetimeLimit)
    {
        float safeActiveSeconds = IsFinite(activeSeconds) ? math.max(0f, activeSeconds) : 0f;
        float safeProjectileSpeed = IsFinite(projectileSpeed) ? math.max(0f, projectileSpeed) : 0f;
        float travelDistance = safeActiveSeconds * safeProjectileSpeed;

        if (!IsFinite(travelDistance))
            travelDistance = MaximumSupportedTravelDistance;

        float maximumTravelDistance = ResolveMaximumTravelDistance(projectileSpeed, rangeLimit, lifetimeLimit);
        return math.clamp(travelDistance, 0f, maximumTravelDistance);
    }

    /// <summary>
    /// Resolves one effective collision radius from projectile scale and beam-local width tuning.
    /// </summary>
    /// <param name="projectileScaleMultiplier">Effective projectile scale multiplier inherited from the shooting config.</param>
    /// <param name="collisionWidthMultiplier">Beam-local collision width multiplier.</param>
    /// <returns>The effective beam collision radius.</returns>
    internal static float ResolveCollisionRadius(float projectileScaleMultiplier,
                                                 float collisionWidthMultiplier)
    {
        float collisionRadius = BaseProjectileRadius * math.max(0.01f, projectileScaleMultiplier) * math.max(0.01f, collisionWidthMultiplier);
        return ClampCollisionRadius(collisionRadius);
    }

    /// <summary>
    /// Resolves one effective visual body width from projectile scale and beam-local width tuning.
    /// </summary>
    /// <param name="projectileScaleMultiplier">Effective projectile scale multiplier inherited from the shooting config.</param>
    /// <param name="bodyWidthMultiplier">Beam-local visual width multiplier.</param>
    /// <returns>The effective beam body width used by the presentation system.</returns>
    internal static float ResolveBodyWidth(float projectileScaleMultiplier,
                                           float bodyWidthMultiplier)
    {
        float bodyWidth = BaseProjectileRadius * 2f * math.max(0.01f, projectileScaleMultiplier) * math.max(0.01f, bodyWidthMultiplier);
        return ClampBodyWidth(bodyWidth);
    }

    /// <summary>
    /// Resolves the current planar player forward used by Laser Beam lanes that must follow actual player rotation.
    /// </summary>
    /// <param name="localTransform">Current player transform after look rotation has been applied.</param>
    /// <returns>Normalized planar forward direction.</returns>
    internal static float3 ResolveCurrentForwardDirection(in LocalTransform localTransform)
    {
        float3 forwardDirection = math.forward(localTransform.Rotation);
        return PlayerControllerMath.NormalizePlanar(forwardDirection, new float3(0f, 0f, 1f));
    }

    /// <summary>
    /// Resolves one evenly spread lane direction from the base look direction and shotgun cone settings.
    /// </summary>
    /// <param name="baseDirection">Base shoot direction.</param>
    /// <param name="laneIndex">Zero-based lane index.</param>
    /// <param name="laneCount">Total lane count in the current primary emission.</param>
    /// <param name="coneAngleDegrees">Total spread angle in degrees.</param>
    /// <returns>The normalized lane direction.</returns>
    internal static float3 ResolveSpreadDirection(float3 baseDirection,
                                                  int laneIndex,
                                                  int laneCount,
                                                  float coneAngleDegrees)
    {
        float3 normalizedBaseDirection = math.normalizesafe(baseDirection, new float3(0f, 0f, 1f));

        if (laneCount <= 1)
            return normalizedBaseDirection;

        float angleDegrees = PlayerProjectileConePatternUtility.ResolveDirectionAngleDegrees(laneIndex,
                                                                                              laneCount,
                                                                                              coneAngleDegrees);
        quaternion rotationOffset = quaternion.AxisAngle(new float3(0f, 1f, 0f), math.radians(angleDegrees));
        float3 spreadDirection = math.rotate(rotationOffset, normalizedBaseDirection);

        if (math.lengthsq(spreadDirection) <= DirectionEpsilon)
            return normalizedBaseDirection;

        return math.normalizesafe(spreadDirection, normalizedBaseDirection);
    }

    /// <summary>
    /// Clamps one requested travel distance to the runtime safety envelope used by beam geometry and queries.
    /// </summary>
    /// <param name="travelDistance">Requested travel distance.</param>
    /// <returns>Safe travel distance.</returns>
    internal static float ClampRequestedTravelDistance(float travelDistance)
    {
        if (!IsFinite(travelDistance))
            return 0f;

        return math.clamp(travelDistance, 0f, MaximumSupportedTravelDistance);
    }

    /// <summary>
    /// Clamps one beam collision radius to the runtime safety envelope used by wall queries and hit resolution.
    /// </summary>
    /// <param name="collisionRadius">Requested collision radius.</param>
    /// <returns>Safe collision radius.</returns>
    internal static float ClampCollisionRadius(float collisionRadius)
    {
        if (!IsFinite(collisionRadius))
            return MinimumCollisionRadius;

        return math.clamp(collisionRadius, MinimumCollisionRadius, MaximumSupportedCollisionRadius);
    }

    /// <summary>
    /// Clamps one beam body width to the runtime safety envelope used by lane storage and presentation.
    /// </summary>
    /// <param name="bodyWidth">Requested beam body width.</param>
    /// <returns>Safe beam body width.</returns>
    internal static float ClampBodyWidth(float bodyWidth)
    {
        if (!IsFinite(bodyWidth))
            return 0.02f;

        return math.clamp(bodyWidth, 0.02f, MaximumSupportedBodyWidth);
    }

    /// <summary>
    /// Resolves one clipped beam segment against walls and returns the final world-space segment data.
    /// </summary>
    /// <param name="startPoint">Requested world-space segment start.</param>
    /// <param name="endPoint">Requested world-space segment end.</param>
    /// <param name="collisionRadius">Effective collision radius used for wall casts.</param>
    /// <param name="physicsWorldSingleton">Physics world used for wall casts.</param>
    /// <param name="wallsCollisionFilter">Collision filter used to query world walls.</param>
    /// <param name="wallsEnabled">True when wall clipping should be evaluated.</param>
    /// <param name="resolvedEndPoint">Final segment end after wall clipping.</param>
    /// <param name="resolvedDirection">Final normalized direction after clipping.</param>
    /// <param name="resolvedLength">Final segment length after clipping.</param>
    /// <param name="hitWall">True when the requested segment was clipped by a wall.</param>
    /// <param name="wallNormal">Wall normal returned by the blocking cast when available.</param>
    /// <returns>True when the resolved segment still has a usable non-zero length.</returns>
    internal static bool TryResolveSegment(float3 startPoint,
                                           float3 endPoint,
                                           float collisionRadius,
                                           in PhysicsWorldSingleton physicsWorldSingleton,
                                           in CollisionFilter wallsCollisionFilter,
                                           bool wallsEnabled,
                                           out float3 resolvedEndPoint,
                                           out float3 resolvedDirection,
                                           out float resolvedLength,
                                           out bool hitWall,
                                           out float3 wallNormal)
    {
        if (!IsFinite(startPoint) || !IsFinite(endPoint))
        {
            resolvedEndPoint = startPoint;
            resolvedDirection = new float3(0f, 0f, 1f);
            resolvedLength = 0f;
            hitWall = false;
            wallNormal = float3.zero;
            return false;
        }

        float3 displacement = endPoint - startPoint;
        resolvedEndPoint = startPoint;
        resolvedDirection = math.normalizesafe(displacement, new float3(0f, 0f, 1f));
        resolvedLength = math.length(displacement);
        hitWall = false;
        wallNormal = float3.zero;
        collisionRadius = ClampCollisionRadius(collisionRadius);

        if (resolvedLength < MinimumTravelDistance)
            return false;

        float3 allowedDisplacement = displacement;

        if (wallsEnabled)
        {
            hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                              startPoint,
                                                                              displacement,
                                                                              collisionRadius,
                                                                              wallsCollisionFilter,
                                                                              out allowedDisplacement,
                                                                              out wallNormal);
        }

        resolvedLength = math.length(allowedDisplacement);

        if (resolvedLength < MinimumTravelDistance)
            return false;

        resolvedDirection = math.normalizesafe(allowedDisplacement, resolvedDirection);
        resolvedEndPoint = startPoint + allowedDisplacement;
        return true;
    }

    /// <summary>
    /// Appends one already-resolved Laser Beam segment to the output buffer.
    /// </summary>
    /// <param name="laneBuffer">Output segment buffer.</param>
    /// <param name="laneIndex">Stable lane index assigned to the segment.</param>
    /// <param name="isSplitChild">True when the segment belongs to a split child lane.</param>
    /// <param name="startPoint">Segment start point.</param>
    /// <param name="endPoint">Segment end point.</param>
    /// <param name="direction">Segment direction.</param>
    /// <param name="length">Segment length.</param>
    /// <param name="collisionRadius">Effective collision radius used by gameplay checks.</param>
    /// <param name="visualWidth">Effective visual width used by the presentation system.</param>
    /// <param name="damageMultiplier">Lane-local damage multiplier.</param>
    /// <param name="isTerminalSegment">True when the segment is the final segment for the lane.</param>
    /// <param name="terminalBlockedByWall">True when the terminal segment ended on a wall clip.</param>
    /// <param name="terminalNormal">Final wall normal stored for debugging and cap logic.</param>
    internal static void AppendLaneSegment(ref DynamicBuffer<PlayerLaserBeamLaneElement> laneBuffer,
                                           int laneIndex,
                                           bool isSplitChild,
                                           float3 startPoint,
                                           float3 endPoint,
                                           float3 direction,
                                           float length,
                                           float collisionRadius,
                                           float visualWidth,
                                           float damageMultiplier,
                                           bool isTerminalSegment,
                                           bool terminalBlockedByWall,
                                           float3 terminalNormal)
    {
        if (!IsFinite(startPoint) || !IsFinite(endPoint) || !IsFinite(direction))
            return;

        laneBuffer.Add(new PlayerLaserBeamLaneElement
        {
            LaneIndex = laneIndex,
            IsSplitChild = isSplitChild ? (byte)1 : (byte)0,
            IsTerminalSegment = isTerminalSegment ? (byte)1 : (byte)0,
            TerminalBlockedByWall = terminalBlockedByWall ? (byte)1 : (byte)0,
            StartPoint = startPoint,
            EndPoint = endPoint,
            Direction = direction,
            Length = length,
            CollisionRadius = ClampCollisionRadius(collisionRadius),
            VisualWidth = ClampBodyWidth(visualWidth),
            DamageMultiplier = math.max(0f, damageMultiplier),
            TerminalNormal = terminalBlockedByWall ? math.normalizesafe(terminalNormal, float3.zero) : float3.zero
        });
    }

    /// <summary>
    /// Builds one bounced beam path and appends all resolved segments to the output buffer.
    /// </summary>
    /// <param name="laneBuffer">Output segment buffer.</param>
    /// <param name="laneIndex">Stable lane index assigned to all appended segments.</param>
    /// <param name="isSplitChild">True when the lane belongs to a split branch.</param>
    /// <param name="startPoint">World-space origin of the lane.</param>
    /// <param name="direction">Initial lane direction.</param>
    /// <param name="travelDistance">Total travel budget available for the lane.</param>
    /// <param name="collisionRadius">Effective collision radius.</param>
    /// <param name="maximumBounceSegments">Maximum reflected wall segments supported by the lane.</param>
    /// <param name="physicsWorldSingleton">Physics world used for wall casts.</param>
    /// <param name="wallsCollisionFilter">Collision filter used to detect world walls.</param>
    /// <param name="wallsEnabled">True when wall tests should be evaluated.</param>
    /// <returns>True when at least one segment was appended.</returns>
    internal static bool TryAppendLaneSegments(ref DynamicBuffer<PlayerLaserBeamLaneElement> laneBuffer,
                                               int laneIndex,
                                               bool isSplitChild,
                                               float3 startPoint,
                                               float3 direction,
                                               float travelDistance,
                                               float collisionRadius,
                                               float visualWidth,
                                               float damageMultiplier,
                                               int maximumBounceSegments,
                                               in PhysicsWorldSingleton physicsWorldSingleton,
                                               in CollisionFilter wallsCollisionFilter,
                                               bool wallsEnabled)
    {
        if (!IsFinite(startPoint) || !IsFinite(direction))
            return false;

        float remainingDistance = ClampRequestedTravelDistance(travelDistance);
        float3 segmentStart = startPoint;
        float3 segmentDirection = math.normalizesafe(direction, new float3(0f, 0f, 1f));
        collisionRadius = ClampCollisionRadius(collisionRadius);
        visualWidth = ClampBodyWidth(visualWidth);

        if (remainingDistance < MinimumTravelDistance)
            return false;

        int appendedSegments = 0;
        int remainingBounces = math.clamp(maximumBounceSegments, 0, MaximumSupportedBounceSegments);

        while (remainingDistance >= MinimumTravelDistance)
        {
            float3 requestedEndPoint = segmentStart + segmentDirection * remainingDistance;

            if (!TryResolveSegment(segmentStart,
                                   requestedEndPoint,
                                   collisionRadius,
                                   in physicsWorldSingleton,
                                   in wallsCollisionFilter,
                                   wallsEnabled,
                                   out float3 resolvedEndPoint,
                                   out float3 resolvedDirection,
                                   out float segmentLength,
                                   out bool hitWall,
                                   out float3 wallNormal))
            {
                break;
            }

            bool isTerminalSegment = !hitWall || remainingBounces <= 0;
            AppendLaneSegment(ref laneBuffer,
                              laneIndex,
                              isSplitChild,
                              segmentStart,
                              resolvedEndPoint,
                              resolvedDirection,
                              segmentLength,
                              collisionRadius,
                              visualWidth,
                              damageMultiplier,
                              isTerminalSegment,
                              hitWall && isTerminalSegment,
                              wallNormal);
            appendedSegments++;
            remainingDistance -= segmentLength;

            if (!hitWall || remainingBounces <= 0)
                break;

            float3 normalizedNormal = math.normalizesafe(wallNormal, float3.zero);

            if (math.lengthsq(normalizedNormal) <= DirectionEpsilon)
                break;

            segmentDirection = math.normalizesafe(math.reflect(segmentDirection, normalizedNormal), segmentDirection);
            segmentStart = resolvedEndPoint + segmentDirection * SurfacePushDistance;
            remainingBounces--;
        }

        return appendedSegments > 0;
    }
    #endregion
    #endregion

    #region Private Methods
    #region Current-Frame Origin
    /// <summary>
    /// Attempts to reconstruct a muzzle pose against the current player transform without consuming LocalToWorld output.
    /// The baked local hierarchy is composed upward until it reaches the player, then the latest player pose is applied.
    /// </summary>
    /// <param name="playerEntity">Player entity that owns the muzzle anchor reference.</param>
    /// <param name="playerTransform">Current player transform used as the root pose.</param>
    /// <param name="muzzleLookup">Lookup used to read the baked muzzle anchor entity.</param>
    /// <param name="transformLookup">Lookup used to read local transforms along the muzzle hierarchy.</param>
    /// <param name="parentLookup">Lookup used to climb from the muzzle anchor back to the player entity.</param>
    /// <param name="position">Resolved current-frame muzzle position.</param>
    /// <param name="rotation">Resolved current-frame muzzle rotation.</param>
    /// <returns>True when a current-frame muzzle pose could be resolved.</returns>
    private static bool TryResolveCurrentFrameMuzzlePose(Entity playerEntity,
                                                         in LocalTransform playerTransform,
                                                         in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                                         in ComponentLookup<LocalTransform> transformLookup,
                                                         in ComponentLookup<Parent> parentLookup,
                                                         out float3 position,
                                                         out quaternion rotation)
    {
        if (!muzzleLookup.HasComponent(playerEntity))
        {
            position = float3.zero;
            rotation = quaternion.identity;
            return false;
        }

        Entity muzzleEntity = muzzleLookup[playerEntity].AnchorEntity;

        if (muzzleEntity == Entity.Null || !transformLookup.HasComponent(muzzleEntity))
        {
            position = float3.zero;
            rotation = quaternion.identity;
            return false;
        }

        LocalTransform accumulatedTransform = transformLookup[muzzleEntity];
        Entity currentEntity = muzzleEntity;

        // Compose local transforms toward the player so lateral motion uses the current player frame, not stale LocalToWorld data.
        for (int hopIndex = 0; hopIndex < MaximumMuzzleParentHops; hopIndex++)
        {
            if (!parentLookup.HasComponent(currentEntity))
            {
                position = accumulatedTransform.Position;
                rotation = accumulatedTransform.Rotation;
                return true;
            }

            Entity parentEntity = parentLookup[currentEntity].Value;

            if (parentEntity == playerEntity)
            {
                LocalTransform worldTransform = ComposeChildTransform(in playerTransform, in accumulatedTransform);
                position = worldTransform.Position;
                rotation = worldTransform.Rotation;
                return true;
            }

            if (parentEntity == Entity.Null || !transformLookup.HasComponent(parentEntity))
                break;

            LocalTransform parentTransform = transformLookup[parentEntity];
            accumulatedTransform = ComposeChildTransform(in parentTransform, in accumulatedTransform);
            currentEntity = parentEntity;
        }

        position = float3.zero;
        rotation = quaternion.identity;
        return false;
    }

    /// <summary>
    /// Composes one child transform into its parent space using Unity.Entities uniform-scale transform semantics.
    /// </summary>
    /// <param name="parentTransform">Parent transform that defines the destination space.</param>
    /// <param name="childTransform">Child transform expressed in parent local space.</param>
    /// <returns>Child transform expressed in the parent parent's space.</returns>
    private static LocalTransform ComposeChildTransform(in LocalTransform parentTransform,
                                                        in LocalTransform childTransform)
    {
        return new LocalTransform
        {
            Position = parentTransform.Position + math.rotate(parentTransform.Rotation, childTransform.Position * parentTransform.Scale),
            Rotation = math.mul(parentTransform.Rotation, childTransform.Rotation),
            Scale = parentTransform.Scale * childTransform.Scale
        };
    }
    #endregion

    #region Runtime Bounds
    /// <summary>
    /// Resolves the absolute maximum travel distance allowed by the inherited range and lifetime caps.
    /// </summary>
    /// <param name="projectileSpeed">Current effective projectile speed.</param>
    /// <param name="rangeLimit">Effective range cap.</param>
    /// <param name="lifetimeLimit">Effective lifetime cap.</param>
    /// <returns>Maximum beam travel distance before wall clipping.</returns>
    internal static float ResolveMaximumTravelDistance(float projectileSpeed,
                                                       float rangeLimit,
                                                       float lifetimeLimit)
    {
        float rangeTravelDistance = rangeLimit > 0f ? rangeLimit : float.MaxValue;
        float lifetimeTravelDistance = lifetimeLimit > 0f
            ? math.max(0f, lifetimeLimit) * math.max(0f, projectileSpeed)
            : float.MaxValue;
        float maximumTravelDistance = math.min(rangeTravelDistance, lifetimeTravelDistance);

        if (maximumTravelDistance == float.MaxValue)
            maximumTravelDistance = DefaultUnboundedBeamDistance;

        return math.max(MinimumTravelDistance, ClampRequestedTravelDistance(maximumTravelDistance));
    }

    /// <summary>
    /// Resolves whether one scalar value can be consumed safely by beam math.
    /// </summary>
    /// <param name="value">Scalar value to validate.</param>
    /// <returns>True when the value is finite.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Resolves whether one float3 can be consumed safely by beam math.
    /// </summary>
    /// <param name="value">Float3 value to validate.</param>
    /// <returns>True when every component is finite.</returns>
    private static bool IsFinite(float3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }
    #endregion
    #endregion

    #endregion
}
