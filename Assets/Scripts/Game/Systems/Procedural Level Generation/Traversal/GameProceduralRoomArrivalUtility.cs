using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Transforms;

/// <summary>
/// Resolves one pending room arrival, preserves or initializes the persistent player pose and binds generated exits.
/// </summary>
internal static class GameProceduralRoomArrivalUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Finds the unique procedural manager and applies its pending arrival before the scene transition can reveal the room.
    /// </summary>
    /// <param name="entityManager">Entity manager owning scene, player and procedural runtime data.</param>
    /// <returns>True when no arrival is pending or relocation completed; false while required baked entities are unavailable.</returns>
    public static bool TryPreparePendingArrival(EntityManager entityManager)
    {
        EntityQuery managerQuery = entityManager.CreateEntityQuery(typeof(GameProceduralLevelConfig),
                                                                   typeof(GameProceduralLevelRuntimeState),
                                                                   typeof(GameProceduralRoomTransitionContext),
                                                                   typeof(GameProceduralRoomNodeElement),
                                                                   typeof(GameProceduralRoomEdgeElement));

        try
        {
            if (managerQuery.CalculateEntityCount() != 1)
                return true;

            return TryPreparePendingArrival(entityManager, managerQuery.GetSingletonEntity());
        }
        finally
        {
            managerQuery.Dispose();
        }
    }

    /// <summary>
    /// Applies one manager's pending arrival and records that relocation no longer needs to block transition readiness.
    /// </summary>
    /// <param name="entityManager">Entity manager owning scene, player and procedural runtime data.</param>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <returns>True when relocation completed or was already applied; false while target authoring or player data is unavailable.</returns>
    public static bool TryPreparePendingArrival(EntityManager entityManager, Entity managerEntity)
    {
        GameProceduralRoomTransitionContext context = entityManager.GetComponentData<GameProceduralRoomTransitionContext>(managerEntity);

        if (context.RelocationPending == 0)
            return true;

        GameProceduralLevelConfig config = entityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);

        if (context.Kind == GameProceduralRoomTransitionKind.IntraLevel &&
            !GameProceduralPlayerTransitionPresentationUtility.IsRelocationTimeReached(config.RelocationNormalizedTime))
        {
            return false;
        }

        if (!TryPreparePlayerPose(entityManager, config, context))
        {
            return false;
        }

        ConfigureRoomPortals(entityManager, managerEntity, context);
        context.RelocationPending = 0;
        entityManager.SetComponentData(managerEntity, context);
        return true;
    }
    #endregion

    #region Arrival Pose
    /// <summary>
    /// Resolves either the unique room center anchor or the graph-selected target entrance position.
    /// </summary>
    /// <param name="entityManager">Entity manager owning freshly loaded room authoring components.</param>
    /// <param name="context">Pending logical transition context.</param>
    /// <param name="position">Resolved world-space player position.</param>
    /// <returns>True when the required target anchor is present.</returns>
    private static bool TryResolveArrivalPosition(EntityManager entityManager,
                                                  GameProceduralRoomTransitionContext context,
                                                  out float3 position)
    {
        if (context.UsesCenterArrival != 0)
            return TryResolveCenterArrival(entityManager, out position);

        return TryResolvePortalArrival(entityManager, context.TargetPortalId, out position);
    }

    /// <summary>
    /// Resolves the unique center-arrival marker baked in the active room.
    /// </summary>
    /// <param name="entityManager">Entity manager owning active room components.</param>
    /// <param name="position">Resolved center position.</param>
    /// <returns>True when exactly one center marker exists.</returns>
    private static bool TryResolveCenterArrival(EntityManager entityManager,
                                                out float3 position)
    {
        position = float3.zero;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomCenterAnchor>(),
                                                            ComponentType.ReadOnly<SceneTag>());
        NativeList<Entity> anchors = new NativeList<Entity>(Allocator.Temp);

        try
        {
            GameProceduralRoomInstanceQueryUtility.CollectActiveRoomEntities(query, ref anchors);

            if (anchors.Length != 1)
                return false;

            GameRoomCenterAnchor anchor = entityManager.GetComponentData<GameRoomCenterAnchor>(anchors[0]);
            position = anchor.Position;
            return true;
        }
        finally
        {
            anchors.Dispose();
            query.Dispose();
        }
    }

    /// <summary>
    /// Resolves the physical entrance selected by the generated incoming edge.
    /// </summary>
    /// <param name="entityManager">Entity manager owning active room portals.</param>
    /// <param name="portalId">Graph-selected target portal ID.</param>
    /// <param name="position">Resolved authored arrival position.</param>
    /// <returns>True when exactly one matching target portal exists.</returns>
    private static bool TryResolvePortalArrival(EntityManager entityManager,
                                                FixedString64Bytes portalId,
                                                out float3 position)
    {
        position = float3.zero;

        if (portalId.Length <= 0)
            return false;

        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomPortal>(),
                                                            ComponentType.ReadOnly<SceneTag>());
        NativeList<Entity> portalEntities = new NativeList<Entity>(Allocator.Temp);

        try
        {
            GameProceduralRoomInstanceQueryUtility.CollectActiveRoomEntities(query, ref portalEntities);
            int matchCount = 0;

            for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
            {
                GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portalEntities[portalIndex]);

                if (!portal.PortalId.Equals(portalId))
                    continue;

                position = portal.ArrivalPosition;
                matchCount++;
            }

            return matchCount == 1;
        }
        finally
        {
            portalEntities.Dispose();
            query.Dispose();
        }
    }
    #endregion

    #region Player
    /// <summary>
    /// Preserves the player's facing while applying the exact authored arrival position for single-slot traversal and
    /// run boundaries. Optional aligned dual-slot traversal preserves the complete pose.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the persistent player.</param>
    /// <param name="config">Baked transition settings.</param>
    /// <param name="context">Pending transition context selecting continuous traversal or authored run-boundary placement.</param>
    /// <returns>True when a unique player was available and its required pose policy completed.</returns>
    private static bool TryPreparePlayerPose(EntityManager entityManager,
                                             GameProceduralLevelConfig config,
                                             GameProceduralRoomTransitionContext context)
    {
        EntityQuery playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                                  ComponentType.ReadWrite<LocalTransform>());

        try
        {
            int playerCount = playerQuery.CalculateEntityCount();

            if (playerCount != 1)
                return false;

            Entity playerEntity = playerQuery.GetSingletonEntity();

            bool preservesContinuousMotion = context.Kind == GameProceduralRoomTransitionKind.IntraLevel &&
                                             GameProceduralRoomTransitionTransactionUtility.IsSpatiallyAlignedStreaming(config.RoomStreamingMode);

            // Optional spatial dual-slot streaming moves the target around the persistent player. Authored single-slot
            // and serial replacement instead place the player on the exact graph-selected entrance behind black.
            if (!preservesContinuousMotion)
            {
                if (!TryResolveArrivalPosition(entityManager, context, out float3 arrivalPosition))
                    return false;

                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
                transform.Position = arrivalPosition;
                entityManager.SetComponentData(playerEntity, transform);
            }

            if (config.ClearPlayerVelocity != 0 && !preservesContinuousMotion)
                ClearPlayerMotion(entityManager, playerEntity);

            return true;
        }
        finally
        {
            playerQuery.Dispose();
        }
    }

    /// <summary>
    /// Clears mutable movement channels that could carry physical momentum across a room boundary. Current-frame
    /// input remains owned by the transition-aware input bridge so look and safe combat never require a manual rearm.
    /// </summary>
    /// <param name="entityManager">Entity manager owning optional player movement state.</param>
    /// <param name="playerEntity">Relocated persistent player entity.</param>
    private static void ClearPlayerMotion(EntityManager entityManager, Entity playerEntity)
    {
        if (entityManager.HasComponent<PlayerMovementState>(playerEntity))
        {
            PlayerMovementState movementState = entityManager.GetComponentData<PlayerMovementState>(playerEntity);
            movementState.DesiredDirection = float3.zero;
            movementState.Velocity = float3.zero;
            movementState.PrevMoveMask = 0;
            movementState.CurrMoveMask = 0;
            movementState.MovePressTimes = float4.zero;
            movementState.ReleaseHoldMask = 0;
            movementState.ReleaseHoldUntilTime = 0f;
            entityManager.SetComponentData(playerEntity, movementState);
        }

    }
    #endregion

    #region Portal Assignment
    /// <summary>
    /// Assigns each active physical exit to its generated edge and disables the selected inbound entrance for this visit.
    /// </summary>
    /// <param name="entityManager">Entity manager owning active room portals and graph data.</param>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="context">Pending logical room transition context.</param>
    private static void ConfigureRoomPortals(EntityManager entityManager,
                                             Entity managerEntity,
                                             GameProceduralRoomTransitionContext context)
    {
        EntityQuery portalQuery = entityManager.CreateEntityQuery(typeof(GameRoomPortal),
                                                                  typeof(GameRoomPortalRuntimeState),
                                                                  typeof(SceneTag));
        NativeList<Entity> portalEntities = new NativeList<Entity>(Allocator.Temp);

        try
        {
            GameProceduralRoomInstanceQueryUtility.CollectActiveRoomEntities(portalQuery, ref portalEntities);
            DynamicBuffer<GameProceduralRoomEdgeElement> edges = entityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity, true);
            DynamicBuffer<GameProceduralRoomNodeElement> nodes = entityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity, true);
            bool activeNodeIsBoss = context.TargetNodeIndex >= 0 &&
                                    context.TargetNodeIndex < nodes.Length &&
                                    nodes[context.TargetNodeIndex].Role == GameProceduralRoomRole.Boss;

            // Bind only graph-selected outgoing physical portals; the inbound target remains permanently disabled for this visit.
            for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
            {
                Entity portalEntity = portalEntities[portalIndex];
                GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portalEntity);
                GameRoomPortalRuntimeState portalState = new GameRoomPortalRuntimeState
                {
                    AssignedEdgeIndex = GameProceduralRoomTraversalConstants.UnassignedEdgeIndex,
                    WasPlayerInside = 1
                };

                if (!portal.PortalId.Equals(context.TargetPortalId))
                {
                    portalState.AssignedEdgeIndex = FindAssignedEdge(edges, context.TargetNodeIndex, portal.PortalId);

                    if (portalState.AssignedEdgeIndex == GameProceduralRoomTraversalConstants.UnassignedEdgeIndex &&
                        portal.Policy == GameRoomPortalConnectionPolicy.LevelExit &&
                        activeNodeIsBoss)
                    {
                        portalState.AssignedEdgeIndex = GameProceduralRoomTraversalConstants.LevelExitEdgeIndex;
                    }
                }

                portalState.TraversalEnabled = 0;
                entityManager.SetComponentData(portalEntity, portalState);
            }

            // Evaluate room-clear gates once, then open only uniquely paired and currently unlocked assignments.
            GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        }
        finally
        {
            portalEntities.Dispose();
            portalQuery.Dispose();
        }
    }

    /// <summary>
    /// Finds the generated outgoing edge assigned to one physical portal in the active logical node.
    /// </summary>
    /// <param name="edges">Generated directed graph edges.</param>
    /// <param name="sourceNodeIndex">Active logical node index.</param>
    /// <param name="sourcePortalId">Physical portal ID to resolve.</param>
    /// <returns>Assigned edge index, or -1 when the portal is sealed or inbound-only.</returns>
    private static int FindAssignedEdge(DynamicBuffer<GameProceduralRoomEdgeElement> edges,
                                        int sourceNodeIndex,
                                        FixedString64Bytes sourcePortalId)
    {
        for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            GameProceduralRoomEdgeElement edge = edges[edgeIndex];

            if (edge.SourceNodeIndex == sourceNodeIndex && edge.SourcePortalId.Equals(sourcePortalId))
                return edge.EdgeIndex;
        }

        return -1;
    }

    #endregion

    #endregion
}
