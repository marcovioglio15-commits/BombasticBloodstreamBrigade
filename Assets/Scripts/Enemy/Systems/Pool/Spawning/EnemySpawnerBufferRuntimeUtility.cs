using Unity.Entities;

/// <summary>
/// Reacquires and writes mutable spawner buffers after operations that can invalidate buffer handles.
/// </summary>
public static class EnemySpawnerBufferRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Writes one wave runtime element after spawn-side structural changes have completed.
    /// </summary>
    /// <param name="entityManager">Entity manager used to reacquire the runtime buffer.</param>
    /// <param name="spawnerEntity">Spawner that owns the runtime buffer.</param>
    /// <param name="waveIndex">Wave index to update.</param>
    /// <param name="runtime">Runtime data to store.</param>
    public static void SetWaveRuntime(EntityManager entityManager,
                                      Entity spawnerEntity,
                                      int waveIndex,
                                      EnemySpawnerWaveRuntimeElement runtime)
    {
        DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntime = entityManager.GetBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);

        if (waveIndex >= 0 && waveIndex < waveRuntime.Length)
            waveRuntime[waveIndex] = runtime;
    }

    /// <summary>
    /// Writes one staged wave event after enemy reservation or activation has potentially changed archetypes.
    /// </summary>
    /// <param name="entityManager">Entity manager used to reacquire the event buffer.</param>
    /// <param name="spawnerEntity">Spawner that owns the event buffer.</param>
    /// <param name="eventIndex">Event index to update.</param>
    /// <param name="waveEvent">Event data to store.</param>
    public static void SetWaveEvent(EntityManager entityManager,
                                    Entity spawnerEntity,
                                    int eventIndex,
                                    EnemySpawnerWaveEventElement waveEvent)
    {
        DynamicBuffer<EnemySpawnerWaveEventElement> waveEvents = entityManager.GetBuffer<EnemySpawnerWaveEventElement>(spawnerEntity);

        if (eventIndex >= 0 && eventIndex < waveEvents.Length)
            waveEvents[eventIndex] = waveEvent;
    }
    #endregion

    #endregion
}
