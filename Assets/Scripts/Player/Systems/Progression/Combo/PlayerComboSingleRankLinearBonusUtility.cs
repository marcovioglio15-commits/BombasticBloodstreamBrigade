using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves rank-wide and milestone-local linear formula weights for single-rank combo progression.
/// </summary>
internal static class PlayerComboSingleRankLinearBonusUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves linear formula progress across either the complete rank or the range after its first enabled milestone.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value.</param>
    /// <param name="runtimeConfig">Current single-rank maximum and linear bonus start option.</param>
    /// <param name="runtimeRanks">Combined runtime reward-entry buffer.</param>
    /// <returns>Normalized rank-wide formula application weight in the 0..1 range.</returns>
    public static float ResolveRankProgressNormalized(int comboValue,
                                                      in PlayerRuntimeComboCounterConfig runtimeConfig,
                                                      DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (runtimeConfig.SingleRankStartLinearBonusesAtFirstMilestone == 0)
            return PlayerComboCounterRuntimeUtility.ResolveSingleRankProgressNormalized(comboValue,
                                                                                        runtimeConfig.SingleRankMaximumComboValue);

        int firstMilestoneRequiredValue = ResolveFirstEnabledMilestoneRequiredValue(runtimeRanks);

        if (firstMilestoneRequiredValue < 0 || comboValue < firstMilestoneRequiredValue)
            return 0f;

        int safeMaximum = math.max(0, runtimeConfig.SingleRankMaximumComboValue);

        if (safeMaximum <= firstMilestoneRequiredValue)
            return comboValue >= safeMaximum ? 1f : 0f;

        return math.saturate((float)(comboValue - firstMilestoneRequiredValue) /
                             (safeMaximum - firstMilestoneRequiredValue));
    }

    /// <summary>
    /// Resolves one milestone formula weight from its threshold to the next higher enabled threshold.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value.</param>
    /// <param name="milestoneIndex">Absolute runtime buffer index of the milestone whose formulas are evaluated.</param>
    /// <param name="runtimeConfig">Current single-rank progression maximum.</param>
    /// <param name="runtimeRanks">Combined runtime reward-entry buffer.</param>
    /// <returns>Normalized milestone-local formula weight in the 0..1 range.</returns>
    public static float ResolveMilestoneProgressNormalized(int comboValue,
                                                           int milestoneIndex,
                                                           in PlayerRuntimeComboCounterConfig runtimeConfig,
                                                           DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (!runtimeRanks.IsCreated || milestoneIndex < 0 || milestoneIndex >= runtimeRanks.Length)
            return 0f;

        PlayerRuntimeComboRankElement milestone = runtimeRanks[milestoneIndex];

        if (milestone.Mode != PlayerComboCounterMode.SingleRankProgression || milestone.Enabled == 0)
            return 0f;

        int startRequiredValue = math.max(0, milestone.RequiredComboValue);

        if (comboValue < startRequiredValue)
            return 0f;

        int endRequiredValue = math.max(0, runtimeConfig.SingleRankMaximumComboValue);

        // Formula scaling may reorder thresholds, so find the next effective progression boundary by value.
        for (int entryIndex = 0; entryIndex < runtimeRanks.Length; entryIndex++)
        {
            PlayerRuntimeComboRankElement candidate = runtimeRanks[entryIndex];
            int candidateRequiredValue = math.max(0, candidate.RequiredComboValue);

            if (candidate.Mode != PlayerComboCounterMode.SingleRankProgression ||
                candidate.Enabled == 0 ||
                candidateRequiredValue <= startRequiredValue ||
                candidateRequiredValue >= endRequiredValue)
                continue;

            endRequiredValue = candidateRequiredValue;
        }

        if (endRequiredValue <= startRequiredValue)
            return 1f;

        return math.saturate((float)(comboValue - startRequiredValue) /
                             (endRequiredValue - startRequiredValue));
    }

    /// <summary>
    /// Resolves the lowest enabled single-rank milestone threshold after runtime scaling.
    /// </summary>
    /// <param name="runtimeRanks">Combined runtime rank and milestone buffer.</param>
    /// <returns>Lowest enabled single-rank threshold, or -1 when no enabled milestone exists.</returns>
    public static int ResolveFirstEnabledMilestoneRequiredValue(DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (!runtimeRanks.IsCreated)
            return -1;

        int firstRequiredValue = int.MaxValue;

        // Runtime percentages may be formula-scaled, so resolve the first reached threshold rather than trusting list order.
        for (int entryIndex = 0; entryIndex < runtimeRanks.Length; entryIndex++)
        {
            PlayerRuntimeComboRankElement entry = runtimeRanks[entryIndex];

            if (entry.Mode != PlayerComboCounterMode.SingleRankProgression || entry.Enabled == 0)
                continue;

            firstRequiredValue = math.min(firstRequiredValue, math.max(0, entry.RequiredComboValue));
        }

        return firstRequiredValue == int.MaxValue ? -1 : firstRequiredValue;
    }
    #endregion

    #endregion
}
