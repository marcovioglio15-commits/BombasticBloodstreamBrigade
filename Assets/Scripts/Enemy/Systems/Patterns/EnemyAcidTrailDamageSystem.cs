using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies periodic player damage from active Acid Wanderer trail segments.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyContactDamageSystem))]
[UpdateAfter(typeof(EnemyAcidTrailSpawnSystem))]
[UpdateBefore(typeof(EnemyDespawnSystem))]
public partial struct EnemyAcidTrailDamageSystem : ISystem
{
    #region Constants
    private const float MinimumApplyIntervalSeconds = 0.01f;
    #endregion

    #region Fields
    private EntityQuery playerQuery;
    private EntityQuery acidTrailQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates player and acid-trail queries used to skip unnecessary work when either side is absent.
    /// </summary>
    /// <param name="state">System state used to register update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform, PlayerHealth, PlayerShield, PlayerRuntimeHealthStatisticsConfig, PlayerDamageGraceState>()
            .Build();
        acidTrailQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyPatternConfig, EnemyAcidTrailSegmentElement, EnemyActive>()
            .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
            .Build();

        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate(acidTrailQuery);
    }

    /// <summary>
    /// Resolves the current player damage gate, runs the Burst trail overlap pass, then applies the accumulated damage once.
    /// </summary>
    /// <param name="state">System state providing ECS access, time, and dependency tracking.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        ComponentLookup<PlayerDashState> dashStateLookup = SystemAPI.GetComponentLookup<PlayerDashState>(true);
        Entity playerEntity = Entity.Null;
        LocalTransform playerTransform = default;
        PlayerHealth playerHealth = default;
        PlayerShield playerShield = default;
        PlayerRuntimeHealthStatisticsConfig runtimeHealthConfig = default;
        PlayerDamageGraceState playerDamageGraceState = default;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((RefRO<LocalTransform> candidatePlayerTransform,
                  RefRO<PlayerHealth> candidatePlayerHealth,
                  RefRO<PlayerShield> candidatePlayerShield,
                  RefRO<PlayerRuntimeHealthStatisticsConfig> candidateRuntimeHealthConfig,
                  RefRO<PlayerDamageGraceState> candidatePlayerDamageGraceState,
                  Entity candidatePlayerEntity) in SystemAPI.Query<RefRO<LocalTransform>,
                                                                   RefRO<PlayerHealth>,
                                                                   RefRO<PlayerShield>,
                                                                   RefRO<PlayerRuntimeHealthStatisticsConfig>,
                                                                   RefRO<PlayerDamageGraceState>>()
                                                            .WithAll<PlayerControllerConfig>()
                                                            .WithEntityAccess())
        {
            playerEntity = candidatePlayerEntity;
            playerTransform = candidatePlayerTransform.ValueRO;
            playerHealth = candidatePlayerHealth.ValueRO;
            playerShield = candidatePlayerShield.ValueRO;
            runtimeHealthConfig = candidateRuntimeHealthConfig.ValueRO;
            playerDamageGraceState = candidatePlayerDamageGraceState.ValueRO;
            break;
        }

        if (playerEntity == Entity.Null)
            return;

        if (!entityManager.Exists(playerEntity))
            return;

        if (dashStateLookup.HasComponent(playerEntity))
        {
            PlayerDashState dashState = dashStateLookup[playerEntity];

            if (dashState.RemainingInvulnerability > 0f)
                return;
        }

        if (PlayerDamageUtility.IsDamageGraceActive(in playerDamageGraceState, elapsedTime))
            return;

        if (playerHealth.Current <= 0f)
            return;

        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;

        if (deltaTime <= 0f)
            return;

        NativeReference<float> accumulatedDamage = new NativeReference<float>(Allocator.TempJob);
        accumulatedDamage.Value = 0f;

        EnemyAcidTrailDamageJob damageJob = new EnemyAcidTrailDamageJob
        {
            PlayerPosition = playerTransform.Position,
            DeltaTime = deltaTime,
            AccumulatedDamage = accumulatedDamage
        };
        JobHandle damageHandle = damageJob.Schedule(state.Dependency);
        damageHandle.Complete();
        state.Dependency = damageHandle;

        float totalDamage = math.max(0f, accumulatedDamage.Value);
        accumulatedDamage.Dispose();

        if (totalDamage <= 0f)
            return;

        ApplyDamage(entityManager,
                    playerEntity,
                    playerTransform.Position,
                    ref playerHealth,
                    ref playerShield,
                    ref playerDamageGraceState,
                    in runtimeHealthConfig,
                    elapsedTime,
                    totalDamage,
                    audioRequests,
                    canEnqueueAudioRequests);
    }
    #endregion

    #region Damage Application
    /// <summary>
    /// Applies one merged flat damage tick to the player and emits the same feedback used by other enemy damage channels.
    /// </summary>
    /// <param name="entityManager">Runtime entity manager used to write player components and trigger feedback.</param>
    /// <param name="playerEntity">Player entity receiving the accumulated damage.</param>
    /// <param name="playerPosition">World-space player position used for positional audio requests.</param>
    /// <param name="playerHealth">Mutable player health snapshot.</param>
    /// <param name="playerShield">Mutable player shield snapshot.</param>
    /// <param name="playerDamageGraceState">Mutable damage grace snapshot.</param>
    /// <param name="runtimeHealthConfig">Runtime health tuning used by shared damage utility.</param>
    /// <param name="elapsedTime">Current elapsed simulation time used for grace windows.</param>
    /// <param name="totalDamage">Accumulated flat damage resolved from all overlapping acid segments.</param>
    /// <param name="audioRequests">Audio event buffer used for standard player damage feedback.</param>
    /// <param name="canEnqueueAudioRequests">True when an audio event singleton buffer is available.</param>
    private static void ApplyDamage(EntityManager entityManager,
                                    Entity playerEntity,
                                    float3 playerPosition,
                                    ref PlayerHealth playerHealth,
                                    ref PlayerShield playerShield,
                                    ref PlayerDamageGraceState playerDamageGraceState,
                                    in PlayerRuntimeHealthStatisticsConfig runtimeHealthConfig,
                                    float elapsedTime,
                                    float totalDamage,
                                    DynamicBuffer<GameAudioEventRequest> audioRequests,
                                    bool canEnqueueAudioRequests)
    {
        float previousHealth = playerHealth.Current;
        float previousShield = playerShield.Current;
        bool damageApplied = PlayerDamageUtility.TryApplyFlatShieldDamage(ref playerHealth,
                                                                          ref playerShield,
                                                                          ref playerDamageGraceState,
                                                                          in runtimeHealthConfig,
                                                                          elapsedTime,
                                                                          totalDamage);

        if (!damageApplied)
            return;

        if (canEnqueueAudioRequests)
        {
            if (playerShield.Current < previousShield)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShieldDamage, playerPosition);

            if (playerHealth.Current < previousHealth)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerHealthDamage, playerPosition);
        }

        entityManager.SetComponentData(playerEntity, playerHealth);
        entityManager.SetComponentData(playerEntity, playerShield);
        entityManager.SetComponentData(playerEntity, playerDamageGraceState);
        DamageFlashRuntimeUtility.Trigger(entityManager, playerEntity);
    }
    #endregion

    #region Jobs
    [BurstCompile]
    [WithAll(typeof(EnemyActive))]
    [WithNone(typeof(EnemyDespawnRequest), typeof(EnemySpawnInactivityLock))]
    private partial struct EnemyAcidTrailDamageJob : IJobEntity
    {
        public float3 PlayerPosition;
        public float DeltaTime;
        public NativeReference<float> AccumulatedDamage;

        /// <summary>
        /// Advances segment tick timers and accumulates damage from segments overlapping the player point.
        /// </summary>
        /// <param name="segments">Per-enemy acid segment buffer evaluated for overlap.</param>
        /// <param name="patternConfig">Compiled pattern config used to skip non-acid enemies.</param>
        private void Execute(DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                             in EnemyPatternConfig patternConfig)
        {
            if (patternConfig.AcidTrailEnabled == 0)
                return;

            float damage = AccumulatedDamage.Value;

            for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                EnemyAcidTrailSegmentElement segment = segments[segmentIndex];
                segment.ApplyTimer -= DeltaTime;

                int pendingApplyCount = ResolvePendingApplyCount(ref segment);

                if (pendingApplyCount > 0 && segment.DamagePerTick > 0f && segment.Radius > 0f)
                {
                    float3 delta = PlayerPosition - segment.Position;
                    delta.y = 0f;
                    float radius = math.max(0f, segment.Radius);

                    if (math.lengthsq(delta) <= radius * radius)
                        damage += math.max(0f, segment.DamagePerTick) * pendingApplyCount;
                }

                segments[segmentIndex] = segment;
            }

            AccumulatedDamage.Value = damage;
        }

        /// <summary>
        /// Converts an overdue segment timer into one or more fixed-interval damage applications.
        /// </summary>
        /// <param name="segment">Mutable acid segment whose apply timer is advanced.</param>
        /// <returns>Number of fixed damage applications due this frame.</returns>
        private static int ResolvePendingApplyCount(ref EnemyAcidTrailSegmentElement segment)
        {
            float applyIntervalSeconds = math.max(MinimumApplyIntervalSeconds, segment.ApplyIntervalSeconds);
            segment.ApplyIntervalSeconds = applyIntervalSeconds;

            if (segment.ApplyTimer > 0f)
                return 0;

            float overdueSeconds = -segment.ApplyTimer;
            int additionalApplyCount = (int)math.floor(overdueSeconds / applyIntervalSeconds);
            int applyCount = 1 + math.max(0, additionalApplyCount);
            segment.ApplyTimer += applyCount * applyIntervalSeconds;

            if (segment.ApplyTimer <= 0f)
                segment.ApplyTimer = applyIntervalSeconds;

            return applyCount;
        }
    }
    #endregion

    #endregion
}
