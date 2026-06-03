using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerControllerPreset", menuName = "Player/Controller Preset", order = 10)]
public sealed class PlayerControllerPreset : ScriptableObject
{
    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this preset, used for stable references.")]
    [FormerlySerializedAs("m_PresetId")]
    [SerializeField] private string presetId;

    [Tooltip("Preset name.")]
    [FormerlySerializedAs("m_PresetName")]
    [SerializeField] private string presetName = "New Player Preset";

    [Tooltip("Short description of the preset use case.")]
    [FormerlySerializedAs("m_Description")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this preset.")]
    [FormerlySerializedAs("m_Version")]
    [SerializeField] private string version = "1.0.0";

    [Header("Controller Settings")]
    [Tooltip("Movement configuration block.")]
    [FormerlySerializedAs("m_MovementSettings")]
    [SerializeField] private MovementSettings movementSettings = new MovementSettings();

    [Tooltip("Look configuration block.")]
    [FormerlySerializedAs("m_LookSettings")]
    [SerializeField] private LookSettings lookSettings = new LookSettings();

    [Tooltip("Camera configuration block.")]
    [FormerlySerializedAs("m_CameraSettings")]
    [SerializeField] private CameraSettings cameraSettings = new CameraSettings();

    [Tooltip("Shooting configuration block.")]
    [SerializeField] private ShootingSettings shootingSettings = new ShootingSettings();

    [Tooltip("Health and shield configuration block.")]
    [SerializeField] private PlayerHealthStatisticsSettings healthStatistics = new PlayerHealthStatisticsSettings();

    [Header("Scaling")]
    [Tooltip("Optional formula-based scaling rules applied to numeric controller properties during bake.")]
    [SerializeField] private List<PlayerStatScalingRule> scalingRules = new List<PlayerStatScalingRule>();

    [Tooltip("Internal migration flag used to avoid replaying the legacy single-element scaling upgrade on every validation.")]
    [HideInInspector]
    [SerializeField] private bool elementalPayloadScalingMigrated;

    [Header("Input Actions")]
    [Tooltip("Selected action ID for movement input.")]
    [FormerlySerializedAs("m_MoveActionId")]
    [SerializeField] private string moveActionId;

    [Tooltip("Selected action ID for look input.")]
    [FormerlySerializedAs("m_LookActionId")]
    [SerializeField] private string lookActionId;

    [Tooltip("Selected action ID for shooting input.")]
    [SerializeField] private string shootActionId;

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

    public MovementSettings MovementSettings
    {
        get
        {
            return movementSettings;
        }
    }

    public LookSettings LookSettings
    {
        get
        {
            return lookSettings;
        }
    }

    public CameraSettings CameraSettings
    {
        get
        {
            return cameraSettings;
        }
    }

    public ShootingSettings ShootingSettings
    {
        get
        {
            return shootingSettings;
        }
    }

    public PlayerHealthStatisticsSettings HealthStatistics
    {
        get
        {
            return healthStatistics;
        }
    }

    public IReadOnlyList<PlayerStatScalingRule> ScalingRules
    {
        get
        {
            return scalingRules;
        }
    }

    public string MoveActionId
    {
        get
        {
            return moveActionId;
        }
    }

    public string LookActionId
    {
        get
        {
            return lookActionId;
        }
    }

    public string ShootActionId
    {
        get
        {
            return shootActionId;
        }
    }

    #endregion

    #region Internal Properties
    internal List<PlayerStatScalingRule> ScalingRulesMutable
    {
        get
        {
            return scalingRules;
        }
    }

    internal bool ElementalPayloadScalingMigrated
    {
        get
        {
            return elementalPayloadScalingMigrated;
        }
        set
        {
            elementalPayloadScalingMigrated = value;
        }
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (movementSettings == null)
            movementSettings = new MovementSettings();

        if (lookSettings == null)
            lookSettings = new LookSettings();

        if (cameraSettings == null)
            cameraSettings = new CameraSettings();

        if (shootingSettings == null)
            shootingSettings = new ShootingSettings();

        if (healthStatistics == null)
            healthStatistics = new PlayerHealthStatisticsSettings();

        if (scalingRules == null)
            scalingRules = new List<PlayerStatScalingRule>();

        movementSettings.Validate();
        lookSettings.Validate();
        cameraSettings.Validate();
        shootingSettings.Validate();
        healthStatistics.Validate();
        PlayerControllerElementalShootingMigrationUtility.MigrateLegacyScalingRules(this);
        PlayerControllerElementalShootingMigrationUtility.PruneAppliedElementScalingRules(this);
        ValidateScalingRules();
    }
    #endregion

    #region Validation
    private void ValidateScalingRules()
    {
        for (int index = 0; index < scalingRules.Count; index++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];

            if (scalingRule != null)
                continue;

            scalingRule = new PlayerStatScalingRule();
            scalingRule.Configure(string.Empty, false, string.Empty);
            scalingRules[index] = scalingRule;
        }

        for (int index = scalingRules.Count - 1; index >= 0; index--)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];
            scalingRule.Validate();

            if (string.IsNullOrWhiteSpace(scalingRule.StatKey))
                scalingRules.RemoveAt(index);
        }
    }
    #endregion

    #region Migration
    /// <summary>
    /// Applies legacy progression health to this controller preset one time.
    /// </summary>
    /// <param name="legacyMaxHealth">Legacy max-health value extracted from old progression preset data.</param>
    /// <returns>True when this preset was modified, otherwise false.</returns>
    public bool TryApplyLegacyMaxHealth(float legacyMaxHealth)
    {
        if (healthStatistics == null)
            healthStatistics = new PlayerHealthStatisticsSettings();

        return healthStatistics.TryApplyLegacyMaxHealth(legacyMaxHealth);
    }
    #endregion
}

[Serializable]
public sealed class PlayerHealthStatisticsSettings
{
    #region Serialized Fields
    [Tooltip("Maximum and initial health assigned to the player when initialized at runtime.")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Chooses how current health is recomputed when Add Scaling changes Max Health at runtime.")]
    [SerializeField] private PlayerMaxStatAdjustmentMode maxHealthAdjustmentMode;

    [Tooltip("Maximum and initial shield reserve assigned to the player when initialized at runtime. Shield absorbs damage before health.")]
    [SerializeField] private float maxShield;

    [Tooltip("Chooses how current shield is recomputed when Add Scaling changes Max Shield at runtime.")]
    [SerializeField] private PlayerMaxStatAdjustmentMode maxShieldAdjustmentMode;

    [Tooltip("Optional invulnerability window in seconds applied after the player receives valid damage. Zero disables the grace window.")]
    [SerializeField] private float graceTimeSeconds;

    [Tooltip("Internal migration marker used to import legacy progression health only once.")]
    [SerializeField] private bool legacyHealthMigrated;
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

    public PlayerMaxStatAdjustmentMode MaxHealthAdjustmentMode
    {
        get
        {
            return maxHealthAdjustmentMode;
        }
    }

    public PlayerMaxStatAdjustmentMode MaxShieldAdjustmentMode
    {
        get
        {
            return maxShieldAdjustmentMode;
        }
    }

    public float GraceTimeSeconds
    {
        get
        {
            return graceTimeSeconds;
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// Snaps structurally invalid values back to safe defaults. Numeric ranges are surfaced as non-destructive editor
    /// warnings by the tool instead of being clamped here, except for the hard floors (max health below 1, negative
    /// reserves, negative durations) that would otherwise break the runtime contract.
    /// </summary>
    public void Validate()
    {
        if (maxHealth < 1f)
            maxHealth = 1f;

        if (maxShield < 0f)
            maxShield = 0f;

        if (graceTimeSeconds < 0f)
            graceTimeSeconds = 0f;
    }
    #endregion

    #region Migration
    /// <summary>
    /// Imports legacy max-health value from old progression presets when not already migrated.
    /// </summary>
    /// <param name="legacyMaxHealth">Legacy max-health value to apply.</param>
    /// <returns>True when this instance was modified, otherwise false.</returns>
    public bool TryApplyLegacyMaxHealth(float legacyMaxHealth)
    {
        if (legacyHealthMigrated)
            return false;

        float sanitizedLegacyMaxHealth = Mathf.Max(1f, legacyMaxHealth);
        bool changed = false;

        if (!Mathf.Approximately(maxHealth, sanitizedLegacyMaxHealth))
        {
            maxHealth = sanitizedLegacyMaxHealth;
            changed = true;
        }

        legacyHealthMigrated = true;
        return changed || legacyHealthMigrated;
    }
    #endregion
}
