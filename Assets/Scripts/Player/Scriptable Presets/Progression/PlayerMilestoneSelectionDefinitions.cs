using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Identifies which runtime resource is granted when a milestone power-up selection is skipped.
/// </summary>
public enum PlayerMilestoneSkipCompensationResourceType
{
    Health = 0,
    Shield = 1,
    PrimaryActiveEnergy = 2,
    SecondaryActiveEnergy = 3,
    Experience = 4
}

/// <summary>
/// Defines how a milestone skip-compensation value is interpreted.
/// </summary>
public enum PlayerMilestoneCompensationApplyMode
{
    Flat = 0,
    Percent = 1
}

/// <summary>
/// Defines one milestone power-up roll by referencing one drop pool from the scoped power-ups preset.
/// </summary>
[Serializable]
public sealed class PlayerMilestonePowerUpUnlockDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Drop pool ID selected from the scoped Power-Ups preset and used to resolve this specific milestone power-up extraction.")]
    [SerializeField] private string dropPoolId;

    [Tooltip("Legacy tier-roll data preserved for backward-compatible migration from inline milestone tier definitions.")]
    [FormerlySerializedAs("tierRolls")]
    [HideInInspector]
    [SerializeField] private List<PlayerMilestoneTierRollDefinition> legacyTierRolls = new List<PlayerMilestoneTierRollDefinition>();
    #endregion

    #endregion

    #region Properties
    public string DropPoolId
    {
        get
        {
            return dropPoolId;
        }
    }

    public IReadOnlyList<PlayerMilestoneTierRollDefinition> LegacyTierRolls
    {
        get
        {
            return legacyTierRolls;
        }
    }

    public bool HasLegacyTierRolls
    {
        get
        {
            return legacyTierRolls != null && legacyTierRolls.Count > 0;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns the selected drop pool and optional legacy tier-roll fallback after migration or duplication logic.
    /// </summary>
    /// <param name="dropPoolIdValue">Drop pool ID resolved by editor selection logic.</param>
    /// <param name="legacyTierRollsValue">Optional legacy tier-roll list preserved for backward compatibility.</param>

    public void Configure(string dropPoolIdValue, List<PlayerMilestoneTierRollDefinition> legacyTierRollsValue = null)
    {
        dropPoolId = dropPoolIdValue;
        legacyTierRolls = legacyTierRollsValue;
    }

    /// <summary>
    /// Sanitizes the selected pool ID and keeps legacy tier-roll data valid for backward-compatible baking fallback.
    /// </summary>

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(dropPoolId))
            dropPoolId = string.Empty;
        else
            dropPoolId = dropPoolId.Trim();

        if (legacyTierRolls == null)
            legacyTierRolls = new List<PlayerMilestoneTierRollDefinition>();

        for (int tierRollIndex = 0; tierRollIndex < legacyTierRolls.Count; tierRollIndex++)
        {
            PlayerMilestoneTierRollDefinition tierRoll = legacyTierRolls[tierRollIndex];

            if (tierRoll != null)
                continue;

            tierRoll = new PlayerMilestoneTierRollDefinition();
            legacyTierRolls[tierRollIndex] = tierRoll;
        }

        for (int tierRollIndex = 0; tierRollIndex < legacyTierRolls.Count; tierRollIndex++)
            legacyTierRolls[tierRollIndex].Validate(string.Empty);
    }

    /// <summary>
    /// Builds a deep-cloned list of tier-roll definitions for migration from legacy milestone data.
    /// </summary>
    /// <param name="sourceTierRolls">Source tier-roll list to clone.</param>
    /// <returns>Cloned tier-roll definitions, never null.</returns>
    public static List<PlayerMilestoneTierRollDefinition> CloneTierRolls(IReadOnlyList<PlayerMilestoneTierRollDefinition> sourceTierRolls)
    {
        List<PlayerMilestoneTierRollDefinition> clonedTierRolls = new List<PlayerMilestoneTierRollDefinition>();

        if (sourceTierRolls == null)
            return clonedTierRolls;

        for (int tierRollIndex = 0; tierRollIndex < sourceTierRolls.Count; tierRollIndex++)
        {
            PlayerMilestoneTierRollDefinition sourceTierRoll = sourceTierRolls[tierRollIndex];
            PlayerMilestoneTierRollDefinition clonedTierRoll = new PlayerMilestoneTierRollDefinition();

            if (sourceTierRoll != null)
                clonedTierRoll.Configure(sourceTierRoll.TierId, sourceTierRoll.SelectionPercentage);

            clonedTierRolls.Add(clonedTierRoll);
        }

        return clonedTierRolls;
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines one resource compensation granted when the player skips a milestone power-up selection.
/// </summary>
[Serializable]
public sealed class PlayerMilestoneSkipCompensationDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Runtime resource granted when the player presses Skip on this milestone.")]
    [SerializeField] private PlayerMilestoneSkipCompensationResourceType resourceType = PlayerMilestoneSkipCompensationResourceType.Health;

    [Tooltip("How Value is interpreted for the selected resource.")]
    [SerializeField] private PlayerMilestoneCompensationApplyMode applyMode = PlayerMilestoneCompensationApplyMode.Flat;

    [Tooltip("Flat amount or percentage granted to the selected resource when this milestone is skipped.")]
    [SerializeField] private float value;
    #endregion

    #endregion

    #region Properties
    public PlayerMilestoneSkipCompensationResourceType ResourceType
    {
        get
        {
            return resourceType;
        }
    }

    public PlayerMilestoneCompensationApplyMode ApplyMode
    {
        get
        {
            return applyMode;
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

    #region Public Methods
    /// <summary>
    /// Assigns skip-compensation values after editor duplication or migration logic.
    /// </summary>
    /// <param name="resourceTypeValue">Resource modified by this compensation entry.</param>
    /// <param name="applyModeValue">Value interpretation mode.</param>
    /// <param name="valueValue">Flat amount or percentage value.</param>

    public void Configure(PlayerMilestoneSkipCompensationResourceType resourceTypeValue,
                          PlayerMilestoneCompensationApplyMode applyModeValue,
                          float valueValue)
    {
        resourceType = resourceTypeValue;
        applyMode = applyModeValue;
        value = valueValue;
    }

    /// <summary>
    /// Sanitizes the serialized compensation value to keep runtime application predictable.
    /// </summary>

    public void Validate()
    {
        if (value < 0f)
            value = 0f;
    }
    #endregion

    #endregion
}
