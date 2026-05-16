using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves active boss HUD entities into summed health and shield snapshots.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyBossHudSnapshotUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves all active boss HUD entities into one summed health/shield snapshot.
    /// /params entityManager Entity manager used to read boss components.
    /// /params bossQuery Query containing potential boss HUD entities.
    /// /params cachedBossEntity Primary boss cache used to avoid needless throttled misses.
    /// /params nextBossResolveTime Next unscaled time at which a missed lookup may run again.
    /// /params currentTime Current unscaled time used to throttle lookup attempts.
    /// /params resolveIntervalSeconds Seconds between lookup attempts when no cached boss is valid.
    /// /params bossSnapshot Resolved aggregate snapshot when at least one boss is active.
    /// /returns True when at least one valid boss entity is available.
    /// </summary>
    public static bool TryResolveSnapshot(EntityManager entityManager,
                                          EntityQuery bossQuery,
                                          ref Entity cachedBossEntity,
                                          ref float nextBossResolveTime,
                                          float currentTime,
                                          float resolveIntervalSeconds,
                                          out EnemyBossHudSnapshot bossSnapshot)
    {
        bossSnapshot = default;

        if (!IsCachedBossValid(entityManager, cachedBossEntity))
        {
            cachedBossEntity = Entity.Null;

            if (currentTime < nextBossResolveTime)
                return false;

            nextBossResolveTime = currentTime + resolveIntervalSeconds;
        }

        if (bossQuery.IsEmptyIgnoreFilter)
            return false;

        NativeArray<Entity> bossEntities = bossQuery.ToEntityArray(Allocator.Temp);
        Entity primaryBossEntity = Entity.Null;
        EnemyBossHudConfig primaryConfig = default;
        float currentHealth = 0f;
        float maxHealth = 0f;
        float currentShield = 0f;
        float maxShield = 0f;
        int activeBossCount = 0;

        try
        {
            for (int index = 0; index < bossEntities.Length; index++)
            {
                Entity candidateEntity = bossEntities[index];

                if (!TryReadActiveBossHudData(entityManager, candidateEntity, out EnemyBossHudConfig hudConfig, out EnemyHealth health))
                    continue;

                if (primaryBossEntity == Entity.Null)
                {
                    primaryBossEntity = candidateEntity;
                    primaryConfig = hudConfig;
                }

                currentHealth += Mathf.Max(0f, health.Current);
                maxHealth += Mathf.Max(0f, health.Max);
                currentShield += Mathf.Max(0f, health.CurrentShield);
                maxShield += Mathf.Max(0f, health.MaxShield);
                activeBossCount += 1;
            }
        }
        finally
        {
            bossEntities.Dispose();
        }

        cachedBossEntity = primaryBossEntity;

        if (primaryBossEntity == Entity.Null)
            return false;

        bossSnapshot = new EnemyBossHudSnapshot(primaryBossEntity,
                                                in primaryConfig,
                                                currentHealth,
                                                maxHealth,
                                                currentShield,
                                                maxShield,
                                                activeBossCount);
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Returns whether the cached primary boss can still be used to skip resolve throttling.
    /// /params entityManager Entity manager used to inspect the cached boss.
    /// /params cachedBossEntity Cached primary boss entity.
    /// /returns True when the cached boss is still active and has an enabled HUD config.
    /// </summary>
    private static bool IsCachedBossValid(EntityManager entityManager, Entity cachedBossEntity)
    {
        if (cachedBossEntity == Entity.Null)
            return false;

        if (!entityManager.Exists(cachedBossEntity))
            return false;

        if (!entityManager.HasComponent<EnemyBossTag>(cachedBossEntity))
            return false;

        if (!entityManager.HasComponent<EnemyBossHudConfig>(cachedBossEntity))
            return false;

        if (!entityManager.HasComponent<EnemyActive>(cachedBossEntity))
            return false;

        if (!entityManager.IsComponentEnabled<EnemyActive>(cachedBossEntity))
            return false;

        EnemyBossHudConfig hudConfig = entityManager.GetComponentData<EnemyBossHudConfig>(cachedBossEntity);
        return hudConfig.Enabled != 0;
    }

    /// <summary>
    /// Reads HUD config and health data only from active boss entities that should contribute to the aggregate bar.
    /// /params entityManager Entity manager used to read candidate components.
    /// /params candidateEntity Entity inspected for active boss HUD data.
    /// /params hudConfig Resolved HUD config when the candidate is valid.
    /// /params health Resolved health data when the candidate is valid.
    /// /returns True when the candidate contributes to the boss HUD aggregate.
    /// </summary>
    private static bool TryReadActiveBossHudData(EntityManager entityManager,
                                                 Entity candidateEntity,
                                                 out EnemyBossHudConfig hudConfig,
                                                 out EnemyHealth health)
    {
        hudConfig = default;
        health = default;

        if (!entityManager.Exists(candidateEntity))
            return false;

        if (!entityManager.HasComponent<EnemyActive>(candidateEntity))
            return false;

        if (!entityManager.IsComponentEnabled<EnemyActive>(candidateEntity))
            return false;

        hudConfig = entityManager.GetComponentData<EnemyBossHudConfig>(candidateEntity);

        if (hudConfig.Enabled == 0)
            return false;

        health = entityManager.GetComponentData<EnemyHealth>(candidateEntity);
        return true;
    }
    #endregion

    #endregion
}
