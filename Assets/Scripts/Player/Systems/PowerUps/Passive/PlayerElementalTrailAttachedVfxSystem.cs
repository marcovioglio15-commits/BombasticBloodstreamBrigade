using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Maintains one managed attached mesh ribbon VFX instance per player while Elemental Trail passive is enabled.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerElementalTrailAttachedVfxSystem : ISystem
{
    #region Constants
    private const float MovementEpsilonSquared = 0.0001f;
    private const float MinimumRadius = 0.05f;
    private const float MinimumWidthMultiplier = 0.01f;
    private const float MinimumTrailWidth = 0.02f;
    #endregion

    #region Fields
    private static readonly Dictionary<Entity, PlayerElementalTrailRibbonInstance> managedInstances = new Dictionary<Entity, PlayerElementalTrailRibbonInstance>(4);
    private static readonly List<Entity> invalidOwnerEntities = new List<Entity>(8);
    #if UNITY_EDITOR
    private static readonly HashSet<int> missingTrailRendererLogCache = new HashSet<int>();
    #endif
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures update requirements for player-owned Elemental Trail presentation.
    /// </summary>
    /// <param name="state">DOTS system state used to register required runtime components.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<PlayerElementalTrailAttachedVfxState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<LocalTransform>();
    }

    /// <summary>
    /// Releases managed trail instances when the world owning this system is destroyed.
    /// </summary>
    /// <param name="state">DOTS system state provided by Unity during teardown.</param>
    public void OnDestroy(ref SystemState state)
    {
        if (managedInstances.Count <= 0)
            return;

        Dictionary<Entity, PlayerElementalTrailRibbonInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
            PlayerElementalTrailRibbonMeshUtility.DestroyInstance(enumerator.Current.Value);

        enumerator.Dispose();
        managedInstances.Clear();
        invalidOwnerEntities.Clear();
    }

    /// <summary>
    /// Synchronizes attached Elemental Trail VFX instances with active player entities.
    /// </summary>
    /// <param name="state">DOTS system state used to read ECS gameplay state and EntityManager data.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;
        CleanupInvalidOwnerInstances(entityManager);

        foreach ((DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  RefRO<PlayerMovementState> movementState,
                  RefRO<LocalTransform> playerTransform,
                  RefRW<PlayerElementalTrailAttachedVfxState> trailAttachedVfxState,
                  Entity playerEntity)
                 in SystemAPI.Query<DynamicBuffer<PlayerPassiveToolsStateElement>,
                                    RefRO<PlayerMovementState>,
                                    RefRO<LocalTransform>,
                                    RefRW<PlayerElementalTrailAttachedVfxState>>()
                             .WithEntityAccess())
        {
            PlayerElementalTrailAttachedVfxState previousTrailState = trailAttachedVfxState.ValueRO;
            PlayerPassiveToolsState passiveToolsState;
            PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer,
                                                      out passiveToolsState);
            ReleasePooledTrailEntityIfAny(entityManager, previousTrailState.VfxEntity);

            GameObject trailPrefab = ResolveTrailPrefab(entityManager, playerEntity);
            bool shouldBeActive = passiveToolsState.HasElementalTrail != 0 && trailPrefab != null;

            if (!shouldBeActive)
            {
                SetManagedInstanceInactive(playerEntity);
                trailAttachedVfxState.ValueRW = default;
                continue;
            }

            PlayerElementalTrailRibbonInstance managedInstance = GetOrCreateManagedInstance(playerEntity, trailPrefab);

            if (managedInstance == null || managedInstance.InstanceObject == null)
            {
                trailAttachedVfxState.ValueRW = default;
                continue;
            }

            ElementalTrailPassiveConfig trailConfig = passiveToolsState.ElementalTrail;
            float radius = math.max(MinimumRadius, trailConfig.TrailRadius);
            float widthMultiplier = math.max(MinimumWidthMultiplier, trailConfig.TrailAttachedVfxScaleMultiplier);
            float desiredTrailWidth = math.max(MinimumTrailWidth, radius * 2f * widthMultiplier);
            float3 desiredPosition = playerTransform.ValueRO.Position + trailConfig.TrailAttachedVfxOffset;
            float3 planarVelocity = movementState.ValueRO.Velocity;
            planarVelocity.y = 0f;
            bool isMoving = math.lengthsq(planarVelocity) > MovementEpsilonSquared;

            PlayerElementalTrailRibbonMeshUtility.UpdateInstance(managedInstance,
                                                                 desiredPosition,
                                                                 desiredTrailWidth,
                                                                 isMoving,
                                                                 deltaTime);
            trailAttachedVfxState.ValueRW = default;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the authored managed prefab reference baked on a player entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read the Unity object reference component.</param>
    /// <param name="playerEntity">Player entity owning the attached VFX.</param>
    /// <returns>Authored prefab GameObject, or null when no valid reference is available.</returns>
    private static GameObject ResolveTrailPrefab(EntityManager entityManager, Entity playerEntity)
    {
        if (!entityManager.HasComponent<PlayerElementalTrailAttachedVfxPrefabReference>(playerEntity))
            return null;

        PlayerElementalTrailAttachedVfxPrefabReference prefabReference = entityManager.GetComponentData<PlayerElementalTrailAttachedVfxPrefabReference>(playerEntity);
        return prefabReference.Prefab.Value;
    }

    /// <summary>
    /// Returns a reusable managed VFX instance for the requested player and prefab.
    /// </summary>
    /// <param name="playerEntity">Player entity used as owner key for the managed instance cache.</param>
    /// <param name="trailPrefab">Prefab that should back the attached trail presentation.</param>
    /// <returns>Existing or newly instantiated managed trail instance, or null when creation fails.</returns>
    private static PlayerElementalTrailRibbonInstance GetOrCreateManagedInstance(Entity playerEntity, GameObject trailPrefab)
    {
        PlayerElementalTrailRibbonInstance managedInstance;

        if (managedInstances.TryGetValue(playerEntity, out managedInstance))
        {
            bool requiresRebuild = managedInstance == null ||
                                   managedInstance.InstanceObject == null ||
                                   managedInstance.SourcePrefab != trailPrefab;

            if (!requiresRebuild)
                return managedInstance;

            PlayerElementalTrailRibbonMeshUtility.DestroyInstance(managedInstance);
            managedInstances.Remove(playerEntity);
        }

        if (trailPrefab == null)
            return null;

        managedInstance = PlayerElementalTrailRibbonMeshUtility.CreateInstance(trailPrefab);

        if (managedInstance == null)
        {
        #if UNITY_EDITOR
            if (missingTrailRendererLogCache.Add(playerEntity.Index))
            {
                Debug.LogWarning(string.Format("[ElementalTrailVfx] Prefab '{0}' could not provide a usable TrailRenderer template. Attached trail will be invisible.", trailPrefab.name));
            }
        #endif
            return null;
        }

        managedInstances[playerEntity] = managedInstance;

        return managedInstance;
    }

    /// <summary>
    /// Disables one cached managed ribbon instance and clears its currently visible samples.
    /// </summary>
    /// <param name="playerEntity">Player entity used to resolve the cached managed instance.</param>
    private static void SetManagedInstanceInactive(Entity playerEntity)
    {
        PlayerElementalTrailRibbonInstance managedInstance;

        if (!managedInstances.TryGetValue(playerEntity, out managedInstance))
            return;

        PlayerElementalTrailRibbonMeshUtility.SetInactive(managedInstance);
    }

    /// <summary>
    /// Destroys managed instances whose owner entity no longer exists in the current world.
    /// </summary>
    /// <param name="entityManager">Entity manager used to validate cached owner entities.</param>
    private static void CleanupInvalidOwnerInstances(EntityManager entityManager)
    {
        if (managedInstances.Count <= 0)
            return;

        invalidOwnerEntities.Clear();
        Dictionary<Entity, PlayerElementalTrailRibbonInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Entity ownerEntity = enumerator.Current.Key;

            if (IsValidEntity(entityManager, ownerEntity))
                continue;

            PlayerElementalTrailRibbonMeshUtility.DestroyInstance(enumerator.Current.Value);
            invalidOwnerEntities.Add(ownerEntity);
        }

        enumerator.Dispose();

        for (int index = 0; index < invalidOwnerEntities.Count; index++)
            managedInstances.Remove(invalidOwnerEntities[index]);

        invalidOwnerEntities.Clear();
    }

    /// <summary>
    /// Releases a legacy pooled VFX entity reference if one was left by older runtime paths.
    /// </summary>
    /// <param name="entityManager">Entity manager used to disable and strip transient VFX components.</param>
    /// <param name="vfxEntity">Entity previously referenced by PlayerElementalTrailAttachedVfxState.</param>
    private static void ReleasePooledTrailEntityIfAny(EntityManager entityManager, Entity vfxEntity)
    {
        if (!IsValidEntity(entityManager, vfxEntity))
            return;

        if (entityManager.HasComponent<PlayerPowerUpVfxLifetime>(vfxEntity))
            entityManager.RemoveComponent<PlayerPowerUpVfxLifetime>(vfxEntity);

        if (entityManager.HasComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity))
            entityManager.RemoveComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity);

        if (entityManager.HasComponent<PlayerPowerUpVfxVelocity>(vfxEntity))
            entityManager.RemoveComponent<PlayerPowerUpVfxVelocity>(vfxEntity);

        if (entityManager.IsEnabled(vfxEntity))
            entityManager.SetEnabled(vfxEntity, false);
    }

    /// <summary>
    /// Checks whether an entity handle can be safely accessed through the current EntityManager.
    /// </summary>
    /// <param name="entityManager">Entity manager that owns the runtime world.</param>
    /// <param name="entity">Entity handle to validate.</param>
    /// <returns>True when the entity is non-null, has a valid index and still exists.</returns>
    private static bool IsValidEntity(EntityManager entityManager, Entity entity)
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
