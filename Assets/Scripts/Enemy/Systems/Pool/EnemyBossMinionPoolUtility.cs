using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides pooled boss-minion helpers shared by spawn scheduling and pending cleanup paths.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyBossMinionPoolUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates and prewarms the pool entity required by one boss minion rule.
    /// /params entityManager Entity manager used for structural changes.
    /// /params rule Rule that owns pool sizing and prefab data.
    /// /returns Created pool entity, or Entity.Null when no valid prefab exists.
    /// </summary>
    public static Entity CreateAndPrewarmRulePool(EntityManager entityManager,
                                                  in EnemyBossMinionSpawnElement rule)
    {
        if (rule.PrefabEntity == Entity.Null || !entityManager.Exists(rule.PrefabEntity))
            return Entity.Null;

        Entity poolEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(poolEntity, new EnemyPoolState
        {
            PrefabEntity = rule.PrefabEntity,
            InitialCapacity = math.max(0, rule.AutomaticPoolSize),
            ExpandBatch = math.max(1, rule.PoolExpandBatch),
            Initialized = 1
        });
        entityManager.AddComponentData(poolEntity, new EnemySpawner
        {
            InitialPoolCapacityPerPrefab = math.max(0, rule.AutomaticPoolSize),
            ExpandBatchPerPrefab = math.max(1, rule.PoolExpandBatch),
            DespawnDistance = math.max(0f, rule.DespawnDistance),
            MaximumSpawnDistanceFromCenter = 0f,
            TotalPlannedEnemyCount = 0
        });
        entityManager.AddBuffer<EnemyPoolElement>(poolEntity);
        EnemyPoolUtility.ExpandPool(entityManager,
                                    poolEntity,
                                    poolEntity,
                                    rule.PrefabEntity,
                                    math.max(0, rule.AutomaticPoolSize));
        return poolEntity;
    }

    /// <summary>
    /// Acquires one inactive minion from a rule pool, expanding the pool when empty.
    /// /params entityManager Entity manager used to access the pool.
    /// /params poolEntity Pool entity.
    /// /params prefabEntity Enemy prefab entity.
    /// /params minionEntity Output acquired minion.
    /// /returns True when a minion was acquired.
    /// </summary>
    public static bool TryAcquireMinion(EntityManager entityManager,
                                        Entity poolEntity,
                                        Entity prefabEntity,
                                        out Entity minionEntity)
    {
        minionEntity = Entity.Null;

        if (poolEntity == Entity.Null || !entityManager.Exists(poolEntity))
            return false;

        if (!entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
            return false;

        DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);

        if (poolBuffer.Length <= 0 && entityManager.HasComponent<EnemyPoolState>(poolEntity))
        {
            EnemyPoolState poolState = entityManager.GetComponentData<EnemyPoolState>(poolEntity);
            EnemyPoolUtility.ExpandPool(entityManager,
                                        poolEntity,
                                        poolEntity,
                                        prefabEntity,
                                        math.max(1, poolState.ExpandBatch));
            poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);
        }

        while (poolBuffer.Length > 0)
        {
            int lastIndex = poolBuffer.Length - 1;
            minionEntity = poolBuffer[lastIndex].EnemyEntity;
            poolBuffer.RemoveAt(lastIndex);

            if (entityManager.Exists(minionEntity))
                return true;
        }

        minionEntity = Entity.Null;
        return false;
    }

    /// <summary>
    /// Builds the standard spawn warning state used before a reserved boss minion becomes active.
    /// /params entityManager Entity manager used to inspect prefab body data.
    /// /params prefabEntity Prefab entity used for body-aware ring sizing.
    /// /params spawnPosition World position where the minion will become active.
    /// /params activationTime World time when the minion should be activated.
    /// /params spawnWarningConfig Warning style used for this minion spawn feedback.
    /// /returns Fully resolved spawn warning state.
    /// </summary>
    public static EnemySpawnWarningState CreateSpawnWarningState(EntityManager entityManager,
                                                                 Entity prefabEntity,
                                                                 float3 spawnPosition,
                                                                 float activationTime,
                                                                 in EnemySpawnWarningConfig spawnWarningConfig)
    {
        return EnemySpawnSystem.CreateWarningState(entityManager,
                                                  prefabEntity,
                                                  spawnPosition,
                                                  activationTime,
                                                  spawnWarningConfig);
    }

    /// <summary>
    /// Writes boss ownership and reward multipliers onto one reserved minion.
    /// /params entityManager Entity manager used to mutate the minion.
    /// /params minionEntity Reserved minion.
    /// /params bossEntity Boss that owns the minion.
    /// /params ruleIndex Source rule index.
    /// /params rule Rule data supplying reward multipliers.
    /// /returns None.
    /// </summary>
    public static void ApplyMinionMetadata(EntityManager entityManager,
                                           Entity minionEntity,
                                           Entity bossEntity,
                                           int ruleIndex,
                                           in EnemyBossMinionSpawnElement rule)
    {
        EnemyBossMinionOwner owner = new EnemyBossMinionOwner
        {
            BossEntity = bossEntity,
            RuleIndex = ruleIndex,
            KillOnBossDeath = rule.KillMinionsOnBossDeath,
            BlocksRunCompletion = rule.RequireMinionsKilledForRunCompletion
        };

        if (entityManager.HasComponent<EnemyBossMinionOwner>(minionEntity))
            entityManager.SetComponentData(minionEntity, owner);
        else
            entityManager.AddComponentData(minionEntity, owner);

        EnemyDropRewardMultiplier rewardMultiplier = new EnemyDropRewardMultiplier
        {
            ExperienceMultiplier = math.max(0f, rule.ExperienceDropMultiplier),
            ExtraComboPointsMultiplier = math.max(0f, rule.ExtraComboPointsMultiplier),
            FutureDropsMultiplier = math.max(0f, rule.FutureDropsMultiplier)
        };

        if (entityManager.HasComponent<EnemyDropRewardMultiplier>(minionEntity))
            entityManager.SetComponentData(minionEntity, rewardMultiplier);
        else
            entityManager.AddComponentData(minionEntity, rewardMultiplier);
    }
    #endregion

    #endregion
}
