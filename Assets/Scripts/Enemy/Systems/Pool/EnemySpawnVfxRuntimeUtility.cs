using Unity.Entities;
using Unity.Mathematics;

#region Utilities
/// <summary>
/// Queues optional enemy spawn VFX requests from pool reservation and activation paths.
/// </summary>
public static class EnemySpawnVfxRuntimeUtility
{
    #region Constants
    private const float MinimumVfxLifetimeSeconds = 0.05f;
    private const float MinimumVfxScale = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Clears per-spawn VFX runtime memory before one pooled enemy starts a new reservation cycle.
    /// </summary>
    /// <param name="entityManager">Entity manager used to write runtime state.</param>
    /// <param name="enemyEntity">Enemy instance whose spawn VFX state is being reset.</param>
    public static void ResetState(EntityManager entityManager, Entity enemyEntity)
    {
        SetWarningQueuedState(entityManager, enemyEntity, 0);
    }

    /// <summary>
    /// Queues a spawn VFX request during the warning phase when the authored timing asks for pre-spawn playback.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read VFX config and append the managed request.</param>
    /// <param name="enemyEntity">Reserved enemy instance that owns the request buffer.</param>
    /// <param name="worldPosition">Final enemy spawn position matched by the warning marker.</param>
    /// <param name="warningState">Resolved warning payload that owns the reservation-time VFX lifetime.</param>
    /// <returns>True when a request was queued.</returns>
    public static bool TryEnqueueForReservation(EntityManager entityManager,
                                                Entity enemyEntity,
                                                float3 worldPosition,
                                                in EnemySpawnWarningState warningState)
    {
        if (!TryResolveConfig(entityManager, enemyEntity, out EnemySpawnVfxConfig config))
            return false;

        if (ResolveTiming(config.Timing) != EnemySpawnVfxTiming.WithSpawnWarning)
            return false;

        if (!TryEnqueue(entityManager,
                        enemyEntity,
                        worldPosition,
                        ResolveWarningLifetimeSeconds(in warningState),
                        in config))
            return false;

        SetWarningQueuedState(entityManager, enemyEntity, 1);
        return true;
    }

    /// <summary>
    /// Queues a spawn VFX request at activation time unless a warning-timed request already represented this spawn.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read VFX config and append the managed request.</param>
    /// <param name="enemyEntity">Enemy instance being activated.</param>
    /// <param name="worldPosition">Final enemy spawn position.</param>
    /// <returns>True when a request was queued.</returns>
    public static bool TryEnqueueForActivation(EntityManager entityManager,
                                               Entity enemyEntity,
                                               float3 worldPosition)
    {
        if (!TryResolveConfig(entityManager, enemyEntity, out EnemySpawnVfxConfig config))
            return false;

        EnemySpawnVfxTiming timing = ResolveTiming(config.Timing);

        if (timing == EnemySpawnVfxTiming.WithSpawnWarning &&
            entityManager.HasComponent<EnemySpawnVfxRuntimeState>(enemyEntity) &&
            entityManager.GetComponentData<EnemySpawnVfxRuntimeState>(enemyEntity).WarningVfxQueued != 0)
        {
            return false;
        }

        return TryEnqueue(entityManager,
                          enemyEntity,
                          worldPosition,
                          config.LifetimeSeconds,
                          in config);
    }
    #endregion

    #region Request Building
    /// <summary>
    /// Reads the spawn VFX config when a valid prefab entity and writable request buffer exist.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect enemy components.</param>
    /// <param name="enemyEntity">Enemy instance being inspected.</param>
    /// <param name="config">Resolved spawn VFX config.</param>
    /// <returns>True when a request can be considered.</returns>
    private static bool TryResolveConfig(EntityManager entityManager,
                                         Entity enemyEntity,
                                         out EnemySpawnVfxConfig config)
    {
        config = default;

        if (!entityManager.HasComponent<EnemySpawnVfxConfig>(enemyEntity))
            return false;

        if (!entityManager.HasBuffer<PlayerPowerUpVfxSpawnRequest>(enemyEntity))
            return false;

        config = entityManager.GetComponentData<EnemySpawnVfxConfig>(enemyEntity);
        return config.PrefabEntity != Entity.Null;
    }

    /// <summary>
    /// Appends one managed VFX request using sanitized runtime-only scale and lifetime values.
    /// </summary>
    /// <param name="entityManager">Entity manager used to append the request.</param>
    /// <param name="enemyEntity">Enemy instance that owns the request buffer.</param>
    /// <param name="worldPosition">Base spawn position before authored VFX offset.</param>
    /// <param name="lifetimeSeconds">Resolved VFX lifetime for the current timing path.</param>
    /// <param name="config">Resolved spawn VFX config.</param>
    /// <returns>True when the request was appended.</returns>
    private static bool TryEnqueue(EntityManager entityManager,
                                   Entity enemyEntity,
                                   float3 worldPosition,
                                   float lifetimeSeconds,
                                   in EnemySpawnVfxConfig config)
    {
        float3 spawnPosition = worldPosition + config.SpawnOffset;

        if (!math.all(math.isfinite(spawnPosition)))
            return false;

        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = entityManager.GetBuffer<PlayerPowerUpVfxSpawnRequest>(enemyEntity);
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.PrefabEntity,
            SourcePrefab = config.Prefab,
            Position = spawnPosition,
            Rotation = quaternion.identity,
            UniformScale = math.max(MinimumVfxScale, config.ScaleMultiplier),
            LifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, lifetimeSeconds),
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero
        });
        return true;
    }

    /// <summary>
    /// Resolves warning-timed spawn VFX lifetime from the warning interval instead of stale preset seconds.
    /// </summary>
    /// <param name="warningState">Resolved warning payload for the reserved enemy.</param>
    /// <returns>Lead time covered by the warning-timed VFX request.</returns>
    private static float ResolveWarningLifetimeSeconds(in EnemySpawnWarningState warningState)
    {
        return math.max(0f, warningState.LeadTimeSeconds);
    }

    /// <summary>
    /// Writes warning-queued state while supporting defensively repaired pooled entities.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate runtime state.</param>
    /// <param name="enemyEntity">Enemy instance receiving the state.</param>
    /// <param name="warningVfxQueued">Queued flag value.</param>
    private static void SetWarningQueuedState(EntityManager entityManager,
                                              Entity enemyEntity,
                                              byte warningVfxQueued)
    {
        EnemySpawnVfxRuntimeState runtimeState = new EnemySpawnVfxRuntimeState
        {
            WarningVfxQueued = warningVfxQueued
        };

        if (!entityManager.HasComponent<EnemySpawnVfxRuntimeState>(enemyEntity))
        {
            entityManager.AddComponentData(enemyEntity, runtimeState);
            return;
        }

        entityManager.SetComponentData(enemyEntity, runtimeState);
    }

    /// <summary>
    /// Resolves invalid timing payloads to a conservative activation-time request.
    /// </summary>
    /// <param name="timing">Authored timing value.</param>
    /// <returns>Runtime-supported spawn VFX timing.</returns>
    private static EnemySpawnVfxTiming ResolveTiming(EnemySpawnVfxTiming timing)
    {
        switch (timing)
        {
            case EnemySpawnVfxTiming.OnSpawn:
            case EnemySpawnVfxTiming.WithSpawnWarning:
                return timing;

            default:
                return EnemySpawnVfxTiming.OnSpawn;
        }
    }
    #endregion

    #endregion
}
#endregion
