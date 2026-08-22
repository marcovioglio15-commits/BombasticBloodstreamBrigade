/// <summary>
/// Owns returning-projectile slot generations and the state transferred through stolen or dropped active power-ups.
/// </summary>
public static class PlayerReturningProjectileLoadoutRuntimeUtility
{
    #region Methods

    #region Generation
    /// <summary>
    /// Advances a non-zero ownership generation while keeping both swappable active identities distinct.
    /// </summary>
    /// <param name="generation">Current slot ownership generation.</param>
    /// <param name="conflictingGeneration">Generation currently owned by the other active identity.</param>
    /// <returns>Next non-zero generation that differs from the other active identity.</returns>
    public static uint AdvanceGeneration(uint generation, uint conflictingGeneration)
    {
        uint nextGeneration = generation == uint.MaxValue ? 1u : generation + 1u;

        if (nextGeneration == 0u || nextGeneration == conflictingGeneration)
            nextGeneration = nextGeneration == uint.MaxValue ? 1u : nextGeneration + 1u;

        return nextGeneration;
    }
    #endregion

    #region Reset
    /// <summary>
    /// Invalidates the returning-projectile ownership currently assigned to one active slot.
    /// </summary>
    /// <param name="slotIndex">Active slot index to reset.</param>
    /// <param name="powerUpsState">Mutable player state carrying slot ownership.</param>
    public static void ResetSlot(int slotIndex, ref PlayerPowerUpsState powerUpsState)
    {
        switch (slotIndex)
        {
            case 0:
                powerUpsState.PrimaryReturningProjectileCount = 0;
                powerUpsState.PrimaryReturningProjectileRecallReadyCount = 0;
                powerUpsState.PrimaryReturningProjectileRecallVersion = 0u;
                powerUpsState.PrimaryReturningProjectileResourceRecallVersion = 0u;
                powerUpsState.PrimaryReturningProjectileResourceDrainActive = 0;
                powerUpsState.PrimaryReturningProjectileReconnectPending = 0;
                powerUpsState.PrimaryReturningProjectileGeneration = AdvanceGeneration(powerUpsState.PrimaryReturningProjectileGeneration,
                                                                                        powerUpsState.SecondaryReturningProjectileGeneration);
                return;
            case 1:
                powerUpsState.SecondaryReturningProjectileCount = 0;
                powerUpsState.SecondaryReturningProjectileRecallReadyCount = 0;
                powerUpsState.SecondaryReturningProjectileRecallVersion = 0u;
                powerUpsState.SecondaryReturningProjectileResourceRecallVersion = 0u;
                powerUpsState.SecondaryReturningProjectileResourceDrainActive = 0;
                powerUpsState.SecondaryReturningProjectileReconnectPending = 0;
                powerUpsState.SecondaryReturningProjectileGeneration = AdvanceGeneration(powerUpsState.SecondaryReturningProjectileGeneration,
                                                                                          powerUpsState.PrimaryReturningProjectileGeneration);
                return;
        }
    }

    /// <summary>
    /// Invalidates every live returning-projectile registration after loadout reset or out-of-band projectile cleanup.
    /// </summary>
    /// <param name="powerUpsState">Mutable player state whose active-slot ownership is reset.</param>
    public static void ResetConcurrency(ref PlayerPowerUpsState powerUpsState)
    {
        ResetSlot(0, ref powerUpsState);
        ResetSlot(1, ref powerUpsState);
        powerUpsState.PrimaryStolenReturningProjectileGeneration = 0u;
        powerUpsState.SecondaryStolenReturningProjectileGeneration = 0u;
        powerUpsState.PrimaryStolenReturningProjectilePolicy = ProjectileStolenOwnershipPolicy.Despawn;
        powerUpsState.SecondaryStolenReturningProjectilePolicy = ProjectileStolenOwnershipPolicy.Despawn;
    }
    #endregion

    #region Storage
    /// <summary>
    /// Captures live returning-projectile ownership into an active power-up payload before its slot is removed.
    /// </summary>
    /// <param name="slotIndex">Active slot index being captured.</param>
    /// <param name="powerUpsState">Current player runtime state.</param>
    /// <param name="storedPowerUp">Stored active payload receiving ownership state.</param>
    public static void CaptureSnapshot(int slotIndex,
                                       in PlayerPowerUpsState powerUpsState,
                                       ref PlayerStoredActivePowerUpData storedPowerUp)
    {
        if (storedPowerUp.SlotConfig.HasReturningProjectiles == 0)
            return;

        switch (slotIndex)
        {
            case 0:
                storedPowerUp.ReturningProjectileCount = powerUpsState.PrimaryReturningProjectileCount;
                storedPowerUp.ReturningProjectileRecallReadyCount = powerUpsState.PrimaryReturningProjectileRecallReadyCount;
                storedPowerUp.ReturningProjectileGeneration = powerUpsState.PrimaryReturningProjectileGeneration;
                storedPowerUp.ReturningProjectileRecallVersion = powerUpsState.PrimaryReturningProjectileRecallVersion;
                storedPowerUp.ReturningProjectileResourceRecallVersion = powerUpsState.PrimaryReturningProjectileResourceRecallVersion;
                storedPowerUp.ReturningProjectileResourceDrainActive = powerUpsState.PrimaryReturningProjectileResourceDrainActive;
                return;
            case 1:
                storedPowerUp.ReturningProjectileCount = powerUpsState.SecondaryReturningProjectileCount;
                storedPowerUp.ReturningProjectileRecallReadyCount = powerUpsState.SecondaryReturningProjectileRecallReadyCount;
                storedPowerUp.ReturningProjectileGeneration = powerUpsState.SecondaryReturningProjectileGeneration;
                storedPowerUp.ReturningProjectileRecallVersion = powerUpsState.SecondaryReturningProjectileRecallVersion;
                storedPowerUp.ReturningProjectileResourceRecallVersion = powerUpsState.SecondaryReturningProjectileResourceRecallVersion;
                storedPowerUp.ReturningProjectileResourceDrainActive = powerUpsState.SecondaryReturningProjectileResourceDrainActive;
                return;
        }
    }

    /// <summary>
    /// Applies the configured stolen-projectile policy to a captured active after its slot has been removed.
    /// </summary>
    /// <param name="slotIndex">Original active slot index.</param>
    /// <param name="storedPowerUp">Mutable stored payload carrying the captured returning-projectile identity.</param>
    /// <param name="powerUpsState">Mutable player state receiving the suspension or despawn command.</param>
    public static void ApplyStolenOwnershipPolicy(int slotIndex,
                                                  ref PlayerStoredActivePowerUpData storedPowerUp,
                                                  ref PlayerPowerUpsState powerUpsState)
    {
        if (storedPowerUp.SlotConfig.HasReturningProjectiles == 0 ||
            storedPowerUp.ReturningProjectileCount <= 0 ||
            storedPowerUp.ReturningProjectileGeneration == 0u)
        {
            return;
        }

        ProjectileStolenOwnershipPolicy policy = storedPowerUp.SlotConfig.ReturningProjectiles.StolenOwnershipPolicy;
        storedPowerUp.PreserveReturningProjectileOwnership = policy == ProjectileStolenOwnershipPolicy.PreserveAndReconnect
            ? (byte)1
            : (byte)0;

        switch (slotIndex)
        {
            case 0:
                powerUpsState.PrimaryStolenReturningProjectileGeneration = storedPowerUp.ReturningProjectileGeneration;
                powerUpsState.PrimaryStolenReturningProjectilePolicy = policy;
                return;
            case 1:
                powerUpsState.SecondaryStolenReturningProjectileGeneration = storedPowerUp.ReturningProjectileGeneration;
                powerUpsState.SecondaryStolenReturningProjectilePolicy = policy;
                return;
        }
    }

    /// <summary>
    /// Restores preserved projectile ownership or initializes a fresh generation for an ordinary stored active.
    /// </summary>
    /// <param name="slotIndex">Destination active slot index.</param>
    /// <param name="storedPowerUp">Stored payload providing optional preserved ownership.</param>
    /// <param name="powerUpsState">Mutable player state receiving the restored identity.</param>
    public static void RestoreSnapshot(int slotIndex,
                                       in PlayerStoredActivePowerUpData storedPowerUp,
                                       ref PlayerPowerUpsState powerUpsState)
    {
        bool preserveOwnership = storedPowerUp.PreserveReturningProjectileOwnership != 0 &&
                                 storedPowerUp.ReturningProjectileCount > 0 &&
                                 storedPowerUp.ReturningProjectileGeneration != 0u;

        switch (slotIndex)
        {
            case 0:
                powerUpsState.PrimaryReturningProjectileCount = preserveOwnership ? storedPowerUp.ReturningProjectileCount : 0;
                powerUpsState.PrimaryReturningProjectileRecallReadyCount = preserveOwnership ? storedPowerUp.ReturningProjectileRecallReadyCount : 0;
                powerUpsState.PrimaryReturningProjectileRecallVersion = preserveOwnership ? storedPowerUp.ReturningProjectileRecallVersion : 0u;
                powerUpsState.PrimaryReturningProjectileResourceRecallVersion = preserveOwnership ? storedPowerUp.ReturningProjectileResourceRecallVersion : 0u;
                powerUpsState.PrimaryReturningProjectileResourceDrainActive = preserveOwnership ? storedPowerUp.ReturningProjectileResourceDrainActive : (byte)0;
                powerUpsState.PrimaryReturningProjectileReconnectPending = preserveOwnership ? (byte)1 : (byte)0;
                powerUpsState.PrimaryReturningProjectileGeneration = preserveOwnership
                    ? storedPowerUp.ReturningProjectileGeneration
                    : AdvanceGeneration(powerUpsState.PrimaryReturningProjectileGeneration,
                                        powerUpsState.SecondaryReturningProjectileGeneration);
                ClearStolenOwnershipCommand(storedPowerUp.ReturningProjectileGeneration, ref powerUpsState);
                return;
            case 1:
                powerUpsState.SecondaryReturningProjectileCount = preserveOwnership ? storedPowerUp.ReturningProjectileCount : 0;
                powerUpsState.SecondaryReturningProjectileRecallReadyCount = preserveOwnership ? storedPowerUp.ReturningProjectileRecallReadyCount : 0;
                powerUpsState.SecondaryReturningProjectileRecallVersion = preserveOwnership ? storedPowerUp.ReturningProjectileRecallVersion : 0u;
                powerUpsState.SecondaryReturningProjectileResourceRecallVersion = preserveOwnership ? storedPowerUp.ReturningProjectileResourceRecallVersion : 0u;
                powerUpsState.SecondaryReturningProjectileResourceDrainActive = preserveOwnership ? storedPowerUp.ReturningProjectileResourceDrainActive : (byte)0;
                powerUpsState.SecondaryReturningProjectileReconnectPending = preserveOwnership ? (byte)1 : (byte)0;
                powerUpsState.SecondaryReturningProjectileGeneration = preserveOwnership
                    ? storedPowerUp.ReturningProjectileGeneration
                    : AdvanceGeneration(powerUpsState.SecondaryReturningProjectileGeneration,
                                        powerUpsState.PrimaryReturningProjectileGeneration);
                ClearStolenOwnershipCommand(storedPowerUp.ReturningProjectileGeneration, ref powerUpsState);
                return;
        }
    }
    #endregion

    #region Commands
    /// <summary>
    /// Clears a suspension or despawn command after its stored active reconnects to a slot.
    /// </summary>
    /// <param name="generation">Returning-projectile generation being restored.</param>
    /// <param name="powerUpsState">Mutable player state carrying stolen ownership commands.</param>
    private static void ClearStolenOwnershipCommand(uint generation, ref PlayerPowerUpsState powerUpsState)
    {
        if (generation == 0u)
            return;

        if (powerUpsState.PrimaryStolenReturningProjectileGeneration == generation)
        {
            powerUpsState.PrimaryStolenReturningProjectileGeneration = 0u;
            powerUpsState.PrimaryStolenReturningProjectilePolicy = ProjectileStolenOwnershipPolicy.Despawn;
        }

        if (powerUpsState.SecondaryStolenReturningProjectileGeneration == generation)
        {
            powerUpsState.SecondaryStolenReturningProjectileGeneration = 0u;
            powerUpsState.SecondaryStolenReturningProjectilePolicy = ProjectileStolenOwnershipPolicy.Despawn;
        }
    }
    #endregion

    #endregion
}
