#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Verifies same-scene purpose policy, Standard request priority and selective procedural queue reset behavior.
/// </summary>
public static class GameProceduralSceneTransitionPurposeSmokeTest
{
    #region Constants
    private const string ReusableRoomSceneId = "SCN_REUSABLE_ROOM";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes scene-purpose and procedural request-arbitration checks from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        GameSceneTransitionState standardState = ExecuteSameSceneRequest(GameSceneTransitionPurpose.Standard);
        Require(standardState.IsTransitioning == 0,
                "A standard non-restart request incorrectly reloaded its already active scene.");

        ValidateProceduralPurpose(GameSceneTransitionPurpose.ProceduralInitialRoom);
        ValidateProceduralPurpose(GameSceneTransitionPurpose.ProceduralRoomTraversal);
        ValidateProceduralPurpose(GameSceneTransitionPurpose.ProceduralLevelBoundary);
        ValidateTraversalRequestArbitration();
        ValidateSameFrameStandardTriggerPriority();
        ValidateDirectDefaultGameplayReplacement();
        ValidateGenerationRequestArbitration();
        ValidateMissingEnabledLevelFailure();
        ValidateResetQueueCleanup();
        Debug.Log("[GameProceduralSceneTransitionPurposeSmokeTest] Same-scene purpose, request arbitration and reset queue checks passed.");
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Verifies one procedural request begins a logical-node reload despite reusing the active scene asset.
    /// </summary>
    /// <param name="purpose">Procedural transition purpose to validate.</param>
    private static void ValidateProceduralPurpose(GameSceneTransitionPurpose purpose)
    {
        GameSceneTransitionState state = ExecuteSameSceneRequest(purpose);
        Require(state.IsTransitioning != 0,
                purpose + " did not begin a same-scene logical room reload.");
        Require(state.Purpose == purpose,
                purpose + " was not propagated to authoritative transition state.");
        Require(state.SourceSceneId.ToString() == ReusableRoomSceneId &&
                state.TargetSceneId.ToString() == ReusableRoomSceneId,
                purpose + " did not retain equal source and target scene IDs.");
        Require(state.Phase == GameSceneTransitionPhase.FadeOut,
                purpose + " unexpectedly advanced beyond the first non-zero fade phase during request acceptance.");
    }

    /// <summary>
    /// Runs one request through the production transition execution system in an isolated ECS world.
    /// </summary>
    /// <param name="purpose">Transition purpose assigned to the request.</param>
    /// <returns>Authoritative transition state after one execution-system update.</returns>
    private static GameSceneTransitionState ExecuteSameSceneRequest(GameSceneTransitionPurpose purpose)
    {
        World world = new World("GameProceduralSceneTransitionPurposeSmokeTest_" + purpose);

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateManager(entityManager, purpose);
            GameSceneTransitionExecutionSystem system = world.GetOrCreateSystemManaged<GameSceneTransitionExecutionSystem>();
            system.Update();
            DynamicBuffer<GameSceneTransitionRequest> requests = entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity, true);
            Require(requests.Length == 0,
                    purpose + " request was not consumed by the transition execution system.");
            return entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies a portal traversal request is discarded without state mutation when a scene command is already queued.
    /// </summary>
    private static void ValidateTraversalRequestArbitration()
    {
        World world = new World("GameProceduralTraversalRequestArbitrationSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateTraversalManager(entityManager);
            GameProceduralRoomTraversalSystem system = world.GetOrCreateSystemManaged<GameProceduralRoomTraversalSystem>();
            system.Update();

            DynamicBuffer<GameSceneTransitionRequest> sceneRequests = entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity, true);
            DynamicBuffer<GameProceduralRoomTraversalRequest> traversalRequests = entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity, true);
            GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
            GameProceduralRoomTransitionContext context = entityManager.GetComponentData<GameProceduralRoomTransitionContext>(managerEntity);
            Require(sceneRequests.Length == 1 && sceneRequests[0].Purpose == GameSceneTransitionPurpose.Standard,
                    "Traversal replaced or appended to an already queued Standard scene request.");
            Require(traversalRequests.Length == 0,
                    "Traversal retained a portal request after losing Scene Manager arbitration.");
            Require(runtimeState.Phase == GameProceduralLevelRuntimePhase.Active &&
                    runtimeState.CurrentNodeIndex == 0 &&
                    runtimeState.PendingNodeIndex == -1,
                    "Traversal mutated procedural runtime state before acquiring the scene request queue.");
            Require(context.SourceNodeIndex == 17 &&
                    context.TargetNodeIndex == 23 &&
                    context.Kind == GameProceduralRoomTransitionKind.None,
                    "Traversal mutated transition context before acquiring the scene request queue.");

            // A Standard request already in flight also invalidates portal input captured during transition teardown.
            entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity).Clear();
            entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity).Add(new GameProceduralRoomTraversalRequest
            {
                SourceNodeIndex = 0,
                AssignedEdgeIndex = 7
            });
            GameSceneTransitionState transitionState = entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
            transitionState.Purpose = GameSceneTransitionPurpose.Standard;
            transitionState.IsTransitioning = 1;
            entityManager.SetComponentData(managerEntity, transitionState);
            system.Update();
            Require(entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity, true).Length == 0,
                    "Traversal retained portal input while a Standard scene transition was already active.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies a Standard trigger request emitted in the same system-group update wins before portal traversal is consumed.
    /// </summary>
    private static void ValidateSameFrameStandardTriggerPriority()
    {
        World world = new World("GameProceduralSameFrameStandardTriggerPrioritySmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateTraversalManager(entityManager);
            entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity).Clear();
            AddGenerationQueryShape(entityManager, managerEntity);
            Entity playerEntity = entityManager.CreateEntity(typeof(PlayerControllerConfig),
                                                               typeof(LocalTransform));
            entityManager.SetComponentData(playerEntity, LocalTransform.FromPosition(float3.zero));
            Entity triggerEntity = entityManager.CreateEntity(typeof(GameSceneTransitionTrigger),
                                                                typeof(GameSceneTransitionTriggerRuntimeState));
            entityManager.SetComponentData(triggerEntity, new GameSceneTransitionTrigger
            {
                TriggerId = new FixedString64Bytes("TRG_STANDARD_PRIORITY"),
                TargetSceneId = new FixedString64Bytes("SCN_MAIN_MENU"),
                Center = float3.zero,
                HalfExtents = new float3(2f),
                CooldownSeconds = 0f,
                OneShot = 1,
                RequirePlayer = 1
            });

            GameSceneManagementSystemGroup group = world.GetOrCreateSystemManaged<GameSceneManagementSystemGroup>();
            GameProceduralRoomTraversalSystem traversalSystem = world.GetOrCreateSystemManaged<GameProceduralRoomTraversalSystem>();
            GameSceneTransitionTriggerSystem triggerSystem = world.GetOrCreateSystemManaged<GameSceneTransitionTriggerSystem>();
            GameProceduralLevelGenerationSystem generationSystem = world.GetOrCreateSystemManaged<GameProceduralLevelGenerationSystem>();
            GameSceneTransitionExecutionSystem executionSystem = world.GetOrCreateSystemManaged<GameSceneTransitionExecutionSystem>();
            executionSystem.Enabled = false;
            group.AddSystemToUpdateList(traversalSystem);
            group.AddSystemToUpdateList(triggerSystem);
            group.AddSystemToUpdateList(generationSystem);
            group.AddSystemToUpdateList(executionSystem);
            group.Update();

            DynamicBuffer<GameSceneTransitionRequest> sceneRequests = entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity, true);
            Require(sceneRequests.Length == 1 &&
                    sceneRequests[0].Purpose == GameSceneTransitionPurpose.Standard &&
                    sceneRequests[0].TargetSceneId.ToString() == "SCN_MAIN_MENU",
                    "Same-frame portal traversal ran before the Standard scene trigger request.");
            Require(entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity, true).Length == 0,
                    "Same-frame portal input was not discarded after the Standard trigger won arbitration.");
            GameProceduralRuntimeResetSmokeUtility.RequireNeutralState(entityManager,
                                                                        managerEntity,
                                                                        "Same-frame Standard trigger reset");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies Play from the main menu starts procedural generation directly instead of loading the fallback gameplay scene first.
    /// </summary>
    private static void ValidateDirectDefaultGameplayReplacement()
    {
        World world = new World("GameProceduralDirectDefaultGameplayReplacementSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateGenerationManager(entityManager, GameSceneKind.MainMenu);
            entityManager.SetComponentData(managerEntity, new GameProceduralLevelConfig
            {
                SeedMode = GameProceduralLevelSeedMode.Fixed,
                FixedSeed = 149u
            });
            AddSceneRequest(entityManager,
                            managerEntity,
                            GameSceneTransitionPurpose.Standard,
                            string.Empty,
                            GameSceneTransitionRequestType.LoadDefaultGameplay);
            GameProceduralLevelGenerationSystem system = world.GetOrCreateSystemManaged<GameProceduralLevelGenerationSystem>();
            system.Update();

            DynamicBuffer<GameSceneTransitionRequest> requests = entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity, true);
            GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
            Require(requests.Length == 0,
                    "The default-gameplay request was left queued for the fallback gameplay scene.");
            Require(runtimeState.Phase == GameProceduralLevelRuntimePhase.Failed &&
                    runtimeState.FailureMessage.ToString() == "The procedural preset contains no enabled levels.",
                    "The intercepted request did not enter procedural generation before scene execution.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies queued and active Standard scene commands invalidate procedural work once without losing Standard priority.
    /// </summary>
    private static void ValidateGenerationRequestArbitration()
    {
        World world = new World("GameProceduralGenerationRequestArbitrationSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateGenerationManager(entityManager, GameSceneKind.Gameplay);
            entityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
            {
                RunSeed = 41u,
                CurrentLevelIndex = 2,
                CurrentNodeIndex = 11,
                PendingNodeIndex = 12,
                Phase = GameProceduralLevelRuntimePhase.Generating,
                Initialized = 1,
                GraphGenerated = 1,
                CurrentRoomCleared = 1
            });
            entityManager.SetComponentData(managerEntity, new GameProceduralRoomTransitionContext
            {
                SourceNodeIndex = 11,
                TargetNodeIndex = 12,
                Kind = GameProceduralRoomTransitionKind.LevelBoundary,
                RelocationPending = 1,
                CommitPending = 1
            });
            DynamicBuffer<GameProceduralRoomNodeElement> nodes = entityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity);
            nodes.Add(new GameProceduralRoomNodeElement
            {
                NodeIndex = 11,
                SceneId = new FixedString64Bytes(ReusableRoomSceneId)
            });
            DynamicBuffer<GameProceduralRoomEdgeElement> edges = entityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity);
            edges.Add(new GameProceduralRoomEdgeElement
            {
                EdgeIndex = 31,
                SourceNodeIndex = 11,
                TargetNodeIndex = 12
            });
            DynamicBuffer<GameProceduralLevelRunRequest> runRequests = entityManager.GetBuffer<GameProceduralLevelRunRequest>(managerEntity);
            runRequests.Add(new GameProceduralLevelRunRequest
            {
                RunSeed = 89u,
                HasExplicitSeed = 1,
                Restart = 1
            });
            AddSceneRequest(entityManager,
                            managerEntity,
                            GameSceneTransitionPurpose.Standard,
                            ReusableRoomSceneId,
                            GameSceneTransitionRequestType.RestartActiveScene);

            GameProceduralLevelGenerationSystem system = world.GetOrCreateSystemManaged<GameProceduralLevelGenerationSystem>();
            system.Update();

            DynamicBuffer<GameSceneTransitionRequest> sceneRequests = entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity, true);
            Require(sceneRequests.Length == 1 && sceneRequests[0].Purpose == GameSceneTransitionPurpose.Standard,
                    "Generation removed or replaced the queued Standard restart request.");
            Require(sceneRequests[0].RequestType == GameSceneTransitionRequestType.RestartActiveScene,
                    "Generation changed the Standard restart request type.");
            GameProceduralRuntimeResetSmokeUtility.RequireNeutralState(entityManager,
                                                                        managerEntity,
                                                                        "Queued Standard restart reset");

            // A repeated update before Scene Management consumes the request must remain neutral without touching it.
            system.Update();
            Require(sceneRequests.Length == 1 &&
                    sceneRequests[0].RequestType == GameSceneTransitionRequestType.RestartActiveScene,
                    "Neutral retry changed the pending Standard restart request.");
            GameProceduralRuntimeResetSmokeUtility.RequireNeutralState(entityManager,
                                                                        managerEntity,
                                                                        "Queued Standard restart neutral retry");

            // Also invalidate a run when the Standard request was consumed before generation observed the queue.
            entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity).Clear();
            entityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
            {
                CurrentLevelIndex = 0,
                CurrentNodeIndex = 0,
                PendingNodeIndex = -1,
                Phase = GameProceduralLevelRuntimePhase.Active,
                Initialized = 1,
                GraphGenerated = 1
            });
            nodes.Add(new GameProceduralRoomNodeElement
            {
                NodeIndex = 0
            });
            GameSceneTransitionState transitionState = entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
            transitionState.Purpose = GameSceneTransitionPurpose.Standard;
            transitionState.IsTransitioning = 1;
            entityManager.SetComponentData(managerEntity, transitionState);
            system.Update();
            GameProceduralRuntimeResetSmokeUtility.RequireNeutralState(entityManager,
                                                                        managerEntity,
                                                                        "Active Standard transition reset");
            system.Update();
            GameProceduralRuntimeResetSmokeUtility.RequireNeutralState(entityManager,
                                                                        managerEntity,
                                                                        "Active Standard transition neutral retry");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies baked data with no enabled level enters one stable terminal failure instead of retrying every update.
    /// </summary>
    private static void ValidateMissingEnabledLevelFailure()
    {
        World world = new World("GameProceduralMissingEnabledLevelFailureSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateGenerationManager(entityManager, GameSceneKind.Gameplay);
            entityManager.SetComponentData(managerEntity, new GameProceduralLevelConfig
            {
                SeedMode = GameProceduralLevelSeedMode.Fixed,
                FixedSeed = 17u
            });
            GameProceduralLevelGenerationSystem system = world.GetOrCreateSystemManaged<GameProceduralLevelGenerationSystem>();
            system.Update();
            GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
            string failureMessage = runtimeState.FailureMessage.ToString();
            Require(runtimeState.Initialized != 0 &&
                    runtimeState.Phase == GameProceduralLevelRuntimePhase.Failed &&
                    runtimeState.GraphGenerated == 0,
                    "Missing enabled levels did not enter terminal generation failure.");
            Require(failureMessage == "The procedural preset contains no enabled levels.",
                    "Missing enabled levels produced an unstable runtime diagnostic.");

            system.Update();
            runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
            Require(runtimeState.Phase == GameProceduralLevelRuntimePhase.Failed &&
                    runtimeState.FailureMessage.ToString() == failureMessage &&
                    entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity, true).Length == 0,
                    "Terminal missing-level failure retried or queued a scene command.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies leaving gameplay clears stale procedural work while retaining a queued Standard transition.
    /// </summary>
    private static void ValidateResetQueueCleanup()
    {
        World world = new World("GameProceduralResetQueueCleanupSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateGenerationManager(entityManager, GameSceneKind.MainMenu);
            entityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
            {
                CurrentLevelIndex = 1,
                CurrentNodeIndex = 4,
                PendingNodeIndex = 5,
                Phase = GameProceduralLevelRuntimePhase.Active,
                Initialized = 1,
                GraphGenerated = 1,
                CurrentRoomCleared = 1
            });
            entityManager.SetComponentData(managerEntity, new GameProceduralRoomTransitionContext
            {
                SourceNodeIndex = 4,
                TargetNodeIndex = 5,
                Kind = GameProceduralRoomTransitionKind.IntraLevel,
                RelocationPending = 1,
                CommitPending = 1
            });
            entityManager.SetComponentData(managerEntity, new GameProceduralRoomClearCounter
            {
                TotalCleared = 8u,
                Version = 9u
            });
            entityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity).Add(new GameProceduralRoomNodeElement
            {
                NodeIndex = 4
            });
            entityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity).Add(new GameProceduralRoomEdgeElement
            {
                EdgeIndex = 6
            });
            entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity).Add(new GameProceduralRoomTraversalRequest
            {
                SourceNodeIndex = 4,
                AssignedEdgeIndex = 6
            });
            entityManager.GetBuffer<GameProceduralLevelRunRequest>(managerEntity).Add(new GameProceduralLevelRunRequest
            {
                RunSeed = 101u,
                HasExplicitSeed = 1,
                Restart = 1
            });
            AddSceneRequest(entityManager,
                            managerEntity,
                            GameSceneTransitionPurpose.Standard,
                            "SCN_MAIN_MENU");
            AddSceneRequest(entityManager,
                            managerEntity,
                            GameSceneTransitionPurpose.ProceduralRoomTraversal,
                            ReusableRoomSceneId);

            GameProceduralLevelGenerationSystem system = world.GetOrCreateSystemManaged<GameProceduralLevelGenerationSystem>();
            system.Update();

            DynamicBuffer<GameSceneTransitionRequest> sceneRequests = entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity, true);
            Require(sceneRequests.Length == 1 && sceneRequests[0].Purpose == GameSceneTransitionPurpose.Standard,
                    "Run reset removed the Standard request or retained a stale procedural scene request.");
            GameProceduralRuntimeResetSmokeUtility.RequireNeutralState(entityManager,
                                                                        managerEntity,
                                                                        "Non-gameplay reset");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Fixture Methods
    /// <summary>
    /// Creates the minimal valid Scene Manager singleton required to evaluate one same-scene request.
    /// </summary>
    /// <param name="entityManager">Isolated fixture entity manager.</param>
    /// <param name="purpose">Purpose assigned to the queued request.</param>
    /// <returns>Created Scene Manager singleton entity.</returns>
    private static Entity CreateManager(EntityManager entityManager, GameSceneTransitionPurpose purpose)
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
    private static void AddGenerationQueryShape(EntityManager entityManager, Entity managerEntity)
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
    }

    /// <summary>
    /// Creates a valid traversal graph with both a pending portal command and a higher-priority Standard scene request.
    /// </summary>
    /// <param name="entityManager">Isolated fixture entity manager.</param>
    /// <returns>Created procedural manager singleton entity.</returns>
    private static Entity CreateTraversalManager(EntityManager entityManager)
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
    private static Entity CreateGenerationManager(EntityManager entityManager, GameSceneKind activeSceneKind)
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
    private static void AddSceneRequest(EntityManager entityManager,
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

    #region Assertion Methods
    /// <summary>
    /// Throws one actionable smoke-test failure when an invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure message describing the violated invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameProceduralSceneTransitionPurposeSmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
