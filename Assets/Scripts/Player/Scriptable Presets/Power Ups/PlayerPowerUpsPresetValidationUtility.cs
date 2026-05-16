using System;
using System.Collections.Generic;

/// <summary>
/// Validates serialized preset state and keeps modular power-up data coherent.
/// </summary>
internal static class PlayerPowerUpsPresetValidationUtility
{
    public static void ValidateMetadata(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (string.IsNullOrWhiteSpace(preset.PresetIdMutable))
            preset.PresetIdMutable = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(preset.PresetNameMutable))
            preset.PresetNameMutable = "New Power Ups Preset";

        if (string.IsNullOrWhiteSpace(preset.VersionMutable))
            preset.VersionMutable = "1.0.0";

        if (string.IsNullOrWhiteSpace(preset.PrimaryToolActionIdMutable))
            preset.PrimaryToolActionIdMutable = "PowerUpPrimary";

        if (string.IsNullOrWhiteSpace(preset.SecondaryToolActionIdMutable))
            preset.SecondaryToolActionIdMutable = "PowerUpSecondary";

        if (string.IsNullOrWhiteSpace(preset.SwapSlotsActionIdMutable))
            preset.SwapSlotsActionIdMutable = "PowerUpSwapSlots";
    }

    public static void ValidateCollections(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (preset.TierLevelsMutable == null)
            preset.TierLevelsMutable = new List<PowerUpTierLevelDefinition>();

        if (preset.DropPoolsMutable == null)
            preset.DropPoolsMutable = new List<PowerUpDropPoolDefinition>();

        if (preset.DropPoolCatalogMutable == null)
            preset.DropPoolCatalogMutable = new List<string>();

        if (preset.ScalingRulesMutable == null)
            preset.ScalingRulesMutable = new List<PlayerStatScalingRule>();

        if (preset.ModuleDefinitionsMutable == null)
            preset.ModuleDefinitionsMutable = new List<PowerUpModuleDefinition>();

        if (preset.ActivePowerUpsMutable == null)
            preset.ActivePowerUpsMutable = new List<ModularPowerUpDefinition>();

        if (preset.PassivePowerUpsMutable == null)
            preset.PassivePowerUpsMutable = new List<ModularPowerUpDefinition>();

        if (preset.EquippedPassivePowerUpIdsMutable == null)
            preset.EquippedPassivePowerUpIdsMutable = new List<string>();

        if (preset.PassiveToolsMutable == null)
            preset.PassiveToolsMutable = new List<PassiveToolDefinition>();

        if (preset.ElementalVfxByElementMutable == null)
            preset.ElementalVfxByElementMutable = new List<ElementalVfxByElementData>();

        if (preset.ActiveToolsMutable == null)
            preset.ActiveToolsMutable = new List<ActiveToolDefinition>();

        if (preset.EquippedPassiveToolIdsMutable == null)
            preset.EquippedPassiveToolIdsMutable = new List<string>();

        if (preset.DropPoolCatalogMutable.Count > 0)
            return;

        preset.DropPoolCatalogMutable.Add("Milestone");
        preset.DropPoolCatalogMutable.Add("Shop");
        preset.DropPoolCatalogMutable.Add("Boss");
    }

    public static void ValidateEntries(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        PlayerPowerUpsPresetDefaultsUtility.GenerateDefaultModularSetupIfEmpty(preset);
        PlayerPowerUpsPresetMigrationUtility.MigrateModuleDefinitionsToUnifiedKinds(preset);
        ValidateScalingRules(preset.ScalingRulesMutable);

        ValidatePassiveTools(preset.PassiveToolsMutable);
        ValidateActiveTools(preset.ActiveToolsMutable);
        ValidateModuleDefinitions(preset.ModuleDefinitionsMutable);
        ValidateModularPowerUps(preset.ActivePowerUpsMutable);
        ValidateModularPowerUps(preset.PassivePowerUpsMutable);
        ValidateTierLevels(preset);
        ValidateDropPools(preset);
        ValidateElementalVfxAssignments(preset);
        PlayerPowerUpsPresetMigrationUtility.MigrateLoadoutIds(preset);
        PlayerPowerUpsPresetLoadoutUtility.ValidateActivePowerUpLoadout(preset);
        PlayerPowerUpsPresetLoadoutUtility.ValidatePassivePowerUpLoadout(preset);
        PlayerPowerUpsPresetLoadoutUtility.ValidateToolLoadout(preset);
    }

    private static void ValidatePassiveTools(List<PassiveToolDefinition> passiveTools)
    {
        if (passiveTools == null)
            return;

        for (int index = 0; index < passiveTools.Count; index++)
        {
            PassiveToolDefinition passiveTool = passiveTools[index];

            if (passiveTool != null)
                passiveTool.Validate();
        }
    }

    private static void ValidateActiveTools(List<ActiveToolDefinition> activeTools)
    {
        if (activeTools == null)
            return;

        for (int index = 0; index < activeTools.Count; index++)
        {
            ActiveToolDefinition activeTool = activeTools[index];

            if (activeTool != null)
                activeTool.Validate();
        }
    }

    private static void ValidateModuleDefinitions(List<PowerUpModuleDefinition> moduleDefinitions)
    {
        if (moduleDefinitions == null)
            return;

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            PowerUpModuleDefinition moduleDefinition = moduleDefinitions[index];

            if (moduleDefinition != null)
                moduleDefinition.Validate();
        }

        HashSet<string> visitedModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            PowerUpModuleDefinition moduleDefinition = moduleDefinitions[index];

            if (moduleDefinition == null)
                continue;

            string moduleId = moduleDefinition.ModuleId;

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            if (visitedModuleIds.Add(moduleId))
                continue;

            moduleDefinitions[index] = null;
        }

        for (int index = moduleDefinitions.Count - 1; index >= 0; index--)
        {
            if (moduleDefinitions[index] != null)
                continue;

            moduleDefinitions.RemoveAt(index);
        }
    }

    private static void ValidateModularPowerUps(List<ModularPowerUpDefinition> powerUps)
    {
        if (powerUps == null)
            return;

        for (int index = 0; index < powerUps.Count; index++)
        {
            ModularPowerUpDefinition powerUp = powerUps[index];

            if (powerUp != null)
                powerUp.Validate();
        }
    }

    private static void ValidateScalingRules(List<PlayerStatScalingRule> scalingRules)
    {
        if (scalingRules == null)
            return;

        for (int index = 0; index < scalingRules.Count; index++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];

            if (scalingRule != null)
                continue;

            scalingRule = new PlayerStatScalingRule();
            scalingRule.Configure(string.Empty, false, string.Empty);
            scalingRules[index] = scalingRule;
        }

        for (int index = scalingRules.Count - 1; index >= 0; index--)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];
            scalingRule.Validate();

            if (!string.IsNullOrWhiteSpace(scalingRule.StatKey))
                continue;

            scalingRules.RemoveAt(index);
        }
    }

    private static void ValidateTierLevels(PlayerPowerUpsPreset preset)
    {
        EnsureDefaultTierLevelsIfMissing(preset);
        HashSet<string> validActivePowerUpIds = CollectModularPowerUpIds(preset.ActivePowerUpsMutable);
        HashSet<string> validPassivePowerUpIds = CollectModularPowerUpIds(preset.PassivePowerUpsMutable);
        HashSet<string> visitedTierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int tierIndex = 0; tierIndex < preset.TierLevelsMutable.Count; tierIndex++)
        {
            PowerUpTierLevelDefinition tierLevel = preset.TierLevelsMutable[tierIndex];

            if (tierLevel == null)
            {
                tierLevel = new PowerUpTierLevelDefinition();
                preset.TierLevelsMutable[tierIndex] = tierLevel;
            }

            string fallbackTierId = string.Format("Tier{0}", tierIndex + 1);
            tierLevel.Validate(fallbackTierId);
            tierLevel.AssignTierId(BuildUniqueTierId(visitedTierIds, tierLevel.TierId, tierIndex + 1));
            tierLevel.RemoveInvalidEntries(validActivePowerUpIds, validPassivePowerUpIds);
        }
    }

    private static void ValidateDropPools(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        PlayerPowerUpsPresetMigrationUtility.MigrateLegacyDropPoolCatalog(preset);
        EnsureDefaultDropPoolsIfMissing(preset);
        HashSet<string> validTierIds = CollectTierIds(preset.TierLevelsMutable);
        HashSet<string> visitedPoolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int dropPoolIndex = 0; dropPoolIndex < preset.DropPoolsMutable.Count; dropPoolIndex++)
        {
            PowerUpDropPoolDefinition dropPool = preset.DropPoolsMutable[dropPoolIndex];

            if (dropPool == null)
            {
                dropPool = new PowerUpDropPoolDefinition();
                preset.DropPoolsMutable[dropPoolIndex] = dropPool;
            }

            string fallbackPoolId = string.Format("Pool{0}", dropPoolIndex + 1);
            dropPool.Validate(fallbackPoolId);
            dropPool.AssignPoolId(BuildUniqueDropPoolId(visitedPoolIds, dropPool.PoolId, dropPoolIndex + 1));
            dropPool.RemoveInvalidTierRolls(validTierIds);
        }
    }

    private static void EnsureDefaultTierLevelsIfMissing(PlayerPowerUpsPreset preset)
    {
        if (preset == null || preset.TierLevelsMutable.Count > 0)
            return;

        preset.TierLevelsMutable.Add(new PowerUpTierLevelDefinition());
    }

    private static void EnsureDefaultDropPoolsIfMissing(PlayerPowerUpsPreset preset)
    {
        if (preset == null || preset.DropPoolsMutable.Count > 0)
            return;

        List<PowerUpDropPoolDefinition> defaultDropPools = PlayerPowerUpsPresetDefaultsUtility.BuildDefaultDropPoolDefinitions(preset);

        for (int dropPoolIndex = 0; dropPoolIndex < defaultDropPools.Count; dropPoolIndex++)
            preset.DropPoolsMutable.Add(defaultDropPools[dropPoolIndex]);
    }

    private static HashSet<string> CollectModularPowerUpIds(List<ModularPowerUpDefinition> modularPowerUps)
    {
        HashSet<string> powerUpIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (modularPowerUps == null)
            return powerUpIds;

        for (int powerUpIndex = 0; powerUpIndex < modularPowerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition modularPowerUp = modularPowerUps[powerUpIndex];

            if (modularPowerUp == null)
                continue;

            PowerUpCommonData commonData = modularPowerUp.CommonData;

            if (commonData == null || string.IsNullOrWhiteSpace(commonData.PowerUpId))
                continue;

            powerUpIds.Add(commonData.PowerUpId.Trim());
        }

        return powerUpIds;
    }

    private static HashSet<string> CollectTierIds(List<PowerUpTierLevelDefinition> tierLevels)
    {
        HashSet<string> tierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (tierLevels == null)
            return tierIds;

        for (int tierIndex = 0; tierIndex < tierLevels.Count; tierIndex++)
        {
            PowerUpTierLevelDefinition tierLevel = tierLevels[tierIndex];

            if (tierLevel == null || string.IsNullOrWhiteSpace(tierLevel.TierId))
                continue;

            tierIds.Add(tierLevel.TierId.Trim());
        }

        return tierIds;
    }

    /// <summary>
    /// Produces a unique tier ID while preserving the user-authored base name whenever possible.
    /// </summary>
    /// <param name="visitedTierIds">Already reserved tier IDs for the current preset validation pass.</param>
    /// <param name="tierId">Requested tier ID coming from serialized data.</param>
    /// <param name="fallbackTierIndex">1-based fallback index used when the serialized value is empty.</param>
    /// <returns>Unique tier ID safe to persist on the validated tier.</returns>
    private static string BuildUniqueTierId(HashSet<string> visitedTierIds, string tierId, int fallbackTierIndex)
    {
        string normalizedTierId = string.IsNullOrWhiteSpace(tierId)
            ? string.Format("Tier{0}", fallbackTierIndex)
            : tierId.Trim();

        if (visitedTierIds.Add(normalizedTierId))
            return normalizedTierId;

        int suffix = 1;

        while (true)
        {
            string candidateTierId = string.Format("{0}_{1}", normalizedTierId, suffix);

            if (visitedTierIds.Add(candidateTierId))
                return candidateTierId;

            suffix++;
        }
    }

    private static string BuildUniqueDropPoolId(HashSet<string> visitedPoolIds, string poolId, int fallbackPoolIndex)
    {
        string normalizedPoolId = string.IsNullOrWhiteSpace(poolId)
            ? string.Format("Pool{0}", fallbackPoolIndex)
            : poolId.Trim();

        if (visitedPoolIds.Add(normalizedPoolId))
            return normalizedPoolId;

        int suffix = 1;

        while (true)
        {
            string candidatePoolId = string.Format("{0}_{1}", normalizedPoolId, suffix);

            if (visitedPoolIds.Add(candidatePoolId))
                return candidatePoolId;

            suffix++;
        }
    }

    private static void ValidateElementalVfxAssignments(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (preset.ElementalVfxByElementMutable == null)
            preset.ElementalVfxByElementMutable = new List<ElementalVfxByElementData>();
        PlayerElementalVfxAssignmentUtility.ValidateAssignments(preset.ElementalVfxByElementMutable);
    }
}
