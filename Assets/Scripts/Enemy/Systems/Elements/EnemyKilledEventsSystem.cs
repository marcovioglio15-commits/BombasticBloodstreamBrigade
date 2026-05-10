using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

/// <summary>
/// Collects enemy killed events into a singleton buffer for cross-system gameplay triggers.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyDespawnSystem))]
[UpdateAfter(typeof(EnemyBossMinionLifecycleSystem))]
[UpdateAfter(typeof(PlayerPassiveExplosionResolveSystem))]
[UpdateAfter(typeof(PlayerLaserBeamDamageSystem))]
[UpdateAfter(typeof(PlayerElementalTrailResolveSystem))]
[UpdateAfter(typeof(EnemyElementalEffectsSystem))]
[UpdateBefore(typeof(EnemyKillCounterSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyKilledEventsSystem : ISystem
{
    #region Fields
    private Entity killedEventsEntity;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        EntityQuery eventsQuery = state.GetEntityQuery(ComponentType.ReadOnly<EnemyKilledEventElement>());

        if (eventsQuery.IsEmptyIgnoreFilter)
        {
            killedEventsEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<EnemyKilledEventElement>(killedEventsEntity);
            return;
        }

        NativeArray<Entity> entities = eventsQuery.ToEntityArray(Allocator.Temp);
        killedEventsEntity = entities.Length > 0 ? entities[0] : Entity.Null;
        entities.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (killedEventsEntity == Entity.Null || !state.EntityManager.Exists(killedEventsEntity))
        {
            killedEventsEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<EnemyKilledEventElement>(killedEventsEntity);
        }

        DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer = state.EntityManager.GetBuffer<EnemyKilledEventElement>(killedEventsEntity);
        killedEventsBuffer.Clear();
        ComponentLookup<EnemyRuntimeState> runtimeStateLookup = SystemAPI.GetComponentLookup<EnemyRuntimeState>(true);
        ComponentLookup<EnemyDropItemsConfig> dropItemsConfigLookup = SystemAPI.GetComponentLookup<EnemyDropItemsConfig>(true);
        ComponentLookup<EnemyDropRewardMultiplier> rewardMultiplierLookup = SystemAPI.GetComponentLookup<EnemyDropRewardMultiplier>(true);
        BufferLookup<EnemyExtraComboPointsModuleElement> extraComboPointsModuleLookup = SystemAPI.GetBufferLookup<EnemyExtraComboPointsModuleElement>(true);
        BufferLookup<EnemyExtraComboPointsConditionElement> extraComboPointsConditionLookup = SystemAPI.GetBufferLookup<EnemyExtraComboPointsConditionElement>(true);

        foreach ((RefRO<EnemyDespawnRequest> despawnRequest,
                  RefRO<EnemyData> enemyData,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity) in SystemAPI.Query<RefRO<EnemyDespawnRequest>,
                                                         RefRO<EnemyData>,
                                                         RefRO<LocalTransform>>()
                                                 .WithEntityAccess())
        {
            if (despawnRequest.ValueRO.Reason != EnemyDespawnReason.Killed)
                continue;

            float3 killedEventPosition = ResolveKilledEventPosition(enemyTransform.ValueRO.Position, enemyData.ValueRO.BodyRadius);
            float comboPointMultiplier = ResolveComboPointMultiplier(enemyEntity,
                                                                    in runtimeStateLookup,
                                                                    in dropItemsConfigLookup,
                                                                    in rewardMultiplierLookup,
                                                                    in extraComboPointsModuleLookup,
                                                                    in extraComboPointsConditionLookup);

            killedEventsBuffer.Add(new EnemyKilledEventElement
            {
                EnemyEntity = enemyEntity,
                Position = killedEventPosition,
                ComboPointMultiplier = comboPointMultiplier
            });
        }
    }

    /// <summary>
    /// Resolves a stable world position used by death-triggered gameplay VFX spawned from killed events.
    /// </summary>
    /// <param name="enemyPosition">Enemy transform position sampled before pooled finalize-despawn.</param>
    /// <param name="bodyRadius">Enemy body radius used to add a small upward safety offset.</param>
    /// <returns>Adjusted kill-event world position with vertical lift to avoid floor clipping.<returns>
    private static float3 ResolveKilledEventPosition(float3 enemyPosition, float bodyRadius)
    {
        float3 resolvedPosition = enemyPosition;
        float verticalOffset = math.max(0.05f, math.max(0f, bodyRadius) * 0.35f);
        resolvedPosition.y += verticalOffset;
        return resolvedPosition;
    }

    /// <summary>
    /// Resolves the combo-points multiplier granted by the killed enemy from its baked Extra Combo Points modules.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity.</param>
    /// <param name="runtimeStateLookup">Lookup used to read enemy runtime timing state.</param>
    /// <param name="dropItemsConfigLookup">Lookup used to read drop-items summary flags.</param>
    /// <param name="rewardMultiplierLookup">Lookup used to read optional special reward multipliers.</param>
    /// <param name="extraComboPointsModuleLookup">Lookup used to read Extra Combo Points module buffers.</param>
    /// <param name="extraComboPointsConditionLookup">Lookup used to read Extra Combo Points condition buffers.</param>
    /// <returns>Resolved combo-points multiplier granted by the kill.<returns>
    private static float ResolveComboPointMultiplier(Entity enemyEntity,
                                                     in ComponentLookup<EnemyRuntimeState> runtimeStateLookup,
                                                     in ComponentLookup<EnemyDropItemsConfig> dropItemsConfigLookup,
                                                     in ComponentLookup<EnemyDropRewardMultiplier> rewardMultiplierLookup,
                                                     in BufferLookup<EnemyExtraComboPointsModuleElement> extraComboPointsModuleLookup,
                                                     in BufferLookup<EnemyExtraComboPointsConditionElement> extraComboPointsConditionLookup)
    {
        if (!runtimeStateLookup.HasComponent(enemyEntity) || !dropItemsConfigLookup.HasComponent(enemyEntity))
            return 1f;

        EnemyRuntimeState runtimeState = runtimeStateLookup[enemyEntity];
        EnemyDropItemsConfig dropItemsConfig = dropItemsConfigLookup[enemyEntity];
        DynamicBuffer<EnemyExtraComboPointsModuleElement> extraComboPointsModules = default;
        DynamicBuffer<EnemyExtraComboPointsConditionElement> extraComboPointsConditions = default;

        if (extraComboPointsModuleLookup.HasBuffer(enemyEntity))
            extraComboPointsModules = extraComboPointsModuleLookup[enemyEntity];

        if (extraComboPointsConditionLookup.HasBuffer(enemyEntity))
            extraComboPointsConditions = extraComboPointsConditionLookup[enemyEntity];

        float comboMultiplier = EnemyExtraComboPointsRuntimeUtility.ResolveKillComboPointMultiplier(in runtimeState,
                                                                                                    in dropItemsConfig,
                                                                                                    extraComboPointsModules,
                                                                                                    extraComboPointsConditions);

        if (!rewardMultiplierLookup.HasComponent(enemyEntity))
            return comboMultiplier;

        EnemyDropRewardMultiplier rewardMultiplier = rewardMultiplierLookup[enemyEntity];
        return comboMultiplier * math.max(0f, rewardMultiplier.ExtraComboPointsMultiplier);
    }
    #endregion

    #endregion
}
