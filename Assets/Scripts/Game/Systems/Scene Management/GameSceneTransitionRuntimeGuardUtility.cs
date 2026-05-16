using Unity.Entities;

/// <summary>
/// Provides lightweight runtime guards for systems and MonoBehaviours that must not mutate gameplay while scenes transition.
/// </summary>
internal static class GameSceneTransitionRuntimeGuardUtility
{
    #region Fields
    private static World cachedDefaultWorld;
    private static EntityQuery cachedDefaultTransitionStateQuery;
    private static bool cachedDefaultTransitionStateQueryInitialized;
    #endregion

    #region Methods

    #region Query
    /// <summary>
    /// Resolves whether the provided transition-state query currently points to an active scene transition.
    /// </summary>
    /// <param name="entityManager">EntityManager that owns the query.</param>
    /// <param name="transitionStateQuery">Query containing GameSceneTransitionState.</param>
    /// <returns>True when one scene manager is actively transitioning.</returns>
    public static bool IsTransitioning(EntityManager entityManager, EntityQuery transitionStateQuery)
    {
        if (!TryGetTransitionState(entityManager, transitionStateQuery, out GameSceneTransitionState transitionState))
            return false;

        return transitionState.IsTransitioning != 0;
    }

    /// <summary>
    /// Resolves whether the current transition phase must block gameplay simulation.
    /// </summary>
    /// <param name="entityManager">EntityManager that owns the query.</param>
    /// <param name="transitionStateQuery">Query containing GameSceneTransitionState.</param>
    /// <returns>True while scenes can still be loading, unloading or settling behind black.</returns>
    public static bool ShouldBlockGameplay(EntityManager entityManager, EntityQuery transitionStateQuery)
    {
        if (!TryGetTransitionState(entityManager, transitionStateQuery, out GameSceneTransitionState transitionState))
            return false;

        return ShouldBlockGameplay(transitionState);
    }
    #endregion

    #region Default World
    /// <summary>
    /// Resolves transition activity from the default ECS world for managed UI cleanup paths.
    /// </summary>
    /// <returns>True when the default scene manager is actively transitioning.</returns>
    public static bool IsDefaultWorldTransitioning()
    {
        if (!TryGetDefaultTransitionState(out GameSceneTransitionState transitionState))
            return false;

        return transitionState.IsTransitioning != 0;
    }

    /// <summary>
    /// Resolves whether the default-world transition phase must block gameplay simulation.
    /// </summary>
    /// <returns>True while scenes can still be loading, unloading or settling behind black.</returns>
    public static bool ShouldBlockDefaultWorldGameplay()
    {
        if (!TryGetDefaultTransitionState(out GameSceneTransitionState transitionState))
            return false;

        return ShouldBlockGameplay(transitionState);
    }
    #endregion

    #region Cache
    /// <summary>
    /// Resolves the cached default-world transition query, recreating it when the default world changes.
    /// </summary>
    /// <param name="world">Current default ECS world.</param>
    /// <returns>Query containing the transition state singleton.</returns>
    private static EntityQuery GetDefaultTransitionStateQuery(World world)
    {
        if (cachedDefaultTransitionStateQueryInitialized &&
            cachedDefaultWorld != null &&
            cachedDefaultWorld.IsCreated &&
            ReferenceEquals(cachedDefaultWorld, world))
        {
            return cachedDefaultTransitionStateQuery;
        }

        cachedDefaultWorld = world;
        cachedDefaultTransitionStateQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameSceneTransitionState>());
        cachedDefaultTransitionStateQueryInitialized = true;
        return cachedDefaultTransitionStateQuery;
    }

    /// <summary>
    /// Resolves the default-world transition state component when a single manager exists.
    /// </summary>
    /// <param name="transitionState">Resolved transition state when available.</param>
    /// <returns>True when one transition state singleton can be read.</returns>
    private static bool TryGetDefaultTransitionState(out GameSceneTransitionState transitionState)
    {
        transitionState = default;
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
        {
            ClearCachedDefaultWorld();
            return false;
        }

        EntityQuery transitionStateQuery = GetDefaultTransitionStateQuery(world);
        return TryGetTransitionState(world.EntityManager, transitionStateQuery, out transitionState);
    }

    /// <summary>
    /// Resolves the transition state component when the query contains exactly one valid entity.
    /// </summary>
    /// <param name="entityManager">EntityManager that owns the query.</param>
    /// <param name="transitionStateQuery">Query containing GameSceneTransitionState.</param>
    /// <param name="transitionState">Resolved transition state when available.</param>
    /// <returns>True when one transition state singleton can be read.</returns>
    private static bool TryGetTransitionState(EntityManager entityManager,
                                              EntityQuery transitionStateQuery,
                                              out GameSceneTransitionState transitionState)
    {
        transitionState = default;

        if (transitionStateQuery.CalculateEntityCount() != 1)
            return false;

        Entity transitionEntity = transitionStateQuery.GetSingletonEntity();

        if (!entityManager.Exists(transitionEntity))
            return false;

        transitionState = entityManager.GetComponentData<GameSceneTransitionState>(transitionEntity);
        return true;
    }

    /// <summary>
    /// Resolves whether a transition state is still in a phase that can expose invalid scene data.
    /// </summary>
    /// <param name="transitionState">Current scene transition state.</param>
    /// <returns>True when gameplay systems should remain frozen.</returns>
    private static bool ShouldBlockGameplay(GameSceneTransitionState transitionState)
    {
        if (transitionState.IsTransitioning == 0)
            return false;

        return transitionState.Phase != GameSceneTransitionPhase.FadeIn;
    }

    /// <summary>
    /// Clears cached query ownership when no valid default world exists.
    /// </summary>
    private static void ClearCachedDefaultWorld()
    {
        cachedDefaultWorld = null;
        cachedDefaultTransitionStateQuery = default;
        cachedDefaultTransitionStateQueryInitialized = false;
    }
    #endregion

    #endregion
}
