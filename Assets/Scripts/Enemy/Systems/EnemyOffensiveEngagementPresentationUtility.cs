using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes predictive timing, color-blend composition, and billboard pulse evaluation for offensive engagement feedback.
/// </summary>
internal static class EnemyOffensiveEngagementPresentationUtility
{
    #region Constants
    private const float BlendEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the strongest currently active offensive color-blend warning across every baked interaction config.
    /// </summary>
    /// <param name="configs">Baked offensive engagement configs for the current enemy.</param>
    /// <param name="shooterRuntime">Current shooter runtime buffer used by weapon timing evaluation.</param>
    /// <param name="patternConfig">Current compiled pattern config used by short-range timing evaluation.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state used by short-range timing evaluation.</param>
    /// <returns>The strongest active color-blend result, or an inactive result when no warning window is currently open.</returns>
    public static EnemyOffensiveEngagementBlendResult ResolveBlendResult(DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs,
                                                                         DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                                         in EnemyPatternConfig patternConfig,
                                                                         in EnemyPatternRuntimeState patternRuntimeState)
    {
        EnemyOffensiveEngagementBlendResult bestResult = default(EnemyOffensiveEngagementBlendResult);
        int configCount = configs.Length;

        for (int configIndex = 0; configIndex < configCount; configIndex++)
        {
            EnemyOffensiveEngagementConfigElement config = configs[configIndex];

            if (config.EnableColorBlend == 0)
            {
                continue;
            }

            if (!TryEvaluateWindow(config.TimingMode,
                                   config.ColorBlendLeadTimeSeconds,
                                   shooterRuntime,
                                   patternConfig,
                                   patternRuntimeState,
                                   out EnemyOffensiveEngagementWindow window))
            {
                continue;
            }

            float candidateBlend = math.saturate(window.NormalizedProgress) * math.saturate(config.ColorBlendMaximumBlend);

            if (candidateBlend <= bestResult.Blend)
            {
                continue;
            }

            bestResult.IsActive = true;
            bestResult.Blend = candidateBlend;
            bestResult.Color = config.ColorBlendColor;
            bestResult.FadeOutSeconds = math.max(0f, config.ColorBlendFadeOutSeconds);
        }

        return bestResult;
    }

    /// <summary>
    /// Resolves the billboard request with the strongest active engagement progress across every baked interaction config.
    /// </summary>
    /// <param name="configs">Baked offensive engagement configs for the current enemy.</param>
    /// <param name="shooterRuntime">Current shooter runtime buffer used by weapon timing evaluation.</param>
    /// <param name="patternConfig">Current compiled pattern config used by short-range timing evaluation.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state used by short-range timing evaluation.</param>
    /// <returns>The strongest active billboard result, or an inactive result when no billboard window is currently open.</returns>
    public static EnemyOffensiveEngagementBillboardResult ResolveBillboardResult(DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs,
                                                                                 DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                                                 in EnemyPatternConfig patternConfig,
                                                                                 in EnemyPatternRuntimeState patternRuntimeState)
    {
        EnemyOffensiveEngagementBillboardResult bestResult = default(EnemyOffensiveEngagementBillboardResult);
        float bestPriority = -1f;
        int configCount = configs.Length;

        for (int configIndex = 0; configIndex < configCount; configIndex++)
        {
            EnemyOffensiveEngagementConfigElement config = configs[configIndex];

            if (config.EnableBillboard == 0)
            {
                continue;
            }

            if (!TryEvaluateWindow(config.TimingMode,
                                   config.BillboardLeadTimeSeconds,
                                   shooterRuntime,
                                   patternConfig,
                                   patternRuntimeState,
                                   out EnemyOffensiveEngagementWindow window))
            {
                continue;
            }

            float candidatePriority = window.NormalizedProgress;

            if (candidatePriority <= bestPriority)
            {
                continue;
            }

            bestPriority = candidatePriority;
            bestResult.IsActive = true;
            bestResult.Source = config.Source;
            bestResult.VisualSettingsKey = config.VisualSettingsKey;
            bestResult.UseOverrideVisualSettings = config.UseOverrideVisualSettings != 0;
            bestResult.Color = config.BillboardColor;
            bestResult.Offset = config.BillboardOffset;
            bestResult.UniformScale = ResolvePulseScale(config, window.ElapsedSeconds);
        }

        return bestResult;
    }

    /// <summary>
    /// Resolves the displayed offensive engagement blend for the current frame, preserving fade-out continuity after the active warning loses priority.
    /// </summary>
    /// <param name="currentBlend">Blend value applied during the previous frame.</param>
    /// <param name="currentFadeOutSeconds">Fade-out duration remembered from the previously dominant offensive warning.</param>
    /// <param name="targetResult">Strongest active offensive blend result for the current frame.</param>
    /// <param name="deltaTime">Presentation delta time.</param>
    /// <param name="rememberedFadeOutSeconds">Updated fade-out duration that should be stored back into presentation state.</param>
    /// <returns>Displayed offensive engagement blend for the current frame.</returns>
    public static float ResolveDisplayedBlend(float currentBlend,
                                              float currentFadeOutSeconds,
                                              EnemyOffensiveEngagementBlendResult targetResult,
                                              float deltaTime,
                                              out float rememberedFadeOutSeconds)
    {
        float targetBlend = targetResult.IsActive ? targetResult.Blend : 0f;
        rememberedFadeOutSeconds = currentFadeOutSeconds;

        if (targetBlend >= currentBlend)
        {
            if (targetResult.IsActive)
            {
                rememberedFadeOutSeconds = math.max(0f, targetResult.FadeOutSeconds);
            }

            return targetBlend;
        }

        float fadeOutSeconds = math.max(0f, currentFadeOutSeconds);

        if (fadeOutSeconds <= 0f)
        {
            return targetBlend;
        }

        float fadeStep = math.max(0f, deltaTime) / fadeOutSeconds;
        float blendedValue = math.lerp(currentBlend, targetBlend, math.saturate(fadeStep));

        if (math.abs(blendedValue - targetBlend) <= BlendEpsilon)
        {
            return targetBlend;
        }

        return blendedValue;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Evaluates one predictive warning window for the requested timing mode and lead time.
    /// </summary>
    /// <param name="timingMode">Timing model used by the current baked config.</param>
    /// <param name="leadTimeSeconds">Requested lead time for the current visual channel.</param>
    /// <param name="shooterRuntime">Current shooter runtime buffer used by weapon timing evaluation.</param>
    /// <param name="patternConfig">Current compiled pattern config used by short-range timing evaluation.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state used by short-range timing evaluation.</param>
    /// <param name="window">Active warning window data when evaluation succeeds.</param>
    /// <returns>True when a warning window is currently active for the requested config.</returns>
    private static bool TryEvaluateWindow(EnemyOffensiveEngagementTimingMode timingMode,
                                          float leadTimeSeconds,
                                          DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                          in EnemyPatternConfig patternConfig,
                                          in EnemyPatternRuntimeState patternRuntimeState,
                                          out EnemyOffensiveEngagementWindow window)
    {
        switch (timingMode)
        {
            case EnemyOffensiveEngagementTimingMode.ShortRangeDashRelease:
                return TryEvaluateShortRangeDashWindow(leadTimeSeconds, patternConfig, patternRuntimeState, out window);

            case EnemyOffensiveEngagementTimingMode.WeaponShot:
                return TryEvaluateWeaponShotWindow(leadTimeSeconds, shooterRuntime, out window);

            default:
                window = default(EnemyOffensiveEngagementWindow);
                return false;
        }
    }

    /// <summary>
    /// Evaluates the active warning window for a short-range dash release.
    /// </summary>
    /// <param name="leadTimeSeconds">Requested visual lead time for the current channel.</param>
    /// <param name="patternConfig">Current compiled pattern config.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state.</param>
    /// <param name="window">Active warning window data when evaluation succeeds.</param>
    /// <returns>True when the dash is currently inside a valid warning window.</returns>
    private static bool TryEvaluateShortRangeDashWindow(float leadTimeSeconds,
                                                        in EnemyPatternConfig patternConfig,
                                                        in EnemyPatternRuntimeState patternRuntimeState,
                                                        out EnemyOffensiveEngagementWindow window)
    {
        window = default(EnemyOffensiveEngagementWindow);

        if (patternRuntimeState.ShortRangeDashPhase != EnemyShortRangeDashPhase.Aiming)
        {
            return false;
        }

        float aimDurationSeconds = math.max(0f, patternConfig.ShortRangeDashAimDuration);
        float effectiveLeadTimeSeconds = math.min(math.max(0f, leadTimeSeconds), aimDurationSeconds);

        if (effectiveLeadTimeSeconds <= 0f)
        {
            return false;
        }

        float elapsedAimSeconds = math.clamp(patternRuntimeState.ShortRangeDashPhaseElapsed, 0f, aimDurationSeconds);
        float timeUntilCommitSeconds = math.max(0f, aimDurationSeconds - elapsedAimSeconds);

        if (timeUntilCommitSeconds > effectiveLeadTimeSeconds)
        {
            return false;
        }

        float elapsedWindowSeconds = math.max(0f, effectiveLeadTimeSeconds - timeUntilCommitSeconds);
        window.NormalizedProgress = 1f - math.saturate(timeUntilCommitSeconds / effectiveLeadTimeSeconds);
        window.ElapsedSeconds = elapsedWindowSeconds;
        return true;
    }

    /// <summary>
    /// Evaluates the active warning window for the next shooter shot using the same burst-start logic already used by the legacy color warning.
    /// </summary>
    /// <param name="leadTimeSeconds">Requested visual lead time for idle pre-burst windows.</param>
    /// <param name="shooterRuntime">Current shooter runtime buffer.</param>
    /// <param name="window">Active warning window data when evaluation succeeds.</param>
    /// <returns>True when at least one shooter slot is currently inside a valid warning window.</returns>
    private static bool TryEvaluateWeaponShotWindow(float leadTimeSeconds,
                                                    DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                    out EnemyOffensiveEngagementWindow window)
    {
        window = default(EnemyOffensiveEngagementWindow);
        float safeLeadTimeSeconds = math.max(0f, leadTimeSeconds);
        int shooterCount = shooterRuntime.Length;
        bool hasActiveWindow = false;
        float bestProgress = 0f;
        float bestElapsedSeconds = 0f;

        for (int shooterIndex = 0; shooterIndex < shooterCount; shooterIndex++)
        {
            EnemyShooterRuntimeElement runtime = shooterRuntime[shooterIndex];

            if (runtime.IsPlayerInRange == 0)
            {
                continue;
            }

            float candidateProgress = 0f;
            float candidateElapsedSeconds = 0f;
            bool hasCandidateWindow = false;

            if (runtime.RemainingBurstShots > 0 && runtime.ShotsFiredInCurrentBurst <= 0)
            {
                float windupDurationSeconds = math.max(0f, runtime.BurstWindupDurationSeconds);

                if (windupDurationSeconds > 0f)
                {
                    float timeUntilCommitSeconds = math.clamp(runtime.NextShotInBurstTimer, 0f, windupDurationSeconds);
                    candidateProgress = 1f - math.saturate(timeUntilCommitSeconds / windupDurationSeconds);
                    candidateElapsedSeconds = math.max(0f, windupDurationSeconds - timeUntilCommitSeconds);
                    hasCandidateWindow = true;
                }
            }
            else if (runtime.RemainingBurstShots <= 0 &&
                     safeLeadTimeSeconds > 0f &&
                     runtime.NextBurstTimer > 0f &&
                     runtime.NextBurstTimer <= safeLeadTimeSeconds)
            {
                float timeUntilCommitSeconds = math.clamp(runtime.NextBurstTimer, 0f, safeLeadTimeSeconds);
                candidateProgress = 1f - math.saturate(timeUntilCommitSeconds / safeLeadTimeSeconds);
                candidateElapsedSeconds = math.max(0f, safeLeadTimeSeconds - timeUntilCommitSeconds);
                hasCandidateWindow = true;
            }

            if (!hasCandidateWindow)
            {
                continue;
            }

            if (candidateProgress <= bestProgress)
            {
                continue;
            }

            hasActiveWindow = true;
            bestProgress = candidateProgress;
            bestElapsedSeconds = candidateElapsedSeconds;
        }

        if (!hasActiveWindow)
        {
            return false;
        }

        window.NormalizedProgress = bestProgress;
        window.ElapsedSeconds = bestElapsedSeconds;
        return true;
    }

    /// <summary>
    /// Resolves the current billboard scale produced by the configured pulse cycle.
    /// </summary>
    /// <param name="config">Baked billboard config currently being rendered.</param>
    /// <param name="elapsedWindowSeconds">Seconds elapsed since the current warning window opened.</param>
    /// <returns>Final uniform billboard scale for the current frame.</returns>
    private static float ResolvePulseScale(EnemyOffensiveEngagementConfigElement config, float elapsedWindowSeconds)
    {
        float baseScale = math.max(0f, config.BillboardBaseScale);

        if (baseScale <= 0f)
        {
            return 0f;
        }

        float peakScale = baseScale * math.max(0f, config.BillboardPulseScaleMultiplier);
        float expandDurationSeconds = math.max(0f, config.BillboardPulseExpandDurationSeconds);
        float contractDurationSeconds = math.max(0f, config.BillboardPulseContractDurationSeconds);
        float pulseDurationSeconds = expandDurationSeconds + contractDurationSeconds;

        if (pulseDurationSeconds <= 0f || math.abs(peakScale - baseScale) <= BlendEpsilon)
        {
            return baseScale;
        }

        float clampedElapsedSeconds = math.max(0f, elapsedWindowSeconds);
        float pulseTimeSeconds = clampedElapsedSeconds % pulseDurationSeconds;

        if (expandDurationSeconds > 0f && pulseTimeSeconds <= expandDurationSeconds)
        {
            return math.lerp(baseScale, peakScale, math.saturate(pulseTimeSeconds / expandDurationSeconds));
        }

        if (contractDurationSeconds <= 0f)
        {
            return baseScale;
        }

        float contractTimeSeconds = expandDurationSeconds > 0f
            ? pulseTimeSeconds - expandDurationSeconds
            : pulseTimeSeconds;
        return math.lerp(peakScale, baseScale, math.saturate(contractTimeSeconds / contractDurationSeconds));
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one currently active predictive warning window resolved for a single offensive config.
/// </summary>
internal struct EnemyOffensiveEngagementWindow
{
    public float NormalizedProgress;
    public float ElapsedSeconds;
}

/// <summary>
/// Stores the strongest currently active offensive color-blend result.
/// </summary>
internal struct EnemyOffensiveEngagementBlendResult
{
    public bool IsActive;
    public float Blend;
    public float4 Color;
    public float FadeOutSeconds;
}

/// <summary>
/// Stores the strongest currently active offensive billboard result.
/// </summary>
internal struct EnemyOffensiveEngagementBillboardResult
{
    public bool IsActive;
    public EnemyOffensiveEngagementTriggerSource Source;
    public int VisualSettingsKey;
    public bool UseOverrideVisualSettings;
    public float4 Color;
    public float3 Offset;
    public float UniformScale;
}
