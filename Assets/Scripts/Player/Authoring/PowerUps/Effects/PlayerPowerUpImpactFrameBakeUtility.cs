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
    /// Builds the runtime Impact Frame config from an authored payload without mutating  values.
    /// </summary>
    /// <param name="impactFrameData">Authored module payload selected for the binding.</param>
    /// <param name="impactFrameConfig">Runtime config consumed by activation and presentation systems.</param>
    /// <returns>True when a payload exists and its full baseline was preserved for runtime scaling.</returns>
    public static bool TryBuildConfig(PowerUpImpactFrameModuleData impactFrameData, out ImpactFramePowerUpConfig impactFrameConfig)
    {
        impactFrameConfig = default;

        if (impactFrameData == null)
            return false;

        float referenceFrameRate = math.max(1f, impactFrameData.ReferenceFrameRate);
        int durationFrames = math.max(0, impactFrameData.DurationFrames);
        float maximumUnscaledDurationSeconds = math.max(0f, impactFrameData.MaximumUnscaledDurationSeconds);

        impactFrameConfig = new ImpactFramePowerUpConfig
        {
            DurationMode = impactFrameData.DurationMode,
            DurationFrames = durationFrames,
            ReferenceFrameRate = referenceFrameRate,
            MaximumUnscaledDurationSeconds = maximumUnscaledDurationSeconds,
            EaseInUnscaledSeconds = math.max(0f, impactFrameData.EaseInUnscaledSeconds),
            EaseOutUnscaledSeconds = math.max(0f, impactFrameData.EaseOutUnscaledSeconds),
            EasingMode = impactFrameData.EasingMode,
            RefreshOnShorterRequest = impactFrameData.RefreshOnShorterRequest ? (byte)1 : (byte)0,
            Effect = BuildEffectConfig(impactFrameData),
            BuildIn = BuildBuildInConfig(impactFrameData.BuildIn)
        };
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the reusable final-impact effect profile from the flat authored payload.
    /// </summary>
    /// <param name="impactFrameData">Authored final-impact payload.</param>
    /// <returns>Runtime effect profile sanitized at the bake boundary.</returns>
    private static ImpactFrameEffectConfig BuildEffectConfig(PowerUpImpactFrameModuleData impactFrameData)
    {
        return BuildEffectConfig(impactFrameData.PresentationScope,
                                 impactFrameData.TimeSlowdownPercent,
                                 impactFrameData.CameraFeedback,
                                 impactFrameData.OverlayIntensity,
                                 impactFrameData.FilterTint,
                                 impactFrameData.DesaturationAmount,
                                 impactFrameData.VignetteIntensity,
                                 impactFrameData.VignetteSoftness,
                                 impactFrameData.VignetteExtent,
                                 impactFrameData.VignetteTint,
                                 impactFrameData.RadialVignetteIntensity,
                                 impactFrameData.RadialVignetteRadius,
                                 impactFrameData.RadialVignetteSoftness,
                                 impactFrameData.RadialVignetteTint,
                                 impactFrameData.ChromaticAberration,
                                 impactFrameData.ScanlineIntensity,
                                 impactFrameData.ScanlineFrequency,
                                 impactFrameData.FlashIntensity,
                                 impactFrameData.RadialDistortion,
                                 impactFrameData.ShockwaveIntensity,
                                 impactFrameData.ShockwaveRadius,
                                 impactFrameData.ShockwaveThickness,
                                 impactFrameData.ZoomPunchIntensity,
                                 impactFrameData.InvertIntensity,
                                 impactFrameData.PosterizeIntensity,
                                 impactFrameData.PosterizeSteps,
                                 impactFrameData.EdgeInkIntensity,
                                 impactFrameData.ScreenTearIntensity,
                                 impactFrameData.ScreenTearFrequency,
                                 impactFrameData.PaletteFlashIntensity,
                                 impactFrameData.PaletteFlashTint);
    }

    /// <summary>
    /// Builds the reusable charge build-in effect profile.
    /// </summary>
    /// <param name="effectData">Authored standalone build-in effect payload.</param>
    /// <returns>Runtime effect profile, or default when no payload is available.</returns>
    internal static ImpactFrameEffectConfig BuildEffectConfig(PowerUpImpactFrameEffectData effectData)
    {
        if (effectData == null)
            return default;

        return BuildEffectConfig(effectData.PresentationScope,
                                 effectData.TimeSlowdownPercent,
                                 effectData.CameraFeedback,
                                 effectData.OverlayIntensity,
                                 effectData.FilterTint,
                                 effectData.DesaturationAmount,
                                 effectData.VignetteIntensity,
                                 effectData.VignetteSoftness,
                                 effectData.VignetteExtent,
                                 effectData.VignetteTint,
                                 effectData.RadialVignetteIntensity,
                                 effectData.RadialVignetteRadius,
                                 effectData.RadialVignetteSoftness,
                                 effectData.RadialVignetteTint,
                                 effectData.ChromaticAberration,
                                 effectData.ScanlineIntensity,
                                 effectData.ScanlineFrequency,
                                 effectData.FlashIntensity,
                                 effectData.RadialDistortion,
                                 effectData.ShockwaveIntensity,
                                 effectData.ShockwaveRadius,
                                 effectData.ShockwaveThickness,
                                 effectData.ZoomPunchIntensity,
                                 effectData.InvertIntensity,
                                 effectData.PosterizeIntensity,
                                 effectData.PosterizeSteps,
                                 effectData.EdgeInkIntensity,
                                 effectData.ScreenTearIntensity,
                                 effectData.ScreenTearFrequency,
                                 effectData.PaletteFlashIntensity,
                                 effectData.PaletteFlashTint);
    }

    /// <summary>
    /// Builds one runtime effect profile from resolved authored values.
    /// </summary>
    /// <param name="presentationScope">Latest camera-stack stage receiving the filter.</param>
    /// <param name="timeSlowdownPercent">Peak global slowdown percentage.</param>
    /// <param name="cameraFeedback">Authored camera motion settings.</param>
    /// <param name="overlayIntensity">Master screen-filter intensity.</param>
    /// <param name="filterTint">Screen tint.</param>
    /// <param name="desaturationAmount">Desaturation amount.</param>
    /// <param name="vignetteIntensity">Border vignette intensity.</param>
    /// <param name="vignetteSoftness">Border vignette softness.</param>
    /// <param name="vignetteExtent">Border vignette inward extent.</param>
    /// <param name="vignetteTint">Border vignette tint.</param>
    /// <param name="radialVignetteIntensity">Radial ring intensity.</param>
    /// <param name="radialVignetteRadius">Radial ring radius.</param>
    /// <param name="radialVignetteSoftness">Radial ring softness.</param>
    /// <param name="radialVignetteTint">Radial ring tint.</param>
    /// <param name="chromaticAberration">Chromatic aberration amount.</param>
    /// <param name="scanlineIntensity">Scanline intensity.</param>
    /// <param name="scanlineFrequency">Scanline frequency.</param>
    /// <param name="flashIntensity">Flash intensity.</param>
    /// <param name="radialDistortion">Radial distortion amount.</param>
    /// <param name="shockwaveIntensity">Shockwave intensity.</param>
    /// <param name="shockwaveRadius">Shockwave radius.</param>
    /// <param name="shockwaveThickness">Shockwave thickness.</param>
    /// <param name="zoomPunchIntensity">Zoom-punch intensity.</param>
    /// <param name="invertIntensity">Color inversion intensity.</param>
    /// <param name="posterizeIntensity">Posterization intensity.</param>
    /// <param name="posterizeSteps">Posterization step count.</param>
    /// <param name="edgeInkIntensity">Edge-ink intensity.</param>
    /// <param name="screenTearIntensity">Screen-tear intensity.</param>
    /// <param name="screenTearFrequency">Screen-tear frequency.</param>
    /// <param name="paletteFlashIntensity">Palette-flash intensity.</param>
    /// <param name="paletteFlashTint">Palette-flash tint.</param>
    /// <returns>Sanitized runtime effect profile.</returns>
    private static ImpactFrameEffectConfig BuildEffectConfig(ImpactFramePresentationScope presentationScope,
                                                              float timeSlowdownPercent,
                                                              PowerUpImpactFrameCameraFeedbackData cameraFeedback,
                                                              float overlayIntensity,
                                                              Color filterTint,
                                                              float desaturationAmount,
                                                              float vignetteIntensity,
                                                              float vignetteSoftness,
                                                              float vignetteExtent,
                                                              Color vignetteTint,
                                                              float radialVignetteIntensity,
                                                              float radialVignetteRadius,
                                                              float radialVignetteSoftness,
                                                              Color radialVignetteTint,
                                                              float chromaticAberration,
                                                              float scanlineIntensity,
                                                              float scanlineFrequency,
                                                              float flashIntensity,
                                                              float radialDistortion,
                                                              float shockwaveIntensity,
                                                              float shockwaveRadius,
                                                              float shockwaveThickness,
                                                              float zoomPunchIntensity,
                                                              float invertIntensity,
                                                              float posterizeIntensity,
                                                              float posterizeSteps,
                                                              float edgeInkIntensity,
                                                              float screenTearIntensity,
                                                              float screenTearFrequency,
                                                              float paletteFlashIntensity,
                                                              Color paletteFlashTint)
    {
        return new ImpactFrameEffectConfig
        {
            PresentationScope = presentationScope,
            TimeSlowdownPercent = math.clamp(timeSlowdownPercent, 0f, 100f),
            CameraFeedback = BuildCameraFeedbackConfig(cameraFeedback),
            OverlayIntensity = math.saturate(overlayIntensity),
            FilterTintRgba = ToSaturatedFloat4(filterTint),
            DesaturationAmount = math.saturate(desaturationAmount),
            VignetteIntensity = math.saturate(vignetteIntensity),
            VignetteSoftness = math.saturate(vignetteSoftness),
            VignetteExtent = math.saturate(vignetteExtent),
            VignetteTintRgba = ToSaturatedFloat4(vignetteTint),
            RadialVignetteIntensity = math.saturate(radialVignetteIntensity),
            RadialVignetteRadius = math.saturate(radialVignetteRadius),
            RadialVignetteSoftness = math.clamp(radialVignetteSoftness, 0.001f, 1f),
            RadialVignetteTintRgba = ToSaturatedFloat4(radialVignetteTint),
            ChromaticAberration = math.max(0f, chromaticAberration),
            ScanlineIntensity = math.saturate(scanlineIntensity),
            ScanlineFrequency = math.max(0f, scanlineFrequency),
            FlashIntensity = math.saturate(flashIntensity),
            RadialDistortion = math.saturate(radialDistortion),
            ShockwaveIntensity = math.saturate(shockwaveIntensity),
            ShockwaveRadius = math.saturate(shockwaveRadius),
            ShockwaveThickness = math.clamp(shockwaveThickness, 0.001f, 1f),
            ZoomPunchIntensity = math.saturate(zoomPunchIntensity),
            InvertIntensity = math.saturate(invertIntensity),
            PosterizeIntensity = math.saturate(posterizeIntensity),
            PosterizeSteps = math.max(2f, posterizeSteps),
            EdgeInkIntensity = math.saturate(edgeInkIntensity),
            ScreenTearIntensity = math.saturate(screenTearIntensity),
            ScreenTearFrequency = math.max(0f, screenTearFrequency),
            PaletteFlashIntensity = math.saturate(paletteFlashIntensity),
            PaletteFlashTintRgba = ToSaturatedFloat4(paletteFlashTint)
        };
    }

    /// <summary>
    /// Builds camera motion used by one runtime Impact Frame effect.
    /// </summary>
    /// <param name="cameraFeedback">Authored camera feedback block.</param>
    /// <returns>Runtime camera motion config.</returns>
    private static ImpactFrameCameraFeedbackConfig BuildCameraFeedbackConfig(PowerUpImpactFrameCameraFeedbackData cameraFeedback)
    {
        if (cameraFeedback == null)
            return default;

        return new ImpactFrameCameraFeedbackConfig
        {
            Enabled = cameraFeedback.Enabled ? (byte)1 : (byte)0,
            MotionMode = cameraFeedback.MotionMode,
            AxisRightEnabled = cameraFeedback.AxisRightEnabled ? (byte)1 : (byte)0,
            AxisUpEnabled = cameraFeedback.AxisUpEnabled ? (byte)1 : (byte)0,
            AxisForwardEnabled = cameraFeedback.AxisForwardEnabled ? (byte)1 : (byte)0,
            PositionalAmplitude = math.max(0f, cameraFeedback.PositionalAmplitude),
            ForwardAmplitude = math.max(0f, cameraFeedback.ForwardAmplitude),
            RotationalAmplitude = math.max(0f, cameraFeedback.RotationalAmplitude),
            Frequency = math.max(0f, cameraFeedback.Frequency),
            ZoomEnabled = cameraFeedback.ZoomEnabled ? (byte)1 : (byte)0,
            ZoomFovDelta = cameraFeedback.ZoomFovDelta
        };
    }

    /// <summary>
    /// Builds charge build-in tuning from the authored optional block.
    /// </summary>
    /// <param name="buildInData">Authored build-in block.</param>
    /// <returns>Runtime build-in config.</returns>
    private static ImpactFrameBuildInConfig BuildBuildInConfig(PowerUpImpactFrameBuildInData buildInData)
    {
        if (buildInData == null)
            return default;

        ImpactFrameEffectConfig effect = BuildEffectConfig(buildInData.Effect);
        return new ImpactFrameBuildInConfig
        {
            Enabled = buildInData.Enabled ? (byte)1 : (byte)0,
            ReleaseUnscaledSeconds = math.max(0f, buildInData.ReleaseUnscaledSeconds),
            EasingMode = buildInData.EasingMode,
            Effect = effect
        };
    }

    /// <summary>
    /// Converts one Unity color into a saturated ECS float vector.
    /// </summary>
    /// <param name="color">Unity color to convert.</param>
    /// <returns>Saturated RGBA vector.</returns>
    private static float4 ToSaturatedFloat4(Color color)
    {
        return math.saturate(new float4(color.r, color.g, color.b, color.a));
    }

    #endregion

    #endregion
}
