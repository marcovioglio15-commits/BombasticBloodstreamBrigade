using System;
using UnityEngine;

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
        if (float.IsNaN(hitVfxLifetimeSeconds) || float.IsInfinity(hitVfxLifetimeSeconds) || hitVfxLifetimeSeconds < 0.05f)
            hitVfxLifetimeSeconds = 0.05f;

        if (float.IsNaN(hitVfxScaleMultiplier) || float.IsInfinity(hitVfxScaleMultiplier) || hitVfxScaleMultiplier < 0.01f)
            hitVfxScaleMultiplier = 0.01f;

        spawnPaintColor.a = Mathf.Clamp01(spawnPaintColor.a);
    }
    #endregion

    #endregion
}
