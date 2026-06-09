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
        ComponentLookup<PlayerLookState> lookStateLookup = SystemAPI.GetComponentLookup<PlayerLookState>(true);
        BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup = SystemAPI.GetBufferLookup<PlayerPowerUpsConfigElement>(true);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        BufferLookup<PlayerOrbitalProjectionPrefabElement> prefabBindingsLookup = SystemAPI.GetBufferLookup<PlayerOrbitalProjectionPrefabElement>(true);
        BufferLookup<PlayerOrbitalProjectionSpawnRequest> spawnRequestLookup = SystemAPI.GetBufferLookup<PlayerOrbitalProjectionSpawnRequest>(false);
        BufferLookup<PlayerOrbitalProjectionLostElement> lostProjectionLookup = SystemAPI.GetBufferLookup<PlayerOrbitalProjectionLostElement>(true);
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
            // FollowPlayerLook respawns slot-align against the player's live look angle so they
            // don't visibly swing from the authored offset toward the current look target.
            PlayerLookState lookState = lookStateLookup.HasComponent(playerEntity)
                ? lookStateLookup[playerEntity]
                : default;
            float currentLookAngleDegrees = PlayerOrbitalProjectionStableLayoutUtility.ResolveLookAngleDegrees(in lookState);
            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools = equippedPassiveToolsLookup.HasBuffer(playerEntity)
                ? equippedPassiveToolsLookup[playerEntity]
                : default;
            DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings = prefabBindingsLookup.HasBuffer(playerEntity)
                ? prefabBindingsLookup[playerEntity]
                : default;
            DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections = lostProjectionLookup.HasBuffer(playerEntity)
                ? lostProjectionLookup[playerEntity]
                : default;

            SyncPersistentProjectionSet(entityManager,
                                        ref commandBuffer,
                                        in transformLookup,
                                        projectionEntities,
                                        projectionInstances,
                                        playerEntity,
                                        playerTransform.ValueRO.Position,
                                        currentLookAngleDegrees,
                                        equippedPassiveTools,
                                        prefabBindings,
                                        lostProjections,
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
                                     currentLookAngleDegrees,
                                     prefabBindings,
                                     lostProjections,
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
    /// <param name="currentLookAngleDegrees">Player's current look angle in degrees, propagated for FollowPlayerLook slot alignment.</param>
    /// <param name="equippedPassiveTools">Equipped passive tool buffer.</param>
    /// <param name="prefabBindings">Player-owned remappable prefab binding table.</param>
    /// <param name="lostProjections">Player-owned permanent loss markers for health-based projections.</param>
    /// <param name="powerUpsConfig">Active slot configuration used for toggle-owned projections.</param>
    /// <param name="powerUpsState">Active slot runtime state used to determine toggle activation.</param>
    private static void SyncPersistentProjectionSet(EntityManager entityManager,
                                                    ref EntityCommandBuffer commandBuffer,
                                                    in ComponentLookup<LocalTransform> transformLookup,
                                                    NativeArray<Entity> projectionEntities,
                                                    NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                    Entity playerEntity,
                                                    float3 playerPosition,
                                                    float currentLookAngleDegrees,
                                                    DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                    DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings,
                                                    DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections,
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
                                              currentLookAngleDegrees,
                                              passiveTool.PowerUpId,
                                              passiveIndex,
                                              prefabBindings,
                                              lostProjections,
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
                                          currentLookAngleDegrees,
                                          powerUpsConfig.PrimarySlot.PowerUpId,
                                          -1,
                                          prefabBindings,
                                          lostProjections,
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
                                          currentLookAngleDegrees,
                                          powerUpsConfig.SecondarySlot.PowerUpId,
                                          -2,
                                          prefabBindings,
                                          lostProjections,
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
    /// <param name="currentLookAngleDegrees">Player's current look angle in degrees, propagated for FollowPlayerLook slot alignment.</param>
    /// <param name="powerUpId">Source power-up identifier used for replacement policy.</param>
    /// <param name="sourceInstanceId">Stable source instance id for the current passive or toggle source.</param>
    /// <param name="prefabBindings">Player-owned remappable prefab binding table.</param>
    /// <param name="lostProjections">Player-owned permanent loss markers for health-based projections.</param>
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
                                                      float currentLookAngleDegrees,
                                                      FixedString64Bytes powerUpId,
                                                      int sourceInstanceId,
                                                      DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings,
                                                      DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections,
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

            if (PlayerOrbitalProjectionLossRuntimeUtility.ShouldSkipPersistentSpawn(lostProjections,
                                                                                    powerUpId,
                                                                                    sourceInstanceId,
                                                                                    in projectionConfig))
            {
                continue;
            }

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

            if (PlayerOrbitalProjectionSpawnRuntimeUtility.HasMatchingProjection(projectionInstances,
                                                                                 playerEntity,
                                                                                 powerUpId,
                                                                                 projectionConfig.ProjectionIndex,
                                                                                 sourceInstanceId,
                                                                                 true))
            {
                continue;
            }

            PlayerOrbitalProjectionSpawnRuntimeUtility.ApplyAcquisitionPolicy(ref commandBuffer,
                                                                              in transformLookup,
                                                                              projectionEntities,
                                                                              projectionInstances,
                                                                              playerEntity,
                                                                              powerUpId,
                                                                              projectionConfig);
            PlayerOrbitalProjectionSpawnRuntimeUtility.SpawnProjection(entityManager,
                                                                       ref commandBuffer,
                                                                       playerEntity,
                                                                       playerPosition,
                                                                       currentLookAngleDegrees,
                                                                       powerUpId,
                                                                       sourceInstanceId,
                                                                       prefabBindings,
                                                                       projectionInstances,
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

            PlayerOrbitalProjectionSpawnRuntimeUtility.BeginDespawn(ref commandBuffer,
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
                    PlayerOrbitalProjectionSpawnRuntimeUtility.ContainsProjection(in passiveTool.Tool, instance.ProjectionIndex))
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
            PlayerOrbitalProjectionSpawnRuntimeUtility.ContainsProjection(in powerUpsConfig.PrimarySlot.TogglePassiveTool, instance.ProjectionIndex))
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
            PlayerOrbitalProjectionSpawnRuntimeUtility.ContainsProjection(in powerUpsConfig.SecondarySlot.TogglePassiveTool, instance.ProjectionIndex))
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
    /// <param name="currentLookAngleDegrees">Player's current look angle in degrees, propagated for FollowPlayerLook slot alignment.</param>
    /// <param name="prefabBindings">Player-owned remappable prefab binding table.</param>
    /// <param name="lostProjections">Player-owned permanent loss markers for persistent health-based projections.</param>
    /// <param name="spawnRequests">Mutable spawn request buffer.</param>
    private static void ConsumeSpawnRequests(EntityManager entityManager,
                                             ref EntityCommandBuffer commandBuffer,
                                             in ComponentLookup<LocalTransform> transformLookup,
                                             NativeArray<Entity> projectionEntities,
                                             NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                             Entity playerEntity,
                                             float3 playerPosition,
                                             float currentLookAngleDegrees,
                                             DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings,
                                             DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections,
                                             DynamicBuffer<PlayerOrbitalProjectionSpawnRequest> spawnRequests)
    {
        for (int requestIndex = 0; requestIndex < spawnRequests.Length; requestIndex++)
        {
            PlayerOrbitalProjectionSpawnRequest request = spawnRequests[requestIndex];
            Entity ownerEntity = request.OwnerEntity != Entity.Null ? request.OwnerEntity : playerEntity;

            for (int configIndex = 0; configIndex < request.Projections.Length; configIndex++)
            {
                OrbitalProjectionConfig projectionConfig = request.Projections[configIndex];

                if (request.Persistent != 0 &&
                    PlayerOrbitalProjectionLossRuntimeUtility.ShouldSkipPersistentSpawn(lostProjections,
                                                                                        request.PowerUpId,
                                                                                        request.SourceInstanceId,
                                                                                        in projectionConfig))
                {
                    continue;
                }

                if (PlayerOrbitalProjectionCategoryRuntimeUtility.ShouldSkipCategorySpawn(in projectionConfig,
                                                                                           projectionInstances,
                                                                                           ownerEntity))
                {
                    continue;
                }

                PlayerOrbitalProjectionSpawnRuntimeUtility.ApplyAcquisitionPolicy(ref commandBuffer,
                                                                                  in transformLookup,
                                                                                  projectionEntities,
                                                                                  projectionInstances,
                                                                                  ownerEntity,
                                                                                  request.PowerUpId,
                                                                                  projectionConfig);
                PlayerOrbitalProjectionSpawnRuntimeUtility.SpawnProjection(entityManager,
                                                                           ref commandBuffer,
                                                                           ownerEntity,
                                                                           playerPosition,
                                                                           currentLookAngleDegrees,
                                                                           request.PowerUpId,
                                                                           request.SourceInstanceId,
                                                                           prefabBindings,
                                                                           projectionInstances,
                                                                           projectionConfig,
                                                                           request.Persistent != 0);
            }
        }

        spawnRequests.Clear();
    }
    #endregion

    #endregion
}
