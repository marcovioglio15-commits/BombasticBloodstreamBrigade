using System;
using UnityEngine;

/// <summary>
/// Identifies the deployment environment included in telemetry batches and developer queries.
/// </summary>
public enum GameDataCollectionEnvironment
{
    Development = 0,
    Staging = 1,
    Production = 2
}

/// <summary>
/// Stores consent, batching, retry, Input Action, and HTTPS endpoint settings for automatic data collection.
/// </summary>
[Serializable]
public sealed class GameDataCollectionSettings
{
    #region Constants
    public const string DefaultServiceBaseUrl = "https://YOUR_ACCOUNT.alwaysdata.net/api/v1";
    public const string DefaultSchemaVersion = "1.0.0";
    public const string DefaultConsentPolicyVersion = "1.0";
    public const string DefaultRevealDevActionsActionName = "UI/RevealDevActions";
    public const float DefaultPerformanceSampleIntervalSeconds = 1f;
    public const float DefaultRenderingSampleIntervalSeconds = 2f;
    public const float DefaultUploadIntervalSeconds = 15f;
    public const int DefaultMaximumEventsPerBatch = 50;
    public const int DefaultMaximumPendingEvents = 1000;
    public const int DefaultMaximumPayloadBytes = 131072;
    public const float DefaultRequestTimeoutSeconds = 10f;
    public const float DefaultInitialRetryDelaySeconds = 2f;
    public const float DefaultMaximumRetryDelaySeconds = 60f;
    public const int DefaultPendingEventRetentionDays = 7;
    public const int DefaultDashboardPageSize = 20;
    #endregion

    #region Fields

    #region Serialized Fields - Runtime Context
    [Header("Runtime Context")]
    [Tooltip("Allows telemetry uploads from the Unity Editor after consent is granted. Keep disabled for ordinary content iteration.")]
    [SerializeField] private bool collectInEditor;

    [Tooltip("Deployment label included in every telemetry batch so development, staging, and production data remain separate.")]
    [SerializeField] private GameDataCollectionEnvironment environment = GameDataCollectionEnvironment.Development;
    #endregion

    #region Serialized Fields - Service
    [Header("HTTPS Service")]
    [Tooltip("HTTPS root of the versioned alwaysdata PHP API. Endpoint paths are appended by the runtime client.")]
    [SerializeField] private string serviceBaseUrl = DefaultServiceBaseUrl;

    [Tooltip("Maximum unscaled seconds allowed for one authentication, telemetry, or developer query request.")]
    [SerializeField] private float requestTimeoutSeconds = DefaultRequestTimeoutSeconds;

    [Tooltip("Maximum UTF-8 request body size accepted by the Unity client before a batch is split.")]
    [SerializeField] private int maximumPayloadBytes = DefaultMaximumPayloadBytes;
    #endregion

    #region Serialized Fields - Contracts
    [Header("Contracts")]
    [Tooltip("Version of the telemetry event contract sent with every event and checked by the server.")]
    [SerializeField] private string schemaVersion = DefaultSchemaVersion;

    [Tooltip("Version of the data-collection notice stored with every consent decision.")]
    [SerializeField] private string consentPolicyVersion = DefaultConsentPolicyVersion;
    #endregion

    #region Serialized Fields - Developer Access
    [Header("Developer Access")]
    [Tooltip("Stable Input Action ID, name, or path that reveals developer registration and login controls in the Settings Dev tab.")]
    [SerializeField] private string revealDevActionsActionId = DefaultRevealDevActionsActionName;

    [Tooltip("Maximum number of aggregate rows requested per developer-dashboard page. Runtime also caps this value to the pre-authored row capacity.")]
    [SerializeField] private int dashboardPageSize = DefaultDashboardPageSize;
    #endregion

    #region Serialized Fields - Sampling
    [Header("Sampling")]
    [Tooltip("Unscaled seconds between programming-oriented frame and ECS load samples. Frame accumulation remains allocation-free between samples.")]
    [SerializeField] private float performanceSampleIntervalSeconds = DefaultPerformanceSampleIntervalSeconds;

    [Tooltip("Unscaled seconds between 3D-oriented renderer, LOD, and visual-load samples.")]
    [SerializeField] private float renderingSampleIntervalSeconds = DefaultRenderingSampleIntervalSeconds;
    #endregion

    #region Serialized Fields - Batching
    [Header("Batching")]
    [Tooltip("Unscaled seconds between automatic upload attempts while consented events are pending.")]
    [SerializeField] private float uploadIntervalSeconds = DefaultUploadIntervalSeconds;

    [Tooltip("Maximum number of telemetry events serialized into one HTTPS request.")]
    [SerializeField] private int maximumEventsPerBatch = DefaultMaximumEventsPerBatch;

    [Tooltip("Maximum number of consented events retained locally before the oldest event is discarded.")]
    [SerializeField] private int maximumPendingEvents = DefaultMaximumPendingEvents;
    #endregion

    #region Serialized Fields - Retry and Offline Queue
    [Header("Retry and Offline Queue")]
    [Tooltip("Persists pseudonymous pending telemetry between launches so temporary connection failures do not lose a complete run.")]
    [SerializeField] private bool persistPendingEvents = true;

    [Tooltip("Maximum age in days for persisted pending telemetry before it is discarded locally.")]
    [SerializeField] private int pendingEventRetentionDays = DefaultPendingEventRetentionDays;

    [Tooltip("Initial unscaled delay before retrying a failed telemetry upload.")]
    [SerializeField] private float initialRetryDelaySeconds = DefaultInitialRetryDelaySeconds;

    [Tooltip("Maximum unscaled delay reached by exponential telemetry upload retries.")]
    [SerializeField] private float maximumRetryDelaySeconds = DefaultMaximumRetryDelaySeconds;
    #endregion

    #endregion

    #region Properties
    public bool CollectInEditor => collectInEditor;
    public GameDataCollectionEnvironment Environment => environment;
    public string ServiceBaseUrl => serviceBaseUrl;
    public float RequestTimeoutSeconds => requestTimeoutSeconds;
    public int MaximumPayloadBytes => maximumPayloadBytes;
    public string SchemaVersion => schemaVersion;
    public string ConsentPolicyVersion => consentPolicyVersion;
    public string RevealDevActionsActionId => revealDevActionsActionId;
    public int DashboardPageSize => dashboardPageSize;
    public float PerformanceSampleIntervalSeconds => performanceSampleIntervalSeconds;
    public float RenderingSampleIntervalSeconds => renderingSampleIntervalSeconds;
    public float UploadIntervalSeconds => uploadIntervalSeconds;
    public int MaximumEventsPerBatch => maximumEventsPerBatch;
    public int MaximumPendingEvents => maximumPendingEvents;
    public bool PersistPendingEvents => persistPendingEvents;
    public int PendingEventRetentionDays => pendingEventRetentionDays;
    public float InitialRetryDelaySeconds => initialRetryDelaySeconds;
    public float MaximumRetryDelaySeconds => maximumRetryDelaySeconds;
    #endregion
}
