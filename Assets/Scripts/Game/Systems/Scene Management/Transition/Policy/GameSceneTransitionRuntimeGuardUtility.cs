using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

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
    /// Resolves whether active scene replacement must block gameplay simulation.
    /// </summary>
    /// <param name="entityManager">EntityManager that owns the query.</param>
    /// <param name="transitionStateQuery">Query containing GameSceneTransitionState.</param>
    /// <returns>True until the transition fully releases scene-streaming and physics ownership.</returns>
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
    /// Resolves whether default-world scene replacement must block gameplay simulation.
    /// </summary>
    /// <returns>True until the transition fully releases scene-streaming and physics ownership.</returns>
    public static bool ShouldBlockDefaultWorldGameplay()
    {
        if (!TryGetDefaultTransitionState(out GameSceneTransitionState transitionState))
            return false;

        return ShouldBlockGameplay(transitionState);
    }

    /// <summary>
    /// Resolves whether default-world player simulation should remain frozen by active scene replacement.
    /// </summary>
    /// <returns>True until the transition ends and the rebuilt physics world is safe for gameplay queries.</returns>
    public static bool ShouldBlockDefaultWorldPlayerGameplay()
    {
        if (!TryGetDefaultTransitionState(out GameSceneTransitionState transitionState))
            return false;

        return ShouldBlockPlayerGameplay(transitionState);
    }

    /// <summary>
    /// Resolves the complete player-facing transition policy from one cached singleton read. Gameplay remains blocked
    /// until completion, while live movement and look can resume once a procedural destination enters its stable
    /// fade-in phase. Optional spatial dual-slot traversal remains live for its complete transaction.
    /// </summary>
    /// <param name="isSceneTransitioning">True while Scene Management owns an active transition.</param>
    /// <param name="shouldBlockGameplay">True while shooting, tools and other mutable gameplay must remain paused.</param>
    /// <param name="allowsLiveMotion">True when current movement and look samples can safely affect the ready player.</param>
    /// <param name="requiresStableMotionRelease">True during procedural FadeIn, whose first frame must discard the
    /// current sample so a load-frame delta cannot become visible player displacement.</param>
    public static void ResolveDefaultWorldPlayerPolicy(out bool isSceneTransitioning,
                                                       out bool shouldBlockGameplay,
                                                       out bool allowsLiveMotion,
                                                       out bool requiresStableMotionRelease)
    {
        isSceneTransitioning = false;
        shouldBlockGameplay = false;
        allowsLiveMotion = false;
        requiresStableMotionRelease = false;

        if (!TryGetDefaultTransitionState(out GameSceneTransitionState transitionState))
            return;

        isSceneTransitioning = transitionState.IsTransitioning != 0;
        shouldBlockGameplay = ShouldBlockPlayerGameplay(transitionState);

        if (!isSceneTransitioning)
            return;

        requiresStableMotionRelease = IsStableProceduralFadeIn(transitionState);
        allowsLiveMotion = requiresStableMotionRelease ||
                           IsContinuousPlayerTraversal(transitionState);
    }

    /// <summary>
    /// Resolves whether presentation and gameplay systems must avoid querying the default DOTS physics world.
    /// Dual-slot traversal retains the source world, while single-slot and scene replacement protect collider blobs
    /// only during their destructive loading phases until the fixed-step readiness barrier completes.
    /// </summary>
    /// <returns>True while a non-continuous transition can replace scene-owned collider data.</returns>
    public static bool ShouldBlockDefaultWorldPhysicsQueries()
    {
        if (!TryGetDefaultTransitionState(out GameSceneTransitionState transitionState))
            return false;

        if (transitionState.IsTransitioning == 0)
            return false;

        if (transitionState.Purpose != GameSceneTransitionPurpose.ProceduralRoomTraversal)
            return true;

        World world = World.DefaultGameObjectInjectionWorld;
        EntityQuery transitionStateQuery = GetDefaultTransitionStateQuery(world);
        Entity transitionEntity = transitionStateQuery.GetSingletonEntity();

        if (!world.EntityManager.HasComponent<GameProceduralLevelConfig>(transitionEntity))
            return true;

        GameProceduralRoomStreamingMode streamingMode =
            world.EntityManager.GetComponentData<GameProceduralLevelConfig>(transitionEntity).RoomStreamingMode;

        if (streamingMode == GameProceduralRoomStreamingMode.TransactionalDualSlot)
            return false;

        if (streamingMode != GameProceduralRoomStreamingMode.AuthoredSingleSlot)
            return true;

        switch (transitionState.Phase)
        {
            case GameSceneTransitionPhase.PreUnload:
            case GameSceneTransitionPhase.Loading:
            case GameSceneTransitionPhase.PostUnload:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether a procedural destination has completed loading, arrival, physics and pool readiness and is now
    /// being revealed. Input consumed here is current-frame state, never input retained from destructive phases.
    /// </summary>
    /// <param name="transitionState">Current authoritative transition state.</param>
    /// <returns>True while a ready procedural target is in FadeIn.</returns>
    private static bool IsStableProceduralFadeIn(GameSceneTransitionState transitionState)
    {
        return transitionState.IsTransitioning != 0 &&
               transitionState.Phase == GameSceneTransitionPhase.FadeIn &&
               GameSceneTransitionPurposeUtility.IsProcedural(transitionState.Purpose);
    }

    /// <summary>
    /// Resolves whether optional spatial dual-slot traversal intentionally keeps motion live through every phase.
    /// </summary>
    /// <param name="transitionState">Current authoritative transition state.</param>
    /// <returns>True only for active dual-slot intra-level traversal.</returns>
    private static bool IsContinuousPlayerTraversal(GameSceneTransitionState transitionState)
    {
        if (transitionState.IsTransitioning == 0 ||
            transitionState.Purpose != GameSceneTransitionPurpose.ProceduralRoomTraversal)
        {
            return false;
        }

        World world = World.DefaultGameObjectInjectionWorld;
        EntityQuery transitionStateQuery = GetDefaultTransitionStateQuery(world);
        Entity transitionEntity = transitionStateQuery.GetSingletonEntity();

        if (!world.EntityManager.HasComponent<GameProceduralLevelConfig>(transitionEntity))
            return false;

        return GameProceduralRoomTransitionTransactionUtility.IsSpatiallyAlignedStreaming(
            world.EntityManager.GetComponentData<GameProceduralLevelConfig>(transitionEntity).RoomStreamingMode);
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
    /// Resolves whether a transition state can expose scene-owned gameplay or physics data that is being replaced.
    /// </summary>
    /// <param name="transitionState">Current scene transition state.</param>
    /// <returns>True until the complete transition, including its reveal, has released all scene-streaming ownership.</returns>
    private static bool ShouldBlockGameplay(GameSceneTransitionState transitionState)
    {
        return transitionState.IsTransitioning != 0;
    }

    /// <summary>
    /// Resolves whether player simulation should stay locked while scene-owned physics data is being replaced.
    /// </summary>
    /// <param name="transitionState">Current scene transition state.</param>
    /// <returns>True until scene streaming and fade presentation both complete.</returns>
    private static bool ShouldBlockPlayerGameplay(GameSceneTransitionState transitionState)
    {
        return transitionState.IsTransitioning != 0;
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

/// <summary>
/// Stores the number of completed default-world physics steps used by scene-transition readiness barriers.
/// </summary>
internal struct GameSceneTransitionPhysicsStepState : IComponentData
{
    #region Fields
    public ulong CompletedStepVersion;
    public byte NavigationWarmupAllowed;
    #endregion
}

/// <summary>
/// Records fixed steps after the exported DOTS physics world is ready for simulation and presentation queries.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(AfterPhysicsSystemGroup), OrderLast = true)]
internal partial struct GameSceneTransitionPhysicsStepTrackingSystem : ISystem
{
    #region Fields
    private Entity stepStateEntity;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the world-local physics step singleton and waits for an exported physics world before updating it.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        stepStateEntity = state.EntityManager.CreateEntity(typeof(GameSceneTransitionPhysicsStepState));
        state.EntityManager.SetName(stepStateEntity, "Game Scene Transition Physics Step State");
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Advances the readiness version after the current fixed step has exported its physics world.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        RefRW<GameSceneTransitionPhysicsStepState> stepState =
            SystemAPI.GetSingletonRW<GameSceneTransitionPhysicsStepState>();
        stepState.ValueRW.CompletedStepVersion++;
    }

    /// <summary>
    /// Releases the world-local readiness singleton if the tracking system is removed before world disposal.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        if (state.EntityManager.Exists(stepStateEntity))
            state.EntityManager.DestroyEntity(stepStateEntity);
    }
    #endregion

    #endregion
}

/// <summary>
/// Coordinates the post-load barrier that prevents a transition from revealing collider data before physics rebuilds.
/// </summary>
internal static class GameSceneTransitionPhysicsReadinessUtility
{
    #region Methods

    #region Readiness
    /// <summary>
    /// Captures the next required physics step once and resolves whether that fixed step has completed.
    /// </summary>
    /// <param name="physicsStepStateQuery">Cached query containing the world-local physics step state.</param>
    /// <param name="readinessRequested">Mutable flag indicating that a target step was captured.</param>
    /// <param name="requiredStepVersion">Mutable version that must be reached before scene reveal.</param>
    /// <returns>True after at least one exported physics step has completed since the barrier was requested.</returns>
    public static bool TryComplete(EntityQuery physicsStepStateQuery,
                                   ref bool readinessRequested,
                                   ref ulong requiredStepVersion)
    {
        if (physicsStepStateQuery.CalculateEntityCount() != 1)
            return false;

        ulong completedStepVersion = physicsStepStateQuery.GetSingleton<GameSceneTransitionPhysicsStepState>().CompletedStepVersion;

        if (!readinessRequested)
        {
            requiredStepVersion = completedStepVersion + 1UL;
            readinessRequested = true;
            return false;
        }

        return completedStepVersion >= requiredStepVersion;
    }

    /// <summary>
    /// Enables or disables transition-time navigation access to the exported physics world after validating singleton ownership.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the world-local physics readiness singleton.</param>
    /// <param name="physicsStepStateQuery">Cached writable query containing the readiness singleton.</param>
    /// <param name="allowed">True only after target physics has completed a fresh fixed step.</param>
    public static void SetNavigationWarmupAllowed(EntityManager entityManager,
                                                  EntityQuery physicsStepStateQuery,
                                                  bool allowed)
    {
        if (physicsStepStateQuery.CalculateEntityCount() != 1)
            return;

        Entity stateEntity = physicsStepStateQuery.GetSingletonEntity();
        GameSceneTransitionPhysicsStepState stepState =
            entityManager.GetComponentData<GameSceneTransitionPhysicsStepState>(stateEntity);
        stepState.NavigationWarmupAllowed = allowed ? (byte)1 : (byte)0;
        entityManager.SetComponentData(stateEntity, stepState);
    }

    /// <summary>
    /// Clears the captured physics readiness target before a new transition starts.
    /// </summary>
    /// <param name="readinessRequested">Mutable flag tracking whether a target step was captured.</param>
    /// <param name="requiredStepVersion">Mutable target physics step version.</param>
    public static void Reset(ref bool readinessRequested, ref ulong requiredStepVersion)
    {
        readinessRequested = false;
        requiredStepVersion = 0UL;
    }
    #endregion

    #endregion
}
