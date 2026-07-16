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
    /// <param name="bombardierRuntime">Current Bombardier runtime buffer used by weapon timing evaluation.</param>
    /// <param name="hasBossSlotRuntimes">Whether boss slot runtime data is available for activation feedback.</param>
    /// <param name="bossSlotRuntimes">Boss slot runtime buffer used by module activation timing.</param>
    /// <param name="patternConfig">Current compiled pattern config used by short-range timing evaluation.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state used by short-range timing evaluation.</param>
    /// <returns>The strongest active color-blend result, or an inactive result when no warning window is currently open.</returns>
    public static EnemyOffensiveEngagementBlendResult ResolveBlendResult(DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs,
                                                                         DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                                         DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                                                                         bool hasBossSlotRuntimes,
                                                                         DynamicBuffer<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimes,
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
                                   config.Source,
                                   config.ColorBlendLeadTimeSeconds,
                                   shooterRuntime,
                                   bombardierRuntime,
                                   hasBossSlotRuntimes,
                                   bossSlotRuntimes,
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
    /// <param name="bombardierRuntime">Current Bombardier runtime buffer used by weapon timing evaluation.</param>
    /// <param name="hasBossSlotRuntimes">Whether boss slot runtime data is available for activation feedback.</param>
    /// <param name="bossSlotRuntimes">Boss slot runtime buffer used by module activation timing.</param>
    /// <param name="patternConfig">Current compiled pattern config used by short-range timing evaluation.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state used by short-range timing evaluation.</param>
    /// <returns>The strongest active billboard result, or an inactive result when no billboard window is currently open.</returns>
    public static EnemyOffensiveEngagementBillboardResult ResolveBillboardResult(DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs,
                                                                                 DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                                                 DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                                                                                 bool hasBossSlotRuntimes,
                                                                                 DynamicBuffer<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimes,
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
                                   config.Source,
                                   config.BillboardLeadTimeSeconds,
                                   shooterRuntime,
                                   bombardierRuntime,
                                   hasBossSlotRuntimes,
                                   bossSlotRuntimes,
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
    /// Resolves whether any offensive engagement warning window is currently active, including billboard-only configs.
    /// </summary>
    /// <param name="configs">Baked offensive engagement configs for the current enemy.</param>
    /// <param name="shooterRuntime">Current shooter runtime buffer used by weapon timing evaluation.</param>
    /// <param name="bombardierRuntime">Current Bombardier runtime buffer used by weapon timing evaluation.</param>
    /// <param name="hasBossSlotRuntimes">Whether boss slot runtime data is available for activation feedback.</param>
    /// <param name="bossSlotRuntimes">Boss slot runtime buffer used by module activation timing.</param>
    /// <param name="patternConfig">Current compiled pattern config used by short-range timing evaluation.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state used by short-range timing evaluation.</param>
    /// <returns>True when at least one offensive engagement timing window is currently active.</returns>
    public static bool HasActiveEngagementWindow(DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs,
                                                 DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                 DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                                                 bool hasBossSlotRuntimes,
                                                 DynamicBuffer<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimes,
                                                 in EnemyPatternConfig patternConfig,
                                                 in EnemyPatternRuntimeState patternRuntimeState)
    {
        int configCount = configs.Length;

        for (int configIndex = 0; configIndex < configCount; configIndex++)
        {
            EnemyOffensiveEngagementConfigElement config = configs[configIndex];

            if (config.EnableColorBlend != 0 &&
                TryEvaluateWindow(config.TimingMode,
                                  config.Source,
                                  config.ColorBlendLeadTimeSeconds,
                                  shooterRuntime,
                                  bombardierRuntime,
                                  hasBossSlotRuntimes,
                                  bossSlotRuntimes,
                                  patternConfig,
                                  patternRuntimeState,
                                  out EnemyOffensiveEngagementWindow colorWindow))
            {
                return true;
            }

            if (config.EnableBillboard != 0 &&
                TryEvaluateWindow(config.TimingMode,
                                  config.Source,
                                  config.BillboardLeadTimeSeconds,
                                  shooterRuntime,
                                  bombardierRuntime,
                                  hasBossSlotRuntimes,
                                  bossSlotRuntimes,
                                  patternConfig,
                                  patternRuntimeState,
                                  out EnemyOffensiveEngagementWindow billboardWindow))
            {
                return true;
            }
        }

        return false;
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

    /// <summary>
    /// Resolves a looping billboard pulse scale from shared billboard tuning values.
    /// </summary>
    /// <param name="baseScale">Base uniform scale applied outside pulse peaks.</param>
    /// <param name="pulseScaleMultiplier">Peak scale multiplier reached during expansion.</param>
    /// <param name="expandDurationSeconds">Seconds spent expanding toward the peak scale.</param>
    /// <param name="contractDurationSeconds">Seconds spent contracting back to base scale.</param>
    /// <param name="elapsedWindowSeconds">Seconds elapsed since the visible window opened.</param>
    /// <returns>Final uniform billboard scale for the current frame.</returns>
    public static float ResolvePulseScale(float baseScale,
                                          float pulseScaleMultiplier,
                                          float expandDurationSeconds,
                                          float contractDurationSeconds,
                                          float elapsedWindowSeconds)
    {
        float safeBaseScale = math.max(0f, baseScale);

        if (safeBaseScale <= 0f)
        {
            return 0f;
        }

        float peakScale = safeBaseScale * math.max(0f, pulseScaleMultiplier);
        float safeExpandDurationSeconds = math.max(0f, expandDurationSeconds);
        float safeContractDurationSeconds = math.max(0f, contractDurationSeconds);
        float pulseDurationSeconds = safeExpandDurationSeconds + safeContractDurationSeconds;

        if (pulseDurationSeconds <= 0f || math.abs(peakScale - safeBaseScale) <= BlendEpsilon)
        {
            return safeBaseScale;
        }

        float clampedElapsedSeconds = math.max(0f, elapsedWindowSeconds);
        float pulseTimeSeconds = clampedElapsedSeconds % pulseDurationSeconds;

        if (safeExpandDurationSeconds > 0f && pulseTimeSeconds <= safeExpandDurationSeconds)
        {
            return math.lerp(safeBaseScale, peakScale, math.saturate(pulseTimeSeconds / safeExpandDurationSeconds));
        }

        if (safeContractDurationSeconds <= 0f)
        {
            return safeBaseScale;
        }

        float contractTimeSeconds = safeExpandDurationSeconds > 0f
            ? pulseTimeSeconds - safeExpandDurationSeconds
            : pulseTimeSeconds;
        return math.lerp(peakScale, safeBaseScale, math.saturate(contractTimeSeconds / safeContractDurationSeconds));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Evaluates one predictive warning window for the requested timing mode and lead time.
    /// </summary>
    /// <param name="timingMode">Timing model used by the current baked config.</param>
    /// <param name="source">Source slot that owns the current baked config.</param>
    /// <param name="leadTimeSeconds">Requested lead time for the current visual channel.</param>
    /// <param name="shooterRuntime">Current shooter runtime buffer used by weapon timing evaluation.</param>
    /// <param name="bombardierRuntime">Current Bombardier runtime buffer used by weapon timing evaluation.</param>
    /// <param name="hasBossSlotRuntimes">Whether boss slot runtime data is available for activation feedback.</param>
    /// <param name="bossSlotRuntimes">Boss slot runtime buffer used by module activation timing.</param>
    /// <param name="patternConfig">Current compiled pattern config used by short-range timing evaluation.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state used by short-range timing evaluation.</param>
    /// <param name="window">Active warning window data when evaluation succeeds.</param>
    /// <returns>True when a warning window is currently active for the requested config.</returns>
    private static bool TryEvaluateWindow(EnemyOffensiveEngagementTimingMode timingMode,
                                          EnemyOffensiveEngagementTriggerSource source,
                                          float leadTimeSeconds,
                                          DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                          DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                                          bool hasBossSlotRuntimes,
                                          DynamicBuffer<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimes,
                                          in EnemyPatternConfig patternConfig,
                                          in EnemyPatternRuntimeState patternRuntimeState,
                                          out EnemyOffensiveEngagementWindow window)
    {
        switch (timingMode)
        {
            case EnemyOffensiveEngagementTimingMode.ShortRangeDashRelease:
                return TryEvaluateShortRangeDashWindow(leadTimeSeconds, patternConfig, patternRuntimeState, out window);

            case EnemyOffensiveEngagementTimingMode.WeaponShot:
                return TryEvaluateWeaponShotWindow(leadTimeSeconds, shooterRuntime, bombardierRuntime, out window);

            case EnemyOffensiveEngagementTimingMode.ModuleActivation:
                return TryEvaluateModuleActivationWindow(source,
                                                         leadTimeSeconds,
                                                         hasBossSlotRuntimes,
                                                         bossSlotRuntimes,
                                                         out window);

            default:
                window = default(EnemyOffensiveEngagementWindow);
                return false;
        }
    }

    /// <summary>
    /// Evaluates the short visual window opened immediately after a boss module candidate becomes active.
    /// </summary>
    /// <param name="source">Source slot that owns the active module candidate.</param>
    /// <param name="durationSeconds">Seconds the activation feedback should remain active.</param>
    /// <param name="hasBossSlotRuntimes">Whether boss slot runtime data is available.</param>
    /// <param name="bossSlotRuntimes">Boss slot runtime buffer used to read candidate elapsed time.</param>
    /// <param name="window">Active activation window data when evaluation succeeds.</param>
    /// <returns>True when the source slot is inside its activation feedback window.</returns>
    private static bool TryEvaluateModuleActivationWindow(EnemyOffensiveEngagementTriggerSource source,
                                                          float durationSeconds,
                                                          bool hasBossSlotRuntimes,
                                                          DynamicBuffer<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimes,
                                                          out EnemyOffensiveEngagementWindow window)
    {
        window = default(EnemyOffensiveEngagementWindow);

        if (!hasBossSlotRuntimes)
        {
            return false;
        }

        EnemyBossPatternSlotKind slotKind = ResolveSlotKind(source);

        for (int slotIndex = 0; slotIndex < bossSlotRuntimes.Length; slotIndex++)
        {
            EnemyBossPatternSlotRuntimeElement slotRuntime = bossSlotRuntimes[slotIndex];

            if (slotRuntime.SlotKind != slotKind)
                continue;

            if (slotRuntime.ActiveCandidateIndex < 0)
                return false;

            float safeDurationSeconds = math.max(0f, durationSeconds);

            if (safeDurationSeconds <= 0f || slotRuntime.ActiveCandidateElapsedSeconds > safeDurationSeconds)
            {
                return false;
            }

            window.NormalizedProgress = 1f;
            window.ElapsedSeconds = math.max(0f, slotRuntime.ActiveCandidateElapsedSeconds);
            return true;
        }

        return false;
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
    /// <param name="bombardierRuntime">Current Bombardier runtime buffer.</param>
    /// <param name="window">Active warning window data when evaluation succeeds.</param>
    /// <returns>True when at least one shooter slot is currently inside a valid warning window.</returns>
    private static bool TryEvaluateWeaponShotWindow(float leadTimeSeconds,
                                                    DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                    DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
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

        for (int bombardierIndex = 0; bombardierIndex < bombardierRuntime.Length; bombardierIndex++)
        {
            EnemyBombardierRuntimeElement runtime = bombardierRuntime[bombardierIndex];

            if (runtime.IsLaunchAllowed == 0)
                continue;

            float candidateProgress = 0f;
            float candidateElapsedSeconds = 0f;
            bool hasCandidateWindow = false;

            if (runtime.RemainingBurstLaunches > 0 && runtime.LaunchesCompletedInCurrentBurst <= 0)
            {
                float windupDurationSeconds = math.max(0f, runtime.BurstWindupDurationSeconds);

                if (windupDurationSeconds > 0f)
                {
                    float timeUntilCommitSeconds = math.clamp(runtime.NextBombInBurstTimer, 0f, windupDurationSeconds);
                    candidateProgress = 1f - math.saturate(timeUntilCommitSeconds / windupDurationSeconds);
                    candidateElapsedSeconds = math.max(0f, windupDurationSeconds - timeUntilCommitSeconds);
                    hasCandidateWindow = true;
                }
            }
            else if (runtime.RemainingBurstLaunches <= 0 &&
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
                continue;

            if (candidateProgress <= bestProgress)
                continue;

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
        return ResolvePulseScale(config.BillboardBaseScale,
                                 config.BillboardPulseScaleMultiplier,
                                 config.BillboardPulseExpandDurationSeconds,
                                 config.BillboardPulseContractDurationSeconds,
                                 elapsedWindowSeconds);
    }

    /// <summary>
    /// Maps an engagement source to the boss slot that owns activation timing for that source.
    /// </summary>
    /// <param name="source">Baked engagement source.</param>
    /// <returns>Boss slot kind associated with the source.</returns>
    private static EnemyBossPatternSlotKind ResolveSlotKind(EnemyOffensiveEngagementTriggerSource source)
    {
        switch (source)
        {
            case EnemyOffensiveEngagementTriggerSource.CoreMovement:
                return EnemyBossPatternSlotKind.CoreMovement;

            case EnemyOffensiveEngagementTriggerSource.WeaponInteraction:
                return EnemyBossPatternSlotKind.WeaponInteraction;

            default:
                return EnemyBossPatternSlotKind.ShortRangeInteraction;
        }
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
