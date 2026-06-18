using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Builds Laser Beam lane polylines that follow the same Perfect Circle trajectory family used by projectile simulation.
/// </summary>
internal static class PlayerLaserBeamPerfectCircleUtility
{
    #region Constants
    private const float MinimumSimulationDeltaTime = 1f / 240f;
    private const float MaximumSimulationDeltaTime = 1f / 20f;
    private const float TargetSegmentLength = 0.52f;
    private const float MaximumAngularStepRadians = 0.12f;
    private const int MaximumSimulationIterations = 384;
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one Laser Beam lane by sampling the Perfect Circle projectile path over the current beam travel budget.
    /// </summary>
    /// <param name="laneBuffer">Output segment buffer.</param>
    /// <param name="laneIndex">Stable lane index assigned to all appended segments.</param>
    /// <param name="laneCount">Total number of sibling lanes emitted by the current beam group.</param>
    /// <param name="isSplitChild">True when the lane belongs to one split child branch.</param>
    /// <param name="shooterEntity">Player entity used for deterministic seed reconstruction.</param>
    /// <param name="shooterPosition">Current shooter position used by orbit phases.</param>
    /// <param name="shooterVelocity">Current shooter velocity used during the radial entry phase.</param>
    /// <param name="startPoint">World-space lane origin used by the deterministic orbit sampler.</param>
    /// <param name="direction">Initial radial direction of the sampled lane, or zero to use the deterministic orbital seed.</param>
    /// <param name="activeSeconds">Consecutive active time currently accumulated by the beam.</param>
    /// <param name="travelDistanceLimit">Current beam travel budget resolved from projectile speed, range and lifetime.</param>
    /// <param name="rangeLimit">Effective projectile range inherited by the beam.</param>
    /// <param name="lifetimeLimit">Effective projectile lifetime inherited by the beam.</param>
    /// <param name="speedMultiplier">Beam-local speed multiplier applied on top of Perfect Circle motion speeds.</param>
    /// <param name="collisionRadius">Effective lane collision radius.</param>
    /// <param name="visualWidth">Effective lane visual width.</param>
    /// <param name="damageMultiplier">Lane-local damage multiplier.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle passive configuration.</param>
    /// <param name="physicsWorldSingleton">Physics world used for optional wall clipping.</param>
    /// <param name="wallsCollisionFilter">Collision filter used to detect world walls.</param>
    /// <param name="reachedVirtualDespawn">True when the sampled lane has reached a virtual despawn condition and can emit split-on-despawn branches.</param>
    /// <param name="wallsEnabled">True when wall clipping should be evaluated.</param>
    /// <returns>True when at least one beam segment was appended.</returns>
    internal static bool TryAppendPerfectCircleLaneSegments(ref DynamicBuffer<PlayerLaserBeamLaneElement> laneBuffer,
                                                            int laneIndex,
                                                            int laneCount,
                                                            bool isSplitChild,
                                                            Entity shooterEntity,
                                                            float3 shooterPosition,
                                                            float3 shooterVelocity,
                                                            float3 startPoint,
                                                            float3 direction,
                                                            float activeSeconds,
                                                            float travelDistanceLimit,
                                                            float rangeLimit,
                                                            float lifetimeLimit,
                                                            float speedMultiplier,
                                                            float collisionRadius,
                                                            float visualWidth,
                                                            float damageMultiplier,
                                                            in PerfectCirclePassiveConfig perfectCircleConfig,
                                                            in PhysicsWorldSingleton physicsWorldSingleton,
                                                            in CollisionFilter wallsCollisionFilter,
                                                            out bool reachedVirtualDespawn,
                                                            bool wallsEnabled)
    {
        reachedVirtualDespawn = false;
        float absoluteMaximumTravelDistance = ResolveMaximumTravelDistance(rangeLimit, lifetimeLimit);
        float maximumTravelDistance = ResolveMaximumTravelDistance(travelDistanceLimit, rangeLimit, lifetimeLimit);
        float maximumSimulationSeconds = ResolveMaximumSimulationSeconds(maximumTravelDistance);

        if (maximumSimulationSeconds <= 0f)
            return false;

        ProjectilePerfectCircleState perfectCircleState = BuildPerfectCircleState(in perfectCircleConfig,
                                                                                  laneIndex,
                                                                                  laneCount,
                                                                                  shooterEntity,
                                                                                  startPoint,
                                                                                  direction,
                                                                                  speedMultiplier);
        float simulatedSeconds = 0f;
        float accumulatedDistance = 0f;
        float3 currentPosition = startPoint;
        float3 terminalNormal = float3.zero;
        bool terminalBlockedByWall = false;
        int laneStartIndex = laneBuffer.Length;
        int simulationIterationCount = 0;

        while (simulatedSeconds < maximumSimulationSeconds &&
               accumulatedDistance < maximumTravelDistance &&
               simulationIterationCount < MaximumSimulationIterations)
        {
            simulationIterationCount++;
            float sampleTrajectoryTime = simulatedSeconds;
            float simulationDeltaTime = ResolveSimulationDeltaTime(in perfectCircleState,
                                                                  in perfectCircleConfig,
                                                                  speedMultiplier,
                                                                  sampleTrajectoryTime);
            float remainingSeconds = maximumSimulationSeconds - simulatedSeconds;

            if (simulationDeltaTime > remainingSeconds)
                simulationDeltaTime = remainingSeconds;

            if (simulationDeltaTime <= 0f)
                break;

            float3 nextPosition = ResolveSamplePosition(ref perfectCircleState,
                                                        shooterPosition,
                                                        shooterVelocity,
                                                        currentPosition,
                                                        simulationDeltaTime,
                                                        sampleTrajectoryTime + simulationDeltaTime,
                                                        speedMultiplier,
                                                        in perfectCircleConfig);
            float3 requestedDisplacement = nextPosition - currentPosition;
            float requestedLength = math.length(requestedDisplacement);
            simulatedSeconds += simulationDeltaTime;

            if (requestedLength <= DirectionEpsilon)
                continue;

            if (accumulatedDistance + requestedLength > maximumTravelDistance)
            {
                float remainingDistance = maximumTravelDistance - accumulatedDistance;

                if (remainingDistance <= PlayerLaserBeamUtility.MinimumTravelDistance)
                    break;

                float clampedFraction = remainingDistance / requestedLength;
                nextPosition = currentPosition + requestedDisplacement * clampedFraction;
                requestedDisplacement = nextPosition - currentPosition;
                requestedLength = remainingDistance;
            }

            if (requestedLength <= PlayerLaserBeamUtility.MinimumTravelDistance)
            {
                accumulatedDistance += requestedLength;
                currentPosition = nextPosition;
                continue;
            }

            if (!PlayerLaserBeamUtility.TryResolveSegment(currentPosition,
                                                          nextPosition,
                                                          collisionRadius,
                                                          in physicsWorldSingleton,
                                                          in wallsCollisionFilter,
                                                          wallsEnabled,
                                                          out float3 resolvedEndPoint,
                                                          out float3 resolvedDirection,
                                                          out float resolvedLength,
                                                          out bool hitWall,
                                                          out float3 wallNormal))
            {
                if (wallsEnabled)
                {
                    terminalBlockedByWall = true;
                    terminalNormal = wallNormal;
                }

                break;
            }

            PlayerLaserBeamUtility.AppendLaneSegment(ref laneBuffer,
                                                     laneIndex,
                                                     isSplitChild,
                                                     currentPosition,
                                                     resolvedEndPoint,
                                                     resolvedDirection,
                                                     resolvedLength,
                                                     collisionRadius,
                                                     visualWidth,
                                                     damageMultiplier,
                                                     false,
                                                     false,
                                                     float3.zero);
            accumulatedDistance += resolvedLength;
            currentPosition = resolvedEndPoint;

            if (!hitWall)
                continue;

            terminalBlockedByWall = true;
            terminalNormal = wallNormal;
            break;
        }

        FinalizeLaneSegments(ref laneBuffer,
                             laneStartIndex,
                             terminalBlockedByWall,
                             terminalNormal);
        bool reachedLifetimeCap = lifetimeLimit > 0f && activeSeconds >= lifetimeLimit;
        bool reachedRangeCap = rangeLimit > 0f &&
                               accumulatedDistance + PlayerLaserBeamUtility.MinimumTravelDistance >= absoluteMaximumTravelDistance;
        reachedVirtualDespawn = terminalBlockedByWall || reachedLifetimeCap || reachedRangeCap;
        return laneBuffer.Length > laneStartIndex;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the effective simulation time window allowed for the current beam lane.
    /// </summary>
    /// <param name="maximumTravelDistance">Distance budget that the sampler needs to consume.</param>
    /// <returns>The time window that can still produce valid geometry.</returns>
    private static float ResolveMaximumSimulationSeconds(float maximumTravelDistance)
    {
        if (maximumTravelDistance <= PlayerLaserBeamUtility.MinimumTravelDistance)
            return 0f;

        return MaximumSimulationIterations * MaximumSimulationDeltaTime;
    }

    /// <summary>
    /// Resolves the maximum path distance allowed by current beam growth, projectile range or the fallback cap.
    /// </summary>
    /// <param name="travelDistanceLimit">Current beam travel budget resolved by the simulation system.</param>
    /// <param name="rangeLimit">Effective projectile range inherited by the beam.</param>
    /// <param name="lifetimeLimit">Effective projectile lifetime inherited by the beam.</param>
    /// <returns>The maximum path distance that can be sampled for the current lane.</returns>
    private static float ResolveMaximumTravelDistance(float travelDistanceLimit,
                                                      float rangeLimit,
                                                      float lifetimeLimit)
    {
        float requestedTravelDistance = PlayerLaserBeamUtility.ClampRequestedTravelDistance(travelDistanceLimit);

        if (requestedTravelDistance <= PlayerLaserBeamUtility.MinimumTravelDistance)
            return 0f;

        return math.min(requestedTravelDistance, ResolveMaximumTravelDistance(rangeLimit, lifetimeLimit));
    }

    /// <summary>
    /// Resolves the absolute path distance allowed by projectile range or the beam fallback cap when no range or lifetime exists.
    /// </summary>
    /// <param name="rangeLimit">Effective projectile range inherited by the beam.</param>
    /// <param name="lifetimeLimit">Effective projectile lifetime inherited by the beam.</param>
    /// <returns>The absolute maximum path distance that can be sampled for the current lane.</returns>
    private static float ResolveMaximumTravelDistance(float rangeLimit,
                                                      float lifetimeLimit)
    {
        float maximumTravelDistance;

        if (rangeLimit > 0f)
            maximumTravelDistance = rangeLimit;
        else if (lifetimeLimit > 0f)
            maximumTravelDistance = PlayerLaserBeamUtility.MaximumSupportedTravelDistance;
        else
            maximumTravelDistance = PlayerLaserBeamUtility.DefaultUnboundedBeamDistance;

        return math.max(PlayerLaserBeamUtility.MinimumTravelDistance,
                        PlayerLaserBeamUtility.ClampRequestedTravelDistance(maximumTravelDistance));
    }

    /// <summary>
    /// Resolves one sampling delta that keeps curved beam reconstruction smooth without exploding segment counts.
    /// </summary>
    /// <param name="perfectCircleState">Current simulated Perfect Circle state.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <param name="speedMultiplier">Beam-local speed multiplier applied to motion speeds.</param>
    /// <param name="globalTime">Absolute world time associated with the current sample start.</param>
    /// <returns>The clamped simulation delta to use for the next sampled step.</returns>
    private static float ResolveSimulationDeltaTime(in ProjectilePerfectCircleState perfectCircleState,
                                                    in PerfectCirclePassiveConfig perfectCircleConfig,
                                                    float speedMultiplier,
                                                    float globalTime)
    {
        return ProjectilePerfectCircleTrajectoryUtility.ResolveSuggestedSimulationDeltaTime(in perfectCircleState,
                                                                                            in perfectCircleConfig,
                                                                                            speedMultiplier,
                                                                                            globalTime,
                                                                                            TargetSegmentLength,
                                                                                            MaximumAngularStepRadians,
                                                                                            MinimumSimulationDeltaTime,
                                                                                            MaximumSimulationDeltaTime);
    }

    /// <summary>
    /// Resolves the next world-space point of one sampled Perfect Circle step.
    /// </summary>
    /// <param name="perfectCircleState">Mutable Perfect Circle state advanced by the sample.</param>
    /// <param name="shooterPosition">Current shooter position used by orbit phases.</param>
    /// <param name="shooterVelocity">Current shooter velocity used during radial entry.</param>
    /// <param name="fallbackPosition">Previous sampled position returned when no movement can be produced.</param>
    /// <param name="deltaTime">Step delta applied to the simulated trajectory.</param>
    /// <param name="globalTime">Absolute world time associated with the sample end.</param>
    /// <param name="speedMultiplier">Beam-local speed multiplier applied to motion speeds.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <returns>The resolved world-space position reached by the sampled trajectory step.</returns>
    private static float3 ResolveSamplePosition(ref ProjectilePerfectCircleState perfectCircleState,
                                                float3 shooterPosition,
                                                float3 shooterVelocity,
                                                float3 fallbackPosition,
                                                float deltaTime,
                                                float globalTime,
                                                float speedMultiplier,
                                                in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        return ProjectilePerfectCircleTrajectoryUtility.ResolveNextPosition(ref perfectCircleState,
                                                                            shooterPosition,
                                                                            shooterVelocity,
                                                                            fallbackPosition,
                                                                            deltaTime,
                                                                            globalTime,
                                                                            speedMultiplier,
                                                                            in perfectCircleConfig);
    }

    /// <summary>
    /// Rebuilds the initial Perfect Circle runtime state for deterministic beam sampling before the first simulation step.
    /// </summary>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <param name="laneIndex">Stable lane index used as request index surrogate.</param>
    /// <param name="laneCount">Total number of sibling lanes emitted by the current beam group.</param>
    /// <param name="shooterEntity">Player entity used for deterministic seed reconstruction.</param>
    /// <param name="startPoint">World-space origin used as entry origin.</param>
    /// <param name="direction">Initial radial direction of the sampled lane.</param>
    /// <param name="speedMultiplier">Beam-local speed multiplier applied to entry velocity.</param>
    /// <returns>One initialized Perfect Circle state ready for sampled simulation.</returns>
    private static ProjectilePerfectCircleState BuildPerfectCircleState(in PerfectCirclePassiveConfig perfectCircleConfig,
                                                                        int laneIndex,
                                                                        int laneCount,
                                                                        Entity shooterEntity,
                                                                        float3 startPoint,
                                                                        float3 direction,
                                                                        float speedMultiplier)
    {
        int safeLaneCount = math.max(1, laneCount);
        int safeLaneIndex = math.abs(laneIndex) % safeLaneCount;
        float seed = laneIndex + shooterEntity.Index * 13f;
        float angleRadians = math.radians(math.max(0f, perfectCircleConfig.GoldenAngleDegrees) * seed);
        float3 radialDirection = direction;

        if (math.lengthsq(radialDirection) <= DirectionEpsilon)
            radialDirection = new float3(math.cos(angleRadians), 0f, math.sin(angleRadians));

        radialDirection = math.normalizesafe(radialDirection, new float3(0f, 0f, 1f));
        float radialEntrySpeed = math.max(0f, perfectCircleConfig.RadialEntrySpeed * math.max(0f, speedMultiplier));
        return new ProjectilePerfectCircleState
        {
            Enabled = 1,
            HasEnteredOrbit = 0,
            CompletedFullOrbit = 0,
            HasOrbitPlaneHeight = 0,
            EntryOrigin = startPoint,
            OrbitAngle = angleRadians,
            OrbitBlendProgress = 0f,
            CurrentRadius = 0f,
            AccumulatedOrbitRadians = 0f,
            RadialDirection = radialDirection,
            EntryVelocity = radialDirection * radialEntrySpeed,
            OrbitPlaneHeight = 0f,
            OrbitLayerIndex = safeLaneIndex,
            OrbitLayerCount = safeLaneCount
        };
    }

    /// <summary>
    /// Marks the final appended segment of the current lane as terminal and propagates optional wall metadata.
    /// </summary>
    /// <param name="laneBuffer">Output segment buffer that already contains the current lane geometry.</param>
    /// <param name="laneStartIndex">Buffer index where the current lane started appending.</param>
    /// <param name="terminalBlockedByWall">True when the lane ended because of a wall clip.</param>
    /// <param name="terminalNormal">Final wall normal stored on the terminal segment.</param>
    private static void FinalizeLaneSegments(ref DynamicBuffer<PlayerLaserBeamLaneElement> laneBuffer,
                                             int laneStartIndex,
                                             bool terminalBlockedByWall,
                                             float3 terminalNormal)
    {
        if (laneBuffer.Length <= laneStartIndex)
            return;

        int lastIndex = laneBuffer.Length - 1;
        PlayerLaserBeamLaneElement lastSegment = laneBuffer[lastIndex];
        lastSegment.IsTerminalSegment = 1;
        lastSegment.TerminalBlockedByWall = terminalBlockedByWall ? (byte)1 : (byte)0;
        lastSegment.TerminalNormal = terminalBlockedByWall ? math.normalizesafe(terminalNormal, float3.zero) : float3.zero;
        laneBuffer[lastIndex] = lastSegment;
    }
    #endregion

    #endregion
}
