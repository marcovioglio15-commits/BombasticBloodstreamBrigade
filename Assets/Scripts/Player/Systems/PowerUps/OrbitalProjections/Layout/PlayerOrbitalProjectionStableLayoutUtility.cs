using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Generates spawn-time stable order keys and resolves continuity-preserving shared-ring angles.
/// Independent Orbit projections use world-relative slots, while Follow Player Look projections use
/// stable-key slots driven by a continuously unwrapped look angle so rapid turns cannot trigger
/// nearest-lattice slot slips or reverse the selected follow trajectory.
/// </summary>
internal static class PlayerOrbitalProjectionStableLayoutUtility
{
    #region Constants
    private const float OrbitDistanceMatchTolerance = 0.01f;
    private const float FullCircleDegrees = 360f;
    private const float RadiansToDegrees = 180f / math.PI;
    private const float MinimumLookDirectionLengthSq = 0.0001f;
    private const int StableKeyOriginOffset = 0x40000000;
    #endregion

    #region Fields
    private static int nextStableOrderKey = StableKeyOriginOffset;
    #endregion

    #region Methods

    #region Stable Order Keys
    /// <summary>
    /// Reserves and returns the next monotonic stable order key. Keys are generated in increasing
    /// order for every new projection in the current editor / play session so that respawned
    /// projections rank after every projection currently alive on the ring.
    /// </summary>
    /// <returns>Unique stable order key for one new projection.</returns>
    public static int ReserveNextStableOrderKey()
    {
        int reservedKey = nextStableOrderKey;
        nextStableOrderKey = unchecked(nextStableOrderKey + 1);
        return reservedKey;
    }
    #endregion

    #region Look Angle
    /// <summary>
    /// Resolves the player's current look angle in the XZ plane, with +Z mapped to zero degrees.
    /// Shared between the spawn path (for slot-aligned initial angles on FollowPlayerLook) and the
    /// per-frame transform tick.
    /// </summary>
    /// <param name="lookState">Player look state used as the angle source.</param>
    /// <returns>Look angle in degrees in the [-180, 180] range.</returns>
    public static float ResolveLookAngleDegrees(in PlayerLookState lookState)
    {
        float3 direction = math.lengthsq(lookState.CurrentDirection) > MinimumLookDirectionLengthSq
            ? lookState.CurrentDirection
            : new float3(0f, 0f, 1f);
        return math.atan2(direction.x, direction.z) * RadiansToDegrees;
    }
    #endregion

    #region Spawn Slot Resolution
    /// <summary>
    /// Resolves the initial angle for a new projection so it lands precisely on its assigned ring
    /// slot. Independent Orbit slots extend the live anchor's world-relative angle; Follow Player
    /// Look slots extend the stable-key ordered look-relative layout used by the live per-frame update.
    /// Authored cone projections always start at their cone center; any non-shared motion mode
    /// falls back to the authored AngleOffsetDegrees value.
    /// </summary>
    /// <param name="projectionConfig">Projection config of the newly spawned projection.</param>
    /// <param name="newStableOrderKey">Stable order key reserved for the new projection.</param>
    /// <param name="playerEntity">Owner player entity used to scope the ring.</param>
    /// <param name="currentLookAngleDegrees">Player's current look angle in degrees (XZ plane).</param>
    /// <param name="projectionInstances">Live projection snapshot captured before this spawn pass.</param>
    /// <returns>World-space angle the new projection should start with.</returns>
    public static float ResolveInitialAngleDegrees(in OrbitalProjectionConfig projectionConfig,
                                                   int newStableOrderKey,
                                                   Entity playerEntity,
                                                   float currentLookAngleDegrees,
                                                   NativeArray<PlayerOrbitalProjectionInstance> projectionInstances)
    {
        // Cone-bounce projections live in their authored cone; the standard ring math does not apply.
        if (projectionConfig.BounceInsideOrbitCone != 0)
            return projectionConfig.OrbitConeCenterAngleDegrees;

        // ReplaceAll empties the entire ring on ECB playback so the new projection orbits alone;
        // for ReplaceMatchingPowerUp we still try to slot-align, the policy only removes overlaps.
        if (projectionConfig.AcquisitionPolicy == OrbitalProjectionAcquisitionPolicy.ReplaceAllOrbitalProjections)
            return ResolveStandaloneInitialAngle(in projectionConfig, currentLookAngleDegrees);

        // Only the two shared layout-aware modes benefit from slot-aligned initial angles.
        if (projectionConfig.MotionMode != OrbitalProjectionMotionMode.IndependentOrbit &&
            projectionConfig.MotionMode != OrbitalProjectionMotionMode.FollowPlayerLook)
            return projectionConfig.AngleOffsetDegrees;

        if (!projectionInstances.IsCreated || projectionInstances.Length <= 0)
            return ResolveStandaloneInitialAngle(in projectionConfig, currentLookAngleDegrees);

        // Collect ring composition: smallest live stable key (anchor) and member count.
        int existingMemberCount = 0;
        int anchorStableOrderKey = int.MaxValue;
        int newSlotIndex = 0;
        float anchorAngleDegrees = 0f;
        float anchorAngleOffsetDegrees = 0f;

        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsSharedRingMatch(in projectionConfig, in candidate, playerEntity))
                continue;

            existingMemberCount++;

            if (candidate.StableOrderKey < newStableOrderKey)
                newSlotIndex++;

            if (candidate.StableOrderKey >= anchorStableOrderKey)
                continue;

            anchorStableOrderKey = candidate.StableOrderKey;
            anchorAngleDegrees = candidate.AngleDegrees;
            anchorAngleOffsetDegrees = candidate.Config.AngleOffsetDegrees;
        }

        // No siblings: lone anchor, free to start at its standalone target angle.
        if (existingMemberCount <= 0)
            return ResolveStandaloneInitialAngle(in projectionConfig, currentLookAngleDegrees);

        int newTotalMemberCount = existingMemberCount + 1;
        float ringStepDegrees = FullCircleDegrees / newTotalMemberCount;

        if (projectionConfig.MotionMode == OrbitalProjectionMotionMode.FollowPlayerLook)
            return currentLookAngleDegrees + anchorAngleOffsetDegrees + ringStepDegrees * newSlotIndex;

        return anchorAngleDegrees + ringStepDegrees * newSlotIndex;
    }
    #endregion

    #region Per-Frame Layout
    /// <summary>
    /// Resolves the target angle for one live Follow Player Look projection inside its ring, when
    /// more than one projection shares the same orbit. Members retain stable-key ordered slots
    /// relative to the continuously unwrapped look target, preventing slot reassignment and nearest
    /// lattice slips while smoothing lags behind rapid player turns. Returns false for solo
    /// projections so callers keep authored single-projection behavior.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="unwrappedLookAngleDegrees">Player's continuously unwrapped look angle in degrees.</param>
    /// <param name="projectionInstances">Live projection snapshot captured before this update.</param>
    /// <param name="targetAngleDegrees">Resolved target angle for this projection's formation slot.</param>
    /// <returns>True when this projection participates in a multi-member ring layout.</returns>
    public static bool TryResolveLiveRingTargetAngle(in PlayerOrbitalProjectionInstance instance,
                                                     float unwrappedLookAngleDegrees,
                                                     NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                     out float targetAngleDegrees)
    {
        targetAngleDegrees = 0f;

        if (!projectionInstances.IsCreated || projectionInstances.Length <= 0)
            return false;

        // Resolve ring composition and the smallest stable-key member that owns the shared offset.
        int memberCount = 0;
        int anchorStableOrderKey = int.MaxValue;
        int slotIndex = 0;
        float anchorAngleOffsetDegrees = instance.Config.AngleOffsetDegrees;

        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsSharedRingMatch(in instance.Config, in candidate, instance.OwnerEntity))
                continue;

            memberCount++;

            if (candidate.StableOrderKey < instance.StableOrderKey)
                slotIndex++;

            if (candidate.StableOrderKey >= anchorStableOrderKey)
                continue;

            anchorStableOrderKey = candidate.StableOrderKey;
            anchorAngleOffsetDegrees = candidate.Config.AngleOffsetDegrees;
        }

        if (memberCount <= 1)
            return false;

        float ringStepDegrees = FullCircleDegrees / memberCount;
        targetAngleDegrees = unwrappedLookAngleDegrees + anchorAngleOffsetDegrees + ringStepDegrees * slotIndex;
        return true;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Computes the angle at which a lone projection should orbit, applying the look origin for
    /// Follow Player Look and the authored offset otherwise.
    /// </summary>
    /// <param name="projectionConfig">Projection config of the new projection.</param>
    /// <param name="currentLookAngleDegrees">Player's current look angle in degrees.</param>
    /// <returns>Initial angle to use when the projection has no ring siblings.</returns>
    private static float ResolveStandaloneInitialAngle(in OrbitalProjectionConfig projectionConfig,
                                                       float currentLookAngleDegrees)
    {
        if (projectionConfig.MotionMode == OrbitalProjectionMotionMode.FollowPlayerLook)
            return currentLookAngleDegrees + projectionConfig.AngleOffsetDegrees;

        return projectionConfig.AngleOffsetDegrees;
    }

    /// <summary>
    /// Checks whether one candidate belongs to the same shared ring (same owner, same motion mode,
    /// same orbit distance, no authored cone reservation, currently alive).
    /// </summary>
    /// <param name="projectionConfig">Projection config of the new projection.</param>
    /// <param name="candidate">Live candidate projection sampled from the snapshot.</param>
    /// <param name="playerEntity">Owner player entity used to scope the ring.</param>
    /// <returns>True when the candidate shares a ring with the new projection.</returns>
    private static bool IsSharedRingMatch(in OrbitalProjectionConfig projectionConfig,
                                          in PlayerOrbitalProjectionInstance candidate,
                                          Entity playerEntity)
    {
        if (candidate.OwnerEntity != playerEntity)
            return false;

        if (candidate.Phase == PlayerOrbitalProjectionPhase.Despawning)
            return false;

        if (candidate.Config.MotionMode != projectionConfig.MotionMode)
            return false;

        if (candidate.Config.BounceInsideOrbitCone != 0)
            return false;

        return math.abs(candidate.Config.OrbitDistance - projectionConfig.OrbitDistance) <= OrbitDistanceMatchTolerance;
    }
    #endregion

    #endregion
}
