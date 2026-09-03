using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Enqueues bounded consent-aware telemetry events without exposing transport or authentication concerns to producers.
/// </summary>
public static class GameTelemetryEventRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends one compact event when its department is explicitly consented and a session is active.
    /// </summary>
    /// <param name="events">Telemetry buffer receiving the event.</param>
    /// <param name="sessionState">Mutable consent and sequence state.</param>
    /// <param name="config">Runtime capacity and availability settings.</param>
    /// <param name="eventType">Stable event contract identifier.</param>
    /// <param name="department">Department allowed to consume the event.</param>
    /// <param name="contextId">Optional stable room, asset, build, or outcome identifier.</param>
    /// <param name="occurredAtUnixMilliseconds">UTC event time in Unix milliseconds.</param>
    /// <param name="metric0">First floating-point metric slot defined by the event contract.</param>
    /// <param name="metric1">Second floating-point metric slot defined by the event contract.</param>
    /// <param name="metric2">Third floating-point metric slot defined by the event contract.</param>
    /// <param name="metric3">Fourth floating-point metric slot defined by the event contract.</param>
    /// <param name="count0">First integer metric slot defined by the event contract.</param>
    /// <param name="count1">Second integer metric slot defined by the event contract.</param>
    /// <param name="count2">Third integer metric slot defined by the event contract.</param>
    /// <param name="count3">Fourth integer metric slot defined by the event contract.</param>
    /// <returns>True when the event was accepted into the bounded queue.</returns>
    public static bool TryEnqueue(DynamicBuffer<GameTelemetryEvent> events,
                                  ref GameDataCollectionSessionState sessionState,
                                  in GameDataCollectionRuntimeConfig config,
                                  GameTelemetryEventType eventType,
                                  GameTelemetryDepartment department,
                                  string contextId,
                                  long occurredAtUnixMilliseconds,
                                  float metric0 = 0f,
                                  float metric1 = 0f,
                                  float metric2 = 0f,
                                  float metric3 = 0f,
                                  int count0 = 0,
                                  int count1 = 0,
                                  int count2 = 0,
                                  int count3 = 0)
    {
        if (eventType == GameTelemetryEventType.None ||
            !CanCollect(in sessionState, in config, department))
            return false;

        int maximumPendingEvents = math.max(1, config.MaximumPendingEvents);

        if (events.Length >= maximumPendingEvents)
            events.RemoveAt(0);

        FixedString128Bytes context = default;
        context.CopyFromTruncated(contextId ?? string.Empty);
        sessionState.NextSequence++;
        events.Add(new GameTelemetryEvent
        {
            Sequence = sessionState.NextSequence,
            OccurredAtUnixMilliseconds = occurredAtUnixMilliseconds,
            EventType = eventType,
            Department = department,
            ContextId = context,
            Metric0 = metric0,
            Metric1 = metric1,
            Metric2 = metric2,
            Metric3 = metric3,
            Count0 = count0,
            Count1 = count1,
            Count2 = count2,
            Count3 = count3
        });
        return true;
    }

    /// <summary>
    /// Returns the current UTC time used by managed telemetry producers.
    /// </summary>
    /// <returns>Current Unix timestamp in milliseconds.</returns>
    public static long GetUtcNowUnixMilliseconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Checks collection availability, active session, notice acknowledgement, and every requested department consent.
    /// </summary>
    /// <param name="sessionState">Current consent and session state.</param>
    /// <param name="config">Current runtime collection config.</param>
    /// <param name="department">Department flags required by the event.</param>
    /// <returns>True when the event may be collected.</returns>
    public static bool CanCollect(in GameDataCollectionSessionState sessionState,
                                  in GameDataCollectionRuntimeConfig config,
                                  GameTelemetryDepartment department)
    {
#if UNITY_EDITOR
        if (config.CollectInEditor == 0)
            return false;
#endif

        if (config.Enabled == 0 ||
            sessionState.NoticeAcknowledged == 0 ||
            sessionState.UserId.IsEmpty ||
            sessionState.SessionId.Length <= 0 ||
            !HasConsentForDepartment(in sessionState, department))
            return false;

        return true;
    }

    /// <summary>
    /// Checks whether every department represented by one event remains explicitly granted.
    /// </summary>
    /// <param name="sessionState">Current category choices.</param>
    /// <param name="department">Department flags required by the event.</param>
    /// <returns>True when the department mask is non-empty and fully consented.</returns>
    public static bool HasConsentForDepartment(in GameDataCollectionSessionState sessionState,
                                               GameTelemetryDepartment department)
    {
        if (department == GameTelemetryDepartment.None)
            return false;

        if ((department & GameTelemetryDepartment.Programming) != 0 &&
            sessionState.ProgrammingConsent != GameTelemetryConsentChoice.Granted)
            return false;

        if ((department & GameTelemetryDepartment.Design) != 0 &&
            sessionState.DesignConsent != GameTelemetryConsentChoice.Granted)
            return false;

        if ((department & GameTelemetryDepartment.Art3D) != 0 &&
            sessionState.Art3DConsent != GameTelemetryConsentChoice.Granted)
            return false;

        return true;
    }
    #endregion

    #endregion
}
