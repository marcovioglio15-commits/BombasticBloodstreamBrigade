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

        bool canApplyDamage = true;

        if (dashStateLookup.HasComponent(playerEntity))
        {
            PlayerDashState dashState = dashStateLookup[playerEntity];

            if (dashState.RemainingInvulnerability > 0f)
                canApplyDamage = false;
        }

        if (PlayerDamageUtility.IsDamageGraceActive(in playerDamageGraceState, elapsedTime))
            canApplyDamage = false;

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

            bool playerOverlapsTrail = false;
            float damagePerTick = 0f;
            float applyIntervalSeconds = 0f;

            // Merge every section owned by this Acid Wanderer into one continuous hazard overlap.
            for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                EnemyAcidTrailSegmentElement segment = segments[segmentIndex];

                if (!IsDamageSectionUsable(in segment))
                    continue;

                if (!IsPlayerOverlappingSection(PlayerPosition, in segment))
                    continue;

                playerOverlapsTrail = true;
                damagePerTick = math.max(damagePerTick, segment.DamagePerTick);
                applyIntervalSeconds = ResolveApplyIntervalSeconds(applyIntervalSeconds, segment.ApplyIntervalSeconds);
            }

            if (!playerOverlapsTrail)
            {
                ResetPlayerOverlap(ref patternRuntimeState);
                return;
            }

            bool playerEnteredTrail = patternRuntimeState.AcidPlayerOverlapping == 0;
            patternRuntimeState.AcidPlayerOverlapping = 1;

            if (playerEnteredTrail)
            {
                patternRuntimeState.AcidPlayerDamageCooldown = 0f;
                TryAccumulateReadyDamage(ref patternRuntimeState,
                                         damagePerTick,
                                         applyIntervalSeconds);
                return;
            }

            patternRuntimeState.AcidPlayerDamageCooldown = math.max(0f, patternRuntimeState.AcidPlayerDamageCooldown - DeltaTime);
            TryAccumulateReadyDamage(ref patternRuntimeState,
                                     damagePerTick,
                                     applyIntervalSeconds);
        }

        /// <summary>
        /// Applies one due owner-level Acid tick and starts the next overlap cooldown when player damage is allowed.
        /// </summary>
        /// <param name="patternRuntimeState">Mutable owner state retaining the current Acid cooldown.</param>
        /// <param name="damagePerTick">Resolved Acid damage for the current owner overlap.</param>
        /// <param name="applyIntervalSeconds">Cooldown started after an accepted due damage request.</param>
        private void TryAccumulateReadyDamage(ref EnemyPatternRuntimeState patternRuntimeState,
                                              float damagePerTick,
                                              float applyIntervalSeconds)
        {
            if (patternRuntimeState.AcidPlayerDamageCooldown > 0f)
                return;

            if (PlayerDamageAllowed == 0 || damagePerTick <= 0f)
                return;

            AccumulatedDamage.Value += math.max(0f, damagePerTick);
            patternRuntimeState.AcidPlayerDamageCooldown = math.max(MinimumApplyIntervalSeconds, applyIntervalSeconds);
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

        /// <summary>
        /// Returns whether one retained Acid section still carries a valid damage payload.
        /// </summary>
        /// <param name="segment">Retained Acid section being evaluated.</param>
        /// <returns>True when the section can contribute to player overlap damage.</returns>
        private static bool IsDamageSectionUsable(in EnemyAcidTrailSegmentElement segment)
        {
            return segment.RemainingLifetime > 0f &&
                   segment.DamagePerTick > 0f &&
                   segment.Radius > 0f;
        }

        /// <summary>
        /// Returns whether the player point is inside the planar capsule represented by one Acid section.
        /// </summary>
        /// <param name="position">World-space player point evaluated on the XZ plane.</param>
        /// <param name="segment">Retained Acid section being evaluated.</param>
        /// <returns>True when the player point is inside the Acid section radius.</returns>
        private static bool IsPlayerOverlappingSection(float3 position, in EnemyAcidTrailSegmentElement segment)
        {
            float radius = math.max(0f, segment.Radius);
            return ResolvePlanarDistanceSquaredToSegment(position,
                                                         segment.StartPosition,
                                                         segment.EndPosition) <= radius * radius;
        }

        /// <summary>
        /// Resolves the shortest retained overlap cooldown when neighboring Acid sections carry different values.
        /// </summary>
        /// <param name="currentIntervalSeconds">Shortest interval already found for this owner overlap.</param>
        /// <param name="candidateIntervalSeconds">Interval copied into the currently overlapping Acid section.</param>
        /// <returns>Shortest safe overlap cooldown in seconds.</returns>
        private static float ResolveApplyIntervalSeconds(float currentIntervalSeconds, float candidateIntervalSeconds)
        {
            float safeCandidateIntervalSeconds = math.max(MinimumApplyIntervalSeconds, candidateIntervalSeconds);

            if (currentIntervalSeconds <= 0f)
                return safeCandidateIntervalSeconds;

            return math.min(currentIntervalSeconds, safeCandidateIntervalSeconds);
        }

        /// <summary>
        /// Resolves the squared planar distance between the player point and one emitted acid path section.
        /// </summary>
        /// <param name="position">World-space point being tested against the trail section.</param>
        /// <param name="startPosition">World-space start of the emitted trail section.</param>
        /// <param name="endPosition">World-space end of the emitted trail section.</param>
        /// <returns>Squared distance on the XZ plane.</returns>
        private static float ResolvePlanarDistanceSquaredToSegment(float3 position,
                                                                   float3 startPosition,
                                                                   float3 endPosition)
        {
            float2 sectionStart = startPosition.xz;
            float2 sectionDelta = endPosition.xz - sectionStart;
            float sectionLengthSquared = math.lengthsq(sectionDelta);

            if (sectionLengthSquared <= math.EPSILON)
                return math.distancesq(position.xz, sectionStart);

            float normalizedProjection = math.saturate(math.dot(position.xz - sectionStart, sectionDelta) / sectionLengthSquared);
            float2 closestPoint = sectionStart + sectionDelta * normalizedProjection;
            return math.distancesq(position.xz, closestPoint);
        }
    }
    #endregion

    #endregion
}
