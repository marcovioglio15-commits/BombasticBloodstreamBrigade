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

    [Header("Screen Filter")]
    [Tooltip("Master overlay intensity expressed as a 0-1 fraction. 0 disables the overlay, 1 uses the authored parameters at full strength.")]
    [SerializeField] private float overlayIntensity = 1f;

    [Tooltip("Tint color blended into the overlay. The color alpha controls the maximum tint strength reachable during the impact peak.")]
    [SerializeField] private Color filterTint = new Color(0.96f, 0.78f, 0.55f, 0.45f);

    [Tooltip("Color desaturation amount applied to the screen, 0 keeps the original color, 1 fully drains color.")]
    [SerializeField] private float desaturationAmount = 0.65f;

    [Tooltip("Vignette darkening intensity applied at the screen border, 0 disables the vignette.")]
    [SerializeField] private float vignetteIntensity = 0.55f;

    [Tooltip("Vignette inner softness, 0 keeps a hard ring, 1 fades smoothly toward the center.")]
    [SerializeField] private float vignetteSoftness = 0.6f;

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
    /// <param name="chromaticAberrationValue">Authored chromatic aberration amount.</param>
    /// <param name="scanlineIntensityValue">Authored scanline intensity.</param>
    /// <param name="scanlineFrequencyValue">Authored scanline frequency.</param>
    /// <param name="flashIntensityValue">Authored flash burst intensity.</param>
    /// <param name="radialDistortionValue">Authored radial distortion intensity.</param>
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
                          float chromaticAberrationValue,
                          float scanlineIntensityValue,
                          float scanlineFrequencyValue,
                          float flashIntensityValue,
                          float radialDistortionValue)
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
        overlayIntensity = overlayIntensityValue;
        filterTint = filterTintValue;
        desaturationAmount = desaturationAmountValue;
        vignetteIntensity = vignetteIntensityValue;
        vignetteSoftness = vignetteSoftnessValue;
        chromaticAberration = chromaticAberrationValue;
        scanlineIntensity = scanlineIntensityValue;
        scanlineFrequency = scanlineFrequencyValue;
        flashIntensity = flashIntensityValue;
        radialDistortion = radialDistortionValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps the payload callable from shared validation paths without snapping designer-authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
#endregion
