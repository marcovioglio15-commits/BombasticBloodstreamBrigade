using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Ages detached Acid Wanderer trail segments and applies their remaining damage after the owner dies.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyAcidTrailDetachOnDespawnSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyDetachedAcidTrailSystem : ISystem
{
    #region Constants
    private const float MinimumApplyIntervalSeconds = 0.01f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires detached acid segments before updating the standalone hazard pass.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyDetachedAcidTrailSegment>();
    }

    /// <summary>
    /// Ages detached segments, destroys expired ones, and applies one merged player damage tick.
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
        bool canApplyDamage = hasPlayer && CanApplyDamage(ref state, playerEntity, in playerHealth, in playerDamageGraceState, elapsedTime);
        float accumulatedDamage = 0f;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRW<EnemyDetachedAcidTrailSegment> detachedSegment,
                  Entity detachedEntity) in SystemAPI.Query<RefRW<EnemyDetachedAcidTrailSegment>>().WithEntityAccess())
        {
            EnemyDetachedAcidTrailSegment segment = detachedSegment.ValueRO;
            segment.RemainingLifetime -= deltaTime;

            if (segment.RemainingLifetime <= 0f)
            {
                commandBuffer.DestroyEntity(detachedEntity);
                continue;
            }

            if (hasPlayer)
                ProcessPlayerOverlap(ref segment, playerTransform.Position, deltaTime, canApplyDamage, ref accumulatedDamage);

            detachedSegment.ValueRW = segment;
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();

        if (!hasPlayer || accumulatedDamage <= 0f)
            return;

        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        ApplyDamage(state.EntityManager,
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

    /// <summary>
    /// Resolves player damage gates shared with the owner-bound Acid trail damage path.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <param name="playerEntity">Player entity being evaluated.</param>
    /// <param name="playerHealth">Current player health state.</param>
    /// <param name="playerDamageGraceState">Current damage grace state.</param>
    /// <returns>True when detached acid trail damage may be applied this frame.</returns>
    private static bool CanApplyDamage(ref SystemState state,
                                       Entity playerEntity,
                                       in PlayerHealth playerHealth,
                                       in PlayerDamageGraceState playerDamageGraceState,
                                       float elapsedTime)
    {
        if (playerHealth.Current <= 0f)
            return false;

        EntityManager entityManager = state.EntityManager;

        if (entityManager.HasComponent<PlayerDashState>(playerEntity))
        {
            PlayerDashState dashState = entityManager.GetComponentData<PlayerDashState>(playerEntity);

            if (dashState.RemainingInvulnerability > 0f)
                return false;
        }

        return !PlayerDamageUtility.IsDamageGraceActive(in playerDamageGraceState, elapsedTime);
    }
    #endregion

    #region Damage
    /// <summary>
    /// Updates one detached segment overlap state and accumulates damage when its cooldown is ready.
    /// </summary>
    /// <param name="segment">Mutable detached segment state.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="deltaTime">Scaled enemy delta time.</param>
    /// <param name="canApplyDamage">True when player damage gates allow damage.</param>
    /// <param name="accumulatedDamage">Merged damage accumulator for this frame.</param>
    private static void ProcessPlayerOverlap(ref EnemyDetachedAcidTrailSegment segment,
                                             float3 playerPosition,
                                             float deltaTime,
                                             bool canApplyDamage,
                                             ref float accumulatedDamage)
    {
        bool playerOverlaps = EnemyAcidTrailGeometryUtility.IsPointOverlappingSection(playerPosition,
                                                                                      segment.StartPosition,
                                                                                      segment.EndPosition,
                                                                                      segment.Radius);

        if (!playerOverlaps)
        {
            segment.PlayerOverlapping = 0;
            segment.PlayerDamageCooldown = 0f;
            return;
        }

        bool playerEnteredTrail = segment.PlayerOverlapping == 0;
        segment.PlayerOverlapping = 1;

        if (playerEnteredTrail)
            segment.PlayerDamageCooldown = 0f;
        else
            segment.PlayerDamageCooldown = math.max(0f, segment.PlayerDamageCooldown - deltaTime);

        if (segment.PlayerDamageCooldown > 0f || !canApplyDamage || segment.DamagePerTick <= 0f)
            return;

        accumulatedDamage += math.max(0f, segment.DamagePerTick);
        segment.PlayerDamageCooldown = math.max(MinimumApplyIntervalSeconds, segment.ApplyIntervalSeconds);
    }

    /// <summary>
    /// Applies one merged damage tick to the player and emits standard feedback/audio.
    /// </summary>
    /// <param name="entityManager">Runtime entity manager used to write player components.</param>
    /// <param name="playerEntity">Player entity receiving damage.</param>
    /// <param name="playerPosition">Player position used for positional audio.</param>
    /// <param name="playerHealth">Mutable player health snapshot.</param>
    /// <param name="playerShield">Mutable player shield snapshot.</param>
    /// <param name="playerDamageGraceState">Mutable player damage grace snapshot.</param>
    /// <param name="runtimeHealthConfig">Runtime health tuning used by shared damage utility.</param>
    /// <param name="elapsedTime">Current elapsed time used by damage grace.</param>
    /// <param name="totalDamage">Accumulated damage to apply.</param>
    /// <param name="audioRequests">Audio request buffer used by standard feedback.</param>
    /// <param name="canEnqueueAudioRequests">True when audio requests can be queued.</param>
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

    #endregion
}
