using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Spawns bomb entities from activation requests and initializes fuse data.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerBombSpawnSystem : ISystem
{
    #region Constants
    private const float SpawnClearanceSkinWidth = 0.02f;
    private const float MinimumAnchorSweepDistanceSquared = 1e-6f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the request and physics-world dependencies needed to spawn bombs safely.
    /// </summary>
    /// <param name="state">System state used to register update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerBombSpawnRequest>();
    }

    /// <summary>
    /// Instantiates pending bomb requests and corrects their initial position against wall collision.
    /// </summary>
    /// <param name="state">System state used to access ECS data and play back spawn commands.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        bool hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out PhysicsWorldSingleton physicsWorldSingleton);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) &&
            worldLayersConfig.WallsLayerMask != 0)
        {
            wallsLayerMask = worldLayersConfig.WallsLayerMask;
        }

        foreach (DynamicBuffer<PlayerBombSpawnRequest> bombRequests in SystemAPI.Query<DynamicBuffer<PlayerBombSpawnRequest>>())
        {
            for (int requestIndex = 0; requestIndex < bombRequests.Length; requestIndex++)
            {
                PlayerBombSpawnRequest request = bombRequests[requestIndex];

                if (request.BombPrefabEntity == Entity.Null)
                    continue;

                float collisionRadius = math.max(0.01f, request.CollisionRadius);
                float3 spawnPosition = hasPhysicsWorld
                    ? ResolveSpawnPosition(in request,
                                           collisionRadius,
                                           in physicsWorldSingleton,
                                           wallsLayerMask,
                                           in transformLookup)
                    : request.Position;
                Entity bombEntity = commandBuffer.Instantiate(request.BombPrefabEntity);

                LocalTransform bombTransform = LocalTransform.FromPositionRotation(spawnPosition, request.Rotation);

                if (entityManager.HasComponent<LocalTransform>(request.BombPrefabEntity))
                    commandBuffer.SetComponent(bombEntity, bombTransform);
                else
                    commandBuffer.AddComponent(bombEntity, bombTransform);

                BombFuseState fuseState = new BombFuseState
                {
                    OwnerEntity = request.OwnerEntity,
                    Position = spawnPosition,
                    Velocity = request.Velocity,
                    CollisionRadius = collisionRadius,
                    BounceOnWalls = request.BounceOnWalls,
                    BounceDamping = math.clamp(request.BounceDamping, 0f, 1f),
                    LinearDampingPerSecond = math.max(0f, request.LinearDampingPerSecond),
                    FuseRemaining = math.max(0.05f, request.FuseSeconds),
                    Radius = math.max(0.1f, request.Radius),
                    Damage = math.max(0f, request.Damage),
                    AffectAllEnemiesInRadius = request.AffectAllEnemiesInRadius,
                    ExplosionVfxPrefabEntity = request.ExplosionVfxPrefabEntity,
                    ScaleVfxToRadius = request.ScaleVfxToRadius,
                    VfxScaleMultiplier = math.max(0.01f, request.VfxScaleMultiplier)
                };

                if (entityManager.HasComponent<BombFuseState>(request.BombPrefabEntity))
                    commandBuffer.SetComponent(bombEntity, fuseState);
                else
                    commandBuffer.AddComponent(bombEntity, fuseState);

                if (entityManager.HasComponent<BombExplodeRequest>(request.BombPrefabEntity))
                    commandBuffer.RemoveComponent<BombExplodeRequest>(bombEntity);
            }

            bombRequests.Clear();
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves a bomb spawn position that remains reachable from the owner and outside wall clearance.
    /// </summary>
    /// <param name="request">Bomb spawn request emitted by the active power-up execution path.</param>
    /// <param name="collisionRadius">Resolved bomb collision radius used for wall queries.</param>
    /// <param name="physicsWorldSingleton">Physics world used for wall sweep and clearance checks.</param>
    /// <param name="wallsLayerMask">Layer mask containing blocking walls.</param>
    /// <param name="transformLookup">Read-only transform lookup used to anchor the spawn to the owner when possible.</param>
    /// <returns>Corrected bomb spawn position.</returns>
    private static float3 ResolveSpawnPosition(in PlayerBombSpawnRequest request,
                                               float collisionRadius,
                                               in PhysicsWorldSingleton physicsWorldSingleton,
                                               int wallsLayerMask,
                                               in ComponentLookup<LocalTransform> transformLookup)
    {
        float3 spawnPosition = ResolveOwnerAnchoredSpawnPosition(in request,
                                                                 collisionRadius,
                                                                 in physicsWorldSingleton,
                                                                 wallsLayerMask,
                                                                 in transformLookup);

        if (WorldWallCollisionUtility.TryResolveMinimumClearance(in physicsWorldSingleton,
                                                                 spawnPosition,
                                                                 collisionRadius + SpawnClearanceSkinWidth,
                                                                 wallsLayerMask,
                                                                 out float3 correctionDisplacement,
                                                                 out float3 _))
        {
            spawnPosition += correctionDisplacement;
        }

        return spawnPosition;
    }

    /// <summary>
    /// Sweeps from the owner to the requested bomb position so offset spawns cannot pass through a wall.
    /// </summary>
    /// <param name="request">Bomb spawn request emitted by the active power-up execution path.</param>
    /// <param name="collisionRadius">Resolved bomb collision radius used for wall queries.</param>
    /// <param name="physicsWorldSingleton">Physics world used for wall sweep checks.</param>
    /// <param name="wallsLayerMask">Layer mask containing blocking walls.</param>
    /// <param name="transformLookup">Read-only transform lookup used to read the owner position.</param>
    /// <returns>Requested position or the closest reachable position before a blocking wall.</returns>
    private static float3 ResolveOwnerAnchoredSpawnPosition(in PlayerBombSpawnRequest request,
                                                            float collisionRadius,
                                                            in PhysicsWorldSingleton physicsWorldSingleton,
                                                            int wallsLayerMask,
                                                            in ComponentLookup<LocalTransform> transformLookup)
    {
        if (wallsLayerMask == 0)
            return request.Position;

        if (request.OwnerEntity == Entity.Null || !transformLookup.HasComponent(request.OwnerEntity))
            return request.Position;

        float3 ownerPosition = transformLookup[request.OwnerEntity].Position;
        float3 desiredDisplacement = request.Position - ownerPosition;

        if (math.lengthsq(desiredDisplacement) <= MinimumAnchorSweepDistanceSquared)
            return request.Position;

        if (!WorldWallCollisionUtility.TryResolveBlockedDisplacement(in physicsWorldSingleton,
                                                                     ownerPosition,
                                                                     desiredDisplacement,
                                                                     collisionRadius,
                                                                     wallsLayerMask,
                                                                     out float3 allowedDisplacement,
                                                                     out float3 _))
        {
            return request.Position;
        }

        return ownerPosition + allowedDisplacement;
    }
    #endregion

    #endregion
}
