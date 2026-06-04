using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts authored Impact Frame payloads into compact ECS runtime configs.
/// </summary>
internal static class PlayerPowerUpImpactFrameBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the runtime Impact Frame config from an authored payload without mutating designer values.
    /// </summary>
    /// <param name="impactFrameData">Authored module payload selected for the binding.</param>
    /// <param name="impactFrameConfig">Runtime config consumed by activation and presentation systems.</param>
    /// <returns>True when the payload contains enough timing and effect data to activate at runtime.</returns>
    public static bool TryBuildConfig(PowerUpImpactFrameModuleData impactFrameData, out ImpactFramePowerUpConfig impactFrameConfig)
    {
        impactFrameConfig = default;

        if (impactFrameData == null)
            return false;

        float referenceFrameRate = math.max(1f, impactFrameData.ReferenceFrameRate);
        int durationFrames = math.max(0, impactFrameData.DurationFrames);
        float maximumUnscaledDurationSeconds = math.max(0f, impactFrameData.MaximumUnscaledDurationSeconds);

        if (!HasValidDuration(impactFrameData.DurationMode, durationFrames, maximumUnscaledDurationSeconds))
            return false;

        Color filterTint = impactFrameData.FilterTint;
        impactFrameConfig = new ImpactFramePowerUpConfig
        {
            DurationMode = impactFrameData.DurationMode,
            DurationFrames = durationFrames,
            ReferenceFrameRate = referenceFrameRate,
            MaximumUnscaledDurationSeconds = maximumUnscaledDurationSeconds,
            EaseInUnscaledSeconds = math.max(0f, impactFrameData.EaseInUnscaledSeconds),
            EaseOutUnscaledSeconds = math.max(0f, impactFrameData.EaseOutUnscaledSeconds),
            EasingMode = impactFrameData.EasingMode,
            TimeSlowdownPercent = math.clamp(impactFrameData.TimeSlowdownPercent, 0f, 100f),
            RefreshOnShorterRequest = impactFrameData.RefreshOnShorterRequest ? (byte)1 : (byte)0,
            OverlayIntensity = math.clamp(impactFrameData.OverlayIntensity, 0f, 1f),
            FilterTintRgba = new float4(math.saturate(filterTint.r),
                                        math.saturate(filterTint.g),
                                        math.saturate(filterTint.b),
                                        math.saturate(filterTint.a)),
            DesaturationAmount = math.clamp(impactFrameData.DesaturationAmount, 0f, 1f),
            VignetteIntensity = math.clamp(impactFrameData.VignetteIntensity, 0f, 1f),
            VignetteSoftness = math.clamp(impactFrameData.VignetteSoftness, 0f, 1f),
            ChromaticAberration = math.max(0f, impactFrameData.ChromaticAberration),
            ScanlineIntensity = math.clamp(impactFrameData.ScanlineIntensity, 0f, 1f),
            ScanlineFrequency = math.max(0f, impactFrameData.ScanlineFrequency),
            FlashIntensity = math.clamp(impactFrameData.FlashIntensity, 0f, 1f),
            RadialDistortion = math.clamp(impactFrameData.RadialDistortion, 0f, 1f)
        };
        return impactFrameConfig.TimeSlowdownPercent > 0f || impactFrameConfig.OverlayIntensity > 0f;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves whether the authored duration source can produce at least one runtime update.
    /// </summary>
    /// <param name="durationMode">Authored duration mode.</param>
    /// <param name="durationFrames">Sanitized authored frame count.</param>
    /// <param name="maximumUnscaledDurationSeconds">Sanitized authored unscaled duration.</param>
    /// <returns>True when the selected duration mode has a positive limit.</returns>
    private static bool HasValidDuration(ImpactFrameDurationMode durationMode,
                                         int durationFrames,
                                         float maximumUnscaledDurationSeconds)
    {
        switch (durationMode)
        {
            case ImpactFrameDurationMode.FramesOnly:
                return durationFrames > 0;
            case ImpactFrameDurationMode.UnscaledSecondsOnly:
                return maximumUnscaledDurationSeconds > 0f;
            default:
                return durationFrames > 0 || maximumUnscaledDurationSeconds > 0f;
        }
    }
    #endregion

    #endregion
}
