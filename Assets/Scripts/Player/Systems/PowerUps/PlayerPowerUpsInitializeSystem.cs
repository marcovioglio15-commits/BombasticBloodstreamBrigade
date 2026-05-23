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
    private EntityQuery missingOrbitalProjectionRequestBufferQuery;
    private EntityQuery missingOrbitalProjectionPrefabBindingBufferQuery;
    private EntityQuery missingElementalTrailSegmentBufferQuery;
    private EntityQuery missingLaserBeamStormTickPulseBufferQuery;
    private EntityQuery missingLaserBeamLaneBufferQuery;
    private EntityQuery missingLaserBeamPulseHitBufferQuery;
    private EntityQuery missingExplosionRequestBufferQuery;
    private EntityQuery missingPowerUpVfxRequestBufferQuery;
    private EntityQuery missingPowerUpVfxPrefabBindingBufferQuery;
    private EntityQuery missingPowerUpVfxCapConfigQuery;
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
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Builds bootstrap queries that detect missing power-up runtime data.
    /// </summary>
    /// <param name="state">System state used to register required singletons and queries.</param>

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();

        missingStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpsState>()
            .Build();

        missingPassiveToolsStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPassiveToolsStateElement>()
            .Build();

        missingDashQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerDashState>()
            .Build();

        missingBulletTimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerBulletTimeState>()
            .Build();

        missingHealOverTimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerHealOverTimeState>()
            .Build();

        missingPassiveExplosionStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPassiveExplosionState>()
            .Build();

        missingPassiveHealStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPassiveHealState>()
            .Build();

        missingPassiveBulletTimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPassiveBulletTimeState>()
            .Build();

        missingLaserBeamStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerLaserBeamState>()
            .Build();

        missingElementalTrailStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerElementalTrailState>()
            .Build();

        missingElementalTrailAttachedVfxStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerElementalTrailAttachedVfxState>()
            .Build();

        missingBombRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerBombSpawnRequest>()
            .Build();

        missingOrbitalProjectionRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerOrbitalProjectionSpawnRequest>()
            .Build();

        missingOrbitalProjectionPrefabBindingBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerOrbitalProjectionPrefabElement>()
            .Build();

        missingElementalTrailSegmentBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerElementalTrailSegmentElement>()
            .Build();

        missingLaserBeamStormTickPulseBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerLaserBeamStormTickPulse>()
            .Build();

        missingLaserBeamLaneBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerLaserBeamLaneElement>()
            .Build();

        missingLaserBeamPulseHitBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerLaserBeamPulseHitElement>()
            .Build();

        missingExplosionRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerExplosionRequest>()
            .Build();

        missingPowerUpVfxRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpVfxSpawnRequest>()
            .Build();

        missingPowerUpVfxPrefabBindingBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpVfxPrefabBindingElement>()
            .Build();

        missingPowerUpVfxCapConfigQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpVfxCapConfig>()
            .Build();

        missingPowerUpCheatPresetEntryBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpCheatPresetEntry>()
            .Build();

        missingPowerUpCheatPresetPassiveBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpCheatPresetPassiveElement>()
            .Build();

        missingPowerUpUnlockCatalogBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpUnlockCatalogElement>()
            .Build();

        missingPowerUpCharacterTuningFormulaBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpCharacterTuningFormulaElement>()
            .Build();

        missingPowerUpTierDefinitionBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpTierDefinitionElement>()
            .Build();

        missingPowerUpTierEntryBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpTierEntryElement>()
            .Build();

        missingPowerUpTierEntryScalingBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerPowerUpTierEntryScalingElement>()
            .Build();

        missingMilestoneSelectionStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerMilestonePowerUpSelectionState>()
            .Build();

        missingMilestoneTimeScaleResumeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerMilestoneTimeScaleResumeState>()
            .Build();

        missingMilestoneSelectionOfferBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfigElement>()
            .WithNone<PlayerMilestonePowerUpSelectionOfferElement>()
            .Build();

    }

    /// <summary>
    /// Adds missing runtime state/buffers to every entity with an external PlayerPowerUpsConfig snapshot and disables the system once bootstrap completes.
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
            in missingPowerUpVfxPrefabBindingBufferQuery,
            in missingPowerUpVfxCapConfigQuery,
            in missingPowerUpCheatPresetEntryBufferQuery,
            in missingPowerUpCheatPresetPassiveBufferQuery,
            in missingPowerUpUnlockCatalogBufferQuery,
            in missingPowerUpCharacterTuningFormulaBufferQuery,
            in missingPowerUpTierDefinitionBufferQuery,
            in missingPowerUpTierEntryBufferQuery,
            in missingPowerUpTierEntryScalingBufferQuery,
            in missingMilestoneSelectionStateQuery,
            in missingMilestoneTimeScaleResumeStateQuery,
            in missingMilestoneSelectionOfferBufferQuery);

        bool hasMissingOrbitalProjectionRequestBuffer = !missingOrbitalProjectionRequestBufferQuery.IsEmptyIgnoreFilter;
        bool hasMissingOrbitalProjectionPrefabBindingBuffer = !missingOrbitalProjectionPrefabBindingBufferQuery.IsEmptyIgnoreFilter;
        bool hasMissingLaserBeamStormTickPulseBuffer = !missingLaserBeamStormTickPulseBufferQuery.IsEmptyIgnoreFilter;

        if (!missingFlags.HasAnyMissing &&
            !hasMissingOrbitalProjectionRequestBuffer &&
            !hasMissingOrbitalProjectionPrefabBindingBuffer &&
            !hasMissingLaserBeamStormTickPulseBuffer)
        {
            return;
        }

        uint currentKillCount = 0u;

        if (SystemAPI.TryGetSingleton<GlobalEnemyKillCounter>(out GlobalEnemyKillCounter killCounter))
        {
            currentKillCount = killCounter.TotalKilled;
        }

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup = SystemAPI.GetBufferLookup<PlayerPowerUpsConfigElement>(true);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(true);

        if (missingFlags.HasMissingState)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingState(ref commandBuffer,
                                                                     in missingStateQuery,
                                                                     in powerUpsConfigLookup,
                                                                     currentKillCount);
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

        if (hasMissingOrbitalProjectionRequestBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingOrbitalProjectionRequestBuffers(ref commandBuffer, in missingOrbitalProjectionRequestBufferQuery);
        }

        if (hasMissingOrbitalProjectionPrefabBindingBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingOrbitalProjectionPrefabBindingBuffers(ref commandBuffer,
                                                                                                     in missingOrbitalProjectionPrefabBindingBufferQuery);
        }

        if (missingFlags.HasMissingElementalTrailSegmentBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingElementalTrailSegmentBuffers(ref commandBuffer, in missingElementalTrailSegmentBufferQuery);
        }

        if (hasMissingLaserBeamStormTickPulseBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingLaserBeamStormTickPulseBuffers(ref commandBuffer,
                                                                                              in missingLaserBeamStormTickPulseBufferQuery);
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

        if (missingFlags.HasMissingPowerUpVfxPrefabBindingBuffer)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpVfxPrefabBindingBuffers(ref commandBuffer, in missingPowerUpVfxPrefabBindingBufferQuery);
        }

        if (missingFlags.HasMissingPowerUpVfxCapConfig)
        {
            PlayerPowerUpsInitializeBootstrapUtility.AddMissingPowerUpVfxCapConfig(ref commandBuffer, in missingPowerUpVfxCapConfigQuery);
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

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();

    }
    #endregion
    #endregion
}
