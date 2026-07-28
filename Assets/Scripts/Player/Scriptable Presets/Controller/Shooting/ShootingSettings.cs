using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Stores trigger, prefab and pooled-shooting settings used by one player controller preset.
/// </summary>
[Serializable]
public sealed class ShootingSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Shooting Trigger")]
    [Tooltip("Defines whether shooting is toggled on or off, fired once per press, or kept active while the input is held.")]
    [SerializeField] private ShootingTriggerMode triggerMode = ShootingTriggerMode.AutomaticToggle;

    [Tooltip("When enabled, projectile movement keeps inheriting the shooter's horizontal velocity after spawn.")]
    [SerializeField] private bool projectilesInheritPlayerSpeed;

    [Header("Shooting References")]
    [Tooltip("Projectile prefab to spawn at runtime when shooting.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Local offset from the weapon reference set on PlayerAuthoring, or from the player transform when no reference is assigned.")]
    [SerializeField] private Vector3 shootOffset = new Vector3(0f, 0.5f, 0.8f);

    [Header("Shooting Values")]
    [Tooltip("Numeric shooting tuning values.")]
    [SerializeField] private ShootingValues values = new ShootingValues();

    [Header("Conditional Weapon Switches")]
    [Tooltip("Stat-driven weapon switch entries evaluated independently from the Switch Weapon power-up. s can opt one or more entries above the equipped power-up per-entry.")]
    [SerializeField] private PlayerConditionalWeaponSwitchSettings conditionalWeaponSwitches = new PlayerConditionalWeaponSwitchSettings();

    [Header("Object Pool")]
    [Tooltip("Number of projectiles pre-created when the shooter pool initializes.")]
    [SerializeField] private int initialPoolCapacity = 512;

    [Tooltip("Number of projectiles created when the pool needs expansion.")]
    [SerializeField] private int poolExpandBatch = 128;
    #endregion

    #endregion

    #region Properties
    public ShootingTriggerMode TriggerMode
    {
        get
        {
            return triggerMode;
        }
    }

    public GameObject ProjectilePrefab
    {
        get
        {
            return projectilePrefab;
        }
    }

    public bool ProjectilesInheritPlayerSpeed
    {
        get
        {
            return projectilesInheritPlayerSpeed;
        }
    }

    public Vector3 ShootOffset
    {
        get
        {
            return shootOffset;
        }
    }

    public ShootingValues Values
    {
        get
        {
            return values;
        }
    }

    public PlayerConditionalWeaponSwitchSettings ConditionalWeaponSwitches
    {
        get
        {
            return conditionalWeaponSwitches;
        }
    }

    public int InitialPoolCapacity
    {
        get
        {
            return initialPoolCapacity;
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

    #region Validation
    /// <summary>
    /// Validates the authored shooting container and keeps nested blocks initialized before bake or editor rendering.
    /// </summary>
    public void Validate()
    {
        if (values == null)
            values = new ShootingValues();

        if (conditionalWeaponSwitches == null)
            conditionalWeaponSwitches = new PlayerConditionalWeaponSwitchSettings();

        values.Validate();
        conditionalWeaponSwitches.Validate();

        if (initialPoolCapacity < 0)
            initialPoolCapacity = 0;

        if (poolExpandBatch < 1)
            poolExpandBatch = 1;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores numeric projectile tuning, default elemental payload slots and per-element bullet behaviour blocks.
/// </summary>
[Serializable]
public sealed class ShootingValues
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Projectile speed in world units per second.")]
    [SerializeField] private float shootSpeed = 24f;

    [Tooltip("Number of projectiles fired per second.")]
    [SerializeField] private float rateOfFire = 8f;

    [Tooltip("Additional flat area radius applied around projectile impact checks.")]
    [SerializeField] private float explosionRadius;

    [Tooltip("Optional maximum travel distance before despawn. Zero or negative disables range despawn.")]
    [SerializeField] private float range;

    [Tooltip("Optional maximum lifetime in seconds before despawn. Zero or negative disables lifetime despawn.")]
    [SerializeField] private float lifetime;

    [Tooltip("Damage applied by each projectile.")]
    [SerializeField] private float damage = 1f;

    [Tooltip("Element slots emitted by default player bullets. Increase or reduce the slot count as needed, and set unused slots to None.")]
    [SerializeField] private PlayerProjectileAppliedElement[] appliedElements = new PlayerProjectileAppliedElement[PlayerControllerElementalShootingMigrationUtility.DefaultAppliedElementSlotCount];

    [Tooltip("Per-element behaviour blocks used whenever the matching element is present in Applied Elements.")]
    [SerializeField] private ElementBulletSettingsByElement elementBehaviours = new ElementBulletSettingsByElement();

    [Tooltip("Legacy single-slot element kept only to migrate existing controller presets.")]
    [HideInInspector]
    [FormerlySerializedAs("appliedElement")]
    [SerializeField] private PlayerProjectileAppliedElement legacyAppliedElement = PlayerProjectileAppliedElement.None;

    [Tooltip("Legacy generic element behaviour kept only to migrate existing controller presets.")]
    [HideInInspector]
    [FormerlySerializedAs("elementBulletSettings")]
    [SerializeField] private ElementBulletSettings legacyElementBulletSettings = new ElementBulletSettings();

    [Tooltip("Internal migration flag used to avoid replaying the single-element to multi-element upgrade on every validation.")]
    [HideInInspector]
    [SerializeField] private bool elementalPayloadDataMigrated;

    [Tooltip("Multiplier applied to projectile base scale before passive or active power-up modifiers.")]
    [SerializeField] private float projectileSizeMultiplier = 1f;

    [Tooltip("Penetration rule applied by default to all player-fired projectiles.")]
    [SerializeField] private ProjectilePenetrationMode penetrationMode = ProjectilePenetrationMode.None;

    [Tooltip("Maximum extra enemies a projectile can pass through after the first valid hit.")]
    [SerializeField] private int maxPenetrations;

    [Tooltip("Default knockback payload applied by base player projectiles when they hit enemies.")]
    [SerializeField] private ProjectileKnockbackSettings knockback = new ProjectileKnockbackSettings();
    #endregion

    #endregion

    #region Properties
    public float ShootSpeed
    {
        get
        {
            return shootSpeed;
        }
    }

    public float RateOfFire
    {
        get
        {
            return rateOfFire;
        }
    }

    public float ExplosionRadius
    {
        get
        {
            return explosionRadius;
        }
    }

    public float Range
    {
        get
        {
            return range;
        }
    }

    public float Lifetime
    {
        get
        {
            return lifetime;
        }
    }

    public float Damage
    {
        get
        {
            return damage;
        }
    }

    public IReadOnlyList<PlayerProjectileAppliedElement> AppliedElements
    {
        get
        {
            return appliedElements;
        }
    }

    public ElementBulletSettingsByElement ElementBehaviours
    {
        get
        {
            return elementBehaviours;
        }
    }

    public float ProjectileSizeMultiplier
    {
        get
        {
            return projectileSizeMultiplier;
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

    public ProjectileKnockbackSettings Knockback
    {
        get
        {
            return knockback;
        }
    }
    #endregion

    #region Methods

    #region Internal API
    internal PlayerProjectileAppliedElement[] AppliedElementsMutable
    {
        get
        {
            return appliedElements;
        }
        set
        {
            appliedElements = value;
        }
    }

    internal ElementBulletSettingsByElement ElementBehavioursMutable
    {
        get
        {
            return elementBehaviours;
        }
        set
        {
            elementBehaviours = value;
        }
    }

    internal PlayerProjectileAppliedElement LegacyAppliedElement
    {
        get
        {
            return legacyAppliedElement;
        }
    }

    internal ElementBulletSettings LegacyElementBulletSettings
    {
        get
        {
            return legacyElementBulletSettings;
        }
    }

    internal bool ElementalPayloadDataMigrated
    {
        get
        {
            return elementalPayloadDataMigrated;
        }
        set
        {
            elementalPayloadDataMigrated = value;
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates authored projectile values, guarantees the new multi-element data shape and preserves migrated legacy data.
    /// </summary>
    public void Validate()
    {
        if (elementBehaviours == null)
            elementBehaviours = new ElementBulletSettingsByElement();

        if (legacyElementBulletSettings == null)
            legacyElementBulletSettings = new ElementBulletSettings();

        appliedElements = PlayerControllerElementalShootingMigrationUtility.EnsureAppliedElementSlots(appliedElements);
        PlayerControllerElementalShootingMigrationUtility.MigrateLegacyElementalPayloadAuthoring(this);

        if (knockback == null)
            knockback = new ProjectileKnockbackSettings();

        elementBehaviours.Validate();
        knockback.Validate();

        if (shootSpeed < 0f)
            shootSpeed = 0f;

        if (rateOfFire < 0f)
            rateOfFire = 0f;

        if (explosionRadius < 0f)
            explosionRadius = 0f;

        if (damage < 0f)
            damage = 0f;

        if (projectileSizeMultiplier < 0.01f)
            projectileSizeMultiplier = 0.01f;

        if (maxPenetrations < 0)
            maxPenetrations = 0;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the default knockback payload emitted by base player projectiles.
/// </summary>
[Serializable]
public sealed class ProjectileKnockbackSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, default player projectiles apply knockback to valid enemy hits.")]
    [SerializeField] private bool enabled;

    [Tooltip("Planar push strength converted into knockback velocity when a projectile hit resolves.")]
    [SerializeField] private float strength = 6f;

    [Tooltip("Seconds over which the knockback velocity decays back to zero.")]
    [SerializeField] private float durationSeconds = 0.18f;

    [Tooltip("Chooses whether the push follows projectile travel or the hit-to-target direction.")]
    [SerializeField] private ProjectileKnockbackDirectionMode directionMode = ProjectileKnockbackDirectionMode.ProjectileTravel;

    [Tooltip("Controls how repeated hits combine with a knockback state already active on the same enemy.")]
    [SerializeField] private ProjectileKnockbackStackingMode stackingMode = ProjectileKnockbackStackingMode.Replace;
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

    public float Strength
    {
        get
        {
            return strength;
        }
    }

    public float DurationSeconds
    {
        get
        {
            return durationSeconds;
        }
    }

    public ProjectileKnockbackDirectionMode DirectionMode
    {
        get
        {
            return directionMode;
        }
    }

    public ProjectileKnockbackStackingMode StackingMode
    {
        get
        {
            return stackingMode;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Validates the authored knockback payload container and preserves the current authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
