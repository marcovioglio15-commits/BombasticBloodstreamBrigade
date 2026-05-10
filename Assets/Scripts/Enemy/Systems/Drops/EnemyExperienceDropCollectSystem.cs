using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Moves active experience drops toward the player and grants experience on collection.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyExperienceDropSpawnSystem))]
public partial struct EnemyExperienceDropCollectSystem : ISystem
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    private static readonly float3 DropParkingPosition = new float3(0f, -12000f, 0f);
    #endregion

    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures required components for drop attraction and collection.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyExperienceDropActive>();
        playerQuery = new EntityQueryBuilder(Allocator.Temp)
                          .WithAll<LocalTransform>()
                          .WithAll<PlayerMovementState>()
                          .WithAll<PlayerExperienceCollection>()
                          .WithAll<PlayerExperience>()
                          .WithAll<PlayerLevel>()
                          .WithAll<PlayerProgressionConfig>()
                          .WithAll<PlayerRuntimeGamePhaseElement>()
                          .WithAll<PlayerRunOutcomeState>()
                          .Build(ref state);
        state.RequireForUpdate(playerQuery);
    }

    /// <summary>
    /// Updates drop attraction and converts collected drops into player experience.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;

        if (playerQuery.CalculateEntityCount() != 1)
            return;

        Entity playerEntity = playerQuery.GetSingletonEntity();
        PlayerRunOutcomeState runOutcomeState = entityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);

        if (runOutcomeState.IsFinalized != 0)
            return;

        float3 playerPosition = entityManager.GetComponentData<LocalTransform>(playerEntity).Position;
        float3 planarVelocity = entityManager.GetComponentData<PlayerMovementState>(playerEntity).Velocity;
        planarVelocity.y = 0f;
        float playerSpeed = math.max(0f, math.length(planarVelocity));
        float pickupRadius = math.max(0f, entityManager.GetComponentData<PlayerExperienceCollection>(playerEntity).PickupRadius);
        PlayerProgressionConfig progressionConfig = entityManager.GetComponentData<PlayerProgressionConfig>(playerEntity);
        DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases = entityManager.GetBuffer<PlayerRuntimeGamePhaseElement>(playerEntity);
        PlayerLevel playerLevel = entityManager.GetComponentData<PlayerLevel>(playerEntity);
        PlayerExperience playerExperience = entityManager.GetComponentData<PlayerExperience>(playerEntity);
        float remainingExperienceCapacity = PlayerProgressionPhaseUtility.ResolveRemainingExperienceUntilLevelCap(progressionConfig,
                                                                                                                   runtimeGamePhases,
                                                                                                                   playerLevel.Current,
                                                                                                                   playerExperience.Current);

        float deltaTime = SystemAPI.Time.DeltaTime;

        if (deltaTime <= 0f)
            return;

        float pickupRadiusSquared = pickupRadius * pickupRadius;
        float grantedExperience = 0f;
        BufferLookup<EnemyExperienceDropPoolElement> poolLookup = SystemAPI.GetBufferLookup<EnemyExperienceDropPoolElement>(false);

        foreach ((RefRW<EnemyExperienceDrop> dropData,
                  RefRW<LocalTransform> dropTransform,
                  EnabledRefRW<EnemyExperienceDropActive> dropActive,
                  Entity dropEntity)
                 in SystemAPI.Query<RefRW<EnemyExperienceDrop>, RefRW<LocalTransform>, EnabledRefRW<EnemyExperienceDropActive>>()
                             .WithAll<EnemyExperienceDropActive>()
                             .WithEntityAccess())
        {
            EnemyExperienceDrop currentDropData = dropData.ValueRO;
            float spawnAnimationDuration = math.max(0f, currentDropData.SpawnAnimationDuration);

            if (spawnAnimationDuration > PrecisionEpsilon && currentDropData.SpawnAnimationElapsed < spawnAnimationDuration)
            {
                float nextSpawnAnimationElapsed = math.min(spawnAnimationDuration, currentDropData.SpawnAnimationElapsed + deltaTime);
                float normalizedTime = nextSpawnAnimationElapsed / spawnAnimationDuration;
                float easedTime = normalizedTime * normalizedTime * (3f - (2f * normalizedTime));
                LocalTransform animatedTransform = dropTransform.ValueRO;
                animatedTransform.Position = math.lerp(currentDropData.SpawnStartPosition, currentDropData.SpawnTargetPosition, easedTime);
                dropTransform.ValueRW = animatedTransform;
                currentDropData.SpawnAnimationElapsed = nextSpawnAnimationElapsed;
                dropData.ValueRW = currentDropData;

                if (nextSpawnAnimationElapsed < spawnAnimationDuration)
                    continue;
            }

            float3 dropPosition = dropTransform.ValueRO.Position;
            float3 toPlayer = playerPosition - dropPosition;
            toPlayer.y = 0f;
            float distanceSquared = math.lengthsq(toPlayer);
            float baseCollectDistance = math.max(0.01f, currentDropData.CollectDistance);
            float collectDistancePerPlayerSpeed = math.max(0f, currentDropData.CollectDistancePerPlayerSpeed);
            float collectDistance = baseCollectDistance + (playerSpeed * collectDistancePerPlayerSpeed);
            float collectDistanceSquared = collectDistance * collectDistance;

            if (remainingExperienceCapacity <= PrecisionEpsilon)
            {
                if (currentDropData.IsAttracting != 0)
                {
                    currentDropData.IsAttracting = 0;
                    dropData.ValueRW = currentDropData;
                }

                continue;
            }

            if (distanceSquared <= collectDistanceSquared)
            {
                float collectedExperience = math.max(0f, currentDropData.ExperienceAmount);
                grantedExperience += collectedExperience;
                remainingExperienceCapacity -= collectedExperience;
                LocalTransform parkedTransform = dropTransform.ValueRO;
                parkedTransform.Position = DropParkingPosition;
                dropTransform.ValueRW = parkedTransform;
                currentDropData.IsAttracting = 0;
                dropData.ValueRW = currentDropData;
                dropActive.ValueRW = false;

                Entity poolEntity = currentDropData.PoolEntity;

                if (poolLookup.HasBuffer(poolEntity))
                {
                    DynamicBuffer<EnemyExperienceDropPoolElement> poolElements = poolLookup[poolEntity];
                    poolElements.Add(new EnemyExperienceDropPoolElement
                    {
                        DropEntity = dropEntity
                    });
                }

                continue;
            }

            bool isAttracting = currentDropData.IsAttracting != 0;

            if (isAttracting == false && distanceSquared <= pickupRadiusSquared)
                isAttracting = true;

            if (isAttracting == false)
                continue;

            currentDropData.IsAttracting = 1;
            dropData.ValueRW = currentDropData;

            float attractionSpeed = math.max(0f, currentDropData.AttractionSpeed);

            if (attractionSpeed <= PrecisionEpsilon)
                continue;

            float moveDistance = attractionSpeed * deltaTime;

            if (moveDistance <= PrecisionEpsilon)
                continue;

            LocalTransform updatedTransform = dropTransform.ValueRO;
            float moveDistanceSquared = moveDistance * moveDistance;

            if (distanceSquared <= PrecisionEpsilon)
            {
                updatedTransform.Position = playerPosition;
                dropTransform.ValueRW = updatedTransform;
                continue;
            }

            if (moveDistanceSquared >= distanceSquared)
            {
                updatedTransform.Position = playerPosition;
                dropTransform.ValueRW = updatedTransform;
                continue;
            }

            float inverseDistance = math.rsqrt(distanceSquared);
            float3 moveDirection = toPlayer * inverseDistance;
            updatedTransform.Position += moveDirection * moveDistance;
            dropTransform.ValueRW = updatedTransform;
        }

        if (grantedExperience <= PrecisionEpsilon)
            return;

        playerExperience.Current += grantedExperience;
        entityManager.SetComponentData(playerEntity, playerExperience);
    }
    #endregion

    #endregion
}
