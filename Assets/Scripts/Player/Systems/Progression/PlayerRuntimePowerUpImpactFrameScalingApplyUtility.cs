using Unity.Mathematics;

/// <summary>
/// Applies runtime Add Scaling payload values that target Impact Frame active-tool settings.
/// </summary>
internal static class PlayerRuntimePowerUpImpactFrameScalingApplyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one numeric or enum-like Add Scaling result to an Impact Frame runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="impactFrameConfig">Mutable Impact Frame config rebuilt from immutable baselines.</param>
    /// <returns>True when the payload path targeted an Impact Frame field.</returns>
    public static bool TryApplyValue(string payloadPath, float resolvedValue, ref ImpactFramePowerUpConfig impactFrameConfig)
    {
        switch (payloadPath)
        {
            case "impactFrame.durationMode":
                impactFrameConfig.DurationMode = PlayerRuntimeScalingEnumUtility.ResolveImpactFrameDurationMode(resolvedValue);
                return true;
            case "impactFrame.durationFrames":
                impactFrameConfig.DurationFrames = math.max(0, (int)resolvedValue);
                return true;
            case "impactFrame.referenceFrameRate":
                impactFrameConfig.ReferenceFrameRate = math.max(1f, resolvedValue);
                return true;
            case "impactFrame.maximumUnscaledDurationSeconds":
                impactFrameConfig.MaximumUnscaledDurationSeconds = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.easeInUnscaledSeconds":
                impactFrameConfig.EaseInUnscaledSeconds = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.easeOutUnscaledSeconds":
                impactFrameConfig.EaseOutUnscaledSeconds = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.easingMode":
                impactFrameConfig.EasingMode = PlayerRuntimeScalingEnumUtility.ResolveImpactFrameEasingMode(resolvedValue);
                return true;
            case "impactFrame.timeSlowdownPercent":
                impactFrameConfig.TimeSlowdownPercent = math.clamp(resolvedValue, 0f, 100f);
                return true;
            case "impactFrame.overlayIntensity":
                impactFrameConfig.OverlayIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.filterTint.r":
                impactFrameConfig.FilterTintRgba.x = math.saturate(resolvedValue);
                return true;
            case "impactFrame.filterTint.g":
                impactFrameConfig.FilterTintRgba.y = math.saturate(resolvedValue);
                return true;
            case "impactFrame.filterTint.b":
                impactFrameConfig.FilterTintRgba.z = math.saturate(resolvedValue);
                return true;
            case "impactFrame.filterTint.a":
                impactFrameConfig.FilterTintRgba.w = math.saturate(resolvedValue);
                return true;
            case "impactFrame.desaturationAmount":
                impactFrameConfig.DesaturationAmount = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.vignetteIntensity":
                impactFrameConfig.VignetteIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.vignetteSoftness":
                impactFrameConfig.VignetteSoftness = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.chromaticAberration":
                impactFrameConfig.ChromaticAberration = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.scanlineIntensity":
                impactFrameConfig.ScanlineIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.scanlineFrequency":
                impactFrameConfig.ScanlineFrequency = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.flashIntensity":
                impactFrameConfig.FlashIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.radialDistortion":
                impactFrameConfig.RadialDistortion = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.shockwaveIntensity":
                impactFrameConfig.ShockwaveIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.shockwaveRadius":
                impactFrameConfig.ShockwaveRadius = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.shockwaveThickness":
                impactFrameConfig.ShockwaveThickness = math.clamp(resolvedValue, 0.001f, 1f);
                return true;
            case "impactFrame.zoomPunchIntensity":
                impactFrameConfig.ZoomPunchIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.invertIntensity":
                impactFrameConfig.InvertIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.posterizeIntensity":
                impactFrameConfig.PosterizeIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.posterizeSteps":
                impactFrameConfig.PosterizeSteps = math.max(2f, resolvedValue);
                return true;
            case "impactFrame.edgeInkIntensity":
                impactFrameConfig.EdgeInkIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.screenTearIntensity":
                impactFrameConfig.ScreenTearIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.screenTearFrequency":
                impactFrameConfig.ScreenTearFrequency = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.paletteFlashIntensity":
                impactFrameConfig.PaletteFlashIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "impactFrame.paletteFlashTint.r":
                impactFrameConfig.PaletteFlashTintRgba.x = math.saturate(resolvedValue);
                return true;
            case "impactFrame.paletteFlashTint.g":
                impactFrameConfig.PaletteFlashTintRgba.y = math.saturate(resolvedValue);
                return true;
            case "impactFrame.paletteFlashTint.b":
                impactFrameConfig.PaletteFlashTintRgba.z = math.saturate(resolvedValue);
                return true;
            case "impactFrame.paletteFlashTint.a":
                impactFrameConfig.PaletteFlashTintRgba.w = math.saturate(resolvedValue);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies one boolean Add Scaling result to an Impact Frame runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="impactFrameConfig">Mutable Impact Frame config rebuilt from immutable baselines.</param>
    /// <returns>True when the payload path targeted an Impact Frame boolean field.</returns>
    public static bool TryApplyBooleanValue(string payloadPath, bool resolvedValue, ref ImpactFramePowerUpConfig impactFrameConfig)
    {
        switch (payloadPath)
        {
            case "impactFrame.refreshOnShorterRequest":
                impactFrameConfig.RefreshOnShorterRequest = resolvedValue ? (byte)1 : (byte)0;
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
