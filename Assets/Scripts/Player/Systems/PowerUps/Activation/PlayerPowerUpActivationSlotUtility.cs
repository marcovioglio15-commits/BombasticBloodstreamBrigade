using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Contains slot-level activation flow for active power ups, including checks, costs and healing side effects.
/// </summary>
public static class PlayerPowerUpActivationSlotUtility
{
    #region Methods

    #region Slot Processing
    public static void ProcessSlotInput(in PlayerPowerUpSlotConfig slotConfig,
                                        in PlayerPowerUpSlotConfig otherSlotConfig,
                                        byte slotIndex,
                                        bool isPressed,
                                        bool pressedThisFrame,
                                        bool releasedThisFrame,
                                        float deltaTime,
                                        in LocalTransform localTransform,
                                        in PlayerLookState lookState,
                                        in PlayerMovementState movementState,
                                        in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                        in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                        DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                                        in PlayerPassiveToolsState passiveToolsState,
                                        in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                        in ComponentLookup<LocalTransform> transformLookup,
                                        in ComponentLookup<LocalToWorld> localToWorldLookup,
                                        float2 moveInput,
                                        float3 lastValidMovementDirection,
                                        ref PlayerLaserBeamState laserBeamState,
                                        ref float slotEnergy,
                                        ref float cooldownRemaining,
                                        ref float charge,
                                        ref byte isCharging,
                                        ref byte isActive,
                                        ref float maintenanceTickTimer,
                                        ref float otherSlotCharge,
                                        ref float otherSlotCooldownRemaining,
                                        ref byte otherSlotIsCharging,
                                        ref byte otherSlotIsActive,
                                        ref float otherSlotMaintenanceTickTimer,
                                        ref byte isShootingSuppressed,
                                        ref PlayerDashState dashState,
                                        ref PlayerBulletTimeState bulletTimeState,
                                        ref PlayerImpactFrameState impactFrameState,
                                        ref PlayerGhostTrailState ghostTrailState,
                                        ref PlayerHealOverTimeState healOverTimeState,
                                        DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                        DynamicBuffer<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequests,
                                        DynamicBuffer<ShootRequest> shootRequests,
                                        DynamicBuffer<GameAudioEventRequest> audioRequests,
                                        bool canEnqueueAudioRequests,
                                        Entity playerEntity,
                                        ref ComponentLookup<PlayerHealth> healthLookup,
                                        ref PlayerHealth updatedHealth,
                                        ref bool healthChanged,
                                        ref ComponentLookup<PlayerShield> shieldLookup,
                                        ref PlayerShield updatedShield,
                                        ref bool shieldChanged)
    {
        if (slotConfig.IsDefined == 0)
            return;

        if (slotConfig.ToolKind == ActiveToolKind.ChargeShot)
        {
            PlayerPowerUpChargeAndToggleActivationUtility.ProcessChargeShotSlot(in slotConfig,
                                                                                isPressed,
                                                                                pressedThisFrame,
                                                                                releasedThisFrame,
                                                                                deltaTime,
                                                                                in localTransform,
                                                                                in lookState,
                                                                                in movementState,
                                                                                in runtimeMovementConfig,
                                                                                in runtimeShootingConfig,
                                                                                appliedElementSlots,
                                                                                in passiveToolsState,
                                                                                in muzzleLookup,
                                                                                in transformLookup,
                                                                                in localToWorldLookup,
                                                                                ref laserBeamState,
                                                                                ref slotEnergy,
                                                                                ref cooldownRemaining,
                                                                                ref charge,
                                                                                ref isCharging,
                                                                                ref isActive,
                                                                                ref maintenanceTickTimer,
                                                                                otherSlotConfig.IsDefined != 0,
                                                                                ref otherSlotCharge,
                                                                                ref otherSlotCooldownRemaining,
                                                                                ref otherSlotIsCharging,
                                                                                ref otherSlotIsActive,
                                                                                ref otherSlotMaintenanceTickTimer,
                                                                                ref isShootingSuppressed,
                                                                                shootRequests,
                                                                                playerEntity,
                                                                                ref healthLookup,
                                                                                ref updatedHealth,
                                                                                ref healthChanged,
                                                                                ref shieldLookup,
                                                                                ref updatedShield,
                                                                                ref shieldChanged,
                                                                                ref dashState,
                                                                                ref bulletTimeState,
                                                                                ref impactFrameState,
                                                                                ref ghostTrailState,
                                                                                moveInput,
                                                                                lastValidMovementDirection,
                                                                                orbitalProjectionRequests,
                                                                                audioRequests,
                                                                                canEnqueueAudioRequests);
            return;
        }

        if (slotConfig.ToolKind == ActiveToolKind.PassiveToggle && slotConfig.Toggleable != 0)
        {
            PlayerPowerUpChargeAndToggleActivationUtility.ProcessPassiveToggleSlot(in slotConfig,
                                                                                   pressedThisFrame,
                                                                                   ref slotEnergy,
                                                                                   ref cooldownRemaining,
                                                                                   ref isActive,
                                                                                   ref maintenanceTickTimer,
                                                                                   otherSlotConfig.IsDefined != 0,
                                                                                   ref otherSlotCharge,
                                                                                   ref otherSlotCooldownRemaining,
                                                                                   ref otherSlotIsCharging,
                                                                                   ref otherSlotIsActive,
                                                                                   ref otherSlotMaintenanceTickTimer,
                                                                                   ref isShootingSuppressed,
                                                                                   playerEntity,
                                                                                   ref healthLookup,
                                                                                   ref updatedHealth,
                                                                                   ref healthChanged,
                                                                                   ref shieldLookup,
                                                                                   ref updatedShield,
                                                                                   ref shieldChanged,
                                                                                   in lookState,
                                                                                   in movementState,
                                                                                   in runtimeMovementConfig,
                                                                                   in localTransform,
                                                                                   moveInput,
                                                                                   lastValidMovementDirection,
                                                                                   ref dashState,
                                                                                   ref bulletTimeState,
                                                                                   ref impactFrameState,
                                                                                   ref ghostTrailState,
                                                                                   slotIndex);
            return;
        }

        isActive = 0;
        maintenanceTickTimer = 0f;
        bool activationTriggered;

        switch (slotConfig.ActivationInputMode)
        {
            case PowerUpActivationInputMode.OnRelease:
                activationTriggered = releasedThisFrame;
                break;
            default:
                activationTriggered = pressedThisFrame;
                break;
        }

        if (!activationTriggered)
            return;

        if (cooldownRemaining > 0f)
            return;

        if (!CanExecuteTool(in slotConfig,
                            in dashState,
                            in bulletTimeState,
                            in healOverTimeState,
                            in lookState,
                            in movementState,
                            in runtimeMovementConfig,
                            in runtimeShootingConfig,
                            in localTransform,
                            moveInput,
                            lastValidMovementDirection,
                            playerEntity,
                            ref healthLookup,
                            ref updatedHealth,
                            ref healthChanged))
            return;

        if (!PlayerPowerUpResourceCostUtility.CanPayActivationCost(in slotConfig,
                                                                   slotEnergy,
                                                                   playerEntity,
                                                                   ref healthLookup,
                                                                   ref updatedHealth,
                                                                   ref healthChanged,
                                                                   ref shieldLookup,
                                                                   ref updatedShield,
                                                                   ref shieldChanged))
            return;

        PlayerPowerUpResourceCostUtility.ConsumeActivationCost(in slotConfig,
                                                               ref slotEnergy,
                                                               playerEntity,
                                                               ref healthLookup,
                                                               ref updatedHealth,
                                                               ref healthChanged,
                                                               ref shieldLookup,
                                                               ref updatedShield,
                                                               ref shieldChanged);

        if (slotConfig.InterruptOtherSlotOnEnter != 0 && otherSlotConfig.IsDefined != 0)
            PlayerPowerUpChargeAndToggleActivationUtility.InterruptOtherSlot(in slotConfig,
                                                                             ref otherSlotCharge,
                                                                             ref otherSlotCooldownRemaining,
                                                                             ref otherSlotIsCharging,
                                                                             ref otherSlotIsActive,
                                                                             ref otherSlotMaintenanceTickTimer,
                                                                             ref dashState,
                                                                             ref bulletTimeState,
                                                                             ref impactFrameState,
                                                                             ref ghostTrailState);

        if (ShouldTriggerImpactFrameOnActivation(in slotConfig))
            PlayerImpactFrameRuntimeUtility.Activate(ref impactFrameState, in slotConfig.ImpactFrame);

        if (slotConfig.HasGhostTrail != 0)
            PlayerGhostTrailRuntimeUtility.Activate(ref ghostTrailState,
                                                    in slotConfig.GhostTrail,
                                                    false,
                                                    byte.MaxValue,
                                                    ResolveMatchedActiveDurationSeconds(in slotConfig));

        if (slotConfig.ToolKind == ActiveToolKind.PortableHealthPack)
            ExecutePortableHealthPack(in slotConfig,
                                      playerEntity,
                                      localTransform.Position,
                                      ref healthLookup,
                                      ref updatedHealth,
                                      ref healthChanged,
                                      ref healOverTimeState,
                                      audioRequests,
                                      canEnqueueAudioRequests);

        PlayerPowerUpActivationExecutionUtility.ExecuteTool(in slotConfig,
                                                            in localTransform,
                                                            in lookState,
                                                            in movementState,
                                                            in runtimeMovementConfig,
                                                            in runtimeShootingConfig,
                                                            appliedElementSlots,
                                                            in passiveToolsState,
                                                            in muzzleLookup,
                                                            in transformLookup,
                                                            in localToWorldLookup,
                                                            moveInput,
                                                            lastValidMovementDirection,
                                                            playerEntity,
                                                            ref laserBeamState,
                                                            ref dashState,
                                                            ref bulletTimeState,
                                                            bombRequests,
                                                            orbitalProjectionRequests,
                                                            shootRequests);

        if (canEnqueueAudioRequests)
            EnqueueActiveToolAudio(in slotConfig, localTransform.Position, audioRequests);

        if (slotConfig.SuppressBaseShootingWhileActive != 0)
            isShootingSuppressed = 1;

        cooldownRemaining = math.max(0f, slotConfig.CooldownSeconds);
    }

    #endregion

    #region Ghost Trail
    /// <summary>
    /// Resolves the host active's own finite duration so a Ghost Trail module set to match the activation duration can
    /// emit on non-toggleable actives (e.g. Dash) that have no toggle on/off lifecycle to bound emission.
    /// </summary>
    /// <param name="slotConfig">Active slot configuration being executed.</param>
    /// <returns>Host active finite duration in seconds, or zero when the tool has no sustained duration to match.</returns>
    private static float ResolveMatchedActiveDurationSeconds(in PlayerPowerUpSlotConfig slotConfig)
    {
        switch (slotConfig.ToolKind)
        {
            case ActiveToolKind.Dash:
                return math.max(0f, slotConfig.Dash.Duration);
            case ActiveToolKind.BulletTime:
                return math.max(0f, slotConfig.BulletTime.Duration);
            default:
                return 0f;
        }
    }
    #endregion

    #region Impact Frame
    /// <summary>
    /// Resolves whether the Impact Frame side-effect should run on input activation for this active tool.
    /// </summary>
    /// <param name="slotConfig">Active slot configuration being executed.</param>
    /// <returns>True when the effect should start immediately instead of waiting for a delayed spawned object payoff.</returns>
    private static bool ShouldTriggerImpactFrameOnActivation(in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.HasImpactFrame == 0)
            return false;

        switch (slotConfig.ToolKind)
        {
            case ActiveToolKind.Bomb:
                return false;
            default:
                return true;
        }
    }
    #endregion

    #region Checks
    private static bool CanExecuteTool(in PlayerPowerUpSlotConfig slotConfig,
                                       in PlayerDashState dashState,
                                       in PlayerBulletTimeState bulletTimeState,
                                       in PlayerHealOverTimeState healOverTimeState,
                                       in PlayerLookState lookState,
                                       in PlayerMovementState movementState,
                                       in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                       in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                       in LocalTransform localTransform,
                                       float2 moveInput,
                                       float3 lastValidMovementDirection,
                                       Entity playerEntity,
                                       ref ComponentLookup<PlayerHealth> healthLookup,
                                       ref PlayerHealth updatedHealth,
                                       ref bool healthChanged)
    {
        switch (slotConfig.ToolKind)
        {
            case ActiveToolKind.Bomb:
                return slotConfig.BombPrefabEntity != Entity.Null;
            case ActiveToolKind.Dash:
                if (dashState.IsDashing != 0)
                    return false;

                if (slotConfig.Dash.Duration <= 0f)
                    return false;

                if (slotConfig.Dash.Distance <= 0f)
                    return false;

                if (!PlayerPowerUpDashActivationUtility.TryResolveDashActivationDirection(slotConfig.Dash.DirectionMode,
                                                                                          in lookState,
                                                                                          in movementState,
                                                                                          in runtimeMovementConfig,
                                                                                          in localTransform,
                                                                                          moveInput,
                                                                                          lastValidMovementDirection,
                                                                                          out float3 _))
                    return false;

                return true;
            case ActiveToolKind.BulletTime:
                if (slotConfig.BulletTime.Duration <= 0f)
                    return false;

                if (slotConfig.BulletTime.EnemySlowPercent <= 0f &&
                    slotConfig.BulletTime.PlayerProjectileSlowPercent <= 0f)
                    return false;

                return true;
            case ActiveToolKind.ImpactFrame:
                return slotConfig.HasImpactFrame != 0 &&
                       PlayerImpactFrameRuntimeUtility.CanActivate(in slotConfig.ImpactFrame);
            case ActiveToolKind.GhostTrail:
                return slotConfig.HasGhostTrail != 0 &&
                       PlayerGhostTrailRuntimeUtility.CanActivate(in slotConfig.GhostTrail, false);
            case ActiveToolKind.Shotgun:
                if (slotConfig.Shotgun.ProjectileCount <= 0)
                    return false;

                if (runtimeShootingConfig.Values.ShootSpeed <= 0f)
                    return false;

                return true;
            case ActiveToolKind.PortableHealthPack:
                if (slotConfig.PortableHealthPack.HealAmount <= 0f)
                    return false;

                if (slotConfig.PortableHealthPack.ApplyMode == PowerUpHealApplicationMode.OverTime &&
                    slotConfig.PortableHealthPack.StackPolicy == PowerUpHealStackPolicy.IgnoreIfActive &&
                    healOverTimeState.IsActive != 0)
                    return false;

                if (!healthChanged)
                {
                    if (!healthLookup.HasComponent(playerEntity))
                        return false;

                    updatedHealth = healthLookup[playerEntity];
                    healthChanged = true;
                }

                if (updatedHealth.Current >= updatedHealth.Max)
                    return false;

                return true;
            case ActiveToolKind.OrbitalProjections:
                return slotConfig.TriggeredProjectilePassiveTool.IsDefined != 0 &&
                       slotConfig.TriggeredProjectilePassiveTool.HasOrbitalProjections != 0 &&
                       slotConfig.TriggeredProjectilePassiveTool.OrbitalProjections.Length > 0;
            case ActiveToolKind.ChargeShot:
                if (slotConfig.ChargeShot.RequiredCharge <= 0f)
                    return false;

                if (slotConfig.ChargeShot.ChargeRatePerSecond <= 0f &&
                    slotConfig.ChargeShot.PassiveChargeGainWhileReleased == 0)
                    return false;

                if (runtimeShootingConfig.Values.ShootSpeed <= 0f)
                    return false;

                return true;
            case ActiveToolKind.PassiveToggle:
                return slotConfig.Toggleable != 0 &&
                       (slotConfig.TogglePassiveTool.IsDefined != 0 ||
                        slotConfig.HasGhostTrail != 0);
            default:
                return false;
        }
    }

    #endregion

    #region Audio
    /// <summary>
    /// Enqueues the active-tool audio event matching a successfully executed non-charge slot.
    /// </summary>
    /// <param name="slotConfig">Runtime active-tool slot configuration.</param>
    /// <param name="position">Player position used for positioned one-shot audio.</param>
    /// <param name="audioRequests">Audio request buffer on the game audio singleton.</param>
    private static void EnqueueActiveToolAudio(in PlayerPowerUpSlotConfig slotConfig,
                                               float3 position,
                                               DynamicBuffer<GameAudioEventRequest> audioRequests)
    {
        switch (slotConfig.ToolKind)
        {
            case ActiveToolKind.Bomb:
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.ActiveThrow, position);
                break;
            case ActiveToolKind.Dash:
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.ActiveDash, position);
                break;
            case ActiveToolKind.Shotgun:
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShootProjectile, position);
                break;
        }
    }
    #endregion

    #region Side Effects
    /// <summary>
    /// Applies an instant or over-time portable health pack payload and emits immediate recharge audio when health is restored now.
    /// </summary>
    /// <param name="slotConfig">Runtime active-tool slot configuration.</param>
    /// <param name="playerEntity">Player entity receiving the healing payload.</param>
    /// <param name="position">Player position used for positioned recharge audio.</param>
    /// <param name="healthLookup">Mutable health lookup used to fetch current health.</param>
    /// <param name="updatedHealth">Cached mutable health value returned to the caller.</param>
    /// <param name="healthChanged">True when updatedHealth contains a valid fetched value.</param>
    /// <param name="healOverTimeState">Mutable heal-over-time state for delayed payloads.</param>
    /// <param name="audioRequests">Optional audio request buffer.</param>
    /// <param name="canEnqueueAudioRequests">True when audioRequests is valid.</param>
    private static void ExecutePortableHealthPack(in PlayerPowerUpSlotConfig slotConfig,
                                                  Entity playerEntity,
                                                  float3 position,
                                                  ref ComponentLookup<PlayerHealth> healthLookup,
                                                  ref PlayerHealth updatedHealth,
                                                  ref bool healthChanged,
                                                  ref PlayerHealOverTimeState healOverTimeState,
                                                  DynamicBuffer<GameAudioEventRequest> audioRequests,
                                                  bool canEnqueueAudioRequests)
    {
        if (!healthChanged)
        {
            if (!healthLookup.HasComponent(playerEntity))
                return;

            updatedHealth = healthLookup[playerEntity];
            healthChanged = true;
        }

        float healAmount = math.max(0f, slotConfig.PortableHealthPack.HealAmount);

        if (healAmount <= 0f)
            return;

        float missingHealth = math.max(0f, updatedHealth.Max - updatedHealth.Current);

        if (missingHealth <= 0f)
            return;

        if (slotConfig.PortableHealthPack.ApplyMode == PowerUpHealApplicationMode.OverTime)
        {
            ApplyHealOverTime(in slotConfig,
                              healAmount,
                              missingHealth,
                              ref healOverTimeState);
            return;
        }

        float previousHealth = updatedHealth.Current;
        updatedHealth.Current += math.min(missingHealth, healAmount);

        if (updatedHealth.Current > updatedHealth.Max)
            updatedHealth.Current = updatedHealth.Max;

        if (canEnqueueAudioRequests && updatedHealth.Current > previousHealth)
            GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerHealthRecharge, position);
    }

    private static void ApplyHealOverTime(in PlayerPowerUpSlotConfig slotConfig,
                                          float requestedHealAmount,
                                          float currentMissingHealth,
                                          ref PlayerHealOverTimeState healOverTimeState)
    {
        float clampedRequestedHeal = math.max(0f, requestedHealAmount);
        float clampedMissingHealth = math.max(0f, currentMissingHealth);
        float totalHeal = math.min(clampedRequestedHeal, clampedMissingHealth);

        if (totalHeal <= 0f)
            return;

        float durationSeconds = math.max(0.05f, slotConfig.PortableHealthPack.DurationSeconds);
        float tickIntervalSeconds = math.max(0.01f, slotConfig.PortableHealthPack.TickIntervalSeconds);
        float healPerSecond = totalHeal / durationSeconds;
        bool hasActiveHot = healOverTimeState.IsActive != 0;

        switch (slotConfig.PortableHealthPack.StackPolicy)
        {
            case PowerUpHealStackPolicy.IgnoreIfActive:
                if (hasActiveHot)
                    return;

                break;
            case PowerUpHealStackPolicy.Additive:
                if (hasActiveHot)
                {
                    healOverTimeState.RemainingTotalHeal += totalHeal;
                    healOverTimeState.RemainingDuration = math.max(healOverTimeState.RemainingDuration, durationSeconds);
                    healOverTimeState.TickIntervalSeconds = math.min(healOverTimeState.TickIntervalSeconds, tickIntervalSeconds);
                    healOverTimeState.HealPerSecond += healPerSecond;
                    healOverTimeState.IsActive = 1;
                    return;
                }

                break;
        }

        healOverTimeState.IsActive = 1;
        healOverTimeState.HealPerSecond = healPerSecond;
        healOverTimeState.RemainingTotalHeal = totalHeal;
        healOverTimeState.RemainingDuration = durationSeconds;
        healOverTimeState.TickIntervalSeconds = tickIntervalSeconds;
        healOverTimeState.TickTimer = 0f;
    }

    #endregion

    #endregion
}
