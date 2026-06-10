using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Provides spawn and replacement helpers for player-owned orbital projection entities.
/// </summary>
internal static class PlayerOrbitalProjectionSpawnRuntimeUtility
{
    #region Methods

    #region Spawn
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
    public static void ApplyAcquisitionPolicy(ref EntityCommandBuffer commandBuffer,
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
    /// Creates one orbital projection entity at the player position. Reserves the monotonic stable
    /// order key first and uses it together with the current look angle to compute a slot-aligned
    /// initial angle when the projection joins an existing Independent Orbit or Follow Player Look
    /// ring, so respawned projections do not force the ring into a violent layout snap. FollowPlayer
    /// Look projections also seed FollowAngleDegrees with the initial target so no blend swing
    /// occurs when the player is mid-turn at respawn time.
    /// </summary>
    /// <param name="entityManager">Entity manager used for prefab component checks.</param>
    /// <param name="commandBuffer">Command buffer receiving the spawn.</param>
    /// <param name="playerEntity">Owner player entity.</param>
    /// <param name="playerPosition">World-space spawn origin.</param>
    /// <param name="currentLookAngleDegrees">Player's current look angle in degrees at spawn time.</param>
    /// <param name="powerUpId">Source power-up identifier.</param>
    /// <param name="sourceInstanceId">Source instance id used for persistent matching.</param>
    /// <param name="prefabBindings">Player-owned remappable prefab binding table.</param>
    /// <param name="hullVertices">Player-owned binding-indexed hull table for model-shaped collision.</param>
    /// <param name="projectionInstances">Live projection snapshot used to slot the new projection.</param>
    /// <param name="projectionConfig">Projection config baked from the module payload.</param>
    /// <param name="persistent">True for passive and toggle-owned projections.</param>
    public static void SpawnProjection(EntityManager entityManager,
                                       ref EntityCommandBuffer commandBuffer,
                                       Entity playerEntity,
                                       float3 playerPosition,
                                       float currentLookAngleDegrees,
                                       FixedString64Bytes powerUpId,
                                       int sourceInstanceId,
                                       DynamicBuffer<PlayerOrbitalProjectionPrefabElement> prefabBindings,
                                       DynamicBuffer<PlayerOrbitalProjectionHullVertexElement> hullVertices,
                                       NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                       in OrbitalProjectionConfig projectionConfig,
                                       bool persistent)
    {
        Entity prefabEntity = ResolvePrefabEntity(entityManager, in projectionConfig, prefabBindings);
        Entity projectionEntity = prefabEntity != Entity.Null
            ? commandBuffer.Instantiate(prefabEntity)
            : commandBuffer.CreateEntity();
        OrbitalProjectionConfig runtimeProjectionConfig = projectionConfig;
        LocalTransform spawnTransform = LocalTransform.FromPosition(playerPosition);
        int stableOrderKey = PlayerOrbitalProjectionStableLayoutUtility.ReserveNextStableOrderKey();
        float initialAngleDegrees = PlayerOrbitalProjectionStableLayoutUtility.ResolveInitialAngleDegrees(in projectionConfig,
                                                                                                          stableOrderKey,
                                                                                                          playerEntity,
                                                                                                          currentLookAngleDegrees,
                                                                                                          projectionInstances);
        // FollowPlayerLook reads FollowAngleDegrees as the smoothed value before applying it to the
        // visible AngleDegrees, so seeding it to the slot-aligned initial value avoids the otherwise
        // visible swing from AngleOffsetDegrees toward the live target.
        float initialFollowAngleDegrees = projectionConfig.MotionMode == OrbitalProjectionMotionMode.FollowPlayerLook
            ? initialAngleDegrees
            : projectionConfig.AngleOffsetDegrees;

        runtimeProjectionConfig.PrefabEntity = prefabEntity;

        // Copy the baked model silhouette onto this instance and derive its broad-phase radius;
        // when fewer than three vertices exist the flag downgrades to the plain Collision Radius.
        DynamicBuffer<PlayerOrbitalProjectionCollisionVertexElement> instanceHull = commandBuffer.AddBuffer<PlayerOrbitalProjectionCollisionVertexElement>(projectionEntity);

        if (runtimeProjectionConfig.AdaptCollisionToModel != 0)
        {
            float boundingRadiusSq = 0f;

            if (hullVertices.IsCreated && runtimeProjectionConfig.PrefabBindingIndex >= 0)
            {
                for (int vertexIndex = 0; vertexIndex < hullVertices.Length; vertexIndex++)
                {
                    PlayerOrbitalProjectionHullVertexElement hullVertex = hullVertices[vertexIndex];

                    if (hullVertex.BindingIndex != runtimeProjectionConfig.PrefabBindingIndex)
                        continue;

                    instanceHull.Add(new PlayerOrbitalProjectionCollisionVertexElement
                    {
                        LocalPositionXZ = hullVertex.LocalPositionXZ
                    });
                    boundingRadiusSq = math.max(boundingRadiusSq, math.lengthsq(hullVertex.LocalPositionXZ));
                }
            }

            if (instanceHull.Length >= 3)
                runtimeProjectionConfig.ModelCollisionBoundingRadius = math.sqrt(boundingRadiusSq);
            else
                runtimeProjectionConfig.AdaptCollisionToModel = 0;
        }

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
            StableOrderKey = stableOrderKey,
            Persistent = persistent ? (byte)1 : (byte)0,
            Phase = PlayerOrbitalProjectionPhase.Spawning,
            Config = runtimeProjectionConfig,
            RemainingLifetimeSeconds = persistent ? 0f : math.max(0.05f, projectionConfig.ActiveDurationSeconds),
            CurrentHealth = projectionConfig.HasHealth != 0 ? math.max(0.01f, projectionConfig.MaximumHealth) : float.MaxValue,
            AngleDegrees = initialAngleDegrees,
            FollowAngleDegrees = initialFollowAngleDegrees,
            FollowAngularVelocityDegrees = 0f,
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
    #endregion

    #region State
    /// <summary>
    /// Moves one live projection into despawn animation phase.
    /// </summary>
    /// <param name="commandBuffer">Command buffer receiving the component update.</param>
    /// <param name="transformLookup">Transform lookup used to capture the current projection position.</param>
    /// <param name="projectionEntity">Projection entity to despawn.</param>
    /// <param name="instance">Current projection instance state.</param>
    public static void BeginDespawn(ref EntityCommandBuffer commandBuffer,
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
    public static bool HasMatchingProjection(NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
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
    public static bool ContainsProjection(in PlayerPassiveToolConfig passiveToolConfig, int projectionIndex)
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

    #region Prefabs
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
    #endregion

    #endregion
}
