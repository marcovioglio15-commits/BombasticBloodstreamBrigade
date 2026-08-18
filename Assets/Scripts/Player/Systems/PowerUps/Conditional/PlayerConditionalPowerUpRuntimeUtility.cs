using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Resolves conditional shot cadence, automatic Sudden Strike charge, and movement-slow recovery without managed allocations.
/// </summary>
public static class PlayerConditionalPowerUpRuntimeUtility
{
    #region Constants
    private const float HealthThresholdEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region State Update
    /// <summary>
    /// Advances one Sudden Strike state from movement, rotation, and actual spawned-shot history.
    /// </summary>
    /// <param name="config">Conditional application settings and baked Trigger Hold Charge payload.</param>
    /// <param name="deltaTime">Current frame duration.</param>
    /// <param name="movementState">Current player movement state.</param>
    /// <param name="lookState">Current player look state.</param>
    /// <param name="shotPulseVersion">Monotonic version incremented after a real base-shot volley spawns.</param>
    /// <param name="runtimeState">Mutable instance state receiving charge and recovery updates.</param>
    public static void UpdateSuddenStrike(in PowerUpConditionalApplicationConfig config,
                                          float deltaTime,
                                          in PlayerMovementState movementState,
                                          in PlayerLookState lookState,
                                          uint shotPulseVersion,
                                          ref PowerUpConditionalApplicationRuntimeState runtimeState)
    {
        if (config.Mode != PowerUpConditionalApplicationMode.SuddenStrike)
        {
            Reset(ref runtimeState);
            return;
        }

        bool shotOccurred = runtimeState.Initialized != 0 && runtimeState.LastObservedShotPulseVersion != shotPulseVersion;
        runtimeState.Initialized = 1;
        runtimeState.LastObservedShotPulseVersion = shotPulseVersion;
        bool conditionSatisfied = ResolveSuddenStrikeCondition(in config,
                                                               in movementState,
                                                               in lookState,
                                                               shotOccurred);

        if (conditionSatisfied && runtimeState.Armed == 0)
        {
            float requiredCharge = math.max(0f, config.HoldCharge.RequiredCharge);
            float maximumCharge = math.max(requiredCharge, config.HoldCharge.MaximumCharge);
            runtimeState.Charge = math.min(maximumCharge,
                                           runtimeState.Charge + math.max(0f, config.HoldCharge.ChargeRatePerSecond) * math.max(0f, deltaTime));

            if (requiredCharge <= 0f || runtimeState.Charge >= requiredCharge)
                runtimeState.Armed = 1;
        }
        else if (!conditionSatisfied && runtimeState.Armed == 0)
        {
            runtimeState.Charge = 0f;
        }

        UpdateMovementSlow(in config,
                           conditionSatisfied,
                           deltaTime,
                           ref runtimeState);
    }

    /// <summary>
    /// Advances an every-X-shots counter or consumes an armed Sudden Strike for the current base-shot volley.
    /// </summary>
    /// <param name="config">Conditional application settings to evaluate.</param>
    /// <param name="runtimeState">Mutable instance counter or charge state.</param>
    /// <returns>True when sibling shooting effects must apply to the current volley.</returns>
    public static bool TryConsumeQualifiedShot(in PowerUpConditionalApplicationConfig config,
                                               ref PowerUpConditionalApplicationRuntimeState runtimeState)
    {
        switch (config.Mode)
        {
            case PowerUpConditionalApplicationMode.DelayedShootApplication:
                runtimeState.ShotCounter++;

                if (runtimeState.ShotCounter < math.max(1, config.DelayedShotInterval))
                    return false;

                runtimeState.ShotCounter = 0;
                return true;
            case PowerUpConditionalApplicationMode.SuddenStrike:
                if (runtimeState.Armed == 0)
                    return false;

                runtimeState.Armed = 0;
                runtimeState.Charge = 0f;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Clears all mutable conditional state when its power-up instance is removed or deactivated.
    /// </summary>
    /// <param name="runtimeState">Mutable instance state reset to its neutral value.</param>
    public static void Reset(ref PowerUpConditionalApplicationRuntimeState runtimeState)
    {
        runtimeState = default;
    }
    #endregion

    #region Movement Slow
    /// <summary>
    /// Returns the already resolved movement-slow percentage retained by one conditional runtime instance.
    /// </summary>
    /// <param name="runtimeState">Conditional runtime state containing current slow recovery progress.</param>
    /// <returns>Movement slow percentage in the 0-100 range.</returns>
    public static float ResolveMovementSlowPercent(in PowerUpConditionalApplicationRuntimeState runtimeState)
    {
        return math.clamp(runtimeState.MovementSlowPercent, 0f, 100f);
    }

    /// <summary>
    /// Samples the fixed normalized charge curve shared with manual Trigger Hold Charge movement slowdown.
    /// </summary>
    /// <param name="samples">Baked normalized charge-curve samples.</param>
    /// <param name="normalizedCharge">Current charge ratio in the 0-1 range.</param>
    /// <returns>Normalized curve output in the 0-1 range.</returns>
    public static float SampleNormalizedSlowCurve(in FixedList128Bytes<float> samples, float normalizedCharge)
    {
        float clampedCharge = math.saturate(normalizedCharge);
        int sampleCount = samples.Length;

        if (sampleCount <= 0)
            return clampedCharge;

        if (sampleCount == 1)
            return math.saturate(samples[0]);

        float scaledSampleIndex = clampedCharge * (sampleCount - 1);
        int lowerSampleIndex = math.clamp((int)math.floor(scaledSampleIndex), 0, sampleCount - 1);
        int upperSampleIndex = math.min(lowerSampleIndex + 1, sampleCount - 1);
        float interpolation = math.saturate(scaledSampleIndex - lowerSampleIndex);
        return math.lerp(math.saturate(samples[lowerSampleIndex]),
                         math.saturate(samples[upperSampleIndex]),
                         interpolation);
    }

    /// <summary>
    /// Applies the authored charge curve while the condition is satisfied and recovers linearly after interruption.
    /// </summary>
    /// <param name="config">Sudden Strike and Trigger Hold Charge settings.</param>
    /// <param name="conditionSatisfied">Whether automatic charge is currently allowed.</param>
    /// <param name="deltaTime">Current frame duration.</param>
    /// <param name="runtimeState">Mutable state receiving the resolved slow percentage.</param>
    private static void UpdateMovementSlow(in PowerUpConditionalApplicationConfig config,
                                           bool conditionSatisfied,
                                           float deltaTime,
                                           ref PowerUpConditionalApplicationRuntimeState runtimeState)
    {
        float maximumSlowPercent = math.clamp(config.HoldCharge.MaximumPlayerSlowPercent, 0f, 100f);
        bool shouldApply = conditionSatisfied &&
                           config.ApplyChargeMovementSlow != 0 &&
                           config.HoldCharge.SlowPlayerWhileCharging != 0 &&
                           maximumSlowPercent > 0f;

        if (shouldApply)
        {
            float maximumCharge = math.max(config.HoldCharge.RequiredCharge, config.HoldCharge.MaximumCharge);
            float normalizedCharge = maximumCharge > 0f ? math.saturate(runtimeState.Charge / maximumCharge) : 1f;
            runtimeState.MovementSlowPercent = maximumSlowPercent *
                                               SampleNormalizedSlowCurve(in config.HoldCharge.PlayerSlowCurveSamples,
                                                                         normalizedCharge);
            return;
        }

        float recoverySeconds = math.max(0f, config.MovementSlowRecoverySeconds);

        if (recoverySeconds <= 0f || maximumSlowPercent <= 0f)
        {
            runtimeState.MovementSlowPercent = 0f;
            return;
        }

        runtimeState.MovementSlowPercent = math.max(0f,
                                                    runtimeState.MovementSlowPercent -
                                                    maximumSlowPercent * math.max(0f, deltaTime) / recoverySeconds);
    }
    #endregion

    #region Conditions
    /// <summary>
    /// Evaluates an authored percentage or direct health threshold against authoritative health values.
    /// </summary>
    /// <param name="config">Self-preservation threshold configuration.</param>
    /// <param name="currentHealth">Current authoritative player health.</param>
    /// <param name="maximumHealth">Current authoritative maximum health.</param>
    /// <returns>True when current health is at or below the resolved threshold.</returns>
    public static bool HasReachedSelfPreservationThreshold(in PowerUpConditionalApplicationConfig config,
                                                           float currentHealth,
                                                           float maximumHealth)
    {
        float threshold;

        switch (config.HealthThresholdMode)
        {
            case SelfPreservationHealthThresholdMode.CurrentHealthValue:
                threshold = math.max(0f, config.HealthThreshold);
                break;
            default:
                threshold = math.max(0f, maximumHealth) *
                            (math.clamp(config.HealthThreshold, 0f, 100f) * 0.01f);
                break;
        }

        return currentHealth <= threshold + HealthThresholdEpsilon;
    }

    /// <summary>
    /// Resolves the selected automatic-charge condition from current player state.
    /// </summary>
    /// <param name="config">Sudden Strike condition settings.</param>
    /// <param name="movementState">Current player movement state.</param>
    /// <param name="lookState">Current player look state.</param>
    /// <param name="shotOccurred">Whether a real shot pulse changed since the previous update.</param>
    /// <returns>True while automatic charge may accumulate.</returns>
    private static bool ResolveSuddenStrikeCondition(in PowerUpConditionalApplicationConfig config,
                                                     in PlayerMovementState movementState,
                                                     in PlayerLookState lookState,
                                                     bool shotOccurred)
    {
        switch (config.SuddenStrikeConditionMode)
        {
            case SuddenStrikeChargeConditionMode.NotShooting:
                return !shotOccurred;
            default:
                float2 planarVelocity = new float2(movementState.Velocity.x, movementState.Velocity.z);
                float speedTolerance = math.max(0f, config.StationarySpeedTolerance);

                if (math.lengthsq(planarVelocity) > speedTolerance * speedTolerance)
                    return false;

                return config.CountRotationAsMovement == 0 ||
                       math.abs(lookState.AngularSpeed) <= math.max(0f, config.StationaryRotationToleranceDegrees);
        }
    }
    #endregion

    #endregion
}
