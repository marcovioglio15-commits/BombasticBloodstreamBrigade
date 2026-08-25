using Unity.Entities;

/// <summary>
/// Resolves the baked HUD-owned Settings navigation singleton without retaining ECS query state.
/// </summary>
internal static class GameHudSettingsNavigationRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Reads the unique Settings navigation config from the active default world.
    /// </summary>
    /// <param name="config">Resolved config when exactly one singleton exists.</param>
    /// <returns>True when a unique baked config is available.</returns>
    public static bool TryResolve(out GameHudSettingsNavigationRuntimeConfig config)
    {
        config = default;
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        EntityQuery query = world.EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GameHudSettingsNavigationRuntimeConfig>());
        bool hasUniqueConfig = query.CalculateEntityCount() == 1;

        if (hasUniqueConfig)
            config = query.GetSingleton<GameHudSettingsNavigationRuntimeConfig>();

        query.Dispose();
        return hasUniqueConfig;
    }
    #endregion

    #endregion
}
