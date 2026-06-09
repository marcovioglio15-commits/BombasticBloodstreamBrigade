using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Generates spawn-time stable order keys and resolves continuity-preserving initial angles for new
/// orbital projections. The monotonic key ensures fresh projections always take the highest slot in
/// the ring, so existing siblings keep their angular spacing while the new one snaps directly into
/// the matching ring offset (no per-frame catch-up swing after Stealer reacquisitions). Independent
/// Orbit projections sit at world-relative slots; Follow Player Look projections sit on a slot
/// lattice pinned to the ring anchor's current angle, so the formation follows the look without
/// ever reshuffling member slots.
/// </summary>
internal static class PlayerOrbitalProjectionStableLayoutUtility
{
    #region Constants
    private const float OrbitDistanceMatchTolerance = 0.01f;
    private const float FullCircleDegrees = 360f;
    private const float RadiansToDegrees = 180f / math.PI;
    private const float MinimumLookDirectionLengthSq = 0.0001f;
    private const float AngleRankEpsilonDegrees = 0.01f;
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
    /// Look slots extend the anchor-pinned look-relative lattice used by the live per-frame layout.
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
        float anchorAngleDegrees = 0f;
        float anchorAngleOffsetDegrees = 0f;

        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsSharedRingMatch(in projectionConfig, in candidate, playerEntity))
                continue;

            existingMemberCount++;

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

        // Monotonic keys guarantee the new projection ranks last, but we still resolve its exact
        // slot in case keys ever come back out of order (e.g. future migrations).
        int newSlotIndex = newStableOrderKey < anchorStableOrderKey
            ? 0
            : ResolveSlotIndexAfterAnchor(newStableOrderKey,
                                          anchorStableOrderKey,
                                          projectionInstances,
                                          in projectionConfig,
                                          playerEntity);

        if (projectionConfig.MotionMode == OrbitalProjectionMotionMode.FollowPlayerLook)
        {
            // Pin the new, denser lattice to the anchor's current angle (same rule as the live
            // per-frame layout) and append the new projection one step past the last circular
            // slot, so the spawn lands exactly where the live layout will keep it.
            float latticeBaseDegrees = currentLookAngleDegrees + anchorAngleOffsetDegrees;
            float anchorTargetDegrees = latticeBaseDegrees + math.round((anchorAngleDegrees - latticeBaseDegrees) / ringStepDegrees) * ringStepDegrees;
            return anchorTargetDegrees + ringStepDegrees * existingMemberCount;
        }

        return anchorAngleDegrees + ringStepDegrees * newSlotIndex;
    }
    #endregion

    #region Per-Frame Layout
    /// <summary>
    /// Resolves the target angle for one live Follow Player Look projection inside its ring, when
    /// more than one projection shares the same orbit. The slot lattice is pinned to the ring
    /// anchor's CURRENT angle (snapped to the nearest look-relative slot), and members are ranked
    /// by circular order around that anchor with a stable-key tie-break. Pinning the lattice to
    /// the formation itself is essential: a look-anchored rank cut sweeps across the smoothing-lagged
    /// members while the player spins, reshuffling slots and visibly swapping projections. With the
    /// formation-pinned cut, look rotations can only shift every member together, and a rotation
    /// equal to a multiple of the slot step leaves a symmetric ring exactly in place. Returns false
    /// for solo projections so callers keep authored single-projection behavior.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="currentLookAngleDegrees">Player's current look angle in degrees.</param>
    /// <param name="projectionInstances">Live projection snapshot captured before this update.</param>
    /// <param name="targetAngleDegrees">Resolved target angle for this projection's formation slot.</param>
    /// <returns>True when this projection participates in a multi-member ring layout.</returns>
    public static bool TryResolveLiveRingTargetAngle(in PlayerOrbitalProjectionInstance instance,
                                                     float currentLookAngleDegrees,
                                                     NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                     out float targetAngleDegrees)
    {
        targetAngleDegrees = 0f;

        if (!projectionInstances.IsCreated || projectionInstances.Length <= 0)
            return false;

        // Resolve ring composition: member count plus the anchor (smallest stable key) whose
        // current angle pins the formation to the look-relative slot lattice.
        int memberCount = 0;
        int anchorStableOrderKey = int.MaxValue;
        float anchorAngleDegrees = instance.AngleDegrees;
        float anchorAngleOffsetDegrees = instance.Config.AngleOffsetDegrees;

        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsSharedRingMatch(in instance.Config, in candidate, instance.OwnerEntity))
                continue;

            memberCount++;

            if (candidate.StableOrderKey >= anchorStableOrderKey)
                continue;

            anchorStableOrderKey = candidate.StableOrderKey;
            anchorAngleDegrees = candidate.AngleDegrees;
            anchorAngleOffsetDegrees = candidate.Config.AngleOffsetDegrees;
        }

        if (memberCount <= 1)
            return false;

        // Snap the anchor to its nearest look-relative slot: this single shared shift moves the
        // whole formation together, so member-vs-member slot assignments can never reshuffle while
        // the player spins (worst case under extreme spin speed is a uniform one-slot slip).
        float ringStepDegrees = FullCircleDegrees / memberCount;
        float latticeBaseDegrees = currentLookAngleDegrees + anchorAngleOffsetDegrees;
        float anchorTargetDegrees = latticeBaseDegrees + math.round((anchorAngleDegrees - latticeBaseDegrees) / ringStepDegrees) * ringStepDegrees;

        // Rank this projection by circular order around the anchor projection (stable-key tie-break):
        // the rank cut sits on the anchor itself and travels with the formation, never across it,
        // and the rank is a guaranteed bijection over 0..memberCount-1 so two projections can never
        // collapse onto the same slot during membership transitions.
        float selfRelativeDegrees = NormalizeAngle360(instance.AngleDegrees - anchorAngleDegrees);
        int slotIndex = 0;

        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsSharedRingMatch(in instance.Config, in candidate, instance.OwnerEntity))
                continue;

            if (candidate.StableOrderKey == instance.StableOrderKey)
                continue;

            float candidateRelativeDegrees = NormalizeAngle360(candidate.AngleDegrees - anchorAngleDegrees);
            float relativeDeltaDegrees = candidateRelativeDegrees - selfRelativeDegrees;

            // Members earlier in the circular order (or tied but with a smaller stable key) take a
            // lower slot.
            if (relativeDeltaDegrees < -AngleRankEpsilonDegrees)
                slotIndex++;
            else if (relativeDeltaDegrees <= AngleRankEpsilonDegrees && candidate.StableOrderKey < instance.StableOrderKey)
                slotIndex++;
        }

        targetAngleDegrees = anchorTargetDegrees + ringStepDegrees * slotIndex;
        return true;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Normalizes one angle into the [0, 360) range for circular ranking.
    /// </summary>
    /// <param name="angleDegrees">Angle to normalize.</param>
    /// <returns>Equivalent angle in the [0, 360) range.</returns>
    private static float NormalizeAngle360(float angleDegrees)
    {
        float normalizedDegrees = math.fmod(angleDegrees, FullCircleDegrees);

        if (normalizedDegrees < 0f)
            normalizedDegrees += FullCircleDegrees;

        return normalizedDegrees;
    }

    /// <summary>
    /// Computes the angle at which a lone (or first) projection should orbit, applying the look
    /// origin for FollowPlayerLook and the authored offset otherwise.
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
    /// Resolves the new projection's slot index inside the post-spawn sorted ring, counting how
    /// many existing siblings have a smaller stable key (excluding the anchor, since anchor is
    /// slot zero).
    /// </summary>
    /// <param name="newStableOrderKey">Stable key reserved for the new projection.</param>
    /// <param name="anchorStableOrderKey">Stable key of the existing ring anchor.</param>
    /// <param name="projectionInstances">Live projection snapshot captured before this spawn pass.</param>
    /// <param name="projectionConfig">Projection config of the new projection.</param>
    /// <param name="playerEntity">Owner player entity used to scope the ring.</param>
    /// <returns>Slot index inside the post-spawn ring, with anchor at slot zero.</returns>
    private static int ResolveSlotIndexAfterAnchor(int newStableOrderKey,
                                                   int anchorStableOrderKey,
                                                   NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                   in OrbitalProjectionConfig projectionConfig,
                                                   Entity playerEntity)
    {
        int slotIndex = 1;

        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsSharedRingMatch(in projectionConfig, in candidate, playerEntity))
                continue;

            if (candidate.StableOrderKey == anchorStableOrderKey)
                continue;

            if (candidate.StableOrderKey < newStableOrderKey)
                slotIndex++;
        }

        return slotIndex;
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
