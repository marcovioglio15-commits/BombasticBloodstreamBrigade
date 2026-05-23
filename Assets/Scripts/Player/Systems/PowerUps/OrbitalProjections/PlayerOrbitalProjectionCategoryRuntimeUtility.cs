using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Centralizes runtime category-id filtering for player-owned orbital projections.
/// </summary>
public static class PlayerOrbitalProjectionCategoryRuntimeUtility
{
    #region Methods

    #region Managed Category Sets
    /// <summary>
    /// Adds category ids from currently equipped passive tools to the provided managed exclusion set.
    /// </summary>
    /// <param name="equippedPassiveTools">Equipped passive tools currently owned by the player.</param>
    /// <param name="categoryIds">Managed category set updated in place.</param>
    public static void AddEquippedPassiveCategories(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                    HashSet<string> categoryIds)
    {
        if (!equippedPassiveTools.IsCreated || categoryIds == null)
            return;

        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            PlayerPassiveToolConfig passiveToolConfig = equippedPassiveTools[passiveIndex].Tool;
            AddProjectionCategories(in passiveToolConfig, categoryIds);
        }
    }

    /// <summary>
    /// Adds currently active toggle-owned projection category ids to the provided managed exclusion set.
    /// </summary>
    /// <param name="powerUpsConfig">Active slot config that owns toggle passive payloads.</param>
    /// <param name="powerUpsState">Active slot state used to know which toggles are currently active.</param>
    /// <param name="categoryIds">Managed category set updated in place.</param>
    public static void AddActiveToggleCategories(in PlayerPowerUpsConfig powerUpsConfig,
                                                 in PlayerPowerUpsState powerUpsState,
                                                 HashSet<string> categoryIds)
    {
        if (categoryIds == null)
            return;

        if (powerUpsState.PrimaryIsActive != 0)
            AddProjectionCategories(in powerUpsConfig.PrimarySlot.TogglePassiveTool, categoryIds);

        if (powerUpsState.SecondaryIsActive != 0)
            AddProjectionCategories(in powerUpsConfig.SecondarySlot.TogglePassiveTool, categoryIds);
    }

    /// <summary>
    /// Adds category ids from one passive-tool config to the provided managed exclusion set.
    /// </summary>
    /// <param name="passiveToolConfig">Passive tool config to scan.</param>
    /// <param name="categoryIds">Managed category set updated in place.</param>
    public static void AddProjectionCategories(in PlayerPassiveToolConfig passiveToolConfig,
                                               HashSet<string> categoryIds)
    {
        if (passiveToolConfig.IsDefined == 0 ||
            passiveToolConfig.HasOrbitalProjections == 0 ||
            categoryIds == null)
        {
            return;
        }

        for (int projectionIndex = 0; projectionIndex < passiveToolConfig.OrbitalProjections.Length; projectionIndex++)
        {
            OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[projectionIndex];
            AddProjectionCategory(in projectionConfig, categoryIds);
        }
    }

    /// <summary>
    /// Adds category ids from one unlock catalog entry to the provided managed exclusion set.
    /// </summary>
    /// <param name="unlockEntry">Catalog entry whose orbital projection payloads should reserve categories.</param>
    /// <param name="categoryIds">Managed category set updated in place.</param>
    public static void AddCatalogEntryCategories(in PlayerPowerUpUnlockCatalogElement unlockEntry,
                                                 HashSet<string> categoryIds)
    {
        if (categoryIds == null)
            return;

        switch (unlockEntry.UnlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                AddProjectionCategories(in unlockEntry.ActiveSlotConfig.TriggeredProjectilePassiveTool, categoryIds);
                AddProjectionCategories(in unlockEntry.ActiveSlotConfig.TogglePassiveTool, categoryIds);
                return;
            case PlayerPowerUpUnlockKind.Passive:
                AddProjectionCategories(in unlockEntry.PassiveToolConfig, categoryIds);
                return;
        }
    }

    /// <summary>
    /// Resolves whether every filter-enabled projection in the catalog entry is already represented by category id.
    /// </summary>
    /// <param name="unlockEntry">Catalog entry being considered for a milestone offer.</param>
    /// <param name="blockedCategoryIds">Category ids already present on the player or reserved by the current roll.</param>
    /// <returns>True when the entry would add no new filter-enabled projection category.</returns>
    public static bool IsCatalogEntryFullyBlocked(in PlayerPowerUpUnlockCatalogElement unlockEntry,
                                                  HashSet<string> blockedCategoryIds)
    {
        if (blockedCategoryIds == null || blockedCategoryIds.Count <= 0)
            return false;

        switch (unlockEntry.UnlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                return AreAllFilteredProjectionsBlocked(in unlockEntry.ActiveSlotConfig.TriggeredProjectilePassiveTool,
                                                        in unlockEntry.ActiveSlotConfig.TogglePassiveTool,
                                                        blockedCategoryIds);
            case PlayerPowerUpUnlockKind.Passive:
                return AreAllFilteredProjectionsBlocked(in unlockEntry.PassiveToolConfig, blockedCategoryIds);
            default:
                return false;
        }
    }
    #endregion

    #region Config Filtering
    /// <summary>
    /// Removes filter-enabled projections whose category id is already present in equipped passive tools.
    /// </summary>
    /// <param name="passiveToolConfig">Passive tool config being granted.</param>
    /// <param name="equippedPassiveTools">Equipped passive tools already owned by the player.</param>
    /// <returns>Filtered passive config that keeps only projections capable of adding a new category.</returns>
    public static PlayerPassiveToolConfig FilterBlockedProjectionCategories(in PlayerPassiveToolConfig passiveToolConfig,
                                                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        if (passiveToolConfig.IsDefined == 0 || passiveToolConfig.HasOrbitalProjections == 0)
            return passiveToolConfig;

        HashSet<string> blockedCategoryIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        AddEquippedPassiveCategories(equippedPassiveTools, blockedCategoryIds);
        return FilterBlockedProjectionCategories(in passiveToolConfig, blockedCategoryIds);
    }

    /// <summary>
    /// Removes filter-enabled projections whose category id is already present in a managed category set.
    /// </summary>
    /// <param name="passiveToolConfig">Passive tool config being granted.</param>
    /// <param name="blockedCategoryIds">Category ids already present or already kept by this filter pass.</param>
    /// <returns>Filtered passive config that keeps only projections capable of adding a new category.</returns>
    public static PlayerPassiveToolConfig FilterBlockedProjectionCategories(in PlayerPassiveToolConfig passiveToolConfig,
                                                                            HashSet<string> blockedCategoryIds)
    {
        if (passiveToolConfig.IsDefined == 0 ||
            passiveToolConfig.HasOrbitalProjections == 0 ||
            blockedCategoryIds == null)
        {
            return passiveToolConfig;
        }

        PlayerPassiveToolConfig filteredConfig = passiveToolConfig;
        FixedList512Bytes<OrbitalProjectionConfig> filteredProjections = default;

        for (int projectionIndex = 0; projectionIndex < passiveToolConfig.OrbitalProjections.Length; projectionIndex++)
        {
            OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[projectionIndex];

            if (projectionConfig.UseCategoryIdAsExclusionFilter != 0 &&
                projectionConfig.CategoryId.Length > 0 &&
                blockedCategoryIds.Contains(projectionConfig.CategoryId.ToString().Trim()))
            {
                continue;
            }

            if (filteredProjections.Length < filteredProjections.Capacity)
                filteredProjections.Add(projectionConfig);

            AddProjectionCategory(in projectionConfig, blockedCategoryIds);
        }

        filteredConfig.OrbitalProjections = filteredProjections;
        filteredConfig.HasOrbitalProjections = filteredProjections.Length > 0 ? (byte)1 : (byte)0;
        return filteredConfig;
    }
    #endregion

    #region Spawn Filtering
    /// <summary>
    /// Resolves whether a projection config should be skipped because its category is already live on the player.
    /// </summary>
    /// <param name="projectionConfig">Projection config being considered for spawn.</param>
    /// <param name="projectionInstances">Live projection snapshot captured before the spawn pass.</param>
    /// <param name="ownerEntity">Player entity that would own the projection.</param>
    /// <returns>True when a non-despawning projection with the same category already exists.</returns>
    public static bool ShouldSkipCategorySpawn(in OrbitalProjectionConfig projectionConfig,
                                               NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                               Entity ownerEntity)
    {
        if (projectionConfig.UseCategoryIdAsExclusionFilter == 0 || projectionConfig.CategoryId.Length <= 0)
            return false;

        for (int instanceIndex = 0; instanceIndex < projectionInstances.Length; instanceIndex++)
        {
            PlayerOrbitalProjectionInstance instance = projectionInstances[instanceIndex];

            if (instance.OwnerEntity != ownerEntity)
                continue;

            if (instance.Phase == PlayerOrbitalProjectionPhase.Despawning)
                continue;

            if (instance.Config.CategoryId != projectionConfig.CategoryId)
                continue;

            return true;
        }

        return false;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one filter-enabled projection category to a managed category set.
    /// </summary>
    /// <param name="projectionConfig">Projection config to inspect.</param>
    /// <param name="categoryIds">Managed category set updated in place.</param>
    private static void AddProjectionCategory(in OrbitalProjectionConfig projectionConfig,
                                              HashSet<string> categoryIds)
    {
        if (projectionConfig.UseCategoryIdAsExclusionFilter == 0 ||
            projectionConfig.CategoryId.Length <= 0 ||
            categoryIds == null)
        {
            return;
        }

        categoryIds.Add(projectionConfig.CategoryId.ToString().Trim());
    }

    /// <summary>
    /// Resolves whether all filter-enabled projections in one passive config are blocked.
    /// </summary>
    /// <param name="passiveToolConfig">Passive config to inspect.</param>
    /// <param name="blockedCategoryIds">Category ids already present or reserved.</param>
    /// <returns>True when the config has at least one filtered projection and none can add a new category.</returns>
    private static bool AreAllFilteredProjectionsBlocked(in PlayerPassiveToolConfig passiveToolConfig,
                                                         HashSet<string> blockedCategoryIds)
    {
        if (passiveToolConfig.IsDefined == 0 || passiveToolConfig.HasOrbitalProjections == 0)
            return false;

        bool hasFilteredProjection = false;

        for (int projectionIndex = 0; projectionIndex < passiveToolConfig.OrbitalProjections.Length; projectionIndex++)
        {
            OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[projectionIndex];

            if (projectionConfig.UseCategoryIdAsExclusionFilter == 0 || projectionConfig.CategoryId.Length <= 0)
                return false;

            hasFilteredProjection = true;

            if (!blockedCategoryIds.Contains(projectionConfig.CategoryId.ToString().Trim()))
                return false;
        }

        return hasFilteredProjection;
    }

    /// <summary>
    /// Resolves whether all filter-enabled projections in two passive configs are blocked.
    /// </summary>
    /// <param name="firstPassiveToolConfig">First passive config to inspect.</param>
    /// <param name="secondPassiveToolConfig">Second passive config to inspect.</param>
    /// <param name="blockedCategoryIds">Category ids already present or reserved.</param>
    /// <returns>True when at least one filtered projection exists and all of them are blocked.</returns>
    private static bool AreAllFilteredProjectionsBlocked(in PlayerPassiveToolConfig firstPassiveToolConfig,
                                                         in PlayerPassiveToolConfig secondPassiveToolConfig,
                                                         HashSet<string> blockedCategoryIds)
    {
        bool firstHasOrbitalProjection = HasOrbitalProjection(in firstPassiveToolConfig);
        bool secondHasOrbitalProjection = HasOrbitalProjection(in secondPassiveToolConfig);

        if (!firstHasOrbitalProjection && !secondHasOrbitalProjection)
            return false;

        if (firstHasOrbitalProjection && !AreAllFilteredProjectionsBlocked(in firstPassiveToolConfig, blockedCategoryIds))
            return false;

        if (secondHasOrbitalProjection && !AreAllFilteredProjectionsBlocked(in secondPassiveToolConfig, blockedCategoryIds))
            return false;

        return true;
    }

    /// <summary>
    /// Checks whether one passive config contains at least one orbital projection.
    /// </summary>
    /// <param name="passiveToolConfig">Passive config to inspect.</param>
    /// <returns>True when the config carries any orbital projection entry.</returns>
    private static bool HasOrbitalProjection(in PlayerPassiveToolConfig passiveToolConfig)
    {
        if (passiveToolConfig.IsDefined == 0 || passiveToolConfig.HasOrbitalProjections == 0)
            return false;

        return passiveToolConfig.OrbitalProjections.Length > 0;
    }
    #endregion

    #endregion
}
