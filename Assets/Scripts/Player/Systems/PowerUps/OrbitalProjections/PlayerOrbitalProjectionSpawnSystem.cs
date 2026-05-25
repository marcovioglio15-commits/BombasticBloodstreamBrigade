using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns, replaces, and despawns player-owned orbital projections from passive, toggle, and active power-up sources.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
[UpdateAfter(typeof(PlayerComboPassivePowerUpUnlockSystem))]
[UpdateAfter(typeof(PlayerPowerUpCheatSystem))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
[UpdateAfter(typeof(PlayerPowerUpTogglePassiveSystem))]
[UpdateBefore(typeof(PlayerOrbitalProjectionTransformSystem))]
public partial struct PlayerOrbitalProjectionSpawnSystem : ISystem
{
    #region Fields
    private EntityQuery projectionQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the projection query and registers the player power-up config required to find orbital sources.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        projectionQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerOrbitalProjectionInstance>()
            .Build();

        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
    }

    /// <summary>
    /// Synchronizes passive and toggle projections, then consumes optional active projection requests when available.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(true);
        BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup = SystemAPI.GetBufferLookup<PlayerPowerUpsConfigElement>(true);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        BufferLookup<PlayerOrbitalProjectionPrefabElement> prefabBindingsLookup = SystemAPI.GetBufferLookup<PlayerOrbitalProjectionPrefabElement>(true);
        BufferLookup<PlayerOrbitalProjectionSpawnRequest> spawnRequestLookup = SystemAPI.GetBufferLookup<PlayerOrbitalProjectionSpawnRequest>(false);
        NativeArray<Entity> projectionEntities = projectionQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerOrbitalProjectionInstance> projectionInstances = projectionQuery.ToComponentDataArray<PlayerOrbitalProjectionInstance>(Allocator.Temp);

        foreach ((RefRO<LocalTransform> playerTransform,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithAll<PlayerPowerUpsConfigElement>()
                             .WithEntityAccess())
        {
            PlayerPowerUpsConfig powerUpsConfig;
            PlayerPowerUpsConfigBufferUtility.Read(playerEntity,
                                                   in powerUpsConfigLookup,
                                                   out powerUpsConfig);
            PlayerPowerUpsState powerUpsState = powerUpsStateLookup.HasComponent(playerEntity)
                ? powerUpsStateLookup[playerEntity]
                : default;
            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools = equippedPassiveToolsLookup.HasBuffer(playerEntity)
                ? equippedPassiveToolsLookup[playerEntity]
                : default;
            DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings = prefabBindingsLookup.HasBuffer(playerEntity)
                ? prefabBindingsLookup[playerEntity]
                : default;

            SyncPersistentProjectionSet(entityManager,
                                        ref commandBuffer,
                                        in transformLookup,
                                        projectionEntities,
                                        projectionInstances,
                                        playerEntity,
                                        playerTransform.ValueRO.Position,
                                        equippedPassiveTools,
                                        prefabBindings,
                                        in powerUpsConfig,
                                        in powerUpsState);

            if (spawnRequestLookup.HasBuffer(playerEntity))
            {
                DynamicBuffer<PlayerOrbitalProjectionSpawnRequest> spawnRequests = spawnRequestLookup[playerEntity];

                ConsumeSpawnRequests(entityManager,
                                     ref commandBuffer,
                                     in transformLookup,
                                     projectionEntities,
                                     projectionInstances,
                                     playerEntity,
                                     playerTransform.ValueRO.Position,
                                     prefabBindings,
                                     spawnRequests);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
        projectionEntities.Dispose();
        projectionInstances.Dispose();
    }
    #endregion

    #region Persistent Sources
    /// <summary>
    /// Ensures persistent passive and active-toggle orbital projections match the current player loadout.
    /// </summary>
    /// <param name="entityManager">Entity manager used for prefab component checks.</param>
    /// <param name="commandBuffer">Command buffer receiving structural changes.</param>
    /// <param name="transformLookup">Transform lookup used to capture despawn start positions.</param>
    /// <param name="projectionEntities">Snapshot of projection entities alive before this update.</param>
    /// <param name="projectionInstances">Snapshot of projection instance data aligned with projectionEntities.</param>
    /// <param name="playerEntity">Player entity owning the persistent projection sources.</param>
    /// <param name="playerPosition">Current player world position used as spawn origin.</param>
    /// <param name="equippedPassiveTools">Equipped passive tool buffer.</param>
    /// <param name="prefabBindings">Player-owned remappable prefab binding table.</param>
    /// <param name="powerUpsConfig">Active slot configuration used for toggle-owned projections.</param>
    /// <param name="powerUpsState">Active slot runtime state used to determine toggle activation.</param>
    private static void SyncPersistentProjectionSet(EntityManager entityManager,
                                                    ref EntityCommandBuffer commandBuffer,
                                                    in ComponentLookup<LocalTransform> transformLookup,
                                                    NativeArray<Entity> projectionEntities,
                                                    NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                    Entity playerEntity,
                                                    float3 playerPosition,
                                                    DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                    DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings,
                                                    in PlayerPowerUpsConfig powerUpsConfig,
                                                    in PlayerPowerUpsState powerUpsState)
    {
        if (equippedPassiveTools.IsCreated)
        {
            for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
            {
                ref EquippedPassiveToolElement passiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);
                SpawnMissingPersistentConfigs(entityManager,
                                              ref commandBuffer,
                                              in transformLookup,
                                              projectionEntities,
                                              projectionInstances,
                                              playerEntity,
                                              playerPosition,
                                              passiveTool.PowerUpId,
                                              passiveIndex,
                                              prefabBindings,
                                              equippedPassiveTools,
                                              in powerUpsConfig,
                                              in powerUpsState,
                                              in passiveTool.Tool);
            }
        }

        if (powerUpsState.PrimaryIsActive != 0)
            SpawnMissingPersistentConfigs(entityManager,
                                          ref commandBuffer,
                                          in transformLookup,
                                          projectionEntities,
                                          projectionInstances,
                                          playerEntity,
                                          playerPosition,
                                          powerUpsConfig.PrimarySlot.PowerUpId,
                                          -1,
                                          prefabBindings,
                                          equippedPassiveTools,
                                          in powerUpsConfig,
                                          in powerUpsState,
                                          in powerUpsConfig.PrimarySlot.TogglePassiveTool);

        if (powerUpsState.SecondaryIsActive != 0)
            SpawnMissingPersistentConfigs(entityManager,
                                          ref commandBuffer,
                                          in transformLookup,
                                          projectionEntities,
                                          projectionInstances,
                                          playerEntity,
                                          playerPosition,
                                          powerUpsConfig.SecondarySlot.PowerUpId,
                                          -2,
                                          prefabBindings,
                                          equippedPassiveTools,
                                          in powerUpsConfig,
                                          in powerUpsState,
                                          in powerUpsConfig.SecondarySlot.TogglePassiveTool);

        DespawnStalePersistentInstances(ref commandBuffer,
                                        in transformLookup,
                                        projectionEntities,
                                        projectionInstances,
                                        playerEntity,
                                        equippedPassiveTools,
                                        in powerUpsConfig,
                                        in powerUpsState);
    }

    /// <summary>
    /// Spawns persistent projection configs that are not already represented by a live instance.
    /// </summary>
    /// <param name="entityManager">Entity manager used for prefab component checks.</param>
    /// <param name="commandBuffer">Command buffer receiving structural changes.</param>
    /// <param name="transformLookup">Transform lookup used to capture replacement despawn positions.</param>
    /// <param name="projectionEntities">Snapshot of projection entities alive before this update.</param>
    /// <param name="projectionInstances">Snapshot of projection instance data aligned with projectionEntities.</param>
    /// <param name="playerEntity">Player entity owning the projection.</param>
    /// <param name="playerPosition">Current player world position used as spawn origin.</param>
    /// <param name="powerUpId">Source power-up identifier used for replacement policy.</param>
    /// <param name="sourceInstanceId">Stable source instance id for the current passive or toggle source.</param>
    /// <param name="prefabBindings">Player-owned remappable prefab binding table.</param>
    /// <param name="equippedPassiveTools">Equipped passive tools used to detect later replacing sources.</param>
    /// <param name="powerUpsConfig">Active slot configuration used to detect replacing toggles.</param>
    /// <param name="powerUpsState">Active slot state used to detect replacing toggles.</param>
    /// <param name="passiveToolConfig">Passive config containing optional orbital projection entries.</param>
    private static void SpawnMissingPersistentConfigs(EntityManager entityManager,
                                                      ref EntityCommandBuffer commandBuffer,
                                                      in ComponentLookup<LocalTransform> transformLookup,
                                                      NativeArray<Entity> projectionEntities,
                                                      NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                      Entity playerEntity,
                                                      float3 playerPosition,
                                                      FixedString64Bytes powerUpId,
                                                      int sourceInstanceId,
                                                      DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings,
                                                      DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                      in PlayerPowerUpsConfig powerUpsConfig,
                                                      in PlayerPowerUpsState powerUpsState,
                                                      in PlayerPassiveToolConfig passiveToolConfig)
    {
        if (passiveToolConfig.IsDefined == 0 || passiveToolConfig.HasOrbitalProjections == 0)
            return;

        for (int configIndex = 0; configIndex < passiveToolConfig.OrbitalProjections.Length; configIndex++)
        {
            OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[configIndex];

            if (IsPersistentConfigSuperseded(sourceInstanceId,
                                             powerUpId,
                                             in projectionConfig,
                                             equippedPassiveTools,
                                             in powerUpsConfig,
                                             in powerUpsState))
            {
                continue;
            }

            if (PlayerOrbitalProjectionCategoryRuntimeUtility.ShouldSkipCategorySpawn(in projectionConfig,
                                                                                       projectionInstances,
                                                                                       playerEntity))
            {
                continue;
            }

            if (HasMatchingProjection(projectionInstances,
                                      playerEntity,
                                      powerUpId,
                                      projectionConfig.ProjectionIndex,
                                      sourceInstanceId,
                                      true))
            {
                continue;
            }

            ApplyAcquisitionPolicy(ref commandBuffer,
                                   in transformLookup,
                                   projectionEntities,
                                   projectionInstances,
                                   playerEntity,
                                   powerUpId,
                                   projectionConfig);
            SpawnProjection(entityManager,
                            ref commandBuffer,
                            playerEntity,
                            playerPosition,
                            powerUpId,
                            sourceInstanceId,
                            prefabBindings,
                            projectionConfig,
                            true);
        }
    }

    /// <summary>
    /// Starts despawn animation for persistent instances whose source is no longer active.
    /// </summary>
    /// <param name="commandBuffer">Command buffer receiving component updates.</param>
    /// <param name="transformLookup">Transform lookup used to capture despawn start positions.</param>
    /// <param name="projectionEntities">Snapshot of projection entities alive before this update.</param>
    /// <param name="projectionInstances">Snapshot of projection instance data aligned with projectionEntities.</param>
    /// <param name="playerEntity">Player entity owning the projection.</param>
    /// <param name="equippedPassiveTools">Equipped passive tool buffer.</param>
    /// <param name="powerUpsConfig">Active slot configuration used for toggle-owned projections.</param>
    /// <param name="powerUpsState">Active slot runtime state used to determine toggle activation.</param>
    private static void DespawnStalePersistentInstances(ref EntityCommandBuffer commandBuffer,
                                                        in ComponentLookup<LocalTransform> transformLookup,
                                                        NativeArray<Entity> projectionEntities,
                                                        NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                        Entity playerEntity,
                                                        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                        in PlayerPowerUpsConfig powerUpsConfig,
                                                        in PlayerPowerUpsState powerUpsState)
    {
        for (int instanceIndex = 0; instanceIndex < projectionInstances.Length; instanceIndex++)
        {
            PlayerOrbitalProjectionInstance instance = projectionInstances[instanceIndex];

            if (instance.OwnerEntity != playerEntity || instance.Persistent == 0)
                continue;

            if (instance.Phase == PlayerOrbitalProjectionPhase.Despawning)
                continue;

            if (IsPersistentSourceActive(instance,
                                         equippedPassiveTools,
                                         in powerUpsConfig,
                                         in powerUpsState))
            {
                continue;
            }

            BeginDespawn(ref commandBuffer,
                         in transformLookup,
                         projectionEntities[instanceIndex],
                         instance);
        }
    }

    /// <summary>
    /// Checks whether one persistent projection still has an active source in loadout or toggles.
    /// </summary>
    /// <param name="instance">Projection instance being checked.</param>
    /// <param name="equippedPassiveTools">Equipped passive tool buffer.</param>
    /// <param name="powerUpsConfig">Active slot configuration used for toggle-owned projections.</param>
    /// <param name="powerUpsState">Active slot runtime state used to determine toggle activation.</param>
    /// <returns>True when the source config is still active.</returns>
    private static bool IsPersistentSourceActive(PlayerOrbitalProjectionInstance instance,
                                                 DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                 in PlayerPowerUpsConfig powerUpsConfig,
                                                 in PlayerPowerUpsState powerUpsState)
    {
        if (equippedPassiveTools.IsCreated)
        {
            for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
            {
                ref EquippedPassiveToolElement passiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);

                if (instance.SourceInstanceId == passiveIndex &&
                    passiveTool.PowerUpId == instance.PowerUpId &&
                    ContainsProjection(passiveTool.Tool, instance.ProjectionIndex))
                {
                    return !IsPersistentConfigSuperseded(instance.SourceInstanceId,
                                                         instance.PowerUpId,
                                                         in instance.Config,
                                                         equippedPassiveTools,
                                                         in powerUpsConfig,
                                                         in powerUpsState);
                }
            }
        }

        if (powerUpsState.PrimaryIsActive != 0 &&
            instance.SourceInstanceId == -1 &&
            powerUpsConfig.PrimarySlot.PowerUpId == instance.PowerUpId &&
            ContainsProjection(powerUpsConfig.PrimarySlot.TogglePassiveTool, instance.ProjectionIndex))
        {
            return !IsPersistentConfigSuperseded(instance.SourceInstanceId,
                                                 instance.PowerUpId,
                                                 in instance.Config,
                                                 equippedPassiveTools,
                                                 in powerUpsConfig,
                                                 in powerUpsState);
        }

        if (powerUpsState.SecondaryIsActive != 0 &&
            instance.SourceInstanceId == -2 &&
            powerUpsConfig.SecondarySlot.PowerUpId == instance.PowerUpId &&
            ContainsProjection(powerUpsConfig.SecondarySlot.TogglePassiveTool, instance.ProjectionIndex))
        {
            return !IsPersistentConfigSuperseded(instance.SourceInstanceId,
                                                 instance.PowerUpId,
                                                 in instance.Config,
                                                 equippedPassiveTools,
                                                 in powerUpsConfig,
                                                 in powerUpsState);
        }

        return false;
    }

    /// <summary>
    /// Resolves whether a later persistent source replaces the current projection config before it can spawn or remain active.
    /// </summary>
    /// <param name="sourceInstanceId">Source instance id of the projection being evaluated.</param>
    /// <param name="powerUpId">Power-up id of the projection being evaluated.</param>
    /// <param name="projectionConfig">Projection config being evaluated.</param>
    /// <param name="equippedPassiveTools">Equipped passive tools ordered by acquisition.</param>
    /// <param name="powerUpsConfig">Active slot configuration used for toggle-owned projection sources.</param>
    /// <param name="powerUpsState">Active slot runtime state used to determine enabled toggles.</param>
    /// <returns>True when a later ReplaceAll or matching ReplaceMatchingPowerUp source supersedes this projection.</returns>
    private static bool IsPersistentConfigSuperseded(int sourceInstanceId,
                                                     FixedString64Bytes powerUpId,
                                                     in OrbitalProjectionConfig projectionConfig,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                     in PlayerPowerUpsConfig powerUpsConfig,
                                                     in PlayerPowerUpsState powerUpsState)
    {
        int passiveCount = equippedPassiveTools.IsCreated ? equippedPassiveTools.Length : 0;
        int sourceOrder = ResolvePersistentSourceOrder(sourceInstanceId, passiveCount);

        if (sourceOrder < 0)
            return false;

        if (equippedPassiveTools.IsCreated)
        {
            for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
            {
                if (passiveIndex <= sourceOrder)
                    continue;

                ref EquippedPassiveToolElement passiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);

                if (DoesSourceSupersedeProjection(passiveTool.PowerUpId,
                                                  in passiveTool.Tool,
                                                  powerUpId,
                                                  in projectionConfig))
                {
                    return true;
                }
            }
        }

        if (powerUpsState.PrimaryIsActive != 0 &&
            passiveCount > sourceOrder &&
            DoesSourceSupersedeProjection(powerUpsConfig.PrimarySlot.PowerUpId,
                                          in powerUpsConfig.PrimarySlot.TogglePassiveTool,
                                          powerUpId,
                                          in projectionConfig))
        {
            return true;
        }

        if (powerUpsState.SecondaryIsActive != 0 &&
            passiveCount + 1 > sourceOrder &&
            DoesSourceSupersedeProjection(powerUpsConfig.SecondarySlot.PowerUpId,
                                          in powerUpsConfig.SecondarySlot.TogglePassiveTool,
                                          powerUpId,
                                          in projectionConfig))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Converts passive and toggle source ids into their deterministic persistent-source order.
    /// </summary>
    /// <param name="sourceInstanceId">Runtime source id stored on the projection instance.</param>
    /// <param name="passiveCount">Number of equipped passive sources currently active.</param>
    /// <returns>Source order, or -1 when the source id is not persistent.</returns>
    private static int ResolvePersistentSourceOrder(int sourceInstanceId, int passiveCount)
    {
        if (sourceInstanceId >= 0)
            return sourceInstanceId < passiveCount ? sourceInstanceId : -1;

        switch (sourceInstanceId)
        {
            case -1:
                return passiveCount;
            case -2:
                return passiveCount + 1;
            default:
                return -1;
        }
    }

    /// <summary>
    /// Checks whether one candidate source owns a replacement policy that supersedes the inspected projection.
    /// </summary>
    /// <param name="candidatePowerUpId">Power-up id for the later source.</param>
    /// <param name="candidateTool">Passive-tool payload for the later source.</param>
    /// <param name="powerUpId">Power-up id of the inspected projection.</param>
    /// <param name="projectionConfig">Projection config being inspected.</param>
    /// <returns>True when the later source replaces all projections or the matching projection from the same PowerUpId.</returns>
    private static bool DoesSourceSupersedeProjection(FixedString64Bytes candidatePowerUpId,
                                                      in PlayerPassiveToolConfig candidateTool,
                                                      FixedString64Bytes powerUpId,
                                                      in OrbitalProjectionConfig projectionConfig)
    {
        if (candidateTool.IsDefined == 0 ||
            candidateTool.HasOrbitalProjections == 0)
        {
            return false;
        }

        for (int configIndex = 0; configIndex < candidateTool.OrbitalProjections.Length; configIndex++)
        {
            OrbitalProjectionConfig candidateProjection = candidateTool.OrbitalProjections[configIndex];

            switch (candidateProjection.AcquisitionPolicy)
            {
                case OrbitalProjectionAcquisitionPolicy.ReplaceAllOrbitalProjections:
                    return true;
                case OrbitalProjectionAcquisitionPolicy.ReplaceMatchingPowerUp:
                    if (candidatePowerUpId == powerUpId &&
                        candidateProjection.ProjectionIndex == projectionConfig.ProjectionIndex)
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }
    #endregion

    #region Active Requests
    /// <summary>
    /// Consumes timed active orbital projection spawn requests.
    /// </summary>
    /// <param name="entityManager">Entity manager used for prefab component checks.</param>
    /// <param name="commandBuffer">Command buffer receiving structural changes.</param>
    /// <param name="transformLookup">Transform lookup used to capture replacement despawn positions.</param>
    /// <param name="projectionEntities">Snapshot of projection entities alive before this update.</param>
    /// <param name="projectionInstances">Snapshot of projection instance data aligned with projectionEntities.</param>
    /// <param name="playerEntity">Player entity that owns the request buffer.</param>
    /// <param name="playerPosition">Current player world position used as spawn origin.</param>
    /// <param name="prefabBindings">Player-owned remappable prefab binding table.</param>
    /// <param name="spawnRequests">Mutable spawn request buffer.</param>
    private static void ConsumeSpawnRequests(EntityManager entityManager,
                                             ref EntityCommandBuffer commandBuffer,
                                             in ComponentLookup<LocalTransform> transformLookup,
                                             NativeArray<Entity> projectionEntities,
                                             NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                             Entity playerEntity,
                                             float3 playerPosition,
                                             DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings,
                                             DynamicBuffer<PlayerOrbitalProjectionSpawnRequest> spawnRequests)
    {
        for (int requestIndex = 0; requestIndex < spawnRequests.Length; requestIndex++)
        {
            PlayerOrbitalProjectionSpawnRequest request = spawnRequests[requestIndex];
            Entity ownerEntity = request.OwnerEntity != Entity.Null ? request.OwnerEntity : playerEntity;

            for (int configIndex = 0; configIndex < request.Projections.Length; configIndex++)
            {
                OrbitalProjectionConfig projectionConfig = request.Projections[configIndex];

                if (PlayerOrbitalProjectionCategoryRuntimeUtility.ShouldSkipCategorySpawn(in projectionConfig,
                                                                                           projectionInstances,
                                                                                           ownerEntity))
                {
                    continue;
                }

                ApplyAcquisitionPolicy(ref commandBuffer,
                                       in transformLookup,
                                       projectionEntities,
                                       projectionInstances,
                                       ownerEntity,
                                       request.PowerUpId,
                                       projectionConfig);
                SpawnProjection(entityManager,
                                ref commandBuffer,
                                ownerEntity,
                                playerPosition,
                                request.PowerUpId,
                                request.SourceInstanceId,
                                prefabBindings,
                                projectionConfig,
                                request.Persistent != 0);
            }
        }

        spawnRequests.Clear();
    }
    #endregion

    #region Spawn Helpers
    /// <summary>
    /// Applies replacement behavior before one projection instance is spawned.
    /// </summary>
    /// <param name="commandBuffer">Command buffer receiving despawn component updates.</param>
    /// <param name="transformLookup">Transform lookup used to capture despawn start positions.</param>
    /// <param name="projectionEntities">Snapshot of projection entities alive before this update.</param>
    /// <param name="projectionInstances">Snapshot of projection instance data aligned with projectionEntities.</param>
    /// <param name="playerEntity">Player entity owning the projection.</param>
    /// <param name="powerUpId">Source power-up identifier used for matching policy.</param>
    /// <param name="projectionConfig">Projection config being spawned.</param>
    private static void ApplyAcquisitionPolicy(ref EntityCommandBuffer commandBuffer,
                                               in ComponentLookup<LocalTransform> transformLookup,
                                               NativeArray<Entity> projectionEntities,
                                               NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                               Entity playerEntity,
                                               FixedString64Bytes powerUpId,
                                               in OrbitalProjectionConfig projectionConfig)
    {
        if (projectionConfig.AcquisitionPolicy == OrbitalProjectionAcquisitionPolicy.Additive)
            return;

        for (int instanceIndex = 0; instanceIndex < projectionInstances.Length; instanceIndex++)
        {
            PlayerOrbitalProjectionInstance instance = projectionInstances[instanceIndex];

            if (instance.OwnerEntity != playerEntity)
                continue;

            if (instance.Phase == PlayerOrbitalProjectionPhase.Despawning)
                continue;

            if (projectionConfig.AcquisitionPolicy == OrbitalProjectionAcquisitionPolicy.ReplaceMatchingPowerUp &&
                (instance.PowerUpId != powerUpId || instance.ProjectionIndex != projectionConfig.ProjectionIndex))
            {
                continue;
            }

            BeginDespawn(ref commandBuffer,
                         in transformLookup,
                         projectionEntities[instanceIndex],
                         instance);
        }
    }

    /// <summary>
    /// Creates one orbital projection entity at the player position.
    /// </summary>
    /// <param name="entityManager">Entity manager used for prefab component checks.</param>
    /// <param name="commandBuffer">Command buffer receiving the spawn.</param>
    /// <param name="playerEntity">Owner player entity.</param>
    /// <param name="playerPosition">World-space spawn origin.</param>
    /// <param name="powerUpId">Source power-up identifier.</param>
    /// <param name="sourceInstanceId">Source instance id used for persistent matching.</param>
    /// <param name="prefabBindings">Player-owned remappable prefab binding table.</param>
    /// <param name="projectionConfig">Projection config baked from the module payload.</param>
    /// <param name="persistent">True for passive and toggle-owned projections.</param>
    private static void SpawnProjection(EntityManager entityManager,
                                        ref EntityCommandBuffer commandBuffer,
                                        Entity playerEntity,
                                        float3 playerPosition,
                                        FixedString64Bytes powerUpId,
                                        int sourceInstanceId,
                                        DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings,
                                        in OrbitalProjectionConfig projectionConfig,
                                        bool persistent)
    {
        Entity prefabEntity = ResolvePrefabEntity(entityManager, in projectionConfig, prefabBindings);
        Entity projectionEntity = prefabEntity != Entity.Null
            ? commandBuffer.Instantiate(prefabEntity)
            : commandBuffer.CreateEntity();
        OrbitalProjectionConfig runtimeProjectionConfig = projectionConfig;
        LocalTransform spawnTransform = LocalTransform.FromPosition(playerPosition);

        runtimeProjectionConfig.PrefabEntity = prefabEntity;

        if (prefabEntity != Entity.Null && entityManager.HasComponent<LocalTransform>(prefabEntity))
            commandBuffer.SetComponent(projectionEntity, spawnTransform);
        else
            commandBuffer.AddComponent(projectionEntity, spawnTransform);

        commandBuffer.AddComponent(projectionEntity, new PlayerOrbitalProjectionInstance
        {
            OwnerEntity = playerEntity,
            PowerUpId = powerUpId,
            ProjectionIndex = projectionConfig.ProjectionIndex,
            SourceInstanceId = sourceInstanceId,
            Persistent = persistent ? (byte)1 : (byte)0,
            Phase = PlayerOrbitalProjectionPhase.Spawning,
            Config = runtimeProjectionConfig,
            RemainingLifetimeSeconds = persistent ? 0f : math.max(0.05f, projectionConfig.ActiveDurationSeconds),
            CurrentHealth = projectionConfig.HasHealth != 0 ? math.max(0.01f, projectionConfig.MaximumHealth) : float.MaxValue,
            AngleDegrees = projectionConfig.BounceInsideOrbitCone != 0
                ? projectionConfig.OrbitConeCenterAngleDegrees
                : projectionConfig.AngleOffsetDegrees,
            FollowAngleDegrees = projectionConfig.AngleOffsetDegrees,
            OrbitBounceDirection = projectionConfig.OrbitSpeedDegreesPerSecond < 0f ? (sbyte)-1 : (sbyte)1,
            PhaseElapsedSeconds = 0f,
            DespawnStartPosition = playerPosition
        });
        if (prefabEntity != Entity.Null &&
            entityManager.HasBuffer<PlayerOrbitalProjectionEnemyContactElement>(prefabEntity))
            commandBuffer.SetBuffer<PlayerOrbitalProjectionEnemyContactElement>(projectionEntity);
        else
            commandBuffer.AddBuffer<PlayerOrbitalProjectionEnemyContactElement>(projectionEntity);
    }

    /// <summary>
    /// Resolves the remapped prefab entity for one projection config.
    /// </summary>
    /// <param name="entityManager">Entity manager used to validate entity references before recording ECB commands.</param>
    /// <param name="projectionConfig">Projection config containing the baked binding index and legacy direct entity fallback.</param>
    /// <param name="prefabBindings">Player-owned remappable prefab binding table.</param>
    /// <returns>Valid prefab entity, or Entity.Null when the projection must spawn as logic-only.</returns>
    private static Entity ResolvePrefabEntity(EntityManager entityManager,
                                              in OrbitalProjectionConfig projectionConfig,
                                              DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings)
    {
        if (projectionConfig.PrefabEntity != Entity.Null &&
            projectionConfig.PrefabBindingIndex >= 0 &&
            prefabBindings.IsCreated)
        {
            for (int bindingIndex = 0; bindingIndex < prefabBindings.Length; bindingIndex++)
            {
                PlayerOrbitalProjectionPrefabElement binding = prefabBindings[bindingIndex];

                if (binding.BindingIndex != projectionConfig.PrefabBindingIndex)
                    continue;

                if (binding.PrefabEntity != Entity.Null && entityManager.Exists(binding.PrefabEntity))
                    return binding.PrefabEntity;

                return Entity.Null;
            }
        }

        if (projectionConfig.PrefabEntity != Entity.Null && entityManager.Exists(projectionConfig.PrefabEntity))
            return projectionConfig.PrefabEntity;

        return Entity.Null;
    }

    /// <summary>
    /// Moves one live projection into despawn animation phase.
    /// </summary>
    /// <param name="commandBuffer">Command buffer receiving the component update.</param>
    /// <param name="transformLookup">Transform lookup used to capture the current projection position.</param>
    /// <param name="projectionEntity">Projection entity to despawn.</param>
    /// <param name="instance">Current projection instance state.</param>
    private static void BeginDespawn(ref EntityCommandBuffer commandBuffer,
                                     in ComponentLookup<LocalTransform> transformLookup,
                                     Entity projectionEntity,
                                     PlayerOrbitalProjectionInstance instance)
    {
        instance.Phase = PlayerOrbitalProjectionPhase.Despawning;
        instance.PhaseElapsedSeconds = 0f;

        if (transformLookup.HasComponent(projectionEntity))
            instance.DespawnStartPosition = transformLookup[projectionEntity].Position;

        commandBuffer.SetComponent(projectionEntity, instance);
    }

    /// <summary>
    /// Checks whether a matching non-despawning projection exists in a snapshot.
    /// </summary>
    /// <param name="projectionInstances">Snapshot of projection instance data.</param>
    /// <param name="playerEntity">Owner player entity.</param>
    /// <param name="powerUpId">Source power-up identifier.</param>
    /// <param name="projectionIndex">Projection index inside the source module.</param>
    /// <param name="sourceInstanceId">Source instance id used for persistent matching.</param>
    /// <param name="persistentOnly">True to ignore timed active projections.</param>
    /// <returns>True when a matching projection already exists.</returns>
    private static bool HasMatchingProjection(NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                              Entity playerEntity,
                                              FixedString64Bytes powerUpId,
                                              int projectionIndex,
                                              int sourceInstanceId,
                                              bool persistentOnly)
    {
        for (int instanceIndex = 0; instanceIndex < projectionInstances.Length; instanceIndex++)
        {
            PlayerOrbitalProjectionInstance instance = projectionInstances[instanceIndex];

            if (instance.OwnerEntity != playerEntity)
                continue;

            if (instance.Phase == PlayerOrbitalProjectionPhase.Despawning)
                continue;

            if (persistentOnly && instance.Persistent == 0)
                continue;

            if (instance.PowerUpId == powerUpId &&
                instance.ProjectionIndex == projectionIndex &&
                instance.SourceInstanceId == sourceInstanceId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether one passive config still contains a projection index.
    /// </summary>
    /// <param name="passiveToolConfig">Passive config being inspected.</param>
    /// <param name="projectionIndex">Projection index to find.</param>
    /// <returns>True when the config contains the projection index.</returns>
    private static bool ContainsProjection(in PlayerPassiveToolConfig passiveToolConfig, int projectionIndex)
    {
        if (passiveToolConfig.IsDefined == 0 || passiveToolConfig.HasOrbitalProjections == 0)
            return false;

        for (int configIndex = 0; configIndex < passiveToolConfig.OrbitalProjections.Length; configIndex++)
        {
            if (passiveToolConfig.OrbitalProjections[configIndex].ProjectionIndex == projectionIndex)
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
