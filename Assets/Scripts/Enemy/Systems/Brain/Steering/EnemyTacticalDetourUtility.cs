using Unity.Mathematics;

/// <summary>
/// Preserves committed flow-field detours while applying bounded local crowd separation.
/// </summary>
internal static class EnemyTacticalDetourUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumSeparationBias = 0.08f;
    private const float MaximumSeparationBias = 0.22f;
    private const float MinimumForwardAlignment = 0.9f;
    private const float SideSignDeadZone = 0.05f;
    #endregion

    #region Methods
    /// <summary>
    /// Resolves detour velocity without allowing local separation to redirect movement into the blocked path.
    /// </summary>
    /// <param name="navigationVelocity">Flow-field velocity around the blocking obstacle.</param>
    /// <param name="separationVelocity">Local crowd-clearance velocity.</param>
    /// <param name="desiredSpeed">Maximum desired movement speed.</param>
    /// <param name="separationUrgency">Normalized local crowd pressure.</param>
    /// <returns>Detour-preserving velocity with a bounded separation bias.</returns>
    internal static float3 ResolveVelocity(float3 navigationVelocity,
                                           float3 separationVelocity,
                                           float desiredSpeed,
                                           float separationUrgency)
    {
        float navigationSpeed = math.length(navigationVelocity);

        if (navigationSpeed <= DirectionEpsilon)
            return float3.zero;

        float3 navigationDirection = navigationVelocity / navigationSpeed;
        float3 resolvedDirection = math.normalizesafe(
            navigationDirection +
            math.normalizesafe(separationVelocity, float3.zero) *
            math.lerp(MinimumSeparationBias, MaximumSeparationBias, math.saturate(separationUrgency)),
            navigationDirection);

        // Reject lateral crowd pressure that would materially compromise the selected route.
        if (math.dot(resolvedDirection, navigationDirection) < MinimumForwardAlignment)
            resolvedDirection = navigationDirection;

        return resolvedDirection * math.min(navigationSpeed, desiredSpeed);
    }

    /// <summary>
    /// Resolves stable side memory from direct-target and selected-detour directions.
    /// </summary>
    /// <param name="targetDirection">Direct direction toward the predicted player.</param>
    /// <param name="detourVelocity">Selected flow-field detour velocity.</param>
    /// <returns>Signed planar detour side, or zero when both directions are effectively collinear.</returns>
    internal static sbyte ResolveSideSign(float3 targetDirection, float3 detourVelocity)
    {
        float3 detourDirection = math.normalizesafe(detourVelocity, float3.zero);
        float cross = targetDirection.x * detourDirection.z -
                      targetDirection.z * detourDirection.x;

        if (math.abs(cross) <= SideSignDeadZone)
            return 0;

        return cross > 0f ? (sbyte)1 : (sbyte)-1;
    }
    #endregion
}
