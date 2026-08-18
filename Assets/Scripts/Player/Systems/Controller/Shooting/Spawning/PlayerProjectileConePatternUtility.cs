using Unity.Mathematics;

/// <summary>
/// Resolves deterministic projectile-cone angles while treating a 360-degree cone as a closed, duplicate-free circle.
/// </summary>
public static class PlayerProjectileConePatternUtility
{
    #region Constants
    public const float FullCircleDegrees = 360f;
    private const float FullCircleToleranceDegrees = 0.001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Clamps a runtime cone angle to the supported planar interval without mutating its authored source.
    /// </summary>
    /// <param name="coneAngleDegrees">Authored total cone angle.</param>
    /// <returns>Cone angle constrained to zero through 360 degrees.</returns>
    public static float ResolveConeAngleDegrees(float coneAngleDegrees)
    {
        return math.clamp(coneAngleDegrees, 0f, FullCircleDegrees);
    }

    /// <summary>
    /// Resolves one centered cone direction, omitting the duplicated positive endpoint for full-circle patterns.
    /// </summary>
    /// <param name="projectileIndex">Zero-based projectile or beam-lane index.</param>
    /// <param name="projectileCount">Total directions emitted by the pattern.</param>
    /// <param name="coneAngleDegrees">Authored total cone angle.</param>
    /// <returns>Signed planar angle relative to the source forward direction.</returns>
    public static float ResolveDirectionAngleDegrees(int projectileIndex,
                                                     int projectileCount,
                                                     float coneAngleDegrees)
    {
        int safeProjectileCount = math.max(1, projectileCount);

        if (safeProjectileCount <= 1)
            return 0f;

        float safeConeAngleDegrees = ResolveConeAngleDegrees(coneAngleDegrees);
        bool isFullCircle = safeConeAngleDegrees >= FullCircleDegrees - FullCircleToleranceDegrees;
        float stepDegrees = isFullCircle
            ? FullCircleDegrees / safeProjectileCount
            : safeConeAngleDegrees / (safeProjectileCount - 1);
        return -safeConeAngleDegrees * 0.5f + stepDegrees * math.clamp(projectileIndex, 0, safeProjectileCount - 1);
    }
    #endregion

    #endregion
}
