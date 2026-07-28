using System.Collections.Generic;
using Unity.Entities;

/// <summary>
/// Builds the effective typed formula context shared by runtime systems that react to scalable-stat hash changes.
/// </summary>
internal static class PlayerRuntimeScalingFormulaContextUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the reusable variable context from base scalable stats and active combo-rank Character Tuning bonuses.
    /// </summary>
    /// <param name="entity">Player entity owning the current scalable-stat state.</param>
    /// <param name="scalableStatsLookup">Read-only base scalable-stat lookup.</param>
    /// <param name="temporaryModifiersLookup">Read-only room-scoped stat modifier lookup.</param>
    /// <param name="temporaryStateLookup">Read-only room-visit state lookup.</param>
    /// <param name="comboConfigLookup">Read-only runtime combo config lookup.</param>
    /// <param name="comboStateLookup">Read-only combo state lookup.</param>
    /// <param name="comboRanksLookup">Read-only runtime combo-rank lookup.</param>
    /// <param name="characterTuningLookup">Read-only Character Tuning formula lookup.</param>
    /// <param name="effectiveScalableStats">Reusable mutable list receiving the effective stat view.</param>
    /// <param name="variableContext">Reusable typed formula context rebuilt in place.</param>
    public static void Fill(Entity entity,
                            in BufferLookup<PlayerScalableStatElement> scalableStatsLookup,
                            in BufferLookup<PlayerRoomRewardTemporaryModifierElement> temporaryModifiersLookup,
                            in ComponentLookup<PlayerRoomRewardTemporaryState> temporaryStateLookup,
                            in ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup,
                            in ComponentLookup<PlayerComboCounterState> comboStateLookup,
                            in BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup,
                            in BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup,
                            List<PlayerScalableStatElement> effectiveScalableStats,
                            Dictionary<string, PlayerFormulaValue> variableContext)
    {
        variableContext.Clear();
        effectiveScalableStats.Clear();

        if (!scalableStatsLookup.HasBuffer(entity))
            return;

        DynamicBuffer<PlayerScalableStatElement> scalableStats = scalableStatsLookup[entity];
        PlayerRuntimeScalingComboApplyUtility.CopyBaseScalableStats(scalableStats, effectiveScalableStats);

        if (temporaryModifiersLookup.HasBuffer(entity) && temporaryStateLookup.HasComponent(entity))
        {
            PlayerRoomRewardTemporaryState temporaryState = temporaryStateLookup[entity];
            PlayerRoomRewardTemporaryModifierUtility.ApplyActiveModifiers(temporaryModifiersLookup[entity],
                                                                          temporaryState.LastVisitOrdinal,
                                                                          effectiveScalableStats);
        }

        if (comboConfigLookup.HasComponent(entity) &&
            comboStateLookup.HasComponent(entity) &&
            comboRanksLookup.HasBuffer(entity) &&
            characterTuningLookup.HasBuffer(entity))
        {
            PlayerComboCounterState comboState = comboStateLookup[entity];
            PlayerRuntimeComboCounterConfig comboConfig = comboConfigLookup[entity];
            DynamicBuffer<PlayerRuntimeComboRankElement> comboRanks = comboRanksLookup[entity];
            int activeRankIndex = PlayerComboCounterRuntimeUtility.ResolveActiveRankIndex(comboState.CurrentValue,
                                                                                          in comboConfig,
                                                                                          comboRanks);
            PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(activeRankIndex,
                                                                              comboState.CurrentValue,
                                                                              comboRanks,
                                                                              characterTuningLookup[entity],
                                                                              effectiveScalableStats);
        }

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(effectiveScalableStats, variableContext);
    }
    #endregion

    #endregion
}
