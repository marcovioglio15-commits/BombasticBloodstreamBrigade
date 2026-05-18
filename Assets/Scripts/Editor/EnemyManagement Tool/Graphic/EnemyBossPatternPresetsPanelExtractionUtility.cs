using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the boss Pattern Extraction controls used by boss pattern presets.
/// </summary>
internal static class EnemyBossPatternPresetsPanelExtractionUtility
{
    #region Constants
    private const float DefaultMaximumConditionSeconds = 180f;
    private const float DefaultMaximumTravelDistance = 300f;
    private const float DefaultMaximumPlayerDistance = 64f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the high-level pattern extraction settings card.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    /// <param name="parent">Parent receiving the card.</param>
    public static void BuildExtractionSettingsCard(EnemyBossPatternPresetsPanel panel,
                                                   SerializedProperty extractionSettingsProperty,
                                                   VisualElement parent)
    {
        BuildExtractionSettingsCard(panel,
                                    extractionSettingsProperty,
                                    parent,
                                    "Pattern Extraction",
                                    "BossPatternExtraction",
                                    true);
    }

    /// <summary>
    /// Builds an extraction settings card with a caller-provided title and foldout state key.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    /// <param name="parent">Parent receiving the card.</param>
    /// <param name="title">Foldout title shown for this extraction block.</param>
    /// <param name="stateKeySuffix">Stable suffix used to store foldout state.</param>
    /// <param name="defaultExpanded">Initial foldout state when no persisted state exists.</param>
    public static void BuildExtractionSettingsCard(EnemyBossPatternPresetsPanel panel,
                                                   SerializedProperty extractionSettingsProperty,
                                                   VisualElement parent,
                                                   string title,
                                                   string stateKeySuffix,
                                                   bool defaultExpanded)
    {
        if (extractionSettingsProperty == null || parent == null)
            return;

        VisualElement card = EnemyBossPatternPresetsPanelSharedUtility.CreateCard();
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(extractionSettingsProperty,
                                                                                  title,
                                                                                  stateKeySuffix,
                                                                                  defaultExpanded);
        card.Add(foldout);
        foldout.Add(new HelpBox("Enabled extraction triggers are alternatives: any single satisfied trigger can roll a new candidate after the minimum-time gate.", HelpBoxMessageType.Info));
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel,
                                                                                          extractionSettingsProperty.FindPropertyRelative("rerollWhenCurrentPatternBecomesInvalid"),
                                                                                          "Reroll When Current Pattern Is Ineligible",
                                                                                          "Extract a new pattern when the currently active candidate no longer satisfies its eligibility criterion."));
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel,
                                                                      foldout,
                                                                      extractionSettingsProperty.FindPropertyRelative("minimumSecondsBetweenExtractions"),
                                                                      "Minimum Seconds Between Extractions",
                                                                      0f,
                                                                      20f,
                                                                      "Minimum time gate applied before interval, health, distance or damage extraction can trigger.");
        AddElapsedExtractionFields(panel, foldout, extractionSettingsProperty);
        AddMissingHealthExtractionFields(panel, foldout, extractionSettingsProperty);
        AddTravelledDistanceExtractionFields(panel, foldout, extractionSettingsProperty);
        AddPlayerDistanceExtractionFields(panel, foldout, extractionSettingsProperty);
        AddDamageWindowExtractionFields(panel, foldout, extractionSettingsProperty);
        parent.Add(card);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds elapsed-interval extraction controls when enabled.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    private static void AddElapsedExtractionFields(EnemyBossPatternPresetsPanel panel,
                                                   VisualElement parent,
                                                   SerializedProperty extractionSettingsProperty)
    {
        SerializedProperty enabledProperty = extractionSettingsProperty.FindPropertyRelative("useElapsedIntervalExtraction");
        parent.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, "Use Elapsed Interval", "Allow elapsed time since the previous extraction to trigger a new pattern roll."));

        if (enabledProperty != null && enabledProperty.boolValue)
            EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, extractionSettingsProperty.FindPropertyRelative("elapsedIntervalSeconds"), "Elapsed Interval Seconds", 0f, DefaultMaximumConditionSeconds, "Seconds after the previous extraction before elapsed time can trigger.");
    }

    /// <summary>
    /// Adds missing-health step extraction controls when enabled.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    private static void AddMissingHealthExtractionFields(EnemyBossPatternPresetsPanel panel,
                                                         VisualElement parent,
                                                         SerializedProperty extractionSettingsProperty)
    {
        SerializedProperty enabledProperty = extractionSettingsProperty.FindPropertyRelative("useMissingHealthStepExtraction");
        parent.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, "Use Missing Health Step", "Allow crossing a missing-health step since the previous extraction to trigger a new pattern roll."));

        if (enabledProperty != null && enabledProperty.boolValue)
            EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, extractionSettingsProperty.FindPropertyRelative("missingHealthStepPercent"), "Missing Health Step", 0f, 1f, "Normalized missing-health delta required since the previous extraction.");
    }

    /// <summary>
    /// Adds travelled-distance extraction controls when enabled.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    private static void AddTravelledDistanceExtractionFields(EnemyBossPatternPresetsPanel panel,
                                                             VisualElement parent,
                                                             SerializedProperty extractionSettingsProperty)
    {
        SerializedProperty enabledProperty = extractionSettingsProperty.FindPropertyRelative("useTravelledDistanceExtraction");
        parent.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, "Use Travelled Distance", "Allow boss movement distance since the previous extraction to trigger a new pattern roll."));

        if (enabledProperty != null && enabledProperty.boolValue)
            EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, extractionSettingsProperty.FindPropertyRelative("travelledDistanceSinceLastExtraction"), "Distance Since Extraction", 0f, DefaultMaximumTravelDistance, "Planar boss movement distance required since the previous extraction.");
    }

    /// <summary>
    /// Adds player-distance hold extraction controls when a condition is selected.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    private static void AddPlayerDistanceExtractionFields(EnemyBossPatternPresetsPanel panel,
                                                          VisualElement parent,
                                                          SerializedProperty extractionSettingsProperty)
    {
        SerializedProperty conditionProperty = extractionSettingsProperty.FindPropertyRelative("playerDistanceCondition");
        parent.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, conditionProperty, "Player Distance Condition", "Optional player-distance hold condition that can trigger a new pattern roll."));

        if (conditionProperty == null || conditionProperty.enumValueIndex == Convert.ToInt32(EnemyBossPatternPlayerDistanceCondition.Disabled))
            return;

        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, extractionSettingsProperty.FindPropertyRelative("playerDistanceThreshold"), "Player Distance Threshold", 0f, DefaultMaximumPlayerDistance, "Planar distance threshold used by the selected player-distance condition.");
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, extractionSettingsProperty.FindPropertyRelative("playerDistanceHoldSeconds"), "Player Distance Hold Seconds", 0f, 20f, "Seconds the distance condition must remain true before extraction can trigger.");
    }

    /// <summary>
    /// Adds damage-window extraction controls when enabled.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    private static void AddDamageWindowExtractionFields(EnemyBossPatternPresetsPanel panel,
                                                        VisualElement parent,
                                                        SerializedProperty extractionSettingsProperty)
    {
        SerializedProperty enabledProperty = extractionSettingsProperty.FindPropertyRelative("useDamageWindowExtraction");
        parent.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, "Use Damage Window", "Allow received damage accumulated inside a time window to trigger a new pattern roll."));

        if (enabledProperty == null || !enabledProperty.boolValue)
            return;

        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, extractionSettingsProperty.FindPropertyRelative("damageWindowSeconds"), "Damage Window Seconds", 0.05f, 20f, "Seconds used to accumulate received damage.");
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, extractionSettingsProperty.FindPropertyRelative("damageThreshold"), "Damage Threshold", 0f, 1000f, "Damage amount required inside the window before extraction can trigger.");
    }
    #endregion

    #endregion
}
