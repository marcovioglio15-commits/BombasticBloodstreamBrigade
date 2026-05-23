using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BouncingProjectilesPassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Bouncing Projectiles")]
    [Tooltip("Maximum wall bounces allowed for each spawned projectile.")]
    [SerializeField] private int maxBounces = 2;

    [Tooltip("Speed percentage delta applied each bounce. Example: -20 means 20% slower, 15 means 15% faster.")]
    [SerializeField] private float speedPercentChangePerBounce = -5f;

    [Tooltip("Minimum resulting speed multiplier after bounce scaling.")]
    [SerializeField] private float minimumSpeedMultiplierAfterBounce = 0.1f;

    [Tooltip("Maximum resulting speed multiplier after bounce scaling.")]
    [SerializeField] private float maximumSpeedMultiplierAfterBounce = 3f;
    #endregion

    #endregion

    #region Properties
    public int MaxBounces
    {
        get
        {
            return maxBounces;
        }
    }

    public float SpeedPercentChangePerBounce
    {
        get
        {
            return speedPercentChangePerBounce;
        }
    }

    public float MinimumSpeedMultiplierAfterBounce
    {
        get
        {
            return minimumSpeedMultiplierAfterBounce;
        }
    }

    public float MaximumSpeedMultiplierAfterBounce
    {
        get
        {
            return maximumSpeedMultiplierAfterBounce;
        }
    }
    #endregion

    #region Methods

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
public sealed class SplittingProjectilesPassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Splitting Projectiles")]
    [Tooltip("Event used to trigger projectile split generation.")]
    [SerializeField] private ProjectileSplitTriggerMode triggerMode = ProjectileSplitTriggerMode.OnEnemyKilled;

    [Tooltip("How split projectile directions are generated.")]
    [SerializeField] private ProjectileSplitDirectionMode directionMode = ProjectileSplitDirectionMode.Uniform;

    [Tooltip("Amount of split projectiles spawned per split event when Uniform mode is active.")]
    [SerializeField] private int splitProjectileCount = 2;

    [Tooltip("Angular offset in degrees used as rotation start for uniform split distribution.")]
    [SerializeField] private float splitOffsetDegrees;

    [Tooltip("Custom split angles in degrees relative to impact direction when Custom Angles mode is active.")]
    [SerializeField] private List<float> customAnglesDegrees = new List<float>
    {
        -20f,
        20f
    };

    [Tooltip("Damage percentage for each split projectile relative to original. 100 means equal damage.")]
    [SerializeField] private float splitDamagePercentFromOriginal = 50f;

    [Tooltip("Size percentage for each split projectile relative to original. 100 means equal size.")]
    [SerializeField] private float splitSizePercentFromOriginal = 75f;

    [Tooltip("Speed percentage for each split projectile relative to original. 100 means equal speed.")]
    [SerializeField] private float splitSpeedPercentFromOriginal = 85f;

    [Tooltip("Lifetime percentage for each split projectile relative to original. 100 means equal lifetime.")]
    [SerializeField] private float splitLifetimePercentFromOriginal = 65f;
    #endregion

    #endregion

    #region Properties
    public ProjectileSplitDirectionMode DirectionMode
    {
        get
        {
            return directionMode;
        }
    }

    public ProjectileSplitTriggerMode TriggerMode
    {
        get
        {
            return triggerMode;
        }
    }

    public int SplitProjectileCount
    {
        get
        {
            return splitProjectileCount;
        }
    }

    public float SplitOffsetDegrees
    {
        get
        {
            return splitOffsetDegrees;
        }
    }

    public IReadOnlyList<float> CustomAnglesDegrees
    {
        get
        {
            return customAnglesDegrees;
        }
    }

    public float SplitDamagePercentFromOriginal
    {
        get
        {
            return splitDamagePercentFromOriginal;
        }
    }

    public float SplitSizePercentFromOriginal
    {
        get
        {
            return splitSizePercentFromOriginal;
        }
    }

    public float SplitSpeedPercentFromOriginal
    {
        get
        {
            return splitSpeedPercentFromOriginal;
        }
    }

    public float SplitLifetimePercentFromOriginal
    {
        get
        {
            return splitLifetimePercentFromOriginal;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (customAnglesDegrees == null)
            customAnglesDegrees = new List<float>();
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class ExplosionPassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Passive Explosion")]
    [Tooltip("Event that triggers this explosion passive.")]
    [SerializeField] private PassiveExplosionTriggerMode triggerMode = PassiveExplosionTriggerMode.Periodic;

    [Tooltip("Minimum seconds required between two explosion triggers in any trigger mode.")]
    [SerializeField] private float cooldownSeconds = 2f;

    [Tooltip("Explosion radius in meters.")]
    [SerializeField] private float radius = 4f;

    [Tooltip("Explosion damage applied to valid enemies.")]
    [SerializeField] private float damage = 20f;

    [Tooltip("When enabled, all enemies inside radius are damaged. Otherwise only closest enemy is damaged.")]
    [SerializeField] private bool affectAllEnemiesInRadius = true;

    [Tooltip("Optional local offset applied to explosion center from trigger origin.")]
    [SerializeField] private Vector3 triggerOffset = Vector3.zero;

    [Header("Explosion VFX (Optional)")]
    [Tooltip("Optional VFX prefab spawned at explosion center.")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Tooltip("When enabled, explosion VFX scale is multiplied by radius.")]
    [SerializeField] private bool scaleVfxToRadius = true;

    [Tooltip("Additional scale multiplier applied to explosion VFX.")]
    [SerializeField] private float vfxScaleMultiplier = 1f;
    #endregion

    #endregion

    #region Properties
    public PassiveExplosionTriggerMode TriggerMode
    {
        get
        {
            return triggerMode;
        }
    }

    public float CooldownSeconds
    {
        get
        {
            return cooldownSeconds;
        }
    }

    public float Radius
    {
        get
        {
            return radius;
        }
    }

    public float Damage
    {
        get
        {
            return damage;
        }
    }

    public bool AffectAllEnemiesInRadius
    {
        get
        {
            return affectAllEnemiesInRadius;
        }
    }

    public Vector3 TriggerOffset
    {
        get
        {
            return triggerOffset;
        }
    }

    public GameObject ExplosionVfxPrefab
    {
        get
        {
            return explosionVfxPrefab;
        }
    }

    public bool ScaleVfxToRadius
    {
        get
        {
            return scaleVfxToRadius;
        }
    }

    public float VfxScaleMultiplier
    {
        get
        {
            return vfxScaleMultiplier;
        }
    }
    #endregion

    #region Methods

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
public sealed class ElementalTrailPassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Elemental Trail")]
    [Tooltip("Elemental effect payload applied by trail segments.")]
    [SerializeField] private ElementalEffectDefinitionData effectData = new ElementalEffectDefinitionData();

    [Tooltip("Seconds each trail segment remains active in the world.")]
    [SerializeField] private float trailSegmentLifetimeSeconds = 1.5f;

    [Tooltip("Distance in meters required to spawn the next segment.")]
    [SerializeField] private float trailSpawnDistance = 0.6f;

    [Tooltip("Fallback time interval in seconds to spawn segment even with low movement.")]
    [SerializeField] private float trailSpawnIntervalSeconds = 0.1f;

    [Tooltip("Radius in meters used by each trail segment to detect enemies.")]
    [SerializeField] private float trailRadius = 1.2f;

    [Tooltip("Maximum active trail segments per player.")]
    [SerializeField] private int maxActiveSegmentsPerPlayer = 30;

    [Tooltip("Stacks applied on each trail damage tick.")]
    [SerializeField] private float stacksPerTick = 1f;

    [Tooltip("Seconds between two stack applications while an enemy remains inside trail area.")]
    [SerializeField] private float applyIntervalSeconds = 0.2f;

    [Tooltip("Local offset applied to the attached trail VFX position relative to player center.")]
    [SerializeField] private Vector3 trailAttachedVfxOffset = new Vector3(0f, 0.08f, 0f);
    #endregion

    #endregion

    #region Properties
    public ElementalEffectDefinitionData EffectData
    {
        get
        {
            return effectData;
        }
    }

    public float TrailSegmentLifetimeSeconds
    {
        get
        {
            return trailSegmentLifetimeSeconds;
        }
    }

    public float TrailSpawnDistance
    {
        get
        {
            return trailSpawnDistance;
        }
    }

    public float TrailSpawnIntervalSeconds
    {
        get
        {
            return trailSpawnIntervalSeconds;
        }
    }

    public float TrailRadius
    {
        get
        {
            return trailRadius;
        }
    }

    public int MaxActiveSegmentsPerPlayer
    {
        get
        {
            return maxActiveSegmentsPerPlayer;
        }
    }

    public float StacksPerTick
    {
        get
        {
            return stacksPerTick;
        }
    }

    public float ApplyIntervalSeconds
    {
        get
        {
            return applyIntervalSeconds;
        }
    }

    public Vector3 TrailAttachedVfxOffset
    {
        get
        {
            return trailAttachedVfxOffset;
        }
    }

    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (effectData == null)
            effectData = new ElementalEffectDefinitionData();

        effectData.Validate();

    }
    #endregion

    #endregion
}
