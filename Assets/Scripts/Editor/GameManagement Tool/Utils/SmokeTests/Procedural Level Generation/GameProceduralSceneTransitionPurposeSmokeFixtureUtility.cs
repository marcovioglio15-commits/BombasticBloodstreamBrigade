#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Builds deterministic ECS fixtures shared by procedural scene-transition purpose smoke checks.
/// </summary>
internal static class GameProceduralSceneTransitionPurposeSmokeFixtureUtility
{
    #region Constants
    private const string ReusableRoomSceneId = "SCN_REUSABLE_ROOM";
    #endregion

    #region Methods

    #region Fixture Methods
    /// <summary>
    /// Creates the minimal valid Scene Manager singleton required to evaluate one same-scene request.
    /// </summary>
    /// <param name="entityManager">Isolated fixture entity manager.</param>
    /// <param name="purpose">Purpose assigned to the queued request.</param>
    /// <returns>Created Scene Manager singleton entity.</returns>
    internal static Entity CreateManager(EntityManager entityManager, GameSceneTransitionPurpose purpose)
    {
        Entity managerEntity = entityManager.CreateEntity(typeof(GameSceneManagerConfig),
                                                           typeof(GameSceneTransitionState),
                                                           typeof(GameSceneFadePresentationState),
                                                           typeof(GameSceneLoadingProgressPresentationState));
        entityManager.SetComponentData(managerEntity, new GameSceneManagerConfig
        {
            AutoLoadInitialScene = 0,
            FadeOutSeconds = 1f,
            PostLoadReadyExtraSeconds = 0f,
            FadeInSeconds = 1f,
            SetTimeScaleDuringTransition = 0,
            LoadBackend = GameSceneLoadBackend.BuildSettings
        });
        entityManager.SetComponentData(managerEntity, new GameSceneTransitionState
        {
            ActiveSceneId = new FixedString64Bytes(ReusableRoomSceneId),
            Phase = GameSceneTransitionPhase.Idle,
            Purpose = GameSceneTransitionPurpose.Standard,
            Initialized = 1,
            IsTransitioning = 0
        });
        DynamicBuffer<GameSceneDefinitionElement> scenes = entityManager.AddBuffer<GameSceneDefinitionElement>(managerEntity);
        scenes.Add(new GameSceneDefinitionElement
        {
            SceneId = new FixedString64Bytes(ReusableRoomSceneId),
            SceneName = new FixedString64Bytes("ReusableRoom"),
            ScenePath = new FixedString512Bytes("Assets/Scenes/ReusableRoom.unity"),
            SceneKind = GameSceneKind.Gameplay,
            UnloadPolicy = GameSceneUnloadPolicy.UnloadOnTransition,
            BuildIndex = 0,
            OrderIndex = 0
        });
        entityManager.AddBuffer<GameSceneTransitionElement>(managerEntity);
        DynamicBuffer<GameSceneTransitionRequest> requests = entityManager.AddBuffer<GameSceneTransitionRequest>(managerEntity);
        requests.Add(new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadScene,
            Purpose = purpose,
            TargetSceneId = new FixedString64Bytes(ReusableRoomSceneId),
            TransitionId = default
        });
        return managerEntity;
    }

    /// <summary>
    /// Completes the generation-system query shape so the ordering fixture contains every constrained system.
    /// </summary>
    /// <param name="entityManager">Isolated fixture entity manager.</param>
    /// <param name="managerEntity">Existing traversal manager receiving generation data.</param>
    internal static void AddGenerationQueryShape(EntityManager entityManager, Entity managerEntity)
    {
        entityManager.AddComponentData(managerEntity, new GameProceduralLevelConfig());
        entityManager.AddComponentData(managerEntity, new GameProceduralRoomClearCounter());
        DynamicBuffer<GameSceneDefinitionElement> scenes = entityManager.AddBuffer<GameSceneDefinitionElement>(managerEntity);
        scenes.Add(new GameSceneDefinitionElement
        {
            SceneId = new FixedString64Bytes(ReusableRoomSceneId),
            SceneKind = GameSceneKind.Gameplay
        });
        entityManager.AddBuffer<GameProceduralLevelDefinitionElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomTileElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomMetadataElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomPortalDefinitionElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralLevelRunRequest>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomClearedEvent>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomEnteredEvent>(managerEntity);
    }

    /// <summary>
    /// Creates a valid traversal graph with a pending portal command and a higher-priority Standard request.
    /// </summary>
    /// <param name="entityManager">Isolated fixture entity manager.</param>
    /// <returns>Created procedural manager singleton entity.</returns>
    internal static Entity CreateTraversalManager(EntityManager entityManager)
    {
        Entity managerEntity = entityManager.CreateEntity(typeof(GameSceneManagerConfig),
                                                           typeof(GameSceneTransitionState),
                                                           typeof(GameProceduralLevelRuntimeState),
                                                           typeof(GameProceduralRoomTransitionContext));
        entityManager.SetComponentData(managerEntity, new GameSceneManagerConfig
        {
            DefaultTriggerCooldownSeconds = 0f
        });
        entityManager.SetComponentData(managerEntity, new GameSceneTransitionState
        {
            ActiveSceneId = new FixedString64Bytes(ReusableRoomSceneId),
            Phase = GameSceneTransitionPhase.Idle,
            Purpose = GameSceneTransitionPurpose.Standard,
            Initialized = 1,
            IsTransitioning = 0
        });
        entityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
        {
            CurrentLevelIndex = 0,
            CurrentNodeIndex = 0,
            PendingNodeIndex = -1,
            Phase = GameProceduralLevelRuntimePhase.Active,
            Initialized = 1,
            GraphGenerated = 1,
            CurrentRoomCleared = 1
        });
        entityManager.SetComponentData(managerEntity, new GameProceduralRoomTransitionContext
        {
            SourceNodeIndex = 17,
            TargetNodeIndex = 23,
            Kind = GameProceduralRoomTransitionKind.None
        });
        entityManager.AddComponentData(managerEntity, new GameProceduralLevelConfig
        {
            RoomStreamingMode = GameProceduralRoomStreamingMode.SerialSceneReplacement
        });
        DynamicBuffer<GameSceneDefinitionElement> scenes = entityManager.AddBuffer<GameSceneDefinitionElement>(managerEntity);
        scenes.Add(new GameSceneDefinitionElement
        {
            SceneId = new FixedString64Bytes(ReusableRoomSceneId),
            SceneKind = GameSceneKind.Gameplay
        });
        scenes.Add(new GameSceneDefinitionElement
        {
            SceneId = new FixedString64Bytes("SCN_NEXT_ROOM"),
            SceneKind = GameSceneKind.Gameplay
        });
        DynamicBuffer<GameProceduralRoomNodeElement> nodes = entityManager.AddBuffer<GameProceduralRoomNodeElement>(managerEntity);
        nodes.Add(new GameProceduralRoomNodeElement
        {
            NodeIndex = 0,
            SceneId = new FixedString64Bytes(ReusableRoomSceneId)
        });
        nodes.Add(new GameProceduralRoomNodeElement
        {
            NodeIndex = 1,
            SceneId = new FixedString64Bytes("SCN_NEXT_ROOM")
        });
        DynamicBuffer<GameProceduralRoomEdgeElement> edges = entityManager.AddBuffer<GameProceduralRoomEdgeElement>(managerEntity);
        edges.Add(new GameProceduralRoomEdgeElement
        {
            SourcePortalId = new FixedString64Bytes("PORTAL_EAST"),
            TargetPortalId = new FixedString64Bytes("PORTAL_WEST"),
            EdgeIndex = 7,
            SourceNodeIndex = 0,
            TargetNodeIndex = 1
        });
        DynamicBuffer<GameProceduralRoomTraversalRequest> traversalRequests = entityManager.AddBuffer<GameProceduralRoomTraversalRequest>(managerEntity);
        traversalRequests.Add(new GameProceduralRoomTraversalRequest
        {
            SourcePortalId = new FixedString64Bytes("PORTAL_EAST"),
            SourceNodeIndex = 0,
            AssignedEdgeIndex = 7
        });
        entityManager.AddBuffer<GameSceneTransitionRequest>(managerEntity);
        entityManager.AddBuffer<GameSceneTransitionElement>(managerEntity);
        AddSceneRequest(entityManager,
                        managerEntity,
                        GameSceneTransitionPurpose.Standard,
                        "SCN_MAIN_MENU");
        return managerEntity;
    }

    /// <summary>
    /// Creates the complete procedural-generation singleton shape with one active scene definition.
    /// </summary>
    /// <param name="entityManager">Isolated fixture entity manager.</param>
    /// <param name="activeSceneKind">Scene kind used to select generation waiting or run-reset behavior.</param>
    /// <returns>Created procedural manager singleton entity.</returns>
    internal static Entity CreateGenerationManager(EntityManager entityManager, GameSceneKind activeSceneKind)
    {
        Entity managerEntity = entityManager.CreateEntity(typeof(GameSceneTransitionState),
                                                           typeof(GameProceduralLevelConfig),
                                                           typeof(GameProceduralLevelRuntimeState),
                                                           typeof(GameProceduralRoomTransitionContext),
                                                           typeof(GameProceduralRoomClearCounter));
        entityManager.SetComponentData(managerEntity, new GameSceneTransitionState
        {
            ActiveSceneId = new FixedString64Bytes(ReusableRoomSceneId),
            Phase = GameSceneTransitionPhase.Idle,
            Purpose = GameSceneTransitionPurpose.Standard,
            Initialized = 1,
            IsTransitioning = 0
        });
        entityManager.SetComponentData(managerEntity, new GameProceduralLevelConfig
        {
            SeedMode = GameProceduralLevelSeedMode.External
        });
        entityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
        {
            CurrentLevelIndex = -1,
            CurrentNodeIndex = -1,
            PendingNodeIndex = -1,
            Phase = GameProceduralLevelRuntimePhase.Uninitialized
        });
        entityManager.SetComponentData(managerEntity, new GameProceduralRoomTransitionContext
        {
            SourceNodeIndex = -1,
            TargetNodeIndex = -1
        });
        DynamicBuffer<GameSceneDefinitionElement> scenes = entityManager.AddBuffer<GameSceneDefinitionElement>(managerEntity);
        scenes.Add(new GameSceneDefinitionElement
        {
            SceneId = new FixedString64Bytes(ReusableRoomSceneId),
            SceneName = new FixedString64Bytes("ReusableRoom"),
            SceneKind = activeSceneKind,
            UnloadPolicy = GameSceneUnloadPolicy.UnloadOnTransition
        });
        entityManager.AddBuffer<GameSceneTransitionRequest>(managerEntity);
        entityManager.AddBuffer<GameProceduralLevelDefinitionElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomTileElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomMetadataElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomPortalDefinitionElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomNodeElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomEdgeElement>(managerEntity);
        entityManager.AddBuffer<GameProceduralLevelRunRequest>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomTraversalRequest>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomClearedEvent>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomEnteredEvent>(managerEntity);
        return managerEntity;
    }

    /// <summary>
    /// Appends one scene request to a fixture manager's shared transition queue.
    /// </summary>
    /// <param name="entityManager">Isolated fixture entity manager.</param>
    /// <param name="managerEntity">Manager singleton owning the request queue.</param>
    /// <param name="purpose">Request purpose used by arbitration.</param>
    /// <param name="targetSceneId">Target scene identifier stored by the request.</param>
    /// <param name="requestType">Scene operation requested from the authoritative manager.</param>
    internal static void AddSceneRequest(EntityManager entityManager,
                                         Entity managerEntity,
                                         GameSceneTransitionPurpose purpose,
                                         string targetSceneId,
                                         GameSceneTransitionRequestType requestType = GameSceneTransitionRequestType.LoadScene)
    {
        entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity).Add(new GameSceneTransitionRequest
        {
            RequestType = requestType,
            Purpose = purpose,
            TargetSceneId = new FixedString64Bytes(targetSceneId)
        });
    }
    #endregion

    #endregion
}
#endif
