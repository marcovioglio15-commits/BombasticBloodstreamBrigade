#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Verifies one-shot room completion, monotonic clear counters and terminal Boss lifecycle advancement.
/// </summary>
public static class GameProceduralRoomClearSmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes isolated room-clear lifecycle checks from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidateRegularRoomClearsOnce();
        ValidateBlockingBossMinionDelaysClear();
        ValidateFinalBossCompletesRun();
        Debug.Log("[GameProceduralRoomClearSmokeTest] All room-clear lifecycle checks passed.");
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Verifies a completed regular room marks its node and increments the global counter exactly once.
    /// </summary>
    private static void ValidateRegularRoomClearsOnce()
    {
        World world = new World("GameProceduralRegularRoomClearSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateManager(entityManager, GameProceduralRoomRole.Regular);
            CreateCompleteSpawner(entityManager);
            UpdateCompletion(world);
            ValidateClearedState(entityManager,
                                 managerEntity,
                                 GameProceduralLevelRuntimePhase.Active,
                                 1u,
                                 1u);

            // A second update must hit the authoritative one-shot guard without changing counters.
            UpdateCompletion(world);
            ValidateClearedState(entityManager,
                                 managerEntity,
                                 GameProceduralLevelRuntimePhase.Active,
                                 1u,
                                 1u);
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies an active configured Boss minion blocks completion until its despawn is already requested.
    /// </summary>
    private static void ValidateBlockingBossMinionDelaysClear()
    {
        World world = new World("GameProceduralBlockingMinionClearSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateManager(entityManager, GameProceduralRoomRole.Regular);
            CreateCompleteSpawner(entityManager);
            Entity minionEntity = entityManager.CreateEntity(typeof(EnemyBossMinionOwner), typeof(EnemyActive));
            entityManager.SetComponentData(minionEntity, new EnemyBossMinionOwner
            {
                BlocksRunCompletion = 1
            });
            UpdateCompletion(world);
            GameProceduralLevelRuntimeState blockedState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
            Require(blockedState.CurrentRoomCleared == 0,
                    "A configured active Boss minion did not block room completion.");

            // Despawn-requested minions leave the blocking query before structural destruction completes.
            entityManager.AddComponent<EnemyDespawnRequest>(minionEntity);
            UpdateCompletion(world);
            ValidateClearedState(entityManager,
                                 managerEntity,
                                 GameProceduralLevelRuntimePhase.Active,
                                 1u,
                                 1u);
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies the Boss in the final enabled level advances the procedural runtime to RunComplete.
    /// </summary>
    private static void ValidateFinalBossCompletesRun()
    {
        World world = new World("GameProceduralFinalBossClearSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateManager(entityManager, GameProceduralRoomRole.Boss);
            CreateCompleteSpawner(entityManager);
            UpdateCompletion(world);
            ValidateClearedState(entityManager,
                                 managerEntity,
                                 GameProceduralLevelRuntimePhase.RunComplete,
                                 1u,
                                 1u);
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies synchronized runtime, graph-node and monotonic counter state after one clear event.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Procedural manager singleton.</param>
    /// <param name="expectedPhase">Expected lifecycle phase after completion.</param>
    /// <param name="expectedTotalCleared">Expected monotonic clear total.</param>
    /// <param name="expectedVersion">Expected clear event version.</param>
    private static void ValidateClearedState(EntityManager entityManager,
                                             Entity managerEntity,
                                             GameProceduralLevelRuntimePhase expectedPhase,
                                             uint expectedTotalCleared,
                                             uint expectedVersion)
    {
        GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        GameProceduralRoomClearCounter counter = entityManager.GetComponentData<GameProceduralRoomClearCounter>(managerEntity);
        DynamicBuffer<GameProceduralRoomNodeElement> nodes = entityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity, true);
        Require(runtimeState.CurrentRoomCleared != 0,
                "Authoritative runtime state did not mark the current room cleared.");
        Require(runtimeState.Phase == expectedPhase,
                "Room completion advanced to " + runtimeState.Phase + " instead of " + expectedPhase + ".");
        Require(nodes.Length == 1 && nodes[0].Cleared != 0,
                "The active logical graph node did not receive its one-shot clear marker.");
        Require(counter.TotalCleared == expectedTotalCleared && counter.Version == expectedVersion,
                "The global room-clear counter or event version is not monotonic and one-shot.");
    }
    #endregion

    #region Fixture Methods
    /// <summary>
    /// Creates one active procedural manager owning a single logical room node and one final enabled level.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="role">Structural role assigned to the active node.</param>
    /// <returns>Created procedural manager singleton.</returns>
    private static Entity CreateManager(EntityManager entityManager, GameProceduralRoomRole role)
    {
        Entity managerEntity = entityManager.CreateEntity(typeof(GameProceduralLevelRuntimeState),
                                                           typeof(GameProceduralRoomClearCounter),
                                                           typeof(GameRoomCombatCompletionState));
        entityManager.SetComponentData(managerEntity, new GameProceduralLevelRuntimeState
        {
            CurrentLevelIndex = 0,
            CurrentNodeIndex = 0,
            Phase = GameProceduralLevelRuntimePhase.Active,
            Initialized = 1,
            GraphGenerated = 1,
            CurrentRoomCleared = 0
        });
        entityManager.SetComponentData(managerEntity, new GameProceduralRoomClearCounter());
        DynamicBuffer<GameProceduralRoomNodeElement> nodes = entityManager.AddBuffer<GameProceduralRoomNodeElement>(managerEntity);
        nodes.Add(new GameProceduralRoomNodeElement
        {
            NodeIndex = 0,
            LevelIndex = 0,
            Role = role,
            Cleared = 0
        });
        DynamicBuffer<GameProceduralLevelDefinitionElement> levels = entityManager.AddBuffer<GameProceduralLevelDefinitionElement>(managerEntity);
        levels.Add(new GameProceduralLevelDefinitionElement
        {
            OrderIndex = 0,
            Enabled = 1
        });
        entityManager.AddBuffer<GameProceduralRoomClearedEvent>(managerEntity);
        return managerEntity;
    }

    /// <summary>
    /// Creates one initialized empty spawner whose only runtime wave is complete.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <returns>Created completed spawner entity.</returns>
    private static Entity CreateCompleteSpawner(EntityManager entityManager)
    {
        Entity spawnerEntity = entityManager.CreateEntity(typeof(EnemySpawner), typeof(EnemySpawnerState));
        entityManager.SetComponentData(spawnerEntity, new EnemySpawnerState
        {
            Initialized = 1,
            AliveCount = 0
        });
        DynamicBuffer<EnemySpawnerWaveRuntimeElement> waves = entityManager.AddBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);
        waves.Add(new EnemySpawnerWaveRuntimeElement
        {
            Completed = 1,
            AliveCount = 0
        });
        return spawnerEntity;
    }

    /// <summary>
    /// Updates the allocation-free combat aggregate before consuming its value in procedural room progression.
    /// </summary>
    /// <param name="world">Isolated smoke-test world owning both systems.</param>
    private static void UpdateCompletion(World world)
    {
        SystemHandle aggregateSystem = world.GetOrCreateSystem<GameRoomCombatCompletionSystem>();
        SystemHandle completionSystem = world.GetOrCreateSystem<GameProceduralRoomCompletionSystem>();
        aggregateSystem.Update(world.Unmanaged);
        completionSystem.Update(world.Unmanaged);
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
            throw new InvalidOperationException("GameProceduralRoomClearSmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
