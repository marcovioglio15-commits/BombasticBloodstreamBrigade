using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Simulates bomb planar movement with optional wall bouncing until fuse expiration.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerBombSpawnSystem))]
[UpdateBefore(typeof(PlayerBombFuseSystem))]
public partial struct PlayerBombMovementSystem : ISystem
{
    #region Constants
    private const float MovementEpsilon = 1e-6f;
    private const float VelocityStopThresholdSquared = 1e-4f;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BombFuseState>();
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
        state.EntityManager.CompleteDependencyBeforeRW<LocalTransform>();
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);

        foreach ((RefRW<BombFuseState> fuseState,
                  Entity bombEntity) in SystemAPI.Query<RefRW<BombFuseState>>().WithEntityAccess())
        {
            float3 position = fuseState.ValueRO.Position;
            float3 velocity = fuseState.ValueRO.Velocity;
            float3 displacement = velocity * deltaTime;
            bool hasMovement = math.lengthsq(displacement) > MovementEpsilon;
            bool hitWall = false;
            float3 wallNormal = float3.zero;

            if (hasMovement)
            {
                if (wallsEnabled)
                {
                    float collisionRadius = math.max(0.01f, fuseState.ValueRO.CollisionRadius);
                    hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                     position,
                                                                                     displacement,
                                                                                     collisionRadius,
                                                                                     wallsLayerMask,
                                                                                     out float3 allowedDisplacement,
                                                                                     out wallNormal);
                    position += allowedDisplacement;
                }
                else
                {
                    position += displacement;
                }
            }

            if (hitWall)
            {
                if (fuseState.ValueRO.BounceOnWalls != 0)
                {
                    float3 normalizedNormal = math.normalizesafe(wallNormal, new float3(0f, 0f, 1f));
                    float3 reflectedVelocity = math.reflect(velocity, normalizedNormal);
                    float bounceDamping = math.clamp(fuseState.ValueRO.BounceDamping, 0f, 1f);
                    velocity = reflectedVelocity * bounceDamping;
                }
                else
                {
                    velocity = float3.zero;
                }
            }

            float dampingPerSecond = math.max(0f, fuseState.ValueRO.LinearDampingPerSecond);

            if (dampingPerSecond > 0f && math.lengthsq(velocity) > MovementEpsilon)
            {
                float dampingFactor = math.max(0f, 1f - dampingPerSecond * deltaTime);
                velocity *= dampingFactor;
            }

            if (math.lengthsq(velocity) < VelocityStopThresholdSquared)
                velocity = float3.zero;

            fuseState.ValueRW.Position = position;
            fuseState.ValueRW.Velocity = velocity;

            if (transformLookup.HasComponent(bombEntity))
            {
                LocalTransform bombTransform = transformLookup[bombEntity];
                bombTransform.Position = position;

                if (math.lengthsq(velocity) > MovementEpsilon)
                {
                    float3 forward = math.normalizesafe(new float3(velocity.x, 0f, velocity.z), math.forward(bombTransform.Rotation));
                    bombTransform.Rotation = quaternion.LookRotationSafe(forward, new float3(0f, 1f, 0f));
                }

                transformLookup[bombEntity] = bombTransform;
            }
        }
    }
    #endregion

    #endregion
}
