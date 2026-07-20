#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Verifies that legacy non-procedural scene managers retain the shared combat aggregate and Victory path.
/// </summary>
public static class GameLegacyRoomVictorySmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the isolated legacy Victory regression check from the aggregate producer through run outcome.
    /// </summary>
    public static void Run()
    {
        World world = new World("GameLegacyRoomVictorySmokeTest");

        try
        {
            // Build a non-procedural room with one alive player and one completed wave.
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateLegacySceneManager(entityManager);
            Entity playerEntity = CreateAlivePlayer(entityManager);
            CreateCompleteSpawner(entityManager);

            // Publish the shared combat predicate before consuming it through the legacy Victory path.
            SystemHandle aggregateSystem = world.GetOrCreateSystem<GameRoomCombatCompletionSystem>();
            SystemHandle outcomeSystem = world.GetOrCreateSystem<PlayerRunOutcomeSystem>();
            aggregateSystem.Update(world.Unmanaged);

            GameRoomCombatCompletionState completionState = entityManager.GetComponentData<GameRoomCombatCompletionState>(managerEntity);
            Require(completionState.IsComplete != 0,
                    "The shared combat aggregate did not complete for a legacy room.");
            outcomeSystem.Update(world.Unmanaged);

            PlayerRunOutcomeState outcomeState = entityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);
            Require(outcomeState.Outcome == PlayerRunOutcome.Victory && outcomeState.IsFinalized != 0,
                    "A completed legacy room did not finalize Victory without procedural runtime components.");
            Debug.Log("[GameLegacyRoomVictorySmokeTest] Legacy aggregate and Victory checks passed.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Fixture Methods
    /// <summary>
    /// Creates a legacy Scene Manager carrying only the shared combat aggregate required by run outcome.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <returns>Created non-procedural Scene Manager entity.</returns>
    private static Entity CreateLegacySceneManager(EntityManager entityManager)
    {
        Entity managerEntity = entityManager.CreateEntity(typeof(GameSceneManagerConfig),
                                                           typeof(GameRoomCombatCompletionState));
        Require(!entityManager.HasComponent<GameProceduralLevelRuntimeState>(managerEntity),
                "The legacy fixture unexpectedly contains procedural runtime state.");
        return managerEntity;
    }

    /// <summary>
    /// Creates the minimal alive local-player state required by authoritative run-outcome evaluation.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <returns>Created local-player entity.</returns>
    private static Entity CreateAlivePlayer(EntityManager entityManager)
    {
        Entity playerEntity = entityManager.CreateEntity(typeof(PlayerControllerConfig),
                                                          typeof(PlayerHealth),
                                                          typeof(PlayerRunOutcomeState),
                                                          typeof(PlayerDeathAnimationConfig));
        entityManager.SetComponentData(playerEntity, new PlayerHealth
        {
            Current = 1f,
            Max = 1f
        });
        return playerEntity;
    }

    /// <summary>
    /// Creates one initialized spawner whose sole authored wave is already complete.
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
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Throws one actionable smoke-test failure when a legacy Victory invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure message describing the violated invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameLegacyRoomVictorySmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
