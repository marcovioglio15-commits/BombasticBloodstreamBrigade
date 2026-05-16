using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides shared resource-payment helpers used by active activation and maintenance flows.
/// </summary>
public static class PlayerPowerUpResourceCostUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether the slot can currently pay its activation cost.
    /// </summary>
    /// <param name="slotConfig">Slot configuration containing resource and threshold settings.</param>
    /// <param name="slotEnergy">Current energy value stored for the slot.</param>
    /// <param name="playerEntity">Player entity used to resolve health and shield resources.</param>
    /// <param name="healthLookup">Health lookup used when the activation resource is Health.</param>
    /// <param name="updatedHealth">Cached mutable health value reused within the current caller.</param>
    /// <param name="healthChanged">True when updatedHealth already contains a fetched runtime value.</param>
    /// <param name="shieldLookup">Shield lookup used when the activation resource is Shield.</param>
    /// <param name="updatedShield">Cached mutable shield value reused within the current caller.</param>
    /// <param name="shieldChanged">True when updatedShield already contains a fetched runtime value.</param>
    /// <returns>True when the activation resource check succeeds.</returns>
    public static bool CanPayActivationCost(in PlayerPowerUpSlotConfig slotConfig,
                                            float slotEnergy,
                                            Entity playerEntity,
                                            ref ComponentLookup<PlayerHealth> healthLookup,
                                            ref PlayerHealth updatedHealth,
                                            ref bool healthChanged,
                                            ref ComponentLookup<PlayerShield> shieldLookup,
                                            ref PlayerShield updatedShield,
                                            ref bool shieldChanged)
    {
        float maximumEnergy = math.max(0f, slotConfig.MaximumEnergy);
        float activationCost = math.max(0f, slotConfig.ActivationCost);
        float minimumActivationEnergyPercent = math.clamp(slotConfig.MinimumActivationEnergyPercent, 0f, 100f);

        switch (slotConfig.ActivationResource)
        {
            case PowerUpResourceType.None:
                return true;
            case PowerUpResourceType.Energy:
                if (maximumEnergy <= 0f)
                    return false;

                if (minimumActivationEnergyPercent > 0f)
                {
                    float minimumEnergyRequired = maximumEnergy * (minimumActivationEnergyPercent * 0.01f);

                    if (slotEnergy + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon < minimumEnergyRequired)
                        return false;
                }

                if (activationCost <= 0f)
                    return true;

                if (slotEnergy + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon < activationCost)
                    return false;

                return true;
            default:
                return CanPayFlatResourceCost(slotConfig.ActivationResource,
                                              activationCost,
                                              slotEnergy,
                                              playerEntity,
                                              ref healthLookup,
                                              ref updatedHealth,
                                              ref healthChanged,
                                              ref shieldLookup,
                                              ref updatedShield,
                                              ref shieldChanged);
        }
    }

    /// <summary>
    /// Consumes the slot activation cost from the configured runtime resource.
    /// </summary>
    /// <param name="slotConfig">Slot configuration containing resource and cost settings.</param>
    /// <param name="slotEnergy">Mutable energy value stored for the slot.</param>
    /// <param name="playerEntity">Player entity used to resolve health and shield resources.</param>
    /// <param name="healthLookup">Health lookup used when the activation resource is Health.</param>
    /// <param name="updatedHealth">Cached mutable health value reused within the current caller.</param>
    /// <param name="healthChanged">True when updatedHealth already contains a fetched runtime value.</param>
    /// <param name="shieldLookup">Shield lookup used when the activation resource is Shield.</param>
    /// <param name="updatedShield">Cached mutable shield value reused within the current caller.</param>
    /// <param name="shieldChanged">True when updatedShield already contains a fetched runtime value.</param>
    public static void ConsumeActivationCost(in PlayerPowerUpSlotConfig slotConfig,
                                             ref float slotEnergy,
                                             Entity playerEntity,
                                             ref ComponentLookup<PlayerHealth> healthLookup,
                                             ref PlayerHealth updatedHealth,
                                             ref bool healthChanged,
                                             ref ComponentLookup<PlayerShield> shieldLookup,
                                             ref PlayerShield updatedShield,
                                             ref bool shieldChanged)
    {
        ConsumeFlatResourceCost(slotConfig.ActivationResource,
                                math.max(0f, slotConfig.ActivationCost),
                                ref slotEnergy,
                                playerEntity,
                                ref healthLookup,
                                ref updatedHealth,
                                ref healthChanged,
                                ref shieldLookup,
                                ref updatedShield,
                                ref shieldChanged);
    }

    /// <summary>
    /// Returns whether the specified flat resource cost can be paid right now.
    /// </summary>
    /// <param name="resourceType">Runtime resource checked for payment.</param>
    /// <param name="cost">Cost amount that must be available.</param>
    /// <param name="slotEnergy">Current energy value stored for the slot.</param>
    /// <param name="playerEntity">Player entity used to resolve health and shield resources.</param>
    /// <param name="healthLookup">Health lookup used when the resource type is Health.</param>
    /// <param name="updatedHealth">Cached mutable health value reused within the current caller.</param>
    /// <param name="healthChanged">True when updatedHealth already contains a fetched runtime value.</param>
    /// <param name="shieldLookup">Shield lookup used when the resource type is Shield.</param>
    /// <param name="updatedShield">Cached mutable shield value reused within the current caller.</param>
    /// <param name="shieldChanged">True when updatedShield already contains a fetched runtime value.</param>
    /// <returns>True when the requested resource can pay the flat cost.</returns>
    public static bool CanPayFlatResourceCost(PowerUpResourceType resourceType,
                                              float cost,
                                              float slotEnergy,
                                              Entity playerEntity,
                                              ref ComponentLookup<PlayerHealth> healthLookup,
                                              ref PlayerHealth updatedHealth,
                                              ref bool healthChanged,
                                              ref ComponentLookup<PlayerShield> shieldLookup,
                                              ref PlayerShield updatedShield,
                                              ref bool shieldChanged)
    {
        float resolvedCost = math.max(0f, cost);

        switch (resourceType)
        {
            case PowerUpResourceType.None:
                return true;
            case PowerUpResourceType.Energy:
                if (resolvedCost <= 0f)
                    return true;

                return slotEnergy + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon >= resolvedCost;
            case PowerUpResourceType.Health:
                if (!healthChanged)
                {
                    if (!healthLookup.HasComponent(playerEntity))
                        return false;

                    updatedHealth = healthLookup[playerEntity];
                    healthChanged = true;
                }

                if (resolvedCost <= 0f)
                    return true;

                if (updatedHealth.Current <= resolvedCost)
                    return false;

                return true;
            case PowerUpResourceType.Shield:
                if (!shieldChanged)
                {
                    if (!shieldLookup.HasComponent(playerEntity))
                        return false;

                    updatedShield = shieldLookup[playerEntity];
                    shieldChanged = true;
                }

                if (resolvedCost <= 0f)
                    return true;

                return updatedShield.Current + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon >= resolvedCost;
            default:
                return false;
        }
    }

    /// <summary>
    /// Consumes one flat resource cost from the selected runtime resource.
    /// </summary>
    /// <param name="resourceType">Runtime resource that pays the cost.</param>
    /// <param name="cost">Cost amount to consume.</param>
    /// <param name="slotEnergy">Mutable energy value stored for the slot.</param>
    /// <param name="playerEntity">Player entity used to resolve health and shield resources.</param>
    /// <param name="healthLookup">Health lookup used when the resource type is Health.</param>
    /// <param name="updatedHealth">Cached mutable health value reused within the current caller.</param>
    /// <param name="healthChanged">True when updatedHealth already contains a fetched runtime value.</param>
    /// <param name="shieldLookup">Shield lookup used when the resource type is Shield.</param>
    /// <param name="updatedShield">Cached mutable shield value reused within the current caller.</param>
    /// <param name="shieldChanged">True when updatedShield already contains a fetched runtime value.</param>
    public static void ConsumeFlatResourceCost(PowerUpResourceType resourceType,
                                               float cost,
                                               ref float slotEnergy,
                                               Entity playerEntity,
                                               ref ComponentLookup<PlayerHealth> healthLookup,
                                               ref PlayerHealth updatedHealth,
                                               ref bool healthChanged,
                                               ref ComponentLookup<PlayerShield> shieldLookup,
                                               ref PlayerShield updatedShield,
                                               ref bool shieldChanged)
    {
        float resolvedCost = math.max(0f, cost);

        switch (resourceType)
        {
            case PowerUpResourceType.Energy:
                if (resolvedCost <= 0f)
                    return;

                slotEnergy -= resolvedCost;

                if (slotEnergy < 0f)
                    slotEnergy = 0f;

                return;
            case PowerUpResourceType.Health:
                if (!healthChanged)
                {
                    if (!healthLookup.HasComponent(playerEntity))
                        return;

                    updatedHealth = healthLookup[playerEntity];
                    healthChanged = true;
                }

                if (resolvedCost <= 0f)
                    return;

                updatedHealth.Current -= resolvedCost;

                if (updatedHealth.Current < 0f)
                    updatedHealth.Current = 0f;

                return;
            case PowerUpResourceType.Shield:
                if (!shieldChanged)
                {
                    if (!shieldLookup.HasComponent(playerEntity))
                        return;

                    updatedShield = shieldLookup[playerEntity];
                    shieldChanged = true;
                }

                if (resolvedCost <= 0f)
                    return;

                updatedShield.Current -= resolvedCost;

                if (updatedShield.Current < 0f)
                    updatedShield.Current = 0f;

                return;
        }
    }
    #endregion

    #endregion
}
