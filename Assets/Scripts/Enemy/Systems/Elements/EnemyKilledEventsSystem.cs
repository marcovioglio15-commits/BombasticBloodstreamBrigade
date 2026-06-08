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
        BufferLookup<EnemyDropItemsModuleSelectionElement> dropItemsSelectionModuleLookup = SystemAPI.GetBufferLookup<EnemyDropItemsModuleSelectionElement>(true);
        ComponentLookup<EnemyDeathVfxConfig> deathVfxConfigLookup = SystemAPI.GetComponentLookup<EnemyDeathVfxConfig>(true);
        ComponentLookup<EnemyDeathPuddleConfig> deathPuddleConfigLookup = SystemAPI.GetComponentLookup<EnemyDeathPuddleConfig>(true);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        DynamicBuffer<EnemyDeathPuddleSpawnRequest> deathPuddleRequests = default;
        bool canQueueDeathPuddles = SystemAPI.TryGetSingletonBuffer(out deathPuddleRequests);

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
            int killedEventIndex = killedEventsBuffer.Length;
            float comboPointMultiplier = ResolveComboPointMultiplier(enemyEntity,
                                                                    killedEventIndex,
                                                                    in runtimeStateLookup,
                                                                    in dropItemsConfigLookup,
                                                                    in rewardMultiplierLookup,
                                                                    in extraComboPointsModuleLookup,
                                                                    in extraComboPointsConditionLookup,
                                                                    in dropItemsSelectionModuleLookup);

            killedEventsBuffer.Add(new EnemyKilledEventElement
            {
                EnemyEntity = enemyEntity,
                Position = killedEventPosition,
                ComboPointMultiplier = comboPointMultiplier
            });
            QueueDeathVfx(enemyEntity,
                          killedEventPosition,
                          in deathVfxConfigLookup,
                          vfxRequestLookup);
            QueueDeathPuddle(enemyEntity,
                             enemyTransform.ValueRO.Position,
                             in enemyData.ValueRO,
                             killedEventIndex,
                             in deathPuddleConfigLookup,
                             canQueueDeathPuddles,
                             deathPuddleRequests);
        }
    }

    /// <summary>
    /// Resolves a stable world position used by death-triggered gameplay VFX spawned from killed events.
    /// </summary>
    /// <param name="enemyPosition">Enemy transform position sampled before pooled finalize-despawn.</param>
    /// <param name="bodyRadius">Enemy body radius used to add a small upward safety offset.</param>
    /// <returns>Adjusted kill-event world position with vertical lift to avoid floor clipping.</returns>
    private static float3 ResolveKilledEventPosition(float3 enemyPosition, float bodyRadius)
    {
        float3 resolvedPosition = enemyPosition;
        float verticalOffset = math.max(0.05f, math.max(0f, bodyRadius) * 0.35f);
        resolvedPosition.y += verticalOffset;
        return resolvedPosition;
    }

    /// <summary>
    /// Queues the optional enemy-authored death VFX before pooled despawn clears runtime buffers.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity that owns the managed VFX request buffer.</param>
    /// <param name="killedEventPosition">Resolved death position shared with gameplay kill events.</param>
    /// <param name="deathVfxConfigLookup">Lookup containing optional death VFX configs.</param>
    /// <param name="vfxRequestLookup">Writable managed VFX request buffers.</param>
    private static void QueueDeathVfx(Entity enemyEntity,
                                      float3 killedEventPosition,
                                      in ComponentLookup<EnemyDeathVfxConfig> deathVfxConfigLookup,
                                      BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (!deathVfxConfigLookup.HasComponent(enemyEntity))
            return;

        if (!vfxRequestLookup.HasBuffer(enemyEntity))
            return;

        EnemyDeathVfxConfig config = deathVfxConfigLookup[enemyEntity];

        if (config.PrefabEntity == Entity.Null && config.Prefab.Value == null)
            return;

        float3 spawnPosition = killedEventPosition + config.SpawnOffset;

        if (!math.all(math.isfinite(spawnPosition)))
            return;

        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = vfxRequestLookup[enemyEntity];
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.PrefabEntity,
            SourcePrefab = config.Prefab,
            Position = spawnPosition,
            Rotation = quaternion.identity,
            UniformScale = math.max(0.01f, config.ScaleMultiplier),
            LifetimeSeconds = math.max(0.05f, config.LifetimeSeconds),
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero,
            HasColorOverride = config.HasDebrisColorOverride,
            ColorOverride = config.DebrisColor,
            SecondaryColorOverride = config.SecondaryDebrisColor,
            ColorOverrideCount = config.DebrisColorCount,
            ColorOverrideChildName = config.DebrisParticleChildName
        });
    }

    /// <summary>
    /// Queues one detached render-only puddle from a confirmed killed enemy before pooled despawn clears its data.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to build a deterministic instance seed.</param>
    /// <param name="enemyPosition">Ground-level enemy transform position captured before recycle.</param>
    /// <param name="enemyData">Enemy footprint data used by footprint-relative puddle sizing.</param>
    /// <param name="killedEventIndex">Current-frame killed event index used to diversify deterministic seeds.</param>
    /// <param name="configLookup">Lookup containing optional baked puddle configs.</param>
    /// <param name="canQueueDeathPuddles">True when the global request buffer exists.</param>
    /// <param name="requests">Global request buffer receiving the resolved puddle instance.</param>
    private static void QueueDeathPuddle(Entity enemyEntity,
                                         float3 enemyPosition,
                                         in EnemyData enemyData,
                                         int killedEventIndex,
                                         in ComponentLookup<EnemyDeathPuddleConfig> configLookup,
                                         bool canQueueDeathPuddles,
                                         DynamicBuffer<EnemyDeathPuddleSpawnRequest> requests)
    {
        if (!canQueueDeathPuddles || !configLookup.HasComponent(enemyEntity))
            return;

        EnemyDeathPuddleConfig config = configLookup[enemyEntity];

        if (config.Enabled == 0 || config.PrefabEntity == Entity.Null)
            return;

        uint seed = math.hash(new uint3((uint)enemyEntity.Index,
                                        (uint)enemyEntity.Version,
                                        (uint)math.max(1, killedEventIndex + 1)));
        float variation = 1f + (HashToSignedUnitFloat(seed) * config.RandomSizeVariation);
        float2 worldSize = config.SizeMode == EnemyDeathPuddleSizeMode.FixedWorldSize
            ? config.FixedWorldSize
            : new float2(math.max(0.05f, enemyData.BodyRadiusX) * 2f,
                         math.max(0.05f, enemyData.BodyRadiusZ) * 2f) * config.FootprintScaleMultiplier;
        float3 spawnPosition = enemyPosition;
        spawnPosition.y += config.GroundOffset;

        requests.Add(new EnemyDeathPuddleSpawnRequest
        {
            PrefabEntity = config.PrefabEntity,
            Position = spawnPosition,
            WorldSize = math.max(new float2(0.05f, 0.05f), worldSize * math.max(0.01f, variation)),
            LifetimeSeconds = config.LifetimeSeconds,
            StableFraction = config.StableFraction,
            FinalScaleRatio = config.FinalScaleRatio,
            EdgeIrregularity = config.EdgeIrregularity,
            BorderWidth = config.BorderWidth,
            EdgeFeather = config.EdgeFeather,
            SecondaryPaletteBlend = config.SecondaryPaletteBlend,
            FlowSpeed = config.FlowSpeed,
            Viscosity = config.Viscosity,
            SurfaceDistortion = config.SurfaceDistortion,
            HighlightStrength = config.HighlightStrength,
            PrimaryColor = config.PrimaryColor,
            SecondaryColor = config.SecondaryColor,
            Seed = seed,
            EvaporationCurve = config.EvaporationCurve,
            RandomRotation = config.RandomRotation
        });
    }

    /// <summary>
    /// Converts a deterministic uint seed into a signed normalized variation.
    /// </summary>
    /// <param name="seed">Source deterministic seed.</param>
    /// <returns>Signed normalized value in the [-1, 1] range.</returns>
    private static float HashToSignedUnitFloat(uint seed)
    {
        uint hashedValue = math.hash(new uint2(seed, 0xC2B2AE35u)) & 0x00FFFFFFu;
        return (hashedValue / 16777215f) * 2f - 1f;
    }

    /// <summary>
    /// Resolves the combo-points multiplier granted by the killed enemy from its baked Extra Combo Points modules.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity.</param>
    /// <param name="killEventIndex">Killed-event index inside the current frame.</param>
    /// <param name="runtimeStateLookup">Lookup used to read enemy runtime timing state.</param>
    /// <param name="dropItemsConfigLookup">Lookup used to read drop-items summary flags.</param>
    /// <param name="rewardMultiplierLookup">Lookup used to read optional special reward multipliers.</param>
    /// <param name="extraComboPointsModuleLookup">Lookup used to read Extra Combo Points module buffers.</param>
    /// <param name="extraComboPointsConditionLookup">Lookup used to read Extra Combo Points condition buffers.</param>
    /// <param name="dropItemsSelectionModuleLookup">Lookup used to read unified Drop Items selection buffers.</param>
    /// <returns>Resolved combo-points multiplier granted by the kill.</returns>
    private static float ResolveComboPointMultiplier(Entity enemyEntity,
                                                     int killEventIndex,
                                                     in ComponentLookup<EnemyRuntimeState> runtimeStateLookup,
                                                     in ComponentLookup<EnemyDropItemsConfig> dropItemsConfigLookup,
                                                     in ComponentLookup<EnemyDropRewardMultiplier> rewardMultiplierLookup,
                                                     in BufferLookup<EnemyExtraComboPointsModuleElement> extraComboPointsModuleLookup,
                                                     in BufferLookup<EnemyExtraComboPointsConditionElement> extraComboPointsConditionLookup,
                                                     in BufferLookup<EnemyDropItemsModuleSelectionElement> dropItemsSelectionModuleLookup)
    {
        if (!runtimeStateLookup.HasComponent(enemyEntity) || !dropItemsConfigLookup.HasComponent(enemyEntity))
            return 1f;

        EnemyRuntimeState runtimeState = runtimeStateLookup[enemyEntity];
        EnemyDropItemsConfig dropItemsConfig = dropItemsConfigLookup[enemyEntity];
        DynamicBuffer<EnemyExtraComboPointsModuleElement> extraComboPointsModules = default;
        DynamicBuffer<EnemyExtraComboPointsConditionElement> extraComboPointsConditions = default;
        DynamicBuffer<EnemyDropItemsModuleSelectionElement> selectionModules = default;

        if (extraComboPointsModuleLookup.HasBuffer(enemyEntity))
            extraComboPointsModules = extraComboPointsModuleLookup[enemyEntity];

        if (extraComboPointsConditionLookup.HasBuffer(enemyEntity))
            extraComboPointsConditions = extraComboPointsConditionLookup[enemyEntity];

        if (dropItemsSelectionModuleLookup.HasBuffer(enemyEntity))
            selectionModules = dropItemsSelectionModuleLookup[enemyEntity];

        float comboMultiplier = EnemyExtraComboPointsRuntimeUtility.ResolveKillComboPointMultiplier(in runtimeState,
                                                                                                    in dropItemsConfig,
                                                                                                    extraComboPointsModules,
                                                                                                    extraComboPointsConditions,
                                                                                                    selectionModules,
                                                                                                    enemyEntity,
                                                                                                    killEventIndex);

        if (!rewardMultiplierLookup.HasComponent(enemyEntity))
            return comboMultiplier;

        EnemyDropRewardMultiplier rewardMultiplier = rewardMultiplierLookup[enemyEntity];
        return comboMultiplier * math.max(0f, rewardMultiplier.ExtraComboPointsMultiplier);
    }
    #endregion

    #endregion
}
