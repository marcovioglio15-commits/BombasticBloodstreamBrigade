using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Resolves raw numeric values and optional Add Scaling formulas needed by runtime roll systems.
/// </summary>
internal static class PlayerRuntimeScalingBakeMetadataUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves raw drop-pool tier-roll scaling metadata from the source power-ups preset.
    /// </summary>
    /// <param name="sourcePreset">Unscaled source power-ups preset.</param>
    /// <param name="dropPoolIndex">Drop-pool index inside the preset.</param>
    /// <param name="tierRollIndex">Tier-roll index inside the selected drop pool.</param>
    /// <param name="baseValue">Raw numeric value stored on the source preset.</param>
    /// <param name="formula">Enabled Add Scaling formula when present.</param>
    /// <returns>True when metadata was resolved from the source preset; otherwise false.<returns>
    public static bool TryResolveDropPoolTierRollScalingData(PlayerPowerUpsPreset sourcePreset,
                                                             int dropPoolIndex,
                                                             int tierRollIndex,
                                                             out float baseValue,
                                                             out string formula)
    {
        baseValue = 0f;
        formula = string.Empty;

        if (sourcePreset == null || sourcePreset.DropPools == null)
            return false;

        if (dropPoolIndex < 0 || dropPoolIndex >= sourcePreset.DropPools.Count)
            return false;

        PowerUpDropPoolDefinition dropPool = sourcePreset.DropPools[dropPoolIndex];

        if (dropPool == null || dropPool.TierRolls == null)
            return false;

        if (tierRollIndex < 0 || tierRollIndex >= dropPool.TierRolls.Count)
            return false;

        PowerUpDropPoolTierDefinition tierRoll = dropPool.TierRolls[tierRollIndex];

        if (tierRoll == null)
            return false;

        baseValue = tierRoll.SelectionPercentage;
        formula = ResolveFormula(sourcePreset,
                                 string.Format("dropPools.Array.data[{0}].tierRolls.Array.data[{1}].selectionPercentage",
                                               dropPoolIndex,
                                               tierRollIndex));
        return true;
    }

    /// <summary>
    /// Resolves raw tier-entry scaling metadata from the source power-ups preset.
    /// </summary>
    /// <param name="sourcePreset">Unscaled source power-ups preset.</param>
    /// <param name="tierIndex">Tier index inside the preset.</param>
    /// <param name="entryIndex">Entry index inside the selected tier.</param>
    /// <param name="baseValue">Raw numeric value stored on the source preset.</param>
    /// <param name="formula">Enabled Add Scaling formula when present.</param>
    /// <returns>True when metadata was resolved from the source preset; otherwise false.<returns>
    public static bool TryResolveTierEntryScalingData(PlayerPowerUpsPreset sourcePreset,
                                                      int tierIndex,
                                                      int entryIndex,
                                                      out float baseValue,
                                                      out string formula)
    {
        baseValue = 0f;
        formula = string.Empty;

        if (sourcePreset == null || sourcePreset.TierLevels == null)
            return false;

        if (tierIndex < 0 || tierIndex >= sourcePreset.TierLevels.Count)
            return false;

        PowerUpTierLevelDefinition tierLevel = sourcePreset.TierLevels[tierIndex];

        if (tierLevel == null || tierLevel.Entries == null)
            return false;

        if (entryIndex < 0 || entryIndex >= tierLevel.Entries.Count)
            return false;

        PowerUpTierEntryDefinition tierEntry = tierLevel.Entries[entryIndex];

        if (tierEntry == null)
            return false;

        baseValue = tierEntry.SelectionWeight;
        formula = ResolveFormula(sourcePreset,
                                 string.Format("tierLevels.Array.data[{0}].entries.Array.data[{1}].selectionWeight",
                                               tierIndex,
                                               entryIndex));
        return true;
    }

    /// <summary>
    /// Resolves raw legacy milestone tier-roll scaling metadata from the source progression preset.
    /// </summary>
    /// <param name="sourcePreset">Unscaled source progression preset.</param>
    /// <param name="phaseIndex">Game-phase index inside the preset.</param>
    /// <param name="milestoneIndex">Milestone index inside the phase.</param>
    /// <param name="tierRollIndex">Legacy tier-roll index inside the milestone unlock.</param>
    /// <param name="baseValue">Raw numeric value stored on the source preset.</param>
    /// <param name="formula">Enabled Add Scaling formula when present.</param>
    /// <returns>True when metadata was resolved from the source preset; otherwise false.<returns>
    public static bool TryResolveLegacyMilestoneTierRollScalingData(PlayerProgressionPreset sourcePreset,
                                                                    int phaseIndex,
                                                                    int milestoneIndex,
                                                                    int unlockIndex,
                                                                    int tierRollIndex,
                                                                    out float baseValue,
                                                                    out string formula)
    {
        baseValue = 0f;
        formula = string.Empty;

        if (sourcePreset == null || sourcePreset.GamePhasesDefinition == null)
            return false;

        if (phaseIndex < 0 || phaseIndex >= sourcePreset.GamePhasesDefinition.Count)
            return false;

        PlayerGamePhaseDefinition gamePhase = sourcePreset.GamePhasesDefinition[phaseIndex];

        if (gamePhase == null || gamePhase.Milestones == null)
            return false;

        if (milestoneIndex < 0 || milestoneIndex >= gamePhase.Milestones.Count)
            return false;

        PlayerLevelUpMilestoneDefinition milestone = gamePhase.Milestones[milestoneIndex];

        if (milestone == null || milestone.PowerUpUnlocks == null)
            return false;

        if (unlockIndex < 0 || unlockIndex >= milestone.PowerUpUnlocks.Count)
            return false;

        PlayerMilestonePowerUpUnlockDefinition powerUpUnlock = milestone.PowerUpUnlocks[unlockIndex];

        if (powerUpUnlock == null || powerUpUnlock.LegacyTierRolls == null)
            return false;

        if (tierRollIndex < 0 || tierRollIndex >= powerUpUnlock.LegacyTierRolls.Count)
            return false;

        string propertyPath = string.Format("gamePhasesDefinition.Array.data[{0}].milestones.Array.data[{1}].powerUpUnlocks.Array.data[{2}].legacyTierRolls.Array.data[{3}].selectionPercentage",
                                            phaseIndex,
                                            milestoneIndex,
                                            unlockIndex,
                                            tierRollIndex);

#if UNITY_EDITOR
        SerializedObject serializedPreset = new SerializedObject(sourcePreset);
        SerializedProperty selectionPercentageProperty = serializedPreset.FindProperty(propertyPath);

        if (selectionPercentageProperty == null)
            return false;

        baseValue = selectionPercentageProperty.floatValue;
        formula = ResolveFormula(serializedPreset,
                                 sourcePreset.ScalingRules,
                                 selectionPercentageProperty);
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// Resolves raw experience pickup radius scaling metadata from the source progression preset.
    /// </summary>
    /// <param name="sourcePreset">Unscaled source progression preset.</param>
    /// <param name="baseValue">Raw numeric value stored on the source preset.</param>
    /// <param name="formula">Enabled Add Scaling formula when present.</param>
    /// <returns>True when metadata was resolved from the source preset; otherwise false.<returns>
    public static bool TryResolveExperiencePickupRadiusScalingData(PlayerProgressionPreset sourcePreset,
                                                                   out float baseValue,
                                                                   out string formula)
    {
        baseValue = 0f;
        formula = string.Empty;

        if (sourcePreset == null)
            return false;

        baseValue = sourcePreset.ExperiencePickupRadius;
        formula = ResolveFormula(sourcePreset, "experiencePickupRadius");
        return true;
    }

    /// <summary>
    /// Resolves raw dropped-container interaction lock scaling metadata from the source progression preset.
    /// /params sourcePreset Unscaled source progression preset.
    /// /params baseValue Raw interaction lock duration stored on the source preset.
    /// /params formula Enabled Add Scaling formula when present.
    /// /returns True when metadata was resolved from the source preset; otherwise false.
    /// </summary>
    public static bool TryResolvePowerUpContainerInteractionLockDurationScalingData(PlayerProgressionPreset sourcePreset,
                                                                                   out float baseValue,
                                                                                   out string formula)
    {
        baseValue = 0f;
        formula = string.Empty;

        if (sourcePreset == null)
            return false;

        PlayerPowerUpContainerInteractionSettings settings = sourcePreset.PowerUpContainerSettings;

        if (settings == null)
            return false;

        baseValue = settings.InteractionLockDuration;
        formula = ResolveFormula(sourcePreset, "powerUpContainerSettings.interactionLockDuration");
        return true;
    }
    #endregion

    #region Private Methods
    private static string ResolveFormula(PlayerPowerUpsPreset sourcePreset, string propertyPath)
    {
        if (sourcePreset == null || string.IsNullOrWhiteSpace(propertyPath))
            return string.Empty;

#if UNITY_EDITOR
        SerializedObject serializedPreset = new SerializedObject(sourcePreset);
        SerializedProperty property = serializedPreset.FindProperty(propertyPath);
        return ResolveFormula(serializedPreset, sourcePreset.ScalingRules, property);
#else
        return string.Empty;
#endif
    }

    private static string ResolveFormula(PlayerProgressionPreset sourcePreset, string propertyPath)
    {
        if (sourcePreset == null || string.IsNullOrWhiteSpace(propertyPath))
            return string.Empty;

#if UNITY_EDITOR
        SerializedObject serializedPreset = new SerializedObject(sourcePreset);
        SerializedProperty property = serializedPreset.FindProperty(propertyPath);
        return ResolveFormula(serializedPreset, sourcePreset.ScalingRules, property);
#else
        return string.Empty;
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Resolves the enabled Add Scaling formula associated with one serialized numeric property.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the target property.</param>
    /// <param name="scalingRules">Scaling rules list stored on the source preset.</param>
    /// <param name="property">Serialized property whose stat key should be resolved.</param>
    /// <returns>Enabled formula when available; otherwise an empty string.<returns>
    private static string ResolveFormula(SerializedObject serializedObject,
                                         System.Collections.Generic.IReadOnlyList<PlayerStatScalingRule> scalingRules,
                                         SerializedProperty property)
    {
        if (serializedObject == null || property == null || scalingRules == null)
            return string.Empty;

        string statKey = PlayerScalingStatKeyUtility.BuildStatKey(property);

        if (string.IsNullOrWhiteSpace(statKey))
            return string.Empty;

        for (int ruleIndex = 0; ruleIndex < scalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling)
                continue;

            if (string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            if (!string.Equals(scalingRule.StatKey, statKey, StringComparison.Ordinal))
                continue;

            return scalingRule.Formula.Trim();
        }

        return string.Empty;
    }
#endif
    #endregion

    #endregion
}
