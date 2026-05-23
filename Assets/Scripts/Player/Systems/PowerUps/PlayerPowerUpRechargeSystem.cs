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
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        uint globalKillCount = 0u;

        if (SystemAPI.TryGetSingleton<GlobalEnemyKillCounter>(out GlobalEnemyKillCounter killCounter))
            globalKillCount = killCounter.TotalKilled;

        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  RefRW<PlayerPowerUpsState> powerUpsState) in SystemAPI.Query<DynamicBuffer<PlayerPowerUpsConfigElement>, RefRW<PlayerPowerUpsState>>())
        {
            PlayerPowerUpsConfig powerUpsConfig = PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer);
            uint previousKillCount = powerUpsState.ValueRO.LastObservedGlobalKillCount;
            uint killDelta = 0u;

            if (globalKillCount >= previousKillCount)
                killDelta = globalKillCount - previousKillCount;
            else
                killDelta = globalKillCount;

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
                         killDelta);
            RechargeSlot(ref secondaryEnergy,
                         in powerUpsConfig.SecondarySlot,
                         secondaryCooldownRemaining,
                         secondaryIsActive,
                         deltaTime,
                         killDelta);

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
        }
    }
    #endregion

    #region Helpers
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

    private static void RechargeSlot(ref float currentEnergy,
                                     in PlayerPowerUpSlotConfig slotConfig,
                                     float cooldownRemaining,
                                     byte isActive,
                                     float deltaTime,
                                     uint killDelta)
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
