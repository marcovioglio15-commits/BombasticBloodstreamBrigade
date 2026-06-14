using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes runtime phase handling and path sampling for the short-range dash movement override.
/// </summary>
internal static class EnemyPatternShortRangeDashUtility
{
    #region Constants
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumDashDuration = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether the dash override is currently in its committed dash phase.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <returns>True when the dash is currently executing its committed motion.</returns>
    public static bool IsCommitted(in EnemyPatternRuntimeState patternRuntimeState)
    {
        return patternRuntimeState.ShortRangeDashPhase == EnemyShortRangeDashPhase.Dashing;
    }

    /// <summary>
    /// Returns whether the dash override is currently aiming or dashing.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <returns>True when the dash owns short-range runtime state.</returns>
    public static bool IsActive(in EnemyPatternRuntimeState patternRuntimeState)
    {
        return patternRuntimeState.ShortRangeDashPhase != EnemyShortRangeDashPhase.Idle;
    }

    /// <summary>
    /// Returns whether the short-range dash is currently in its post-dash recovery cooldown.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <returns>True when the dash cannot start a new aim cycle yet.</returns>
    public static bool IsCoolingDown(in EnemyPatternRuntimeState patternRuntimeState)
    {
        return patternRuntimeState.ShortRangeDashCooldownRemaining > 0f;
    }

    /// <summary>
    /// Returns whether the short-range dash can currently take over movement or continue an already-started dash phase.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <returns>True when the dash override is allowed to drive movement.</returns>
    public static bool IsAvailableForTakeover(in EnemyPatternRuntimeState patternRuntimeState)
    {
        if (patternRuntimeState.ShortRangeDashPhase != EnemyShortRangeDashPhase.Idle)
            return true;

        return !IsCoolingDown(in patternRuntimeState);
    }

    /// <summary>
    /// Advances the post-dash recovery cooldown while the enemy is back on its core movement module.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    public static void UpdateCooldown(ref EnemyPatternRuntimeState patternRuntimeState, float deltaTime)
    {
        if (patternRuntimeState.ShortRangeDashCooldownRemaining <= 0f)
            return;

        patternRuntimeState.ShortRangeDashCooldownRemaining = math.max(0f,
                                                                       patternRuntimeState.ShortRangeDashCooldownRemaining - math.max(0f, deltaTime));
    }

    /// <summary>
    /// Clears non-committed dash state after the player leaves the short-range band.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    public static void HandleShortRangeBandReleased(ref EnemyPatternRuntimeState patternRuntimeState)
    {
        if (patternRuntimeState.ShortRangeDashPhase != EnemyShortRangeDashPhase.Aiming)
            return;

        ResetPhase(ref patternRuntimeState, false);
    }

    /// <summary>
    /// Aborts the committed dash after a strong wall block so the enemy can restart the telegraph cleanly.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    public static void HandleWallHit(in EnemyPatternConfig patternConfig, ref EnemyPatternRuntimeState patternRuntimeState)
    {
        switch (patternRuntimeState.ShortRangeDashPhase)
        {
            case EnemyShortRangeDashPhase.Dashing:
                EndCommittedDash(in patternConfig, ref patternRuntimeState, true);
                return;

            case EnemyShortRangeDashPhase.Aiming:
                ResetPhase(ref patternRuntimeState, false);
                return;

            default:
                ResetPhase(ref patternRuntimeState, false);
                return;
        }
    }

    /// <summary>
    /// Resolves the dash-owned desired velocity for the current frame and advances internal dash phases.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity used for deterministic random-side selection.</param>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="enemyPosition">Current enemy world position.</param>
    /// <param name="playerPosition">Current player world position.</param>
    /// <param name="moveSpeed">Current resolved enemy move speed after external slow modifiers.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    /// <param name="elapsedTime">Current world elapsed time in seconds.</param>
    /// <param name="resolvedPhase">Active dash phase that produced the returned velocity for this frame.</param>
    /// <returns>Desired planar velocity for the current frame.</returns>
    public static float3 ResolveVelocity(Entity enemyEntity,
                                         in EnemyPatternConfig patternConfig,
                                         ref EnemyPatternRuntimeState patternRuntimeState,
                                         float3 enemyPosition,
                                         float3 playerPosition,
                                         float moveSpeed,
                                         float deltaTime,
                                         float elapsedTime,
                                         out EnemyShortRangeDashPhase resolvedPhase)
    {
        float safeDeltaTime = math.max(0f, deltaTime);
        float3 toPlayer = playerPosition - enemyPosition;
        toPlayer.y = 0f;
        float playerDistance = math.length(toPlayer);
        float3 playerDirection = math.normalizesafe(toPlayer, ResolveFallbackDirection(in patternRuntimeState));
        EnemyShortRangeDashPhase phase = patternRuntimeState.ShortRangeDashPhase;

        switch (phase)
        {
            case EnemyShortRangeDashPhase.Idle:
                BeginAim(ref patternRuntimeState,
                         enemyPosition,
                         playerDirection,
                         ResolveTravelDistance(in patternConfig, playerDistance));

                if (!ShouldStartDash(in patternConfig, in patternRuntimeState, safeDeltaTime))
                {
                    resolvedPhase = EnemyShortRangeDashPhase.Aiming;
                    return ResolveAimVelocity(in patternConfig, playerDirection, moveSpeed);
                }

                BeginDash(enemyEntity,
                          in patternConfig,
                          ref patternRuntimeState,
                          enemyPosition,
                          playerDirection,
                          playerDistance,
                          elapsedTime);
                resolvedPhase = EnemyShortRangeDashPhase.Dashing;
                return ResolveDashStepVelocity(in patternConfig,
                                               ref patternRuntimeState,
                                               enemyPosition,
                                               safeDeltaTime,
                                               true);

            case EnemyShortRangeDashPhase.Aiming:
                patternRuntimeState.ShortRangeDashOrigin = enemyPosition;
                patternRuntimeState.ShortRangeDashAimDirection = playerDirection;
                patternRuntimeState.ShortRangeDashTravelDistance = ResolveTravelDistance(in patternConfig, playerDistance);
                resolvedPhase = EnemyShortRangeDashPhase.Aiming;

                if (!ShouldStartDash(in patternConfig, in patternRuntimeState, safeDeltaTime))
                {
                    patternRuntimeState.ShortRangeDashPhaseElapsed += safeDeltaTime;
                    return ResolveAimVelocity(in patternConfig, playerDirection, moveSpeed);
                }

                BeginDash(enemyEntity,
                          in patternConfig,
                          ref patternRuntimeState,
                          enemyPosition,
                          playerDirection,
                          playerDistance,
                          elapsedTime);
                resolvedPhase = EnemyShortRangeDashPhase.Dashing;
                return ResolveDashStepVelocity(in patternConfig,
                                               ref patternRuntimeState,
                                               enemyPosition,
                                               safeDeltaTime,
                                               true);

            case EnemyShortRangeDashPhase.Dashing:
                resolvedPhase = EnemyShortRangeDashPhase.Dashing;
                return ResolveDashStepVelocity(in patternConfig,
                                               ref patternRuntimeState,
                                               enemyPosition,
                                               safeDeltaTime,
                                               false);

            default:
                ResetPhase(ref patternRuntimeState, false);
                resolvedPhase = EnemyShortRangeDashPhase.Idle;
                return float3.zero;
        }
    }

    /// <summary>
    /// Resolves the current facing direction used by aim and dash phases.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="lookDirection">Resolved planar look direction.</param>
    /// <returns>True when a dash-owned look direction is currently available.</returns>
    public static bool TryResolveLookDirection(in EnemyPatternRuntimeState patternRuntimeState, out float3 lookDirection)
    {
        lookDirection = math.normalizesafe(patternRuntimeState.ShortRangeDashAimDirection, ForwardAxis);

        if (patternRuntimeState.ShortRangeDashPhase == EnemyShortRangeDashPhase.Idle)
            return false;

        return math.lengthsq(lookDirection) > DirectionEpsilon;
    }

    /// <summary>
    /// Resolves one world-space point along the sampled dash path for debug drawing and runtime previews.
    /// </summary>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="normalizedProgress">Requested normalized progress in the 0-1 range.</param>
    /// <returns>World-space point on the authored dash path.</returns>
    public static float3 ResolvePathPoint(in EnemyPatternConfig patternConfig,
                                          in EnemyPatternRuntimeState patternRuntimeState,
                                          float normalizedProgress)
    {
        float3 forwardDirection = math.normalizesafe(patternRuntimeState.ShortRangeDashAimDirection, ForwardAxis);
        float3 rightDirection = ResolveRightDirection(forwardDirection);
        float2 sampledPoint = SamplePath(in patternConfig.ShortRangeDashPathSamples, normalizedProgress);
        float forwardDistance = sampledPoint.y * math.max(0f, patternRuntimeState.ShortRangeDashTravelDistance);
        float lateralDistance = sampledPoint.x *
                                math.max(0f, patternConfig.ShortRangeDashLateralAmplitude) *
                                ResolveMirrorSignMagnitude(in patternConfig, in patternRuntimeState);
        return patternRuntimeState.ShortRangeDashOrigin +
               forwardDirection * forwardDistance +
               rightDirection * lateralDistance;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Starts the short telegraph phase for the short-range dash.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="aimDirection">Current player-facing aim direction.</param>
    /// <param name="travelDistance">Current resolved travel distance preview.</param>
    private static void BeginAim(ref EnemyPatternRuntimeState patternRuntimeState,
                                 float3 enemyPosition,
                                 float3 aimDirection,
                                 float travelDistance)
    {
        patternRuntimeState.ShortRangeDashPhase = EnemyShortRangeDashPhase.Aiming;
        patternRuntimeState.ShortRangeDashPhaseElapsed = 0f;
        patternRuntimeState.ShortRangeDashOrigin = enemyPosition;
        patternRuntimeState.ShortRangeDashAimDirection = aimDirection;
        patternRuntimeState.ShortRangeDashTravelDistance = travelDistance;
    }

    /// <summary>
    /// Starts the committed dash phase by locking origin, direction, travel distance and lateral side.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity used for deterministic random-side selection.</param>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="enemyPosition">Current enemy world position.</param>
    /// <param name="playerDirection">Current player-facing aim direction.</param>
    /// <param name="playerDistance">Current planar player distance.</param>
    /// <param name="elapsedTime">Current world elapsed time in seconds.</param>
    private static void BeginDash(Entity enemyEntity,
                                  in EnemyPatternConfig patternConfig,
                                  ref EnemyPatternRuntimeState patternRuntimeState,
                                  float3 enemyPosition,
                                  float3 playerDirection,
                                  float playerDistance,
                                  float elapsedTime)
    {
        patternRuntimeState.ShortRangeDashPhase = EnemyShortRangeDashPhase.Dashing;
        patternRuntimeState.ShortRangeDashPhaseElapsed = 0f;
        patternRuntimeState.ShortRangeDashCooldownRemaining = 0f;
        patternRuntimeState.ShortRangeDashOrigin = enemyPosition;
        patternRuntimeState.ShortRangeDashAimDirection = playerDirection;
        patternRuntimeState.ShortRangeDashTravelDistance = ResolveTravelDistance(in patternConfig, playerDistance);
        patternRuntimeState.ShortRangeDashLateralSign = ResolveMirrorSign(enemyEntity,
                                                                          patternConfig.ShortRangeDashMirrorMode,
                                                                          patternRuntimeState.ShortRangeDashLateralSign,
                                                                          elapsedTime);
    }

    /// <summary>
    /// Resolves one desired velocity step along the sampled dash path and advances dash time.
    /// </summary>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="enemyPosition">Current enemy world position.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    /// <param name="startedThisFrame">True when the dash phase started on the current frame.</param>
    /// <returns>Desired planar velocity for the current dash step.</returns>
    private static float3 ResolveDashStepVelocity(in EnemyPatternConfig patternConfig,
                                                  ref EnemyPatternRuntimeState patternRuntimeState,
                                                  float3 enemyPosition,
                                                  float deltaTime,
                                                  bool startedThisFrame)
    {
        float safeDeltaTime = math.max(0.0001f, deltaTime);
        float dashDuration = math.max(MinimumDashDuration, patternConfig.ShortRangeDashDuration);
        float previousElapsed = startedThisFrame ? 0f : math.max(0f, patternRuntimeState.ShortRangeDashPhaseElapsed);
        float nextElapsed = math.min(dashDuration, previousElapsed + safeDeltaTime);
        float nextProgress = math.saturate(nextElapsed / dashDuration);
        float3 nextTargetPoint = ResolvePathPoint(in patternConfig, in patternRuntimeState, nextProgress);
        float3 desiredVelocity = (nextTargetPoint - enemyPosition) / safeDeltaTime;
        desiredVelocity.y = 0f;
        patternRuntimeState.ShortRangeDashPhaseElapsed = nextElapsed;

        if (nextElapsed + DirectionEpsilon >= dashDuration)
            EndCommittedDash(in patternConfig, ref patternRuntimeState, true);

        return desiredVelocity;
    }

    /// <summary>
    /// Returns whether the telegraph phase should transition into a committed dash on this frame.
    /// </summary>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    /// <returns>True when the aim phase has reached its release threshold.</returns>
    private static bool ShouldStartDash(in EnemyPatternConfig patternConfig,
                                        in EnemyPatternRuntimeState patternRuntimeState,
                                        float deltaTime)
    {
        float aimDuration = math.max(0f, patternConfig.ShortRangeDashAimDuration);

        if (aimDuration <= 0f)
            return true;

        return patternRuntimeState.ShortRangeDashPhaseElapsed + math.max(0f, deltaTime) >= aimDuration;
    }

    /// <summary>
    /// Resolves the authored movement speed used while the enemy is still taking aim.
    /// </summary>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <param name="aimDirection">Current player-facing aim direction.</param>
    /// <param name="moveSpeed">Current resolved enemy move speed.</param>
    /// <returns>Desired telegraph velocity.</returns>
    private static float3 ResolveAimVelocity(in EnemyPatternConfig patternConfig,
                                             float3 aimDirection,
                                             float moveSpeed)
    {
        float resolvedMoveSpeed = math.max(0f, moveSpeed) * math.max(0f, patternConfig.ShortRangeDashAimMoveSpeedMultiplier);
        return aimDirection * resolvedMoveSpeed;
    }

    /// <summary>
    /// Resolves one dash travel distance from the current player distance and the authored distance model.
    /// </summary>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <param name="playerDistance">Current planar player distance.</param>
    /// <returns>Resolved dash travel distance.</returns>
    private static float ResolveTravelDistance(in EnemyPatternConfig patternConfig, float playerDistance)
    {
        float resolvedTravelDistance;

        switch (patternConfig.ShortRangeDashDistanceSource)
        {
            case EnemyShortRangeDashDistanceSource.FixedDistance:
                resolvedTravelDistance = math.max(0f, patternConfig.ShortRangeDashFixedDistance);
                break;

            default:
                resolvedTravelDistance = math.max(0f, playerDistance) * math.max(0f, patternConfig.ShortRangeDashDistanceMultiplier) +
                                         patternConfig.ShortRangeDashDistanceOffset;
                break;
        }

        float minimumTravelDistance = math.max(0f, patternConfig.ShortRangeDashMinimumTravelDistance);
        float maximumTravelDistance = math.max(minimumTravelDistance, patternConfig.ShortRangeDashMaximumTravelDistance);
        return math.clamp(resolvedTravelDistance, minimumTravelDistance, maximumTravelDistance);
    }

    /// <summary>
    /// Resolves the lateral side sign used for the next committed dash.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity used for deterministic random-side selection.</param>
    /// <param name="mirrorMode">Authored mirror mode.</param>
    /// <param name="previousSign">Previously used lateral sign.</param>
    /// <param name="elapsedTime">Current world elapsed time in seconds.</param>
    /// <returns>Lateral sign in the -1 or +1 range.</returns>
    private static float ResolveMirrorSign(Entity enemyEntity,
                                           EnemyShortRangeDashMirrorMode mirrorMode,
                                           float previousSign,
                                           float elapsedTime)
    {
        switch (mirrorMode)
        {
            case EnemyShortRangeDashMirrorMode.Left:
                return -1f;

            case EnemyShortRangeDashMirrorMode.Alternate:
                if (previousSign < 0f)
                    return 1f;

                return -1f;

            case EnemyShortRangeDashMirrorMode.Random:
                uint seed = math.hash(new int3(enemyEntity.Index,
                                               enemyEntity.Version,
                                               (int)math.floor(math.max(0f, elapsedTime) * 31f)));
                return (seed & 1u) == 0u ? -1f : 1f;

            default:
                return 1f;
        }
    }

    /// <summary>
    /// Samples one normalized local path point from the baked dash path samples.
    /// </summary>
    /// <param name="pathSamples">Baked dash path samples.</param>
    /// <param name="normalizedProgress">Requested normalized progress in the 0-1 range.</param>
    /// <returns>Sampled normalized local path point.</returns>
    private static float2 SamplePath(in FixedList128Bytes<float2> pathSamples, float normalizedProgress)
    {
        int sampleCount = pathSamples.Length;

        if (sampleCount <= 0)
            return new float2(0f, math.saturate(normalizedProgress));

        if (sampleCount == 1)
            return pathSamples[0];

        float safeProgress = math.saturate(normalizedProgress);
        float scaledIndex = safeProgress * (sampleCount - 1);
        int lowerIndex = math.clamp((int)math.floor(scaledIndex), 0, sampleCount - 1);
        int upperIndex = math.clamp(lowerIndex + 1, 0, sampleCount - 1);

        if (lowerIndex == upperIndex)
            return pathSamples[lowerIndex];

        float interpolation = math.saturate(scaledIndex - lowerIndex);
        return math.lerp(pathSamples[lowerIndex], pathSamples[upperIndex], interpolation);
    }

    /// <summary>
    /// Resolves a stable fallback forward direction when the player is exactly on top of the enemy.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <returns>Safe normalized fallback direction.</returns>
    private static float3 ResolveFallbackDirection(in EnemyPatternRuntimeState patternRuntimeState)
    {
        return math.normalizesafe(patternRuntimeState.ShortRangeDashAimDirection, ForwardAxis);
    }

    /// <summary>
    /// Resolves one planar right vector from the current forward direction.
    /// </summary>
    /// <param name="forwardDirection">Current normalized forward direction.</param>
    /// <returns>Normalized planar right direction.</returns>
    private static float3 ResolveRightDirection(float3 forwardDirection)
    {
        float3 rightDirection = new float3(forwardDirection.z, 0f, -forwardDirection.x);
        return math.normalizesafe(rightDirection, new float3(1f, 0f, 0f));
    }

    /// <summary>
    /// Resolves the stored dash side sign while guaranteeing a usable magnitude.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <returns>Stable -1 or +1 sign.</returns>
    private static float ResolveMirrorSignMagnitude(in EnemyPatternConfig patternConfig,
                                                    in EnemyPatternRuntimeState patternRuntimeState)
    {
        if (patternRuntimeState.ShortRangeDashLateralSign < 0f)
            return -1f;

        if (patternRuntimeState.ShortRangeDashLateralSign > 0f)
            return 1f;

        if (patternConfig.ShortRangeDashMirrorMode == EnemyShortRangeDashMirrorMode.Left ||
            patternConfig.ShortRangeDashMirrorMode == EnemyShortRangeDashMirrorMode.Alternate)
        {
            return -1f;
        }

        return 1f;
    }

    /// <summary>
    /// Resets transient dash phase data while optionally keeping the last used lateral side for alternating paths.
    /// </summary>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="preserveLateralSign">True to keep the last side for the next alternating dash.</param>
    private static void ResetPhase(ref EnemyPatternRuntimeState patternRuntimeState, bool preserveLateralSign)
    {
        float previousLateralSign = preserveLateralSign ? patternRuntimeState.ShortRangeDashLateralSign : 0f;
        patternRuntimeState.ShortRangeDashPhase = EnemyShortRangeDashPhase.Idle;
        patternRuntimeState.ShortRangeDashPhaseElapsed = 0f;
        patternRuntimeState.ShortRangeDashOrigin = float3.zero;
        patternRuntimeState.ShortRangeDashTravelDistance = 0f;
        patternRuntimeState.ShortRangeDashAimDirection = ResolveFallbackDirection(in patternRuntimeState);
        patternRuntimeState.ShortRangeDashLateralSign = previousLateralSign;
    }

    /// <summary>
    /// Ends a committed dash, restores idle phase state and starts the authored recovery cooldown.
    /// </summary>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="preserveLateralSign">True to keep the last side for the next alternating dash.</param>
    private static void EndCommittedDash(in EnemyPatternConfig patternConfig,
                                         ref EnemyPatternRuntimeState patternRuntimeState,
                                         bool preserveLateralSign)
    {
        ResetPhase(ref patternRuntimeState, preserveLateralSign);
        patternRuntimeState.ShortRangeDashCooldownRemaining = math.max(0f, patternConfig.ShortRangeDashCooldownSeconds);
    }
    #endregion

    #endregion
}
