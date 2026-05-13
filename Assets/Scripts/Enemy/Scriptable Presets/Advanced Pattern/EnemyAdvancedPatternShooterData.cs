using System;
using UnityEngine;

/// <summary>
/// Contains projectile tuning values for Shooter modules.
/// </summary>
[Serializable]
public sealed class EnemyShooterProjectilePayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Projectile distribution used by this shooter activation. Forward Spread aims toward the target, while Radial Burst emits an even full-circle ring.")]
    [SerializeField] private EnemyShooterShotPattern shotPattern = EnemyShooterShotPattern.ForwardSpread;

    [Tooltip("Projectile count emitted each time this shooter module fires.")]
    [SerializeField] private int projectilesPerShot = 1;

    [Tooltip("Total spread angle in degrees used when emitting multiple projectiles.")]
    [SerializeField] private float spreadAngleDegrees;

    [Tooltip("Projectile travel speed in meters per second.")]
    [SerializeField] private float projectileSpeed = 14f;

    [Tooltip("Flat projectile damage applied on hit.")]
    [SerializeField] private float projectileDamage = 6f;

    [Tooltip("Maximum projectile travel distance. 0 keeps distance unlimited.")]
    [SerializeField] private float projectileRange = 28f;

    [Tooltip("Maximum projectile lifetime in seconds. 0 keeps lifetime unlimited.")]
    [SerializeField] private float projectileLifetime = 4f;

    [Tooltip("Additional projectile impact radius added to base collision size.")]
    [SerializeField] private float projectileExplosionRadius;

    [Tooltip("Uniform projectile scale multiplier applied at spawn.")]
    [SerializeField] private float projectileScaleMultiplier = 1f;

    [Tooltip("Projectile penetration behavior for this shooter module.")]
    [SerializeField] private ProjectilePenetrationMode penetrationMode = ProjectilePenetrationMode.None;

    [Tooltip("Maximum extra penetrations when penetration mode is FixedHits or DamageBased.")]
    [SerializeField] private int maxPenetrations;

    [Tooltip("When enabled, projectiles inherit owner velocity similarly to player projectiles.")]
    [SerializeField] private bool inheritShooterSpeed;
    #endregion

    #endregion

    #region Properties
    public EnemyShooterShotPattern ShotPattern
    {
        get
        {
            return shotPattern;
        }
    }

    public int ProjectilesPerShot
    {
        get
        {
            return projectilesPerShot;
        }
    }

    public float SpreadAngleDegrees
    {
        get
        {
            return spreadAngleDegrees;
        }
    }

    public float ProjectileSpeed
    {
        get
        {
            return projectileSpeed;
        }
    }

    public float ProjectileDamage
    {
        get
        {
            return projectileDamage;
        }
    }

    public float ProjectileRange
    {
        get
        {
            return projectileRange;
        }
    }

    public float ProjectileLifetime
    {
        get
        {
            return projectileLifetime;
        }
    }

    public float ProjectileExplosionRadius
    {
        get
        {
            return projectileExplosionRadius;
        }
    }

    public float ProjectileScaleMultiplier
    {
        get
        {
            return projectileScaleMultiplier;
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

    public bool InheritShooterSpeed
    {
        get
        {
            return inheritShooterSpeed;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures Shooter projectile payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains runtime projectile prefab and pool tuning for Shooter modules.
/// </summary>
[Serializable]
public sealed class EnemyShooterRuntimeProjectilePayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Projectile prefab used by shooter runtime. Required for projectile spawning.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Initial projectile pool capacity per shooter entity when this runtime payload is selected.")]
    [SerializeField] private int poolInitialCapacity = 24;

    [Tooltip("Projectile pool expansion batch size when runtime demand exceeds available pooled instances.")]
    [SerializeField] private int poolExpandBatch = 8;
    #endregion

    #endregion

    #region Properties
    public GameObject ProjectilePrefab
    {
        get
        {
            return projectilePrefab;
        }
    }

    public int PoolInitialCapacity
    {
        get
        {
            return poolInitialCapacity;
        }
    }

    public int PoolExpandBatch
    {
        get
        {
            return poolExpandBatch;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Configures runtime projectile payload values in one call.
    /// </summary>
    /// <param name="projectilePrefabValue">Projectile prefab reference.</param>
    /// <param name="poolInitialCapacityValue">Initial pool capacity value.</param>
    /// <param name="poolExpandBatchValue">Pool expansion batch value.</param>
    public void Configure(GameObject projectilePrefabValue, int poolInitialCapacityValue, int poolExpandBatchValue)
    {
        projectilePrefab = projectilePrefabValue;
        poolInitialCapacity = poolInitialCapacityValue;
        poolExpandBatch = poolExpandBatchValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures Shooter runtime projectile payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains elemental tuning values for Shooter modules.
/// </summary>
[Serializable]
public sealed class EnemyShooterElementalPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, this shooter module applies elemental stacks to the player on hit.")]
    [SerializeField] private bool enableElementalDamage;

    [Tooltip("Elemental payload definition used when elemental damage is enabled.")]
    [SerializeField] private ElementalEffectDefinitionData effectData = new ElementalEffectDefinitionData();

    [Tooltip("Elemental stacks applied to player per projectile hit.")]
    [SerializeField] private float stacksPerHit = 1f;
    #endregion

    #endregion

    #region Properties
    public bool EnableElementalDamage
    {
        get
        {
            return enableElementalDamage;
        }
    }

    public ElementalEffectDefinitionData EffectData
    {
        get
        {
            return effectData;
        }
    }

    public float StacksPerHit
    {
        get
        {
            return stacksPerHit;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures Shooter elemental payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
        if (effectData == null)
            effectData = new ElementalEffectDefinitionData();
    }
    #endregion

    #endregion
}

/// <summary>
/// Groups Shooter cadence, range, aim and nested payloads.
/// </summary>
[Serializable]
public sealed class EnemyShooterModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Aim mode used when deciding projectile direction.")]
    [SerializeField] private EnemyShooterAimPolicy aimPolicy = EnemyShooterAimPolicy.LockOnFireStart;

    [Tooltip("Movement policy applied while this module is firing.")]
    [SerializeField] private EnemyShooterMovementPolicy movementPolicy = EnemyShooterMovementPolicy.KeepMoving;

    [Tooltip("Seconds between burst starts for this shooter module.")]
    [SerializeField] private float fireInterval = 1f;

    [Tooltip("Projectiles groups fired for each activation. 1 means single shot.")]
    [SerializeField] private int burstCount = 1;

    [Tooltip("Extra aim windup in seconds applied before the first shot of each burst. 0 fires immediately.")]
    [SerializeField] private float aimWindupSeconds = 0.25f;

    [Tooltip("Minimum seconds this enemy must remain stopped before the first shot when Movement Policy is Stop While Aiming.")]
    [SerializeField] private float preFireStopSeconds = 0.25f;

    [Tooltip("Minimum seconds this enemy remains stopped after the final shot when Movement Policy is Stop While Aiming.")]
    [SerializeField] private float postFireStopSeconds;

    [Tooltip("Seconds between shots inside the same burst.")]
    [SerializeField] private float intraBurstDelay = 0.08f;

    [Tooltip("When enabled, firing is blocked while the player is too close.")]
    [SerializeField] private bool useMinimumRange;

    [Tooltip("Minimum distance required to allow firing when minimum range gate is enabled.")]
    [SerializeField] private float minimumRange = 2f;

    [Tooltip("When enabled, firing is blocked while the player is too far.")]
    [SerializeField] private bool useMaximumRange;

    [Tooltip("Maximum distance allowed to fire when maximum range gate is enabled.")]
    [SerializeField] private float maximumRange = 20f;

    [Tooltip("Projectile tuning payload for this shooter module.")]
    [SerializeField] private EnemyShooterProjectilePayload projectile = new EnemyShooterProjectilePayload();

    [Tooltip("Runtime projectile prefab and pool payload used by this shooter module.")]
    [SerializeField] private EnemyShooterRuntimeProjectilePayload runtimeProjectile = new EnemyShooterRuntimeProjectilePayload();

    [Tooltip("Optional elemental tuning payload for this shooter module.")]
    [SerializeField] private EnemyShooterElementalPayload elemental = new EnemyShooterElementalPayload();
    #endregion

    #endregion

    #region Properties
    public EnemyShooterAimPolicy AimPolicy
    {
        get
        {
            return aimPolicy;
        }
    }

    public EnemyShooterMovementPolicy MovementPolicy
    {
        get
        {
            return movementPolicy;
        }
    }

    public float FireInterval
    {
        get
        {
            return fireInterval;
        }
    }

    public int BurstCount
    {
        get
        {
            return burstCount;
        }
    }

    public float AimWindupSeconds
    {
        get
        {
            return aimWindupSeconds;
        }
    }

    public float PreFireStopSeconds
    {
        get
        {
            return preFireStopSeconds;
        }
    }

    public float PostFireStopSeconds
    {
        get
        {
            return postFireStopSeconds;
        }
    }

    public float IntraBurstDelay
    {
        get
        {
            return intraBurstDelay;
        }
    }

    public bool UseMinimumRange
    {
        get
        {
            return useMinimumRange;
        }
    }

    public float MinimumRange
    {
        get
        {
            return minimumRange;
        }
    }

    public bool UseMaximumRange
    {
        get
        {
            return useMaximumRange;
        }
    }

    public float MaximumRange
    {
        get
        {
            return maximumRange;
        }
    }

    public EnemyShooterProjectilePayload Projectile
    {
        get
        {
            return projectile;
        }
    }

    public EnemyShooterRuntimeProjectilePayload RuntimeProjectile
    {
        get
        {
            return runtimeProjectile;
        }
    }

    public EnemyShooterElementalPayload Elemental
    {
        get
        {
            return elemental;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures Shooter module nested payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
        if (projectile == null)
            projectile = new EnemyShooterProjectilePayload();

        if (runtimeProjectile == null)
            runtimeProjectile = new EnemyShooterRuntimeProjectilePayload();

        if (elemental == null)
            elemental = new EnemyShooterElementalPayload();

        projectile.Validate();
        runtimeProjectile.Validate();
        elemental.Validate();
    }
    #endregion

    #endregion
}
