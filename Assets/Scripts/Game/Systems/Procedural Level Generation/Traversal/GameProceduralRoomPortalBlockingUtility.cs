using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

/// <summary>
/// Synchronizes fail-closed portal barriers with graph assignments without polling them every frame.
/// </summary>
public static class GameProceduralRoomPortalBlockingUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Unity Physics filter that exposes a closed portal to existing player wall queries.
    /// </summary>
    /// <param name="wallsLayerMask">Unity layer bit used by PlayerWorldLayersConfig for wall collision.</param>
    /// <returns>Collider filter in the configured Walls category, or a universal fail-closed filter when unavailable.</returns>
    public static CollisionFilter BuildBlockingFilter(int wallsLayerMask)
    {
        uint wallsCategory = wallsLayerMask > 0 ? (uint)wallsLayerMask : 0u;

        if (wallsCategory == 0u)
        {
            return new CollisionFilter
            {
                BelongsTo = uint.MaxValue,
                CollidesWith = uint.MaxValue,
                GroupIndex = 0
            };
        }

        return new CollisionFilter
        {
            BelongsTo = wallsCategory,
            CollidesWith = uint.MaxValue,
            GroupIndex = 0
        };
    }

    /// <summary>
    /// Recomputes traversal availability from the active level and room-clear state before synchronizing physical barriers.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the active room and procedural state.</param>
    /// <param name="managerEntity">Unique procedural manager containing level and lifecycle data.</param>
    /// <returns>Number of blocker colliders whose open or closed state changed.</returns>
    public static int SynchronizeTraversalAvailability(EntityManager entityManager, Entity managerEntity)
    {
        return Synchronize(entityManager, managerEntity);
    }

    /// <summary>
    /// Synchronizes logical availability and physical colliders through one event-driven portal scan.
    /// </summary>
    /// <param name="entityManager">Entity manager owning portals and blockers.</param>
    /// <param name="managerEntity">Procedural manager used to recompute logical availability.</param>
    /// <returns>Number of blocker colliders whose state changed.</returns>
    private static int Synchronize(EntityManager entityManager,
                                   Entity managerEntity)
    {
        EntityQuery portalQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomPortal>(),
                                                                  ComponentType.ReadOnly<GameRoomPortalRuntimeState>());
        EntityQuery blockerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomPortalBlocker>(),
                                                                   ComponentType.ReadWrite<PhysicsCollider>());
        NativeArray<Entity> portalEntities = default;
        NativeArray<Entity> blockerEntities = default;

        try
        {
            portalEntities = portalQuery.ToEntityArray(Allocator.Temp);
            blockerEntities = blockerQuery.ToEntityArray(Allocator.Temp);
            int changedCount = 0;

            UpdateTraversalAvailability(entityManager, managerEntity, portalEntities);

            // Resolve each barrier independently so missing or duplicate logical IDs remain safely closed.
            for (int blockerIndex = 0; blockerIndex < blockerEntities.Length; blockerIndex++)
            {
                Entity blockerEntity = blockerEntities[blockerIndex];
                GameRoomPortalBlocker blocker = entityManager.GetComponentData<GameRoomPortalBlocker>(blockerEntity);
                bool shouldBlock = ResolveShouldBlock(entityManager,
                                                      portalEntities,
                                                      blocker.PortalId);

                if (SetBlockingState(entityManager, blockerEntity, shouldBlock))
                    changedCount++;
            }

            return changedCount;
        }
        finally
        {
            if (portalEntities.IsCreated)
                portalEntities.Dispose();

            if (blockerEntities.IsCreated)
                blockerEntities.Dispose();

            portalQuery.Dispose();
            blockerQuery.Dispose();
        }
    }

    /// <summary>
    /// Changes one barrier by swapping only its PhysicsCollider reference while preserving the baked collider blob.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the blocker.</param>
    /// <param name="blockerEntity">Static portal blocker entity to update.</param>
    /// <param name="shouldBlock">Whether the baked Walls collider must participate in the next physics build.</param>
    /// <returns>True when the collider state changed; false when it was already synchronized or invalid.</returns>
    public static bool SetBlockingState(EntityManager entityManager,
                                        Entity blockerEntity,
                                        bool shouldBlock)
    {
        if (!entityManager.Exists(blockerEntity) ||
            !entityManager.HasComponent<GameRoomPortalBlocker>(blockerEntity) ||
            !entityManager.HasComponent<PhysicsCollider>(blockerEntity))
        {
            return false;
        }

        GameRoomPortalBlocker blocker = entityManager.GetComponentData<GameRoomPortalBlocker>(blockerEntity);
        PhysicsCollider physicsCollider = entityManager.GetComponentData<PhysicsCollider>(blockerEntity);
        bool isBlocking = blocker.IsBlocking != 0;
        bool hasBlockingCollider = physicsCollider.Value.IsCreated;

        if (isBlocking == shouldBlock && hasBlockingCollider == shouldBlock)
            return false;

        physicsCollider.Value = shouldBlock ? blocker.BlockingCollider : default;
        blocker.IsBlocking = shouldBlock ? (byte)1 : (byte)0;
        entityManager.SetComponentData(blockerEntity, physicsCollider);
        entityManager.SetComponentData(blockerEntity, blocker);
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates each graph assignment from the current room-clear policy without polling between lifecycle events.
    /// </summary>
    /// <param name="entityManager">Entity manager owning portal and manager data.</param>
    /// <param name="managerEntity">Procedural manager whose lifecycle unlocks exits.</param>
    /// <param name="portalEntities">Active room logical portals.</param>
    private static void UpdateTraversalAvailability(EntityManager entityManager,
                                                    Entity managerEntity,
                                                    NativeArray<Entity> portalEntities)
    {
        bool hasValidManager = managerEntity != Entity.Null &&
                               entityManager.Exists(managerEntity) &&
                               entityManager.HasComponent<GameProceduralLevelRuntimeState>(managerEntity) &&
                               entityManager.HasBuffer<GameProceduralLevelDefinitionElement>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState = hasValidManager
            ? entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity)
            : default;
        DynamicBuffer<GameProceduralLevelDefinitionElement> levels = hasValidManager
            ? entityManager.GetBuffer<GameProceduralLevelDefinitionElement>(managerEntity, true)
            : default;
        bool hasActiveLevel = hasValidManager &&
                              runtimeState.CurrentLevelIndex >= 0 &&
                              runtimeState.CurrentLevelIndex < levels.Length;
        bool levelRequiresClear = hasActiveLevel &&
                                  levels[runtimeState.CurrentLevelIndex].RequireRoomClearBeforeExit != 0;

        // Fail closed when lifecycle ownership is unavailable; otherwise mirror the traversal gate once per event.
        for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
        {
            Entity portalEntity = portalEntities[portalIndex];
            GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portalEntity);
            GameRoomPortalRuntimeState portalState = entityManager.GetComponentData<GameRoomPortalRuntimeState>(portalEntity);
            portalState.TraversalEnabled = hasActiveLevel &&
                                           HasUniquePortalIdentity(entityManager,
                                                                   portalEntities,
                                                                   portal.PortalId) &&
                                           IsTraversalAvailable(portal,
                                                                portalState.AssignedEdgeIndex,
                                                                runtimeState,
                                                                levelRequiresClear)
                ? (byte)1
                : (byte)0;
            entityManager.SetComponentData(portalEntity, portalState);
        }
    }

    /// <summary>
    /// Resolves whether exactly one logical portal owns a stable identity inside the active room.
    /// </summary>
    /// <param name="entityManager">Entity manager owning logical portal data.</param>
    /// <param name="portalEntities">All active room portal entities.</param>
    /// <param name="portalId">Stable identity to count.</param>
    /// <returns>True when exactly one active portal owns the identity.</returns>
    private static bool HasUniquePortalIdentity(EntityManager entityManager,
                                                NativeArray<Entity> portalEntities,
                                                FixedString64Bytes portalId)
    {
        int matchCount = 0;

        // Stop as soon as a duplicate is proven because this check runs only on lifecycle events.
        for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
        {
            if (!entityManager.GetComponentData<GameRoomPortal>(portalEntities[portalIndex]).PortalId.Equals(portalId))
                continue;

            matchCount++;

            if (matchCount > 1)
                return false;
        }

        return matchCount == 1;
    }

    /// <summary>
    /// Resolves whether one valid outgoing assignment is physically unlocked for the active lifecycle event.
    /// </summary>
    /// <param name="portal">Immutable authored portal policy.</param>
    /// <param name="assignedEdgeIndex">Generated edge index or traversal sentinel.</param>
    /// <param name="runtimeState">Current room and level lifecycle.</param>
    /// <param name="levelRequiresClear">Whether every regular exit is gated by room completion.</param>
    /// <returns>True when the physical blocker may be removed.</returns>
    private static bool IsTraversalAvailable(GameRoomPortal portal,
                                             int assignedEdgeIndex,
                                             GameProceduralLevelRuntimeState runtimeState,
                                             bool levelRequiresClear)
    {
        if (!SupportsOutgoingTraversal(portal.Capability))
            return false;

        if (assignedEdgeIndex == GameProceduralRoomTraversalConstants.LevelExitEdgeIndex)
            return runtimeState.Phase == GameProceduralLevelRuntimePhase.LevelComplete &&
                   runtimeState.CurrentRoomCleared != 0;

        if (assignedEdgeIndex < 0)
            return false;

        if ((levelRequiresClear || portal.RequireRoomClear != 0) && runtimeState.CurrentRoomCleared == 0)
            return false;

        switch (runtimeState.Phase)
        {
            case GameProceduralLevelRuntimePhase.LoadingInitialRoom:
            case GameProceduralLevelRuntimePhase.Active:
            case GameProceduralLevelRuntimePhase.Traversing:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves one barrier state from exactly one matching logical portal, failing closed for invalid authoring.
    /// </summary>
    /// <param name="entityManager">Entity manager owning logical portal state.</param>
    /// <param name="portalEntities">All logical portals in the active room scene.</param>
    /// <param name="portalId">Blocker identity to pair with one logical portal.</param>
    /// <returns>True unless one unique outgoing-capable portal owns a valid enabled assignment.</returns>
    private static bool ResolveShouldBlock(EntityManager entityManager,
                                           NativeArray<Entity> portalEntities,
                                           FixedString64Bytes portalId)
    {
        int matchCount = 0;
        bool hasOpenAssignment = false;

        // Preserve individual same-side portals by pairing through their stable technical IDs.
        for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
        {
            Entity portalEntity = portalEntities[portalIndex];
            GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portalEntity);

            if (!portal.PortalId.Equals(portalId))
                continue;

            GameRoomPortalRuntimeState portalState = entityManager.GetComponentData<GameRoomPortalRuntimeState>(portalEntity);
            matchCount++;
            hasOpenAssignment = SupportsOutgoingTraversal(portal.Capability) &&
                                portalState.TraversalEnabled != 0 &&
                                portalState.AssignedEdgeIndex != GameProceduralRoomTraversalConstants.UnassignedEdgeIndex;
        }

        return matchCount != 1 || !hasOpenAssignment;
    }

    /// <summary>
    /// Resolves whether an authored capability may ever serve as a generated outgoing connection.
    /// </summary>
    /// <param name="capability">Authored portal capability.</param>
    /// <returns>True for Exit and Both capabilities.</returns>
    private static bool SupportsOutgoingTraversal(GameRoomPortalCapability capability)
    {
        switch (capability)
        {
            case GameRoomPortalCapability.Exit:
            case GameRoomPortalCapability.Both:
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
