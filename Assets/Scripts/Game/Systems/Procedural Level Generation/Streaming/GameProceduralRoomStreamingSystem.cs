using Unity.Entities;

/// <summary>
/// Preloads graph-adjacent logical rooms and advances deferred retirement outside transition-critical frames.
/// </summary>
[UpdateInGroup(typeof(GameSceneManagementSystemGroup))]
[UpdateAfter(typeof(GameProceduralRoomTransitionCommitSystem))]
public partial class GameProceduralRoomStreamingSystem : SystemBase
{
    #region Fields
    private EntityQuery managerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the unique manager query containing streaming policy, generated graph and scene catalog data.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameSceneManagerConfig),
                                      typeof(GameSceneTransitionState),
                                      typeof(GameSceneDefinitionElement),
                                      typeof(GameProceduralLevelConfig),
                                      typeof(GameProceduralLevelRuntimeState),
                                      typeof(GameProceduralRoomNodeElement),
                                      typeof(GameProceduralRoomEdgeElement));
    }

    /// <summary>
    /// Advances in-flight streaming, stages outgoing graph nodes and retires over-budget previous rooms.
    /// </summary>
    protected override void OnUpdate()
    {
        GameProceduralRoomStreamingRuntimeUtility.TickLoading(EntityManager);

        if (managerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameProceduralLevelConfig proceduralConfig = EntityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);

        if (proceduralConfig.RoomStreamingMode != GameProceduralRoomStreamingMode.TransactionalDualSlot)
            return;

        GameSceneTransitionState transitionState = EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);

        if (transitionState.IsTransitioning != 0)
            return;

        GameProceduralLevelRuntimeState runtimeState = EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        bool runtimeActive = runtimeState.Phase == GameProceduralLevelRuntimePhase.Active &&
                             runtimeState.CurrentNodeIndex >= 0;

        if (runtimeActive)
        {
            ulong generationKey = BuildGenerationKey(runtimeState);
            DynamicBuffer<GameProceduralRoomEdgeElement> edges = EntityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity, true);
            GameProceduralRoomStreamingRuntimeUtility.ReconcileCandidateReachability(generationKey,
                                                                                     runtimeState.CurrentNodeIndex,
                                                                                     edges);
        }

        int retiredRoomBudget = runtimeActive
            ? proceduralConfig.RetiredRoomBudget
            : 0;
        GameProceduralRoomStreamingRuntimeUtility.TickRetirement(EntityManager,
                                                                 retiredRoomBudget,
                                                                 proceduralConfig.RetirementWorkBudgetMilliseconds);

        if (!runtimeActive)
            return;

        PreloadOutgoingRooms(managerEntity, proceduralConfig, runtimeState.CurrentNodeIndex);
    }
    #endregion

    #region Preload
    /// <summary>
    /// Starts deterministic outgoing-node preloads until the configured staged-room budget is exhausted.
    /// </summary>
    /// <param name="managerEntity">Unique scene and procedural manager entity.</param>
    /// <param name="proceduralConfig">Baked transactional streaming policy.</param>
    /// <param name="currentNodeIndex">Currently active generated graph node.</param>
    private void PreloadOutgoingRooms(Entity managerEntity,
                                      GameProceduralLevelConfig proceduralConfig,
                                      int currentNodeIndex)
    {
        if (proceduralConfig.AdjacentPreloadPolicy == GameProceduralAdjacentPreloadPolicy.Disabled ||
            proceduralConfig.MaximumStagedRooms < 1 ||
            GameProceduralRoomStreamingRuntimeUtility.HasInFlightStagingWork())
        {
            return;
        }

        GameSceneManagerConfig sceneConfig = EntityManager.GetComponentData<GameSceneManagerConfig>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState = EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        ulong generationKey = BuildGenerationKey(runtimeState);
        DynamicBuffer<GameProceduralRoomNodeElement> nodes = EntityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity, true);
        DynamicBuffer<GameProceduralRoomEdgeElement> edges = EntityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity, true);
        DynamicBuffer<GameSceneDefinitionElement> scenes = EntityManager.GetBuffer<GameSceneDefinitionElement>(managerEntity, true);
        bool firstOutgoingOnly = proceduralConfig.AdjacentPreloadPolicy == GameProceduralAdjacentPreloadPolicy.FirstOutgoingOnly;

        // Preserve graph edge order so preload selection remains deterministic for fixed seeds.
        for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            GameProceduralRoomEdgeElement edge = edges[edgeIndex];

            if (edge.SourceNodeIndex != currentNodeIndex ||
                edge.TargetNodeIndex < 0 ||
                edge.TargetNodeIndex >= nodes.Length)
            {
                continue;
            }

            if (GameProceduralRoomStreamingRuntimeUtility.ShouldDeferCandidate(generationKey, edge.TargetNodeIndex) ||
                GameProceduralRoomStreamingRuntimeUtility.ContainsNode(generationKey, edge.TargetNodeIndex))
            {
                if (firstOutgoingOnly)
                    return;

                continue;
            }

            if (GameProceduralRoomStreamingRuntimeUtility.CountStagedInstances() >= proceduralConfig.MaximumStagedRooms)
                return;

            GameProceduralRoomNodeElement node = nodes[edge.TargetNodeIndex];

            if (!TryResolveScene(scenes, node.SceneId, out GameSceneDefinitionElement sceneDefinition))
            {
                if (firstOutgoingOnly)
                    return;

                continue;
            }

            if (GameProceduralRoomStreamingRuntimeUtility.EnsureNodeLoading(generationKey,
                                                                           node.NodeIndex,
                                                                           sceneDefinition,
                                                                           sceneConfig.LoadBackend,
                                                                           true))
                return;
        }
    }

    /// <summary>
    /// Resolves one canonical managed scene definition from the baked Scene Manager catalog.
    /// </summary>
    /// <param name="scenes">Baked scene definitions.</param>
    /// <param name="sceneId">Canonical room scene ID.</param>
    /// <param name="sceneDefinition">Resolved scene definition when present.</param>
    /// <returns>True when the scene catalog contains the requested ID.</returns>
    internal static bool TryResolveScene(DynamicBuffer<GameSceneDefinitionElement> scenes,
                                         Unity.Collections.FixedString64Bytes sceneId,
                                         out GameSceneDefinitionElement sceneDefinition)
    {
        for (int sceneIndex = 0; sceneIndex < scenes.Length; sceneIndex++)
        {
            if (!scenes[sceneIndex].SceneId.Equals(sceneId))
                continue;

            sceneDefinition = scenes[sceneIndex];
            return true;
        }

        sceneDefinition = default;
        return false;
    }

    /// <summary>
    /// Combines the monotonic graph version and deterministic level seed into the logical scene-instance identity.
    /// </summary>
    /// <param name="runtimeState">Current generated level state.</param>
    /// <returns>Stable identity that changes whenever a run or level graph is regenerated.</returns>
    internal static ulong BuildGenerationKey(GameProceduralLevelRuntimeState runtimeState)
    {
        return ((ulong)runtimeState.GenerationVersion << 32) | runtimeState.LevelSeed;
    }
    #endregion

    #endregion
}
