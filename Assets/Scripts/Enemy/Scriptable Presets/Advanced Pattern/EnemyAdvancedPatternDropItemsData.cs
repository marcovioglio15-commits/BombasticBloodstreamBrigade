using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Declares one drop-definition entry for experience drops.
/// </summary>
[Serializable]
public sealed class EnemyExperienceDropDefinitionData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Prefab spawned for this experience drop entry.")]
    [SerializeField] private GameObject dropPrefab;

    [Tooltip("Experience granted when this drop is collected by the player.")]
    [SerializeField] private float experienceAmount = 1f;
    #endregion

    #endregion

    #region Properties
    public GameObject DropPrefab
    {
        get
        {
            return dropPrefab;
        }
    }

    public float ExperienceAmount
    {
        get
        {
            return experienceAmount;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures one experience drop-definition entry remains structurally valid without snapping authored settings.
    /// </summary>

    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores movement settings used by spawned experience drops.
/// </summary>
[Serializable]
public sealed class EnemyExperienceDropCollectionSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Movement speed used while drops move toward the player.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("Distance from the player used to convert the drop into experience.")]
    [SerializeField] private float collectDistance = 0.3f;

    [Tooltip("Linear multiplier applied to collect distance using current player speed. Effective distance = Collect Distance + (Player Speed * this value).")]
    [SerializeField] private float collectDistancePerPlayerSpeed = 0.05f;

    [Tooltip("Minimum duration (seconds) used for the initial spawn spread animation.")]
    [SerializeField] private float spawnAnimationMinDuration = 0.08f;

    [Tooltip("Maximum duration (seconds) used for the initial spawn spread animation.")]
    [SerializeField] private float spawnAnimationMaxDuration = 0.16f;
    #endregion

    #endregion

    #region Properties
    public float MoveSpeed
    {
        get
        {
            return moveSpeed;
        }
    }

    public float CollectDistance
    {
        get
        {
            return collectDistance;
        }
    }

    public float CollectDistancePerPlayerSpeed
    {
        get
        {
            return collectDistancePerPlayerSpeed;
        }
    }

    public float SpawnAnimationMinDuration
    {
        get
        {
            return spawnAnimationMinDuration;
        }
    }

    public float SpawnAnimationMaxDuration
    {
        get
        {
            return spawnAnimationMaxDuration;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures experience drop collection settings remain structurally valid without snapping authored settings.
    /// </summary>

    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores experience drop payload values used by DropItems modules.
/// </summary>
[Serializable]
public sealed class EnemyExperienceDropPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Definitions catalog used to compose the spawned experience drops.")]
    [SerializeField] private List<EnemyExperienceDropDefinitionData> dropDefinitions = new List<EnemyExperienceDropDefinitionData>();

    [Tooltip("Minimum total experience dropped at enemy death, distributed over the defined drop entries.")]
    [FormerlySerializedAs("complessiveExperienceDrop")]
    [SerializeField] private float complessiveExperienceDropMinimum = 10f;

    [Tooltip("Maximum total experience dropped at enemy death, distributed over the defined drop entries.")]
    [SerializeField] private float complessiveExperienceDropMaximum;

    [Tooltip("Distribution bias for drop composition: 0 favors low-value drops count, 1 favors high-value drops count.")]
    [Range(0f, 1f)]
    [SerializeField] private float dropsDistribution = 0.5f;

    [Tooltip("Radius around the enemy used to distribute spawned drops uniformly.")]
    [SerializeField] private float dropRadius = 0.6f;

    [Tooltip("Collection movement settings applied to spawned experience drops.")]
    [SerializeField] private EnemyExperienceDropCollectionSettings collectionMovement = new EnemyExperienceDropCollectionSettings();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<EnemyExperienceDropDefinitionData> DropDefinitions
    {
        get
        {
            return dropDefinitions;
        }
    }

    public float ComplessiveExperienceDropMinimum
    {
        get
        {
            return complessiveExperienceDropMinimum;
        }
    }

    public float ComplessiveExperienceDropMaximum
    {
        get
        {
            return complessiveExperienceDropMaximum;
        }
    }

    public float DropsDistribution
    {
        get
        {
            return dropsDistribution;
        }
    }

    public float DropRadius
    {
        get
        {
            return dropRadius;
        }
    }

    public EnemyExperienceDropCollectionSettings CollectionMovement
    {
        get
        {
            return collectionMovement;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures experience payload references remain structurally valid without snapping authored settings.
    /// </summary>

    public void Validate()
    {
        if (dropDefinitions == null)
            dropDefinitions = new List<EnemyExperienceDropDefinitionData>();

        if (collectionMovement == null)
            collectionMovement = new EnemyExperienceDropCollectionSettings();
    }
    #endregion

    #endregion
}

/// <summary>
/// Declares one drop-definition entry for health and shield recovery drops.
/// </summary>
[Serializable]
public sealed class EnemyRecoveryDropDefinitionData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Prefab spawned for this recovery drop entry.")]
    [SerializeField] private GameObject dropPrefab;

    [Tooltip("Health restored when this drop is collected by the player.")]
    [SerializeField] private float healthRestoreAmount = 5f;

    [Tooltip("Shield restored when this drop is collected by the player.")]
    [SerializeField] private float shieldRestoreAmount;

    [Tooltip("Fixed number of pickups spawned from this definition when the owning recovery module succeeds its drop chance.")]
    [Min(0)]
    [SerializeField] private int dropCount = 1;
    #endregion

    #endregion

    #region Properties
    public GameObject DropPrefab
    {
        get
        {
            return dropPrefab;
        }
    }

    public float HealthRestoreAmount
    {
        get
        {
            return healthRestoreAmount;
        }
    }

    public float ShieldRestoreAmount
    {
        get
        {
            return shieldRestoreAmount;
        }
    }

    public int DropCount
    {
        get
        {
            return dropCount;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Preserves one recovery drop-definition entry while keeping validation hooks consistent with other drop definitions.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores recovery drop payload values used by DropItems modules.
/// </summary>
[Serializable]
public sealed class EnemyRecoveryDropPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Definitions catalog used to compose spawned health and shield recovery drops.")]
    [SerializeField] private List<EnemyRecoveryDropDefinitionData> dropDefinitions = new List<EnemyRecoveryDropDefinitionData>();

    [Tooltip("Chance in percent that this recovery module drops any configured recovery definitions.")]
    [Range(0f, 100f)]
    [SerializeField] private float dropChancePercent = 100f;

    [Tooltip("Radius around the enemy used to distribute spawned recovery drops uniformly.")]
    [SerializeField] private float dropRadius = 0.6f;

    [Tooltip("Collection movement settings applied to spawned recovery drops.")]
    [SerializeField] private EnemyExperienceDropCollectionSettings collectionMovement = new EnemyExperienceDropCollectionSettings();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<EnemyRecoveryDropDefinitionData> DropDefinitions
    {
        get
        {
            return dropDefinitions;
        }
    }

    public float DropChancePercent
    {
        get
        {
            return dropChancePercent;
        }
    }

    public float DropRadius
    {
        get
        {
            return dropRadius;
        }
    }

    public EnemyExperienceDropCollectionSettings CollectionMovement
    {
        get
        {
            return collectionMovement;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures recovery payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
        if (dropDefinitions == null)
            dropDefinitions = new List<EnemyRecoveryDropDefinitionData>();

        if (collectionMovement == null)
            collectionMovement = new EnemyExperienceDropCollectionSettings();
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one metric-driven combo-points response evaluated when the enemy dies.
/// </summary>
[Serializable]
public sealed class EnemyExtraComboPointsConditionData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Runtime metric evaluated when the enemy dies to decide whether this multiplier is active.")]
    [SerializeField] private EnemyExtraComboPointsMetric metric = EnemyExtraComboPointsMetric.LifetimeSinceSpawnSeconds;

    [Tooltip("Inclusive minimum metric value required for this multiplier to match.")]
    [SerializeField] private float minimumValue;

    [Tooltip("When enabled, this multiplier also requires the metric to stay below Maximum Value.")]
    [SerializeField] private bool useMaximumValue;

    [Tooltip("Inclusive maximum metric value required for this multiplier when Maximum Value gating is enabled.")]
    [SerializeField] private float maximumValue = 5f;

    [Tooltip("Multiplier returned when the normalized response curve evaluates to 0.")]
    [SerializeField] private float minimumMultiplier = 1f;

    [Tooltip("Multiplier returned when the normalized response curve evaluates to 1.")]
    [FormerlySerializedAs("multiplier")]
    [SerializeField] private float maximumMultiplier = 2f;

    [Tooltip("Normalized response curve sampled across the metric range. X maps Minimum Value to Maximum Value, Y maps Minimum Multiplier to Maximum Multiplier. Use descending curves to reward quick kills and ascending curves to reward slower kills.")]
    [SerializeField] private AnimationCurve normalizedMultiplierCurve = CreateDefaultNormalizedMultiplierCurve();
    #endregion

    #endregion

    #region Properties
    public EnemyExtraComboPointsMetric Metric
    {
        get
        {
            return metric;
        }
    }

    public float MinimumValue
    {
        get
        {
            return minimumValue;
        }
    }

    public bool UseMaximumValue
    {
        get
        {
            return useMaximumValue;
        }
    }

    public float MaximumValue
    {
        get
        {
            return maximumValue;
        }
    }

    public float MinimumMultiplier
    {
        get
        {
            return minimumMultiplier;
        }
    }

    public float MaximumMultiplier
    {
        get
        {
            return maximumMultiplier;
        }
    }

    public AnimationCurve NormalizedMultiplierCurve
    {
        get
        {
            return normalizedMultiplierCurve;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures one Extra Combo Points condition remains structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
        if (normalizedMultiplierCurve == null)
            normalizedMultiplierCurve = CreateDefaultNormalizedMultiplierCurve();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates the default normalized curve used to reward quick kills more than delayed kills.
    /// </summary>
    /// <returns>Default normalized multiplier curve.</returns>
    private static AnimationCurve CreateDefaultNormalizedMultiplierCurve()
    {
        return new AnimationCurve(new Keyframe(0f, 1f),
                                  new Keyframe(1f, 0f));
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores conditional combo-points multiplier settings used by DropItems modules.
/// </summary>
[Serializable]
public sealed class EnemyExtraComboPointsPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Base multiplier applied to the combo points granted by this kill before conditional modifiers are evaluated.")]
    [SerializeField] private float baseMultiplier = 1f;

    [Tooltip("Strategy used to combine every matching conditional multiplier inside this module.")]
    [SerializeField] private EnemyExtraComboPointsConditionCombineMode conditionCombineMode = EnemyExtraComboPointsConditionCombineMode.MultiplyMatchingConditions;

    [Tooltip("Minimum final multiplier produced by this module after the base multiplier and matching conditions are combined.")]
    [SerializeField] private float minimumFinalMultiplier;

    [Tooltip("Maximum final multiplier produced by this module after the base multiplier and matching conditions are combined.")]
    [SerializeField] private float maximumFinalMultiplier = 4f;

    [Tooltip("Conditional multipliers evaluated against enemy lifetime and damage timing data when the enemy dies.")]
    [SerializeField] private List<EnemyExtraComboPointsConditionData> conditions = new List<EnemyExtraComboPointsConditionData>();
    #endregion

    #endregion

    #region Properties
    public float BaseMultiplier
    {
        get
        {
            return baseMultiplier;
        }
    }

    public EnemyExtraComboPointsConditionCombineMode ConditionCombineMode
    {
        get
        {
            return conditionCombineMode;
        }
    }

    public float MinimumFinalMultiplier
    {
        get
        {
            return minimumFinalMultiplier;
        }
    }

    public float MaximumFinalMultiplier
    {
        get
        {
            return maximumFinalMultiplier;
        }
    }

    public IReadOnlyList<EnemyExtraComboPointsConditionData> Conditions
    {
        get
        {
            return conditions;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures Extra Combo Points payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
        if (conditions == null)
            conditions = new List<EnemyExtraComboPointsConditionData>();

        for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
        {
            if (conditions[conditionIndex] == null)
                conditions[conditionIndex] = new EnemyExtraComboPointsConditionData();

            conditions[conditionIndex].Validate();
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Groups DropItems module selection and payload settings.
/// </summary>
[Serializable]
public sealed class EnemyDropItemsModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Drop payload category used by this module.")]
    [SerializeField] private EnemyDropItemsPayloadKind dropPayloadKind = EnemyDropItemsPayloadKind.Experience;

    [Tooltip("Experience payload used when Drop Payload Kind is Experience.")]
    [SerializeField] private EnemyExperienceDropPayload experience = new EnemyExperienceDropPayload();

    [Tooltip("Extra Combo Points payload used when Drop Payload Kind is Extra Combo Points.")]
    [SerializeField] private EnemyExtraComboPointsPayload extraComboPoints = new EnemyExtraComboPointsPayload();

    [Tooltip("Recovery payload used when Drop Payload Kind is Recovery.")]
    [SerializeField] private EnemyRecoveryDropPayload recovery = new EnemyRecoveryDropPayload();
    #endregion

    #endregion

    #region Properties
    public EnemyDropItemsPayloadKind DropPayloadKind
    {
        get
        {
            return dropPayloadKind;
        }
    }

    public EnemyExperienceDropPayload Experience
    {
        get
        {
            return experience;
        }
    }

    public EnemyExtraComboPointsPayload ExtraComboPoints
    {
        get
        {
            return extraComboPoints;
        }
    }

    public EnemyRecoveryDropPayload Recovery
    {
        get
        {
            return recovery;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures DropItems nested payload references remain structurally valid without snapping authored settings.
    /// </summary>

    public void Validate()
    {
        if (experience == null)
            experience = new EnemyExperienceDropPayload();

        if (extraComboPoints == null)
            extraComboPoints = new EnemyExtraComboPointsPayload();

        if (recovery == null)
            recovery = new EnemyRecoveryDropPayload();

        experience.Validate();
        extraComboPoints.Validate();
        recovery.Validate();
    }
    #endregion

    #endregion
}
