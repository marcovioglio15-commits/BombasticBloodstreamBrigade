using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Tracks short acquisition windows during which enemy Power-Up Stealer modules must ignore newly obtained player power-ups.
/// </summary>
internal static class PlayerPowerUpStealCooldownRuntimeUtility
{
    #region Constants
    private const float TimestampEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Acquisition Tracking
    /// <summary>
    /// Marks one catalog entry as acquired or recovered at the provided gameplay time.
    /// </summary>
    /// <param name="catalogIndex">Catalog index to update.</param>
    /// <param name="unlockCatalog">Mutable player unlock catalog.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used as the cooldown origin.</param>
    /// <returns>True when the catalog entry was found and updated.</returns>
    public static bool MarkCatalogEntryAcquired(int catalogIndex,
                                                DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                float elapsedTime)
    {
        if (!unlockCatalog.IsCreated || catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
            return false;

        PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];
        catalogEntry.LastAcquiredTime = math.max(TimestampEpsilon, elapsedTime);
        unlockCatalog[catalogIndex] = catalogEntry;
        return true;
    }

    /// <summary>
    /// Marks one catalog entry as acquired by matching its power-up id and kind.
    /// </summary>
    /// <param name="powerUpId">Power-up identifier to match.</param>
    /// <param name="unlockKind">Expected active or passive catalog kind.</param>
    /// <param name="unlockCatalog">Mutable player unlock catalog.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used as the cooldown origin.</param>
    /// <returns>True when a matching catalog entry was updated.</returns>
    public static bool TryMarkPowerUpAcquired(FixedString64Bytes powerUpId,
                                              PlayerPowerUpUnlockKind unlockKind,
                                              DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                              float elapsedTime)
    {
        int catalogIndex = FindCatalogIndex(powerUpId, unlockKind, unlockCatalog);

        if (catalogIndex < 0)
            return false;

        return MarkCatalogEntryAcquired(catalogIndex, unlockCatalog, elapsedTime);
    }
    #endregion

    #region Protection
    /// <summary>
    /// Checks whether one catalog entry is inside the configured anti-steal cooldown window.
    /// </summary>
    /// <param name="catalogEntry">Catalog entry being inspected.</param>
    /// <param name="cooldownSeconds">Configured cooldown duration in seconds.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>True when the entry should be ignored by Stealer selection.</returns>
    public static bool IsPowerUpProtectedFromSteal(in PlayerPowerUpUnlockCatalogElement catalogEntry,
                                                   float cooldownSeconds,
                                                   float elapsedTime)
    {
        float sanitizedCooldown = math.max(0f, cooldownSeconds);

        if (sanitizedCooldown <= 0f)
            return false;

        if (catalogEntry.LastAcquiredTime <= 0f)
            return false;

        return elapsedTime < catalogEntry.LastAcquiredTime + sanitizedCooldown;
    }

    /// <summary>
    /// Checks whether a power-up id/kind pair is inside the configured anti-steal cooldown window.
    /// </summary>
    /// <param name="powerUpId">Power-up identifier to match.</param>
    /// <param name="unlockKind">Expected active or passive catalog kind.</param>
    /// <param name="unlockCatalog">Player unlock catalog scanned for the entry.</param>
    /// <param name="cooldownSeconds">Configured cooldown duration in seconds.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>True when the matching entry exists and is temporarily protected.</returns>
    public static bool IsPowerUpProtectedFromSteal(FixedString64Bytes powerUpId,
                                                   PlayerPowerUpUnlockKind unlockKind,
                                                   DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                   float cooldownSeconds,
                                                   float elapsedTime)
    {
        int catalogIndex = FindCatalogIndex(powerUpId, unlockKind, unlockCatalog);

        if (catalogIndex < 0)
            return false;

        PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];
        return IsPowerUpProtectedFromSteal(in catalogEntry, cooldownSeconds, elapsedTime);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds one unlock catalog entry by id and kind.
    /// </summary>
    /// <param name="powerUpId">Power-up identifier to find.</param>
    /// <param name="unlockKind">Expected active or passive catalog kind.</param>
    /// <param name="unlockCatalog">Catalog buffer to scan.</param>
    /// <returns>Catalog index, or -1 when no matching entry exists.</returns>
    private static int FindCatalogIndex(FixedString64Bytes powerUpId,
                                        PlayerPowerUpUnlockKind unlockKind,
                                        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog)
    {
        if (powerUpId.Length <= 0 || !unlockCatalog.IsCreated)
            return -1;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

            if (catalogEntry.UnlockKind != unlockKind)
                continue;

            if (catalogEntry.PowerUpId != powerUpId)
                continue;

            return catalogIndex;
        }

        return -1;
    }
    #endregion

    #endregion
}
