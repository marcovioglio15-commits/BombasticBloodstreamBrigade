using Unity.Entities;

/// <summary>
/// Observes authoritative procedural-room and run-outcome state to emit idempotent Design telemetry events.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class GameTelemetryProgressionObservationSystem : SystemBase
{
    #region Fields
    private EntityQuery telemetryQuery;
    private EntityQuery proceduralQuery;
    private EntityQuery runOutcomeQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Builds reusable singleton queries without taking ownership of gameplay event buffers.
    /// </summary>
    protected override void OnCreate()
    {
        telemetryQuery = GetEntityQuery(ComponentType.ReadOnly<GameDataCollectionRuntimeConfig>(),
                                        ComponentType.ReadWrite<GameDataCollectionSessionState>(),
                                        ComponentType.ReadWrite<GameTelemetryProgressionObservationState>(),
                                        ComponentType.ReadWrite<GameTelemetryEvent>());
        proceduralQuery = GetEntityQuery(ComponentType.ReadOnly<GameProceduralRoomEnteredEvent>(),
                                         ComponentType.ReadOnly<GameProceduralRoomClearedEvent>());
        runOutcomeQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerRunOutcomeState>());
    }

    /// <summary>
    /// Observes new monotonic room versions and finalized outcomes, then advances observation state even without consent.
    /// </summary>
    protected override void OnUpdate()
    {
        if (telemetryQuery.CalculateEntityCount() != 1)
            return;

        Entity telemetryEntity = telemetryQuery.GetSingletonEntity();
        GameDataCollectionRuntimeConfig config = EntityManager.GetComponentData<GameDataCollectionRuntimeConfig>(telemetryEntity);

        if (config.Enabled == 0)
            return;

#if UNITY_EDITOR
        if (config.CollectInEditor == 0)
            return;
#endif

        GameDataCollectionSessionState sessionState = EntityManager.GetComponentData<GameDataCollectionSessionState>(telemetryEntity);
        GameTelemetryProgressionObservationState observationState =
            EntityManager.GetComponentData<GameTelemetryProgressionObservationState>(telemetryEntity);
        DynamicBuffer<GameTelemetryEvent> events = EntityManager.GetBuffer<GameTelemetryEvent>(telemetryEntity);
        ObserveProceduralEvents(events, ref sessionState, ref observationState, in config);
        ObserveRunOutcome(events, ref sessionState, ref observationState, in config);
        EntityManager.SetComponentData(telemetryEntity, sessionState);
        EntityManager.SetComponentData(telemetryEntity, observationState);
    }
    #endregion

    #region Observation
    /// <summary>
    /// Converts new room-entered and room-cleared versions into Design telemetry without clearing source buffers.
    /// </summary>
    /// <param name="events">Pending telemetry destination.</param>
    /// <param name="sessionState">Mutable consent and sequence state.</param>
    /// <param name="observationState">Mutable monotonic observation state.</param>
    /// <param name="config">Baked collection config.</param>
    private void ObserveProceduralEvents(DynamicBuffer<GameTelemetryEvent> events,
                                         ref GameDataCollectionSessionState sessionState,
                                         ref GameTelemetryProgressionObservationState observationState,
                                         in GameDataCollectionRuntimeConfig config)
    {
        if (proceduralQuery.CalculateEntityCount() != 1)
            return;

        Entity proceduralEntity = proceduralQuery.GetSingletonEntity();
        DynamicBuffer<GameProceduralRoomEnteredEvent> enteredEvents =
            EntityManager.GetBuffer<GameProceduralRoomEnteredEvent>(proceduralEntity, true);
        DynamicBuffer<GameProceduralRoomClearedEvent> clearedEvents =
            EntityManager.GetBuffer<GameProceduralRoomClearedEvent>(proceduralEntity, true);

        if (enteredEvents.Length > 0)
            ObserveRoomEntered(events,
                               ref sessionState,
                               ref observationState,
                               in config,
                               enteredEvents[enteredEvents.Length - 1]);

        if (clearedEvents.Length > 0)
            ObserveRoomCleared(events,
                               ref sessionState,
                               ref observationState,
                               in config,
                               clearedEvents[clearedEvents.Length - 1]);
    }

    /// <summary>
    /// Emits run-start and room-entered records for one unseen authoritative visit ordinal.
    /// </summary>
    /// <param name="events">Pending telemetry destination.</param>
    /// <param name="sessionState">Mutable consent and sequence state.</param>
    /// <param name="observationState">Mutable monotonic observation state.</param>
    /// <param name="config">Baked collection config.</param>
    /// <param name="enteredEvent">Latest authoritative room-entered event.</param>
    private static void ObserveRoomEntered(DynamicBuffer<GameTelemetryEvent> events,
                                           ref GameDataCollectionSessionState sessionState,
                                           ref GameTelemetryProgressionObservationState observationState,
                                           in GameDataCollectionRuntimeConfig config,
                                           GameProceduralRoomEnteredEvent enteredEvent)
    {
        if (enteredEvent.GenerationVersion == observationState.ObservedGenerationVersion &&
            enteredEvent.VisitOrdinal <= observationState.ObservedVisitOrdinal)
            return;

        long occurredAt = GameTelemetryEventRuntimeUtility.GetUtcNowUnixMilliseconds();

        if (observationState.RunStarted == 0 || enteredEvent.RunSeed != observationState.ObservedRunSeed)
        {
            GameTelemetryEventRuntimeUtility.TryEnqueue(events,
                                                        ref sessionState,
                                                        in config,
                                                        GameTelemetryEventType.RunStarted,
                                                        GameTelemetryDepartment.Design,
                                                        enteredEvent.RunSeed.ToString(),
                                                        occurredAt,
                                                        count0: enteredEvent.LevelIndex);
            observationState.RunStarted = 1;
            observationState.ObservedRunOutcome = PlayerRunOutcome.None;
        }

        string roomContext = string.Format("{0}:{1}:{2}",
                                           enteredEvent.LevelIndex,
                                           enteredEvent.NodeIndex,
                                           enteredEvent.TileIndex);
        GameTelemetryEventRuntimeUtility.TryEnqueue(events,
                                                    ref sessionState,
                                                    in config,
                                                    GameTelemetryEventType.RoomEntered,
                                                    GameTelemetryDepartment.Design,
                                                    roomContext,
                                                    occurredAt,
                                                    count0: enteredEvent.FirstVisit,
                                                    count1: (int)enteredEvent.VisitOrdinal);
        observationState.ObservedRunSeed = enteredEvent.RunSeed;
        observationState.ObservedGenerationVersion = enteredEvent.GenerationVersion;
        observationState.ObservedVisitOrdinal = enteredEvent.VisitOrdinal;
    }

    /// <summary>
    /// Emits one room-clear record for a previously unseen authoritative clear version.
    /// </summary>
    /// <param name="events">Pending telemetry destination.</param>
    /// <param name="sessionState">Mutable consent and sequence state.</param>
    /// <param name="observationState">Mutable monotonic observation state.</param>
    /// <param name="config">Baked collection config.</param>
    /// <param name="clearedEvent">Latest authoritative room-cleared event.</param>
    private static void ObserveRoomCleared(DynamicBuffer<GameTelemetryEvent> events,
                                           ref GameDataCollectionSessionState sessionState,
                                           ref GameTelemetryProgressionObservationState observationState,
                                           in GameDataCollectionRuntimeConfig config,
                                           GameProceduralRoomClearedEvent clearedEvent)
    {
        if (clearedEvent.GenerationVersion == observationState.ObservedGenerationVersion &&
            clearedEvent.ClearVersion <= observationState.ObservedClearVersion)
            return;

        string roomContext = string.Format("{0}:{1}:{2}",
                                           clearedEvent.LevelIndex,
                                           clearedEvent.NodeIndex,
                                           clearedEvent.TileIndex);
        GameTelemetryEventRuntimeUtility.TryEnqueue(events,
                                                    ref sessionState,
                                                    in config,
                                                    GameTelemetryEventType.RoomCleared,
                                                    GameTelemetryDepartment.Design,
                                                    roomContext,
                                                    GameTelemetryEventRuntimeUtility.GetUtcNowUnixMilliseconds(),
                                                    count0: (int)clearedEvent.ClearVersion);
        observationState.ObservedGenerationVersion = clearedEvent.GenerationVersion;
        observationState.ObservedClearVersion = clearedEvent.ClearVersion;
    }

    /// <summary>
    /// Emits one terminal run result and one defeat record when the authoritative outcome becomes finalized.
    /// </summary>
    /// <param name="events">Pending telemetry destination.</param>
    /// <param name="sessionState">Mutable consent and sequence state.</param>
    /// <param name="observationState">Mutable observed outcome state.</param>
    /// <param name="config">Baked collection config.</param>
    private void ObserveRunOutcome(DynamicBuffer<GameTelemetryEvent> events,
                                   ref GameDataCollectionSessionState sessionState,
                                   ref GameTelemetryProgressionObservationState observationState,
                                   in GameDataCollectionRuntimeConfig config)
    {
        if (runOutcomeQuery.CalculateEntityCount() != 1)
            return;

        PlayerRunOutcomeState outcomeState = runOutcomeQuery.GetSingleton<PlayerRunOutcomeState>();

        if (outcomeState.IsFinalized == 0 ||
            outcomeState.Outcome == PlayerRunOutcome.None ||
            outcomeState.Outcome == observationState.ObservedRunOutcome)
            return;

        long occurredAt = GameTelemetryEventRuntimeUtility.GetUtcNowUnixMilliseconds();
        GameTelemetryEventRuntimeUtility.TryEnqueue(events,
                                                    ref sessionState,
                                                    in config,
                                                    GameTelemetryEventType.RunEnded,
                                                    GameTelemetryDepartment.Design,
                                                    outcomeState.Outcome.ToString(),
                                                    occurredAt,
                                                    count0: (int)outcomeState.Outcome);

        if (outcomeState.Outcome == PlayerRunOutcome.Defeat)
        {
            GameTelemetryEventRuntimeUtility.TryEnqueue(events,
                                                        ref sessionState,
                                                        in config,
                                                        GameTelemetryEventType.PlayerDefeated,
                                                        GameTelemetryDepartment.Design,
                                                        outcomeState.Outcome.ToString(),
                                                        occurredAt);
        }

        observationState.ObservedRunOutcome = outcomeState.Outcome;
    }
    #endregion

    #endregion
}
