using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves deterministic bounce sectors for independent orbital projections that share one player orbit ring.
/// </summary>
internal static class PlayerOrbitalProjectionConeLayoutUtility
{
    #region Constants
    private const float OrbitDistanceMatchTolerance = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the active bounce sector for one independent orbital projection without mutating its current angle.
    /// </summary>
    /// <param name="instance">Projection instance requesting a motion sector.</param>
    /// <param name="projectionEntity">Projection entity used as the final deterministic sort tie-breaker.</param>
    /// <param name="projectionEntities">Projection entities aligned with the instance snapshot.</param>
    /// <param name="projectionInstances">Projection instance snapshot captured before the transform update.</param>
    /// <param name="sectorStartDegrees">Inclusive unwrapped sector start in degrees.</param>
    /// <param name="sectorEndDegrees">Inclusive unwrapped sector end in degrees.</param>
    /// <returns>True when this projection should bounce inside the resolved sector this frame.</returns>
    public static bool TryResolveBounceSector(in PlayerOrbitalProjectionInstance instance,
                                              Entity projectionEntity,
                                              NativeArray<Entity> projectionEntities,
                                              NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                              out float sectorStartDegrees,
                                              out float sectorEndDegrees)
    {
        sectorStartDegrees = 0f;
        sectorEndDegrees = 0f;

        if (instance.Config.MotionMode != OrbitalProjectionMotionMode.IndependentOrbit)
            return false;

        if (instance.Config.BounceInsideOrbitCone != 0)
            return TryResolveAuthoredConeSector(in instance,
                                                projectionEntity,
                                                projectionEntities,
                                                projectionInstances,
                                                out sectorStartDegrees,
                                                out sectorEndDegrees);

        if (instance.Config.FullOrbitConeResponse != OrbitalProjectionFullOrbitConeResponse.BounceInComplementaryCone)
            return false;

        return TryResolveComplementaryConeSector(in instance,
                                                 projectionEntity,
                                                 projectionEntities,
                                                 projectionInstances,
                                                 out sectorStartDegrees,
                                                 out sectorEndDegrees);
    }

    /// <summary>
    /// Checks whether two live independent projections use the same owner and orbit-distance ring.
    /// </summary>
    /// <param name="reference">Reference projection whose ring is being resolved.</param>
    /// <param name="candidate">Candidate projection from the current frame snapshot.</param>
    /// <returns>True when the candidate participates in the same independent orbit ring.</returns>
    public static bool IsSameRingIndependentProjection(in PlayerOrbitalProjectionInstance reference,
                                                       in PlayerOrbitalProjectionInstance candidate)
    {
        if (candidate.OwnerEntity != reference.OwnerEntity)
            return false;

        if (candidate.Phase == PlayerOrbitalProjectionPhase.Despawning)
            return false;

        if (candidate.Config.MotionMode != OrbitalProjectionMotionMode.IndependentOrbit)
            return false;

        return math.abs(candidate.Config.OrbitDistance - reference.Config.OrbitDistance) <= OrbitDistanceMatchTolerance;
    }

    /// <summary>
    /// Checks whether the reference orbit ring contains at least one usable authored bounce cone.
    /// </summary>
    /// <param name="reference">Projection whose owner and orbit distance identify the ring.</param>
    /// <param name="projectionInstances">Projection instance snapshot captured before the transform update.</param>
    /// <returns>True when one live authored cone can reserve angular space on the ring.</returns>
    public static bool HasAuthoredConeOnRing(in PlayerOrbitalProjectionInstance reference,
                                             NativeArray<PlayerOrbitalProjectionInstance> projectionInstances)
    {
        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsAuthoredConeRingMatch(in reference, in candidate))
                continue;

            if (PlayerOrbitalProjectionConeAngleUtility.ResolveConeAngleDegrees(in candidate.Config) >
                PlayerOrbitalProjectionConeAngleUtility.ConeAngleEpsilon)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves a deterministic ordering key for orbital projection layout slots on the same ring.
    /// </summary>
    /// <param name="projectionEntity">Projection entity used as the final ordering tie-breaker.</param>
    /// <param name="instance">Projection instance carrying source metadata.</param>
    /// <returns>Stable integer key for one live projection.</returns>
    public static int ResolveSortKey(Entity projectionEntity, in PlayerOrbitalProjectionInstance instance)
    {
        int sourceKey = instance.SourceInstanceId + 2048;
        int projectionKey = instance.ProjectionIndex + 2048;
        return sourceKey * 1_000_000 + projectionKey * 1_000 + projectionEntity.Index;
    }
    #endregion

    #region Authored Cones
    /// <summary>
    /// Resolves an authored cone and subdivides its merged overlap group into deterministic equal sectors.
    /// </summary>
    /// <param name="instance">Authored cone projection requesting a sector.</param>
    /// <param name="projectionEntity">Projection entity used for slot ordering.</param>
    /// <param name="projectionEntities">Projection entities aligned with the instance snapshot.</param>
    /// <param name="projectionInstances">Projection instance snapshot captured before the transform update.</param>
    /// <param name="sectorStartDegrees">Inclusive unwrapped sub-cone start.</param>
    /// <param name="sectorEndDegrees">Inclusive unwrapped sub-cone end.</param>
    /// <returns>True when the authored cone is valid and a bounce sector was assigned.</returns>
    private static bool TryResolveAuthoredConeSector(in PlayerOrbitalProjectionInstance instance,
                                                    Entity projectionEntity,
                                                    NativeArray<Entity> projectionEntities,
                                                    NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                    out float sectorStartDegrees,
                                                    out float sectorEndDegrees)
    {
        sectorStartDegrees = 0f;
        sectorEndDegrees = 0f;

        if (!PlayerOrbitalProjectionConeAngleUtility.TryResolveConeInterval(in instance.Config,
                                                                            instance.Config.OrbitConeCenterAngleDegrees,
                                                                            out float mergedStartDegrees,
                                                                            out float mergedEndDegrees))
        {
            return false;
        }

        MergeOverlappingAuthoredCones(in instance,
                                      projectionInstances,
                                      ref mergedStartDegrees,
                                      ref mergedEndDegrees);

        int currentSortKey = ResolveSortKey(projectionEntity, in instance);
        int sectorCount = 0;
        int sectorSlot = 0;

        // Count every authored cone whose final interval belongs to this overlap group.
        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsAuthoredConeInMergedGroup(in instance,
                                             in candidate,
                                             mergedStartDegrees,
                                             mergedEndDegrees))
            {
                continue;
            }

            sectorCount++;

            if (ResolveSortKey(projectionEntities[candidateIndex], in candidate) < currentSortKey)
                sectorSlot++;
        }

        if (sectorCount <= 0)
            return false;

        PlayerOrbitalProjectionConeAngleUtility.ResolveSubSector(mergedStartDegrees,
                                                                 mergedEndDegrees,
                                                                 sectorCount,
                                                                 sectorSlot,
                                                                 out sectorStartDegrees,
                                                                 out sectorEndDegrees);
        return true;
    }

    /// <summary>
    /// Expands one authored cone interval until every transitively overlapping cone on the same ring is covered.
    /// </summary>
    /// <param name="reference">Reference projection whose ring and overlap group are being resolved.</param>
    /// <param name="projectionInstances">Projection instance snapshot captured before the transform update.</param>
    /// <param name="mergedStartDegrees">Mutable merged interval start in unwrapped degrees.</param>
    /// <param name="mergedEndDegrees">Mutable merged interval end in unwrapped degrees.</param>
    private static void MergeOverlappingAuthoredCones(in PlayerOrbitalProjectionInstance reference,
                                                      NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                      ref float mergedStartDegrees,
                                                      ref float mergedEndDegrees)
    {
        bool expanded = true;

        // Re-scan after each expansion so overlap chains from different power-ups merge as one occupied cone.
        for (int passIndex = 0; passIndex < projectionInstances.Length && expanded; passIndex++)
        {
            expanded = false;

            for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
            {
                PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

                if (!IsAuthoredConeRingMatch(in reference, in candidate))
                    continue;

                if (!PlayerOrbitalProjectionConeAngleUtility.TryResolveConeInterval(in candidate.Config,
                                                                                    PlayerOrbitalProjectionConeAngleUtility.ResolveIntervalCenter(mergedStartDegrees, mergedEndDegrees),
                                                                                    out float candidateStartDegrees,
                                                                                    out float candidateEndDegrees))
                {
                    continue;
                }

                if (!PlayerOrbitalProjectionConeAngleUtility.IntervalsOverlap(mergedStartDegrees,
                                                                              mergedEndDegrees,
                                                                              candidateStartDegrees,
                                                                              candidateEndDegrees))
                {
                    continue;
                }

                float nextStartDegrees = math.min(mergedStartDegrees, candidateStartDegrees);
                float nextEndDegrees = math.max(mergedEndDegrees, candidateEndDegrees);

                if (nextEndDegrees - nextStartDegrees >= PlayerOrbitalProjectionConeAngleUtility.FullCircleDegrees -
                                                               PlayerOrbitalProjectionConeAngleUtility.ConeAngleEpsilon)
                {
                    float centerDegrees = PlayerOrbitalProjectionConeAngleUtility.ResolveIntervalCenter(nextStartDegrees, nextEndDegrees);
                    mergedStartDegrees = centerDegrees - PlayerOrbitalProjectionConeAngleUtility.HalfCircleDegrees;
                    mergedEndDegrees = centerDegrees + PlayerOrbitalProjectionConeAngleUtility.HalfCircleDegrees;
                    return;
                }

                expanded = expanded ||
                           math.abs(nextStartDegrees - mergedStartDegrees) > PlayerOrbitalProjectionConeAngleUtility.IntervalOverlapTolerance ||
                           math.abs(nextEndDegrees - mergedEndDegrees) > PlayerOrbitalProjectionConeAngleUtility.IntervalOverlapTolerance;
                mergedStartDegrees = nextStartDegrees;
                mergedEndDegrees = nextEndDegrees;
            }
        }
    }

    /// <summary>
    /// Checks whether one authored cone candidate belongs to an already merged interval.
    /// </summary>
    /// <param name="reference">Reference projection that owns the ring group.</param>
    /// <param name="candidate">Candidate projection tested against the merged interval.</param>
    /// <param name="mergedStartDegrees">Merged overlap-group start in unwrapped degrees.</param>
    /// <param name="mergedEndDegrees">Merged overlap-group end in unwrapped degrees.</param>
    /// <returns>True when the candidate cone contributes a sector to the merged cone group.</returns>
    private static bool IsAuthoredConeInMergedGroup(in PlayerOrbitalProjectionInstance reference,
                                                    in PlayerOrbitalProjectionInstance candidate,
                                                    float mergedStartDegrees,
                                                    float mergedEndDegrees)
    {
        if (!IsAuthoredConeRingMatch(in reference, in candidate))
            return false;

        if (!PlayerOrbitalProjectionConeAngleUtility.TryResolveConeInterval(in candidate.Config,
                                                                            PlayerOrbitalProjectionConeAngleUtility.ResolveIntervalCenter(mergedStartDegrees, mergedEndDegrees),
                                                                            out float candidateStartDegrees,
                                                                            out float candidateEndDegrees))
        {
            return false;
        }

        return PlayerOrbitalProjectionConeAngleUtility.IntervalsOverlap(mergedStartDegrees,
                                                                        mergedEndDegrees,
                                                                        candidateStartDegrees,
                                                                        candidateEndDegrees);
    }

    /// <summary>
    /// Checks whether a candidate is an authored cone projection on the same live orbit ring.
    /// </summary>
    /// <param name="reference">Reference projection whose ring is being resolved.</param>
    /// <param name="candidate">Candidate projection from the frame snapshot.</param>
    /// <returns>True when both projections share the same authored cone layout ring.</returns>
    private static bool IsAuthoredConeRingMatch(in PlayerOrbitalProjectionInstance reference,
                                                in PlayerOrbitalProjectionInstance candidate)
    {
        return candidate.Config.BounceInsideOrbitCone != 0 &&
               IsSameRingIndependentProjection(in reference, in candidate);
    }
    #endregion

    #region Complementary Cone
    /// <summary>
    /// Resolves the largest free cone left by authored cones and subdivides it among responsive full-orbit projections.
    /// </summary>
    /// <param name="instance">Responsive full-orbit projection requesting a complementary sector.</param>
    /// <param name="projectionEntity">Projection entity used for slot ordering.</param>
    /// <param name="projectionEntities">Projection entities aligned with the instance snapshot.</param>
    /// <param name="projectionInstances">Projection instance snapshot captured before the transform update.</param>
    /// <param name="sectorStartDegrees">Inclusive unwrapped complementary sub-cone start.</param>
    /// <param name="sectorEndDegrees">Inclusive unwrapped complementary sub-cone end.</param>
    /// <returns>True when cone-bounce projections leave a complementary sector to assign.</returns>
    private static bool TryResolveComplementaryConeSector(in PlayerOrbitalProjectionInstance instance,
                                                          Entity projectionEntity,
                                                          NativeArray<Entity> projectionEntities,
                                                          NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                          out float sectorStartDegrees,
                                                          out float sectorEndDegrees)
    {
        sectorStartDegrees = 0f;
        sectorEndDegrees = 0f;

        if (!TryResolveLargestComplementaryInterval(in instance,
                                                    projectionInstances,
                                                    out float complementaryStartDegrees,
                                                    out float complementaryEndDegrees))
        {
            return false;
        }

        int currentSortKey = ResolveSortKey(projectionEntity, in instance);
        int sectorCount = 0;
        int sectorSlot = 0;

        // Responsive full-orbit projections share the same complementary cone on this ring.
        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsComplementaryConeProjection(in instance, in candidate))
                continue;

            sectorCount++;

            if (ResolveSortKey(projectionEntities[candidateIndex], in candidate) < currentSortKey)
                sectorSlot++;
        }

        if (sectorCount <= 0)
            return false;

        PlayerOrbitalProjectionConeAngleUtility.ResolveSubSector(complementaryStartDegrees,
                                                                 complementaryEndDegrees,
                                                                 sectorCount,
                                                                 sectorSlot,
                                                                 out sectorStartDegrees,
                                                                 out sectorEndDegrees);
        return true;
    }

    /// <summary>
    /// Finds the widest uncovered interval between authored cone boundaries on one orbit ring.
    /// </summary>
    /// <param name="reference">Responsive projection whose orbit ring is inspected.</param>
    /// <param name="projectionInstances">Projection instance snapshot captured before the transform update.</param>
    /// <param name="intervalStartDegrees">Start angle for the widest complementary interval.</param>
    /// <param name="intervalEndDegrees">End angle for the widest complementary interval.</param>
    /// <returns>True when authored cones occupy a strict subset of the ring.</returns>
    private static bool TryResolveLargestComplementaryInterval(in PlayerOrbitalProjectionInstance reference,
                                                               NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                               out float intervalStartDegrees,
                                                               out float intervalEndDegrees)
    {
        intervalStartDegrees = 0f;
        intervalEndDegrees = 0f;
        bool hasAuthoredCone = false;
        float bestWidthDegrees = 0f;

        // Test each authored cone end as the possible start of one free angular interval.
        for (int sourceIndex = 0; sourceIndex < projectionInstances.Length; sourceIndex++)
        {
            PlayerOrbitalProjectionInstance source = projectionInstances[sourceIndex];

            if (!IsAuthoredConeRingMatch(in reference, in source))
                continue;

            float sourceConeAngleDegrees = PlayerOrbitalProjectionConeAngleUtility.ResolveConeAngleDegrees(in source.Config);

            if (sourceConeAngleDegrees <= PlayerOrbitalProjectionConeAngleUtility.ConeAngleEpsilon)
                continue;

            hasAuthoredCone = true;

            if (sourceConeAngleDegrees >= PlayerOrbitalProjectionConeAngleUtility.FullCircleDegrees -
                                              PlayerOrbitalProjectionConeAngleUtility.ConeAngleEpsilon)
                return false;

            float freeStartDegrees = PlayerOrbitalProjectionConeAngleUtility.NormalizeAngle(source.Config.OrbitConeCenterAngleDegrees +
                                                                                            sourceConeAngleDegrees * 0.5f);
            float freeWidthDegrees = ResolveNextConeStartDistance(in reference,
                                                                  projectionInstances,
                                                                  freeStartDegrees);

            if (freeWidthDegrees <= PlayerOrbitalProjectionConeAngleUtility.ConeAngleEpsilon)
                continue;

            if (IsAngleInsideAnyAuthoredCone(in reference,
                                             projectionInstances,
                                             freeStartDegrees + freeWidthDegrees * 0.5f))
            {
                continue;
            }

            if (freeWidthDegrees <= bestWidthDegrees)
                continue;

            bestWidthDegrees = freeWidthDegrees;
            intervalStartDegrees = freeStartDegrees;
            intervalEndDegrees = freeStartDegrees + freeWidthDegrees;
        }

        return hasAuthoredCone && bestWidthDegrees > PlayerOrbitalProjectionConeAngleUtility.ConeAngleEpsilon;
    }

    /// <summary>
    /// Resolves the next authored cone start found clockwise after one authored cone end.
    /// </summary>
    /// <param name="reference">Projection whose orbit ring is inspected.</param>
    /// <param name="projectionInstances">Projection instance snapshot captured before the transform update.</param>
    /// <param name="freeStartDegrees">Clockwise search start in degrees.</param>
    /// <returns>Smallest clockwise distance to a cone start, or zero when no gap starts here.</returns>
    private static float ResolveNextConeStartDistance(in PlayerOrbitalProjectionInstance reference,
                                                      NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                      float freeStartDegrees)
    {
        float nextDistanceDegrees = PlayerOrbitalProjectionConeAngleUtility.FullCircleDegrees;

        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsAuthoredConeRingMatch(in reference, in candidate))
                continue;

            float candidateConeAngleDegrees = PlayerOrbitalProjectionConeAngleUtility.ResolveConeAngleDegrees(in candidate.Config);

            if (candidateConeAngleDegrees <= PlayerOrbitalProjectionConeAngleUtility.ConeAngleEpsilon)
                continue;

            float candidateStartDegrees = PlayerOrbitalProjectionConeAngleUtility.NormalizeAngle(candidate.Config.OrbitConeCenterAngleDegrees -
                                                                                                 candidateConeAngleDegrees * 0.5f);
            float distanceDegrees = PlayerOrbitalProjectionConeAngleUtility.ResolveClockwiseDistance(freeStartDegrees, candidateStartDegrees);

            if (distanceDegrees <= PlayerOrbitalProjectionConeAngleUtility.ConeAngleEpsilon)
                return 0f;

            nextDistanceDegrees = math.min(nextDistanceDegrees, distanceDegrees);
        }

        return nextDistanceDegrees;
    }

    /// <summary>
    /// Checks whether one angle falls inside any authored bounce cone on the reference orbit ring.
    /// </summary>
    /// <param name="reference">Projection whose orbit ring is inspected.</param>
    /// <param name="projectionInstances">Projection instance snapshot captured before the transform update.</param>
    /// <param name="angleDegrees">Angle tested against authored cone coverage.</param>
    /// <returns>True when the angle is covered by at least one authored cone.</returns>
    private static bool IsAngleInsideAnyAuthoredCone(in PlayerOrbitalProjectionInstance reference,
                                                     NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                     float angleDegrees)
    {
        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsAuthoredConeRingMatch(in reference, in candidate))
                continue;

            float halfConeDegrees = PlayerOrbitalProjectionConeAngleUtility.ResolveConeAngleDegrees(in candidate.Config) * 0.5f;

            if (halfConeDegrees <= PlayerOrbitalProjectionConeAngleUtility.ConeAngleEpsilon * 0.5f)
                continue;

            if (math.abs(PlayerOrbitalProjectionConeAngleUtility.ResolveSignedAngleDelta(candidate.Config.OrbitConeCenterAngleDegrees,
                                                                                          angleDegrees)) <= halfConeDegrees)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a candidate full-orbit projection opted into complementary-cone bounce on the same ring.
    /// </summary>
    /// <param name="reference">Reference projection whose ring is being resolved.</param>
    /// <param name="candidate">Candidate projection from the frame snapshot.</param>
    /// <returns>True when the candidate shares complementary cone slots with the reference.</returns>
    private static bool IsComplementaryConeProjection(in PlayerOrbitalProjectionInstance reference,
                                                      in PlayerOrbitalProjectionInstance candidate)
    {
        return candidate.Config.BounceInsideOrbitCone == 0 &&
               candidate.Config.FullOrbitConeResponse == OrbitalProjectionFullOrbitConeResponse.BounceInComplementaryCone &&
               IsSameRingIndependentProjection(in reference, in candidate);
    }
    #endregion

    #endregion
}
