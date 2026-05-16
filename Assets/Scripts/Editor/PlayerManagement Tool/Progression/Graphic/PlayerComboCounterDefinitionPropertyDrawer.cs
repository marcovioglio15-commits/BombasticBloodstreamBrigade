using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws the combo-counter preset module with scalable global settings and list-level validation warnings.
/// none.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerComboCounterDefinition))]
public sealed class PlayerComboCounterDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for the combo-counter definition.
    /// </summary>
    /// <param name="property">Serialized combo-counter property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty isEnabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty comboGainPerKillProperty = property.FindPropertyRelative("comboGainPerKill");
        SerializedProperty damageBreakModeProperty = property.FindPropertyRelative("damageBreakMode");
        SerializedProperty shieldDamageBreaksComboProperty = property.FindPropertyRelative("shieldDamageBreaksCombo");
        SerializedProperty preventDecayIntoNonDecayingRanksProperty = property.FindPropertyRelative("preventDecayIntoNonDecayingRanks");
        SerializedProperty rankDefinitionsProperty = property.FindPropertyRelative("rankDefinitions");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (isEnabledProperty == null ||
            comboGainPerKillProperty == null ||
            damageBreakModeProperty == null ||
            shieldDamageBreaksComboProperty == null ||
            preventDecayIntoNonDecayingRanksProperty == null ||
            rankDefinitionsProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Combo counter fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        HelpBox infoBox = new HelpBox("Health damage always triggers the selected Damage Break Mode. Shield damage uses the same break mode only when Shield Damage Breaks Combo is enabled, and each rank can also define point decay, progressive Character Tuning boost, and passive power-up unlocks. All numeric, boolean, enum, and token fields below support Add Scaling where applicable.", HelpBoxMessageType.Info);
        root.Add(infoBox);
        root.Add(PlayerScalingFieldElementFactory.CreateField(isEnabledProperty,
                                                              scalingRulesProperty,
                                                              "Enabled"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(comboGainPerKillProperty,
                                                              scalingRulesProperty,
                                                              "Combo Gain Per Kill"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(damageBreakModeProperty,
                                                              scalingRulesProperty,
                                                              "Damage Break Mode"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(shieldDamageBreaksComboProperty,
                                                              scalingRulesProperty,
                                                              "Shield Damage Breaks Combo"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(preventDecayIntoNonDecayingRanksProperty,
                                                              scalingRulesProperty,
                                                              "Prevent Decay Into Non-Decaying Ranks"));

        PropertyField rankDefinitionsField = new PropertyField(rankDefinitionsProperty, "Rank Definitions");
        rankDefinitionsField.BindProperty(rankDefinitionsProperty);
        root.Add(rankDefinitionsField);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(warningBox);

        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshWarnings(isEnabledProperty,
                            comboGainPerKillProperty,
                            damageBreakModeProperty,
                            preventDecayIntoNonDecayingRanksProperty,
                            rankDefinitionsProperty,
                            warningBox);
        });

        RefreshWarnings(isEnabledProperty,
                        comboGainPerKillProperty,
                        damageBreakModeProperty,
                        preventDecayIntoNonDecayingRanksProperty,
                        rankDefinitionsProperty,
                        warningBox);
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Rebuilds the warning message shown for the combo-counter module.
    /// </summary>
    /// <param name="isEnabledProperty">Serialized combo enabled property.</param>
    /// <param name="comboGainPerKillProperty">Serialized kill gain property.</param>
    /// <param name="damageBreakModeProperty">Serialized damage-break mode property.</param>
    /// <param name="preventDecayIntoNonDecayingRanksProperty">Serialized decay-floor preservation property.</param>
    /// <param name="rankDefinitionsProperty">Serialized combo-rank list property.</param>
    /// <param name="warningBox">Warning help box refreshed in place.</param>
    private static void RefreshWarnings(SerializedProperty isEnabledProperty,
                                        SerializedProperty comboGainPerKillProperty,
                                        SerializedProperty damageBreakModeProperty,
                                        SerializedProperty preventDecayIntoNonDecayingRanksProperty,
                                        SerializedProperty rankDefinitionsProperty,
                                        HelpBox warningBox)
    {
        if (warningBox == null)
        {
            return;
        }

        List<string> warningLines = new List<string>();
        bool usesRankDowngrade = ResolveDamageBreakMode(damageBreakModeProperty) == PlayerComboDamageBreakMode.DowngradeToPreviousRank;

        if (isEnabledProperty != null &&
            isEnabledProperty.propertyType == SerializedPropertyType.Boolean &&
            isEnabledProperty.boolValue &&
            comboGainPerKillProperty != null &&
            comboGainPerKillProperty.intValue <= 0)
        {
            warningLines.Add("Combo Gain Per Kill should be > 0 while the combo counter is enabled.");
        }

        if (rankDefinitionsProperty == null || !rankDefinitionsProperty.isArray)
        {
            warningLines.Add("Rank Definitions are not available.");
        }
        else if (rankDefinitionsProperty.arraySize <= 0)
        {
            warningLines.Add("No ranks configured. The combo counter can still count kills, but it cannot grant rank bonuses.");

            if (usesRankDowngrade)
            {
                warningLines.Add("Damage Break Mode is set to Downgrade To Previous Rank, but no ranks are configured. Damage will behave like a full reset.");
            }
        }
        else
        {
            HashSet<string> visitedRankIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int previousRequiredValue = int.MinValue;
            float previousPointsDecayPerSecond = 0f;
            bool hasDecayFloorPreservationTransition = false;

            for (int rankIndex = 0; rankIndex < rankDefinitionsProperty.arraySize; rankIndex++)
            {
                SerializedProperty rankProperty = rankDefinitionsProperty.GetArrayElementAtIndex(rankIndex);
                SerializedProperty rankIdProperty = rankProperty != null ? rankProperty.FindPropertyRelative("rankId") : null;
                SerializedProperty requiredComboValueProperty = rankProperty != null ? rankProperty.FindPropertyRelative("requiredComboValue") : null;
                SerializedProperty pointsDecayPerSecondProperty = rankProperty != null ? rankProperty.FindPropertyRelative("pointsDecayPerSecond") : null;
                string rankId = rankIdProperty != null && !string.IsNullOrWhiteSpace(rankIdProperty.stringValue)
                    ? rankIdProperty.stringValue.Trim()
                    : string.Empty;
                int requiredComboValue = requiredComboValueProperty != null ? requiredComboValueProperty.intValue : 0;
                float pointsDecayPerSecond = pointsDecayPerSecondProperty != null ? pointsDecayPerSecondProperty.floatValue : 0f;

                if (string.IsNullOrWhiteSpace(rankId))
                {
                    warningLines.Add(string.Format("Rank #{0} should define a non-empty Rank ID.", rankIndex + 1));
                }
                else if (!visitedRankIds.Add(rankId))
                {
                    warningLines.Add(string.Format("Rank ID '{0}' is duplicated. Stable Add Scaling keys and rank-state labels can become ambiguous.", rankId));
                }

                if (requiredComboValue < 0)
                {
                    warningLines.Add(string.Format("Rank '{0}' should use a Required Combo Value >= 0.", string.IsNullOrWhiteSpace(rankId) ? "#" + (rankIndex + 1) : rankId));
                }

                if (rankIndex > 0 && requiredComboValue < previousRequiredValue)
                {
                    warningLines.Add(string.Format("Rank '{0}' should not require less combo than the previous rank.", string.IsNullOrWhiteSpace(rankId) ? "#" + (rankIndex + 1) : rankId));
                }

                if (usesRankDowngrade &&
                    rankIndex > 0 &&
                    requiredComboValue == previousRequiredValue)
                {
                    warningLines.Add(string.Format("Rank '{0}' uses the same Required Combo Value as the previous rank. Downgrade To Previous Rank may not actually change the active rank.", string.IsNullOrWhiteSpace(rankId) ? "#" + (rankIndex + 1) : rankId));
                }

                if (rankIndex > 0 &&
                    previousPointsDecayPerSecond <= 0f &&
                    pointsDecayPerSecond > 0f)
                {
                    hasDecayFloorPreservationTransition = true;
                }

                previousRequiredValue = requiredComboValue;
                previousPointsDecayPerSecond = pointsDecayPerSecond;
            }

            if (usesRankDowngrade && rankDefinitionsProperty.arraySize < 2)
            {
                warningLines.Add("Downgrade To Previous Rank behaves like a full reset until at least two ranks are configured.");
            }

            if (preventDecayIntoNonDecayingRanksProperty != null &&
                preventDecayIntoNonDecayingRanksProperty.propertyType == SerializedPropertyType.Boolean &&
                preventDecayIntoNonDecayingRanksProperty.boolValue &&
                !hasDecayFloorPreservationTransition)
            {
                warningLines.Add("Prevent Decay Into Non-Decaying Ranks is enabled, but no configured higher rank decays into a lower no-decay rank, so the option currently has no runtime effect.");
            }
        }

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Resolves the authored combo damage-break mode with a safe enum fallback.
    /// </summary>
    /// <param name="damageBreakModeProperty">Serialized damage-break mode property.</param>
    /// <returns>Resolved authored damage-break mode.</returns>
    private static PlayerComboDamageBreakMode ResolveDamageBreakMode(SerializedProperty damageBreakModeProperty)
    {
        if (damageBreakModeProperty == null || damageBreakModeProperty.propertyType != SerializedPropertyType.Enum)
        {
            return PlayerComboDamageBreakMode.ResetCombo;
        }

        if (damageBreakModeProperty.enumValueIndex == (int)PlayerComboDamageBreakMode.DowngradeToPreviousRank)
        {
            return PlayerComboDamageBreakMode.DowngradeToPreviousRank;
        }

        return PlayerComboDamageBreakMode.ResetCombo;
    }
    #endregion

    #endregion
}
