using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Resolves wall contacts for active projectiles, including bounce, outbound return transition, split, and terminal pooling.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(ProjectileSimulationSystem))]
[UpdateBefore(typeof(ProjectileDespawnSystem))]
public partial struct ProjectileWallCollisionSystem : ISystem
{
    #region Constants
    private const float BaseProjectileCollisionRadius = 0.05f;
    private const float MovementEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures the component and physics-world requirements used by wall resolution.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Projectile>();
        state.RequireForUpdate<ProjectileOwner>();
        state.RequireForUpdate<ProjectilePerfectCircleState>();
        state.RequireForUpdate<ProjectileActive>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Resolves swept projectile contacts against configured wall layers after projectile movement.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (PlayerGameplayPauseUtility.IsPlayerCombatHardPauseActive())
            return;

        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        if (wallsLayerMask == 0)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        float enemyTimeScale = 1f;
        float playerProjectileTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
        {
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);
            playerProjectileTimeScale = math.clamp(enemyGlobalTimeScale.PlayerProjectileScale, 0f, 1f);
        }

        CollisionFilter wallsCollisionFilter = WorldWallCollisionUtility.BuildWallsCollisionFilter(wallsLayerMask);
        BufferLookup<ProjectilePoolElement> poolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<ShootRequest> shootRequestLookup = SystemAPI.GetBufferLookup<ShootRequest>(false);
        ComponentLookup<PlayerMovementState> movementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<EnemyData> enemyDataLookup = SystemAPI.GetComponentLookup<EnemyData>(true);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);
        ComponentLookup<ProjectileBounceState> projectileBounceStateLookup = SystemAPI.GetComponentLookup<ProjectileBounceState>(false);
        ComponentLookup<ProjectileSplitState> projectileSplitStateLookup = SystemAPI.GetComponentLookup<ProjectileSplitState>(false);
        ComponentLookup<ProjectileElementalPayload> projectileElementalPayloadLookup = SystemAPI.GetComponentLookup<ProjectileElementalPayload>(true);
        ComponentLookup<ProjectileActive> projectileActiveLookup = SystemAPI.GetComponentLookup<ProjectileActive>(false);
        ComponentLookup<ProjectileContactState> projectileContactStateLookup = SystemAPI.GetComponentLookup<ProjectileContactState>(true);
        ComponentLookup<ProjectileReturnState> projectileReturnStateLookup = SystemAPI.GetComponentLookup<ProjectileReturnState>(false);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(false);
        ComponentLookup<PlayerProjectileDeathVfxConfig> projectileDeathVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerProjectileDeathVfxConfig>(true);
        ComponentLookup<EnemyProjectileDeathVfxConfig> enemyProjectileDeathVfxConfigLookup = SystemAPI.GetComponentLookup<EnemyProjectileDeathVfxConfig>(true);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        BufferLookup<ProjectileReturnPathPoint> returnPathLookup = SystemAPI.GetBufferLookup<ProjectileReturnPathPoint>(false);

        foreach ((RefRW<Projectile> projectile,
                  RefRO<ProjectileOwner> owner,
                  RefRW<ProjectilePerfectCircleState> perfectCircleState,
                  RefRW<LocalTransform> projectileTransform,
                  Entity projectileEntity) in SystemAPI.Query<RefRW<Projectile>, RefRO<ProjectileOwner>, RefRW<ProjectilePerfectCircleState>, RefRW<LocalTransform>>()
                                                       .WithAll<ProjectileActive>()
                                                       .WithEntityAccess())
        {
            Projectile projectileData = projectile.ValueRO;
            ProjectileOwner projectileOwner = owner.ValueRO;
            ProjectileReturnState returnState = projectileReturnStateLookup.HasComponent(projectileEntity)
                ? projectileReturnStateLookup[projectileEntity]
                : default;

            if (returnState.Enabled != 0 && returnState.Phase != ProjectileReturnPhase.Outbound)
                continue;
            float projectileDeltaTime = ProjectileKinematicsUtility.ResolveOwnerScaledDeltaTime(in projectileOwner,
                                                                                                in enemyDataLookup,
                                                                                                deltaTime,
                                                                                                enemyTimeScale,
                                                                                                playerProjectileTimeScale);
            float3 displacement = perfectCircleState.ValueRO.Enabled != 0
                ? projectileData.Velocity * projectileDeltaTime
                : ProjectileKinematicsUtility.ResolveLinearDisplacement(in projectileData,
                                                                        in projectileOwner,
                                                                        in movementStateLookup,
                                                                        projectileDeltaTime);

            if (math.lengthsq(displacement) <= MovementEpsilon)
                continue;

            float3 endPosition = projectileTransform.ValueRO.Position;
            float3 startPosition = endPosition - displacement;
            float projectileScale = math.max(0.01f, projectileTransform.ValueRO.Scale);
            float prefabPlanarRadius = returnState.Enabled != 0
                ? math.max(BaseProjectileCollisionRadius, returnState.Config.ReplacementProjectilePlanarRadius)
                : BaseProjectileCollisionRadius;
            float collisionRadius = math.max(0.005f, prefabPlanarRadius * projectileScale);
            bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                   startPosition,
                                                                                   displacement,
                                                                                   collisionRadius,
                                                                                   wallsCollisionFilter,
                                                                                   out float3 allowedDisplacement,
                                                                                   out float3 wallNormal);

            if (!hitWall)
                continue;

            // Enemy-hit continuation never bypasses room physics; every wall resolves a real contact before bounce or return.
            float3 resolvedPosition = startPosition + allowedDisplacement;
            LocalTransform resolvedTransform = projectileTransform.ValueRO;
            resolvedTransform.Position = resolvedPosition;
            projectileTransform.ValueRW = resolvedTransform;

            if (returnState.Enabled != 0 &&
                returnState.Config.ReturnPathMode == ProjectileReturnPathMode.RetraceOutboundPath &&
                returnPathLookup.HasBuffer(projectileEntity))
            {
                ProjectileReturnRuntimeUtility.RecordOutboundPoint(returnPathLookup[projectileEntity],
                                                                   resolvedPosition,
                                                                   math.max(0.01f, returnState.Config.PathSampleDistance),
                                                                   true);
            }

            if (projectileBounceStateLookup.HasComponent(projectileEntity))
            {
                ProjectileBounceState projectileBounceState = projectileBounceStateLookup[projectileEntity];
                bool consumesBounceBeforeReturn = returnState.Enabled == 0 ||
                                                  ProjectileReturnPowerUpInteractionUtility.CompletesBouncesBeforeReturn(in returnState.Config);

                if (consumesBounceBeforeReturn &&
                    TryApplyBounce(ref projectileData, ref projectileBounceState, wallNormal))
                {
                    ProjectileReturnRuntimeUtility.AlignFlightRotation(ref resolvedTransform,
                                                                        ref returnState,
                                                                        projectileData.Velocity,
                                                                        0f);
                    projectile.ValueRW = projectileData;
                    projectileTransform.ValueRW = resolvedTransform;
                    projectileBounceStateLookup[projectileEntity] = projectileBounceState;

                    if (projectileReturnStateLookup.HasComponent(projectileEntity))
                        projectileReturnStateLookup[projectileEntity] = returnState;

                    continue;
                }
            }

            // A terminal wall remains the authored despawn point for Projectile Split even when the source projectile returns.
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
                    ProjectileSplitUtility.TryEnqueueSplitRequests(in projectileData,
                                                                   in projectileSplitState,
                                                                   in projectileTransform.ValueRO,
                                                                   currentScaleMultiplier,
                                                                   in projectileElementalPayload,
                                                                   in projectileOwner,
                                                                   in returnState,
                                                                   ref shootRequestLookup);
                    projectileSplitState.CanSplit = 0;
                    projectileSplitStateLookup[projectileEntity] = projectileSplitState;
                }
            }

            if (returnState.Enabled != 0 && returnPathLookup.HasBuffer(projectileEntity))
            {
                ProjectilePerfectCircleState mutablePerfectCircleState = perfectCircleState.ValueRO;

                // Keep orbital projectiles alive at the contact point until a compatible full-orbit prerequisite completes.
                if (!ProjectileReturnRuntimeUtility.CanBeginReturn(in returnState,
                                                                    in mutablePerfectCircleState))
                {
                    continue;
                }

                LocalTransform returningTransform = projectileTransform.ValueRO;
                ProjectileReturnRuntimeUtility.BeginReturn(ref returnState,
                                                            ref projectileData,
                                                            ref mutablePerfectCircleState,
                                                            ref returningTransform,
                                                             returnPathLookup[projectileEntity],
                                                             returnState.OutboundHitCapacityExhausted != 0 ||
                                                             returnState.OutboundNaturalHitCapacityExhausted != 0,
                                                             false);
                ProjectileActivationRecallRuntimeUtility.RegisterReady(projectileOwner.ShooterEntity,
                                                                        ref returnState,
                                                                        ref powerUpsStateLookup);
                projectile.ValueRW = projectileData;
                perfectCircleState.ValueRW = mutablePerfectCircleState;
                projectileTransform.ValueRW = returningTransform;
                projectileReturnStateLookup[projectileEntity] = returnState;
                continue;
            }

            if (ProjectileReturnVfxPolicyUtility.AllowsDeathVfx(projectileOwner.PoolPrefabEntity,
                                                                returnState.Enabled != 0,
                                                                in returnState.Config))
                ProjectileDeathVfxRuntimeUtility.TryEnqueue(ProjectileDeathVfxOccasion.TerminalWallHit,
                                                            projectileEntity,
                                                            projectileOwner.ShooterEntity,
                                                            in projectileTransform.ValueRO,
                                                            in projectileContactStateLookup,
                                                            in projectileDeathVfxConfigLookup,
                                                            in enemyProjectileDeathVfxConfigLookup,
                                                            ref vfxRequestLookup);
            LocalTransform parkedTransform = projectileTransform.ValueRO;
            ProjectileActivationRecallRuntimeUtility.ReleaseOwnership(projectileOwner.ShooterEntity,
                                                                       ref returnState,
                                                                       ref powerUpsStateLookup);

            if (projectileReturnStateLookup.HasComponent(projectileEntity))
                projectileReturnStateLookup[projectileEntity] = returnState;

            ProjectilePoolUtility.DespawnToPool(projectileEntity,
                                                projectileOwner.ShooterEntity,
                                                projectileOwner.PoolPrefabEntity,
                                                ref parkedTransform,
                                                ref poolLookup,
                                                ref projectileActiveLookup);
            projectileTransform.ValueRW = parkedTransform;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Reflects projectile velocity and consumes one configured bounce when the contact normal is usable.
    /// </summary>
    /// <param name="projectile">Mutable projectile velocity.</param>
    /// <param name="bounceState">Mutable bounce budget and speed scaling.</param>
    /// <param name="wallNormal">Resolved wall contact normal.</param>
    /// <returns>True when a bounce was applied.</returns>
    private static bool TryApplyBounce(ref Projectile projectile, ref ProjectileBounceState bounceState, float3 wallNormal)
    {
        if (bounceState.RemainingBounces <= 0)
            return false;

        float3 normalizedNormal = math.normalizesafe(wallNormal, float3.zero);

        if (math.lengthsq(normalizedNormal) <= MovementEpsilon)
            return false;

        float3 reflectedVelocity = math.reflect(projectile.Velocity, normalizedNormal);

        if (math.lengthsq(reflectedVelocity) <= MovementEpsilon)
            return false;

        float oldMultiplier = bounceState.CurrentSpeedMultiplier;

        if (oldMultiplier <= 0f)
            oldMultiplier = 1f;

        float multiplierStep = 1f + bounceState.SpeedPercentChangePerBounce * 0.01f;
        float minimumMultiplier = math.max(0f, bounceState.MinimumSpeedMultiplierAfterBounce);
        float maximumMultiplier = math.max(minimumMultiplier, bounceState.MaximumSpeedMultiplierAfterBounce);
        float nextMultiplier = math.clamp(oldMultiplier * multiplierStep, minimumMultiplier, maximumMultiplier);
        float speedRatio = oldMultiplier > 1e-6f ? nextMultiplier / oldMultiplier : 1f;
        projectile.Velocity = reflectedVelocity * speedRatio;
        bounceState.CurrentSpeedMultiplier = nextMultiplier;
        bounceState.RemainingBounces--;
        return true;
    }

    /// <summary>
    /// Resolves current projectile scale relative to its prefab-specific cached base scale.
    /// </summary>
    /// <param name="projectileEntity">Projectile entity to inspect.</param>
    /// <param name="currentScale">Current transform scale.</param>
    /// <param name="projectileBaseScaleLookup">Read-only cached base-scale lookup.</param>
    /// <returns>Positive current scale multiplier.</returns>
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
