using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves explicit wave prerequisites and aggregate sequence-step barriers without allocating runtime state.
/// </summary>
internal static class EnemyWaveSequenceRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the clock used to schedule one wave from either an explicit wave or the preceding parallel step.
    /// </summary>
    /// <param name="spawnerState">Spawner clock used by first-step waves and empty selected steps.</param>
    /// <param name="definitions">Immutable definitions for every wave owned by the spawner.</param>
    /// <param name="runtime">Mutable state for every wave owned by the spawner.</param>
    /// <param name="definition">Wave definition requesting a reference time.</param>
    /// <param name="referenceTime">Resolved absolute runtime timestamp when the dependency is satisfied.</param>
    /// <returns>True when the requested dependency state is available, otherwise false.</returns>
    public static bool TryResolveReferenceTime(EnemySpawnerState spawnerState,
                                               DynamicBuffer<EnemySpawnerWaveDefinitionElement> definitions,
                                               DynamicBuffer<EnemySpawnerWaveRuntimeElement> runtime,
                                               EnemySpawnerWaveDefinitionElement definition,
                                               out float referenceTime)
    {
        if (definition.ReferenceWaveIndex >= 0)
        {
            if (definition.ReferenceWaveIndex >= definitions.Length ||
                definition.ReferenceWaveIndex >= runtime.Length)
            {
                referenceTime = 0f;
                return false;
            }

            return TryResolveWaveReferenceTime(spawnerState,
                                               definitions[definition.ReferenceWaveIndex],
                                               runtime[definition.ReferenceWaveIndex],
                                               definition.StartMode,
                                               out referenceTime);
        }

        if (definition.ReferenceSequenceStepIndex >= 0)
        {
            return TryResolveStepReferenceTime(spawnerState,
                                               definitions,
                                               runtime,
                                               definition.ReferenceSequenceStepIndex,
                                               definition.StartMode,
                                               out referenceTime);
        }

        referenceTime = spawnerState.StartTime;
        return true;
    }
    #endregion

    #region Reference Methods
    /// <summary>
    /// Resolves the requested event timestamp from one explicitly referenced wave.
    /// </summary>
    /// <param name="spawnerState">Spawner clock used by the explicit spawner-start mode.</param>
    /// <param name="previousDefinition">Immutable definition of the referenced wave.</param>
    /// <param name="previousRuntime">Current runtime state of the referenced wave.</param>
    /// <param name="startMode">Event that must be available before scheduling.</param>
    /// <param name="referenceTime">Resolved absolute runtime timestamp.</param>
    /// <returns>True when the requested event is available, otherwise false.</returns>
    private static bool TryResolveWaveReferenceTime(EnemySpawnerState spawnerState,
                                                    EnemySpawnerWaveDefinitionElement previousDefinition,
                                                    EnemySpawnerWaveRuntimeElement previousRuntime,
                                                    EnemyWaveStartMode startMode,
                                                    out float referenceTime)
    {
        switch (startMode)
        {
            case EnemyWaveStartMode.FromSpawnerStart:
                referenceTime = spawnerState.StartTime;
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveStart:
                if (previousRuntime.Started != 0)
                {
                    referenceTime = previousRuntime.SpawnStartTime;
                    return true;
                }
                break;

            case EnemyWaveStartMode.AfterPreviousWaveSpawnEnd:
                if (previousRuntime.Started != 0)
                {
                    referenceTime = previousRuntime.SpawnStartTime +
                                    math.max(0f, previousDefinition.SpawnDurationSeconds);
                    return true;
                }
                break;

            case EnemyWaveStartMode.AfterPreviousWaveCompleted:
                if (previousRuntime.Completed != 0)
                {
                    referenceTime = previousRuntime.CompletionTime;
                    return true;
                }
                break;

            case EnemyWaveStartMode.AfterPreviousWaveFirstKill:
                if (previousRuntime.FirstKillRegistered != 0)
                {
                    referenceTime = previousRuntime.FirstKillTime;
                    return true;
                }
                break;
        }

        referenceTime = 0f;
        return false;
    }

    /// <summary>
    /// Resolves an aggregate barrier across every enabled wave in one preceding sequence step.
    /// </summary>
    /// <param name="spawnerState">Spawner clock used by the explicit spawner-start mode or an empty selected step.</param>
    /// <param name="definitions">Immutable wave definitions containing step membership.</param>
    /// <param name="runtime">Current runtime wave states containing selection and progress.</param>
    /// <param name="stepIndex">Preceding sequence step that must satisfy the barrier.</param>
    /// <param name="startMode">Aggregate event required by the dependent wave.</param>
    /// <param name="referenceTime">Resolved absolute timestamp across the step.</param>
    /// <returns>True when the aggregate condition is satisfied, otherwise false.</returns>
    private static bool TryResolveStepReferenceTime(EnemySpawnerState spawnerState,
                                                    DynamicBuffer<EnemySpawnerWaveDefinitionElement> definitions,
                                                    DynamicBuffer<EnemySpawnerWaveRuntimeElement> runtime,
                                                    int stepIndex,
                                                    EnemyWaveStartMode startMode,
                                                    out float referenceTime)
    {
        if (startMode == EnemyWaveStartMode.FromSpawnerStart)
        {
            referenceTime = spawnerState.StartTime;
            return true;
        }

        bool foundEnabledWave = false;
        bool foundReferenceEvent = false;
        referenceTime = startMode == EnemyWaveStartMode.AfterPreviousWaveFirstKill
            ? float.MaxValue
            : spawnerState.StartTime;

        // Aggregate only runtime-enabled waves; difficulty-rejected alternatives never block a later step.
        for (int waveIndex = 0; waveIndex < definitions.Length && waveIndex < runtime.Length; waveIndex++)
        {
            if (definitions[waveIndex].SequenceStepIndex != stepIndex || runtime[waveIndex].Enabled == 0)
                continue;

            foundEnabledWave = true;

            if (startMode == EnemyWaveStartMode.AfterPreviousWaveFirstKill)
            {
                if (runtime[waveIndex].FirstKillRegistered != 0)
                {
                    referenceTime = math.min(referenceTime, runtime[waveIndex].FirstKillTime);
                    foundReferenceEvent = true;
                }

                continue;
            }

            if (!TryAccumulateStepReference(definitions[waveIndex],
                                            runtime[waveIndex],
                                            startMode,
                                            ref referenceTime))
            {
                return false;
            }
        }

        if (!foundEnabledWave)
        {
            referenceTime = spawnerState.StartTime;
            return true;
        }

        return startMode != EnemyWaveStartMode.AfterPreviousWaveFirstKill || foundReferenceEvent;
    }

    /// <summary>
    /// Accumulates one enabled parallel wave into the barrier timestamp for its sequence step.
    /// </summary>
    /// <param name="definition">Immutable parallel wave definition.</param>
    /// <param name="runtime">Current progress of the parallel wave.</param>
    /// <param name="startMode">Aggregate event required by the dependent step.</param>
    /// <param name="referenceTime">Mutable maximum timestamp, or minimum timestamp for first-kill mode.</param>
    /// <returns>True when this wave satisfies the requested event condition, otherwise false.</returns>
    private static bool TryAccumulateStepReference(EnemySpawnerWaveDefinitionElement definition,
                                                   EnemySpawnerWaveRuntimeElement runtime,
                                                   EnemyWaveStartMode startMode,
                                                   ref float referenceTime)
    {
        switch (startMode)
        {
            case EnemyWaveStartMode.AfterPreviousWaveStart:
                if (runtime.Started == 0)
                    return false;

                referenceTime = math.max(referenceTime, runtime.SpawnStartTime);
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveSpawnEnd:
                if (runtime.Started == 0)
                    return false;

                referenceTime = math.max(referenceTime,
                                         runtime.SpawnStartTime + math.max(0f, definition.SpawnDurationSeconds));
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveCompleted:
                if (runtime.Completed == 0)
                    return false;

                referenceTime = math.max(referenceTime, runtime.CompletionTime);
                return true;

        }

        return false;
    }
    #endregion

    #endregion
}
