using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Identifies projectile despawn occasions that may produce configured bullet-death VFX.
/// </summary>
public enum ProjectileDeathVfxOccasion : byte
{
    RangeOrLifetime = 0,
    TerminalWallHit = 1
}

/// <summary>
/// Queues projectile-death one-shot VFX without coupling projectile pool return logic to managed presentation.
/// </summary>
public static class ProjectileDeathVfxRuntimeUtility
{
    #region Constants
    private const float MinimumScale = 0.01f;
    private const float MinimumLifetimeSeconds = 0.05f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Queues the configured VFX for one eligible projectile despawn occasion.
    /// </summary>
    /// <param name="occasion">Projectile despawn occasion being resolved.</param>
    /// <param name="projectileEntity">Projectile entity whose current activation is ending.</param>
    /// <param name="shooterEntity">Shooter entity owning the projectile and VFX request buffer.</param>
    /// <param name="projectileTransform">Final projectile pose before it is parked.</param>
    /// <param name="contactStateLookup">Read-only contact-state lookup used to suppress VFX after valid enemy hits.</param>
    /// <param name="configLookup">Read-only projectile-death VFX config lookup.</param>
    /// <param name="vfxRequestLookup">Writable managed VFX request buffer lookup.</param>
    public static void TryEnqueue(ProjectileDeathVfxOccasion occasion,
                                  Entity projectileEntity,
                                  Entity shooterEntity,
                                  in LocalTransform projectileTransform,
                                  in ComponentLookup<ProjectileContactState> contactStateLookup,
                                  in ComponentLookup<PlayerProjectileDeathVfxConfig> configLookup,
                                  ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (contactStateLookup.HasComponent(projectileEntity) &&
            contactStateLookup[projectileEntity].HasHitTarget != 0)
            return;

        if (!configLookup.HasComponent(shooterEntity) || !vfxRequestLookup.HasBuffer(shooterEntity))
            return;

        PlayerProjectileDeathVfxConfig config = configLookup[shooterEntity];
        PlayerProjectileDeathVfxEventConfig eventConfig;

        switch (occasion)
        {
            case ProjectileDeathVfxOccasion.TerminalWallHit:
                eventConfig = config.TerminalWallHit;
                break;
            default:
                eventConfig = config.RangeOrLifetime;
                break;
        }

        TryEnqueuePlayerEvent(in eventConfig,
                              shooterEntity,
                              in projectileTransform,
                              ref vfxRequestLookup);
    }

    /// <summary>
    /// Queues the configured VFX for one eligible projectile despawn occasion, supporting player and enemy shooters.
    /// </summary>
    /// <param name="occasion">Projectile despawn occasion being resolved.</param>
    /// <param name="projectileEntity">Projectile entity whose current activation is ending.</param>
    /// <param name="shooterEntity">Shooter entity owning the projectile and VFX request buffer.</param>
    /// <param name="projectileTransform">Final projectile pose before it is parked.</param>
    /// <param name="contactStateLookup">Read-only contact-state lookup used to suppress VFX after valid target hits.</param>
    /// <param name="playerConfigLookup">Read-only player projectile-death VFX config lookup.</param>
    /// <param name="enemyConfigLookup">Read-only enemy projectile-death VFX config lookup.</param>
    /// <param name="vfxRequestLookup">Writable managed VFX request buffer lookup.</param>
    public static void TryEnqueue(ProjectileDeathVfxOccasion occasion,
                                  Entity projectileEntity,
                                  Entity shooterEntity,
                                  in LocalTransform projectileTransform,
                                  in ComponentLookup<ProjectileContactState> contactStateLookup,
                                  in ComponentLookup<PlayerProjectileDeathVfxConfig> playerConfigLookup,
                                  in ComponentLookup<EnemyProjectileDeathVfxConfig> enemyConfigLookup,
                                  ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (contactStateLookup.HasComponent(projectileEntity) &&
            contactStateLookup[projectileEntity].HasHitTarget != 0)
            return;

        if (!vfxRequestLookup.HasBuffer(shooterEntity))
            return;

        if (playerConfigLookup.HasComponent(shooterEntity))
        {
            PlayerProjectileDeathVfxConfig playerConfig = playerConfigLookup[shooterEntity];
            PlayerProjectileDeathVfxEventConfig playerEventConfig = ResolvePlayerDeathEvent(occasion, in playerConfig);
            TryEnqueuePlayerEvent(in playerEventConfig,
                                  shooterEntity,
                                  in projectileTransform,
                                  ref vfxRequestLookup);
            return;
        }

        if (!enemyConfigLookup.HasComponent(shooterEntity))
            return;

        EnemyProjectileDeathVfxConfig enemyConfig = enemyConfigLookup[shooterEntity];
        EnemyProjectileVfxEventConfig enemyEventConfig = ResolveEnemyDeathEvent(occasion, in enemyConfig);
        TryEnqueueEnemyEvent(in enemyEventConfig,
                             shooterEntity,
                             in projectileTransform,
                             ref vfxRequestLookup);
    }

    /// <summary>
    /// Queues enemy-authored bullet-hit VFX when an enemy-owned projectile hits the player.
    /// </summary>
    /// <param name="shooterEntity">Enemy shooter entity owning the VFX request buffer.</param>
    /// <param name="projectileTransform">Projectile impact pose.</param>
    /// <param name="configLookup">Read-only enemy projectile-hit VFX config lookup.</param>
    /// <param name="vfxRequestLookup">Writable managed VFX request buffer lookup.</param>
    public static void TryEnqueueEnemyHit(Entity shooterEntity,
                                          in LocalTransform projectileTransform,
                                          in ComponentLookup<EnemyProjectileHitVfxConfig> configLookup,
                                          ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (!configLookup.HasComponent(shooterEntity) || !vfxRequestLookup.HasBuffer(shooterEntity))
            return;

        EnemyProjectileHitVfxConfig config = configLookup[shooterEntity];
        TryEnqueueEnemyEvent(in config.Hit,
                             shooterEntity,
                             in projectileTransform,
                             ref vfxRequestLookup);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the player projectile-death event matching one despawn occasion.
    /// </summary>
    /// <param name="occasion">Projectile despawn occasion being resolved.</param>
    /// <param name="config">Player projectile-death VFX config.</param>
    /// <returns>Event config matching the occasion.</returns>
    private static PlayerProjectileDeathVfxEventConfig ResolvePlayerDeathEvent(ProjectileDeathVfxOccasion occasion,
                                                                               in PlayerProjectileDeathVfxConfig config)
    {
        switch (occasion)
        {
            case ProjectileDeathVfxOccasion.TerminalWallHit:
                return config.TerminalWallHit;
            default:
                return config.RangeOrLifetime;
        }
    }

    /// <summary>
    /// Resolves the enemy projectile-death event matching one despawn occasion.
    /// </summary>
    /// <param name="occasion">Projectile despawn occasion being resolved.</param>
    /// <param name="config">Enemy projectile-death VFX config.</param>
    /// <returns>Event config matching the occasion.</returns>
    private static EnemyProjectileVfxEventConfig ResolveEnemyDeathEvent(ProjectileDeathVfxOccasion occasion,
                                                                        in EnemyProjectileDeathVfxConfig config)
    {
        switch (occasion)
        {
            case ProjectileDeathVfxOccasion.TerminalWallHit:
                return config.TerminalWallHit;
            default:
                return config.RangeOrLifetime;
        }
    }

    /// <summary>
    /// Queues one player projectile VFX event into the shooter-owned managed VFX buffer.
    /// </summary>
    /// <param name="eventConfig">Player VFX event config to enqueue.</param>
    /// <param name="shooterEntity">Shooter entity owning the request buffer.</param>
    /// <param name="projectileTransform">Projectile pose used by the spawned VFX.</param>
    /// <param name="vfxRequestLookup">Writable managed VFX request buffer lookup.</param>
    private static void TryEnqueuePlayerEvent(in PlayerProjectileDeathVfxEventConfig eventConfig,
                                              Entity shooterEntity,
                                              in LocalTransform projectileTransform,
                                              ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (eventConfig.Enabled == 0)
            return;

        if (eventConfig.PrefabEntity == Entity.Null && eventConfig.SourcePrefab.Value == null)
            return;

        EnqueueResolvedEvent(eventConfig.PrefabEntity,
                             eventConfig.SourcePrefab,
                             eventConfig.SpawnOffset,
                             eventConfig.UniformScale,
                             eventConfig.LifetimeSeconds,
                             shooterEntity,
                             in projectileTransform,
                             ref vfxRequestLookup);
    }

    /// <summary>
    /// Queues one enemy projectile VFX event into the shooter-owned managed VFX buffer.
    /// </summary>
    /// <param name="eventConfig">Enemy VFX event config to enqueue.</param>
    /// <param name="shooterEntity">Shooter entity owning the request buffer.</param>
    /// <param name="projectileTransform">Projectile pose used by the spawned VFX.</param>
    /// <param name="vfxRequestLookup">Writable managed VFX request buffer lookup.</param>
    private static void TryEnqueueEnemyEvent(in EnemyProjectileVfxEventConfig eventConfig,
                                             Entity shooterEntity,
                                             in LocalTransform projectileTransform,
                                             ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (eventConfig.Enabled == 0)
            return;

        if (eventConfig.PrefabEntity == Entity.Null && eventConfig.SourcePrefab.Value == null)
            return;

        EnqueueResolvedEvent(eventConfig.PrefabEntity,
                             eventConfig.SourcePrefab,
                             eventConfig.SpawnOffset,
                             eventConfig.UniformScale,
                             eventConfig.LifetimeSeconds,
                             shooterEntity,
                             in projectileTransform,
                             ref vfxRequestLookup);
    }

    /// <summary>
    /// Adds one normalized projectile VFX request to the shooter-owned managed VFX buffer.
    /// </summary>
    /// <param name="prefabEntity">Resolved ECS prefab entity for the VFX.</param>
    /// <param name="sourcePrefab">Managed source prefab fallback used by the VFX pool.</param>
    /// <param name="spawnOffset">Projectile-local offset applied at the final projectile pose.</param>
    /// <param name="uniformScale">Uniform authored scale multiplier.</param>
    /// <param name="lifetimeSeconds">Lifetime assigned to the spawned VFX instance.</param>
    /// <param name="shooterEntity">Shooter entity owning the request buffer.</param>
    /// <param name="projectileTransform">Projectile pose used by the spawned VFX.</param>
    /// <param name="vfxRequestLookup">Writable managed VFX request buffer lookup.</param>
    private static void EnqueueResolvedEvent(Entity prefabEntity,
                                             UnityObjectRef<GameObject> sourcePrefab,
                                             float3 spawnOffset,
                                             float uniformScale,
                                             float lifetimeSeconds,
                                             Entity shooterEntity,
                                             in LocalTransform projectileTransform,
                                             ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        quaternion rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(projectileTransform.Rotation);
        float projectileScale = math.max(MinimumScale, projectileTransform.Scale);
        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = vfxRequestLookup[shooterEntity];
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = sourcePrefab,
            Position = projectileTransform.Position + math.rotate(rotation, spawnOffset * projectileScale),
            Rotation = rotation,
            UniformScale = math.max(MinimumScale, uniformScale * projectileScale),
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = math.max(MinimumLifetimeSeconds, lifetimeSeconds),
            FollowTargetEntity = Entity.Null,
            FollowValidationEntity = Entity.Null,
            Velocity = float3.zero,
            RestartOldestOnCap = 1
        });
    }
    #endregion

    #endregion
}
