using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Simulates Bombardier bomb trajectories, resolves delayed explosions and applies player damage.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyBombardierBombSpawnSystem))]
[UpdateBefore(typeof(EnemyProjectileHitPlayerSystem))]
public partial struct EnemyBombardierBombSystem : ISystem
{
    #region Constants
    private const float HiddenBombScale = 0.0001f;
    private const float TimeScaleEpsilon = 0.0001f;
    #endregion

    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches player query and declares Bombardier bomb state as an update dependency.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform, PlayerHealth, PlayerShield, PlayerRuntimeHealthStatisticsConfig, PlayerDamageGraceState>()
            .Build();

        state.RequireForUpdate<EnemyBombardierBomb>();
    }

    /// <summary>
    /// Advances active enemy bombs and damages the player once per explosion.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        float unscaledDeltaTime = SystemAPI.Time.DeltaTime;
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = unscaledDeltaTime * enemyTimeScale;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<EnemyBombardierWarningState> warningStateLookup = SystemAPI.GetComponentLookup<EnemyBombardierWarningState>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        PlayerDamageSnapshot playerSnapshot = ResolvePlayerSnapshot(ref state);
        float accumulatedDamage = 0f;
        bool anyBombExplodedNearPlayer = false;
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((RefRW<EnemyBombardierBomb> bomb,
                  RefRW<LocalTransform> bombTransform,
                  Entity bombEntity) in SystemAPI.Query<RefRW<EnemyBombardierBomb>,
                                                        RefRW<LocalTransform>>()
                                                .WithEntityAccess())
        {
            EnemyBombardierBomb bombState = bomb.ValueRO;

            if (bombState.HasExploded != 0)
            {
                if (elapsedTime >= bombState.WarningFadeOutEndTime)
                    commandBuffer.DestroyEntity(bombEntity);

                continue;
            }

            AdvanceBombState(ref bombState, ref bombTransform.ValueRW, deltaTime);
            SynchronizeBombardierWarning(bombEntity,
                                         ref bombState,
                                         ref warningStateLookup,
                                         elapsedTime,
                                         unscaledDeltaTime,
                                         enemyTimeScale);

            if (ShouldExplode(in bombState))
            {
                bombState.HasExploded = 1;
                bombTransform.ValueRW.Position = bombState.LandingPosition;
                bombTransform.ValueRW.Scale = HiddenBombScale;

                EnemyBombardierExplosionFeedbackUtility.EnqueueExplosionFeedback(in bombState,
                                                                                 bombState.LandingPosition,
                                                                                 in localTransformLookup,
                                                                                 ref vfxRequestLookup,
                                                                                 canEnqueueAudioRequests,
                                                                                 audioRequests);

                if (ShouldDamagePlayer(in playerSnapshot, in bombState))
                {
                    accumulatedDamage += math.max(0f, bombState.Damage);
                    anyBombExplodedNearPlayer = true;
                }

                if (elapsedTime >= bombState.WarningFadeOutEndTime)
                    commandBuffer.DestroyEntity(bombEntity);
            }

            bomb.ValueRW = bombState;
        }

        if (anyBombExplodedNearPlayer)
            ApplyAccumulatedPlayerDamage(entityManager,
                                         in playerSnapshot,
                                         accumulatedDamage,
                                         elapsedTime,
                                         canEnqueueAudioRequests,
                                         audioRequests);

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Simulation
    /// <summary>
    /// Advances one Bombardier bomb position until impact, then advances its explosion delay.
    /// </summary>
    /// <param name="bombState">Mutable bomb state.</param>
    /// <param name="bombTransform">Mutable bomb transform.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    private static void AdvanceBombState(ref EnemyBombardierBomb bombState,
                                         ref LocalTransform bombTransform,
                                         float deltaTime)
    {
        float safeDeltaTime = math.max(0f, deltaTime);

        if (bombState.HasImpacted != 0)
        {
            bombState.ExplosionDelayElapsedSeconds += safeDeltaTime;
            bombTransform.Position = bombState.LandingPosition;
            return;
        }

        bombState.ElapsedSeconds += safeDeltaTime;

        if (bombState.ElapsedSeconds >= math.max(0f, bombState.FlightDurationSeconds))
        {
            bombState.HasImpacted = 1;
            bombState.ElapsedSeconds = math.max(0f, bombState.FlightDurationSeconds);
            bombTransform.Position = bombState.LandingPosition;
            return;
        }

        float3 position = bombState.LaunchPosition + bombState.Velocity * bombState.ElapsedSeconds;
        position.y -= 0.5f * math.max(0f, bombState.Gravity) * bombState.ElapsedSeconds * bombState.ElapsedSeconds;
        bombTransform.Position = position;

        float3 instantaneousVelocity = bombState.Velocity;
        instantaneousVelocity.y -= math.max(0f, bombState.Gravity) * bombState.ElapsedSeconds;

        if (math.lengthsq(instantaneousVelocity) <= 1e-6f)
            return;

        bombTransform.Rotation = quaternion.LookRotationSafe(math.normalizesafe(instantaneousVelocity, new float3(0f, 0f, 1f)),
                                                             new float3(0f, 1f, 0f));
    }

    /// <summary>
    /// Resolves whether one impacted bomb should explode this frame.
    /// </summary>
    /// <param name="bombState">Current bomb state.</param>
    /// <returns>True when the impact delay is complete.</returns>
    private static bool ShouldExplode(in EnemyBombardierBomb bombState)
    {
        if (bombState.HasImpacted == 0)
            return false;

        return bombState.ExplosionDelayElapsedSeconds >= math.max(0f, bombState.ImpactExplosionDelaySeconds);
    }

    /// <summary>
    /// Keeps the landing warning timing aligned with Bullet-Time-scaled bomb flight and delayed explosion cleanup.
    /// </summary>
    /// <param name="bombEntity">Bomb entity that may own an enabled landing warning state.</param>
    /// <param name="bombState">Mutable bomb simulation state whose cleanup timing is synchronized.</param>
    /// <param name="warningStateLookup">Writable lookup used to update the paired warning state.</param>
    /// <param name="elapsedTime">Current world elapsed time.</param>
    /// <param name="unscaledDeltaTime">Unscaled frame delta time used to hold warnings while time is frozen.</param>
    /// <param name="enemyTimeScale">Enemy global time scale applied to bomb simulation.</param>
    private static void SynchronizeBombardierWarning(Entity bombEntity,
                                                     ref EnemyBombardierBomb bombState,
                                                     ref ComponentLookup<EnemyBombardierWarningState> warningStateLookup,
                                                     float elapsedTime,
                                                     float unscaledDeltaTime,
                                                     float enemyTimeScale)
    {
        if (!warningStateLookup.HasComponent(bombEntity))
            return;

        if (!warningStateLookup.IsComponentEnabled(bombEntity))
            return;

        EnemyBombardierWarningState warningState = warningStateLookup[bombEntity];

        if (bombState.HasExploded != 0)
        {
            bombState.WarningFadeOutEndTime = math.max(bombState.WarningFadeOutEndTime, warningState.FadeOutEndTime);
            return;
        }

        if (enemyTimeScale <= TimeScaleEpsilon && bombState.HasImpacted == 0)
        {
            float shiftSeconds = math.max(0f, unscaledDeltaTime);
            warningState.WarningStartTime += shiftSeconds;
            warningState.ImpactTime += shiftSeconds;
            warningState.FadeOutEndTime += shiftSeconds;
            bombState.WarningFadeOutEndTime += shiftSeconds;
            warningStateLookup[bombEntity] = warningState;
            return;
        }

        float predictedImpactTime = ResolveBombardierWarningImpactTime(in bombState,
                                                                       elapsedTime,
                                                                       enemyTimeScale);
        float predictedExplosionTime = ResolveBombardierExplosionTime(in bombState,
                                                                      predictedImpactTime,
                                                                      elapsedTime,
                                                                      enemyTimeScale);
        warningState.ImpactTime = predictedImpactTime;
        warningState.FadeOutEndTime = predictedImpactTime + math.max(0f, warningState.FadeOutSeconds);

        if (elapsedTime < warningState.WarningStartTime)
        {
            float configuredLeadSeconds = math.max(0f, warningState.LeadTimeSeconds);
            warningState.WarningStartTime = math.max(elapsedTime, predictedImpactTime - configuredLeadSeconds);
            warningState.LeadTimeSeconds = math.max(0f, predictedImpactTime - warningState.WarningStartTime);
        }
        else
        {
            warningState.LeadTimeSeconds = math.max(0f, predictedImpactTime - warningState.WarningStartTime);
        }

        bombState.WarningFadeOutEndTime = predictedExplosionTime + math.max(0f, warningState.FadeOutSeconds);
        warningStateLookup[bombEntity] = warningState;
    }

    /// <summary>
    /// Predicts the world time at which the scaled bomb flight will reach the landing point.
    /// </summary>
    /// <param name="bombState">Bomb simulation state containing flight progress.</param>
    /// <param name="elapsedTime">Current world elapsed time.</param>
    /// <param name="enemyTimeScale">Enemy global time scale applied to bomb simulation.</param>
    /// <returns>Predicted impact world time.</returns>
    private static float ResolveBombardierWarningImpactTime(in EnemyBombardierBomb bombState,
                                                            float elapsedTime,
                                                            float enemyTimeScale)
    {
        if (bombState.HasImpacted != 0)
            return elapsedTime;

        float remainingFlightSeconds = math.max(0f, bombState.FlightDurationSeconds - bombState.ElapsedSeconds);
        return elapsedTime + remainingFlightSeconds / math.max(TimeScaleEpsilon, enemyTimeScale);
    }

    /// <summary>
    /// Predicts the world time at which the scaled post-impact delay will complete.
    /// </summary>
    /// <param name="bombState">Bomb simulation state containing impact delay progress.</param>
    /// <param name="predictedImpactTime">Predicted world time of the landing impact.</param>
    /// <param name="elapsedTime">Current world elapsed time.</param>
    /// <param name="enemyTimeScale">Enemy global time scale applied to bomb simulation.</param>
    /// <returns>Predicted explosion world time.</returns>
    private static float ResolveBombardierExplosionTime(in EnemyBombardierBomb bombState,
                                                        float predictedImpactTime,
                                                        float elapsedTime,
                                                        float enemyTimeScale)
    {
        float remainingDelaySeconds = math.max(0f,
                                               bombState.ImpactExplosionDelaySeconds -
                                               bombState.ExplosionDelayElapsedSeconds);

        if (bombState.HasImpacted == 0)
            return predictedImpactTime + remainingDelaySeconds / math.max(TimeScaleEpsilon, enemyTimeScale);

        return elapsedTime + remainingDelaySeconds / math.max(TimeScaleEpsilon, enemyTimeScale);
    }
    #endregion

    #region Player Damage
    /// <summary>
    /// Captures current player damage-relevant state once per frame.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Player snapshot used by bomb overlap and damage application.</returns>
    private PlayerDamageSnapshot ResolvePlayerSnapshot(ref SystemState state)
    {
        ComponentLookup<PlayerDashState> dashStateLookup = SystemAPI.GetComponentLookup<PlayerDashState>(true);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRO<LocalTransform> playerTransform,
                  RefRO<PlayerHealth> playerHealth,
                  RefRO<PlayerShield> playerShield,
                  RefRO<PlayerRuntimeHealthStatisticsConfig> runtimeHealthConfig,
                  RefRO<PlayerDamageGraceState> playerDamageGraceState,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<LocalTransform>,
                                    RefRO<PlayerHealth>,
                                    RefRO<PlayerShield>,
                                    RefRO<PlayerRuntimeHealthStatisticsConfig>,
                                    RefRO<PlayerDamageGraceState>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            bool canTakeDamage = playerHealth.ValueRO.Current > 0f;

            if (dashStateLookup.HasComponent(playerEntity))
            {
                PlayerDashState dashState = dashStateLookup[playerEntity];

                if (dashState.RemainingInvulnerability > 0f)
                    canTakeDamage = false;
            }

            if (PlayerDamageUtility.IsDamageGraceActive(in playerDamageGraceState.ValueRO, elapsedTime))
                canTakeDamage = false;

            return new PlayerDamageSnapshot
            {
                Entity = playerEntity,
                Position = playerTransform.ValueRO.Position,
                Health = playerHealth.ValueRO,
                Shield = playerShield.ValueRO,
                RuntimeHealthConfig = runtimeHealthConfig.ValueRO,
                DamageGraceState = playerDamageGraceState.ValueRO,
                CanTakeDamage = canTakeDamage
            };
        }

        return default;
    }

    /// <summary>
    /// Checks whether one bomb explosion overlaps the current player snapshot.
    /// </summary>
    /// <param name="playerSnapshot">Player snapshot resolved for this frame.</param>
    /// <param name="bombState">Bomb explosion state.</param>
    /// <returns>True when the player should receive this bomb's damage.</returns>
    private static bool ShouldDamagePlayer(in PlayerDamageSnapshot playerSnapshot, in EnemyBombardierBomb bombState)
    {
        if (!playerSnapshot.CanTakeDamage)
            return false;

        if (playerSnapshot.Entity == Entity.Null)
            return false;

        if (bombState.Damage <= 0f)
            return false;

        float3 delta = bombState.LandingPosition - playerSnapshot.Position;
        delta.y = 0f;
        float hitRadius = math.max(0f, bombState.DamageRadius) + PlayerHitAreaUtility.HitRadius;
        return math.lengthsq(delta) <= hitRadius * hitRadius;
    }

    /// <summary>
    /// Applies accumulated Bombardier damage to the player and triggers feedback once.
    /// </summary>
    /// <param name="entityManager">Entity manager used to write player components.</param>
    /// <param name="playerSnapshot">Player snapshot resolved before bomb iteration.</param>
    /// <param name="accumulatedDamage">Total damage from overlapping bomb explosions.</param>
    /// <param name="elapsedTime">Current elapsed world time.</param>
    /// <param name="canEnqueueAudioRequests">Whether audio requests can be written.</param>
    /// <param name="audioRequests">Audio request buffer.</param>
    private static void ApplyAccumulatedPlayerDamage(EntityManager entityManager,
                                                     in PlayerDamageSnapshot playerSnapshot,
                                                     float accumulatedDamage,
                                                     float elapsedTime,
                                                     bool canEnqueueAudioRequests,
                                                     DynamicBuffer<GameAudioEventRequest> audioRequests)
    {
        if (playerSnapshot.Entity == Entity.Null)
            return;

        if (!entityManager.Exists(playerSnapshot.Entity))
            return;

        if (accumulatedDamage <= 0f)
            return;

        PlayerHealth playerHealth = playerSnapshot.Health;
        PlayerShield playerShield = playerSnapshot.Shield;
        PlayerDamageGraceState damageGraceState = playerSnapshot.DamageGraceState;
        float previousHealth = playerHealth.Current;
        float previousShield = playerShield.Current;
        bool damageApplied = PlayerDamageUtility.TryApplyFlatShieldDamage(ref playerHealth,
                                                                          ref playerShield,
                                                                          ref damageGraceState,
                                                                          in playerSnapshot.RuntimeHealthConfig,
                                                                          elapsedTime,
                                                                          accumulatedDamage);

        if (!damageApplied)
            return;

        if (canEnqueueAudioRequests)
        {
            if (playerShield.Current < previousShield)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShieldDamage, playerSnapshot.Position);

            if (playerHealth.Current < previousHealth)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerHealthDamage, playerSnapshot.Position);
        }

        entityManager.SetComponentData(playerSnapshot.Entity, playerHealth);
        entityManager.SetComponentData(playerSnapshot.Entity, playerShield);
        entityManager.SetComponentData(playerSnapshot.Entity, damageGraceState);
        DamageFlashRuntimeUtility.Trigger(entityManager, playerSnapshot.Entity);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores player state required for Bombardier explosion damage.
    /// </summary>
    private struct PlayerDamageSnapshot
    {
        public Entity Entity;
        public float3 Position;
        public PlayerHealth Health;
        public PlayerShield Shield;
        public PlayerRuntimeHealthStatisticsConfig RuntimeHealthConfig;
        public PlayerDamageGraceState DamageGraceState;
        public bool CanTakeDamage;
    }
    #endregion
}
