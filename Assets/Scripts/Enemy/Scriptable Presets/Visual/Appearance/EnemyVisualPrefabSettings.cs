using System;
using UnityEngine;

/// <summary>
/// Stores one enemy projectile VFX event shared by player-hit impact and projectile death occasions.
/// </summary>
[Serializable]
public sealed class EnemyProjectileVfxEventSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, this enemy projectile VFX event can spawn when its runtime occasion is reached.")]
    [SerializeField]
    private bool enabled;

    [Tooltip("One-shot VFX prefab spawned for this enemy projectile event.")]
    [SerializeField]
    private GameObject vfxPrefab;

    [Tooltip("Projectile-local offset applied at the projectile pose. The offset scales with the current projectile size.")]
    [SerializeField]
    private Vector3 spawnOffset = Vector3.zero;

    [Tooltip("Uniform VFX scale multiplier applied on top of the current projectile size.")]
    [SerializeField]
    private float scaleMultiplier = 1f;

    [Tooltip("Lifetime in seconds before the spawned one-shot VFX returns to the managed VFX pool.")]
    [SerializeField]
    private float lifetimeSeconds = 0.5f;
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

    public GameObject VfxPrefab
    {
        get
        {
            return vfxPrefab;
        }
    }

    public Vector3 SpawnOffset
    {
        get
        {
            return spawnOffset;
        }
    }

    public float ScaleMultiplier
    {
        get
        {
            return scaleMultiplier;
        }
    }

    public float LifetimeSeconds
    {
        get
        {
            return lifetimeSeconds;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid authored values without mutating the visual preset.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    /// <param name="eventLabel">User-facing enemy projectile VFX event label.</param>
    /// <param name="fallbackPrefab">Optional fallback prefab accepted when this event has no direct assignment.</param>
    public void Validate(string ownerAssetName, string eventLabel, GameObject fallbackPrefab)
    {
        if (!enabled)
            return;

        if (vfxPrefab == null && fallbackPrefab == null)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - {1}: event is enabled but no VFX prefab is assigned.", ownerAssetName, eventLabel));

        if (!IsFinite(spawnOffset))
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - {1}: spawn offset contains an invalid numeric value.", ownerAssetName, eventLabel));

        if (!IsFinite(scaleMultiplier) || scaleMultiplier <= 0f)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - {1}: scale multiplier should be finite and greater than zero.", ownerAssetName, eventLabel));

        if (!IsFinite(lifetimeSeconds) || lifetimeSeconds <= 0f)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - {1}: lifetime should be finite and greater than zero.", ownerAssetName, eventLabel));
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether every vector component is finite.
    /// </summary>
    /// <param name="value">Vector value to inspect.</param>
    /// <returns>True when every component is finite.</returns>
    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    /// <summary>
    /// Checks whether one floating-point value is finite.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores enemy projectile-death VFX settings for natural expiry and non-bouncing terminal wall impacts.
/// </summary>
[Serializable]
public sealed class EnemyProjectileDeathVfxSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("VFX event spawned when an enemy projectile reaches its configured range or lifetime without hitting the player.")]
    [SerializeField]
    private EnemyProjectileVfxEventSettings rangeOrLifetime = new EnemyProjectileVfxEventSettings();

    [Tooltip("VFX event spawned when an enemy projectile despawns on a wall after all configured projectile bounces are unavailable, provided it has not hit the player.")]
    [SerializeField]
    private EnemyProjectileVfxEventSettings terminalWallHit = new EnemyProjectileVfxEventSettings();
    #endregion

    #endregion

    #region Properties
    public EnemyProjectileVfxEventSettings RangeOrLifetime
    {
        get
        {
            return rangeOrLifetime;
        }
    }

    public EnemyProjectileVfxEventSettings TerminalWallHit
    {
        get
        {
            return terminalWallHit;
        }
    }

    public bool HasAnyPrefab
    {
        get
        {
            return rangeOrLifetime != null && rangeOrLifetime.VfxPrefab != null ||
                   terminalWallHit != null && terminalWallHit.VfxPrefab != null;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports missing nested event settings and invalid authored values without snapping them.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (rangeOrLifetime == null)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - Bullet Death VFX: Range Or Lifetime settings are missing.", ownerAssetName));
        else
            rangeOrLifetime.Validate(ownerAssetName, "Bullet Death VFX / Range Or Lifetime", null);

        if (terminalWallHit == null)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - Bullet Death VFX: Terminal Wall Hit settings are missing.", ownerAssetName));
        else
            terminalWallHit.Validate(ownerAssetName,
                                     "Bullet Death VFX / Terminal Wall Hit",
                                     rangeOrLifetime != null ? rangeOrLifetime.VfxPrefab : null);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores prefab references and paint color metadata used by one enemy type.
/// </summary>
[Serializable]
public sealed class EnemyVisualPrefabSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enemy prefab associated with this enemy type. This prefab must contain EnemyAuthoring.")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("Optional one-shot VFX prefab spawned every time this enemy receives a projectile hit.")]
    [SerializeField] private GameObject hitVfxPrefab;

    [Tooltip("World-space offset added to the resolved impact position before spawning the enemy hit VFX.")]
    [SerializeField] private Vector3 hitVfxSpawnOffset;

    [Tooltip("Lifetime in seconds assigned to each spawned hit VFX instance.")]
    [SerializeField] private float hitVfxLifetimeSeconds = 0.35f;

    [Tooltip("Uniform scale multiplier applied to the spawned hit VFX instance.")]
    [SerializeField] private float hitVfxScaleMultiplier = 1f;

    [Tooltip("Optional one-shot VFX prefab spawned when this enemy appears or when its spawn warning starts.")]
    [SerializeField] private GameObject spawnVfxPrefab;

    [Tooltip("Controls whether the optional spawn VFX is requested at activation time or together with the spawn warning.")]
    [SerializeField] private EnemySpawnVfxTiming spawnVfxTiming = EnemySpawnVfxTiming.OnSpawn;

    [Tooltip("World-space offset added to the reserved or activated enemy spawn position before spawning the optional spawn VFX.")]
    [SerializeField] private Vector3 spawnVfxSpawnOffset;

    [Tooltip("Lifetime in seconds assigned to an On Spawn optional spawn VFX instance. Warning-timed spawn VFX use the resolved spawn-warning lead time instead.")]
    [SerializeField] private float spawnVfxLifetimeSeconds = 0.5f;

    [Tooltip("Uniform scale multiplier applied to each optional spawn VFX instance.")]
    [SerializeField] private float spawnVfxScaleMultiplier = 1f;

    [Tooltip("Optional one-shot VFX prefab spawned when this enemy dies.")]
    [SerializeField] private GameObject deathVfxPrefab;

    [Tooltip("World-space offset added to the enemy death position before spawning the optional death VFX.")]
    [SerializeField] private Vector3 deathVfxSpawnOffset;

    [Tooltip("Lifetime in seconds assigned to each spawned death VFX instance.")]
    [SerializeField] private float deathVfxLifetimeSeconds = 0.75f;

    [Tooltip("Uniform scale multiplier applied to each optional death VFX instance.")]
    [SerializeField] private float deathVfxScaleMultiplier = 1f;

    [Header("Bullet Hit VFX")]
    [Tooltip("One-shot VFX settings spawned when an enemy-owned projectile hits the player.")]
    [SerializeField]
    private EnemyProjectileVfxEventSettings bulletHitVfx = new EnemyProjectileVfxEventSettings();

    [Header("Bullet Death VFX")]
    [Tooltip("One-shot VFX settings spawned when enemy-owned projectiles expire by range, lifetime, or terminal wall impact.")]
    [SerializeField]
    private EnemyProjectileDeathVfxSettings bulletDeathVfx = new EnemyProjectileDeathVfxSettings();

    [Tooltip("When enabled, death debris particles use a compact palette sampled from this enemy prefab's visible body renderers at bake time.")]
    [SerializeField] private bool useEnemyBaseColorForDeathDebris = true;

    [Tooltip("Fallback debris particle color used when visual palette extraction is disabled or no usable enemy body color can be sampled.")]
    [SerializeField] private Color deathDebrisFallbackColor = Color.white;

    [Tooltip("Particle-system child object name that receives the death debris color override.")]
    [SerializeField] private string deathDebrisParticleChildName = "VFX_Debris";

    [Tooltip("Color used by the wave painter and scene preview for this enemy type.")]
    [SerializeField] private Color spawnPaintColor = new Color(1f, 0.3f, 0.3f, 1f);
    #endregion

    #endregion

    #region Properties
    public GameObject EnemyPrefab
    {
        get
        {
            return enemyPrefab;
        }
    }

    public GameObject HitVfxPrefab
    {
        get
        {
            return hitVfxPrefab;
        }
    }

    public Vector3 HitVfxSpawnOffset
    {
        get
        {
            return hitVfxSpawnOffset;
        }
    }

    public float HitVfxLifetimeSeconds
    {
        get
        {
            return hitVfxLifetimeSeconds;
        }
    }

    public float HitVfxScaleMultiplier
    {
        get
        {
            return hitVfxScaleMultiplier;
        }
    }

    public GameObject SpawnVfxPrefab
    {
        get
        {
            return spawnVfxPrefab;
        }
    }

    public EnemySpawnVfxTiming SpawnVfxTiming
    {
        get
        {
            return spawnVfxTiming;
        }
    }

    public Vector3 SpawnVfxSpawnOffset
    {
        get
        {
            return spawnVfxSpawnOffset;
        }
    }

    public float SpawnVfxLifetimeSeconds
    {
        get
        {
            return spawnVfxLifetimeSeconds;
        }
    }

    public float SpawnVfxScaleMultiplier
    {
        get
        {
            return spawnVfxScaleMultiplier;
        }
    }

    public GameObject DeathVfxPrefab
    {
        get
        {
            return deathVfxPrefab;
        }
    }

    public Vector3 DeathVfxSpawnOffset
    {
        get
        {
            return deathVfxSpawnOffset;
        }
    }

    public float DeathVfxLifetimeSeconds
    {
        get
        {
            return deathVfxLifetimeSeconds;
        }
    }

    public float DeathVfxScaleMultiplier
    {
        get
        {
            return deathVfxScaleMultiplier;
        }
    }

    public EnemyProjectileVfxEventSettings BulletHitVfx
    {
        get
        {
            return bulletHitVfx;
        }
    }

    public EnemyProjectileDeathVfxSettings BulletDeathVfx
    {
        get
        {
            return bulletDeathVfx;
        }
    }

    public bool UseEnemyBaseColorForDeathDebris
    {
        get
        {
            return useEnemyBaseColorForDeathDebris;
        }
    }

    public Color DeathDebrisFallbackColor
    {
        get
        {
            return deathDebrisFallbackColor;
        }
    }

    public string DeathDebrisParticleChildName
    {
        get
        {
            return deathDebrisParticleChildName;
        }
    }

    public Color SpawnPaintColor
    {
        get
        {
            return spawnPaintColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes prefab settings after asset edits.
    /// </summary>
    public void Validate()
    {
        Validate(string.Empty);
    }

    /// <summary>
    /// Sanitizes legacy prefab settings and validates projectile VFX settings after asset edits.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (float.IsNaN(hitVfxLifetimeSeconds) || float.IsInfinity(hitVfxLifetimeSeconds) || hitVfxLifetimeSeconds < 0.05f)
            hitVfxLifetimeSeconds = 0.05f;

        if (float.IsNaN(hitVfxScaleMultiplier) || float.IsInfinity(hitVfxScaleMultiplier) || hitVfxScaleMultiplier < 0.01f)
            hitVfxScaleMultiplier = 0.01f;

        if (bulletHitVfx == null)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - Bullet Hit VFX settings are missing.", ownerAssetName));
        else
            bulletHitVfx.Validate(ownerAssetName, "Bullet Hit VFX", null);

        if (bulletDeathVfx == null)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - Bullet Death VFX settings are missing.", ownerAssetName));
        else
            bulletDeathVfx.Validate(ownerAssetName);

        spawnPaintColor.a = Mathf.Clamp01(spawnPaintColor.a);
    }
    #endregion

    #endregion
}
