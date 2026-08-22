#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;

using PhysicsBoxCollider = Unity.Physics.BoxCollider;

/// <summary>
/// Verifies fail-closed portal collider filtering, graph assignment synchronization and collider restoration.
/// </summary>
public static class GameProceduralRoomPortalBlockingSmokeTest
{
    #region Constants
    private const int WallsLayerMask = 1 << 5;
    private const int PortalBarrierLayerMask = 1 << 6;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes isolated physical portal barrier checks from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        CollisionFilter blockerFilter =
            GameProceduralRoomPortalBlockingUtility.BuildBlockingFilter(PortalBarrierLayerMask);
        BlobAssetReference<Unity.Physics.Collider> blockerCollider = PhysicsBoxCollider.Create(new BoxGeometry
        {
            Center = float3.zero,
            Orientation = quaternion.identity,
            Size = new float3(2f, 2f, 0.4f),
            BevelRadius = 0f
        }, blockerFilter);
        World world = new World("GameProceduralRoomPortalBlockingSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity outgoingPortal = CreatePortal(entityManager,
                                                 "OUTGOING",
                                                 GameRoomPortalCapability.Exit,
                                                 3,
                                                 true);
            CreatePortal(entityManager,
                         "INBOUND_BOTH",
                         GameRoomPortalCapability.Both,
                         GameProceduralRoomTraversalConstants.UnassignedEdgeIndex,
                         false);
            CreatePortal(entityManager,
                         "UNUSED_EXIT",
                         GameRoomPortalCapability.Exit,
                         GameProceduralRoomTraversalConstants.UnassignedEdgeIndex,
                         false);
            Entity levelExitPortal = CreatePortal(entityManager,
                                                  "LEVEL_EXIT",
                                                  GameRoomPortalCapability.Exit,
                                                  GameProceduralRoomTraversalConstants.LevelExitEdgeIndex,
                                                  true);
            Entity outgoingBlocker = CreateBlocker(entityManager, "OUTGOING", blockerCollider);
            Entity inboundBothBlocker = CreateBlocker(entityManager, "INBOUND_BOTH", blockerCollider);
            Entity unusedBlocker = CreateBlocker(entityManager, "UNUSED_EXIT", blockerCollider);
            Entity levelExitBlocker = CreateBlocker(entityManager, "LEVEL_EXIT", blockerCollider);
            Entity managerEntity = CreateManager(entityManager);

            ValidateFilter(blockerFilter);
            ValidatePhysicsWorldParticipation(blockerCollider);
            ValidateWallLineOfSightOcclusion();
            ValidateAssignmentSynchronization(entityManager,
                                              managerEntity,
                                              outgoingBlocker,
                                              inboundBothBlocker,
                                              unusedBlocker,
                                              levelExitBlocker);
            ValidateColliderRestoration(entityManager,
                                        managerEntity,
                                        outgoingPortal,
                                        outgoingBlocker,
                                        blockerCollider);
            ValidateRoomClearGating(entityManager,
                                    managerEntity,
                                    levelExitBlocker,
                                    blockerCollider);
            ValidateDuplicateIdentityFailsClosed(entityManager,
                                                 managerEntity,
                                                 levelExitPortal,
                                                 levelExitBlocker);
            Debug.Log("[GameProceduralRoomPortalBlockingSmokeTest] All physical portal barrier checks passed.");
        }
        finally
        {
            world.Dispose();

            if (blockerCollider.IsCreated)
                blockerCollider.Dispose();
        }
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Verifies the baked category matches only the reserved player movement query identity.
    /// </summary>
    /// <param name="blockerFilter">Production blocker collision filter.</param>
    private static void ValidateFilter(CollisionFilter blockerFilter)
    {
        CollisionFilter playerMovementQuery =
            WorldPortalBarrierCollisionUtility.BuildPlayerMovementFilter(WallsLayerMask,
                                                                          PortalBarrierLayerMask);
        CollisionFilter wallsOnlyQuery =
            WorldWallCollisionUtility.BuildWallsCollisionFilter(WallsLayerMask);
        CollisionFilter unrelatedQuery =
            WorldWallCollisionUtility.BuildWallsCollisionFilter(1 << 7);
        CollisionFilter fallbackFilter = GameProceduralRoomPortalBlockingUtility.BuildBlockingFilter(0);
        Require(blockerFilter.BelongsTo == (uint)PortalBarrierLayerMask,
                "The blocker does not belong exclusively to the PortalBarrier category.");
        Require(blockerFilter.CollidesWith ==
                WorldPortalBarrierCollisionUtility.PlayerMovementQueryCategory,
                "The blocker accepts categories other than the reserved player movement identity.");
        Require(CollisionFilter.IsCollisionEnabled(blockerFilter, playerMovementQuery),
                "The blocker filter is invisible to the player movement query.");
        Require(!CollisionFilter.IsCollisionEnabled(blockerFilter, wallsOnlyQuery),
                "The blocker leaks into a generic Walls query used by projectiles, drops or enemies.");
        Require(!CollisionFilter.IsCollisionEnabled(blockerFilter, unrelatedQuery),
                "The blocker filter leaks into a non-Walls query category.");
        Require(fallbackFilter.BelongsTo == 0u && fallbackFilter.CollidesWith == 0u,
                "A missing PortalBarrier layer produced a collider that could affect unrelated entities.");
    }

    /// <summary>
    /// Verifies the baked-style collider participates as a rotated static body in a built Unity Physics broadphase.
    /// </summary>
    /// <param name="blockingCollider">Portal blocker collider blob used by the ECS fixture.</param>
    private static void ValidatePhysicsWorldParticipation(BlobAssetReference<Unity.Physics.Collider> blockingCollider)
    {
        PhysicsWorld physicsWorld = new PhysicsWorld(1, 0, 0);

        try
        {
            NativeArray<RigidBody> staticBodies = physicsWorld.StaticBodies;
            staticBodies[0] = new RigidBody
            {
                WorldFromBody = new RigidTransform(quaternion.RotateY(math.PI * 0.5f), float3.zero),
                Scale = 1f,
                Collider = blockingCollider,
                Entity = Entity.Null
            };
            physicsWorld.CollisionWorld.BuildBroadphase(ref physicsWorld,
                                                        1f / 60f,
                                                        float3.zero);
            RaycastInput input = new RaycastInput
            {
                Start = new float3(-2f, 0f, 0f),
                End = new float3(2f, 0f, 0f),
                Filter =
                    WorldPortalBarrierCollisionUtility.BuildPlayerMovementFilter(WallsLayerMask,
                                                                                 PortalBarrierLayerMask)
            };
            Require(physicsWorld.CastRay(input),
                    "The rotated portal blocker did not participate in the Unity Physics static broadphase.");
        }
        finally
        {
            physicsWorld.Dispose();
        }
    }

    /// <summary>
    /// Verifies the shared wall segment query blocks projectile damage across geometry without blocking same-side hits.
    /// </summary>
    private static void ValidateWallLineOfSightOcclusion()
    {
        CollisionFilter wallsFilter =
            WorldWallCollisionUtility.BuildWallsCollisionFilter(WallsLayerMask);
        BlobAssetReference<Unity.Physics.Collider> wallCollider =
            PhysicsBoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(2f, 2f, 0.4f),
                BevelRadius = 0f
            }, wallsFilter);
        PhysicsWorld physicsWorld = new PhysicsWorld(1, 0, 0);

        try
        {
            NativeArray<RigidBody> staticBodies = physicsWorld.StaticBodies;
            staticBodies[0] = new RigidBody
            {
                WorldFromBody = new RigidTransform(quaternion.identity,
                                                   float3.zero),
                Scale = 1f,
                Collider = wallCollider,
                Entity = Entity.Null
            };
            physicsWorld.CollisionWorld.BuildBroadphase(ref physicsWorld,
                                                        1f / 60f,
                                                        float3.zero);
            PhysicsWorldSingleton physicsWorldSingleton =
                new PhysicsWorldSingleton
                {
                    PhysicsWorld = physicsWorld
                };
            Require(WorldWallCollisionUtility.IsLineOfSightBlocked(
                        in physicsWorldSingleton,
                        new float3(0f, 0f, -2f),
                        new float3(0f, 0f, 2f),
                        in wallsFilter),
                    "A wall did not block projectile damage between opposite sides.");
            Require(!WorldWallCollisionUtility.IsLineOfSightBlocked(
                        in physicsWorldSingleton,
                        new float3(0f, 0f, -2f),
                        new float3(0.5f, 0f, -1f),
                        in wallsFilter),
                    "A wall blocked projectile damage between two unobstructed same-side points.");
        }
        finally
        {
            physicsWorld.Dispose();

            if (wallCollider.IsCreated)
                wallCollider.Dispose();
        }
    }

    /// <summary>
    /// Verifies only a lifecycle-valid regular assignment loses its physical barrier while LevelExit remains gated.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural lifecycle manager used for fail-closed synchronization.</param>
    /// <param name="outgoingBlocker">Assigned regular outgoing blocker.</param>
    /// <param name="inboundBothBlocker">Inbound Both-capability blocker.</param>
    /// <param name="unusedBlocker">Unassigned exit blocker.</param>
    /// <param name="levelExitBlocker">Assigned level-exit blocker.</param>
    private static void ValidateAssignmentSynchronization(EntityManager entityManager,
                                                          Entity managerEntity,
                                                          Entity outgoingBlocker,
                                                          Entity inboundBothBlocker,
                                                          Entity unusedBlocker,
                                                          Entity levelExitBlocker)
    {
        int changedCount = GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        Require(changedCount == 1,
                "Synchronization did not open exactly one lifecycle-valid regular barrier.");
        Require(!IsBlocking(entityManager, outgoingBlocker),
                "A graph-assigned outgoing portal remained physically blocked.");
        Require(IsBlocking(entityManager, inboundBothBlocker),
                "The inbound portal opened solely because its authored capability is Both.");
        Require(IsBlocking(entityManager, unusedBlocker),
                "An unassigned outgoing-capable portal became physically traversable.");
        Require(IsBlocking(entityManager, levelExitBlocker),
                "An assigned Boss LevelExit opened before LevelComplete.");
    }

    /// <summary>
    /// Verifies closing a previously assigned portal restores its original baked collider reference.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural lifecycle manager used for fail-closed synchronization.</param>
    /// <param name="outgoingPortal">Logical outgoing portal fixture.</param>
    /// <param name="outgoingBlocker">Paired physical blocker fixture.</param>
    /// <param name="expectedCollider">Original baked collider reference.</param>
    private static void ValidateColliderRestoration(EntityManager entityManager,
                                                    Entity managerEntity,
                                                    Entity outgoingPortal,
                                                    Entity outgoingBlocker,
                                                    BlobAssetReference<Unity.Physics.Collider> expectedCollider)
    {
        GameRoomPortalRuntimeState state = entityManager.GetComponentData<GameRoomPortalRuntimeState>(outgoingPortal);
        state.AssignedEdgeIndex = GameProceduralRoomTraversalConstants.UnassignedEdgeIndex;
        state.TraversalEnabled = 0;
        entityManager.SetComponentData(outgoingPortal, state);
        int changedCount = GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        PhysicsCollider physicsCollider = entityManager.GetComponentData<PhysicsCollider>(outgoingBlocker);
        Require(changedCount == 1,
                "Closing one outgoing assignment changed an unexpected number of barriers.");
        Require(IsBlocking(entityManager, outgoingBlocker),
                "The removed graph assignment did not restore physical blocking.");
        Require(physicsCollider.Value.Equals(expectedCollider),
                "The blocker did not restore its original baked collider blob.");
    }

    /// <summary>
    /// Verifies global, per-portal and LevelExit barriers open only after their corresponding lifecycle gate.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural lifecycle manager shared by all gate checks.</param>
    /// <param name="levelExitBlocker">Existing assigned LevelExit barrier.</param>
    /// <param name="blockingCollider">Reusable baked-style collider reference.</param>
    private static void ValidateRoomClearGating(EntityManager entityManager,
                                                Entity managerEntity,
                                                Entity levelExitBlocker,
                                                BlobAssetReference<Unity.Physics.Collider> blockingCollider)
    {
        Entity globalPortal = CreatePortal(entityManager,
                                           "GLOBAL_GATE",
                                           GameRoomPortalCapability.Exit,
                                           7,
                                           true,
                                           false,
                                           GameRoomPortalConnectionPolicy.Optional);
        Entity localPortal = CreatePortal(entityManager,
                                          "LOCAL_GATE",
                                          GameRoomPortalCapability.Exit,
                                          8,
                                          true,
                                          true,
                                          GameRoomPortalConnectionPolicy.Optional);
        Entity globalBlocker = CreateBlocker(entityManager, "GLOBAL_GATE", blockingCollider);
        Entity localBlocker = CreateBlocker(entityManager, "LOCAL_GATE", blockingCollider);
        DynamicBuffer<GameProceduralLevelDefinitionElement> levels = entityManager.GetBuffer<GameProceduralLevelDefinitionElement>(managerEntity);
        GameProceduralLevelDefinitionElement initialLevel = levels[0];
        initialLevel.RequireRoomClearBeforeExit = 1;
        levels[0] = initialLevel;
        entityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
        {
            CurrentLevelIndex = 0,
            CurrentRoomCleared = 0,
            Phase = GameProceduralLevelRuntimePhase.Active
        });

        GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        Require(IsBlocking(entityManager, globalBlocker) && IsBlocking(entityManager, localBlocker),
                "Room arrival opened a physically gated regular exit before combat completion.");
        Require(IsBlocking(entityManager, levelExitBlocker),
                "LevelExit opened while the Boss room was still active.");

        GameProceduralLevelDefinitionElement level = levels[0];
        level.RequireRoomClearBeforeExit = 0;
        levels[0] = level;
        GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        Require(!IsBlocking(entityManager, globalBlocker) && IsBlocking(entityManager, localBlocker),
                "Per-portal clear gating did not remain closed after the global gate was disabled.");

        GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        runtimeState.CurrentRoomCleared = 1;
        entityManager.SetComponentData(managerEntity, runtimeState);
        GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        Require(!IsBlocking(entityManager, globalBlocker) && !IsBlocking(entityManager, localBlocker),
                "Completed regular room did not open all graph-assigned regular exits.");
        Require(IsBlocking(entityManager, levelExitBlocker),
                "LevelExit opened before the runtime entered LevelComplete.");
        Require(entityManager.GetComponentData<GameRoomPortalRuntimeState>(globalPortal).TraversalEnabled != 0 &&
                entityManager.GetComponentData<GameRoomPortalRuntimeState>(localPortal).TraversalEnabled != 0,
                "Physical regular-exit gating diverged from logical traversal availability.");

        runtimeState.Phase = GameProceduralLevelRuntimePhase.LevelComplete;
        entityManager.SetComponentData(managerEntity, runtimeState);
        GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        Require(!IsBlocking(entityManager, levelExitBlocker),
                "Completed non-final Boss did not open its assigned LevelExit.");
        Require(IsBlocking(entityManager, globalBlocker) && IsBlocking(entityManager, localBlocker),
                "LevelComplete left obsolete regular graph exits physically open beside LevelExit.");
        Require(entityManager.GetComponentData<GameRoomPortalRuntimeState>(globalPortal).TraversalEnabled == 0 &&
                entityManager.GetComponentData<GameRoomPortalRuntimeState>(localPortal).TraversalEnabled == 0,
                "LevelComplete left obsolete regular graph exits logically traversable.");

        runtimeState.Phase = GameProceduralLevelRuntimePhase.RunComplete;
        entityManager.SetComponentData(managerEntity, runtimeState);
        GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        Require(IsBlocking(entityManager, levelExitBlocker),
                "The final Boss RunComplete phase left LevelExit physically open.");

        runtimeState.Phase = GameProceduralLevelRuntimePhase.LevelComplete;
        entityManager.SetComponentData(managerEntity, runtimeState);
        GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        Require(!IsBlocking(entityManager, levelExitBlocker),
                "Returning the fixture to non-final LevelComplete did not reopen LevelExit.");
    }

    /// <summary>
    /// Verifies duplicate technical IDs cannot accidentally open either paired barrier.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural lifecycle manager used for duplicate synchronization.</param>
    /// <param name="levelExitPortal">Assigned logical portal whose identity is duplicated.</param>
    /// <param name="levelExitBlocker">Physical barrier paired to the duplicated identity.</param>
    private static void ValidateDuplicateIdentityFailsClosed(EntityManager entityManager,
                                                             Entity managerEntity,
                                                             Entity levelExitPortal,
                                                             Entity levelExitBlocker)
    {
        GameRoomPortal duplicatePortal = entityManager.GetComponentData<GameRoomPortal>(levelExitPortal);
        GameRoomPortalRuntimeState duplicateState = entityManager.GetComponentData<GameRoomPortalRuntimeState>(levelExitPortal);
        Entity duplicateEntity = entityManager.CreateEntity(typeof(GameRoomPortal),
                                                             typeof(GameRoomPortalRuntimeState),
                                                             typeof(SceneTag));
        entityManager.SetComponentData(duplicateEntity, duplicatePortal);
        entityManager.SetComponentData(duplicateEntity, duplicateState);
        int changedCount = GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
        Require(changedCount == 1 && IsBlocking(entityManager, levelExitBlocker),
                "A duplicate portal ID did not force its paired barrier closed.");
        Require(entityManager.GetComponentData<GameRoomPortalRuntimeState>(levelExitPortal).TraversalEnabled == 0 &&
                entityManager.GetComponentData<GameRoomPortalRuntimeState>(duplicateEntity).TraversalEnabled == 0,
                "A duplicate portal ID remained logically armed after its physical barrier failed closed.");
    }
    #endregion

    #region Fixture Methods
    /// <summary>
    /// Creates one valid active procedural lifecycle manager with a single ungated level.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <returns>Created lifecycle manager entity.</returns>
    private static Entity CreateManager(EntityManager entityManager)
    {
        Entity managerEntity = entityManager.CreateEntity(typeof(GameProceduralLevelRuntimeState));
        DynamicBuffer<GameProceduralLevelDefinitionElement> levels = entityManager.AddBuffer<GameProceduralLevelDefinitionElement>(managerEntity);
        levels.Add(new GameProceduralLevelDefinitionElement());
        entityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
        {
            CurrentLevelIndex = 0,
            CurrentRoomCleared = 1,
            Phase = GameProceduralLevelRuntimePhase.Active
        });
        return managerEntity;
    }

    /// <summary>
    /// Creates one logical portal assignment fixture.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="portalId">Stable physical portal identity.</param>
    /// <param name="capability">Authored traversal capability.</param>
    /// <param name="assignedEdgeIndex">Generated edge or sentinel assignment.</param>
    /// <param name="traversalEnabled">Whether the assignment is enabled for this room visit.</param>
    /// <param name="requireRoomClear">Whether this individual portal remains gated until completion.</param>
    /// <param name="connectionPolicy">Authored connection policy.</param>
    /// <returns>Created logical portal entity.</returns>
    private static Entity CreatePortal(EntityManager entityManager,
                                       string portalId,
                                       GameRoomPortalCapability capability,
                                       int assignedEdgeIndex,
                                       bool traversalEnabled,
                                       bool requireRoomClear = false,
                                       GameRoomPortalConnectionPolicy connectionPolicy = GameRoomPortalConnectionPolicy.Optional)
    {
        Entity portalEntity = entityManager.CreateEntity(typeof(GameRoomPortal),
                                                          typeof(GameRoomPortalRuntimeState),
                                                          typeof(SceneTag));
        entityManager.SetComponentData(portalEntity, new GameRoomPortal
        {
            PortalId = new FixedString64Bytes(portalId),
            Capability = capability,
            Policy = connectionPolicy,
            RequireRoomClear = requireRoomClear ? (byte)1 : (byte)0
        });
        entityManager.SetComponentData(portalEntity, new GameRoomPortalRuntimeState
        {
            AssignedEdgeIndex = assignedEdgeIndex,
            TraversalEnabled = traversalEnabled ? (byte)1 : (byte)0
        });
        return portalEntity;
    }

    /// <summary>
    /// Creates one initially closed static blocker fixture.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="portalId">Logical portal identity paired to this blocker.</param>
    /// <param name="blockingCollider">Reusable baked-style collider reference.</param>
    /// <returns>Created physical blocker entity.</returns>
    private static Entity CreateBlocker(EntityManager entityManager,
                                        string portalId,
                                        BlobAssetReference<Unity.Physics.Collider> blockingCollider)
    {
        Entity blockerEntity = entityManager.CreateEntity(typeof(GameRoomPortalBlocker),
                                                           typeof(PhysicsCollider),
                                                           typeof(LocalTransform),
                                                           typeof(PhysicsWorldIndex),
                                                           typeof(SceneTag));
        entityManager.SetComponentData(blockerEntity, new GameRoomPortalBlocker
        {
            PortalId = new FixedString64Bytes(portalId),
            BlockingCollider = blockingCollider,
            IsBlocking = 1
        });
        entityManager.SetComponentData(blockerEntity, new PhysicsCollider
        {
            Value = blockingCollider
        });
        entityManager.SetComponentData(blockerEntity, LocalTransform.Identity);
        return blockerEntity;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Resolves whether one blocker owns both an enabled state flag and a valid collider reference.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="blockerEntity">Blocker entity to inspect.</param>
    /// <returns>True when the blocker is physically present.</returns>
    private static bool IsBlocking(EntityManager entityManager, Entity blockerEntity)
    {
        GameRoomPortalBlocker blocker = entityManager.GetComponentData<GameRoomPortalBlocker>(blockerEntity);
        PhysicsCollider physicsCollider = entityManager.GetComponentData<PhysicsCollider>(blockerEntity);
        return blocker.IsBlocking != 0 && physicsCollider.Value.IsCreated;
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
            throw new InvalidOperationException("GameProceduralRoomPortalBlockingSmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
