using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Applies resolved player movement velocity to the transform while enforcing wall collision and dash-specific wall bounce response.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementSpeedSystem))]
public partial struct PlayerMovementApplySystem : ISystem
{
    #region Constants
    private const float PlayerCollisionRadius = 0.35f;
    private const float DashBounceDirectionEpsilon = 1e-6f;
    private const float DashMaximumSoftBounceCoefficient = 0.82f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares movement, transform, runtime movement config and physics world requirements for player movement application.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PlayerRuntimeMovementConfig>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Applies velocity-driven displacement, resolves wall blocking, and redirects active dash motion after wall hits.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (PlayerGameplayPauseUtility.IsPlayerMotionHardPauseActive())
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        ComponentLookup<PlayerDashState> dashLookup = SystemAPI.GetComponentLookup<PlayerDashState>(false);
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();
        bool canQueryScenePhysics = !GameSceneTransitionRuntimeGuardUtility.ShouldBlockDefaultWorldPhysicsQueries();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) &&
            worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        // Single-slot traversal keeps transform integration live while scene collider blobs are replaced. Wall
        // clearance resumes only after the target physics-readiness barrier has exported a safe world.
        bool wallsEnabled = wallsLayerMask != 0 && canQueryScenePhysics;

        foreach ((RefRW<LocalTransform> localTransform,
                  RefRW<PlayerMovementState> movementState,
                  RefRO<PlayerRuntimeMovementConfig> runtimeMovementConfig,
                  RefRO<PlayerInputState> inputState,
                  Entity entity) in SystemAPI.Query<RefRW<LocalTransform>,
                                                    RefRW<PlayerMovementState>,
                                                    RefRO<PlayerRuntimeMovementConfig>,
                                                    RefRO<PlayerInputState>>()
                                                .WithEntityAccess())
        {
            if (inputState.ValueRO.SuppressMotionIntegration != 0)
                continue;

            float wallCollisionSkinWidth = math.max(0f, runtimeMovementConfig.ValueRO.Values.WallCollisionSkinWidth);
            float3 currentPosition = localTransform.ValueRO.Position;
            float3 resolvedPosition = currentPosition;
            float3 resolvedVelocity = movementState.ValueRO.Velocity;
            float3 displacement = resolvedVelocity * deltaTime;
            bool hasDisplacement = math.lengthsq(displacement) >= 1e-8f;

            if (wallsEnabled)
            {
                if (hasDisplacement)
                {
                    bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                           currentPosition,
                                                                                           displacement,
                                                                                           PlayerCollisionRadius,
                                                                                           wallsLayerMask,
                                                                                           wallCollisionSkinWidth,
                                                                                           out float3 allowedDisplacement,
                                                                                           out float3 hitNormal);
                    resolvedPosition = currentPosition + allowedDisplacement;

                    if (hitWall)
                    {
                        if (!TryApplyDashWallBounce(entity,
                                                    hitNormal,
                                                    ref resolvedVelocity,
                                                    ref dashLookup))
                        {
                            float wallBounceCoefficient = runtimeMovementConfig.ValueRO.Values.WallBounceCoefficient;
                            resolvedVelocity = WorldWallCollisionUtility.ComputeBounceVelocity(resolvedVelocity, hitNormal, wallBounceCoefficient);
                        }
                    }
                }

                float minimumClearanceDistance = PlayerCollisionRadius + wallCollisionSkinWidth;

                for (int solverIteration = 0; solverIteration < 2; solverIteration++)
                {
                    bool hasCorrection = WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                                              resolvedPosition,
                                                                                              minimumClearanceDistance,
                                                                                              wallsLayerMask,
                                                                                              out float3 correctionDisplacement,
                                                                                              out float3 correctionNormal);

                    if (!hasCorrection)
                        break;

                    resolvedPosition += correctionDisplacement;
                    resolvedVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(resolvedVelocity, correctionNormal);
                }

                localTransform.ValueRW.Position = resolvedPosition;
                movementState.ValueRW.Velocity = resolvedVelocity;

                continue;
            }

            if (hasDisplacement)
                localTransform.ValueRW.Position = currentPosition + displacement;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Applies dash-specific wall bounce and redirects the active dash for following frames.
    /// </summary>
    /// <param name="entity">Player entity that may own a dash state.</param>
    /// <param name="hitNormal">Wall surface normal returned by the sweep collision.</param>
    /// <param name="resolvedVelocity">Mutable velocity after collision response.</param>
    /// <param name="dashLookup">Mutable lookup used to read and write PlayerDashState.</param>
    /// <returns>True when an active dash consumed the wall collision response.</returns>
    private static bool TryApplyDashWallBounce(Entity entity,
                                               float3 hitNormal,
                                               ref float3 resolvedVelocity,
                                               ref ComponentLookup<PlayerDashState> dashLookup)
    {
        if (!dashLookup.HasComponent(entity))
            return false;

        PlayerDashState dashState = dashLookup[entity];

        if (dashState.IsDashing == 0)
            return false;

        float bounceIntensity = math.clamp(dashState.WallBounceIntensity, 0f, 1f);
        float softenedBounceCoefficient = ResolveSoftenedDashBounceCoefficient(bounceIntensity);
        float3 bouncedVelocity = WorldWallCollisionUtility.ComputeBounceVelocity(resolvedVelocity,
                                                                                 hitNormal,
                                                                                 softenedBounceCoefficient);
        float3 bouncedPlanarVelocity = bouncedVelocity;
        bouncedPlanarVelocity.y = 0f;

        if (math.lengthsq(bouncedPlanarVelocity) <= DashBounceDirectionEpsilon)
        {
            resolvedVelocity = float3.zero;
            StopDashAfterWallImpact(ref dashState);
            dashLookup[entity] = dashState;
            return true;
        }

        dashState.Direction = math.normalizesafe(bouncedPlanarVelocity, dashState.Direction);
        dashLookup[entity] = dashState;
        resolvedVelocity = bouncedVelocity;
        return true;
    }

    /// <summary>
    /// Converts the authored dash bounce intensity into a softer physical response for wall impacts.
    /// </summary>
    /// <param name="bounceIntensity">Authored intensity in the normalized [0..1] range.</param>
    /// <returns>Smoothed bounce coefficient consumed by the shared wall utility.</returns>
    private static float ResolveSoftenedDashBounceCoefficient(float bounceIntensity)
    {
        float clampedIntensity = math.saturate(bounceIntensity);
        float smoothedIntensity = clampedIntensity * clampedIntensity * (3f - 2f * clampedIntensity);
        return smoothedIntensity * DashMaximumSoftBounceCoefficient;
    }

    /// <summary>
    /// Stops dash motion after a non-bouncing wall impact while preserving already-authored invulnerability timers.
    /// </summary>
    /// <param name="dashState">Mutable dash state to stop.</param>
    private static void StopDashAfterWallImpact(ref PlayerDashState dashState)
    {
        dashState.IsDashing = 0;
        dashState.ClearVelocityAfterApply = 1;
        dashState.Phase = 0;
        dashState.PhaseRemaining = 0f;
        dashState.HoldDuration = 0f;
        dashState.Duration = 0f;
        dashState.Distance = 0f;
        dashState.ElapsedDuration = 0f;
        dashState.Direction = float3.zero;
        dashState.EntryVelocity = float3.zero;
        dashState.Speed = 0f;
        dashState.TransitionInDuration = 0f;
        dashState.TransitionOutDuration = 0f;
        dashState.WallBounceIntensity = 0f;
    }
    #endregion

    #endregion

}
