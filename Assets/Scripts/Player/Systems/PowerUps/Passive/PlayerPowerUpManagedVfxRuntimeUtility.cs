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

            if (!IsInstanceUsable(instance))
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
            DestroyInstance(activeInstances[activeIndex]);

        for (int pooledIndex = 0; pooledIndex < pooledInstances.Count; pooledIndex++)
            DestroyInstance(pooledInstances[pooledIndex]);

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
        if (request.PrefabEntity == Entity.Null)
            return null;

        for (int bindingIndex = 0; bindingIndex < prefabBindings.Length; bindingIndex++)
        {
            PlayerPowerUpVfxPrefabBindingElement binding = prefabBindings[bindingIndex];

            if (binding.PrefabEntity != request.PrefabEntity)
                continue;

            return binding.Prefab.Value;
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

        if (request.FollowTargetEntity == Entity.Null && capConfig.MaxSamePrefabPerCell > 0)
        {
            int areaCount = CountAreaInstances(request.PrefabEntity,
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

            if (!IsInstanceUsable(pooledInstance))
            {
                RemovePooledInstanceAt(instanceIndex);
                continue;
            }

            if (pooledInstance.SourcePrefab != sourcePrefab)
                continue;

            RemovePooledInstanceAt(instanceIndex);
            return pooledInstance;
        }

        return CreateInstance(sourcePrefab);
    }

    /// <summary>
    /// Instantiates one managed VFX object and caches presentation components used during reuse.
    /// </summary>
    /// <param name="sourcePrefab">Source prefab asset requested by gameplay.</param>
    /// <returns>Created managed VFX instance, or null when the prefab cannot be instantiated.</returns>
    private static PlayerPowerUpManagedVfxInstance CreateInstance(GameObject sourcePrefab)
    {
        if (sourcePrefab == null)
            return null;

        GameObject instanceObject = Object.Instantiate(sourcePrefab);

        if (instanceObject == null)
            return null;

        ParticleSystem[] particleSystems = instanceObject.GetComponentsInChildren<ParticleSystem>(true);
        TrailRenderer[] trailRenderers = instanceObject.GetComponentsInChildren<TrailRenderer>(true);

        instanceObject.name = string.Format("{0}_PowerUpVfx", sourcePrefab.name);
        return new PlayerPowerUpManagedVfxInstance
        {
            SourcePrefab = sourcePrefab,
            InstanceObject = instanceObject,
            InstanceTransform = instanceObject.transform,
            RootBaseLocalScale = instanceObject.transform.localScale,
            ParticleSystems = particleSystems,
            TrailRenderers = trailRenderers,
            TrailRendererBaseWidths = BuildTrailRendererBaseWidths(trailRenderers),
            TrailRendererBaseTimes = BuildTrailRendererBaseTimes(trailRenderers)
        };
    }

    /// <summary>
    /// Caches authored trail widths so pooled VFX can be rescaled from stable prefab values.
    /// </summary>
    /// <param name="trailRenderers">Trail renderers collected from the spawned VFX instance.</param>
    /// <returns>Width multipliers matching the renderer array order.</returns>
    private static float[] BuildTrailRendererBaseWidths(TrailRenderer[] trailRenderers)
    {
        if (trailRenderers == null || trailRenderers.Length <= 0)
            return null;

        float[] baseWidths = new float[trailRenderers.Length];

        for (int trailIndex = 0; trailIndex < trailRenderers.Length; trailIndex++)
        {
            TrailRenderer trailRenderer = trailRenderers[trailIndex];
            baseWidths[trailIndex] = trailRenderer != null ? trailRenderer.widthMultiplier : 1f;
        }

        return baseWidths;
    }

    /// <summary>
    /// Caches authored trail retention times so pooled VFX can restore source prefab history settings.
    /// </summary>
    /// <param name="trailRenderers">Trail renderers collected from the spawned VFX instance.</param>
    /// <returns>Retention times matching the renderer array order.</returns>
    private static float[] BuildTrailRendererBaseTimes(TrailRenderer[] trailRenderers)
    {
        if (trailRenderers == null || trailRenderers.Length <= 0)
            return null;

        float[] baseTimes = new float[trailRenderers.Length];

        for (int trailIndex = 0; trailIndex < trailRenderers.Length; trailIndex++)
        {
            TrailRenderer trailRenderer = trailRenderers[trailIndex];
            baseTimes[trailIndex] = trailRenderer != null ? trailRenderer.time : 0f;
        }

        return baseTimes;
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
        instance.RemainingSeconds = math.max(MinimumLifetimeSeconds, request.LifetimeSeconds);
        instance.FollowTargetEntity = request.FollowTargetEntity;
        instance.FollowPositionOffset = request.FollowPositionOffset;
        instance.FollowValidationEntity = request.FollowValidationEntity;
        instance.FollowValidationSpawnVersion = request.FollowValidationSpawnVersion;
        instance.Velocity = request.Velocity;
        instance.HasFollowTarget = request.FollowTargetEntity != Entity.Null;
        instance.HasVelocity = !instance.HasFollowTarget && math.lengthsq(request.Velocity) > VelocityEpsilonSquared;
        instance.Position = request.Position;

        PlayerPowerUpManagedVfxPresentationUtility.ApplyTransform(instance,
                                                                  request.Position,
                                                                  request.Rotation,
                                                                  math.max(MinimumScale, request.UniformScale),
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
        if (instance.HasFollowTarget)
        {
            float3 targetPosition;

            if (!TryResolveFollowPosition(entityManager, instance, out targetPosition))
                return false;

            instance.Position = targetPosition + instance.FollowPositionOffset;
            PlayerPowerUpManagedVfxPresentationUtility.ApplyPosition(instance, instance.Position);
        }
        else if (instance.HasVelocity)
        {
            instance.Position += instance.Velocity * deltaTime;
            PlayerPowerUpManagedVfxPresentationUtility.ApplyPosition(instance, instance.Position);
        }

        instance.RemainingSeconds -= deltaTime;
        return instance.RemainingSeconds > 0f;
    }

    /// <summary>
    /// Resolves and validates the current world position for a follow-target VFX instance.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect target entities.</param>
    /// <param name="instance">Managed VFX instance containing follow metadata.</param>
    /// <param name="targetPosition">Current target position when the method succeeds.</param>
    /// <returns>True when the target is alive and has a readable transform.</returns>
    private static bool TryResolveFollowPosition(EntityManager entityManager,
                                                 PlayerPowerUpManagedVfxInstance instance,
                                                 out float3 targetPosition)
    {
        targetPosition = float3.zero;

        if (!IsEntityUsable(entityManager, instance.FollowTargetEntity))
            return false;

        if (!IsValidationTargetAlive(entityManager, instance))
            return false;

        if (entityManager.HasComponent<LocalToWorld>(instance.FollowTargetEntity))
        {
            LocalToWorld localToWorld = entityManager.GetComponentData<LocalToWorld>(instance.FollowTargetEntity);
            targetPosition = localToWorld.Position;
            return true;
        }

        if (entityManager.HasComponent<LocalTransform>(instance.FollowTargetEntity))
        {
            LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(instance.FollowTargetEntity);
            targetPosition = localTransform.Position;
            return true;
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

            if (!IsInstanceUsable(instance))
                continue;

            if (!instance.HasFollowTarget)
                continue;

            if (instance.PrefabEntity != request.PrefabEntity)
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
                                          float3 position,
                                          float cellSize)
    {
        int requestedCellX = (int)math.floor(position.x / cellSize);
        int requestedCellY = (int)math.floor(position.z / cellSize);
        int count = 0;

        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!IsInstanceUsable(instance))
                continue;

            if (instance.HasFollowTarget)
                continue;

            if (instance.PrefabEntity != prefabEntity)
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
        float desiredLifetime = math.max(MinimumLifetimeSeconds, request.LifetimeSeconds);

        if (desiredLifetime > instance.RemainingSeconds)
            instance.RemainingSeconds = desiredLifetime;

        PlayerPowerUpManagedVfxPresentationUtility.ApplyTrailRendererSettings(instance,
                                                                               math.max(MinimumScale, request.UniformScale),
                                                                               request.TrailRendererWidthOverride,
                                                                               request.TrailRendererTimeOverrideSeconds);
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
        if (!IsInstanceUsable(instance))
            return;

        PlayerPowerUpManagedVfxPresentationUtility.StopVisualPlayback(instance);

        if (instance.InstanceObject.activeSelf)
            instance.InstanceObject.SetActive(false);

        ResetRuntimeState(instance);
        pooledInstances.Add(instance);
    }

    /// <summary>
    /// Clears runtime-only metadata before a managed VFX instance is pooled.
    /// </summary>
    /// <param name="instance">Managed VFX instance being reset.</param>
    private static void ResetRuntimeState(PlayerPowerUpManagedVfxInstance instance)
    {
        instance.PrefabEntity = Entity.Null;
        instance.RemainingSeconds = 0f;
        instance.FollowTargetEntity = Entity.Null;
        instance.FollowPositionOffset = float3.zero;
        instance.FollowValidationEntity = Entity.Null;
        instance.FollowValidationSpawnVersion = 0u;
        instance.Velocity = float3.zero;
        instance.Position = float3.zero;
        instance.HasFollowTarget = false;
        instance.HasVelocity = false;
    }

    /// <summary>
    /// Destroys one managed VFX GameObject and clears cached component references.
    /// </summary>
    /// <param name="instance">Managed VFX instance being destroyed.</param>
    private static void DestroyInstance(PlayerPowerUpManagedVfxInstance instance)
    {
        if (instance == null)
            return;

        if (instance.InstanceObject != null)
            Object.Destroy(instance.InstanceObject);

        instance.SourcePrefab = null;
        instance.InstanceObject = null;
        instance.InstanceTransform = null;
        instance.ParticleSystems = null;
        instance.TrailRenderers = null;
        ResetRuntimeState(instance);
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
    /// Checks whether a managed VFX instance still has a live GameObject and transform.
    /// </summary>
    /// <param name="instance">Managed VFX instance to validate.</param>
    /// <returns>True when the instance can be updated or pooled.</returns>
    private static bool IsInstanceUsable(PlayerPowerUpManagedVfxInstance instance)
    {
        if (instance == null)
            return false;

        if (instance.InstanceObject == null)
            return false;

        if (instance.InstanceTransform == null)
            return false;

        return true;
    }

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
