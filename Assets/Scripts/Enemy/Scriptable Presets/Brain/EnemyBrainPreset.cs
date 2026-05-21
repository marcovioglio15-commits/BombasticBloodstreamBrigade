using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Stores movement settings used by enemy simulation and steering systems.
/// </summary>
[Serializable]
public sealed class EnemyBrainMovementSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Meters per second used as baseline enemy movement speed toward the player.")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Hard cap applied to the enemy velocity magnitude.")]
    [SerializeField] private float maxSpeed = 4f;

    [Tooltip("Meters per second squared used to accelerate toward desired velocity.")]
    [SerializeField] private float acceleration = 8f;

    [Tooltip("Reserved deceleration value for future braking behaviors. Currently unused at runtime.")]
    [SerializeField] private float deceleration = 8f;

    [Tooltip("Seconds after spawn during which the enemy stays fully idle while still remaining damageable.")]
    [SerializeField] private float inactivityTime;

    [Tooltip("Self-rotation speed in degrees per second. Positive rotates clockwise around Y, negative counter-clockwise.")]
    [SerializeField] private float rotationSpeedDegreesPerSecond;

    [Tooltip("General enemy priority tier used by steering and visual overlap rules. Higher values keep right-of-way over lower tiers.")]
    [SerializeField] private int priorityTier;

    [Tooltip("Scales steering and clearance reactivity. Higher values produce stronger side-step and avoidance corrections.")]
    [SerializeField] private float steeringAggressiveness = 1f;

    [Tooltip("Extra distance in meters kept from static wall colliders by standard steering-driven enemies.")]
    [SerializeField] private float minimumWallDistance = 0.25f;

    [Tooltip("When enabled, player projectile knockback is ignored by this enemy regardless of player stats or projectile payloads.")]
    [SerializeField] private bool disablePlayerKnockback;
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

    public float MaxSpeed
    {
        get
        {
            return maxSpeed;
        }
    }

    public float Acceleration
    {
        get
        {
            return acceleration;
        }
    }

    public float Deceleration
    {
        get
        {
            return deceleration;
        }
    }

    public float InactivityTime
    {
        get
        {
            return inactivityTime;
        }
    }

    public float RotationSpeedDegreesPerSecond
    {
        get
        {
            return rotationSpeedDegreesPerSecond;
        }
    }

    public int PriorityTier
    {
        get
        {
            return priorityTier;
        }
    }

    public float SteeringAggressiveness
    {
        get
        {
            return steeringAggressiveness;
        }
    }

    public float MinimumWallDistance
    {
        get
        {
            return minimumWallDistance;
        }
    }

    public bool DisablePlayerKnockback
    {
        get
        {
            return disablePlayerKnockback;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes movement values after asset edits.
    /// </summary>
    public void Validate()
    {
        if (moveSpeed < 0f)
            moveSpeed = 0f;

        if (maxSpeed < 0f)
            maxSpeed = 0f;

        if (acceleration < 0f)
            acceleration = 0f;

        if (deceleration < 0f)
            deceleration = 0f;

        if (inactivityTime < 0f)
            inactivityTime = 0f;

        if (float.IsNaN(rotationSpeedDegreesPerSecond) || float.IsInfinity(rotationSpeedDegreesPerSecond))
            rotationSpeedDegreesPerSecond = 0f;

        priorityTier = Mathf.Clamp(priorityTier, -128, 128);

        if (float.IsNaN(steeringAggressiveness) || float.IsInfinity(steeringAggressiveness))
            steeringAggressiveness = 1f;
        else
            steeringAggressiveness = Mathf.Clamp(steeringAggressiveness, 0f, 2.5f);

        if (minimumWallDistance < 0f)
            minimumWallDistance = 0f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores neighbor separation settings used by enemy steering.
/// </summary>
[Serializable]
public sealed class EnemyBrainSteeringSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Radius used to search neighboring enemies for separation steering.")]
    [SerializeField] private float separationRadius = 1.1f;

    [Tooltip("Weight applied to the separation vector before velocity clamping.")]
    [SerializeField] private float separationWeight = 2f;

    [Tooltip("Physical body radius used for projectile hit checks and overlap handling.")]
    [SerializeField] private float bodyRadius = 0.55f;
    #endregion

    #endregion

    #region Properties
    public float SeparationRadius
    {
        get
        {
            return separationRadius;
        }
    }

    public float SeparationWeight
    {
        get
        {
            return separationWeight;
        }
    }

    public float BodyRadius
    {
        get
        {
            return bodyRadius;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes steering values after asset edits.
    /// </summary>
    public void Validate()
    {
        if (separationRadius < 0.1f)
            separationRadius = 0.1f;

        if (separationWeight < 0f)
            separationWeight = 0f;

        if (bodyRadius < 0.05f)
            bodyRadius = 0.05f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores enemy damage settings applied against the player.
/// </summary>
[Serializable]
public sealed class EnemyBrainDamageSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, enemies apply flat periodic damage while the player is inside contact radius.")]
    [SerializeField] private bool contactDamageEnabled = true;

    [Tooltip("Distance from enemy center used to trigger contact damage ticks.")]
    [SerializeField] private float contactRadius = 1.2f;

    [Tooltip("Flat damage amount subtracted from player health at each contact tick.")]
    [FormerlySerializedAs("contactDamage")]
    [SerializeField] private float contactAmountPerTick = 5f;

    [Tooltip("Interval in seconds between two contact damage ticks.")]
    [FormerlySerializedAs("contactInterval")]
    [SerializeField] private float contactTickInterval = 0.75f;

    [Tooltip("When enabled, enemies apply periodic percentage damage while the player is inside area radius.")]
    [SerializeField] private bool areaDamageEnabled;

    [Tooltip("Distance from enemy center used to trigger area damage ticks.")]
    [SerializeField] private float areaRadius = 2.25f;

    [Tooltip("Percent of player max health applied per area damage tick. Example: 2 means 2%.")]
    [SerializeField] private float areaAmountPerTickPercent = 2f;

    [Tooltip("Interval in seconds between two area damage ticks.")]
    [SerializeField] private float areaTickInterval = 1f;
    #endregion

    #endregion

    #region Properties
    public bool ContactDamageEnabled
    {
        get
        {
            return contactDamageEnabled;
        }
    }

    public float ContactRadius
    {
        get
        {
            return contactRadius;
        }
    }

    public float ContactAmountPerTick
    {
        get
        {
            return contactAmountPerTick;
        }
    }

    public float ContactTickInterval
    {
        get
        {
            return contactTickInterval;
        }
    }

    public bool AreaDamageEnabled
    {
        get
        {
            return areaDamageEnabled;
        }
    }

    public float AreaRadius
    {
        get
        {
            return areaRadius;
        }
    }

    public float AreaAmountPerTickPercent
    {
        get
        {
            return areaAmountPerTickPercent;
        }
    }

    public float AreaTickInterval
    {
        get
        {
            return areaTickInterval;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes damage values after asset edits.
    /// </summary>
    public void Validate()
    {
        if (contactRadius < 0f)
            contactRadius = 0f;

        if (contactAmountPerTick < 0f)
            contactAmountPerTick = 0f;

        if (contactTickInterval < 0.01f)
            contactTickInterval = 0.01f;

        if (areaRadius < 0f)
            areaRadius = 0f;

        if (areaAmountPerTickPercent < 0f)
            areaAmountPerTickPercent = 0f;

        if (areaTickInterval < 0.01f)
            areaTickInterval = 0.01f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores health and shield values assigned when an enemy is activated from pool.
/// </summary>
[Serializable]
public sealed class EnemyBrainHealthStatisticsSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Maximum and initial health assigned to this enemy when spawned from pool.")]
    [SerializeField] private float maxHealth = 30f;

    [Tooltip("Maximum shield reserve assigned to this enemy at spawn. Shield absorbs incoming damage before health.")]
    [SerializeField] private float maxShield;
    #endregion

    #endregion

    #region Properties
    public float MaxHealth
    {
        get
        {
            return maxHealth;
        }
    }

    public float MaxShield
    {
        get
        {
            return maxShield;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes health values after asset edits.
    /// </summary>
    public void Validate()
    {
        if (maxHealth < 1f)
            maxHealth = 1f;

        if (maxShield < 0f)
            maxShield = 0f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores authoring-time simulation settings for one enemy brain preset.
/// </summary>
[CreateAssetMenu(fileName = "EnemyBrainPreset", menuName = "Enemy/Brain Preset", order = 10)]
public sealed class EnemyBrainPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Preset name.")]
    [SerializeField] private string presetName = "New Enemy Brain Preset";

    [Tooltip("Short description of the preset use case.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Brain")]
    [Tooltip("Movement settings block.")]
    [SerializeField] private EnemyBrainMovementSettings movement = new EnemyBrainMovementSettings();

    [Tooltip("Steering settings block.")]
    [SerializeField] private EnemyBrainSteeringSettings steering = new EnemyBrainSteeringSettings();

    [Tooltip("Tactical navigation settings block.")]
    [SerializeField] private EnemyBrainTacticalNavigationSettings tacticalNavigation = new EnemyBrainTacticalNavigationSettings();

    [Tooltip("Damage settings block.")]
    [FormerlySerializedAs("playerContact")]
    [SerializeField] private EnemyBrainDamageSettings damage = new EnemyBrainDamageSettings();

    [Tooltip("Health and shield settings block.")]
    [SerializeField] private EnemyBrainHealthStatisticsSettings healthStatistics = new EnemyBrainHealthStatisticsSettings();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public EnemyBrainMovementSettings Movement
    {
        get
        {
            return movement;
        }
    }

    public EnemyBrainSteeringSettings Steering
    {
        get
        {
            return steering;
        }
    }

    public EnemyBrainTacticalNavigationSettings TacticalNavigation
    {
        get
        {
            return tacticalNavigation;
        }
    }

    public EnemyBrainDamageSettings Damage
    {
        get
        {
            return damage;
        }
    }

    public EnemyBrainHealthStatisticsSettings HealthStatistics
    {
        get
        {
            return healthStatistics;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates nested settings and guarantees stable metadata defaults.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (movement == null)
            movement = new EnemyBrainMovementSettings();

        if (steering == null)
            steering = new EnemyBrainSteeringSettings();

        if (tacticalNavigation == null)
            tacticalNavigation = new EnemyBrainTacticalNavigationSettings();

        if (damage == null)
            damage = new EnemyBrainDamageSettings();

        if (healthStatistics == null)
            healthStatistics = new EnemyBrainHealthStatisticsSettings();

        movement.Validate();
        steering.Validate();
        tacticalNavigation.Validate();
        damage.Validate();
        healthStatistics.Validate();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Revalidates the asset after inspector changes.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
