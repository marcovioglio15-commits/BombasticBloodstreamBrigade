#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Verifies floor-projected oriented portal containment, Optional exit latching, clear gating and entrance blocking.
/// </summary>
public static class GameProceduralRoomTraversalSmokeTest
{
    #region Constants
    private const int AssignedEdgeIndex = 7;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes isolated traversal-system checks from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        World world = new World("GameProceduralRoomTraversalSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateManager(entityManager);
            Entity playerEntity = CreatePlayer(entityManager);
            Entity portalEntity = CreateRotatedPortal(entityManager);
            SystemHandle portalSystem = world.GetOrCreateSystem<GameProceduralRoomPortalDetectionSystem>();

            ValidateSpawnOverlapLatch(world,
                                      portalSystem,
                                      entityManager,
                                      managerEntity,
                                      playerEntity,
                                      portalEntity);
            ValidateOrientedContainmentAndLatch(world,
                                                portalSystem,
                                                entityManager,
                                                managerEntity,
                                                playerEntity,
                                                portalEntity);
            ValidateClearGate(world,
                              portalSystem,
                              entityManager,
                              managerEntity,
                              playerEntity,
                              portalEntity);
            ValidateEntranceCannotEmit(world,
                                       portalSystem,
                                       entityManager,
                                       managerEntity,
                                       playerEntity,
                                       portalEntity);
            Debug.Log("[GameProceduralRoomTraversalSmokeTest] Floor-projected OBB, Optional exit and traversal-policy checks passed.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Verifies a newly arrived player overlapping a portal must leave its volume before a deliberate re-entry can traverse.
    /// </summary>
    /// <param name="world">Isolated ECS world.</param>
    /// <param name="portalSystem">Production portal detection system.</param>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural manager singleton.</param>
    /// <param name="playerEntity">Persistent player fixture.</param>
    /// <param name="portalEntity">Rotated portal fixture.</param>
    private static void ValidateSpawnOverlapLatch(World world,
                                                  SystemHandle portalSystem,
                                                  EntityManager entityManager,
                                                  Entity managerEntity,
                                                  Entity playerEntity,
                                                  Entity portalEntity)
    {
        GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portalEntity);
        GameRoomPortalRuntimeState portalState = entityManager.GetComponentData<GameRoomPortalRuntimeState>(portalEntity);
        portalState.WasPlayerInside = 1;
        entityManager.SetComponentData(portalEntity, portalState);
        SetPlayerPosition(entityManager, playerEntity, GetInsidePosition(portal));
        portalSystem.Update(world.Unmanaged);
        DynamicBuffer<GameProceduralRoomTraversalRequest> requests = entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity);
        Require(requests.Length == 0,
                "An arrival overlap emitted a traversal without a new outside-to-inside crossing.");

        SetPlayerPosition(entityManager, playerEntity, GetOutsidePosition(portal));
        portalSystem.Update(world.Unmanaged);
        SetPlayerPosition(entityManager, playerEntity, GetInsidePosition(portal));
        portalSystem.Update(world.Unmanaged);
        ValidateSingleRequest(entityManager, managerEntity, portal.PortalId);

        requests.Clear();
        SetPlayerPosition(entityManager, playerEntity, GetOutsidePosition(portal));
        portalSystem.Update(world.Unmanaged);
    }

    /// <summary>
    /// Verifies a point inside the rotated local box emits once and must leave before it can emit again.
    /// </summary>
    /// <param name="world">Isolated ECS world.</param>
    /// <param name="portalSystem">Production portal detection system.</param>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural manager singleton.</param>
    /// <param name="playerEntity">Persistent player fixture.</param>
    /// <param name="portalEntity">Rotated portal fixture.</param>
    private static void ValidateOrientedContainmentAndLatch(World world,
                                                            SystemHandle portalSystem,
                                                            EntityManager entityManager,
                                                            Entity managerEntity,
                                                            Entity playerEntity,
                                                            Entity portalEntity)
    {
        GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portalEntity);
        float3 insidePosition = GetInsidePosition(portal);
        Require(math.abs(insidePosition.z - portal.Center.z) > portal.HalfExtents.z,
                "The fixture point does not distinguish oriented containment from an axis-aligned test.");
        SetPlayerPosition(entityManager, playerEntity, insidePosition);
        portalSystem.Update(world.Unmanaged);
        ValidateSingleRequest(entityManager, managerEntity, portal.PortalId);

        // Removing the consumed request while remaining inside must not bypass the per-entry latch.
        DynamicBuffer<GameProceduralRoomTraversalRequest> requests = entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity);
        requests.Clear();
        portalSystem.Update(world.Unmanaged);
        Require(requests.Length == 0,
                "The same continuous portal overlap emitted more than one traversal request.");

        // Leaving resets both overlap and trigger state, allowing a later deliberate re-entry.
        SetPlayerPosition(entityManager, playerEntity, GetOutsidePosition(portal));
        portalSystem.Update(world.Unmanaged);
        SetPlayerPosition(entityManager, playerEntity, insidePosition);
        portalSystem.Update(world.Unmanaged);
        ValidateSingleRequest(entityManager, managerEntity, portal.PortalId);
    }

    /// <summary>
    /// Verifies authored clear gating blocks traversal until the active logical room is complete.
    /// </summary>
    /// <param name="world">Isolated ECS world.</param>
    /// <param name="portalSystem">Production portal detection system.</param>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural manager singleton.</param>
    /// <param name="playerEntity">Persistent player fixture.</param>
    /// <param name="portalEntity">Portal fixture.</param>
    private static void ValidateClearGate(World world,
                                          SystemHandle portalSystem,
                                          EntityManager entityManager,
                                          Entity managerEntity,
                                          Entity playerEntity,
                                          Entity portalEntity)
    {
        DynamicBuffer<GameProceduralRoomTraversalRequest> requests = entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity);
        requests.Clear();
        GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portalEntity);
        portal.RequireRoomClear = 1;
        portal.Capability = GameRoomPortalCapability.Exit;
        entityManager.SetComponentData(portalEntity, portal);
        GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        runtimeState.CurrentRoomCleared = 0;
        runtimeState.Phase = GameProceduralLevelRuntimePhase.Active;
        entityManager.SetComponentData(managerEntity, runtimeState);

        ResetPortalOverlap(world, portalSystem, entityManager, playerEntity, portal);
        SetPlayerPosition(entityManager, playerEntity, GetInsidePosition(portal));
        portalSystem.Update(world.Unmanaged);
        Require(requests.Length == 0,
                "A clear-gated exit emitted before the active room completed.");

        // Re-enter after authoritative completion so the latch sees a new eligible crossing.
        ResetPortalOverlap(world, portalSystem, entityManager, playerEntity, portal);
        runtimeState.CurrentRoomCleared = 1;
        entityManager.SetComponentData(managerEntity, runtimeState);
        SetPlayerPosition(entityManager, playerEntity, GetInsidePosition(portal));
        portalSystem.Update(world.Unmanaged);
        ValidateSingleRequest(entityManager, managerEntity, portal.PortalId);
    }

    /// <summary>
    /// Verifies entrance-only portals never emit outgoing traversal requests even when graph state is assigned.
    /// </summary>
    /// <param name="world">Isolated ECS world.</param>
    /// <param name="portalSystem">Production portal detection system.</param>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural manager singleton.</param>
    /// <param name="playerEntity">Persistent player fixture.</param>
    /// <param name="portalEntity">Portal fixture.</param>
    private static void ValidateEntranceCannotEmit(World world,
                                                   SystemHandle portalSystem,
                                                   EntityManager entityManager,
                                                   Entity managerEntity,
                                                   Entity playerEntity,
                                                   Entity portalEntity)
    {
        DynamicBuffer<GameProceduralRoomTraversalRequest> requests = entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity);
        requests.Clear();
        GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portalEntity);
        portal.RequireRoomClear = 0;
        portal.Capability = GameRoomPortalCapability.Entrance;
        entityManager.SetComponentData(portalEntity, portal);
        ResetPortalOverlap(world, portalSystem, entityManager, playerEntity, portal);
        SetPlayerPosition(entityManager, playerEntity, GetInsidePosition(portal));
        portalSystem.Update(world.Unmanaged);
        Require(requests.Length == 0,
                "An entrance-only portal emitted an outgoing traversal request.");
    }
    #endregion

    #region Fixture Methods
    /// <summary>
    /// Creates the minimal procedural manager state required by the portal detection system.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <returns>Created procedural manager entity.</returns>
    private static Entity CreateManager(EntityManager entityManager)
    {
        Entity managerEntity = entityManager.CreateEntity(typeof(GameSceneTransitionState),
                                                           typeof(GameProceduralLevelRuntimeState));
        entityManager.SetComponentData(managerEntity, new GameSceneTransitionState
        {
            Phase = GameSceneTransitionPhase.Idle,
            Initialized = 1,
            IsTransitioning = 0
        });
        entityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
        {
            CurrentLevelIndex = 0,
            CurrentNodeIndex = 0,
            Phase = GameProceduralLevelRuntimePhase.Active,
            Initialized = 1,
            GraphGenerated = 1,
            CurrentRoomCleared = 1
        });
        DynamicBuffer<GameProceduralLevelDefinitionElement> levels = entityManager.AddBuffer<GameProceduralLevelDefinitionElement>(managerEntity);
        levels.Add(new GameProceduralLevelDefinitionElement
        {
            Enabled = 1,
            RequireRoomClearBeforeExit = 0
        });
        entityManager.AddBuffer<GameProceduralRoomTraversalRequest>(managerEntity);
        return managerEntity;
    }

    /// <summary>
    /// Creates one uniquely queryable persistent player transform.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <returns>Created player entity.</returns>
    private static Entity CreatePlayer(EntityManager entityManager)
    {
        Entity playerEntity = entityManager.CreateEntity(typeof(PlayerControllerConfig), typeof(LocalTransform));
        entityManager.SetComponentData(playerEntity, LocalTransform.FromPosition(float3.zero));
        return playerEntity;
    }

    /// <summary>
    /// Creates a ninety-degree rotated portal whose long local axis differs from its world AABB axes.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <returns>Created portal entity.</returns>
    private static Entity CreateRotatedPortal(EntityManager entityManager)
    {
        Entity portalEntity = entityManager.CreateEntity(typeof(GameRoomPortal), typeof(GameRoomPortalRuntimeState));
        entityManager.SetComponentData(portalEntity, new GameRoomPortal
        {
            PortalId = new FixedString64Bytes("ROTATED_EAST_EXIT"),
            Side = GameRoomPortalSide.East,
            Capability = GameRoomPortalCapability.Exit,
            Policy = GameRoomPortalConnectionPolicy.Optional,
            Center = new float3(5f, 2.5f, 3f),
            HalfExtents = new float3(2f, 1f, 0.25f),
            Rotation = quaternion.RotateY(math.radians(90f)),
            RequireRoomClear = 0
        });
        entityManager.SetComponentData(portalEntity, new GameRoomPortalRuntimeState
        {
            AssignedEdgeIndex = AssignedEdgeIndex,
            TraversalEnabled = 1
        });
        return portalEntity;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Moves outside the portal and updates once so overlap and trigger latches return to their neutral state.
    /// </summary>
    /// <param name="world">Isolated ECS world.</param>
    /// <param name="portalSystem">Production portal detection system.</param>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="playerEntity">Persistent player fixture.</param>
    /// <param name="portal">Portal defining the oriented outside position.</param>
    private static void ResetPortalOverlap(World world,
                                           SystemHandle portalSystem,
                                           EntityManager entityManager,
                                           Entity playerEntity,
                                           GameRoomPortal portal)
    {
        SetPlayerPosition(entityManager, playerEntity, GetOutsidePosition(portal));
        portalSystem.Update(world.Unmanaged);
    }

    /// <summary>
    /// Resolves a point inside the portal's rotated local volume.
    /// </summary>
    /// <param name="portal">Portal defining center, rotation and extents.</param>
    /// <returns>World-space inside position.</returns>
    private static float3 GetInsidePosition(GameRoomPortal portal)
    {
        float3 position =
            portal.Center +
            math.mul(portal.Rotation, new float3(1.5f, 0f, 0.1f));
        position.y = 0f;
        return position;
    }

    /// <summary>
    /// Resolves a point beyond the portal's long rotated local extent.
    /// </summary>
    /// <param name="portal">Portal defining center, rotation and extents.</param>
    /// <returns>World-space outside position.</returns>
    private static float3 GetOutsidePosition(GameRoomPortal portal)
    {
        float3 position =
            portal.Center +
            math.mul(portal.Rotation, new float3(2.5f, 0f, 0f));
        position.y = 0f;
        return position;
    }

    /// <summary>
    /// Updates only the player position while preserving a valid uniform transform.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="playerEntity">Persistent player fixture.</param>
    /// <param name="position">Target world-space position.</param>
    private static void SetPlayerPosition(EntityManager entityManager, Entity playerEntity, float3 position)
    {
        LocalTransform transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
        transform.Position = position;
        entityManager.SetComponentData(playerEntity, transform);
    }

    /// <summary>
    /// Verifies one request contains the expected physical source and graph assignment.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural manager singleton.</param>
    /// <param name="expectedPortalId">Expected source portal identity.</param>
    private static void ValidateSingleRequest(EntityManager entityManager,
                                              Entity managerEntity,
                                              FixedString64Bytes expectedPortalId)
    {
        DynamicBuffer<GameProceduralRoomTraversalRequest> requests = entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity, true);
        Require(requests.Length == 1, "An eligible portal entry did not emit exactly one request.");
        Require(requests[0].SourcePortalId.Equals(expectedPortalId),
                "The traversal request references the wrong physical source portal.");
        Require(requests[0].AssignedEdgeIndex == AssignedEdgeIndex && requests[0].SourceNodeIndex == 0,
                "The traversal request lost its generated edge or logical source node assignment.");
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
            throw new InvalidOperationException("GameProceduralRoomTraversalSmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
