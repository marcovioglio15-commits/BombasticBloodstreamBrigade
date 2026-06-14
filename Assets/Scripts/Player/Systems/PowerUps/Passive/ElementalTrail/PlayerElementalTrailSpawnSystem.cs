using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns elemental trail segment entities while players move with Elemental Trail passive enabled.
/// Each new gameplay segment also queues one pooled Particle System VFX request synchronised to the segment lifetime
/// so the visual "fire zone" stays in sync with the actual damage area and is released through the shared managed pool.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerElementalTrailSpawnSystem : ISystem
{
    #region Constants
    private const float MovementEpsilonSquared = 0.0001f;
    private const float MinimumVfxLifetimeSeconds = 0.05f;
    private const float MinimumVfxScale = 0.01f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers update requirements covering the Elemental Trail passive runtime payload and player transform.
    /// </summary>
    /// <param name="state">DOTS system state used to declare required component dependencies.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<PlayerElementalTrailState>();
        state.RequireForUpdate<PlayerElementalTrailSegmentElement>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<LocalTransform>();
    }

    /// <summary>
    /// Spawns missing gameplay segments and queues per-segment pooled VFX requests using the player attached-VFX prefab reference.
    /// </summary>
    /// <param name="state">DOTS system state providing delta time and entity access.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  RefRW<PlayerElementalTrailState> trailState,
                  RefRO<PlayerMovementState> movementState,
                  RefRO<LocalTransform> playerTransform,
                  DynamicBuffer<PlayerElementalTrailSegmentElement> trailSegments,
                  Entity playerEntity)
                 in SystemAPI.Query<DynamicBuffer<PlayerPassiveToolsStateElement>,
                                    RefRW<PlayerElementalTrailState>,
                                    RefRO<PlayerMovementState>,
                                    RefRO<LocalTransform>,
                                    DynamicBuffer<PlayerElementalTrailSegmentElement>>()
                             .WithEntityAccess())
        {
            PlayerElementalTrailState currentTrailState = trailState.ValueRO;
            PlayerPassiveToolsState passiveToolsState;
            PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer,
                                                      out passiveToolsState);
            CompactSegments(entityManager, trailSegments, ref currentTrailState);

            if (passiveToolsState.HasElementalTrail == 0)
            {
                currentTrailState.Initialized = 0;
                currentTrailState.SpawnTimer = 0f;
                trailState.ValueRW = currentTrailState;
                continue;
            }

            ElementalTrailPassiveConfig trailConfig = passiveToolsState.ElementalTrail;
            bool hasValidPayload = trailConfig.TrailRadius > 0f && trailConfig.StacksPerTick > 0f;

            if (!hasValidPayload)
            {
                trailState.ValueRW = currentTrailState;
                continue;
            }

            float3 playerPosition = playerTransform.ValueRO.Position;

            if (currentTrailState.Initialized == 0)
            {
                currentTrailState.Initialized = 1;
                currentTrailState.LastSpawnPosition = playerPosition;
                currentTrailState.SpawnTimer = 0f;
            }

            float nextSpawnTimer = currentTrailState.SpawnTimer - deltaTime;
            float3 delta = playerPosition - currentTrailState.LastSpawnPosition;
            delta.y = 0f;
            float movedDistance = math.length(delta);
            float3 planarVelocity = movementState.ValueRO.Velocity;
            planarVelocity.y = 0f;
            bool isMoving = math.lengthsq(planarVelocity) > MovementEpsilonSquared;

            if (!isMoving)
            {
                currentTrailState.LastSpawnPosition = playerPosition;
                currentTrailState.SpawnTimer = 0f;
                trailState.ValueRW = currentTrailState;
                continue;
            }

            bool distanceTriggered = trailConfig.TrailSpawnDistance > 0f && movedDistance >= trailConfig.TrailSpawnDistance;
            bool timerTriggered = nextSpawnTimer <= 0f;

            if (!distanceTriggered && !timerTriggered)
            {
                currentTrailState.SpawnTimer = nextSpawnTimer;
                trailState.ValueRW = currentTrailState;
                continue;
            }

            int maxSegments = math.max(1, trailConfig.MaxActiveSegmentsPerPlayer);

            // Evict the oldest gameplay segments so we never exceed the configured cap before pushing a new one.
            while (trailSegments.Length >= maxSegments)
            {
                Entity oldestSegment = trailSegments[0].SegmentEntity;
                trailSegments.RemoveAt(0);

                if (oldestSegment.Index >= 0 && entityManager.Exists(oldestSegment))
                    commandBuffer.DestroyEntity(oldestSegment);
            }

            float segmentLifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, trailConfig.TrailSegmentLifetimeSeconds);
            Entity segmentEntity = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(segmentEntity, LocalTransform.FromPositionRotationScale(playerPosition, quaternion.identity, 1f));
            commandBuffer.AddComponent(segmentEntity, new ElementalTrailSegment
            {
                OwnerEntity = playerEntity,
                Radius = math.max(0f, trailConfig.TrailRadius),
                RemainingLifetime = segmentLifetimeSeconds,
                ApplyIntervalSeconds = math.max(0.01f, trailConfig.ApplyIntervalSeconds),
                ApplyTimer = 0f,
                StacksPerTick = math.max(0f, trailConfig.StacksPerTick),
                Effect = trailConfig.Effect
            });

            commandBuffer.AppendToBuffer(playerEntity, new PlayerElementalTrailSegmentElement
            {
                SegmentEntity = segmentEntity
            });

            // Queue one pooled particle VFX synchronised to the new gameplay segment lifetime.
            EnqueueSegmentVfxRequest(ref state,
                                     playerEntity,
                                     in trailConfig,
                                     playerPosition,
                                     segmentLifetimeSeconds);

            currentTrailState.ActiveSegments = trailSegments.Length + 1;
            currentTrailState.LastSpawnPosition = playerPosition;
            currentTrailState.SpawnTimer = math.max(0.01f, trailConfig.TrailSpawnIntervalSeconds);

            trailState.ValueRW = currentTrailState;
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Removes gameplay trail segments that have already expired or whose backing entities became invalid since last frame.
    /// </summary>
    /// <param name="entityManager">Entity manager used to validate segment entities and read their remaining lifetime.</param>
    /// <param name="trailSegments">Per-player buffer of active gameplay trail segments to compact in place.</param>
    /// <param name="trailState">Mutable trail state receiving the compacted active segment count.</param>
    private static void CompactSegments(EntityManager entityManager,
                                        DynamicBuffer<PlayerElementalTrailSegmentElement> trailSegments,
                                        ref PlayerElementalTrailState trailState)
    {
        for (int index = 0; index < trailSegments.Length; index++)
        {
            Entity segmentEntity = trailSegments[index].SegmentEntity;

            if (segmentEntity == Entity.Null)
            {
                trailSegments.RemoveAt(index);
                index--;
                continue;
            }

            if (segmentEntity.Index < 0)
            {
                trailSegments.RemoveAt(index);
                index--;
                continue;
            }

            if (!entityManager.Exists(segmentEntity))
            {
                trailSegments.RemoveAt(index);
                index--;
                continue;
            }

            if (!entityManager.HasComponent<ElementalTrailSegment>(segmentEntity))
            {
                trailSegments.RemoveAt(index);
                index--;
                continue;
            }

            ElementalTrailSegment segment = entityManager.GetComponentData<ElementalTrailSegment>(segmentEntity);

            if (segment.RemainingLifetime > 0f)
                continue;

            trailSegments.RemoveAt(index);
            index--;
        }

        trailState.ActiveSegments = trailSegments.Length;
    }

    /// <summary>
    /// Resolves the per-player attached VFX prefab reference and pushes one pooled particle VFX request synchronised to a new gameplay segment.
    /// Falls back silently when no prefab has been assigned so the gameplay layer keeps working without a visual.
    /// </summary>
    /// <param name="state">DOTS system state required by the SystemAPI source generator to flow handles through helper calls.</param>
    /// <param name="playerEntity">Player entity owning the VFX request buffer and the prefab reference.</param>
    /// <param name="trailConfig">Resolved Elemental Trail config providing per-segment offset, scale and lifetime.</param>
    /// <param name="spawnPosition">World-space player position at segment emission time.</param>
    /// <param name="segmentLifetimeSeconds">Configured gameplay segment lifetime in seconds, used as the VFX lifetime.</param>
    private static void EnqueueSegmentVfxRequest(ref SystemState state,
                                                 Entity playerEntity,
                                                 in ElementalTrailPassiveConfig trailConfig,
                                                 float3 spawnPosition,
                                                 float segmentLifetimeSeconds)
    {
        EntityManager entityManager = state.EntityManager;

        if (!entityManager.HasBuffer<PlayerPowerUpVfxSpawnRequest>(playerEntity))
            return;

        UnityObjectRef<UnityEngine.GameObject> prefabReference = ResolveAttachedPrefabReference(entityManager, playerEntity);
        bool hasBakedPrefabEntity = trailConfig.TrailAttachedVfxPrefabEntity != Entity.Null;
        bool hasUnityObjectPrefab = prefabReference.Value != null;

        if (!hasBakedPrefabEntity && !hasUnityObjectPrefab)
            return;

        float uniformScale = math.max(MinimumVfxScale, trailConfig.TrailAttachedVfxScaleMultiplier);
        float3 worldPosition = spawnPosition + trailConfig.TrailAttachedVfxOffset;

        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = entityManager.GetBuffer<PlayerPowerUpVfxSpawnRequest>(playerEntity);
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = trailConfig.TrailAttachedVfxPrefabEntity,
            SourcePrefab = prefabReference,
            Position = worldPosition,
            Rotation = quaternion.identity,
            UniformScale = uniformScale,
            LifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, segmentLifetimeSeconds),
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero,
            DetachWhenFollowTargetInvalid = 0
        });
    }

    /// <summary>
    /// Reads the optional attached VFX prefab reference baked on the player entity.
    /// Returning a default UnityObjectRef is fine; the caller will then rely on the baked prefab entity binding instead.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect the player entity components.</param>
    /// <param name="playerEntity">Player entity expected to carry the attached VFX prefab reference component.</param>
    /// <returns>Burst-safe UnityObjectRef to the authored GameObject prefab, or default when the reference is missing.</returns>
    private static UnityObjectRef<UnityEngine.GameObject> ResolveAttachedPrefabReference(EntityManager entityManager,
                                                                                          Entity playerEntity)
    {
        if (!entityManager.HasComponent<PlayerElementalTrailAttachedVfxPrefabReference>(playerEntity))
            return default;

        PlayerElementalTrailAttachedVfxPrefabReference prefabReference = entityManager.GetComponentData<PlayerElementalTrailAttachedVfxPrefabReference>(playerEntity);
        return prefabReference.Prefab;
    }
    #endregion

    #endregion
}
