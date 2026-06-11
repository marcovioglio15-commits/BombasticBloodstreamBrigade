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
    /// Number of currently active managed VFX instances.
    /// </summary>
    public static int ActiveInstanceCount
    {
        get
        {
            return activeInstances.Count;
        }
    }

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
        GameObject sourcePrefab = PlayerPowerUpManagedVfxInstanceUtility.ResolveSourcePrefab(prefabBindings, in request);

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
        PlayerPowerUpManagedVfxCapSelectionUtility.ResetActivationSequence();
    }

    /// <summary>
    /// Disables the GameObject of every active managed VFX instance whose follow target matches the requested player
    /// entity. Used by the death animation system the frame the player visual bridge hides so player-attached VFX
    /// (Charge Shot, Level-Up, Health/Shield Increase, Muzzle Flash follow-pose VFX, Elemental Trail attached, etc.)
    /// disappear together with the rig instead of floating around the now-invisible player. The instances are kept on
    /// the active list so the same call can re-show them via <see cref="ShowPlayerAttachedInstances"/>; they will be
    /// pooled normally when their lifetime expires.
    /// </summary>
    /// <param name="playerEntity">Player entity whose attached VFX should be hidden.</param>
    public static void HidePlayerAttachedInstances(Entity playerEntity)
    {
        if (playerEntity == Entity.Null)
            return;

        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
                continue;

            if (!instance.HasFollowTarget || instance.FollowTargetEntity != playerEntity)
                continue;

            if (instance.InstanceObject.activeSelf)
                instance.InstanceObject.SetActive(false);
        }
    }

    /// <summary>
    /// Re-enables the GameObject of every active managed VFX instance whose follow target matches the requested player
    /// entity. Mirror of <see cref="HidePlayerAttachedInstances"/> used when the run-outcome transitions back to idle
    /// without finalizing (rare; mostly a defensive escape hatch). Does not extend lifetimes: the instance keeps
    /// counting down the remaining seconds it had when it was hidden.
    /// </summary>
    /// <param name="playerEntity">Player entity whose attached VFX should be re-shown.</param>
    public static void ShowPlayerAttachedInstances(Entity playerEntity)
    {
        if (playerEntity == Entity.Null)
            return;

        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
                continue;

            if (!instance.HasFollowTarget || instance.FollowTargetEntity != playerEntity)
                continue;

            if (!instance.InstanceObject.activeSelf)
                instance.InstanceObject.SetActive(true);
        }
    }
    #endregion

    #region Spawn
    /// <summary>
    /// Checks configured VFX caps and refreshes or restarts an eligible capped instance when requested.
    /// </summary>
    /// <param name="request">VFX request being evaluated.</param>
    /// <param name="capConfig">Runtime cap settings for this player.</param>
    /// <param name="refreshedExistingInstance">True when an existing instance was refreshed or restarted.</param>
    /// <returns>True when a new instance can be spawned.</returns>
    private static bool CanSpawnUnderCaps(in PlayerPowerUpVfxSpawnRequest request,
                                          in PlayerPowerUpVfxCapConfig capConfig,
                                          out bool refreshedExistingInstance)
    {
        refreshedExistingInstance = false;

        PlayerPowerUpManagedVfxInstance keyedInstance = request.RefreshKey != 0
            ? PlayerPowerUpManagedVfxCapSelectionUtility.FindKeyedInstance(activeInstances, in request)
            : null;

        if (keyedInstance != null)
        {
            RefreshInstance(keyedInstance, in request);
            refreshedExistingInstance = true;
            return false;
        }

        if (capConfig.MaxActiveOneShotVfx > 0 &&
            activeInstances.Count >= capConfig.MaxActiveOneShotVfx)
        {
            if (request.RestartOldestOnCap != 0)
            {
                PlayerPowerUpManagedVfxInstance oldestRestartableInstance =
                    PlayerPowerUpManagedVfxCapSelectionUtility.FindOldestMatchingRestartableInstance(activeInstances, in request);

                if (oldestRestartableInstance != null)
                {
                    ConfigureInstance(oldestRestartableInstance, in request);
                    refreshedExistingInstance = true;
                    return false;
                }

                oldestRestartableInstance =
                    PlayerPowerUpManagedVfxCapSelectionUtility.FindOldestRestartableInstance(activeInstances);

                if (oldestRestartableInstance == null)
                    return false;

                ReleaseActiveInstanceAt(activeInstances.IndexOf(oldestRestartableInstance));
            }
            else
            {
                return false;
            }
        }

        if (request.FollowTargetEntity != Entity.Null && capConfig.MaxAttachedSamePrefabPerTarget > 0)
        {
            PlayerPowerUpManagedVfxInstance existingInstance;
            int attachedCount = PlayerPowerUpManagedVfxCapSelectionUtility.CountAttachedInstances(activeInstances,
                                                                                                   in request,
                                                                                                   out existingInstance);

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
            int areaCount = PlayerPowerUpManagedVfxCapSelectionUtility.CountAreaInstances(activeInstances,
                                                                                           request.PrefabEntity,
                                                                                           request.SourcePrefab.Value,
                                                                                           request.Position,
                                                                                           math.max(MinimumCellSize, capConfig.CellSize),
                                                                                           out PlayerPowerUpManagedVfxInstance oldestRestartableInstance);

            if (areaCount >= capConfig.MaxSamePrefabPerCell)
            {
                if (request.RestartOldestOnCap != 0 && oldestRestartableInstance != null)
                {
                    ConfigureInstance(oldestRestartableInstance, in request);
                    refreshedExistingInstance = true;
                }

                return false;
            }
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
        instance.RestartOldestOnCap = request.RestartOldestOnCap != 0;
        instance.ActivationSequence = PlayerPowerUpManagedVfxCapSelectionUtility.ResolveNextActivationSequence();

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
