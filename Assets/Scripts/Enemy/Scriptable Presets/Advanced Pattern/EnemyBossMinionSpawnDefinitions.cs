using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines one minion spawn rule owned by a boss pattern preset.
/// </summary>
[Serializable]
public sealed class EnemyBossMinionSpawnRule
{
    #region Constants
    private const int DefaultSpawnCount = 1;
    private const int DefaultMaxAlive = 12;
    private const float DefaultIntervalSeconds = 8f;
    private const float DefaultSpawnRadius = 4f;
    private const float DefaultDespawnDistance = 80f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Enables this minion spawn rule.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Enemy prefab spawned by this rule. The prefab must contain EnemyAuthoring and must already define a normal enemy pattern.")]
    [SerializeField] private GameObject minionPrefab;

    [Tooltip("Runtime condition that activates this minion spawn rule.")]
    [SerializeField] private EnemyBossMinionSpawnTrigger trigger = EnemyBossMinionSpawnTrigger.Interval;

    [Tooltip("Seconds between interval spawns when Trigger is Interval.")]
    [SerializeField] private float intervalSeconds = DefaultIntervalSeconds;

    [Tooltip("Minimum seconds between Boss Damaged trigger activations. Set to 0 to allow one spawn for every accepted boss hit.")]
    [SerializeField] private float bossHitCooldownSeconds;

    [Tooltip("Normalized boss health threshold used when Trigger is Health Below Percent.")]
    [Range(0f, 1f)]
    [SerializeField] private float healthThresholdPercent = 0.5f;

    [Tooltip("Number of minions emitted per trigger activation.")]
    [SerializeField] private int spawnCount = DefaultSpawnCount;

    [Tooltip("Maximum active minions alive at the same time from this rule.")]
    [SerializeField] private int maxAliveMinions = DefaultMaxAlive;

    [Tooltip("Radius around the boss used to place spawned minions.")]
    [SerializeField] private float spawnRadius = DefaultSpawnRadius;

    [Tooltip("Distance from player after which minions from this rule are returned to their pool.")]
    [SerializeField] private float despawnDistance = DefaultDespawnDistance;

    [Tooltip("Multiplier applied to experience drops emitted by these minions. Set to 0 to suppress experience.")]
    [SerializeField] private float experienceDropMultiplier = 0.25f;

    [Tooltip("Multiplier applied to Extra Combo Points granted by these minions. Set to 0 to suppress combo rewards.")]
    [SerializeField] private float extraComboPointsMultiplier = 0.25f;

    [Tooltip("Multiplier reserved for future drop modules emitted by these minions. Set to 0 to suppress future generic drops.")]
    [SerializeField] private float futureDropsMultiplier = 0.25f;
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

    public GameObject MinionPrefab
    {
        get
        {
            return minionPrefab;
        }
    }

    public EnemyBossMinionSpawnTrigger Trigger
    {
        get
        {
            return trigger;
        }
    }

    public float IntervalSeconds
    {
        get
        {
            return intervalSeconds;
        }
    }

    public float BossHitCooldownSeconds
    {
        get
        {
            return bossHitCooldownSeconds;
        }
    }

    public float HealthThresholdPercent
    {
        get
        {
            return healthThresholdPercent;
        }
    }

    public int SpawnCount
    {
        get
        {
            return spawnCount;
        }
    }

    public int MaxAliveMinions
    {
        get
        {
            return maxAliveMinions;
        }
    }

    public float SpawnRadius
    {
        get
        {
            return spawnRadius;
        }
    }

    public float DespawnDistance
    {
        get
        {
            return despawnDistance;
        }
    }

    public float ExperienceDropMultiplier
    {
        get
        {
            return experienceDropMultiplier;
        }
    }

    public float ExtraComboPointsMultiplier
    {
        get
        {
            return extraComboPointsMultiplier;
        }
    }

    public float FutureDropsMultiplier
    {
        get
        {
            return futureDropsMultiplier;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Calculates the pool size required to satisfy the configured trigger cadence and active cap.
    /// </summary>
    /// <returns>Automatic pool capacity for this minion rule.</returns>
    public int CalculateAutomaticPoolSize()
    {
        int safeSpawnCount = Mathf.Max(0, spawnCount);
        int safeMaxAlive = Mathf.Max(0, maxAliveMinions);

        if (safeSpawnCount <= 0)
            return 0;

        if (safeMaxAlive <= 0)
            return safeSpawnCount;

        return Mathf.Max(safeSpawnCount, safeMaxAlive);
    }

    /// <summary>
    /// Keeps nested rule state structurally valid without snapping authored numeric settings.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Groups boss minion spawning rules and shared defaults.
/// </summary>
[Serializable]
public sealed class EnemyBossMinionSpawnSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables boss-owned minion spawning.")]
    [SerializeField] private bool enabled;

    [Tooltip("Fallback interval used when an enabled minion rule has a non-positive interval.")]
    [SerializeField] private float fallbackIntervalSeconds = 8f;

    [Tooltip("Fallback expand batch used by automatically created minion pools.")]
    [SerializeField] private int poolExpandBatch = 4;

    [Tooltip("When enabled, active minions spawned by this boss are killed automatically when the boss dies.")]
    [SerializeField] private bool killMinionsOnBossDeath = true;

    [Tooltip("When Kill Minions On Boss Death is disabled, surviving active minions must be killed before the run can complete.")]
    [SerializeField] private bool requireMinionsKilledForRunCompletion = true;

    [Tooltip("Minion spawn rules evaluated by the boss runtime system.")]
    [SerializeField] private List<EnemyBossMinionSpawnRule> rules = new List<EnemyBossMinionSpawnRule>();
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

    public float FallbackIntervalSeconds
    {
        get
        {
            return fallbackIntervalSeconds;
        }
    }

    public int PoolExpandBatch
    {
        get
        {
            return poolExpandBatch;
        }
    }

    public bool KillMinionsOnBossDeath
    {
        get
        {
            return killMinionsOnBossDeath;
        }
    }

    public bool RequireMinionsKilledForRunCompletion
    {
        get
        {
            return requireMinionsKilledForRunCompletion;
        }
    }

    public IReadOnlyList<EnemyBossMinionSpawnRule> Rules
    {
        get
        {
            return rules;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps the rules list allocated and validates each child rule.
    /// </summary>
    public void Validate()
    {
        if (rules == null)
            rules = new List<EnemyBossMinionSpawnRule>();

        for (int index = 0; index < rules.Count; index++)
        {
            if (rules[index] == null)
                rules[index] = new EnemyBossMinionSpawnRule();

            rules[index].Validate();
        }
    }
    #endregion

    #endregion
}
