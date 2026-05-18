using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds power-up unlock catalogs, tier buffers and cheat preset snapshots during player baking.
/// </summary>
public static class PlayerPowerUpCatalogBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Populates the equipped passive runtime buffer.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="preset">Source power-ups preset.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <param name="equippedPassiveToolsBuffer">Destination ECS buffer.</param>
    public static void PopulateEquippedPassiveToolsBuffer(PlayerAuthoring authoring,
                                                          PlayerPowerUpsPreset preset,
                                                          Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                          DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer)
    {
        if (preset == null)
            return;

        List<PlayerPassiveToolConfig> equippedPassiveToolConfigs = new List<PlayerPassiveToolConfig>(8);
        List<FixedString64Bytes> equippedPassiveToolIds = new List<FixedString64Bytes>(8);
        PlayerPowerUpPassiveBakeUtility.CollectEquippedPassiveToolConfigs(authoring,
                                                                          preset,
                                                                          resolveDynamicPrefabEntity,
                                                                          equippedPassiveToolConfigs,
                                                                          equippedPassiveToolIds);

        for (int passiveToolIndex = 0; passiveToolIndex < equippedPassiveToolConfigs.Count; passiveToolIndex++)
        {
            PlayerPassiveToolConfig passiveToolConfig = equippedPassiveToolConfigs[passiveToolIndex];

            equippedPassiveToolsBuffer.Add(new EquippedPassiveToolElement
            {
                PowerUpId = passiveToolIndex < equippedPassiveToolIds.Count ? equippedPassiveToolIds[passiveToolIndex] : default,
                Tool = passiveToolConfig
            });
        }
    }

    /// <summary>
    /// Populates unlock catalog and tier buffers used by milestone power-up rolls.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="preset">Scaled source power-ups preset.</param>
    /// <param name="sourcePreset">Unscaled source power-ups preset used to extract runtime scaling metadata.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <param name="powerUpUnlockCatalogBuffer">Destination unlock catalog buffer.</param>
    /// <param name="powerUpTierDefinitionsBuffer">Destination tier definition buffer.</param>
    /// <param name="powerUpTierEntriesBuffer">Destination flattened tier entry buffer.</param>
    /// <param name="powerUpTierEntryScalingBuffer">Destination optional tier-entry scaling metadata buffer.</param>
    public static void PopulatePowerUpUnlockTierBuffers(PlayerAuthoring authoring,
                                                        PlayerPowerUpsPreset preset,
                                                        PlayerPowerUpsPreset sourcePreset,
                                                        Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> powerUpUnlockCatalogBuffer,
                                                        DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> powerUpCharacterTuningFormulaBuffer,
                                                        DynamicBuffer<PlayerPowerUpTierDefinitionElement> powerUpTierDefinitionsBuffer,
                                                        DynamicBuffer<PlayerPowerUpTierEntryElement> powerUpTierEntriesBuffer,
                                                        DynamicBuffer<PlayerPowerUpTierEntryScalingElement> powerUpTierEntryScalingBuffer)
    {
        powerUpUnlockCatalogBuffer.Clear();
        powerUpTierDefinitionsBuffer.Clear();
        powerUpTierEntriesBuffer.Clear();
        powerUpTierEntryScalingBuffer.Clear();

        if (preset == null)
            return;

        Dictionary<string, int> unlockCatalogIndexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<ModularPowerUpDefinition> activePowerUps = preset.ActivePowerUps;
        IReadOnlyList<ModularPowerUpDefinition> passivePowerUps = preset.PassivePowerUps;

        AddUnlockCatalogEntries(authoring,
                                preset,
                                activePowerUps,
                                PlayerPowerUpUnlockKind.Active,
                                resolveDynamicPrefabEntity,
                                powerUpCharacterTuningFormulaBuffer,
                                powerUpUnlockCatalogBuffer,
                                unlockCatalogIndexByKey);
        AddUnlockCatalogEntries(authoring,
                                preset,
                                passivePowerUps,
                                PlayerPowerUpUnlockKind.Passive,
                                resolveDynamicPrefabEntity,
                                powerUpCharacterTuningFormulaBuffer,
                                powerUpUnlockCatalogBuffer,
                                unlockCatalogIndexByKey);
        MarkInitialLoadoutUnlocks(preset, activePowerUps, powerUpUnlockCatalogBuffer, unlockCatalogIndexByKey);
        BuildTierBuffers(preset,
                         sourcePreset,
                         powerUpUnlockCatalogBuffer,
                         powerUpTierDefinitionsBuffer,
                         powerUpTierEntriesBuffer,
                         powerUpTierEntryScalingBuffer,
                         unlockCatalogIndexByKey);
        EnsureFallbackTierIfMissing(powerUpUnlockCatalogBuffer,
                                    powerUpTierDefinitionsBuffer,
                                    powerUpTierEntriesBuffer,
                                    powerUpTierEntryScalingBuffer);
    }

    /// <summary>
    /// Bakes cheat preset snapshots used by runtime debug shortcuts.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <param name="cheatPresetEntriesBuffer">Destination cheat preset entry buffer.</param>
    /// <param name="cheatPresetPassivesBuffer">Destination flattened passive config buffer.</param>
    public static void PopulatePowerUpCheatPresetBuffers(PlayerAuthoring authoring,
                                                         Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                         DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntriesBuffer,
                                                         DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassivesBuffer)
    {
        if (authoring == null)
            return;

        PlayerPowerUpsPresetLibrary cheatPresetLibrary = authoring.PowerUpsCheatPresetLibrary;

        if (cheatPresetLibrary == null)
            return;

        IReadOnlyList<PlayerPowerUpsPreset> cheatPresets = cheatPresetLibrary.Presets;

        if (cheatPresets == null || cheatPresets.Count <= 0)
            return;

        List<PlayerPassiveToolConfig> collectedPassiveToolConfigs = new List<PlayerPassiveToolConfig>(8);
        List<FixedString64Bytes> collectedPassivePowerUpIds = new List<FixedString64Bytes>(8);

        for (int presetIndex = 0; presetIndex < cheatPresets.Count; presetIndex++)
        {
            PlayerPowerUpsPreset cheatPreset = cheatPresets[presetIndex];
            int passiveStartIndex = cheatPresetPassivesBuffer.Length;
            int passiveCount = 0;
            byte isDefined = 0;
            PlayerPowerUpsConfig powerUpsConfigSnapshot = default;

            if (cheatPreset != null)
            {
                isDefined = 1;
                powerUpsConfigSnapshot = PlayerPowerUpActiveBakeUtility.BuildPowerUpsConfig(authoring,
                                                                                            cheatPreset,
                                                                                            resolveDynamicPrefabEntity);
                PlayerPowerUpPassiveBakeUtility.CollectEquippedPassiveToolConfigs(authoring,
                                                                                  cheatPreset,
                                                                                  resolveDynamicPrefabEntity,
                                                                                  collectedPassiveToolConfigs,
                                                                                  collectedPassivePowerUpIds);

                for (int passiveToolIndex = 0; passiveToolIndex < collectedPassiveToolConfigs.Count; passiveToolIndex++)
                {
                    PlayerPassiveToolConfig passiveToolConfig = collectedPassiveToolConfigs[passiveToolIndex];

                    cheatPresetPassivesBuffer.Add(new PlayerPowerUpCheatPresetPassiveElement
                    {
                        PowerUpId = passiveToolIndex < collectedPassivePowerUpIds.Count ? collectedPassivePowerUpIds[passiveToolIndex] : default,
                        Tool = passiveToolConfig
                    });
                    passiveCount++;
                }
            }

            cheatPresetEntriesBuffer.Add(new PlayerPowerUpCheatPresetEntry
            {
                IsDefined = isDefined,
                PassiveStartIndex = passiveStartIndex,
                PassiveCount = passiveCount,
                PowerUpsConfig = powerUpsConfigSnapshot
            });
        }
    }
    #endregion

    #region Private Methods
    private static void AddUnlockCatalogEntries(PlayerAuthoring authoring,
                                                PlayerPowerUpsPreset preset,
                                                IReadOnlyList<ModularPowerUpDefinition> powerUps,
                                                PlayerPowerUpUnlockKind unlockKind,
                                                Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> powerUpCharacterTuningFormulaBuffer,
                                                DynamicBuffer<PlayerPowerUpUnlockCatalogElement> powerUpUnlockCatalogBuffer,
                                                Dictionary<string, int> unlockCatalogIndexByKey)
    {
        if (powerUps == null)
            return;

        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp == null || powerUp.CommonData == null || string.IsNullOrWhiteSpace(powerUp.CommonData.PowerUpId))
                continue;

            string powerUpId = powerUp.CommonData.PowerUpId.Trim();
            string catalogKey = BuildUnlockCatalogKey(unlockKind, powerUpId);

            if (unlockCatalogIndexByKey.ContainsKey(catalogKey))
                continue;

            PlayerPowerUpUnlockCatalogElement unlockCatalogEntry = new PlayerPowerUpUnlockCatalogElement
            {
                PowerUpId = new FixedString64Bytes(powerUpId),
                DisplayName = new FixedString64Bytes(string.IsNullOrWhiteSpace(powerUp.CommonData.DisplayName) ? powerUpId : powerUp.CommonData.DisplayName.Trim()),
                Description = new FixedString128Bytes(string.IsNullOrWhiteSpace(powerUp.CommonData.Description) ? string.Empty : powerUp.CommonData.Description.Trim()),
                UnlockKind = unlockKind,
                IsUnlocked = 0,
                PendingInitialCharacterTuningApply = 0,
                CurrentUnlockCount = 0,
                MaximumUnlockCount = ResolveMaximumUnlockCount(preset, powerUp),
                CharacterTuningFormulaStartIndex = powerUpCharacterTuningFormulaBuffer.Length,
                CharacterTuningFormulaCount = AppendCharacterTuningFormulas(preset, powerUp, powerUpCharacterTuningFormulaBuffer),
                ActiveSlotConfig = default,
                PassiveToolConfig = default
            };

            if (unlockKind == PlayerPowerUpUnlockKind.Active)
                unlockCatalogEntry.ActiveSlotConfig = PlayerPowerUpActiveBakeUtility.BuildSlotConfigFromModularPowerUp(authoring,
                                                                                                                        preset,
                                                                                                                        powerUp,
                                                                                                                        resolveDynamicPrefabEntity);
            else
                unlockCatalogEntry.PassiveToolConfig = PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(authoring,
                                                                                                                                preset,
                                                                                                                                powerUp,
                                                                                                                                resolveDynamicPrefabEntity);

            int catalogIndex = powerUpUnlockCatalogBuffer.Length;
            powerUpUnlockCatalogBuffer.Add(unlockCatalogEntry);
            unlockCatalogIndexByKey.Add(catalogKey, catalogIndex);
        }
    }

    private static void MarkInitialLoadoutUnlocks(PlayerPowerUpsPreset preset,
                                                  IReadOnlyList<ModularPowerUpDefinition> activePowerUps,
                                                  DynamicBuffer<PlayerPowerUpUnlockCatalogElement> powerUpUnlockCatalogBuffer,
                                                  Dictionary<string, int> unlockCatalogIndexByKey)
    {
        if (preset == null)
            return;

        ResolveActiveLoadoutPowerUpIds(preset, activePowerUps, out string primaryActivePowerUpId, out string secondaryActivePowerUpId);
        TryMarkUnlocked(PlayerPowerUpUnlockKind.Active, primaryActivePowerUpId, powerUpUnlockCatalogBuffer, unlockCatalogIndexByKey);
        TryMarkUnlocked(PlayerPowerUpUnlockKind.Active, secondaryActivePowerUpId, powerUpUnlockCatalogBuffer, unlockCatalogIndexByKey);
        IReadOnlyList<string> equippedPassivePowerUpIds = preset.EquippedPassivePowerUpIds;

        if (equippedPassivePowerUpIds == null || equippedPassivePowerUpIds.Count <= 0)
            equippedPassivePowerUpIds = preset.EquippedPassiveToolIds;

        if (equippedPassivePowerUpIds == null)
            return;

        for (int passiveIndex = 0; passiveIndex < equippedPassivePowerUpIds.Count; passiveIndex++)
            TryMarkUnlocked(PlayerPowerUpUnlockKind.Passive,
                            equippedPassivePowerUpIds[passiveIndex],
                            powerUpUnlockCatalogBuffer,
                            unlockCatalogIndexByKey);
    }

    private static void ResolveActiveLoadoutPowerUpIds(PlayerPowerUpsPreset preset,
                                                       IReadOnlyList<ModularPowerUpDefinition> activePowerUps,
                                                       out string primaryActivePowerUpId,
                                                       out string secondaryActivePowerUpId)
    {
        primaryActivePowerUpId = string.Empty;
        secondaryActivePowerUpId = string.Empty;

        if (preset == null || activePowerUps == null || activePowerUps.Count <= 0)
            return;

        ModularPowerUpDefinition primaryPowerUp = PlayerPowerUpBakeSharedUtility.ResolveLoadoutActivePowerUp(preset,
                                                                                                              preset.PrimaryActivePowerUpId,
                                                                                                              0,
                                                                                                              false);
        ModularPowerUpDefinition secondaryPowerUp = PlayerPowerUpBakeSharedUtility.ResolveLoadoutActivePowerUp(preset,
                                                                                                                preset.SecondaryActivePowerUpId,
                                                                                                                1,
                                                                                                                false);

        primaryActivePowerUpId = ResolvePowerUpId(primaryPowerUp);
        secondaryActivePowerUpId = ResolvePowerUpId(secondaryPowerUp);
    }

    private static string ResolvePowerUpId(ModularPowerUpDefinition powerUp)
    {
        if (powerUp == null || powerUp.CommonData == null || string.IsNullOrWhiteSpace(powerUp.CommonData.PowerUpId))
            return string.Empty;

        return powerUp.CommonData.PowerUpId.Trim();
    }

    private static void BuildTierBuffers(PlayerPowerUpsPreset preset,
                                         PlayerPowerUpsPreset sourcePreset,
                                         DynamicBuffer<PlayerPowerUpUnlockCatalogElement> powerUpUnlockCatalogBuffer,
                                         DynamicBuffer<PlayerPowerUpTierDefinitionElement> powerUpTierDefinitionsBuffer,
                                         DynamicBuffer<PlayerPowerUpTierEntryElement> powerUpTierEntriesBuffer,
                                         DynamicBuffer<PlayerPowerUpTierEntryScalingElement> powerUpTierEntryScalingBuffer,
                                         Dictionary<string, int> unlockCatalogIndexByKey)
    {
        IReadOnlyList<PowerUpTierLevelDefinition> tierLevels = preset.TierLevels;

        if (tierLevels == null)
            return;

        for (int tierIndex = 0; tierIndex < tierLevels.Count; tierIndex++)
        {
            PowerUpTierLevelDefinition tierLevel = tierLevels[tierIndex];

            if (tierLevel == null || string.IsNullOrWhiteSpace(tierLevel.TierId))
                continue;

            int tierEntriesStartIndex = powerUpTierEntriesBuffer.Length;
            IReadOnlyList<PowerUpTierEntryDefinition> tierEntries = tierLevel.Entries;

            if (tierEntries != null)
            {
                for (int entryIndex = 0; entryIndex < tierEntries.Count; entryIndex++)
                {
                    PowerUpTierEntryDefinition tierEntry = tierEntries[entryIndex];

                    if (tierEntry == null || string.IsNullOrWhiteSpace(tierEntry.PowerUpId))
                        continue;

                    PlayerPowerUpUnlockKind unlockKind = tierEntry.EntryKind == PowerUpTierEntryKind.Active
                        ? PlayerPowerUpUnlockKind.Active
                        : PlayerPowerUpUnlockKind.Passive;
                    string catalogKey = BuildUnlockCatalogKey(unlockKind, tierEntry.PowerUpId);

                    if (!unlockCatalogIndexByKey.TryGetValue(catalogKey, out int catalogIndex))
                        continue;

                    if (catalogIndex < 0 || catalogIndex >= powerUpUnlockCatalogBuffer.Length)
                        continue;

                    PlayerPowerUpUnlockCatalogElement catalogEntry = powerUpUnlockCatalogBuffer[catalogIndex];

                    if (!HasRemainingUnlocks(catalogEntry))
                        continue;

                    powerUpTierEntriesBuffer.Add(new PlayerPowerUpTierEntryElement
                    {
                        CatalogIndex = catalogIndex,
                        SelectionWeight = math.max(0f, tierEntry.SelectionWeight)
                    });
                    TryAddTierEntryScalingMetadata(sourcePreset,
                                                   tierIndex,
                                                   entryIndex,
                                                   powerUpTierEntriesBuffer.Length - 1,
                                                   powerUpTierEntryScalingBuffer);
                }
            }

            powerUpTierDefinitionsBuffer.Add(new PlayerPowerUpTierDefinitionElement
            {
                TierId = new FixedString64Bytes(tierLevel.TierId.Trim()),
                EntryStartIndex = tierEntriesStartIndex,
                EntryCount = powerUpTierEntriesBuffer.Length - tierEntriesStartIndex
            });
        }
    }

    private static void EnsureFallbackTierIfMissing(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> powerUpUnlockCatalogBuffer,
                                                    DynamicBuffer<PlayerPowerUpTierDefinitionElement> powerUpTierDefinitionsBuffer,
                                                    DynamicBuffer<PlayerPowerUpTierEntryElement> powerUpTierEntriesBuffer,
                                                    DynamicBuffer<PlayerPowerUpTierEntryScalingElement> powerUpTierEntryScalingBuffer)
    {
        if (powerUpTierDefinitionsBuffer.Length > 0)
            return;

        int startIndex = powerUpTierEntriesBuffer.Length;

        for (int catalogIndex = 0; catalogIndex < powerUpUnlockCatalogBuffer.Length; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = powerUpUnlockCatalogBuffer[catalogIndex];

            if (!HasRemainingUnlocks(catalogEntry))
                continue;

            powerUpTierEntriesBuffer.Add(new PlayerPowerUpTierEntryElement
            {
                CatalogIndex = catalogIndex,
                SelectionWeight = 1f
            });
        }

        powerUpTierDefinitionsBuffer.Add(new PlayerPowerUpTierDefinitionElement
        {
            TierId = new FixedString64Bytes("Default"),
            EntryStartIndex = startIndex,
            EntryCount = powerUpTierEntriesBuffer.Length - startIndex
        });
    }

    private static void TryAddTierEntryScalingMetadata(PlayerPowerUpsPreset sourcePreset,
                                                       int tierIndex,
                                                       int entryIndex,
                                                       int tierEntryIndex,
                                                       DynamicBuffer<PlayerPowerUpTierEntryScalingElement> powerUpTierEntryScalingBuffer)
    {
        if (!PlayerRuntimeScalingBakeMetadataUtility.TryResolveTierEntryScalingData(sourcePreset,
                                                                                    tierIndex,
                                                                                    entryIndex,
                                                                                    out float baseSelectionWeight,
                                                                                    out string scalingFormula))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(scalingFormula))
        {
            return;
        }

        powerUpTierEntryScalingBuffer.Add(new PlayerPowerUpTierEntryScalingElement
        {
            TierEntryIndex = tierEntryIndex,
            BaseSelectionWeight = math.max(0f, baseSelectionWeight),
            ScalingFormula = new FixedString512Bytes(scalingFormula)
        });
    }

    private static void TryMarkUnlocked(PlayerPowerUpUnlockKind unlockKind,
                                        string powerUpId,
                                        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> powerUpUnlockCatalogBuffer,
                                        Dictionary<string, int> unlockCatalogIndexByKey)
    {
        if (string.IsNullOrWhiteSpace(powerUpId))
            return;

        string catalogKey = BuildUnlockCatalogKey(unlockKind, powerUpId.Trim());

        if (!unlockCatalogIndexByKey.TryGetValue(catalogKey, out int catalogIndex))
            return;

        if (catalogIndex < 0 || catalogIndex >= powerUpUnlockCatalogBuffer.Length)
            return;

        PlayerPowerUpUnlockCatalogElement catalogEntry = powerUpUnlockCatalogBuffer[catalogIndex];
        int maximumUnlockCount = math.max(1, catalogEntry.MaximumUnlockCount);

        if (catalogEntry.CurrentUnlockCount >= maximumUnlockCount)
            return;

        catalogEntry.CurrentUnlockCount = math.min(maximumUnlockCount, catalogEntry.CurrentUnlockCount + 1);
        catalogEntry.IsUnlocked = 1;
        catalogEntry.PendingInitialCharacterTuningApply = PlayerPowerUpCharacterTuningRuntimeUtility.ShouldApplyOnAcquisition(in catalogEntry) ? (byte)1 : (byte)0;
        powerUpUnlockCatalogBuffer[catalogIndex] = catalogEntry;
    }

    private static bool HasRemainingUnlocks(PlayerPowerUpUnlockCatalogElement catalogEntry)
    {
        return catalogEntry.CurrentUnlockCount < math.max(1, catalogEntry.MaximumUnlockCount);
    }

    private static int ResolveMaximumUnlockCount(PlayerPowerUpsPreset preset, ModularPowerUpDefinition powerUp)
    {
        IReadOnlyList<PowerUpModuleBinding> moduleBindings = powerUp != null ? powerUp.ModuleBindings : null;

        if (moduleBindings == null)
            return 1;

        int maximumUnlockCount = 1;

        for (int bindingIndex = 0; bindingIndex < moduleBindings.Count; bindingIndex++)
        {
            PowerUpModuleBinding binding = moduleBindings[bindingIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            PowerUpModuleDefinition moduleDefinition = PlayerPowerUpBakeSharedUtility.ResolveModuleDefinitionById(preset, binding.ModuleId);

            if (moduleDefinition == null || moduleDefinition.ModuleKind != PowerUpModuleKind.Stackable)
                continue;

            PowerUpModuleData payload = binding.ResolvePayload(moduleDefinition);
            PowerUpStackableModuleData stackableData = payload != null ? payload.Stackable : null;

            if (stackableData == null)
                continue;

            maximumUnlockCount = math.max(maximumUnlockCount, stackableData.MaxAcquisitions);
        }

        return maximumUnlockCount;
    }

    private static int AppendCharacterTuningFormulas(PlayerPowerUpsPreset preset,
                                                     ModularPowerUpDefinition powerUp,
                                                     DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> powerUpCharacterTuningFormulaBuffer)
    {
        IReadOnlyList<PowerUpModuleBinding> moduleBindings = powerUp != null ? powerUp.ModuleBindings : null;

        if (moduleBindings == null)
            return 0;

        int appendedFormulaCount = 0;

        for (int bindingIndex = 0; bindingIndex < moduleBindings.Count; bindingIndex++)
        {
            PowerUpModuleBinding binding = moduleBindings[bindingIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            PowerUpModuleDefinition moduleDefinition = PlayerPowerUpBakeSharedUtility.ResolveModuleDefinitionById(preset, binding.ModuleId);

            if (moduleDefinition == null || moduleDefinition.ModuleKind != PowerUpModuleKind.CharacterTuning)
                continue;

            PowerUpModuleData payload = binding.ResolvePayload(moduleDefinition);
            PowerUpCharacterTuningModuleData characterTuningData = payload != null ? payload.CharacterTuning : null;
            IReadOnlyList<PowerUpCharacterTuningFormulaData> formulas = characterTuningData != null ? characterTuningData.Formulas : null;

            if (formulas == null)
                continue;

            for (int formulaIndex = 0; formulaIndex < formulas.Count; formulaIndex++)
            {
                PowerUpCharacterTuningFormulaData formulaData = formulas[formulaIndex];
                string formula = formulaData != null ? formulaData.Formula : string.Empty;

                if (string.IsNullOrWhiteSpace(formula))
                    continue;

                powerUpCharacterTuningFormulaBuffer.Add(new PlayerPowerUpCharacterTuningFormulaElement
                {
                    Formula = new FixedString128Bytes(formula.Trim())
                });
                appendedFormulaCount++;
            }
        }

        return appendedFormulaCount;
    }

    private static string BuildUnlockCatalogKey(PlayerPowerUpUnlockKind unlockKind, string powerUpId)
    {
        return string.Format("{0}|{1}",
                             unlockKind == PlayerPowerUpUnlockKind.Active ? "A" : "P",
                             string.IsNullOrWhiteSpace(powerUpId) ? string.Empty : powerUpId.Trim());
    }
    #endregion

    #endregion
}
