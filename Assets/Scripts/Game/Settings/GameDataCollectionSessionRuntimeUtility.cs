using System;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Applies explicit consent and server-verified identity to the data-collection ECS singleton.
/// </summary>
public static class GameDataCollectionSessionRuntimeUtility
{
    #region Methods

    #region Consent
    /// <summary>
    /// Applies a versioned notice acknowledgement and category choices, then purges newly disallowed pending events.
    /// </summary>
    /// <param name="noticeAcknowledged">True when the current notice was shown and acknowledged.</param>
    /// <param name="programmingConsent">True to allow programming telemetry.</param>
    /// <param name="designConsent">True to allow design telemetry.</param>
    /// <param name="art3DConsent">True to allow 3D telemetry.</param>
    /// <returns>True when the unique data-collection singleton was updated.</returns>
    public static bool TryApplyConsent(bool noticeAcknowledged,
                                       bool programmingConsent,
                                       bool designConsent,
                                       bool art3DConsent)
    {
        if (!TryResolve(out EntityManager entityManager, out Entity entity))
            return false;

        GameDataCollectionRuntimeConfig config = entityManager.GetComponentData<GameDataCollectionRuntimeConfig>(entity);
        GameDataCollectionSessionState sessionState = entityManager.GetComponentData<GameDataCollectionSessionState>(entity);
        DynamicBuffer<GameTelemetryEvent> events = entityManager.GetBuffer<GameTelemetryEvent>(entity);
        bool anyConsentGranted = noticeAcknowledged &&
                                 (programmingConsent || designConsent || art3DConsent);
        bool sessionStarted = sessionState.SessionId.Length > 0;

        sessionState.NoticeAcknowledged = noticeAcknowledged ? (byte)1 : (byte)0;
        sessionState.ProgrammingConsent = ResolveChoice(noticeAcknowledged, programmingConsent);
        sessionState.DesignConsent = ResolveChoice(noticeAcknowledged, designConsent);
        sessionState.Art3DConsent = ResolveChoice(noticeAcknowledged, art3DConsent);

        if (anyConsentGranted && !sessionStarted)
        {
            sessionState.SessionId = new FixedString64Bytes(Guid.NewGuid().ToString("D"));

            if (sessionState.DesignConsent == GameTelemetryConsentChoice.Granted)
            {
                GameTelemetryEventRuntimeUtility.TryEnqueue(events,
                                                            ref sessionState,
                                                            in config,
                                                            GameTelemetryEventType.GameSessionStarted,
                                                            GameTelemetryDepartment.Design,
                                                            config.Environment.ToString(),
                                                            GameTelemetryEventRuntimeUtility.GetUtcNowUnixMilliseconds());
            }
        }

        PurgeDisallowedEvents(events, in sessionState);
        entityManager.SetComponentData(entity, sessionState);
        return true;
    }
    #endregion

    #region Authentication
    /// <summary>
    /// Stores only a server-verified public user ID and role; passwords and bearer tokens remain outside ECS memory.
    /// </summary>
    /// <param name="userId">Public pseudonymous user identifier returned by the server.</param>
    /// <param name="role">Server-authoritative role returned by login.</param>
    /// <returns>True when the unique data-collection singleton was updated.</returns>
    public static bool TryApplyAuthenticatedUser(string userId, GameDataCollectionUserRole role)
    {
        if (!TryResolve(out EntityManager entityManager, out Entity entity))
            return false;

        GameDataCollectionSessionState sessionState = entityManager.GetComponentData<GameDataCollectionSessionState>(entity);
        sessionState.UserId = default;
        sessionState.UserId.CopyFromTruncated(userId ?? string.Empty);
        sessionState.Role = role;
        entityManager.SetComponentData(entity, sessionState);
        return true;
    }

    /// <summary>
    /// Clears identity, consent, session, and pending events while preserving only local developer-action visibility.
    /// </summary>
    /// <returns>True when the unique data-collection singleton was updated.</returns>
    public static bool TryClearAuthentication()
    {
        if (!TryResolve(out EntityManager entityManager, out Entity entity))
            return false;

        GameDataCollectionSessionState previousState =
            entityManager.GetComponentData<GameDataCollectionSessionState>(entity);
        entityManager.GetBuffer<GameTelemetryEvent>(entity).Clear();
        entityManager.SetComponentData(entity, new GameDataCollectionSessionState
        {
            DevActionsRevealed = previousState.DevActionsRevealed
        });
        return true;
    }

    /// <summary>
    /// Marks developer registration and login controls visible without granting a server role.
    /// </summary>
    /// <returns>True when the unique data-collection singleton was updated.</returns>
    public static bool TryRevealDeveloperActions()
    {
        if (!TryResolve(out EntityManager entityManager, out Entity entity))
            return false;

        GameDataCollectionSessionState sessionState = entityManager.GetComponentData<GameDataCollectionSessionState>(entity);
        sessionState.DevActionsRevealed = 1;
        entityManager.SetComponentData(entity, sessionState);
        return true;
    }
    #endregion

    #region State Resolution
    /// <summary>
    /// Reads current runtime config and session state for managed Settings UI presentation.
    /// </summary>
    /// <param name="config">Receives the baked collection config.</param>
    /// <param name="sessionState">Receives current consent, identity, and reveal state.</param>
    /// <returns>True when exactly one data-collection singleton exists.</returns>
    public static bool TryReadState(out GameDataCollectionRuntimeConfig config,
                                    out GameDataCollectionSessionState sessionState)
    {
        config = default;
        sessionState = default;

        if (!TryResolve(out EntityManager entityManager, out Entity entity))
            return false;

        config = entityManager.GetComponentData<GameDataCollectionRuntimeConfig>(entity);
        sessionState = entityManager.GetComponentData<GameDataCollectionSessionState>(entity);
        return true;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the unique runtime entity that owns collection config, session state, and pending events.
    /// </summary>
    /// <param name="entityManager">Receives the default-world entity manager.</param>
    /// <param name="entity">Receives the unique collection entity.</param>
    /// <returns>True when the default world and exactly one matching entity exist.</returns>
    private static bool TryResolve(out EntityManager entityManager, out Entity entity)
    {
        entityManager = default;
        entity = Entity.Null;
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GameDataCollectionRuntimeConfig>(),
            ComponentType.ReadWrite<GameDataCollectionSessionState>(),
            ComponentType.ReadWrite<GameTelemetryEvent>());
        int entityCount = query.CalculateEntityCount();

        if (entityCount == 1)
            entity = query.GetSingletonEntity();

        query.Dispose();
        return entityCount == 1;
    }

    /// <summary>
    /// Converts a modal choice to its explicit runtime state, preserving Unknown until the notice is acknowledged.
    /// </summary>
    /// <param name="noticeAcknowledged">True when the current notice was acknowledged.</param>
    /// <param name="granted">True when the category was explicitly selected.</param>
    /// <returns>Unknown, Declined, or Granted according to the explicit choice.</returns>
    private static GameTelemetryConsentChoice ResolveChoice(bool noticeAcknowledged, bool granted)
    {
        if (!noticeAcknowledged)
            return GameTelemetryConsentChoice.Unknown;

        return granted ? GameTelemetryConsentChoice.Granted : GameTelemetryConsentChoice.Declined;
    }

    /// <summary>
    /// Removes pending events whose department is no longer fully consented.
    /// </summary>
    /// <param name="events">Pending telemetry buffer to filter in place.</param>
    /// <param name="sessionState">Updated consent state.</param>
    private static void PurgeDisallowedEvents(DynamicBuffer<GameTelemetryEvent> events,
                                              in GameDataCollectionSessionState sessionState)
    {
        for (int eventIndex = events.Length - 1; eventIndex >= 0; eventIndex--)
        {
            GameTelemetryEvent telemetryEvent = events[eventIndex];

            if (!GameTelemetryEventRuntimeUtility.HasConsentForDepartment(in sessionState,
                                                                          telemetryEvent.Department))
                events.RemoveAt(eventIndex);
        }
    }
    #endregion

    #endregion
}
