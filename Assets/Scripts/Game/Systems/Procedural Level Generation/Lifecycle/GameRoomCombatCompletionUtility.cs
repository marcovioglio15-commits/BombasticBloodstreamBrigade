using Unity.Entities;

/// <summary>
/// Provides allocation-free local predicates used by the shared combat-completion aggregate system.
/// </summary>
public static class GameRoomCombatCompletionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether one spawner is initialized, empty and owns only completed runtime waves.
    /// </summary>
    /// <param name="spawnerState">Current compact spawner lifecycle state.</param>
    /// <param name="waves">Runtime wave buffer owned by the spawner.</param>
    /// <param name="hasWaves">True when the spawner owns at least one authored runtime wave.</param>
    /// <returns>True when the spawner and each of its waves have completed.</returns>
    public static bool IsSpawnerComplete(EnemySpawnerState spawnerState,
                                         DynamicBuffer<EnemySpawnerWaveRuntimeElement> waves,
                                         out bool hasWaves)
    {
        hasWaves = waves.Length > 0;

        if (spawnerState.Initialized == 0 || spawnerState.AliveCount > 0 || !hasWaves)
            return false;

        for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
        {
            if (waves[waveIndex].Completed == 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves whether one active Boss minion is configured to delay completion after its owner dies.
    /// </summary>
    /// <param name="owner">Boss ownership and completion policy stored on the active minion.</param>
    /// <returns>True when this minion blocks room and run completion.</returns>
    public static bool BlocksCompletion(EnemyBossMinionOwner owner)
    {
        return owner.BlocksRunCompletion != 0;
    }
    #endregion

    #endregion
}
