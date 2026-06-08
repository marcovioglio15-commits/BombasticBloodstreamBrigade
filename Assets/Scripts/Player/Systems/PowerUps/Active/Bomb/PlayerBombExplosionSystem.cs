using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies bomb explosion damage and despawn requests to enemies, then destroys bomb entities.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerBombFuseSystem))]
[UpdateBefore(typeof(PlayerImpactFrameUpdateSystem))]
public partial struct PlayerBombExplosionSystem : ISystem
{
    #region Fields
    private EntityQuery bombQuery;
    private EntityQuery enemyQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates cached bomb and enemy queries used by the explosion resolution pass.
    /// </summary>
    /// <param name="state">Mutable system state used to build and require runtime queries.</param>
    public void OnCreate(ref SystemState state)
    {
        bombQuery = SystemAPI.QueryBuilder()
            .WithAll<BombFuseState, BombExplodeRequest>()
            .Build();

        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, EnemyRuntimeState, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        state.RequireForUpdate(bombQuery);
    }

    /// <summary>
    /// Resolves every requested bomb explosion, commits enemy damage feedback and retires consumed bomb entities.
    /// </summary>
    /// <param name="state">Mutable system state used to read and commit bomb explosion results.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PlayerImpactFrameState> impactFrameLookup = SystemAPI.GetComponentLookup<PlayerImpactFrameState>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        NativeArray<Entity> bombEntities = bombQuery.ToEntityArray(frameAllocator);
        NativeArray<BombFuseState> bombFuseStates = bombQuery.ToComponentDataArray<BombFuseState>(frameAllocator);

        int enemyCount = enemyQuery.CalculateEntityCount();
        NativeArray<Entity> enemyEntities = default;
        NativeArray<EnemyData> enemyDataArray = default;
        NativeArray<EnemyHealth> enemyHealthArray = default;
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = default;
        NativeArray<LocalTransform> enemyTransforms = default;
        NativeArray<float3> enemyPositions = default;
        NativeArray<float3> enemyElasticHitDirections = default;
        NativeArray<float> enemyBodyRadii = default;
        NativeArray<byte> enemyDirtyFlags = default;
        NativeParallelMultiHashMap<int, int> enemyCellMap = default;
        float inverseCellSize = 0f;
        float maximumEnemyRadius = 0.05f;

        if (enemyCount > 0)
        {
            enemyEntities = enemyQuery.ToEntityArray(frameAllocator);
            enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(frameAllocator);
            enemyHealthArray = enemyQuery.ToComponentDataArray<EnemyHealth>(frameAllocator);
            enemyRuntimeArray = enemyQuery.ToComponentDataArray<EnemyRuntimeState>(frameAllocator);
            enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(frameAllocator);
            enemyPositions = CollectionHelper.CreateNativeArray<float3>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
            enemyElasticHitDirections = CollectionHelper.CreateNativeArray<float3>(enemyCount,
                                                                                    frameAllocator,
                                                                                    NativeArrayOptions.ClearMemory);
            enemyBodyRadii = CollectionHelper.CreateNativeArray<float>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
            enemyDirtyFlags = CollectionHelper.CreateNativeArray<byte>(enemyCount, frameAllocator, NativeArrayOptions.ClearMemory);
            enemyCellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, frameAllocator);

            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                enemyPositions[enemyIndex] = enemyTransforms[enemyIndex].Position;
                float bodyRadius = math.max(0f, enemyDataArray[enemyIndex].BodyRadius);
                enemyBodyRadii[enemyIndex] = bodyRadius;

                if (bodyRadius > maximumEnemyRadius)
                    maximumEnemyRadius = bodyRadius;
            }

            float cellSize = EnemySpatialHashUtility.ResolveCellSize(maximumEnemyRadius);
            inverseCellSize = 1f / cellSize;
            EnemySpatialHashUtility.BuildCellMap(in enemyPositions, inverseCellSize, ref enemyCellMap);
        }

        for (int bombIndex = 0; bombIndex < bombEntities.Length; bombIndex++)
        {
            BombFuseState fuseState = bombFuseStates[bombIndex];

            if (canEnqueueAudioRequests)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.ExplosionBomb, fuseState.Position);

            TryActivateImpactFrame(in fuseState, ref impactFrameLookup);

            if (enemyCount > 0)
                ApplyExplosionToEnemies(entityManager,
                                        in fuseState,
                                        enemyCount,
                                        in enemyEntities,
                                        ref enemyHealthArray,
                                        in enemyPositions,
                                        in enemyBodyRadii,
                                        ref enemyDirtyFlags,
                                        ref enemyElasticHitDirections,
                                        in enemyCellMap,
                                        inverseCellSize,
                                        maximumEnemyRadius,
                                        ref commandBuffer);

            EnqueueExplosionVfxRequest(in fuseState, in localTransformLookup, ref vfxRequestLookup);

            Entity bombEntity = bombEntities[bombIndex];

            if (entityManager.Exists(bombEntity))
                commandBuffer.DestroyEntity(bombEntity);
        }

        if (enemyCount > 0)
        {
            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                if (enemyDirtyFlags[enemyIndex] == 0)
                    continue;

                Entity enemyEntity = enemyEntities[enemyIndex];

                if (!entityManager.Exists(enemyEntity))
                    continue;

                EnemyRuntimeState enemyRuntimeState = enemyRuntimeArray[enemyIndex];
                EnemyHealth enemyHealth = enemyHealthArray[enemyIndex];
                EnemyExtraComboPointsRuntimeUtility.MarkEnemyDamaged(ref enemyRuntimeState);
                entityManager.SetComponentData(enemyEntity, enemyRuntimeState);
                entityManager.SetComponentData(enemyEntity, enemyHealth);
                DamageFlashRuntimeUtility.Trigger(entityManager, enemyEntity);
                EnemyElasticHitRuntimeUtility.Trigger(entityManager,
                                                      enemyEntity,
                                                      in enemyHealth,
                                                      enemyElasticHitDirections[enemyIndex],
                                                      true);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Starts the carried Impact Frame payload when a player-spawned bomb reaches its explosion frame.
    /// </summary>
    /// <param name="fuseState">Runtime bomb fuse state containing the owner and optional Impact Frame config.</param>
    /// <param name="impactFrameLookup">Mutable lookup for the owning player's Impact Frame state.</param>
    private static void TryActivateImpactFrame(in BombFuseState fuseState,
                                               ref ComponentLookup<PlayerImpactFrameState> impactFrameLookup)
    {
        if (fuseState.HasImpactFrame == 0)
            return;

        if (fuseState.OwnerEntity == Entity.Null)
            return;

        if (!impactFrameLookup.HasComponent(fuseState.OwnerEntity))
            return;

        PlayerImpactFrameState impactFrameState = impactFrameLookup[fuseState.OwnerEntity];
        PlayerImpactFrameRuntimeUtility.ActivateAtWorldPosition(ref impactFrameState,
                                                                in fuseState.ImpactFrame,
                                                                fuseState.Position);
        impactFrameLookup[fuseState.OwnerEntity] = impactFrameState;
    }

    private static void EnqueueExplosionVfxRequest(in BombFuseState fuseState,
                                                   in ComponentLookup<LocalTransform> localTransformLookup,
                                                   ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (fuseState.OwnerEntity == Entity.Null)
            return;

        if (fuseState.ExplosionVfxPrefabEntity == Entity.Null)
            return;

        if (!vfxRequestLookup.HasBuffer(fuseState.OwnerEntity))
            return;

        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = vfxRequestLookup[fuseState.OwnerEntity];
        float scaleMultiplier = math.max(0.01f, fuseState.VfxScaleMultiplier);

        if (fuseState.ScaleVfxToRadius != 0)
            scaleMultiplier *= math.max(0.1f, fuseState.Radius);

        float3 explosionVfxPosition = fuseState.Position;

        if (localTransformLookup.HasComponent(fuseState.OwnerEntity))
        {
            float ownerFloorReferenceY = localTransformLookup[fuseState.OwnerEntity].Position.y;

            if (explosionVfxPosition.y < ownerFloorReferenceY)
                explosionVfxPosition.y = ownerFloorReferenceY;
        }

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = fuseState.ExplosionVfxPrefabEntity,
            Position = explosionVfxPosition,
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

    /// <summary>
    /// Resolves one bomb against spatially relevant enemies and records direct elastic hit directions for damaged targets.
    /// </summary>
    /// <param name="entityManager">Entity manager used to reject stale enemy entities.</param>
    /// <param name="fuseState">Resolved bomb explosion payload.</param>
    /// <param name="enemyCount">Number of cached active enemies.</param>
    /// <param name="enemyEntities">Cached active enemy entities.</param>
    /// <param name="enemyHealthArray">Mutable projected health per enemy.</param>
    /// <param name="enemyPositions">Cached enemy world positions.</param>
    /// <param name="enemyBodyRadii">Cached enemy collision radii.</param>
    /// <param name="enemyDirtyFlags">Mutable flags marking enemies whose health changed.</param>
    /// <param name="enemyElasticHitDirections">Mutable last explosion direction per damaged enemy.</param>
    /// <param name="enemyCellMap">Spatial hash containing active enemy indices.</param>
    /// <param name="inverseCellSize">Inverse spatial-hash cell size.</param>
    /// <param name="maximumEnemyRadius">Largest cached enemy radius.</param>
    /// <param name="commandBuffer">Command buffer receiving killed-enemy despawn requests.</param>
    private static void ApplyExplosionToEnemies(EntityManager entityManager,
                                                in BombFuseState fuseState,
                                                int enemyCount,
                                                in NativeArray<Entity> enemyEntities,
                                                ref NativeArray<EnemyHealth> enemyHealthArray,
                                                in NativeArray<float3> enemyPositions,
                                                in NativeArray<float> enemyBodyRadii,
                                                ref NativeArray<byte> enemyDirtyFlags,
                                                ref NativeArray<float3> enemyElasticHitDirections,
                                                in NativeParallelMultiHashMap<int, int> enemyCellMap,
                                                float inverseCellSize,
                                                float maximumEnemyRadius,
                                                ref EntityCommandBuffer commandBuffer)
    {
        float explosionRadius = math.max(0.1f, fuseState.Radius);
        float explosionRadiusSquared = explosionRadius * explosionRadius;
        float explosionDamage = math.max(0f, fuseState.Damage);

        if (explosionDamage <= 0f)
            return;

        if (fuseState.AffectAllEnemiesInRadius != 0)
        {
            float queryRadius = explosionRadius + math.max(0f, maximumEnemyRadius);
            EnemySpatialHashUtility.ResolveCellBounds(fuseState.Position,
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

                        ApplyExplosionDamageToEnemy(entityManager,
                                                    in fuseState,
                                                    enemyIndex,
                                                    explosionRadiusSquared,
                                                    explosionDamage,
                                                    in enemyEntities,
                                                    ref enemyHealthArray,
                                                    in enemyPositions,
                                                    in enemyBodyRadii,
                                                    ref enemyDirtyFlags,
                                                    ref enemyElasticHitDirections,
                                                    ref commandBuffer);
                    }
                    while (enemyCellMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            return;
        }

        int closestEnemyIndex = -1;
        float closestDistanceSquared = float.MaxValue;
        float closestQueryRadius = explosionRadius + math.max(0f, maximumEnemyRadius);
        EnemySpatialHashUtility.ResolveCellBounds(fuseState.Position,
                                                  closestQueryRadius,
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
                    float3 delta = enemyPosition - fuseState.Position;
                    delta.y = 0f;
                    float sqrDistance = math.lengthsq(delta);
                    float bodyRadius = enemyBodyRadii[enemyIndex];
                    float bodyRadiusSquared = bodyRadius * bodyRadius;

                    if (sqrDistance > explosionRadiusSquared + bodyRadiusSquared)
                        continue;

                    if (sqrDistance >= closestDistanceSquared)
                        continue;

                    closestDistanceSquared = sqrDistance;
                    closestEnemyIndex = enemyIndex;
                }
                while (enemyCellMap.TryGetNextValue(out enemyIndex, ref iterator));
            }
        }

        if (closestEnemyIndex < 0)
            return;

        ApplyExplosionDamageToEnemy(entityManager,
                                    in fuseState,
                                    closestEnemyIndex,
                                    explosionRadiusSquared,
                                    explosionDamage,
                                    in enemyEntities,
                                    ref enemyHealthArray,
                                    in enemyPositions,
                                    in enemyBodyRadii,
                                    ref enemyDirtyFlags,
                                    ref enemyElasticHitDirections,
                                    ref commandBuffer);
    }

    /// <summary>
    /// Applies one validated bomb damage packet and stores its horizontal direct-impact direction.
    /// </summary>
    /// <param name="entityManager">Entity manager used to reject stale enemy entities.</param>
    /// <param name="fuseState">Resolved bomb explosion payload.</param>
    /// <param name="enemyIndex">Target index inside cached enemy arrays.</param>
    /// <param name="explosionRadiusSquared">Squared bomb radius.</param>
    /// <param name="explosionDamage">Sanitized bomb damage.</param>
    /// <param name="enemyEntities">Cached active enemy entities.</param>
    /// <param name="enemyHealthArray">Mutable projected health per enemy.</param>
    /// <param name="enemyPositions">Cached enemy world positions.</param>
    /// <param name="enemyBodyRadii">Cached enemy collision radii.</param>
    /// <param name="enemyDirtyFlags">Mutable flags marking enemies whose health changed.</param>
    /// <param name="enemyElasticHitDirections">Mutable last explosion direction per damaged enemy.</param>
    /// <param name="commandBuffer">Command buffer receiving killed-enemy despawn requests.</param>
    private static void ApplyExplosionDamageToEnemy(EntityManager entityManager,
                                                    in BombFuseState fuseState,
                                                    int enemyIndex,
                                                    float explosionRadiusSquared,
                                                    float explosionDamage,
                                                    in NativeArray<Entity> enemyEntities,
                                                    ref NativeArray<EnemyHealth> enemyHealthArray,
                                                    in NativeArray<float3> enemyPositions,
                                                    in NativeArray<float> enemyBodyRadii,
                                                    ref NativeArray<byte> enemyDirtyFlags,
                                                    ref NativeArray<float3> enemyElasticHitDirections,
                                                    ref EntityCommandBuffer commandBuffer)
    {
        Entity enemyEntity = enemyEntities[enemyIndex];

        if (!entityManager.Exists(enemyEntity))
            return;

        float3 enemyPosition = enemyPositions[enemyIndex];
        float3 delta = enemyPosition - fuseState.Position;
        delta.y = 0f;
        float sqrDistance = math.lengthsq(delta);
        float bodyRadius = math.max(0f, enemyBodyRadii[enemyIndex]);
        float bodyRadiusSquared = bodyRadius * bodyRadius;

        if (sqrDistance > explosionRadiusSquared + bodyRadiusSquared)
            return;

        EnemyHealth enemyHealth = enemyHealthArray[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return;

        bool damageApplied = EnemyDamageUtility.TryApplyFlatShieldDamage(ref enemyHealth, explosionDamage);

        if (!damageApplied)
            return;

        enemyHealthArray[enemyIndex] = enemyHealth;
        enemyDirtyFlags[enemyIndex] = 1;
        enemyElasticHitDirections[enemyIndex] = delta;

        if (enemyHealth.Current > 0f)
            return;

        commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
        {
            Reason = EnemyDespawnReason.Killed
        });
    }
    #endregion

    #endregion
}
