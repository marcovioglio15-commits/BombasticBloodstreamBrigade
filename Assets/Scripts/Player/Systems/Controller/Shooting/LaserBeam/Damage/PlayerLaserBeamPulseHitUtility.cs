using System;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Maintains per-pulse Laser Beam hit history so one storm pulse cannot damage the same enemy more than once.
/// </summary>
internal static class PlayerLaserBeamPulseHitUtility
{
    #region Nested Types
    /// <summary>
    /// Hashable key used for frame-local pulse-hit lookups.
    /// </summary>
    public readonly struct PulseHitKey : IEquatable<PulseHitKey>
    {
        public readonly int PulseId;
        public readonly Entity EnemyEntity;

        /// <summary>
        /// Creates one immutable pulse-hit key for hash-set storage.
        /// </summary>
        /// <param name="pulseId">Unique id of the storm pulse being resolved.</param>
        /// <param name="enemyEntity">Enemy entity linked to the pulse hit.</param>
        public PulseHitKey(int pulseId, Entity enemyEntity)
        {
            PulseId = pulseId;
            EnemyEntity = enemyEntity;
        }

        /// <summary>
        /// Checks whether two pulse-hit keys reference the same pulse and enemy entity.
        /// </summary>
        /// <param name="other">Key to compare with this key.</param>
        /// <returns>True when pulse id and enemy entity match.</returns>
        public bool Equals(PulseHitKey other)
        {
            return PulseId == other.PulseId &&
                   EnemyEntity == other.EnemyEntity;
        }

        /// <summary>
        /// Checks whether the provided object is an equivalent pulse-hit key.
        /// </summary>
        /// <param name="obj">Object to compare with this key.</param>
        /// <returns>True when the object is an equivalent pulse-hit key.</returns>
        public override bool Equals(object obj)
        {
            return obj is PulseHitKey other && Equals(other);
        }

        /// <summary>
        /// Builds a stable hash code from pulse id and enemy entity identity.
        /// </summary>
        /// <returns>Hash code used by NativeParallelHashSet lookups.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PulseId;
                hashCode = (hashCode * 397) ^ EnemyEntity.Index;
                hashCode = (hashCode * 397) ^ EnemyEntity.Version;
                return hashCode;
            }
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Clears all remembered pulse hits when no active storm pulses are still tracked by the beam state.
    /// </summary>
    /// <param name="pulseHits">Mutable pulse-hit buffer owned by the player.</param>
    public static void ClearPulseHits(DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits)
    {
        pulseHits.Clear();
    }

    /// <summary>
    /// Removes hit-history entries whose pulse ids are no longer active on the current beam state.
    /// </summary>
    /// <param name="pulseHits">Mutable pulse-hit buffer owned by the player.</param>
    /// <param name="stormTickPulses">Current active storm pulses owned by the player.</param>
    public static void RetainActivePulseHits(DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                                             in DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses)
    {
        if (pulseHits.Length <= 0)
            return;

        if (stormTickPulses.Length <= 0)
        {
            pulseHits.Clear();
            return;
        }

        for (int hitIndex = pulseHits.Length - 1; hitIndex >= 0; hitIndex--)
        {
            if (HasActivePulseId(pulseHits[hitIndex].PulseId, in stormTickPulses))
                continue;

            pulseHits.RemoveAt(hitIndex);
        }
    }

    /// <summary>
    /// Rebuilds a frame-local lookup from the persistent pulse-hit buffer.
    /// </summary>
    /// <param name="pulseHits">Pulse-hit buffer owned by the player.</param>
    /// <param name="pulseHitSet">Mutable frame-local lookup cleared and filled by this method.</param>
    public static void PopulatePulseHitSet(in DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                                           ref NativeParallelHashSet<PulseHitKey> pulseHitSet)
    {
        pulseHitSet.Clear();

        for (int hitIndex = 0; hitIndex < pulseHits.Length; hitIndex++)
        {
            PlayerLaserBeamPulseHitElement pulseHit = pulseHits[hitIndex];
            pulseHitSet.Add(BuildPulseHitKey(pulseHit.PulseId, pulseHit.EnemyEntity));
        }
    }

    /// <summary>
    /// Returns whether the requested enemy has already received damage from the requested storm pulse.
    /// </summary>
    /// <param name="pulseHitSet">Frame-local pulse-hit lookup built from the persistent player buffer.</param>
    /// <param name="pulseId">Unique id of the storm pulse being resolved.</param>
    /// <param name="enemyEntity">Enemy entity being considered for damage.</param>
    /// <returns>True when the enemy has already been damaged by the pulse.</returns>
    public static bool HasPulseHit(in NativeParallelHashSet<PulseHitKey> pulseHitSet,
                                   int pulseId,
                                   Entity enemyEntity)
    {
        if (pulseId <= 0 || enemyEntity == Entity.Null)
            return false;

        return pulseHitSet.Contains(BuildPulseHitKey(pulseId, enemyEntity));
    }

    /// <summary>
    /// Records that the requested enemy has received damage from the requested storm pulse.
    /// </summary>
    /// <param name="pulseHits">Mutable pulse-hit buffer owned by the player.</param>
    /// <param name="pulseHitSet">Mutable frame-local lookup updated together with the persistent buffer.</param>
    /// <param name="pulseId">Unique id of the storm pulse being resolved.</param>
    /// <param name="enemyEntity">Enemy entity that has just received pulse damage.</param>
    public static void RegisterPulseHit(DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                                        ref NativeParallelHashSet<PulseHitKey> pulseHitSet,
                                        int pulseId,
                                        Entity enemyEntity)
    {
        if (pulseId <= 0 || enemyEntity == Entity.Null)
            return;

        PulseHitKey pulseHitKey = BuildPulseHitKey(pulseId, enemyEntity);

        if (!pulseHitSet.Add(pulseHitKey))
            return;

        pulseHits.Add(new PlayerLaserBeamPulseHitElement
        {
            PulseId = pulseId,
            EnemyEntity = enemyEntity
        });
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the hashable key used by frame-local pulse-hit lookups.
    /// </summary>
    /// <param name="pulseId">Unique id of the storm pulse being resolved.</param>
    /// <param name="enemyEntity">Enemy entity being considered for damage.</param>
    /// <returns>Hashable pulse-hit key.</returns>
    private static PulseHitKey BuildPulseHitKey(int pulseId,
                                                Entity enemyEntity)
    {
        return new PulseHitKey(pulseId, enemyEntity);
    }

    /// <summary>
    /// Checks whether one pulse id is still present in the active pulse list.
    /// </summary>
    /// <param name="pulseId">Pulse id to find.</param>
    /// <param name="stormTickPulses">Current active storm pulses owned by the player.</param>
    /// <returns>True when the pulse id is still active.</returns>
    private static bool HasActivePulseId(int pulseId,
                                         in DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses)
    {
        if (pulseId <= 0)
            return false;

        for (int pulseIndex = 0; pulseIndex < stormTickPulses.Length; pulseIndex++)
        {
            if (stormTickPulses[pulseIndex].PulseId == pulseId)
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
