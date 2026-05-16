using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Maintains one managed attached TrailRenderer VFX instance per player while Elemental Trail passive is enabled.
/// /params None.
/// /returns None.
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
    private const float WidthChangeEpsilon = 0.0001f;
    #endregion

    #region Fields
    private static readonly Dictionary<Entity, ManagedTrailVfxInstance> managedInstances = new Dictionary<Entity, ManagedTrailVfxInstance>(4);
    private static readonly List<Entity> invalidOwnerEntities = new List<Entity>(8);
    private static readonly Quaternion GroundAlignedTrailRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
    #if UNITY_EDITOR
    private static readonly HashSet<int> missingTrailRendererLogCache = new HashSet<int>();
    #endif
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures update requirements for player-owned Elemental Trail presentation.
    /// /params state DOTS system state used to register required runtime components.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<PlayerElementalTrailAttachedVfxState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<LocalTransform>();
    }

    /// <summary>
    /// Releases managed trail instances when the world owning this system is destroyed.
    /// /params state DOTS system state provided by Unity during teardown.
    /// /returns None.
    /// </summary>
    public void OnDestroy(ref SystemState state)
    {
        if (managedInstances.Count <= 0)
            return;

        Dictionary<Entity, ManagedTrailVfxInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
            DestroyManagedInstance(enumerator.Current.Value);

        enumerator.Dispose();
        managedInstances.Clear();
        invalidOwnerEntities.Clear();
    }

    /// <summary>
    /// Synchronizes attached Elemental Trail VFX instances with active player entities.
    /// /params state DOTS system state used to read ECS gameplay state and EntityManager data.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();
        EntityManager entityManager = state.EntityManager;
        CleanupInvalidOwnerInstances(entityManager);

        foreach ((RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRO<PlayerMovementState> movementState,
                  RefRO<LocalTransform> playerTransform,
                  RefRW<PlayerElementalTrailAttachedVfxState> trailAttachedVfxState,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerPassiveToolsState>,
                                    RefRO<PlayerMovementState>,
                                    RefRO<LocalTransform>,
                                    RefRW<PlayerElementalTrailAttachedVfxState>>()
                             .WithEntityAccess())
        {
            PlayerElementalTrailAttachedVfxState previousTrailState = trailAttachedVfxState.ValueRO;
            ReleasePooledTrailEntityIfAny(entityManager, previousTrailState.VfxEntity);

            GameObject trailPrefab = ResolveTrailPrefab(entityManager, playerEntity);
            bool shouldBeActive = passiveToolsState.ValueRO.HasElementalTrail != 0 && trailPrefab != null;

            if (!shouldBeActive)
            {
                SetManagedInstanceActive(playerEntity, false, float3.zero, 1f, MinimumTrailWidth);
                trailAttachedVfxState.ValueRW = default;
                continue;
            }

            ManagedTrailVfxInstance managedInstance = GetOrCreateManagedInstance(playerEntity, trailPrefab);

            if (managedInstance == null || managedInstance.InstanceObject == null)
            {
                trailAttachedVfxState.ValueRW = default;
                continue;
            }

            ElementalTrailPassiveConfig trailConfig = passiveToolsState.ValueRO.ElementalTrail;
            float radius = math.max(MinimumRadius, trailConfig.TrailRadius);
            float widthMultiplier = math.max(MinimumWidthMultiplier, trailConfig.TrailAttachedVfxScaleMultiplier);
            float desiredTrailWidth = math.max(MinimumTrailWidth, radius * 2f * widthMultiplier);
            float3 desiredPosition = playerTransform.ValueRO.Position + trailConfig.TrailAttachedVfxOffset;
            float3 planarVelocity = movementState.ValueRO.Velocity;
            planarVelocity.y = 0f;
            bool isMoving = math.lengthsq(planarVelocity) > MovementEpsilonSquared;

            SetManagedInstanceActive(playerEntity, true, desiredPosition, 1f, isMoving ? desiredTrailWidth : 0f);
            trailAttachedVfxState.ValueRW = default;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the authored managed prefab reference baked on a player entity.
    /// /params entityManager Entity manager used to read the Unity object reference component.
    /// /params playerEntity Player entity owning the attached VFX.
    /// /returns Authored prefab GameObject, or null when no valid reference is available.
    /// </summary>
    private static GameObject ResolveTrailPrefab(EntityManager entityManager, Entity playerEntity)
    {
        if (!entityManager.HasComponent<PlayerElementalTrailAttachedVfxPrefabReference>(playerEntity))
            return null;

        PlayerElementalTrailAttachedVfxPrefabReference prefabReference = entityManager.GetComponentData<PlayerElementalTrailAttachedVfxPrefabReference>(playerEntity);
        return prefabReference.Prefab.Value;
    }

    /// <summary>
    /// Returns a reusable managed VFX instance for the requested player and prefab.
    /// /params playerEntity Player entity used as owner key for the managed instance cache.
    /// /params trailPrefab Prefab that should back the attached trail presentation.
    /// /returns Existing or newly instantiated managed trail instance, or null when creation fails.
    /// </summary>
    private static ManagedTrailVfxInstance GetOrCreateManagedInstance(Entity playerEntity, GameObject trailPrefab)
    {
        ManagedTrailVfxInstance managedInstance;

        if (managedInstances.TryGetValue(playerEntity, out managedInstance))
        {
            bool requiresRebuild = managedInstance == null ||
                                   managedInstance.InstanceObject == null ||
                                   managedInstance.SourcePrefab != trailPrefab;

            if (!requiresRebuild)
                return managedInstance;

            DestroyManagedInstance(managedInstance);
            managedInstances.Remove(playerEntity);
        }

        if (trailPrefab == null)
            return null;

        GameObject instanceObject = Object.Instantiate(trailPrefab);

        if (instanceObject == null)
            return null;

        instanceObject.name = string.Format("{0}_ElementalTrail", trailPrefab.name);
        Transform instanceTransform = instanceObject.transform;
        instanceTransform.rotation = Quaternion.identity;
        instanceTransform.localScale = Vector3.one;

        TrailRenderer[] trailRenderers = instanceObject.GetComponentsInChildren<TrailRenderer>(true);
        ConfigureTrailRenderersForGroundPlane(trailRenderers);

        if (instanceObject.activeSelf)
            instanceObject.SetActive(false);

        managedInstance = new ManagedTrailVfxInstance
        {
            SourcePrefab = trailPrefab,
            InstanceObject = instanceObject,
            TrailRenderers = trailRenderers,
            VisualCenterOffset = ResolveVisualCenterOffset(instanceTransform, trailRenderers)
        };
        managedInstances[playerEntity] = managedInstance;

    #if UNITY_EDITOR
        if ((trailRenderers == null || trailRenderers.Length <= 0) && missingTrailRendererLogCache.Add(playerEntity.Index))
        {
            Debug.LogWarning(string.Format("[ElementalTrailVfx] Prefab '{0}' has no TrailRenderer in children. Attached trail will be invisible.", trailPrefab.name));
        }
    #endif

        return managedInstance;
    }

    /// <summary>
    /// Forces attached trail renderers to use a camera-independent horizontal ribbon setup.
    /// /params trailRenderers TrailRenderer components collected from the managed VFX prefab.
    /// /returns None.
    /// </summary>
    private static void ConfigureTrailRenderersForGroundPlane(TrailRenderer[] trailRenderers)
    {
        if (trailRenderers == null || trailRenderers.Length <= 0)
            return;

        for (int rendererIndex = 0; rendererIndex < trailRenderers.Length; rendererIndex++)
        {
            TrailRenderer trailRenderer = trailRenderers[rendererIndex];

            if (trailRenderer == null)
                continue;

            Transform trailTransform = trailRenderer.transform;

            if (trailTransform != null)
                trailTransform.rotation = GroundAlignedTrailRotation;

            trailRenderer.alignment = LineAlignment.TransformZ;
            trailRenderer.emitting = false;
            trailRenderer.enabled = false;
            trailRenderer.Clear();
        }
    }

    /// <summary>
    /// Applies activation, transform and renderer state for one cached managed trail instance.
    /// /params playerEntity Player entity used to resolve the cached managed instance.
    /// /params isActive True while the passive and prefab reference are valid.
    /// /params worldPosition Desired world position for the visual emission point.
    /// /params uniformScale Uniform scale applied to the managed root.
    /// /params desiredTrailWidth Runtime trail width, or zero to stop emission while preserving fading points.
    /// /returns None.
    /// </summary>
    private static void SetManagedInstanceActive(Entity playerEntity,
                                                 bool isActive,
                                                 float3 worldPosition,
                                                 float uniformScale,
                                                 float desiredTrailWidth)
    {
        ManagedTrailVfxInstance managedInstance;

        if (!managedInstances.TryGetValue(playerEntity, out managedInstance))
            return;

        if (managedInstance == null || managedInstance.InstanceObject == null)
            return;

        if (!isActive)
        {
            ApplyTrailRenderersState(managedInstance, false, 0f, true);

            if (managedInstance.InstanceObject.activeSelf)
                managedInstance.InstanceObject.SetActive(false);

            return;
        }

        Transform instanceTransform = managedInstance.InstanceObject.transform;
        float3 visualCenterOffset = managedInstance.VisualCenterOffset;

        instanceTransform.position = new Vector3(worldPosition.x - visualCenterOffset.x,
                                                 worldPosition.y - visualCenterOffset.y,
                                                 worldPosition.z - visualCenterOffset.z);
        instanceTransform.rotation = Quaternion.identity;
        instanceTransform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);

        if (!managedInstance.InstanceObject.activeSelf)
            managedInstance.InstanceObject.SetActive(true);

        bool shouldEmit = desiredTrailWidth > WidthChangeEpsilon;
        ApplyTrailRenderersState(managedInstance, shouldEmit, desiredTrailWidth, false);
    }

    /// <summary>
    /// Updates TrailRenderer emission and width without reallocating or rebuilding the managed VFX instance.
    /// /params managedInstance Cached managed trail instance whose renderers should be updated.
    /// /params isEmitting True when player movement should add new trail points.
    /// /params desiredTrailWidth Width assigned while emitting.
    /// /params clearWhenDisabled True when disabling should immediately clear previous trail points.
    /// /returns None.
    /// </summary>
    private static void ApplyTrailRenderersState(ManagedTrailVfxInstance managedInstance,
                                                 bool isEmitting,
                                                 float desiredTrailWidth,
                                                 bool clearWhenDisabled)
    {
        if (managedInstance == null || managedInstance.TrailRenderers == null || managedInstance.TrailRenderers.Length <= 0)
            return;

        for (int rendererIndex = 0; rendererIndex < managedInstance.TrailRenderers.Length; rendererIndex++)
        {
            TrailRenderer trailRenderer = managedInstance.TrailRenderers[rendererIndex];

            if (trailRenderer == null)
                continue;

            bool wasEnabled = trailRenderer.enabled;
            bool shouldBeEnabled = isEmitting || !clearWhenDisabled;

            if (isEmitting && !wasEnabled)
                trailRenderer.Clear();

            if (trailRenderer.enabled != shouldBeEnabled)
                trailRenderer.enabled = shouldBeEnabled;

            if (trailRenderer.emitting != isEmitting)
                trailRenderer.emitting = isEmitting;

            if (isEmitting)
            {
                float clampedTrailWidth = math.max(MinimumTrailWidth, desiredTrailWidth);

                if (math.abs(trailRenderer.widthMultiplier - clampedTrailWidth) > WidthChangeEpsilon)
                    trailRenderer.widthMultiplier = clampedTrailWidth;
            }
            else if (clearWhenDisabled)
            {
                trailRenderer.Clear();
            }
        }
    }

    /// <summary>
    /// Computes the average local offset between the prefab root and its TrailRenderer emission transforms.
    /// /params rootTransform Root transform of the instantiated managed VFX object.
    /// /params trailRenderers TrailRenderer components used as visual emission anchors.
    /// /returns Average root-local offset used to keep the visual center aligned with the player.
    /// </summary>
    private static float3 ResolveVisualCenterOffset(Transform rootTransform, TrailRenderer[] trailRenderers)
    {
        if (rootTransform == null || trailRenderers == null || trailRenderers.Length <= 0)
            return float3.zero;

        float3 accumulatedOffset = float3.zero;
        int validRendererCount = 0;

        for (int rendererIndex = 0; rendererIndex < trailRenderers.Length; rendererIndex++)
        {
            TrailRenderer trailRenderer = trailRenderers[rendererIndex];

            if (trailRenderer == null)
                continue;

            Vector3 localPosition = rootTransform.InverseTransformPoint(trailRenderer.transform.position);
            accumulatedOffset += new float3(localPosition.x, localPosition.y, localPosition.z);
            validRendererCount++;
        }

        if (validRendererCount <= 0)
            return float3.zero;

        return accumulatedOffset / validRendererCount;
    }

    /// <summary>
    /// Destroys managed instances whose owner entity no longer exists in the current world.
    /// /params entityManager Entity manager used to validate cached owner entities.
    /// /returns None.
    /// </summary>
    private static void CleanupInvalidOwnerInstances(EntityManager entityManager)
    {
        if (managedInstances.Count <= 0)
            return;

        invalidOwnerEntities.Clear();
        Dictionary<Entity, ManagedTrailVfxInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Entity ownerEntity = enumerator.Current.Key;

            if (IsValidEntity(entityManager, ownerEntity))
                continue;

            DestroyManagedInstance(enumerator.Current.Value);
            invalidOwnerEntities.Add(ownerEntity);
        }

        enumerator.Dispose();

        for (int index = 0; index < invalidOwnerEntities.Count; index++)
            managedInstances.Remove(invalidOwnerEntities[index]);

        invalidOwnerEntities.Clear();
    }

    /// <summary>
    /// Destroys one managed trail GameObject and clears cached Unity component references.
    /// /params managedInstance Managed VFX instance being released.
    /// /returns None.
    /// </summary>
    private static void DestroyManagedInstance(ManagedTrailVfxInstance managedInstance)
    {
        if (managedInstance == null || managedInstance.InstanceObject == null)
            return;

        Object.Destroy(managedInstance.InstanceObject);
        managedInstance.InstanceObject = null;
        managedInstance.TrailRenderers = null;
        managedInstance.SourcePrefab = null;
    }

    /// <summary>
    /// Releases a legacy pooled VFX entity reference if one was left by older runtime paths.
    /// /params entityManager Entity manager used to disable and strip transient VFX components.
    /// /params vfxEntity Entity previously referenced by PlayerElementalTrailAttachedVfxState.
    /// /returns None.
    /// </summary>
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
    /// /params entityManager Entity manager that owns the runtime world.
    /// /params entity Entity handle to validate.
    /// /returns True when the entity is non-null, has a valid index and still exists.
    /// </summary>
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

    #region Nested Types
    /// <summary>
    /// Stores cached Unity objects for one player-owned attached Elemental Trail VFX instance.
    /// /params None.
    /// /returns None.
    /// </summary>
    private sealed class ManagedTrailVfxInstance
    {
        public GameObject SourcePrefab;
        public GameObject InstanceObject;
        public TrailRenderer[] TrailRenderers;
        public float3 VisualCenterOffset;
    }
    #endregion

    #endregion
}
