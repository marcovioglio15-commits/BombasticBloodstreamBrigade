using Unity.Entities;

/// <summary>
/// Provides allocation-free public entry points for starting or restarting the authoritative procedural run.
/// </summary>
public static class GameProceduralLevelRunRequestUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Queues a run start using the Fixed or Random seed policy baked in the active Procedural Level preset.
    /// </summary>
    /// <returns>True when the unique procedural manager accepted the request; false when External mode requires an explicit seed.</returns>
    public static bool TryStartRun()
    {
        return TryEnqueue(default);
    }

    /// <summary>
    /// Queues a run start using an authoritative external seed, including when the preset seed mode is External.
    /// </summary>
    /// <param name="runSeed">Non-zero deterministic run seed supplied by save, network or test orchestration.</param>
    /// <returns>True when the unique procedural manager accepted the request.</returns>
    public static bool TryStartRun(uint runSeed)
    {
        if (runSeed == 0u)
            return false;

        return TryEnqueue(new GameProceduralLevelRunRequest
        {
            RunSeed = runSeed,
            HasExplicitSeed = 1
        });
    }

    /// <summary>
    /// Queues a full graph restart and returns the player to the first enabled procedural level.
    /// </summary>
    /// <param name="runSeed">Optional explicit run seed; zero reapplies the preset seed policy.</param>
    /// <returns>True when the unique procedural manager accepted the restart request; false when External mode requires a non-zero seed.</returns>
    public static bool TryRestartRun(uint runSeed = 0u)
    {
        return TryEnqueue(new GameProceduralLevelRunRequest
        {
            RunSeed = runSeed,
            HasExplicitSeed = runSeed != 0u ? (byte)1 : (byte)0,
            Restart = 1
        });
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one request to the unique manager buffer without creating runtime objects or relying on reflection.
    /// </summary>
    /// <param name="request">Run request to enqueue.</param>
    /// <returns>True when a unique initialized ECS world and procedural manager were found.</returns>
    private static bool TryEnqueue(GameProceduralLevelRunRequest request)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        EntityQuery query = world.EntityManager.CreateEntityQuery(typeof(GameProceduralLevelConfig),
                                                                  typeof(GameProceduralLevelRunRequest));

        try
        {
            if (query.CalculateEntityCount() != 1)
                return false;

            Entity managerEntity = query.GetSingletonEntity();
            GameProceduralLevelConfig config = world.EntityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);

            if (config.SeedMode == GameProceduralLevelSeedMode.External && request.HasExplicitSeed == 0)
                return false;

            world.EntityManager.GetBuffer<GameProceduralLevelRunRequest>(managerEntity).Add(request);
            return true;
        }
        finally
        {
            query.Dispose();
        }
    }
    #endregion

    #endregion
}
