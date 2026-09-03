using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Persists a bounded consent-filtered telemetry queue for temporary connection failures.
/// </summary>
internal static class GameTelemetryOfflineStore
{
    #region Constants
    private const string FileName = "pending-telemetry.json";
    #endregion

    #region Types
    [Serializable]
    private sealed class OfflineFile
    {
        [Tooltip("Public user identifier that owns the persisted queue.")]
        [SerializeField] private string userId;

        [Tooltip("Stable game-session UUID restored with the queue.")]
        [SerializeField] private string gameSessionId;

        [Tooltip("UTC save time used to enforce local retention.")]
        [SerializeField] private long savedAtUnixMilliseconds;

        [Tooltip("Bounded consent-filtered events stored for a later retry.")]
        [SerializeField] private OfflineEvent[] events;

        public string UserId => userId;
        public string GameSessionId => gameSessionId;
        public long SavedAtUnixMilliseconds => savedAtUnixMilliseconds;
        public OfflineEvent[] Events => events;

        /// <summary>
        /// Creates one file snapshot tied to the authenticated public user identifier.
        /// </summary>
        /// <param name="userIdValue">Server-issued public user identifier.</param>
        /// <param name="gameSessionIdValue">Client session identifier retained across an offline restart.</param>
        /// <param name="eventsValue">Bounded event array to persist.</param>
        public OfflineFile(string userIdValue, string gameSessionIdValue, OfflineEvent[] eventsValue)
        {
            userId = userIdValue;
            gameSessionId = gameSessionIdValue;
            savedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            events = eventsValue;
        }
    }

    [Serializable]
    private sealed class OfflineEvent
    {
        [Tooltip("Monotonic event sequence retained across an offline restart.")]
        [SerializeField] private ulong sequence;

        [Tooltip("UTC event time retained as Unix milliseconds.")]
        [SerializeField] private long occurredAtUnixMilliseconds;

        [Tooltip("Stable event contract identifier retained offline.")]
        [SerializeField] private GameTelemetryEventType eventType;

        [Tooltip("Consent category owning the retained event.")]
        [SerializeField] private GameTelemetryDepartment department;

        [Tooltip("Short event context retained offline.")]
        [SerializeField] private string contextId;

        [Tooltip("First floating-point event slot retained offline.")]
        [SerializeField] private float metric0;

        [Tooltip("Second floating-point event slot retained offline.")]
        [SerializeField] private float metric1;

        [Tooltip("Third floating-point event slot retained offline.")]
        [SerializeField] private float metric2;

        [Tooltip("Fourth floating-point event slot retained offline.")]
        [SerializeField] private float metric3;

        [Tooltip("First integer event slot retained offline.")]
        [SerializeField] private int count0;

        [Tooltip("Second integer event slot retained offline.")]
        [SerializeField] private int count1;

        [Tooltip("Third integer event slot retained offline.")]
        [SerializeField] private int count2;

        [Tooltip("Fourth integer event slot retained offline.")]
        [SerializeField] private int count3;

        /// <summary>
        /// Creates one serializable copy of a compact ECS telemetry event.
        /// </summary>
        /// <param name="source">ECS event to copy.</param>
        public OfflineEvent(in GameTelemetryEvent source)
        {
            sequence = source.Sequence;
            occurredAtUnixMilliseconds = source.OccurredAtUnixMilliseconds;
            eventType = source.EventType;
            department = source.Department;
            contextId = source.ContextId.ToString();
            metric0 = source.Metric0;
            metric1 = source.Metric1;
            metric2 = source.Metric2;
            metric3 = source.Metric3;
            count0 = source.Count0;
            count1 = source.Count1;
            count2 = source.Count2;
            count3 = source.Count3;
        }

        /// <summary>
        /// Rebuilds the compact ECS value after retention and user checks pass.
        /// </summary>
        /// <returns>Restored telemetry event.</returns>
        public GameTelemetryEvent ToEvent()
        {
            return new GameTelemetryEvent
            {
                Sequence = sequence,
                OccurredAtUnixMilliseconds = occurredAtUnixMilliseconds,
                EventType = eventType,
                Department = department,
                ContextId = new FixedString128Bytes(contextId ?? string.Empty),
                Metric0 = metric0,
                Metric1 = metric1,
                Metric2 = metric2,
                Metric3 = metric3,
                Count0 = count0,
                Count1 = count1,
                Count2 = count2,
                Count3 = count3
            };
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Saves the current queue only when persistence and an authenticated user are available.
    /// </summary>
    /// <param name="entityManager">Entity manager that owns the telemetry singleton.</param>
    /// <param name="entity">Telemetry singleton entity.</param>
    /// <param name="config">Baked persistence policy.</param>
    /// <param name="sessionState">Current authenticated session state.</param>
    public static void Save(EntityManager entityManager,
                            Entity entity,
                            in GameDataCollectionRuntimeConfig config,
                            in GameDataCollectionSessionState sessionState)
    {
        if (config.PersistPendingEvents == 0 || sessionState.UserId.IsEmpty)
            return;

        DynamicBuffer<GameTelemetryEvent> events = entityManager.GetBuffer<GameTelemetryEvent>(entity);

        if (events.IsEmpty)
        {
            DeleteFile();
            return;
        }

        OfflineEvent[] copies = new OfflineEvent[events.Length];

        for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
            copies[eventIndex] = new OfflineEvent(in events.ElementAt(eventIndex));

        WriteFile(new OfflineFile(sessionState.UserId.ToString(), sessionState.SessionId.ToString(), copies));
    }

    /// <summary>
    /// Restores one unexpired queue only for the same authenticated user.
    /// </summary>
    /// <param name="entityManager">Entity manager that owns the telemetry singleton.</param>
    /// <param name="entity">Telemetry singleton entity.</param>
    /// <param name="config">Baked persistence and retention policy.</param>
    /// <param name="sessionState">Current authenticated session state.</param>
    public static void Restore(EntityManager entityManager,
                               Entity entity,
                               in GameDataCollectionRuntimeConfig config,
                               ref GameDataCollectionSessionState sessionState)
    {
        if (config.PersistPendingEvents == 0 || sessionState.UserId.IsEmpty)
            return;

        OfflineFile file = ReadFile();

        if (!CanRestore(file, sessionState.UserId.ToString(), config.PendingEventRetentionDays))
            return;

        DynamicBuffer<GameTelemetryEvent> events = entityManager.GetBuffer<GameTelemetryEvent>(entity);
        OfflineEvent[] storedEvents = file.Events;
        int maximumEvents = Mathf.Max(1, config.MaximumPendingEvents);
        events.Clear();
        sessionState.SessionId = new FixedString64Bytes(file.GameSessionId ?? string.Empty);

        for (int eventIndex = 0; eventIndex < storedEvents.Length && events.Length < maximumEvents; eventIndex++)
        {
            GameTelemetryEvent telemetryEvent = storedEvents[eventIndex].ToEvent();

            if (!GameTelemetryEventRuntimeUtility.HasConsentForDepartment(in sessionState, telemetryEvent.Department))
                continue;

            events.Add(telemetryEvent);
            sessionState.NextSequence = Math.Max(sessionState.NextSequence, telemetryEvent.Sequence + 1);
        }

        entityManager.SetComponentData(entity, sessionState);
        DeleteFile();
    }

    /// <summary>
    /// Removes the local queue snapshot after consent revocation or successful restoration.
    /// </summary>
    public static void DeleteFile()
    {
        try
        {
            string path = ResolvePath();

            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[GameTelemetryOfflineStore] Could not remove the pending queue: " + exception.Message);
        }
    }
    #endregion

    #region File Operations
    /// <summary>
    /// Writes one complete snapshot through a temporary file to avoid partial JSON.
    /// </summary>
    /// <param name="file">Snapshot to persist.</param>
    private static void WriteFile(OfflineFile file)
    {
        try
        {
            string path = ResolvePath();
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(file));

            if (File.Exists(path))
                File.Delete(path);

            File.Move(temporaryPath, path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[GameTelemetryOfflineStore] Could not persist the pending queue: " + exception.Message);
        }
    }

    /// <summary>
    /// Reads the pending file without allowing malformed data to interrupt gameplay.
    /// </summary>
    /// <returns>Parsed file or null when missing or invalid.</returns>
    private static OfflineFile ReadFile()
    {
        try
        {
            string path = ResolvePath();
            return File.Exists(path) ? JsonUtility.FromJson<OfflineFile>(File.ReadAllText(path)) : null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[GameTelemetryOfflineStore] Could not read the pending queue: " + exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Validates user ownership, age and content before restoration.
    /// </summary>
    /// <param name="file">Parsed offline file.</param>
    /// <param name="userId">Currently authenticated public user identifier.</param>
    /// <param name="retentionDays">Configured maximum file age.</param>
    /// <returns>True when the stored events may be restored.</returns>
    private static bool CanRestore(OfflineFile file, string userId, int retentionDays)
    {
        if (file == null || file.Events == null ||
            !string.Equals(file.UserId, userId, StringComparison.Ordinal) ||
            !Guid.TryParse(file.GameSessionId, out Guid gameSessionId) || gameSessionId == Guid.Empty)
            return false;

        long maximumAgeMilliseconds = Math.Max(1, retentionDays) * 86400000L;
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - file.SavedAtUnixMilliseconds <= maximumAgeMilliseconds;
    }

    /// <summary>
    /// Resolves the application-owned pending queue path.
    /// </summary>
    /// <returns>Absolute JSON file path.</returns>
    private static string ResolvePath()
    {
        return Path.Combine(Application.persistentDataPath, FileName);
    }
    #endregion

    #endregion
}
