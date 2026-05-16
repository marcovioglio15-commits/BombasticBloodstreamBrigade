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

        foreach ((RefRO<Projectile> projectile,
                  RefRO<ProjectileRuntimeState> runtimeState,
                  RefRW<LocalTransform> projectileTransform,
                  RefRO<ProjectileOwner> owner,
                  Entity projectileEntity) in SystemAPI.Query<RefRO<Projectile>, RefRO<ProjectileRuntimeState>, RefRW<LocalTransform>, RefRO<ProjectileOwner>>()
                                                      .WithAll<ProjectileActive>()
                                                      .WithEntityAccess())
        {
            bool reachedRange = projectile.ValueRO.MaxRange > 0f && runtimeState.ValueRO.TraveledDistance >= projectile.ValueRO.MaxRange;
            bool reachedLifetime = projectile.ValueRO.MaxLifetime > 0f && runtimeState.ValueRO.ElapsedLifetime >= projectile.ValueRO.MaxLifetime;

            if (reachedRange == false && reachedLifetime == false)
                continue;

            if (projectileSplitStateLookup.HasComponent(projectileEntity))
            {
                ProjectileSplitState projectileSplitState = projectileSplitStateLookup[projectileEntity];

                if (ProjectileSplitUtility.ShouldSplitOnDespawn(in projectileSplitState))
                {
                    ProjectileElementalPayload projectileElementalPayload = default(ProjectileElementalPayload);

                    if (projectileElementalPayloadLookup.HasComponent(projectileEntity))
                        projectileElementalPayload = projectileElementalPayloadLookup[projectileEntity];

                    float currentScaleMultiplier = ResolveCurrentScaleMultiplier(projectileEntity,
                                                                                 projectileTransform.ValueRO.Scale,
                                                                                 in projectileBaseScaleLookup);
                    ProjectileSplitUtility.TryEnqueueSplitRequests(in projectile.ValueRO,
                                                                   in projectileSplitState,
                                                                   in projectileTransform.ValueRO,
                                                                   currentScaleMultiplier,
                                                                   in projectileElementalPayload,
                                                                   in owner.ValueRO,
                                                                   ref shootRequestLookup);
                    projectileSplitState.CanSplit = 0;
                    projectileSplitStateLookup[projectileEntity] = projectileSplitState;
                }
            }

            LocalTransform parkedTransform = projectileTransform.ValueRO;
            ProjectilePoolUtility.DespawnToPool(projectileEntity,
                                                owner.ValueRO.ShooterEntity,
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
        if (projectileBaseScaleLookup.HasComponent(projectileEntity) == false)
            return math.max(0.01f, currentScale);

        float baseScale = math.max(0.0001f, projectileBaseScaleLookup[projectileEntity].Value);
        return math.max(0.01f, currentScale / baseScale);
    }
    #endregion

    #endregion

}
