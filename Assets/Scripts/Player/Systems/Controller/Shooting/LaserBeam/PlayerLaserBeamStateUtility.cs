using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Centralizes mutable Laser Beam runtime-state operations shared by simulation, damage and presentation paths.
/// </summary>
internal static class PlayerLaserBeamStateUtility
{
    #region Constants
    private const int MaximumStormTickPulseCount = 64;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resets all transient Laser Beam runtime timers and flags to their idle state.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    /// <param name="stormTickPulses">Mutable pulse buffer owned by the same player entity.</param>
    public static void ResetBeamState(ref PlayerLaserBeamState laserBeamState,
                                      DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses)
    {
        laserBeamState.IsActive = 0;
        laserBeamState.IsOverheated = 0;
        laserBeamState.IsTickReady = 0;
        laserBeamState.LastResolvedPrimaryLaneCount = 0;
        laserBeamState.CooldownRemaining = 0f;
        laserBeamState.ConsecutiveActiveElapsed = 0f;
        laserBeamState.DamageTickTimer = 0f;
        laserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
        ClearStormBurst(ref laserBeamState);
        ClearStormTickPulses(stormTickPulses);
        laserBeamState.NextStormTickPulseId = 1;
        ClearTriggeredActiveLaser(ref laserBeamState);
        ClearChargeImpulse(ref laserBeamState);
    }

    /// <summary>
    /// Synchronizes the transient electrical-storm burst timer with the currently started storm pulse.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    /// <param name="laserBeamConfig">Runtime Laser Beam config that provides pulse travel and hold timing.</param>
    /// <param name="stormTickPulses">Pulse buffer used to resolve the current burst lifetime.</param>
    /// <param name="deltaTime">Unused frame delta kept to preserve the shared update-call shape.</param>
    public static void UpdateStormBurstTimer(ref PlayerLaserBeamState laserBeamState,
                                             in LaserBeamPassiveConfig laserBeamConfig,
                                             in DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                                             float deltaTime)
    {
        float totalDurationSeconds = ResolveStormTickTotalDurationSeconds(in laserBeamConfig);
        laserBeamState.StormBurstRemainingSeconds = ResolveCurrentStormBurstRemainingSeconds(in stormTickPulses,
                                                                                             totalDurationSeconds);
    }

    /// <summary>
    /// Clears the transient electrical-storm burst state when the beam stops or resets.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    public static void ClearStormBurst(ref PlayerLaserBeamState laserBeamState)
    {
        laserBeamState.StormBurstRemainingSeconds = 0f;
    }

    /// <summary>
    /// Advances every active traveling damage packet so presentation and hit coverage share the same elapsed pulse time.
    /// </summary>
    /// <param name="stormTickPulses">Mutable pulse buffer owned by the current player.</param>
    /// <param name="laserBeamConfig">Aggregated Laser Beam passive configuration.</param>
    /// <param name="deltaTime">Frame delta used to advance packet travel.</param>
    public static void AdvanceStormTickPulses(DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                                              in LaserBeamPassiveConfig laserBeamConfig,
                                              float deltaTime)
    {
        if (stormTickPulses.Length <= 0)
            return;

        if (math.max(0f, laserBeamConfig.StormTickTravelSpeed) <= 0f)
        {
            ClearStormTickPulses(stormTickPulses);
            return;
        }

        float safeDeltaTime = math.max(0f, deltaTime);

        if (safeDeltaTime <= 0f)
            return;

        for (int pulseIndex = 0; pulseIndex < stormTickPulses.Length; pulseIndex++)
        {
            PlayerLaserBeamStormTickPulse pulse = stormTickPulses[pulseIndex];
            pulse.CurrentElapsedSeconds += safeDeltaTime;
            stormTickPulses[pulseIndex] = pulse;
        }
    }

    /// <summary>
    /// Removes completed traveling damage packets once their travel and post-travel hold have fully elapsed.
    /// </summary>
    /// <param name="stormTickPulses">Mutable pulse buffer owned by the current player.</param>
    /// <param name="laserBeamConfig">Aggregated Laser Beam passive configuration.</param>
    public static void RemoveCompletedStormTickPulses(DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                                                      in LaserBeamPassiveConfig laserBeamConfig)
    {
        if (stormTickPulses.Length <= 0)
            return;

        float totalDurationSeconds = ResolveStormTickTotalDurationSeconds(in laserBeamConfig);

        if (totalDurationSeconds <= 0f)
        {
            ClearStormTickPulses(stormTickPulses);
            return;
        }

        for (int pulseIndex = stormTickPulses.Length - 1; pulseIndex >= 0; pulseIndex--)
        {
            PlayerLaserBeamStormTickPulse pulse = stormTickPulses[pulseIndex];

            if (pulse.CurrentElapsedSeconds < totalDurationSeconds)
                continue;

            stormTickPulses.RemoveAt(pulseIndex);
        }
    }

    /// <summary>
    /// Clears the transient tick-highlight packet queue stored in the player pulse buffer.
    /// </summary>
    /// <param name="stormTickPulses">Mutable pulse buffer owned by the current player.</param>
    public static void ClearStormTickPulses(DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses)
    {
        stormTickPulses.Clear();
    }

    /// <summary>
    /// Advances the active timed Laser Beam snapshot triggered by non-toggle projectile actives.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    /// <param name="deltaTime">Frame delta used to decrease the remaining active time.</param>
    public static void UpdateTriggeredActiveLaser(ref PlayerLaserBeamState laserBeamState,
                                                  float deltaTime)
    {
        if (laserBeamState.TriggeredActiveRemainingSeconds <= 0f)
            return;

        laserBeamState.TriggeredActiveRemainingSeconds = math.max(0f,
                                                                  laserBeamState.TriggeredActiveRemainingSeconds - math.max(0f, deltaTime));

        if (laserBeamState.TriggeredActiveRemainingSeconds > 0f)
            return;

        ClearTriggeredActiveLaser(ref laserBeamState);
    }

    /// <summary>
    /// Stores one timed Laser Beam snapshot emitted by a non-toggle projectile active.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    /// <param name="durationSeconds">Authored active duration in seconds.</param>
    /// <param name="penetrationMode">Projectile penetration mode resolved at trigger time.</param>
    /// <param name="maximumPenetrations">Maximum penetration budget resolved at trigger time.</param>
    /// <param name="projectileTemplate">Projectile snapshot resolved at trigger time.</param>
    /// <param name="passiveToolsSnapshot">Aggregated passive snapshot resolved at trigger time.</param>
    public static void ActivateTriggeredActiveLaser(ref PlayerLaserBeamState laserBeamState,
                                                    float durationSeconds,
                                                    ProjectilePenetrationMode penetrationMode,
                                                    int maximumPenetrations,
                                                    in PlayerProjectileRequestTemplate projectileTemplate,
                                                    in PlayerPassiveToolsState passiveToolsSnapshot)
    {
        ClearChargeImpulse(ref laserBeamState);
        laserBeamState.TriggeredActiveRemainingSeconds = math.max(0.05f, durationSeconds);
        laserBeamState.TriggeredActivePenetrationMode = penetrationMode;
        laserBeamState.TriggeredActiveMaxPenetrations = math.max(0, maximumPenetrations);
        laserBeamState.TriggeredActiveProjectileTemplate = projectileTemplate;
        laserBeamState.TriggeredActivePassiveSnapshot = BuildTriggeredPassiveSnapshot(in passiveToolsSnapshot);
        laserBeamState.DamageTickTimer = 0f;
        laserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
    }

    /// <summary>
    /// Clears the timed Laser Beam snapshot emitted by non-toggle projectile actives.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    public static void ClearTriggeredActiveLaser(ref PlayerLaserBeamState laserBeamState)
    {
        laserBeamState.TriggeredActiveRemainingSeconds = 0f;
        laserBeamState.TriggeredActivePenetrationMode = ProjectilePenetrationMode.None;
        laserBeamState.TriggeredActiveMaxPenetrations = 0;
        laserBeamState.TriggeredActiveProjectileTemplate = default;
        laserBeamState.TriggeredActivePassiveSnapshot = default;
    }

    /// <summary>
    /// Queues one or more independent traveling damage packets after consuming Laser Beam tick budget.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    /// <param name="stormTickPulses">Mutable pulse buffer owned by the current player.</param>
    /// <param name="laserBeamConfig">Runtime Laser Beam config that provides pulse travel and post-travel hold timing.</param>
    /// <param name="pendingTickCount">Number of damage ticks consumed during the current frame.</param>
    public static void EnqueueStormTickPulses(ref PlayerLaserBeamState laserBeamState,
                                              DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                                              in LaserBeamPassiveConfig laserBeamConfig,
                                              int pendingTickCount)
    {
        if (pendingTickCount <= 0)
            return;

        float totalDurationSeconds = ResolveStormTickTotalDurationSeconds(in laserBeamConfig);

        if (totalDurationSeconds <= 0f)
            return;

        for (int pulseIndex = 0; pulseIndex < pendingTickCount; pulseIndex++)
        {
            if (stormTickPulses.Length >= MaximumStormTickPulseCount)
                stormTickPulses.RemoveAt(0);

            stormTickPulses.Add(new PlayerLaserBeamStormTickPulse
            {
                PulseId = AllocateStormTickPulseId(ref laserBeamState),
                CurrentElapsedSeconds = 0f
            });
        }

        laserBeamState.StormBurstRemainingSeconds = ResolveCurrentStormBurstRemainingSeconds(in stormTickPulses,
                                                                                             totalDurationSeconds);
    }

    /// <summary>
    /// Converts one pulse elapsed time into normalized beam-length progress.
    /// </summary>
    /// <param name="elapsedSeconds">Pulse travel time in seconds.</param>
    /// <param name="travelSpeed">Authored normalized travel speed.</param>
    /// <returns>Normalized pulse progress in the 0-1 range.</returns>
    public static float ResolveNormalizedStormTickProgress(float elapsedSeconds,
                                                           float travelSpeed)
    {
        float safeTravelSpeed = math.max(0f, travelSpeed);

        if (safeTravelSpeed <= 0f)
            return 1f;

        return math.saturate(math.max(0f, elapsedSeconds) * safeTravelSpeed);
    }

    /// <summary>
    /// Resolves the travel duration required by one storm packet to cross the full beam length.
    /// </summary>
    /// <param name="travelSpeed">Authored normalized travel speed.</param>
    /// <returns>Packet travel duration in seconds.</returns>
    public static float ResolveStormTickTravelDurationSeconds(float travelSpeed)
    {
        return 1f / math.max(0.0001f, travelSpeed);
    }

    /// <summary>
    /// Resolves the total lifetime of one storm pulse, including travel and post-travel hold.
    /// </summary>
    /// <param name="laserBeamConfig">Runtime Laser Beam config that provides travel speed and hold time.</param>
    /// <returns>Total pulse lifetime in seconds.</returns>
    public static float ResolveStormTickTotalDurationSeconds(in LaserBeamPassiveConfig laserBeamConfig)
    {
        if (laserBeamConfig.StormTickTravelSpeed <= 0f)
            return 0f;

        float travelDurationSeconds = ResolveStormTickTravelDurationSeconds(laserBeamConfig.StormTickTravelSpeed);
        return travelDurationSeconds + math.max(0f, laserBeamConfig.StormTickPostTravelHoldSeconds);
    }

    /// <summary>
    /// Resolves whether a timed Laser Beam snapshot emitted by a non-toggle projectile active is currently alive.
    /// </summary>
    /// <param name="laserBeamState">Runtime Laser Beam state.</param>
    /// <returns>True when the triggered active snapshot is still active.</returns>
    public static bool HasTriggeredActiveLaser(in PlayerLaserBeamState laserBeamState)
    {
        return laserBeamState.TriggeredActiveRemainingSeconds > 0f &&
               laserBeamState.TriggeredActivePassiveSnapshot.HasLaserBeam != 0;
    }

    /// <summary>
    /// Resolves the passive snapshot that should drive the current Laser Beam frame.
    /// </summary>
    /// <param name="passiveToolsState">Aggregated always-on passive state.</param>
    /// <param name="laserBeamState">Runtime Laser Beam state.</param>
    /// <returns>Effective passive snapshot for the current frame.</returns>
    public static PlayerPassiveToolsState ResolveEffectivePassiveToolsState(in PlayerPassiveToolsState passiveToolsState,
                                                                            in PlayerLaserBeamState laserBeamState)
    {
        if (HasTriggeredActiveLaser(in laserBeamState))
            return BuildPassiveToolsState(in laserBeamState.TriggeredActivePassiveSnapshot);

        return passiveToolsState;
    }

    /// <summary>
    /// Advances the transient Charge Shot impulse timer carried by the Laser Beam runtime state.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    /// <param name="deltaTime">Frame delta used to decrease timers.</param>
    public static void UpdateChargeImpulse(ref PlayerLaserBeamState laserBeamState,
                                           float deltaTime)
    {
        if (laserBeamState.ChargeImpulseRemainingSeconds > 0f)
            laserBeamState.ChargeImpulseRemainingSeconds = math.max(0f, laserBeamState.ChargeImpulseRemainingSeconds - math.max(0f, deltaTime));

        if (laserBeamState.ChargeImpulseRemainingSeconds > 0f)
            return;

        ClearChargeImpulse(ref laserBeamState);
    }

    /// <summary>
    /// Clears the transient Charge Shot impulse modifiers applied to the current beam.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    public static void ClearChargeImpulse(ref PlayerLaserBeamState laserBeamState)
    {
        laserBeamState.ChargeImpulseRemainingSeconds = 0f;
        laserBeamState.ChargeImpulseDamageMultiplier = 0f;
        laserBeamState.ChargeImpulseWidthMultiplier = 0f;
        laserBeamState.ChargeImpulseTravelDistance = 0f;
    }

    /// <summary>
    /// Advances Laser Beam cooldown timers and clears the overheated state once cooldown expires.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    /// <param name="laserBeamConfig">Aggregated Laser Beam passive configuration.</param>
    /// <param name="deltaTime">Frame delta used to decrease timers.</param>
    public static void UpdateCooldown(ref PlayerLaserBeamState laserBeamState,
                                      in LaserBeamPassiveConfig laserBeamConfig,
                                      float deltaTime)
    {
        if (laserBeamState.CooldownRemaining > 0f)
            laserBeamState.CooldownRemaining = math.max(0f, laserBeamState.CooldownRemaining - math.max(0f, deltaTime));

        if (laserBeamState.IsOverheated == 0)
            return;

        if (math.max(0f, laserBeamConfig.CooldownSeconds) <= 0f || laserBeamState.CooldownRemaining <= 0f)
            laserBeamState.IsOverheated = 0;
    }

    /// <summary>
    /// Evaluates whether the current uninterrupted activation window has reached the configured overheating threshold.
    /// </summary>
    /// <param name="laserBeamConfig">Aggregated Laser Beam passive configuration.</param>
    /// <param name="consecutiveActiveElapsed">Current uninterrupted active time.</param>
    /// <returns>True when Laser Beam must enter cooldown.</returns>
    public static bool ShouldOverheat(in LaserBeamPassiveConfig laserBeamConfig,
                                      float consecutiveActiveElapsed)
    {
        if (math.max(0f, laserBeamConfig.CooldownSeconds) <= 0f)
            return false;

        float maximumContinuousActiveSeconds = math.max(0f, laserBeamConfig.MaximumContinuousActiveSeconds);

        if (maximumContinuousActiveSeconds <= 0f)
            return false;

        return consecutiveActiveElapsed >= maximumContinuousActiveSeconds;
    }

    /// <summary>
    /// Resolves the effective bounce budget inherited by the beam from the projectile bounce passive.
    /// </summary>
    /// <param name="passiveToolsState">Aggregated passive runtime state.</param>
    /// <param name="laserBeamConfig">Aggregated Laser Beam passive configuration.</param>
    /// <returns>Effective bounce count used to build reflected segments.</returns>
    public static int ResolveMaximumBounceSegments(in PlayerPassiveToolsState passiveToolsState,
                                                   in LaserBeamPassiveConfig laserBeamConfig)
    {
        if (passiveToolsState.HasBouncingProjectiles == 0)
            return 0;

        int inheritedMaximumBounces = math.max(0, passiveToolsState.BouncingProjectiles.MaxBounces);
        int laserBeamBounceCap = math.max(0, laserBeamConfig.MaximumBounceSegments);

        if (laserBeamBounceCap <= 0)
            return inheritedMaximumBounces;

        return math.min(inheritedMaximumBounces, laserBeamBounceCap);
    }

    /// <summary>
    /// Resolves the last segment currently stored for one lane index.
    /// </summary>
    /// <param name="laserBeamLanes">Current lane buffer.</param>
    /// <param name="laneIndex">Lane index to inspect.</param>
    /// <param name="terminalSegment">Last segment found for the requested lane.</param>
    /// <returns>True when the requested lane exists in the buffer.</returns>
    public static bool TryResolveTerminalSegment(DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                                 int laneIndex,
                                                 out PlayerLaserBeamLaneElement terminalSegment)
    {
        terminalSegment = default;
        bool foundLane = false;

        for (int segmentIndex = 0; segmentIndex < laserBeamLanes.Length; segmentIndex++)
        {
            PlayerLaserBeamLaneElement currentSegment = laserBeamLanes[segmentIndex];

            if (currentSegment.LaneIndex != laneIndex)
                continue;

            terminalSegment = currentSegment;
            foundLane = true;
        }

        return foundLane;
    }

    /// <summary>
    /// Rotates one planar forward direction around the world up axis by the requested angle in degrees.
    /// </summary>
    /// <param name="direction">Source forward direction.</param>
    /// <param name="angleDegrees">Signed planar angle in degrees.</param>
    /// <returns>The normalized rotated planar direction.</returns>
    public static float3 RotatePlanarDirection(float3 direction,
                                               float angleDegrees)
    {
        float radians = math.radians(angleDegrees);
        quaternion rotationOffset = quaternion.AxisAngle(new float3(0f, 1f, 0f), radians);
        float3 rotatedDirection = math.rotate(rotationOffset, math.normalizesafe(direction, new float3(0f, 0f, 1f)));
        return math.normalizesafe(rotatedDirection, new float3(0f, 0f, 1f));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Allocates the next positive pulse id and advances the state counter with wrap protection.
    /// </summary>
    /// <param name="laserBeamState">Mutable beam state that stores the next pulse id.</param>
    /// <returns>Positive id assigned to the newly queued pulse.</returns>
    private static int AllocateStormTickPulseId(ref PlayerLaserBeamState laserBeamState)
    {
        if (laserBeamState.NextStormTickPulseId <= 0)
            laserBeamState.NextStormTickPulseId = 1;

        int pulseId = laserBeamState.NextStormTickPulseId;
        laserBeamState.NextStormTickPulseId = pulseId == int.MaxValue ? 1 : pulseId + 1;
        return pulseId;
    }

    /// <summary>
    /// Resolves the remaining burst lifetime of the oldest started pulse currently driving the storm visuals.
    /// </summary>
    /// <param name="stormTickPulses">Pulse buffer containing the active storm packets.</param>
    /// <param name="totalDurationSeconds">Total duration of one pulse including travel and hold.</param>
    /// <returns>Remaining burst lifetime in seconds, or 0 when no pulse is currently started.</returns>
    private static float ResolveCurrentStormBurstRemainingSeconds(in DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                                                                  float totalDurationSeconds)
    {
        if (stormTickPulses.Length <= 0 || totalDurationSeconds <= 0f)
            return 0f;

        for (int pulseIndex = 0; pulseIndex < stormTickPulses.Length; pulseIndex++)
        {
            PlayerLaserBeamStormTickPulse pulse = stormTickPulses[pulseIndex];

            if (pulse.CurrentElapsedSeconds < 0f || pulse.CurrentElapsedSeconds >= totalDurationSeconds)
                continue;

            return totalDurationSeconds - pulse.CurrentElapsedSeconds;
        }

        return 0f;
    }

    /// <summary>
    /// Copies only the passive modules the Laser Beam active needs while it owns a timed snapshot.
    /// </summary>
    /// <param name="passiveToolsState">Full passive state resolved when the active was triggered.</param>
    /// <returns>Compact snapshot stored inside PlayerLaserBeamState.</returns>
    private static PlayerLaserBeamPassiveSnapshot BuildTriggeredPassiveSnapshot(in PlayerPassiveToolsState passiveToolsState)
    {
        return new PlayerLaserBeamPassiveSnapshot
        {
            HasLaserBeam = passiveToolsState.HasLaserBeam,
            LaserBeam = passiveToolsState.LaserBeam,
            HasPerfectCircle = passiveToolsState.HasPerfectCircle,
            PerfectCircle = passiveToolsState.PerfectCircle,
            HasShotgun = passiveToolsState.HasShotgun,
            Shotgun = passiveToolsState.Shotgun,
            HasBouncingProjectiles = passiveToolsState.HasBouncingProjectiles,
            BouncingProjectiles = passiveToolsState.BouncingProjectiles,
            HasSplittingProjectiles = passiveToolsState.HasSplittingProjectiles,
            SplittingProjectiles = passiveToolsState.SplittingProjectiles
        };
    }

    /// <summary>
    /// Rehydrates the compact timed Laser Beam snapshot into the passive state shape expected by shared beam code.
    /// </summary>
    /// <param name="snapshot">Compact timed active snapshot stored on PlayerLaserBeamState.</param>
    /// <returns>Passive state containing only the Laser Beam relevant modules.</returns>
    private static PlayerPassiveToolsState BuildPassiveToolsState(in PlayerLaserBeamPassiveSnapshot snapshot)
    {
        return new PlayerPassiveToolsState
        {
            ProjectileSizeMultiplier = 1f,
            ProjectileDamageMultiplier = 1f,
            ProjectileSpeedMultiplier = 1f,
            ProjectileLifetimeSecondsMultiplier = 1f,
            ProjectileLifetimeRangeMultiplier = 1f,
            HasShotgun = snapshot.HasShotgun,
            Shotgun = snapshot.Shotgun,
            HasPerfectCircle = snapshot.HasPerfectCircle,
            PerfectCircle = snapshot.PerfectCircle,
            HasBouncingProjectiles = snapshot.HasBouncingProjectiles,
            BouncingProjectiles = snapshot.BouncingProjectiles,
            HasSplittingProjectiles = snapshot.HasSplittingProjectiles,
            SplittingProjectiles = snapshot.SplittingProjectiles,
            HasLaserBeam = snapshot.HasLaserBeam,
            LaserBeam = snapshot.LaserBeam
        };
    }
    #endregion

    #endregion
}
