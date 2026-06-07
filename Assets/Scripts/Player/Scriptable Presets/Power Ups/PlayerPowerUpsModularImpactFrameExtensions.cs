using System;
using UnityEngine;

#region Camera Feedback
/// <summary>
/// Stores camera motion layered while an Impact Frame effect is visible. The effect uses the Impact Frame blend as its
/// envelope, so it shares the same smooth entry, build-in and release transitions without maintaining separate timers.
/// </summary>
[Serializable]
public sealed class PowerUpImpactFrameCameraFeedbackData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Master toggle for Impact Frame camera motion. When disabled no position, roll or FOV contribution is applied.")]
    [SerializeField] private bool enabled;

    [Tooltip("Continuous samples a moving noise field. Single Impulse keeps one stable directional kick for the effect lifetime.")]
    [SerializeField] private CameraShakeMotionMode motionMode = CameraShakeMotionMode.SingleImpulse;

    [Tooltip("When enabled, camera motion includes displacement along the camera Right axis.")]
    [SerializeField] private bool axisRightEnabled = true;

    [Tooltip("When enabled, camera motion includes displacement along the camera Up axis.")]
    [SerializeField] private bool axisUpEnabled = true;

    [Tooltip("When enabled, camera motion includes displacement along the camera Forward axis.")]
    [SerializeField] private bool axisForwardEnabled;

    [Tooltip("Maximum world-space displacement applied independently to the enabled Right and Up axes at full effect blend.")]
    [SerializeField] private float positionalAmplitude = 0.35f;

    [Tooltip("Maximum world-space displacement applied to the enabled Forward axis at full effect blend.")]
    [SerializeField] private float forwardAmplitude = 0.12f;

    [Tooltip("Maximum view-axis roll in degrees applied at full effect blend.")]
    [SerializeField] private float rotationalAmplitude = 1.25f;

    [Tooltip("Noise sampling frequency in cycles per second while Motion Mode is Continuous.")]
    [SerializeField] private float frequency = 18f;

    [Tooltip("When enabled, camera field of view receives the configured delta multiplied by the current effect blend.")]
    [SerializeField] private bool zoomEnabled;

    [Tooltip("Peak FOV delta in degrees. Negative values zoom in and positive values zoom out.")]
    [SerializeField] private float zoomFovDelta = -2f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public CameraShakeMotionMode MotionMode => motionMode;
    public bool AxisRightEnabled => axisRightEnabled;
    public bool AxisUpEnabled => axisUpEnabled;
    public bool AxisForwardEnabled => axisForwardEnabled;
    public float PositionalAmplitude => positionalAmplitude;
    public float ForwardAmplitude => forwardAmplitude;
    public float RotationalAmplitude => rotationalAmplitude;
    public float Frequency => frequency;
    public bool ZoomEnabled => zoomEnabled;
    public float ZoomFovDelta => zoomFovDelta;
    #endregion
}
#endregion

#region Reusable Effect
/// <summary>
/// Stores a standalone Impact Frame effect profile used by charge build-in presentation. Timing is owned by the
/// surrounding build-in settings while this block only defines time slowdown, fullscreen filtering and camera motion.
/// </summary>
[Serializable]
public sealed class PowerUpImpactFrameEffectData
{
    #region Fields

    #region Serialized Fields - Scope And Time
    [Tooltip("Latest camera-stack stage receiving the fullscreen filter. Environment Only excludes gameplay entities and UI; Environment And Gameplay excludes UI.")]
    [SerializeField] private ImpactFramePresentationScope presentationScope = ImpactFramePresentationScope.EnvironmentAndGameplay;

    [Tooltip("Percentage by which global time slows at full effect blend. 100 means a full freeze and 0 disables slowdown.")]
    [SerializeField] private float timeSlowdownPercent;

    [Tooltip("Camera position, roll and FOV motion multiplied by the current effect blend.")]
    [SerializeField] private PowerUpImpactFrameCameraFeedbackData cameraFeedback = new PowerUpImpactFrameCameraFeedbackData();
    #endregion

    #region Serialized Fields - Screen Effects Core
    [Tooltip("Master fullscreen-filter intensity expressed as a 0-1 fraction.")]
    [SerializeField] private float overlayIntensity;

    [Tooltip("Tint color blended into the filtered result. Alpha controls maximum tint strength.")]
    [SerializeField] private Color filterTint = Color.white;

    [Tooltip("Color desaturation amount, where 0 keeps original color and 1 fully drains color.")]
    [SerializeField] private float desaturationAmount;

    [Tooltip("Screen-border vignette intensity. 0 disables the perimetral tint.")]
    [SerializeField] private float vignetteIntensity;

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

    [Tooltip("Chromatic aberration shift expressed in normalized screen units.")]
    [SerializeField] private float chromaticAberration;

    [Tooltip("Horizontal scanline opacity.")]
    [SerializeField] private float scanlineIntensity;

    [Tooltip("Scanline count across the vertical viewport.")]
    [SerializeField] private float scanlineFrequency = 320f;

    [Tooltip("White flash intensity added over the filtered result.")]
    [SerializeField] private float flashIntensity;

    [Tooltip("Radial screen distortion intensity.")]
    [SerializeField] private float radialDistortion;
    #endregion

    #region Serialized Fields - Screen Effects Advanced
    [Tooltip("Expanding shockwave ring intensity.")]
    [SerializeField] private float shockwaveIntensity;

    [Tooltip("Maximum normalized viewport radius reached by the shockwave ring.")]
    [SerializeField] private float shockwaveRadius = 0.65f;

    [Tooltip("Normalized shockwave ring thickness.")]
    [SerializeField] private float shockwaveThickness = 0.12f;

    [Tooltip("Radial zoom-punch intensity.")]
    [SerializeField] private float zoomPunchIntensity;

    [Tooltip("Color inversion intensity.")]
    [SerializeField] private float invertIntensity;

    [Tooltip("Posterization blend amount.")]
    [SerializeField] private float posterizeIntensity;

    [Tooltip("Color-step count used by posterization.")]
    [SerializeField] private float posterizeSteps = 6f;

    [Tooltip("Ink-like local edge contrast intensity.")]
    [SerializeField] private float edgeInkIntensity;

    [Tooltip("Horizontal screen-tear intensity.")]
    [SerializeField] private float screenTearIntensity;

    [Tooltip("Screen-tear line frequency across the vertical viewport.")]
    [SerializeField] private float screenTearFrequency = 24f;

    [Tooltip("Palette-flash blend intensity.")]
    [SerializeField] private float paletteFlashIntensity;

    [Tooltip("Palette-flash tint. Alpha controls maximum flash strength.")]
    [SerializeField] private Color paletteFlashTint = Color.white;
    #endregion

    #endregion

    #region Properties
    public ImpactFramePresentationScope PresentationScope => presentationScope;
    public float TimeSlowdownPercent => timeSlowdownPercent;
    public PowerUpImpactFrameCameraFeedbackData CameraFeedback => cameraFeedback;
    public float OverlayIntensity => overlayIntensity;
    public Color FilterTint => filterTint;
    public float DesaturationAmount => desaturationAmount;
    public float VignetteIntensity => vignetteIntensity;
    public float VignetteSoftness => vignetteSoftness;
    public float VignetteExtent => vignetteExtent;
    public Color VignetteTint => new Color(vignetteTint.x, vignetteTint.y, vignetteTint.z, vignetteTint.w);
    public float RadialVignetteIntensity => radialVignetteIntensity;
    public float RadialVignetteRadius => radialVignetteRadius;
    public float RadialVignetteSoftness => radialVignetteSoftness;
    public Color RadialVignetteTint => radialVignetteTint;
    public float ChromaticAberration => chromaticAberration;
    public float ScanlineIntensity => scanlineIntensity;
    public float ScanlineFrequency => scanlineFrequency;
    public float FlashIntensity => flashIntensity;
    public float RadialDistortion => radialDistortion;
    public float ShockwaveIntensity => shockwaveIntensity;
    public float ShockwaveRadius => shockwaveRadius;
    public float ShockwaveThickness => shockwaveThickness;
    public float ZoomPunchIntensity => zoomPunchIntensity;
    public float InvertIntensity => invertIntensity;
    public float PosterizeIntensity => posterizeIntensity;
    public float PosterizeSteps => posterizeSteps;
    public float EdgeInkIntensity => edgeInkIntensity;
    public float ScreenTearIntensity => screenTearIntensity;
    public float ScreenTearFrequency => screenTearFrequency;
    public float PaletteFlashIntensity => paletteFlashIntensity;
    public Color PaletteFlashTint => paletteFlashTint;
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures reference blocks remain allocated without mutating authored numeric values.
    /// </summary>
    public void Validate()
    {
        if (cameraFeedback == null)
            cameraFeedback = new PowerUpImpactFrameCameraFeedbackData();
    }
    #endregion

    #endregion
}
#endregion

#region Build In
/// <summary>
/// Configures the gradual pre-impact effect driven by Trigger Hold Charge progress and its rapid smooth release after
/// charging stops or the final Impact Frame is applied.
/// </summary>
[Serializable]
public sealed class PowerUpImpactFrameBuildInData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables a gradual pre-impact effect while a paired Trigger Hold Charge module is actively charging.")]
    [SerializeField] private bool enabled;

    [Tooltip("Unscaled seconds used to release the build-in effect after charging stops or the final Impact Frame starts.")]
    [SerializeField] private float releaseUnscaledSeconds = 0.06f;

    [Tooltip("Easing curve used both while mapping charge progress and while releasing the build-in effect.")]
    [SerializeField] private ImpactFrameEasingMode easingMode = ImpactFrameEasingMode.EaseOutCubic;

    [Tooltip("Standalone time, fullscreen-filter and camera settings progressively applied while charge approaches maximum.")]
    [SerializeField] private PowerUpImpactFrameEffectData effect = new PowerUpImpactFrameEffectData();
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public float ReleaseUnscaledSeconds => releaseUnscaledSeconds;
    public ImpactFrameEasingMode EasingMode => easingMode;
    public PowerUpImpactFrameEffectData Effect => effect;
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures the nested effect profile remains allocated without snapping authored values.
    /// </summary>
    public void Validate()
    {
        if (effect == null)
            effect = new PowerUpImpactFrameEffectData();

        effect.Validate();
    }
    #endregion

    #endregion
}
#endregion
