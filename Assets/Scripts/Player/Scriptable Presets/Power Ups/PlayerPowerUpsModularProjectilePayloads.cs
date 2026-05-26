using System;
using UnityEngine;

#region Projectile Payloads
[Serializable]
public sealed class PowerUpProjectilePatternConeModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Projectile count spawned by this cone pattern.")]
    [SerializeField] private int projectileCount = 6;

    [Tooltip("Total cone angle in degrees.")]
    [SerializeField] private float coneAngleDegrees = 45f;

    [Tooltip("Seconds for which a Laser Beam triggered by this active cone pattern remains active.")]
    [SerializeField] private float laserDurationSeconds = 0.35f;

    [Tooltip("When projectile speed inheritance is enabled, ignore inherited player velocity on the world X axis for projectiles emitted by this module.")]
    [SerializeField] private bool ignoreInheritedPlayerVelocityX;

    [Tooltip("When projectile speed inheritance is enabled, ignore inherited player velocity on the world Z axis for projectiles emitted by this module.")]
    [SerializeField] private bool ignoreInheritedPlayerVelocityZ;
    #endregion

    #endregion

    #region Properties
    public int ProjectileCount
    {
        get
        {
            return projectileCount;
        }
    }

    public float ConeAngleDegrees
    {
        get
        {
            return coneAngleDegrees;
        }
    }

    public float LaserDurationSeconds
    {
        get
        {
            return laserDurationSeconds;
        }
    }

    public bool IgnoreInheritedPlayerVelocityX
    {
        get
        {
            return ignoreInheritedPlayerVelocityX;
        }
    }

    public bool IgnoreInheritedPlayerVelocityZ
    {
        get
        {
            return ignoreInheritedPlayerVelocityZ;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(int projectileCountValue, float coneAngleDegreesValue)
    {
        projectileCount = projectileCountValue;
        coneAngleDegrees = coneAngleDegreesValue;
        ignoreInheritedPlayerVelocityX = false;
        ignoreInheritedPlayerVelocityZ = false;
    }

    public void Configure(int projectileCountValue,
                          float coneAngleDegreesValue,
                          float laserDurationSecondsValue)
    {
        projectileCount = projectileCountValue;
        coneAngleDegrees = coneAngleDegreesValue;
        laserDurationSeconds = laserDurationSecondsValue;
        ignoreInheritedPlayerVelocityX = false;
        ignoreInheritedPlayerVelocityZ = false;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps the payload callable from shared validation paths without snapping designer-authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpProjectileScaleModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Multiplier applied to projectile visual and collision scale.")]
    [SerializeField] private float sizeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile damage.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile speed.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile max range.")]
    [SerializeField] private float rangeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile lifetime seconds.")]
    [SerializeField] private float lifetimeMultiplier = 1f;
    #endregion

    #endregion

    #region Properties
    public float SizeMultiplier
    {
        get
        {
            return sizeMultiplier;
        }
    }

    public float DamageMultiplier
    {
        get
        {
            return damageMultiplier;
        }
    }

    public float SpeedMultiplier
    {
        get
        {
            return speedMultiplier;
        }
    }

    public float RangeMultiplier
    {
        get
        {
            return rangeMultiplier;
        }
    }

    public float LifetimeMultiplier
    {
        get
        {
            return lifetimeMultiplier;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float sizeMultiplierValue,
                          float damageMultiplierValue,
                          float speedMultiplierValue,
                          float rangeMultiplierValue,
                          float lifetimeMultiplierValue)
    {
        sizeMultiplier = sizeMultiplierValue;
        damageMultiplier = damageMultiplierValue;
        speedMultiplier = speedMultiplierValue;
        rangeMultiplier = rangeMultiplierValue;
        lifetimeMultiplier = lifetimeMultiplierValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps the payload callable from shared validation paths without snapping designer-authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpProjectilePenetrationModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Penetration behavior applied to the projectile.")]
    [SerializeField] private ProjectilePenetrationMode mode = ProjectilePenetrationMode.None;

    [Tooltip("Number of extra enemy penetrations when mode is FixedHits.")]
    [SerializeField] private int maxPenetrations;
    #endregion

    #endregion

    #region Properties
    public ProjectilePenetrationMode Mode
    {
        get
        {
            return mode;
        }
    }

    public int MaxPenetrations
    {
        get
        {
            return maxPenetrations;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(ProjectilePenetrationMode modeValue, int maxPenetrationsValue)
    {
        mode = modeValue;
        maxPenetrations = maxPenetrationsValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps the payload callable from shared validation paths without snapping designer-authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpProjectileTuningModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Multiplier applied to projectile visual and collision scale.")]
    [SerializeField] private float sizeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile damage.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile speed.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile max range.")]
    [SerializeField] private float rangeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile lifetime seconds.")]
    [SerializeField] private float lifetimeMultiplier = 1f;

    [Tooltip("Penetration behavior applied to the projectile.")]
    [SerializeField] private ProjectilePenetrationMode penetrationMode = ProjectilePenetrationMode.None;

    [Tooltip("Number of extra enemy penetrations when mode is FixedHits.")]
    [SerializeField] private int maxPenetrations;

    [Header("Elemental Stacks On Hit")]
    [Tooltip("When enabled, each projectile hit applies elemental stacks using the payload below.")]
    [SerializeField] private bool applyElementalOnHit;

    [Tooltip("Elemental effect payload applied on projectile hit when elemental stacks are enabled.")]
    [SerializeField] private ElementalEffectDefinitionData elementalEffectData = new ElementalEffectDefinitionData();

    [Tooltip("Elemental stacks applied on each projectile hit when elemental stacks are enabled.")]
    [SerializeField] private float elementalStacksPerHit = 1f;
    #endregion

    #endregion

    #region Properties
    public float SizeMultiplier
    {
        get
        {
            return sizeMultiplier;
        }
    }

    public float DamageMultiplier
    {
        get
        {
            return damageMultiplier;
        }
    }

    public float SpeedMultiplier
    {
        get
        {
            return speedMultiplier;
        }
    }

    public float RangeMultiplier
    {
        get
        {
            return rangeMultiplier;
        }
    }

    public float LifetimeMultiplier
    {
        get
        {
            return lifetimeMultiplier;
        }
    }

    public ProjectilePenetrationMode PenetrationMode
    {
        get
        {
            return penetrationMode;
        }
    }

    public int MaxPenetrations
    {
        get
        {
            return maxPenetrations;
        }
    }

    public bool ApplyElementalOnHit
    {
        get
        {
            return applyElementalOnHit;
        }
    }

    public ElementalEffectDefinitionData ElementalEffectData
    {
        get
        {
            return elementalEffectData;
        }
    }

    public float ElementalStacksPerHit
    {
        get
        {
            return elementalStacksPerHit;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float sizeMultiplierValue,
                          float damageMultiplierValue,
                          float speedMultiplierValue,
                          float rangeMultiplierValue,
                          float lifetimeMultiplierValue,
                          ProjectilePenetrationMode penetrationModeValue,
                          int maxPenetrationsValue,
                          bool applyElementalOnHitValue = false,
                          ElementalEffectDefinitionData elementalEffectDataValue = null,
                          float elementalStacksPerHitValue = 0f)
    {
        sizeMultiplier = sizeMultiplierValue;
        damageMultiplier = damageMultiplierValue;
        speedMultiplier = speedMultiplierValue;
        rangeMultiplier = rangeMultiplierValue;
        lifetimeMultiplier = lifetimeMultiplierValue;
        penetrationMode = penetrationModeValue;
        maxPenetrations = maxPenetrationsValue;
        applyElementalOnHit = applyElementalOnHitValue;
        elementalEffectData = elementalEffectDataValue != null ? elementalEffectDataValue : new ElementalEffectDefinitionData();
        elementalStacksPerHit = elementalStacksPerHitValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (elementalEffectData == null)
            elementalEffectData = new ElementalEffectDefinitionData();

        elementalEffectData.Validate();
    }
    #endregion

    #endregion
}
#endregion
