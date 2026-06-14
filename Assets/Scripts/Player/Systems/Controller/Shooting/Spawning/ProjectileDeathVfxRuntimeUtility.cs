using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

        if (eventConfig.Enabled == 0)
            return;

        if (eventConfig.PrefabEntity == Entity.Null && eventConfig.SourcePrefab.Value == null)
            return;

        quaternion rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(projectileTransform.Rotation);
        float projectileScale = math.max(MinimumScale, projectileTransform.Scale);
        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = vfxRequestLookup[shooterEntity];
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = eventConfig.PrefabEntity,
            SourcePrefab = eventConfig.SourcePrefab,
            Position = projectileTransform.Position + math.rotate(rotation, eventConfig.SpawnOffset * projectileScale),
            Rotation = rotation,
            UniformScale = math.max(MinimumScale, eventConfig.UniformScale * projectileScale),
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = math.max(MinimumLifetimeSeconds, eventConfig.LifetimeSeconds),
            FollowTargetEntity = Entity.Null,
            FollowValidationEntity = Entity.Null,
            Velocity = float3.zero,
            RestartOldestOnCap = 1
        });
    }
    #endregion

    #endregion
}
