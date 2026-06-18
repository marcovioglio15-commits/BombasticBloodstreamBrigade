using System;
using UnityEngine;

/// <summary>
/// Stores the high-level extraction rules used to roll the next eligible boss pattern candidate.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternExtractionSettings
{
    #region Fields

    #region Serialized Fields
    [Header("General Extraction")]
    [Tooltip("When enabled, a new pattern is extracted as soon as the active pattern is no longer eligible.")]
    [SerializeField] private bool rerollWhenCurrentPatternBecomesInvalid = true;

    [Tooltip("Minimum seconds between two pattern extractions after the current pattern has satisfied its own minimum active duration.")]
    [SerializeField] private float minimumSecondsBetweenExtractions = 1f;

    [Header("Elapsed Time")]
    [Tooltip("When enabled, elapsed extraction interval can trigger a new pattern roll.")]
    [SerializeField] private bool useElapsedIntervalExtraction = true;

    [Tooltip("Seconds after the previous extraction before elapsed time can trigger a new pattern roll.")]
    [SerializeField] private float elapsedIntervalSeconds = 4f;

    [Header("Missing Health Steps")]
    [Tooltip("When enabled, crossing missing-health steps can trigger a new pattern roll.")]
    [SerializeField] private bool useMissingHealthStepExtraction = true;

    [Tooltip("Normalized missing-health step, from 0 to 1, required since the previous extraction.")]
    [Range(0f, 1f)]
    [SerializeField] private float missingHealthStepPercent = 0.25f;

    [Header("Travelled Distance")]
    [Tooltip("When enabled, boss travelled distance since the previous extraction can trigger a new pattern roll.")]
    [SerializeField] private bool useTravelledDistanceExtraction;

    [Tooltip("Planar boss movement distance required since the previous extraction.")]
    [SerializeField] private float travelledDistanceSinceLastExtraction = 10f;

    [Header("Player Distance Hold")]
    [Tooltip("Player-distance hold condition that can trigger a new pattern roll after the threshold is held long enough.")]
    [SerializeField] private EnemyBossPatternPlayerDistanceCondition playerDistanceCondition = EnemyBossPatternPlayerDistanceCondition.Disabled;

    [Tooltip("Planar player distance threshold used by the hold condition.")]
    [SerializeField] private float playerDistanceThreshold = 8f;

    [Tooltip("Seconds the player-distance condition must remain true before a new pattern roll can trigger.")]
    [SerializeField] private float playerDistanceHoldSeconds = 1f;

    [Header("Damage Window")]
    [Tooltip("When enabled, damage received inside the configured window can trigger a new pattern roll.")]
    [SerializeField] private bool useDamageWindowExtraction;

    [Tooltip("Seconds used to accumulate received damage for extraction checks.")]
    [SerializeField] private float damageWindowSeconds = 2f;

    [Tooltip("Damage amount that must be received inside the configured window before a new pattern roll can trigger.")]
    [SerializeField] private float damageThreshold = 20f;
    #endregion

    #endregion

    #region Properties
    public bool RerollWhenCurrentPatternBecomesInvalid
    {
        get
        {
            return rerollWhenCurrentPatternBecomesInvalid;
        }
    }

    public float MinimumSecondsBetweenExtractions
    {
        get
        {
            return minimumSecondsBetweenExtractions;
        }
    }

    public bool UseElapsedIntervalExtraction
    {
        get
        {
            return useElapsedIntervalExtraction;
        }
    }

    public float ElapsedIntervalSeconds
    {
        get
        {
            return elapsedIntervalSeconds;
        }
    }

    public bool UseMissingHealthStepExtraction
    {
        get
        {
            return useMissingHealthStepExtraction;
        }
    }

    public float MissingHealthStepPercent
    {
        get
        {
            return missingHealthStepPercent;
        }
    }

    public bool UseTravelledDistanceExtraction
    {
        get
        {
            return useTravelledDistanceExtraction;
        }
    }

    public float TravelledDistanceSinceLastExtraction
    {
        get
        {
            return travelledDistanceSinceLastExtraction;
        }
    }

    public EnemyBossPatternPlayerDistanceCondition PlayerDistanceCondition
    {
        get
        {
            return playerDistanceCondition;
        }
    }

    public float PlayerDistanceThreshold
    {
        get
        {
            return playerDistanceThreshold;
        }
    }

    public float PlayerDistanceHoldSeconds
    {
        get
        {
            return playerDistanceHoldSeconds;
        }
    }

    public bool UseDamageWindowExtraction
    {
        get
        {
            return useDamageWindowExtraction;
        }
    }

    public float DamageWindowSeconds
    {
        get
        {
            return damageWindowSeconds;
        }
    }

    public float DamageThreshold
    {
        get
        {
            return damageThreshold;
        }
    }
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
/// Stores one boss pattern candidate with independent internal extraction per module slot.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternInteractionDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Interaction")]
    [Tooltip("Enables this boss interaction during bake and runtime selection.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Boss-only eligibility criterion that decides when this pattern candidate can be extracted.")]
    [SerializeField] private EnemyBossPatternInteractionType interactionType = EnemyBossPatternInteractionType.Always;

    [Tooltip("Readable interaction name shown by the Boss Pattern Assemble section.")]
    [SerializeField] private string displayName = "Always Interaction";

    [Tooltip("Minimum seconds the current boss interaction must remain active before another valid interaction can replace it.")]
    [SerializeField] private float minimumActiveSeconds = 1f;

    [Tooltip("Relative weight used when this eligible interaction is part of a pattern extraction roll.")]
    [SerializeField] private float selectionWeight = 1f;

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

    [Header("Internal Core Movement Extraction")]
    [Tooltip("Core Movement extraction candidates used while this pattern candidate remains active.")]
    [SerializeField] private EnemyBossPatternCoreMovementExtractionDefinition coreMovementExtraction = new EnemyBossPatternCoreMovementExtractionDefinition();

    [Header("Internal Short-Range Extraction")]
    [Tooltip("Short-Range Interaction extraction candidates used while this pattern candidate remains active.")]
    [SerializeField] private EnemyBossPatternShortRangeExtractionDefinition shortRangeExtraction = new EnemyBossPatternShortRangeExtractionDefinition();

    [Header("Internal Weapon Extraction")]
    [Tooltip("Weapon Interaction extraction candidates used while this pattern candidate remains active.")]
    [SerializeField] private EnemyBossPatternWeaponExtractionDefinition weaponExtraction = new EnemyBossPatternWeaponExtractionDefinition();

    [Tooltip("Hidden legacy Core Movement override migrated into Internal Core Movement Extraction when the new candidate list is empty.")]
    [HideInInspector]
    [SerializeField] private EnemyBossPatternCoreMovementOverrideAssembly coreMovement = new EnemyBossPatternCoreMovementOverrideAssembly();

    [Tooltip("Hidden legacy Short-Range Interaction override migrated into Internal Short-Range Extraction when the new candidate list is empty.")]
    [HideInInspector]
    [SerializeField] private EnemyPatternShortRangeInteractionAssembly shortRangeInteraction = new EnemyPatternShortRangeInteractionAssembly();

    [Tooltip("Hidden legacy Weapon Interaction override migrated into Internal Weapon Extraction when the new candidate list is empty.")]
    [HideInInspector]
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

    public float SelectionWeight
    {
        get
        {
            return selectionWeight;
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

    public EnemyBossPatternCoreMovementExtractionDefinition CoreMovementExtraction
    {
        get
        {
            return coreMovementExtraction;
        }
    }

    public EnemyPatternShortRangeInteractionAssembly ShortRangeInteraction
    {
        get
        {
            return shortRangeInteraction;
        }
    }

    public EnemyBossPatternShortRangeExtractionDefinition ShortRangeExtraction
    {
        get
        {
            return shortRangeExtraction;
        }
    }

    public EnemyPatternWeaponInteractionAssembly WeaponInteraction
    {
        get
        {
            return weaponInteraction;
        }
    }

    public EnemyBossPatternWeaponExtractionDefinition WeaponExtraction
    {
        get
        {
            return weaponExtraction;
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

        if (coreMovementExtraction == null)
            coreMovementExtraction = new EnemyBossPatternCoreMovementExtractionDefinition();

        if (shortRangeExtraction == null)
            shortRangeExtraction = new EnemyBossPatternShortRangeExtractionDefinition();

        if (weaponExtraction == null)
            weaponExtraction = new EnemyBossPatternWeaponExtractionDefinition();

        MigrateLegacySlotsIfNeeded();
        coreMovementExtraction.Validate();
        shortRangeExtraction.Validate();
        weaponExtraction.Validate();
    }

    /// <summary>
    /// Converts an interaction type into a readable default label.
    /// </summary>
    /// <param name="type">Interaction type to format.</param>
    /// <returns> interaction type label.</returns>
    public static string FormatInteractionType(EnemyBossPatternInteractionType type)
    {
        switch (type)
        {
            case EnemyBossPatternInteractionType.Always:
                return "Always Interaction";

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

    #region Private Methods
    /// <summary>
    /// Migrates hidden legacy one-slot overrides into the new internal extraction lists when needed.
    /// </summary>
    private void MigrateLegacySlotsIfNeeded()
    {
        if (coreMovement != null)
        {
            coreMovement.Validate();

            if (coreMovement.IsEnabled)
                coreMovementExtraction.TryMigrateLegacyCandidate(coreMovement.Binding, displayName + " Core Movement");
        }

        if (shortRangeInteraction != null)
        {
            shortRangeInteraction.Validate();

            if (shortRangeInteraction.IsEnabled)
                shortRangeExtraction.TryMigrateLegacyCandidate(shortRangeInteraction, displayName + " Short-Range");
        }

        if (weaponInteraction != null)
        {
            weaponInteraction.Validate();

            if (weaponInteraction.IsEnabled)
                weaponExtraction.TryMigrateLegacyCandidate(weaponInteraction, displayName + " Weapon");
        }
    }
    #endregion

    #endregion
}
