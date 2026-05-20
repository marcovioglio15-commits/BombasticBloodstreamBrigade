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
    private const float PlayerHitRadius = 0.55f;
    private const float HiddenBombScale = 0.0001f;
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
        float deltaTime = SystemAPI.Time.DeltaTime;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
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

            if (ShouldExplode(in bombState))
            {
                bombState.HasExploded = 1;
                bombTransform.ValueRW.Position = bombState.LandingPosition;
                bombTransform.ValueRW.Scale = HiddenBombScale;

                if (canEnqueueAudioRequests)
                    GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.ExplosionBomb, bombState.LandingPosition);

                EnqueueExplosionVfxRequest(in bombState, in localTransformLookup, ref vfxRequestLookup);

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

    #region VFX
    /// <summary>
    /// Queues the optional one-shot explosion VFX through the shared managed VFX pool.
    /// </summary>
    /// <param name="bombState">Bomb state carrying the resolved VFX prefab and explosion radius.</param>
    /// <param name="localTransformLookup">Read-only transform lookup used to keep floor-level VFX above the owner plane.</param>
    /// <param name="vfxRequestLookup">Writable VFX request buffers keyed by Bombardier owner entity.</param>
    private static void EnqueueExplosionVfxRequest(in EnemyBombardierBomb bombState,
                                                   in ComponentLookup<LocalTransform> localTransformLookup,
                                                   ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (bombState.OwnerEntity == Entity.Null)
            return;

        if (bombState.ExplosionVfxPrefabEntity == Entity.Null)
            return;

        if (!vfxRequestLookup.HasBuffer(bombState.OwnerEntity))
            return;

        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = vfxRequestLookup[bombState.OwnerEntity];
        float scaleMultiplier = math.max(0.01f, bombState.ExplosionVfxScaleMultiplier);

        if (bombState.ScaleExplosionVfxToDamageRadius != 0)
            scaleMultiplier *= math.max(0.1f, bombState.DamageRadius);

        float3 explosionVfxPosition = bombState.LandingPosition;

        if (localTransformLookup.HasComponent(bombState.OwnerEntity))
        {
            float ownerFloorReferenceY = localTransformLookup[bombState.OwnerEntity].Position.y;

            if (explosionVfxPosition.y < ownerFloorReferenceY)
                explosionVfxPosition.y = ownerFloorReferenceY;
        }

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = bombState.ExplosionVfxPrefabEntity,
            SourcePrefab = bombState.ExplosionVfxPrefab,
            Position = explosionVfxPosition,
            Rotation = quaternion.identity,
            UniformScale = scaleMultiplier,
            LifetimeSeconds = 2f,
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero
        });
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
        float hitRadius = math.max(0f, bombState.DamageRadius) + PlayerHitRadius;
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
