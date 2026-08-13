using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws the combo-counter preset module with scalable global settings and topology-specific validation warnings.
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
        SerializedProperty modeProperty = property.FindPropertyRelative("mode");
        SerializedProperty comboGainPerKillProperty = property.FindPropertyRelative("comboGainPerKill");
        SerializedProperty damageBreakModeProperty = property.FindPropertyRelative("damageBreakMode");
        SerializedProperty shieldDamageBreaksComboProperty = property.FindPropertyRelative("shieldDamageBreaksCombo");
        SerializedProperty preventDecayIntoNonDecayingRanksProperty = property.FindPropertyRelative("preventDecayIntoNonDecayingRanks");
        SerializedProperty rankDefinitionsProperty = property.FindPropertyRelative("rankDefinitions");
        SerializedProperty singleRankProgressionProperty = property.FindPropertyRelative("singleRankProgression");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (isEnabledProperty == null ||
            modeProperty == null ||
            comboGainPerKillProperty == null ||
            damageBreakModeProperty == null ||
            shieldDamageBreaksComboProperty == null ||
            preventDecayIntoNonDecayingRanksProperty == null ||
            rankDefinitionsProperty == null ||
            singleRankProgressionProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Combo counter fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        HelpBox infoBox = new HelpBox("Health damage always triggers the selected Damage Break Mode. Ranks uses independent thresholds and bonuses; Single Rank Progression exposes one capped bar with percentage reward milestones. Every dedicated numeric, Boolean, enum, and token field supports Add Scaling.", HelpBoxMessageType.Info);
        root.Add(infoBox);
        root.Add(PlayerScalingFieldElementFactory.CreateField(isEnabledProperty,
                                                              scalingRulesProperty,
                                                              "Enabled"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(modeProperty,
                                                              scalingRulesProperty,
                                                              "Mode"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(comboGainPerKillProperty,
                                                              scalingRulesProperty,
                                                              "Combo Gain Per Kill"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(damageBreakModeProperty,
                                                              scalingRulesProperty,
                                                              "Damage Break Mode"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(shieldDamageBreaksComboProperty,
                                                              scalingRulesProperty,
                                                              "Shield Damage Breaks Combo"));
        VisualElement ranksOptionsRoot = new VisualElement();
        ranksOptionsRoot.Add(PlayerScalingFieldElementFactory.CreateField(preventDecayIntoNonDecayingRanksProperty,
                                                                          scalingRulesProperty,
                                                                          "Prevent Decay Into Non-Decaying Ranks"));
        PropertyField rankDefinitionsField = new PropertyField(rankDefinitionsProperty, "Rank Definitions");
        rankDefinitionsField.BindProperty(rankDefinitionsProperty);
        ranksOptionsRoot.Add(rankDefinitionsField);
        root.Add(ranksOptionsRoot);

        VisualElement singleRankOptionsRoot = BuildSingleRankOptions(singleRankProgressionProperty, scalingRulesProperty);
        root.Add(singleRankOptionsRoot);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(warningBox);

        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            PlayerManagementSelectionContext.NotifyProgressionPresetContentChanged();
            RefreshWarnings(isEnabledProperty,
                            modeProperty,
                            comboGainPerKillProperty,
                            damageBreakModeProperty,
                            preventDecayIntoNonDecayingRanksProperty,
                            rankDefinitionsProperty,
                            singleRankProgressionProperty,
                            warningBox);
            RefreshModeVisibility(modeProperty, ranksOptionsRoot, singleRankOptionsRoot);
        });

        RefreshModeVisibility(modeProperty, ranksOptionsRoot, singleRankOptionsRoot);
        RefreshWarnings(isEnabledProperty,
                        modeProperty,
                        comboGainPerKillProperty,
                        damageBreakModeProperty,
                        preventDecayIntoNonDecayingRanksProperty,
                        rankDefinitionsProperty,
                        singleRankProgressionProperty,
                        warningBox);
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds all fields dedicated to continuous single-rank progression without exposing power-up-only trigger scope data.
    /// </summary>
    /// <param name="singleRankProgressionProperty">Serialized single-rank progression payload.</param>
    /// <param name="scalingRulesProperty">Serialized progression Add Scaling rule list.</param>
    /// <returns>Container holding single-rank-only options.</returns>
    private static VisualElement BuildSingleRankOptions(SerializedProperty singleRankProgressionProperty,
                                                        SerializedProperty scalingRulesProperty)
    {
        VisualElement root = new VisualElement();
        SerializedProperty rankIdProperty = singleRankProgressionProperty.FindPropertyRelative("rankId");
        SerializedProperty maximumComboValueProperty = singleRankProgressionProperty.FindPropertyRelative("maximumComboValue");
        SerializedProperty pointsDecayPerSecondProperty = singleRankProgressionProperty.FindPropertyRelative("pointsDecayPerSecond");
        SerializedProperty valueDisplayModeProperty = singleRankProgressionProperty.FindPropertyRelative("valueDisplayMode");
        SerializedProperty formulaDistributionModeProperty = singleRankProgressionProperty.FindPropertyRelative("formulaDistributionMode");
        SerializedProperty linearBonusRangeModeProperty = singleRankProgressionProperty.FindPropertyRelative("linearBonusRangeMode");
        SerializedProperty showMeterOnlyAfterFirstMilestoneProperty = singleRankProgressionProperty.FindPropertyRelative("showMeterOnlyAfterFirstMilestone");
        SerializedProperty startLinearBonusesAtFirstMilestoneProperty = singleRankProgressionProperty.FindPropertyRelative("startLinearBonusesAtFirstMilestone");
        SerializedProperty bonusMilestonesProperty = singleRankProgressionProperty.FindPropertyRelative("bonusMilestones");
        root.Add(new HelpBox("The single rank uses one capped progression bar. Its meter can appear from the first combo point or wait for the first enabled milestone. Linear formulas can share one rank-wide weight or progress independently from their milestone to the next enabled threshold.", HelpBoxMessageType.Info));
        root.Add(PlayerScalingFieldElementFactory.CreateField(rankIdProperty,
                                                              scalingRulesProperty,
                                                              "Rank ID",
                                                              null,
                                                              true));
        root.Add(PlayerScalingFieldElementFactory.CreateField(maximumComboValueProperty,
                                                              scalingRulesProperty,
                                                              "Maximum Combo Value"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(pointsDecayPerSecondProperty,
                                                              scalingRulesProperty,
                                                              "Points Decay Per Second"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(valueDisplayModeProperty,
                                                              scalingRulesProperty,
                                                              "Value Display Mode"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(formulaDistributionModeProperty,
                                                              scalingRulesProperty,
                                                              "Formula Distribution Mode"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(showMeterOnlyAfterFirstMilestoneProperty,
                                                              scalingRulesProperty,
                                                              "Show Meter Only After First Milestone"));
        VisualElement linearFormulaOptionsRoot = new VisualElement();
        linearFormulaOptionsRoot.Add(PlayerScalingFieldElementFactory.CreateField(linearBonusRangeModeProperty,
                                                                                   scalingRulesProperty,
                                                                                   "Linear Bonus Range Mode"));
        VisualElement entireProgressionOptionsRoot = new VisualElement();
        entireProgressionOptionsRoot.Add(PlayerScalingFieldElementFactory.CreateField(startLinearBonusesAtFirstMilestoneProperty,
                                                                                       scalingRulesProperty,
                                                                                       "Start Linear Bonuses At First Milestone"));
        linearFormulaOptionsRoot.Add(entireProgressionOptionsRoot);
        root.Add(linearFormulaOptionsRoot);

        if (bonusMilestonesProperty != null)
        {
            PropertyField milestonesField = new PropertyField(bonusMilestonesProperty, "Bonus Milestones");
            milestonesField.BindProperty(bonusMilestonesProperty);
            root.Add(milestonesField);
        }

        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            RefreshSingleRankFormulaVisibility(formulaDistributionModeProperty,
                                               linearBonusRangeModeProperty,
                                               linearFormulaOptionsRoot,
                                               entireProgressionOptionsRoot);
        });
        RefreshSingleRankFormulaVisibility(formulaDistributionModeProperty,
                                           linearBonusRangeModeProperty,
                                           linearFormulaOptionsRoot,
                                           entireProgressionOptionsRoot);

        return root;
    }

    /// <summary>
    /// Shows linear settings only for continuous formulas and rank-wide settings only for their matching range mode.
    /// </summary>
    /// <param name="formulaDistributionModeProperty">Serialized single-rank formula distribution mode.</param>
    /// <param name="linearBonusRangeModeProperty">Serialized interval selection for linear formulas.</param>
    /// <param name="linearFormulaOptionsRoot">Container holding settings that only affect linear formulas.</param>
    /// <param name="entireProgressionOptionsRoot">Container holding settings that only affect rank-wide blending.</param>
    private static void RefreshSingleRankFormulaVisibility(SerializedProperty formulaDistributionModeProperty,
                                                           SerializedProperty linearBonusRangeModeProperty,
                                                           VisualElement linearFormulaOptionsRoot,
                                                           VisualElement entireProgressionOptionsRoot)
    {
        bool usesLinearDistribution = ResolveSingleRankFormulaDistributionMode(formulaDistributionModeProperty) ==
                                      PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression;
        linearFormulaOptionsRoot.style.display = usesLinearDistribution
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        entireProgressionOptionsRoot.style.display = usesLinearDistribution &&
                                                     ResolveSingleRankLinearBonusRangeMode(linearBonusRangeModeProperty) ==
                                                     PlayerComboSingleRankLinearBonusRangeMode.EntireProgression
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Shows only the option group owned by the currently authored combo topology.
    /// </summary>
    /// <param name="modeProperty">Serialized combo mode.</param>
    /// <param name="ranksOptionsRoot">Traditional rank option container.</param>
    /// <param name="singleRankOptionsRoot">Continuous single-rank option container.</param>
    private static void RefreshModeVisibility(SerializedProperty modeProperty,
                                              VisualElement ranksOptionsRoot,
                                              VisualElement singleRankOptionsRoot)
    {
        bool usesSingleRank = ResolveMode(modeProperty) == PlayerComboCounterMode.SingleRankProgression;
        ranksOptionsRoot.style.display = usesSingleRank ? DisplayStyle.None : DisplayStyle.Flex;
        singleRankOptionsRoot.style.display = usesSingleRank ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Rebuilds the warning message shown for the combo-counter module.
    /// </summary>
    /// <param name="isEnabledProperty">Serialized combo enabled property.</param>
    /// <param name="modeProperty">Serialized combo topology.</param>
    /// <param name="comboGainPerKillProperty">Serialized kill gain property.</param>
    /// <param name="damageBreakModeProperty">Serialized damage-break mode property.</param>
    /// <param name="preventDecayIntoNonDecayingRanksProperty">Serialized decay-floor preservation property.</param>
    /// <param name="rankDefinitionsProperty">Serialized combo-rank list property.</param>
    /// <param name="singleRankProgressionProperty">Serialized continuous single-rank payload.</param>
    /// <param name="warningBox">Warning help box refreshed in place.</param>
    private static void RefreshWarnings(SerializedProperty isEnabledProperty,
                                        SerializedProperty modeProperty,
                                        SerializedProperty comboGainPerKillProperty,
                                        SerializedProperty damageBreakModeProperty,
                                        SerializedProperty preventDecayIntoNonDecayingRanksProperty,
                                        SerializedProperty rankDefinitionsProperty,
                                        SerializedProperty singleRankProgressionProperty,
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

        if (ResolveMode(modeProperty) == PlayerComboCounterMode.SingleRankProgression)
        {
            AppendSingleRankWarnings(singleRankProgressionProperty,
                                     usesRankDowngrade,
                                     warningLines);
            ApplyWarnings(warningLines, warningBox);
            return;
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

        ApplyWarnings(warningLines, warningBox);
    }

    /// <summary>
    /// Appends validation messages for the continuous single-rank topology.
    /// </summary>
    /// <param name="singleRankProgressionProperty">Serialized continuous single-rank payload.</param>
    /// <param name="usesRankDowngrade">Whether damage attempts to downgrade one reward milestone.</param>
    /// <param name="warningLines">Destination warning collection.</param>
    private static void AppendSingleRankWarnings(SerializedProperty singleRankProgressionProperty,
                                                 bool usesRankDowngrade,
                                                 List<string> warningLines)
    {
        if (singleRankProgressionProperty == null)
        {
            warningLines.Add("Single Rank Progression settings are not available.");
            return;
        }

        SerializedProperty rankIdProperty = singleRankProgressionProperty.FindPropertyRelative("rankId");
        SerializedProperty maximumComboValueProperty = singleRankProgressionProperty.FindPropertyRelative("maximumComboValue");
        SerializedProperty pointsDecayPerSecondProperty = singleRankProgressionProperty.FindPropertyRelative("pointsDecayPerSecond");
        SerializedProperty formulaDistributionModeProperty = singleRankProgressionProperty.FindPropertyRelative("formulaDistributionMode");
        SerializedProperty linearBonusRangeModeProperty = singleRankProgressionProperty.FindPropertyRelative("linearBonusRangeMode");
        SerializedProperty showMeterOnlyAfterFirstMilestoneProperty = singleRankProgressionProperty.FindPropertyRelative("showMeterOnlyAfterFirstMilestone");
        SerializedProperty startLinearBonusesAtFirstMilestoneProperty = singleRankProgressionProperty.FindPropertyRelative("startLinearBonusesAtFirstMilestone");
        SerializedProperty bonusMilestonesProperty = singleRankProgressionProperty.FindPropertyRelative("bonusMilestones");

        bool delaysMeterUntilFirstMilestone = showMeterOnlyAfterFirstMilestoneProperty != null &&
                                               showMeterOnlyAfterFirstMilestoneProperty.boolValue;
        bool usesLinearBonuses = ResolveSingleRankFormulaDistributionMode(formulaDistributionModeProperty) ==
                                 PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression;
        bool usesSegmentedLinearBonuses = usesLinearBonuses &&
                                          ResolveSingleRankLinearBonusRangeMode(linearBonusRangeModeProperty) ==
                                          PlayerComboSingleRankLinearBonusRangeMode.MilestoneToNextMilestone;
        bool delaysLinearBonusesUntilFirstMilestone = usesLinearBonuses &&
                                                       !usesSegmentedLinearBonuses &&
                                                       startLinearBonusesAtFirstMilestoneProperty != null &&
                                                       startLinearBonusesAtFirstMilestoneProperty.boolValue;

        if (rankIdProperty == null || string.IsNullOrWhiteSpace(rankIdProperty.stringValue))
            warningLines.Add("Single Rank Progression should define a non-empty Rank ID.");

        if (maximumComboValueProperty == null || maximumComboValueProperty.intValue <= 0)
            warningLines.Add("Maximum Combo Value should be > 0 so progression and percentage milestones can be evaluated.");

        if (pointsDecayPerSecondProperty != null &&
            (float.IsNaN(pointsDecayPerSecondProperty.floatValue) ||
             float.IsInfinity(pointsDecayPerSecondProperty.floatValue) ||
             pointsDecayPerSecondProperty.floatValue < 0f))
            warningLines.Add("Points Decay Per Second should be finite and >= 0.");

        if (bonusMilestonesProperty == null || !bonusMilestonesProperty.isArray)
        {
            warningLines.Add("Bonus Milestones are not available.");
            return;
        }

        HashSet<string> visitedMilestoneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        float previousPercentage = float.NegativeInfinity;
        float firstEnabledPercentage = float.PositiveInfinity;
        int enabledMilestoneCount = 0;

        // Validate only enabled milestones because disabled entries do not participate in runtime progression.
        for (int milestoneIndex = 0; milestoneIndex < bonusMilestonesProperty.arraySize; milestoneIndex++)
        {
            SerializedProperty milestoneProperty = bonusMilestonesProperty.GetArrayElementAtIndex(milestoneIndex);
            SerializedProperty enabledProperty = milestoneProperty != null ? milestoneProperty.FindPropertyRelative("isEnabled") : null;

            if (enabledProperty != null && !enabledProperty.boolValue)
                continue;

            SerializedProperty milestoneIdProperty = milestoneProperty != null ? milestoneProperty.FindPropertyRelative("milestoneId") : null;
            SerializedProperty requiredPercentageProperty = milestoneProperty != null ? milestoneProperty.FindPropertyRelative("requiredProgressPercent") : null;
            SerializedProperty bonusesProperty = milestoneProperty != null ? milestoneProperty.FindPropertyRelative("bonuses") : null;
            SerializedProperty formulasProperty = bonusesProperty != null ? bonusesProperty.FindPropertyRelative("formulas") : null;
            string milestoneId = milestoneIdProperty != null && !string.IsNullOrWhiteSpace(milestoneIdProperty.stringValue)
                ? milestoneIdProperty.stringValue.Trim()
                : string.Empty;
            float requiredPercentage = requiredPercentageProperty != null
                ? requiredPercentageProperty.floatValue
                : 0f;
            enabledMilestoneCount++;

            if (requiredPercentage < firstEnabledPercentage)
                firstEnabledPercentage = requiredPercentage;

            if (string.IsNullOrWhiteSpace(milestoneId))
                warningLines.Add(string.Format("Enabled milestone #{0} should define a non-empty Milestone ID.", milestoneIndex + 1));
            else if (!visitedMilestoneIds.Add(milestoneId))
                warningLines.Add(string.Format("Milestone ID '{0}' is duplicated. Stable Add Scaling keys and reward-state labels can become ambiguous.", milestoneId));

            if (float.IsNaN(requiredPercentage) ||
                float.IsInfinity(requiredPercentage) ||
                requiredPercentage < 0f ||
                requiredPercentage > 100f)
                warningLines.Add(string.Format("Milestone '{0}' should use a finite Required Progress Percent from 0 to 100.", string.IsNullOrWhiteSpace(milestoneId) ? "#" + (milestoneIndex + 1) : milestoneId));

            if (requiredPercentage < previousPercentage)
                warningLines.Add(string.Format("Milestone '{0}' should not require a lower percentage than the previous enabled milestone.", string.IsNullOrWhiteSpace(milestoneId) ? "#" + (milestoneIndex + 1) : milestoneId));
            else if (usesRankDowngrade && requiredPercentage == previousPercentage)
                warningLines.Add(string.Format("Milestone '{0}' shares its percentage with the previous enabled milestone. Downgrade To Previous Rank may skip an expected reward boundary.", string.IsNullOrWhiteSpace(milestoneId) ? "#" + (milestoneIndex + 1) : milestoneId));

            if (usesSegmentedLinearBonuses &&
                requiredPercentage >= 100f &&
                formulasProperty != null &&
                formulasProperty.isArray &&
                formulasProperty.arraySize > 0)
                warningLines.Add(string.Format("Milestone '{0}' starts at 100%, so its segmented linear formulas have no interpolation interval and become full at completion.", string.IsNullOrWhiteSpace(milestoneId) ? "#" + (milestoneIndex + 1) : milestoneId));

            previousPercentage = requiredPercentage;
        }

        if (enabledMilestoneCount <= 0)
        {
            warningLines.Add("No enabled bonus milestones are configured. The bar can still progress, but it cannot grant milestone rewards.");

            if (delaysMeterUntilFirstMilestone)
                warningLines.Add("Show Meter Only After First Milestone is enabled, but no enabled milestone can make the meter visible.");

            if (delaysLinearBonusesUntilFirstMilestone)
                warningLines.Add("Start Linear Bonuses At First Milestone is enabled, but no enabled milestone can activate linear formulas.");
        }
        else if (firstEnabledPercentage <= 0f)
        {
            if (delaysMeterUntilFirstMilestone)
                warningLines.Add("Show Meter Only After First Milestone has no practical delay because the first enabled milestone starts at 0%.");

            if (delaysLinearBonusesUntilFirstMilestone)
                warningLines.Add("Start Linear Bonuses At First Milestone has no practical delay because the first enabled milestone starts at 0%.");
        }

        if (usesRankDowngrade && enabledMilestoneCount < 2)
            warningLines.Add("Downgrade To Previous Rank behaves like a full reset until at least two bonus milestones are enabled.");
    }

    /// <summary>
    /// Applies a warning collection to a reusable help box.
    /// </summary>
    /// <param name="warningLines">Validated warning messages.</param>
    /// <param name="warningBox">Warning help box refreshed in place.</param>
    private static void ApplyWarnings(List<string> warningLines, HelpBox warningBox)
    {
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
    /// Resolves the authored combo topology with a safe enum fallback.
    /// </summary>
    /// <param name="modeProperty">Serialized combo mode property.</param>
    /// <returns>Resolved authored combo topology.</returns>
    private static PlayerComboCounterMode ResolveMode(SerializedProperty modeProperty)
    {
        if (modeProperty != null &&
            modeProperty.propertyType == SerializedPropertyType.Enum &&
            modeProperty.enumValueIndex == (int)PlayerComboCounterMode.SingleRankProgression)
            return PlayerComboCounterMode.SingleRankProgression;

        return PlayerComboCounterMode.Ranks;
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

    /// <summary>
    /// Resolves the authored single-rank formula distribution mode with a safe enum fallback.
    /// </summary>
    /// <param name="formulaDistributionModeProperty">Serialized single-rank formula distribution mode.</param>
    /// <returns>Resolved formula distribution behavior.</returns>
    private static PlayerComboSingleRankFormulaDistributionMode ResolveSingleRankFormulaDistributionMode(SerializedProperty formulaDistributionModeProperty)
    {
        if (formulaDistributionModeProperty != null &&
            formulaDistributionModeProperty.propertyType == SerializedPropertyType.Enum &&
            formulaDistributionModeProperty.enumValueIndex == (int)PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression)
            return PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression;

        return PlayerComboSingleRankFormulaDistributionMode.MilestoneSteps;
    }

    /// <summary>
    /// Resolves the authored single-rank linear bonus interval with a safe enum fallback.
    /// </summary>
    /// <param name="linearBonusRangeModeProperty">Serialized single-rank linear bonus range mode.</param>
    /// <returns>Resolved linear bonus interval behavior.</returns>
    private static PlayerComboSingleRankLinearBonusRangeMode ResolveSingleRankLinearBonusRangeMode(SerializedProperty linearBonusRangeModeProperty)
    {
        if (linearBonusRangeModeProperty != null &&
            linearBonusRangeModeProperty.propertyType == SerializedPropertyType.Enum &&
            linearBonusRangeModeProperty.enumValueIndex == (int)PlayerComboSingleRankLinearBonusRangeMode.MilestoneToNextMilestone)
            return PlayerComboSingleRankLinearBonusRangeMode.MilestoneToNextMilestone;

        return PlayerComboSingleRankLinearBonusRangeMode.EntireProgression;
    }
    #endregion

    #endregion
}
