using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines one weighted tier candidate available inside a power-up drop pool.
/// </summary>
[Serializable]
public sealed class PowerUpDropPoolTierDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Tier ID selected from the Power-Up tier catalog.")]
    [SerializeField] private string tierId;

    [Tooltip("Selection percentage used when this drop pool rolls among its available tiers. The total should be 100%.")]
    [SerializeField] private float selectionPercentage = 100f;
    #endregion

    #endregion

    #region Properties
    public string TierId
    {
        get
        {
            return tierId;
        }
    }

    public float SelectionPercentage
    {
        get
        {
            return selectionPercentage;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns one serialized drop-pool tier candidate after editor creation or migration logic.
    /// </summary>
    /// <param name="tierIdValue">Tier ID referenced by this pool candidate.</param>
    /// <param name="selectionPercentageValue">Selection percentage assigned inside the pool.</param>

    public void Configure(string tierIdValue, float selectionPercentageValue)
    {
        tierId = tierIdValue;
        selectionPercentage = selectionPercentageValue;
    }

    /// <summary>
    /// Normalizes the serialized candidate values to keep pool resolution deterministic.
    /// </summary>
    /// <param name="fallbackTierId">Fallback tier ID used when the current value is empty.</param>

    public void Validate(string fallbackTierId)
    {
        if (string.IsNullOrWhiteSpace(tierId))
            tierId = string.IsNullOrWhiteSpace(fallbackTierId) ? string.Empty : fallbackTierId.Trim();
        else
            tierId = tierId.Trim();

    }
    #endregion

    #endregion
}

/// <summary>
/// Defines one named drop pool used by milestone power-up unlocks to roll weighted tiers.
/// </summary>
[Serializable]
public sealed class PowerUpDropPoolDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable pool ID referenced by progression milestone power-up unlock definitions.")]
    [SerializeField] private string poolId;

    [Tooltip("Percentage-based tier candidates available when this pool is selected by a milestone roll.")]
    [SerializeField] private List<PowerUpDropPoolTierDefinition> tierRolls = new List<PowerUpDropPoolTierDefinition>();
    #endregion

    #endregion

    #region Properties
    public string PoolId
    {
        get
        {
            return poolId;
        }
    }

    public IReadOnlyList<PowerUpDropPoolTierDefinition> TierRolls
    {
        get
        {
            return tierRolls;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns one complete drop-pool definition after editor duplication or migration logic.
    /// </summary>
    /// <param name="poolIdValue">Stable pool identifier to persist.</param>
    /// <param name="tierRollsValue">Weighted tier candidates used by the pool.</param>

    public void Configure(string poolIdValue, List<PowerUpDropPoolTierDefinition> tierRollsValue)
    {
        poolId = poolIdValue;
        tierRolls = tierRollsValue;
    }

    /// <summary>
    /// Assigns the normalized pool ID after duplicate resolution.
    /// </summary>
    /// <param name="poolIdValue">Unique pool ID to persist on this definition.</param>

    public void AssignPoolId(string poolIdValue)
    {
        poolId = poolIdValue;
    }

    /// <summary>
    /// Normalizes pool data and keeps child entries allocated without removing duplicate tier references.
    /// </summary>
    /// <param name="fallbackPoolId">Fallback pool ID used when the current one is empty.</param>
    public void Validate(string fallbackPoolId)
    {
        if (string.IsNullOrWhiteSpace(poolId))
            poolId = string.IsNullOrWhiteSpace(fallbackPoolId) ? "Pool0" : fallbackPoolId.Trim();
        else
            poolId = poolId.Trim();

        if (tierRolls == null)
            tierRolls = new List<PowerUpDropPoolTierDefinition>();

        for (int tierRollIndex = 0; tierRollIndex < tierRolls.Count; tierRollIndex++)
        {
            PowerUpDropPoolTierDefinition tierRoll = tierRolls[tierRollIndex];

            if (tierRoll != null)
                continue;

            tierRoll = new PowerUpDropPoolTierDefinition();
            tierRolls[tierRollIndex] = tierRoll;
        }

        for (int tierRollIndex = 0; tierRollIndex < tierRolls.Count; tierRollIndex++)
            tierRolls[tierRollIndex].Validate(string.Empty);
    }

    /// <summary>
    /// Removes tier candidates that reference tier IDs no longer defined on the owning preset.
    /// </summary>
    /// <param name="validTierIds">Set of valid tier IDs defined on the owning power-ups preset.</param>

    public void RemoveInvalidTierRolls(ISet<string> validTierIds)
    {
        if (tierRolls == null)
            return;

        for (int tierRollIndex = tierRolls.Count - 1; tierRollIndex >= 0; tierRollIndex--)
        {
            PowerUpDropPoolTierDefinition tierRoll = tierRolls[tierRollIndex];

            if (tierRoll == null)
            {
                tierRolls.RemoveAt(tierRollIndex);
                continue;
            }

            if (string.IsNullOrWhiteSpace(tierRoll.TierId))
                continue;

            if (validTierIds == null || validTierIds.Contains(tierRoll.TierId))
                continue;

            tierRolls.RemoveAt(tierRollIndex);
        }
    }
    #endregion

    #endregion
}
