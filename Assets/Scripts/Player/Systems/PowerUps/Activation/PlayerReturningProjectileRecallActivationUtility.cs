using Unity.Entities;

/// <summary>
/// Resolves active-input recall ownership and optional Resource Gate repayment for returning projectiles.
/// </summary>
public static class PlayerReturningProjectileRecallActivationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Consumes an additional active-input tap as a returning-projectile recall without re-executing sibling modules.
    /// Invalid early taps remain consumed so concurrent-projectile settings cannot turn a recall input into another activation.
    /// </summary>
    /// <param name="slotConfig">Active slot carrying return mode and optional Resource Gate cost data.</param>
    /// <param name="activeReturningProjectileCount">Number of live projectiles owned by this active slot.</param>
    /// <param name="pressedThisFrame">Whether the owning active input started this frame.</param>
    /// <param name="returningProjectileRecallReadyCount">Mutable number of owned projectiles waiting at an endpoint.</param>
    /// <param name="returningProjectileRecallVersion">Mutable version observed by owned projectiles.</param>
    /// <param name="slotEnergy">Mutable active energy used by optional Resource Gate payment.</param>
    /// <param name="playerEntity">Player entity used by health and shield resource costs.</param>
    /// <param name="healthLookup">Mutable player health lookup.</param>
    /// <param name="updatedHealth">Cached mutable player health.</param>
    /// <param name="healthChanged">Whether cached health has been fetched or changed.</param>
    /// <param name="shieldLookup">Mutable player shield lookup.</param>
    /// <param name="updatedShield">Cached mutable player shield.</param>
    /// <param name="shieldChanged">Whether cached shield has been fetched or changed.</param>
    /// <returns>True when recall mode owns the input and ordinary activation must stop.</returns>
    public static bool TryProcess(in PlayerPowerUpSlotConfig slotConfig,
                                  int activeReturningProjectileCount,
                                  bool pressedThisFrame,
                                  ref int returningProjectileRecallReadyCount,
                                  ref uint returningProjectileRecallVersion,
                                  ref float slotEnergy,
                                  Entity playerEntity,
                                  ref ComponentLookup<PlayerHealth> healthLookup,
                                  ref PlayerHealth updatedHealth,
                                  ref bool healthChanged,
                                  ref ComponentLookup<PlayerShield> shieldLookup,
                                  ref PlayerShield updatedShield,
                                  ref bool shieldChanged)
    {
        if (slotConfig.HasReturningProjectiles == 0 ||
            slotConfig.ReturningProjectiles.ReturnStartMode != ProjectileReturnStartMode.ActivationTap ||
            activeReturningProjectileCount <= 0 ||
            !pressedThisFrame)
        {
            return false;
        }

        if (slotConfig.ReturningProjectiles.AllowEarlyActivationRecall == 0 &&
            returningProjectileRecallReadyCount <= 0)
        {
            return true;
        }

        if (slotConfig.ReturningProjectiles.ReapplyResourceGateCostOnRecall != 0)
        {
            if (!PlayerPowerUpResourceCostUtility.CanPayActivationCost(in slotConfig,
                                                                       slotEnergy,
                                                                       playerEntity,
                                                                       ref healthLookup,
                                                                       ref updatedHealth,
                                                                       ref healthChanged,
                                                                       ref shieldLookup,
                                                                       ref updatedShield,
                                                                       ref shieldChanged))
            {
                return true;
            }

            PlayerPowerUpResourceCostUtility.ConsumeActivationCost(in slotConfig,
                                                                   ref slotEnergy,
                                                                   playerEntity,
                                                                   ref healthLookup,
                                                                   ref updatedHealth,
                                                                   ref healthChanged,
                                                                   ref shieldLookup,
                                                                   ref updatedShield,
                                                                   ref shieldChanged);
        }

        returningProjectileRecallVersion = returningProjectileRecallVersion == uint.MaxValue
            ? 1u
            : returningProjectileRecallVersion + 1u;
        returningProjectileRecallReadyCount = 0;
        return true;
    }
    #endregion

    #endregion
}
