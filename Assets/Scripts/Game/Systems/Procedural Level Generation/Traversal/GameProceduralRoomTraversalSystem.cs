using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Resolves latched physical portal requests into authoritative graph progression and Scene Manager requests.
/// </summary>
[UpdateInGroup(typeof(GameSceneManagementSystemGroup))]
[UpdateAfter(typeof(GameSceneTransitionTriggerSystem))]
[UpdateBefore(typeof(GameProceduralLevelGenerationSystem))]
public partial class GameProceduralRoomTraversalSystem : SystemBase
{
    #region Fields
    private EntityQuery managerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the unique manager query containing transition state and generated graph buffers.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameSceneTransitionState),
                                      typeof(GameSceneTransitionRequest),
                                      typeof(GameProceduralLevelRuntimeState),
                                      typeof(GameProceduralRoomNodeElement),
                                      typeof(GameProceduralRoomEdgeElement),
                                      typeof(GameProceduralRoomTraversalRequest),
                                      typeof(GameProceduralRoomTransitionContext));
    }

    /// <summary>
    /// Consumes one valid portal request, advances to a graph child or unlocks next-level generation.
    /// </summary>
    protected override void OnUpdate()
    {
        if (managerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameSceneTransitionState transitionState = EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        DynamicBuffer<GameProceduralRoomTraversalRequest> traversalRequests = EntityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity);

        if (transitionState.IsTransitioning != 0)
        {
            if (transitionState.Purpose == GameSceneTransitionPurpose.Standard)
                traversalRequests.Clear();

            return;
        }

        if (traversalRequests.Length <= 0)
            return;

        DynamicBuffer<GameSceneTransitionRequest> sceneRequests = EntityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity);

        if (sceneRequests.Length > 0)
        {
            traversalRequests.Clear();
            return;
        }

        GameProceduralRoomTraversalRequest request = traversalRequests[0];
        traversalRequests.RemoveAt(0);
        GameProceduralLevelRuntimeState runtimeState = EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);

        if (request.SourceNodeIndex != runtimeState.CurrentNodeIndex)
            return;

        if (request.AssignedEdgeIndex == GameProceduralRoomTraversalConstants.LevelExitEdgeIndex)
        {
            BeginLevelAdvance(managerEntity, ref runtimeState);
            return;
        }

        if (runtimeState.Phase != GameProceduralLevelRuntimePhase.Active)
            return;

        DynamicBuffer<GameProceduralRoomEdgeElement> edges = EntityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity, true);

        if (!TryFindEdge(edges, request, out GameProceduralRoomEdgeElement edge))
            return;

        DynamicBuffer<GameProceduralRoomNodeElement> nodes = EntityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity, true);

        if (edge.TargetNodeIndex < 0 || edge.TargetNodeIndex >= nodes.Length)
            return;

        GameProceduralRoomNodeElement targetNode = nodes[edge.TargetNodeIndex];
        runtimeState.PendingNodeIndex = targetNode.NodeIndex;
        runtimeState.CurrentRoomCleared = 0;
        runtimeState.Phase = GameProceduralLevelRuntimePhase.Traversing;
        EntityManager.SetComponentData(managerEntity, runtimeState);
        EntityManager.SetComponentData(managerEntity, new GameProceduralRoomTransitionContext
        {
            SourcePortalId = edge.SourcePortalId,
            TargetPortalId = edge.TargetPortalId,
            SourceNodeIndex = edge.SourceNodeIndex,
            TargetNodeIndex = edge.TargetNodeIndex,
            Kind = GameProceduralRoomTransitionKind.IntraLevel,
            UsesCenterArrival = edge.UsesCenterArrival,
            RelocationPending = 1,
            CommitPending = 1
        });

        sceneRequests.Add(new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadScene,
            Purpose = GameSceneTransitionPurpose.ProceduralRoomTraversal,
            TargetSceneId = targetNode.SceneId
        });
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Moves a cleared non-final Boss room into the explicit generation phase after its LevelExit is crossed.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="runtimeState">Mutable completed-level state.</param>
    private void BeginLevelAdvance(Entity managerEntity, ref GameProceduralLevelRuntimeState runtimeState)
    {
        if (runtimeState.Phase != GameProceduralLevelRuntimePhase.LevelComplete ||
            runtimeState.CurrentRoomCleared == 0)
        {
            return;
        }

        runtimeState.Phase = GameProceduralLevelRuntimePhase.Generating;
        EntityManager.SetComponentData(managerEntity, runtimeState);
    }

    /// <summary>
    /// Resolves one request against its assigned edge and source portal to reject stale or mismatched commands.
    /// </summary>
    /// <param name="edges">Generated directed graph edges.</param>
    /// <param name="request">Latched physical traversal request.</param>
    /// <param name="edge">Matching edge when available.</param>
    /// <returns>True when edge index, node and source portal all match.</returns>
    private static bool TryFindEdge(DynamicBuffer<GameProceduralRoomEdgeElement> edges,
                                    GameProceduralRoomTraversalRequest request,
                                    out GameProceduralRoomEdgeElement edge)
    {
        for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            GameProceduralRoomEdgeElement candidate = edges[edgeIndex];

            if (candidate.EdgeIndex != request.AssignedEdgeIndex ||
                candidate.SourceNodeIndex != request.SourceNodeIndex ||
                !candidate.SourcePortalId.Equals(request.SourcePortalId))
            {
                continue;
            }

            edge = candidate;
            return true;
        }

        edge = default;
        return false;
    }
    #endregion

    #endregion
}
