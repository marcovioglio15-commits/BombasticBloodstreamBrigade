using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes combo-rank resolution, time-based decay, HUD presentation data, and runtime-scaling signatures.
/// </summary>
internal static class PlayerComboCounterRuntimeUtility
{
    #region Constants
    private const float MinimumRemainingDecayTime = 0.0001f;
    private const float MaximumStoredDecayCarry = 0.9999f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the highest currently active combo rank from the current runtime combo value.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value.</param>
    /// <param name="runtimeConfig">Current runtime combo config.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds.</param>
    /// <returns>Highest active rank index, or -1 when no rank is active.</returns>
    public static int ResolveActiveRankIndex(int comboValue,
                                             in PlayerRuntimeComboCounterConfig runtimeConfig,
                                             DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (runtimeConfig.Enabled == 0)
        {
            return -1;
        }

        if (!runtimeRanks.IsCreated || runtimeRanks.Length <= 0)
        {
            return -1;
        }

        int sanitizedComboValue = math.max(0, comboValue);
        int activeRankIndex = -1;

        for (int rankIndex = 0; rankIndex < runtimeRanks.Length; rankIndex++)
        {
            PlayerRuntimeComboRankElement rankElement = runtimeRanks[rankIndex];

            if (rankElement.Mode != runtimeConfig.Mode || rankElement.Enabled == 0)
                continue;

            if (sanitizedComboValue < math.max(0, rankElement.RequiredComboValue))
                continue;

            activeRankIndex = rankIndex;
        }

        return activeRankIndex;
    }

    /// <summary>
    /// Updates cached combo HUD data from the latest runtime combo config, thresholds, and combo value.
    /// </summary>
    /// <param name="comboCounterState">Mutable combo runtime state receiving presentation fields.</param>
    /// <param name="runtimeConfig">Current runtime combo config.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds.</param>
    public static void UpdatePresentation(ref PlayerComboCounterState comboCounterState,
                                          in PlayerRuntimeComboCounterConfig runtimeConfig,
                                          DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        int sanitizedComboValue = math.max(0, comboCounterState.CurrentValue);
        comboCounterState.CurrentValue = sanitizedComboValue;
        comboCounterState.CurrentRankIndex = -1;
        comboCounterState.CurrentRankId = default;
        comboCounterState.CurrentRankRequiredValue = 0;
        comboCounterState.NextRankRequiredValue = 0;
        comboCounterState.ProgressNormalized = 0f;

        if (runtimeConfig.Enabled == 0)
        {
            return;
        }

        if (runtimeConfig.Mode == PlayerComboCounterMode.SingleRankProgression)
        {
            comboCounterState.CurrentValue = math.min(sanitizedComboValue, math.max(0, runtimeConfig.SingleRankMaximumComboValue));
            comboCounterState.CurrentRankIndex = comboCounterState.CurrentValue > 0 ? 0 : -1;
            comboCounterState.CurrentRankId = runtimeConfig.SingleRankId;
            comboCounterState.CurrentRankRequiredValue = 0;
            comboCounterState.NextRankRequiredValue = math.max(0, runtimeConfig.SingleRankMaximumComboValue);
            comboCounterState.ProgressNormalized = ResolveSingleRankProgressNormalized(comboCounterState.CurrentValue,
                                                                                       runtimeConfig.SingleRankMaximumComboValue);
            return;
        }

        if (!runtimeRanks.IsCreated || runtimeRanks.Length <= 0)
            return;

        int activeRankIndex = ResolveActiveRankIndex(sanitizedComboValue, in runtimeConfig, runtimeRanks);
        int nextRankIndex = ResolveNextRankIndex(sanitizedComboValue, runtimeConfig.Mode, runtimeRanks);

        if (activeRankIndex >= 0)
        {
            PlayerRuntimeComboRankElement activeRank = runtimeRanks[activeRankIndex];
            comboCounterState.CurrentRankIndex = activeRankIndex;
            comboCounterState.CurrentRankId = activeRank.RankId;
            comboCounterState.CurrentRankRequiredValue = math.max(0, activeRank.RequiredComboValue);
        }

        if (nextRankIndex >= 0)
        {
            comboCounterState.NextRankRequiredValue = math.max(0, runtimeRanks[nextRankIndex].RequiredComboValue);
        }

        comboCounterState.ProgressNormalized = ResolveProgressNormalized(sanitizedComboValue,
                                                                        comboCounterState.CurrentRankRequiredValue,
                                                                        comboCounterState.NextRankRequiredValue,
                                                                        activeRankIndex >= 0);
    }

    /// <summary>
    /// Resolves the combo value that should remain after a damage event breaks the current combo.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value before the break.</param>
    /// <param name="runtimeConfig">Current runtime combo config.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds.</param>
    /// <returns>Combo value preserved after the configured damage-break behavior.</returns>
    public static int ResolveDamageBreakComboValue(int comboValue,
                                                   in PlayerRuntimeComboCounterConfig runtimeConfig,
                                                   DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        switch (runtimeConfig.DamageBreakMode)
        {
            case PlayerComboDamageBreakMode.DowngradeToPreviousRank:
                return ResolvePreviousRankRequiredValue(ResolveActiveRankIndex(comboValue, in runtimeConfig, runtimeRanks),
                                                        runtimeConfig.Mode,
                                                        runtimeRanks);
            default:
                return 0;
        }
    }

    /// <summary>
    /// Applies point decay over time using the currently active combo rank and keeps fractional loss in the combo state carry.
    /// </summary>
    /// <param name="comboCounterState">Mutable combo runtime state receiving the updated combo value and fractional decay carry.</param>
    /// <param name="runtimeConfig">Current runtime combo config.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds and decay rates.</param>
    /// <param name="deltaTime">Frame delta time in seconds.</param>
    public static void ApplyRankDecay(ref PlayerComboCounterState comboCounterState,
                                      in PlayerRuntimeComboCounterConfig runtimeConfig,
                                      DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                      float deltaTime)
    {
        float safeDeltaTime = math.max(0f, deltaTime);

        if (safeDeltaTime <= 0f)
        {
            return;
        }

        if (runtimeConfig.Enabled == 0)
        {
            comboCounterState.DecayPointsCarry = 0f;
            return;
        }

        if (runtimeConfig.Mode == PlayerComboCounterMode.SingleRankProgression)
        {
            ApplySingleRankDecay(ref comboCounterState, in runtimeConfig, safeDeltaTime);
            return;
        }

        if (!runtimeRanks.IsCreated || runtimeRanks.Length <= 0)
        {
            comboCounterState.DecayPointsCarry = 0f;
            return;
        }

        int currentComboValue = math.max(0, comboCounterState.CurrentValue);

        if (currentComboValue <= 0)
        {
            comboCounterState.CurrentValue = 0;
            comboCounterState.DecayPointsCarry = 0f;
            return;
        }

        float remainingDeltaTime = safeDeltaTime;
        float decayPointsCarry = math.clamp(comboCounterState.DecayPointsCarry, 0f, MaximumStoredDecayCarry);

        while (remainingDeltaTime > MinimumRemainingDecayTime && currentComboValue > 0)
        {
            int activeRankIndex = ResolveActiveRankIndex(currentComboValue, in runtimeConfig, runtimeRanks);

            if (activeRankIndex < 0)
            {
                decayPointsCarry = 0f;
                break;
            }

            float pointsDecayPerSecond = math.max(0f, runtimeRanks[activeRankIndex].PointsDecayPerSecond);

            if (pointsDecayPerSecond <= 0f)
            {
                decayPointsCarry = 0f;
                break;
            }

            int pointsToLeaveRank = ResolvePointsToLeaveCurrentRank(currentComboValue,
                                                                    activeRankIndex,
                                                                    in runtimeConfig,
                                                                    runtimeRanks);

            if (pointsToLeaveRank <= 0)
            {
                decayPointsCarry = 0f;
                break;
            }

            float totalDecayPoints = decayPointsCarry + pointsDecayPerSecond * remainingDeltaTime;
            int wholeDecayPoints = totalDecayPoints >= int.MaxValue
                ? int.MaxValue
                : (int)math.floor(totalDecayPoints);

            if (wholeDecayPoints < pointsToLeaveRank)
            {
                if (wholeDecayPoints > 0)
                {
                    currentComboValue = math.max(0, currentComboValue - wholeDecayPoints);
                }

                decayPointsCarry = totalDecayPoints - wholeDecayPoints;
                remainingDeltaTime = 0f;
                break;
            }

            float decayPointsNeeded = math.max(0f, pointsToLeaveRank - decayPointsCarry);
            float timeToLeaveRank = decayPointsNeeded / pointsDecayPerSecond;

            if (timeToLeaveRank > remainingDeltaTime)
            {
                timeToLeaveRank = remainingDeltaTime;
            }

            currentComboValue = math.max(0, currentComboValue - pointsToLeaveRank);
            remainingDeltaTime = math.max(0f, remainingDeltaTime - timeToLeaveRank);
            decayPointsCarry = 0f;
        }

        comboCounterState.CurrentValue = currentComboValue;
        comboCounterState.DecayPointsCarry = currentComboValue > 0 ? decayPointsCarry : 0f;
    }

    /// <summary>
    /// Combines the permanent scalable-stat signature with the currently active combo-rank signature used by runtime bonuses.
    /// </summary>
    /// <param name="scalableStatsHash">Hash built from permanent scalable stats.</param>
    /// <param name="activeRankIndex">Currently active combo-rank index, or -1 when no combo bonus is active.</param>
    /// <returns>Combined runtime-scaling signature.</returns>
    public static uint ComputeRuntimeScalingHash(uint scalableStatsHash, int activeRankIndex)
    {
        uint sanitizedActiveRankSignature = (uint)(math.max(-1, activeRankIndex) + 1);
        return math.hash(new uint2(scalableStatsHash, sanitizedActiveRankSignature));
    }

    /// <summary>
    /// Combines the permanent scalable-stat signature with combo rank and progressive boost progress signatures.
    /// </summary>
    /// <param name="scalableStatsHash">Hash built from permanent scalable stats.</param>
    /// <param name="activeRankIndex">Currently active combo-rank index, or -1 when no combo bonus is active.</param>
    /// <param name="comboValue">Current combo value used only when the next rank exposes progressive boost.</param>
    /// <param name="runtimeConfig">Current combo mode and single-rank formula distribution settings.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds and progressive boost settings.</param>
    /// <returns>Combined runtime-scaling signature.</returns>
    public static uint ComputeRuntimeScalingHash(uint scalableStatsHash,
                                                 int activeRankIndex,
                                                 int comboValue,
                                                 in PlayerRuntimeComboCounterConfig runtimeConfig,
                                                 DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        uint baseHash = ComputeRuntimeScalingHash(scalableStatsHash, activeRankIndex);

        if (runtimeConfig.Mode == PlayerComboCounterMode.SingleRankProgression &&
            runtimeConfig.SingleRankFormulaDistributionMode == PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression)
        {
            return math.hash(new uint2(baseHash, (uint)math.max(0, comboValue)));
        }

        int nextRankIndex = ResolveNextEntryIndex(activeRankIndex, runtimeConfig.Mode, runtimeRanks);

        if (!runtimeRanks.IsCreated || nextRankIndex < 0 || nextRankIndex >= runtimeRanks.Length)
        {
            return baseHash;
        }

        if (runtimeRanks[nextRankIndex].ProgressiveBoostPercent <= 0f)
        {
            return baseHash;
        }

        uint progressSignature = (uint)math.max(0, comboValue);
        return math.hash(new uint2(baseHash, progressSignature));
    }

    /// <summary>
    /// Resolves the integer combo threshold represented by one authored single-rank percentage milestone.
    /// </summary>
    /// <param name="maximumComboValue">Current single-rank progression maximum.</param>
    /// <param name="requiredProgressPercent">Authored percentage required by the milestone.</param>
    /// <returns>Safe integer threshold within the current single-rank progression range.</returns>
    public static int ResolveSingleRankMilestoneRequiredValue(int maximumComboValue, float requiredProgressPercent)
    {
        int safeMaximum = math.max(0, maximumComboValue);
        float safePercent = math.clamp(math.isfinite(requiredProgressPercent) ? requiredProgressPercent : 0f, 0f, 100f);
        return (int)math.round(safeMaximum * safePercent * 0.01f);
    }

    /// <summary>
    /// Resolves continuous progress across the complete single-rank range.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value.</param>
    /// <param name="maximumComboValue">Combo value that completes the progression.</param>
    /// <returns>Normalized single-rank progress in the 0..1 range.</returns>
    public static float ResolveSingleRankProgressNormalized(int comboValue, int maximumComboValue)
    {
        int safeMaximum = math.max(0, maximumComboValue);

        if (safeMaximum <= 0)
            return 0f;

        return math.saturate((float)math.max(0, comboValue) / safeMaximum);
    }

    /// <summary>
    /// Counts enabled reward entries belonging to one combo topology.
    /// </summary>
    /// <param name="mode">Combo topology whose entries should be counted.</param>
    /// <param name="runtimeRanks">Combined runtime rank and milestone buffer.</param>
    /// <returns>Number of enabled entries owned by the requested topology.</returns>
    public static int CountEnabledEntries(PlayerComboCounterMode mode,
                                          DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (!runtimeRanks.IsCreated)
            return 0;

        int count = 0;

        for (int entryIndex = 0; entryIndex < runtimeRanks.Length; entryIndex++)
        {
            PlayerRuntimeComboRankElement entry = runtimeRanks[entryIndex];

            if (entry.Mode == mode && entry.Enabled != 0)
                count += 1;
        }

        return count;
    }

    /// <summary>
    /// Resolves normalized progress from the previous rank threshold toward the requested target rank.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value.</param>
    /// <param name="activeRankIndex">Currently active combo-rank index, or -1 before the first rank.</param>
    /// <param name="targetRankIndex">Rank whose bonuses are being progressively approached.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds.</param>
    /// <returns>Normalized progress in the 0..1 range.</returns>
    public static float ResolveProgressToRank(int comboValue,
                                              int activeRankIndex,
                                              int targetRankIndex,
                                              DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (!runtimeRanks.IsCreated || targetRankIndex < 0 || targetRankIndex >= runtimeRanks.Length)
        {
            return 0f;
        }

        int sanitizedComboValue = math.max(0, comboValue);
        int startRequiredValue = 0;

        if (activeRankIndex >= 0 && activeRankIndex < runtimeRanks.Length)
        {
            startRequiredValue = math.max(0, runtimeRanks[activeRankIndex].RequiredComboValue);
        }

        int targetRequiredValue = math.max(0, runtimeRanks[targetRankIndex].RequiredComboValue);
        int range = targetRequiredValue - startRequiredValue;

        if (range <= 0)
        {
            return sanitizedComboValue >= targetRequiredValue ? 1f : 0f;
        }

        return math.saturate((float)(sanitizedComboValue - startRequiredValue) / range);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the next unreached rank threshold after the current combo value.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds.</param>
    /// <returns>Next unreached rank index, or -1 when the top rank is already active.</returns>
    private static int ResolveNextRankIndex(int comboValue,
                                            PlayerComboCounterMode mode,
                                            DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        int sanitizedComboValue = math.max(0, comboValue);

        for (int rankIndex = 0; rankIndex < runtimeRanks.Length; rankIndex++)
        {
            PlayerRuntimeComboRankElement rank = runtimeRanks[rankIndex];

            if (rank.Mode != mode || rank.Enabled == 0)
                continue;

            if (sanitizedComboValue >= math.max(0, rank.RequiredComboValue))
                continue;

            return rankIndex;
        }

        return -1;
    }

    /// <summary>
    /// Resolves the threshold that should remain when damage downgrades the combo to the previous reached rank.
    /// </summary>
    /// <param name="activeRankIndex">Highest rank currently reached before the break.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds.</param>
    /// <returns>Previous-rank threshold, or zero when no lower rank exists.</returns>
    private static int ResolvePreviousRankRequiredValue(int activeRankIndex,
                                                        PlayerComboCounterMode mode,
                                                        DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (!runtimeRanks.IsCreated || activeRankIndex <= 0)
            return 0;

        for (int previousRankIndex = math.min(activeRankIndex - 1, runtimeRanks.Length - 1);
             previousRankIndex >= 0;
             previousRankIndex--)
        {
            PlayerRuntimeComboRankElement previousRank = runtimeRanks[previousRankIndex];

            if (previousRank.Mode == mode && previousRank.Enabled != 0)
                return math.max(0, previousRank.RequiredComboValue);
        }

        return 0;
    }

    /// <summary>
    /// Resolves the next enabled buffer entry belonging to the requested combo topology.
    /// </summary>
    /// <param name="activeEntryIndex">Current absolute buffer index, or -1 before the first entry.</param>
    /// <param name="mode">Combo topology whose next entry should be resolved.</param>
    /// <param name="runtimeRanks">Combined runtime rank and milestone buffer.</param>
    /// <returns>Next absolute buffer index, or -1 when no later matching entry exists.</returns>
    private static int ResolveNextEntryIndex(int activeEntryIndex,
                                             PlayerComboCounterMode mode,
                                             DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (!runtimeRanks.IsCreated)
            return -1;

        for (int entryIndex = math.max(0, activeEntryIndex + 1); entryIndex < runtimeRanks.Length; entryIndex++)
        {
            PlayerRuntimeComboRankElement entry = runtimeRanks[entryIndex];

            if (entry.Mode == mode && entry.Enabled != 0)
                return entryIndex;
        }

        return -1;
    }

    /// <summary>
    /// Resolves how many integer combo points must be lost before the currently active rank stops being active.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value.</param>
    /// <param name="activeRankIndex">Highest rank currently active before the decay step.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds.</param>
    /// <returns>Integer point loss required to leave the current rank.</returns>
    private static int ResolvePointsToLeaveCurrentRank(int comboValue,
                                                       int activeRankIndex,
                                                       in PlayerRuntimeComboCounterConfig runtimeConfig,
                                                       DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (!runtimeRanks.IsCreated || activeRankIndex < 0 || activeRankIndex >= runtimeRanks.Length)
        {
            return 0;
        }

        int sanitizedComboValue = math.max(0, comboValue);
        int currentRankRequiredValue = math.max(0, runtimeRanks[activeRankIndex].RequiredComboValue);

        if (sanitizedComboValue < currentRankRequiredValue)
        {
            return 0;
        }

        if (ShouldPreserveCurrentRankFloor(activeRankIndex, in runtimeConfig, runtimeRanks))
        {
            return sanitizedComboValue - currentRankRequiredValue;
        }

        return sanitizedComboValue - currentRankRequiredValue + 1;
    }

    /// <summary>
    /// Resolves whether decay should stop at the current rank threshold because the lower rank has no point decay.
    /// </summary>
    /// <param name="activeRankIndex">Current active rank index.</param>
    /// <param name="runtimeConfig">Current runtime combo config.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank thresholds and decay rates.</param>
    /// <returns>True when decay must preserve the current rank threshold.</returns>
    private static bool ShouldPreserveCurrentRankFloor(int activeRankIndex,
                                                       in PlayerRuntimeComboCounterConfig runtimeConfig,
                                                       DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (runtimeConfig.PreventDecayIntoNonDecayingRanks == 0)
        {
            return false;
        }

        if (!runtimeRanks.IsCreated || activeRankIndex <= 0 || activeRankIndex >= runtimeRanks.Length)
            return false;

        for (int previousRankIndex = activeRankIndex - 1; previousRankIndex >= 0; previousRankIndex--)
        {
            PlayerRuntimeComboRankElement previousRank = runtimeRanks[previousRankIndex];

            if (previousRank.Mode != runtimeConfig.Mode || previousRank.Enabled == 0)
                continue;

            return previousRank.PointsDecayPerSecond <= 0f;
        }

        return false;
    }

    /// <summary>
    /// Applies continuous single-rank point decay while preserving fractional loss between simulation ticks.
    /// </summary>
    /// <param name="comboCounterState">Mutable combo state receiving the decayed value and fractional carry.</param>
    /// <param name="runtimeConfig">Current single-rank maximum and decay rate.</param>
    /// <param name="deltaTime">Safe positive simulation delta time.</param>
    private static void ApplySingleRankDecay(ref PlayerComboCounterState comboCounterState,
                                             in PlayerRuntimeComboCounterConfig runtimeConfig,
                                             float deltaTime)
    {
        int currentValue = math.min(math.max(0, comboCounterState.CurrentValue),
                                    math.max(0, runtimeConfig.SingleRankMaximumComboValue));
        float pointsDecayPerSecond = math.max(0f, runtimeConfig.SingleRankPointsDecayPerSecond);

        if (currentValue <= 0 || pointsDecayPerSecond <= 0f)
        {
            comboCounterState.CurrentValue = currentValue;
            comboCounterState.DecayPointsCarry = 0f;
            return;
        }

        float totalDecayPoints = math.clamp(comboCounterState.DecayPointsCarry, 0f, MaximumStoredDecayCarry) +
                                 pointsDecayPerSecond * deltaTime;
        int wholeDecayPoints = totalDecayPoints >= int.MaxValue
            ? int.MaxValue
            : (int)math.floor(totalDecayPoints);
        comboCounterState.CurrentValue = math.max(0, currentValue - wholeDecayPoints);
        comboCounterState.DecayPointsCarry = comboCounterState.CurrentValue > 0
            ? totalDecayPoints - wholeDecayPoints
            : 0f;
    }

    /// <summary>
    /// Resolves the normalized progress shown by the HUD bar for the current combo value.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value.</param>
    /// <param name="currentRankRequiredValue">Threshold of the currently active rank, or zero when none is active.</param>
    /// <param name="nextRankRequiredValue">Threshold of the next rank, or zero when already at the top rank.</param>
    /// <param name="hasActiveRank">True when at least one combo rank is active.</param>
    /// <returns>Normalized progress in the 0..1 range.</returns>
    private static float ResolveProgressNormalized(int comboValue,
                                                   int currentRankRequiredValue,
                                                   int nextRankRequiredValue,
                                                   bool hasActiveRank)
    {
        int sanitizedComboValue = math.max(0, comboValue);
        int sanitizedCurrentRequiredValue = math.max(0, currentRankRequiredValue);
        int sanitizedNextRequiredValue = math.max(0, nextRankRequiredValue);

        if (sanitizedNextRequiredValue <= 0)
        {
            return hasActiveRank ? 1f : 0f;
        }

        if (!hasActiveRank)
        {
            return math.saturate((float)sanitizedComboValue / sanitizedNextRequiredValue);
        }

        int range = sanitizedNextRequiredValue - sanitizedCurrentRequiredValue;

        if (range <= 0)
        {
            return 1f;
        }

        return math.saturate((float)(sanitizedComboValue - sanitizedCurrentRequiredValue) / range);
    }
    #endregion

    #endregion
}
