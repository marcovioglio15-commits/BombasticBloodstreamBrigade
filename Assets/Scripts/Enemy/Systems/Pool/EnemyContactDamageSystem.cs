using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies close-range enemy damage to the player using contact and area tick channels.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyProjectileHitSystem))]
public partial struct EnemyContactDamageSystem : ISystem
{
    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyActive>();
        state.RequireForUpdate<EnemyData>();
        state.RequireForUpdate<EnemyRuntimeState>();

        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform, PlayerHealth, PlayerShield, PlayerRuntimeHealthStatisticsConfig, PlayerDamageGraceState>()
            .Build();

        state.RequireForUpdate(playerQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        ComponentLookup<PlayerDashState> dashStateLookup = SystemAPI.GetComponentLookup<PlayerDashState>(true);
        ComponentLookup<EnemyPowerUpStealerVisualState> stealerVisualStateLookup = SystemAPI.GetComponentLookup<EnemyPowerUpStealerVisualState>(false);
        ComponentLookup<EnemyPatternRuntimeState> patternRuntimeStateLookup = SystemAPI.GetComponentLookup<EnemyPatternRuntimeState>(true);
        BufferLookup<EnemyPowerUpStealerConfigElement> stealerConfigLookup = SystemAPI.GetBufferLookup<EnemyPowerUpStealerConfigElement>(false);
        BufferLookup<EnemyPowerUpStealerRuntimeElement> stealerRuntimeLookup = SystemAPI.GetBufferLookup<EnemyPowerUpStealerRuntimeElement>(false);
        EnemyPowerUpStealerPlayerAccess stealerPlayerAccess = new EnemyPowerUpStealerPlayerAccess
        {
            PowerUpsConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsConfig>(false),
            PowerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(false),
            EquippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false),
            PassiveToolsStateLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(false),
            UnlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false),
            ContainerConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpContainerInteractionConfig>(true)
        };
        Entity playerEntity = Entity.Null;
        LocalTransform playerTransform = default;
        PlayerHealth playerHealth = default;
        PlayerShield playerShield = default;
        PlayerRuntimeHealthStatisticsConfig runtimeHealthConfig = default;
        PlayerDamageGraceState playerDamageGraceState = default;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((RefRO<LocalTransform> candidatePlayerTransform,
                  RefRO<PlayerHealth> candidatePlayerHealth,
                  RefRO<PlayerShield> candidatePlayerShield,
                  RefRO<PlayerRuntimeHealthStatisticsConfig> candidateRuntimeHealthConfig,
                  RefRO<PlayerDamageGraceState> candidatePlayerDamageGraceState,
                  Entity candidatePlayerEntity) in SystemAPI.Query<RefRO<LocalTransform>,
                                                                   RefRO<PlayerHealth>,
                                                                   RefRO<PlayerShield>,
                                                                   RefRO<PlayerRuntimeHealthStatisticsConfig>,
                                                                   RefRO<PlayerDamageGraceState>>()
                                                            .WithAll<PlayerControllerConfig>()
                                                            .WithEntityAccess())
        {
            playerEntity = candidatePlayerEntity;
            playerTransform = candidatePlayerTransform.ValueRO;
            playerHealth = candidatePlayerHealth.ValueRO;
            playerShield = candidatePlayerShield.ValueRO;
            runtimeHealthConfig = candidateRuntimeHealthConfig.ValueRO;
            playerDamageGraceState = candidatePlayerDamageGraceState.ValueRO;
            break;
        }

        if (playerEntity == Entity.Null)
            return;

        if (!entityManager.Exists(playerEntity))
            return;

        if (dashStateLookup.HasComponent(playerEntity))
        {
            PlayerDashState dashState = dashStateLookup[playerEntity];

            if (dashState.RemainingInvulnerability > 0f)
                return;
        }

        if (PlayerDamageUtility.IsDamageGraceActive(in playerDamageGraceState, elapsedTime))
            return;

        if (playerHealth.Current <= 0f)
            return;

        float3 playerPosition = playerTransform.Position;
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;
        float accumulatedContactDamage = 0f;
        float accumulatedAreaPercentDamage = 0f;

        if (deltaTime <= 0f)
            return;

        foreach ((RefRO<EnemyData> enemyData,
                  RefRW<EnemyRuntimeState> runtimeState,
                  RefRO<EnemyHealth> enemyHealth,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity) in SystemAPI.Query<RefRO<EnemyData>,
                                                         RefRW<EnemyRuntimeState>,
                                                         RefRO<EnemyHealth>,
                                                         RefRO<LocalTransform>>()
                                                  .WithAll<EnemyActive>()
                                                  .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                                                  .WithEntityAccess())
        {
            EnemyRuntimeState nextState = runtimeState.ValueRO;
            nextState.ContactDamageCooldown -= deltaTime;
            nextState.AreaDamageCooldown -= deltaTime;
            bool hitPlayerThisTick = false;

            if (nextState.ContactDamageCooldown < 0f)
                nextState.ContactDamageCooldown = 0f;

            if (nextState.AreaDamageCooldown < 0f)
                nextState.AreaDamageCooldown = 0f;

            float3 delta = enemyTransform.ValueRO.Position - playerPosition;
            delta.y = 0f;
            float sqrDistance = math.lengthsq(delta);

            if (enemyData.ValueRO.ContactDamageEnabled != 0)
            {
                float contactRadius = math.max(0f, enemyData.ValueRO.ContactRadius);

                if (contactRadius > 0f)
                {
                    float contactRadiusSquared = contactRadius * contactRadius;

                    if (sqrDistance <= contactRadiusSquared && nextState.ContactDamageCooldown <= 0f)
                    {
                        accumulatedContactDamage += math.max(0f, enemyData.ValueRO.ContactAmountPerTick);
                        nextState.ContactDamageCooldown = math.max(0.01f, enemyData.ValueRO.ContactTickInterval);
                        hitPlayerThisTick = true;
                    }
                }
            }

            if (enemyData.ValueRO.AreaDamageEnabled != 0)
            {
                float areaRadius = math.max(0f, enemyData.ValueRO.AreaRadius);

                if (areaRadius > 0f)
                {
                    float areaRadiusSquared = areaRadius * areaRadius;

                    if (sqrDistance <= areaRadiusSquared && nextState.AreaDamageCooldown <= 0f)
                    {
                        accumulatedAreaPercentDamage += math.max(0f, enemyData.ValueRO.AreaAmountPerTickPercent);
                        nextState.AreaDamageCooldown = math.max(0.01f, enemyData.ValueRO.AreaTickInterval);
                        hitPlayerThisTick = true;
                    }
                }
            }

            if (hitPlayerThisTick &&
                stealerConfigLookup.HasBuffer(enemyEntity) &&
                stealerRuntimeLookup.HasBuffer(enemyEntity) &&
                patternRuntimeStateLookup.HasComponent(enemyEntity))
            {
                DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs = stealerConfigLookup[enemyEntity];
                DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime = stealerRuntimeLookup[enemyEntity];
                EnemyPatternRuntimeState patternRuntimeState = patternRuntimeStateLookup[enemyEntity];
                bool stolen = EnemyPowerUpStealerRuntimeUtility.TryStealForTrigger(enemyEntity,
                                                                                   playerEntity,
                                                                                   enemyTransform.ValueRO.Position,
                                                                                   playerPosition,
                                                                                   in nextState,
                                                                                   in patternRuntimeState,
                                                                                   in enemyHealth.ValueRO,
                                                                                   EnemyPowerUpStealTriggerMode.OnFirstPlayerHit,
                                                                                   stealerConfigs,
                                                                                   stealerRuntime,
                                                                                   ref stealerVisualStateLookup,
                                                                                   ref stealerPlayerAccess);

                if (!stolen)
                {
                    EnemyPowerUpStealerRuntimeUtility.TryStealForTrigger(enemyEntity,
                                                                         playerEntity,
                                                                         enemyTransform.ValueRO.Position,
                                                                         playerPosition,
                                                                         in nextState,
                                                                         in patternRuntimeState,
                                                                         in enemyHealth.ValueRO,
                                                                         EnemyPowerUpStealTriggerMode.OnEveryPlayerHit,
                                                                         stealerConfigs,
                                                                         stealerRuntime,
                                                                         ref stealerVisualStateLookup,
                                                                         ref stealerPlayerAccess);
                }
            }

            runtimeState.ValueRW = nextState;
        }

        float maxHealth = math.max(0f, playerHealth.Max);
        float areaDamage = maxHealth * math.max(0f, accumulatedAreaPercentDamage) * 0.01f;
        float totalDamage = math.max(0f, accumulatedContactDamage) + math.max(0f, areaDamage);

        if (totalDamage <= 0f)
            return;

        float previousHealth = playerHealth.Current;
        float previousShield = playerShield.Current;
        bool damageApplied = PlayerDamageUtility.TryApplyFlatShieldDamage(ref playerHealth,
                                                                          ref playerShield,
                                                                          ref playerDamageGraceState,
                                                                          in runtimeHealthConfig,
                                                                          elapsedTime,
                                                                          totalDamage);

        if (!damageApplied)
            return;

        if (canEnqueueAudioRequests)
        {
            if (playerShield.Current < previousShield)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShieldDamage, playerPosition);

            if (playerHealth.Current < previousHealth)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerHealthDamage, playerPosition);
        }

        entityManager.SetComponentData(playerEntity, playerHealth);
        entityManager.SetComponentData(playerEntity, playerShield);
        entityManager.SetComponentData(playerEntity, playerDamageGraceState);
        DamageFlashRuntimeUtility.Trigger(entityManager, playerEntity);
    }
    #endregion

    #endregion
}
