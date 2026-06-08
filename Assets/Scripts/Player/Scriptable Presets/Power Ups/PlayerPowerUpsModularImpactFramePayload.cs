using System;
using UnityEngine;

#region Module Payload
/// <summary>
/// Authored data for the Impact Frame module. Slows global time on power-up activation and drives a customizable
/// screen filter for the duration of the effect. Honoured by every active tool kind it is paired with.
/// </summary>
[Serializable]
public sealed class PowerUpImpactFrameModuleData
{
    #region Fields

    #region Serialized Fields
    [Header("Timing")]
    [Tooltip("Selects which limit ends the impact frame: earliest of frames/unscaled seconds, frames only, or unscaled seconds only.")]
    [SerializeField] private ImpactFrameDurationMode durationMode = ImpactFrameDurationMode.UseEarliestLimit;

    [Tooltip("Maximum impact frame duration expressed in frames at the project's target framerate. Used by Use Earliest Limit and Frames Only modes.")]
    [SerializeField] private int durationFrames = 6;

    [Tooltip("Reference target framerate used to convert authored frames into unscaled seconds.")]
    [SerializeField] private float referenceFrameRate = 60f;

    [Tooltip("Maximum impact frame duration in real unscaled seconds. Used by Use Earliest Limit and Unscaled Seconds Only modes.")]
    [SerializeField] private float maximumUnscaledDurationSeconds = 0.12f;

    [Tooltip("Unscaled seconds blended from current time scale to the impact time scale at activation. 0 means an instant cut.")]
    [SerializeField] private float easeInUnscaledSeconds = 0.02f;

    [Tooltip("Unscaled seconds blended from the impact time scale back to the previous time scale once the effect ends. 0 means an instant cut.")]
    [SerializeField] private float easeOutUnscaledSeconds = 0.08f;

    [Tooltip("Easing curve applied to the time scale and overlay during the impact entry and recovery transitions.")]
    [SerializeField] private ImpactFrameEasingMode easingMode = ImpactFrameEasingMode.EaseOutCubic;

    [Header("Time Scale")]
    [Tooltip("Percentage by which global time slows while the impact frame is active. 100 means a full freeze, 0 means no slowdown.")]
    [SerializeField] private float timeSlowdownPercent = 95f;

    [Tooltip("When enabled, repeated activations refresh remaining duration whenever the new request is shorter than what is left.")]
    [SerializeField] private bool refreshOnShorterRequest;

    [Header("Presentation Scope And Camera")]
    [Tooltip("Latest camera-stack stage receiving the fullscreen filter. Environment Only excludes gameplay entities and UI; Environment And Gameplay excludes UI.")]
    [SerializeField] private ImpactFramePresentationScope presentationScope = ImpactFramePresentationScope.EnvironmentAndGameplay;

    [Tooltip("Camera position, roll and FOV motion multiplied by the active Impact Frame blend.")]
    [SerializeField] private PowerUpImpactFrameCameraFeedbackData cameraFeedback = new PowerUpImpactFrameCameraFeedbackData();

    [Header("Screen Effects")]
    [Tooltip("Master overlay intensity expressed as a 0-1 fraction. 0 disables the overlay, 1 uses the authored parameters at full strength.")]
    [SerializeField] private float overlayIntensity = 1f;

    [Tooltip("Tint color blended into the overlay. The color alpha controls the maximum tint strength reachable during the impact peak.")]
    [SerializeField] private Color filterTint = new Color(0.96f, 0.78f, 0.55f, 0.45f);

    [Tooltip("Color desaturation amount applied to the screen, 0 keeps the original color, 1 fully drains color.")]
    [SerializeField] private float desaturationAmount = 0.65f;

    [Tooltip("Screen-border vignette intensity. 0 disables the perimetral tint.")]
    [SerializeField] private float vignetteIntensity = 0.55f;

    [Tooltip("Screen-border vignette softness. 0 creates a sharp inner edge, while 1 fades across the full authored extent.")]
    [SerializeField] private float vignetteSoftness = 0.6f;

    [Tooltip("Normalized distance reached inward from every screen edge by the screen-border vignette. 0 keeps it at the edge, while 1 reaches the viewport center.")]
    [SerializeField] private float vignetteExtent = 0.35f;

    [Tooltip("RGBA tint applied along the screen-border vignette. W controls maximum border strength in addition to Vignette Intensity; every channel supports Add Scaling.")]
    [SerializeField] private Vector4 vignetteTint = new Vector4(0f, 0f, 0f, 1f);

    [Tooltip("Tinted radial-ring vignette intensity. 0 disables the ring.")]
    [SerializeField] private float radialVignetteIntensity;

    [Tooltip("Normalized viewport radius at the center of the tinted radial vignette ring.")]
    [SerializeField] private float radialVignetteRadius = 0.55f;

    [Tooltip("Normalized softness controlling the tinted radial vignette ring thickness.")]
    [SerializeField] private float radialVignetteSoftness = 0.12f;

    [Tooltip("Tint applied to the radial vignette ring. Alpha controls maximum ring strength.")]
    [SerializeField] private Color radialVignetteTint = new Color(0.1f, 0f, 0.2f, 0.8f);

    [Tooltip("Chromatic aberration shift expressed in normalized screen units. 0 disables the effect.")]
    [SerializeField] private float chromaticAberration = 0.012f;

    [Tooltip("Scanline opacity overlay. 0 disables horizontal scanlines.")]
    [SerializeField] private float scanlineIntensity = 0.18f;

    [Tooltip("Scanline frequency expressed as line count across the vertical viewport.")]
    [SerializeField] private float scanlineFrequency = 320f;

    [Tooltip("White flash burst added at activation, then decays during the impact entry. 0 disables the flash.")]
    [SerializeField] private float flashIntensity = 0.35f;

    [Tooltip("Time warp ring radial distortion applied to the screen center, 0 disables the distortion.")]
    [SerializeField] private float radialDistortion = 0.22f;

    [Header("Screen Effects - Advanced")]
    [Tooltip("Expanding shockwave ring intensity. 0 disables the ring; values near 1 create a strong displacement pulse.")]
    [SerializeField] private float shockwaveIntensity = 0.35f;

    [Tooltip("Maximum normalized viewport radius reached by the shockwave ring during the effect.")]
    [SerializeField] private float shockwaveRadius = 0.65f;

    [Tooltip("Normalized thickness of the shockwave ring. Smaller values create a sharper ring.")]
    [SerializeField] private float shockwaveThickness = 0.12f;

    [Tooltip("Radial zoom punch intensity applied during the first part of the impact. 0 disables the punch.")]
    [SerializeField] private float zoomPunchIntensity = 0.18f;

    [Tooltip("Color inversion intensity applied at peak impact. 0 disables inversion, 1 fully inverts screen colors.")]
    [SerializeField] private float invertIntensity;

    [Tooltip("Posterization blend amount. 0 keeps continuous color, 1 uses stepped arcade-style colors.")]
    [SerializeField] private float posterizeIntensity;

    [Tooltip("Number of color steps used by posterization. Values below 2 are ignored at runtime.")]
    [SerializeField] private float posterizeSteps = 6f;

    [Tooltip("Ink-like edge contrast intensity derived from local color differences. 0 disables edge ink.")]
    [SerializeField] private float edgeInkIntensity = 0.2f;

    [Tooltip("Horizontal screen tear intensity. 0 disables tear lines.")]
    [SerializeField] private float screenTearIntensity;

    [Tooltip("Screen tear line frequency across the vertical viewport.")]
    [SerializeField] private float screenTearFrequency = 24f;

    [Tooltip("Palette flash intensity blended over the filtered result.")]
    [SerializeField] private float paletteFlashIntensity = 0.25f;

    [Tooltip("Palette flash tint. Alpha controls maximum flash strength in addition to Palette Flash Intensity.")]
    [SerializeField] private Color paletteFlashTint = new Color(1f, 0.9f, 0.45f, 0.7f);

    [Header("Trigger Hold Charge Build-In")]
    [Tooltip("Optional gradual pre-impact effect driven by a paired Trigger Hold Charge module.")]
    [SerializeField] private PowerUpImpactFrameBuildInData buildIn = new PowerUpImpactFrameBuildInData();
    #endregion

    #endregion

    #region Properties
    public ImpactFrameDurationMode DurationMode
    {
        get
        {
            return durationMode;
        }
    }

    public int DurationFrames
    {
        get
        {
            return durationFrames;
        }
    }

    public float ReferenceFrameRate
    {
        get
        {
            return referenceFrameRate;
        }
    }

    public float MaximumUnscaledDurationSeconds
    {
        get
        {
            return maximumUnscaledDurationSeconds;
        }
    }

    public float EaseInUnscaledSeconds
    {
        get
        {
            return easeInUnscaledSeconds;
        }
    }

    public float EaseOutUnscaledSeconds
    {
        get
        {
            return easeOutUnscaledSeconds;
        }
    }

    public ImpactFrameEasingMode EasingMode
    {
        get
        {
            return easingMode;
        }
    }

    public float TimeSlowdownPercent
    {
        get
        {
            return timeSlowdownPercent;
        }
    }

    public bool RefreshOnShorterRequest
    {
        get
        {
            return refreshOnShorterRequest;
        }
    }

    public ImpactFramePresentationScope PresentationScope => presentationScope;

    public PowerUpImpactFrameCameraFeedbackData CameraFeedback => cameraFeedback;

    public float OverlayIntensity
    {
        get
        {
            return overlayIntensity;
        }
    }

    public Color FilterTint
    {
        get
        {
            return filterTint;
        }
    }

    public float DesaturationAmount
    {
        get
        {
            return desaturationAmount;
        }
    }

    public float VignetteIntensity
    {
        get
        {
            return vignetteIntensity;
        }
    }

    public float VignetteSoftness
    {
        get
        {
            return vignetteSoftness;
        }
    }

    public float VignetteExtent => vignetteExtent;

    public Color VignetteTint => new Color(vignetteTint.x, vignetteTint.y, vignetteTint.z, vignetteTint.w);

    public float RadialVignetteIntensity => radialVignetteIntensity;

    public float RadialVignetteRadius => radialVignetteRadius;

    public float RadialVignetteSoftness => radialVignetteSoftness;

    public Color RadialVignetteTint => radialVignetteTint;

    public float ChromaticAberration
    {
        get
        {
            return chromaticAberration;
        }
    }

    public float ScanlineIntensity
    {
        get
        {
            return scanlineIntensity;
        }
    }

    public float ScanlineFrequency
    {
        get
        {
            return scanlineFrequency;
        }
    }

    public float FlashIntensity
    {
        get
        {
            return flashIntensity;
        }
    }

    public float RadialDistortion
    {
        get
        {
            return radialDistortion;
        }
    }

    public float ShockwaveIntensity
    {
        get
        {
            return shockwaveIntensity;
        }
    }

    public float ShockwaveRadius
    {
        get
        {
            return shockwaveRadius;
        }
    }

    public float ShockwaveThickness
    {
        get
        {
            return shockwaveThickness;
        }
    }

    public float ZoomPunchIntensity
    {
        get
        {
            return zoomPunchIntensity;
        }
    }

    public float InvertIntensity
    {
        get
        {
            return invertIntensity;
        }
    }

    public float PosterizeIntensity
    {
        get
        {
            return posterizeIntensity;
        }
    }

    public float PosterizeSteps
    {
        get
        {
            return posterizeSteps;
        }
    }

    public float EdgeInkIntensity
    {
        get
        {
            return edgeInkIntensity;
        }
    }

    public float ScreenTearIntensity
    {
        get
        {
            return screenTearIntensity;
        }
    }

    public float ScreenTearFrequency
    {
        get
        {
            return screenTearFrequency;
        }
    }

    public float PaletteFlashIntensity
    {
        get
        {
            return paletteFlashIntensity;
        }
    }

    public Color PaletteFlashTint
    {
        get
        {
            return paletteFlashTint;
        }
    }

    public PowerUpImpactFrameBuildInData BuildIn => buildIn;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Replaces every authored Impact Frame value at once. Used by defaults utilities and preset migration paths.
    /// </summary>
    /// <param name="durationModeValue">Selected duration source.</param>
    /// <param name="durationFramesValue">Authored maximum frame count.</param>
    /// <param name="referenceFrameRateValue">Authored target framerate used to convert frames into seconds.</param>
    /// <param name="maximumUnscaledDurationSecondsValue">Authored maximum unscaled duration.</param>
    /// <param name="easeInUnscaledSecondsValue">Authored entry blend duration.</param>
    /// <param name="easeOutUnscaledSecondsValue">Authored recovery blend duration.</param>
    /// <param name="easingModeValue">Authored easing curve.</param>
    /// <param name="timeSlowdownPercentValue">Authored time slowdown percentage.</param>
    /// <param name="refreshOnShorterRequestValue">Authored re-activation policy.</param>
    /// <param name="overlayIntensityValue">Authored master overlay intensity.</param>
    /// <param name="filterTintValue">Authored overlay tint.</param>
    /// <param name="desaturationAmountValue">Authored desaturation amount.</param>
    /// <param name="vignetteIntensityValue">Authored vignette intensity.</param>
    /// <param name="vignetteSoftnessValue">Authored vignette softness.</param>
    /// <param name="vignetteExtentValue">Authored screen-border vignette extent.</param>
    /// <param name="vignetteTintValue">Authored screen-border vignette tint.</param>
    /// <param name="chromaticAberrationValue">Authored chromatic aberration amount.</param>
    /// <param name="scanlineIntensityValue">Authored scanline intensity.</param>
    /// <param name="scanlineFrequencyValue">Authored scanline frequency.</param>
    /// <param name="flashIntensityValue">Authored flash burst intensity.</param>
    /// <param name="radialDistortionValue">Authored radial distortion intensity.</param>
    /// <param name="shockwaveIntensityValue">Authored shockwave ring intensity.</param>
    /// <param name="shockwaveRadiusValue">Authored shockwave ring maximum radius.</param>
    /// <param name="shockwaveThicknessValue">Authored shockwave ring thickness.</param>
    /// <param name="zoomPunchIntensityValue">Authored radial zoom punch intensity.</param>
    /// <param name="invertIntensityValue">Authored color inversion intensity.</param>
    /// <param name="posterizeIntensityValue">Authored posterization blend amount.</param>
    /// <param name="posterizeStepsValue">Authored posterization color-step count.</param>
    /// <param name="edgeInkIntensityValue">Authored ink edge intensity.</param>
    /// <param name="screenTearIntensityValue">Authored horizontal screen tear intensity.</param>
    /// <param name="screenTearFrequencyValue">Authored horizontal screen tear frequency.</param>
    /// <param name="paletteFlashIntensityValue">Authored palette flash intensity.</param>
    /// <param name="paletteFlashTintValue">Authored palette flash tint.</param>
    public void Configure(ImpactFrameDurationMode durationModeValue,
                          int durationFramesValue,
                          float referenceFrameRateValue,
                          float maximumUnscaledDurationSecondsValue,
                          float easeInUnscaledSecondsValue,
                          float easeOutUnscaledSecondsValue,
                          ImpactFrameEasingMode easingModeValue,
                          float timeSlowdownPercentValue,
                          bool refreshOnShorterRequestValue,
                          float overlayIntensityValue,
                          Color filterTintValue,
                          float desaturationAmountValue,
                          float vignetteIntensityValue,
                          float vignetteSoftnessValue,
                          float vignetteExtentValue,
                          Color vignetteTintValue,
                          float chromaticAberrationValue,
                          float scanlineIntensityValue,
                          float scanlineFrequencyValue,
                          float flashIntensityValue,
                          float radialDistortionValue,
                          float shockwaveIntensityValue,
                          float shockwaveRadiusValue,
                          float shockwaveThicknessValue,
                          float zoomPunchIntensityValue,
                          float invertIntensityValue,
                          float posterizeIntensityValue,
                          float posterizeStepsValue,
                          float edgeInkIntensityValue,
                          float screenTearIntensityValue,
                          float screenTearFrequencyValue,
                          float paletteFlashIntensityValue,
                          Color paletteFlashTintValue)
    {
        durationMode = durationModeValue;
        durationFrames = durationFramesValue;
        referenceFrameRate = referenceFrameRateValue;
        maximumUnscaledDurationSeconds = maximumUnscaledDurationSecondsValue;
        easeInUnscaledSeconds = easeInUnscaledSecondsValue;
        easeOutUnscaledSeconds = easeOutUnscaledSecondsValue;
        easingMode = easingModeValue;
        timeSlowdownPercent = timeSlowdownPercentValue;
        refreshOnShorterRequest = refreshOnShorterRequestValue;
        presentationScope = ImpactFramePresentationScope.EnvironmentAndGameplay;
        cameraFeedback = new PowerUpImpactFrameCameraFeedbackData();
        overlayIntensity = overlayIntensityValue;
        filterTint = filterTintValue;
        desaturationAmount = desaturationAmountValue;
        vignetteIntensity = vignetteIntensityValue;
        vignetteSoftness = vignetteSoftnessValue;
        vignetteExtent = vignetteExtentValue;
        vignetteTint = new Vector4(vignetteTintValue.r,
                                   vignetteTintValue.g,
                                   vignetteTintValue.b,
                                   vignetteTintValue.a);
        radialVignetteIntensity = 0f;
        radialVignetteRadius = 0.55f;
        radialVignetteSoftness = 0.12f;
        radialVignetteTint = new Color(0.1f, 0f, 0.2f, 0.8f);
        chromaticAberration = chromaticAberrationValue;
        scanlineIntensity = scanlineIntensityValue;
        scanlineFrequency = scanlineFrequencyValue;
        flashIntensity = flashIntensityValue;
        radialDistortion = radialDistortionValue;
        shockwaveIntensity = shockwaveIntensityValue;
        shockwaveRadius = shockwaveRadiusValue;
        shockwaveThickness = shockwaveThicknessValue;
        zoomPunchIntensity = zoomPunchIntensityValue;
        invertIntensity = invertIntensityValue;
        posterizeIntensity = posterizeIntensityValue;
        posterizeSteps = posterizeStepsValue;
        edgeInkIntensity = edgeInkIntensityValue;
        screenTearIntensity = screenTearIntensityValue;
        screenTearFrequency = screenTearFrequencyValue;
        paletteFlashIntensity = paletteFlashIntensityValue;
        paletteFlashTint = paletteFlashTintValue;
        buildIn = new PowerUpImpactFrameBuildInData();
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps the payload callable from shared validation paths without snapping authored values.
    /// </summary>
    public void Validate()
    {
        if (cameraFeedback == null)
            cameraFeedback = new PowerUpImpactFrameCameraFeedbackData();

        if (buildIn == null)
            buildIn = new PowerUpImpactFrameBuildInData();

        buildIn.Validate();
    }
    #endregion

    #endregion
}
#endregion
