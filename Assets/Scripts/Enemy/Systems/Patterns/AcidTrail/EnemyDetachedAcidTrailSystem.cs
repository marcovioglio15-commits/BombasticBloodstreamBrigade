using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Continues the owner-level lifetime and damage behavior of Acid Wanderer trails after their owner despawns.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyAcidTrailDetachOnDespawnSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyDetachedAcidTrailSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires detached Acid owner state before updating standalone hazard groups.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyDetachedAcidTrailState>();
    }

    /// <summary>
    /// Ages detached owner-level trail groups, destroys expired groups, and applies one merged player damage tick.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;

        if (deltaTime <= 0f)
            return;

        Entity playerEntity = Entity.Null;
        LocalTransform playerTransform = default;
        PlayerHealth playerHealth = default;
        PlayerShield playerShield = default;
        PlayerRuntimeHealthStatisticsConfig runtimeHealthConfig = default;
        PlayerDamageGraceState playerDamageGraceState = default;
        bool hasPlayer = TryReadPlayerState(ref state,
                                            out playerEntity,
                                            out playerTransform,
                                            out playerHealth,
                                            out playerShield,
                                            out runtimeHealthConfig,
                                            out playerDamageGraceState);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        byte playerDamageAllowed = hasPlayer &&
                                   EnemyAcidTrailRuntimeUtility.CanApplyDamage(state.EntityManager,
                                                                              playerEntity,
                                                                              in playerHealth,
                                                                              in playerDamageGraceState,
                                                                              elapsedTime)
            ? (byte)1
            : (byte)0;
        float accumulatedDamage = 0f;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        // Continue each dead owner's retained trail as one continuous damage source.
        foreach ((RefRW<EnemyDetachedAcidTrailState> detachedState,
                  DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                  Entity detachedEntity) in SystemAPI.Query<RefRW<EnemyDetachedAcidTrailState>,
                                                            DynamicBuffer<EnemyAcidTrailSegmentElement>>()
                                                    .WithEntityAccess())
        {
            EnemyDetachedAcidTrailState currentState = detachedState.ValueRO;

            if (currentState.SkipCurrentUpdate != 0)
            {
                currentState.SkipCurrentUpdate = 0;
                detachedState.ValueRW = currentState;
                continue;
            }

            EnemyAcidTrailRuntimeUtility.CompactSegments(segments, deltaTime);

            if (segments.Length <= 0)
            {
                commandBuffer.DestroyEntity(detachedEntity);
                continue;
            }

            if (hasPlayer)
            {
                bool playerOverlapsTrail = EnemyAcidTrailRuntimeUtility.TryResolveOverlap(playerTransform.Position,
                                                                                          segments,
                                                                                          out float damagePerTick,
                                                                                          out float applyIntervalSeconds);
                accumulatedDamage += EnemyAcidTrailRuntimeUtility.AdvanceOverlap(playerOverlapsTrail,
                                                                                 ref currentState.PlayerOverlapping,
                                                                                 ref currentState.PlayerDamageCooldown,
                                                                                 deltaTime,
                                                                                 playerDamageAllowed,
                                                                                 damagePerTick,
                                                                                 applyIntervalSeconds);
            }

            detachedState.ValueRW = currentState;
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();

        if (!hasPlayer || accumulatedDamage <= 0f)
            return;

        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        EnemyAcidTrailRuntimeUtility.ApplyDamage(state.EntityManager,
                                                 playerEntity,
                                                 playerTransform.Position,
                                                 ref playerHealth,
                                                 ref playerShield,
                                                 ref playerDamageGraceState,
                                                 in runtimeHealthConfig,
                                                 elapsedTime,
                                                 accumulatedDamage,
                                                 audioRequests,
                                                 canEnqueueAudioRequests);
    }
    #endregion

    #region Player State
    /// <summary>
    /// Reads the first player entity and the health components needed by detached trail damage.
    /// </summary>
    /// <param name="state">Current ECS system state used by SystemAPI.Query source generation.</param>
    /// <param name="playerEntity">Resolved player entity.</param>
    /// <param name="playerTransform">Resolved player transform.</param>
    /// <param name="playerHealth">Resolved player health.</param>
    /// <param name="playerShield">Resolved player shield.</param>
    /// <param name="runtimeHealthConfig">Resolved runtime health statistics.</param>
    /// <param name="playerDamageGraceState">Resolved damage grace state.</param>
    /// <returns>True when a valid player state was found.</returns>
    private bool TryReadPlayerState(ref SystemState state,
                                    out Entity playerEntity,
                                    out LocalTransform playerTransform,
                                    out PlayerHealth playerHealth,
                                    out PlayerShield playerShield,
                                    out PlayerRuntimeHealthStatisticsConfig runtimeHealthConfig,
                                    out PlayerDamageGraceState playerDamageGraceState)
    {
        playerEntity = Entity.Null;
        playerTransform = default;
        playerHealth = default;
        playerShield = default;
        runtimeHealthConfig = default;
        playerDamageGraceState = default;

        foreach ((RefRO<LocalTransform> candidatePlayerTransform,
                  RefRO<PlayerHealth> candidatePlayerHealth,
                  RefRO<PlayerShield> candidatePlayerShield,
                  RefRO<PlayerRuntimeHealthStatisticsConfig> candidateRuntimeHealthConfig,
                  RefRO<PlayerDamageGraceState> candidateDamageGraceState,
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
            playerDamageGraceState = candidateDamageGraceState.ValueRO;
            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
