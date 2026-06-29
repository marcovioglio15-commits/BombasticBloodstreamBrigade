using System;
using UnityEngine;

/// <summary>
/// Stores presentation settings specific to one health, shield, boss, or active-energy syringe channel.
/// </summary>
[Serializable]
public sealed class PlayerSyringeChannelSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables this syringe channel.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Hides this syringe when its authoritative maximum value is zero or negative.")]
    [SerializeField] private bool hideWhenMaximumUnavailable;

    [Tooltip("Seconds used to move the displayed liquid boundary and plunger toward the authoritative current value. Set zero for immediate movement.")]
    [Range(0f, 2f)]
    [SerializeField] private float smoothingSeconds = 0.08f;

    [Tooltip("Routes reactive slosh to the procedural bubbles only: the liquid fills flat up to the current value while the bubbles carry the movement. Disables the liquid wave and surface-slosh settings.")]
    [SerializeField] private bool sloshAffectsBubblesOnly;

    [Tooltip("Direct color palette used by this syringe channel.")]
    [SerializeField] private PlayerSyringePaletteSettings palette = new PlayerSyringePaletteSettings();

    [Tooltip("Procedural liquid flow and bubble settings used by this syringe channel.")]
    [SerializeField] private PlayerSyringeFluidSettings fluid = new PlayerSyringeFluidSettings();

    [Tooltip("Optional movement and value-change reaction settings used by this syringe channel.")]
    [SerializeField] private PlayerSyringeMotionSettings motion = new PlayerSyringeMotionSettings();

    [Tooltip("Optional stylized outline and internal painted-streak settings used by this syringe channel.")]
    [SerializeField] private PlayerSyringeOutlineStyleSettings outlineStyle = new PlayerSyringeOutlineStyleSettings();
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public bool HideWhenMaximumUnavailable => hideWhenMaximumUnavailable;
    public float SmoothingSeconds => smoothingSeconds;
    public bool SloshAffectsBubblesOnly => sloshAffectsBubblesOnly;
    public PlayerSyringePaletteSettings Palette => palette;
    public PlayerSyringeFluidSettings Fluid => fluid;
    public PlayerSyringeMotionSettings Motion => motion;
    public PlayerSyringeOutlineStyleSettings OutlineStyle => outlineStyle;
    #endregion

    #region Methods

    #region Construction
    /// <summary>
    /// Creates a channel that remains visible when its authoritative maximum is unavailable.
    /// </summary>
    public PlayerSyringeChannelSettings()
    {
    }

    /// <summary>
    /// Creates a channel with an explicit unavailable-maximum visibility policy.
    /// </summary>
    /// <param name="hideWhenMaximumUnavailable">True when the channel should stay hidden until its authoritative maximum becomes positive.</param>
    public PlayerSyringeChannelSettings(bool hideWhenMaximumUnavailable)
    {
        this.hideWhenMaximumUnavailable = hideWhenMaximumUnavailable;
    }

    /// <summary>
    /// Creates a channel with explicit unavailable-maximum visibility and palette policies.
    /// </summary>
    /// <param name="hideWhenMaximumUnavailable">True when the channel should stay hidden until its authoritative maximum becomes positive.</param>
    /// <param name="useShieldPalette">True when the channel should use the default shield-oriented direct palette.</param>
    public PlayerSyringeChannelSettings(bool hideWhenMaximumUnavailable, bool useShieldPalette)
    {
        this.hideWhenMaximumUnavailable = hideWhenMaximumUnavailable;
        palette = new PlayerSyringePaletteSettings(useShieldPalette);
    }

    /// <summary>
    /// Creates a channel with explicit visibility and a named default direct palette.
    /// </summary>
    /// <param name="enabled">True when the channel should render by default.</param>
    /// <param name="hideWhenMaximumUnavailable">True when the channel should stay hidden until its authoritative maximum becomes positive.</param>
    /// <param name="palettePreset">Built-in palette used only to initialize newly authored settings.</param>
    public PlayerSyringeChannelSettings(bool enabled,
                                        bool hideWhenMaximumUnavailable,
                                        PlayerSyringePalettePreset palettePreset)
    {
        this.enabled = enabled;
        this.hideWhenMaximumUnavailable = hideWhenMaximumUnavailable;
        palette = new PlayerSyringePaletteSettings(palettePreset);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Reports invalid channel values without mutating serialized authoring data.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    /// <param name="channelLabel">User-facing channel label used by warning messages.</param>
    public void Validate(string ownerAssetName, string channelLabel)
    {
        if (palette == null || fluid == null || motion == null || outlineStyle == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Health Bars/{1}: one or more settings blocks are missing.",
                                           ownerAssetName,
                                           channelLabel));
            return;
        }

        if (!IsFinite(smoothingSeconds) || smoothingSeconds < 0f || smoothingSeconds > 2f)
            LogWarning(ownerAssetName, channelLabel, "Smoothing Seconds should be finite and within 0-2.");

        if (!IsPaletteFinite(palette))
            LogWarning(ownerAssetName, channelLabel, "All direct palette color channels should be finite.");

        if (!IsFinite(fluid.FlowSpeed) || fluid.FlowSpeed < -4f || fluid.FlowSpeed > 4f)
            LogWarning(ownerAssetName, channelLabel, "Flow Speed should be finite and within -4 to 4.");

        if (!IsFinite(fluid.WaveAmplitude) ||
            !IsFinite(fluid.WaveFrequency) ||
            !IsFinite(fluid.Viscosity) ||
            fluid.WaveAmplitude < 0f ||
            fluid.WaveAmplitude > 0.25f ||
            fluid.WaveFrequency < 0f ||
            fluid.WaveFrequency > 20f ||
            fluid.Viscosity < 0f ||
            fluid.Viscosity > 1f)
        {
            LogWarning(ownerAssetName, channelLabel, "Wave Amplitude should be within 0-0.25, Frequency within 0-20, and Viscosity within 0-1.");
        }

        if (fluid.BubblesEnabled &&
            (!IsFinite(fluid.BubbleDensity) ||
             !IsFinite(fluid.BubbleMinimumSize) ||
             !IsFinite(fluid.BubbleMaximumSize) ||
             !IsFinite(fluid.BubbleRiseSpeed) ||
             !IsFinite(fluid.BubbleDrift) ||
             fluid.BubbleDensity < 0f ||
             fluid.BubbleDensity > 1f ||
             fluid.BubbleMinimumSize < 0f ||
             fluid.BubbleMaximumSize < fluid.BubbleMinimumSize ||
             fluid.BubbleMaximumSize > 0.25f ||
             fluid.BubbleRiseSpeed < -2f ||
             fluid.BubbleRiseSpeed > 2f ||
             fluid.BubbleDrift < -2f ||
             fluid.BubbleDrift > 2f))
        {
            LogWarning(ownerAssetName, channelLabel, "Bubble Density should be within 0-1, sizes ordered within 0-0.25, and rise speed and drift within -2 to 2.");
        }

        // Routing slosh to the bubbles only is meaningless without bubbles, so flag the incoherent combination.
        if (sloshAffectsBubblesOnly && !fluid.BubblesEnabled)
            LogWarning(ownerAssetName, channelLabel, "Slosh Affects Bubbles Only is enabled while Bubbles are disabled; the liquid will fill flat with no visible slosh.");

        if (motion.MovementReactionEnabled &&
            (!IsFinite(motion.SloshStrength) ||
             !IsFinite(motion.SurfaceSloshStrength) ||
             !IsFinite(motion.HorizontalSloshStrength) ||
             !IsFinite(motion.SloshSpring) ||
             !IsFinite(motion.SloshDamping) ||
             !IsFinite(motion.MaximumSlosh) ||
             motion.SloshStrength < 0f ||
             motion.SloshStrength > 4f ||
             motion.SurfaceSloshStrength < 0f ||
             motion.SurfaceSloshStrength > 1f ||
             motion.HorizontalSloshStrength < 0f ||
             motion.HorizontalSloshStrength > 0.5f ||
             motion.SloshSpring < 0f ||
             motion.SloshSpring > 100f ||
             motion.SloshDamping < 0f ||
             motion.SloshDamping > 50f ||
             motion.MaximumSlosh < 0f ||
             motion.MaximumSlosh > 1f))
        {
            LogWarning(ownerAssetName, channelLabel, "Slosh response should be within 0-4, surface strength and maximum displacement within 0-1, horizontal strength within 0-0.5, spring within 0-100, and damping within 0-50.");
        }

        if (motion.TiltEnabled &&
            (!IsFinite(motion.MaximumTiltDegrees) ||
             !IsFinite(motion.TiltSpring) ||
             !IsFinite(motion.TiltDamping) ||
             motion.MaximumTiltDegrees < 0f ||
             motion.MaximumTiltDegrees > 20f ||
             motion.TiltSpring < 0f ||
             motion.TiltSpring > 100f ||
             motion.TiltDamping < 0f ||
             motion.TiltDamping > 50f))
        {
            LogWarning(ownerAssetName, channelLabel, "Tilt limit should be within 0-20 degrees, spring within 0-100, and damping within 0-50.");
        }

        if (motion.ValueImpulseEnabled &&
            (!IsFinite(motion.ValueImpulseStrength) ||
             !IsFinite(motion.ValueImpulseDecay) ||
             motion.ValueImpulseStrength < 0f ||
             motion.ValueImpulseStrength > 4f ||
             motion.ValueImpulseDecay < 0f ||
             motion.ValueImpulseDecay > 50f))
        {
            LogWarning(ownerAssetName, channelLabel, "Value Impulse Strength should be within 0-4 and Value Impulse Decay within 0-50.");
        }

        outlineStyle.Validate(ownerAssetName, channelLabel);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one channel-specific preset warning.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name.</param>
    /// <param name="channelLabel">User-facing channel label.</param>
    /// <param name="message">Warning message.</param>
    private static void LogWarning(string ownerAssetName, string channelLabel, string message)
    {
        Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Health Bars/{1}: {2}",
                                       ownerAssetName,
                                       channelLabel,
                                       message));
    }

    /// <summary>
    /// Checks whether every direct palette color channel is finite.
    /// </summary>
    /// <param name="palette">Palette to inspect.</param>
    /// <returns>True when all RGBA channels are finite.</returns>
    private static bool IsPaletteFinite(PlayerSyringePaletteSettings palette)
    {
        return IsFinite(palette.Outline) &&
               IsFinite(palette.Body) &&
               IsFinite(palette.BodyShadow) &&
               IsFinite(palette.Chamber) &&
               IsFinite(palette.Liquid) &&
               IsFinite(palette.LiquidHighlight) &&
               IsFinite(palette.Bubbles) &&
               IsFinite(palette.Graduation) &&
               IsFinite(palette.Label) &&
               IsFinite(palette.LabelOutline) &&
               IsFinite(palette.Plunger) &&
               IsFinite(palette.PlungerWindow) &&
               IsFinite(palette.TerminationOutline) &&
               IsFinite(palette.TerminationInterior);
    }

    /// <summary>
    /// Checks whether every channel of one direct color is finite.
    /// </summary>
    /// <param name="value">Color to inspect.</param>
    /// <returns>True when all RGBA channels are finite.</returns>
    private static bool IsFinite(Color value)
    {
        return IsFinite(value.r) &&
               IsFinite(value.g) &&
               IsFinite(value.b) &&
               IsFinite(value.a);
    }

    /// <summary>
    /// Checks whether one floating-point value is finite.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
