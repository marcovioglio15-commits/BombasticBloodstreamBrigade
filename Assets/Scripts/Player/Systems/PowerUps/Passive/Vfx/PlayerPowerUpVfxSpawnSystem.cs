using Unity.Entities;

/// <summary>
/// Consumes queued power-up VFX requests and forwards them to the managed runtime VFX pool.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(PlayerElementalTrailResolveSystem))]
[UpdateAfter(typeof(PlayerPassiveExplosionResolveSystem))]
[UpdateAfter(typeof(EnemyKilledEventsSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct PlayerPowerUpVfxSpawnSystem : ISystem
{
    #region Constants
#if UNITY_ANDROID || UNITY_SWITCH
    private const int MaxManagedVfxSpawnRequestsPerFrame = 32;
    private const int MaxActiveManagedVfx = 160;
#else
    private const int MaxManagedVfxSpawnRequestsPerFrame = 64;
    private const int MaxActiveManagedVfx = 256;
#endif
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires player-side VFX request and cap buffers before updating the managed VFX pool.
    /// </summary>
    /// <param name="state">System state used by Unity Entities.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpVfxSpawnRequest>();
        state.RequireForUpdate<PlayerPowerUpVfxCapConfig>();
    }

    /// <summary>
    /// Clears all managed VFX instances owned by the static runtime pool when the world is destroyed.
    /// </summary>
    /// <param name="state">System state used by Unity Entities.</param>
    public void OnDestroy(ref SystemState state)
    {
        PlayerPowerUpManagedVfxRuntimeUtility.DestroyAll();
    }

    /// <summary>
    /// Updates active managed VFX instances and consumes newly queued spawn requests.
    /// </summary>
    /// <param name="state">System state used by Unity Entities.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        EntityManager entityManager = state.EntityManager;
        PlayerPowerUpManagedVfxRuntimeUtility.UpdateActiveInstances(entityManager, SystemAPI.Time.DeltaTime);

        int remainingFrameSpawnBudget = MaxManagedVfxSpawnRequestsPerFrame;

        foreach ((DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                  DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                  RefRO<PlayerPowerUpVfxCapConfig> vfxCapConfig)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpVfxSpawnRequest>,
                                    DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement>,
                                    RefRO<PlayerPowerUpVfxCapConfig>>())
        {
            if (vfxRequests.Length <= 0)
                continue;

            PlayerPowerUpVfxCapConfig capConfig = SanitizeCapConfig(in vfxCapConfig.ValueRO);

            for (int requestIndex = 0; requestIndex < vfxRequests.Length; requestIndex++)
            {
                if (remainingFrameSpawnBudget <= 0)
                    break;

                PlayerPowerUpVfxSpawnRequest request = vfxRequests[requestIndex];
                int activeInstanceCountBeforeSpawn = PlayerPowerUpManagedVfxRuntimeUtility.ActiveInstanceCount;
                bool acceptedRequest = PlayerPowerUpManagedVfxRuntimeUtility.TrySpawn(entityManager,
                                                                                      prefabBindings,
                                                                                      in request,
                                                                                      in capConfig);

                if (acceptedRequest && PlayerPowerUpManagedVfxRuntimeUtility.ActiveInstanceCount > activeInstanceCountBeforeSpawn)
                    remainingFrameSpawnBudget--;
            }

            vfxRequests.Clear();
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Sanitizes runtime VFX cap values without mutating authoring data.
    /// </summary>
    /// <param name="sourceConfig">Baked cap config read from the player entity.</param>
    /// <returns>Runtime-safe cap config for this frame.</returns>
    private static PlayerPowerUpVfxCapConfig SanitizeCapConfig(in PlayerPowerUpVfxCapConfig sourceConfig)
    {
        PlayerPowerUpVfxCapConfig config = sourceConfig;

        if (config.MaxSamePrefabPerCell < 0)
            config.MaxSamePrefabPerCell = 0;

        if (config.CellSize < 0.1f)
            config.CellSize = 0.1f;

        if (config.MaxAttachedSamePrefabPerTarget < 0)
            config.MaxAttachedSamePrefabPerTarget = 0;

        if (config.MaxActiveOneShotVfx < 0)
            config.MaxActiveOneShotVfx = 0;

        if (config.MaxActiveOneShotVfx == 0 || config.MaxActiveOneShotVfx > MaxActiveManagedVfx)
            config.MaxActiveOneShotVfx = MaxActiveManagedVfx;

        return config;
    }
    #endregion

    #endregion
}
