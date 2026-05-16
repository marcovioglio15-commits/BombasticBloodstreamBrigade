using System;
using UnityEngine;

/// <summary>
/// Stores the base boss pattern assembled with the same interaction slots used by normal enemies.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternAssemblyDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Core Movement")]
    [Tooltip("Base core movement selection resolved while no higher-priority boss interaction overrides it.")]
    [SerializeField] private EnemyPatternCoreMovementAssembly coreMovement = new EnemyPatternCoreMovementAssembly();

    [Header("Short-Range Interaction")]
    [Tooltip("Optional base short-range interaction that behaves exactly like the normal enemy Pattern Assemble slot.")]
    [SerializeField] private EnemyPatternShortRangeInteractionAssembly shortRangeInteraction = new EnemyPatternShortRangeInteractionAssembly();

    [Header("Weapon Interaction")]
    [Tooltip("Optional base weapon interaction that behaves exactly like the normal enemy Pattern Assemble slot.")]
    [SerializeField] private EnemyPatternWeaponInteractionAssembly weaponInteraction = new EnemyPatternWeaponInteractionAssembly();
    #endregion

    #endregion

    #region Properties
    public EnemyPatternCoreMovementAssembly CoreMovement
    {
        get
        {
            return coreMovement;
        }
    }

    public EnemyPatternShortRangeInteractionAssembly ShortRangeInteraction
    {
        get
        {
            return shortRangeInteraction;
        }
    }

    public EnemyPatternWeaponInteractionAssembly WeaponInteraction
    {
        get
        {
            return weaponInteraction;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures nested base pattern slots always exist before editor drawing and bake-time compilation.
    /// </summary>
    public void Validate()
    {
        if (coreMovement == null)
            coreMovement = new EnemyPatternCoreMovementAssembly();

        if (shortRangeInteraction == null)
            shortRangeInteraction = new EnemyPatternShortRangeInteractionAssembly();

        if (weaponInteraction == null)
            weaponInteraction = new EnemyPatternWeaponInteractionAssembly();

        coreMovement.Validate();
        shortRangeInteraction.Validate();
        weaponInteraction.Validate();
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one optional core movement override inside a boss-specific interaction layer.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternCoreMovementOverrideAssembly
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, this boss interaction replaces the base Core Movement slot while the interaction is active.")]
    [SerializeField] private bool isEnabled;

    [Tooltip("Core movement module binding resolved from Core Movement definitions.")]
    [SerializeField] private EnemyPatternModuleBinding binding = new EnemyPatternModuleBinding();
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

    public EnemyPatternModuleBinding Binding
    {
        get
        {
            return binding;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the optional core override owns one valid binding instance.
    /// </summary>
    public void Validate()
    {
        if (binding == null)
            binding = new EnemyPatternModuleBinding();

        binding.Validate();
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one ordered boss-specific interaction that can override selected normal Pattern Assemble slots.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternInteractionDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Interaction")]
    [Tooltip("Enables this boss interaction during bake and runtime selection.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Boss-only trigger that decides when this interaction can override the base pattern.")]
    [SerializeField] private EnemyBossPatternInteractionType interactionType = EnemyBossPatternInteractionType.MissingHealth;

    [Tooltip("Readable interaction name shown by the Boss Pattern Assemble section.")]
    [SerializeField] private string displayName = "Missing Health Interaction";

    [Tooltip("Minimum seconds the current boss interaction must remain active before another valid interaction can replace it.")]
    [SerializeField] private float minimumActiveSeconds = 1f;

    [Header("Missing Health")]
    [Tooltip("Minimum missing-health percentage, from 0 to 1, required by Missing Health interactions.")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumMissingHealthPercent = 0.25f;

    [Tooltip("Maximum missing-health percentage, from 0 to 1, allowed by Missing Health interactions. Set to 0 to disable the upper bound.")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumMissingHealthPercent;

    [Header("Elapsed Time")]
    [Tooltip("Minimum seconds since boss spawn required by Elapsed Time interactions.")]
    [SerializeField] private float minimumElapsedSeconds;

    [Tooltip("Maximum seconds since boss spawn allowed by Elapsed Time interactions. Set to 0 to disable the upper bound.")]
    [SerializeField] private float maximumElapsedSeconds;

    [Header("Travelled Distance")]
    [Tooltip("Minimum planar distance travelled by the boss required by Travelled Distance interactions.")]
    [SerializeField] private float minimumTravelledDistance;

    [Tooltip("Maximum planar distance travelled by the boss allowed by Travelled Distance interactions. Set to 0 to disable the upper bound.")]
    [SerializeField] private float maximumTravelledDistance;

    [Header("Player Distance")]
    [Tooltip("Minimum planar distance from player required by Player Distance interactions.")]
    [SerializeField] private float minimumPlayerDistance;

    [Tooltip("Maximum planar distance from player allowed by Player Distance interactions. Set to 0 to disable the upper bound.")]
    [SerializeField] private float maximumPlayerDistance = 12f;

    [Header("Recently Damaged")]
    [Tooltip("Seconds after receiving damage for which Recently Damaged interactions are considered valid.")]
    [SerializeField] private float recentlyDamagedWindowSeconds = 1.25f;

    [Header("Core Movement Override")]
    [Tooltip("Optional Core Movement slot override applied while this boss interaction is active.")]
    [SerializeField] private EnemyBossPatternCoreMovementOverrideAssembly coreMovement = new EnemyBossPatternCoreMovementOverrideAssembly();

    [Header("Short-Range Interaction Override")]
    [Tooltip("Optional Short-Range Interaction slot override applied while this boss interaction is active.")]
    [SerializeField] private EnemyPatternShortRangeInteractionAssembly shortRangeInteraction = new EnemyPatternShortRangeInteractionAssembly();

    [Header("Weapon Interaction Override")]
    [Tooltip("Optional Weapon Interaction slot override applied while this boss interaction is active.")]
    [SerializeField] private EnemyPatternWeaponInteractionAssembly weaponInteraction = new EnemyPatternWeaponInteractionAssembly();
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public EnemyBossPatternInteractionType InteractionType
    {
        get
        {
            return interactionType;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public float MinimumActiveSeconds
    {
        get
        {
            return minimumActiveSeconds;
        }
    }

    public float MinimumMissingHealthPercent
    {
        get
        {
            return minimumMissingHealthPercent;
        }
    }

    public float MaximumMissingHealthPercent
    {
        get
        {
            return maximumMissingHealthPercent;
        }
    }

    public float MinimumElapsedSeconds
    {
        get
        {
            return minimumElapsedSeconds;
        }
    }

    public float MaximumElapsedSeconds
    {
        get
        {
            return maximumElapsedSeconds;
        }
    }

    public float MinimumTravelledDistance
    {
        get
        {
            return minimumTravelledDistance;
        }
    }

    public float MaximumTravelledDistance
    {
        get
        {
            return maximumTravelledDistance;
        }
    }

    public float MinimumPlayerDistance
    {
        get
        {
            return minimumPlayerDistance;
        }
    }

    public float MaximumPlayerDistance
    {
        get
        {
            return maximumPlayerDistance;
        }
    }

    public float RecentlyDamagedWindowSeconds
    {
        get
        {
            return recentlyDamagedWindowSeconds;
        }
    }

    public EnemyBossPatternCoreMovementOverrideAssembly CoreMovement
    {
        get
        {
            return coreMovement;
        }
    }

    public EnemyPatternShortRangeInteractionAssembly ShortRangeInteraction
    {
        get
        {
            return shortRangeInteraction;
        }
    }

    public EnemyPatternWeaponInteractionAssembly WeaponInteraction
    {
        get
        {
            return weaponInteraction;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps interaction identity and nested slot references valid without changing authored thresholds.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = FormatInteractionType(interactionType);

        if (coreMovement == null)
            coreMovement = new EnemyBossPatternCoreMovementOverrideAssembly();

        if (shortRangeInteraction == null)
            shortRangeInteraction = new EnemyPatternShortRangeInteractionAssembly();

        if (weaponInteraction == null)
            weaponInteraction = new EnemyPatternWeaponInteractionAssembly();

        coreMovement.Validate();
        shortRangeInteraction.Validate();
        weaponInteraction.Validate();
    }

    /// <summary>
    /// Converts an interaction type into a readable default label.
    /// </summary>
    /// <param name="type">Interaction type to format.</param>
    /// <returns>Human-readable interaction type label.</returns>
    public static string FormatInteractionType(EnemyBossPatternInteractionType type)
    {
        switch (type)
        {
            case EnemyBossPatternInteractionType.ElapsedTime:
                return "Elapsed Time Interaction";

            case EnemyBossPatternInteractionType.TravelledDistance:
                return "Travelled Distance Interaction";

            case EnemyBossPatternInteractionType.PlayerDistance:
                return "Player Distance Interaction";

            case EnemyBossPatternInteractionType.RecentlyDamaged:
                return "Recently Damaged Interaction";

            default:
                return "Missing Health Interaction";
        }
    }
    #endregion

    #endregion
}
