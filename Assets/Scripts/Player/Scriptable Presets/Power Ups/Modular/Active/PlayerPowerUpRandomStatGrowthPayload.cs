using System;
using System.Collections.Generic;
using UnityEngine;

#region Random Stat Growth Definitions
/// <summary>
/// Selects one permanent player statistic supported by the Random Stat Growth active module.
/// </summary>
public enum PlayerRandomStatGrowthTarget : byte
{
    MaximumHealth = 0,
    MaximumShield = 1,
    ExperiencePickupRadius = 2,
    MovementBaseSpeed = 3,
    MovementMaximumSpeed = 4,
    MovementAcceleration = 5,
    MovementDeceleration = 6,
    LookRotationSpeed = 7,
    ProjectileSpeed = 8,
    RateOfFire = 9,
    ProjectileDamage = 10,
    ProjectileRange = 11,
    ProjectileLifetime = 12,
    ProjectileSizeMultiplier = 13,
    CustomScalableStat = 14
}

/// <summary>
/// Defines one selectable candidate, its random increase range, weight, and optional presentation color.
/// </summary>
[Serializable]
public sealed class PlayerRandomStatGrowthEntryData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier used to preserve Add Scaling bindings when this pool entry is reordered.")]
    [HideInInspector]
    [SerializeField]
    private string entryId = string.Empty;

    [Tooltip("Player statistic that can be selected when this active power-up executes.")]
    [SerializeField]
    private PlayerRandomStatGrowthTarget target = PlayerRandomStatGrowthTarget.ProjectileDamage;

    [Tooltip("Numeric scalable-stat identifier used when Target is Custom Scalable Stat.")]
    [SerializeField]
    private string customScalableStatName = string.Empty;

    [Tooltip("Minimum permanent amount granted when this entry is selected.")]
    [SerializeField]
    private float minimumIncrease = 1f;

    [Tooltip("Maximum permanent amount granted when this entry is selected.")]
    [SerializeField]
    private float maximumIncrease = 1f;

    [Tooltip("Relative selection weight used only when Weighted Selection is enabled. Zero excludes this candidate from weighted rolls.")]
    [SerializeField]
    private float selectionWeight = 1f;

    [Tooltip("Uses this candidate's color for the statistic increase shown above the player.")]
    [SerializeField]
    private bool useCustomPresentationColor;

    [Tooltip("Text color used above the player when this candidate is selected and its custom color is enabled.")]
    [SerializeField]
    private Color presentationColor = Color.white;
    #endregion

    #endregion

    #region Properties
    public string EntryId => entryId;
    public PlayerRandomStatGrowthTarget Target => target;
    public string CustomScalableStatName => customScalableStatName;
    public float MinimumIncrease => minimumIncrease;
    public float MaximumIncrease => maximumIncrease;
    public float SelectionWeight => selectionWeight;
    public bool UseCustomPresentationColor => useCustomPresentationColor;
    public Color PresentationColor => presentationColor;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns one selectable statistic and its authored increase range.
    /// </summary>
    /// <param name="targetValue">Statistic selected by this candidate.</param>
    /// <param name="customScalableStatNameValue">Scalable-stat identifier used by custom candidates.</param>
    /// <param name="minimumIncreaseValue">Minimum permanent increase.</param>
    /// <param name="maximumIncreaseValue">Maximum permanent increase.</param>
    /// <param name="selectionWeightValue">Relative candidate weight used by weighted selection.</param>
    /// <param name="useCustomPresentationColorValue">Whether this candidate overrides the presentation color.</param>
    /// <param name="presentationColorValue">Above-player text color used by this candidate.</param>
    public void Configure(PlayerRandomStatGrowthTarget targetValue,
                          string customScalableStatNameValue,
                          float minimumIncreaseValue,
                          float maximumIncreaseValue,
                          float selectionWeightValue,
                          bool useCustomPresentationColorValue,
                          Color presentationColorValue)
    {
        EnsureStableId();
        target = targetValue;
        customScalableStatName = customScalableStatNameValue;
        minimumIncrease = minimumIncreaseValue;
        maximumIncrease = maximumIncreaseValue;
        selectionWeight = selectionWeightValue;
        useCustomPresentationColor = useCustomPresentationColorValue;
        presentationColor = presentationColorValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures only the semantic identifier required by scaling paths without changing authored gameplay values.
    /// </summary>
    public void Validate()
    {
        EnsureStableId();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates the semantic identifier once so list reordering cannot invalidate scaling paths.
    /// </summary>
    private void EnsureStableId()
    {
        if (string.IsNullOrWhiteSpace(entryId))
            entryId = Guid.NewGuid().ToString("N");
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the selectable statistic pool executed by a Random Stat Growth active module.
/// </summary>
[Serializable]
public sealed class PowerUpRandomStatGrowthModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Uses each candidate's Selection Weight instead of equal probability. Candidates with zero weight are excluded.")]
    [SerializeField]
    private bool useWeightedSelection;

    [Tooltip("Statistic candidates. Custom entries accept only Float, Integer, or Unsigned scalable stats.")]
    [SerializeField]
    private List<PlayerRandomStatGrowthEntryData> entries = new List<PlayerRandomStatGrowthEntryData>();
    #endregion

    #endregion

    #region Properties
    public bool UseWeightedSelection => useWeightedSelection;
    public IReadOnlyList<PlayerRandomStatGrowthEntryData> Entries => entries;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Replaces the selectable statistic pool while retaining the provided entry instances and their stable IDs.
    /// </summary>
    /// <param name="entriesValue">Ordered candidates included in the statistic pool.</param>
    /// <param name="useWeightedSelectionValue">Whether candidate weights replace equal selection probability.</param>
    public void Configure(IEnumerable<PlayerRandomStatGrowthEntryData> entriesValue,
                          bool useWeightedSelectionValue = false)
    {
        useWeightedSelection = useWeightedSelectionValue;
        entries = entriesValue != null
            ? new List<PlayerRandomStatGrowthEntryData>(entriesValue)
            : new List<PlayerRandomStatGrowthEntryData>();
        Validate();
    }
    #endregion

    #region Validation
    /// <summary>
    /// Allocates a missing pool and repairs semantic entry IDs without normalizing authored ranges.
    /// </summary>
    public void Validate()
    {
        if (entries == null)
            entries = new List<PlayerRandomStatGrowthEntryData>();

        // Stable IDs are metadata and do not alter the authored statistic or range.
        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            if (entries[entryIndex] != null)
                entries[entryIndex].Validate();
        }
    }
    #endregion

    #endregion
}
#endregion
