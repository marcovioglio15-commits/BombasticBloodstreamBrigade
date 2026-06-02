using Unity.Entities;

/// <summary>
/// Snapshot of missing runtime power-up data used by PlayerPowerUpsInitializeSystem.
/// </summary>
internal readonly struct PlayerPowerUpsMissingRuntimeFlags
{
    #region Fields
    public readonly bool HasMissingState;
    public readonly bool HasMissingPassiveToolsState;
    public readonly bool HasMissingDash;
    public readonly bool HasMissingBulletTimeState;
    public readonly bool HasMissingHealOverTimeState;
    public readonly bool HasMissingPassiveExplosionState;
    public readonly bool HasMissingPassiveHealState;
    public readonly bool HasMissingPassiveBulletTimeState;
    public readonly bool HasMissingLaserBeamState;
    public readonly bool HasMissingElementalTrailState;
    public readonly bool HasMissingBombRequestBuffer;
    public readonly bool HasMissingElementalTrailSegmentBuffer;
    public readonly bool HasMissingLaserBeamLaneBuffer;
    public readonly bool HasMissingLaserBeamPulseHitBuffer;
    public readonly bool HasMissingExplosionRequestBuffer;
    public readonly bool HasMissingPowerUpVfxRequestBuffer;
    public readonly bool HasMissingPowerUpVfxPrefabBindingBuffer;
    public readonly bool HasMissingPowerUpVfxCapConfig;
    public readonly bool HasMissingPowerUpCheatPresetEntryBuffer;
    public readonly bool HasMissingPowerUpCheatPresetSlotBuffer;
    public readonly bool HasMissingPowerUpCheatPresetPassiveBuffer;
    public readonly bool HasMissingPowerUpUnlockCatalogBuffer;
    public readonly bool HasMissingPowerUpCharacterTuningFormulaBuffer;
    public readonly bool HasMissingPowerUpTierDefinitionBuffer;
    public readonly bool HasMissingPowerUpTierEntryBuffer;
    public readonly bool HasMissingPowerUpTierEntryScalingBuffer;
    public readonly bool HasMissingMilestoneSelectionState;
    public readonly bool HasMissingMilestoneTimeScaleResumeState;
    public readonly bool HasMissingMilestoneSelectionOfferBuffer;
    #endregion

    #region Properties
    public bool HasAnyMissing
    {
        get
        {
            return HasMissingState ||
                   HasMissingPassiveToolsState ||
                   HasMissingDash ||
                   HasMissingBulletTimeState ||
                   HasMissingHealOverTimeState ||
                   HasMissingPassiveExplosionState ||
                   HasMissingPassiveHealState ||
                   HasMissingPassiveBulletTimeState ||
                   HasMissingLaserBeamState ||
                   HasMissingElementalTrailState ||
                   HasMissingBombRequestBuffer ||
                   HasMissingElementalTrailSegmentBuffer ||
                   HasMissingLaserBeamLaneBuffer ||
                   HasMissingLaserBeamPulseHitBuffer ||
                   HasMissingExplosionRequestBuffer ||
                   HasMissingPowerUpVfxRequestBuffer ||
                   HasMissingPowerUpVfxPrefabBindingBuffer ||
                   HasMissingPowerUpVfxCapConfig ||
                   HasMissingPowerUpCheatPresetEntryBuffer ||
                   HasMissingPowerUpCheatPresetSlotBuffer ||
                   HasMissingPowerUpCheatPresetPassiveBuffer ||
                   HasMissingPowerUpUnlockCatalogBuffer ||
                   HasMissingPowerUpCharacterTuningFormulaBuffer ||
                   HasMissingPowerUpTierDefinitionBuffer ||
                   HasMissingPowerUpTierEntryBuffer ||
                   HasMissingPowerUpTierEntryScalingBuffer ||
                   HasMissingMilestoneSelectionState ||
                   HasMissingMilestoneTimeScaleResumeState ||
                   HasMissingMilestoneSelectionOfferBuffer;
        }
    }
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates a new immutable snapshot of missing power-up runtime flags.
    /// </summary>
    /// <param name="hasMissingState">True when PlayerPowerUpsState is missing.</param>
    /// <param name="hasMissingPassiveToolsState">True when the PlayerPassiveToolsStateElement snapshot buffer is missing.</param>
    /// <param name="hasMissingDash">True when PlayerDashState is missing.</param>
    /// <param name="hasMissingBulletTimeState">True when PlayerBulletTimeState is missing.</param>
    /// <param name="hasMissingHealOverTimeState">True when PlayerHealOverTimeState is missing.</param>
    /// <param name="hasMissingPassiveExplosionState">True when PlayerPassiveExplosionState is missing.</param>
    /// <param name="hasMissingPassiveHealState">True when PlayerPassiveHealState is missing.</param>
    /// <param name="hasMissingPassiveBulletTimeState">True when PlayerPassiveBulletTimeState is missing.</param>
    /// <param name="hasMissingLaserBeamState">True when PlayerLaserBeamState is missing.</param>
    /// <param name="hasMissingElementalTrailState">True when PlayerElementalTrailState is missing.</param>
    /// <param name="hasMissingBombRequestBuffer">True when PlayerBombSpawnRequest buffer is missing.</param>
    /// <param name="hasMissingElementalTrailSegmentBuffer">True when PlayerElementalTrailSegmentElement buffer is missing.</param>
    /// <param name="hasMissingLaserBeamLaneBuffer">True when PlayerLaserBeamLaneElement buffer is missing.</param>
    /// <param name="hasMissingLaserBeamPulseHitBuffer">True when PlayerLaserBeamPulseHitElement buffer is missing.</param>
    /// <param name="hasMissingExplosionRequestBuffer">True when PlayerExplosionRequest buffer is missing.</param>
    /// <param name="hasMissingPowerUpVfxRequestBuffer">True when PlayerPowerUpVfxSpawnRequest buffer is missing.</param>
    /// <param name="hasMissingPowerUpVfxPrefabBindingBuffer">True when PlayerPowerUpVfxPrefabBindingElement buffer is missing.</param>
    /// <param name="hasMissingPowerUpVfxCapConfig">True when PlayerPowerUpVfxCapConfig is missing.</param>
    /// <param name="hasMissingPowerUpCheatPresetEntryBuffer">True when PlayerPowerUpCheatPresetEntry buffer is missing.</param>
    /// <param name="hasMissingPowerUpCheatPresetSlotBuffer">True when PlayerPowerUpCheatPresetSlotElement buffer is missing.</param>
    /// <param name="hasMissingPowerUpCheatPresetPassiveBuffer">True when PlayerPowerUpCheatPresetPassiveElement buffer is missing.</param>
    /// <param name="hasMissingPowerUpUnlockCatalogBuffer">True when PlayerPowerUpUnlockCatalogElement buffer is missing.</param>
    /// <param name="hasMissingPowerUpCharacterTuningFormulaBuffer">True when PlayerPowerUpCharacterTuningFormulaElement buffer is missing.</param>
    /// <param name="hasMissingPowerUpTierDefinitionBuffer">True when PlayerPowerUpTierDefinitionElement buffer is missing.</param>
    /// <param name="hasMissingPowerUpTierEntryBuffer">True when PlayerPowerUpTierEntryElement buffer is missing.</param>
    /// <param name="hasMissingPowerUpTierEntryScalingBuffer">True when PlayerPowerUpTierEntryScalingElement buffer is missing.</param>
    /// <param name="hasMissingMilestoneSelectionState">True when PlayerMilestonePowerUpSelectionState is missing.</param>
    /// <param name="hasMissingMilestoneTimeScaleResumeState">True when PlayerMilestoneTimeScaleResumeState is missing.</param>
    /// <param name="hasMissingMilestoneSelectionOfferBuffer">True when PlayerMilestonePowerUpSelectionOfferElement buffer is missing.</param>
    /// <returns>A populated immutable snapshot.</returns>
    public PlayerPowerUpsMissingRuntimeFlags(
        bool hasMissingState,
        bool hasMissingPassiveToolsState,
        bool hasMissingDash,
        bool hasMissingBulletTimeState,
        bool hasMissingHealOverTimeState,
        bool hasMissingPassiveExplosionState,
        bool hasMissingPassiveHealState,
        bool hasMissingPassiveBulletTimeState,
        bool hasMissingLaserBeamState,
        bool hasMissingElementalTrailState,
        bool hasMissingBombRequestBuffer,
        bool hasMissingElementalTrailSegmentBuffer,
        bool hasMissingLaserBeamLaneBuffer,
        bool hasMissingLaserBeamPulseHitBuffer,
        bool hasMissingExplosionRequestBuffer,
        bool hasMissingPowerUpVfxRequestBuffer,
        bool hasMissingPowerUpVfxPrefabBindingBuffer,
        bool hasMissingPowerUpVfxCapConfig,
        bool hasMissingPowerUpCheatPresetEntryBuffer,
        bool hasMissingPowerUpCheatPresetSlotBuffer,
        bool hasMissingPowerUpCheatPresetPassiveBuffer,
        bool hasMissingPowerUpUnlockCatalogBuffer,
        bool hasMissingPowerUpCharacterTuningFormulaBuffer,
        bool hasMissingPowerUpTierDefinitionBuffer,
        bool hasMissingPowerUpTierEntryBuffer,
        bool hasMissingPowerUpTierEntryScalingBuffer,
        bool hasMissingMilestoneSelectionState,
        bool hasMissingMilestoneTimeScaleResumeState,
        bool hasMissingMilestoneSelectionOfferBuffer)
    {
        HasMissingState = hasMissingState;
        HasMissingPassiveToolsState = hasMissingPassiveToolsState;
        HasMissingDash = hasMissingDash;
        HasMissingBulletTimeState = hasMissingBulletTimeState;
        HasMissingHealOverTimeState = hasMissingHealOverTimeState;
        HasMissingPassiveExplosionState = hasMissingPassiveExplosionState;
        HasMissingPassiveHealState = hasMissingPassiveHealState;
        HasMissingPassiveBulletTimeState = hasMissingPassiveBulletTimeState;
        HasMissingLaserBeamState = hasMissingLaserBeamState;
        HasMissingElementalTrailState = hasMissingElementalTrailState;
        HasMissingBombRequestBuffer = hasMissingBombRequestBuffer;
        HasMissingElementalTrailSegmentBuffer = hasMissingElementalTrailSegmentBuffer;
        HasMissingLaserBeamLaneBuffer = hasMissingLaserBeamLaneBuffer;
        HasMissingLaserBeamPulseHitBuffer = hasMissingLaserBeamPulseHitBuffer;
        HasMissingExplosionRequestBuffer = hasMissingExplosionRequestBuffer;
        HasMissingPowerUpVfxRequestBuffer = hasMissingPowerUpVfxRequestBuffer;
        HasMissingPowerUpVfxPrefabBindingBuffer = hasMissingPowerUpVfxPrefabBindingBuffer;
        HasMissingPowerUpVfxCapConfig = hasMissingPowerUpVfxCapConfig;
        HasMissingPowerUpCheatPresetEntryBuffer = hasMissingPowerUpCheatPresetEntryBuffer;
        HasMissingPowerUpCheatPresetSlotBuffer = hasMissingPowerUpCheatPresetSlotBuffer;
        HasMissingPowerUpCheatPresetPassiveBuffer = hasMissingPowerUpCheatPresetPassiveBuffer;
        HasMissingPowerUpUnlockCatalogBuffer = hasMissingPowerUpUnlockCatalogBuffer;
        HasMissingPowerUpCharacterTuningFormulaBuffer = hasMissingPowerUpCharacterTuningFormulaBuffer;
        HasMissingPowerUpTierDefinitionBuffer = hasMissingPowerUpTierDefinitionBuffer;
        HasMissingPowerUpTierEntryBuffer = hasMissingPowerUpTierEntryBuffer;
        HasMissingPowerUpTierEntryScalingBuffer = hasMissingPowerUpTierEntryScalingBuffer;
        HasMissingMilestoneSelectionState = hasMissingMilestoneSelectionState;
        HasMissingMilestoneTimeScaleResumeState = hasMissingMilestoneTimeScaleResumeState;
        HasMissingMilestoneSelectionOfferBuffer = hasMissingMilestoneSelectionOfferBuffer;
    }
    #endregion

    #region Factories
    /// <summary>
    /// Evaluates all runtime bootstrap queries and returns a snapshot of missing data.
    /// </summary>
    /// <param name="missingStateQuery">Query for entities missing PlayerPowerUpsState.</param>
    /// <param name="missingPassiveToolsStateQuery">Query for entities missing the PlayerPassiveToolsStateElement snapshot buffer.</param>
    /// <param name="missingDashQuery">Query for entities missing PlayerDashState.</param>
    /// <param name="missingBulletTimeStateQuery">Query for entities missing PlayerBulletTimeState.</param>
    /// <param name="missingHealOverTimeStateQuery">Query for entities missing PlayerHealOverTimeState.</param>
    /// <param name="missingPassiveExplosionStateQuery">Query for entities missing PlayerPassiveExplosionState.</param>
    /// <param name="missingPassiveHealStateQuery">Query for entities missing PlayerPassiveHealState.</param>
    /// <param name="missingPassiveBulletTimeStateQuery">Query for entities missing PlayerPassiveBulletTimeState.</param>
    /// <param name="missingLaserBeamStateQuery">Query for entities missing PlayerLaserBeamState.</param>
    /// <param name="missingElementalTrailStateQuery">Query for entities missing PlayerElementalTrailState.</param>
    /// <param name="missingBombRequestBufferQuery">Query for entities missing PlayerBombSpawnRequest buffer.</param>
    /// <param name="missingElementalTrailSegmentBufferQuery">Query for entities missing PlayerElementalTrailSegmentElement buffer.</param>
    /// <param name="missingLaserBeamLaneBufferQuery">Query for entities missing PlayerLaserBeamLaneElement buffer.</param>
    /// <param name="missingLaserBeamPulseHitBufferQuery">Query for entities missing PlayerLaserBeamPulseHitElement buffer.</param>
    /// <param name="missingExplosionRequestBufferQuery">Query for entities missing PlayerExplosionRequest buffer.</param>
    /// <param name="missingPowerUpVfxRequestBufferQuery">Query for entities missing PlayerPowerUpVfxSpawnRequest buffer.</param>
    /// <param name="missingPowerUpVfxPrefabBindingBufferQuery">Query for entities missing PlayerPowerUpVfxPrefabBindingElement buffer.</param>
    /// <param name="missingPowerUpVfxCapConfigQuery">Query for entities missing PlayerPowerUpVfxCapConfig.</param>
    /// <param name="missingPowerUpCheatPresetEntryBufferQuery">Query for entities missing PlayerPowerUpCheatPresetEntry buffer.</param>
    /// <param name="missingPowerUpCheatPresetSlotBufferQuery">Query for entities missing PlayerPowerUpCheatPresetSlotElement buffer.</param>
    /// <param name="missingPowerUpCheatPresetPassiveBufferQuery">Query for entities missing PlayerPowerUpCheatPresetPassiveElement buffer.</param>
    /// <param name="missingPowerUpUnlockCatalogBufferQuery">Query for entities missing PlayerPowerUpUnlockCatalogElement buffer.</param>
    /// <param name="missingPowerUpCharacterTuningFormulaBufferQuery">Query for entities missing PlayerPowerUpCharacterTuningFormulaElement buffer.</param>
    /// <param name="missingPowerUpTierDefinitionBufferQuery">Query for entities missing PlayerPowerUpTierDefinitionElement buffer.</param>
    /// <param name="missingPowerUpTierEntryBufferQuery">Query for entities missing PlayerPowerUpTierEntryElement buffer.</param>
    /// <param name="missingPowerUpTierEntryScalingBufferQuery">Query for entities missing PlayerPowerUpTierEntryScalingElement buffer.</param>
    /// <param name="missingMilestoneSelectionStateQuery">Query for entities missing PlayerMilestonePowerUpSelectionState.</param>
    /// <param name="missingMilestoneTimeScaleResumeStateQuery">Query for entities missing PlayerMilestoneTimeScaleResumeState.</param>
    /// <param name="missingMilestoneSelectionOfferBufferQuery">Query for entities missing PlayerMilestonePowerUpSelectionOfferElement buffer.</param>
    /// <returns>A snapshot of all missing-runtime flags.</returns>
    public static PlayerPowerUpsMissingRuntimeFlags Create(
        in EntityQuery missingStateQuery,
        in EntityQuery missingPassiveToolsStateQuery,
        in EntityQuery missingDashQuery,
        in EntityQuery missingBulletTimeStateQuery,
        in EntityQuery missingHealOverTimeStateQuery,
        in EntityQuery missingPassiveExplosionStateQuery,
        in EntityQuery missingPassiveHealStateQuery,
        in EntityQuery missingPassiveBulletTimeStateQuery,
        in EntityQuery missingLaserBeamStateQuery,
        in EntityQuery missingElementalTrailStateQuery,
        in EntityQuery missingBombRequestBufferQuery,
        in EntityQuery missingElementalTrailSegmentBufferQuery,
        in EntityQuery missingLaserBeamLaneBufferQuery,
        in EntityQuery missingLaserBeamPulseHitBufferQuery,
        in EntityQuery missingExplosionRequestBufferQuery,
        in EntityQuery missingPowerUpVfxRequestBufferQuery,
        in EntityQuery missingPowerUpVfxPrefabBindingBufferQuery,
        in EntityQuery missingPowerUpVfxCapConfigQuery,
        in EntityQuery missingPowerUpCheatPresetEntryBufferQuery,
        in EntityQuery missingPowerUpCheatPresetSlotBufferQuery,
        in EntityQuery missingPowerUpCheatPresetPassiveBufferQuery,
        in EntityQuery missingPowerUpUnlockCatalogBufferQuery,
        in EntityQuery missingPowerUpCharacterTuningFormulaBufferQuery,
        in EntityQuery missingPowerUpTierDefinitionBufferQuery,
        in EntityQuery missingPowerUpTierEntryBufferQuery,
        in EntityQuery missingPowerUpTierEntryScalingBufferQuery,
        in EntityQuery missingMilestoneSelectionStateQuery,
        in EntityQuery missingMilestoneTimeScaleResumeStateQuery,
        in EntityQuery missingMilestoneSelectionOfferBufferQuery)
    {
        return new PlayerPowerUpsMissingRuntimeFlags(
            !missingStateQuery.IsEmptyIgnoreFilter,
            !missingPassiveToolsStateQuery.IsEmptyIgnoreFilter,
            !missingDashQuery.IsEmptyIgnoreFilter,
            !missingBulletTimeStateQuery.IsEmptyIgnoreFilter,
            !missingHealOverTimeStateQuery.IsEmptyIgnoreFilter,
            !missingPassiveExplosionStateQuery.IsEmptyIgnoreFilter,
            !missingPassiveHealStateQuery.IsEmptyIgnoreFilter,
            !missingPassiveBulletTimeStateQuery.IsEmptyIgnoreFilter,
            !missingLaserBeamStateQuery.IsEmptyIgnoreFilter,
            !missingElementalTrailStateQuery.IsEmptyIgnoreFilter,
            !missingBombRequestBufferQuery.IsEmptyIgnoreFilter,
            !missingElementalTrailSegmentBufferQuery.IsEmptyIgnoreFilter,
            !missingLaserBeamLaneBufferQuery.IsEmptyIgnoreFilter,
            !missingLaserBeamPulseHitBufferQuery.IsEmptyIgnoreFilter,
            !missingExplosionRequestBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpVfxRequestBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpVfxPrefabBindingBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpVfxCapConfigQuery.IsEmptyIgnoreFilter,
            !missingPowerUpCheatPresetEntryBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpCheatPresetSlotBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpCheatPresetPassiveBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpUnlockCatalogBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpCharacterTuningFormulaBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpTierDefinitionBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpTierEntryBufferQuery.IsEmptyIgnoreFilter,
            !missingPowerUpTierEntryScalingBufferQuery.IsEmptyIgnoreFilter,
            !missingMilestoneSelectionStateQuery.IsEmptyIgnoreFilter,
            !missingMilestoneTimeScaleResumeStateQuery.IsEmptyIgnoreFilter,
            !missingMilestoneSelectionOfferBufferQuery.IsEmptyIgnoreFilter);
    }
    #endregion

    #endregion
}
