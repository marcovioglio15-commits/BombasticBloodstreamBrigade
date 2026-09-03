using System;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Identifies the developer dashboard that owns a telemetry event or aggregate.
/// </summary>
[Flags]
public enum GameTelemetryDepartment : byte
{
    None = 0,
    Programming = 1,
    Design = 2,
    Art3D = 4
}

/// <summary>
/// Identifies stable telemetry contracts shared by ECS producers, PHP validation, and developer dashboards.
/// </summary>
public enum GameTelemetryEventType : ushort
{
    None = 0,
    GameSessionStarted = 1,
    PerformanceSample = 10,
    EntityLoadSample = 11,
    RenderingLoadSample = 20,
    RunStarted = 30,
    RunEnded = 31,
    RoomEntered = 32,
    RoomCleared = 33,
    PlayerDefeated = 36
}

/// <summary>
/// Represents one explicit user choice for a telemetry category.
/// </summary>
public enum GameTelemetryConsentChoice : byte
{
    Unknown = 0,
    Declined = 1,
    Granted = 2
}

/// <summary>
/// Represents the server-authoritative role exposed to the Settings Dev section.
/// </summary>
public enum GameDataCollectionUserRole : byte
{
    None = 0,
    User = 1,
    Developer = 2
}

/// <summary>
/// Stores safe data-collection settings baked from the active Settings Manager preset.
/// </summary>
public struct GameDataCollectionRuntimeConfig : IComponentData
{
    #region Fields
    public byte Enabled;
    public byte CollectInEditor;
    public GameDataCollectionEnvironment Environment;
    public FixedString512Bytes ServiceBaseUrl;
    public FixedString64Bytes SchemaVersion;
    public FixedString64Bytes ConsentPolicyVersion;
    public FixedString64Bytes RevealDevActionsActionId;
    public float PerformanceSampleIntervalSeconds;
    public float RenderingSampleIntervalSeconds;
    public float UploadIntervalSeconds;
    public int MaximumEventsPerBatch;
    public int MaximumPendingEvents;
    public int MaximumPayloadBytes;
    public float RequestTimeoutSeconds;
    public float InitialRetryDelaySeconds;
    public float MaximumRetryDelaySeconds;
    public byte PersistPendingEvents;
    public int PendingEventRetentionDays;
    public int DashboardPageSize;
    #endregion
}

/// <summary>
/// Stores mutable consent and authenticated-role state without placing credentials in the ECS world.
/// </summary>
public struct GameDataCollectionSessionState : IComponentData
{
    #region Fields
    public FixedString64Bytes SessionId;
    public FixedString64Bytes UserId;
    public ulong NextSequence;
    public GameTelemetryConsentChoice ProgrammingConsent;
    public GameTelemetryConsentChoice DesignConsent;
    public GameTelemetryConsentChoice Art3DConsent;
    public GameDataCollectionUserRole Role;
    public byte NoticeAcknowledged;
    public byte DevActionsRevealed;
    #endregion
}

/// <summary>
/// Stores allocation-free frame accumulation between configurable telemetry sample boundaries.
/// </summary>
public struct GameTelemetrySamplingState : IComponentData
{
    #region Fields
    public double PerformanceElapsedSeconds;
    public double RenderingElapsedSeconds;
    public double FrameDurationSumSeconds;
    public float MaximumFrameDurationSeconds;
    public uint FrameCount;
    #endregion
}

/// <summary>
/// Tracks authoritative run and room versions so event observation remains idempotent across frames.
/// </summary>
public struct GameTelemetryProgressionObservationState : IComponentData
{
    #region Fields
    public uint ObservedRunSeed;
    public uint ObservedGenerationVersion;
    public uint ObservedVisitOrdinal;
    public uint ObservedClearVersion;
    public PlayerRunOutcome ObservedRunOutcome;
    public byte RunStarted;
    #endregion
}

/// <summary>
/// Stores one compact, schema-versioned telemetry record before managed JSON serialization.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameTelemetryEvent : IBufferElementData
{
    #region Fields
    public ulong Sequence;
    public long OccurredAtUnixMilliseconds;
    public GameTelemetryEventType EventType;
    public GameTelemetryDepartment Department;
    public FixedString128Bytes ContextId;
    public float Metric0;
    public float Metric1;
    public float Metric2;
    public float Metric3;
    public int Count0;
    public int Count1;
    public int Count2;
    public int Count3;
    #endregion
}
