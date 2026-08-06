#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;

/// <summary>
/// Verifies that authoritative room-clear rewards reach both preauthored player and portal world-space views.
/// </summary>
internal static class GameRoomRewardPresentationPlayModeSmokeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Waits for room completion, then verifies one player log delivery and one visible outgoing portal log.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager owning procedural and player runtime data.</param>
    /// <param name="managerEntity">Unique procedural manager singleton.</param>
    /// <param name="ready">True when both presentation paths contain live content.</param>
    /// <param name="failure">Immediate structural failure that cannot be resolved by waiting for another frame.</param>
    /// <returns>True when validation may continue; false when the presentation configuration is invalid.</returns>
    public static bool TryValidate(EntityManager entityManager,
                                   Entity managerEntity,
                                   out bool ready,
                                   out string failure)
    {
        ready = false;
        failure = string.Empty;
        GameProceduralLevelRuntimeState runtimeState =
            entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);

        // Commit through the production transaction so the batch test does not depend on manually defeating waves.
        if (runtimeState.CurrentRoomCleared == 0)
        {
            GameProceduralRoomCompletionTransactionUtility.TryCommit(entityManager,
                                                                     managerEntity);
            return true;
        }

        if (!TryValidatePlayerLog(entityManager, out bool playerReady, out failure))
            return false;

        if (!playerReady)
            return true;

        if (!TryValidatePortalLog(entityManager,
                                      managerEntity,
                                      runtimeState.CurrentNodeIndex,
                                      out bool portalReady,
                                      out failure))
            return false;

        ready = portalReady;
        return true;
    }
    #endregion

    #region Player Validation
    /// <summary>
    /// Resolves the preauthored player log and waits until it has consumed at least one authoritative reward event.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the unique player.</param>
    /// <param name="ready">True after the runtime view accepted a formatted reward entry.</param>
    /// <param name="failure">Structural player-view failure.</param>
    /// <returns>True when the view exists or may still become available; false for an invalid hierarchy.</returns>
    private static bool TryValidatePlayerLog(EntityManager entityManager,
                                             out bool ready,
                                             out string failure)
    {
        ready = false;
        failure = string.Empty;
        EntityQuery playerQuery =
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                            ComponentType.ReadOnly<PlayerRoomRewardPresentationEvent>());

        try
        {
            if (playerQuery.CalculateEntityCount() != 1)
                return true;

            Entity playerEntity = playerQuery.GetSingletonEntity();

            if (!PlayerManagedVisualAnimatorBridgeSystem.TryGetRuntimeBridgeRoot(playerEntity,
                                                                                 out Transform visualRoot))
                return true;

            PlayerRoomRewardLogView view =
                visualRoot.GetComponentInChildren<PlayerRoomRewardLogView>(true);

            if (view == null)
            {
                failure = "The runtime player visual has no preauthored PlayerRoomRewardLogView.";
                return false;
            }

            ready = view.TotalEnqueuedItems > 0;
            return true;
        }
        finally
        {
            playerQuery.Dispose();
        }
    }
    #endregion

    #region Portal Validation
    /// <summary>
    /// Finds a graph-assigned outgoing portal and verifies that its matching managed-scene log is visible.
    /// </summary>
    /// <param name="entityManager">Entity manager owning generated graph and physical portals.</param>
    /// <param name="managerEntity">Procedural manager owning the graph buffers.</param>
    /// <param name="currentNodeIndex">Active graph node whose outgoing portal should be presented.</param>
    /// <param name="ready">True when one matching log contains visible destination rewards.</param>
    /// <param name="failure">Structural graph or portal failure.</param>
    /// <returns>True when the view exists or may still rebuild; false for invalid runtime assignments.</returns>
    private static bool TryValidatePortalLog(EntityManager entityManager,
                                                 Entity managerEntity,
                                                 int currentNodeIndex,
                                                 out bool ready,
                                                 out string failure)
    {
        ready = false;
        failure = string.Empty;
        DynamicBuffer<GameProceduralRoomEdgeElement> edges =
            entityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity, true);
        EntityQuery portalQuery =
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomPortal>(),
                                            ComponentType.ReadOnly<GameRoomPortalRuntimeState>(),
                                            ComponentType.ReadOnly<SceneTag>());
        NativeList<Entity> portalEntities = new NativeList<Entity>(Allocator.Temp);

        try
        {
            GameProceduralRoomInstanceQueryUtility.CollectActiveRoomEntities(portalQuery,
                                                                              ref portalEntities);
            bool hasOutgoingEdge = false;
            bool hasAssignedPortal = false;

            // Resolve an exact graph-to-physical assignment before inspecting its managed presentation mirror.
            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                GameProceduralRoomEdgeElement edge = edges[edgeIndex];

                if (edge.SourceNodeIndex != currentNodeIndex)
                    continue;

                hasOutgoingEdge = true;

                for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
                {
                    Entity portalEntity = portalEntities[portalIndex];
                    GameRoomPortal portal =
                        entityManager.GetComponentData<GameRoomPortal>(portalEntity);
                    GameRoomPortalRuntimeState portalState =
                        entityManager.GetComponentData<GameRoomPortalRuntimeState>(portalEntity);

                    if (portalState.AssignedEdgeIndex != edge.EdgeIndex ||
                        !portal.PortalId.Equals(edge.SourcePortalId))
                        continue;

                    hasAssignedPortal = true;

                    if (!GameRoomPortalRewardLogAnchor.TryResolve(
                            portal.PortalId,
                            portal.Center,
                            out GameRoomPortalRewardLogView view))
                        continue;

                    if (view != null && view.HasVisibleContent)
                    {
                        ready = true;
                        return true;
                    }
                }
            }

            if (!hasOutgoingEdge)
            {
                failure = "The completed starting room has no outgoing graph edge to present.";
                return false;
            }

            if (!hasAssignedPortal)
            {
                failure = "No loaded physical portal owns an outgoing edge from the completed starting room.";
                return false;
            }

            return true;
        }
        finally
        {
            portalEntities.Dispose();
            portalQuery.Dispose();
        }
    }
    #endregion

    #endregion
}
#endif
