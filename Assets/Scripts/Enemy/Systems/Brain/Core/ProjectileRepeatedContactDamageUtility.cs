using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Tracks uninterrupted projectile-enemy contacts and applies optional flat-damage ticks without consuming projectile hit payloads.
/// </summary>
public static class ProjectileRepeatedContactDamageUtility
{
    #region Constants
    private const float MinimumTickIntervalSeconds = 0.01f;
    private const float TimeTolerance = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Removes tracked enemies that no longer overlap the projectile so re-entry remains a new ordinary contact.
    /// </summary>
    /// <param name="currentOverlapEnemyIndices">Current overlap indices resolved by the spatial collision pass.</param>
    /// <param name="enemyEntities">Stable enemy entities matching the collision indices.</param>
    /// <param name="hitHistory">Mutable per-projectile contact history.</param>
    public static void PruneToCurrentOverlaps(NativeList<int> currentOverlapEnemyIndices,
                                              NativeArray<Entity> enemyEntities,
                                              ref DynamicBuffer<ProjectileHitHistoryElement> hitHistory)
    {
        // Remove only contacts that ended; live contacts retain their next eligible tick time.
        for (int historyIndex = hitHistory.Length - 1; historyIndex >= 0; historyIndex--)
        {
            if (ContainsEnemy(currentOverlapEnemyIndices, enemyEntities, hitHistory[historyIndex].EnemyEntity))
                continue;

            hitHistory.RemoveAt(historyIndex);
        }
    }

    /// <summary>
    /// Checks whether the projectile already performed its ordinary hit against an uninterrupted enemy contact.
    /// </summary>
    /// <param name="hitHistory">Current projectile contact history.</param>
    /// <param name="enemyEntity">Enemy contact to inspect.</param>
    /// <returns>True when the enemy remains registered in the current contact.</returns>
    public static bool HasTrackedContact(DynamicBuffer<ProjectileHitHistoryElement> hitHistory,
                                         Entity enemyEntity)
    {
        int historyIndex = FindHistoryIndex(hitHistory, enemyEntity);
        return historyIndex >= 0 && hitHistory[historyIndex].BlocksOrdinaryHit != 0;
    }

    /// <summary>
    /// Releases ordinary-hit locks during a non-damaging transition while retaining repeated-damage cadence.
    /// </summary>
    /// <param name="hitHistory">Mutable per-projectile contact history.</param>
    public static void ReleaseOrdinaryHitLocks(ref DynamicBuffer<ProjectileHitHistoryElement> hitHistory)
    {
        for (int historyIndex = 0; historyIndex < hitHistory.Length; historyIndex++)
        {
            ProjectileHitHistoryElement historyElement = hitHistory[historyIndex];
            historyElement.BlocksOrdinaryHit = 0;
            hitHistory[historyIndex] = historyElement;
        }
    }

    /// <summary>
    /// Registers an ordinary projectile hit and schedules its first optional repeated damage tick.
    /// </summary>
    /// <param name="canTrackProjectileHits">Whether the projectile owns a mutable contact-history buffer.</param>
    /// <param name="enemyEntity">Enemy that received the ordinary projectile hit.</param>
    /// <param name="elapsedTime">Current world time in seconds.</param>
    /// <param name="returnConfig">Returning-projectile configuration controlling repeated contact damage.</param>
    /// <param name="hitHistory">Mutable per-projectile contact history.</param>
    public static void RegisterInitialHit(bool canTrackProjectileHits,
                                          Entity enemyEntity,
                                          float elapsedTime,
                                          in ReturningProjectilesConfig returnConfig,
                                          ref DynamicBuffer<ProjectileHitHistoryElement> hitHistory)
    {
        if (!canTrackProjectileHits || enemyEntity == Entity.Null)
            return;

        int historyIndex = FindHistoryIndex(hitHistory, enemyEntity);
        ProjectileHitHistoryElement historyElement = new ProjectileHitHistoryElement
        {
            EnemyEntity = enemyEntity,
            NextRepeatedContactDamageTime = returnConfig.EnableRepeatedContactDamage != 0
                ? elapsedTime + math.max(MinimumTickIntervalSeconds, returnConfig.RepeatedContactDamageIntervalSeconds)
                : float.MaxValue,
            BlocksOrdinaryHit = 1
        };

        if (historyIndex >= 0)
            hitHistory[historyIndex] = historyElement;
        else
            hitHistory.Add(historyElement);
    }

    /// <summary>
    /// Applies every due flat-damage tick while preserving penetration, split triggers, elemental payloads, and ordinary hit history.
    /// </summary>
    /// <param name="returnConfig">Returning-projectile configuration containing tick damage and cadence.</param>
    /// <param name="elapsedTime">Current world time in seconds.</param>
    /// <param name="allowUntrackedContacts">Whether transition phases may start a damage contact without an ordinary projectile hit.</param>
    /// <param name="currentOverlapEnemyIndices">Current overlap indices resolved by the spatial collision pass.</param>
    /// <param name="enemyEntities">Stable enemy entities matching the collision indices.</param>
    /// <param name="projectedEnemyHealth">Mutable projected health snapshot committed after all projectiles are processed.</param>
    /// <param name="hitHistory">Mutable per-projectile contact history.</param>
    /// <returns>True when at least one repeated damage tick was applied.</returns>
    public static bool ApplyDueTicks(in ReturningProjectilesConfig returnConfig,
                                     float elapsedTime,
                                     bool allowUntrackedContacts,
                                     NativeList<int> currentOverlapEnemyIndices,
                                     NativeArray<Entity> enemyEntities,
                                     ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                     ref DynamicBuffer<ProjectileHitHistoryElement> hitHistory)
    {
        if (returnConfig.EnableRepeatedContactDamage == 0 || returnConfig.RepeatedContactDamage <= 0f)
            return false;

        float tickIntervalSeconds = math.max(MinimumTickIntervalSeconds,
                                             returnConfig.RepeatedContactDamageIntervalSeconds);
        bool appliedDamage = false;

        // Resolve each enemy independently so crowded stationary projectiles retain per-contact cadence.
        for (int overlapIndex = 0; overlapIndex < currentOverlapEnemyIndices.Length; overlapIndex++)
        {
            int enemyIndex = currentOverlapEnemyIndices[overlapIndex];

            if (enemyIndex < 0 || enemyIndex >= enemyEntities.Length || enemyIndex >= projectedEnemyHealth.Length)
                continue;

            Entity enemyEntity = enemyEntities[enemyIndex];
            int historyIndex = FindHistoryIndex(hitHistory, enemyEntity);

            if (historyIndex < 0 && !allowUntrackedContacts)
                continue;

            if (historyIndex >= 0 && elapsedTime + TimeTolerance < hitHistory[historyIndex].NextRepeatedContactDamageTime)
                continue;

            EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

            if (enemyHealth.Current <= 0f)
                continue;

            EnemyDamageUtility.ApplyFlatShieldDamage(ref enemyHealth, returnConfig.RepeatedContactDamage);
            projectedEnemyHealth[enemyIndex] = enemyHealth;
            ProjectileHitHistoryElement historyElement = new ProjectileHitHistoryElement
            {
                EnemyEntity = enemyEntity,
                NextRepeatedContactDamageTime = elapsedTime + tickIntervalSeconds,
                BlocksOrdinaryHit = historyIndex >= 0 ? hitHistory[historyIndex].BlocksOrdinaryHit : (byte)0
            };

            if (historyIndex >= 0)
                hitHistory[historyIndex] = historyElement;
            else
                hitHistory.Add(historyElement);

            appliedDamage = true;
        }

        return appliedDamage;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Finds one enemy in a projectile contact history.
    /// </summary>
    /// <param name="hitHistory">Projectile contact history to search.</param>
    /// <param name="enemyEntity">Enemy entity to locate.</param>
    /// <returns>Matching buffer index, or -1 when the contact is not tracked.</returns>
    private static int FindHistoryIndex(DynamicBuffer<ProjectileHitHistoryElement> hitHistory,
                                        Entity enemyEntity)
    {
        for (int historyIndex = 0; historyIndex < hitHistory.Length; historyIndex++)
        {
            if (hitHistory[historyIndex].EnemyEntity == enemyEntity)
                return historyIndex;
        }

        return -1;
    }

    /// <summary>
    /// Checks whether one tracked entity is still present in the current overlap list.
    /// </summary>
    /// <param name="currentOverlapEnemyIndices">Current overlap indices.</param>
    /// <param name="enemyEntities">Stable enemy entities matching the collision indices.</param>
    /// <param name="enemyEntity">Tracked enemy entity to locate.</param>
    /// <returns>True when the tracked enemy remains overlapped.</returns>
    private static bool ContainsEnemy(NativeList<int> currentOverlapEnemyIndices,
                                      NativeArray<Entity> enemyEntities,
                                      Entity enemyEntity)
    {
        if (enemyEntity == Entity.Null)
            return false;

        for (int overlapIndex = 0; overlapIndex < currentOverlapEnemyIndices.Length; overlapIndex++)
        {
            int enemyIndex = currentOverlapEnemyIndices[overlapIndex];

            if (enemyIndex < 0 || enemyIndex >= enemyEntities.Length)
                continue;

            if (enemyEntities[enemyIndex] == enemyEntity)
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
