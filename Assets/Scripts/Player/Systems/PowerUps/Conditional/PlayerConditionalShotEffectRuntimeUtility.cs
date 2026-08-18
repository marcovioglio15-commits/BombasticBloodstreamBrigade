using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Qualifies shot-conditioned passive and toggleable-active instances, then applies their effects to one projectile volley.
/// </summary>
internal static class PlayerConditionalShotEffectRuntimeUtility
{
    #region Methods

    #region Aggregation
    /// <summary>
    /// Qualifies conditional passive and active-toggle instances for one base-shot volley, then merges only their eligible effects.
    /// </summary>
    /// <param name="equippedPassiveTools">Mutable equipped-passive buffer containing independent cadence states.</param>
    /// <param name="powerUpsConfig">Current active-slot configuration snapshot.</param>
    /// <param name="powerUpsState">Mutable active-slot state containing toggle activity and cadence states.</param>
    /// <param name="localTransform">Current player transform used by qualified object-spawn effects.</param>
    /// <param name="lookState">Current look state used to orient qualified object-spawn effects.</param>
    /// <param name="runtimeShootingConfig">Current authoritative shooting config used as the conditional Character Tuning baseline.</param>
    /// <param name="conditionalCharacterTuningContext">Runtime formula inputs used to build shot-local Character Tuning overlays.</param>
    /// <param name="conditionalShotContextInitialized">Mutable flag tracking whether scalable stats were copied for this shot.</param>
    /// <param name="shotShootingConfig">Mutable shot-local shooting config rebuilt when a qualified source supplies Character Tuning.</param>
    /// <param name="appliedElementSlots">Current elemental slots used as the standalone charged-beam template baseline.</param>
    /// <param name="playerEntity">Player entity owning spawned objects.</param>
    /// <param name="bombRequests">Output buffer receiving qualified object-spawn requests.</param>
    /// <param name="laserBeamState">Mutable laser state receiving qualified standalone charged beams.</param>
    /// <param name="shotPassiveToolsState">Per-volley passive snapshot updated with eligible sibling effects.</param>
    public static void AccumulateQualifiedEffects(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                  in PlayerPowerUpsConfig powerUpsConfig,
                                                  ref PlayerPowerUpsState powerUpsState,
                                                  in LocalTransform localTransform,
                                                  in PlayerLookState lookState,
                                                  in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                  in PlayerConditionalCharacterTuningContext conditionalCharacterTuningContext,
                                                  ref bool conditionalShotContextInitialized,
                                                  ref PlayerRuntimeShootingConfig shotShootingConfig,
                                                  DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                                                  Entity playerEntity,
                                                  DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                                  ref PlayerLaserBeamState laserBeamState,
                                                  ref PlayerPassiveToolsState shotPassiveToolsState)
    {
        // Qualify every equipped passive independently so identical module types never share counters.
        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            ref EquippedPassiveToolElement passive = ref equippedPassiveTools.ElementAt(passiveIndex);

            if (!PlayerConditionalPowerUpRuntimeUtility.TryConsumeQualifiedShot(in passive.Tool.ConditionalApplication,
                                                                                ref passive.ConditionalApplicationState))
            {
                continue;
            }

            if (PlayerConditionalCharacterTuningRuntimeUtility.TryAccumulate(passive.PowerUpId,
                                                                             in conditionalCharacterTuningContext,
                                                                             ref conditionalShotContextInitialized))
            {
                PlayerConditionalCharacterTuningRuntimeUtility.RebuildShootingConfig(in runtimeShootingConfig,
                                                                                     in conditionalCharacterTuningContext,
                                                                                     out shotShootingConfig);
            }

            ApplyQualifiedEffects(in passive.Tool,
                                  in localTransform,
                                  in lookState,
                                  in shotShootingConfig,
                                  appliedElementSlots,
                                  playerEntity,
                                  bombRequests,
                                  ref laserBeamState,
                                  ref shotPassiveToolsState);
        }

        // Active toggle payloads retain one state per physical slot and participate only while toggled on.
        AccumulateQualifiedToggleEffects(in powerUpsConfig.PrimarySlot,
                                         powerUpsState.PrimaryIsActive,
                                         ref powerUpsState.PrimaryConditionalApplication,
                                         in localTransform,
                                         in lookState,
                                         in runtimeShootingConfig,
                                         in conditionalCharacterTuningContext,
                                         ref conditionalShotContextInitialized,
                                         ref shotShootingConfig,
                                         appliedElementSlots,
                                         playerEntity,
                                         bombRequests,
                                         ref laserBeamState,
                                         ref shotPassiveToolsState);
        AccumulateQualifiedToggleEffects(in powerUpsConfig.SecondarySlot,
                                         powerUpsState.SecondaryIsActive,
                                         ref powerUpsState.SecondaryConditionalApplication,
                                         in localTransform,
                                         in lookState,
                                         in runtimeShootingConfig,
                                         in conditionalCharacterTuningContext,
                                         ref conditionalShotContextInitialized,
                                         ref shotShootingConfig,
                                         appliedElementSlots,
                                         playerEntity,
                                         bombRequests,
                                         ref laserBeamState,
                                         ref shotPassiveToolsState);
    }

    /// <summary>
    /// Qualifies one active toggle slot and merges its sibling effects into the current volley.
    /// </summary>
    /// <param name="slotConfig">Active slot whose embedded passive tool may use a shot condition.</param>
    /// <param name="isActive">Non-zero while the toggle is currently active.</param>
    /// <param name="runtimeState">Mutable per-slot cadence or automatic-charge state.</param>
    /// <param name="localTransform">Current player transform used by qualified object-spawn effects.</param>
    /// <param name="lookState">Current player look state used to orient qualified object-spawn effects.</param>
    /// <param name="runtimeShootingConfig">Current authoritative shooting config used as the conditional Character Tuning baseline.</param>
    /// <param name="conditionalCharacterTuningContext">Runtime formula inputs used to build shot-local Character Tuning overlays.</param>
    /// <param name="conditionalShotContextInitialized">Mutable flag tracking whether scalable stats were copied for this shot.</param>
    /// <param name="shotShootingConfig">Mutable shot-local shooting config rebuilt when this slot supplies Character Tuning.</param>
    /// <param name="appliedElementSlots">Current elemental slots used as the standalone charged-beam template baseline.</param>
    /// <param name="playerEntity">Player entity owning spawned objects.</param>
    /// <param name="bombRequests">Output buffer receiving qualified object-spawn requests.</param>
    /// <param name="laserBeamState">Mutable laser state receiving a qualified standalone charged beam.</param>
    /// <param name="shotPassiveToolsState">Per-volley passive snapshot updated with eligible sibling effects.</param>
    private static void AccumulateQualifiedToggleEffects(in PlayerPowerUpSlotConfig slotConfig,
                                                         byte isActive,
                                                         ref PowerUpConditionalApplicationRuntimeState runtimeState,
                                                         in LocalTransform localTransform,
                                                         in PlayerLookState lookState,
                                                         in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                         in PlayerConditionalCharacterTuningContext conditionalCharacterTuningContext,
                                                         ref bool conditionalShotContextInitialized,
                                                         ref PlayerRuntimeShootingConfig shotShootingConfig,
                                                         DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                                                         Entity playerEntity,
                                                         DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                                         ref PlayerLaserBeamState laserBeamState,
                                                         ref PlayerPassiveToolsState shotPassiveToolsState)
    {
        PlayerPassiveToolConfig passiveTool = slotConfig.TogglePassiveTool;

        if (isActive == 0 ||
            !PlayerConditionalPowerUpRuntimeUtility.TryConsumeQualifiedShot(in passiveTool.ConditionalApplication,
                                                                            ref runtimeState))
        {
            return;
        }

        if (PlayerConditionalCharacterTuningRuntimeUtility.TryAccumulate(slotConfig.PowerUpId,
                                                                         in conditionalCharacterTuningContext,
                                                                         ref conditionalShotContextInitialized))
        {
            PlayerConditionalCharacterTuningRuntimeUtility.RebuildShootingConfig(in runtimeShootingConfig,
                                                                                 in conditionalCharacterTuningContext,
                                                                                 out shotShootingConfig);
        }

        ApplyQualifiedEffects(in passiveTool,
                              in localTransform,
                              in lookState,
                              in shotShootingConfig,
                              appliedElementSlots,
                              playerEntity,
                              bombRequests,
                              ref laserBeamState,
                              ref shotPassiveToolsState);
    }
    #endregion

    #region Effects
    /// <summary>
    /// Applies projectile-compatible sibling effects and an optional object spawn after the caller qualifies one condition.
    /// </summary>
    /// <param name="passiveTool">Conditional tool containing the effects to apply.</param>
    /// <param name="localTransform">Current player transform used by an object spawn.</param>
    /// <param name="lookState">Current player look state used to orient an object spawn.</param>
    /// <param name="runtimeShootingConfig">Current shooting config used by an automatic standalone charged beam.</param>
    /// <param name="appliedElementSlots">Current elemental slots used as the standalone charged-beam template baseline.</param>
    /// <param name="playerEntity">Player entity owning the spawned object.</param>
    /// <param name="bombRequests">Output buffer receiving an optional object-spawn request.</param>
    /// <param name="laserBeamState">Mutable laser state receiving a qualified standalone charged beam.</param>
    /// <param name="shotPassiveToolsState">Per-volley passive snapshot updated in place.</param>
    private static void ApplyQualifiedEffects(in PlayerPassiveToolConfig passiveTool,
                                              in LocalTransform localTransform,
                                              in PlayerLookState lookState,
                                              in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                              DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                                              Entity playerEntity,
                                              DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                              ref PlayerLaserBeamState laserBeamState,
                                              ref PlayerPassiveToolsState shotPassiveToolsState)
    {
        PlayerPassiveToolsAggregationUtility.AccumulateConditionalPassiveTool(ref shotPassiveToolsState,
                                                                              in passiveTool);
        PowerUpConditionalApplicationConfig conditionalApplication = passiveTool.ConditionalApplication;

        if (conditionalApplication.Mode == PowerUpConditionalApplicationMode.SuddenStrike)
        {
            ApplyFullChargeProjectileModifiers(in conditionalApplication.HoldCharge,
                                               ref shotPassiveToolsState);

            if (conditionalApplication.HoldCharge.UseChargedLaserBeam != 0)
                PlayerPowerUpActivationExecutionUtility.ExecuteStandaloneChargedLaser(in conditionalApplication.HoldCharge,
                                                                                       in runtimeShootingConfig,
                                                                                       appliedElementSlots,
                                                                                       ref laserBeamState,
                                                                                       math.max(1f, conditionalApplication.HoldCharge.SizeMultiplier),
                                                                                       math.max(1f, conditionalApplication.HoldCharge.DamageMultiplier),
                                                                                       math.max(1f, conditionalApplication.HoldCharge.SpeedMultiplier),
                                                                                       math.max(1f, conditionalApplication.HoldCharge.RangeMultiplier),
                                                                                       math.max(1f, conditionalApplication.HoldCharge.LifetimeMultiplier));
        }

        if (conditionalApplication.HasSpawnObject == 0)
            return;

        PlayerPowerUpActivationExecutionUtility.ExecuteSpawnObject(conditionalApplication.SpawnObjectPrefabEntity,
                                                                    in conditionalApplication.SpawnObject,
                                                                    conditionalApplication.HasImpactFrame,
                                                                    in conditionalApplication.ImpactFrame,
                                                                    in localTransform,
                                                                    in lookState,
                                                                    playerEntity,
                                                                    bombRequests);
    }

    /// <summary>
    /// Applies the fully charged Trigger Hold Charge projectile payload to a qualified Sudden Strike volley.
    /// </summary>
    /// <param name="holdCharge">Baked charge payload whose maximum modifiers apply automatically.</param>
    /// <param name="shotPassiveToolsState">Per-volley passive snapshot updated with charge modifiers.</param>
    private static void ApplyFullChargeProjectileModifiers(in ChargeShotPowerUpConfig holdCharge,
                                                           ref PlayerPassiveToolsState shotPassiveToolsState)
    {
        shotPassiveToolsState.ProjectileSizeMultiplier *= math.max(0.01f, holdCharge.SizeMultiplier);
        shotPassiveToolsState.ProjectileDamageMultiplier *= math.max(0f, holdCharge.DamageMultiplier);
        shotPassiveToolsState.ProjectileSpeedMultiplier *= math.max(0f, holdCharge.SpeedMultiplier);
        shotPassiveToolsState.ProjectileLifetimeRangeMultiplier *= math.max(0f, holdCharge.RangeMultiplier);
        shotPassiveToolsState.ProjectileLifetimeSecondsMultiplier *= math.max(0f, holdCharge.LifetimeMultiplier);

        if (holdCharge.PenetrationMode != ProjectilePenetrationMode.None ||
            holdCharge.IgnoreInheritedPlayerVelocityX != 0 ||
            holdCharge.IgnoreInheritedPlayerVelocityZ != 0)
        {
            shotPassiveToolsState.HasShotgun = 1;
            shotPassiveToolsState.Shotgun.ProjectileCount = math.max(1,
                                                                     shotPassiveToolsState.Shotgun.ProjectileCount);
            shotPassiveToolsState.Shotgun.PenetrationMode = (ProjectilePenetrationMode)math.max((int)shotPassiveToolsState.Shotgun.PenetrationMode,
                                                                                                 (int)holdCharge.PenetrationMode);
            shotPassiveToolsState.Shotgun.MaxPenetrations += math.max(0, holdCharge.MaxPenetrations);
            shotPassiveToolsState.Shotgun.IgnoreInheritedPlayerVelocityX = shotPassiveToolsState.Shotgun.IgnoreInheritedPlayerVelocityX != 0 ||
                                                                           holdCharge.IgnoreInheritedPlayerVelocityX != 0
                ? (byte)1
                : (byte)0;
            shotPassiveToolsState.Shotgun.IgnoreInheritedPlayerVelocityZ = shotPassiveToolsState.Shotgun.IgnoreInheritedPlayerVelocityZ != 0 ||
                                                                           holdCharge.IgnoreInheritedPlayerVelocityZ != 0
                ? (byte)1
                : (byte)0;
        }

        if (holdCharge.HasElementalPayload == 0 || holdCharge.ElementalStacksPerHit <= 0f)
            return;

        shotPassiveToolsState.HasElementalProjectiles = 1;
        shotPassiveToolsState.ElementalProjectiles.Effect = holdCharge.ElementalEffect;
        shotPassiveToolsState.ElementalProjectiles.StacksPerHit += math.max(0f,
                                                                           holdCharge.ElementalStacksPerHit);
    }
    #endregion

    #endregion
}
