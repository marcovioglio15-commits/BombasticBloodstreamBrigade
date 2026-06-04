using Unity.Mathematics;

/// <summary>
/// Centralizes Impact Frame activation, duration, and easing state transitions.
/// </summary>
public static class PlayerImpactFrameRuntimeUtility
{
    #region Constants
    private const byte PhaseIdle = 0;
    private const byte PhaseEaseIn = 1;
    private const byte PhaseHold = 2;
    private const byte PhaseEaseOut = 3;
    private const float ComparisonEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether a slot-level Impact Frame config can activate.
    /// </summary>
    /// <param name="impactFrameConfig">Runtime config authored on the active power-up slot.</param>
    /// <returns>True when duration and effect intensity are valid.</returns>
    public static bool CanActivate(in ImpactFramePowerUpConfig impactFrameConfig)
    {
        return IsValidConfig(in impactFrameConfig);
    }

    /// <summary>
    /// Starts or refreshes one Impact Frame request from a successful active power-up activation.
    /// </summary>
    /// <param name="impactFrameState">Mutable runtime state updated in place.</param>
    /// <param name="impactFrameConfig">Runtime config authored on the active power-up slot.</param>
    public static void Activate(ref PlayerImpactFrameState impactFrameState, in ImpactFramePowerUpConfig impactFrameConfig)
    {
        ActivateInternal(ref impactFrameState, in impactFrameConfig, float3.zero, 0);
    }

    /// <summary>
    /// Starts or refreshes one Impact Frame request from a spatial world position such as a spawned-object explosion.
    /// </summary>
    /// <param name="impactFrameState">Mutable runtime state updated in place.</param>
    /// <param name="impactFrameConfig">Runtime config authored on the active power-up slot.</param>
    /// <param name="worldPosition">World position used by screen-space effects that need an origin.</param>
    public static void ActivateAtWorldPosition(ref PlayerImpactFrameState impactFrameState,
                                               in ImpactFramePowerUpConfig impactFrameConfig,
                                               float3 worldPosition)
    {
        ActivateInternal(ref impactFrameState, in impactFrameConfig, worldPosition, 1);
    }

    /// <summary>
    /// Clears all Impact Frame state immediately.
    /// </summary>
    /// <param name="impactFrameState">Mutable runtime state reset in place.</param>
    public static void Clear(ref PlayerImpactFrameState impactFrameState)
    {
        impactFrameState = default;
    }

    /// <summary>
    /// Advances Impact Frame phase timers using unscaled time.
    /// </summary>
    /// <param name="impactFrameState">Mutable runtime state updated in place.</param>
    /// <param name="unscaledDeltaTime">Current unscaled frame delta.</param>
    /// <returns>True while the effect remains active after this tick.</returns>
    public static bool Tick(ref PlayerImpactFrameState impactFrameState, float unscaledDeltaTime)
    {
        if (impactFrameState.IsActive == 0)
            return false;

        float safeDeltaTime = math.max(0f, unscaledDeltaTime);
        impactFrameState.EffectElapsedUnscaledSeconds += safeDeltaTime;

        switch (impactFrameState.Phase)
        {
            case PhaseEaseIn:
                TickEaseIn(ref impactFrameState, safeDeltaTime);
                break;
            case PhaseHold:
                TickHold(ref impactFrameState, safeDeltaTime);
                break;
            case PhaseEaseOut:
                TickEaseOut(ref impactFrameState, safeDeltaTime);
                break;
            default:
                Clear(ref impactFrameState);
                break;
        }

        return impactFrameState.IsActive != 0;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Shared activation path used by centered and spatial Impact Frame requests.
    /// </summary>
    /// <param name="impactFrameState">Mutable runtime state updated in place.</param>
    /// <param name="impactFrameConfig">Runtime config authored on the active power-up slot.</param>
    /// <param name="worldPosition">Optional world position used by spatial effects.</param>
    /// <param name="hasWorldOrigin">One when worldPosition should drive the screen-space origin.</param>
    private static void ActivateInternal(ref PlayerImpactFrameState impactFrameState,
                                         in ImpactFramePowerUpConfig impactFrameConfig,
                                         float3 worldPosition,
                                         byte hasWorldOrigin)
    {
        if (!IsValidConfig(in impactFrameConfig))
            return;

        float requestedDurationSeconds = ResolveRequestedDurationSeconds(in impactFrameConfig);
        float currentRemainingSeconds = ResolveCurrentRemainingSeconds(in impactFrameState);

        if (impactFrameState.IsActive != 0 &&
            impactFrameConfig.RefreshOnShorterRequest == 0 &&
            requestedDurationSeconds < currentRemainingSeconds)
        {
            return;
        }

        impactFrameState.IsActive = 1;
        impactFrameState.HasFrameLimit = ResolveHasFrameLimit(in impactFrameConfig);
        impactFrameState.HasSecondLimit = ResolveHasSecondLimit(in impactFrameConfig);
        impactFrameState.RemainingFrames = impactFrameState.HasFrameLimit != 0 ? math.max(1, impactFrameConfig.DurationFrames) : 0;
        impactFrameState.RemainingUnscaledSeconds = impactFrameState.HasSecondLimit != 0
            ? math.max(0f, impactFrameConfig.MaximumUnscaledDurationSeconds)
            : 0f;
        impactFrameState.ReferenceFrameRate = math.max(1f, impactFrameConfig.ReferenceFrameRate);
        impactFrameState.EaseInUnscaledSeconds = math.max(0f, impactFrameConfig.EaseInUnscaledSeconds);
        impactFrameState.EaseOutUnscaledSeconds = math.max(0f, impactFrameConfig.EaseOutUnscaledSeconds);
        impactFrameState.EasingMode = impactFrameConfig.EasingMode;
        impactFrameState.TimeSlowdownPercent = math.clamp(impactFrameConfig.TimeSlowdownPercent, 0f, 100f);
        impactFrameState.OverlayIntensity = math.clamp(impactFrameConfig.OverlayIntensity, 0f, 1f);
        impactFrameState.FilterTintRgba = math.saturate(impactFrameConfig.FilterTintRgba);
        impactFrameState.DesaturationAmount = math.clamp(impactFrameConfig.DesaturationAmount, 0f, 1f);
        impactFrameState.VignetteIntensity = math.clamp(impactFrameConfig.VignetteIntensity, 0f, 1f);
        impactFrameState.VignetteSoftness = math.clamp(impactFrameConfig.VignetteSoftness, 0f, 1f);
        impactFrameState.ChromaticAberration = math.max(0f, impactFrameConfig.ChromaticAberration);
        impactFrameState.ScanlineIntensity = math.clamp(impactFrameConfig.ScanlineIntensity, 0f, 1f);
        impactFrameState.ScanlineFrequency = math.max(0f, impactFrameConfig.ScanlineFrequency);
        impactFrameState.FlashIntensity = math.clamp(impactFrameConfig.FlashIntensity, 0f, 1f);
        impactFrameState.RadialDistortion = math.clamp(impactFrameConfig.RadialDistortion, 0f, 1f);
        impactFrameState.ShockwaveIntensity = math.clamp(impactFrameConfig.ShockwaveIntensity, 0f, 1f);
        impactFrameState.ShockwaveRadius = math.clamp(impactFrameConfig.ShockwaveRadius, 0f, 1f);
        impactFrameState.ShockwaveThickness = math.clamp(impactFrameConfig.ShockwaveThickness, 0.001f, 1f);
        impactFrameState.ZoomPunchIntensity = math.clamp(impactFrameConfig.ZoomPunchIntensity, 0f, 1f);
        impactFrameState.InvertIntensity = math.clamp(impactFrameConfig.InvertIntensity, 0f, 1f);
        impactFrameState.PosterizeIntensity = math.clamp(impactFrameConfig.PosterizeIntensity, 0f, 1f);
        impactFrameState.PosterizeSteps = math.max(2f, impactFrameConfig.PosterizeSteps);
        impactFrameState.EdgeInkIntensity = math.clamp(impactFrameConfig.EdgeInkIntensity, 0f, 1f);
        impactFrameState.ScreenTearIntensity = math.clamp(impactFrameConfig.ScreenTearIntensity, 0f, 1f);
        impactFrameState.ScreenTearFrequency = math.max(0f, impactFrameConfig.ScreenTearFrequency);
        impactFrameState.PaletteFlashIntensity = math.clamp(impactFrameConfig.PaletteFlashIntensity, 0f, 1f);
        impactFrameState.PaletteFlashTintRgba = math.saturate(impactFrameConfig.PaletteFlashTintRgba);
        impactFrameState.TotalDurationUnscaledSeconds = math.max(ComparisonEpsilon,
                                                                 requestedDurationSeconds +
                                                                 impactFrameState.EaseInUnscaledSeconds +
                                                                 impactFrameState.EaseOutUnscaledSeconds);
        impactFrameState.EffectElapsedUnscaledSeconds = 0f;
        impactFrameState.EffectOriginWorldPosition = worldPosition;
        impactFrameState.HasWorldOrigin = hasWorldOrigin;
        impactFrameState.PhaseElapsedUnscaledSeconds = 0f;
        impactFrameState.CurrentBlend = impactFrameState.EaseInUnscaledSeconds > ComparisonEpsilon ? 0f : 1f;
        impactFrameState.Phase = impactFrameState.EaseInUnscaledSeconds > ComparisonEpsilon ? PhaseEaseIn : PhaseHold;
    }
    /// <summary>
    /// Resolves whether a runtime config can produce a visible or time-scale effect.
    /// </summary>
    /// <param name="impactFrameConfig">Runtime config to inspect.</param>
    /// <returns>True when duration and effect intensity are valid.</returns>
    private static bool IsValidConfig(in ImpactFramePowerUpConfig impactFrameConfig)
    {
        if (ResolveRequestedDurationSeconds(in impactFrameConfig) <= ComparisonEpsilon)
            return false;

        return impactFrameConfig.TimeSlowdownPercent > ComparisonEpsilon ||
               impactFrameConfig.OverlayIntensity > ComparisonEpsilon;
    }

    /// <summary>
    /// Resolves the approximate authored duration used for refresh comparisons.
    /// </summary>
    /// <param name="impactFrameConfig">Runtime config to inspect.</param>
    /// <returns>Requested duration in unscaled seconds.</returns>
    private static float ResolveRequestedDurationSeconds(in ImpactFramePowerUpConfig impactFrameConfig)
    {
        float frameDurationSeconds = impactFrameConfig.DurationFrames > 0
            ? impactFrameConfig.DurationFrames / math.max(1f, impactFrameConfig.ReferenceFrameRate)
            : 0f;
        float secondsDuration = math.max(0f, impactFrameConfig.MaximumUnscaledDurationSeconds);

        switch (impactFrameConfig.DurationMode)
        {
            case ImpactFrameDurationMode.FramesOnly:
                return frameDurationSeconds;
            case ImpactFrameDurationMode.UnscaledSecondsOnly:
                return secondsDuration;
            default:
                if (frameDurationSeconds > ComparisonEpsilon && secondsDuration > ComparisonEpsilon)
                    return math.min(frameDurationSeconds, secondsDuration);

                return math.max(frameDurationSeconds, secondsDuration);
        }
    }

    /// <summary>
    /// Resolves the currently remaining duration for refresh policy comparisons.
    /// </summary>
    /// <param name="impactFrameState">Current runtime state.</param>
    /// <returns>Approximate remaining duration in unscaled seconds.</returns>
    private static float ResolveCurrentRemainingSeconds(in PlayerImpactFrameState impactFrameState)
    {
        if (impactFrameState.IsActive == 0)
            return 0f;

        float frameRemainingSeconds = impactFrameState.HasFrameLimit != 0
            ? impactFrameState.RemainingFrames / math.max(1f, impactFrameState.ReferenceFrameRate)
            : 0f;
        float secondRemaining = impactFrameState.HasSecondLimit != 0 ? impactFrameState.RemainingUnscaledSeconds : 0f;
        float phaseRemaining = impactFrameState.Phase == PhaseEaseOut
            ? math.max(0f, impactFrameState.EaseOutUnscaledSeconds - impactFrameState.PhaseElapsedUnscaledSeconds)
            : 0f;
        return math.max(math.max(frameRemainingSeconds, secondRemaining), phaseRemaining);
    }

    /// <summary>
    /// Resolves whether the selected duration mode uses frame countdown.
    /// </summary>
    /// <param name="impactFrameConfig">Runtime config to inspect.</param>
    /// <returns>One when a frame limit should be ticked.</returns>
    private static byte ResolveHasFrameLimit(in ImpactFramePowerUpConfig impactFrameConfig)
    {
        if (impactFrameConfig.DurationFrames <= 0)
            return 0;

        return impactFrameConfig.DurationMode != ImpactFrameDurationMode.UnscaledSecondsOnly ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Resolves whether the selected duration mode uses unscaled seconds countdown.
    /// </summary>
    /// <param name="impactFrameConfig">Runtime config to inspect.</param>
    /// <returns>One when an unscaled seconds limit should be ticked.</returns>
    private static byte ResolveHasSecondLimit(in ImpactFramePowerUpConfig impactFrameConfig)
    {
        if (impactFrameConfig.MaximumUnscaledDurationSeconds <= 0f)
            return 0;

        return impactFrameConfig.DurationMode != ImpactFrameDurationMode.FramesOnly ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Advances the entry transition until the effect reaches peak intensity.
    /// </summary>
    /// <param name="impactFrameState">Mutable runtime state updated in place.</param>
    /// <param name="safeDeltaTime">Non-negative unscaled delta.</param>
    private static void TickEaseIn(ref PlayerImpactFrameState impactFrameState, float safeDeltaTime)
    {
        impactFrameState.PhaseElapsedUnscaledSeconds += safeDeltaTime;

        if (impactFrameState.EaseInUnscaledSeconds <= ComparisonEpsilon ||
            impactFrameState.PhaseElapsedUnscaledSeconds >= impactFrameState.EaseInUnscaledSeconds)
        {
            impactFrameState.Phase = PhaseHold;
            impactFrameState.PhaseElapsedUnscaledSeconds = 0f;
            impactFrameState.CurrentBlend = 1f;
            return;
        }

        float normalizedProgress = math.saturate(impactFrameState.PhaseElapsedUnscaledSeconds / impactFrameState.EaseInUnscaledSeconds);
        impactFrameState.CurrentBlend = EvaluateEasing(impactFrameState.EasingMode, normalizedProgress);
    }

    /// <summary>
    /// Advances the peak hold duration and starts recovery when the selected limit expires.
    /// </summary>
    /// <param name="impactFrameState">Mutable runtime state updated in place.</param>
    /// <param name="safeDeltaTime">Non-negative unscaled delta.</param>
    private static void TickHold(ref PlayerImpactFrameState impactFrameState, float safeDeltaTime)
    {
        bool expired = false;
        impactFrameState.CurrentBlend = 1f;

        if (impactFrameState.HasSecondLimit != 0)
        {
            impactFrameState.RemainingUnscaledSeconds = math.max(0f, impactFrameState.RemainingUnscaledSeconds - safeDeltaTime);
            expired = impactFrameState.RemainingUnscaledSeconds <= ComparisonEpsilon;
        }

        if (impactFrameState.HasFrameLimit != 0)
        {
            impactFrameState.RemainingFrames = math.max(0, impactFrameState.RemainingFrames - 1);
            expired = expired || impactFrameState.RemainingFrames <= 0;
        }

        if (!expired)
            return;

        BeginEaseOut(ref impactFrameState);
    }

    /// <summary>
    /// Advances the recovery transition and clears the state when intensity reaches zero.
    /// </summary>
    /// <param name="impactFrameState">Mutable runtime state updated in place.</param>
    /// <param name="safeDeltaTime">Non-negative unscaled delta.</param>
    private static void TickEaseOut(ref PlayerImpactFrameState impactFrameState, float safeDeltaTime)
    {
        impactFrameState.PhaseElapsedUnscaledSeconds += safeDeltaTime;

        if (impactFrameState.EaseOutUnscaledSeconds <= ComparisonEpsilon ||
            impactFrameState.PhaseElapsedUnscaledSeconds >= impactFrameState.EaseOutUnscaledSeconds)
        {
            Clear(ref impactFrameState);
            return;
        }

        float normalizedProgress = math.saturate(impactFrameState.PhaseElapsedUnscaledSeconds / impactFrameState.EaseOutUnscaledSeconds);
        impactFrameState.CurrentBlend = 1f - EvaluateEasing(impactFrameState.EasingMode, normalizedProgress);
    }

    /// <summary>
    /// Moves the state into recovery or clears it immediately when no recovery is authored.
    /// </summary>
    /// <param name="impactFrameState">Mutable runtime state updated in place.</param>
    private static void BeginEaseOut(ref PlayerImpactFrameState impactFrameState)
    {
        impactFrameState.PhaseElapsedUnscaledSeconds = 0f;

        if (impactFrameState.EaseOutUnscaledSeconds <= ComparisonEpsilon)
        {
            Clear(ref impactFrameState);
            return;
        }

        impactFrameState.Phase = PhaseEaseOut;
        impactFrameState.CurrentBlend = 1f;
    }

    /// <summary>
    /// Evaluates one normalized easing curve used by time-scale and overlay transitions.
    /// </summary>
    /// <param name="easingMode">Selected easing curve.</param>
    /// <param name="normalizedProgress">Normalized 0-1 progress.</param>
    /// <returns>Eased 0-1 progress.</returns>
    private static float EvaluateEasing(ImpactFrameEasingMode easingMode, float normalizedProgress)
    {
        float t = math.saturate(normalizedProgress);

        switch (easingMode)
        {
            case ImpactFrameEasingMode.EaseInOutSine:
                return -(math.cos(math.PI * t) - 1f) * 0.5f;
            case ImpactFrameEasingMode.EaseOutCubic:
                return 1f - math.pow(1f - t, 3f);
            case ImpactFrameEasingMode.EaseInExpo:
                return t <= ComparisonEpsilon ? 0f : math.pow(2f, 10f * t - 10f);
            case ImpactFrameEasingMode.EaseOutExpo:
                return t >= 1f - ComparisonEpsilon ? 1f : 1f - math.pow(2f, -10f * t);
            default:
                return t;
        }
    }
    #endregion

    #endregion
}
