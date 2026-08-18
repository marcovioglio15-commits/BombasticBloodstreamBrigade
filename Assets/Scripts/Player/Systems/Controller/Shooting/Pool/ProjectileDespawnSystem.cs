using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// This system handles the despawning of projectile entities when they exceed their maximum range 
/// or lifetime. It runs after the ProjectileSimulationSystem to ensure that projectile movement 
/// and state updates have been processed before checking for despawn conditions. 
/// When a projectile is despawned, 
/// it is parked and returned to the shooter's projectile pool for reuse.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(ProjectileSimulationSystem))]
public partial struct ProjectileDespawnSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures component requirements for projectile despawn evaluation.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Projectile>();
        state.RequireForUpdate<ProjectileRuntimeState>();
        state.RequireForUpdate<ProjectileOwner>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<ProjectileActive>();
    }

    /// <summary>
    /// Evaluates active projectiles and returns expired ones to pool, including optional split spawn enqueue.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<ProjectilePoolElement> poolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<ShootRequest> shootRequestLookup = SystemAPI.GetBufferLookup<ShootRequest>(false);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);
        ComponentLookup<ProjectileSplitState> projectileSplitStateLookup = SystemAPI.GetComponentLookup<ProjectileSplitState>(false);
        ComponentLookup<ProjectileElementalPayload> projectileElementalPayloadLookup = SystemAPI.GetComponentLookup<ProjectileElementalPayload>(true);
        ComponentLookup<ProjectileActive> projectileActiveLookup = SystemAPI.GetComponentLookup<ProjectileActive>(false);
        ComponentLookup<ProjectileContactState> projectileContactStateLookup = SystemAPI.GetComponentLookup<ProjectileContactState>(true);
        ComponentLookup<ProjectileReturnState> projectileReturnStateLookup = SystemAPI.GetComponentLookup<ProjectileReturnState>(false);
        ComponentLookup<ProjectilePerfectCircleState> perfectCircleStateLookup = SystemAPI.GetComponentLookup<ProjectilePerfectCircleState>(false);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(false);
        ComponentLookup<PlayerProjectileDeathVfxConfig> projectileDeathVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerProjectileDeathVfxConfig>(true);
        ComponentLookup<EnemyProjectileDeathVfxConfig> enemyProjectileDeathVfxConfigLookup = SystemAPI.GetComponentLookup<EnemyProjectileDeathVfxConfig>(true);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        BufferLookup<ProjectileReturnPathPoint> returnPathLookup = SystemAPI.GetBufferLookup<ProjectileReturnPathPoint>(false);

        foreach ((RefRW<Projectile> projectile,
                  RefRO<ProjectileRuntimeState> runtimeState,
                  RefRW<LocalTransform> projectileTransform,
                  RefRO<ProjectileOwner> owner,
                  Entity projectileEntity) in SystemAPI.Query<RefRW<Projectile>, RefRO<ProjectileRuntimeState>, RefRW<LocalTransform>, RefRO<ProjectileOwner>>()
                                                      .WithAll<ProjectileActive>()
                                                      .WithEntityAccess())
        {
            bool reachedRange = projectile.ValueRO.MaxRange > 0f && runtimeState.ValueRO.TraveledDistance >= projectile.ValueRO.MaxRange;
            bool reachedLifetime = projectile.ValueRO.MaxLifetime > 0f && runtimeState.ValueRO.ElapsedLifetime >= projectile.ValueRO.MaxLifetime;
            ProjectileReturnState returnState = projectileReturnStateLookup.HasComponent(projectileEntity)
                ? projectileReturnStateLookup[projectileEntity]
                : default;
            bool completedReturn = returnState.Enabled != 0 && returnState.Phase == ProjectileReturnPhase.Completed;
            bool isOutboundReturnProjectile = returnState.Enabled != 0 && returnState.Phase == ProjectileReturnPhase.Outbound;
            bool waitingForOutboundPrerequisites = isOutboundReturnProjectile &&
                                                   returnState.OutboundHitCapacityExhausted != 0;
            bool naturalHitCapacityExhausted = isOutboundReturnProjectile &&
                                               (returnState.OutboundHitCapacityExhausted != 0 ||
                                                returnState.OutboundNaturalHitCapacityExhausted != 0);

            if (!reachedRange && !reachedLifetime && !completedReturn && !waitingForOutboundPrerequisites)
                continue;

            // Despawn-triggered split children originate at the natural outbound terminal point, even when the parent returns afterward.
            if (projectileSplitStateLookup.HasComponent(projectileEntity))
            {
                ProjectileSplitState projectileSplitState = projectileSplitStateLookup[projectileEntity];

                if (ProjectileSplitUtility.ShouldSplitOnDespawn(in projectileSplitState))
                {
                    ProjectileElementalPayload projectileElementalPayload = projectileElementalPayloadLookup.HasComponent(projectileEntity)
                        ? projectileElementalPayloadLookup[projectileEntity]
                        : default;
                    float currentScaleMultiplier = ResolveCurrentScaleMultiplier(projectileEntity,
                                                                                 projectileTransform.ValueRO.Scale,
                                                                                 in projectileBaseScaleLookup);
                    ProjectileSplitUtility.TryEnqueueSplitRequests(in projectile.ValueRO,
                                                                   in projectileSplitState,
                                                                   in projectileTransform.ValueRO,
                                                                   currentScaleMultiplier,
                                                                   in projectileElementalPayload,
                                                                   in owner.ValueRO,
                                                                   in returnState,
                                                                   ref shootRequestLookup);
                    projectileSplitState.CanSplit = 0;
                    projectileSplitStateLookup[projectileEntity] = projectileSplitState;
                }
            }

            if (returnState.Enabled != 0 && !completedReturn)
            {
                if (returnState.Phase != ProjectileReturnPhase.Outbound)
                    continue;

                if (!returnPathLookup.HasBuffer(projectileEntity))
                    continue;

                ProjectilePerfectCircleState perfectCircleState = perfectCircleStateLookup.HasComponent(projectileEntity)
                    ? perfectCircleStateLookup[projectileEntity]
                    : default;
                if (!ProjectileReturnRuntimeUtility.CanBeginReturn(in returnState,
                                                                    in perfectCircleState))
                {
                    continue;
                }

                Projectile projectileData = projectile.ValueRO;
                LocalTransform returningTransform = projectileTransform.ValueRO;
                DynamicBuffer<ProjectileReturnPathPoint> returnPath = returnPathLookup[projectileEntity];
                ProjectileReturnRuntimeUtility.BeginReturn(ref returnState,
                                                            ref projectileData,
                                                            ref perfectCircleState,
                                                             ref returningTransform,
                                                             returnPath,
                                                             naturalHitCapacityExhausted,
                                                             false);
                ProjectileActivationRecallRuntimeUtility.RegisterReady(owner.ValueRO.ShooterEntity,
                                                                        ref returnState,
                                                                        ref powerUpsStateLookup);
                projectileReturnStateLookup[projectileEntity] = returnState;
                perfectCircleStateLookup[projectileEntity] = perfectCircleState;
                projectileTransform.ValueRW = returningTransform;
                projectile.ValueRW = projectileData;
                continue;
            }

            if (ProjectileReturnVfxPolicyUtility.AllowsDeathVfx(owner.ValueRO.PoolPrefabEntity,
                                                                returnState.Enabled != 0,
                                                                in returnState.Config))
                ProjectileDeathVfxRuntimeUtility.TryEnqueue(ProjectileDeathVfxOccasion.RangeOrLifetime,
                                                            projectileEntity,
                                                            owner.ValueRO.ShooterEntity,
                                                            in projectileTransform.ValueRO,
                                                            in projectileContactStateLookup,
                                                            in projectileDeathVfxConfigLookup,
                                                            in enemyProjectileDeathVfxConfigLookup,
                                                            ref vfxRequestLookup);
            LocalTransform parkedTransform = projectileTransform.ValueRO;
            ProjectileActivationRecallRuntimeUtility.ReleaseOwnership(owner.ValueRO.ShooterEntity,
                                                                       ref returnState,
                                                                       ref powerUpsStateLookup);
            if (projectileReturnStateLookup.HasComponent(projectileEntity))
                projectileReturnStateLookup[projectileEntity] = returnState;
            ProjectilePoolUtility.DespawnToPool(projectileEntity,
                                                owner.ValueRO.ShooterEntity,
                                                owner.ValueRO.PoolPrefabEntity,
                                                ref parkedTransform,
                                                ref poolLookup,
                                                ref projectileActiveLookup);
            projectileTransform.ValueRW = parkedTransform;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves runtime projectile scale multiplier relative to cached base scale.
    /// </summary>
    /// <param name="projectileEntity">Projectile entity to evaluate.</param>
    /// <param name="currentScale">Current runtime transform scale.</param>
    /// <param name="projectileBaseScaleLookup">Lookup used to read base scale components.</param>
    /// <returns>Multiplier used by split spawn logic.</returns>
    private static float ResolveCurrentScaleMultiplier(Entity projectileEntity,
                                                       float currentScale,
                                                       in ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup)
    {
        if (!projectileBaseScaleLookup.HasComponent(projectileEntity))
            return math.max(0.01f, currentScale);

        float baseScale = math.max(0.0001f, projectileBaseScaleLookup[projectileEntity].Value);
        return math.max(0.01f, currentScale / baseScale);
    }
    #endregion

    #endregion

}
