using System;
using Unity.Mathematics;

/// <summary>
/// Applies runtime Add Scaling payload values that target final-impact or charge build-in Impact Frame settings.
/// </summary>
internal static class PlayerRuntimePowerUpImpactFrameScalingApplyUtility
{
    #region Constants
    private const string MainEffectPrefix = "impactFrame.";
    private const string BuildInEffectPrefix = "impactFrame.buildIn.effect.";
    #endregion

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
            case "impactFrame.buildIn.releaseUnscaledSeconds":
                impactFrameConfig.BuildIn.ReleaseUnscaledSeconds = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.buildIn.easingMode":
                impactFrameConfig.BuildIn.EasingMode = PlayerRuntimeScalingEnumUtility.ResolveImpactFrameEasingMode(resolvedValue);
                return true;
        }

        if (payloadPath.StartsWith(BuildInEffectPrefix, StringComparison.Ordinal))
        {
            string normalizedEffectPath = MainEffectPrefix + payloadPath.Substring(BuildInEffectPrefix.Length);
            return TryApplyEffectValue(normalizedEffectPath, resolvedValue, ref impactFrameConfig.BuildIn.Effect);
        }

        return TryApplyEffectValue(payloadPath, resolvedValue, ref impactFrameConfig.Effect);
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
            case "impactFrame.buildIn.enabled":
                impactFrameConfig.BuildIn.Enabled = resolvedValue ? (byte)1 : (byte)0;
                return true;
        }

        if (payloadPath.StartsWith(BuildInEffectPrefix, StringComparison.Ordinal))
        {
            string normalizedEffectPath = MainEffectPrefix + payloadPath.Substring(BuildInEffectPrefix.Length);
            return TryApplyEffectBooleanValue(normalizedEffectPath, resolvedValue, ref impactFrameConfig.BuildIn.Effect);
        }

        return TryApplyEffectBooleanValue(payloadPath, resolvedValue, ref impactFrameConfig.Effect);
    }
    #endregion

    #region Effect Values
    /// <summary>
    /// Applies one normalized effect payload value to the requested runtime effect profile.
    /// </summary>
    /// <param name="payloadPath">Normalized path beginning with impactFrame.</param>
    /// <param name="resolvedValue">Resolved numeric or enum-like value.</param>
    /// <param name="effect">Mutable runtime effect profile.</param>
    /// <returns>True when the path targeted a supported effect field.</returns>
    private static bool TryApplyEffectValue(string payloadPath, float resolvedValue, ref ImpactFrameEffectConfig effect)
    {
        switch (payloadPath)
        {
            case "impactFrame.presentationScope":
                effect.PresentationScope = PlayerRuntimeScalingEnumUtility.ResolveImpactFramePresentationScope(resolvedValue);
                return true;
            case "impactFrame.timeSlowdownPercent":
                effect.TimeSlowdownPercent = math.clamp(resolvedValue, 0f, 100f);
                return true;
            case "impactFrame.cameraFeedback.motionMode":
                effect.CameraFeedback.MotionMode = PlayerRuntimeScalingEnumUtility.ResolveCameraShakeMotionMode(resolvedValue);
                return true;
            case "impactFrame.cameraFeedback.positionalAmplitude":
                effect.CameraFeedback.PositionalAmplitude = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.cameraFeedback.forwardAmplitude":
                effect.CameraFeedback.ForwardAmplitude = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.cameraFeedback.rotationalAmplitude":
                effect.CameraFeedback.RotationalAmplitude = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.cameraFeedback.frequency":
                effect.CameraFeedback.Frequency = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.cameraFeedback.zoomFovDelta":
                effect.CameraFeedback.ZoomFovDelta = resolvedValue;
                return true;
            case "impactFrame.overlayIntensity":
                effect.OverlayIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.filterTint.r":
                effect.FilterTintRgba.x = math.saturate(resolvedValue);
                return true;
            case "impactFrame.filterTint.g":
                effect.FilterTintRgba.y = math.saturate(resolvedValue);
                return true;
            case "impactFrame.filterTint.b":
                effect.FilterTintRgba.z = math.saturate(resolvedValue);
                return true;
            case "impactFrame.filterTint.a":
                effect.FilterTintRgba.w = math.saturate(resolvedValue);
                return true;
            case "impactFrame.desaturationAmount":
                effect.DesaturationAmount = math.saturate(resolvedValue);
                return true;
            case "impactFrame.vignetteIntensity":
                effect.VignetteIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.vignetteSoftness":
                effect.VignetteSoftness = math.saturate(resolvedValue);
                return true;
            case "impactFrame.vignetteExtent":
                effect.VignetteExtent = math.saturate(resolvedValue);
                return true;
            case "impactFrame.vignetteTint.x":
                effect.VignetteTintRgba.x = math.saturate(resolvedValue);
                return true;
            case "impactFrame.vignetteTint.y":
                effect.VignetteTintRgba.y = math.saturate(resolvedValue);
                return true;
            case "impactFrame.vignetteTint.z":
                effect.VignetteTintRgba.z = math.saturate(resolvedValue);
                return true;
            case "impactFrame.vignetteTint.w":
                effect.VignetteTintRgba.w = math.saturate(resolvedValue);
                return true;
            case "impactFrame.radialVignetteIntensity":
                effect.RadialVignetteIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.radialVignetteRadius":
                effect.RadialVignetteRadius = math.saturate(resolvedValue);
                return true;
            case "impactFrame.radialVignetteSoftness":
                effect.RadialVignetteSoftness = math.clamp(resolvedValue, 0.001f, 1f);
                return true;
            case "impactFrame.radialVignetteTint.r":
                effect.RadialVignetteTintRgba.x = math.saturate(resolvedValue);
                return true;
            case "impactFrame.radialVignetteTint.g":
                effect.RadialVignetteTintRgba.y = math.saturate(resolvedValue);
                return true;
            case "impactFrame.radialVignetteTint.b":
                effect.RadialVignetteTintRgba.z = math.saturate(resolvedValue);
                return true;
            case "impactFrame.radialVignetteTint.a":
                effect.RadialVignetteTintRgba.w = math.saturate(resolvedValue);
                return true;
            case "impactFrame.chromaticAberration":
                effect.ChromaticAberration = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.scanlineIntensity":
                effect.ScanlineIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.scanlineFrequency":
                effect.ScanlineFrequency = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.flashIntensity":
                effect.FlashIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.radialDistortion":
                effect.RadialDistortion = math.saturate(resolvedValue);
                return true;
            case "impactFrame.shockwaveIntensity":
                effect.ShockwaveIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.shockwaveRadius":
                effect.ShockwaveRadius = math.saturate(resolvedValue);
                return true;
            case "impactFrame.shockwaveThickness":
                effect.ShockwaveThickness = math.clamp(resolvedValue, 0.001f, 1f);
                return true;
            case "impactFrame.zoomPunchIntensity":
                effect.ZoomPunchIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.invertIntensity":
                effect.InvertIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.posterizeIntensity":
                effect.PosterizeIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.posterizeSteps":
                effect.PosterizeSteps = math.max(2f, resolvedValue);
                return true;
            case "impactFrame.edgeInkIntensity":
                effect.EdgeInkIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.screenTearIntensity":
                effect.ScreenTearIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.screenTearFrequency":
                effect.ScreenTearFrequency = math.max(0f, resolvedValue);
                return true;
            case "impactFrame.paletteFlashIntensity":
                effect.PaletteFlashIntensity = math.saturate(resolvedValue);
                return true;
            case "impactFrame.paletteFlashTint.r":
                effect.PaletteFlashTintRgba.x = math.saturate(resolvedValue);
                return true;
            case "impactFrame.paletteFlashTint.g":
                effect.PaletteFlashTintRgba.y = math.saturate(resolvedValue);
                return true;
            case "impactFrame.paletteFlashTint.b":
                effect.PaletteFlashTintRgba.z = math.saturate(resolvedValue);
                return true;
            case "impactFrame.paletteFlashTint.a":
                effect.PaletteFlashTintRgba.w = math.saturate(resolvedValue);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies one normalized effect boolean payload value to the requested runtime effect profile.
    /// </summary>
    /// <param name="payloadPath">Normalized path beginning with impactFrame.</param>
    /// <param name="resolvedValue">Resolved boolean value.</param>
    /// <param name="effect">Mutable runtime effect profile.</param>
    /// <returns>True when the path targeted a supported boolean effect field.</returns>
    private static bool TryApplyEffectBooleanValue(string payloadPath, bool resolvedValue, ref ImpactFrameEffectConfig effect)
    {
        byte value = resolvedValue ? (byte)1 : (byte)0;

        switch (payloadPath)
        {
            case "impactFrame.cameraFeedback.enabled":
                effect.CameraFeedback.Enabled = value;
                return true;
            case "impactFrame.cameraFeedback.axisRightEnabled":
                effect.CameraFeedback.AxisRightEnabled = value;
                return true;
            case "impactFrame.cameraFeedback.axisUpEnabled":
                effect.CameraFeedback.AxisUpEnabled = value;
                return true;
            case "impactFrame.cameraFeedback.axisForwardEnabled":
                effect.CameraFeedback.AxisForwardEnabled = value;
                return true;
            case "impactFrame.cameraFeedback.zoomEnabled":
                effect.CameraFeedback.ZoomEnabled = value;
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
