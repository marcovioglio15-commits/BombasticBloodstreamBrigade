using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PowerUpCommonData
{
    #region Fields

    #region Serialized Fields
    [Header("Identity")]
    [Tooltip("Stable identifier for this power up entry.")]
    [SerializeField] private string powerUpId;

    [Tooltip("Display name shown to players (WIP).")]
    [SerializeField] private string displayName = "New Power Up";

    [Tooltip("Description shown in tooltips and codex entries(WIP).")]
    [SerializeField] private string description;

    [Header("Presentation")]
    [Tooltip("Optional sprite icon shown in milestone offers, prompts, and dropped power-up containers.")]
    [SerializeField] private Sprite icon;

    [Header("Drop")]
    [Tooltip("Drop pools where this power up can appear(WIP).")]
    [SerializeField] private List<string> dropPools = new List<string>();

    [Tooltip("Rarity tier for this power up. Range: 1 to 5(WIP).")]
    [SerializeField] private int dropTier = 1;

    [Tooltip("Shop purchase cost associated with this power up(WIP).")]
    [SerializeField] private int purchaseCost;
    #endregion

    #endregion

    #region Properties
    public string PowerUpId
    {
        get
        {
            return powerUpId;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public Sprite Icon
    {
        get
        {
            return icon;
        }
    }

    public IReadOnlyList<string> DropPools
    {
        get
        {
            return dropPools;
        }
    }

    public int DropTier
    {
        get
        {
            return dropTier;
        }
    }

    public int PurchaseCost
    {
        get
        {
            return purchaseCost;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(string powerUpIdValue,
                          string displayNameValue,
                          string descriptionValue,
                          Sprite iconValue,
                          List<string> dropPoolsValue,
                          int dropTierValue,
                          int purchaseCostValue)
    {
        powerUpId = powerUpIdValue;
        displayName = displayNameValue;
        description = descriptionValue;
        icon = iconValue;
        dropPools = dropPoolsValue;
        dropTier = dropTierValue;
        purchaseCost = purchaseCostValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(powerUpId))
            powerUpId = Guid.NewGuid().ToString("N");

        if (dropPools == null)
            dropPools = new List<string>();

    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PassiveStatModifier
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Target stat modified by this passive effect.")]
    [SerializeField] private PassiveStatType statType = PassiveStatType.MaxHealth;

    [Tooltip("Operation used to apply Value to the target stat.")]
    [SerializeField] private PassiveStatOperation operation = PassiveStatOperation.Add;

    [Tooltip("Modifier value applied to the selected stat.")]
    [SerializeField] private float value = 1f;
    #endregion

    #endregion

    #region Properties
    public PassiveStatType StatType
    {
        get
        {
            return statType;
        }
    }

    public PassiveStatOperation Operation
    {
        get
        {
            return operation;
        }
    }

    public float Value
    {
        get
        {
            return value;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Keeps the payload callable from shared validation paths without snapping authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PassiveModifierDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Common metadata and drop data for this passive modifier.")]
    [SerializeField] private PowerUpCommonData commonData = new PowerUpCommonData();

    [Tooltip("Passive modifier behavior category.")]
    [SerializeField] private PassiveModifierKind modifierKind = PassiveModifierKind.StatModifier;

    [Tooltip("List of stat modifiers applied by this passive modifier.")]
    [SerializeField] private List<PassiveStatModifier> statModifiers = new List<PassiveStatModifier>();
    #endregion

    #endregion

    #region Properties
    public PowerUpCommonData CommonData
    {
        get
        {
            return commonData;
        }
    }

    public PassiveModifierKind ModifierKind
    {
        get
        {
            return modifierKind;
        }
    }

    public IReadOnlyList<PassiveStatModifier> StatModifiers
    {
        get
        {
            return statModifiers;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (commonData == null)
            commonData = new PowerUpCommonData();

        commonData.Validate();

        if (statModifiers == null)
            statModifiers = new List<PassiveStatModifier>();

        for (int index = 0; index < statModifiers.Count; index++)
        {
            PassiveStatModifier modifier = statModifiers[index];

            if (modifier == null)
                continue;

            modifier.Validate();
        }
    }
    #endregion

    #endregion
}
