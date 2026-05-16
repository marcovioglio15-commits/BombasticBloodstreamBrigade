using Unity.Mathematics;

/// <summary>
/// Centralizes Perfect Circle trajectory advancement so projectile simulation and Laser Beam sampling stay aligned.
/// </summary>
internal static class ProjectilePerfectCircleTrajectoryUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumOrbitRadius = 0.05f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the pulsing circular radius used by the standard Perfect Circle orbit mode.
    /// </summary>
    /// <param name="globalTime">Absolute world time used by the radius pulse.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <returns>Current circular orbit radius.</returns>
    public static float ResolveCircularOrbitRadius(float globalTime,
                                                   in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        float minimumRadius = math.max(0f, perfectCircleConfig.OrbitRadiusMin);
        float maximumRadius = math.max(minimumRadius, perfectCircleConfig.OrbitRadiusMax);
        float pulseFrequency = math.max(0f, perfectCircleConfig.OrbitPulseFrequency);
        float pulsePhase = globalTime * pulseFrequency * (math.PI * 2f);
        float pulse = pulseFrequency > 0f ? math.sin(pulsePhase) * 0.5f + 0.5f : 1f;
        return math.lerp(minimumRadius, maximumRadius, pulse);
    }

    /// <summary>
    /// Resolves the radial distance at which the path should leave the straight entry phase and begin orbit blending.
    /// </summary>
    /// <param name="globalTime">Absolute world time used by pulsing-circle mode.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <returns>Orbit-entry threshold distance.</returns>
    public static float ResolveOrbitEntryThreshold(float globalTime,
                                                   in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        switch (perfectCircleConfig.PathMode)
        {
            case ProjectileOrbitPathMode.GoldenSpiral:
                return math.max(MinimumOrbitRadius, perfectCircleConfig.SpiralStartRadius);
            default:
                float orbitRadius = ResolveCircularOrbitRadius(globalTime, in perfectCircleConfig);
                float orbitEntryRatio = math.clamp(perfectCircleConfig.OrbitEntryRatio, 0f, 1f);
                return math.max(MinimumOrbitRadius, orbitRadius * orbitEntryRatio);
        }
    }

    /// <summary>
    /// Resolves one simulation delta that keeps sampled Laser Beam orbit lanes smooth without exploding segment counts.
    /// </summary>
    /// <param name="perfectCircleState">Current Perfect Circle runtime state.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <param name="speedMultiplier">Beam-local speed multiplier applied to Perfect Circle motion.</param>
    /// <param name="globalTime">Absolute world time at the current sample.</param>
    /// <param name="targetSegmentLength">Preferred straight-line length of one sampled segment.</param>
    /// <param name="maximumAngularStepRadians">Maximum angular change allowed per sample.</param>
    /// <param name="minimumSimulationDeltaTime">Lower simulation-delta clamp.</param>
    /// <param name="maximumSimulationDeltaTime">Upper simulation-delta clamp.</param>
    /// <returns>Suggested simulation delta for the next lane sample.</returns>
    public static float ResolveSuggestedSimulationDeltaTime(in ProjectilePerfectCircleState perfectCircleState,
                                                            in PerfectCirclePassiveConfig perfectCircleConfig,
                                                            float speedMultiplier,
                                                            float globalTime,
                                                            float targetSegmentLength,
                                                            float maximumAngularStepRadians,
                                                            float minimumSimulationDeltaTime,
                                                            float maximumSimulationDeltaTime)
    {
        float effectiveSpeedMultiplier = math.max(0f, speedMultiplier);
        float effectiveLinearSpeed = math.max(MinimumOrbitRadius,
                                              perfectCircleConfig.RadialEntrySpeed * effectiveSpeedMultiplier);
        float angularSpeedRadiansPerSecond = 0f;

        if (perfectCircleState.HasEnteredOrbit != 0)
        {
            switch (perfectCircleConfig.PathMode)
            {
                case ProjectileOrbitPathMode.GoldenSpiral:
                    angularSpeedRadiansPerSecond = math.radians(math.max(0f,
                                                                         perfectCircleConfig.SpiralAngularSpeedDegreesPerSecond *
                                                                         effectiveSpeedMultiplier));
                    effectiveLinearSpeed = math.max(MinimumOrbitRadius,
                                                    angularSpeedRadiansPerSecond *
                                                    math.max(MinimumOrbitRadius,
                                                             perfectCircleState.CurrentRadius));
                    break;
                default:
                    float orbitRadius = ResolveCircularOrbitRadius(globalTime, in perfectCircleConfig);
                    effectiveLinearSpeed = math.max(MinimumOrbitRadius,
                                                    perfectCircleConfig.OrbitalSpeed * effectiveSpeedMultiplier);

                    if (orbitRadius > 0.001f)
                        angularSpeedRadiansPerSecond = effectiveLinearSpeed / orbitRadius;

                    break;
            }
        }

        float deltaFromDistance = targetSegmentLength / effectiveLinearSpeed;
        float deltaFromAngularStep = angularSpeedRadiansPerSecond > DirectionEpsilon
            ? maximumAngularStepRadians / angularSpeedRadiansPerSecond
            : maximumSimulationDeltaTime;
        float resolvedDeltaTime = math.min(deltaFromDistance, deltaFromAngularStep);

        if (perfectCircleState.HasEnteredOrbit != 0 && perfectCircleState.OrbitBlendProgress < 1f)
            resolvedDeltaTime *= 0.55f;

        return math.clamp(resolvedDeltaTime,
                          minimumSimulationDeltaTime,
                          maximumSimulationDeltaTime);
    }

    /// <summary>
    /// Advances one Perfect Circle state by a single simulation step and returns the world-space position reached.
    /// </summary>
    /// <param name="perfectCircleState">Mutable Perfect Circle state to advance.</param>
    /// <param name="shooterPosition">Current shooter position used as orbit center.</param>
    /// <param name="shooterInheritedVelocity">Current shooter velocity used by radial entry and transition blending.</param>
    /// <param name="fallbackPosition">Previous world-space position returned when no movement can be produced.</param>
    /// <param name="deltaTime">Step delta to apply.</param>
    /// <param name="globalTime">Absolute world time associated with the end of the step.</param>
    /// <param name="speedMultiplier">Motion multiplier applied on top of the authored Perfect Circle speeds.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <returns>The world-space position reached after advancing the trajectory.</returns>
    public static float3 ResolveNextPosition(ref ProjectilePerfectCircleState perfectCircleState,
                                             float3 shooterPosition,
                                             float3 shooterInheritedVelocity,
                                             float3 fallbackPosition,
                                             float deltaTime,
                                             float globalTime,
                                             float speedMultiplier,
                                             in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        if (perfectCircleState.Enabled == 0 || deltaTime <= 0f)
            return fallbackPosition;

        EnsureOrbitPlaneHeight(ref perfectCircleState,
                               shooterPosition,
                               fallbackPosition,
                               in perfectCircleConfig);

        float3 entryDirection = ResolveEntryDirection(ref perfectCircleState, fallbackPosition);
        bool justEnteredOrbit = false;

        if (perfectCircleState.HasEnteredOrbit == 0)
        {
            float3 entryPosition = AdvanceRadialEntry(ref perfectCircleState,
                                                      shooterInheritedVelocity,
                                                      entryDirection,
                                                      deltaTime,
                                                      globalTime,
                                                      speedMultiplier,
                                                      in perfectCircleConfig,
                                                      out bool reachedOrbitEntry);

            if (!reachedOrbitEntry)
                return entryPosition;

            InitializeOrbitTransition(ref perfectCircleState,
                                      entryPosition,
                                      shooterPosition,
                                      shooterInheritedVelocity,
                                      entryDirection,
                                      speedMultiplier,
                                      in perfectCircleConfig);
            justEnteredOrbit = true;
        }

        float3 orbitPosition = ResolveOrbitPosition(ref perfectCircleState,
                                                    shooterPosition,
                                                    deltaTime,
                                                    globalTime,
                                                    speedMultiplier,
                                                    in perfectCircleConfig);
        return ResolveBlendedOrbitPosition(ref perfectCircleState,
                                           orbitPosition,
                                           deltaTime,
                                           justEnteredOrbit,
                                           in perfectCircleConfig);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves a safe radial direction for the current trajectory state.
    /// </summary>
    /// <param name="perfectCircleState">Mutable Perfect Circle state that stores the radial direction.</param>
    /// <param name="fallbackPosition">Previous valid world-space position used as fallback when no direction was authored.</param>
    /// <returns>A normalized radial direction.</returns>
    private static float3 ResolveEntryDirection(ref ProjectilePerfectCircleState perfectCircleState,
                                                float3 fallbackPosition)
    {
        float3 entryDirection = perfectCircleState.RadialDirection;
        entryDirection.y = 0f;

        if (math.lengthsq(entryDirection) > DirectionEpsilon)
            return entryDirection;

        entryDirection = fallbackPosition - perfectCircleState.EntryOrigin;
        entryDirection.y = 0f;
        entryDirection = math.normalizesafe(entryDirection, new float3(0f, 0f, 1f));
        perfectCircleState.RadialDirection = entryDirection;
        return entryDirection;
    }

    /// <summary>
    /// Advances the straight radial entry phase and reports whether the path has reached the orbit threshold.
    /// </summary>
    /// <param name="perfectCircleState">Mutable Perfect Circle state to advance.</param>
    /// <param name="shooterInheritedVelocity">Current shooter velocity inherited by the radial phase.</param>
    /// <param name="entryDirection">Normalized outward radial direction.</param>
    /// <param name="deltaTime">Step delta to apply.</param>
    /// <param name="globalTime">Absolute world time associated with the end of the step.</param>
    /// <param name="speedMultiplier">Motion multiplier applied on top of the authored entry speed.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <param name="reachedOrbitEntry">True when the radial phase reached the orbit threshold during this step.</param>
    /// <returns>The world-space position reached by the radial phase.</returns>
    private static float3 AdvanceRadialEntry(ref ProjectilePerfectCircleState perfectCircleState,
                                             float3 shooterInheritedVelocity,
                                             float3 entryDirection,
                                             float deltaTime,
                                             float globalTime,
                                             float speedMultiplier,
                                             in PerfectCirclePassiveConfig perfectCircleConfig,
                                             out bool reachedOrbitEntry)
    {
        float radialSpeed = math.max(0f, perfectCircleConfig.RadialEntrySpeed * math.max(0f, speedMultiplier));
        float orbitEntryThreshold = ResolveOrbitEntryThreshold(globalTime, in perfectCircleConfig);
        perfectCircleState.CurrentRadius += radialSpeed * deltaTime;
        perfectCircleState.EntryOrigin += shooterInheritedVelocity * deltaTime;
        perfectCircleState.RadialDirection = entryDirection;

        float3 entryPosition = perfectCircleState.EntryOrigin + entryDirection * perfectCircleState.CurrentRadius;
        entryPosition.y = perfectCircleState.OrbitPlaneHeight;
        reachedOrbitEntry = perfectCircleState.CurrentRadius >= orbitEntryThreshold;
        return entryPosition;
    }

    /// <summary>
    /// Initializes the orbit-phase state and stores the linear continuation used by the transition blend.
    /// </summary>
    /// <param name="perfectCircleState">Mutable Perfect Circle state entering the orbit phase.</param>
    /// <param name="entryPosition">Final world-space position reached by the radial phase.</param>
    /// <param name="shooterPosition">Current shooter position used to derive the orbit angle.</param>
    /// <param name="shooterInheritedVelocity">Current shooter velocity used to preserve motion continuity.</param>
    /// <param name="entryDirection">Normalized outward radial direction.</param>
    /// <param name="speedMultiplier">Motion multiplier applied on top of the authored entry speed.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    private static void InitializeOrbitTransition(ref ProjectilePerfectCircleState perfectCircleState,
                                                  float3 entryPosition,
                                                  float3 shooterPosition,
                                                  float3 shooterInheritedVelocity,
                                                  float3 entryDirection,
                                                  float speedMultiplier,
                                                  in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        float3 entryOffset = entryPosition - shooterPosition;
        entryOffset.y = 0f;
        float entryRadius = math.length(entryOffset);
        float3 orbitEntryDirection = entryRadius > DirectionEpsilon
            ? entryOffset / entryRadius
            : entryDirection;
        float radialSpeed = math.max(0f, perfectCircleConfig.RadialEntrySpeed * math.max(0f, speedMultiplier));

        perfectCircleState.HasEnteredOrbit = 1;
        perfectCircleState.CompletedFullOrbit = 0;
        perfectCircleState.CurrentRadius = math.max(MinimumOrbitRadius, entryRadius);
        perfectCircleState.OrbitAngle = math.atan2(orbitEntryDirection.z, orbitEntryDirection.x);
        perfectCircleState.OrbitBlendProgress = 0f;
        perfectCircleState.AccumulatedOrbitRadians = 0f;
        perfectCircleState.EntryOrigin = entryPosition;
        perfectCircleState.EntryVelocity = shooterInheritedVelocity + entryDirection * radialSpeed;
        perfectCircleState.RadialDirection = entryDirection;
    }

    /// <summary>
    /// Resolves the unblended orbit target for the current step.
    /// </summary>
    /// <param name="perfectCircleState">Mutable Perfect Circle state advanced by the orbit phase.</param>
    /// <param name="shooterPosition">Current shooter position used as orbit center.</param>
    /// <param name="deltaTime">Step delta to apply.</param>
    /// <param name="globalTime">Absolute world time associated with the end of the step.</param>
    /// <param name="speedMultiplier">Motion multiplier applied on top of the authored orbit speed.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <returns>The unblended orbit target position for the current step.</returns>
    private static float3 ResolveOrbitPosition(ref ProjectilePerfectCircleState perfectCircleState,
                                               float3 shooterPosition,
                                               float deltaTime,
                                               float globalTime,
                                               float speedMultiplier,
                                               in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        switch (perfectCircleConfig.PathMode)
        {
            case ProjectileOrbitPathMode.GoldenSpiral:
                return ResolveGoldenSpiralOrbitPosition(ref perfectCircleState,
                                                        shooterPosition,
                                                        deltaTime,
                                                        speedMultiplier,
                                                        in perfectCircleConfig);
            default:
                return ResolveCircularOrbitPosition(ref perfectCircleState,
                                                   shooterPosition,
                                                   deltaTime,
                                                   globalTime,
                                                   speedMultiplier,
                                                   in perfectCircleConfig);
        }
    }

    /// <summary>
    /// Resolves one circular-orbit position using the pulsing-radius configuration.
    /// </summary>
    /// <param name="perfectCircleState">Mutable Perfect Circle state advanced by the circular orbit.</param>
    /// <param name="shooterPosition">Current shooter position used as orbit center.</param>
    /// <param name="deltaTime">Step delta to apply.</param>
    /// <param name="globalTime">Absolute world time associated with the end of the step.</param>
    /// <param name="speedMultiplier">Motion multiplier applied on top of the authored orbit speed.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <returns>The circular-orbit target position reached this step.</returns>
    private static float3 ResolveCircularOrbitPosition(ref ProjectilePerfectCircleState perfectCircleState,
                                                       float3 shooterPosition,
                                                       float deltaTime,
                                                       float globalTime,
                                                       float speedMultiplier,
                                                       in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        float orbitRadius = ResolveCircularOrbitRadius(globalTime, in perfectCircleConfig);
        float orbitSpeed = math.max(0f, perfectCircleConfig.OrbitalSpeed * math.max(0f, speedMultiplier));
        float angularSpeed = orbitRadius > 0.001f ? orbitSpeed / orbitRadius : 0f;
        float angularStep = angularSpeed * deltaTime;
        perfectCircleState.CurrentRadius = orbitRadius;
        perfectCircleState.OrbitAngle += angularStep;

        if (perfectCircleState.CompletedFullOrbit == 0)
        {
            perfectCircleState.AccumulatedOrbitRadians += math.abs(angularStep);

            if (perfectCircleState.AccumulatedOrbitRadians >= math.PI * 2f)
                perfectCircleState.CompletedFullOrbit = 1;
        }

        float cosine = math.cos(perfectCircleState.OrbitAngle);
        float sine = math.sin(perfectCircleState.OrbitAngle);
        float3 orbitOffset = new float3(cosine * orbitRadius, 0f, sine * orbitRadius);
        float3 orbitPosition = shooterPosition + orbitOffset;
        orbitPosition.y = perfectCircleState.OrbitPlaneHeight;
        return orbitPosition;
    }

    /// <summary>
    /// Resolves one golden-spiral orbit position using the authored growth and angular-speed configuration.
    /// </summary>
    /// <param name="perfectCircleState">Mutable Perfect Circle state advanced by the golden spiral.</param>
    /// <param name="shooterPosition">Current shooter position used as orbit center.</param>
    /// <param name="deltaTime">Step delta to apply.</param>
    /// <param name="speedMultiplier">Motion multiplier applied on top of the authored spiral speed.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <returns>The golden-spiral target position reached this step.</returns>
    private static float3 ResolveGoldenSpiralOrbitPosition(ref ProjectilePerfectCircleState perfectCircleState,
                                                           float3 shooterPosition,
                                                           float deltaTime,
                                                           float speedMultiplier,
                                                           in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        const float GoldenRatio = 1.61803398875f;

        float spiralStartRadius = math.max(MinimumOrbitRadius, perfectCircleConfig.SpiralStartRadius);
        float spiralMaximumRadius = math.max(spiralStartRadius, perfectCircleConfig.SpiralMaximumRadius);
        float angularSpeedRadiansPerSecond = math.radians(math.max(0f,
                                                                   perfectCircleConfig.SpiralAngularSpeedDegreesPerSecond *
                                                                   math.max(0f, speedMultiplier)));
        float directionSign = perfectCircleConfig.SpiralClockwise != 0 ? -1f : 1f;
        float angularStep = angularSpeedRadiansPerSecond * deltaTime * directionSign;
        float growthMultiplier = math.max(0f, perfectCircleConfig.SpiralGrowthMultiplier);
        float growthExponent = growthMultiplier > 0f ? math.log(GoldenRatio) * (2f / math.PI) * growthMultiplier : 0f;
        perfectCircleState.OrbitAngle += angularStep;
        perfectCircleState.AccumulatedOrbitRadians += math.abs(angularStep);

        float orbitRadius = growthExponent > 0f
            ? spiralStartRadius * math.exp(growthExponent * perfectCircleState.AccumulatedOrbitRadians)
            : spiralStartRadius;

        if (orbitRadius > spiralMaximumRadius)
            orbitRadius = spiralMaximumRadius;

        perfectCircleState.CurrentRadius = orbitRadius;

        if (perfectCircleState.CompletedFullOrbit == 0)
        {
            float despawnAngleThreshold = math.max(0.1f, perfectCircleConfig.SpiralTurnsBeforeDespawn) * (math.PI * 2f);

            if (perfectCircleState.AccumulatedOrbitRadians >= despawnAngleThreshold ||
                orbitRadius + 0.001f >= spiralMaximumRadius)
            {
                perfectCircleState.CompletedFullOrbit = 1;
            }
        }

        float cosine = math.cos(perfectCircleState.OrbitAngle);
        float sine = math.sin(perfectCircleState.OrbitAngle);
        float3 orbitOffset = new float3(cosine * orbitRadius, 0f, sine * orbitRadius);
        float3 orbitPosition = shooterPosition + orbitOffset;
        orbitPosition.y = perfectCircleState.OrbitPlaneHeight;
        return orbitPosition;
    }

    /// <summary>
    /// Captures the vertical plane used by one orbital projectile before radial entry or orbit blending begins.
    /// </summary>
    /// <param name="perfectCircleState">Mutable trajectory state receiving the resolved plane height.</param>
    /// <param name="shooterPosition">Current shooter position used when an explicit height offset is authored.</param>
    /// <param name="fallbackPosition">Current projectile position used to preserve muzzle height when the offset is zero.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    private static void EnsureOrbitPlaneHeight(ref ProjectilePerfectCircleState perfectCircleState,
                                               float3 shooterPosition,
                                               float3 fallbackPosition,
                                               in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        if (perfectCircleState.HasOrbitPlaneHeight != 0)
            return;

        float configuredPlaneHeight = shooterPosition.y + perfectCircleConfig.HeightOffset;
        perfectCircleState.OrbitPlaneHeight = math.abs(perfectCircleConfig.HeightOffset) > DirectionEpsilon
            ? configuredPlaneHeight
            : math.max(configuredPlaneHeight, fallbackPosition.y);
        perfectCircleState.HasOrbitPlaneHeight = 1;
    }

    /// <summary>
    /// Blends from the straight radial continuation into the orbit target so the entry path does not form a sharp V.
    /// </summary>
    /// <param name="perfectCircleState">Mutable Perfect Circle state storing the blend anchor and progress.</param>
    /// <param name="orbitPosition">Unblended orbit target reached this step.</param>
    /// <param name="deltaTime">Step delta applied to the transition.</param>
    /// <param name="justEnteredOrbit">True when the current step crossed the orbit threshold for the first time.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle configuration.</param>
    /// <returns>The final blended trajectory position for the current step.</returns>
    private static float3 ResolveBlendedOrbitPosition(ref ProjectilePerfectCircleState perfectCircleState,
                                                      float3 orbitPosition,
                                                      float deltaTime,
                                                      bool justEnteredOrbit,
                                                      in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        float blendDuration = math.max(0f, perfectCircleConfig.OrbitBlendDuration);

        if (blendDuration <= 0f)
        {
            perfectCircleState.OrbitBlendProgress = 1f;
            perfectCircleState.EntryOrigin = orbitPosition;
            return orbitPosition;
        }

        if (!justEnteredOrbit)
        {
            float remainingBlendWeight = 1f - math.saturate(perfectCircleState.OrbitBlendProgress);
            perfectCircleState.EntryOrigin += perfectCircleState.EntryVelocity * deltaTime * remainingBlendWeight;
        }

        perfectCircleState.OrbitBlendProgress += deltaTime / blendDuration;
        perfectCircleState.OrbitBlendProgress = math.saturate(perfectCircleState.OrbitBlendProgress);
        float smoothBlend = ResolveSmootherStep01(perfectCircleState.OrbitBlendProgress);
        float3 blendedPosition = math.lerp(perfectCircleState.EntryOrigin, orbitPosition, smoothBlend);

        if (perfectCircleState.OrbitBlendProgress >= 1f)
            perfectCircleState.EntryOrigin = orbitPosition;

        return blendedPosition;
    }

    /// <summary>
    /// Resolves a smoother-step interpolation value to avoid visible hard acceleration changes during orbit entry.
    /// </summary>
    /// <param name="value">Unsaturated interpolation value.</param>
    /// <returns>Smoothed interpolation in the 0-1 range.</returns>
    private static float ResolveSmootherStep01(float value)
    {
        float saturatedValue = math.saturate(value);
        return saturatedValue * saturatedValue * saturatedValue *
               (saturatedValue * (saturatedValue * 6f - 15f) + 10f);
    }
    #endregion

    #endregion
}
