using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides shared helpers for boss minions reserved during spawn-warning presentation.
/// </summary>
internal static class EnemyBossMinionPendingSpawnUtility
{
    #region Constants
    private const float SpawnWarningLeadTimeSeconds = 0.7f;
    private const float SpawnWarningFadeOutSeconds = 0.18f;
    private const float SpawnWarningRadiusScale = 0.45f;
    private const float SpawnWarningRingWidth = 0.15f;
    private const float SpawnWarningHeightOffset = 0.06f;
    private const float SpawnWarningMaximumAlpha = 0.95f;
    private const float SpawnWarningCellSize = 1f;
    private static readonly float4 SpawnWarningColor = new float4(1f, 0.48f, 0.027f, 1f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the shared warning style used before boss-spawned minions become active.
    /// </summary>
    /// <returns>Spawn warning config matching the default enemy-spawner visual language.</returns>
    public static EnemySpawnWarningConfig BuildSpawnWarningConfig()
    {
        return new EnemySpawnWarningConfig
        {
            Enabled = 1,
            LeadTimeSeconds = SpawnWarningLeadTimeSeconds,
            FadeOutSeconds = SpawnWarningFadeOutSeconds,
            RadiusScale = SpawnWarningRadiusScale,
            RingWidth = SpawnWarningRingWidth,
            HeightOffset = SpawnWarningHeightOffset,
            MaximumAlpha = SpawnWarningMaximumAlpha,
            Color = SpawnWarningColor,
            CellSize = SpawnWarningCellSize
        };
    }

    /// <summary>
    /// Appends one pending minion activation to the owning boss buffer.
    /// </summary>
    /// <param name="entityManager">Entity manager used to reacquire the boss pending buffer.</param>
    /// <param name="bossEntity">Boss that owns the pending minion.</param>
    /// <param name="pendingSpawn">Pending minion data to append.</param>
    /// <returns>True when the pending entry was stored.</returns>
    public static bool TryAppendPendingSpawn(EntityManager entityManager,
                                             Entity bossEntity,
                                             in EnemyBossPendingMinionSpawnElement pendingSpawn)
    {
        if (!entityManager.Exists(bossEntity))
            return false;

        if (!entityManager.HasBuffer<EnemyBossPendingMinionSpawnElement>(bossEntity))
            return false;

        DynamicBuffer<EnemyBossPendingMinionSpawnElement> pendingSpawns = entityManager.GetBuffer<EnemyBossPendingMinionSpawnElement>(bossEntity);
        pendingSpawns.Add(pendingSpawn);
        return true;
    }

    /// <summary>
    /// Checks whether a pending minion reservation can still be activated.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect boss and minion entities.</param>
    /// <param name="bossEntity">Boss that owns the pending spawn.</param>
    /// <param name="pendingSpawn">Pending minion reservation.</param>
    /// <returns>True when the minion and its source boss are still valid.</returns>
    public static bool CanActivatePendingSpawn(EntityManager entityManager,
                                               Entity bossEntity,
                                               in EnemyBossPendingMinionSpawnElement pendingSpawn)
    {
        if (!entityManager.Exists(bossEntity))
            return false;

        if (!entityManager.HasComponent<EnemyActive>(bossEntity))
            return false;

        if (!entityManager.IsComponentEnabled<EnemyActive>(bossEntity))
            return false;

        if (entityManager.HasComponent<EnemyDespawnRequest>(bossEntity))
            return false;

        if (pendingSpawn.MinionEntity == Entity.Null || !entityManager.Exists(pendingSpawn.MinionEntity))
            return false;

        return pendingSpawn.PoolEntity != Entity.Null && entityManager.Exists(pendingSpawn.PoolEntity);
    }

    /// <summary>
    /// Returns one reserved but unactivated minion to its pool.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate the minion and pool buffer.</param>
    /// <param name="pendingSpawn">Pending minion reservation to recycle.</param>
    public static void RecyclePendingSpawn(EntityManager entityManager,
                                           in EnemyBossPendingMinionSpawnElement pendingSpawn)
    {
        if (pendingSpawn.MinionEntity == Entity.Null || !entityManager.Exists(pendingSpawn.MinionEntity))
            return;

        if (pendingSpawn.PoolEntity == Entity.Null || !entityManager.Exists(pendingSpawn.PoolEntity))
            return;

        EnemyPoolUtility.PrepareEnemyForPool(entityManager,
                                             pendingSpawn.MinionEntity,
                                             pendingSpawn.PoolEntity,
                                             pendingSpawn.PoolEntity);

        if (!entityManager.HasBuffer<EnemyPoolElement>(pendingSpawn.PoolEntity))
            return;

        DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(pendingSpawn.PoolEntity);
        poolBuffer.Add(new EnemyPoolElement
        {
            EnemyEntity = pendingSpawn.MinionEntity
        });
    }

    /// <summary>
    /// Recycles and clears every pending minion reservation owned by one boss.
    /// </summary>
    /// <param name="entityManager">Entity manager used to access the boss pending buffer.</param>
    /// <param name="bossEntity">Boss whose pending minion reservations must be cancelled.</param>
    public static void RecycleAndClearPendingSpawns(EntityManager entityManager, Entity bossEntity)
    {
        if (!entityManager.Exists(bossEntity))
            return;

        if (!entityManager.HasBuffer<EnemyBossPendingMinionSpawnElement>(bossEntity))
            return;

        DynamicBuffer<EnemyBossPendingMinionSpawnElement> pendingSpawns = entityManager.GetBuffer<EnemyBossPendingMinionSpawnElement>(bossEntity);
        NativeList<EnemyBossPendingMinionSpawnElement> pendingSpawnCopies = new NativeList<EnemyBossPendingMinionSpawnElement>(pendingSpawns.Length, Allocator.Temp);

        try
        {
            // Copy before recycling because returning minions to pools performs structural changes.
            for (int pendingIndex = 0; pendingIndex < pendingSpawns.Length; pendingIndex++)
                pendingSpawnCopies.Add(pendingSpawns[pendingIndex]);

            pendingSpawns.Clear();

            for (int pendingIndex = 0; pendingIndex < pendingSpawnCopies.Length; pendingIndex++)
            {
                EnemyBossPendingMinionSpawnElement pendingSpawn = pendingSpawnCopies[pendingIndex];
                RecyclePendingSpawn(entityManager, in pendingSpawn);
            }
        }
        finally
        {
            pendingSpawnCopies.Dispose();
        }
    }
    #endregion

    #endregion
}
