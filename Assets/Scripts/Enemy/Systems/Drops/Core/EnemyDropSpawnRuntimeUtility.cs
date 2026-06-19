using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides deterministic helper functions shared by enemy reward drop spawn paths.
/// </summary>
internal static class EnemyDropSpawnRuntimeUtility
{
    #region Constants
    public const float PrecisionEpsilon = 0.0001f;
    public const int MaxSpawnStepsPerEnemy = 4096;
    private const float TwoPi = 6.283185307179586f;
    #endregion

    #region Methods

    #region Numeric Guards
    /// <summary>
    /// Checks whether a floating-point value can safely participate in drop distribution math.
    /// </summary>
    /// <param name="value">Candidate value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    public static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #region Seed
    /// <summary>
    /// Builds a deterministic non-zero seed for one drop module on one killed enemy event.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used as the stable source id.</param>
    /// <param name="killEventIndex">Index of the kill event inside the current frame snapshot.</param>
    /// <param name="moduleIndex">Module index used to decorrelate sibling drop modules.</param>
    /// <returns>Non-zero deterministic unsigned seed.</returns>
    public static uint ResolveDropTotalRandomSeed(Entity enemyEntity, int killEventIndex, int moduleIndex)
    {
        int sanitizedKillEventIndex = math.max(0, killEventIndex);
        int sanitizedModuleIndex = math.max(0, moduleIndex);
        uint seed = math.hash(new int4(enemyEntity.Index,
                                       enemyEntity.Version,
                                       sanitizedKillEventIndex + 1,
                                       sanitizedModuleIndex + 19349663));

        if (seed == 0u)
            return 1u;

        return seed;
    }
    #endregion

    #region Spawn Geometry
    /// <summary>
    /// Applies the authored vertical ground-height offset to the killed enemy position used as the pickup spawn center.
    /// </summary>
    /// <param name="deathEventPosition">Killed enemy position emitted by the death-event system.</param>
    /// <param name="groundHeightOffset">Vertical offset in world units applied before radial spread.</param>
    /// <returns>Adjusted spawn center position, or the original position when the offset is not finite.</returns>
    public static float3 ResolveDropGroundAdjustedPosition(float3 deathEventPosition, float groundHeightOffset)
    {
        if (!IsFinite(groundHeightOffset))
            return deathEventPosition;

        float3 adjustedPosition = deathEventPosition;
        adjustedPosition.y += groundHeightOffset;
        return adjustedPosition;
    }

    /// <summary>
    /// Resolves a deterministic radial spawn target around the killed enemy without allocating RNG state.
    /// </summary>
    /// <param name="centerPosition">Killed enemy position used as the spread center.</param>
    /// <param name="dropIndex">Deterministic pickup index inside the spawned sequence.</param>
    /// <param name="dropRadius">Maximum radial spread distance.</param>
    /// <returns>World-space spawn target for the pickup animation.</returns>
    public static float3 ResolveDropSpawnPosition(float3 centerPosition, int dropIndex, float dropRadius)
    {
        if (dropRadius <= PrecisionEpsilon)
            return centerPosition;

        // Low-discrepancy samples spread drops evenly without per-frame RNG state.
        int sampleIndex = dropIndex + 1;
        float radialSample = ResolveHaltonSequence(sampleIndex, 2);
        float angularSample = ResolveHaltonSequence(sampleIndex, 3);
        float radius = dropRadius * math.sqrt(math.saturate(radialSample));
        float angleRadians = TwoPi * angularSample;
        float2 offset = new float2(math.cos(angleRadians), math.sin(angleRadians)) * radius;
        float3 position = centerPosition;
        position.x += offset.x;
        position.z += offset.y;
        return position;
    }

    /// <summary>
    /// Resolves a deterministic spawn animation duration inside the module duration range.
    /// </summary>
    /// <param name="dropIndex">Deterministic pickup index inside the spawned sequence.</param>
    /// <param name="minimumDuration">Minimum animation duration in seconds.</param>
    /// <param name="maximumDuration">Maximum animation duration in seconds.</param>
    /// <returns>Interpolated animation duration.</returns>
    public static float ResolveSpawnAnimationDuration(int dropIndex, float minimumDuration, float maximumDuration)
    {
        int sampleIndex = dropIndex + 1;
        float durationSample = ResolveHaltonSequence(sampleIndex, 5);
        return math.lerp(minimumDuration, maximumDuration, durationSample);
    }

    /// <summary>
    /// Computes one Halton sequence sample for deterministic low-discrepancy drop placement.
    /// </summary>
    /// <param name="index">One-based sample index.</param>
    /// <param name="radix">Sequence radix used for the sample axis.</param>
    /// <returns>Normalized sample in the 0..1 range.</returns>
    private static float ResolveHaltonSequence(int index, int radix)
    {
        float result = 0f;
        float fraction = 1f / radix;
        int remaining = index;

        while (remaining > 0)
        {
            int digit = remaining % radix;
            result += fraction * digit;
            remaining /= radix;
            fraction /= radix;
        }

        return result;
    }
    #endregion

    #endregion
}
