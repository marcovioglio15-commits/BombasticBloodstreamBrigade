using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Consumes a Returning Projectiles active's bound Resource Gate maintenance resource and requests recall at its floor.
/// </summary>
public static class PlayerReturningProjectileResourceDrainUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Reports whether slot-energy recharge must pause while a live returning projectile owns continuous energy drain.
    /// </summary>
    /// <param name="slotConfig">Active slot containing the Returning Projectiles and Resource Gate configuration.</param>
    /// <param name="activeProjectileCount">Number of live returning projectiles still owned by the slot.</param>
    /// <param name="resourceDrainActive">Runtime latch covering outbound travel, endpoint waiting, and return initiation.</param>
    /// <returns>True when ordinary slot-energy recharge would offset the configured continuous drain.</returns>
    public static bool ShouldSuspendEnergyRecharge(in PlayerPowerUpSlotConfig slotConfig,
                                                   int activeProjectileCount,
                                                   byte resourceDrainActive)
    {
        return resourceDrainActive != 0 &&
               activeProjectileCount > 0 &&
               slotConfig.HasReturningProjectiles != 0 &&
               slotConfig.HasResourceGate != 0 &&
               slotConfig.MaintenanceResource == PowerUpResourceType.Energy &&
               ProjectileReturnStartModeUtility.UsesResourceDrain(slotConfig.ReturningProjectiles.ReturnStartMode);
    }

    /// <summary>
    /// Advances one slot's continuous drain once and increments its version when the configured threshold is reached.
    /// </summary>
    /// <param name="slotConfig">Active slot containing Returning Projectiles and its required Resource Gate.</param>
    /// <param name="activeProjectileCount">Number of live returning projectiles owned by the slot.</param>
    /// <param name="deltaTime">Scaled frame delta used by continuous consumption.</param>
    /// <param name="resourceDrainActive">Mutable flag set while the launched projectile still owns continuous drain.</param>
    /// <param name="resourceRecallVersion">Mutable recall version observed by projectiles.</param>
    /// <param name="slotEnergy">Mutable energy resource owned by the active slot.</param>
    /// <param name="playerEntity">Player entity used to access health and shield resources.</param>
    /// <param name="healthLookup">Mutable health lookup used when Resource Gate maintenance consumes health.</param>
    /// <param name="updatedHealth">Cached mutable health reused by both active slots.</param>
    /// <param name="healthChanged">Whether the cached health has been fetched or changed.</param>
    /// <param name="shieldLookup">Mutable shield lookup used when Resource Gate maintenance consumes shield.</param>
    /// <param name="updatedShield">Cached mutable shield reused by both active slots.</param>
    /// <param name="shieldChanged">Whether the cached shield has been fetched or changed.</param>
    public static void Tick(in PlayerPowerUpSlotConfig slotConfig,
                            int activeProjectileCount,
                            float deltaTime,
                            ref byte resourceDrainActive,
                            ref uint resourceRecallVersion,
                            ref float slotEnergy,
                            Entity playerEntity,
                            ref ComponentLookup<PlayerHealth> healthLookup,
                            ref PlayerHealth updatedHealth,
                            ref bool healthChanged,
                            ref ComponentLookup<PlayerShield> shieldLookup,
                            ref PlayerShield updatedShield,
                            ref bool shieldChanged)
    {
        if (resourceDrainActive == 0 ||
            activeProjectileCount <= 0 ||
            slotConfig.HasReturningProjectiles == 0 ||
            slotConfig.HasResourceGate == 0 ||
            !ProjectileReturnStartModeUtility.UsesResourceDrain(slotConfig.ReturningProjectiles.ReturnStartMode))
        {
            return;
        }

        float currentResource;
        float maximumResource;

        if (!TryResolveResource(slotConfig.MaintenanceResource,
                                slotEnergy,
                                slotConfig.MaximumEnergy,
                                playerEntity,
                                ref healthLookup,
                                ref updatedHealth,
                                ref healthChanged,
                                ref shieldLookup,
                                ref updatedShield,
                                ref shieldChanged,
                                out currentResource,
                                out maximumResource))
        {
            return;
        }

        float threshold = maximumResource *
                          (math.clamp(slotConfig.ReturningProjectiles.ResourceReturnThresholdPercent, 0f, 100f) * 0.01f);

        if (slotConfig.MaintenanceResource == PowerUpResourceType.Health)
            threshold = math.max(1f, threshold);

        float availableAboveThreshold = math.max(0f, currentResource - threshold);
        float requestedCost = math.max(0f, slotConfig.MaintenanceCostPerSecond) * math.max(0f, deltaTime);
        float consumedCost = math.min(requestedCost, availableAboveThreshold);

        if (consumedCost > 0f)
        {
            PlayerPowerUpResourceCostUtility.ConsumeFlatResourceCost(slotConfig.MaintenanceResource,
                                                                     consumedCost,
                                                                     ref slotEnergy,
                                                                     playerEntity,
                                                                     ref healthLookup,
                                                                     ref updatedHealth,
                                                                     ref healthChanged,
                                                                     ref shieldLookup,
                                                                     ref updatedShield,
                                                                     ref shieldChanged);
        }

        if (availableAboveThreshold > consumedCost + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon)
            return;

        resourceRecallVersion = AdvanceVersion(resourceRecallVersion);
        resourceDrainActive = 0;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the current and maximum values of one Resource Gate maintenance resource.
    /// </summary>
    /// <param name="resourceType">Resource Gate maintenance resource.</param>
    /// <param name="slotEnergy">Current slot energy.</param>
    /// <param name="maximumEnergy">Maximum energy capacity baked by Resource Gate.</param>
    /// <param name="playerEntity">Player entity used for component resources.</param>
    /// <param name="healthLookup">Mutable player health lookup.</param>
    /// <param name="updatedHealth">Cached mutable health.</param>
    /// <param name="healthChanged">Whether cached health is initialized.</param>
    /// <param name="shieldLookup">Mutable player shield lookup.</param>
    /// <param name="updatedShield">Cached mutable shield.</param>
    /// <param name="shieldChanged">Whether cached shield is initialized.</param>
    /// <param name="currentResource">Resolved current resource value.</param>
    /// <param name="maximumResource">Resolved maximum resource value.</param>
    /// <returns>True when the selected resource is valid and available.</returns>
    private static bool TryResolveResource(PowerUpResourceType resourceType,
                                           float slotEnergy,
                                           float maximumEnergy,
                                           Entity playerEntity,
                                           ref ComponentLookup<PlayerHealth> healthLookup,
                                           ref PlayerHealth updatedHealth,
                                           ref bool healthChanged,
                                           ref ComponentLookup<PlayerShield> shieldLookup,
                                           ref PlayerShield updatedShield,
                                           ref bool shieldChanged,
                                           out float currentResource,
                                           out float maximumResource)
    {
        currentResource = 0f;
        maximumResource = 0f;

        switch (resourceType)
        {
            case PowerUpResourceType.Energy:
                currentResource = math.max(0f, slotEnergy);
                maximumResource = math.max(0f, maximumEnergy);
                return maximumResource > 0f;
            case PowerUpResourceType.Health:
                if (!healthChanged)
                {
                    if (!healthLookup.HasComponent(playerEntity))
                        return false;

                    updatedHealth = healthLookup[playerEntity];
                    healthChanged = true;
                }

                currentResource = math.max(0f, updatedHealth.Current);
                maximumResource = math.max(0f, updatedHealth.Max);
                return maximumResource > 0f;
            case PowerUpResourceType.Shield:
                if (!shieldChanged)
                {
                    if (!shieldLookup.HasComponent(playerEntity))
                        return false;

                    updatedShield = shieldLookup[playerEntity];
                    shieldChanged = true;
                }

                currentResource = math.max(0f, updatedShield.Current);
                maximumResource = math.max(0f, updatedShield.Max);
                return maximumResource > 0f;
            default:
                return false;
        }
    }

    /// <summary>
    /// Advances a non-zero recall version while preserving wraparound safety.
    /// </summary>
    /// <param name="version">Current version.</param>
    /// <returns>Next non-zero version.</returns>
    private static uint AdvanceVersion(uint version)
    {
        return version == uint.MaxValue ? 1u : version + 1u;
    }
    #endregion

    #endregion
}
