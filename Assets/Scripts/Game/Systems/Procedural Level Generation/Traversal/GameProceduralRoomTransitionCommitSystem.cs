using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Commits a pending logical room only after Scene Management has completed loading and hidden player relocation.
/// </summary>
[UpdateInGroup(typeof(GameSceneManagementSystemGroup))]
[UpdateAfter(typeof(GameSceneTransitionExecutionSystem))]
public partial class GameProceduralRoomTransitionCommitSystem : SystemBase
{
    #region Fields
    private EntityQuery managerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the unique procedural and scene-transition manager query.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameSceneTransitionState),
                                      typeof(GameProceduralLevelRuntimeState),
                                      typeof(GameProceduralRoomTransitionContext),
                                      typeof(GameProceduralRoomNodeElement),
                                      typeof(GameProceduralRoomEnteredEvent));
    }

    /// <summary>
    /// Marks the target node active after scene readiness, arrival relocation and fade completion.
    /// </summary>
    protected override void OnUpdate()
    {
        if (managerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameSceneTransitionState transitionState = EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameProceduralRoomTransitionContext context = EntityManager.GetComponentData<GameProceduralRoomTransitionContext>(managerEntity);

        if (transitionState.IsTransitioning != 0 || context.CommitPending == 0)
            return;

        DynamicBuffer<GameProceduralRoomNodeElement> nodes = EntityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity);

        if (context.TargetNodeIndex < 0 || context.TargetNodeIndex >= nodes.Length)
            return;

        GameProceduralRoomNodeElement targetNode = nodes[context.TargetNodeIndex];

        if (!transitionState.ActiveSceneId.Equals(targetNode.SceneId))
            return;

        if (!GameProceduralRoomArrivalUtility.TryPreparePendingArrival(EntityManager, managerEntity))
            return;

        byte firstVisit = targetNode.Visited == 0 ? (byte)1 : (byte)0;
        targetNode.Visited = 1;
        nodes[context.TargetNodeIndex] = targetNode;
        GameProceduralLevelRuntimeState runtimeState = EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);

        if (firstVisit != 0)
            runtimeState.VisitOrdinal++;

        runtimeState.CurrentNodeIndex = context.TargetNodeIndex;
        runtimeState.PendingNodeIndex = -1;
        runtimeState.CurrentDepth = targetNode.Depth;
        runtimeState.CurrentRoomCleared = targetNode.Cleared;
        runtimeState.Phase = GameProceduralLevelRuntimePhase.Active;
        EntityManager.SetComponentData(managerEntity, runtimeState);
        DynamicBuffer<GameProceduralRoomEnteredEvent> enteredEvents = EntityManager.GetBuffer<GameProceduralRoomEnteredEvent>(managerEntity);
        enteredEvents.Clear();
        enteredEvents.Add(new GameProceduralRoomEnteredEvent
        {
            RunSeed = runtimeState.RunSeed,
            GenerationVersion = runtimeState.GenerationVersion,
            VisitOrdinal = runtimeState.VisitOrdinal,
            LevelIndex = runtimeState.CurrentLevelIndex,
            NodeIndex = targetNode.NodeIndex,
            TileIndex = targetNode.TileIndex,
            FirstVisit = firstVisit
        });
        context.Kind = GameProceduralRoomTransitionKind.None;
        context.CommitPending = 0;
        context.RelocationPending = 0;
        EntityManager.SetComponentData(managerEntity, context);
    }
    #endregion

    #endregion
}
