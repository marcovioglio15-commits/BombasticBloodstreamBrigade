using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Detects player entry into graph-assigned oriented portal volumes and emits one latched traversal request.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerControllerSystemGroup))]
public partial struct GameProceduralRoomPortalDetectionSystem : ISystem
{
    #region Fields
    private EntityQuery managerQuery;
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates cached queries for procedural state, level gating and the unique persistent player transform.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        managerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GameSceneTransitionState,
                     GameProceduralLevelRuntimeState,
                     GameProceduralLevelDefinitionElement,
                     GameProceduralRoomTraversalRequest>()
            .Build(ref state);
        playerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build(ref state);
    }

    /// <summary>
    /// Evaluates active portal OBBs after player movement and latches only the first valid crossing this frame.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (managerQuery.CalculateEntityCount() != 1 || playerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameSceneTransitionState transitionState = state.EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);

        if (transitionState.IsTransitioning != 0)
            return;

        GameProceduralLevelRuntimeState runtimeState = state.EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);

        if (runtimeState.Phase != GameProceduralLevelRuntimePhase.Active &&
            runtimeState.Phase != GameProceduralLevelRuntimePhase.LevelComplete)
        {
            return;
        }

        DynamicBuffer<GameProceduralRoomTraversalRequest> requests = state.EntityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity);

        if (requests.Length > 0)
            return;

        DynamicBuffer<GameProceduralLevelDefinitionElement> levels = state.EntityManager.GetBuffer<GameProceduralLevelDefinitionElement>(managerEntity, true);

        if (runtimeState.CurrentLevelIndex < 0 || runtimeState.CurrentLevelIndex >= levels.Length)
            return;

        float3 playerPosition = state.EntityManager.GetComponentData<LocalTransform>(playerQuery.GetSingletonEntity()).Position;
        bool levelRequiresClear = levels[runtimeState.CurrentLevelIndex].RequireRoomClearBeforeExit != 0;

        foreach ((RefRO<GameRoomPortal> portal,
                  RefRW<GameRoomPortalRuntimeState> portalState)
                 in SystemAPI.Query<RefRO<GameRoomPortal>, RefRW<GameRoomPortalRuntimeState>>())
        {
            bool playerInside = ContainsPoint(portal.ValueRO, playerPosition);

            if (!playerInside)
            {
                portalState.ValueRW.WasPlayerInside = 0;
                portalState.ValueRW.HasTriggered = 0;
                continue;
            }

            if (portalState.ValueRO.WasPlayerInside != 0)
                continue;

            portalState.ValueRW.WasPlayerInside = 1;

            if (!CanTraverse(portal.ValueRO,
                             portalState.ValueRO,
                             runtimeState,
                             levelRequiresClear))
            {
                continue;
            }

            requests.Add(new GameProceduralRoomTraversalRequest
            {
                SourcePortalId = portal.ValueRO.PortalId,
                SourceNodeIndex = runtimeState.CurrentNodeIndex,
                AssignedEdgeIndex = portalState.ValueRO.AssignedEdgeIndex
            });
            portalState.ValueRW.HasTriggered = 1;
            break;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves whether one graph-assigned exit is unlocked for the current room lifecycle state.
    /// </summary>
    /// <param name="portal">Immutable baked physical portal data.</param>
    /// <param name="portalState">Mutable graph assignment and latch state.</param>
    /// <param name="runtimeState">Current procedural room lifecycle state.</param>
    /// <param name="levelRequiresClear">Whether the active level gates all exits on room completion.</param>
    /// <returns>True when the portal may emit one traversal request.</returns>
    private static bool CanTraverse(GameRoomPortal portal,
                                    GameRoomPortalRuntimeState portalState,
                                    GameProceduralLevelRuntimeState runtimeState,
                                    bool levelRequiresClear)
    {
        if (portalState.TraversalEnabled == 0 || portalState.HasTriggered != 0)
            return false;

        switch (portal.Capability)
        {
            case GameRoomPortalCapability.Exit:
            case GameRoomPortalCapability.Both:
                break;
            default:
                return false;
        }

        if (portalState.AssignedEdgeIndex == GameProceduralRoomTraversalConstants.LevelExitEdgeIndex)
            return runtimeState.Phase == GameProceduralLevelRuntimePhase.LevelComplete &&
                   runtimeState.CurrentRoomCleared != 0;

        if (portalState.AssignedEdgeIndex < 0)
            return false;

        if ((levelRequiresClear || portal.RequireRoomClear != 0) && runtimeState.CurrentRoomCleared == 0)
            return false;

        return runtimeState.Phase == GameProceduralLevelRuntimePhase.Active;
    }

    /// <summary>
    /// Tests one world-space point against a baked oriented portal box without using trigger callbacks.
    /// </summary>
    /// <param name="portal">Rotation-aware portal volume.</param>
    /// <param name="point">World-space player position.</param>
    /// <returns>True when the point lies inside the portal OBB.</returns>
    private static bool ContainsPoint(GameRoomPortal portal, float3 point)
    {
        float3 localPoint = math.mul(math.inverse(portal.Rotation), point - portal.Center);
        float3 absolutePoint = math.abs(localPoint);
        return math.all(absolutePoint <= portal.HalfExtents);
    }
    #endregion

    #endregion
}
