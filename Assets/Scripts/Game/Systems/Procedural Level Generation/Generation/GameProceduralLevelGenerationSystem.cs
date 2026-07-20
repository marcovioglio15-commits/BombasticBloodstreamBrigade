using System;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Generates authoritative procedural graphs from baked buffers and queues their initial room scene transitions.
/// </summary>
[UpdateInGroup(typeof(GameSceneManagementSystemGroup))]
[UpdateAfter(typeof(GameSceneTransitionTriggerSystem))]
[UpdateBefore(typeof(GameSceneTransitionExecutionSystem))]
public partial class GameProceduralLevelGenerationSystem : SystemBase
{
    #region Constants
    private const string NoEnabledLevelsDiagnostic = "The procedural preset contains no enabled levels.";
    #endregion

    #region Fields
    private EntityQuery managerQuery;
    private bool loggedManagerCountWarning;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the unique manager query containing scene state, procedural configuration and generated graph buffers.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameSceneTransitionState),
                                      typeof(GameSceneDefinitionElement),
                                      typeof(GameSceneTransitionRequest),
                                      typeof(GameProceduralLevelConfig),
                                      typeof(GameProceduralLevelRuntimeState),
                                      typeof(GameProceduralLevelDefinitionElement),
                                      typeof(GameProceduralRoomTileElement),
                                      typeof(GameProceduralRoomMetadataElement),
                                      typeof(GameProceduralRoomPortalDefinitionElement),
                                      typeof(GameProceduralRoomNodeElement),
                                      typeof(GameProceduralRoomEdgeElement),
                                      typeof(GameProceduralRoomTraversalRequest),
                                      typeof(GameProceduralRoomTransitionContext),
                                      typeof(GameProceduralRoomClearCounter),
                                      typeof(GameProceduralLevelRunRequest));
    }

    /// <summary>
    /// Starts the first enabled level, advances after Boss completion or resets progression after leaving gameplay.
    /// </summary>
    protected override void OnUpdate()
    {
        int managerCount = managerQuery.CalculateEntityCount();

        if (managerCount != 1)
        {
            LogManagerCountWarning(managerCount);
            return;
        }

        loggedManagerCountWarning = false;
        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameSceneTransitionState transitionState = EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState = EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        DynamicBuffer<GameSceneTransitionRequest> sceneRequests = EntityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity);

        if (transitionState.IsTransitioning == 0 &&
            runtimeState.Initialized == 0 &&
            TryReplaceDefaultGameplayRequest(managerEntity, sceneRequests))
        {
            return;
        }

        if (HasStandardInterruption(transitionState, sceneRequests) &&
            HasProceduralRunState(managerEntity, runtimeState, sceneRequests))
        {
            ResetRun(managerEntity);
            return;
        }

        if (transitionState.IsTransitioning != 0)
            return;

        DynamicBuffer<GameSceneDefinitionElement> scenes = EntityManager.GetBuffer<GameSceneDefinitionElement>(managerEntity, true);

        if (runtimeState.Initialized != 0 &&
            !IsGameplayScene(scenes, transitionState.ActiveSceneId))
        {
            ResetRun(managerEntity);
            return;
        }

        if (sceneRequests.Length > 0)
            return;

        DynamicBuffer<GameProceduralLevelRunRequest> runRequests = EntityManager.GetBuffer<GameProceduralLevelRunRequest>(managerEntity);

        if (runRequests.Length > 0 && runRequests[0].Restart != 0)
        {
            GameProceduralLevelConfig restartConfig = EntityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);

            if (!TryResolveRunSeed(restartConfig, runRequests, out uint restartSeed))
                return;

            runRequests.RemoveAt(0);
            ResetRun(managerEntity);
            TryGenerateFirstLevel(managerEntity, scenes, restartSeed);
            return;
        }

        if (runtimeState.Phase == GameProceduralLevelRuntimePhase.Generating)
        {
            TryGenerateNextLevel(managerEntity, scenes, runtimeState);
            return;
        }

        if (runtimeState.Initialized != 0 ||
            !IsGameplayScene(scenes, transitionState.ActiveSceneId))
        {
            if (runtimeState.Initialized != 0 && runRequests.Length > 0)
                runRequests.RemoveAt(0);

            return;
        }

        GameProceduralLevelConfig config = EntityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);

        if (!TryResolveRunSeed(config, runRequests, out uint runSeed))
            return;

        if (runRequests.Length > 0)
            runRequests.RemoveAt(0);

        TryGenerateFirstLevel(managerEntity, scenes, runSeed);
    }
    #endregion

    #region Generation
    /// <summary>
    /// Finds and generates the first enabled level in authored order.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="scenes">Canonical Scene Manager catalog.</param>
    /// <param name="runSeed">Resolved authoritative run seed.</param>
    private void TryGenerateFirstLevel(Entity managerEntity,
                                       DynamicBuffer<GameSceneDefinitionElement> scenes,
                                       uint runSeed)
    {
        DynamicBuffer<GameProceduralLevelDefinitionElement> levels = EntityManager.GetBuffer<GameProceduralLevelDefinitionElement>(managerEntity, true);
        int levelIndex = FindNextEnabledLevel(levels, -1);

        if (levelIndex < 0)
        {
            SetGenerationFailure(managerEntity, NoEnabledLevelsDiagnostic);
            return;
        }

        GenerateLevel(managerEntity,
                      scenes,
                      levels[levelIndex],
                      levelIndex,
                      runSeed,
                      GameProceduralRoomTransitionKind.InitialRoom);
    }

    /// <summary>
    /// Finds and generates the next enabled level after one completed Boss room.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="scenes">Canonical Scene Manager catalog.</param>
    /// <param name="runtimeState">Current completed level state.</param>
    private void TryGenerateNextLevel(Entity managerEntity,
                                      DynamicBuffer<GameSceneDefinitionElement> scenes,
                                      GameProceduralLevelRuntimeState runtimeState)
    {
        DynamicBuffer<GameProceduralLevelDefinitionElement> levels = EntityManager.GetBuffer<GameProceduralLevelDefinitionElement>(managerEntity, true);
        int levelIndex = FindNextEnabledLevel(levels, runtimeState.CurrentLevelIndex);

        if (levelIndex < 0)
        {
            runtimeState.Phase = GameProceduralLevelRuntimePhase.RunComplete;
            EntityManager.SetComponentData(managerEntity, runtimeState);
            return;
        }

        GenerateLevel(managerEntity,
                      scenes,
                      levels[levelIndex],
                      levelIndex,
                      runtimeState.RunSeed,
                      GameProceduralRoomTransitionKind.LevelBoundary);
    }

    /// <summary>
    /// Runs the shared solver, writes ECS graph buffers and queues the generated Start room scene.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="scenes">Canonical Scene Manager catalog.</param>
    /// <param name="level">Baked level definition to generate.</param>
    /// <param name="levelIndex">Ordered level buffer index.</param>
    /// <param name="runSeed">Authoritative run seed shared across ordered levels.</param>
    /// <param name="transitionKind">Initial-run or level-boundary transition context.</param>
    private void GenerateLevel(Entity managerEntity,
                               DynamicBuffer<GameSceneDefinitionElement> scenes,
                               GameProceduralLevelDefinitionElement level,
                               int levelIndex,
                               uint runSeed,
                               GameProceduralRoomTransitionKind transitionKind)
    {
        GameProceduralLevelConfig config = EntityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);
        DynamicBuffer<GameProceduralRoomTileElement> tiles = EntityManager.GetBuffer<GameProceduralRoomTileElement>(managerEntity, true);
        DynamicBuffer<GameProceduralRoomMetadataElement> metadata = EntityManager.GetBuffer<GameProceduralRoomMetadataElement>(managerEntity, true);
        DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portals = EntityManager.GetBuffer<GameProceduralRoomPortalDefinitionElement>(managerEntity, true);

        if (!GameProceduralLevelRuntimeSolverInputUtility.TryBuild(config,
                                                                  level,
                                                                  tiles,
                                                                  metadata,
                                                                  portals,
                                                                  out GameProceduralLevelSolverInput solverInput,
                                                                  out string diagnostic))
        {
            SetGenerationFailure(managerEntity, diagnostic);
            return;
        }

        GameProceduralLevelGenerationResult result = GameProceduralLevelSolver.Generate(solverInput, runSeed);

        if (!result.Success)
        {
            SetGenerationFailure(managerEntity, result.FailureCode + ": " + result.Diagnostic);
            return;
        }

        if (!ValidateGeneratedSceneCatalog(scenes, result, out string sceneDiagnostic))
        {
            SetGenerationFailure(managerEntity, sceneDiagnostic);
            return;
        }

        int startNodeIndex = WriteGraph(managerEntity, level, levelIndex, tiles, result);

        if (startNodeIndex < 0)
        {
            SetGenerationFailure(managerEntity, "The generated graph contains no Start node.");
            return;
        }

        DynamicBuffer<GameProceduralRoomNodeElement> nodes = EntityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity, true);
        GameProceduralRoomNodeElement startNode = nodes[startNodeIndex];

        BeginGeneratedLevelTransition(managerEntity,
                                      levelIndex,
                                      result,
                                      startNode,
                                      transitionKind);
    }
    #endregion

    #region Graph Storage
    /// <summary>
    /// Replaces generated ECS graph buffers with one immutable solver result and resolves its Start node index.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="level">Generated baked level definition.</param>
    /// <param name="levelIndex">Ordered level buffer index.</param>
    /// <param name="tiles">Flattened reusable tile buffer used to resolve stable tile indices.</param>
    /// <param name="result">Successful shared solver result.</param>
    /// <returns>Generated Start node index, or -1 when none exists.</returns>
    private int WriteGraph(Entity managerEntity,
                           GameProceduralLevelDefinitionElement level,
                           int levelIndex,
                           DynamicBuffer<GameProceduralRoomTileElement> tiles,
                           GameProceduralLevelGenerationResult result)
    {
        DynamicBuffer<GameProceduralRoomNodeElement> nodeBuffer = EntityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity);
        DynamicBuffer<GameProceduralRoomEdgeElement> edgeBuffer = EntityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity);
        nodeBuffer.Clear();
        edgeBuffer.Clear();
        int startNodeIndex = -1;

        // Preserve solver IDs exactly so graph edges, portal assignments and editor preview use the same indexing.
        for (int nodeIndex = 0; nodeIndex < result.Nodes.Count; nodeIndex++)
        {
            GameProceduralLevelGraphNode node = result.Nodes[nodeIndex];
            nodeBuffer.Add(new GameProceduralRoomNodeElement
            {
                TileTechnicalId = new FixedString64Bytes(node.TileTechnicalId),
                TileId = new FixedString64Bytes(node.TileId),
                SceneId = new FixedString64Bytes(node.SceneId),
                NodeIndex = node.NodeId,
                LevelIndex = levelIndex,
                TileIndex = FindTileIndex(level, tiles, node.TileTechnicalId),
                Depth = node.Depth,
                Role = node.Role
            });

            if (node.Role == GameProceduralRoomRole.Start)
                startNodeIndex = node.NodeId;
        }

        for (int edgeIndex = 0; edgeIndex < result.Edges.Count; edgeIndex++)
        {
            GameProceduralLevelGraphEdge edge = result.Edges[edgeIndex];
            edgeBuffer.Add(new GameProceduralRoomEdgeElement
            {
                SourcePortalId = new FixedString64Bytes(edge.SourcePortalId),
                TargetPortalId = new FixedString64Bytes(edge.TargetPortalId),
                EdgeIndex = edge.EdgeId,
                SourceNodeIndex = edge.SourceNodeId,
                TargetNodeIndex = edge.TargetNodeId,
                SourceSide = edge.SourceSide,
                TargetSide = edge.TargetSide,
                UsesCenterArrival = edge.UsesCenterArrival ? (byte)1 : (byte)0
            });
        }

        return startNodeIndex;
    }

    /// <summary>
    /// Resolves one generated node's reusable tile index inside the current level's contiguous range.
    /// </summary>
    /// <param name="level">Generated level definition owning the tile range.</param>
    /// <param name="tiles">Flattened room tile buffer.</param>
    /// <param name="technicalId">Stable tile technical ID emitted by the solver.</param>
    /// <returns>Flattened tile index, or -1 when baked data is inconsistent.</returns>
    private static int FindTileIndex(GameProceduralLevelDefinitionElement level,
                                     DynamicBuffer<GameProceduralRoomTileElement> tiles,
                                     string technicalId)
    {
        for (int tileOffset = 0; tileOffset < level.TileCount; tileOffset++)
        {
            int tileIndex = level.TileStartIndex + tileOffset;

            if (string.Equals(tiles[tileIndex].TechnicalId.ToString(), technicalId, StringComparison.Ordinal))
                return tileIndex;
        }

        return -1;
    }
    #endregion

    #region Transition
    /// <summary>
    /// Writes pending logical-room context and enqueues the scene request for a generated Start node.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="levelIndex">Generated ordered level index.</param>
    /// <param name="result">Successful shared solver result.</param>
    /// <param name="startNode">Generated Start node to load.</param>
    /// <param name="transitionKind">Initial-run or level-boundary transition context.</param>
    private void BeginGeneratedLevelTransition(Entity managerEntity,
                                               int levelIndex,
                                               GameProceduralLevelGenerationResult result,
                                               GameProceduralRoomNodeElement startNode,
                                               GameProceduralRoomTransitionKind transitionKind)
    {
        GameProceduralLevelRuntimeState runtimeState = EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        runtimeState.FailureMessage = default;
        runtimeState.RunSeed = result.RunSeed;
        runtimeState.LevelSeed = result.LevelSeed;
        runtimeState.CurrentLevelIndex = levelIndex;
        runtimeState.CurrentNodeIndex = -1;
        runtimeState.PendingNodeIndex = startNode.NodeIndex;
        runtimeState.CurrentDepth = 0;
        runtimeState.Phase = GameProceduralLevelRuntimePhase.LoadingInitialRoom;
        runtimeState.Initialized = 1;
        runtimeState.GraphGenerated = 1;
        runtimeState.CurrentRoomCleared = 0;
        EntityManager.SetComponentData(managerEntity, runtimeState);
        EntityManager.SetComponentData(managerEntity, new GameProceduralRoomTransitionContext
        {
            SourceNodeIndex = -1,
            TargetNodeIndex = startNode.NodeIndex,
            Kind = transitionKind,
            UsesCenterArrival = 1,
            RelocationPending = 1,
            CommitPending = 1
        });

        DynamicBuffer<GameSceneTransitionRequest> requests = EntityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity);
        requests.Add(new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadScene,
            Purpose = transitionKind == GameProceduralRoomTransitionKind.InitialRoom
                ? GameSceneTransitionPurpose.ProceduralInitialRoom
                : GameSceneTransitionPurpose.ProceduralLevelBoundary,
            TargetSceneId = startNode.SceneId
        });
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Replaces the main-menu default-gameplay command with direct generation of the first procedural room.
    /// This prevents loading the fallback gameplay scene only to unload it again immediately afterward.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="sceneRequests">Shared Scene Manager request queue whose first command owns execution priority.</param>
    /// <returns>True when the default-gameplay request was consumed and procedural generation was started.</returns>
    private bool TryReplaceDefaultGameplayRequest(Entity managerEntity,
                                                  DynamicBuffer<GameSceneTransitionRequest> sceneRequests)
    {
        if (sceneRequests.Length <= 0)
            return false;

        GameSceneTransitionRequest request = sceneRequests[0];

        if (request.Purpose != GameSceneTransitionPurpose.Standard ||
            request.RequestType != GameSceneTransitionRequestType.LoadDefaultGameplay)
        {
            return false;
        }

        GameProceduralLevelConfig config = EntityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);
        DynamicBuffer<GameProceduralLevelRunRequest> runRequests = EntityManager.GetBuffer<GameProceduralLevelRunRequest>(managerEntity);

        if (!TryResolveRunSeed(config, runRequests, out uint runSeed))
            return false;

        // Consume only after seed resolution so External mode can retain the standard fallback until a seed is supplied.
        sceneRequests.RemoveAt(0);

        if (runRequests.Length > 0)
            runRequests.RemoveAt(0);

        DynamicBuffer<GameSceneDefinitionElement> scenes = EntityManager.GetBuffer<GameSceneDefinitionElement>(managerEntity, true);
        TryGenerateFirstLevel(managerEntity, scenes, runSeed);
        return true;
    }

    /// <summary>
    /// Resolves whether a queued or active Standard scene command owns transition priority over procedural progression.
    /// </summary>
    /// <param name="transitionState">Current authoritative scene transition state.</param>
    /// <param name="requests">Shared Scene Manager request queue.</param>
    /// <returns>True when a Standard command is queued or already transitioning.</returns>
    private static bool HasStandardInterruption(GameSceneTransitionState transitionState,
                                                DynamicBuffer<GameSceneTransitionRequest> requests)
    {
        if (transitionState.IsTransitioning != 0 &&
            transitionState.Purpose == GameSceneTransitionPurpose.Standard)
        {
            return true;
        }

        for (int requestIndex = 0; requestIndex < requests.Length; requestIndex++)
        {
            if (requests[requestIndex].Purpose == GameSceneTransitionPurpose.Standard)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Detects published or pending procedural work so one Standard interruption resets it exactly once.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="runtimeState">Current procedural lifecycle state.</param>
    /// <param name="sceneRequests">Shared Scene Manager request queue.</param>
    /// <returns>True when runtime state, graph data or procedural commands still require invalidation.</returns>
    private bool HasProceduralRunState(Entity managerEntity,
                                       GameProceduralLevelRuntimeState runtimeState,
                                       DynamicBuffer<GameSceneTransitionRequest> sceneRequests)
    {
        if (runtimeState.Initialized != 0 ||
            runtimeState.Phase != GameProceduralLevelRuntimePhase.Uninitialized ||
            runtimeState.GraphGenerated != 0 ||
            runtimeState.CurrentLevelIndex >= 0 ||
            runtimeState.CurrentNodeIndex >= 0 ||
            runtimeState.PendingNodeIndex >= 0)
        {
            return true;
        }

        GameProceduralRoomTransitionContext context = EntityManager.GetComponentData<GameProceduralRoomTransitionContext>(managerEntity);
        GameProceduralRoomClearCounter clearCounter = EntityManager.GetComponentData<GameProceduralRoomClearCounter>(managerEntity);

        if (context.Kind != GameProceduralRoomTransitionKind.None ||
            context.RelocationPending != 0 ||
            context.CommitPending != 0 ||
            context.SourceNodeIndex >= 0 ||
            context.TargetNodeIndex >= 0 ||
            clearCounter.TotalCleared != 0u ||
            clearCounter.Version != 0u)
        {
            return true;
        }

        if (EntityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity, true).Length > 0 ||
            EntityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity, true).Length > 0 ||
            EntityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity, true).Length > 0 ||
            EntityManager.GetBuffer<GameProceduralLevelRunRequest>(managerEntity, true).Length > 0)
        {
            return true;
        }

        for (int requestIndex = 0; requestIndex < sceneRequests.Length; requestIndex++)
        {
            if (sceneRequests[requestIndex].Purpose != GameSceneTransitionPurpose.Standard)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Validates every generated node against the baked Scene Manager catalog before publishing any graph buffers.
    /// </summary>
    /// <param name="scenes">Canonical Scene Manager catalog.</param>
    /// <param name="result">Successful solver result awaiting atomic publication.</param>
    /// <param name="diagnostic">Actionable first invalid-scene diagnostic.</param>
    /// <returns>True when every generated room is gameplay-loadable.</returns>
    private static bool ValidateGeneratedSceneCatalog(DynamicBuffer<GameSceneDefinitionElement> scenes,
                                                      GameProceduralLevelGenerationResult result,
                                                      out string diagnostic)
    {
        for (int nodeIndex = 0; nodeIndex < result.Nodes.Count; nodeIndex++)
        {
            GameProceduralLevelGraphNode node = result.Nodes[nodeIndex];
            FixedString64Bytes sceneId = new FixedString64Bytes(node.SceneId);

            if (IsGameplayScene(scenes, sceneId))
                continue;

            diagnostic = "Generated room node " + node.NodeId + " references scene '" + node.SceneId + "', which is missing or is not gameplay-loadable.";
            return false;
        }

        diagnostic = string.Empty;
        return true;
    }

    /// <summary>
    /// Resolves explicit, fixed or random seed policy while preserving External requests until a seed is supplied.
    /// </summary>
    /// <param name="config">Baked global procedural settings.</param>
    /// <param name="requests">Pending run request buffer.</param>
    /// <param name="runSeed">Resolved authoritative run seed.</param>
    /// <returns>True when generation may begin.</returns>
    private static bool TryResolveRunSeed(GameProceduralLevelConfig config,
                                          DynamicBuffer<GameProceduralLevelRunRequest> requests,
                                          out uint runSeed)
    {
        if (requests.Length > 0 && requests[0].HasExplicitSeed != 0)
        {
            runSeed = requests[0].RunSeed;
            return true;
        }

        switch (config.SeedMode)
        {
            case GameProceduralLevelSeedMode.Fixed:
                runSeed = config.FixedSeed;
                return true;
            case GameProceduralLevelSeedMode.RandomPerRun:
                runSeed = CreateRunSeed();
                return true;
            case GameProceduralLevelSeedMode.External:
                runSeed = 0u;
                return false;
            default:
                runSeed = 0u;
                return false;
        }
    }

    /// <summary>
    /// Creates one non-zero authoritative seed, which is then persisted for deterministic ordered-level generation.
    /// </summary>
    /// <returns>Non-zero run seed.</returns>
    private static uint CreateRunSeed()
    {
        uint seed = unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount);
        return seed != 0u ? seed : 1u;
    }

    /// <summary>
    /// Finds the next enabled authored level after a supplied buffer index.
    /// </summary>
    /// <param name="levels">Ordered baked level definitions.</param>
    /// <param name="currentLevelIndex">Current index, or -1 before run start.</param>
    /// <returns>Next enabled level index, or -1 when none exists.</returns>
    private static int FindNextEnabledLevel(DynamicBuffer<GameProceduralLevelDefinitionElement> levels, int currentLevelIndex)
    {
        for (int levelIndex = currentLevelIndex + 1; levelIndex < levels.Length; levelIndex++)
        {
            if (levels[levelIndex].Enabled != 0)
                return levelIndex;
        }

        return -1;
    }

    /// <summary>
    /// Resolves whether the current scene ID is a gameplay-like Scene Manager definition.
    /// </summary>
    /// <param name="scenes">Canonical Scene Manager catalog.</param>
    /// <param name="sceneId">Scene ID to inspect.</param>
    /// <returns>True when the scene is gameplay-like.</returns>
    private static bool IsGameplayScene(DynamicBuffer<GameSceneDefinitionElement> scenes, FixedString64Bytes sceneId)
    {
        for (int sceneIndex = 0; sceneIndex < scenes.Length; sceneIndex++)
        {
            GameSceneDefinitionElement scene = scenes[sceneIndex];

            if (scene.SceneId.Equals(sceneId))
                return GameScenePersistentPlayerSceneUtility.IsGameplayLikeScene(scene);
        }

        return false;
    }

    /// <summary>
    /// Resets generated graph and lifecycle data after leaving gameplay, an explicit restart or a Standard interruption.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    private void ResetRun(Entity managerEntity)
    {
        EntityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity).Clear();
        EntityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity).Clear();
        EntityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity).Clear();
        EntityManager.GetBuffer<GameProceduralLevelRunRequest>(managerEntity).Clear();
        RemoveProceduralSceneRequests(EntityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity));
        EntityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
        {
            CurrentLevelIndex = -1,
            CurrentNodeIndex = -1,
            PendingNodeIndex = -1,
            Phase = GameProceduralLevelRuntimePhase.Uninitialized
        });
        EntityManager.SetComponentData(managerEntity, new GameProceduralRoomTransitionContext
        {
            SourceNodeIndex = -1,
            TargetNodeIndex = -1
        });
        EntityManager.SetComponentData(managerEntity, new GameProceduralRoomClearCounter());
    }

    /// <summary>
    /// Stores a bounded failure diagnostic and prevents partial graph traversal.
    /// </summary>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="diagnostic">Actionable solver or baked-data failure message.</param>
    private void SetGenerationFailure(Entity managerEntity, string diagnostic)
    {
        string resolvedDiagnostic = string.IsNullOrEmpty(diagnostic)
            ? "Procedural level generation failed without a diagnostic."
            : diagnostic;
        string boundedDiagnostic = TruncateToFixedString128Capacity(resolvedDiagnostic);
        GameProceduralLevelRuntimeState runtimeState = EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        runtimeState.FailureMessage = new FixedString128Bytes(boundedDiagnostic);
        runtimeState.Phase = GameProceduralLevelRuntimePhase.Failed;
        runtimeState.Initialized = 1;
        runtimeState.GraphGenerated = 0;
        EntityManager.SetComponentData(managerEntity, runtimeState);
        Debug.LogError("[GameProceduralLevel] " + boundedDiagnostic);
    }

    /// <summary>
    /// Removes stale procedural scene commands while preserving any higher-priority standard transition request.
    /// </summary>
    /// <param name="requests">Shared Scene Manager request queue being reset.</param>
    private static void RemoveProceduralSceneRequests(DynamicBuffer<GameSceneTransitionRequest> requests)
    {
        for (int requestIndex = requests.Length - 1; requestIndex >= 0; requestIndex--)
        {
            if (requests[requestIndex].Purpose == GameSceneTransitionPurpose.Standard)
                continue;

            requests.RemoveAt(requestIndex);
        }
    }

    /// <summary>
    /// Truncates a diagnostic at a complete UTF-16 scalar boundary that fits the FixedString128Bytes UTF-8 payload.
    /// </summary>
    /// <param name="value">Diagnostic text to bound without splitting a valid surrogate pair.</param>
    /// <returns>The longest leading substring that fits the runtime failure-message storage.</returns>
    private static string TruncateToFixedString128Capacity(string value)
    {
        if (Encoding.UTF8.GetByteCount(value) <= FixedString128Bytes.UTF8MaxLengthInBytes)
            return value;

        int characterIndex = 0;
        int utf8ByteCount = 0;

        // Count complete Unicode scalar values so conversion cannot split a valid surrogate pair.
        while (characterIndex < value.Length)
        {
            int characterCount = char.IsHighSurrogate(value[characterIndex]) &&
                                 characterIndex + 1 < value.Length &&
                                 char.IsLowSurrogate(value[characterIndex + 1])
                ? 2
                : 1;
            int characterUtf8ByteCount = Encoding.UTF8.GetByteCount(value,
                                                                     characterIndex,
                                                                     characterCount);

            if (utf8ByteCount + characterUtf8ByteCount > FixedString128Bytes.UTF8MaxLengthInBytes)
                break;

            utf8ByteCount += characterUtf8ByteCount;
            characterIndex += characterCount;
        }

        return value.Substring(0, characterIndex);
    }

    /// <summary>
    /// Logs an invalid procedural manager count once until singleton topology becomes valid again.
    /// </summary>
    /// <param name="managerCount">Current manager query entity count.</param>
    private void LogManagerCountWarning(int managerCount)
    {
        if (loggedManagerCountWarning || managerCount <= 1)
            return;

        loggedManagerCountWarning = true;
        Debug.LogWarning("[GameProceduralLevel] Expected one procedural manager singleton, found " + managerCount + ".");
    }
    #endregion

    #endregion
}
