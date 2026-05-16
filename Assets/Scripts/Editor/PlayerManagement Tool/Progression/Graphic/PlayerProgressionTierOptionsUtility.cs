using System;
using System.Collections.Generic;

/// <summary>
/// Provides editor lookup helpers for progression milestone drop-pool and tier selectors.
/// </summary>
public static class PlayerProgressionTierOptionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects distinct tier IDs from the power-ups preset currently relevant to the edited progression context.
    /// </summary>
    /// <returns>Sorted list of available tier IDs.</returns>
    public static List<string> BuildTierIdOptionsFromPowerUpsLibrary()
    {
        List<string> tierIds = new List<string>();

        if (TryResolveScopedPowerUpsPreset(out PlayerPowerUpsPreset scopedPreset))
        {
            HashSet<string> visitedTierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectTierIds(scopedPreset, tierIds, visitedTierIds);
            tierIds.Sort(StringComparer.OrdinalIgnoreCase);
            return tierIds;
        }

        PlayerPowerUpsPresetLibrary library = PlayerPowerUpsPresetLibraryUtility.GetOrCreateLibrary();

        if (library == null || library.Presets == null)
            return tierIds;

        HashSet<string> visitedGlobalTierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int presetIndex = 0; presetIndex < library.Presets.Count; presetIndex++)
            CollectTierIds(library.Presets[presetIndex], tierIds, visitedGlobalTierIds);

        tierIds.Sort(StringComparer.OrdinalIgnoreCase);
        return tierIds;
    }

    /// <summary>
    /// Collects distinct drop-pool IDs from the power-ups preset currently relevant to the edited progression context.
    /// </summary>
    /// <returns>Sorted list of available drop-pool IDs.</returns>
    public static List<string> BuildDropPoolIdOptionsFromPowerUpsLibrary()
    {
        List<string> dropPoolIds = new List<string>();

        if (TryResolveScopedPowerUpsPreset(out PlayerPowerUpsPreset scopedPreset))
        {
            HashSet<string> visitedDropPoolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectDropPoolIds(scopedPreset, dropPoolIds, visitedDropPoolIds);
            dropPoolIds.Sort(StringComparer.OrdinalIgnoreCase);
            return dropPoolIds;
        }

        PlayerPowerUpsPresetLibrary library = PlayerPowerUpsPresetLibraryUtility.GetOrCreateLibrary();

        if (library == null || library.Presets == null)
            return dropPoolIds;

        HashSet<string> visitedGlobalDropPoolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int presetIndex = 0; presetIndex < library.Presets.Count; presetIndex++)
            CollectDropPoolIds(library.Presets[presetIndex], dropPoolIds, visitedGlobalDropPoolIds);

        dropPoolIds.Sort(StringComparer.OrdinalIgnoreCase);
        return dropPoolIds;
    }

    /// <summary>
    /// Resolves the scoped power-ups preset used by progression milestone selectors in the current editor context.
    /// </summary>
    /// <param name="scopedPreset">Resolved power-ups preset when available.</param>
    /// <returns>True when a scoped power-ups preset was found; otherwise false.</returns>
    public static bool TryResolveScopedPowerUpsPreset(out PlayerPowerUpsPreset scopedPreset)
    {
        scopedPreset = null;
        PlayerMasterPreset activeMasterPreset = PlayerManagementSelectionContext.ActiveMasterPreset;

        if (activeMasterPreset != null && activeMasterPreset.PowerUpsPreset != null)
        {
            scopedPreset = activeMasterPreset.PowerUpsPreset;
            return true;
        }

        PlayerProgressionPreset activeProgressionPreset = PlayerManagementSelectionContext.ActiveProgressionPreset;

        if (activeProgressionPreset != null &&
            TryResolvePowerUpsPresetFromProgression(activeProgressionPreset, out PlayerPowerUpsPreset powerUpsPresetFromProgression))
        {
            scopedPreset = powerUpsPresetFromProgression;
            return true;
        }

        if (PlayerManagementSelectionContext.ActivePowerUpsPreset != null)
        {
            scopedPreset = PlayerManagementSelectionContext.ActivePowerUpsPreset;
            return true;
        }

        return false;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the power-ups preset linked to the first master preset that references the provided progression preset.
    /// </summary>
    /// <param name="progressionPreset">Progression preset currently edited.</param>
    /// <param name="powerUpsPreset">Resolved power-ups preset when a referencing master preset is found.</param>
    /// <returns>True when a referencing master preset with a power-ups preset exists; otherwise false.</returns>
    private static bool TryResolvePowerUpsPresetFromProgression(PlayerProgressionPreset progressionPreset, out PlayerPowerUpsPreset powerUpsPreset)
    {
        powerUpsPreset = null;

        if (progressionPreset == null)
            return false;

        PlayerMasterPresetLibrary masterLibrary = PlayerMasterPresetLibraryUtility.GetOrCreateLibrary();

        if (masterLibrary == null || masterLibrary.Presets == null)
            return false;

        for (int presetIndex = 0; presetIndex < masterLibrary.Presets.Count; presetIndex++)
        {
            PlayerMasterPreset masterPreset = masterLibrary.Presets[presetIndex];

            if (masterPreset == null || masterPreset.ProgressionPreset != progressionPreset)
                continue;

            if (masterPreset.PowerUpsPreset == null)
                continue;

            powerUpsPreset = masterPreset.PowerUpsPreset;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Appends distinct tier IDs from one power-ups preset into the provided destination list.
    /// </summary>
    /// <param name="preset">Power-ups preset that owns the tier definitions.</param>
    /// <param name="tierIds">Destination list receiving normalized tier IDs.</param>
    /// <param name="visitedTierIds">Set used to prevent duplicate additions across scanned presets.</param>

    private static void CollectTierIds(PlayerPowerUpsPreset preset, List<string> tierIds, HashSet<string> visitedTierIds)
    {
        if (preset == null || preset.TierLevels == null || tierIds == null || visitedTierIds == null)
            return;

        for (int tierIndex = 0; tierIndex < preset.TierLevels.Count; tierIndex++)
        {
            PowerUpTierLevelDefinition tierLevel = preset.TierLevels[tierIndex];

            if (tierLevel == null || string.IsNullOrWhiteSpace(tierLevel.TierId))
                continue;

            string tierId = tierLevel.TierId.Trim();

            if (!visitedTierIds.Add(tierId))
                continue;

            tierIds.Add(tierId);
        }
    }

    /// <summary>
    /// Appends distinct drop-pool IDs from one power-ups preset into the provided destination list.
    /// </summary>
    /// <param name="preset">Power-ups preset that owns the drop-pool definitions.</param>
    /// <param name="dropPoolIds">Destination list receiving normalized drop-pool IDs.</param>
    /// <param name="visitedDropPoolIds">Set used to prevent duplicate additions across scanned presets.</param>

    private static void CollectDropPoolIds(PlayerPowerUpsPreset preset, List<string> dropPoolIds, HashSet<string> visitedDropPoolIds)
    {
        if (preset == null || preset.DropPools == null || dropPoolIds == null || visitedDropPoolIds == null)
            return;

        for (int dropPoolIndex = 0; dropPoolIndex < preset.DropPools.Count; dropPoolIndex++)
        {
            PowerUpDropPoolDefinition dropPool = preset.DropPools[dropPoolIndex];

            if (dropPool == null || string.IsNullOrWhiteSpace(dropPool.PoolId))
                continue;

            string poolId = dropPool.PoolId.Trim();

            if (!visitedDropPoolIds.Add(poolId))
                continue;

            dropPoolIds.Add(poolId);
        }
    }
    #endregion

    #endregion
}
