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
    /// slotConfig: Slot configuration compiled for a charge-shot active.
    /// isPressed: True while the bound slot input remains held.
    /// pressedThisFrame: True when the bound slot input was pressed during the current frame.
    /// releasedThisFrame: True when the bound slot input was released during the current frame.
    /// deltaTime: Current frame delta time.
    /// localTransform: Player transform used to emit the projectile burst.
    /// lookState: Player look state used to resolve the firing direction.
    /// movementState: Player movement state used when a Dash module is chained to the release.
    /// runtimeMovementConfig: Movement config used to resolve movement-relative chained dash directions.
    /// controllerConfig: Player controller config used to resolve projectile defaults.
    /// passiveToolsState: Aggregated passive state used to augment spawned projectiles.
    /// slotEnergy: Mutable slot resource state.
    /// cooldownRemaining: Mutable cooldown state reused to block charge-shot input while cooling down.
    /// charge: Mutable stored charge amount.
    /// isCharging: Mutable charging flag for the slot.
    /// isActive: Mutable active flag reset because charge shots are not persistent toggles.
    /// maintenanceTickTimer: Mutable maintenance timer reset because charge shots do not use toggle maintenance.
    /// hasOtherSlotDefinition: True when the opposite slot currently contains one defined power-up.
    /// otherSlotCharge: Mutable opposite-slot charge state that can be interrupted.
    /// otherSlotCooldownRemaining: Mutable opposite-slot cooldown state that can be cleared on hard interruption.
    /// otherSlotIsCharging: Mutable opposite-slot charging flag that can be interrupted.
    /// otherSlotIsActive: Mutable opposite-slot active flag that can be interrupted.
    /// otherSlotMaintenanceTickTimer: Mutable opposite-slot maintenance accumulator that can be interrupted.
    /// isShootingSuppressed: Shared per-frame shooting suppression flag updated while charging.
    /// shootRequests: Output shoot-request buffer used to spawn the charge-shot burst.
    /// playerEntity: Player entity used to resolve activation resources.
    /// healthLookup: Health lookup used when the activation resource is Health.
    /// updatedHealth: Cached mutable health state reused by the caller.
    /// healthChanged: True when updatedHealth already contains a fetched runtime value.
    /// shieldLookup: Shield lookup used when the activation resource is Shield.
    /// updatedShield: Cached mutable shield state reused by the caller.
    /// shieldChanged: True when updatedShield already contains a fetched runtime value.
    /// dashState: Mutable dash state interrupted by hard slot interruption rules.
    /// bulletTimeState: Mutable bullet-time state interrupted by hard slot interruption rules.
    /// moveInput: Raw movement input used as final fallback for chained Dash modules.
    /// lastValidMovementDirection: Cached movement direction used as fallback for chained Dash modules.
    /// audioRequests: Optional audio request buffer used when a Game Audio singleton exists.
    /// canEnqueueAudioRequests: True when audioRequests points to a valid buffer.
    /// returns void.
    /// </summary>
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
                                             float2 moveInput,
                                             float3 lastValidMovementDirection,
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

            if (canEnqueueAudioRequests)
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
                                       ref bulletTimeState);

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
                                                                          shootRequests);

                ExecuteReleaseDashIfConfigured(in slotConfig,
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
    /// slotConfig: Slot configuration compiled as a passive toggle active tool.
    /// pressedThisFrame: True when the bound slot input was pressed during the current frame.
    /// slotEnergy: Mutable slot resource state.
    /// cooldownRemaining: Mutable startup-lock timer reused from the slot cooldown state.
    /// isActive: Mutable active flag tracking whether the passive effect is currently enabled.
    /// maintenanceTickTimer: Mutable maintenance accumulator reset on activation and deactivation.
    /// hasOtherSlotDefinition: True when the opposite slot currently contains one defined power-up.
    /// otherSlotCharge: Mutable opposite-slot charge state that can be interrupted.
    /// otherSlotCooldownRemaining: Mutable opposite-slot cooldown state that can be cleared on hard interruption.
    /// otherSlotIsCharging: Mutable opposite-slot charging flag that can be interrupted.
    /// otherSlotIsActive: Mutable opposite-slot active flag that can be interrupted.
    /// otherSlotMaintenanceTickTimer: Mutable opposite-slot maintenance accumulator that can be interrupted.
    /// isShootingSuppressed: Shared per-frame shooting suppression flag updated when the toggle remains active.
    /// playerEntity: Player entity used to resolve activation resources.
    /// healthLookup: Health lookup used when the activation resource is Health.
    /// updatedHealth: Cached mutable health state reused by the caller.
    /// healthChanged: True when updatedHealth already contains a fetched runtime value.
    /// shieldLookup: Shield lookup used when the activation resource is Shield.
    /// updatedShield: Cached mutable shield state reused by the caller.
    /// shieldChanged: True when updatedShield already contains a fetched runtime value.
    /// dashState: Mutable dash state interrupted by hard slot interruption rules.
    /// bulletTimeState: Mutable bullet-time state interrupted by hard slot interruption rules.
    /// returns void.
    /// </summary>
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
                                                ref PlayerDashState dashState,
                                                ref PlayerBulletTimeState bulletTimeState)
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
            return;
        }

        maintenanceTickTimer = 0f;

        if (!pressedThisFrame || slotConfig.TogglePassiveTool.IsDefined == 0)
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
                               ref bulletTimeState);

        isActive = 1;
        maintenanceTickTimer = 0f;
        cooldownRemaining = math.max(0f, slotConfig.CooldownSeconds);

        if (slotConfig.SuppressBaseShootingWhileActive != 0)
            isShootingSuppressed = 1;
    }

    /// <summary>
    /// Interrupts opposite-slot charging or, when configured, the full active runtime state.
    /// slotConfig: Slot configuration driving the interruption rules.
    /// otherSlotCharge: Mutable opposite-slot charge state.
    /// otherSlotCooldownRemaining: Mutable opposite-slot cooldown state.
    /// otherSlotIsCharging: Mutable opposite-slot charging flag.
    /// otherSlotIsActive: Mutable opposite-slot active flag.
    /// otherSlotMaintenanceTickTimer: Mutable opposite-slot maintenance accumulator.
    /// dashState: Mutable dash state interrupted by hard slot interruption rules.
    /// bulletTimeState: Mutable bullet-time state interrupted by hard slot interruption rules.
    /// returns void.
    /// </summary>
    public static void InterruptOtherSlot(in PlayerPowerUpSlotConfig slotConfig,
                                          ref float otherSlotCharge,
                                          ref float otherSlotCooldownRemaining,
                                          ref byte otherSlotIsCharging,
                                          ref byte otherSlotIsActive,
                                          ref float otherSlotMaintenanceTickTimer,
                                          ref PlayerDashState dashState,
                                          ref PlayerBulletTimeState bulletTimeState)
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
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Executes the optional Dash module chained to a successful hold-charge release.
    /// /params slotConfig Active slot containing charge-shot and optional dash payloads.
    /// /params lookState Current player look direction state.
    /// /params movementState Current player movement state.
    /// /params runtimeMovementConfig Movement config used for movement-relative dash directions.
    /// /params localTransform Player transform used for fallback direction basis.
    /// /params moveInput Raw movement input used as final movement-direction fallback.
    /// /params lastValidMovementDirection Cached movement direction from previous frames.
    /// /params dashState Mutable dash state receiving the chained release dash.
    /// /returns None.
    /// </summary>
    private static void ExecuteReleaseDashIfConfigured(in PlayerPowerUpSlotConfig slotConfig,
                                                       in PlayerLookState lookState,
                                                       in PlayerMovementState movementState,
                                                       in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                       in LocalTransform localTransform,
                                                       float2 moveInput,
                                                       float3 lastValidMovementDirection,
                                                       ref PlayerDashState dashState)
    {
        if (slotConfig.Dash.Distance <= 0f)
            return;

        if (slotConfig.Dash.Duration <= 0f)
            return;

        if (dashState.IsDashing != 0)
            return;

        PlayerPowerUpActivationExecutionUtility.ExecuteDash(in slotConfig,
                                                            in lookState,
                                                            in movementState,
                                                            in runtimeMovementConfig,
                                                            in localTransform,
                                                            moveInput,
                                                            lastValidMovementDirection,
                                                            ref dashState);
    }

    /// <summary>
    /// Resolves the normalized charge fraction above the minimum release threshold.
    /// slotCharge: Stored charge sampled at release time.
    /// requiredCharge: Minimum charge required to fire.
    /// maximumCharge: Maximum charge supported by the slot.
    /// returns Normalized charge ratio in the 0-1 range.
    /// </summary>
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
    /// chargeShotConfig: Charge-shot payload containing released-state gain and decay options.
    /// deltaTime: Current frame delta time.
    /// maximumCharge: Maximum charge cap used to convert percentages into absolute amounts.
    /// charge: Mutable stored charge amount.
    /// returns void.
    /// </summary>
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
