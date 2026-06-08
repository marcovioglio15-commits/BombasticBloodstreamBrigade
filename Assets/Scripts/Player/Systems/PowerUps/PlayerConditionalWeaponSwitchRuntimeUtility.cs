using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Evaluates the baked conditional weapon switch table against the player's current scalable stats and writes
/// the winning entry into <see cref="PlayerConditionalWeaponSwitchState"/>. Resolution follows three rules:
/// 1) An entry matches when every Necessary condition is true AND every Necessary And Sufficient condition is
/// true AND at least one Sufficient or Necessary And Sufficient condition is true (or no sufficient class
/// exists at all). 2) Among matching entries the highest Priority wins; ties are broken by authored order.
/// 3) The winning entry's Override Power Up Switch flag is forwarded so the animator presentation layer knows
/// whether the conditional pipeline supersedes the equipped Switch Weapon power-up selection.
/// </summary>
public static class PlayerConditionalWeaponSwitchRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the winning conditional weapon switch entry. Callers must first verify the entry buffer is not
    /// empty using <see cref="PlayerConditionalWeaponSwitchConfig.EntryCount"/>; this method tolerates empty
    /// inputs and emits a neutral state in that case so the animator pipeline never needs special branches.
    /// </summary>
    /// <param name="entryBuffer">Baked conditional weapon switch entries.</param>
    /// <param name="conditionBuffer">Flattened baked condition buffer indexed by every entry slice.</param>
    /// <param name="scalableStatsBuffer">Current runtime scalable stats values keyed by stat name.</param>
    /// <param name="state">Resolved state updated in place with the winning entry or a neutral payload.</param>
    public static void Evaluate(in DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> entryBuffer,
                                in DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> conditionBuffer,
                                in DynamicBuffer<PlayerScalableStatElement> scalableStatsBuffer,
                                ref PlayerConditionalWeaponSwitchState state)
    {
        state.HasMatch = 0;
        state.OverridesPowerUpSwitch = 0;
        state.MatchedPriority = int.MinValue;
        state.WeaponId = default;
        state.Initialized = 1;

        if (!entryBuffer.IsCreated || entryBuffer.Length <= 0)
            return;

        // Walk every entry; keep the first match per priority tier so authored order breaks ties deterministically.
        for (int entryIndex = 0; entryIndex < entryBuffer.Length; entryIndex++)
        {
            PlayerConditionalWeaponSwitchEntryElement entry = entryBuffer[entryIndex];

            if (entry.WeaponId.Length <= 0)
                continue;

            if (!IsEntryMatching(in entry, in conditionBuffer, in scalableStatsBuffer))
                continue;

            if (state.HasMatch != 0 && entry.Priority <= state.MatchedPriority)
                continue;

            state.HasMatch = 1;
            state.MatchedPriority = entry.Priority;
            state.WeaponId = entry.WeaponId;
            state.OverridesPowerUpSwitch = entry.OverridePowerUpSwitch;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Determines whether one entry is a candidate. Necessary conditions all gate the entry; the Sufficient
    /// group is short-circuited as soon as one of its conditions succeeds. Entries with no sufficient-class
    /// condition pass the sufficiency check by default.
    /// </summary>
    /// <param name="entry">Entry being evaluated.</param>
    /// <param name="conditionBuffer">Flattened baked condition buffer.</param>
    /// <param name="scalableStatsBuffer">Current runtime scalable stats values.</param>
    /// <returns>True when the entry satisfies the requirement composition.</returns>
    private static bool IsEntryMatching(in PlayerConditionalWeaponSwitchEntryElement entry,
                                        in DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> conditionBuffer,
                                        in DynamicBuffer<PlayerScalableStatElement> scalableStatsBuffer)
    {
        // An entry with zero conditions matches unconditionally so it can be used as a stat-independent fallback.
        if (entry.ConditionCount <= 0)
            return true;

        bool sufficientGroupSatisfied = entry.SufficientGroupCount == 0;

        for (int conditionIndex = 0; conditionIndex < entry.ConditionCount; conditionIndex++)
        {
            int globalIndex = entry.ConditionStartIndex + conditionIndex;

            if (globalIndex < 0 || globalIndex >= conditionBuffer.Length)
                return false;

            PlayerConditionalWeaponSwitchConditionElement condition = conditionBuffer[globalIndex];
            bool conditionIsTrue = EvaluateCondition(in condition, in scalableStatsBuffer);
            PlayerConditionalWeaponSwitchConditionRequirement requirement = (PlayerConditionalWeaponSwitchConditionRequirement)condition.Requirement;

            switch (requirement)
            {
                case PlayerConditionalWeaponSwitchConditionRequirement.Necessary:
                    if (!conditionIsTrue)
                        return false;
                    break;

                case PlayerConditionalWeaponSwitchConditionRequirement.NecessaryAndSufficient:
                    if (!conditionIsTrue)
                        return false;

                    sufficientGroupSatisfied = true;
                    break;

                case PlayerConditionalWeaponSwitchConditionRequirement.Sufficient:
                    if (conditionIsTrue)
                        sufficientGroupSatisfied = true;
                    break;
            }
        }

        return sufficientGroupSatisfied;
    }

    /// <summary>
    /// Reads one scalable stat value and compares it against the condition inclusive range. Missing stats fail
    /// closed so a typo in the authored stat name cannot silently activate an entry.
    /// </summary>
    /// <param name="condition">Condition being evaluated.</param>
    /// <param name="scalableStatsBuffer">Current runtime scalable stats values.</param>
    /// <returns>True when the stat resolves and its projected numeric value lies within the inclusive range.</returns>
    private static bool EvaluateCondition(in PlayerConditionalWeaponSwitchConditionElement condition,
                                          in DynamicBuffer<PlayerScalableStatElement> scalableStatsBuffer)
    {
        if (condition.StatName.Length <= 0 || !scalableStatsBuffer.IsCreated)
            return false;

        if (!TryResolveStatValue(in scalableStatsBuffer, in condition.StatName, out float statValue))
            return false;

        float minimumValue = condition.MinimumValue;
        float maximumValue = condition.MaximumValue;

        // Tolerate inverted bounds; treat them as the same inclusive range without raising a runtime error.
        if (minimumValue > maximumValue)
            return statValue >= maximumValue && statValue <= minimumValue;

        return statValue >= minimumValue && statValue <= maximumValue;
    }

    /// <summary>
    /// Resolves the numeric projection of one scalable stat by name. Boolean stats project to zero or one; token
    /// stats fail because there is no obvious ordering against an inclusive numeric range.
    /// </summary>
    /// <param name="scalableStatsBuffer">Current runtime scalable stats values.</param>
    /// <param name="statName">Scalable stat name to resolve.</param>
    /// <param name="statValue">Resolved numeric projection.</param>
    /// <returns>True when the stat exists and supports a numeric projection.</returns>
    private static bool TryResolveStatValue(in DynamicBuffer<PlayerScalableStatElement> scalableStatsBuffer,
                                            in FixedString64Bytes statName,
                                            out float statValue)
    {
        statValue = 0f;

        for (int statIndex = 0; statIndex < scalableStatsBuffer.Length; statIndex++)
        {
            PlayerScalableStatElement statElement = scalableStatsBuffer[statIndex];

            if (!statElement.Name.Equals(statName))
                continue;

            PlayerScalableStatType statType = (PlayerScalableStatType)statElement.Type;

            switch (statType)
            {
                case PlayerScalableStatType.Float:
                case PlayerScalableStatType.Integer:
                case PlayerScalableStatType.Unsigned:
                    statValue = statElement.Value;
                    return true;

                case PlayerScalableStatType.Boolean:
                    statValue = statElement.BooleanValue != 0 ? 1f : 0f;
                    return true;

                default:
                    return false;
            }
        }

        return false;
    }
    #endregion

    #endregion
}
