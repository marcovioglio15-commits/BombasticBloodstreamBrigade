using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Blocks enemy projectiles and enemy bombs that intersect active player orbital projections.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(PlayerOrbitalProjectionEnemyContactSystem))]
[UpdateBefore(typeof(EnemyBombardierBombSystem))]
[UpdateBefore(typeof(EnemyProjectileHitPlayerSystem))]
public partial struct PlayerOrbitalProjectionInterceptionSystem : ISystem
{
    #region Constants
    private const float BaseProjectileHitRadius = 0.05f;
    private const float BaseBombHitRadius = 0.18f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers projection components required by projectile and bomb interception.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerOrbitalProjectionInstance>();
    }

    /// <summary>
    /// Resolves projectile and bomb overlaps against active projections.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        ComponentLookup<PlayerControllerConfig> playerControllerLookup = SystemAPI.GetComponentLookup<PlayerControllerConfig>(true);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        BufferLookup<ProjectilePoolElement> projectilePoolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        BufferLookup<PlayerOrbitalProjectionLostElement> lostProjectionLookup = SystemAPI.GetBufferLookup<PlayerOrbitalProjectionLostElement>(false);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((RefRW<PlayerOrbitalProjectionInstance> projection,
                  RefRO<LocalTransform> projectionTransform)
                 in SystemAPI.Query<RefRW<PlayerOrbitalProjectionInstance>, RefRO<LocalTransform>>())
        {
            PlayerOrbitalProjectionInstance instance = projection.ValueRO;

            if (instance.Phase == PlayerOrbitalProjectionPhase.Despawning)
                continue;

            if (instance.Config.BlockEnemyProjectiles != 0)
            {
                foreach ((RefRO<Projectile> projectile,
                          RefRO<ProjectileOwner> projectileOwner,
                          RefRO<LocalTransform> projectileTransform,
                          Entity projectileEntity)
                         in SystemAPI.Query<RefRO<Projectile>, RefRO<ProjectileOwner>, RefRO<LocalTransform>>()
                                     .WithAll<ProjectileActive>()
                                     .WithEntityAccess())
                {
                    Entity shooterEntity = projectileOwner.ValueRO.ShooterEntity;

                    if (playerControllerLookup.HasComponent(shooterEntity))
                        continue;

                    float projectileRadius = BaseProjectileHitRadius * math.max(0.01f, projectileTransform.ValueRO.Scale) +
                                             math.max(0f, projectile.ValueRO.ExplosionRadius);

                    if (!IsOverlapping(projectionTransform.ValueRO.Position,
                                       instance.Config.CollisionRadius,
                                       projectileTransform.ValueRO.Position,
                                       projectileRadius))
                    {
                        continue;
                    }

                    DespawnProjectile(entityManager,
                                      projectileEntity,
                                      shooterEntity,
                                      ref projectilePoolLookup);
                    ApplyProjectionHealthCost(ref instance,
                                              projectionTransform.ValueRO.Position,
                                              instance.Config.ProjectileBlockHealthDamage,
                                              ref lostProjectionLookup);

                    if (instance.Phase == PlayerOrbitalProjectionPhase.Despawning)
                        break;
                }
            }

            if (instance.Phase != PlayerOrbitalProjectionPhase.Despawning &&
                instance.Config.BlockEnemyBombs != 0)
            {
                foreach ((RefRO<EnemyBombardierBomb> bomb,
                          RefRO<LocalTransform> bombTransform,
                          Entity bombEntity)
                         in SystemAPI.Query<RefRO<EnemyBombardierBomb>, RefRO<LocalTransform>>()
                                     .WithEntityAccess())
                {
                    if (bomb.ValueRO.HasExploded != 0)
                        continue;

                    if (bomb.ValueRO.PreventMidAirInterception != 0)
                        continue;

                    if (!IsOverlapping(projectionTransform.ValueRO.Position,
                                       instance.Config.CollisionRadius,
                                       bombTransform.ValueRO.Position,
                                       ResolveBombHitRadius(in bomb.ValueRO, in bombTransform.ValueRO)))
                    {
                        continue;
                    }

                    EnemyBombardierExplosionFeedbackUtility.EnqueueExplosionFeedback(in bomb.ValueRO,
                                                                                     bombTransform.ValueRO.Position,
                                                                                     in localTransformLookup,
                                                                                     ref vfxRequestLookup,
                                                                                     canEnqueueAudioRequests,
                                                                                     audioRequests);
                    commandBuffer.DestroyEntity(bombEntity);
                    ApplyProjectionHealthCost(ref instance,
                                              projectionTransform.ValueRO.Position,
                                              instance.Config.BombBlockHealthDamage,
                                              ref lostProjectionLookup);

                    if (instance.Phase == PlayerOrbitalProjectionPhase.Despawning)
                        break;
                }
            }

            projection.ValueRW = instance;
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Projectile Blocking
    /// <summary>
    /// Parks one intercepted projectile and returns it to the shooter pool when available.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate projectile components.</param>
    /// <param name="projectileEntity">Projectile entity being blocked.</param>
    /// <param name="shooterEntity">Shooter entity that owns the projectile pool.</param>
    /// <param name="projectilePoolLookup">Projectile pool lookup used to requeue the projectile.</param>
    private static void DespawnProjectile(EntityManager entityManager,
                                          Entity projectileEntity,
                                          Entity shooterEntity,
                                          ref BufferLookup<ProjectilePoolElement> projectilePoolLookup)
    {
        ProjectilePoolUtility.SetProjectileParked(entityManager, projectileEntity);
        entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);

        if (!projectilePoolLookup.HasBuffer(shooterEntity))
            return;

        DynamicBuffer<ProjectilePoolElement> shooterPool = projectilePoolLookup[shooterEntity];
        shooterPool.Add(new ProjectilePoolElement
        {
            ProjectileEntity = projectileEntity
        });
    }
    #endregion

    #region Shared Helpers
    /// <summary>
    /// Resolves the physical Bombardier bomb body radius used for orbital interception.
    /// </summary>
    /// <param name="bomb">Bombardier bomb state carrying an authored collision radius when available.</param>
    /// <param name="bombTransform">Current bomb transform used as compatibility fallback.</param>
    /// <returns>Positive radius for body-level interception.</returns>
    private static float ResolveBombHitRadius(in EnemyBombardierBomb bomb, in LocalTransform bombTransform)
    {
        if (bomb.CollisionRadius > 0f)
            return bomb.CollisionRadius;

        return BaseBombHitRadius * math.max(0.01f, bombTransform.Scale);
    }

    /// <summary>
    /// Applies optional health loss to the projection after an interception.
    /// </summary>
    /// <param name="instance">Projection instance updated in place.</param>
    /// <param name="currentPosition">Current projection position used when despawn starts.</param>
    /// <param name="healthDamage">Health cost applied to the projection.</param>
    /// <param name="lostProjectionLookup">Writable owner lookup used to store permanent loss markers.</param>
    private static void ApplyProjectionHealthCost(ref PlayerOrbitalProjectionInstance instance,
                                                  float3 currentPosition,
                                                  float healthDamage,
                                                  ref BufferLookup<PlayerOrbitalProjectionLostElement> lostProjectionLookup)
    {
        if (instance.Config.HasHealth == 0 || healthDamage <= 0f)
            return;

        instance.CurrentHealth -= healthDamage;

        if (instance.CurrentHealth > 0f)
            return;

        instance.Phase = PlayerOrbitalProjectionPhase.Despawning;
        instance.PhaseElapsedSeconds = 0f;
        instance.DespawnStartPosition = currentPosition;
        PlayerOrbitalProjectionLossRuntimeUtility.TryRecordPermanentLoss(ref lostProjectionLookup,
                                                                         in instance);
    }

    /// <summary>
    /// Checks overlap between two circles in the XZ plane.
    /// </summary>
    /// <param name="aPosition">First world position.</param>
    /// <param name="aRadius">First radius.</param>
    /// <param name="bPosition">Second world position.</param>
    /// <param name="bRadius">Second radius.</param>
    /// <returns>True when the two circles overlap.</returns>
    private static bool IsOverlapping(float3 aPosition,
                                      float aRadius,
                                      float3 bPosition,
                                      float bRadius)
    {
        float3 delta = bPosition - aPosition;
        delta.y = 0f;
        float radius = math.max(0f, aRadius) + math.max(0f, bRadius);
        return math.lengthsq(delta) <= radius * radius;
    }
    #endregion

    #endregion
}
