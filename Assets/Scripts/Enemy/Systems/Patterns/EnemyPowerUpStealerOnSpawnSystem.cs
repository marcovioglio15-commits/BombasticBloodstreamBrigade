using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Executes Power-Up Stealer modules configured to steal when the module becomes active.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyBossPatternRuntimeSystem))]
[UpdateBefore(typeof(EnemyContactDamageSystem))]
public partial struct EnemyPowerUpStealerModuleActivationSystem : ISystem
{
    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches player lookup requirements and declares Stealer buffers as runtime dependencies.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        playerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<PlayerControllerConfig,
                     LocalTransform,
                     PlayerPowerUpsConfig,
                     PlayerPowerUpsState,
                     EquippedPassiveToolElement,
                     PlayerPassiveToolsState,
                     PlayerPowerUpUnlockCatalogElement>()
            .Build(ref state);

        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate<EnemyPowerUpStealerConfigElement>();
        state.RequireForUpdate<EnemyPowerUpStealerRuntimeElement>();
    }

    /// <summary>
    /// Attempts module-activation steals for active enemies that have not already consumed their activation trigger.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (playerQuery.IsEmptyIgnoreFilter)
            return;

        Entity playerEntity = playerQuery.GetSingletonEntity();
        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        EnemyPowerUpStealerPlayerAccess playerAccess = new EnemyPowerUpStealerPlayerAccess
        {
            PowerUpsConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsConfig>(false),
            PowerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(false),
            EquippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false),
            PassiveToolsStateLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(false),
            UnlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false),
            ContainerConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpContainerInteractionConfig>(true)
        };
        ComponentLookup<EnemyPowerUpStealerVisualState> visualStateLookup = SystemAPI.GetComponentLookup<EnemyPowerUpStealerVisualState>(false);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRO<EnemyRuntimeState> enemyRuntimeState,
                  RefRO<EnemyPatternRuntimeState> patternRuntimeState,
                  RefRO<EnemyHealth> enemyHealth,
                  RefRO<LocalTransform> enemyTransform,
                  DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs,
                  DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyRuntimeState>,
                                    RefRO<EnemyPatternRuntimeState>,
                                    RefRO<EnemyHealth>,
                                    RefRO<LocalTransform>,
                                    DynamicBuffer<EnemyPowerUpStealerConfigElement>,
                                    DynamicBuffer<EnemyPowerUpStealerRuntimeElement>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                             .WithEntityAccess())
        {
            EnemyPowerUpStealerRuntimeUtility.TryStealForTrigger(enemyEntity,
                                                                 playerEntity,
                                                                 enemyTransform.ValueRO.Position,
                                                                 playerTransform.Position,
                                                                 in enemyRuntimeState.ValueRO,
                                                                 in patternRuntimeState.ValueRO,
                                                                 in enemyHealth.ValueRO,
                                                                 EnemyPowerUpStealTriggerMode.OnModuleActivation,
                                                                 elapsedTime,
                                                                 stealerConfigs,
                                                                 stealerRuntime,
                                                                 ref visualStateLookup,
                                                                 ref playerAccess);
        }
    }
    #endregion

    #endregion
}
