using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Keeps runtime-scaled configs synchronized with scalable-stat changes between authoring bake and gameplay systems.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLevelUpSystem))]
[UpdateAfter(typeof(PlayerMilestonePowerUpSelectionResolveSystem))]
[UpdateAfter(typeof(PlayerPowerUpCharacterTuningInitializeSystem))]
[UpdateBefore(typeof(PlayerPowerUpRechargeSystem))]
[UpdateBefore(typeof(PlayerMovementDirectionSystem))]
[UpdateBefore(typeof(PlayerLookDirectionSystem))]
[UpdateBefore(typeof(PlayerLookMultiplierSystem))]
[UpdateBefore(typeof(PlayerPassiveBulletTimeSystem))]
[UpdateBefore(typeof(PlayerPassiveExplosionSystem))]
[UpdateBefore(typeof(PlayerPassiveHealSystem))]
[UpdateBefore(typeof(PlayerPowerUpActivationSystem))]
[UpdateBefore(typeof(PlayerMovementSpeedSystem))]
[UpdateBefore(typeof(PlayerShootingIntentSystem))]
public partial struct PlayerRuntimeScalingSyncSystem : ISystem
{
    #region Fields
    private EntityQuery runtimeScalingQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to rebuild scaled controller, progression, and power-up configs.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        runtimeScalingQuery = new EntityQueryBuilder(Allocator.Temp)
                                 .WithAll<PlayerRuntimeScalingState>()
                                 .WithAll<PlayerScalableStatElement>()
                                 .WithAll<PlayerRoomRewardTemporaryModifierElement>()
                                 .WithAll<PlayerRoomRewardTemporaryState>()
                                 .WithAll<PlayerRuntimeControllerScalingElement>()
                                 .WithAll<PlayerBaseMovementConfig>()
                                 .WithAll<PlayerRuntimeMovementConfig>()
                                 .WithAll<PlayerBaseLookConfig>()
                                 .WithAll<PlayerRuntimeLookConfig>()
                                 .WithAll<PlayerBaseCameraConfig>()
                                 .WithAll<PlayerRuntimeCameraConfig>()
                                 .WithAll<PlayerBaseShootingConfig>()
                                 .WithAll<PlayerRuntimeShootingConfig>()
                                 .WithAll<PlayerBaseShootingAppliedElementSlot>()
                                 .WithAll<PlayerRuntimeShootingAppliedElementSlot>()
                                 .WithAll<PlayerBaseHealthStatisticsConfig>()
                                 .WithAll<PlayerRuntimeHealthStatisticsConfig>()
                                 .WithAll<PlayerBaseDeathAnimationConfig>()
                                 .WithAll<PlayerDeathAnimationConfig>()
                                 .WithAll<PlayerRuntimeDeathAnimationScalingElement>()
                                 .WithAll<PlayerRuntimeProgressionScalingElement>()
                                 .WithAll<PlayerBaseGamePhaseElement>()
                                 .WithAll<PlayerRuntimeGamePhaseElement>()
                                 .WithAll<PlayerBaseComboCounterConfig>()
                                 .WithAll<PlayerRuntimeComboCounterConfig>()
                                 .WithAll<PlayerBaseComboRankElement>()
                                 .WithAll<PlayerRuntimeComboRankElement>()
                                 .WithAll<PlayerBaseComboPassiveUnlockElement>()
                                 .WithAll<PlayerRuntimeComboPassiveUnlockElement>()
                                 .WithAll<PlayerRuntimeComboCounterScalingElement>()
                                 .WithAll<PlayerComboCounterState>()
                                 .WithAll<PlayerPowerUpCharacterTuningFormulaElement>()
                                 .WithAll<PlayerPowerUpBaseConfigElement>()
                                 .WithAll<PlayerRuntimePowerUpScalingElement>()
                                 .WithAll<PlayerPowerUpsConfigElement>()
                                 .WithAll<PlayerPowerUpUnlockCatalogElement>()
                                 .WithAll<EquippedPassiveToolElement>()
                                 .WithAll<PlayerPassiveToolsStateElement>()
                                 .WithAll<PlayerHealth>()
                                 .WithAll<PlayerShield>()
                                 .WithAll<PlayerProgressionConfig>()
                                 .WithAll<PlayerExperience>()
                                 .WithAll<PlayerLevel>()
                                 .WithAll<PlayerExperienceCollection>()
                                 .Build(ref state);
        state.RequireForUpdate(runtimeScalingQuery);
    }

    /// <summary>
    /// Rebuilds runtime-scaled configs only when the scalable-stat hash changed since the previous applied sample.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(false);
        BufferLookup<PlayerRoomRewardTemporaryModifierElement> temporaryModifiersLookup =
            SystemAPI.GetBufferLookup<PlayerRoomRewardTemporaryModifierElement>(true);
        ComponentLookup<PlayerRoomRewardTemporaryState> temporaryStateLookup =
            SystemAPI.GetComponentLookup<PlayerRoomRewardTemporaryState>(true);
        BufferLookup<PlayerRuntimeControllerScalingElement> controllerScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeControllerScalingElement>(true);
        ComponentLookup<PlayerBaseMovementConfig> baseMovementLookup = SystemAPI.GetComponentLookup<PlayerBaseMovementConfig>(true);
        ComponentLookup<PlayerRuntimeMovementConfig> runtimeMovementLookup = SystemAPI.GetComponentLookup<PlayerRuntimeMovementConfig>(false);
        ComponentLookup<PlayerBaseLookConfig> baseLookLookup = SystemAPI.GetComponentLookup<PlayerBaseLookConfig>(true);
        ComponentLookup<PlayerRuntimeLookConfig> runtimeLookLookup = SystemAPI.GetComponentLookup<PlayerRuntimeLookConfig>(false);
        ComponentLookup<PlayerBaseCameraConfig> baseCameraLookup = SystemAPI.GetComponentLookup<PlayerBaseCameraConfig>(true);
        ComponentLookup<PlayerRuntimeCameraConfig> runtimeCameraLookup = SystemAPI.GetComponentLookup<PlayerRuntimeCameraConfig>(false);
        ComponentLookup<PlayerBaseShootingConfig> baseShootingLookup = SystemAPI.GetComponentLookup<PlayerBaseShootingConfig>(true);
        ComponentLookup<PlayerRuntimeShootingConfig> runtimeShootingLookup = SystemAPI.GetComponentLookup<PlayerRuntimeShootingConfig>(false);
        BufferLookup<PlayerBaseShootingAppliedElementSlot> baseAppliedElementSlotsLookup = SystemAPI.GetBufferLookup<PlayerBaseShootingAppliedElementSlot>(true);
        BufferLookup<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlotsLookup = SystemAPI.GetBufferLookup<PlayerRuntimeShootingAppliedElementSlot>(false);
        ComponentLookup<PlayerBaseHealthStatisticsConfig> baseHealthLookup = SystemAPI.GetComponentLookup<PlayerBaseHealthStatisticsConfig>(true);
        ComponentLookup<PlayerRuntimeHealthStatisticsConfig> runtimeHealthLookup = SystemAPI.GetComponentLookup<PlayerRuntimeHealthStatisticsConfig>(false);
        ComponentLookup<PlayerBaseDeathAnimationConfig> baseDeathAnimationLookup = SystemAPI.GetComponentLookup<PlayerBaseDeathAnimationConfig>(true);
        ComponentLookup<PlayerDeathAnimationConfig> runtimeDeathAnimationLookup = SystemAPI.GetComponentLookup<PlayerDeathAnimationConfig>(false);
        BufferLookup<PlayerRuntimeDeathAnimationScalingElement> deathAnimationScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeDeathAnimationScalingElement>(true);
        BufferLookup<PlayerRuntimeProgressionScalingElement> progressionScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeProgressionScalingElement>(true);
        BufferLookup<PlayerBaseGamePhaseElement> baseGamePhasesLookup = SystemAPI.GetBufferLookup<PlayerBaseGamePhaseElement>(true);
        BufferLookup<PlayerRuntimeGamePhaseElement> runtimeGamePhasesLookup = SystemAPI.GetBufferLookup<PlayerRuntimeGamePhaseElement>(false);
        ComponentLookup<PlayerBaseComboCounterConfig> baseComboConfigLookup = SystemAPI.GetComponentLookup<PlayerBaseComboCounterConfig>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> runtimeComboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(false);
        BufferLookup<PlayerBaseComboRankElement> baseComboRanksLookup = SystemAPI.GetBufferLookup<PlayerBaseComboRankElement>(true);
        BufferLookup<PlayerRuntimeComboRankElement> runtimeComboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(false);
        BufferLookup<PlayerBaseComboPassiveUnlockElement> baseComboPassiveUnlocksLookup = SystemAPI.GetBufferLookup<PlayerBaseComboPassiveUnlockElement>(true);
        BufferLookup<PlayerRuntimeComboPassiveUnlockElement> runtimeComboPassiveUnlocksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboPassiveUnlockElement>(false);
        BufferLookup<PlayerRuntimeComboCounterScalingElement> comboScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboCounterScalingElement>(true);
        ComponentLookup<PlayerComboCounterState> comboCounterStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(false);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);
        BufferLookup<PlayerPowerUpBaseConfigElement> basePowerUpConfigsLookup = SystemAPI.GetBufferLookup<PlayerPowerUpBaseConfigElement>(false);
        BufferLookup<PlayerRuntimePowerUpScalingElement> powerUpScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimePowerUpScalingElement>(true);
        BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup = SystemAPI.GetBufferLookup<PlayerPowerUpsConfigElement>(false);
        BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(false);
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);
        ComponentLookup<PlayerProgressionConfig> progressionConfigLookup = SystemAPI.GetComponentLookup<PlayerProgressionConfig>(true);
        ComponentLookup<PlayerExperience> experienceLookup = SystemAPI.GetComponentLookup<PlayerExperience>(false);
        ComponentLookup<PlayerLevel> levelLookup = SystemAPI.GetComponentLookup<PlayerLevel>(false);
        ComponentLookup<PlayerExperienceCollection> experienceCollectionLookup = SystemAPI.GetComponentLookup<PlayerExperienceCollection>(false);
        ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup = SystemAPI.GetComponentLookup<PlayerRuntimeScalingState>(false);
        NativeArray<Entity> entities = runtimeScalingQuery.ToEntityArray(Allocator.Temp);

        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            PlayerRuntimeScalingRefreshUtility.TryApplyForEntity(entities[entityIndex],
                                                                 scalableStatsLookup,
                                                                 temporaryModifiersLookup,
                                                                 temporaryStateLookup,
                                                                 controllerScalingLookup,
                                                                 baseMovementLookup,
                                                                 runtimeMovementLookup,
                                                                 baseLookLookup,
                                                                 runtimeLookLookup,
                                                                 baseCameraLookup,
                                                                 runtimeCameraLookup,
                                                                 baseShootingLookup,
                                                                 runtimeShootingLookup,
                                                                 baseAppliedElementSlotsLookup,
                                                                 runtimeAppliedElementSlotsLookup,
                                                                 baseHealthLookup,
                                                                 runtimeHealthLookup,
                                                                 baseDeathAnimationLookup,
                                                                 runtimeDeathAnimationLookup,
                                                                 deathAnimationScalingLookup,
                                                                 progressionScalingLookup,
                                                                 baseGamePhasesLookup,
                                                                 runtimeGamePhasesLookup,
                                                                 baseComboConfigLookup,
                                                                 runtimeComboConfigLookup,
                                                                 baseComboRanksLookup,
                                                                 runtimeComboRanksLookup,
                                                                 baseComboPassiveUnlocksLookup,
                                                                 runtimeComboPassiveUnlocksLookup,
                                                                 comboScalingLookup,
                                                                 comboCounterStateLookup,
                                                                 characterTuningFormulaLookup,
                                                                 basePowerUpConfigsLookup,
                                                                 powerUpScalingLookup,
                                                                 powerUpsConfigLookup,
                                                                 unlockCatalogLookup,
                                                                 equippedPassiveToolsLookup,
                                                                 passiveToolsStateLookup,
                                                                 healthLookup,
                                                                 shieldLookup,
                                                                 progressionConfigLookup,
                                                                 experienceLookup,
                                                                 levelLookup,
                                                                 experienceCollectionLookup,
                                                                 runtimeScalingStateLookup,
                                                                 false);
        }

        entities.Dispose();
    }
    #endregion

    #endregion
}
