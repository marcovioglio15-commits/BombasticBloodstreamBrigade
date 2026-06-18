using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Moves active reward drops toward the player and grants their payload on collection.
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
                          .WithAll<PlayerHealth>()
                          .WithAll<PlayerShield>()
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
        PlayerHealth playerHealth = entityManager.GetComponentData<PlayerHealth>(playerEntity);
        PlayerShield playerShield = entityManager.GetComponentData<PlayerShield>(playerEntity);
        float remainingExperienceCapacity = PlayerProgressionPhaseUtility.ResolveRemainingExperienceUntilLevelCap(progressionConfig,
                                                                                                                   runtimeGamePhases,
                                                                                                                   playerLevel.Current,
                                                                                                                   playerExperience.Current);

        float deltaTime = SystemAPI.Time.DeltaTime;

        if (deltaTime <= 0f)
            return;

        float pickupRadiusSquared = pickupRadius * pickupRadius;
        float grantedExperience = 0f;
        bool healthChanged = false;
        bool shieldChanged = false;
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
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

            if (currentDropData.RewardKind == EnemyDropPickupRewardKind.Experience &&
                remainingExperienceCapacity <= PrecisionEpsilon)
            {
                if (currentDropData.IsAttracting != 0)
                {
                    currentDropData.IsAttracting = 0;
                    dropData.ValueRW = currentDropData;
                }

                continue;
            }

            if (currentDropData.RewardKind == EnemyDropPickupRewardKind.Recovery &&
                !CanRecoveryDropAffectPlayer(in currentDropData, in playerHealth, in playerShield))
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
                if (currentDropData.RewardKind == EnemyDropPickupRewardKind.Experience)
                {
                    float collectedExperience = math.max(0f, currentDropData.ExperienceAmount);
                    grantedExperience += collectedExperience;
                    remainingExperienceCapacity -= collectedExperience;
                }
                else
                {
                    ApplyRecoveryDrop(in currentDropData,
                                      ref playerHealth,
                                      ref playerShield,
                                      ref healthChanged,
                                      ref shieldChanged);
                }

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

            if (!isAttracting && distanceSquared <= pickupRadiusSquared)
                isAttracting = true;

            if (!isAttracting)
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
        {
            if (healthChanged)
                entityManager.SetComponentData(playerEntity, playerHealth);

            if (shieldChanged)
                entityManager.SetComponentData(playerEntity, playerShield);

            EnqueueRecoveryAudio(healthChanged,
                                 shieldChanged,
                                 playerPosition,
                                 audioRequests,
                                 canEnqueueAudioRequests);
            return;
        }

        playerExperience.Current += grantedExperience;
        entityManager.SetComponentData(playerEntity, playerExperience);

        if (healthChanged)
            entityManager.SetComponentData(playerEntity, playerHealth);

        if (shieldChanged)
            entityManager.SetComponentData(playerEntity, playerShield);

        EnqueueRecoveryAudio(healthChanged,
                             shieldChanged,
                             playerPosition,
                             audioRequests,
                             canEnqueueAudioRequests);
    }
    #endregion

    #region Recovery
    /// <summary>
    /// Resolves whether a recovery drop can currently change player health or shield.
    /// </summary>
    /// <param name="dropData">Drop payload being tested.</param>
    /// <param name="playerHealth">Current player health state.</param>
    /// <param name="playerShield">Current player shield state.</param>
    /// <returns>True when collecting the drop would restore at least one resource.</returns>
    private static bool CanRecoveryDropAffectPlayer(in EnemyExperienceDrop dropData,
                                                    in PlayerHealth playerHealth,
                                                    in PlayerShield playerShield)
    {
        bool canRestoreHealth = dropData.HealthRestoreAmount > 0f &&
                                playerHealth.Current < math.max(0f, playerHealth.Max);
        bool canRestoreShield = dropData.ShieldRestoreAmount > 0f &&
                                playerShield.Current < math.max(0f, playerShield.Max);
        return canRestoreHealth || canRestoreShield;
    }

    /// <summary>
    /// Applies one recovery drop payload to player health and shield using current max values as caps.
    /// </summary>
    /// <param name="dropData">Drop payload being collected.</param>
    /// <param name="playerHealth">Mutable player health state.</param>
    /// <param name="playerShield">Mutable player shield state.</param>
    /// <param name="healthChanged">Mutable flag set when health changed during this frame.</param>
    /// <param name="shieldChanged">Mutable flag set when shield changed during this frame.</param>
    private static void ApplyRecoveryDrop(in EnemyExperienceDrop dropData,
                                          ref PlayerHealth playerHealth,
                                          ref PlayerShield playerShield,
                                          ref bool healthChanged,
                                          ref bool shieldChanged)
    {
        float maxHealth = math.max(0f, playerHealth.Max);
        float maxShield = math.max(0f, playerShield.Max);
        float nextHealth = math.min(maxHealth, math.max(0f, playerHealth.Current) + math.max(0f, dropData.HealthRestoreAmount));
        float nextShield = math.min(maxShield, math.max(0f, playerShield.Current) + math.max(0f, dropData.ShieldRestoreAmount));

        if (nextHealth > playerHealth.Current + PrecisionEpsilon)
        {
            playerHealth.Current = nextHealth;
            healthChanged = true;
        }

        if (nextShield > playerShield.Current + PrecisionEpsilon)
        {
            playerShield.Current = nextShield;
            shieldChanged = true;
        }
    }

    /// <summary>
    /// Enqueues recharge audio for recovery pickups that changed player resources.
    /// </summary>
    /// <param name="healthChanged">True when health was restored.</param>
    /// <param name="shieldChanged">True when shield was restored.</param>
    /// <param name="playerPosition">Current player world position.</param>
    /// <param name="audioRequests">Optional audio request buffer.</param>
    /// <param name="canEnqueueAudioRequests">True when audioRequests is available.</param>
    private static void EnqueueRecoveryAudio(bool healthChanged,
                                             bool shieldChanged,
                                             float3 playerPosition,
                                             DynamicBuffer<GameAudioEventRequest> audioRequests,
                                             bool canEnqueueAudioRequests)
    {
        if (!canEnqueueAudioRequests)
            return;

        if (healthChanged)
            GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerHealthRecharge, playerPosition);

        if (shieldChanged)
            GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShieldRecharge, playerPosition);
    }
    #endregion

    #endregion
}
