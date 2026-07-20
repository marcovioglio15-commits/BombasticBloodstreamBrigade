using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves one pending room arrival, relocates the persistent player and binds physical exits to generated graph edges.
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

        if (!TryResolveArrivalPose(entityManager, context, out float3 arrivalPosition, out quaternion arrivalRotation))
            return false;

        if (!TryRelocatePlayer(entityManager,
                               config,
                               arrivalPosition,
                               arrivalRotation))
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
    /// Resolves either the unique room center anchor or the graph-selected target entrance pose.
    /// </summary>
    /// <param name="entityManager">Entity manager owning freshly loaded room authoring components.</param>
    /// <param name="context">Pending logical transition context.</param>
    /// <param name="position">Resolved world-space player position.</param>
    /// <param name="rotation">Resolved world-space player rotation.</param>
    /// <returns>True when the required target anchor is present.</returns>
    private static bool TryResolveArrivalPose(EntityManager entityManager,
                                              GameProceduralRoomTransitionContext context,
                                              out float3 position,
                                              out quaternion rotation)
    {
        if (context.UsesCenterArrival != 0)
            return TryResolveCenterArrival(entityManager, out position, out rotation);

        return TryResolvePortalArrival(entityManager, context.TargetPortalId, out position, out rotation);
    }

    /// <summary>
    /// Resolves the unique center-arrival marker baked in the active room.
    /// </summary>
    /// <param name="entityManager">Entity manager owning active room components.</param>
    /// <param name="position">Resolved center position.</param>
    /// <param name="rotation">Resolved center rotation.</param>
    /// <returns>True when exactly one center marker exists.</returns>
    private static bool TryResolveCenterArrival(EntityManager entityManager,
                                                out float3 position,
                                                out quaternion rotation)
    {
        position = float3.zero;
        rotation = quaternion.identity;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomCenterAnchor>());

        try
        {
            if (query.CalculateEntityCount() != 1)
                return false;

            GameRoomCenterAnchor anchor = query.GetSingleton<GameRoomCenterAnchor>();
            position = anchor.Position;
            rotation = anchor.Rotation;
            return true;
        }
        finally
        {
            query.Dispose();
        }
    }

    /// <summary>
    /// Resolves the physical entrance selected by the generated incoming edge.
    /// </summary>
    /// <param name="entityManager">Entity manager owning active room portals.</param>
    /// <param name="portalId">Graph-selected target portal ID.</param>
    /// <param name="position">Resolved authored arrival position.</param>
    /// <param name="rotation">Resolved authored arrival rotation.</param>
    /// <returns>True when exactly one matching target portal exists.</returns>
    private static bool TryResolvePortalArrival(EntityManager entityManager,
                                                FixedString64Bytes portalId,
                                                out float3 position,
                                                out quaternion rotation)
    {
        position = float3.zero;
        rotation = quaternion.identity;

        if (portalId.Length <= 0)
            return false;

        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomPortal>());
        NativeArray<GameRoomPortal> portals = default;

        try
        {
            portals = query.ToComponentDataArray<GameRoomPortal>(Allocator.Temp);
            int matchCount = 0;

            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
            {
                if (!portals[portalIndex].PortalId.Equals(portalId))
                    continue;

                position = portals[portalIndex].ArrivalPosition;
                rotation = portals[portalIndex].ArrivalRotation;
                matchCount++;
            }

            return matchCount == 1;
        }
        finally
        {
            if (portals.IsCreated)
                portals.Dispose();

            query.Dispose();
        }
    }
    #endregion

    #region Player
    /// <summary>
    /// Relocates the unique persistent player and optionally clears movement and held input state.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the persistent player.</param>
    /// <param name="config">Baked transition settings.</param>
    /// <param name="position">Target world position.</param>
    /// <param name="rotation">Target world rotation.</param>
    /// <returns>True when exactly one player was available and relocated.</returns>
    private static bool TryRelocatePlayer(EntityManager entityManager,
                                          GameProceduralLevelConfig config,
                                          float3 position,
                                          quaternion rotation)
    {
        EntityQuery playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                                  ComponentType.ReadWrite<LocalTransform>());

        try
        {
            if (playerQuery.CalculateEntityCount() != 1)
                return false;

            Entity playerEntity = playerQuery.GetSingletonEntity();
            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            transform.Position = position;
            transform.Rotation = rotation;
            entityManager.SetComponentData(playerEntity, transform);
            ApplyArrivalFacing(entityManager, playerEntity, rotation);

            if (config.ClearPlayerVelocity != 0)
                ClearPlayerMotion(entityManager, playerEntity);

            return true;
        }
        finally
        {
            playerQuery.Dispose();
        }
    }

    /// <summary>
    /// Aligns optional look state with the authored arrival rotation so the next controller tick preserves facing.
    /// </summary>
    /// <param name="entityManager">Entity manager owning optional player look state.</param>
    /// <param name="playerEntity">Relocated persistent player entity.</param>
    /// <param name="rotation">Authored arrival rotation.</param>
    private static void ApplyArrivalFacing(EntityManager entityManager, Entity playerEntity, quaternion rotation)
    {
        if (!entityManager.HasComponent<PlayerLookState>(playerEntity))
            return;

        PlayerLookState lookState = entityManager.GetComponentData<PlayerLookState>(playerEntity);
        float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
        forward.y = 0f;

        if (math.lengthsq(forward) <= 0.0001f)
            forward = new float3(0f, 0f, 1f);
        else
            forward = math.normalize(forward);

        lookState.DesiredDirection = forward;
        lookState.CurrentDirection = forward;
        lookState.AngularSpeed = 0f;
        entityManager.SetComponentData(playerEntity, lookState);
    }

    /// <summary>
    /// Clears mutable movement and input channels that could carry momentum across a room boundary.
    /// </summary>
    /// <param name="entityManager">Entity manager owning optional player movement and input state.</param>
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
            movementState.ReleaseHoldMask = 0;
            entityManager.SetComponentData(playerEntity, movementState);
        }

        if (entityManager.HasComponent<PlayerInputState>(playerEntity))
        {
            PlayerInputState inputState = entityManager.GetComponentData<PlayerInputState>(playerEntity);
            inputState.Move = float2.zero;
            inputState.Look = float2.zero;
            inputState.Shoot = 0f;
            inputState.PowerUpPrimary = 0f;
            inputState.PowerUpSecondary = 0f;
            inputState.SwapPowerUpSlots = 0f;
            inputState.MoveUsesAnalogSource = 0;
            inputState.LookUsesAnalogSource = 0;
            entityManager.SetComponentData(playerEntity, inputState);
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
                                                                  typeof(GameRoomPortalRuntimeState));
        NativeArray<Entity> portalEntities = default;

        try
        {
            portalEntities = portalQuery.ToEntityArray(Allocator.Temp);
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
            if (portalEntities.IsCreated)
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
