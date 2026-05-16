using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Provides shared runtime helpers for milestone tier roll extraction and offer generation.
/// </summary>
public static class PlayerMilestonePowerUpRollUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether all milestone-selection runtime components are available on the player.
    /// </summary>
    /// <param name="entity">Player entity being validated.</param>
    /// <param name="milestoneSelectionStateLookup">Selection-state component lookup.</param>
    /// <param name="milestoneSelectionOffersLookup">Selection-offers buffer lookup.</param>
    /// <param name="unlockCatalogLookup">Unlock catalog buffer lookup.</param>
    /// <param name="tierDefinitionsLookup">Tier definitions buffer lookup.</param>
    /// <param name="tierEntriesLookup">Tier entries buffer lookup.</param>
    /// <returns>True when all required components/buffers are available; otherwise false.</returns>
    public static bool HasMilestoneSelectionData(Entity entity,
                                                 in ComponentLookup<PlayerMilestonePowerUpSelectionState> milestoneSelectionStateLookup,
                                                 in BufferLookup<PlayerMilestonePowerUpSelectionOfferElement> milestoneSelectionOffersLookup,
                                                 in BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup,
                                                 in BufferLookup<PlayerPowerUpTierDefinitionElement> tierDefinitionsLookup,
                                                 in BufferLookup<PlayerPowerUpTierEntryElement> tierEntriesLookup)
    {
        if (!milestoneSelectionStateLookup.HasComponent(entity))
            return false;

        if (!milestoneSelectionOffersLookup.HasBuffer(entity))
            return false;

        if (!unlockCatalogLookup.HasBuffer(entity))
            return false;

        if (!tierDefinitionsLookup.HasBuffer(entity))
            return false;

        return tierEntriesLookup.HasBuffer(entity);
    }

    /// <summary>
    /// Rolls milestone offers and activates runtime selection state.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="activeGamePhaseIndex">Resolved active game-phase index for current level.</param>
    /// <param name="milestoneLevel">Milestone level being processed.</param>
    /// <param name="scalableStats">Current runtime scalable-stat buffer used by runtime scaling formulas.</param>
    /// <param name="unlockCatalog">Unlock catalog used to exclude already unlocked entries.</param>
    /// <param name="tierDefinitions">Tier definitions buffer.</param>
    /// <param name="tierEntries">Flattened tier-entry buffer.</param>
    /// <param name="tierEntryScaling">Optional runtime scaling metadata for tier-entry weights.</param>
    /// <param name="equippedPassiveTools">Current equipped passive-tools buffer used to exclude incompatible passive offers.</param>
    /// <param name="selectionOffers">Selection-offers destination buffer.</param>
    /// <param name="selectionState">Selection-state component updated in place.</param>
    /// <param name="rolledOfferCount">Number of offers rolled for this milestone selection.</param>
    /// <returns>True when at least one offer is rolled and selection is activated; otherwise false.</returns>
    public static bool TryOpenMilestoneSelection(PlayerProgressionConfig progressionConfig,
                                                 int activeGamePhaseIndex,
                                                 int milestoneLevel,
                                                 DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                 DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                 DynamicBuffer<PlayerPowerUpTierDefinitionElement> tierDefinitions,
                                                 DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                                 DynamicBuffer<PlayerPowerUpTierEntryScalingElement> tierEntryScaling,
                                                 DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                 DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers,
                                                 ref PlayerMilestonePowerUpSelectionState selectionState,
                                                 out int rolledOfferCount)
    {
        rolledOfferCount = 0;

        if (!progressionConfig.Config.IsCreated)
            return false;

        if (selectionState.IsSelectionActive != 0)
            return false;

        if (activeGamePhaseIndex < 0 || activeGamePhaseIndex >= progressionConfig.Config.Value.GamePhases.Length)
            return false;

        if (!PlayerProgressionPhaseUtility.TryResolveMilestoneIndex(progressionConfig,
                                                                    activeGamePhaseIndex,
                                                                    milestoneLevel,
                                                                    out int milestoneIndex))
            return false;

        ref PlayerGamePhaseBlob gamePhase = ref progressionConfig.Config.Value.GamePhases[activeGamePhaseIndex];
        ref PlayerLevelUpMilestoneBlob milestoneBlob = ref gamePhase.Milestones[milestoneIndex];

        if (milestoneBlob.PowerUpUnlocks.Length <= 0)
            return false;

        selectionOffers.Clear();
        Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);
        HashSet<int> rolledCatalogIndices = new HashSet<int>();
        HashSet<PassiveToolKind> blockedPassiveKinds = BuildBlockedPassiveKinds(equippedPassiveTools);

        for (int rollIndex = 0; rollIndex < milestoneBlob.PowerUpUnlocks.Length; rollIndex++)
        {
            ref PlayerMilestonePowerUpUnlockBlob powerUpUnlockBlob = ref milestoneBlob.PowerUpUnlocks[rollIndex];

            if (!TryRollMilestoneOffer(ref powerUpUnlockBlob,
                                       variableContext,
                                       unlockCatalog,
                                       tierDefinitions,
                                       tierEntries,
                                       tierEntryScaling,
                                       rolledCatalogIndices,
                                       blockedPassiveKinds,
                                       out int rolledCatalogIndex,
                                       out string selectedDropPoolId,
                                       out string selectedTierId,
                                       out float selectedTierPercentage,
                                       out float selectedEntryWeight))
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                        "[PlayerLevelUpSystem] Milestone {0} roll {1}/{2} failed: no valid drop-pool tier or power-up candidate for Pool '{3}'.",
                                        milestoneLevel,
                                        rollIndex + 1,
                                        milestoneBlob.PowerUpUnlocks.Length,
                                        powerUpUnlockBlob.DropPoolId.ToString()));
                continue;
            }

            rolledCatalogIndices.Add(rolledCatalogIndex);
            PlayerPowerUpUnlockCatalogElement unlockEntry = unlockCatalog[rolledCatalogIndex];

            if (unlockEntry.UnlockKind == PlayerPowerUpUnlockKind.Passive && unlockEntry.PassiveToolConfig.IsDefined != 0)
                blockedPassiveKinds.Add(unlockEntry.PassiveToolConfig.ToolKind);

            selectionOffers.Add(new PlayerMilestonePowerUpSelectionOfferElement
            {
                CatalogIndex = rolledCatalogIndex,
                PowerUpId = unlockEntry.PowerUpId,
                DisplayName = unlockEntry.DisplayName,
                Description = unlockEntry.Description,
                UnlockKind = unlockEntry.UnlockKind
            });
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                    "[PlayerLevelUpSystem] Milestone {0} roll {1}/{2}: Pool '{3}' -> Tier '{4}' ({5:0.###}%) -> Power-Up '{6}' ({7}) [Entry Weight {8:0.###}].",
                                    milestoneLevel,
                                    rollIndex + 1,
                                    milestoneBlob.PowerUpUnlocks.Length,
                                    selectedDropPoolId,
                                    selectedTierId,
                                    selectedTierPercentage,
                                    unlockEntry.PowerUpId.ToString(),
                                    unlockEntry.UnlockKind,
                                    selectedEntryWeight));
        }

        rolledOfferCount = selectionOffers.Length;

        if (rolledOfferCount <= 0)
            return false;

        selectionState.IsSelectionActive = 1;
        selectionState.MilestoneLevel = milestoneLevel;
        selectionState.GamePhaseIndex = activeGamePhaseIndex;
        selectionState.MilestoneIndex = milestoneIndex;
        selectionState.OfferCount = rolledOfferCount;
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Rolls one unlock catalog entry from milestone tier candidates.
    /// </summary>
    /// <param name="powerUpUnlockBlob">Milestone unlock blob containing tier-roll settings.</param>
    /// <param name="variableContext">Current scalable-stat dictionary used by runtime scaling formulas.</param>
    /// <param name="unlockCatalog">Unlock catalog buffer.</param>
    /// <param name="tierDefinitions">Tier definitions buffer.</param>
    /// <param name="tierEntries">Flattened tier-entry buffer.</param>
    /// <param name="tierEntryScaling">Optional runtime scaling metadata for tier-entry weights.</param>
    /// <param name="rolledCatalogIndices">Catalog indices already rolled in this milestone selection.</param>
    /// <param name="blockedPassiveKinds">Passive kinds already equipped or already rolled during this selection.</param>
    /// <param name="rolledCatalogIndex">Resolved rolled catalog index when successful.</param>
    /// <param name="selectedTierId">Tier ID selected for the current roll.</param>
    /// <param name="selectedTierPercentage">Percentage assigned to the selected milestone tier candidate.</param>
    /// <param name="selectedEntryWeight">Weight of the selected power-up entry inside the selected tier.</param>
    /// <returns>True when an entry is successfully rolled; otherwise false.</returns>
    private static bool TryRollMilestoneOffer(ref PlayerMilestonePowerUpUnlockBlob powerUpUnlockBlob,
                                              IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                              DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                              DynamicBuffer<PlayerPowerUpTierDefinitionElement> tierDefinitions,
                                              DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                              DynamicBuffer<PlayerPowerUpTierEntryScalingElement> tierEntryScaling,
                                              HashSet<int> rolledCatalogIndices,
                                              HashSet<PassiveToolKind> blockedPassiveKinds,
                                              out int rolledCatalogIndex,
                                              out string selectedDropPoolId,
                                              out string selectedTierId,
                                              out float selectedTierPercentage,
                                              out float selectedEntryWeight)
    {
        rolledCatalogIndex = -1;
        selectedDropPoolId = powerUpUnlockBlob.DropPoolId.ToString();
        selectedTierId = string.Empty;
        selectedTierPercentage = 0f;
        selectedEntryWeight = 0f;
        List<int> rollCandidateIndices = new List<int>();
        List<float> rollCandidatePercentages = new List<float>();

        // Collect milestone tier rolls that currently have at least one available unlock candidate.
        for (int tierRollIndex = 0; tierRollIndex < powerUpUnlockBlob.TierRolls.Length; tierRollIndex++)
        {
            ref PlayerMilestoneTierRollBlob tierRoll = ref powerUpUnlockBlob.TierRolls[tierRollIndex];
            float tierRollPercentage = ResolveTierRollPercentage(ref tierRoll, variableContext);

            if (tierRollPercentage <= 0f)
                continue;

            string tierId = tierRoll.TierId.ToString();

            if (!TryResolveTierDefinition(tierDefinitions, tierId, out PlayerPowerUpTierDefinitionElement tierDefinition))
                continue;

            if (!HasAnyRollableEntry(tierDefinition,
                                     tierEntries,
                                     tierEntryScaling,
                                     variableContext,
                                     unlockCatalog,
                                     rolledCatalogIndices,
                                     blockedPassiveKinds))
                continue;

            rollCandidateIndices.Add(tierRollIndex);
            rollCandidatePercentages.Add(tierRollPercentage);
        }

        int selectedTierRollCandidate = RollWeightedIndex(rollCandidatePercentages);

        if (selectedTierRollCandidate < 0)
            return false;

        int selectedTierRollIndex = rollCandidateIndices[selectedTierRollCandidate];
        ref PlayerMilestoneTierRollBlob selectedTierRoll = ref powerUpUnlockBlob.TierRolls[selectedTierRollIndex];
        selectedTierId = selectedTierRoll.TierId.ToString();
        selectedTierPercentage = ResolveTierRollPercentage(ref selectedTierRoll, variableContext);

        if (!TryResolveTierDefinition(tierDefinitions, selectedTierId, out PlayerPowerUpTierDefinitionElement selectedTierDefinition))
            return false;

        return TryRollCatalogFromTier(selectedTierDefinition,
                                      tierEntries,
                                      tierEntryScaling,
                                      variableContext,
                                      unlockCatalog,
                                      rolledCatalogIndices,
                                      blockedPassiveKinds,
                                      out rolledCatalogIndex,
                                      out selectedEntryWeight);
    }

    /// <summary>
    /// Resolves one tier definition by ID.
    /// </summary>
    /// <param name="tierDefinitions">Tier definitions buffer.</param>
    /// <param name="tierId">Requested tier ID.</param>
    /// <param name="tierDefinition">Resolved tier definition when found.</param>
    /// <returns>True when tier exists; otherwise false.</returns>
    private static bool TryResolveTierDefinition(DynamicBuffer<PlayerPowerUpTierDefinitionElement> tierDefinitions,
                                                 string tierId,
                                                 out PlayerPowerUpTierDefinitionElement tierDefinition)
    {
        tierDefinition = default;

        if (string.IsNullOrWhiteSpace(tierId))
            return false;

        for (int tierIndex = 0; tierIndex < tierDefinitions.Length; tierIndex++)
        {
            PlayerPowerUpTierDefinitionElement candidate = tierDefinitions[tierIndex];

            if (!string.Equals(candidate.TierId.ToString(), tierId, StringComparison.OrdinalIgnoreCase))
                continue;

            tierDefinition = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a tier still has at least one available unlock candidate.
    /// </summary>
    /// <param name="tierDefinition">Tier metadata entry.</param>
    /// <param name="tierEntries">Flattened tier-entry buffer.</param>
    /// <param name="tierEntryScaling">Optional runtime scaling metadata for tier-entry weights.</param>
    /// <param name="variableContext">Current scalable-stat dictionary used by runtime scaling formulas.</param>
    /// <param name="unlockCatalog">Unlock catalog buffer.</param>
    /// <param name="rolledCatalogIndices">Catalog indices already rolled in current milestone selection.</param>
    /// <param name="blockedPassiveKinds">Passive kinds that cannot be offered for this milestone selection.</param>
    /// <returns>True when at least one rollable candidate is available; otherwise false.</returns>
    private static bool HasAnyRollableEntry(in PlayerPowerUpTierDefinitionElement tierDefinition,
                                            DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                            DynamicBuffer<PlayerPowerUpTierEntryScalingElement> tierEntryScaling,
                                            IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                            HashSet<int> rolledCatalogIndices,
                                            HashSet<PassiveToolKind> blockedPassiveKinds)
    {
        int startIndex = mathMax(0, tierDefinition.EntryStartIndex);
        int endIndex = mathMin(tierEntries.Length, startIndex + mathMax(0, tierDefinition.EntryCount));

        for (int tierEntryIndex = startIndex; tierEntryIndex < endIndex; tierEntryIndex++)
        {
            PlayerPowerUpTierEntryElement tierEntry = tierEntries[tierEntryIndex];

            if (ResolveTierEntryWeight(tierEntries,
                                       tierEntryScaling,
                                       tierEntryIndex,
                                       variableContext) <= 0f)
                continue;

            int catalogIndex = tierEntry.CatalogIndex;

            if (catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
                continue;

            if (rolledCatalogIndices.Contains(catalogIndex))
                continue;

            PlayerPowerUpUnlockCatalogElement unlockEntry = unlockCatalog[catalogIndex];

            if (!HasRemainingUnlocks(in unlockEntry))
                continue;

            if (IsPassiveOfferBlocked(in unlockEntry, blockedPassiveKinds))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Rolls one unlock catalog index from a tier definition.
    /// </summary>
    /// <param name="tierDefinition">Tier metadata entry.</param>
    /// <param name="tierEntries">Flattened tier-entry buffer.</param>
    /// <param name="tierEntryScaling">Optional runtime scaling metadata for tier-entry weights.</param>
    /// <param name="variableContext">Current scalable-stat dictionary used by runtime scaling formulas.</param>
    /// <param name="unlockCatalog">Unlock catalog buffer.</param>
    /// <param name="rolledCatalogIndices">Catalog indices already rolled in current milestone selection.</param>
    /// <param name="blockedPassiveKinds">Passive kinds that cannot be offered for this milestone selection.</param>
    /// <param name="catalogIndex">Resolved catalog index when successful.</param>
    /// <param name="entryWeight">Weight of the selected power-up entry.</param>
    /// <returns>True when a candidate is rolled; otherwise false.</returns>
    private static bool TryRollCatalogFromTier(in PlayerPowerUpTierDefinitionElement tierDefinition,
                                               DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                               DynamicBuffer<PlayerPowerUpTierEntryScalingElement> tierEntryScaling,
                                               IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                               DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                               HashSet<int> rolledCatalogIndices,
                                               HashSet<PassiveToolKind> blockedPassiveKinds,
                                               out int catalogIndex,
                                               out float entryWeight)
    {
        catalogIndex = -1;
        entryWeight = 0f;
        List<int> candidateCatalogIndices = new List<int>();
        List<float> candidateWeights = new List<float>();
        int startIndex = mathMax(0, tierDefinition.EntryStartIndex);
        int endIndex = mathMin(tierEntries.Length, startIndex + mathMax(0, tierDefinition.EntryCount));

        // Collect eligible entries from this tier.
        for (int tierEntryIndex = startIndex; tierEntryIndex < endIndex; tierEntryIndex++)
        {
            PlayerPowerUpTierEntryElement tierEntry = tierEntries[tierEntryIndex];
            float tierEntryWeight = ResolveTierEntryWeight(tierEntries,
                                                           tierEntryScaling,
                                                           tierEntryIndex,
                                                           variableContext);

            if (tierEntryWeight <= 0f)
                continue;

            int candidateCatalogIndex = tierEntry.CatalogIndex;

            if (candidateCatalogIndex < 0 || candidateCatalogIndex >= unlockCatalog.Length)
                continue;

            if (rolledCatalogIndices.Contains(candidateCatalogIndex))
                continue;

            PlayerPowerUpUnlockCatalogElement unlockEntry = unlockCatalog[candidateCatalogIndex];

            if (!HasRemainingUnlocks(in unlockEntry))
                continue;

            if (IsPassiveOfferBlocked(in unlockEntry, blockedPassiveKinds))
                continue;

            candidateCatalogIndices.Add(candidateCatalogIndex);
            candidateWeights.Add(tierEntryWeight);
        }

        int selectedCandidateIndex = RollWeightedIndex(candidateWeights);

        if (selectedCandidateIndex < 0)
            return false;

        catalogIndex = candidateCatalogIndices[selectedCandidateIndex];
        entryWeight = candidateWeights[selectedCandidateIndex];
        return true;
    }

    private static HashSet<PassiveToolKind> BuildBlockedPassiveKinds(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        HashSet<PassiveToolKind> blockedPassiveKinds = new HashSet<PassiveToolKind>();

        if (!equippedPassiveTools.IsCreated)
            return blockedPassiveKinds;

        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            PlayerPassiveToolConfig passiveToolConfig = equippedPassiveTools[passiveIndex].Tool;

            if (passiveToolConfig.IsDefined == 0)
                continue;

            blockedPassiveKinds.Add(passiveToolConfig.ToolKind);
        }

        return blockedPassiveKinds;
    }

    private static bool IsPassiveOfferBlocked(in PlayerPowerUpUnlockCatalogElement unlockEntry, HashSet<PassiveToolKind> blockedPassiveKinds)
    {
        if (unlockEntry.UnlockKind != PlayerPowerUpUnlockKind.Passive)
            return false;

        if (unlockEntry.CurrentUnlockCount > 0)
            return false;

        if (unlockEntry.PassiveToolConfig.IsDefined == 0)
            return false;

        if (blockedPassiveKinds == null)
            return false;

        return blockedPassiveKinds.Contains(unlockEntry.PassiveToolConfig.ToolKind);
    }

    private static bool HasRemainingUnlocks(in PlayerPowerUpUnlockCatalogElement unlockEntry)
    {
        return unlockEntry.CurrentUnlockCount < mathMax(1, unlockEntry.MaximumUnlockCount);
    }

    private static float ResolveTierRollPercentage(ref PlayerMilestoneTierRollBlob tierRoll,
                                                   IReadOnlyDictionary<string, PlayerFormulaValue> variableContext)
    {
        float selectionPercentage = mathMax(0f, tierRoll.SelectionPercentage);
        string scalingFormula = tierRoll.ScalingFormula.ToString();

        if (string.IsNullOrWhiteSpace(scalingFormula))
            return selectionPercentage;

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(scalingFormula,
                                                                   tierRoll.BaseSelectionPercentage,
                                                                   variableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
        {
            return selectionPercentage;
        }

        return mathMax(0f, evaluatedValue);
    }

    private static float ResolveTierEntryWeight(DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                                DynamicBuffer<PlayerPowerUpTierEntryScalingElement> tierEntryScaling,
                                                int tierEntryIndex,
                                                IReadOnlyDictionary<string, PlayerFormulaValue> variableContext)
    {
        if (tierEntryIndex < 0 || tierEntryIndex >= tierEntries.Length)
            return 0f;

        float selectionWeight = mathMax(0f, tierEntries[tierEntryIndex].SelectionWeight);

        if (!TryResolveTierEntryScaling(tierEntryScaling, tierEntryIndex, out PlayerPowerUpTierEntryScalingElement scalingEntry))
            return selectionWeight;

        string scalingFormula = scalingEntry.ScalingFormula.ToString();

        if (string.IsNullOrWhiteSpace(scalingFormula))
            return selectionWeight;

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(scalingFormula,
                                                                   scalingEntry.BaseSelectionWeight,
                                                                   variableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
        {
            return selectionWeight;
        }

        return mathMax(0f, evaluatedValue);
    }

    private static bool TryResolveTierEntryScaling(DynamicBuffer<PlayerPowerUpTierEntryScalingElement> tierEntryScaling,
                                                   int tierEntryIndex,
                                                   out PlayerPowerUpTierEntryScalingElement scalingEntry)
    {
        scalingEntry = default;

        if (!tierEntryScaling.IsCreated)
            return false;

        for (int scalingIndex = 0; scalingIndex < tierEntryScaling.Length; scalingIndex++)
        {
            PlayerPowerUpTierEntryScalingElement candidate = tierEntryScaling[scalingIndex];

            if (candidate.TierEntryIndex != tierEntryIndex)
                continue;

            scalingEntry = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves one weighted random index.
    /// </summary>
    /// <param name="weights">Weight list where each element maps to one candidate index.</param>
    /// <returns>Rolled candidate index, or -1 when no valid weight exists.</returns>
    private static int RollWeightedIndex(List<float> weights)
    {
        if (weights == null || weights.Count <= 0)
            return -1;

        float totalWeight = 0f;

        for (int weightIndex = 0; weightIndex < weights.Count; weightIndex++)
            totalWeight += mathMax(0f, weights[weightIndex]);

        if (totalWeight <= 0f)
            return -1;

        float roll = UnityEngine.Random.value * totalWeight;
        float cumulativeWeight = 0f;

        for (int weightIndex = 0; weightIndex < weights.Count; weightIndex++)
        {
            cumulativeWeight += mathMax(0f, weights[weightIndex]);

            if (roll > cumulativeWeight)
                continue;

            return weightIndex;
        }

        return weights.Count - 1;
    }

    private static int mathMax(int left, int right)
    {
        return left > right ? left : right;
    }

    private static int mathMin(int left, int right)
    {
        return left < right ? left : right;
    }

    private static float mathMax(float left, float right)
    {
        return left > right ? left : right;
    }
    #endregion

    #endregion
}
