using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves ECS combo topology data required only by the managed Synchro Meter presentation.
/// </summary>
internal static class HUDComboCounterRuntimePresentationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the number of enabled runtime entries used to normalize traditional rank convergence.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager owning the player rank buffer.</param>
    /// <param name="playerEntity">Player entity driving the meter.</param>
    /// <param name="currentRankIndex">Current rank index used as a safe fallback.</param>
    /// <param name="mode">Active combo topology used to exclude pre-baked alternate entries.</param>
    /// <returns>Available runtime entry count, or a positive fallback derived from the current rank.</returns>
    public static int ResolveRankCount(EntityManager runtimeEntityManager,
                                       Entity playerEntity,
                                       int currentRankIndex,
                                       PlayerComboCounterMode mode)
    {
        if (!runtimeEntityManager.HasBuffer<PlayerRuntimeComboRankElement>(playerEntity))
            return Mathf.Max(1, currentRankIndex + 1);

        DynamicBuffer<PlayerRuntimeComboRankElement> ranks = runtimeEntityManager.GetBuffer<PlayerRuntimeComboRankElement>(playerEntity, true);
        int matchingRankCount = 0;

        // Alternate topology entries coexist so scalable mode changes require no structural rebuild.
        for (int rankIndex = 0; rankIndex < ranks.Length; rankIndex++)
            if (ranks[rankIndex].Mode == mode && ranks[rankIndex].Enabled != 0)
                matchingRankCount++;

        return Mathf.Max(1, matchingRankCount);
    }
    #endregion

    #endregion
}
