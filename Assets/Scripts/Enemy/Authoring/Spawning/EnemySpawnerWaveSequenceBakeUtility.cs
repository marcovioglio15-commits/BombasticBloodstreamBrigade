using System;
using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// Resolves authored explicit wave references and preceding sequence-step barriers during ECS baking.
/// </summary>
internal static class EnemySpawnerWaveSequenceBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves an optional explicit prerequisite without applying the normal sequence-step dependency.
    /// </summary>
    /// <param name="waves">Complete authored wave collection.</param>
    /// <param name="wave">Current wave definition.</param>
    /// <returns>Referenced wave index, or negative one when the authored sequence provides the dependency.</returns>
    public static int ResolveReferenceWaveIndex(IReadOnlyList<EnemySpawnWaveAuthoring> waves,
                                                EnemySpawnWaveAuthoring wave)
    {
        if (string.IsNullOrWhiteSpace(wave.ReferenceWaveId))
            return -1;

        for (int candidateIndex = 0; candidateIndex < waves.Count; candidateIndex++)
        {
            EnemySpawnWaveAuthoring candidate = waves[candidateIndex];

            if (candidate != null && string.Equals(candidate.WaveId,
                                                   wave.ReferenceWaveId,
                                                   StringComparison.OrdinalIgnoreCase))
            {
                return candidateIndex;
            }
        }

        return -1;
    }

    /// <summary>
    /// Resolves the nearest authored step preceding the current wave when no explicit wave override is assigned.
    /// </summary>
    /// <param name="waves">Complete authored wave collection.</param>
    /// <param name="wave">Current wave definition.</param>
    /// <returns>Previous authored step index, or negative one for the first step or an explicit dependency.</returns>
    public static int ResolveReferenceSequenceStepIndex(IReadOnlyList<EnemySpawnWaveAuthoring> waves,
                                                        EnemySpawnWaveAuthoring wave)
    {
        if (!string.IsNullOrWhiteSpace(wave.ReferenceWaveId))
            return -1;

        int referenceStepIndex = -1;

        for (int candidateIndex = 0; candidateIndex < waves.Count; candidateIndex++)
        {
            EnemySpawnWaveAuthoring candidate = waves[candidateIndex];

            if (candidate == null || candidate.SequenceStepIndex >= wave.SequenceStepIndex)
                continue;

            referenceStepIndex = math.max(referenceStepIndex, candidate.SequenceStepIndex);
        }

        return referenceStepIndex;
    }
    #endregion

    #endregion
}
