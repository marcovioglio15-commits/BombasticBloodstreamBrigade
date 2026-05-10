using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementSpeedSystem))]
public partial struct PlayerMovementApplySystem : ISystem
{
    #region Constants
    private const float PlayerCollisionRadius = 0.35f;
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PlayerRuntimeMovementConfig>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (PlayerGameplayPauseUtility.IsHardGameplayPauseActive())
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        bool wallsEnabled = wallsLayerMask != 0;

        foreach ((RefRW<LocalTransform> localTransform,
                  RefRW<PlayerMovementState> movementState,
                  RefRO<PlayerRuntimeMovementConfig> runtimeMovementConfig) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<PlayerMovementState>, RefRO<PlayerRuntimeMovementConfig>>())
        {
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
                        float wallBounceCoefficient = runtimeMovementConfig.ValueRO.Values.WallBounceCoefficient;
                        resolvedVelocity = WorldWallCollisionUtility.ComputeBounceVelocity(resolvedVelocity, hitNormal, wallBounceCoefficient);
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


}
