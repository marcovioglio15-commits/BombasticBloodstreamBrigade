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
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(candidateIndex);

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
                                             -1f,
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
        return TryAcquirePassiveCatalogEntry(catalogIndex,
                                             unlockCatalog,
                                             equippedPassiveTools,
                                             ref passiveToolsState,
                                             -1f,
                                             out applyTarget,
                                             out equippedOnGrant);
    }

    /// <summary>
    /// Acquires one passive catalog entry and records the acquisition time for Power-Up Stealer cooldown protection.
    /// </summary>
    /// <param name="catalogIndex">Runtime unlock catalog index to acquire.</param>
    /// <param name="unlockCatalog">Mutable runtime unlock catalog updated with unlock ownership.</param>
    /// <param name="equippedPassiveTools">Mutable equipped-passive tool buffer.</param>
    /// <param name="passiveToolsState">Mutable aggregated passive state rebuilt when a tool is equipped.</param>
    /// <param name="acquisitionTime">Gameplay elapsed time used as the anti-steal cooldown origin, or negative to leave it unchanged.</param>
    /// <param name="applyTarget">Debug label describing the passive apply result.</param>
    /// <param name="equippedOnGrant">True when this acquisition added the passive tool to the equipped buffer.</param>
    /// <returns>True when the catalog entry ownership changed; otherwise false.</returns>
    public static bool TryAcquirePassiveCatalogEntry(int catalogIndex,
                                                     DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                     ref PlayerPassiveToolsState passiveToolsState,
                                                     float acquisitionTime,
                                                     out string applyTarget,
                                                     out bool equippedOnGrant)
    {
        applyTarget = "InvalidCatalogIndex";
        equippedOnGrant = false;

        if (!unlockCatalog.IsCreated || catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
        {
            return false;
        }

        ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

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

            if (!equippedOnGrant &&
                TryAddPassiveOwnershipMarker(in catalogEntry, equippedPassiveTools, out applyTarget))
            {
                equippedOnGrant = true;
            }
        }
        else
        {
            equippedOnGrant = TryStackOrbitalProjectionTool(in catalogEntry,
                                                            equippedPassiveTools,
                                                            ref passiveToolsState,
                                                            out applyTarget);

            if (!equippedOnGrant)
                applyTarget = "PassiveStacked";
        }

        catalogEntry.CurrentUnlockCount = math.min(maximumUnlockCount, catalogEntry.CurrentUnlockCount + 1);
        catalogEntry.IsUnlocked = 1;
        catalogEntry.PendingInitialCharacterTuningApply = 0;

        if (acquisitionTime >= 0f)
            PlayerPowerUpStealCooldownRuntimeUtility.MarkCatalogEntryAcquired(catalogIndex,
                                                                              unlockCatalog,
                                                                              acquisitionTime);

        return true;
    }

    /// <summary>
    /// Adds an orbital-only passive entry for repeated Stackable acquisitions so projection acquisition policies remain source-aware.
    /// </summary>
    /// <param name="selectedCatalogEntry">Passive catalog entry being acquired again.</param>
    /// <param name="equippedPassiveTools">Runtime equipped-passive tool buffer receiving the orbital-only source.</param>
    /// <param name="passiveToolsState">Aggregated passive runtime state rebuilt after the source is added.</param>
    /// <param name="applyTarget">Debug label describing the stacked orbital result.</param>
    /// <returns>True when an orbital projection source was added for this stacked acquisition.</returns>
    public static bool TryStackOrbitalProjectionTool(in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                     ref PlayerPassiveToolsState passiveToolsState,
                                                     out string applyTarget)
    {
        applyTarget = "NoOrbitalProjectionStack";

        if (!equippedPassiveTools.IsCreated)
            return false;

        if (!TryCreateOrbitalProjectionOnlyTool(in selectedCatalogEntry.PassiveToolConfig,
                                                out PlayerPassiveToolConfig orbitalOnlyTool))
            return false;

        AddEquippedPassiveTool(equippedPassiveTools,
                               selectedCatalogEntry.PowerUpId,
                               in orbitalOnlyTool);
        PlayerPassiveToolsAggregationUtility.RebuildPassiveToolsState(equippedPassiveTools,
                                                                      ref passiveToolsState);
        applyTarget = "PassiveOrbitalProjectionStacked";
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

        ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

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

        if (catalogEntry.CurrentUnlockCount > 0 || grant.EquippedOnGrant == 0)
        {
            return true;
        }

        if (!TryRemoveEquippedPassiveTool(grant.PowerUpId, equippedPassiveTools))
        {
            return true;
        }

        PlayerPassiveToolsAggregationUtility.RebuildPassiveToolsState(equippedPassiveTools,
                                                                      ref passiveToolsState);
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
        PlayerPassiveToolConfig passiveToolConfig;
        PlayerOrbitalProjectionCategoryRuntimeUtility.FilterBlockedProjectionCategories(in selectedCatalogEntry.PassiveToolConfig,
                                                                                       equippedPassiveTools,
                                                                                       out passiveToolConfig);
        applyTarget = "PassiveBuffer";

        if (passiveToolConfig.IsDefined == 0)
        {
            applyTarget = "InvalidPassiveConfig";
            return false;
        }

        if (IsPassiveAlreadyEquipped(in selectedCatalogEntry, in passiveToolConfig, equippedPassiveTools))
        {
            applyTarget = "AlreadyEquipped";
            return false;
        }

        AddEquippedPassiveTool(equippedPassiveTools,
                               selectedCatalogEntry.PowerUpId,
                               in passiveToolConfig);
        PlayerPassiveToolsAggregationUtility.RebuildPassiveToolsState(equippedPassiveTools,
                                                                      ref passiveToolsState);
        applyTarget = "PassiveAdded";
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves whether one catalog passive is already represented by its own id or by an exclusive non-custom passive kind.
    /// </summary>
    /// <param name="selectedCatalogEntry">Catalog entry being considered for runtime equip.</param>
    /// <param name="passiveToolConfig">Runtime passive payload built from the catalog entry.</param>
    /// <param name="equippedPassiveTools">Current equipped-passive runtime buffer.</param>
    /// <returns>True when the passive must not be added again.</returns>
    private static bool IsPassiveAlreadyEquipped(in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                                 in PlayerPassiveToolConfig passiveToolConfig,
                                                 DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        if (ContainsPassivePowerUpId(equippedPassiveTools, selectedCatalogEntry.PowerUpId))
            return true;

        if (passiveToolConfig.ToolKind == PassiveToolKind.Custom)
            return false;

        return ContainsPassiveToolKind(equippedPassiveTools, passiveToolConfig.ToolKind);
    }

    /// <summary>
    /// Builds a passive payload that carries only orbital projection configs from a stackable passive acquisition.
    /// </summary>
    /// <param name="sourceTool">Source passive payload stored in the unlock catalog.</param>
    /// <param name="orbitalOnlyTool">Orbital-only passive tool when the source has projection entries.</param>
    /// <returns>True when an orbital-only tool was created.</returns>
    private static bool TryCreateOrbitalProjectionOnlyTool(in PlayerPassiveToolConfig sourceTool,
                                                           out PlayerPassiveToolConfig orbitalOnlyTool)
    {
        orbitalOnlyTool = default;

        if (sourceTool.IsDefined == 0 ||
            sourceTool.HasOrbitalProjections == 0 ||
            sourceTool.OrbitalProjections.Length <= 0)
        {
            return false;
        }

        orbitalOnlyTool = new PlayerPassiveToolConfig
        {
            IsDefined = 1,
            ToolKind = PassiveToolKind.Custom,
            HasOrbitalProjections = 1,
            OrbitalProjections = sourceTool.OrbitalProjections
        };
        return true;
    }

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
    /// Adds a passive ownership marker when the catalog entry has no runtime tool to equip.
    /// </summary>
    /// <param name="selectedCatalogEntry">Passive catalog entry being acquired.</param>
    /// <param name="equippedPassiveTools">Runtime equipped-passive buffer receiving the marker.</param>
    /// <param name="applyTarget">Debug label describing the marker apply result.</param>
    /// <returns>True when a marker was added to preserve ownership order and Stealer visibility.</returns>
    private static bool TryAddPassiveOwnershipMarker(in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                     out string applyTarget)
    {
        applyTarget = "InvalidPassiveMarker";

        if (!equippedPassiveTools.IsCreated || selectedCatalogEntry.PowerUpId.Length <= 0)
            return false;

        if (ContainsPassivePowerUpId(equippedPassiveTools, selectedCatalogEntry.PowerUpId))
        {
            applyTarget = "PassiveMarkerAlreadyPresent";
            return false;
        }

        PlayerPassiveToolConfig passiveToolConfig = default;
        AddEquippedPassiveTool(equippedPassiveTools,
                               selectedCatalogEntry.PowerUpId,
                               in passiveToolConfig);
        applyTarget = "PassiveOwnershipMarker";
        return true;
    }

    /// <summary>
    /// Checks whether a passive ownership entry already exists for one PowerUpId.
    /// </summary>
    /// <param name="equippedPassiveTools">Runtime equipped-passive buffer to scan.</param>
    /// <param name="powerUpId">PowerUpId to find.</param>
    /// <returns>True when the buffer already contains the requested PowerUpId.</returns>
    private static bool ContainsPassivePowerUpId(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                 FixedString64Bytes powerUpId)
    {
        if (!equippedPassiveTools.IsCreated || powerUpId.Length <= 0)
            return false;

        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            if (equippedPassiveTools[passiveIndex].PowerUpId != powerUpId)
                continue;

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
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(grant.CatalogIndex);

            if (catalogEntry.UnlockKind == PlayerPowerUpUnlockKind.Passive && catalogEntry.PowerUpId == grant.PowerUpId)
            {
                catalogIndex = grant.CatalogIndex;
                return true;
            }
        }

        return TryFindPassiveCatalogIndex(grant.PowerUpId, unlockCatalog, out catalogIndex);
    }

    /// <summary>
    /// Appends one equipped passive entry without passing the large buffer element payload by value.
    /// </summary>
    /// <param name="equippedPassiveTools">Mutable equipped-passive buffer receiving the entry.</param>
    /// <param name="powerUpId">Power-up id stored on the equipped entry.</param>
    /// <param name="passiveToolConfig">Passive tool payload copied into the new buffer slot.</param>
    private static void AddEquippedPassiveTool(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                               FixedString64Bytes powerUpId,
                                               in PlayerPassiveToolConfig passiveToolConfig)
    {
        int passiveIndex = equippedPassiveTools.Length;
        equippedPassiveTools.ResizeUninitialized(passiveIndex + 1);
        ref EquippedPassiveToolElement equippedPassiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);
        equippedPassiveTool.PowerUpId = powerUpId;
        equippedPassiveTool.Tool = passiveToolConfig;
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
