using Unity.Entities;

/// <summary>
/// Consumes queued power-up VFX requests and forwards them to the managed runtime VFX pool.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(PlayerElementalTrailResolveSystem))]
[UpdateAfter(typeof(PlayerPassiveExplosionResolveSystem))]
public partial struct PlayerPowerUpVfxSpawnSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires player-side VFX request and cap buffers before updating the managed VFX pool.
    /// /params state System state used by Unity Entities.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpVfxSpawnRequest>();
        state.RequireForUpdate<PlayerPowerUpVfxCapConfig>();
    }

    /// <summary>
    /// Clears all managed VFX instances owned by the static runtime pool when the world is destroyed.
    /// /params state System state used by Unity Entities.
    /// /returns None.
    /// </summary>
    public void OnDestroy(ref SystemState state)
    {
        PlayerPowerUpManagedVfxRuntimeUtility.DestroyAll();
    }

    /// <summary>
    /// Updates active managed VFX instances and consumes newly queued spawn requests.
    /// /params state System state used by Unity Entities.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        EntityManager entityManager = state.EntityManager;
        PlayerPowerUpManagedVfxRuntimeUtility.UpdateActiveInstances(entityManager, SystemAPI.Time.DeltaTime);

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
                PlayerPowerUpVfxSpawnRequest request = vfxRequests[requestIndex];
                PlayerPowerUpManagedVfxRuntimeUtility.TrySpawn(entityManager,
                                                               prefabBindings,
                                                               in request,
                                                               in capConfig);
            }

            vfxRequests.Clear();
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Sanitizes runtime VFX cap values without mutating authoring data.
    /// /params sourceConfig Baked cap config read from the player entity.
    /// /returns Runtime-safe cap config for this frame.
    /// </summary>
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

        return config;
    }
    #endregion

    #endregion
}
