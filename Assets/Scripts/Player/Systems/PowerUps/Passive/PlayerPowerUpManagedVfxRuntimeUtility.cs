using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Runtime GameObject pool for temporary power-up VFX that cannot safely use DOTS companion cloning in player builds.
/// /params None.
/// /returns None.
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
    /// /params entityManager Entity manager used to read target transforms and enemy despawn state.
    /// /params deltaTime Frame delta time used to consume lifetimes.
    /// /returns None.
    /// </summary>
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
    /// /params entityManager Entity manager used for cap validation against live targets.
    /// /params prefabBindings Player-owned prefab entity to GameObject source bindings.
    /// /params request VFX spawn request produced by gameplay systems.
    /// /params capConfig Runtime cap settings for this player.
    /// /returns True when a managed VFX instance was spawned or an existing capped instance was refreshed.
    /// </summary>
    public static bool TrySpawn(EntityManager entityManager,
                                DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                in PlayerPowerUpVfxSpawnRequest request,
                                in PlayerPowerUpVfxCapConfig capConfig)
    {
        GameObject sourcePrefab = ResolveSourcePrefab(prefabBindings, request.PrefabEntity);

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
    /// /params None.
    /// /returns None.
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
    /// /params prefabBindings Player-owned prefab entity to GameObject source bindings.
    /// /params prefabEntity Baked prefab entity referenced by the VFX request.
    /// /returns Source GameObject prefab, or null when no binding exists.
    /// </summary>
    private static GameObject ResolveSourcePrefab(DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                                  Entity prefabEntity)
    {
        if (prefabEntity == Entity.Null)
            return null;

        for (int bindingIndex = 0; bindingIndex < prefabBindings.Length; bindingIndex++)
        {
            PlayerPowerUpVfxPrefabBindingElement binding = prefabBindings[bindingIndex];

            if (binding.PrefabEntity != prefabEntity)
                continue;

            return binding.Prefab.Value;
        }

        return null;
    }

    /// <summary>
    /// Checks configured VFX caps and refreshes an attached capped instance when requested.
    /// /params request VFX request being evaluated.
    /// /params capConfig Runtime cap settings for this player.
    /// /params refreshedExistingInstance True when an existing attached instance lifetime was refreshed.
    /// /returns True when a new instance can be spawned.
    /// </summary>
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
                    RefreshLifetime(existingInstance, request.LifetimeSeconds);
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
    /// /params sourcePrefab Source prefab asset requested by gameplay.
    /// /returns Ready managed VFX instance, or null when instantiation fails.
    /// </summary>
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
    /// /params sourcePrefab Source prefab asset requested by gameplay.
    /// /returns Created managed VFX instance, or null when the prefab cannot be instantiated.
    /// </summary>
    private static PlayerPowerUpManagedVfxInstance CreateInstance(GameObject sourcePrefab)
    {
        if (sourcePrefab == null)
            return null;

        GameObject instanceObject = Object.Instantiate(sourcePrefab);

        if (instanceObject == null)
            return null;

        instanceObject.name = string.Format("{0}_PowerUpVfx", sourcePrefab.name);
        return new PlayerPowerUpManagedVfxInstance
        {
            SourcePrefab = sourcePrefab,
            InstanceObject = instanceObject,
            InstanceTransform = instanceObject.transform,
            ParticleSystems = instanceObject.GetComponentsInChildren<ParticleSystem>(true),
            TrailRenderers = instanceObject.GetComponentsInChildren<TrailRenderer>(true)
        };
    }

    /// <summary>
    /// Applies request data to one newly active managed VFX instance.
    /// /params instance Managed VFX instance being configured.
    /// /params request VFX request produced by gameplay systems.
    /// /returns None.
    /// </summary>
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
                                                                  math.max(MinimumScale, request.UniformScale));

        if (!instance.InstanceObject.activeSelf)
            instance.InstanceObject.SetActive(true);

        PlayerPowerUpManagedVfxPresentationUtility.RestartVisualPlayback(instance);
    }
    #endregion

    #region Update
    /// <summary>
    /// Updates one active managed VFX instance and reports whether it should remain active.
    /// /params entityManager Entity manager used to read target transform and enemy lifecycle components.
    /// /params instance Managed VFX instance being updated.
    /// /params deltaTime Frame delta time used to consume lifetime.
    /// /returns True while the instance remains valid and alive.
    /// </summary>
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
    /// /params entityManager Entity manager used to inspect target entities.
    /// /params instance Managed VFX instance containing follow metadata.
    /// /params targetPosition Current target position when the method succeeds.
    /// /returns True when the target is alive and has a readable transform.
    /// </summary>
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
    /// /params entityManager Entity manager used to inspect enemy lifecycle components.
    /// /params instance Managed VFX instance containing validation metadata.
    /// /returns True when no validation is required or the target enemy is still the same active spawn.
    /// </summary>
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
    /// /params request VFX request being evaluated.
    /// /params existingInstance First matching active instance found during the scan.
    /// /returns Number of active matching attached instances.
    /// </summary>
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
    /// /params prefabEntity VFX prefab entity used as the cap key.
    /// /params position Requested spawn position.
    /// /params cellSize Sanitized cap cell size.
    /// /returns Number of active matching one-shot instances in the same cell.
    /// </summary>
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
    /// Extends an existing attached VFX lifetime when cap refresh is enabled.
    /// /params instance Existing attached VFX instance.
    /// /params requestedLifetimeSeconds Lifetime requested by the rejected spawn request.
    /// /returns None.
    /// </summary>
    private static void RefreshLifetime(PlayerPowerUpManagedVfxInstance instance,
                                        float requestedLifetimeSeconds)
    {
        float desiredLifetime = math.max(MinimumLifetimeSeconds, requestedLifetimeSeconds);

        if (desiredLifetime <= instance.RemainingSeconds)
            return;

        instance.RemainingSeconds = desiredLifetime;
    }
    #endregion

    #region Release
    /// <summary>
    /// Releases one active instance and removes it from the active list.
    /// /params instanceIndex Active list index to release.
    /// /returns None.
    /// </summary>
    private static void ReleaseActiveInstanceAt(int instanceIndex)
    {
        PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];
        RemoveActiveInstanceAt(instanceIndex);
        ReleaseInstance(instance);
    }

    /// <summary>
    /// Returns one managed VFX instance to the pool when its GameObject is still alive.
    /// /params instance Managed VFX instance being released.
    /// /returns None.
    /// </summary>
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
    /// /params instance Managed VFX instance being reset.
    /// /returns None.
    /// </summary>
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
    /// /params instance Managed VFX instance being destroyed.
    /// /returns None.
    /// </summary>
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
    /// /params instanceIndex Active list index to remove.
    /// /returns None.
    /// </summary>
    private static void RemoveActiveInstanceAt(int instanceIndex)
    {
        int lastIndex = activeInstances.Count - 1;
        activeInstances[instanceIndex] = activeInstances[lastIndex];
        activeInstances.RemoveAt(lastIndex);
    }

    /// <summary>
    /// Removes one pooled-list element without preserving order.
    /// /params instanceIndex Pooled list index to remove.
    /// /returns None.
    /// </summary>
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
    /// /params instance Managed VFX instance to validate.
    /// /returns True when the instance can be updated or pooled.
    /// </summary>
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
    /// /params entityManager Entity manager used to test existence.
    /// /params entity Entity to validate.
    /// /returns True when the entity is non-null, non-deferred and still exists.
    /// </summary>
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
