using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Selects how one conditional weapon-switch condition contributes to the entry verdict. Each entry resolves
/// to a match when every Necessary condition is true, every Necessary And Sufficient condition is true, and at
/// least one Sufficient or Necessary And Sufficient condition is true (or no sufficient class exists). The
/// requirement type is read at runtime by the conditional weapon switch evaluator.
/// </summary>
public enum PlayerConditionalWeaponSwitchConditionRequirement : byte
{
    Sufficient = 0,
    Necessary = 1,
    NecessaryAndSufficient = 2
}

/// <summary>
/// Stores one inclusive numeric range gate built against a scalable stat declared in the Level-Up & Progression
/// preset. Both numeric and boolean stats are supported: booleans project to zero or one before the comparison.
/// Token-typed stats are not supported and surface a warning in the tool because there is no obvious ordering.
/// </summary>
[Serializable]
public sealed class PlayerConditionalWeaponSwitchCondition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Scalable stat name declared in the Level-Up & Progression preset. Must reference a numeric or boolean stat.")]
    [SerializeField] private string statName = string.Empty;

    [Tooltip("Inclusive minimum value the scalable stat must satisfy for this condition to evaluate to true.")]
    [SerializeField] private float minimumValue;

    [Tooltip("Inclusive maximum value the scalable stat must satisfy for this condition to evaluate to true.")]
    [SerializeField] private float maximumValue = 1f;

    [Tooltip("Requirement class used by the entry aggregation: Sufficient means any-match, Necessary means must-be-true, Necessary And Sufficient is both.")]
    [SerializeField] private PlayerConditionalWeaponSwitchConditionRequirement requirement = PlayerConditionalWeaponSwitchConditionRequirement.NecessaryAndSufficient;
    #endregion

    #endregion

    #region Properties
    public string StatName
    {
        get
        {
            return statName;
        }
    }

    public float MinimumValue
    {
        get
        {
            return minimumValue;
        }
    }

    public float MaximumValue
    {
        get
        {
            return maximumValue;
        }
    }

    public PlayerConditionalWeaponSwitchConditionRequirement Requirement
    {
        get
        {
            return requirement;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Pre-populates the condition with a fresh stat reference and default inclusive range. Used by the tool
    /// when s append a new condition slot via the Add button.
    /// </summary>
    /// <param name="statNameValue">Scalable stat name to bind initially.</param>
    public void Configure(string statNameValue)
    {
        statName = statNameValue ?? string.Empty;
        minimumValue = 0f;
        maximumValue = 1f;
        requirement = PlayerConditionalWeaponSwitchConditionRequirement.NecessaryAndSufficient;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Preserves authored values so the tool can surface incoherent ranges as non-destructive warnings instead
    /// of silently snapping them back to safe defaults.
    /// </summary>
    public void Validate()
    {
        // Authored bounds are intentionally preserved; the panel reports inverted or non-finite ranges separately.
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one conditional weapon-switch entry. Each entry binds a target Weapon Id to a list of conditions
/// evaluated against the current scalable stats. The Priority field breaks ties between simultaneously matching
/// entries and the Override Power Up Switch flag elevates one entry above the equipped Switch Weapon module so
/// the conditional pipeline can override the power-up selection when s explicitly opt in.
/// </summary>
[Serializable]
public sealed class PlayerConditionalWeaponSwitchEntry
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Mountable Weapon Id selected when this entry wins. Must match one entry on the resolved Player Visual Preset.")]
    [SerializeField] private string weaponId = string.Empty;

    [Tooltip("Priority used to resolve conflicts between simultaneously matching entries. Higher priority wins.")]
    [SerializeField] private int priority;

    [Tooltip("When enabled, this entry wins against the active Switch Weapon power-up. When disabled, the power-up keeps priority.")]
    [SerializeField] private bool overridePowerUpSwitch;

    [Tooltip("Conditions evaluated against the current scalable stats to decide whether this entry is a candidate.")]
    [SerializeField] private List<PlayerConditionalWeaponSwitchCondition> conditions = new List<PlayerConditionalWeaponSwitchCondition>();
    #endregion

    #endregion

    #region Properties
    public string WeaponId
    {
        get
        {
            return weaponId;
        }
    }

    public int Priority
    {
        get
        {
            return priority;
        }
    }

    public bool OverridePowerUpSwitch
    {
        get
        {
            return overridePowerUpSwitch;
        }
    }

    public IReadOnlyList<PlayerConditionalWeaponSwitchCondition> Conditions
    {
        get
        {
            return conditions;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Pre-populates a freshly created entry with the supplied Weapon Id and an empty condition list. Used by
    /// the tool when s append a new entry slot via the Add button.
    /// </summary>
    /// <param name="weaponIdValue">Initial Weapon Id pre-selected on the new entry.</param>
    public void Configure(string weaponIdValue)
    {
        weaponId = weaponIdValue ?? string.Empty;
        priority = 0;
        overridePowerUpSwitch = false;

        if (conditions == null)
            conditions = new List<PlayerConditionalWeaponSwitchCondition>();
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures the condition list is allocated and forwards validation to every authored condition so 
    /// edits never produce null payloads at bake time.
    /// </summary>
    public void Validate()
    {
        if (conditions == null)
            conditions = new List<PlayerConditionalWeaponSwitchCondition>();

        for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
        {
            PlayerConditionalWeaponSwitchCondition condition = conditions[conditionIndex];

            if (condition == null)
            {
                condition = new PlayerConditionalWeaponSwitchCondition();
                condition.Configure(string.Empty);
                conditions[conditionIndex] = condition;
                continue;
            }

            condition.Validate();
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the conditional weapon-switch container nested under the shooting Values block. The ordered entry
/// list is preserved as the tie-breaker after Priority so s can predict resolution in dense rule sets.
/// </summary>
[Serializable]
public sealed class PlayerConditionalWeaponSwitchSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Ordered conditional weapon-switch entries. Each entry binds one Weapon Id to a list of scalable-stat range conditions and a priority.")]
    [SerializeField] private List<PlayerConditionalWeaponSwitchEntry> entries = new List<PlayerConditionalWeaponSwitchEntry>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<PlayerConditionalWeaponSwitchEntry> Entries
    {
        get
        {
            return entries;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures the entry list is allocated and forwards validation to every authored entry so the bake utility
    /// never iterates a null payload.
    /// </summary>
    public void Validate()
    {
        if (entries == null)
            entries = new List<PlayerConditionalWeaponSwitchEntry>();

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            PlayerConditionalWeaponSwitchEntry entry = entries[entryIndex];

            if (entry == null)
            {
                entry = new PlayerConditionalWeaponSwitchEntry();
                entry.Configure(string.Empty);
                entries[entryIndex] = entry;
                continue;
            }

            entry.Validate();
        }
    }
    #endregion

    #endregion
}
