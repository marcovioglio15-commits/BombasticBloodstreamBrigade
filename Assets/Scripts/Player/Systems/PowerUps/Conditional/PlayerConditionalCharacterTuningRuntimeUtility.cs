using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides the immutable ECS inputs required to evaluate Character Tuning for one qualified conditional shot.
/// </summary>
public readonly struct PlayerConditionalCharacterTuningContext
{
    #region Fields
    public readonly DynamicBuffer<PlayerPowerUpUnlockCatalogElement> UnlockCatalog;
    public readonly DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> CharacterTuningFormulas;
    public readonly DynamicBuffer<PlayerScalableStatElement> ScalableStats;
    public readonly DynamicBuffer<PlayerRuntimeControllerScalingElement> ControllerScaling;
    public readonly DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> TemporaryModifiers;
    public readonly PlayerRoomRewardTemporaryState TemporaryState;
    public readonly DynamicBuffer<PlayerRuntimeComboRankElement> RuntimeComboRanks;
    public readonly PlayerRuntimeComboCounterConfig RuntimeComboConfig;
    public readonly PlayerComboCounterState ComboState;
    #endregion

    #region Methods

    #region Construction
    /// <summary>
    /// Captures read-only runtime buffers and state used to derive a shot-local scalable-stat context without mutating player progression.
    /// </summary>
    /// <param name="unlockCatalog">Power-up catalog used to resolve the qualified source formula range.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formulas shared by power-ups and combo ranks.</param>
    /// <param name="scalableStats">Authoritative persistent scalable stats used as the local overlay baseline.</param>
    /// <param name="controllerScaling">Baked controller formulas used to rebuild the local shooting configuration.</param>
    /// <param name="temporaryModifiers">Current room-scoped modifiers applied after conditional assignments.</param>
    /// <param name="temporaryState">Current room visit state used to select active temporary modifiers.</param>
    /// <param name="runtimeComboRanks">Current combo ranks contributing temporary stat formulas.</param>
    /// <param name="runtimeComboConfig">Current combo topology used to resolve the active rank.</param>
    /// <param name="comboState">Current combo value used to evaluate rank bonuses.</param>
    public PlayerConditionalCharacterTuningContext(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                   DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                   DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                   DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScaling,
                                                   DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers,
                                                   in PlayerRoomRewardTemporaryState temporaryState,
                                                   DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                                   in PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                                   in PlayerComboCounterState comboState)
    {
        UnlockCatalog = unlockCatalog;
        CharacterTuningFormulas = characterTuningFormulas;
        ScalableStats = scalableStats;
        ControllerScaling = controllerScaling;
        TemporaryModifiers = temporaryModifiers;
        TemporaryState = temporaryState;
        RuntimeComboRanks = runtimeComboRanks;
        RuntimeComboConfig = runtimeComboConfig;
        ComboState = comboState;
    }
    #endregion

    #endregion
}

/// <summary>
/// Evaluates conditional Character Tuning into a shot-local shooting configuration while preserving persistent player stats.
/// </summary>
public static class PlayerConditionalCharacterTuningRuntimeUtility
{
    #region Fields
    private static readonly List<PlayerScalableStatElement> conditionalBaseStats = new List<PlayerScalableStatElement>(64);
    private static readonly List<PlayerScalableStatElement> conditionalEffectiveStats = new List<PlayerScalableStatElement>(64);
    private static readonly Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(64, System.StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Data
    /// <summary>
    /// Stores the compact immutable catalog data required to evaluate one conditional Character Tuning source without copying its large ECS catalog element.
    /// </summary>
    private readonly struct ConditionalCharacterTuningSource
    {
        #region Fields
        public readonly int FormulaStartIndex;
        public readonly int FormulaCount;
        public readonly int ApplicationCount;
        #endregion

        #region Methods

        #region Construction
        /// <summary>
        /// Captures the flattened formula range and owned stack count resolved from one qualifying catalog entry.
        /// </summary>
        /// <param name="formulaStartIndex">First formula index assigned to the source.</param>
        /// <param name="formulaCount">Number of contiguous formulas assigned to the source.</param>
        /// <param name="applicationCount">Positive number of owned acquisitions applied to the shot.</param>
        public ConditionalCharacterTuningSource(int formulaStartIndex,
                                                int formulaCount,
                                                int applicationCount)
        {
            FormulaStartIndex = formulaStartIndex;
            FormulaCount = formulaCount;
            ApplicationCount = applicationCount;
        }
        #endregion

        #endregion
    }
    #endregion

    #region Methods

    #region Formula Accumulation
    /// <summary>
    /// Applies the qualified power-up's Character Tuning range to the reusable shot-local baseline, including stack count, without changing authoritative scalable stats.
    /// </summary>
    /// <param name="powerUpId">Stable identifier of the passive or toggleable Active that qualified the current shot.</param>
    /// <param name="context">Read-only runtime data used to resolve and evaluate the formula range.</param>
    /// <param name="shotContextInitialized">Mutable flag indicating whether persistent scalable stats were copied for this shot.</param>
    /// <returns>True when at least one assignment changed the shot-local scalable-stat context.</returns>
    public static bool TryAccumulate(FixedString64Bytes powerUpId,
                                     in PlayerConditionalCharacterTuningContext context,
                                     ref bool shotContextInitialized)
    {
        if (powerUpId.Length <= 0 ||
            !context.UnlockCatalog.IsCreated ||
            !context.CharacterTuningFormulas.IsCreated ||
            !context.ScalableStats.IsCreated)
        {
            return false;
        }

        if (!TryResolveSource(powerUpId,
                              context.UnlockCatalog,
                              out ConditionalCharacterTuningSource source))
            return false;

        if (!shotContextInitialized)
        {
            PlayerRuntimeScalingComboApplyUtility.CopyBaseScalableStats(context.ScalableStats, conditionalBaseStats);
            shotContextInitialized = true;
        }

        bool anyChanged = false;

        // Preserve stack semantics by applying the source range once per owned acquisition.
        for (int applicationIndex = 0; applicationIndex < source.ApplicationCount; applicationIndex++)
        {
            if (!PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuningRange(source.FormulaStartIndex,
                                                                                         source.FormulaCount,
                                                                                         context.CharacterTuningFormulas,
                                                                                         conditionalBaseStats,
                                                                                        out int appliedFormulaCount))
            {
                continue;
            }

            anyChanged = anyChanged || appliedFormulaCount > 0;
        }

        return anyChanged;
    }
    #endregion

    #region Shooting Rebuild
    /// <summary>
    /// Rebuilds only the current shot's shooting configuration after conditional formulas, room modifiers, and combo bonuses are composed in normal runtime order.
    /// </summary>
    /// <param name="baselineShootingConfig">Current authoritative shooting configuration used for fields without scaling metadata.</param>
    /// <param name="context">Read-only runtime data supplying temporary modifiers, combo bonuses, and controller scaling metadata.</param>
    /// <param name="shotShootingConfig">Resolved shot-local shooting configuration.</param>
    public static void RebuildShootingConfig(in PlayerRuntimeShootingConfig baselineShootingConfig,
                                             in PlayerConditionalCharacterTuningContext context,
                                             out PlayerRuntimeShootingConfig shotShootingConfig)
    {
        shotShootingConfig = baselineShootingConfig;

        if (conditionalBaseStats.Count <= 0 || !context.ControllerScaling.IsCreated)
            return;

        conditionalEffectiveStats.Clear();

        // Copy the conditional baseline before adding transient room and combo contributions.
        for (int statIndex = 0; statIndex < conditionalBaseStats.Count; statIndex++)
            conditionalEffectiveStats.Add(conditionalBaseStats[statIndex]);

        PlayerRoomRewardTemporaryModifierUtility.ApplyActiveModifiers(context.TemporaryModifiers,
                                                                      context.TemporaryState.LastVisitOrdinal,
                                                                      conditionalEffectiveStats);
        PlayerRuntimeComboCounterConfig runtimeComboConfig = context.RuntimeComboConfig;
        int activeComboRankIndex = PlayerComboCounterRuntimeUtility.ResolveActiveRankIndex(context.ComboState.CurrentValue,
                                                                                           in runtimeComboConfig,
                                                                                           context.RuntimeComboRanks);
        PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(activeComboRankIndex,
                                                                          context.ComboState.CurrentValue,
                                                                          in runtimeComboConfig,
                                                                          context.RuntimeComboRanks,
                                                                          context.CharacterTuningFormulas,
                                                                          conditionalEffectiveStats);
        PlayerScalingRuntimeFormulaUtility.FillVariableContext(conditionalEffectiveStats, variableContext);
        PlayerRuntimeMovementConfig unusedMovement = default;
        PlayerRuntimeLookConfig unusedLook = default;
        PlayerRuntimeCameraConfig unusedCamera = default;
        PlayerRuntimeHealthStatisticsConfig unusedHealth = default;
        DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> unavailableAppliedElementSlots = default;
        PlayerRuntimeScalingControllerApplyUtility.Apply(context.ControllerScaling,
                                                         variableContext,
                                                         ref unusedMovement,
                                                         ref unusedLook,
                                                         ref unusedCamera,
                                                         ref shotShootingConfig,
                                                         unavailableAppliedElementSlots,
                                                         ref unusedHealth);
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves the compact conditional Character Tuning data matching one stable power-up identifier without requesting write access.
    /// </summary>
    /// <param name="powerUpId">Stable identifier to locate.</param>
    /// <param name="unlockCatalog">Runtime catalog scanned in deterministic bake order.</param>
    /// <param name="source">Compact formula range and stack count when a qualifying entry exists.</param>
    /// <returns>True when a matching conditional Character Tuning source exists.</returns>
    private static bool TryResolveSource(FixedString64Bytes powerUpId,
                                         DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                         out ConditionalCharacterTuningSource source)
    {
        if (!unlockCatalog.IsCreated || unlockCatalog.Length <= 0)
        {
            source = default;
            return false;
        }

        // The indexer returns a read-only-safe snapshot and this scan only runs for qualified conditional shots.
        for (int candidateIndex = 0; candidateIndex < unlockCatalog.Length; candidateIndex++)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[candidateIndex];

            if (catalogEntry.PowerUpId != powerUpId)
                continue;

            if (catalogEntry.CharacterTuningFormulaCount <= 0)
            {
                source = default;
                return false;
            }

            PowerUpConditionalApplicationMode mode = catalogEntry.UnlockKind == PlayerPowerUpUnlockKind.Passive
                ? catalogEntry.PassiveToolConfig.ConditionalApplication.Mode
                : catalogEntry.ActiveSlotConfig.TogglePassiveTool.ConditionalApplication.Mode;

            switch (mode)
            {
                case PowerUpConditionalApplicationMode.DelayedShootApplication:
                case PowerUpConditionalApplicationMode.SuddenStrike:
                    source = new ConditionalCharacterTuningSource(catalogEntry.CharacterTuningFormulaStartIndex,
                                                                   catalogEntry.CharacterTuningFormulaCount,
                                                                   math.max(1, catalogEntry.CurrentUnlockCount));
                    return true;
                default:
                    source = default;
                    return false;
            }
        }

        source = default;
        return false;
    }
    #endregion

    #endregion
}
