using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Stores optional movement and value-change reactions used by one player syringe.
/// </summary>
[Serializable]
public sealed class PlayerSyringeMotionSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables inertial liquid movement opposite to player acceleration.")]
    [SerializeField] private bool movementReactionEnabled = true;

    [Tooltip("Converts player horizontal acceleration into normalized liquid displacement.")]
    [Range(0f, 4f)]
    [SerializeField] private float sloshStrength = 0.08f;

    [Tooltip("Converts normalized slosh displacement into a visible liquid-surface slope.")]
    [Range(0f, 1f)]
    [SerializeField] private float surfaceSloshStrength = 0.45f;

    [Tooltip("Enables horizontal inertial displacement of the liquid boundary and procedural bubbles.")]
    [SerializeField] private bool horizontalSloshEnabled = true;

    [Tooltip("Converts normalized slosh displacement into horizontal liquid and bubble travel along the graduated value track.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float horizontalSloshStrength = 0.16f;

    [Tooltip("Spring force returning liquid displacement to rest.")]
    [Range(0f, 100f)]
    [SerializeField] private float sloshSpring = 18f;

    [Tooltip("Damping applied while liquid displacement returns to rest.")]
    [Range(0f, 50f)]
    [SerializeField] private float sloshDamping = 8f;

    [Tooltip("Maximum normalized inertial liquid displacement.")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumSlosh = 0.25f;

    [Tooltip("Enables a small Z-axis syringe inclination driven by player movement.")]
    [SerializeField] private bool tiltEnabled = true;

    [Tooltip("Maximum absolute Z-axis inclination in degrees.")]
    [Range(0f, 20f)]
    [SerializeField] private float maximumTiltDegrees = 2.5f;

    [Tooltip("Spring force returning syringe inclination to rest.")]
    [Range(0f, 100f)]
    [SerializeField] private float tiltSpring = 16f;

    [Tooltip("Damping applied while syringe inclination returns to rest.")]
    [Range(0f, 50f)]
    [SerializeField] private float tiltDamping = 8f;

    [Tooltip("Enables a liquid impulse when the represented current value changes.")]
    [SerializeField] private bool valueImpulseEnabled = true;

    [Tooltip("Converts normalized value delta into an additional liquid impulse.")]
    [Range(0f, 4f)]
    [SerializeField] private float valueImpulseStrength = 0.65f;

    [Tooltip("Exponential decay speed of value-change impulses.")]
    [Range(0f, 50f)]
    [SerializeField] private float valueImpulseDecay = 7f;
    #endregion

    #endregion

    #region Properties
    public bool MovementReactionEnabled => movementReactionEnabled;
    public float SloshStrength => sloshStrength;
    public float SurfaceSloshStrength => surfaceSloshStrength;
    public bool HorizontalSloshEnabled => horizontalSloshEnabled;
    public float HorizontalSloshStrength => horizontalSloshStrength;
    public float SloshSpring => sloshSpring;
    public float SloshDamping => sloshDamping;
    public float MaximumSlosh => maximumSlosh;
    public bool TiltEnabled => tiltEnabled;
    public float MaximumTiltDegrees => maximumTiltDegrees;
    public float TiltSpring => tiltSpring;
    public float TiltDamping => tiltDamping;
    public bool ValueImpulseEnabled => valueImpulseEnabled;
    public float ValueImpulseStrength => valueImpulseStrength;
    public float ValueImpulseDecay => valueImpulseDecay;
    #endregion
}

/// <summary>
/// Stores presentation settings specific to one health or shield syringe.
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
    #endregion

    #region Validation
    /// <summary>
    /// Reports invalid channel values without mutating serialized authoring data.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    /// <param name="channelLabel">User-facing channel label used by warning messages.</param>
    public void Validate(string ownerAssetName, string channelLabel)
    {
        if (palette == null || fluid == null || motion == null)
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

/// <summary>
/// Stores scalable authoring data shared by the player health and shield syringe HUD views.
/// </summary>
[Serializable]
public sealed class PlayerHealthBarsVisualSettings
{
    #region Constants
    public const int AuthoredLabelPoolCapacity = 24;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the ECS-authoritative player health and shield syringe HUD.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Hides both syringe views while no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;

    [Tooltip("Vertical spacing in pixels between the health and shield syringe roots.")]
    [Range(-200f, 200f)]
    [SerializeField] private float verticalSpacing = 12f;

    [Tooltip("Authoritative value represented by every full major graduation interval.")]
    [Range(0.1f, 100f)]
    [SerializeField] private float unitsPerMajorDivision = 1f;

    [Tooltip("Horizontal pixels assigned to every full major graduation interval.")]
    [Range(8f, 256f)]
    [SerializeField] private float pixelsPerMajorDivision = 52f;

    [Tooltip("Minimum complete syringe width in pixels.")]
    [Range(64f, 2048f)]
    [SerializeField] private float minimumLength = 340f;

    [Tooltip("Maximum complete syringe width in pixels before the view stops growing and reports a warning.")]
    [Range(64f, 2048f)]
    [SerializeField] private float maximumLength = 760f;

    [Tooltip("Number of smaller graduation intervals drawn inside every major interval.")]
    [SerializeField] private int minorDivisionsPerMajor = 1;

    [Tooltip("Displays one numeric label every N major graduation intervals.")]
    [SerializeField] private int labelEveryMajorDivision = 1;

    [Tooltip("Maximum number of preauthored numeric labels the runtime may activate per syringe.")]
    [SerializeField] private int maximumLabelCount = AuthoredLabelPoolCapacity;

    [Tooltip("Minimum horizontal pixel spacing maintained between active numeric labels before their interval is increased automatically.")]
    [Range(8f, 256f)]
    [SerializeField] private float labelMinimumSpacing = 46f;

    [Tooltip("Additional horizontal pixels reserved before the first graduated value; no matching padding is added after the final value.")]
    [Range(0f, 256f)]
    [SerializeField] private float graduationEndPadding;

    [Tooltip("Places graduation ticks and numeric labels inside the liquid chamber or on a dedicated external plate.")]
    [SerializeField] private PlayerSyringeLabelPlacement labelPlacement = PlayerSyringeLabelPlacement.InsideChamber;

    [Tooltip("TextMeshPro font asset applied directly to every numeric graduation label.")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Tooltip("TextMeshPro font size used by numeric graduation labels.")]
    [Range(6f, 72f)]
    [SerializeField] private float labelFontSize = 15f;

    [Tooltip("Pixel offset applied to every numeric label relative to its major tick.")]
    [SerializeField] private Vector2 labelOffset = Vector2.zero;

    [Tooltip("TextMeshPro outline width used to keep numeric graduation labels readable over changing liquid colors.")]
    [Range(0f, 1f)]
    [SerializeField] private float labelOutlineWidth = 0.12f;

    [Tooltip("Optional normalized vertical offset applied to the entire graduation - ticks and numeric labels - within the syringe. Positive values move the graduation up.")]
    [Range(-0.5f, 0.5f)]
    [SerializeField] private float graduationVerticalOffset;

    [Tooltip("Procedural body silhouette shared by the health and shield syringe channels.")]
    [SerializeField] private PlayerSyringeBodyStyle bodyStyle = PlayerSyringeBodyStyle.SimplePaintedContainer;

    [Tooltip("Complete procedural syringe height in pixels.")]
    [Range(24f, 256f)]
    [SerializeField] private float barHeight = 88f;

    [Tooltip("Normalized outline thickness relative to the complete syringe height.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float outlineThickness = 0.035f;

    [Tooltip("Normalized inset separating the liquid chamber from the outer body.")]
    [Range(0f, 0.49f)]
    [SerializeField] private float chamberInset = 0.16f;

    [Tooltip("Reference-length normalized width of the plunger head. Runtime compensation preserves its pixel footprint across short and long syringes.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float plungerWidth = 0.032f;

    [Tooltip("Horizontal width of each non-scaling end cap; the simplified right termination starts at the final graduated value.")]
    [Range(0f, 256f)]
    [SerializeField] private float endCapWidth = 36f;

    [Tooltip("Horizontal pixel gap maintained between the final graduated value and the simplified right termination.")]
    [Range(0f, 256f)]
    [SerializeField] private float terminationOffset = 8f;

    [Tooltip("Procedural silhouette used by both syringe end caps.")]
    [SerializeField] private PlayerSyringeTerminationStyle terminationStyle = PlayerSyringeTerminationStyle.Angular;

    [Tooltip("Optional procedural paint-like drips extending from the syringe body borders.")]
    [SerializeField] private PlayerSyringePaintDripSettings paintDrips = new PlayerSyringePaintDripSettings();

    [Tooltip("Health syringe presentation and motion settings.")]
    [SerializeField] private PlayerSyringeChannelSettings health = new PlayerSyringeChannelSettings();

    [Tooltip("Shield syringe presentation and motion settings.")]
    [SerializeField] private PlayerSyringeChannelSettings shield = new PlayerSyringeChannelSettings(true, true);
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public bool HideWhenPlayerMissing => hideWhenPlayerMissing;
    public float VerticalSpacing => verticalSpacing;
    public float UnitsPerMajorDivision => unitsPerMajorDivision;
    public float PixelsPerMajorDivision => pixelsPerMajorDivision;
    public float MinimumLength => minimumLength;
    public float MaximumLength => maximumLength;
    public int MinorDivisionsPerMajor => minorDivisionsPerMajor;
    public int LabelEveryMajorDivision => labelEveryMajorDivision;
    public int MaximumLabelCount => maximumLabelCount;
    public float LabelMinimumSpacing => labelMinimumSpacing;
    public float GraduationEndPadding => graduationEndPadding;
    public PlayerSyringeLabelPlacement LabelPlacement => labelPlacement;
    public TMP_FontAsset FontAsset => fontAsset;
    public float LabelFontSize => labelFontSize;
    public Vector2 LabelOffset => labelOffset;
    public float LabelOutlineWidth => labelOutlineWidth;
    public float GraduationVerticalOffset => graduationVerticalOffset;
    public PlayerSyringeBodyStyle BodyStyle => bodyStyle;
    public float BarHeight => barHeight;
    public float OutlineThickness => outlineThickness;
    public float ChamberInset => chamberInset;
    public float PlungerWidth => plungerWidth;
    public float EndCapWidth => endCapWidth;
    public float TerminationOffset => terminationOffset;
    public PlayerSyringeTerminationStyle TerminationStyle => terminationStyle;
    public PlayerSyringePaintDripSettings PaintDrips => paintDrips;
    public PlayerSyringeChannelSettings Health => health;
    public PlayerSyringeChannelSettings Shield => shield;
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid health-bar authoring values without snapping or mutating them.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (!IsFinite(verticalSpacing) || verticalSpacing < -200f || verticalSpacing > 200f)
            LogWarning(ownerAssetName, "Vertical Spacing should be finite and within -200 to 200.");

        if (!IsFinite(unitsPerMajorDivision) || unitsPerMajorDivision < 0.1f || unitsPerMajorDivision > 100f)
            LogWarning(ownerAssetName, "Units Per Major Division should be finite and within 0.1-100.");

        if (!IsFinite(pixelsPerMajorDivision) || pixelsPerMajorDivision < 8f || pixelsPerMajorDivision > 256f)
            LogWarning(ownerAssetName, "Pixels Per Major Division should be finite and within 8-256.");

        if (!IsFinite(minimumLength) ||
            !IsFinite(maximumLength) ||
            minimumLength < 64f ||
            maximumLength > 2048f ||
            maximumLength < minimumLength)
        {
            LogWarning(ownerAssetName, "Minimum and Maximum Length should be finite, ordered, and within 64-2048.");
        }

        if (minorDivisionsPerMajor < 1)
            LogWarning(ownerAssetName, "Minor Divisions Per Major should be at least one.");

        if (labelEveryMajorDivision < 1)
            LogWarning(ownerAssetName, "Label Every Major Division should be at least one.");

        if (maximumLabelCount < 2 || maximumLabelCount > AuthoredLabelPoolCapacity)
            LogWarning(ownerAssetName, string.Format("Maximum Label Count should be between 2 and the preauthored pool capacity of {0}.",
                                                     AuthoredLabelPoolCapacity));

        if (!IsFinite(labelMinimumSpacing) || labelMinimumSpacing < 8f || labelMinimumSpacing > 256f)
            LogWarning(ownerAssetName, "Label Minimum Spacing should be finite and within 8-256.");

        if (!IsFinite(graduationEndPadding) || graduationEndPadding < 0f || graduationEndPadding > 256f)
            LogWarning(ownerAssetName, "Graduation End Padding should be finite and within 0-256.");

        if (!IsSupportedLabelPlacement(labelPlacement))
            LogWarning(ownerAssetName, "Label Placement should resolve to Inside Chamber or Graduation Plate.");

        if (!IsFinite(labelFontSize) || labelFontSize < 6f || labelFontSize > 72f)
            LogWarning(ownerAssetName, "Label Font Size should be finite and within 6-72.");

        if (!IsFinite(labelOffset.x) || !IsFinite(labelOffset.y))
            LogWarning(ownerAssetName, "Label Offset components should be finite.");

        if (!IsFinite(labelOutlineWidth) || labelOutlineWidth < 0f || labelOutlineWidth > 1f)
            LogWarning(ownerAssetName, "Label Outline Width should be finite and within 0-1.");

        if (!IsFinite(graduationVerticalOffset) || graduationVerticalOffset < -0.5f || graduationVerticalOffset > 0.5f)
            LogWarning(ownerAssetName, "Graduation Vertical Offset should be finite and within -0.5 to 0.5.");

        if (!IsSupportedBodyStyle(bodyStyle))
            LogWarning(ownerAssetName, "Body Style should resolve to Simple Painted Container or Detailed Syringe.");

        if (!IsFinite(barHeight) || barHeight < 24f || barHeight > 256f)
            LogWarning(ownerAssetName, "Bar Height should be finite and within 24-256.");

        if (!IsFinite(outlineThickness) || outlineThickness < 0f || outlineThickness > 0.2f)
            LogWarning(ownerAssetName, "Outline Thickness should be finite and within 0-0.2.");

        if (!IsFinite(chamberInset) || chamberInset < 0f || chamberInset >= 0.5f)
            LogWarning(ownerAssetName, "Chamber Inset should be finite, non-negative, and lower than 0.5.");

        if (!IsFinite(plungerWidth) || plungerWidth < 0f || plungerWidth > 0.2f)
            LogWarning(ownerAssetName, "Plunger Width should be finite and within 0-0.2.");

        if (!IsFinite(endCapWidth) || endCapWidth < 0f || endCapWidth > 256f)
            LogWarning(ownerAssetName, "End Cap Width should be finite and within 0-256.");

        if (!IsFinite(terminationOffset) || terminationOffset < 0f || terminationOffset > 256f)
            LogWarning(ownerAssetName, "Termination Offset should be finite and within 0-256.");

        if (!IsSupportedTerminationStyle(terminationStyle))
            LogWarning(ownerAssetName, "Termination Style should resolve to Flat, Angular, Rounded, or Needle.");

        if (paintDrips == null || health == null || shield == null)
        {
            LogWarning(ownerAssetName, "Paint Drips, Health, or Shield settings are missing.");
            return;
        }

        paintDrips.Validate(ownerAssetName);
        health.Validate(ownerAssetName, "Health");
        shield.Validate(ownerAssetName, "Shield");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one shared health-bars preset warning.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name.</param>
    /// <param name="message">Warning message.</param>
    private static void LogWarning(string ownerAssetName, string message)
    {
        Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Health Bars: {1}", ownerAssetName, message));
    }

    /// <summary>
    /// Checks whether one authored termination style maps to a supported procedural silhouette.
    /// </summary>
    /// <param name="value">Termination style to inspect.</param>
    /// <returns>True when the style is supported by the syringe shader.</returns>
    private static bool IsSupportedTerminationStyle(PlayerSyringeTerminationStyle value)
    {
        switch (value)
        {
            case PlayerSyringeTerminationStyle.Flat:
            case PlayerSyringeTerminationStyle.Angular:
            case PlayerSyringeTerminationStyle.Rounded:
            case PlayerSyringeTerminationStyle.Needle:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether one authored body style maps to a supported procedural silhouette.
    /// </summary>
    /// <param name="value">Body style to inspect.</param>
    /// <returns>True when the style is supported by the syringe shader.</returns>
    private static bool IsSupportedBodyStyle(PlayerSyringeBodyStyle value)
    {
        switch (value)
        {
            case PlayerSyringeBodyStyle.SimplePaintedContainer:
            case PlayerSyringeBodyStyle.DetailedSyringe:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether one authored label placement maps to a supported runtime layout.
    /// </summary>
    /// <param name="value">Label placement to inspect.</param>
    /// <returns>True when the placement is supported by the syringe view and shader.</returns>
    private static bool IsSupportedLabelPlacement(PlayerSyringeLabelPlacement value)
    {
        switch (value)
        {
            case PlayerSyringeLabelPlacement.InsideChamber:
            case PlayerSyringeLabelPlacement.GraduationPlate:
                return true;
            default:
                return false;
        }
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
