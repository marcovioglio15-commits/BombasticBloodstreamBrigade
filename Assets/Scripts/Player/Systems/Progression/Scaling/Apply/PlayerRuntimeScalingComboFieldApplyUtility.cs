using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies typed combo Add Scaling results to runtime config, reward entries, and nested passive unlocks.
/// </summary>
internal static class PlayerRuntimeScalingComboFieldApplyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one Boolean combo formula result to config or a mode-specific nested reward entry.
    /// </summary>
    /// <param name="fieldId">Target combo field identifier.</param>
    /// <param name="entryMode">Topology owning a nested reward entry.</param>
    /// <param name="entryIndex">Logical mode-local reward entry index.</param>
    /// <param name="passiveUnlockIndex">Logical passive unlock index inside the reward entry.</param>
    /// <param name="resolvedBoolean">Evaluated Boolean value.</param>
    /// <param name="runtimeComboConfig">Mutable runtime combo config.</param>
    /// <param name="runtimeComboRanks">Mutable combined reward-entry buffer.</param>
    /// <param name="runtimePassiveUnlocks">Mutable flattened passive-unlock buffer.</param>
    public static void ApplyBooleanValue(PlayerRuntimeComboCounterFieldId fieldId,
                                         PlayerComboCounterMode entryMode,
                                         int entryIndex,
                                         int passiveUnlockIndex,
                                         bool resolvedBoolean,
                                         ref PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                         DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                         DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks)
    {
        switch (fieldId)
        {
            case PlayerRuntimeComboCounterFieldId.Enabled:
                runtimeComboConfig.Enabled = resolvedBoolean ? (byte)1 : (byte)0;
                break;
            case PlayerRuntimeComboCounterFieldId.ShieldDamageBreaksCombo:
                runtimeComboConfig.ShieldDamageBreaksCombo = resolvedBoolean ? (byte)1 : (byte)0;
                break;
            case PlayerRuntimeComboCounterFieldId.PreventDecayIntoNonDecayingRanks:
                runtimeComboConfig.PreventDecayIntoNonDecayingRanks = resolvedBoolean ? (byte)1 : (byte)0;
                break;
            case PlayerRuntimeComboCounterFieldId.SingleRankMilestoneEnabled:
                if (!TryResolveRuntimeEntryIndex(entryMode, entryIndex, runtimeComboRanks, out int absoluteEntryIndex))
                    return;

                PlayerRuntimeComboRankElement entry = runtimeComboRanks[absoluteEntryIndex];
                entry.Enabled = resolvedBoolean ? (byte)1 : (byte)0;
                runtimeComboRanks[absoluteEntryIndex] = entry;
                break;
            case PlayerRuntimeComboCounterFieldId.RankPassiveUnlockEnabled:
                if (!TryResolvePassiveUnlockAbsoluteIndex(entryMode,
                                                          entryIndex,
                                                          passiveUnlockIndex,
                                                          runtimeComboRanks,
                                                          runtimePassiveUnlocks,
                                                          out int absoluteUnlockIndex))
                {
                    return;
                }

                PlayerRuntimeComboPassiveUnlockElement passiveUnlock = runtimePassiveUnlocks[absoluteUnlockIndex];
                passiveUnlock.IsEnabled = resolvedBoolean ? (byte)1 : (byte)0;
                runtimePassiveUnlocks[absoluteUnlockIndex] = passiveUnlock;
                break;
        }
    }

    /// <summary>
    /// Applies one numeric or enum combo formula result and preserves mode-local entry addressing.
    /// </summary>
    /// <param name="fieldId">Target combo field identifier.</param>
    /// <param name="entryMode">Topology owning a nested reward entry.</param>
    /// <param name="entryIndex">Logical mode-local reward entry index.</param>
    /// <param name="resolvedValue">Evaluated numeric value.</param>
    /// <param name="runtimeComboConfig">Mutable runtime combo config.</param>
    /// <param name="runtimeComboRanks">Mutable combined reward-entry buffer.</param>
    public static void ApplyNumericValue(PlayerRuntimeComboCounterFieldId fieldId,
                                         PlayerComboCounterMode entryMode,
                                         int entryIndex,
                                         float resolvedValue,
                                         ref PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                         DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks)
    {
        switch (fieldId)
        {
            case PlayerRuntimeComboCounterFieldId.Mode:
                runtimeComboConfig.Mode = PlayerRuntimeScalingEnumUtility.ResolveComboCounterMode(resolvedValue);
                return;
            case PlayerRuntimeComboCounterFieldId.ComboGainPerKill:
                runtimeComboConfig.ComboGainPerKill = math.max(0, (int)math.round(resolvedValue));
                return;
            case PlayerRuntimeComboCounterFieldId.DamageBreakMode:
                runtimeComboConfig.DamageBreakMode = PlayerRuntimeScalingEnumUtility.ResolveComboDamageBreakMode(resolvedValue);
                return;
            case PlayerRuntimeComboCounterFieldId.SingleRankMaximumComboValue:
                runtimeComboConfig.SingleRankMaximumComboValue = math.max(0, (int)math.round(resolvedValue));
                return;
            case PlayerRuntimeComboCounterFieldId.SingleRankPointsDecayPerSecond:
                runtimeComboConfig.SingleRankPointsDecayPerSecond = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeComboCounterFieldId.SingleRankValueDisplayMode:
                runtimeComboConfig.SingleRankValueDisplayMode = PlayerRuntimeScalingEnumUtility.ResolveComboSingleRankValueDisplayMode(resolvedValue);
                return;
            case PlayerRuntimeComboCounterFieldId.SingleRankFormulaDistributionMode:
                runtimeComboConfig.SingleRankFormulaDistributionMode = PlayerRuntimeScalingEnumUtility.ResolveComboSingleRankFormulaDistributionMode(resolvedValue);
                return;
        }

        if (!TryResolveRuntimeEntryIndex(entryMode, entryIndex, runtimeComboRanks, out int absoluteEntryIndex))
            return;

        PlayerRuntimeComboRankElement entry = runtimeComboRanks[absoluteEntryIndex];

        switch (fieldId)
        {
            case PlayerRuntimeComboCounterFieldId.RankRequiredComboValue:
                entry.RequiredComboValue = math.max(0, (int)math.round(resolvedValue));
                break;
            case PlayerRuntimeComboCounterFieldId.RankPointsDecayPerSecond:
                entry.PointsDecayPerSecond = math.max(0f, resolvedValue);
                break;
            case PlayerRuntimeComboCounterFieldId.RankProgressiveBoostPercent:
                entry.ProgressiveBoostPercent = resolvedValue;
                break;
            case PlayerRuntimeComboCounterFieldId.SingleRankMilestoneRequiredProgressPercent:
                entry.RequiredProgressPercent = resolvedValue;
                break;
            default:
                return;
        }

        runtimeComboRanks[absoluteEntryIndex] = entry;
    }

    /// <summary>
    /// Applies one token combo formula result to the single-rank label, milestone identity, or a nested passive unlock.
    /// </summary>
    /// <param name="fieldId">Target combo field identifier.</param>
    /// <param name="entryMode">Topology owning a nested reward entry.</param>
    /// <param name="entryIndex">Logical mode-local reward entry index.</param>
    /// <param name="passiveUnlockIndex">Logical passive unlock index inside the reward entry.</param>
    /// <param name="resolvedToken">Evaluated token value.</param>
    /// <param name="runtimeComboConfig">Mutable runtime combo config.</param>
    /// <param name="runtimeComboRanks">Mutable combined reward-entry buffer.</param>
    /// <param name="runtimePassiveUnlocks">Mutable flattened passive-unlock buffer.</param>
    public static void ApplyTokenValue(PlayerRuntimeComboCounterFieldId fieldId,
                                       PlayerComboCounterMode entryMode,
                                       int entryIndex,
                                       int passiveUnlockIndex,
                                       string resolvedToken,
                                       ref PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                       DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                       DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks)
    {
        FixedString64Bytes token = new FixedString64Bytes(string.IsNullOrWhiteSpace(resolvedToken)
            ? string.Empty
            : resolvedToken.Trim());

        if (fieldId == PlayerRuntimeComboCounterFieldId.SingleRankId)
        {
            runtimeComboConfig.SingleRankId = token;
            return;
        }

        if (fieldId == PlayerRuntimeComboCounterFieldId.SingleRankMilestoneId)
        {
            if (!TryResolveRuntimeEntryIndex(entryMode, entryIndex, runtimeComboRanks, out int absoluteEntryIndex))
                return;

            PlayerRuntimeComboRankElement entry = runtimeComboRanks[absoluteEntryIndex];
            entry.RankId = token;
            runtimeComboRanks[absoluteEntryIndex] = entry;
            return;
        }

        if (fieldId != PlayerRuntimeComboCounterFieldId.RankPassiveUnlockPowerUpId)
            return;

        if (!TryResolvePassiveUnlockAbsoluteIndex(entryMode,
                                                  entryIndex,
                                                  passiveUnlockIndex,
                                                  runtimeComboRanks,
                                                  runtimePassiveUnlocks,
                                                  out int absoluteUnlockIndex))
        {
            return;
        }

        PlayerRuntimeComboPassiveUnlockElement passiveUnlock = runtimePassiveUnlocks[absoluteUnlockIndex];
        passiveUnlock.PassivePowerUpId = token;
        runtimePassiveUnlocks[absoluteUnlockIndex] = passiveUnlock;
    }

    /// <summary>
    /// Recalculates every single-rank milestone threshold after maximum or percentage formulas change.
    /// </summary>
    /// <param name="runtimeComboConfig">Current runtime single-rank maximum.</param>
    /// <param name="runtimeComboRanks">Mutable combined reward-entry buffer.</param>
    public static void RefreshSingleRankMilestoneThresholds(in PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                                            DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks)
    {
        if (!runtimeComboRanks.IsCreated)
            return;

        for (int entryIndex = 0; entryIndex < runtimeComboRanks.Length; entryIndex++)
        {
            PlayerRuntimeComboRankElement entry = runtimeComboRanks[entryIndex];

            if (entry.Mode != PlayerComboCounterMode.SingleRankProgression)
                continue;

            entry.RequiredComboValue = PlayerComboCounterRuntimeUtility.ResolveSingleRankMilestoneRequiredValue(runtimeComboConfig.SingleRankMaximumComboValue,
                                                                                                                entry.RequiredProgressPercent);
            runtimeComboRanks[entryIndex] = entry;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves one logical mode-local reward index into the combined runtime buffer.
    /// </summary>
    /// <param name="entryMode">Topology owning the requested reward entry.</param>
    /// <param name="entryIndex">Logical zero-based index among entries of the same topology.</param>
    /// <param name="runtimeComboRanks">Combined runtime reward-entry buffer.</param>
    /// <param name="absoluteEntryIndex">Resolved absolute buffer index.</param>
    /// <returns>True when a matching reward entry exists.</returns>
    private static bool TryResolveRuntimeEntryIndex(PlayerComboCounterMode entryMode,
                                                    int entryIndex,
                                                    DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                                    out int absoluteEntryIndex)
    {
        absoluteEntryIndex = -1;

        if (!runtimeComboRanks.IsCreated || entryIndex < 0)
            return false;

        int logicalIndex = 0;

        for (int candidateIndex = 0; candidateIndex < runtimeComboRanks.Length; candidateIndex++)
        {
            if (runtimeComboRanks[candidateIndex].Mode != entryMode)
                continue;

            if (logicalIndex == entryIndex)
            {
                absoluteEntryIndex = candidateIndex;
                return true;
            }

            logicalIndex += 1;
        }

        return false;
    }

    /// <summary>
    /// Resolves one mode-local passive unlock index into the flattened runtime unlock buffer.
    /// </summary>
    /// <param name="entryMode">Topology owning the reward entry.</param>
    /// <param name="entryIndex">Logical mode-local reward entry index.</param>
    /// <param name="passiveUnlockIndex">Logical unlock index inside the reward entry.</param>
    /// <param name="runtimeComboRanks">Combined runtime reward-entry buffer.</param>
    /// <param name="runtimePassiveUnlocks">Flattened runtime passive-unlock buffer.</param>
    /// <param name="absoluteUnlockIndex">Resolved absolute unlock buffer index.</param>
    /// <returns>True when both nested indices resolve to valid runtime elements.</returns>
    private static bool TryResolvePassiveUnlockAbsoluteIndex(PlayerComboCounterMode entryMode,
                                                             int entryIndex,
                                                             int passiveUnlockIndex,
                                                             DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                                             DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                                                             out int absoluteUnlockIndex)
    {
        absoluteUnlockIndex = -1;

        if (!runtimePassiveUnlocks.IsCreated || passiveUnlockIndex < 0)
            return false;

        if (!TryResolveRuntimeEntryIndex(entryMode, entryIndex, runtimeComboRanks, out int absoluteEntryIndex))
            return false;

        PlayerRuntimeComboRankElement entry = runtimeComboRanks[absoluteEntryIndex];

        if (passiveUnlockIndex >= entry.PassiveUnlockCount)
            return false;

        int resolvedUnlockIndex = entry.PassiveUnlockStartIndex + passiveUnlockIndex;

        if (resolvedUnlockIndex < 0 || resolvedUnlockIndex >= runtimePassiveUnlocks.Length)
            return false;

        absoluteUnlockIndex = resolvedUnlockIndex;
        return true;
    }
    #endregion

    #endregion
}
