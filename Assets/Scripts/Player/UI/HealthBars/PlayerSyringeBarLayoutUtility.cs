using Unity.Mathematics;

/// <summary>
/// Provides deterministic layout calculations shared by procedural syringe HUD views.
/// </summary>
internal static class PlayerSyringeBarLayoutUtility
{
    #region Methods

    #region Shape
    /// <summary>
    /// Converts the shared legacy geometry fields into the explicit shape structure used by the runtime view.
    /// </summary>
    /// <param name="shared">Shared health-bar visual configuration.</param>
    /// <returns>Shape configuration equivalent to the shared health and shield geometry.</returns>
    public static PlayerSyringeShapeConfig BuildShapeFromShared(in PlayerHealthBarVisualConfig shared)
    {
        return new PlayerSyringeShapeConfig
        {
            LabelOffset = shared.LabelOffset,
            UnitsPerMajorDivision = shared.UnitsPerMajorDivision,
            PixelsPerMajorDivision = shared.PixelsPerMajorDivision,
            MinimumLength = shared.MinimumLength,
            MaximumLength = shared.MaximumLength,
            LabelMinimumSpacing = shared.LabelMinimumSpacing,
            GraduationEndPadding = shared.GraduationEndPadding,
            LabelFontSize = shared.LabelFontSize,
            LabelOutlineWidth = shared.LabelOutlineWidth,
            GraduationVerticalOffset = shared.GraduationVerticalOffset,
            BarHeight = shared.BarHeight,
            OutlineThickness = shared.OutlineThickness,
            ChamberInset = shared.ChamberInset,
            PlungerWidth = shared.PlungerWidth,
            EndCapWidth = shared.EndCapWidth,
            TerminationOffset = shared.TerminationOffset,
            MinorDivisionsPerMajor = shared.MinorDivisionsPerMajor,
            LabelEveryMajorDivision = shared.LabelEveryMajorDivision,
            MaximumLabelCount = shared.MaximumLabelCount,
            UniformLabelCount = shared.UniformLabelCount,
            PaintDrips = shared.PaintDrips,
            BodyStyle = shared.BodyStyle,
            LabelPlacement = shared.LabelPlacement,
            GraduationMode = shared.GraduationMode,
            TerminationStyle = shared.TerminationStyle,
            ClampPlungerStartInsideBody = shared.ClampPlungerStartInsideBody,
            ClampPlungerEndInsideBody = shared.ClampPlungerEndInsideBody,
            StopLiquidAtPlunger = shared.StopLiquidAtPlunger,
            TerminationEnabled = shared.TerminationEnabled
        };
    }
    #endregion

    #region Size
    /// <summary>
    /// Converts a reference-length normalized visual size into the normalized size needed by the current syringe length.
    /// </summary>
    /// <param name="normalizedValue">Authored normalized value tuned against the reference syringe length.</param>
    /// <param name="resolvedLength">Current resolved syringe length in pixels.</param>
    /// <param name="maximumValue">Maximum normalized value accepted by the target shader property.</param>
    /// <param name="referenceLength">Reference syringe length used by the authored value.</param>
    /// <returns>Length-compensated normalized value preserving stable pixel size across short and long syringes.</returns>
    public static float ResolveReferenceScaledNormalized(float normalizedValue,
                                                         float resolvedLength,
                                                         float maximumValue,
                                                         float referenceLength)
    {
        return math.clamp(math.max(0f, normalizedValue) *
                          referenceLength /
                          math.max(1f, resolvedLength),
                          0f,
                          maximumValue);
    }

    /// <summary>
    /// Resolves the number of value-track intervals that should drive syringe length and tick spacing.
    /// </summary>
    /// <param name="maximumValue">Authoritative maximum represented by this syringe.</param>
    /// <param name="safeUnitsPerMajorDivision">Positive value represented by a fixed major interval.</param>
    /// <param name="graduationMode">Runtime graduation distribution mode.</param>
    /// <param name="uniformLabelCount">Requested uniform label count.</param>
    /// <returns>Non-negative interval count used by layout and shader tick distribution.</returns>
    public static float ResolveLayoutIntervalCount(float maximumValue,
                                                   float safeUnitsPerMajorDivision,
                                                   PlayerSyringeGraduationMode graduationMode,
                                                   int uniformLabelCount)
    {
        switch (graduationMode)
        {
            case PlayerSyringeGraduationMode.UniformLabels:
                return math.max(1f, uniformLabelCount > 1 ? uniformLabelCount - 1 : 1);
            default:
                return math.max(0f, maximumValue / safeUnitsPerMajorDivision);
        }
    }
    #endregion

    #region Graduation
    /// <summary>
    /// Resolves unsupported runtime graduation enum values back to the authored fixed-unit behavior.
    /// </summary>
    /// <param name="graduationMode">Runtime value that may have been changed by formulas.</param>
    /// <returns>Supported graduation mode.</returns>
    public static PlayerSyringeGraduationMode ResolveGraduationMode(PlayerSyringeGraduationMode graduationMode)
    {
        switch (graduationMode)
        {
            case PlayerSyringeGraduationMode.FixedUnits:
            case PlayerSyringeGraduationMode.UniformLabels:
            case PlayerSyringeGraduationMode.Hidden:
                return graduationMode;
            default:
                return PlayerSyringeGraduationMode.FixedUnits;
        }
    }
    #endregion

    #endregion
}
