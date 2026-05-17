using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Initializes missing runtime state and buffers required by power-up systems.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerPowerUpsInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingStateQuery;
    private EntityQuery missingPassiveToolsStateQuery;
    private EntityQuery missingDashQuery;
    private EntityQuery missingBulletTimeStateQuery;
    private EntityQuery missingHealOverTimeStateQuery;
    private EntityQuery missingPassiveExplosionStateQuery;
    private EntityQuery missingPassiveHealStateQuery;
    private EntityQuery missingPassiveBulletTimeStateQuery;
    private EntityQuery missingLaserBeamStateQuery;
    private EntityQuery missingElementalTrailStateQuery;
    private EntityQuery missingElementalTrailAttachedVfxStateQuery;
    private EntityQuery missingBombRequestBufferQuery;
    private EntityQuery missingElementalTrailSegmentBufferQuery;
    private EntityQuery missingLaserBeamLaneBufferQuery;
    private EntityQuery missingLaserBeamPulseHitBufferQuery;
    private EntityQuery missingExplosionRequestBufferQuery;
    private EntityQuery missingPowerUpVfxRequestBufferQuery;
    private EntityQuery missingPowerUpVfxPoolBufferQuery;
    private EntityQuery missingPowerUpVfxPrefabBindingBufferQuery;
    private EntityQuery missingPowerUpVfxCapConfigQuery;
    private EntityQuery missingPowerUpCheatBufferQuery;
    private EntityQuery missingPowerUpCheatPresetEntryBufferQuery;
    private EntityQuery missingPowerUpCheatPresetPassiveBufferQuery;
    private EntityQuery missingPowerUpUnlockCatalogBufferQuery;
    private EntityQuery missingPowerUpCharacterTuningFormulaBufferQuery;
    private EntityQuery missingPowerUpTierDefinitionBufferQuery;
    private EntityQuery missingPowerUpTierEntryBufferQuery;
    private EntityQuery missingPowerUpTierEntryScalingBufferQuery;
    private EntityQuery missingMilestoneSelectionStateQuery;
    private EntityQuery missingMilestoneTimeScaleResumeStateQuery;
    private EntityQuery missingMilestoneSelectionOfferBufferQuery;
    private EntityQuery missingMilestoneSelectionCommandBufferQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Builds bootstrap queries that detect missing power-up runtime data.
    /// </summary>
    /// <param name="state">System state used to register required singletons and queries.</param>

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfig>();

        missingStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpsState>()
            .Build();

        missingPassiveToolsStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPassiveToolsState>()
            .Build();

        missingDashQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerDashState>()
            .Build();

        missingBulletTimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerBulletTimeState>()
            .Build();

        missingHealOverTimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerHealOverTimeState>()
            .Build();

        missingPassiveExplosionStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPassiveExplosionState>()
            .Build();

        missingPassiveHealStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPassiveHealState>()
            .Build();

        missingPassiveBulletTimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPassiveBulletTimeState>()
            .Build();

        missingLaserBeamStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerLaserBeamState>()
            .Build();

        missingElementalTrailStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerElementalTrailState>()
            .Build();

        missingElementalTrailAttachedVfxStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerElementalTrailAttachedVfxState>()
            .Build();

        missingBombRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerBombSpawnRequest>()
            .Build();

        missingElementalTrailSegmentBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerElementalTrailSegmentElement>()
            .Build();

        missingLaserBeamLaneBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerLaserBeamLaneElement>()
            .Build();

        missingLaserBeamPulseHitBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerLaserBeamPulseHitElement>()
            .Build();

        missingExplosionRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerExplosionRequest>()
            .Build();

        missingPowerUpVfxRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpVfxSpawnRequest>()
            .Build();

        missingPowerUpVfxPoolBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpVfxPoolElement>()
            .Build();

        missingPowerUpVfxPrefabBindingBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpVfxPrefabBindingElement>()
            .Build();

        missingPowerUpVfxCapConfigQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpVfxCapConfig>()
            .Build();

        missingPowerUpCheatBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpCheatCommand>()
            .Build();

        missingPowerUpCheatPresetEntryBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpCheatPresetEntry>()
            .Build();

        missingPowerUpCheatPresetPassiveBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpCheatPresetPassiveElement>()
            .Build();

        missingPowerUpUnlockCatalogBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpUnlockCatalogElement>()
            .Build();

        missingPowerUpCharacterTuningFormulaBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpCharacterTuningFormulaElement>()
            .Build();

        missingPowerUpTierDefinitionBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpTierDefinitionElement>()
            .Build();

        missingPowerUpTierEntryBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpTierEntryElement>()
            .Build();

        missingPowerUpTierEntryScalingBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpTierEntryScalingElement>()
            .Build();

        missingMilestoneSelectionStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerMilestonePowerUpSelectionState>()
            .Build();

        missingMilestoneTimeScaleResumeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerMilestoneTimeScaleResumeState>()
            .Build();

        missingMilestoneSelectionOfferBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerMilestonePowerUpSelectionOfferElement>()
            .Build();

        missingMilestoneSelectionCommandBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerMilestonePowerUpSelectionCommand>()
            .Build();
    }

    /// <summary>
    /// Adds missing runtime state/buffers to every entity with PlayerPowerUpsConfig and disables the system once bootstrap completes.
    /// </summary>
    /// <param name="state">System state used to query and write ECS runtime data.</param>

    public void OnUpdate(ref SystemState state)
    {
        PlayerPowerUpsMissingRuntimeFlags missingFlags = PlayerPowerUpsMissingRuntimeFlags.Create(
            in missingStateQuery,
            in missingPassiveToolsStateQuery,
            in missingDashQuery,
            in missingBulletTimeStateQuery,
            in missingHealOverTimeStateQuery,
            in missingPassiveExplosionStateQuery,
            in missingPassiveHealStateQuery,
            in missingPassiveBulletTimeStateQuery,
            in missingLaserBeamStateQuery,
            in missingElementalTrailStateQuery,
            in missingElementalTrailAttachedVfxStateQuery,
            in missingBombRequestBufferQuery,
            in missingElementalTrailSegmentBufferQuery,
            in missingLaserBeamLaneBufferQuery,
            in missingLaserBeamPulseHitBufferQuery,
            in missingExplosionRequestBufferQuery,
            in missingPowerUpVfxRequestBufferQuery,
            in missingPowerUpVfxPoolBufferQuery,
            in missingPowerUpVfxPrefabBindingBufferQuery,
            in missingPowerUpVfxCapConfigQuery,
            in missingPowerUpCheatBufferQuery,
            in missingPowerUpCheatPresetEntryBufferQuery,
            in missingPowerUpCheatPresetPassiveBufferQuery,
            in missingPowerUpUnlockCatalogBufferQuery,
            in missingPowerUpCharacterTuningFormulaBufferQuery,
            in missingPowerUpTierDefinitionBufferQuery,
            in missingPowerUpTierEntryBufferQuery,
            in missingPowerUpTierEntryScalingBufferQuery,
            in missingMilestoneSelectionStateQuery,
            in missingMilestoneTimeScaleResumeStateQuery,
            in missingMilestoneSelectionOfferBufferQuery,
            in missingMilestoneSelectionCommandBufferQuery);

        if (!missingFlags.HasAnyMissing)
            return;

        uint currentKillCount = 0u;

        if (SystemAPI.TryGetSingleton<GlobalEnemyKillCounter>(out GlobalEnemyKillCounter killCounter))
        {
            currentKillCount = killCounter.TotalKilled;
        }

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(true);

        if (missingFlags.HasMissingState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingState(ref commandBuffer, in missingStateQuery, currentKillCount);
        }

        if (missingFlags.HasMissingPassiveToolsState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPassiveToolsState(ref commandBuffer, in missingPassiveToolsStateQuery, in equippedPassiveToolsLookup);
        }

        if (missingFlags.HasMissingDash)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingDashState(ref commandBuffer, in missingDashQuery);
        }

        if (missingFlags.HasMissingBulletTimeState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingBulletTimeState(ref commandBuffer, in missingBulletTimeStateQuery);
        }

        if (missingFlags.HasMissingHealOverTimeState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingHealOverTimeState(ref commandBuffer, in missingHealOverTimeStateQuery);
        }

        if (missingFlags.HasMissingPassiveExplosionState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPassiveExplosionState(ref commandBuffer, in missingPassiveExplosionStateQuery);
        }

        if (missingFlags.HasMissingPassiveHealState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPassiveHealState(ref commandBuffer, in missingPassiveHealStateQuery);
        }

        if (missingFlags.HasMissingPassiveBulletTimeState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPassiveBulletTimeState(ref commandBuffer, in missingPassiveBulletTimeStateQuery);
        }

        if (missingFlags.HasMissingLaserBeamState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingLaserBeamState(ref commandBuffer, in missingLaserBeamStateQuery);
        }

        if (missingFlags.HasMissingElementalTrailState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingElementalTrailState(ref commandBuffer, in missingElementalTrailStateQuery);
        }

        if (missingFlags.HasMissingElementalTrailAttachedVfxState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingElementalTrailAttachedVfxState(ref commandBuffer, in missingElementalTrailAttachedVfxStateQuery);
        }

        if (missingFlags.HasMissingBombRequestBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingBombRequestBuffers(ref commandBuffer, in missingBombRequestBufferQuery);
        }

        if (missingFlags.HasMissingElementalTrailSegmentBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingElementalTrailSegmentBuffers(ref commandBuffer, in missingElementalTrailSegmentBufferQuery);
        }

        if (missingFlags.HasMissingLaserBeamLaneBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingLaserBeamLaneBuffers(ref commandBuffer, in missingLaserBeamLaneBufferQuery);
        }

        if (missingFlags.HasMissingLaserBeamPulseHitBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingLaserBeamPulseHitBuffers(ref commandBuffer, in missingLaserBeamPulseHitBufferQuery);
        }

        if (missingFlags.HasMissingExplosionRequestBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingExplosionRequestBuffers(ref commandBuffer, in missingExplosionRequestBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpVfxRequestBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpVfxRequestBuffers(ref commandBuffer, in missingPowerUpVfxRequestBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpVfxPoolBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpVfxPoolBuffers(ref commandBuffer, in missingPowerUpVfxPoolBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpVfxPrefabBindingBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpVfxPrefabBindingBuffers(ref commandBuffer, in missingPowerUpVfxPrefabBindingBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpVfxCapConfig)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpVfxCapConfig(ref commandBuffer, in missingPowerUpVfxCapConfigQuery);
        }

        if (missingFlags.HasMissingPowerUpCheatBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpCheatBuffers(ref commandBuffer, in missingPowerUpCheatBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpCheatPresetEntryBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpCheatPresetEntryBuffers(ref commandBuffer, in missingPowerUpCheatPresetEntryBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpCheatPresetPassiveBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpCheatPresetPassiveBuffers(ref commandBuffer, in missingPowerUpCheatPresetPassiveBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpUnlockCatalogBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpUnlockCatalogBuffers(ref commandBuffer, in missingPowerUpUnlockCatalogBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpCharacterTuningFormulaBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpCharacterTuningFormulaBuffers(ref commandBuffer,
                                                                                                    in missingPowerUpCharacterTuningFormulaBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpTierDefinitionBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpTierDefinitionBuffers(ref commandBuffer, in missingPowerUpTierDefinitionBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpTierEntryBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpTierEntryBuffers(ref commandBuffer, in missingPowerUpTierEntryBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpTierEntryScalingBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpTierEntryScalingBuffers(ref commandBuffer,
                                                                                              in missingPowerUpTierEntryScalingBufferQuery);
        }

        if (missingFlags.HasMissingMilestoneSelectionState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingMilestoneSelectionState(ref commandBuffer, in missingMilestoneSelectionStateQuery);
        }

        if (missingFlags.HasMissingMilestoneTimeScaleResumeState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingMilestoneTimeScaleResumeState(ref commandBuffer, in missingMilestoneTimeScaleResumeStateQuery);
        }

        if (missingFlags.HasMissingMilestoneSelectionOfferBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingMilestoneSelectionOfferBuffers(ref commandBuffer, in missingMilestoneSelectionOfferBufferQuery);
        }

        if (missingFlags.HasMissingMilestoneSelectionCommandBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingMilestoneSelectionCommandBuffers(ref commandBuffer, in missingMilestoneSelectionCommandBufferQuery);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();

        PlayerPowerUpsMissingRuntimeFlags remainingMissingFlags = PlayerPowerUpsMissingRuntimeFlags.Create(
            in missingStateQuery,
            in missingPassiveToolsStateQuery,
            in missingDashQuery,
            in missingBulletTimeStateQuery,
            in missingHealOverTimeStateQuery,
            in missingPassiveExplosionStateQuery,
            in missingPassiveHealStateQuery,
            in missingPassiveBulletTimeStateQuery,
            in missingLaserBeamStateQuery,
            in missingElementalTrailStateQuery,
            in missingElementalTrailAttachedVfxStateQuery,
            in missingBombRequestBufferQuery,
            in missingElementalTrailSegmentBufferQuery,
            in missingLaserBeamLaneBufferQuery,
            in missingLaserBeamPulseHitBufferQuery,
            in missingExplosionRequestBufferQuery,
            in missingPowerUpVfxRequestBufferQuery,
            in missingPowerUpVfxPoolBufferQuery,
            in missingPowerUpVfxPrefabBindingBufferQuery,
            in missingPowerUpVfxCapConfigQuery,
            in missingPowerUpCheatBufferQuery,
            in missingPowerUpCheatPresetEntryBufferQuery,
            in missingPowerUpCheatPresetPassiveBufferQuery,
            in missingPowerUpUnlockCatalogBufferQuery,
            in missingPowerUpCharacterTuningFormulaBufferQuery,
            in missingPowerUpTierDefinitionBufferQuery,
            in missingPowerUpTierEntryBufferQuery,
            in missingPowerUpTierEntryScalingBufferQuery,
            in missingMilestoneSelectionStateQuery,
            in missingMilestoneTimeScaleResumeStateQuery,
            in missingMilestoneSelectionOfferBufferQuery,
            in missingMilestoneSelectionCommandBufferQuery);

    }
    #endregion
    #endregion
}
