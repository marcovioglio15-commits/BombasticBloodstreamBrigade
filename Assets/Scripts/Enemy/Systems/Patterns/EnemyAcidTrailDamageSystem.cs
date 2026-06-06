using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies Acid Wanderer trail entry damage and overlap cooldown damage to the player.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyContactDamageSystem))]
[UpdateAfter(typeof(EnemyAcidTrailSpawnSystem))]
[UpdateBefore(typeof(EnemyDespawnSystem))]
public partial struct EnemyAcidTrailDamageSystem : ISystem
{
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
            .WithAll<EnemyPatternConfig, EnemyPatternRuntimeState, EnemyAcidTrailSegmentElement, EnemyActive>()
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

        if (playerHealth.Current <= 0f)
            return;

        bool canApplyDamage = EnemyAcidTrailRuntimeUtility.CanApplyDamage(entityManager,
                                                                         playerEntity,
                                                                         in playerHealth,
                                                                         in playerDamageGraceState,
                                                                         elapsedTime);
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
            PlayerDamageAllowed = canApplyDamage ? (byte)1 : (byte)0,
            AccumulatedDamage = accumulatedDamage
        };
        JobHandle damageHandle = damageJob.Schedule(state.Dependency);
        damageHandle.Complete();
        state.Dependency = damageHandle;

        float totalDamage = math.max(0f, accumulatedDamage.Value);
        accumulatedDamage.Dispose();

        if (totalDamage <= 0f)
            return;

        EnemyAcidTrailRuntimeUtility.ApplyDamage(entityManager,
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

    #region Jobs
    [BurstCompile]
    [WithAll(typeof(EnemyActive))]
    [WithNone(typeof(EnemyDespawnRequest), typeof(EnemySpawnInactivityLock))]
    private partial struct EnemyAcidTrailDamageJob : IJobEntity
    {
        public float3 PlayerPosition;
        public float DeltaTime;
        public byte PlayerDamageAllowed;
        public NativeReference<float> AccumulatedDamage;

        /// <summary>
        /// Tracks one owner-level trail overlap window and accumulates entry or cooldown damage when it becomes due.
        /// </summary>
        /// <param name="patternRuntimeState">Mutable owner state retaining player overlap and damage cooldown.</param>
        /// <param name="segments">Per-enemy acid segment buffer evaluated for overlap.</param>
        /// <param name="patternConfig">Compiled pattern config used to skip non-acid enemies.</param>
        private void Execute(ref EnemyPatternRuntimeState patternRuntimeState,
                             DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                             in EnemyPatternConfig patternConfig)
        {
            if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererAcid ||
                patternConfig.AcidTrailEnabled == 0 ||
                segments.Length <= 0)
            {
                ResetPlayerOverlap(ref patternRuntimeState);
                return;
            }

            bool playerOverlapsTrail = EnemyAcidTrailRuntimeUtility.TryResolveOverlap(PlayerPosition,
                                                                                      segments,
                                                                                      out float damagePerTick,
                                                                                      out float applyIntervalSeconds);
            AccumulatedDamage.Value += EnemyAcidTrailRuntimeUtility.AdvanceOverlap(playerOverlapsTrail,
                                                                                   ref patternRuntimeState.AcidPlayerOverlapping,
                                                                                   ref patternRuntimeState.AcidPlayerDamageCooldown,
                                                                                   DeltaTime,
                                                                                   PlayerDamageAllowed,
                                                                                   damagePerTick,
                                                                                   applyIntervalSeconds);
        }

        /// <summary>
        /// Clears owner-level Acid overlap data when the player is no longer on any retained section.
        /// </summary>
        /// <param name="patternRuntimeState">Mutable owner state cleared for the next trail entry.</param>
        private static void ResetPlayerOverlap(ref EnemyPatternRuntimeState patternRuntimeState)
        {
            patternRuntimeState.AcidPlayerDamageCooldown = 0f;
            patternRuntimeState.AcidPlayerOverlapping = 0;
        }

    }
    #endregion

    #endregion
}
