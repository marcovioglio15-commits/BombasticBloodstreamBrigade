using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Runtime GameObject pool for temporary power-up VFX that cannot safely use DOTS companion cloning in player builds.
/// </summary>
public static class PlayerPowerUpManagedVfxRuntimeUtility
{
    #region Constants
    private const float MinimumLifetimeSeconds = 0.01f;
    private const float MinimumScale = 0.01f;
    private const float MinimumCellSize = 0.1f;
    private const float VelocityEpsilonSquared = 0.000001f;
    #endregion

    #region Fields
    private static readonly List<PlayerPowerUpManagedVfxInstance> activeInstances = new List<PlayerPowerUpManagedVfxInstance>(256);
    private static readonly List<PlayerPowerUpManagedVfxInstance> pooledInstances = new List<PlayerPowerUpManagedVfxInstance>(256);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Advances active managed VFX lifetimes, follow targets and velocity movement.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read target transforms and enemy despawn state.</param>
    /// <param name="deltaTime">Frame delta time used to consume lifetimes.</param>
    public static void UpdateActiveInstances(EntityManager entityManager, float deltaTime)
    {
        for (int instanceIndex = activeInstances.Count - 1; instanceIndex >= 0; instanceIndex--)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
            {
                RemoveActiveInstanceAt(instanceIndex);
                continue;
            }

            if (UpdateInstance(entityManager, instance, deltaTime))
                continue;

            ReleaseActiveInstanceAt(instanceIndex);
        }
    }

    /// <summary>
    /// Spawns or reuses one managed VFX instance for a queued power-up VFX request.
    /// </summary>
    /// <param name="entityManager">Entity manager used for cap validation against live targets.</param>
    /// <param name="prefabBindings">Player-owned prefab entity to GameObject source bindings.</param>
    /// <param name="request">VFX spawn request produced by gameplay systems.</param>
    /// <param name="capConfig">Runtime cap settings for this player.</param>
    /// <returns>True when a managed VFX instance was spawned or an existing capped instance was refreshed.</returns>
    public static bool TrySpawn(EntityManager entityManager,
                                DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                in PlayerPowerUpVfxSpawnRequest request,
                                in PlayerPowerUpVfxCapConfig capConfig)
    {
        GameObject sourcePrefab = ResolveSourcePrefab(prefabBindings, in request);

        if (sourcePrefab == null)
            return false;

        bool refreshedExistingInstance;

        if (!CanSpawnUnderCaps(in request, in capConfig, out refreshedExistingInstance))
            return refreshedExistingInstance;

        PlayerPowerUpManagedVfxInstance instance = AcquireInstance(sourcePrefab);

        if (instance == null)
            return false;

        ConfigureInstance(instance, in request);
        activeInstances.Add(instance);
        return true;
    }

    /// <summary>
    /// Destroys every managed power-up VFX instance and clears the runtime pool.
    /// </summary>
    public static void DestroyAll()
    {
        for (int activeIndex = 0; activeIndex < activeInstances.Count; activeIndex++)
            PlayerPowerUpManagedVfxInstanceUtility.DestroyInstance(activeInstances[activeIndex]);

        for (int pooledIndex = 0; pooledIndex < pooledInstances.Count; pooledIndex++)
            PlayerPowerUpManagedVfxInstanceUtility.DestroyInstance(pooledInstances[pooledIndex]);

        activeInstances.Clear();
        pooledInstances.Clear();
    }
    #endregion

    #region Spawn
    /// <summary>
    /// Resolves the GameObject prefab mapped to one baked VFX prefab entity.
    /// </summary>
    /// <param name="prefabBindings">Player-owned prefab entity to GameObject source bindings.</param>
    /// <param name="request">Baked VFX request that can carry either a player binding entity or a direct source prefab reference.</param>
    /// <returns>Source GameObject prefab, or null when no binding exists.</returns>
    private static GameObject ResolveSourcePrefab(DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                                  in PlayerPowerUpVfxSpawnRequest request)
    {
        if (request.PrefabEntity != Entity.Null)
        {
            for (int bindingIndex = 0; bindingIndex < prefabBindings.Length; bindingIndex++)
            {
                PlayerPowerUpVfxPrefabBindingElement binding = prefabBindings[bindingIndex];

                if (binding.PrefabEntity != request.PrefabEntity)
                    continue;

                return binding.Prefab.Value;
            }
        }

        return request.SourcePrefab.Value;
    }

    /// <summary>
    /// Checks configured VFX caps and refreshes an attached capped instance when requested.
    /// </summary>
    /// <param name="request">VFX request being evaluated.</param>
    /// <param name="capConfig">Runtime cap settings for this player.</param>
    /// <param name="refreshedExistingInstance">True when an existing attached instance lifetime was refreshed.</param>
    /// <returns>True when a new instance can be spawned.</returns>
    private static bool CanSpawnUnderCaps(in PlayerPowerUpVfxSpawnRequest request,
                                          in PlayerPowerUpVfxCapConfig capConfig,
                                          out bool refreshedExistingInstance)
    {
        refreshedExistingInstance = false;

        if (request.RefreshKey != 0 && TryRefreshKeyedInstance(in request))
        {
            refreshedExistingInstance = true;
            return false;
        }

        if (capConfig.MaxActiveOneShotVfx > 0 && activeInstances.Count >= capConfig.MaxActiveOneShotVfx)
            return false;

        if (request.FollowTargetEntity != Entity.Null && capConfig.MaxAttachedSamePrefabPerTarget > 0)
        {
            PlayerPowerUpManagedVfxInstance existingInstance;
            int attachedCount = CountAttachedInstances(in request, out existingInstance);

            if (attachedCount >= capConfig.MaxAttachedSamePrefabPerTarget)
            {
                if (capConfig.RefreshAttachedLifetimeOnCapHit != 0 && existingInstance != null)
                {
                    RefreshAttachedInstance(existingInstance, in request);
                    refreshedExistingInstance = true;
                }

                return false;
            }
        }

        bool shouldCheckAreaCellCap = request.FollowTargetEntity == Entity.Null &&
                                      request.BypassAreaCellCap == 0 &&
                                      capConfig.MaxSamePrefabPerCell > 0;

        if (shouldCheckAreaCellCap)
        {
            int areaCount = CountAreaInstances(request.PrefabEntity,
                                               request.SourcePrefab.Value,
                                               request.Position,
                                               math.max(MinimumCellSize, capConfig.CellSize));

            if (areaCount >= capConfig.MaxSamePrefabPerCell)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Reuses a pooled instance for the same source prefab or creates a new one.
    /// </summary>
    /// <param name="sourcePrefab">Source prefab asset requested by gameplay.</param>
    /// <returns>Ready managed VFX instance, or null when instantiation fails.</returns>
    private static PlayerPowerUpManagedVfxInstance AcquireInstance(GameObject sourcePrefab)
    {
        for (int instanceIndex = pooledInstances.Count - 1; instanceIndex >= 0; instanceIndex--)
        {
            PlayerPowerUpManagedVfxInstance pooledInstance = pooledInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(pooledInstance))
            {
                RemovePooledInstanceAt(instanceIndex);
                continue;
            }

            if (pooledInstance.SourcePrefab != sourcePrefab)
                continue;

            RemovePooledInstanceAt(instanceIndex);
            return pooledInstance;
        }

        return PlayerPowerUpManagedVfxInstanceUtility.CreateInstance(sourcePrefab);
    }

    /// <summary>
    /// Applies request data to one newly active managed VFX instance.
    /// </summary>
    /// <param name="instance">Managed VFX instance being configured.</param>
    /// <param name="request">VFX request produced by gameplay systems.</param>
    private static void ConfigureInstance(PlayerPowerUpManagedVfxInstance instance,
                                          in PlayerPowerUpVfxSpawnRequest request)
    {
        instance.PrefabEntity = request.PrefabEntity;
        instance.RefreshKey = request.RefreshKey;
        instance.RemainingSeconds = math.max(MinimumLifetimeSeconds, request.LifetimeSeconds);
        instance.FollowTargetEntity = request.FollowTargetEntity;
        instance.FollowPositionOffset = request.FollowPositionOffset;
        instance.FollowValidationEntity = request.FollowValidationEntity;
        instance.FollowValidationSpawnVersion = request.FollowValidationSpawnVersion;
        instance.Velocity = request.Velocity;
        instance.HasFollowTarget = request.FollowTargetEntity != Entity.Null;
        instance.HasVelocity = !instance.HasFollowTarget && math.lengthsq(request.Velocity) > VelocityEpsilonSquared;
        instance.Position = request.Position;
        instance.Rotation = request.Rotation;
        instance.FollowMuzzlePose = request.FollowMuzzlePose != 0;
        instance.DetachWhenFollowTargetInvalid = request.DetachWhenFollowTargetInvalid != 0;
        instance.KeepAliveWhileFollowTargetValid = request.KeepAliveWhileFollowTargetValid != 0;

        PlayerPowerUpManagedVfxPresentationUtility.ApplyTransform(instance,
                                                                  request.Position,
                                                                  request.Rotation,
                                                                  math.max(MinimumScale, request.UniformScale),
                                                                  request.ParticleSimulationSpeedMultiplier,
                                                                  request.ForceLooping != 0,
                                                                  request.HasColorOverride != 0,
                                                                  request.ColorOverride,
                                                                  request.SecondaryColorOverride,
                                                                  request.ColorOverrideCount,
                                                                  request.ColorOverrideChildName.ToString(),
                                                                  request.TrailRendererWidthOverride,
                                                                  request.TrailRendererTimeOverrideSeconds);

        if (!instance.InstanceObject.activeSelf)
            instance.InstanceObject.SetActive(true);

        PlayerPowerUpManagedVfxPresentationUtility.RestartVisualPlayback(instance);
    }
    #endregion

    #region Update
    /// <summary>
    /// Updates one active managed VFX instance and reports whether it should remain active.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read target transform and enemy lifecycle components.</param>
    /// <param name="instance">Managed VFX instance being updated.</param>
    /// <param name="deltaTime">Frame delta time used to consume lifetime.</param>
    /// <returns>True while the instance remains valid and alive.</returns>
    private static bool UpdateInstance(EntityManager entityManager,
                                       PlayerPowerUpManagedVfxInstance instance,
                                       float deltaTime)
    {
        bool hasValidKeepAliveTarget = false;

        if (instance.HasFollowTarget)
        {
            float3 targetPosition;
            quaternion targetRotation;

            if (!TryResolveFollowPose(entityManager, instance, out targetPosition, out targetRotation))
                return false;

            if (instance.FollowMuzzlePose)
            {
                instance.Position = targetPosition + math.rotate(targetRotation, instance.FollowPositionOffset);
                instance.Rotation = targetRotation;
                PlayerPowerUpManagedVfxPresentationUtility.ApplyPositionAndRotation(instance,
                                                                                    instance.Position,
                                                                                    instance.Rotation);
            }
            else
            {
                instance.Position = targetPosition + instance.FollowPositionOffset;
                PlayerPowerUpManagedVfxPresentationUtility.ApplyPosition(instance, instance.Position);
            }

            hasValidKeepAliveTarget = instance.KeepAliveWhileFollowTargetValid && instance.HasFollowTarget;
        }
        else if (instance.HasVelocity)
        {
            instance.Position += instance.Velocity * deltaTime;
            PlayerPowerUpManagedVfxPresentationUtility.ApplyPosition(instance, instance.Position);
        }

        if (!hasValidKeepAliveTarget)
            instance.RemainingSeconds -= deltaTime;

        return instance.RemainingSeconds > 0f;
    }

    /// <summary>
    /// Resolves and validates the current world pose for a follow-target VFX instance.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect target entities.</param>
    /// <param name="instance">Managed VFX instance containing follow metadata.</param>
    /// <param name="targetPosition">Current target or muzzle position when the method succeeds.</param>
    /// <param name="targetRotation">Current target or muzzle rotation when the method succeeds.</param>
    /// <returns>True when the target is alive and has a readable pose.</returns>
    private static bool TryResolveFollowPose(EntityManager entityManager,
                                             PlayerPowerUpManagedVfxInstance instance,
                                             out float3 targetPosition,
                                             out quaternion targetRotation)
    {
        targetPosition = float3.zero;
        targetRotation = quaternion.identity;

        if (!IsEntityUsable(entityManager, instance.FollowTargetEntity))
            return TryDetachInvalidFollowTarget(instance, out targetPosition, out targetRotation);

        if (!IsValidationTargetAlive(entityManager, instance))
            return TryDetachInvalidFollowTarget(instance, out targetPosition, out targetRotation);

        if (entityManager.HasComponent<ProjectileActive>(instance.FollowTargetEntity) &&
            !entityManager.IsComponentEnabled<ProjectileActive>(instance.FollowTargetEntity))
        {
            return TryDetachInvalidFollowTarget(instance, out targetPosition, out targetRotation);
        }

        if (instance.FollowMuzzlePose && TryResolveMuzzlePose(entityManager,
                                                              instance.FollowTargetEntity,
                                                              out targetPosition,
                                                              out targetRotation))
            return true;

        if (entityManager.HasComponent<LocalToWorld>(instance.FollowTargetEntity))
        {
            LocalToWorld localToWorld = entityManager.GetComponentData<LocalToWorld>(instance.FollowTargetEntity);
            targetPosition = localToWorld.Value.c3.xyz;
            targetRotation = quaternion.LookRotationSafe(localToWorld.Value.c2.xyz, localToWorld.Value.c1.xyz);
            return true;
        }

        if (entityManager.HasComponent<LocalTransform>(instance.FollowTargetEntity))
        {
            LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(instance.FollowTargetEntity);
            targetPosition = localTransform.Position;
            targetRotation = localTransform.Rotation;
            return true;
        }

        return TryDetachInvalidFollowTarget(instance, out targetPosition, out targetRotation);
    }

    /// <summary>
    /// Detaches a follow VFX from its invalid target so trails and particles can expire naturally at their last pose.
    /// </summary>
    /// <param name="instance">Managed VFX instance with stale follow metadata.</param>
    /// <param name="targetPosition">Last known world position reused for the detached frame.</param>
    /// <param name="targetRotation">Last known world rotation reused for the detached frame.</param>
    /// <returns>True when the instance can continue as a detached lifetime-only VFX.</returns>
    private static bool TryDetachInvalidFollowTarget(PlayerPowerUpManagedVfxInstance instance,
                                                     out float3 targetPosition,
                                                     out quaternion targetRotation)
    {
        targetPosition = instance.Position;
        targetRotation = instance.Rotation;

        if (!instance.DetachWhenFollowTargetInvalid)
            return false;

        instance.HasFollowTarget = false;
        instance.FollowTargetEntity = Entity.Null;
        instance.FollowPositionOffset = float3.zero;
        instance.FollowValidationEntity = Entity.Null;
        instance.FollowValidationSpawnVersion = 0u;
        instance.FollowMuzzlePose = false;
        instance.DetachWhenFollowTargetInvalid = false;
        instance.KeepAliveWhileFollowTargetValid = false;

        // Stop emission so the trail fades from the last pose, then keep the instance alive long enough to finish fading.
        float longestTrailTime = PlayerPowerUpManagedVfxPresentationUtility.StopEmissionForDetach(instance);
        instance.RemainingSeconds = math.max(instance.RemainingSeconds, longestTrailTime);
        return true;
    }

    /// <summary>
    /// Resolves the latest baked weapon muzzle pose for a player-following managed VFX, with animated pose used only as fallback.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect player and muzzle entities.</param>
    /// <param name="playerEntity">Player entity followed by the managed VFX.</param>
    /// <param name="position">Resolved muzzle position.</param>
    /// <param name="rotation">Resolved muzzle rotation.</param>
    /// <returns>True when a readable baked or fallback animated muzzle pose exists.</returns>
    private static bool TryResolveMuzzlePose(EntityManager entityManager,
                                             Entity playerEntity,
                                             out float3 position,
                                             out quaternion rotation)
    {
        position = float3.zero;
        rotation = quaternion.identity;

        if (entityManager.HasComponent<ShooterMuzzleAnchor>(playerEntity))
        {
            Entity muzzleEntity = entityManager.GetComponentData<ShooterMuzzleAnchor>(playerEntity).AnchorEntity;

            if (IsEntityUsable(entityManager, muzzleEntity))
            {
                if (entityManager.HasComponent<LocalToWorld>(muzzleEntity))
                {
                    LocalToWorld localToWorld = entityManager.GetComponentData<LocalToWorld>(muzzleEntity);
                    position = localToWorld.Value.c3.xyz;
                    rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(quaternion.LookRotationSafe(localToWorld.Value.c2.xyz, localToWorld.Value.c1.xyz));
                    return true;
                }

                if (entityManager.HasComponent<LocalTransform>(muzzleEntity))
                {
                    LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(muzzleEntity);
                    position = localTransform.Position;
                    rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(localTransform.Rotation);
                    return true;
                }
            }
        }

        if (entityManager.HasComponent<PlayerAnimatedMuzzleWorldPose>(playerEntity))
        {
            PlayerAnimatedMuzzleWorldPose muzzlePose = entityManager.GetComponentData<PlayerAnimatedMuzzleWorldPose>(playerEntity);

            if (muzzlePose.IsValid != 0)
            {
                position = muzzlePose.Position;
                rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(muzzlePose.Rotation);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates optional enemy spawn-version metadata for attached enemy VFX.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect enemy lifecycle components.</param>
    /// <param name="instance">Managed VFX instance containing validation metadata.</param>
    /// <returns>True when no validation is required or the target enemy is still the same active spawn.</returns>
    private static bool IsValidationTargetAlive(EntityManager entityManager,
                                                PlayerPowerUpManagedVfxInstance instance)
    {
        if (instance.FollowValidationSpawnVersion == 0u)
            return true;

        if (!IsEntityUsable(entityManager, instance.FollowValidationEntity))
            return false;

        if (!entityManager.HasComponent<EnemyRuntimeState>(instance.FollowValidationEntity))
            return false;

        if (!entityManager.HasComponent<EnemyActive>(instance.FollowValidationEntity))
            return false;

        if (!entityManager.IsComponentEnabled<EnemyActive>(instance.FollowValidationEntity))
            return false;

        if (entityManager.HasComponent<EnemyDespawnRequest>(instance.FollowValidationEntity))
            return false;

        EnemyRuntimeState runtimeState = entityManager.GetComponentData<EnemyRuntimeState>(instance.FollowValidationEntity);
        return runtimeState.SpawnVersion == instance.FollowValidationSpawnVersion;
    }
    #endregion

    #region Caps
    /// <summary>
    /// Counts active attached instances matching the same prefab and enemy spawn-version key.
    /// </summary>
    /// <param name="request">VFX request being evaluated.</param>
    /// <param name="existingInstance">First matching active instance found during the scan.</param>
    /// <returns>Number of active matching attached instances.</returns>
    private static int CountAttachedInstances(in PlayerPowerUpVfxSpawnRequest request,
                                              out PlayerPowerUpManagedVfxInstance existingInstance)
    {
        existingInstance = null;

        if (request.FollowValidationEntity == Entity.Null || request.FollowValidationSpawnVersion == 0u)
            return 0;

        int count = 0;

        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
                continue;

            if (!instance.HasFollowTarget)
                continue;

            if (!MatchesRequestedPrefab(instance, in request))
                continue;

            if (instance.FollowValidationEntity != request.FollowValidationEntity)
                continue;

            if (instance.FollowValidationSpawnVersion != request.FollowValidationSpawnVersion)
                continue;

            existingInstance = existingInstance == null ? instance : existingInstance;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Counts active one-shot instances matching one prefab and area-cap cell.
    /// </summary>
    /// <param name="prefabEntity">VFX prefab entity used as the cap key.</param>
    /// <param name="position">Requested spawn position.</param>
    /// <param name="cellSize">Sanitized cap cell size.</param>
    /// <returns>Number of active matching one-shot instances in the same cell.</returns>
    private static int CountAreaInstances(Entity prefabEntity,
                                          GameObject sourcePrefab,
                                          float3 position,
                                          float cellSize)
    {
        int requestedCellX = (int)math.floor(position.x / cellSize);
        int requestedCellY = (int)math.floor(position.z / cellSize);
        int count = 0;

        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
                continue;

            if (instance.HasFollowTarget)
                continue;

            if (!MatchesRequestedPrefab(instance, prefabEntity, sourcePrefab))
                continue;

            int instanceCellX = (int)math.floor(instance.Position.x / cellSize);
            int instanceCellY = (int)math.floor(instance.Position.z / cellSize);

            if (instanceCellX != requestedCellX || instanceCellY != requestedCellY)
                continue;

            count++;
        }

        return count;
    }

    /// <summary>
    /// Refreshes an existing attached VFX when cap refresh is enabled.
    /// </summary>
    /// <param name="instance">Existing attached VFX instance.</param>
    /// <param name="request">Request rejected by the attached VFX cap.</param>
    private static void RefreshAttachedInstance(PlayerPowerUpManagedVfxInstance instance,
                                                in PlayerPowerUpVfxSpawnRequest request)
    {
        RefreshInstance(instance, in request);
    }

    /// <summary>
    /// Refreshes an existing keyed VFX instance when gameplay wants one continuous effect instead of repeated spawns.
    /// </summary>
    /// <param name="request">Request carrying the refresh key and updated presentation settings.</param>
    /// <returns>True when a matching active instance was found and refreshed.</returns>
    private static bool TryRefreshKeyedInstance(in PlayerPowerUpVfxSpawnRequest request)
    {
        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
                continue;

            if (instance.RefreshKey != request.RefreshKey)
                continue;

            if (!MatchesRequestedPrefab(instance, in request))
                continue;

            if (instance.FollowTargetEntity != request.FollowTargetEntity)
                continue;

            RefreshInstance(instance, in request);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies non-restarting presentation updates and lifetime extension to an already active VFX instance.
    /// </summary>
    /// <param name="instance">Existing managed VFX instance.</param>
    /// <param name="request">Request rejected by caps or matched by refresh key.</param>
    private static void RefreshInstance(PlayerPowerUpManagedVfxInstance instance,
                                        in PlayerPowerUpVfxSpawnRequest request)
    {
        float desiredLifetime = math.max(MinimumLifetimeSeconds, request.LifetimeSeconds);

        if (desiredLifetime > instance.RemainingSeconds)
            instance.RemainingSeconds = desiredLifetime;

        instance.FollowTargetEntity = request.FollowTargetEntity;
        instance.FollowPositionOffset = request.FollowPositionOffset;
        instance.FollowValidationEntity = request.FollowValidationEntity;
        instance.FollowValidationSpawnVersion = request.FollowValidationSpawnVersion;
        instance.Velocity = request.Velocity;
        instance.HasFollowTarget = request.FollowTargetEntity != Entity.Null;
        instance.HasVelocity = !instance.HasFollowTarget && math.lengthsq(request.Velocity) > VelocityEpsilonSquared;
        instance.Position = request.Position;
        instance.Rotation = request.Rotation;
        instance.FollowMuzzlePose = request.FollowMuzzlePose != 0;
        instance.DetachWhenFollowTargetInvalid = request.DetachWhenFollowTargetInvalid != 0;
        instance.KeepAliveWhileFollowTargetValid = request.KeepAliveWhileFollowTargetValid != 0;

        PlayerPowerUpManagedVfxPresentationUtility.ApplyTrailRendererSettings(instance,
                                                                               math.max(MinimumScale, request.UniformScale),
                                                                               request.TrailRendererWidthOverride,
                                                                               request.TrailRendererTimeOverrideSeconds);
        PlayerPowerUpManagedVfxPresentationUtility.ApplyParticleSystemRuntimeSettings(instance,
                                                                                      request.ParticleSimulationSpeedMultiplier,
                                                                                      request.ForceLooping != 0,
                                                                                      request.HasColorOverride != 0,
                                                                                      request.ColorOverride,
                                                                                      request.SecondaryColorOverride,
                                                                                      request.ColorOverrideCount,
                                                                                      request.ColorOverrideChildName.ToString());
    }

    /// <summary>
    /// Checks whether a managed instance matches the prefab identity carried by a request.
    /// </summary>
    /// <param name="instance">Active managed VFX instance to inspect.</param>
    /// <param name="request">Spawn request carrying prefab identity.</param>
    /// <returns>True when the instance and request reference the same prefab.</returns>
    private static bool MatchesRequestedPrefab(PlayerPowerUpManagedVfxInstance instance,
                                               in PlayerPowerUpVfxSpawnRequest request)
    {
        return MatchesRequestedPrefab(instance, request.PrefabEntity, request.SourcePrefab.Value);
    }

    /// <summary>
    /// Checks whether a managed instance matches a prefab entity or direct source prefab identity.
    /// </summary>
    /// <param name="instance">Active managed VFX instance to inspect.</param>
    /// <param name="prefabEntity">Optional baked prefab entity.</param>
    /// <param name="sourcePrefab">Optional direct source prefab reference.</param>
    /// <returns>True when the instance and source identify the same prefab.</returns>
    private static bool MatchesRequestedPrefab(PlayerPowerUpManagedVfxInstance instance,
                                               Entity prefabEntity,
                                               GameObject sourcePrefab)
    {
        if (prefabEntity != Entity.Null)
            return instance.PrefabEntity == prefabEntity;

        return sourcePrefab != null && instance.SourcePrefab == sourcePrefab;
    }
    #endregion

    #region Release
    /// <summary>
    /// Releases one active instance and removes it from the active list.
    /// </summary>
    /// <param name="instanceIndex">Active list index to release.</param>
    private static void ReleaseActiveInstanceAt(int instanceIndex)
    {
        PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];
        RemoveActiveInstanceAt(instanceIndex);
        ReleaseInstance(instance);
    }

    /// <summary>
    /// Returns one managed VFX instance to the pool when its GameObject is still alive.
    /// </summary>
    /// <param name="instance">Managed VFX instance being released.</param>
    private static void ReleaseInstance(PlayerPowerUpManagedVfxInstance instance)
    {
        if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
            return;

        PlayerPowerUpManagedVfxPresentationUtility.StopVisualPlayback(instance);

        if (instance.InstanceObject.activeSelf)
            instance.InstanceObject.SetActive(false);

        PlayerPowerUpManagedVfxInstanceUtility.ResetRuntimeState(instance);
        pooledInstances.Add(instance);
    }

    #endregion

    #region Collection Helpers
    /// <summary>
    /// Removes one active-list element without preserving order.
    /// </summary>
    /// <param name="instanceIndex">Active list index to remove.</param>
    private static void RemoveActiveInstanceAt(int instanceIndex)
    {
        int lastIndex = activeInstances.Count - 1;
        activeInstances[instanceIndex] = activeInstances[lastIndex];
        activeInstances.RemoveAt(lastIndex);
    }

    /// <summary>
    /// Removes one pooled-list element without preserving order.
    /// </summary>
    /// <param name="instanceIndex">Pooled list index to remove.</param>
    private static void RemovePooledInstanceAt(int instanceIndex)
    {
        int lastIndex = pooledInstances.Count - 1;
        pooledInstances[instanceIndex] = pooledInstances[lastIndex];
        pooledInstances.RemoveAt(lastIndex);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Checks whether an entity can safely be inspected through EntityManager.
    /// </summary>
    /// <param name="entityManager">Entity manager used to test existence.</param>
    /// <param name="entity">Entity to validate.</param>
    /// <returns>True when the entity is non-null, non-deferred and still exists.</returns>
    private static bool IsEntityUsable(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null)
            return false;

        if (entity.Index < 0)
            return false;

        if (!entityManager.Exists(entity))
            return false;

        return true;
    }
    #endregion

    #endregion
}
