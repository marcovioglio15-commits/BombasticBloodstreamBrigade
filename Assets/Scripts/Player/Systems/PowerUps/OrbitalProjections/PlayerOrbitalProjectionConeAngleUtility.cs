using Unity.Mathematics;

/// <summary>
/// Resolves cone intervals and angle operations shared by orbital projection cone layouts.
/// </summary>
internal static class PlayerOrbitalProjectionConeAngleUtility
{
    #region Constants
    public const float FullCircleDegrees = 360f;
    public const float HalfCircleDegrees = 180f;
    public const float ConeAngleEpsilon = 0.01f;
    public const float IntervalOverlapTolerance = 0.001f;
    #endregion

    #region Methods

    #region Interval Methods
    /// <summary>
    /// Resolves one authored cone into an unwrapped angular interval near the supplied reference angle.
    /// </summary>
    /// <param name="config">Projection config carrying authored cone data.</param>
    /// <param name="referenceDegrees">Reference angle used to unwrap the cone center.</param>
    /// <param name="intervalStartDegrees">Resolved interval start.</param>
    /// <param name="intervalEndDegrees">Resolved interval end.</param>
    /// <returns>True when the authored cone angle is usable at runtime.</returns>
    public static bool TryResolveConeInterval(in OrbitalProjectionConfig config,
                                              float referenceDegrees,
                                              out float intervalStartDegrees,
                                              out float intervalEndDegrees)
    {
        intervalStartDegrees = 0f;
        intervalEndDegrees = 0f;
        float coneAngleDegrees = ResolveConeAngleDegrees(in config);

        if (coneAngleDegrees <= ConeAngleEpsilon)
            return false;

        float centerDegrees = UnwrapNear(config.OrbitConeCenterAngleDegrees, referenceDegrees);
        float halfConeDegrees = coneAngleDegrees * 0.5f;
        intervalStartDegrees = centerDegrees - halfConeDegrees;
        intervalEndDegrees = centerDegrees + halfConeDegrees;
        return true;
    }

    /// <summary>
    /// Splits one interval into an equal deterministic sub-sector.
    /// </summary>
    /// <param name="intervalStartDegrees">Whole interval start in unwrapped degrees.</param>
    /// <param name="intervalEndDegrees">Whole interval end in unwrapped degrees.</param>
    /// <param name="sectorCount">Number of equal sectors requested.</param>
    /// <param name="sectorSlot">Zero-based sector slot assigned to this projection.</param>
    /// <param name="sectorStartDegrees">Resolved sub-sector start.</param>
    /// <param name="sectorEndDegrees">Resolved sub-sector end.</param>
    public static void ResolveSubSector(float intervalStartDegrees,
                                        float intervalEndDegrees,
                                        int sectorCount,
                                        int sectorSlot,
                                        out float sectorStartDegrees,
                                        out float sectorEndDegrees)
    {
        float sectorWidthDegrees = math.max(0f, intervalEndDegrees - intervalStartDegrees) / math.max(1, sectorCount);
        sectorStartDegrees = intervalStartDegrees + sectorWidthDegrees * math.clamp(sectorSlot, 0, math.max(0, sectorCount - 1));
        sectorEndDegrees = sectorStartDegrees + sectorWidthDegrees;
    }

    /// <summary>
    /// Checks whether two unwrapped angular intervals overlap or touch within tolerance.
    /// </summary>
    /// <param name="firstStartDegrees">First interval start.</param>
    /// <param name="firstEndDegrees">First interval end.</param>
    /// <param name="secondStartDegrees">Second interval start.</param>
    /// <param name="secondEndDegrees">Second interval end.</param>
    /// <returns>True when the intervals share angular coverage.</returns>
    public static bool IntervalsOverlap(float firstStartDegrees,
                                        float firstEndDegrees,
                                        float secondStartDegrees,
                                        float secondEndDegrees)
    {
        return firstStartDegrees <= secondEndDegrees + IntervalOverlapTolerance &&
               secondStartDegrees <= firstEndDegrees + IntervalOverlapTolerance;
    }

    /// <summary>
    /// Resolves one cone angle into the runtime-supported full-circle range.
    /// </summary>
    /// <param name="config">Projection config carrying authored or scaled cone width.</param>
    /// <returns>Cone angle clamped to zero through 360 degrees.</returns>
    public static float ResolveConeAngleDegrees(in OrbitalProjectionConfig config)
    {
        return math.clamp(config.OrbitConeAngleDegrees, 0f, FullCircleDegrees);
    }

    /// <summary>
    /// Resolves the center angle of one unwrapped interval.
    /// </summary>
    /// <param name="intervalStartDegrees">Interval start.</param>
    /// <param name="intervalEndDegrees">Interval end.</param>
    /// <returns>Unwrapped center angle in degrees.</returns>
    public static float ResolveIntervalCenter(float intervalStartDegrees, float intervalEndDegrees)
    {
        return intervalStartDegrees + (intervalEndDegrees - intervalStartDegrees) * 0.5f;
    }
    #endregion

    #region Angle Methods
    /// <summary>
    /// Unwraps an angle to the shortest equivalent representation near a reference angle.
    /// </summary>
    /// <param name="angleDegrees">Angle to unwrap.</param>
    /// <param name="referenceDegrees">Reference angle that anchors the unwrapped representation.</param>
    /// <returns>Equivalent angle nearest to the reference angle.</returns>
    public static float UnwrapNear(float angleDegrees, float referenceDegrees)
    {
        return referenceDegrees + ResolveSignedAngleDelta(referenceDegrees, angleDegrees);
    }

    /// <summary>
    /// Normalizes one angle into the zero-through-360 range.
    /// </summary>
    /// <param name="angleDegrees">Angle to normalize.</param>
    /// <returns>Normalized angle in degrees.</returns>
    public static float NormalizeAngle(float angleDegrees)
    {
        float normalizedDegrees = math.fmod(angleDegrees, FullCircleDegrees);

        if (normalizedDegrees < 0f)
            normalizedDegrees += FullCircleDegrees;

        return normalizedDegrees;
    }

    /// <summary>
    /// Resolves the clockwise distance from one normalized angle to another.
    /// </summary>
    /// <param name="startDegrees">Clockwise search start.</param>
    /// <param name="endDegrees">Clockwise search end.</param>
    /// <returns>Clockwise distance in the zero-through-360 range.</returns>
    public static float ResolveClockwiseDistance(float startDegrees, float endDegrees)
    {
        float distanceDegrees = NormalizeAngle(endDegrees) - NormalizeAngle(startDegrees);

        if (distanceDegrees < 0f)
            distanceDegrees += FullCircleDegrees;

        return distanceDegrees;
    }

    /// <summary>
    /// Resolves the shortest signed delta from one angle to another.
    /// </summary>
    /// <param name="currentDegrees">Current angle in degrees.</param>
    /// <param name="targetDegrees">Target angle in degrees.</param>
    /// <returns>Signed delta in the -180 through 180 range.</returns>
    public static float ResolveSignedAngleDelta(float currentDegrees, float targetDegrees)
    {
        return math.fmod(targetDegrees - currentDegrees + 540f, FullCircleDegrees) - HalfCircleDegrees;
    }
    #endregion

    #endregion
}
