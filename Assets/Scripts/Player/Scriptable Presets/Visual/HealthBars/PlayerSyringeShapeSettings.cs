using System;
using UnityEngine;

/// <summary>
/// Stores silhouette, graduation, and numeric-label settings used by one procedural syringe shape.
/// </summary>
[Serializable]
public sealed class PlayerSyringeShapeSettings
{
    #region Serialized Fields
    [Tooltip("Authoritative value represented by every full major graduation interval.")]
    [Range(0.1f, 100f)]
    [SerializeField] private float unitsPerMajorDivision = 1f;

    [Tooltip("Horizontal pixels assigned to every full major graduation interval.")]
    [Range(8f, 256f)]
    [SerializeField] private float pixelsPerMajorDivision = 52f;

    [Tooltip("Selects whether graduations use fixed value units, uniformly distributed labels, or stay completely hidden.")]
    [SerializeField] private PlayerSyringeGraduationMode graduationMode = PlayerSyringeGraduationMode.FixedUnits;

    [Tooltip("Total numeric labels shown in Uniform Labels mode. Labels are distributed evenly from zero to the represented maximum.")]
    [Range(0, PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity)]
    [SerializeField] private int uniformLabelCount = 5;

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
    [SerializeField] private int maximumLabelCount = PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity;

    [Tooltip("Minimum horizontal pixel spacing maintained between active numeric labels before their interval is increased automatically.")]
    [Range(8f, 256f)]
    [SerializeField] private float labelMinimumSpacing = 46f;

    [Tooltip("Additional horizontal pixels reserved before the first graduated value; no matching padding is added after the final value.")]
    [Range(0f, 256f)]
    [SerializeField] private float graduationEndPadding;

    [Tooltip("Places graduation ticks and numeric labels inside the liquid chamber or on a dedicated external plate.")]
    [SerializeField] private PlayerSyringeLabelPlacement labelPlacement = PlayerSyringeLabelPlacement.InsideChamber;

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

    [Tooltip("Procedural body silhouette used by this syringe shape.")]
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

    [Tooltip("Keeps the plunger head inside the syringe body when the represented value is at the first graduated position.")]
    [SerializeField] private bool clampPlungerStartInsideBody = true;

    [Tooltip("Keeps the plunger head inside the syringe body when the represented value is at the final graduated position.")]
    [SerializeField] private bool clampPlungerEndInsideBody;

    [Tooltip("Stops the liquid boundary at the plunger's leading edge so the fluid never renders underneath the plunger head.")]
    [SerializeField] private bool stopLiquidAtPlunger = true;

    [Tooltip("Draws the right-side syringe termination and reserves its dedicated spacing in the procedural layout.")]
    [SerializeField] private bool terminationEnabled = true;

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
    #endregion

    #region Properties
    public float UnitsPerMajorDivision => unitsPerMajorDivision;
    public float PixelsPerMajorDivision => pixelsPerMajorDivision;
    public PlayerSyringeGraduationMode GraduationMode => graduationMode;
    public int UniformLabelCount => uniformLabelCount;
    public float MinimumLength => minimumLength;
    public float MaximumLength => maximumLength;
    public int MinorDivisionsPerMajor => minorDivisionsPerMajor;
    public int LabelEveryMajorDivision => labelEveryMajorDivision;
    public int MaximumLabelCount => maximumLabelCount;
    public float LabelMinimumSpacing => labelMinimumSpacing;
    public float GraduationEndPadding => graduationEndPadding;
    public PlayerSyringeLabelPlacement LabelPlacement => labelPlacement;
    public float LabelFontSize => labelFontSize;
    public Vector2 LabelOffset => labelOffset;
    public float LabelOutlineWidth => labelOutlineWidth;
    public float GraduationVerticalOffset => graduationVerticalOffset;
    public PlayerSyringeBodyStyle BodyStyle => bodyStyle;
    public float BarHeight => barHeight;
    public float OutlineThickness => outlineThickness;
    public float ChamberInset => chamberInset;
    public float PlungerWidth => plungerWidth;
    public bool ClampPlungerStartInsideBody => clampPlungerStartInsideBody;
    public bool ClampPlungerEndInsideBody => clampPlungerEndInsideBody;
    public bool StopLiquidAtPlunger => stopLiquidAtPlunger;
    public bool TerminationEnabled => terminationEnabled;
    public float EndCapWidth => endCapWidth;
    public float TerminationOffset => terminationOffset;
    public PlayerSyringeTerminationStyle TerminationStyle => terminationStyle;
    public PlayerSyringePaintDripSettings PaintDrips => paintDrips;
    #endregion

    #region Methods

    #region Construction
    /// <summary>
    /// Creates a syringe-shape profile with player health-bar defaults.
    /// </summary>
    public PlayerSyringeShapeSettings()
    {
    }

    /// <summary>
    /// Creates defaults tuned for the player experience syringe.
    /// </summary>
    /// <returns>New shape settings using compact experience-bar dimensions.</returns>
    public static PlayerSyringeShapeSettings CreateExperienceDefaults()
    {
        return new PlayerSyringeShapeSettings
        {
            unitsPerMajorDivision = 1f,
            pixelsPerMajorDivision = 46f,
            graduationMode = PlayerSyringeGraduationMode.UniformLabels,
            uniformLabelCount = 5,
            minimumLength = 260f,
            maximumLength = 620f,
            labelMinimumSpacing = 40f,
            barHeight = 58f,
            endCapWidth = 30f,
            terminationOffset = 4f
        };
    }
    #endregion

    #region Validation
    /// <summary>
    /// Reports invalid shape authoring values without snapping serialized data.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    /// <param name="shapeLabel">User-facing shape label used by warning messages.</param>
    public void Validate(string ownerAssetName, string shapeLabel)
    {
        if (!IsFinite(unitsPerMajorDivision) || unitsPerMajorDivision < 0.1f || unitsPerMajorDivision > 100f)
            LogWarning(ownerAssetName, shapeLabel, "Units Per Major Division should be finite and within 0.1-100.");

        if (!IsFinite(pixelsPerMajorDivision) || pixelsPerMajorDivision < 8f || pixelsPerMajorDivision > 256f)
            LogWarning(ownerAssetName, shapeLabel, "Pixels Per Major Division should be finite and within 8-256.");

        if (!IsSupportedGraduationMode(graduationMode))
            LogWarning(ownerAssetName, shapeLabel, "Graduation Mode should resolve to Fixed Units, Uniform Labels, or Hidden.");

        if (uniformLabelCount < 0 || uniformLabelCount > PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity)
            LogWarning(ownerAssetName, shapeLabel, "Uniform Label Count should stay inside the preauthored label-pool range.");

        if (!IsFinite(minimumLength) ||
            !IsFinite(maximumLength) ||
            minimumLength < 64f ||
            maximumLength > 2048f ||
            maximumLength < minimumLength)
        {
            LogWarning(ownerAssetName, shapeLabel, "Minimum and Maximum Length should be finite, ordered, and within 64-2048.");
        }

        if (minorDivisionsPerMajor < 1)
            LogWarning(ownerAssetName, shapeLabel, "Minor Divisions Per Major should be at least one.");

        if (labelEveryMajorDivision < 1)
            LogWarning(ownerAssetName, shapeLabel, "Label Every Major Division should be at least one.");

        if (maximumLabelCount < 2 || maximumLabelCount > PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity)
            LogWarning(ownerAssetName, shapeLabel, "Maximum Label Count should stay inside the preauthored label-pool range.");

        if (!IsFinite(labelMinimumSpacing) || labelMinimumSpacing < 8f || labelMinimumSpacing > 256f)
            LogWarning(ownerAssetName, shapeLabel, "Label Minimum Spacing should be finite and within 8-256.");

        if (!IsFinite(graduationEndPadding) || graduationEndPadding < 0f || graduationEndPadding > 256f)
            LogWarning(ownerAssetName, shapeLabel, "Graduation End Padding should be finite and within 0-256.");

        if (!IsSupportedLabelPlacement(labelPlacement))
            LogWarning(ownerAssetName, shapeLabel, "Label Placement should resolve to Inside Chamber or Graduation Plate.");

        if (!IsFinite(labelFontSize) || labelFontSize < 6f || labelFontSize > 72f)
            LogWarning(ownerAssetName, shapeLabel, "Label Font Size should be finite and within 6-72.");

        if (!IsFinite(labelOffset.x) || !IsFinite(labelOffset.y))
            LogWarning(ownerAssetName, shapeLabel, "Label Offset components should be finite.");

        if (!IsFinite(labelOutlineWidth) || labelOutlineWidth < 0f || labelOutlineWidth > 1f)
            LogWarning(ownerAssetName, shapeLabel, "Label Outline Width should be finite and within 0-1.");

        if (!IsFinite(graduationVerticalOffset) || graduationVerticalOffset < -0.5f || graduationVerticalOffset > 0.5f)
            LogWarning(ownerAssetName, shapeLabel, "Graduation Vertical Offset should be finite and within -0.5 to 0.5.");

        if (!IsSupportedBodyStyle(bodyStyle))
            LogWarning(ownerAssetName, shapeLabel, "Body Style should resolve to Simple Painted Container or Detailed Syringe.");

        if (!IsFinite(barHeight) || barHeight < 24f || barHeight > 256f)
            LogWarning(ownerAssetName, shapeLabel, "Bar Height should be finite and within 24-256.");

        if (!IsFinite(outlineThickness) || outlineThickness < 0f || outlineThickness > 0.2f)
            LogWarning(ownerAssetName, shapeLabel, "Outline Thickness should be finite and within 0-0.2.");

        if (!IsFinite(chamberInset) || chamberInset < 0f || chamberInset >= 0.5f)
            LogWarning(ownerAssetName, shapeLabel, "Chamber Inset should be finite, non-negative, and lower than 0.5.");

        if (!IsFinite(plungerWidth) || plungerWidth < 0f || plungerWidth > 0.2f)
            LogWarning(ownerAssetName, shapeLabel, "Plunger Width should be finite and within 0-0.2.");

        if (!IsFinite(endCapWidth) || endCapWidth < 0f || endCapWidth > 256f)
            LogWarning(ownerAssetName, shapeLabel, "End Cap Width should be finite and within 0-256.");

        if (!IsFinite(terminationOffset) || terminationOffset < 0f || terminationOffset > 256f)
            LogWarning(ownerAssetName, shapeLabel, "Termination Offset should be finite and within 0-256.");

        if (!IsSupportedTerminationStyle(terminationStyle))
            LogWarning(ownerAssetName, shapeLabel, "Termination Style should resolve to Flat, Angular, Rounded, or Needle.");

        if (paintDrips == null)
        {
            LogWarning(ownerAssetName, shapeLabel, "Paint Drips settings are missing.");
            return;
        }

        paintDrips.Validate(ownerAssetName);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one syringe-shape warning.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name.</param>
    /// <param name="shapeLabel">User-facing shape label.</param>
    /// <param name="message">Warning message.</param>
    private static void LogWarning(string ownerAssetName, string shapeLabel, string message)
    {
        Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Health Bars/{1} Shape: {2}",
                                       ownerAssetName,
                                       shapeLabel,
                                       message));
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
    /// Checks whether one authored graduation mode maps to a supported runtime distribution.
    /// </summary>
    /// <param name="value">Graduation mode to inspect.</param>
    /// <returns>True when the mode is supported by the syringe view and shader.</returns>
    private static bool IsSupportedGraduationMode(PlayerSyringeGraduationMode value)
    {
        switch (value)
        {
            case PlayerSyringeGraduationMode.FixedUnits:
            case PlayerSyringeGraduationMode.UniformLabels:
            case PlayerSyringeGraduationMode.Hidden:
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
