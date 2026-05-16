using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves passive explosion requests by damaging enemies in radius and scheduling optional VFX.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyProjectileHitSystem))]
[UpdateBefore(typeof(EnemyContactDamageSystem))]
public partial struct PlayerPassiveExplosionResolveSystem : ISystem
{
    #region Fields
    private EntityQuery enemyQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerExplosionRequest>();

        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, EnemyRuntimeState, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        int enemyCount = enemyQuery.CalculateEntityCount();

        if (enemyCount <= 0)
        {
            ClearRequestsWithoutEnemies(ref state, audioRequests, canEnqueueAudioRequests);
            return;
        }

        EntityManager entityManager = state.EntityManager;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        NativeArray<Entity> enemyEntities = enemyQuery.ToEntityArray(frameAllocator);
        NativeArray<EnemyData> enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(frameAllocator);
        NativeArray<EnemyHealth> enemyHealthArray = enemyQuery.ToComponentDataArray<EnemyHealth>(frameAllocator);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = enemyQuery.ToComponentDataArray<EnemyRuntimeState>(frameAllocator);
        NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(frameAllocator);
        NativeArray<float3> enemyPositions = CollectionHelper.CreateNativeArray<float3>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> enemyBodyRadii = CollectionHelper.CreateNativeArray<float>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<byte> enemyDirtyFlags = CollectionHelper.CreateNativeArray<byte>(enemyCount, frameAllocator, NativeArrayOptions.ClearMemory);
        ComponentLookup<EnemyDespawnRequest> despawnRequestLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        float maximumEnemyRadius = 0.05f;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            enemyPositions[enemyIndex] = enemyTransforms[enemyIndex].Position;
            float bodyRadius = math.max(0f, enemyDataArray[enemyIndex].BodyRadius);
            enemyBodyRadii[enemyIndex] = bodyRadius;

            if (bodyRadius > maximumEnemyRadius)
                maximumEnemyRadius = bodyRadius;
        }

        float cellSize = EnemySpatialHashUtility.ResolveCellSize(maximumEnemyRadius);
        float inverseCellSize = 1f / cellSize;
        NativeParallelMultiHashMap<int, int> enemyCellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, frameAllocator);
        EnemySpatialHashUtility.BuildCellMap(in enemyPositions, inverseCellSize, ref enemyCellMap);

        foreach ((DynamicBuffer<PlayerExplosionRequest> explosionRequests,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
                 in SystemAPI.Query<DynamicBuffer<PlayerExplosionRequest>, DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>())
        {
            for (int requestIndex = 0; requestIndex < explosionRequests.Length; requestIndex++)
            {
                PlayerExplosionRequest explosionRequest = explosionRequests[requestIndex];

                EnqueueExplosionAudio(in explosionRequest, audioRequests, canEnqueueAudioRequests);

                ApplyExplosionRequest(in explosionRequest,
                                      enemyCount,
                                      in enemyEntities,
                                      ref enemyHealthArray,
                                      in enemyPositions,
                                      in enemyBodyRadii,
                                      ref enemyDirtyFlags,
                                      in enemyCellMap,
                                      inverseCellSize,
                                      maximumEnemyRadius,
                                      in despawnRequestLookup,
                                      ref commandBuffer);
                EnqueueExplosionVfxRequest(in explosionRequest, vfxRequests);
            }

            explosionRequests.Clear();
        }

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            if (enemyDirtyFlags[enemyIndex] == 0)
                continue;

            Entity enemyEntity = enemyEntities[enemyIndex];

            if (!entityManager.Exists(enemyEntity))
                continue;

            EnemyRuntimeState enemyRuntimeState = enemyRuntimeArray[enemyIndex];
            EnemyExtraComboPointsRuntimeUtility.MarkEnemyDamaged(ref enemyRuntimeState);
            entityManager.SetComponentData(enemyEntity, enemyRuntimeState);
            entityManager.SetComponentData(enemyEntity, enemyHealthArray[enemyIndex]);
            DamageFlashRuntimeUtility.Trigger(entityManager, enemyEntity);
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Clears passive explosion requests when there are no valid enemies while still emitting VFX and audio.
    /// </summary>
    /// <param name="state">Mutable system state used by the request query.</param>
    /// <param name="audioRequests">Optional audio request buffer.</param>
    /// <param name="canEnqueueAudioRequests">True when the audio singleton exists.</param>
    private void ClearRequestsWithoutEnemies(ref SystemState state,
                                             DynamicBuffer<GameAudioEventRequest> audioRequests,
                                             bool canEnqueueAudioRequests)
    {
        foreach ((DynamicBuffer<PlayerExplosionRequest> explosionRequests,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
                 in SystemAPI.Query<DynamicBuffer<PlayerExplosionRequest>, DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>())
        {
            for (int requestIndex = 0; requestIndex < explosionRequests.Length; requestIndex++)
            {
                PlayerExplosionRequest explosionRequest = explosionRequests[requestIndex];

                EnqueueExplosionAudio(in explosionRequest, audioRequests, canEnqueueAudioRequests);

                EnqueueExplosionVfxRequest(in explosionRequest, vfxRequests);
            }

            explosionRequests.Clear();
        }
    }

    private static void ApplyExplosionRequest(in PlayerExplosionRequest explosionRequest,
                                              int enemyCount,
                                              in NativeArray<Entity> enemyEntities,
                                              ref NativeArray<EnemyHealth> enemyHealthArray,
                                              in NativeArray<float3> enemyPositions,
                                              in NativeArray<float> enemyBodyRadii,
                                              ref NativeArray<byte> enemyDirtyFlags,
                                              in NativeParallelMultiHashMap<int, int> enemyCellMap,
                                              float inverseCellSize,
                                              float maximumEnemyRadius,
                                              in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                              ref EntityCommandBuffer commandBuffer)
    {
        float radius = math.max(0f, explosionRequest.Radius);
        float damage = math.max(0f, explosionRequest.Damage);

        if (radius <= 0f || damage <= 0f)
            return;

        float radiusSquared = radius * radius;

        if (explosionRequest.AffectAllEnemiesInRadius != 0)
        {
            float queryRadius = radius + math.max(0f, maximumEnemyRadius);
            EnemySpatialHashUtility.ResolveCellBounds(explosionRequest.Position,
                                                      queryRadius,
                                                      inverseCellSize,
                                                      out int minCellX,
                                                      out int maxCellX,
                                                      out int minCellY,
                                                      out int maxCellY);

            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    int cellKey = EnemySpatialHashUtility.EncodeCell(cellX, cellY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int enemyIndex;

                    if (!enemyCellMap.TryGetFirstValue(cellKey, out enemyIndex, out iterator))
                        continue;

                    do
                    {
                        if (enemyIndex < 0 || enemyIndex >= enemyCount)
                            continue;

                        TryDamageEnemy(enemyIndex,
                                       radiusSquared,
                                       damage,
                                       explosionRequest.Position,
                                       in enemyEntities,
                                       ref enemyHealthArray,
                                       in enemyPositions,
                                       in enemyBodyRadii,
                                       ref enemyDirtyFlags,
                                       in despawnRequestLookup,
                                       ref commandBuffer);
                    }
                    while (enemyCellMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            return;
        }

        int closestEnemyIndex = -1;
        float closestDistanceSquared = float.MaxValue;
        float closestCandidateQueryRadius = radius + math.max(0f, maximumEnemyRadius);
        EnemySpatialHashUtility.ResolveCellBounds(explosionRequest.Position,
                                                  closestCandidateQueryRadius,
                                                  inverseCellSize,
                                                  out int closestMinCellX,
                                                  out int closestMaxCellX,
                                                  out int closestMinCellY,
                                                  out int closestMaxCellY);

        for (int cellX = closestMinCellX; cellX <= closestMaxCellX; cellX++)
        {
            for (int cellY = closestMinCellY; cellY <= closestMaxCellY; cellY++)
            {
                int cellKey = EnemySpatialHashUtility.EncodeCell(cellX, cellY);
                NativeParallelMultiHashMapIterator<int> iterator;
                int enemyIndex;

                if (!enemyCellMap.TryGetFirstValue(cellKey, out enemyIndex, out iterator))
                    continue;

                do
                {
                    if (enemyIndex < 0 || enemyIndex >= enemyCount)
                        continue;

                    float3 enemyPosition = enemyPositions[enemyIndex];
                    float3 delta = enemyPosition - explosionRequest.Position;
                    delta.y = 0f;
                    float distanceSquared = math.lengthsq(delta);
                    float bodyRadius = enemyBodyRadii[enemyIndex];
                    float bodyRadiusSquared = bodyRadius * bodyRadius;

                    if (distanceSquared > radiusSquared + bodyRadiusSquared)
                        continue;

                    if (distanceSquared >= closestDistanceSquared)
                        continue;

                    closestDistanceSquared = distanceSquared;
                    closestEnemyIndex = enemyIndex;
                }
                while (enemyCellMap.TryGetNextValue(out enemyIndex, ref iterator));
            }
        }

        if (closestEnemyIndex < 0)
            return;

        TryDamageEnemy(closestEnemyIndex,
                       radiusSquared,
                       damage,
                       explosionRequest.Position,
                       in enemyEntities,
                       ref enemyHealthArray,
                       in enemyPositions,
                       in enemyBodyRadii,
                       ref enemyDirtyFlags,
                       in despawnRequestLookup,
                       ref commandBuffer);
    }

    private static void TryDamageEnemy(int enemyIndex,
                                       float radiusSquared,
                                       float damage,
                                       float3 explosionPosition,
                                       in NativeArray<Entity> enemyEntities,
                                       ref NativeArray<EnemyHealth> enemyHealthArray,
                                       in NativeArray<float3> enemyPositions,
                                       in NativeArray<float> enemyBodyRadii,
                                       ref NativeArray<byte> enemyDirtyFlags,
                                       in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                       ref EntityCommandBuffer commandBuffer)
    {
        Entity enemyEntity = enemyEntities[enemyIndex];
        float3 enemyPosition = enemyPositions[enemyIndex];
        float3 delta = enemyPosition - explosionPosition;
        delta.y = 0f;
        float distanceSquared = math.lengthsq(delta);
        float bodyRadius = math.max(0f, enemyBodyRadii[enemyIndex]);
        float bodyRadiusSquared = bodyRadius * bodyRadius;

        if (distanceSquared > radiusSquared + bodyRadiusSquared)
            return;

        EnemyHealth enemyHealth = enemyHealthArray[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return;

        bool damageApplied = EnemyDamageUtility.TryApplyFlatShieldDamage(ref enemyHealth, damage);

        if (!damageApplied)
            return;

        enemyHealthArray[enemyIndex] = enemyHealth;
        enemyDirtyFlags[enemyIndex] = 1;

        if (enemyHealth.Current > 0f)
            return;

        if (despawnRequestLookup.HasComponent(enemyEntity))
            return;

        commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
        {
            Reason = EnemyDespawnReason.Killed
        });
    }

    /// <summary>
    /// Emits the audio event attached to a passive explosion request.
    /// </summary>
    /// <param name="explosionRequest">Explosion payload generated by the passive explosion trigger system.</param>
    /// <param name="audioRequests">Optional global audio request buffer.</param>
    /// <param name="canEnqueueAudioRequests">True when the audio singleton exists in the active world.</param>
    private static void EnqueueExplosionAudio(in PlayerExplosionRequest explosionRequest,
                                              DynamicBuffer<GameAudioEventRequest> audioRequests,
                                              bool canEnqueueAudioRequests)
    {
        if (!canEnqueueAudioRequests)
            return;

        GameAudioEventId audioEventId = explosionRequest.AudioEventId != GameAudioEventId.None ? explosionRequest.AudioEventId : GameAudioEventId.ExplosionPassive;
        GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, audioEventId, explosionRequest.Position);
    }

    private static void EnqueueExplosionVfxRequest(in PlayerExplosionRequest explosionRequest, DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
    {
        if (explosionRequest.ExplosionVfxPrefabEntity == Entity.Null)
            return;

        float scaleMultiplier = math.max(0.01f, explosionRequest.VfxScaleMultiplier);

        if (explosionRequest.ScaleVfxToRadius != 0)
            scaleMultiplier *= math.max(0.1f, explosionRequest.Radius);

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = explosionRequest.ExplosionVfxPrefabEntity,
            Position = explosionRequest.Position,
            Rotation = quaternion.identity,
            UniformScale = scaleMultiplier,
            LifetimeSeconds = 2f,
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero
        });
    }
    #endregion

    #endregion
}
