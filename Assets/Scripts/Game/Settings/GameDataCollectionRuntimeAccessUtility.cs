using Unity.Entities;

/// <summary>
/// Resolves shared telemetry runtime state and maps transport-facing enum contracts.
/// </summary>
internal static class GameDataCollectionRuntimeAccessUtility
{
    #region Methods

    #region Queue Persistence
    /// <summary>
    /// Saves the current buffer using the configured offline policy.
    /// </summary>
    public static void SavePendingEvents()
    {
        if (!TryReadRuntime(out GameDataCollectionRuntimeConfig config,
                            out GameDataCollectionSessionState sessionState,
                            out EntityManager entityManager,
                            out Entity entity))
            return;

        GameTelemetryOfflineStore.Save(entityManager, entity, in config, in sessionState);
    }

    /// <summary>
    /// Restores a same-user offline buffer after server consent has been recorded.
    /// </summary>
    public static void RestorePendingEvents()
    {
        if (!TryReadRuntime(out GameDataCollectionRuntimeConfig config,
                            out GameDataCollectionSessionState sessionState,
                            out EntityManager entityManager,
                            out Entity entity))
            return;

        GameTelemetryOfflineStore.Restore(entityManager, entity, in config, ref sessionState);
    }
    #endregion

    #region Runtime Resolution
    /// <summary>
    /// Resolves the unique telemetry singleton and its safe state.
    /// </summary>
    /// <param name="config">Baked data collection configuration.</param>
    /// <param name="sessionState">Mutable consent and public identity state.</param>
    /// <param name="entityManager">Owning entity manager.</param>
    /// <param name="entity">Unique telemetry entity.</param>
    /// <returns>True when exactly one complete telemetry singleton exists.</returns>
    public static bool TryReadRuntime(out GameDataCollectionRuntimeConfig config,
                                      out GameDataCollectionSessionState sessionState,
                                      out EntityManager entityManager,
                                      out Entity entity)
    {
        config = default;
        sessionState = default;
        entityManager = default;
        entity = Entity.Null;
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GameDataCollectionRuntimeConfig>(),
            ComponentType.ReadOnly<GameDataCollectionSessionState>(),
            ComponentType.ReadOnly<GameTelemetryEvent>());
        bool hasSingleton = query.CalculateEntityCount() == 1;

        if (hasSingleton)
        {
            entity = query.GetSingletonEntity();
            config = entityManager.GetComponentData<GameDataCollectionRuntimeConfig>(entity);
            sessionState = entityManager.GetComponentData<GameDataCollectionSessionState>(entity);
        }

        query.Dispose();
        return hasSingleton;
    }
    #endregion

    #region Contract Mapping
    /// <summary>
    /// Maps the server role contract to the local enum.
    /// </summary>
    /// <param name="roleName">Server role string.</param>
    /// <returns>Matching role or None for an invalid response.</returns>
    public static GameDataCollectionUserRole ResolveRole(string roleName)
    {
        switch (roleName)
        {
            case "user":
                return GameDataCollectionUserRole.User;
            case "developer":
                return GameDataCollectionUserRole.Developer;
            default:
                return GameDataCollectionUserRole.None;
        }
    }

    /// <summary>
    /// Maps the dashboard enum to the lower-case endpoint contract.
    /// </summary>
    /// <param name="department">Requested developer department.</param>
    /// <returns>Endpoint query value.</returns>
    public static string ResolveDashboardDepartment(GameTelemetryDepartment department)
    {
        switch (department)
        {
            case GameTelemetryDepartment.Programming:
                return "programming";
            case GameTelemetryDepartment.Design:
                return "design";
            case GameTelemetryDepartment.Art3D:
                return "art3d";
            default:
                return string.Empty;
        }
    }
    #endregion

    #endregion
}
