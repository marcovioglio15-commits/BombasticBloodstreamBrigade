using System;
using UnityEditor;

/// <summary>
/// Applies deterministic defaults to boss mixed patterns inserted through Unity serialized arrays, which otherwise clone the previous element.
/// </summary>
internal static class EnemyBossPatternPresetsPanelDefaultsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures one newly inserted boss mixed pattern without inheriting eligibility, warning or internal extraction data from its predecessor.
    /// </summary>
    /// <param name="insertedInteraction">Serialized interaction created by Unity array insertion.</param>
    /// <param name="sourcePreset">Source module catalog used to seed the first Core Movement candidate.</param>
    /// <param name="insertIndex">Interaction index used for the readable default name.</param>
    public static void ConfigureInsertedInteraction(SerializedProperty insertedInteraction,
                                                    EnemyModulesAndPatternsPreset sourcePreset,
                                                    int insertIndex)
    {
        if (insertedInteraction == null)
            return;

        ConfigureInteractionIdentity(insertedInteraction, insertIndex);
        ConfigureInteractionEligibility(insertedInteraction);
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(insertedInteraction.FindPropertyRelative("useEngagementFeedbackOverride"), false);
        EnemyOffensiveEngagementFeedbackDrawerUtility.ApplyDefaultValues(insertedInteraction.FindPropertyRelative("engagementFeedbackOverride"));
        ConfigureDefaultExtractionSlot(insertedInteraction.FindPropertyRelative("coreMovementExtraction"));
        ConfigureDefaultExtractionSlot(insertedInteraction.FindPropertyRelative("shortRangeExtraction"));
        ConfigureDefaultExtractionSlot(insertedInteraction.FindPropertyRelative("weaponExtraction"));
        DisableLegacyInteractionSlots(insertedInteraction);
        ConfigureDefaultCoreCandidate(insertedInteraction.FindPropertyRelative("coreMovementExtraction"), sourcePreset);
    }

    /// <summary>
    /// Clears warning toggles and payload values cloned by Unity when a boss module candidate is inserted into a non-empty array.
    /// </summary>
    /// <param name="candidateProperty">Serialized module candidate being initialized.</param>
    /// <param name="slotKind">Boss slot that determines where engagement settings are stored.</param>
    public static void ConfigureCandidateWarningDefaults(SerializedProperty candidateProperty,
                                                         EnemyBossPatternSlotKind slotKind)
    {
        if (candidateProperty == null)
            return;

        SerializedProperty warningOwnerProperty = slotKind == EnemyBossPatternSlotKind.CoreMovement
            ? candidateProperty
            : candidateProperty.FindPropertyRelative("interaction");

        if (warningOwnerProperty == null)
            return;

        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(warningOwnerProperty.FindPropertyRelative("displayBehaviourEngagementTrigger"), false);
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(warningOwnerProperty.FindPropertyRelative("preventWarningInterruption"), false);
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(warningOwnerProperty.FindPropertyRelative("useEngagementFeedbackOverride"), false);
        EnemyOffensiveEngagementFeedbackDrawerUtility.ApplyDefaultValues(warningOwnerProperty.FindPropertyRelative("engagementFeedbackOverride"));
    }
    #endregion

    #region Interaction Defaults
    /// <summary>
    /// Applies stable enabled, type and display-name defaults to one new mixed pattern.
    /// </summary>
    /// <param name="interactionProperty">Serialized mixed-pattern definition.</param>
    /// <param name="insertIndex">Array index used for the generated display name.</param>
    private static void ConfigureInteractionIdentity(SerializedProperty interactionProperty, int insertIndex)
    {
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(interactionProperty.FindPropertyRelative("enabled"), true);
        EnemyBossPatternPresetsPanelModuleUtility.SetEnumIndex(interactionProperty.FindPropertyRelative("interactionType"), Convert.ToInt32(EnemyBossPatternInteractionType.Always));
        EnemyBossPatternPresetsPanelModuleUtility.SetString(interactionProperty.FindPropertyRelative("displayName"), "Always Mixed Pattern " + (insertIndex + 1));
    }

    /// <summary>
    /// Resets eligibility thresholds and selection weight to the runtime definition defaults.
    /// </summary>
    /// <param name="interactionProperty">Serialized mixed-pattern definition.</param>
    private static void ConfigureInteractionEligibility(SerializedProperty interactionProperty)
    {
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("minimumActiveSeconds"), 1f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("selectionWeight"), 1f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("minimumMissingHealthPercent"), 0.25f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("maximumMissingHealthPercent"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("minimumElapsedSeconds"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("maximumElapsedSeconds"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("minimumTravelledDistance"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("maximumTravelledDistance"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("minimumPlayerDistance"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("maximumPlayerDistance"), 12f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(interactionProperty.FindPropertyRelative("recentlyDamagedWindowSeconds"), 1.25f);
    }
    #endregion

    #region Extraction Defaults
    /// <summary>
    /// Resets one internal extraction slot so cloned candidates and trigger thresholds cannot leak from the previous mixed pattern.
    /// </summary>
    /// <param name="extractionProperty">Serialized internal extraction definition to reset.</param>
    private static void ConfigureDefaultExtractionSlot(SerializedProperty extractionProperty)
    {
        if (extractionProperty == null)
            return;

        ConfigureDefaultExtractionSettings(extractionProperty.FindPropertyRelative("extractionSettings"));
        SerializedProperty candidatesProperty = extractionProperty.FindPropertyRelative("candidates");

        if (candidatesProperty != null)
            candidatesProperty.arraySize = 0;
    }

    /// <summary>
    /// Applies canonical extraction defaults to one new internal slot.
    /// </summary>
    /// <param name="settingsProperty">Serialized extraction settings block to initialize.</param>
    private static void ConfigureDefaultExtractionSettings(SerializedProperty settingsProperty)
    {
        if (settingsProperty == null)
            return;

        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(settingsProperty.FindPropertyRelative("rerollWhenCurrentPatternBecomesInvalid"), true);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(settingsProperty.FindPropertyRelative("minimumSecondsBetweenExtractions"), 1f);
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(settingsProperty.FindPropertyRelative("useElapsedIntervalExtraction"), true);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(settingsProperty.FindPropertyRelative("elapsedIntervalSeconds"), 4f);
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(settingsProperty.FindPropertyRelative("useMissingHealthStepExtraction"), true);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(settingsProperty.FindPropertyRelative("missingHealthStepPercent"), 0.25f);
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(settingsProperty.FindPropertyRelative("useTravelledDistanceExtraction"), false);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(settingsProperty.FindPropertyRelative("travelledDistanceSinceLastExtraction"), 10f);
        EnemyBossPatternPresetsPanelModuleUtility.SetEnumIndex(settingsProperty.FindPropertyRelative("playerDistanceCondition"), Convert.ToInt32(EnemyBossPatternPlayerDistanceCondition.Disabled));
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(settingsProperty.FindPropertyRelative("playerDistanceThreshold"), 8f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(settingsProperty.FindPropertyRelative("playerDistanceHoldSeconds"), 1f);
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(settingsProperty.FindPropertyRelative("useDamageWindowExtraction"), false);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(settingsProperty.FindPropertyRelative("damageWindowSeconds"), 2f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(settingsProperty.FindPropertyRelative("damageThreshold"), 20f);
    }

    /// <summary>
    /// Adds the first available Core Movement module to a new mixed pattern after its candidate lists have been cleared.
    /// </summary>
    /// <param name="coreExtractionProperty">Serialized Core Movement extraction root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    private static void ConfigureDefaultCoreCandidate(SerializedProperty coreExtractionProperty,
                                                      EnemyModulesAndPatternsPreset sourcePreset)
    {
        if (coreExtractionProperty == null)
            return;

        if (!EnemyBossPatternPresetsPanelModuleUtility.TryResolveFirstModuleId(sourcePreset,
                                                                               EnemyPatternModuleCatalogSection.CoreMovement,
                                                                               out string moduleId))
            return;

        SerializedProperty candidatesProperty = coreExtractionProperty.FindPropertyRelative("candidates");

        if (candidatesProperty == null)
            return;

        candidatesProperty.InsertArrayElementAtIndex(0);
        SerializedProperty candidateProperty = candidatesProperty.GetArrayElementAtIndex(0);
        SerializedProperty eligibilityProperty = candidateProperty.FindPropertyRelative("eligibility");
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(eligibilityProperty.FindPropertyRelative("enabled"), true);
        EnemyBossPatternPresetsPanelModuleUtility.SetString(eligibilityProperty.FindPropertyRelative("displayName"), "Default Core Movement");
        EnemyBossPatternPresetsPanelModuleUtility.SetEnumIndex(candidateProperty.FindPropertyRelative("moduleMode"), Convert.ToInt32(EnemyBossPatternModuleMode.Module));
        EnemyBossPatternPresetsPanelModuleUtility.ConfigureBinding(candidateProperty.FindPropertyRelative("binding"), moduleId);
        ConfigureCandidateWarningDefaults(candidateProperty,
                                          EnemyBossPatternSlotKind.CoreMovement);
    }
    #endregion

    #region Legacy Defaults
    /// <summary>
    /// Disables hidden legacy slots so validation cannot migrate cloned content back into reset candidate lists.
    /// </summary>
    /// <param name="interactionProperty">Serialized mixed-pattern interaction being initialized.</param>
    private static void DisableLegacyInteractionSlots(SerializedProperty interactionProperty)
    {
        DisableLegacySlot(interactionProperty.FindPropertyRelative("coreMovement"));
        DisableLegacySlot(interactionProperty.FindPropertyRelative("shortRangeInteraction"));
        DisableLegacySlot(interactionProperty.FindPropertyRelative("weaponInteraction"));
    }

    /// <summary>
    /// Disables one hidden legacy slot when its enable field exists.
    /// </summary>
    /// <param name="slotProperty">Serialized legacy slot.</param>
    private static void DisableLegacySlot(SerializedProperty slotProperty)
    {
        if (slotProperty != null)
            EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(slotProperty.FindPropertyRelative("isEnabled"), false);
    }
    #endregion

    #endregion
}
