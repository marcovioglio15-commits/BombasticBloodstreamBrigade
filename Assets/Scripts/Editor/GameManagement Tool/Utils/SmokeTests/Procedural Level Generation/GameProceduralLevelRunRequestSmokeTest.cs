#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Verifies public procedural run requests preserve explicit External seed authority without blocking later requests.
/// </summary>
public static class GameProceduralLevelRunRequestSmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes Fixed and External start and restart request checks from Unity batch mode.
    /// </summary>
    public static void Run()
    {
        World previousWorld = World.DefaultGameObjectInjectionWorld;
        World world = new World("GameProceduralLevelRunRequestSmokeTest");
        World.DefaultGameObjectInjectionWorld = world;

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(managerEntity, new GameProceduralLevelConfig
            {
                SeedMode = GameProceduralLevelSeedMode.External
            });
            DynamicBuffer<GameProceduralLevelRunRequest> requests = entityManager.AddBuffer<GameProceduralLevelRunRequest>(managerEntity);

            Require(!GameProceduralLevelRunRequestUtility.TryStartRun(),
                    "External mode accepted a start request without an authoritative seed.");
            Require(requests.Length == 0,
                    "Rejected External start request polluted the authoritative request buffer.");
            Require(!GameProceduralLevelRunRequestUtility.TryStartRun(0u),
                    "Explicit start accepted the reserved zero seed.");
            Require(GameProceduralLevelRunRequestUtility.TryStartRun(37u),
                    "External mode rejected a valid explicit start seed.");
            Require(requests.Length == 1 && requests[0].RunSeed == 37u && requests[0].HasExplicitSeed != 0,
                    "External explicit start request was not stored exactly once.");

            requests.Clear();
            Require(!GameProceduralLevelRunRequestUtility.TryRestartRun(),
                    "External mode accepted a restart without an authoritative seed.");
            Require(requests.Length == 0,
                    "Rejected External restart request polluted the authoritative request buffer.");
            Require(GameProceduralLevelRunRequestUtility.TryRestartRun(73u),
                    "External mode rejected a valid explicit restart seed.");
            Require(requests.Length == 1 && requests[0].RunSeed == 73u && requests[0].Restart != 0,
                    "External explicit restart request lost its restart semantics.");

            requests.Clear();
            GameProceduralLevelConfig config = entityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);
            config.SeedMode = GameProceduralLevelSeedMode.Fixed;
            entityManager.SetComponentData(managerEntity, config);
            Require(GameProceduralLevelRunRequestUtility.TryStartRun(),
                    "Fixed mode rejected its baked seed policy request.");
            Require(requests.Length == 1 && requests[0].HasExplicitSeed == 0,
                    "Fixed policy start request was not queued exactly once.");

            Debug.Log("[GameProceduralLevelRunRequestSmokeTest] All run-request seed policy checks passed.");
        }
        finally
        {
            World.DefaultGameObjectInjectionWorld = previousWorld;
            world.Dispose();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Throws one actionable smoke-test failure when a run-request invariant is violated.
    /// </summary>
    /// <param name="condition">Invariant result.</param>
    /// <param name="message">Failure diagnostic.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameProceduralLevelRunRequestSmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
