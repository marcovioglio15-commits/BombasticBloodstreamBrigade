#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Provides focused failure detection and runtime snapshots for procedural Play Mode transition regression tests.
/// </summary>
internal static class GameProceduralLevelTransitionSmokeDiagnosticUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks one log fragment for failures relevant to scene streaming, ECS lifetime or procedural generation.
    /// </summary>
    /// <param name="value">Log fragment to inspect.</param>
    /// <returns>True when the fragment contains a targeted runtime failure signature.</returns>
    public static bool ContainsTargetedFailure(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.IndexOf("ObjectDisposedException", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("Attempted to access BufferTypeHandle", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("BlobAssetReference is not valid", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("AsyncLoadSceneOperation", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("[GameSceneManagerAuthoring]", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("[GameProceduralLevel]", StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Builds a compact state snapshot when an asynchronous Play Mode transition exceeds its timeout.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager.</param>
    /// <param name="managerEntity">Scene and procedural manager singleton.</param>
    /// <returns>Transition, procedural context and target-portal state for the blocked readiness condition.</returns>
    public static string BuildRuntimeDiagnostic(EntityManager entityManager,
                                                Entity managerEntity)
    {
        GameSceneTransitionState transitionState =
            entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState =
            entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        GameProceduralRoomTransitionContext context =
            entityManager.GetComponentData<GameProceduralRoomTransitionContext>(managerEntity);
        EntityQuery portalQuery =
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomPortal>());
        int matchingPortalCount = 0;

        try
        {
            using NativeArray<GameRoomPortal> portals =
                portalQuery.ToComponentDataArray<GameRoomPortal>(Allocator.Temp);

            // Count only the graph-selected arrival identity so missing or stale authoring remains explicit.
            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
            {
                if (portals[portalIndex].PortalId.Equals(context.TargetPortalId))
                    matchingPortalCount++;
            }

            return "Transition=" + transitionState.Phase +
                   ", IsTransitioning=" + transitionState.IsTransitioning +
                   ", Active='" + transitionState.ActiveSceneId +
                   "', Target='" + transitionState.TargetSceneId +
                   "', RuntimePhase=" + runtimeState.Phase +
                   ", CurrentNode=" + runtimeState.CurrentNodeIndex +
                   ", PendingNode=" + runtimeState.PendingNodeIndex +
                   ", RelocationPending=" + context.RelocationPending +
                   ", CommitPending=" + context.CommitPending +
                   ", TargetPortal='" + context.TargetPortalId +
                   "', MatchingPortals=" + matchingPortalCount +
                   ", TotalPortals=" + portals.Length + ".";
        }
        finally
        {
            portalQuery.Dispose();
        }
    }
    #endregion

    #endregion
}
#endif
