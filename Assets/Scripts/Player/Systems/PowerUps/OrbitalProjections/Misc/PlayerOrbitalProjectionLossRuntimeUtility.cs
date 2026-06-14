using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Tracks permanent runtime loss for persistent health-based player orbital projections.
/// </summary>
internal static class PlayerOrbitalProjectionLossRuntimeUtility
{
    #region Constants
    private const int StolenPassiveSourceIdBase = -1000000;
    #endregion

    #region Methods

    #region Recording
    /// <summary>
    /// Records a persistent health-based projection as permanently lost on its owner player.
    /// </summary>
    /// <param name="lostProjectionLookup">Writable owner lookup for lost projection markers.</param>
    /// <param name="instance">Projection instance that has reached zero health.</param>
    /// <returns>True when a new loss marker was added.</returns>
    public static bool TryRecordPermanentLoss(ref BufferLookup<PlayerOrbitalProjectionLostElement> lostProjectionLookup,
                                              in PlayerOrbitalProjectionInstance instance)
    {
        if (!CanBePermanentlyLost(in instance))
            return false;

        if (!lostProjectionLookup.HasBuffer(instance.OwnerEntity))
            return false;

        DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections = lostProjectionLookup[instance.OwnerEntity];
        return TryAddLostProjection(lostProjections,
                                    instance.PowerUpId,
                                    instance.ProjectionIndex,
                                    instance.SourceInstanceId);
    }

    /// <summary>
    /// Adds one lost projection marker when an equivalent marker is not already present.
    /// </summary>
    /// <param name="lostProjections">Mutable lost projection marker buffer.</param>
    /// <param name="powerUpId">Source power-up identifier.</param>
    /// <param name="projectionIndex">Projection index inside the source module.</param>
    /// <param name="sourceInstanceId">Runtime source instance id for passive or toggle source identity.</param>
    /// <returns>True when a new marker was appended.</returns>
    public static bool TryAddLostProjection(DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections,
                                            FixedString64Bytes powerUpId,
                                            int projectionIndex,
                                            int sourceInstanceId)
    {
        if (!lostProjections.IsCreated)
            return false;

        if (IsProjectionLost(lostProjections, powerUpId, projectionIndex, sourceInstanceId))
            return false;

        lostProjections.Add(new PlayerOrbitalProjectionLostElement
        {
            PowerUpId = powerUpId,
            ProjectionIndex = projectionIndex,
            SourceInstanceId = sourceInstanceId
        });
        return true;
    }
    #endregion

    #region Passive Source Indexing
    /// <summary>
    /// Updates lost projection source ids after an equipped passive is removed from the player buffer.
    /// </summary>
    /// <param name="lostProjections">Mutable lost projection marker buffer.</param>
    /// <param name="removedPowerUpId">Power-up id removed by the Stealer.</param>
    /// <param name="removedIndex">Passive buffer index removed by the Stealer.</param>
    public static void ShiftAfterPassiveRemoval(DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections,
                                                FixedString64Bytes removedPowerUpId,
                                                int removedIndex)
    {
        if (!lostProjections.IsCreated)
            return;

        int stolenSourceId = EncodeStolenPassiveSourceId(removedIndex);

        for (int lostIndex = 0; lostIndex < lostProjections.Length; lostIndex++)
        {
            PlayerOrbitalProjectionLostElement lostProjection = lostProjections[lostIndex];

            if (lostProjection.SourceInstanceId == removedIndex &&
                lostProjection.PowerUpId == removedPowerUpId)
            {
                lostProjection.SourceInstanceId = stolenSourceId;
                lostProjections[lostIndex] = lostProjection;
                continue;
            }

            if (lostProjection.SourceInstanceId <= removedIndex)
                continue;

            lostProjection.SourceInstanceId -= 1;
            lostProjections[lostIndex] = lostProjection;
        }
    }

    /// <summary>
    /// Updates lost projection source ids after a stolen passive is reinserted into the player buffer.
    /// </summary>
    /// <param name="lostProjections">Mutable lost projection marker buffer.</param>
    /// <param name="restoredPowerUpId">Power-up id restored by the Stealer.</param>
    /// <param name="originalIndex">Original passive index captured when the Stealer removed the power-up.</param>
    /// <param name="restoredIndex">Actual index where the passive was reinserted.</param>
    public static void ShiftAfterPassiveRestore(DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections,
                                                FixedString64Bytes restoredPowerUpId,
                                                int originalIndex,
                                                int restoredIndex)
    {
        if (!lostProjections.IsCreated)
            return;

        int stolenSourceId = EncodeStolenPassiveSourceId(originalIndex);

        for (int lostIndex = 0; lostIndex < lostProjections.Length; lostIndex++)
        {
            PlayerOrbitalProjectionLostElement lostProjection = lostProjections[lostIndex];

            if (lostProjection.SourceInstanceId == stolenSourceId &&
                lostProjection.PowerUpId == restoredPowerUpId)
            {
                lostProjection.SourceInstanceId = restoredIndex;
                lostProjections[lostIndex] = lostProjection;
                continue;
            }

            if (lostProjection.SourceInstanceId < restoredIndex)
                continue;

            lostProjection.SourceInstanceId += 1;
            lostProjections[lostIndex] = lostProjection;
        }
    }
    #endregion

    #region Queries
    /// <summary>
    /// Checks whether one persistent projection config has already been permanently lost for a source instance.
    /// </summary>
    /// <param name="lostProjections">Lost projection marker buffer for the player.</param>
    /// <param name="powerUpId">Source power-up identifier.</param>
    /// <param name="projectionIndex">Projection index inside the source module.</param>
    /// <param name="sourceInstanceId">Runtime source instance id for passive or toggle source identity.</param>
    /// <returns>True when an equivalent permanent loss marker exists.</returns>
    public static bool IsProjectionLost(DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections,
                                        FixedString64Bytes powerUpId,
                                        int projectionIndex,
                                        int sourceInstanceId)
    {
        if (!lostProjections.IsCreated)
            return false;

        for (int lostIndex = 0; lostIndex < lostProjections.Length; lostIndex++)
        {
            PlayerOrbitalProjectionLostElement lostProjection = lostProjections[lostIndex];

            if (lostProjection.SourceInstanceId != sourceInstanceId)
                continue;

            if (lostProjection.ProjectionIndex != projectionIndex)
                continue;

            if (lostProjection.PowerUpId != powerUpId)
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a persistent source should skip a health-based projection because it was permanently lost.
    /// </summary>
    /// <param name="lostProjections">Lost projection marker buffer for the player.</param>
    /// <param name="powerUpId">Source power-up identifier.</param>
    /// <param name="sourceInstanceId">Runtime source instance id for passive or toggle source identity.</param>
    /// <param name="projectionConfig">Projection config being evaluated for spawning.</param>
    /// <returns>True when spawning should be suppressed for the lost config.</returns>
    public static bool ShouldSkipPersistentSpawn(DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections,
                                                 FixedString64Bytes powerUpId,
                                                 int sourceInstanceId,
                                                 in OrbitalProjectionConfig projectionConfig)
    {
        if (projectionConfig.HasHealth == 0)
            return false;

        return IsProjectionLost(lostProjections,
                                powerUpId,
                                projectionConfig.ProjectionIndex,
                                sourceInstanceId);
    }

    /// <summary>
    /// Checks whether a passive source still has at least one orbital projection that has not been permanently lost.
    /// </summary>
    /// <param name="passiveToolConfig">Passive tool config to inspect.</param>
    /// <param name="powerUpId">Source power-up identifier.</param>
    /// <param name="sourceInstanceId">Runtime source instance id for passive source identity.</param>
    /// <param name="lostProjections">Lost projection marker buffer for the player.</param>
    /// <returns>True when a projection remains valid for this source.</returns>
    public static bool HasAnyAvailableProjection(in PlayerPassiveToolConfig passiveToolConfig,
                                                 FixedString64Bytes powerUpId,
                                                 int sourceInstanceId,
                                                 DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections)
    {
        if (passiveToolConfig.IsDefined == 0 || passiveToolConfig.HasOrbitalProjections == 0)
            return false;

        for (int configIndex = 0; configIndex < passiveToolConfig.OrbitalProjections.Length; configIndex++)
        {
            OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[configIndex];

            if (!ShouldSkipPersistentSpawn(lostProjections,
                                           powerUpId,
                                           sourceInstanceId,
                                           in projectionConfig))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a passive tool contains non-orbital effects that remain meaningful after orbital losses.
    /// </summary>
    /// <param name="passiveToolConfig">Passive tool config to inspect.</param>
    /// <returns>True when the tool has any non-orbital runtime effect enabled.</returns>
    public static bool HasNonOrbitalPassiveEffects(in PlayerPassiveToolConfig passiveToolConfig)
    {
        if (passiveToolConfig.IsDefined == 0)
            return false;

        return passiveToolConfig.HasProjectileSize != 0 ||
               passiveToolConfig.HasShotgun != 0 ||
               passiveToolConfig.HasElementalProjectiles != 0 ||
               passiveToolConfig.HasPerfectCircle != 0 ||
               passiveToolConfig.HasBouncingProjectiles != 0 ||
               passiveToolConfig.HasSplittingProjectiles != 0 ||
               passiveToolConfig.HasExplosion != 0 ||
               passiveToolConfig.HasElementalTrail != 0 ||
               passiveToolConfig.HasHeal != 0 ||
               passiveToolConfig.HasBulletTime != 0 ||
               passiveToolConfig.HasLaserBeam != 0;
    }

    /// <summary>
    /// Checks whether an equipped passive remains a valid Stealer target after permanent orbital losses.
    /// </summary>
    /// <param name="passive">Equipped passive entry being inspected.</param>
    /// <param name="sourceInstanceId">Passive buffer index used as persistent orbital source identity.</param>
    /// <param name="lostProjections">Lost projection marker buffer for the player.</param>
    /// <returns>True when the passive still has a stealable runtime effect.</returns>
    public static bool CanStealPassive(in EquippedPassiveToolElement passive,
                                       int sourceInstanceId,
                                       DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections)
    {
        if (passive.Tool.IsDefined == 0)
            return true;

        if (HasNonOrbitalPassiveEffects(in passive.Tool))
            return true;

        if (passive.Tool.HasOrbitalProjections == 0)
            return true;

        return HasAnyAvailableProjection(in passive.Tool,
                                         passive.PowerUpId,
                                         sourceInstanceId,
                                         lostProjections);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether one projection instance is eligible for permanent loss tracking.
    /// </summary>
    /// <param name="instance">Projection instance being inspected.</param>
    /// <returns>True when the instance is persistent, health-based and depleted.</returns>
    private static bool CanBePermanentlyLost(in PlayerOrbitalProjectionInstance instance)
    {
        return instance.Persistent != 0 &&
               instance.Config.HasHealth != 0 &&
               instance.CurrentHealth <= 0f &&
               instance.PowerUpId.Length > 0;
    }

    /// <summary>
    /// Encodes a temporarily removed passive source id outside the active passive and toggle source ranges.
    /// </summary>
    /// <param name="originalIndex">Original passive buffer index captured when the Stealer removed the power-up.</param>
    /// <returns>Negative source id reserved for stolen passive tombstones.</returns>
    private static int EncodeStolenPassiveSourceId(int originalIndex)
    {
        return StolenPassiveSourceIdBase - originalIndex;
    }
    #endregion

    #endregion
}
