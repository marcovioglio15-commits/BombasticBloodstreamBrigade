using Unity.Mathematics;

/// <summary>
/// Provides world-up muzzle pose helpers for managed player VFX that need intuitive authored offsets.
/// </summary>
internal static class PlayerMuzzleVfxPoseUtility
{
    #region Constants
    private static readonly float3 UpAxis = new float3(0f, 1f, 0f);
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float PlanarForwardEpsilon = 0.000001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts a raw muzzle rotation into a yaw-only rotation that preserves world-up authored offsets.
    /// </summary>
    /// <param name="sourceRotation">Raw muzzle rotation resolved from ECS or the animated visual bridge.</param>
    /// <returns>World-up rotation using the muzzle planar forward direction.</returns>
    public static quaternion ResolveWorldUpRotation(quaternion sourceRotation)
    {
        float3 forward = math.forward(sourceRotation);
        forward.y = 0f;

        if (math.lengthsq(forward) <= PlanarForwardEpsilon)
            forward = ForwardAxis;

        return quaternion.LookRotationSafe(math.normalizesafe(forward, ForwardAxis), UpAxis);
    }
    #endregion

    #endregion
}
