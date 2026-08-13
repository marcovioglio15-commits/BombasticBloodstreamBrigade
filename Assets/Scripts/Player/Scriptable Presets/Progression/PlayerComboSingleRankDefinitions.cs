using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Selects whether combo progression uses multiple authored ranks or one continuous rank with bonus milestones.
/// </summary>
public enum PlayerComboCounterMode : byte
{
    Ranks = 0,
    SingleRankProgression = 1
}

/// <summary>
/// Selects the numeric text format used by the single-rank combo presentation.
/// </summary>
public enum PlayerComboSingleRankValueDisplayMode : byte
{
    CurrentValue = 0,
    CurrentAndMaximum = 1
}

/// <summary>
/// Selects whether single-rank Character Tuning formulas activate at milestones or blend across the configured progression range.
/// </summary>
public enum PlayerComboSingleRankFormulaDistributionMode : byte
{
    MilestoneSteps = 0,
    LinearAcrossProgression = 1
}

/// <summary>
/// Selects the progression interval used to blend linear single-rank formulas.
/// </summary>
public enum PlayerComboSingleRankLinearBonusRangeMode : byte
{
    EntireProgression = 0,
    MilestoneToNextMilestone = 1
}

/// <summary>
/// Stores the continuous progression settings used when the combo counter exposes one visual rank.
/// </summary>
[Serializable]
public sealed class PlayerComboSingleRankDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Identity and Progression")]
    [Tooltip("Stable identifier shown for the single combo rank and exposed through the authoritative combo state.")]
    [SerializeField] private string rankId = "SYNCHRO";

    [Tooltip("Combo value that completes the single-rank progression bar and caps further combo gain.")]
    [SerializeField] private int maximumComboValue = 1500;

    [Tooltip("Combo points removed every second while the single rank contains points. Use 0 to disable time-based decay.")]
    [SerializeField] private float pointsDecayPerSecond;

    [Tooltip("Controls whether the HUD shows only the current combo value or the current value followed by the progression maximum.")]
    [SerializeField] private PlayerComboSingleRankValueDisplayMode valueDisplayMode = PlayerComboSingleRankValueDisplayMode.CurrentAndMaximum;

    [Tooltip("Controls whether Character Tuning formulas activate at their milestone percentages or blend linearly from zero to their full result across the configured progression range.")]
    [SerializeField] private PlayerComboSingleRankFormulaDistributionMode formulaDistributionMode = PlayerComboSingleRankFormulaDistributionMode.MilestoneSteps;

    [Tooltip("When Linear Across Progression is selected, controls whether all formulas share the complete rank interval or each milestone formula reaches full strength at the next enabled milestone.")]
    [SerializeField] private PlayerComboSingleRankLinearBonusRangeMode linearBonusRangeMode = PlayerComboSingleRankLinearBonusRangeMode.EntireProgression;

    [Tooltip("Keeps the Synchro Meter hidden until combo progress reaches the first enabled bonus milestone. Combo accumulation remains active while the meter is hidden.")]
    [SerializeField] private bool showMeterOnlyAfterFirstMilestone;

    [Tooltip("When Linear Across Progression is selected, keeps every linear Character Tuning bonus inactive until the first enabled milestone, then distributes it across the remaining progression.")]
    [SerializeField] private bool startLinearBonusesAtFirstMilestone;

    [Header("Rewards")]
    [Tooltip("Ordered percentage milestones that grant cumulative Character Tuning formulas and temporary passive power-ups inside the single rank.")]
    [SerializeField] private List<PlayerComboBonusMilestoneDefinition> bonusMilestones = new List<PlayerComboBonusMilestoneDefinition>();
    #endregion

    #endregion

    #region Properties
    public string RankId => rankId;
    public int MaximumComboValue => maximumComboValue;
    public float PointsDecayPerSecond => pointsDecayPerSecond;
    public PlayerComboSingleRankValueDisplayMode ValueDisplayMode => valueDisplayMode;
    public PlayerComboSingleRankFormulaDistributionMode FormulaDistributionMode => formulaDistributionMode;
    public PlayerComboSingleRankLinearBonusRangeMode LinearBonusRangeMode => linearBonusRangeMode;
    public bool ShowMeterOnlyAfterFirstMilestone => showMeterOnlyAfterFirstMilestone;
    public bool StartLinearBonusesAtFirstMilestone => startLinearBonusesAtFirstMilestone;
    public IReadOnlyList<PlayerComboBonusMilestoneDefinition> BonusMilestones => bonusMilestones;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the complete authored single-rank progression definition used by preset creation and deterministic tests.
    /// </summary>
    /// <param name="rankIdValue">Stable visual rank identifier.</param>
    /// <param name="maximumComboValueValue">Combo value that completes and caps progression.</param>
    /// <param name="pointsDecayPerSecondValue">Continuous combo point loss per second.</param>
    /// <param name="valueDisplayModeValue">Numeric HUD text format.</param>
    /// <param name="formulaDistributionModeValue">Character Tuning formula distribution behavior.</param>
    /// <param name="bonusMilestonesValue">Ordered percentage milestone list.</param>
    /// <param name="showMeterOnlyAfterFirstMilestoneValue">Whether presentation waits for the first enabled milestone.</param>
    /// <param name="startLinearBonusesAtFirstMilestoneValue">Whether linear formulas start from the first enabled milestone.</param>
    /// <param name="linearBonusRangeModeValue">Progression interval used by linear formulas.</param>
    public void Configure(string rankIdValue,
                          int maximumComboValueValue,
                          float pointsDecayPerSecondValue,
                          PlayerComboSingleRankValueDisplayMode valueDisplayModeValue,
                          PlayerComboSingleRankFormulaDistributionMode formulaDistributionModeValue,
                          List<PlayerComboBonusMilestoneDefinition> bonusMilestonesValue,
                          bool showMeterOnlyAfterFirstMilestoneValue = false,
                          bool startLinearBonusesAtFirstMilestoneValue = false,
                          PlayerComboSingleRankLinearBonusRangeMode linearBonusRangeModeValue = PlayerComboSingleRankLinearBonusRangeMode.EntireProgression)
    {
        rankId = rankIdValue;
        maximumComboValue = maximumComboValueValue;
        pointsDecayPerSecond = pointsDecayPerSecondValue;
        valueDisplayMode = valueDisplayModeValue;
        formulaDistributionMode = formulaDistributionModeValue;
        bonusMilestones = bonusMilestonesValue;
        showMeterOnlyAfterFirstMilestone = showMeterOnlyAfterFirstMilestoneValue;
        startLinearBonusesAtFirstMilestone = startLinearBonusesAtFirstMilestoneValue;
        linearBonusRangeMode = linearBonusRangeModeValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Restores missing nested milestone data and removes identifier whitespace without snapping authored gameplay values.
    /// </summary>
    public void Validate()
    {
        if (rankId == null)
            rankId = string.Empty;

        rankId = rankId.Trim();

        if (bonusMilestones == null)
            bonusMilestones = new List<PlayerComboBonusMilestoneDefinition>();

        for (int milestoneIndex = 0; milestoneIndex < bonusMilestones.Count; milestoneIndex++)
        {
            PlayerComboBonusMilestoneDefinition milestone = bonusMilestones[milestoneIndex];

            if (milestone == null)
            {
                milestone = new PlayerComboBonusMilestoneDefinition();
                bonusMilestones[milestoneIndex] = milestone;
            }

            milestone.Validate();
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one percentage reward milestone inside a continuous single-rank combo progression.
/// </summary>
[Serializable]
public sealed class PlayerComboBonusMilestoneDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Milestone")]
    [Tooltip("Stable milestone identifier used by Add Scaling keys and runtime diagnostics without changing the visual single-rank label.")]
    [SerializeField] private string milestoneId = "Milestone01";

    [Tooltip("Enables this reward milestone while preserving its authored data for formula-driven mode changes.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Percentage of the complete single-rank progression required to activate this milestone and its temporary rewards.")]
    [SerializeField] private float requiredProgressPercent = 25f;

    [Header("Rewards")]
    [Tooltip("Ordered Character Tuning formulas granted by this milestone. Numeric formulas can optionally blend across the complete single-rank progression.")]
    [SerializeField] private PowerUpCharacterTuningModuleData bonuses = new PowerUpCharacterTuningModuleData();

    [Tooltip("Passive power-ups granted after this milestone is reached and removed when combo progression falls below it or resets.")]
    [SerializeField] private List<PlayerComboPassivePowerUpUnlockDefinition> passivePowerUpUnlocks = new List<PlayerComboPassivePowerUpUnlockDefinition>();
    #endregion

    #endregion

    #region Properties
    public string MilestoneId => milestoneId;
    public bool IsEnabled => isEnabled;
    public float RequiredProgressPercent => requiredProgressPercent;
    public PowerUpCharacterTuningModuleData Bonuses => bonuses;
    public IReadOnlyList<PlayerComboPassivePowerUpUnlockDefinition> PassivePowerUpUnlocks => passivePowerUpUnlocks;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns one single-rank bonus milestone and its temporary reward payloads.
    /// </summary>
    /// <param name="milestoneIdValue">Stable milestone identifier.</param>
    /// <param name="isEnabledValue">Whether the milestone participates in runtime progression.</param>
    /// <param name="requiredProgressPercentValue">Percentage required to activate the milestone.</param>
    /// <param name="bonusesValue">Character Tuning formulas owned by the milestone.</param>
    /// <param name="passivePowerUpUnlocksValue">Temporary passive unlocks owned by the milestone.</param>
    public void Configure(string milestoneIdValue,
                          bool isEnabledValue,
                          float requiredProgressPercentValue,
                          PowerUpCharacterTuningModuleData bonusesValue,
                          List<PlayerComboPassivePowerUpUnlockDefinition> passivePowerUpUnlocksValue)
    {
        milestoneId = milestoneIdValue;
        isEnabled = isEnabledValue;
        requiredProgressPercent = requiredProgressPercentValue;
        bonuses = bonusesValue;
        passivePowerUpUnlocks = passivePowerUpUnlocksValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Restores missing reward payloads and removes identifier whitespace without changing authored percentages.
    /// </summary>
    public void Validate()
    {
        if (milestoneId == null)
            milestoneId = string.Empty;

        milestoneId = milestoneId.Trim();

        if (bonuses == null)
            bonuses = new PowerUpCharacterTuningModuleData();

        if (passivePowerUpUnlocks == null)
            passivePowerUpUnlocks = new List<PlayerComboPassivePowerUpUnlockDefinition>();

        for (int unlockIndex = 0; unlockIndex < passivePowerUpUnlocks.Count; unlockIndex++)
        {
            PlayerComboPassivePowerUpUnlockDefinition passiveUnlock = passivePowerUpUnlocks[unlockIndex];

            if (passiveUnlock == null)
            {
                passiveUnlock = new PlayerComboPassivePowerUpUnlockDefinition();
                passivePowerUpUnlocks[unlockIndex] = passiveUnlock;
            }

            passiveUnlock.Validate();
        }

        bonuses.Validate();
    }
    #endregion

    #endregion
}
