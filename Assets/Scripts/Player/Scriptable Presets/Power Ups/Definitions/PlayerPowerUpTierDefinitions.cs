using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies the modular power-up collection referenced by one tier entry.
/// </summary>
public enum PowerUpTierEntryKind
{
    Active = 0,
    Passive = 1
}

/// <summary>
/// Defines one percentage-based power-up entry used inside a tier roll.
/// </summary>
[Serializable]
public sealed class PowerUpTierEntryDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Power-up category used to resolve the selectable ID list.")]
    [SerializeField] private PowerUpTierEntryKind entryKind = PowerUpTierEntryKind.Active;

    [Tooltip("Power-up ID selected from the chosen category.")]
    [SerializeField] private string powerUpId;

    [Tooltip("Selection percentage used when this tier is rolled. Values <= 0 disable this entry and the tier should total 100%.")]
    [SerializeField] private float selectionWeight = 1f;
    #endregion

    #endregion

    #region Properties
    public PowerUpTierEntryKind EntryKind
    {
        get
        {
            return entryKind;
        }
    }

    public string PowerUpId
    {
        get
        {
            return powerUpId;
        }
    }

    public float SelectionWeight
    {
        get
        {
            return selectionWeight;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns entry data after external selection logic in editor tools.
    /// </summary>
    /// <param name="entryKindValue">Category that owns the referenced power-up ID.</param>
    /// <param name="powerUpIdValue">Selected modular power-up ID.</param>
    /// <param name="selectionWeightValue">Selection percentage assigned to this tier entry.</param>

    public void Configure(PowerUpTierEntryKind entryKindValue, string powerUpIdValue, float selectionWeightValue)
    {
        entryKind = entryKindValue;
        powerUpId = powerUpIdValue;
        selectionWeight = selectionWeightValue;
    }

    /// <summary>
    /// Sanitizes serialized values to keep runtime tier resolution stable.
    /// </summary>
    /// <param name="fallbackPowerUpId">Fallback power-up ID used when the selected one is empty.</param>

    public void Validate(string fallbackPowerUpId)
    {
        if (string.IsNullOrWhiteSpace(powerUpId))
            powerUpId = string.IsNullOrWhiteSpace(fallbackPowerUpId) ? string.Empty : fallbackPowerUpId.Trim();
        else
            powerUpId = powerUpId.Trim();

    }
    #endregion

    #endregion
}

/// <summary>
/// Defines one named tier and the percentage-based modular power-up entries available in that tier.
/// </summary>
[Serializable]
public sealed class PowerUpTierLevelDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable tier identifier used by progression milestones.")]
    [SerializeField] private string tierId;

    [Tooltip("Percentage-based power-up entries available when this tier is rolled. The sum should be 100%.")]
    [SerializeField] private List<PowerUpTierEntryDefinition> entries = new List<PowerUpTierEntryDefinition>();
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

    public IReadOnlyList<PowerUpTierEntryDefinition> Entries
    {
        get
        {
            return entries;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns tier data after external normalization logic in editor tools.
    /// </summary>
    /// <param name="tierIdValue">Tier ID used to reference this tier from milestones.</param>
    /// <param name="entriesValue">Weighted tier entries.</param>

    public void Configure(string tierIdValue, List<PowerUpTierEntryDefinition> entriesValue)
    {
        tierId = tierIdValue;
        entries = entriesValue;
    }

    /// <summary>
    /// Assigns the normalized tier ID after external validation resolves duplicates.
    /// </summary>
    /// <param name="tierIdValue">Unique tier ID to persist on this definition.</param>

    public void AssignTierId(string tierIdValue)
    {
        tierId = tierIdValue;
    }

    /// <summary>
    /// Sanitizes tier data, normalizes IDs, and keeps duplicate non-empty entry bindings when explicitly authored.
    /// </summary>
    /// <param name="fallbackTierId">Fallback tier ID used when the current one is empty.</param>

    public void Validate(string fallbackTierId)
    {
        if (string.IsNullOrWhiteSpace(tierId))
            tierId = string.IsNullOrWhiteSpace(fallbackTierId) ? "Tier0" : fallbackTierId.Trim();
        else
            tierId = tierId.Trim();

        if (entries == null)
            entries = new List<PowerUpTierEntryDefinition>();

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            PowerUpTierEntryDefinition entry = entries[entryIndex];

            if (entry != null)
                continue;

            entry = new PowerUpTierEntryDefinition();
            entries[entryIndex] = entry;
        }

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            entries[entryIndex].Validate(string.Empty);
    }

    /// <summary>
    /// Removes null tier entries and preserves authored invalid IDs so the editor can repair mismatched references manually.
    /// </summary>
    /// <param name="validActivePowerUpIds">Set of active modular power-up IDs available in the preset.</param>
    /// <param name="validPassivePowerUpIds">Set of passive modular power-up IDs available in the preset.</param>

    public void RemoveInvalidEntries(ISet<string> validActivePowerUpIds, ISet<string> validPassivePowerUpIds)
    {
        if (entries == null)
            return;

        for (int entryIndex = entries.Count - 1; entryIndex >= 0; entryIndex--)
        {
            PowerUpTierEntryDefinition entry = entries[entryIndex];

            if (entry == null)
            {
                entries.RemoveAt(entryIndex);
                continue;
            }

            // Preserve invalid references until the user explicitly repairs them in the editor.
        }
    }
    #endregion

    #endregion
}
