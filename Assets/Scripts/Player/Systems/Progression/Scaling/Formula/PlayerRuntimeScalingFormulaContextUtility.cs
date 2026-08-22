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
    /// Rebuilds the effective formula context directly from the components owned by one player entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read the current player scaling state.</param>
    /// <param name="entity">Player entity owning permanent, temporary and combo scaling data.</param>
    /// <param name="effectiveScalableStats">Reusable mutable list receiving the effective stat view.</param>
    /// <param name="variableContext">Reusable typed formula context rebuilt in place.</param>
    public static void Fill(EntityManager entityManager,
                            Entity entity,
                            List<PlayerScalableStatElement> effectiveScalableStats,
                            Dictionary<string, PlayerFormulaValue> variableContext)
    {
        uint lastVisitOrdinal = 0u;

        if (entity != Entity.Null &&
            entityManager.Exists(entity) &&
            entityManager.HasComponent<PlayerRoomRewardTemporaryState>(entity))
        {
            lastVisitOrdinal = entityManager.GetComponentData<PlayerRoomRewardTemporaryState>(entity).LastVisitOrdinal;
        }

        Fill(entityManager,
             entity,
             lastVisitOrdinal,
             effectiveScalableStats,
             variableContext);
    }

    /// <summary>
    /// Rebuilds the effective formula context from one player entity at an explicit room-visit ordinal.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read the current player scaling state.</param>
    /// <param name="entity">Player entity owning permanent, temporary and combo scaling data.</param>
    /// <param name="lastVisitOrdinal">Room visit used to select active temporary modifiers.</param>
    /// <param name="effectiveScalableStats">Reusable mutable list receiving the effective stat view.</param>
    /// <param name="variableContext">Reusable typed formula context rebuilt in place.</param>
    public static void Fill(EntityManager entityManager,
                            Entity entity,
                            uint lastVisitOrdinal,
                            List<PlayerScalableStatElement> effectiveScalableStats,
                            Dictionary<string, PlayerFormulaValue> variableContext)
    {
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasBuffer<PlayerScalableStatElement>(entity))
        {
            Clear(effectiveScalableStats, variableContext);
            return;
        }

        DynamicBuffer<PlayerScalableStatElement> scalableStats =
            entityManager.GetBuffer<PlayerScalableStatElement>(entity, true);
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers =
            entityManager.HasBuffer<PlayerRoomRewardTemporaryModifierElement>(entity)
                ? entityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(entity, true)
                : default;
        PlayerRuntimeComboCounterConfig comboConfig = default;
        PlayerComboCounterState comboState = default;
        DynamicBuffer<PlayerRuntimeComboRankElement> comboRanks = default;
        DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas = default;

        if (entityManager.HasComponent<PlayerRuntimeComboCounterConfig>(entity) &&
            entityManager.HasComponent<PlayerComboCounterState>(entity) &&
            entityManager.HasBuffer<PlayerRuntimeComboRankElement>(entity) &&
            entityManager.HasBuffer<PlayerPowerUpCharacterTuningFormulaElement>(entity))
        {
            comboConfig = entityManager.GetComponentData<PlayerRuntimeComboCounterConfig>(entity);
            comboState = entityManager.GetComponentData<PlayerComboCounterState>(entity);
            comboRanks = entityManager.GetBuffer<PlayerRuntimeComboRankElement>(entity, true);
            characterTuningFormulas =
                entityManager.GetBuffer<PlayerPowerUpCharacterTuningFormulaElement>(entity, true);
        }

        Fill(scalableStats,
             temporaryModifiers,
             lastVisitOrdinal,
             in comboConfig,
             in comboState,
             comboRanks,
             characterTuningFormulas,
             effectiveScalableStats,
             variableContext);
    }

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
        if (!scalableStatsLookup.HasBuffer(entity))
        {
            Clear(effectiveScalableStats, variableContext);
            return;
        }

        DynamicBuffer<PlayerScalableStatElement> scalableStats = scalableStatsLookup[entity];
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers = default;
        uint lastVisitOrdinal = 0u;

        if (temporaryModifiersLookup.HasBuffer(entity) && temporaryStateLookup.HasComponent(entity))
        {
            PlayerRoomRewardTemporaryState temporaryState = temporaryStateLookup[entity];
            temporaryModifiers = temporaryModifiersLookup[entity];
            lastVisitOrdinal = temporaryState.LastVisitOrdinal;
        }

        PlayerRuntimeComboCounterConfig comboConfig = default;
        PlayerComboCounterState comboState = default;
        DynamicBuffer<PlayerRuntimeComboRankElement> comboRanks = default;
        DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas = default;

        if (comboConfigLookup.HasComponent(entity) &&
            comboStateLookup.HasComponent(entity) &&
            comboRanksLookup.HasBuffer(entity) &&
            characterTuningLookup.HasBuffer(entity))
        {
            comboConfig = comboConfigLookup[entity];
            comboState = comboStateLookup[entity];
            comboRanks = comboRanksLookup[entity];
            characterTuningFormulas = characterTuningLookup[entity];
        }

        Fill(scalableStats,
             temporaryModifiers,
             lastVisitOrdinal,
             in comboConfig,
             in comboState,
             comboRanks,
             characterTuningFormulas,
             effectiveScalableStats,
             variableContext);
    }

    /// <summary>
    /// Rebuilds a typed formula context from explicit ECS buffers so runtime consumers and deterministic tests share the same effective-stat composition.
    /// </summary>
    /// <param name="scalableStats">Permanent scalable-stat buffer used as the base state.</param>
    /// <param name="temporaryModifiers">Optional room-scoped modifiers applied before combo bonuses.</param>
    /// <param name="lastVisitOrdinal">Current room-visit ordinal used to filter temporary modifiers.</param>
    /// <param name="comboConfig">Current runtime combo topology and formula-distribution settings.</param>
    /// <param name="comboState">Current combo value used to resolve active and linearly blended bonuses.</param>
    /// <param name="comboRanks">Optional runtime combo-rank buffer containing formula ranges.</param>
    /// <param name="characterTuningFormulas">Optional flattened Character Tuning formula buffer.</param>
    /// <param name="effectiveScalableStats">Reusable mutable list receiving the effective stat view.</param>
    /// <param name="variableContext">Reusable typed formula context rebuilt in place.</param>
    public static void Fill(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                            DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers,
                            uint lastVisitOrdinal,
                            in PlayerRuntimeComboCounterConfig comboConfig,
                            in PlayerComboCounterState comboState,
                            DynamicBuffer<PlayerRuntimeComboRankElement> comboRanks,
                            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                            List<PlayerScalableStatElement> effectiveScalableStats,
                            Dictionary<string, PlayerFormulaValue> variableContext)
    {
        Clear(effectiveScalableStats, variableContext);

        if (!scalableStats.IsCreated || effectiveScalableStats == null || variableContext == null)
            return;

        PlayerRuntimeScalingComboApplyUtility.CopyBaseScalableStats(scalableStats, effectiveScalableStats);
        PlayerRoomRewardTemporaryModifierUtility.ApplyActiveModifiers(temporaryModifiers,
                                                                      lastVisitOrdinal,
                                                                      effectiveScalableStats);

        if (comboRanks.IsCreated && characterTuningFormulas.IsCreated)
        {
            int activeRankIndex = PlayerComboCounterRuntimeUtility.ResolveActiveRankIndex(comboState.CurrentValue,
                                                                                          in comboConfig,
                                                                                          comboRanks);
            PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(activeRankIndex,
                                                                              comboState.CurrentValue,
                                                                              in comboConfig,
                                                                              comboRanks,
                                                                              characterTuningFormulas,
                                                                              effectiveScalableStats);
        }

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(effectiveScalableStats, variableContext);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Clears reusable effective-stat collections while accepting missing destinations during guarded runtime setup.
    /// </summary>
    /// <param name="effectiveScalableStats">Mutable effective-stat list to clear when available.</param>
    /// <param name="variableContext">Typed variable dictionary to clear when available.</param>
    private static void Clear(List<PlayerScalableStatElement> effectiveScalableStats,
                              Dictionary<string, PlayerFormulaValue> variableContext)
    {
        effectiveScalableStats?.Clear();
        variableContext?.Clear();
    }
    #endregion

    #endregion
}
