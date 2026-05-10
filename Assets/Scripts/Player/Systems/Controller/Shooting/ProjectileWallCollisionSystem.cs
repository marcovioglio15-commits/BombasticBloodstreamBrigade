using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Despawns active projectiles when they hit wall colliders on the configured wall layer.
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
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Projectile>();
        state.RequireForUpdate<ProjectileOwner>();
        state.RequireForUpdate<ProjectilePerfectCircleState>();
        state.RequireForUpdate<ProjectileActive>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (PlayerGameplayPauseUtility.IsHardGameplayPauseActive())
            return;

        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        if (wallsLayerMask == 0)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        CollisionFilter wallsCollisionFilter = WorldWallCollisionUtility.BuildWallsCollisionFilter(wallsLayerMask);
        BufferLookup<ProjectilePoolElement> poolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<ShootRequest> shootRequestLookup = SystemAPI.GetBufferLookup<ShootRequest>(false);
        ComponentLookup<PlayerMovementState> movementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);
        ComponentLookup<ProjectileBounceState> projectileBounceStateLookup = SystemAPI.GetComponentLookup<ProjectileBounceState>(false);
        ComponentLookup<ProjectileSplitState> projectileSplitStateLookup = SystemAPI.GetComponentLookup<ProjectileSplitState>(false);
        ComponentLookup<ProjectileElementalPayload> projectileElementalPayloadLookup = SystemAPI.GetComponentLookup<ProjectileElementalPayload>(true);
        ComponentLookup<ProjectileActive> projectileActiveLookup = SystemAPI.GetComponentLookup<ProjectileActive>(false);

        foreach ((RefRW<Projectile> projectile,
                  RefRO<ProjectileOwner> owner,
                  RefRO<ProjectilePerfectCircleState> perfectCircleState,
                  RefRW<LocalTransform> projectileTransform,
                  Entity projectileEntity) in SystemAPI.Query<RefRW<Projectile>, RefRO<ProjectileOwner>, RefRO<ProjectilePerfectCircleState>, RefRW<LocalTransform>>()
                                                       .WithAll<ProjectileActive>()
                                                       .WithEntityAccess())
        {
            Projectile projectileData = projectile.ValueRO;
            float3 velocity = projectileData.Velocity;

            if (perfectCircleState.ValueRO.Enabled == 0 && projectileData.InheritPlayerSpeed != 0)
                velocity += ResolveInheritedVelocity(in projectileData, in owner.ValueRO, in movementStateLookup);

            float3 displacement = velocity * deltaTime;

            if (math.lengthsq(displacement) <= MovementEpsilon)
                continue;

            float3 endPosition = projectileTransform.ValueRO.Position;
            float3 startPosition = endPosition - displacement;
            float projectileScale = math.max(0.01f, projectileTransform.ValueRO.Scale);
            float collisionRadius = math.max(0.005f, BaseProjectileCollisionRadius * projectileScale);
            bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                   startPosition,
                                                                                   displacement,
                                                                                   collisionRadius,
                                                                                   wallsCollisionFilter,
                                                                                   out float3 allowedDisplacement,
                                                                                   out float3 wallNormal);

            if (!hitWall)
                continue;

            float3 resolvedPosition = startPosition + allowedDisplacement;
            LocalTransform resolvedTransform = projectileTransform.ValueRO;
            resolvedTransform.Position = resolvedPosition;
            projectileTransform.ValueRW = resolvedTransform;

            if (projectileBounceStateLookup.HasComponent(projectileEntity))
            {
                ProjectileBounceState projectileBounceState = projectileBounceStateLookup[projectileEntity];

                if (TryApplyBounce(ref projectileData, ref projectileBounceState, wallNormal))
                {
                    projectile.ValueRW = projectileData;
                    projectileBounceStateLookup[projectileEntity] = projectileBounceState;
                    continue;
                }
            }

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
                    ProjectileSplitUtility.TryEnqueueSplitRequests(in projectileData,
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
    private static float3 ResolveInheritedVelocity(in Projectile projectile,
                                                   in ProjectileOwner owner,
                                                   in ComponentLookup<PlayerMovementState> movementStateLookup)
    {
        if (!movementStateLookup.HasComponent(owner.ShooterEntity))
            return float3.zero;

        float3 inheritedVelocity = movementStateLookup[owner.ShooterEntity].Velocity;
        float speedSquared = math.lengthsq(projectile.Velocity);

        if (speedSquared <= 1e-6f)
            return inheritedVelocity;

        float projectionScale = math.dot(inheritedVelocity, projectile.Velocity) / speedSquared;
        inheritedVelocity -= projectile.Velocity * projectionScale;
        return inheritedVelocity;
    }

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
