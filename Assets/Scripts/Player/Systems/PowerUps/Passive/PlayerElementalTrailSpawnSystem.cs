using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns elemental trail segment entities while players move with Elemental Trail passive enabled.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerElementalTrailSpawnSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<PlayerElementalTrailState>();
        state.RequireForUpdate<PlayerElementalTrailSegmentElement>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<LocalTransform>();
    }

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
            PlayerPassiveToolsState passiveToolsState = PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer);
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
            bool isMoving = math.lengthsq(planarVelocity) > 0.0001f;

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

            while (trailSegments.Length >= maxSegments)
            {
                Entity oldestSegment = trailSegments[0].SegmentEntity;
                trailSegments.RemoveAt(0);

                if (oldestSegment.Index >= 0 && entityManager.Exists(oldestSegment))
                    commandBuffer.DestroyEntity(oldestSegment);
            }

            Entity segmentEntity = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(segmentEntity, LocalTransform.FromPositionRotationScale(playerPosition, quaternion.identity, 1f));
            commandBuffer.AddComponent(segmentEntity, new ElementalTrailSegment
            {
                OwnerEntity = playerEntity,
                Radius = math.max(0f, trailConfig.TrailRadius),
                RemainingLifetime = math.max(0.05f, trailConfig.TrailSegmentLifetimeSeconds),
                ApplyIntervalSeconds = math.max(0.01f, trailConfig.ApplyIntervalSeconds),
                ApplyTimer = 0f,
                StacksPerTick = math.max(0f, trailConfig.StacksPerTick),
                Effect = trailConfig.Effect
            });

            commandBuffer.AppendToBuffer(playerEntity, new PlayerElementalTrailSegmentElement
            {
                SegmentEntity = segmentEntity
            });

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
    #endregion

    #endregion
}
