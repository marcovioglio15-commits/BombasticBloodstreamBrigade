using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Updates trail segment lifetimes and applies elemental stacks to enemies overlapping active segments.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyProjectileHitSystem))]
[UpdateBefore(typeof(EnemyElementalEffectsSystem))]
public partial struct PlayerElementalTrailResolveSystem : ISystem
{
    #region Fields
    private EntityQuery enemyQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ElementalTrailSegment>();

        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        BufferLookup<EnemyElementStackElement> elementalStackLookup = SystemAPI.GetBufferLookup<EnemyElementStackElement>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerElementalVfxConfig>(true);
        ComponentLookup<EnemyRuntimeState> enemyRuntimeLookup = SystemAPI.GetComponentLookup<EnemyRuntimeState>(true);
        ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup = SystemAPI.GetComponentLookup<EnemyElementalVfxAnchor>(true);

        int enemyCount = enemyQuery.CalculateEntityCount();
        NativeArray<Entity> enemyEntities = default;
        NativeArray<EnemyData> enemyDataArray = default;
        NativeArray<float3> enemyPositions = default;
        NativeArray<float> enemyBodyRadii = default;
        NativeParallelMultiHashMap<int, int> enemyCellMap = default;
        float inverseCellSize = 0f;
        float maximumEnemyRadius = 0.05f;

        if (enemyCount > 0)
        {
            enemyEntities = enemyQuery.ToEntityArray(frameAllocator);
            enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(frameAllocator);
            NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(frameAllocator);
            enemyPositions = CollectionHelper.CreateNativeArray<float3>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
            enemyBodyRadii = CollectionHelper.CreateNativeArray<float>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
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

        foreach ((RefRW<ElementalTrailSegment> trailSegment,
                  RefRO<LocalTransform> trailTransform,
                  Entity segmentEntity)
                 in SystemAPI.Query<RefRW<ElementalTrailSegment>, RefRO<LocalTransform>>()
                             .WithEntityAccess())
        {
            ElementalTrailSegment segment = trailSegment.ValueRO;
            segment.RemainingLifetime -= deltaTime;
            segment.ApplyTimer -= deltaTime;

            if (segment.RemainingLifetime <= 0f)
            {
                trailSegment.ValueRW = segment;
                commandBuffer.DestroyEntity(segmentEntity);
                continue;
            }

            int pendingApplyCount = ResolvePendingApplyCount(ref segment);
            bool canApply = pendingApplyCount > 0 && segment.StacksPerTick > 0f && segment.Radius > 0f;

            if (canApply && enemyCount > 0)
            {
                float radius = math.max(0f, segment.Radius);
                float stacksToApply = math.max(0f, segment.StacksPerTick) * pendingApplyCount;
                float3 trailPosition = trailTransform.ValueRO.Position;
                float procVfxLifetimeSeconds = ResolveProcVfxLifetimeSeconds(in segment.Effect);
                Entity ownerEntity = segment.OwnerEntity;
                bool canSpawnElementalVfx = false;
                DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = default;
                ElementalVfxDefinitionConfig elementalVfxConfig = default;

                if (ownerEntity != Entity.Null &&
                    ownerEntity.Index >= 0 &&
                    vfxRequestLookup.HasBuffer(ownerEntity) &&
                    elementalVfxConfigLookup.HasComponent(ownerEntity))
                {
                    vfxRequests = vfxRequestLookup[ownerEntity];
                    PlayerElementalVfxConfig ownerElementalVfxConfig = elementalVfxConfigLookup[ownerEntity];
                    elementalVfxConfig = ResolveElementalVfxDefinition(in ownerElementalVfxConfig, segment.Effect.ElementType);
                    canSpawnElementalVfx = elementalVfxConfig.SpawnStackVfx != 0 || elementalVfxConfig.SpawnProcVfx != 0;
                }

                float queryRadius = radius + math.max(0f, maximumEnemyRadius);
                EnemySpatialHashUtility.ResolveCellBounds(trailPosition,
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

                            Entity enemyEntity = enemyEntities[enemyIndex];

                            if (!entityManager.Exists(enemyEntity))
                                continue;

                            float3 enemyPosition = enemyPositions[enemyIndex];
                            float3 delta = enemyPosition - trailPosition;
                            delta.y = 0f;
                            float distanceSquared = math.lengthsq(delta);
                            float bodyRadius = math.max(0f, enemyBodyRadii[enemyIndex]);
                            float totalRadius = radius + bodyRadius;

                            if (distanceSquared > totalRadius * totalRadius)
                                continue;

                            bool procTriggered;
                            bool applied = EnemyElementalStackUtility.TryApplyStacks(enemyEntity,
                                                                                     stacksToApply,
                                                                                     segment.Effect,
                                                                                     ref elementalStackLookup,
                                                                                     out procTriggered);

                            if (!applied || !canSpawnElementalVfx)
                                continue;

                            if (!enemyRuntimeLookup.HasComponent(enemyEntity))
                                continue;

                            EnemyRuntimeState enemyRuntimeState = enemyRuntimeLookup[enemyEntity];
                            Entity followTargetEntity = enemyEntity;

                            if (elementalVfxAnchorLookup.HasComponent(enemyEntity))
                            {
                                Entity anchorEntity = elementalVfxAnchorLookup[enemyEntity].AnchorEntity;

                                if (anchorEntity != Entity.Null)
                                    followTargetEntity = anchorEntity;
                            }

                            if (elementalVfxConfig.SpawnStackVfx != 0)
                                EnqueueElementalVfx(ref vfxRequests,
                                                    elementalVfxConfig.StackVfxPrefabEntity,
                                                    enemyPosition,
                                                    elementalVfxConfig.StackVfxScaleMultiplier,
                                                    followTargetEntity,
                                                    enemyEntity,
                                                    enemyRuntimeState.SpawnVersion,
                                                    0.35f);

                            if (procTriggered && elementalVfxConfig.SpawnProcVfx != 0)
                                EnqueueElementalVfx(ref vfxRequests,
                                                    elementalVfxConfig.ProcVfxPrefabEntity,
                                                    enemyPosition,
                                                    elementalVfxConfig.ProcVfxScaleMultiplier,
                                                    followTargetEntity,
                                                    enemyEntity,
                                                    enemyRuntimeState.SpawnVersion,
                                                    procVfxLifetimeSeconds);
                        }
                        while (enemyCellMap.TryGetNextValue(out enemyIndex, ref iterator));
                    }
                }
            }

            trailSegment.ValueRW = segment;
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

    }
    #endregion

    #region Helpers
    private static ElementalVfxDefinitionConfig ResolveElementalVfxDefinition(in PlayerElementalVfxConfig elementalVfxConfig, ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Fire:
                return elementalVfxConfig.Fire;
            case ElementType.Ice:
                return elementalVfxConfig.Ice;
            case ElementType.Poison:
                return elementalVfxConfig.Poison;
            default:
                return elementalVfxConfig.Custom;
        }
    }

    private static void EnqueueElementalVfx(ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                                            Entity prefabEntity,
                                            float3 position,
                                            float scaleMultiplier,
                                            Entity followTargetEntity,
                                            Entity followValidationEntity,
                                            uint followValidationSpawnVersion,
                                            float lifetimeSeconds)
    {
        if (prefabEntity == Entity.Null)
            return;

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = prefabEntity,
            Position = position,
            Rotation = quaternion.identity,
            UniformScale = math.max(0.01f, scaleMultiplier),
            LifetimeSeconds = math.max(0.05f, lifetimeSeconds),
            FollowTargetEntity = followTargetEntity,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = followValidationEntity,
            FollowValidationSpawnVersion = followValidationSpawnVersion,
            Velocity = float3.zero
        });
    }

    private static float ResolveProcVfxLifetimeSeconds(in ElementalEffectConfig effectConfig)
    {
        switch (effectConfig.EffectKind)
        {
            case ElementalEffectKind.Dots:
                return math.max(0.05f, effectConfig.DotDurationSeconds);
            case ElementalEffectKind.Impediment:
                return math.max(0.05f, effectConfig.ImpedimentDurationSeconds);
            default:
                return 0.5f;
        }
    }

    private static int ResolvePendingApplyCount(ref ElementalTrailSegment segment)
    {
        float applyIntervalSeconds = math.max(0.01f, segment.ApplyIntervalSeconds);
        segment.ApplyIntervalSeconds = applyIntervalSeconds;

        if (segment.ApplyTimer > 0f)
            return 0;

        float overdueSeconds = -segment.ApplyTimer;
        int additionalApplyCount = (int)math.floor(overdueSeconds / applyIntervalSeconds);
        int applyCount = 1 + math.max(0, additionalApplyCount);
        segment.ApplyTimer += applyCount * applyIntervalSeconds;

        if (segment.ApplyTimer <= 0f)
            segment.ApplyTimer = applyIntervalSeconds;

        return applyCount;
    }
    #endregion

    #endregion
}
