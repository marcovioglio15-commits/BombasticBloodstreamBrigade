using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Connects procedural transition requests to preloaded room instances without exposing streaming internals to the generic executor.
/// </summary>
internal static class GameProceduralRoomTransitionTransactionUtility
{
    #region Methods

    #region Policy
    /// <summary>
    /// Resolves whether one procedural request uses explicit exact-instance room ownership.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the procedural configuration.</param>
    /// <param name="managerEntity">Unique scene and procedural manager entity.</param>
    /// <param name="purpose">Transition purpose being executed.</param>
    /// <returns>True when the purpose is procedural and single-slot or dual-slot transactional streaming is enabled.</returns>
    public static bool UsesTransactionalStreaming(EntityManager entityManager,
                                                  Entity managerEntity,
                                                  GameSceneTransitionPurpose purpose)
    {
        if (!GameSceneTransitionPurposeUtility.IsProcedural(purpose) ||
            !entityManager.HasComponent<GameProceduralLevelConfig>(managerEntity))
        {
            return false;
        }

        return IsExplicitInstanceStreaming(entityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity).RoomStreamingMode);
    }

    /// <summary>
    /// Resolves whether one procedural request must unload its exact active room before loading the destination instance.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the procedural configuration.</param>
    /// <param name="managerEntity">Unique scene and procedural manager entity.</param>
    /// <param name="purpose">Transition purpose being executed.</param>
    /// <returns>True when authoritative single-slot ownership is active for the procedural request.</returns>
    public static bool UsesSingleSlotStreaming(EntityManager entityManager,
                                               Entity managerEntity,
                                               GameSceneTransitionPurpose purpose)
    {
        if (!GameSceneTransitionPurposeUtility.IsProcedural(purpose) ||
            !entityManager.HasComponent<GameProceduralLevelConfig>(managerEntity))
        {
            return false;
        }

        return entityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity).RoomStreamingMode ==
               GameProceduralRoomStreamingMode.AuthoredSingleSlot;
    }

    /// <summary>
    /// Checks whether one mode explicitly owns exact managed and DOTS room instances.
    /// </summary>
    /// <param name="streamingMode">Baked procedural room streaming mode.</param>
    /// <returns>True for authored single-slot and optional dual-slot ownership.</returns>
    private static bool IsExplicitInstanceStreaming(GameProceduralRoomStreamingMode streamingMode)
    {
        switch (streamingMode)
        {
            case GameProceduralRoomStreamingMode.AuthoredSingleSlot:
            case GameProceduralRoomStreamingMode.TransactionalDualSlot:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether one streaming mode translates concurrently resident room instances off-world.
    /// </summary>
    /// <param name="streamingMode">Baked procedural room streaming mode.</param>
    /// <returns>True only for the optional dual-slot spatial staging mode.</returns>
    public static bool IsSpatiallyAlignedStreaming(GameProceduralRoomStreamingMode streamingMode)
    {
        switch (streamingMode)
        {
            case GameProceduralRoomStreamingMode.TransactionalDualSlot:
                return true;
            default:
                return false;
        }
    }
    #endregion

    #region Commit
    /// <summary>
    /// Ensures the pending logical target is loaded and atomically commits it once its exact instance is ready.
    /// </summary>
    /// <param name="entityManager">Entity manager owning procedural context and DOTS room scenes.</param>
    /// <param name="managerEntity">Unique scene and procedural manager entity.</param>
    /// <param name="targetScene">Managed scene definition referenced by the pending node.</param>
    /// <param name="loadBackend">Configured managed scene loading backend.</param>
    /// <param name="sourceOwnedByStreaming">True when the previous active room was retired by the transaction.</param>
    /// <returns>True when the target instance was ready and committed.</returns>
    public static bool TryCommitPendingTarget(EntityManager entityManager,
                                              Entity managerEntity,
                                              GameSceneDefinitionElement targetScene,
                                              GameSceneLoadBackend loadBackend,
                                              out bool sourceOwnedByStreaming)
    {
        sourceOwnedByStreaming = false;

        if (!entityManager.HasComponent<GameProceduralRoomTransitionContext>(managerEntity))
            return false;

        GameProceduralRoomTransitionContext context = entityManager.GetComponentData<GameProceduralRoomTransitionContext>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        GameProceduralLevelConfig config = entityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);
        ulong generationKey = GameProceduralRoomStreamingSystem.BuildGenerationKey(runtimeState);

        if (context.TargetNodeIndex < 0)
            return false;

        if (!GameProceduralRoomStreamingRuntimeUtility.EnsureNodeLoading(generationKey,
                                                                         context.TargetNodeIndex,
                                                                         targetScene,
                                                                         loadBackend,
                                                                         IsSpatiallyAlignedStreaming(config.RoomStreamingMode)))
        {
            return false;
        }

        GameProceduralRoomStreamingRuntimeUtility.TickLoading(entityManager);

        if (!GameProceduralRoomStreamingRuntimeUtility.IsNodeReady(generationKey, context.TargetNodeIndex))
            return false;

        if (!TryResolveTargetPlacementOffset(entityManager,
                                             generationKey,
                                             context,
                                             config.RoomStreamingMode,
                                             out float3 targetPlacementOffset))
        {
            return false;
        }

        return GameProceduralRoomStreamingRuntimeUtility.TryCommitNode(entityManager,
                                                                       generationKey,
                                                                       context.TargetNodeIndex,
                                                                       targetPlacementOffset,
                                                                       out sourceOwnedByStreaming);
    }

    /// <summary>
    /// Resolves zero for authored single-slot and run boundaries, or aligns an optional dual-slot target around the
    /// unchanged player position.
    /// </summary>
    /// <param name="entityManager">Entity manager owning player and exact target portal data.</param>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="context">Pending procedural transition context.</param>
    /// <param name="streamingMode">Baked room ownership and placement policy.</param>
    /// <param name="targetPlacementOffset">World translation applied when the staged target becomes active.</param>
    /// <returns>True when no alignment is required or both target portal and unique player position were resolved.</returns>
    private static bool TryResolveTargetPlacementOffset(EntityManager entityManager,
                                                        ulong generationKey,
                                                        GameProceduralRoomTransitionContext context,
                                                        GameProceduralRoomStreamingMode streamingMode,
                                                        out float3 targetPlacementOffset)
    {
        targetPlacementOffset = float3.zero;

        if (context.Kind != GameProceduralRoomTransitionKind.IntraLevel ||
            !IsSpatiallyAlignedStreaming(streamingMode))
            return true;

        if (!GameProceduralRoomStreamingRuntimeUtility.TryResolveNodePortalArrival(entityManager,
                                                                                   generationKey,
                                                                                   context.TargetNodeIndex,
                                                                                   context.TargetPortalId,
                                                                                   out float3 targetArrivalPosition,
                                                                                   out float3 currentPlacementOffset))
        {
            return false;
        }

        EntityQuery playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                                  ComponentType.ReadOnly<LocalTransform>());

        try
        {
            if (playerQuery.CalculateEntityCount() != 1)
                return false;

            LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(playerQuery.GetSingletonEntity());
            targetPlacementOffset = currentPlacementOffset + playerTransform.Position - targetArrivalPosition;
            return true;
        }
        finally
        {
            playerQuery.Dispose();
        }
    }
    #endregion

    #endregion
}
