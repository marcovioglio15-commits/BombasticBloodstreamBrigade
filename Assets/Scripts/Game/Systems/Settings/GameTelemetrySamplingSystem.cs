using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

/// <summary>
/// Accumulates frame cost every frame and emits low-frequency programming and 3D telemetry aggregates after consent.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct GameTelemetrySamplingSystem : ISystem
{
    #region Fields
    private EntityQuery enemyPoolQuery;
    private EntityQuery activeEnemyQuery;
    private EntityQuery projectilePoolQuery;
    private EntityQuery activeProjectileQuery;
    private EntityQuery renderEntityQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Builds reusable count queries and requires the data-collection singleton before updates begin.
    /// </summary>
    /// <param name="state">System state used to create queries and update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        enemyPoolQuery = state.GetEntityQuery(ComponentType.ReadOnly<EnemyData>());
        activeEnemyQuery = state.GetEntityQuery(ComponentType.ReadOnly<EnemyData>(),
                                                ComponentType.ReadOnly<EnemyActive>());
        projectilePoolQuery = state.GetEntityQuery(ComponentType.ReadOnly<Projectile>());
        activeProjectileQuery = state.GetEntityQuery(ComponentType.ReadOnly<Projectile>(),
                                                     ComponentType.ReadOnly<ProjectileActive>());
        renderEntityQuery = state.GetEntityQuery(ComponentType.ReadOnly<MaterialMeshInfo>());
        state.RequireForUpdate<GameDataCollectionRuntimeConfig>();
        state.RequireForUpdate<GameDataCollectionSessionState>();
        state.RequireForUpdate<GameTelemetrySamplingState>();
        state.RequireForUpdate<GameTelemetryEvent>();
    }

    /// <summary>
    /// Advances allocation-free accumulators and emits at most one sample per configured category interval.
    /// </summary>
    /// <param name="state">System state providing frame time and singleton access.</param>
    public void OnUpdate(ref SystemState state)
    {
        GameDataCollectionRuntimeConfig config = SystemAPI.GetSingleton<GameDataCollectionRuntimeConfig>();

        if (config.Enabled == 0)
            return;

#if UNITY_EDITOR
        if (config.CollectInEditor == 0)
            return;
#endif

        GameDataCollectionSessionState sessionState = SystemAPI.GetSingleton<GameDataCollectionSessionState>();
        GameTelemetrySamplingState samplingState = SystemAPI.GetSingleton<GameTelemetrySamplingState>();
        DynamicBuffer<GameTelemetryEvent> events = SystemAPI.GetSingletonBuffer<GameTelemetryEvent>();
        float deltaSeconds = math.max(0f, SystemAPI.Time.DeltaTime);
        TickProgramming(events, ref sessionState, ref samplingState, in config, deltaSeconds);
        TickArt3D(events, ref sessionState, ref samplingState, in config, deltaSeconds);
        SystemAPI.SetSingleton(sessionState);
        SystemAPI.SetSingleton(samplingState);
    }
    #endregion

    #region Sampling
    /// <summary>
    /// Accumulates frame timings and emits performance plus ECS pool counts at the configured interval.
    /// </summary>
    /// <param name="events">Pending telemetry buffer.</param>
    /// <param name="sessionState">Mutable consent and sequence state.</param>
    /// <param name="samplingState">Mutable frame accumulator.</param>
    /// <param name="config">Baked cadence and capacity settings.</param>
    /// <param name="deltaSeconds">Current frame duration.</param>
    private void TickProgramming(DynamicBuffer<GameTelemetryEvent> events,
                                 ref GameDataCollectionSessionState sessionState,
                                 ref GameTelemetrySamplingState samplingState,
                                 in GameDataCollectionRuntimeConfig config,
                                 float deltaSeconds)
    {
        if (!GameTelemetryEventRuntimeUtility.HasConsentForDepartment(in sessionState,
                                                                     GameTelemetryDepartment.Programming))
        {
            samplingState.PerformanceElapsedSeconds = 0d;
            samplingState.FrameDurationSumSeconds = 0d;
            samplingState.MaximumFrameDurationSeconds = 0f;
            samplingState.FrameCount = 0;
            return;
        }

        samplingState.PerformanceElapsedSeconds += deltaSeconds;
        samplingState.FrameDurationSumSeconds += deltaSeconds;
        samplingState.MaximumFrameDurationSeconds = math.max(samplingState.MaximumFrameDurationSeconds,
                                                             deltaSeconds);
        samplingState.FrameCount++;

        if (samplingState.PerformanceElapsedSeconds < config.PerformanceSampleIntervalSeconds)
            return;

        float averageFrameSeconds = samplingState.FrameCount > 0
            ? (float)(samplingState.FrameDurationSumSeconds / samplingState.FrameCount)
            : 0f;
        float averageFramesPerSecond = averageFrameSeconds > 0f ? 1f / averageFrameSeconds : 0f;
        long occurredAt = GameTelemetryEventRuntimeUtility.GetUtcNowUnixMilliseconds();
        int activeEnemies = activeEnemyQuery.CalculateEntityCount();
        int activeProjectiles = activeProjectileQuery.CalculateEntityCount();
        GameTelemetryEventRuntimeUtility.TryEnqueue(events,
                                                    ref sessionState,
                                                    in config,
                                                    GameTelemetryEventType.PerformanceSample,
                                                    GameTelemetryDepartment.Programming,
                                                    Application.version,
                                                    occurredAt,
                                                    averageFrameSeconds * 1000f,
                                                    samplingState.MaximumFrameDurationSeconds * 1000f,
                                                    averageFramesPerSecond,
                                                    (float)samplingState.PerformanceElapsedSeconds,
                                                    activeEnemies,
                                                    activeProjectiles);
        GameTelemetryEventRuntimeUtility.TryEnqueue(events,
                                                    ref sessionState,
                                                    in config,
                                                    GameTelemetryEventType.EntityLoadSample,
                                                    GameTelemetryDepartment.Programming,
                                                    Application.version,
                                                    occurredAt,
                                                    count0: enemyPoolQuery.CalculateEntityCount(),
                                                    count1: activeEnemies,
                                                    count2: projectilePoolQuery.CalculateEntityCount(),
                                                    count3: activeProjectiles);
        samplingState.PerformanceElapsedSeconds = 0d;
        samplingState.FrameDurationSumSeconds = 0d;
        samplingState.MaximumFrameDurationSeconds = 0f;
        samplingState.FrameCount = 0;
    }

    /// <summary>
    /// Emits a low-frequency ECS rendering-load sample for the 3D dashboard.
    /// </summary>
    /// <param name="events">Pending telemetry buffer.</param>
    /// <param name="sessionState">Mutable consent and sequence state.</param>
    /// <param name="samplingState">Mutable rendering cadence state.</param>
    /// <param name="config">Baked cadence and capacity settings.</param>
    /// <param name="deltaSeconds">Current frame duration.</param>
    private void TickArt3D(DynamicBuffer<GameTelemetryEvent> events,
                           ref GameDataCollectionSessionState sessionState,
                           ref GameTelemetrySamplingState samplingState,
                           in GameDataCollectionRuntimeConfig config,
                           float deltaSeconds)
    {
        if (!GameTelemetryEventRuntimeUtility.HasConsentForDepartment(in sessionState,
                                                                     GameTelemetryDepartment.Art3D))
        {
            samplingState.RenderingElapsedSeconds = 0d;
            return;
        }

        samplingState.RenderingElapsedSeconds += deltaSeconds;

        if (samplingState.RenderingElapsedSeconds < config.RenderingSampleIntervalSeconds)
            return;

        GameTelemetryEventRuntimeUtility.TryEnqueue(events,
                                                    ref sessionState,
                                                    in config,
                                                    GameTelemetryEventType.RenderingLoadSample,
                                                    GameTelemetryDepartment.Art3D,
                                                    Application.version,
                                                    GameTelemetryEventRuntimeUtility.GetUtcNowUnixMilliseconds(),
                                                    metric0: (float)samplingState.RenderingElapsedSeconds,
                                                    count0: renderEntityQuery.CalculateEntityCount(),
                                                    count1: activeEnemyQuery.CalculateEntityCount(),
                                                    count2: activeProjectileQuery.CalculateEntityCount());
        samplingState.RenderingElapsedSeconds = 0d;
    }
    #endregion

    #endregion
}
