using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Describes how the combo reacts after a damage event configured to break it.
/// none.
/// </summary>
public enum PlayerComboDamageBreakMode : byte
{
    ResetCombo = 0,
    DowngradeToPreviousRank = 1
}

/// <summary>
/// Stores authored combo-counter rules used by progression presets to grant temporary rank-based bonuses.
/// none.
/// </summary>
[Serializable]
public sealed class PlayerComboCounterDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Runtime")]
    [Tooltip("Enables combo accumulation, rank evaluation, and temporary combo bonuses for this progression preset.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Amount added to the combo counter every time one enemy is killed while the combo remains unbroken.")]
    [SerializeField] private int comboGainPerKill = 1;

    [Tooltip("Controls whether valid damage breaks the combo entirely or only drops it to the previous reached rank threshold.")]
    [SerializeField] private PlayerComboDamageBreakMode damageBreakMode = PlayerComboDamageBreakMode.ResetCombo;

    [Tooltip("When enabled, shield-only damage also breaks the current combo. Health damage always breaks it.")]
    [SerializeField] private bool shieldDamageBreaksCombo;

    [Tooltip("When enabled, rank point decay stops on the current rank threshold before it would fall into a lower rank that has no point decay.")]
    [SerializeField] private bool preventDecayIntoNonDecayingRanks;

    [Header("Ranks")]
    [Tooltip("Ordered rank milestones used to resolve the active combo rank and its temporary Character Tuning bonuses.")]
    [SerializeField] private List<PlayerComboRankDefinition> rankDefinitions = new List<PlayerComboRankDefinition>();
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled
    {
        get
        {
            return isEnabled;
        }
    }

    public int ComboGainPerKill
    {
        get
        {
            return comboGainPerKill;
        }
    }

    public bool ShieldDamageBreaksCombo
    {
        get
        {
            return shieldDamageBreaksCombo;
        }
    }

    public PlayerComboDamageBreakMode DamageBreakMode
    {
        get
        {
            return damageBreakMode;
        }
    }

    public bool PreventDecayIntoNonDecayingRanks
    {
        get
        {
            return preventDecayIntoNonDecayingRanks;
        }
    }

    public IReadOnlyList<PlayerComboRankDefinition> RankDefinitions
    {
        get
        {
            return rankDefinitions;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the authored combo runtime rules and rank list.
    /// </summary>
    /// <param name="isEnabledValue">Enables or disables the combo system for the owning preset.</param>
    /// <param name="comboGainPerKillValue">Amount added for every valid enemy kill.</param>
    /// <param name="damageBreakModeValue">Controls whether damage resets the combo entirely or downgrades it to the previous rank.</param>
    /// <param name="shieldDamageBreaksComboValue">True when shield-only damage should also interrupt the combo.</param>
    /// <param name="preventDecayIntoNonDecayingRanksValue">True when point decay should preserve the current rank before falling into a no-decay lower rank.</param>
    /// <param name="rankDefinitionsValue">Ordered rank list stored by this combo definition.</param>
    public void Configure(bool isEnabledValue,
                          int comboGainPerKillValue,
                          PlayerComboDamageBreakMode damageBreakModeValue,
                          bool shieldDamageBreaksComboValue,
                          bool preventDecayIntoNonDecayingRanksValue,
                          List<PlayerComboRankDefinition> rankDefinitionsValue)
    {
        isEnabled = isEnabledValue;
        comboGainPerKill = comboGainPerKillValue;
        damageBreakMode = damageBreakModeValue;
        shieldDamageBreaksCombo = shieldDamageBreaksComboValue;
        preventDecayIntoNonDecayingRanks = preventDecayIntoNonDecayingRanksValue;
        rankDefinitions = rankDefinitionsValue;
    }

    /// <summary>
    /// Assigns the authored combo runtime rules and rank list while keeping decay-floor preservation disabled for older call sites.
    /// </summary>
    /// <param name="isEnabledValue">Enables or disables the combo system for the owning preset.</param>
    /// <param name="comboGainPerKillValue">Amount added for every valid enemy kill.</param>
    /// <param name="damageBreakModeValue">Controls whether damage resets the combo entirely or downgrades it to the previous rank.</param>
    /// <param name="shieldDamageBreaksComboValue">True when shield-only damage should also interrupt the combo.</param>
    /// <param name="rankDefinitionsValue">Ordered rank list stored by this combo definition.</param>
    public void Configure(bool isEnabledValue,
                          int comboGainPerKillValue,
                          PlayerComboDamageBreakMode damageBreakModeValue,
                          bool shieldDamageBreaksComboValue,
                          List<PlayerComboRankDefinition> rankDefinitionsValue)
    {
        Configure(isEnabledValue,
                  comboGainPerKillValue,
                  damageBreakModeValue,
                  shieldDamageBreaksComboValue,
                  false,
                  rankDefinitionsValue);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures nested collections exist and normalizes nested combo ranks without snapping authored numeric values.
    /// </summary>
    public void Validate()
    {
        if (rankDefinitions == null)
        {
            rankDefinitions = new List<PlayerComboRankDefinition>();
        }

        for (int rankIndex = 0; rankIndex < rankDefinitions.Count; rankIndex++)
        {
            PlayerComboRankDefinition rankDefinition = rankDefinitions[rankIndex];

            if (rankDefinition == null)
            {
                rankDefinition = new PlayerComboRankDefinition();
                rankDefinitions[rankIndex] = rankDefinition;
            }

            rankDefinition.Validate();
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one temporary passive power-up acquisition granted while its combo rank remains reached.
/// none.
/// </summary>
[Serializable]
public sealed class PlayerComboPassivePowerUpUnlockDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables this temporary passive power-up unlock while the owning combo rank remains reached.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Passive PowerUpId granted while the owning combo rank remains reached. The ID is resolved against the scoped Power-Ups preset runtime catalog and is revoked on derank or combo reset.")]
    [SerializeField] private string passivePowerUpId = string.Empty;
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled
    {
        get
        {
            return isEnabled;
        }
    }

    public string PassivePowerUpId
    {
        get
        {
            return passivePowerUpId;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the passive unlock entry data used by temporary combo rank rewards.
    /// </summary>
    /// <param name="isEnabledValue">True when the unlock should be processed while the owning rank is active.</param>
    /// <param name="passivePowerUpIdValue">Passive PowerUpId resolved against the runtime unlock catalog.</param>
    public void Configure(bool isEnabledValue, string passivePowerUpIdValue)
    {
        isEnabled = isEnabledValue;
        passivePowerUpId = passivePowerUpIdValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Trims the authored passive PowerUpId while preserving authored enablement.
    /// </summary>
    public void Validate()
    {
        if (passivePowerUpId == null)
        {
            passivePowerUpId = string.Empty;
        }

        passivePowerUpId = passivePowerUpId.Trim();
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one combo rank milestone with its display identifier, HUD presentation overrides, time-based point decay, and temporary Character Tuning bonus formulas.
/// none.
/// </summary>
[Serializable]
public sealed class PlayerComboRankDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable combo-rank identifier used for Add Scaling stat keys and for the runtime rank label exposed by the combo state.")]
    [SerializeField] private string rankId = "Rank01";

    [Tooltip("Minimum combo value required to activate this rank and its temporary bonuses.")]
    [SerializeField] private int requiredComboValue = 10;

    [Tooltip("Points removed from the combo every second while this rank is active. Values above 0 can naturally cause rank retrocession over time.")]
    [SerializeField] private float pointsDecayPerSecond;

    [Tooltip("Percentage of this rank's numeric Character Tuning boost distributed linearly while progressing from the previous rank threshold to this rank threshold.")]
    [SerializeField] private float progressiveBoostPercent;

    [Tooltip("Ordered Character Tuning formulas applied while this rank remains active. Active combo ranks stack cumulatively in ascending milestone order.")]
    [SerializeField] private PowerUpCharacterTuningModuleData rankBonuses = new PowerUpCharacterTuningModuleData();

    [Tooltip("Passive power-ups granted while this combo rank remains reached and removed when the combo deranks below this rank or resets.")]
    [SerializeField] private List<PlayerComboPassivePowerUpUnlockDefinition> passivePowerUpUnlocks = new List<PlayerComboPassivePowerUpUnlockDefinition>();
    #endregion

    #endregion

    #region Properties
    public string RankId
    {
        get
        {
            return rankId;
        }
    }

    public int RequiredComboValue
    {
        get
        {
            return requiredComboValue;
        }
    }

    public PowerUpCharacterTuningModuleData RankBonuses
    {
        get
        {
            return rankBonuses;
        }
    }

    public float PointsDecayPerSecond
    {
        get
        {
            return pointsDecayPerSecond;
        }
    }

    public float ProgressiveBoostPercent
    {
        get
        {
            return progressiveBoostPercent;
        }
    }

    public IReadOnlyList<PlayerComboPassivePowerUpUnlockDefinition> PassivePowerUpUnlocks
    {
        get
        {
            return passivePowerUpUnlocks;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the authored combo-rank identity, milestone threshold, point-decay rate, and temporary Character Tuning bonuses.
    /// </summary>
    /// <param name="rankIdValue">Stable rank identifier shown by the runtime combo label.</param>
    /// <param name="requiredComboValueValue">Minimum combo value required by this rank.</param>
    /// <param name="pointsDecayPerSecondValue">Combo points removed per second while this rank is active.</param>
    /// <param name="progressiveBoostPercentValue">Percent of numeric rank bonus distributed before this rank is reached.</param>
    /// <param name="rankBonusesValue">Character Tuning formulas applied while the rank is active.</param>
    /// <param name="passivePowerUpUnlocksValue">Passive power-ups granted while this rank remains reached.</param>
    public void Configure(string rankIdValue,
                          int requiredComboValueValue,
                          float pointsDecayPerSecondValue,
                          float progressiveBoostPercentValue,
                          PowerUpCharacterTuningModuleData rankBonusesValue,
                          List<PlayerComboPassivePowerUpUnlockDefinition> passivePowerUpUnlocksValue)
    {
        rankId = rankIdValue;
        requiredComboValue = requiredComboValueValue;
        pointsDecayPerSecond = pointsDecayPerSecondValue;
        progressiveBoostPercent = progressiveBoostPercentValue;
        rankBonuses = rankBonusesValue;
        passivePowerUpUnlocks = passivePowerUpUnlocksValue;
    }

    /// <summary>
    /// Assigns the authored combo-rank identity, milestone threshold, point-decay rate, and temporary Character Tuning bonuses while preserving passive unlocks.
    /// </summary>
    /// <param name="rankIdValue">Stable rank identifier shown by the runtime combo label.</param>
    /// <param name="requiredComboValueValue">Minimum combo value required by this rank.</param>
    /// <param name="pointsDecayPerSecondValue">Combo points removed per second while this rank is active.</param>
    /// <param name="rankBonusesValue">Character Tuning formulas applied while the rank is active.</param>
    public void Configure(string rankIdValue,
                          int requiredComboValueValue,
                          float pointsDecayPerSecondValue,
                          PowerUpCharacterTuningModuleData rankBonusesValue)
    {
        Configure(rankIdValue,
                  requiredComboValueValue,
                  pointsDecayPerSecondValue,
                  progressiveBoostPercent,
                  rankBonusesValue,
                  passivePowerUpUnlocks);
    }

    /// <summary>
    /// Assigns the authored combo-rank identity, milestone threshold, and temporary Character Tuning bonuses while preserving point-decay rate.
    /// </summary>
    /// <param name="rankIdValue">Stable rank identifier shown by the runtime combo label.</param>
    /// <param name="requiredComboValueValue">Minimum combo value required by this rank.</param>
    /// <param name="rankBonusesValue">Character Tuning formulas applied while the rank is active.</param>
    public void Configure(string rankIdValue, int requiredComboValueValue, PowerUpCharacterTuningModuleData rankBonusesValue)
    {
        Configure(rankIdValue,
                  requiredComboValueValue,
                  pointsDecayPerSecond,
                  rankBonusesValue);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures nested Character Tuning payloads exist and trims identifier serialization noise without snapping numeric values.
    /// </summary>
    public void Validate()
    {
        if (rankId == null)
        {
            rankId = string.Empty;
        }

        rankId = rankId.Trim();

        if (rankBonuses == null)
        {
            rankBonuses = new PowerUpCharacterTuningModuleData();
        }

        if (passivePowerUpUnlocks == null)
        {
            passivePowerUpUnlocks = new List<PlayerComboPassivePowerUpUnlockDefinition>();
        }

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

        rankBonuses.Validate();
    }
    #endregion

    #endregion
}
