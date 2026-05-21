using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Emits compact acid trail buffer segments for active Acid Wanderer enemies while they move.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyPatternMovementSystem))]
[UpdateBefore(typeof(EnemyProjectileHitSystem))]
public partial struct EnemyAcidTrailSpawnSystem : ISystem
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumSegmentLifetimeSeconds = 0.05f;
    private const float MinimumSpawnIntervalSeconds = 0.01f;
    #endregion

    #region Fields
    private EntityQuery acidTrailQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the query used to skip the system when no active enemy can own acid trail buffers.
    /// </summary>
    /// <param name="state">System state used to register update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        acidTrailQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyPatternConfig, EnemyPatternRuntimeState, EnemyRuntimeState, LocalTransform, EnemyAcidTrailSegmentElement, EnemyActive>()
            .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
            .Build();

        state.RequireForUpdate(acidTrailQuery);
    }

    /// <summary>
    /// Schedules the Burst trail emission job using the enemy time scale so pause/slow effects affect the hazard lifetime.
    /// </summary>
    /// <param name="state">System state providing elapsed time and dependency tracking.</param>
    public void OnUpdate(ref SystemState state)
    {
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;

        if (deltaTime <= 0f)
            return;

        EnemyAcidTrailSpawnJob spawnJob = new EnemyAcidTrailSpawnJob
        {
            DeltaTime = deltaTime
        };

        state.Dependency = spawnJob.Schedule(state.Dependency);
    }
    #endregion

    #region Jobs
    [BurstCompile]
    [WithAll(typeof(EnemyActive))]
    [WithNone(typeof(EnemyDespawnRequest), typeof(EnemySpawnInactivityLock))]
    private partial struct EnemyAcidTrailSpawnJob : IJobEntity
    {
        public float DeltaTime;

        /// <summary>
        /// Ages existing segments and appends a new segment when the owner moved far enough or the interval elapsed.
        /// </summary>
        /// <param name="patternRuntimeState">Mutable pattern state storing acid spawn timers and last emission point.</param>
        /// <param name="segments">Per-enemy compact buffer of active acid trail segments.</param>
        /// <param name="patternConfig">Compiled pattern config holding acid trail tuning.</param>
        /// <param name="runtimeState">Enemy runtime state used to determine whether it is moving enough to emit acid.</param>
        /// <param name="enemyTransform">Enemy world transform used as the segment emission position.</param>
        private void Execute(ref EnemyPatternRuntimeState patternRuntimeState,
                             DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                             in EnemyPatternConfig patternConfig,
                             in EnemyRuntimeState runtimeState,
                             in LocalTransform enemyTransform)
        {
            CompactSegments(segments, DeltaTime);

            if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererAcid ||
                patternConfig.AcidTrailEnabled == 0)
            {
                ResetState(ref patternRuntimeState, segments);
                return;
            }

            bool hasValidPayload = patternConfig.AcidTrailRadius > 0f &&
                                   patternConfig.AcidTrailDamagePerTick > 0f &&
                                   patternConfig.AcidTrailSegmentLifetimeSeconds > 0f &&
                                   patternConfig.AcidTrailMaxActiveSegments > 0;

            if (!hasValidPayload)
            {
                ResetState(ref patternRuntimeState, segments);
                return;
            }

            float3 enemyPosition = enemyTransform.Position;

            if (patternRuntimeState.AcidInitialized == 0)
            {
                patternRuntimeState.AcidInitialized = 1;
                patternRuntimeState.AcidLastSpawnPosition = enemyPosition;
                patternRuntimeState.AcidSpawnTimer = 0f;
            }

            float3 planarVelocity = runtimeState.Velocity;
            planarVelocity.y = 0f;
            float minimumMovementSpeed = math.max(0f, patternConfig.AcidTrailMinimumMovementSpeed);

            if (math.lengthsq(planarVelocity) < minimumMovementSpeed * minimumMovementSpeed)
            {
                patternRuntimeState.AcidLastSpawnPosition = enemyPosition;
                patternRuntimeState.AcidSpawnTimer = 0f;
                return;
            }

            float nextSpawnTimer = patternRuntimeState.AcidSpawnTimer - DeltaTime;
            float3 spawnDelta = enemyPosition - patternRuntimeState.AcidLastSpawnPosition;
            spawnDelta.y = 0f;
            float movedDistance = math.length(spawnDelta);
            float spawnDistance = math.max(0f, patternConfig.AcidTrailSpawnDistance);
            bool distanceTriggered = spawnDistance > DirectionEpsilon && movedDistance >= spawnDistance;
            bool timerTriggered = nextSpawnTimer <= 0f;

            if (!distanceTriggered && !timerTriggered)
            {
                patternRuntimeState.AcidSpawnTimer = nextSpawnTimer;
                return;
            }

            AddSegment(segments,
                       patternRuntimeState.AcidLastSpawnPosition,
                       enemyPosition,
                       in patternConfig);
            patternRuntimeState.AcidLastSpawnPosition = enemyPosition;
            patternRuntimeState.AcidSpawnTimer = math.max(MinimumSpawnIntervalSeconds, patternConfig.AcidTrailSpawnIntervalSeconds);
        }

        /// <summary>
        /// Removes expired segments in place while aging active entries by the current simulation delta.
        /// </summary>
        /// <param name="segments">Per-enemy acid segment buffer to compact.</param>
        /// <param name="deltaTime">Scaled enemy delta time applied to remaining lifetimes.</param>
        private static void CompactSegments(DynamicBuffer<EnemyAcidTrailSegmentElement> segments, float deltaTime)
        {
            for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                EnemyAcidTrailSegmentElement segment = segments[segmentIndex];
                segment.RemainingLifetime -= deltaTime;

                if (segment.RemainingLifetime <= 0f)
                {
                    segments.RemoveAt(segmentIndex);
                    segmentIndex--;
                    continue;
                }

                segments[segmentIndex] = segment;
            }
        }

        /// <summary>
        /// Clears active acid data when the compiled pattern no longer represents a valid Acid Wanderer.
        /// </summary>
        /// <param name="patternRuntimeState">Mutable pattern state reset for future activation.</param>
        /// <param name="segments">Per-enemy acid segment buffer cleared to avoid stale hazards.</param>
        private static void ResetState(ref EnemyPatternRuntimeState patternRuntimeState,
                                       DynamicBuffer<EnemyAcidTrailSegmentElement> segments)
        {
            segments.Clear();
            patternRuntimeState.AcidInitialized = 0;
            patternRuntimeState.AcidSpawnTimer = 0f;
            patternRuntimeState.AcidPlayerDamageCooldown = 0f;
            patternRuntimeState.AcidPlayerOverlapping = 0;
        }

        /// <summary>
        /// Appends one gameplay segment after enforcing the configured per-enemy segment cap.
        /// </summary>
        /// <param name="segments">Per-enemy acid segment buffer receiving the new segment.</param>
        /// <param name="startPosition">World-space position of the previous acid emission point.</param>
        /// <param name="endPosition">World-space position of the current acid emission point.</param>
        /// <param name="patternConfig">Compiled acid settings copied into the segment payload.</param>
        private static void AddSegment(DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                                       float3 startPosition,
                                       float3 endPosition,
                                       in EnemyPatternConfig patternConfig)
        {
            int maxActiveSegments = math.max(1, patternConfig.AcidTrailMaxActiveSegments);

            while (segments.Length >= maxActiveSegments)
                segments.RemoveAt(0);

            segments.Add(new EnemyAcidTrailSegmentElement
            {
                StartPosition = startPosition,
                EndPosition = endPosition,
                Radius = math.max(0f, patternConfig.AcidTrailRadius),
                RemainingLifetime = math.max(MinimumSegmentLifetimeSeconds, patternConfig.AcidTrailSegmentLifetimeSeconds),
                ApplyIntervalSeconds = math.max(MinimumSpawnIntervalSeconds, patternConfig.AcidTrailApplyIntervalSeconds),
                DamagePerTick = math.max(0f, patternConfig.AcidTrailDamagePerTick),
                VfxSpawned = 0
            });
        }
    }
    #endregion

    #endregion
}
