using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Spawns and synchronizes a managed player visual GameObject when no valid Animator companion is available.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(PlayerAnimatorSyncSystem))]
public partial struct PlayerManagedVisualAnimatorBridgeSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<Entity, ManagedPlayerVisualInstance> managedInstances = new Dictionary<Entity, ManagedPlayerVisualInstance>(2);
    private static readonly Dictionary<Entity, byte> appliedRenderHiddenState = new Dictionary<Entity, byte>(2);
    private static readonly List<Entity> invalidOwnerEntities = new List<Entity>(4);
    private static readonly List<Entity> hierarchyTraversalEntities = new List<Entity>(32);
    private static readonly List<PendingAnimatorAssignment> pendingAnimatorAssignments = new List<PendingAnimatorAssignment>(2);
    private static readonly List<PendingRenderVisibilityAssignment> pendingRenderVisibilityAssignments = new List<PendingRenderVisibilityAssignment>(2);
#if UNITY_EDITOR
    private static readonly HashSet<int> missingPrefabLogCache = new HashSet<int>();
    private static readonly HashSet<int> missingAnimatorLogCache = new HashSet<int>();
#endif
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Keeps lifecycle cleanup active for the persistent game world even while no player entity is loaded.
    /// </summary>
    /// <param name="state">System state used to bind updates to the persistent scene manager.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameSceneManagerConfig>();
    }

    /// <summary>
    /// Releases every persistent managed visual when the owning ECS world is disposed.
    /// </summary>
    /// <param name="state">System state owning the player presentation world.</param>
    public void OnDestroy(ref SystemState state)
    {
        if (managedInstances.Count > 0)
        {
            Dictionary<Entity, ManagedPlayerVisualInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

            while (enumerator.MoveNext())
            {
                DestroyManagedInstance(enumerator.Current.Value);
            }

            enumerator.Dispose();
        }

        managedInstances.Clear();
        appliedRenderHiddenState.Clear();
        invalidOwnerEntities.Clear();
        hierarchyTraversalEntities.Clear();
        pendingAnimatorAssignments.Clear();
        pendingRenderVisibilityAssignments.Clear();
        PlayerManagedVisualBridgeCompanionVfxVisibilityUtility.RestoreAll();
#if UNITY_EDITOR
        missingPrefabLogCache.Clear();
        missingAnimatorLogCache.Clear();
#endif
    }

    /// <summary>
    /// Synchronizes live player visuals and removes persistent instances immediately after their ECS owner disappears.
    /// </summary>
    /// <param name="state">System state providing the player presentation entity manager.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        EntityManager entityManager = state.EntityManager;
        CleanupInvalidOwnerInstances(entityManager);
        pendingAnimatorAssignments.Clear();
        pendingRenderVisibilityAssignments.Clear();
        ComponentLookup<LocalTransform> playerTransformLookup =
            SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach ((RefRO<PlayerVisualRuntimeDataOwner> visualRuntimeOwner,
                  RefRO<PlayerVisualRuntimeBridgeConfig> visualBridgeConfig,
                  Entity visualRuntimeEntity)
                 in SystemAPI.Query<RefRO<PlayerVisualRuntimeDataOwner>,
                                    RefRO<PlayerVisualRuntimeBridgeConfig>>()
                             .WithEntityAccess())
        {
            Entity playerEntity = visualRuntimeOwner.ValueRO.PlayerEntity;

            if (!playerTransformLookup.TryGetComponent(playerEntity,
                                                       out LocalTransform playerTransform))
            {
                continue;
            }

            Animator animatorComponent = ResolveAnimatorComponent(entityManager, visualRuntimeEntity);
            bool runtimeBridgeEnabled = visualBridgeConfig.ValueRO.SpawnWhenAnimatorMissing != 0;
            ManagedPlayerVisualInstance runtimeInstance;
            bool hasRuntimeInstance = managedInstances.TryGetValue(playerEntity, out runtimeInstance);
            bool shouldUseRuntimeBridge = false;

            if (runtimeBridgeEnabled)
            {
                if (hasRuntimeInstance)
                {
                    shouldUseRuntimeBridge = true;
                }
                else if (animatorComponent == null)
                {
                    shouldUseRuntimeBridge = true;
                }
            }

            if (shouldUseRuntimeBridge)
            {
                ManagedPlayerVisualInstance managedInstance = GetOrCreateManagedInstance(playerEntity, visualBridgeConfig.ValueRO.VisualPrefab.Value);

                if (managedInstance != null && managedInstance.AnimatorComponent != null)
                {
                    QueueAnimatorAssignment(entityManager,
                                            visualRuntimeEntity,
                                            managedInstance.AnimatorComponent);
                }

                runtimeInstance = managedInstance;
                hasRuntimeInstance = managedInstance != null;
            }
            else if (hasRuntimeInstance)
            {
                DestroyManagedInstance(runtimeInstance);
                managedInstances.Remove(playerEntity);
                runtimeInstance = null;
                hasRuntimeInstance = false;
            }

            if (hasRuntimeInstance &&
                (runtimeInstance == null || runtimeInstance.InstanceObject == null || runtimeInstance.RootTransform == null))
            {
                DestroyManagedInstance(runtimeInstance);
                managedInstances.Remove(playerEntity);
                runtimeInstance = null;
                hasRuntimeInstance = false;
            }

            if (hasRuntimeInstance)
            {
                SyncManagedInstanceTransform(runtimeInstance,
                                             playerTransform,
                                             visualBridgeConfig.ValueRO);
            }

            QueueRenderVisibilityAssignment(playerEntity, hasRuntimeInstance);
        }

        ApplyQueuedAnimatorAssignments(entityManager);
        ApplyQueuedRenderVisibilityAssignments(entityManager);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Disables the runtime visual bridge GameObject for the requested player entity. Used by the death animation
    /// presentation system to hide the player rig the frame the despawn VFX takes over so the VFX visually replaces
    /// the player. No-op when the entity has no managed instance (the bridge was never spawned, e.g. an Animator
    /// companion was used instead).
    /// </summary>
    /// <param name="playerEntity">Player entity whose managed bridge GameObject should be disabled.</param>
    /// <returns>True when an instance was found and hidden, otherwise false.</returns>
    public static bool TryHideRuntimeBridgeInstance(Entity playerEntity)
    {
        if (!managedInstances.TryGetValue(playerEntity, out ManagedPlayerVisualInstance managedInstance))
            return false;

        if (managedInstance == null || managedInstance.InstanceObject == null)
            return false;

        if (managedInstance.InstanceObject.activeSelf)
            managedInstance.InstanceObject.SetActive(false);

        return true;
    }

    /// <summary>
    /// Enables the runtime visual bridge GameObject for the requested player entity after a death-animation hide.
    /// Used when the same player entity returns to an idle run state and the bridge instance should be reused.
    /// </summary>
    /// <param name="playerEntity">Player entity whose managed bridge GameObject should be enabled.</param>
    /// <returns>True when an instance was found and shown, otherwise false.</returns>
    public static bool TryShowRuntimeBridgeInstance(Entity playerEntity)
    {
        if (!managedInstances.TryGetValue(playerEntity, out ManagedPlayerVisualInstance managedInstance))
            return false;

        if (managedInstance == null || managedInstance.InstanceObject == null)
            return false;

        if (!managedInstance.InstanceObject.activeSelf)
            managedInstance.InstanceObject.SetActive(true);

        return true;
    }

    /// <summary>
    /// Resolves the root transform of the runtime-spawned Visual Player instance owned by one player entity.
    /// </summary>
    /// <param name="playerEntity">Player entity whose managed Visual Player root should be resolved.</param>
    /// <param name="rootTransform">Resolved runtime visual root when available.</param>
    /// <returns>True when a valid runtime Visual Player instance exists.</returns>
    public static bool TryGetRuntimeBridgeRoot(Entity playerEntity, out Transform rootTransform)
    {
        rootTransform = null;

        if (!managedInstances.TryGetValue(playerEntity, out ManagedPlayerVisualInstance managedInstance))
            return false;

        if (managedInstance == null || managedInstance.InstanceObject == null || managedInstance.RootTransform == null)
            return false;

        rootTransform = managedInstance.RootTransform;
        return true;
    }

    /// <summary>
    /// Queues one managed Animator replacement so structural changes occur after the presentation query completes.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect the current managed component.</param>
    /// <param name="visualRuntimeEntity">Presentation companion that receives the Animator.</param>
    /// <param name="targetAnimatorComponent">Managed Animator resolved from the active visual hierarchy.</param>
    private static void QueueAnimatorAssignment(EntityManager entityManager,
                                                Entity visualRuntimeEntity,
                                                Animator targetAnimatorComponent)
    {
        if (targetAnimatorComponent == null)
        {
            return;
        }

        if (entityManager.HasComponent<Animator>(visualRuntimeEntity))
        {
            Animator currentAnimatorComponent =
                entityManager.GetComponentObject<Animator>(visualRuntimeEntity);

            if (currentAnimatorComponent == targetAnimatorComponent)
            {
                return;
            }
        }

        for (int assignmentIndex = 0; assignmentIndex < pendingAnimatorAssignments.Count; assignmentIndex++)
        {
            PendingAnimatorAssignment existingAssignment = pendingAnimatorAssignments[assignmentIndex];

            if (existingAssignment.VisualRuntimeEntity != visualRuntimeEntity)
            {
                continue;
            }

            existingAssignment.AnimatorComponent = targetAnimatorComponent;
            pendingAnimatorAssignments[assignmentIndex] = existingAssignment;
            return;
        }

        pendingAnimatorAssignments.Add(new PendingAnimatorAssignment
        {
            VisualRuntimeEntity = visualRuntimeEntity,
            AnimatorComponent = targetAnimatorComponent
        });
    }

    /// <summary>
    /// Applies queued managed Animator replacements to presentation companions after entity iteration.
    /// </summary>
    /// <param name="entityManager">Entity manager used for the deferred structural changes.</param>
    private static void ApplyQueuedAnimatorAssignments(EntityManager entityManager)
    {
        if (pendingAnimatorAssignments.Count <= 0)
        {
            return;
        }

        for (int assignmentIndex = 0; assignmentIndex < pendingAnimatorAssignments.Count; assignmentIndex++)
        {
            PendingAnimatorAssignment assignment = pendingAnimatorAssignments[assignmentIndex];

            if (!entityManager.Exists(assignment.VisualRuntimeEntity))
            {
                continue;
            }

            if (assignment.AnimatorComponent == null)
            {
                continue;
            }

            if (entityManager.HasComponent<Animator>(assignment.VisualRuntimeEntity))
            {
                Animator currentAnimatorComponent =
                    entityManager.GetComponentObject<Animator>(assignment.VisualRuntimeEntity);

                if (currentAnimatorComponent == assignment.AnimatorComponent)
                {
                    continue;
                }

                entityManager.RemoveComponent<Animator>(assignment.VisualRuntimeEntity);
            }

            entityManager.AddComponentObject(assignment.VisualRuntimeEntity,
                                             assignment.AnimatorComponent);
        }

        pendingAnimatorAssignments.Clear();
    }

    private static void QueueRenderVisibilityAssignment(Entity playerEntity, bool hideRendering)
    {
        byte hideRenderingByte = hideRendering ? (byte)1 : (byte)0;

        for (int assignmentIndex = 0; assignmentIndex < pendingRenderVisibilityAssignments.Count; assignmentIndex++)
        {
            PendingRenderVisibilityAssignment existingAssignment = pendingRenderVisibilityAssignments[assignmentIndex];

            if (existingAssignment.PlayerEntity != playerEntity)
            {
                continue;
            }

            existingAssignment.HideRendering = hideRenderingByte;
            pendingRenderVisibilityAssignments[assignmentIndex] = existingAssignment;
            return;
        }

        pendingRenderVisibilityAssignments.Add(new PendingRenderVisibilityAssignment
        {
            PlayerEntity = playerEntity,
            HideRendering = hideRenderingByte
        });
    }

    private static void ApplyQueuedRenderVisibilityAssignments(EntityManager entityManager)
    {
        if (pendingRenderVisibilityAssignments.Count <= 0)
        {
            return;
        }

        for (int assignmentIndex = 0; assignmentIndex < pendingRenderVisibilityAssignments.Count; assignmentIndex++)
        {
            PendingRenderVisibilityAssignment assignment = pendingRenderVisibilityAssignments[assignmentIndex];

            if (!entityManager.Exists(assignment.PlayerEntity))
            {
                appliedRenderHiddenState.Remove(assignment.PlayerEntity);
                continue;
            }

            byte appliedState;

            if (appliedRenderHiddenState.TryGetValue(assignment.PlayerEntity, out appliedState) &&
                appliedState == assignment.HideRendering)
            {
                continue;
            }

            SetHierarchyRenderingHidden(entityManager, assignment.PlayerEntity, assignment.HideRendering != 0);
            appliedRenderHiddenState[assignment.PlayerEntity] = assignment.HideRendering;
        }

        pendingRenderVisibilityAssignments.Clear();
    }

    private static void SetHierarchyRenderingHidden(EntityManager entityManager, Entity rootEntity, bool hidden)
    {
        CollectHierarchyEntities(entityManager, rootEntity, hierarchyTraversalEntities);

        for (int entityIndex = 0; entityIndex < hierarchyTraversalEntities.Count; entityIndex++)
        {
            Entity hierarchyEntity = hierarchyTraversalEntities[entityIndex];
            PlayerManagedVisualBridgeCompanionVfxVisibilityUtility.SetHidden(entityManager, hierarchyEntity, hidden);

            if (!entityManager.HasComponent<MaterialMeshInfo>(hierarchyEntity))
            {
                continue;
            }

            bool hasDisableRendering = entityManager.HasComponent<DisableRendering>(hierarchyEntity);

            if (hidden)
            {
                if (!hasDisableRendering)
                {
                    entityManager.AddComponent<DisableRendering>(hierarchyEntity);
                }

                continue;
            }

            if (hasDisableRendering)
            {
                entityManager.RemoveComponent<DisableRendering>(hierarchyEntity);
            }
        }
    }

    private static void CollectHierarchyEntities(EntityManager entityManager, Entity rootEntity, List<Entity> outputEntities)
    {
        outputEntities.Clear();

        if (!IsValidEntity(entityManager, rootEntity))
        {
            return;
        }

        outputEntities.Add(rootEntity);

        for (int entityIndex = 0; entityIndex < outputEntities.Count; entityIndex++)
        {
            Entity currentEntity = outputEntities[entityIndex];

            if (!entityManager.HasBuffer<Child>(currentEntity))
            {
                continue;
            }

            DynamicBuffer<Child> childrenBuffer = entityManager.GetBuffer<Child>(currentEntity);

            for (int childIndex = 0; childIndex < childrenBuffer.Length; childIndex++)
            {
                Entity childEntity = childrenBuffer[childIndex].Value;

                if (entityManager.Exists(childEntity))
                {
                    outputEntities.Add(childEntity);
                }
            }
        }
    }

    /// <summary>
    /// Resolves the managed Animator currently attached to one presentation companion.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect managed components.</param>
    /// <param name="visualRuntimeEntity">Presentation companion expected to own the Animator.</param>
    /// <returns>Current managed Animator, or null when the bridge has not assigned one yet.</returns>
    private static Animator ResolveAnimatorComponent(EntityManager entityManager,
                                                     Entity visualRuntimeEntity)
    {
        if (!entityManager.HasComponent<Animator>(visualRuntimeEntity))
        {
            return null;
        }

        return entityManager.GetComponentObject<Animator>(visualRuntimeEntity);
    }

    /// <summary>
    /// Resolves or creates the persistent managed visual owned by one authoritative player entity.
    /// </summary>
    /// <param name="playerEntity">Player entity retaining visual ownership across room scenes.</param>
    /// <param name="runtimeVisualPrefab">Configured managed player visual prefab.</param>
    /// <returns>Reusable persistent visual instance, or null when configuration is incomplete.</returns>
    private static ManagedPlayerVisualInstance GetOrCreateManagedInstance(Entity playerEntity, GameObject runtimeVisualPrefab)
    {
        ManagedPlayerVisualInstance managedInstance;

        if (managedInstances.TryGetValue(playerEntity, out managedInstance))
        {
            bool requiresRebuild = managedInstance == null ||
                                   managedInstance.InstanceObject == null ||
                                   managedInstance.SourcePrefab != runtimeVisualPrefab;

            if (!requiresRebuild)
            {
                return managedInstance;
            }

            DestroyManagedInstance(managedInstance);
            managedInstances.Remove(playerEntity);
        }

        if (runtimeVisualPrefab == null)
        {
#if UNITY_EDITOR
            if (missingPrefabLogCache.Add(playerEntity.Index))
            {
                Debug.LogWarning("[PlayerManagedVisualAnimatorBridgeSystem] Runtime visual bridge prefab is missing. Assign a prefab asset on the active PlayerVisualPreset, or keep the hidden PlayerAuthoring fallback populated.");
            }
#endif
            return null;
        }

        GameObject instanceObject = Object.Instantiate(runtimeVisualPrefab);

        if (instanceObject == null)
        {
            return null;
        }

        Animator animatorComponent = instanceObject.GetComponentInChildren<Animator>(true);

        if (animatorComponent == null)
        {
#if UNITY_EDITOR
            if (missingAnimatorLogCache.Add(playerEntity.Index))
            {
                Debug.LogWarning(string.Format("[PlayerManagedVisualAnimatorBridgeSystem] Runtime visual bridge prefab '{0}' has no Animator in hierarchy.", runtimeVisualPrefab.name));
            }
#endif
            Object.Destroy(instanceObject);
            return null;
        }

        instanceObject.name = string.Format("{0}_RuntimeVisual", runtimeVisualPrefab.name);

        // The authoritative player entity survives room replacement, so its managed visual must not inherit the
        // currently active room scene's lifetime. Entity-owner cleanup still destroys it at run or world teardown.
        if (Application.isPlaying)
            Object.DontDestroyOnLoad(instanceObject);

        managedInstance = new ManagedPlayerVisualInstance
        {
            SourcePrefab = runtimeVisualPrefab,
            InstanceObject = instanceObject,
            RootTransform = instanceObject.transform,
            AnimatorComponent = animatorComponent
        };

        managedInstances[playerEntity] = managedInstance;
        return managedInstance;
    }

    private static void SyncManagedInstanceTransform(ManagedPlayerVisualInstance runtimeInstance,
                                                     in LocalTransform playerTransform,
                                                     in PlayerVisualRuntimeBridgeConfig visualBridgeConfig)
    {
        if (runtimeInstance == null || runtimeInstance.RootTransform == null)
        {
            return;
        }

        float3 rotatedOffset = math.rotate(playerTransform.Rotation, visualBridgeConfig.PositionOffset);
        float3 worldPosition = playerTransform.Position + rotatedOffset;
        runtimeInstance.RootTransform.position = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z);

        if (visualBridgeConfig.SyncRotation != 0)
        {
            quaternion rotation = playerTransform.Rotation;
            runtimeInstance.RootTransform.rotation = new Quaternion(rotation.value.x,
                                                                    rotation.value.y,
                                                                    rotation.value.z,
                                                                    rotation.value.w);
        }
    }

    private static void CleanupInvalidOwnerInstances(EntityManager entityManager)
    {
        if (managedInstances.Count <= 0)
        {
            return;
        }

        invalidOwnerEntities.Clear();
        Dictionary<Entity, ManagedPlayerVisualInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Entity ownerEntity = enumerator.Current.Key;

            if (IsValidEntity(entityManager, ownerEntity))
            {
                continue;
            }

            DestroyManagedInstance(enumerator.Current.Value);
            invalidOwnerEntities.Add(ownerEntity);
        }

        enumerator.Dispose();

        for (int index = 0; index < invalidOwnerEntities.Count; index++)
        {
            Entity invalidOwnerEntity = invalidOwnerEntities[index];
            managedInstances.Remove(invalidOwnerEntity);
            appliedRenderHiddenState.Remove(invalidOwnerEntity);
        }

        invalidOwnerEntities.Clear();
    }

    private static void DestroyManagedInstance(ManagedPlayerVisualInstance managedInstance)
    {
        if (managedInstance == null || managedInstance.InstanceObject == null)
        {
            return;
        }

        if (Application.isPlaying)
            Object.Destroy(managedInstance.InstanceObject);
        else
            Object.DestroyImmediate(managedInstance.InstanceObject);

        managedInstance.InstanceObject = null;
        managedInstance.RootTransform = null;
        managedInstance.AnimatorComponent = null;
        managedInstance.SourcePrefab = null;
    }

    private static bool IsValidEntity(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null)
        {
            return false;
        }

        if (entity.Index < 0)
        {
            return false;
        }

        if (!entityManager.Exists(entity))
        {
            return false;
        }

        return true;
    }
    #endregion

    #region Nested Types
    private sealed class ManagedPlayerVisualInstance
    {
        public GameObject SourcePrefab;
        public GameObject InstanceObject;
        public Transform RootTransform;
        public Animator AnimatorComponent;
    }

    private struct PendingAnimatorAssignment
    {
        public Entity VisualRuntimeEntity;
        public Animator AnimatorComponent;
    }

    private struct PendingRenderVisibilityAssignment
    {
        public Entity PlayerEntity;
        public byte HideRendering;
    }
    #endregion

    #endregion
}
