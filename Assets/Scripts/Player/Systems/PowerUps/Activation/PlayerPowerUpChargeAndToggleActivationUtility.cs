using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hosts charge-shot and toggle-passive activation helpers extracted from the main slot utility to keep slot orchestration compact.
/// </summary>
internal static class PlayerPowerUpChargeAndToggleActivationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Processes the per-frame runtime for one charge-shot slot, including stored charge, release execution, and optional released-state gain or decay.
    /// </summary>
    /// <param name="slotConfig">Slot configuration compiled for a charge-shot active.</param>
    /// <param name="isPressed">True while the bound slot input remains held.</param>
    /// <param name="pressedThisFrame">True when the bound slot input was pressed during the current frame.</param>
    /// <param name="releasedThisFrame">True when the bound slot input was released during the current frame.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <param name="localTransform">Player transform used to emit the projectile burst.</param>
    /// <param name="lookState">Player look state used to resolve the firing direction.</param>
    /// <param name="movementState">Player movement state used when a Dash module is chained to the release.</param>
    /// <param name="runtimeMovementConfig">Movement config used to resolve movement-relative chained dash directions.</param>
    /// <param name="controllerConfig">Player controller config used to resolve projectile defaults.</param>
    /// <param name="passiveToolsState">Aggregated passive state used to augment spawned projectiles.</param>
    /// <param name="slotEnergy">Mutable slot resource state.</param>
    /// <param name="cooldownRemaining">Mutable cooldown state reused to block charge-shot input while cooling down.</param>
    /// <param name="charge">Mutable stored charge amount.</param>
    /// <param name="isCharging">Mutable charging flag for the slot.</param>
    /// <param name="isActive">Mutable active flag reset because charge shots are not persistent toggles.</param>
    /// <param name="maintenanceTickTimer">Mutable maintenance timer reset because charge shots do not use toggle maintenance.</param>
    /// <param name="hasOtherSlotDefinition">True when the opposite slot currently contains one defined power-up.</param>
    /// <param name="otherSlotCharge">Mutable opposite-slot charge state that can be interrupted.</param>
    /// <param name="otherSlotCooldownRemaining">Mutable opposite-slot cooldown state that can be cleared on hard interruption.</param>
    /// <param name="otherSlotIsCharging">Mutable opposite-slot charging flag that can be interrupted.</param>
    /// <param name="otherSlotIsActive">Mutable opposite-slot active flag that can be interrupted.</param>
    /// <param name="otherSlotMaintenanceTickTimer">Mutable opposite-slot maintenance accumulator that can be interrupted.</param>
    /// <param name="isShootingSuppressed">Shared per-frame shooting suppression flag updated while charging.</param>
    /// <param name="shootRequests">Output shoot-request buffer used to spawn the charge-shot burst.</param>
    /// <param name="playerEntity">Player entity used to resolve activation resources.</param>
    /// <param name="healthLookup">Health lookup used when the activation resource is Health.</param>
    /// <param name="updatedHealth">Cached mutable health state reused by the caller.</param>
    /// <param name="healthChanged">True when updatedHealth already contains a fetched runtime value.</param>
    /// <param name="shieldLookup">Shield lookup used when the activation resource is Shield.</param>
    /// <param name="updatedShield">Cached mutable shield state reused by the caller.</param>
    /// <param name="shieldChanged">True when updatedShield already contains a fetched runtime value.</param>
    /// <param name="dashState">Mutable dash state interrupted by hard slot interruption rules.</param>
    /// <param name="bulletTimeState">Mutable bullet-time state interrupted by hard slot interruption rules.</param>
    /// <param name="impactFrameState">Mutable Impact Frame state interrupted by hard slot interruption rules and activated on valid release.</param>
    /// <param name="ghostTrailState">Mutable Ghost Trail state interrupted by hard slot rules and activated on valid release.</param>
    /// <param name="moveInput">Raw movement input used as final fallback for chained Dash modules.</param>
    /// <param name="lastValidMovementDirection">Cached movement direction used as fallback for chained Dash modules.</param>
    /// <param name="orbitalProjectionRequests">Output orbital projection spawn request buffer.</param>
    /// <param name="dropCollectionRequests">Shared drop-collection request queue receiving activation side effects.</param>
    /// <param name="audioRequests">Optional audio request buffer used when a Game Audio singleton exists.</param>
    /// <param name="canEnqueueAudioRequests">True when audioRequests points to a valid buffer.</param>
    public static void ProcessChargeShotSlot(in PlayerPowerUpSlotConfig slotConfig,
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
                                             ref PlayerLaserBeamState laserBeamState,
                                             ref float slotEnergy,
                                             ref float cooldownRemaining,
                                             ref float charge,
                                             ref byte isCharging,
                                             ref byte isActive,
                                             ref float maintenanceTickTimer,
                                             bool hasOtherSlotDefinition,
                                             ref float otherSlotCharge,
                                             ref float otherSlotCooldownRemaining,
                                             ref byte otherSlotIsCharging,
                                             ref byte otherSlotIsActive,
                                             ref float otherSlotMaintenanceTickTimer,
                                             ref byte isShootingSuppressed,
                                             DynamicBuffer<ShootRequest> shootRequests,
                                             Entity playerEntity,
                                             ref ComponentLookup<PlayerHealth> healthLookup,
                                             ref PlayerHealth updatedHealth,
                                             ref bool healthChanged,
                                             ref ComponentLookup<PlayerShield> shieldLookup,
                                             ref PlayerShield updatedShield,
                                             ref bool shieldChanged,
                                             ref PlayerDashState dashState,
                                             ref PlayerBulletTimeState bulletTimeState,
                                             ref PlayerImpactFrameState impactFrameState,
                                             ref PlayerGhostTrailState ghostTrailState,
                                             float2 moveInput,
                                             float3 lastValidMovementDirection,
                                             DynamicBuffer<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequests,
                                             DynamicBuffer<EnemyDropCollectionRequest> dropCollectionRequests,
                                             DynamicBuffer<GameAudioEventRequest> audioRequests,
                                             bool canEnqueueAudioRequests)
    {
        isActive = 0;
        maintenanceTickTimer = 0f;

        if (cooldownRemaining > 0f)
        {
            isCharging = 0;
            charge = 0f;
            return;
        }

        float requiredCharge = math.max(0f, slotConfig.ChargeShot.RequiredCharge);
        float maximumCharge = math.max(requiredCharge, slotConfig.ChargeShot.MaximumCharge);
        float chargeRate = math.max(0f, slotConfig.ChargeShot.ChargeRatePerSecond);

        if (requiredCharge <= 0f || maximumCharge <= 0f)
        {
            isCharging = 0;
            charge = 0f;
            return;
        }

        if (charge > maximumCharge)
            charge = maximumCharge;

        if (pressedThisFrame)
        {
            isCharging = 1;

            // Mute the charge sting when the slot has a resource gate that cannot pay the activation cost
            // right now: the release branch is already gated, so the charge cue would otherwise tease an
            // activation the player will never get to release.
            if (canEnqueueAudioRequests &&
                PlayerPowerUpResourceCostUtility.CanPayActivationCost(in slotConfig,
                                                                      slotEnergy,
                                                                      playerEntity,
                                                                      ref healthLookup,
                                                                      ref updatedHealth,
                                                                      ref healthChanged,
                                                                      ref shieldLookup,
                                                                      ref updatedShield,
                                                                      ref shieldChanged))
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.ActiveCharge, localTransform.Position);
        }

        if (isCharging != 0 && isPressed && chargeRate > 0f)
        {
            charge += chargeRate * math.max(0f, deltaTime);
            charge = math.min(charge, maximumCharge);
        }

        if (isCharging != 0 && isPressed)
        {
            if (slotConfig.ChargeShot.SuppressBaseShootingWhileCharging != 0)
                isShootingSuppressed = 1;

            if (slotConfig.SuppressBaseShootingWhileActive != 0)
                isShootingSuppressed = 1;
        }

        if (releasedThisFrame && isCharging != 0)
        {
            isCharging = 0;

            bool hasEnoughCharge = charge + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon >= requiredCharge;
            bool canPayActivationCost = PlayerPowerUpResourceCostUtility.CanPayActivationCost(in slotConfig,
                                                                                              slotEnergy,
                                                                                              playerEntity,
                                                                                              ref healthLookup,
                                                                                              ref updatedHealth,
                                                                                              ref healthChanged,
                                                                                              ref shieldLookup,
                                                                                              ref updatedShield,
                                                                                              ref shieldChanged);

            if (hasEnoughCharge && canPayActivationCost)
            {
                float normalizedCharge = ResolveNormalizedChargeRatio(charge,
                                                                     requiredCharge,
                                                                     maximumCharge);
                PlayerPowerUpResourceCostUtility.ConsumeActivationCost(in slotConfig,
                                                                       ref slotEnergy,
                                                                       playerEntity,
                                                                       ref healthLookup,
                                                                       ref updatedHealth,
                                                                       ref healthChanged,
                                                                       ref shieldLookup,
                                                                       ref updatedShield,
                                                                       ref shieldChanged);

                if (slotConfig.InterruptOtherSlotOnEnter != 0 && hasOtherSlotDefinition)
                    InterruptOtherSlot(in slotConfig,
                                       ref otherSlotCharge,
                                       ref otherSlotCooldownRemaining,
                                       ref otherSlotIsCharging,
                                       ref otherSlotIsActive,
                                       ref otherSlotMaintenanceTickTimer,
                                       ref dashState,
                                       ref bulletTimeState,
                                       ref impactFrameState,
                                       ref ghostTrailState);

                if (slotConfig.HasImpactFrame != 0)
                    PlayerImpactFrameRuntimeUtility.Activate(ref impactFrameState, in slotConfig.ImpactFrame);

                if (slotConfig.HasGhostTrail != 0)
                    PlayerGhostTrailRuntimeUtility.Activate(ref ghostTrailState, in slotConfig.GhostTrail, false);

                PlayerPowerUpActivationExecutionUtility.ExecuteChargeShot(in slotConfig,
                                                                          in localTransform,
                                                                          in lookState,
                                                                          in runtimeShootingConfig,
                                                                          appliedElementSlots,
                                                                          in passiveToolsState,
                                                                          playerEntity,
                                                                          in muzzleLookup,
                                                                          in transformLookup,
                                                                          in localToWorldLookup,
                                                                          ref laserBeamState,
                                                                          normalizedCharge,
                                                                          orbitalProjectionRequests,
                                                                          shootRequests);

                if (slotConfig.HasDropAttraction != 0)
                {
                    EnemyDropCollectionRequestUtility.Enqueue(dropCollectionRequests,
                                                              slotConfig.DropAttraction.AttractionRadius,
                                                              slotConfig.DropAttraction.ConsumeUnusableDrops != 0);
                }

                PlayerPowerUpDashActivationUtility.ExecuteDashIfConfigured(in slotConfig,
                                                                            in lookState,
                                                                            in movementState,
                                                                            in runtimeMovementConfig,
                                                                            in localTransform,
                                                                            moveInput,
                                                                            lastValidMovementDirection,
                                                                            ref dashState);

                if (canEnqueueAudioRequests)
                {
                    GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.ActiveRelease, localTransform.Position);
                    GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShootCannon, localTransform.Position);
                }

                cooldownRemaining = math.max(0f, slotConfig.CooldownSeconds);
                charge = 0f;
                return;
            }
        }

        if (isPressed)
            return;

        TickReleasedChargeState(in slotConfig.ChargeShot,
                                deltaTime,
                                maximumCharge,
                                ref charge);
    }

    /// <summary>
    /// Processes one press-to-toggle passive slot, handling activation, deactivation, and cross-slot interruption.
    /// </summary>
    /// <param name="slotConfig">Slot configuration compiled as a passive toggle active tool.</param>
    /// <param name="pressedThisFrame">True when the bound slot input was pressed during the current frame.</param>
    /// <param name="slotEnergy">Mutable slot resource state.</param>
    /// <param name="cooldownRemaining">Mutable startup-lock timer reused from the slot cooldown state.</param>
    /// <param name="isActive">Mutable active flag tracking whether the passive effect is currently enabled.</param>
    /// <param name="maintenanceTickTimer">Mutable maintenance accumulator reset on activation and deactivation.</param>
    /// <param name="hasOtherSlotDefinition">True when the opposite slot currently contains one defined power-up.</param>
    /// <param name="otherSlotCharge">Mutable opposite-slot charge state that can be interrupted.</param>
    /// <param name="otherSlotCooldownRemaining">Mutable opposite-slot cooldown state that can be cleared on hard interruption.</param>
    /// <param name="otherSlotIsCharging">Mutable opposite-slot charging flag that can be interrupted.</param>
    /// <param name="otherSlotIsActive">Mutable opposite-slot active flag that can be interrupted.</param>
    /// <param name="otherSlotMaintenanceTickTimer">Mutable opposite-slot maintenance accumulator that can be interrupted.</param>
    /// <param name="isShootingSuppressed">Shared per-frame shooting suppression flag updated when the toggle remains active.</param>
    /// <param name="playerEntity">Player entity used to resolve activation resources.</param>
    /// <param name="healthLookup">Health lookup used when the activation resource is Health.</param>
    /// <param name="updatedHealth">Cached mutable health state reused by the caller.</param>
    /// <param name="healthChanged">True when updatedHealth already contains a fetched runtime value.</param>
    /// <param name="shieldLookup">Shield lookup used when the activation resource is Shield.</param>
    /// <param name="updatedShield">Cached mutable shield state reused by the caller.</param>
    /// <param name="shieldChanged">True when updatedShield already contains a fetched runtime value.</param>
    /// <param name="lookState">Player look state used by optional Dash payloads.</param>
    /// <param name="movementState">Player movement state used by optional Dash payloads.</param>
    /// <param name="runtimeMovementConfig">Runtime movement config used by optional Dash payloads.</param>
    /// <param name="localTransform">Player transform used by optional Dash payloads.</param>
    /// <param name="moveInput">Raw movement input used by optional Dash payloads.</param>
    /// <param name="lastValidMovementDirection">Cached movement direction used by optional Dash payloads.</param>
    /// <param name="dashState">Mutable dash state interrupted by hard slot interruption rules.</param>
    /// <param name="bulletTimeState">Mutable bullet-time state interrupted by hard slot interruption rules.</param>
    /// <param name="impactFrameState">Mutable Impact Frame state interrupted by hard slot interruption rules and activated on toggle-on.</param>
    /// <param name="ghostTrailState">Mutable Ghost Trail state interrupted by hard slot rules and activated or stopped with the toggle.</param>
    /// <param name="slotIndex">Owning active slot index used to match Ghost Trail lifetime to this toggle.</param>
    public static void ProcessPassiveToggleSlot(in PlayerPowerUpSlotConfig slotConfig,
                                                bool pressedThisFrame,
                                                ref float slotEnergy,
                                                ref float cooldownRemaining,
                                                ref byte isActive,
                                                ref float maintenanceTickTimer,
                                                bool hasOtherSlotDefinition,
                                                ref float otherSlotCharge,
                                                ref float otherSlotCooldownRemaining,
                                                ref byte otherSlotIsCharging,
                                                ref byte otherSlotIsActive,
                                                ref float otherSlotMaintenanceTickTimer,
                                                ref byte isShootingSuppressed,
                                                Entity playerEntity,
                                                ref ComponentLookup<PlayerHealth> healthLookup,
                                                ref PlayerHealth updatedHealth,
                                                ref bool healthChanged,
                                                ref ComponentLookup<PlayerShield> shieldLookup,
                                                ref PlayerShield updatedShield,
                                                ref bool shieldChanged,
                                                in PlayerLookState lookState,
                                                in PlayerMovementState movementState,
                                                in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                in LocalTransform localTransform,
                                                float2 moveInput,
                                                float3 lastValidMovementDirection,
                                                ref PlayerDashState dashState,
                                                ref PlayerBulletTimeState bulletTimeState,
                                                ref PlayerImpactFrameState impactFrameState,
                                                ref PlayerGhostTrailState ghostTrailState,
                                                byte slotIndex)
    {
        if (isActive != 0)
        {
            if (slotConfig.SuppressBaseShootingWhileActive != 0)
                isShootingSuppressed = 1;

            if (!pressedThisFrame || cooldownRemaining > 0f)
                return;

            isActive = 0;
            maintenanceTickTimer = 0f;
            cooldownRemaining = 0f;

            if (slotConfig.HasGhostTrail != 0)
                PlayerGhostTrailRuntimeUtility.StopMatchedToggle(ref ghostTrailState, slotIndex);

            return;
        }

        maintenanceTickTimer = 0f;

        if (!pressedThisFrame)
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

        if (slotConfig.InterruptOtherSlotOnEnter != 0 && hasOtherSlotDefinition)
            InterruptOtherSlot(in slotConfig,
                               ref otherSlotCharge,
                               ref otherSlotCooldownRemaining,
                               ref otherSlotIsCharging,
                               ref otherSlotIsActive,
                               ref otherSlotMaintenanceTickTimer,
                               ref dashState,
                               ref bulletTimeState,
                               ref impactFrameState,
                               ref ghostTrailState);

        isActive = 1;
        maintenanceTickTimer = 0f;
        cooldownRemaining = math.max(0f, slotConfig.CooldownSeconds);

        if (slotConfig.HasImpactFrame != 0)
            PlayerImpactFrameRuntimeUtility.Activate(ref impactFrameState, in slotConfig.ImpactFrame);

        if (slotConfig.HasGhostTrail != 0)
            PlayerGhostTrailRuntimeUtility.Activate(ref ghostTrailState, in slotConfig.GhostTrail, true, slotIndex);

        PlayerPowerUpDashActivationUtility.ExecuteDashIfConfigured(in slotConfig,
                                                                    in lookState,
                                                                    in movementState,
                                                                    in runtimeMovementConfig,
                                                                    in localTransform,
                                                                    moveInput,
                                                                    lastValidMovementDirection,
                                                                    ref dashState);

        if (slotConfig.SuppressBaseShootingWhileActive != 0)
            isShootingSuppressed = 1;
    }

    /// <summary>
    /// Interrupts opposite-slot charging or, when configured, the full active runtime state.
    /// </summary>
    /// <param name="slotConfig">Slot configuration driving the interruption rules.</param>
    /// <param name="otherSlotCharge">Mutable opposite-slot charge state.</param>
    /// <param name="otherSlotCooldownRemaining">Mutable opposite-slot cooldown state.</param>
    /// <param name="otherSlotIsCharging">Mutable opposite-slot charging flag.</param>
    /// <param name="otherSlotIsActive">Mutable opposite-slot active flag.</param>
    /// <param name="otherSlotMaintenanceTickTimer">Mutable opposite-slot maintenance accumulator.</param>
    /// <param name="dashState">Mutable dash state interrupted by hard slot interruption rules.</param>
    /// <param name="bulletTimeState">Mutable bullet-time state interrupted by hard slot interruption rules.</param>
    /// <param name="impactFrameState">Mutable Impact Frame state interrupted by hard slot interruption rules.</param>
    /// <param name="ghostTrailState">Mutable Ghost Trail state interrupted by hard slot interruption rules.</param>
    public static void InterruptOtherSlot(in PlayerPowerUpSlotConfig slotConfig,
                                          ref float otherSlotCharge,
                                          ref float otherSlotCooldownRemaining,
                                          ref byte otherSlotIsCharging,
                                          ref byte otherSlotIsActive,
                                          ref float otherSlotMaintenanceTickTimer,
                                          ref PlayerDashState dashState,
                                          ref PlayerBulletTimeState bulletTimeState,
                                          ref PlayerImpactFrameState impactFrameState,
                                          ref PlayerGhostTrailState ghostTrailState)
    {
        otherSlotCharge = 0f;
        otherSlotIsCharging = 0;

        if (slotConfig.InterruptOtherSlotChargingOnly != 0)
            return;

        otherSlotCooldownRemaining = 0f;
        otherSlotIsActive = 0;
        otherSlotMaintenanceTickTimer = 0f;
        dashState.IsDashing = 0;
        dashState.ClearVelocityAfterApply = 0;
        dashState.Phase = 0;
        dashState.PhaseRemaining = 0f;
        dashState.HoldDuration = 0f;
        dashState.RemainingInvulnerability = 0f;
        dashState.Duration = 0f;
        dashState.Distance = 0f;
        dashState.ElapsedDuration = 0f;
        dashState.Direction = float3.zero;
        dashState.EntryVelocity = float3.zero;
        dashState.Speed = 0f;
        dashState.TransitionInDuration = 0f;
        dashState.TransitionOutDuration = 0f;
        dashState.WallBounceIntensity = 0f;
        PlayerBulletTimeRuntimeUtility.Clear(ref bulletTimeState);
        PlayerImpactFrameRuntimeUtility.Clear(ref impactFrameState);
        PlayerGhostTrailRuntimeUtility.Clear(ref ghostTrailState);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the normalized charge fraction above the minimum release threshold.
    /// </summary>
    /// <param name="slotCharge">Stored charge sampled at release time.</param>
    /// <param name="requiredCharge">Minimum charge required to fire.</param>
    /// <param name="maximumCharge">Maximum charge supported by the slot.</param>
    /// <returns>Normalized charge ratio in the 0-1 range.</returns>
    private static float ResolveNormalizedChargeRatio(float slotCharge,
                                                      float requiredCharge,
                                                      float maximumCharge)
    {
        float clampedCharge = math.clamp(slotCharge, 0f, math.max(requiredCharge, maximumCharge));

        if (maximumCharge <= requiredCharge)
            return clampedCharge >= requiredCharge ? 1f : 0f;

        return math.saturate((clampedCharge - requiredCharge) / (maximumCharge - requiredCharge));
    }

    /// <summary>
    /// Updates released charge storage using the optional passive gain and decay settings.
    /// </summary>
    /// <param name="chargeShotConfig">Charge-shot payload containing released-state gain and decay options.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <param name="maximumCharge">Maximum charge cap used to convert percentages into absolute amounts.</param>
    /// <param name="charge">Mutable stored charge amount.</param>
    private static void TickReleasedChargeState(in ChargeShotPowerUpConfig chargeShotConfig,
                                                float deltaTime,
                                                float maximumCharge,
                                                ref float charge)
    {
        float safeDeltaTime = math.max(0f, deltaTime);
        float chargeDelta = 0f;

        if (chargeShotConfig.PassiveChargeGainWhileReleased != 0)
            chargeDelta += maximumCharge * (math.max(0f, chargeShotConfig.PassiveChargeGainPercentPerSecond) * 0.01f) * safeDeltaTime;

        if (chargeShotConfig.DecayAfterRelease != 0)
            chargeDelta -= maximumCharge * (math.max(0f, chargeShotConfig.DecayAfterReleasePercentPerSecond) * 0.01f) * safeDeltaTime;

        if (chargeDelta == 0f)
        {
            charge = 0f;
            return;
        }

        charge = math.clamp(charge + chargeDelta, 0f, maximumCharge);
    }
    #endregion

    #endregion
}
