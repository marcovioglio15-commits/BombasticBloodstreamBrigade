using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Recharges active-tool energy based on configured charge rules.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
public partial struct PlayerPowerUpRechargeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the player power-up configuration and mutable state required by recharge evaluation.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
    }

    /// <summary>
    /// Applies time, enemy-kill and procedural room-clear recharge deltas to every configured player slot.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        uint globalKillCount = 0u;
        uint globalRoomClearCount = 0u;

        if (SystemAPI.TryGetSingleton<GlobalEnemyKillCounter>(out GlobalEnemyKillCounter killCounter))
            globalKillCount = killCounter.TotalKilled;

        if (SystemAPI.TryGetSingleton<GameProceduralRoomClearCounter>(out GameProceduralRoomClearCounter roomClearCounter))
            globalRoomClearCount = roomClearCounter.TotalCleared;

        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  RefRW<PlayerPowerUpsState> powerUpsState) in SystemAPI.Query<DynamicBuffer<PlayerPowerUpsConfigElement>, RefRW<PlayerPowerUpsState>>())
        {
            PlayerPowerUpsConfig powerUpsConfig;
            PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer,
                                                   out powerUpsConfig);
            uint previousKillCount = powerUpsState.ValueRO.LastObservedGlobalKillCount;
            uint previousRoomClearCount = powerUpsState.ValueRO.LastObservedRoomClearCount;
            uint killDelta = 0u;
            uint roomClearDelta = 0u;

            if (globalKillCount >= previousKillCount)
                killDelta = globalKillCount - previousKillCount;
            else
                killDelta = globalKillCount;

            if (globalRoomClearCount >= previousRoomClearCount)
                roomClearDelta = globalRoomClearCount - previousRoomClearCount;
            else
                roomClearDelta = globalRoomClearCount;

            float primaryEnergy = powerUpsState.ValueRO.PrimaryEnergy;
            float secondaryEnergy = powerUpsState.ValueRO.SecondaryEnergy;
            float previousPrimaryEnergy = primaryEnergy;
            float previousSecondaryEnergy = secondaryEnergy;
            float primaryCooldownRemaining = powerUpsState.ValueRO.PrimaryCooldownRemaining;
            float secondaryCooldownRemaining = powerUpsState.ValueRO.SecondaryCooldownRemaining;
            byte primaryIsActive = powerUpsState.ValueRO.PrimaryIsActive;
            byte secondaryIsActive = powerUpsState.ValueRO.SecondaryIsActive;

            TickCooldown(ref primaryCooldownRemaining, deltaTime);
            TickCooldown(ref secondaryCooldownRemaining, deltaTime);
            RechargeSlot(ref primaryEnergy,
                         in powerUpsConfig.PrimarySlot,
                         primaryCooldownRemaining,
                         primaryIsActive,
                         deltaTime,
                         killDelta,
                         roomClearDelta);
            RechargeSlot(ref secondaryEnergy,
                         in powerUpsConfig.SecondarySlot,
                         secondaryCooldownRemaining,
                         secondaryIsActive,
                         deltaTime,
                         killDelta,
                         roomClearDelta);

            if (canEnqueueAudioRequests)
            {
                if (DidReachEnergyRequirement(previousPrimaryEnergy, primaryEnergy, in powerUpsConfig.PrimarySlot) ||
                    DidReachEnergyRequirement(previousSecondaryEnergy, secondaryEnergy, in powerUpsConfig.SecondarySlot))
                {
                    GameAudioEventRequestUtility.EnqueueGlobal(audioRequests, GameAudioEventId.ActiveEnergyFull);
                }
            }

            powerUpsState.ValueRW.PrimaryEnergy = primaryEnergy;
            powerUpsState.ValueRW.SecondaryEnergy = secondaryEnergy;
            powerUpsState.ValueRW.PrimaryCooldownRemaining = primaryCooldownRemaining;
            powerUpsState.ValueRW.SecondaryCooldownRemaining = secondaryCooldownRemaining;
            powerUpsState.ValueRW.LastObservedGlobalKillCount = globalKillCount;
            powerUpsState.ValueRW.LastObservedRoomClearCount = globalRoomClearCount;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Advances one slot cooldown toward zero without allowing negative runtime values.
    /// </summary>
    /// <param name="cooldownRemaining">Mutable cooldown duration remaining on the slot.</param>
    /// <param name="deltaTime">Scaled simulation delta time for the current frame.</param>
    private static void TickCooldown(ref float cooldownRemaining, float deltaTime)
    {
        if (cooldownRemaining <= 0f)
        {
            cooldownRemaining = 0f;
            return;
        }

        cooldownRemaining -= math.max(0f, deltaTime);

        if (cooldownRemaining < 0f)
            cooldownRemaining = 0f;
    }

    /// <summary>
    /// Applies the configured recharge rule to one slot while respecting cooldown, toggle and maximum-energy gates.
    /// </summary>
    /// <param name="currentEnergy">Mutable energy currently stored by the slot.</param>
    /// <param name="slotConfig">Resolved runtime slot configuration.</param>
    /// <param name="cooldownRemaining">Cooldown duration remaining after the current frame tick.</param>
    /// <param name="isActive">Non-zero when the slot is currently active.</param>
    /// <param name="deltaTime">Scaled simulation delta time used by time-based recharge.</param>
    /// <param name="killDelta">New global enemy kills observed since the previous recharge pass.</param>
    /// <param name="roomClearDelta">New procedural room clears observed since the previous recharge pass.</param>
    private static void RechargeSlot(ref float currentEnergy,
                                     in PlayerPowerUpSlotConfig slotConfig,
                                     float cooldownRemaining,
                                     byte isActive,
                                     float deltaTime,
                                     uint killDelta,
                                     uint roomClearDelta)
    {
        if (slotConfig.IsDefined == 0)
            return;

        bool isTogglePassiveSlot = slotConfig.ToolKind == ActiveToolKind.PassiveToggle && slotConfig.Toggleable != 0;
        bool isToggleActive = isTogglePassiveSlot && isActive != 0;

        if (cooldownRemaining > 0f && (!isToggleActive || slotConfig.AllowRechargeDuringToggleStartupLock == 0))
            return;

        float maximumEnergy = math.max(0f, slotConfig.MaximumEnergy);

        if (maximumEnergy <= 0f)
            return;

        if (currentEnergy >= maximumEnergy)
        {
            currentEnergy = maximumEnergy;
            return;
        }

        float rechargeAmount = 0f;

        switch (slotConfig.ChargeType)
        {
            case PowerUpChargeType.Time:
                rechargeAmount = math.max(0f, slotConfig.ChargePerTrigger) * deltaTime;
                break;
            case PowerUpChargeType.EnemiesDestroyed:
                rechargeAmount = math.max(0f, slotConfig.ChargePerTrigger) * killDelta;
                break;
            case PowerUpChargeType.RoomsCleared:
                rechargeAmount = math.max(0f, slotConfig.ChargePerTrigger) * roomClearDelta;
                break;
        }

        if (rechargeAmount <= 0f)
            return;

        currentEnergy += rechargeAmount;

        if (currentEnergy > maximumEnergy)
            currentEnergy = maximumEnergy;
    }

    /// <summary>
    /// Checks whether a slot crossed its activation energy requirement during the current recharge pass.
    /// </summary>
    /// <param name="previousEnergy">Energy value before recharge.</param>
    /// <param name="currentEnergy">Energy value after recharge.</param>
    /// <param name="slotConfig">Runtime slot config used to resolve activation threshold.</param>
    /// <returns>True when the threshold was crossed this frame.</returns>
    private static bool DidReachEnergyRequirement(float previousEnergy, float currentEnergy, in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        float maximumEnergy = math.max(0f, slotConfig.MaximumEnergy);

        if (maximumEnergy <= 0f)
            return false;

        float minimumActivationEnergyPercent = math.clamp(slotConfig.MinimumActivationEnergyPercent, 0f, 100f);
        float activationCost = math.max(0f, slotConfig.ActivationCost);
        float requiredEnergy = math.max(activationCost, maximumEnergy * minimumActivationEnergyPercent * 0.01f);

        if (requiredEnergy <= 0f)
            requiredEnergy = maximumEnergy;

        return previousEnergy + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon < requiredEnergy &&
               currentEnergy + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon >= requiredEnergy;
    }
    #endregion

    #endregion
}
