using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Shares runtime acquisition helpers for passive power-ups granted by milestone selections and combo rank rewards.
/// none.
/// </summary>
internal static class PlayerPowerUpPassiveUnlockRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Finds one passive unlock catalog entry by PowerUpId.
    /// </summary>
    /// <param name="passivePowerUpId">Passive PowerUpId requested by the caller.</param>
    /// <param name="unlockCatalog">Runtime unlock catalog scanned for a passive entry.</param>
    /// <param name="catalogIndex">Resolved catalog index when a matching passive entry is found.</param>
    /// <returns>True when the catalog contains the requested passive PowerUpId.</returns>
    public static bool TryFindPassiveCatalogIndex(FixedString64Bytes passivePowerUpId,
                                                  DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                  out int catalogIndex)
    {
        catalogIndex = -1;

        if (passivePowerUpId.Length <= 0 || !unlockCatalog.IsCreated)
        {
            return false;
        }

        for (int candidateIndex = 0; candidateIndex < unlockCatalog.Length; candidateIndex++)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[candidateIndex];

            if (catalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Passive)
            {
                continue;
            }

            if (catalogEntry.PowerUpId != passivePowerUpId)
            {
                continue;
            }

            catalogIndex = candidateIndex;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Acquires one passive catalog entry and equips its passive tool on first acquisition when possible.
    /// </summary>
    /// <param name="catalogIndex">Runtime unlock catalog index to acquire.</param>
    /// <param name="unlockCatalog">Mutable runtime unlock catalog updated with unlock ownership.</param>
    /// <param name="equippedPassiveTools">Mutable equipped-passive tool buffer.</param>
    /// <param name="passiveToolsState">Mutable aggregated passive state rebuilt when a tool is equipped.</param>
    /// <param name="applyTarget">Debug label describing the passive apply result.</param>
    /// <returns>True when the catalog entry ownership changed; otherwise false.</returns>
    public static bool TryAcquirePassiveCatalogEntry(int catalogIndex,
                                                     DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                     ref PlayerPassiveToolsState passiveToolsState,
                                                     out string applyTarget)
    {
        return TryAcquirePassiveCatalogEntry(catalogIndex,
                                             unlockCatalog,
                                             equippedPassiveTools,
                                             ref passiveToolsState,
                                             out applyTarget,
                                             out bool _);
    }

    /// <summary>
    /// Acquires one passive catalog entry and reports whether this acquisition equipped the passive tool.
    /// </summary>
    /// <param name="catalogIndex">Runtime unlock catalog index to acquire.</param>
    /// <param name="unlockCatalog">Mutable runtime unlock catalog updated with unlock ownership.</param>
    /// <param name="equippedPassiveTools">Mutable equipped-passive tool buffer.</param>
    /// <param name="passiveToolsState">Mutable aggregated passive state rebuilt when a tool is equipped.</param>
    /// <param name="applyTarget">Debug label describing the passive apply result.</param>
    /// <param name="equippedOnGrant">True when this acquisition added the passive tool to the equipped buffer.</param>
    /// <returns>True when the catalog entry ownership changed; otherwise false.</returns>
    public static bool TryAcquirePassiveCatalogEntry(int catalogIndex,
                                                     DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                     ref PlayerPassiveToolsState passiveToolsState,
                                                     out string applyTarget,
                                                     out bool equippedOnGrant)
    {
        applyTarget = "InvalidCatalogIndex";
        equippedOnGrant = false;

        if (!unlockCatalog.IsCreated || catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
        {
            return false;
        }

        PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

        if (catalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Passive)
        {
            applyTarget = "NotPassive";
            return false;
        }

        int maximumUnlockCount = math.max(1, catalogEntry.MaximumUnlockCount);

        if (catalogEntry.CurrentUnlockCount >= maximumUnlockCount)
        {
            applyTarget = "AcquisitionCapReached";
            return false;
        }

        if (catalogEntry.CurrentUnlockCount <= 0)
        {
            equippedOnGrant = TryEquipPassiveTool(in catalogEntry,
                                                  equippedPassiveTools,
                                                  ref passiveToolsState,
                                                  out applyTarget);
        }
        else
        {
            applyTarget = "PassiveStacked";
        }

        catalogEntry.CurrentUnlockCount = math.min(maximumUnlockCount, catalogEntry.CurrentUnlockCount + 1);
        catalogEntry.IsUnlocked = 1;
        catalogEntry.PendingInitialCharacterTuningApply = 0;
        unlockCatalog[catalogIndex] = catalogEntry;
        return true;
    }

    /// <summary>
    /// Releases one passive catalog stack previously granted by combo rank-up and removes the equipped tool only when that grant equipped it.
    /// </summary>
    /// <param name="grant">Combo passive grant entry being revoked.</param>
    /// <param name="unlockCatalog">Mutable runtime unlock catalog updated with reduced ownership.</param>
    /// <param name="equippedPassiveTools">Mutable equipped-passive tool buffer.</param>
    /// <param name="passiveToolsState">Mutable aggregated passive state rebuilt when a tool is removed.</param>
    /// <returns>True when catalog ownership or equipped passive state changed.</returns>
    public static bool TryReleaseComboPassiveGrant(in PlayerComboPassivePowerUpGrantElement grant,
                                                   DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                   DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                   ref PlayerPassiveToolsState passiveToolsState)
    {
        if (!TryResolveGrantCatalogIndex(in grant, unlockCatalog, out int catalogIndex))
        {
            return false;
        }

        PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

        if (catalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Passive || catalogEntry.CurrentUnlockCount <= 0)
        {
            return false;
        }

        catalogEntry.CurrentUnlockCount = math.max(0, catalogEntry.CurrentUnlockCount - 1);

        if (catalogEntry.CurrentUnlockCount <= 0)
        {
            catalogEntry.IsUnlocked = 0;
            catalogEntry.PendingInitialCharacterTuningApply = 0;
        }

        unlockCatalog[catalogIndex] = catalogEntry;

        if (catalogEntry.CurrentUnlockCount > 0 || grant.EquippedOnGrant == 0)
        {
            return true;
        }

        if (!TryRemoveEquippedPassiveTool(grant.PowerUpId, equippedPassiveTools))
        {
            return true;
        }

        passiveToolsState = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(equippedPassiveTools);
        return true;
    }

    /// <summary>
    /// Equips one passive tool into the passive buffer and rebuilds aggregated passive runtime state.
    /// </summary>
    /// <param name="selectedCatalogEntry">Passive unlock catalog entry containing the passive tool payload.</param>
    /// <param name="equippedPassiveTools">Runtime equipped-passive tool buffer.</param>
    /// <param name="passiveToolsState">Aggregated passive runtime state updated when a tool is added.</param>
    /// <param name="applyTarget">Debug label describing the passive-apply result.</param>
    /// <returns>True when a passive tool was added; otherwise false.</returns>
    public static bool TryEquipPassiveTool(in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                           DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                           ref PlayerPassiveToolsState passiveToolsState,
                                           out string applyTarget)
    {
        PlayerPassiveToolConfig passiveToolConfig = selectedCatalogEntry.PassiveToolConfig;
        applyTarget = "PassiveBuffer";

        if (passiveToolConfig.IsDefined == 0)
        {
            applyTarget = "InvalidPassiveConfig";
            return false;
        }

        if (ContainsPassiveToolKind(equippedPassiveTools, passiveToolConfig.ToolKind))
        {
            applyTarget = "AlreadyEquipped";
            return false;
        }

        equippedPassiveTools.Add(new EquippedPassiveToolElement
        {
            PowerUpId = selectedCatalogEntry.PowerUpId,
            Tool = passiveToolConfig
        });
        passiveToolsState = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(equippedPassiveTools);
        applyTarget = "PassiveAdded";
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Checks whether one passive tool kind is already present in the equipped buffer.
    /// </summary>
    /// <param name="equippedPassiveTools">Runtime equipped-passive tool buffer.</param>
    /// <param name="toolKind">Passive tool kind to test.</param>
    /// <returns>True when at least one matching passive tool kind exists.</returns>
    private static bool ContainsPassiveToolKind(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                PassiveToolKind toolKind)
    {
        if (!equippedPassiveTools.IsCreated)
        {
            return false;
        }

        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            PlayerPassiveToolConfig candidate = equippedPassiveTools[passiveIndex].Tool;

            if (candidate.IsDefined == 0)
            {
                continue;
            }

            if (candidate.ToolKind != toolKind)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the catalog entry targeted by one combo passive grant using its cached index first, then PowerUpId fallback.
    /// </summary>
    /// <param name="grant">Combo passive grant entry being revoked.</param>
    /// <param name="unlockCatalog">Runtime unlock catalog scanned for the grant target.</param>
    /// <param name="catalogIndex">Resolved catalog index.</param>
    /// <returns>True when a matching passive catalog entry exists.</returns>
    private static bool TryResolveGrantCatalogIndex(in PlayerComboPassivePowerUpGrantElement grant,
                                                    DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                    out int catalogIndex)
    {
        catalogIndex = -1;

        if (!unlockCatalog.IsCreated)
        {
            return false;
        }

        if (grant.CatalogIndex >= 0 && grant.CatalogIndex < unlockCatalog.Length)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[grant.CatalogIndex];

            if (catalogEntry.UnlockKind == PlayerPowerUpUnlockKind.Passive && catalogEntry.PowerUpId == grant.PowerUpId)
            {
                catalogIndex = grant.CatalogIndex;
                return true;
            }
        }

        return TryFindPassiveCatalogIndex(grant.PowerUpId, unlockCatalog, out catalogIndex);
    }

    /// <summary>
    /// Removes one equipped passive tool by PowerUpId.
    /// </summary>
    /// <param name="passivePowerUpId">Passive PowerUpId to remove.</param>
    /// <param name="equippedPassiveTools">Mutable equipped-passive tool buffer.</param>
    /// <returns>True when one equipped passive entry was removed.</returns>
    private static bool TryRemoveEquippedPassiveTool(FixedString64Bytes passivePowerUpId,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        if (passivePowerUpId.Length <= 0 || !equippedPassiveTools.IsCreated)
        {
            return false;
        }

        for (int passiveIndex = equippedPassiveTools.Length - 1; passiveIndex >= 0; passiveIndex--)
        {
            if (equippedPassiveTools[passiveIndex].PowerUpId != passivePowerUpId)
            {
                continue;
            }

            equippedPassiveTools.RemoveAt(passiveIndex);
            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
