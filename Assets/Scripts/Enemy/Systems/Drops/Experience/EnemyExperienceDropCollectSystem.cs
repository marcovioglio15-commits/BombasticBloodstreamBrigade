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
    private EntityQuery activeDropQuery;
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
        activeDropQuery = new EntityQueryBuilder(Allocator.Temp)
                          .WithAll<EnemyExperienceDrop, LocalTransform, EnemyExperienceDropActive>()
                          .Build(ref state);
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
                          .WithAll<PlayerPassiveToolsStateElement>()
                          .WithAll<PlayerRunOutcomeState>()
                          .Build(ref state);
        state.RequireForUpdate<EnemyDropCollectionRequestQueue>();
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
        DynamicBuffer<EnemyDropCollectionRequest> dropCollectionRequests =
            SystemAPI.GetSingletonBuffer<EnemyDropCollectionRequest>();
        EnemyDropCollectionRequest pendingRequest = default;
        bool hasPendingRequest = dropCollectionRequests.Length > 0;

        // Consume the bounded command even when there are no drops, preventing stale one-shot pulses.
        if (hasPendingRequest)
        {
            pendingRequest = dropCollectionRequests[0];
            dropCollectionRequests.Clear();
        }

        if (activeDropQuery.CalculateEntityCount() <= 0)
            return;

        bool collectAllImmediately = pendingRequest.CollectAllImmediately != 0;
        PlayerRunOutcomeState runOutcomeState = entityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);

        if (runOutcomeState.IsFinalized != 0 && !collectAllImmediately)
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
        PlayerPassiveToolsStateBufferUtility.Read(entityManager.GetBuffer<PlayerPassiveToolsStateElement>(playerEntity),
                                                  out PlayerPassiveToolsState passiveToolsState);
        float remainingExperienceCapacity = PlayerProgressionPhaseUtility.ResolveRemainingExperienceUntilLevelCap(progressionConfig,
                                                                                                                   runtimeGamePhases,
                                                                                                                   playerLevel.Current,
                                                                                                                   playerExperience.Current);

        float deltaTime = SystemAPI.Time.DeltaTime;

        if (deltaTime <= 0f && !collectAllImmediately)
            return;

        float pickupRadiusSquared = pickupRadius * pickupRadius;
        bool hasPassiveAttraction = passiveToolsState.HasDropAttraction != 0 &&
                                    passiveToolsState.DropAttraction.AttractionRadius > 0f;
        float passiveAttractionRadiusSquared = hasPassiveAttraction
            ? passiveToolsState.DropAttraction.AttractionRadius * passiveToolsState.DropAttraction.AttractionRadius
            : 0f;
        bool hasRequestedAttraction = hasPendingRequest &&
                                      !collectAllImmediately &&
                                      pendingRequest.AttractionRadius > 0f;
        float requestedAttractionRadiusSquared = hasRequestedAttraction
            ? pendingRequest.AttractionRadius * pendingRequest.AttractionRadius
            : 0f;
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
            float3 dropPosition = dropTransform.ValueRO.Position;
            float3 toPlayer = playerPosition - dropPosition;
            toPlayer.y = 0f;
            float distanceSquared = math.lengthsq(toPlayer);
            bool isInsidePassiveAttraction = hasPassiveAttraction &&
                                             distanceSquared <= passiveAttractionRadiusSquared;
            bool isInsideRequestedAttraction = hasRequestedAttraction &&
                                               distanceSquared <= requestedAttractionRadiusSquared;

            // Capture one-shot and passive attraction while a drop is still completing its spawn arc.
            if (isInsidePassiveAttraction || isInsideRequestedAttraction)
            {
                currentDropData.IsAttracting = 1;

                if ((isInsidePassiveAttraction && passiveToolsState.DropAttraction.ConsumeUnusableDrops != 0) ||
                    (isInsideRequestedAttraction && pendingRequest.ConsumeUnusableDrops != 0))
                {
                    currentDropData.ConsumeWhenUnusable = 1;
                }
            }

            float spawnAnimationDuration = math.max(0f, currentDropData.SpawnAnimationDuration);

            if (!collectAllImmediately &&
                spawnAnimationDuration > PrecisionEpsilon &&
                currentDropData.SpawnAnimationElapsed < spawnAnimationDuration)
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

            dropPosition = dropTransform.ValueRO.Position;
            toPlayer = playerPosition - dropPosition;
            toPlayer.y = 0f;
            distanceSquared = math.lengthsq(toPlayer);
            float baseCollectDistance = math.max(0.01f, currentDropData.CollectDistance);
            float collectDistancePerPlayerSpeed = math.max(0f, currentDropData.CollectDistancePerPlayerSpeed);
            float collectDistance = baseCollectDistance + (playerSpeed * collectDistancePerPlayerSpeed);
            float collectDistanceSquared = collectDistance * collectDistance;
            isInsidePassiveAttraction = hasPassiveAttraction && distanceSquared <= passiveAttractionRadiusSquared;
            isInsideRequestedAttraction = hasRequestedAttraction && distanceSquared <= requestedAttractionRadiusSquared;
            bool isInsidePickupRadius = distanceSquared <= pickupRadiusSquared;
            bool canAffectPlayer = CanDropAffectPlayer(in currentDropData,
                                                       remainingExperienceCapacity,
                                                       in playerHealth,
                                                       in playerShield);
            bool consumeWhenUnusable = collectAllImmediately ||
                                       currentDropData.ConsumeWhenUnusable != 0 ||
                                       (isInsidePassiveAttraction && passiveToolsState.DropAttraction.ConsumeUnusableDrops != 0) ||
                                       (isInsideRequestedAttraction && pendingRequest.ConsumeUnusableDrops != 0);

            if (!canAffectPlayer && !consumeWhenUnusable)
            {
                if (currentDropData.IsAttracting != 0)
                {
                    currentDropData.IsAttracting = 0;
                    dropData.ValueRW = currentDropData;
                }

                continue;
            }

            if (consumeWhenUnusable)
                currentDropData.ConsumeWhenUnusable = 1;

            if (collectAllImmediately || distanceSquared <= collectDistanceSquared)
            {
                switch (currentDropData.RewardKind)
                {
                    case EnemyDropPickupRewardKind.Experience:
                        if (canAffectPlayer)
                        {
                            float collectedExperience = math.min(math.max(0f, currentDropData.ExperienceAmount),
                                                                 remainingExperienceCapacity);
                            grantedExperience += collectedExperience;
                            remainingExperienceCapacity -= collectedExperience;
                        }
                        break;
                    case EnemyDropPickupRewardKind.Recovery:
                        if (canAffectPlayer)
                        {
                            ApplyRecoveryDrop(in currentDropData,
                                              ref playerHealth,
                                              ref playerShield,
                                              ref healthChanged,
                                              ref shieldChanged);
                        }
                        break;
                }

                LocalTransform parkedTransform = dropTransform.ValueRO;
                parkedTransform.Position = DropParkingPosition;
                dropTransform.ValueRW = parkedTransform;
                currentDropData.IsAttracting = 0;
                currentDropData.ConsumeWhenUnusable = 0;
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

            if (!isAttracting &&
                (isInsidePickupRadius || isInsidePassiveAttraction || isInsideRequestedAttraction))
            {
                isAttracting = true;
            }

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
    /// Resolves whether one drop can change the current player state before any consume-when-unusable policy is applied.
    /// </summary>
    /// <param name="dropData">Drop payload being tested.</param>
    /// <param name="remainingExperienceCapacity">Experience still accepted before the configured level cap.</param>
    /// <param name="playerHealth">Current player health state.</param>
    /// <param name="playerShield">Current player shield state.</param>
    /// <returns>True when the reward payload can currently change experience, health or shield.</returns>
    private static bool CanDropAffectPlayer(in EnemyExperienceDrop dropData,
                                            float remainingExperienceCapacity,
                                            in PlayerHealth playerHealth,
                                            in PlayerShield playerShield)
    {
        switch (dropData.RewardKind)
        {
            case EnemyDropPickupRewardKind.Experience:
                return dropData.ExperienceAmount > PrecisionEpsilon &&
                       remainingExperienceCapacity > PrecisionEpsilon;
            case EnemyDropPickupRewardKind.Recovery:
                return CanRecoveryDropAffectPlayer(in dropData, in playerHealth, in playerShield);
            default:
                return false;
        }
    }

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
