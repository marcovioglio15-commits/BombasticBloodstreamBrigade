using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Resolves runtime-usable dropped-container views, creating scene clones when ECS companion objects are not directly renderable.
/// none.
/// </summary>
public static class PlayerDroppedPowerUpContainerViewRuntimeUtility
{
    #region Fields
    private static Dictionary<Entity, PlayerDroppedPowerUpContainerView> cachedViewsByEntity;
    private static Dictionary<Entity, PlayerDroppedPowerUpContainerView> fallbackViewsByEntity;
    private static Stack<PlayerDroppedPowerUpContainerView> fallbackViewPool;
    private static List<Entity> fallbackReleaseCandidates;
    private static PlayerDroppedPowerUpContainerView fallbackTemplateView;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one dropped-container view that is guaranteed to be usable at runtime inside the active scene.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect runtime ECS data.</param>
    /// <param name="containerEntity">Dropped container entity whose view must be resolved.</param>
    /// <param name="containerView">Runtime-usable view returned to the caller when available.</param>
    /// <returns>True when a usable view was resolved; otherwise false.</returns>
    public static bool TryResolveRuntimeView(EntityManager entityManager,
                                             Entity containerEntity,
                                             out PlayerDroppedPowerUpContainerView containerView)
    {
        EnsureCollections();
        containerView = null;

        if (!TryResolveViewEntity(entityManager, containerEntity, out Entity viewEntity))
            return false;

        if (cachedViewsByEntity.TryGetValue(containerEntity, out PlayerDroppedPowerUpContainerView cachedView))
        {
            if (IsRuntimeUsableView(cachedView))
            {
                EnsureViewActive(cachedView);
                containerView = cachedView;
                return true;
            }

            cachedViewsByEntity.Remove(containerEntity);
        }

        PlayerDroppedPowerUpContainerView resolvedView = entityManager.GetComponentObject<PlayerDroppedPowerUpContainerView>(viewEntity);

        if (resolvedView == null)
            return false;

        PlayerDroppedPowerUpContainerView runtimeView = ResolveRuntimeUsableView(containerEntity, resolvedView);

        if (runtimeView == null)
            return false;

        cachedViewsByEntity[containerEntity] = runtimeView;
        containerView = runtimeView;
        return true;
    }

    /// <summary>
    /// Synchronizes one runtime-usable dropped-container view with the current ECS transform state.
    /// </summary>
    /// <param name="entityManager">Entity manager used to resolve the runtime view.</param>
    /// <param name="containerEntity">Dropped container entity whose view must be synchronized.</param>
    /// <param name="containerTransform">ECS transform driving the runtime scene view.</param>
    public static void SyncViewPose(EntityManager entityManager,
                                    Entity containerEntity,
                                    in LocalTransform containerTransform)
    {
        if (!TryResolveRuntimeView(entityManager, containerEntity, out PlayerDroppedPowerUpContainerView containerView))
            return;

        SyncViewPose(containerView, in containerTransform);
    }

    /// <summary>
    /// Synchronizes one already-resolved runtime-usable dropped-container view with the current ECS transform state.
    /// </summary>
    /// <param name="containerView">Runtime view already resolved for the target container.</param>
    /// <param name="containerTransform">ECS transform driving the runtime scene view.</param>
    public static void SyncViewPose(PlayerDroppedPowerUpContainerView containerView,
                                    in LocalTransform containerTransform)
    {
        if (containerView == null)
            return;

        Vector3 worldPosition = new Vector3(containerTransform.Position.x,
                                            containerTransform.Position.y,
                                            containerTransform.Position.z);
        Quaternion worldRotation = new Quaternion(containerTransform.Rotation.value.x,
                                                 containerTransform.Rotation.value.y,
                                                 containerTransform.Rotation.value.z,
                                                 containerTransform.Rotation.value.w);
        containerView.SyncWorldPose(worldPosition, worldRotation, containerTransform.Scale);
    }

    /// <summary>
    /// Releases the runtime view currently associated with one consumed dropped-container entity.
    /// </summary>
    /// <param name="containerEntity">Dropped container entity whose visual runtime state must be cleared.</param>
    public static void ReleaseRuntimeView(Entity containerEntity)
    {
        EnsureCollections();

        if (containerEntity == Entity.Null)
            return;

        if (fallbackViewsByEntity.TryGetValue(containerEntity, out PlayerDroppedPowerUpContainerView fallbackView))
        {
            cachedViewsByEntity.Remove(containerEntity);
            fallbackViewsByEntity.Remove(containerEntity);
            ReleaseFallbackView(fallbackView);
            return;
        }

        if (!cachedViewsByEntity.TryGetValue(containerEntity, out PlayerDroppedPowerUpContainerView cachedView))
            return;

        cachedViewsByEntity.Remove(containerEntity);
        HideRuntimeView(cachedView, true);
    }

    /// <summary>
    /// Releases fallback runtime clones whose owning dropped-container entities are no longer valid.
    /// </summary>
    /// <param name="entityManager">Entity manager used to detect stale container entities.</param>
    public static void ReleaseInactiveViews(EntityManager entityManager)
    {
        EnsureCollections();

        if (fallbackViewsByEntity.Count <= 0)
        {
            ReleaseStaleCachedViews(entityManager);
            return;
        }

        fallbackReleaseCandidates.Clear();

        foreach (KeyValuePair<Entity, PlayerDroppedPowerUpContainerView> pair in fallbackViewsByEntity)
        {
            if (IsContainerEntityAlive(entityManager, pair.Key))
                continue;

            fallbackReleaseCandidates.Add(pair.Key);
        }

        for (int releaseIndex = 0; releaseIndex < fallbackReleaseCandidates.Count; releaseIndex++)
        {
            Entity containerEntity = fallbackReleaseCandidates[releaseIndex];

            if (!fallbackViewsByEntity.TryGetValue(containerEntity, out PlayerDroppedPowerUpContainerView fallbackView))
                continue;

            cachedViewsByEntity.Remove(containerEntity);
            fallbackViewsByEntity.Remove(containerEntity);
            ReleaseFallbackView(fallbackView);
        }

        ReleaseStaleCachedViews(entityManager);
    }

    /// <summary>
    /// Destroys every pooled or active fallback runtime clone and clears cached view state.
    /// none.
    /// </summary>
    public static void Shutdown()
    {
        EnsureCollections();
        DestroyFallbackViews(fallbackViewsByEntity.Values);
        fallbackViewsByEntity.Clear();
        DestroyFallbackViews(fallbackViewPool);
        fallbackViewPool.Clear();
        cachedViewsByEntity.Clear();
        fallbackReleaseCandidates.Clear();
        fallbackTemplateView = null;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Lazily allocates the runtime caches used by the resolver utility.
    /// none.
    /// </summary>
    private static void EnsureCollections()
    {
        if (cachedViewsByEntity == null)
            cachedViewsByEntity = new Dictionary<Entity, PlayerDroppedPowerUpContainerView>(128);

        if (fallbackViewsByEntity == null)
            fallbackViewsByEntity = new Dictionary<Entity, PlayerDroppedPowerUpContainerView>(64);

        if (fallbackViewPool == null)
            fallbackViewPool = new Stack<PlayerDroppedPowerUpContainerView>(32);

        if (fallbackReleaseCandidates == null)
            fallbackReleaseCandidates = new List<Entity>(64);
    }

    /// <summary>
    /// Resolves the entity that owns the baked dropped-container view component.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect linked entities.</param>
    /// <param name="containerEntity">Root dropped-container entity.</param>
    /// <param name="viewEntity">Entity that carries the baked view component when found.</param>
    /// <returns>True when a view entity was found; otherwise false.</returns>
    private static bool TryResolveViewEntity(EntityManager entityManager,
                                             Entity containerEntity,
                                             out Entity viewEntity)
    {
        viewEntity = Entity.Null;

        if (containerEntity == Entity.Null || !entityManager.Exists(containerEntity))
            return false;

        if (entityManager.HasComponent<PlayerDroppedPowerUpContainerView>(containerEntity))
        {
            viewEntity = containerEntity;
            return true;
        }

        if (!entityManager.HasBuffer<LinkedEntityGroup>(containerEntity))
            return false;

        DynamicBuffer<LinkedEntityGroup> linkedEntities = entityManager.GetBuffer<LinkedEntityGroup>(containerEntity);

        for (int linkedIndex = 0; linkedIndex < linkedEntities.Length; linkedIndex++)
        {
            Entity linkedEntity = linkedEntities[linkedIndex].Value;

            if (linkedEntity == Entity.Null)
                continue;

            if (!entityManager.HasComponent<PlayerDroppedPowerUpContainerView>(linkedEntity))
                continue;

            viewEntity = linkedEntity;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a scene-usable view, instantiating one fallback clone when the baked ECS component object is not renderable directly.
    /// </summary>
    /// <param name="containerEntity">Dropped container entity owning the resolved view.</param>
    /// <param name="resolvedView">View returned by ECS component-object lookup.</param>
    /// <returns>Scene-usable view when available; otherwise null.</returns>
    private static PlayerDroppedPowerUpContainerView ResolveRuntimeUsableView(Entity containerEntity,
                                                                              PlayerDroppedPowerUpContainerView resolvedView)
    {
        if (IsRuntimeUsableView(resolvedView))
        {
            EnsureViewActive(resolvedView);
            return resolvedView;
        }

        if (resolvedView != null && fallbackTemplateView == null)
            fallbackTemplateView = resolvedView;

        if (fallbackViewsByEntity.TryGetValue(containerEntity, out PlayerDroppedPowerUpContainerView fallbackView))
        {
            if (IsRuntimeUsableView(fallbackView))
            {
                EnsureViewActive(fallbackView);
                return fallbackView;
            }

            fallbackViewsByEntity.Remove(containerEntity);
        }

        PlayerDroppedPowerUpContainerView acquiredView = AcquireFallbackView();

        if (acquiredView == null)
            return null;

        fallbackViewsByEntity[containerEntity] = acquiredView;
        EnsureViewActive(acquiredView);
        return acquiredView;
    }

    /// <summary>
    /// Acquires one fallback scene clone from the pool or creates a fresh instance from the baked template.
    /// none.
    /// </summary>
    /// <returns>Runtime scene clone when available; otherwise null.</returns>
    private static PlayerDroppedPowerUpContainerView AcquireFallbackView()
    {
        while (fallbackViewPool.Count > 0)
        {
            PlayerDroppedPowerUpContainerView pooledView = fallbackViewPool.Pop();

            if (!IsRuntimeUsableView(pooledView))
                continue;

            EnsureViewActive(pooledView);
            return pooledView;
        }

        if (fallbackTemplateView == null)
            return null;

        GameObject templateObject = fallbackTemplateView.gameObject;

        if (templateObject == null)
            return null;

        GameObject instanceObject = Object.Instantiate(templateObject);

        if (instanceObject == null)
            return null;

        instanceObject.name = string.Format("{0}_RuntimeClone", templateObject.name);
        instanceObject.SetActive(true);
        return instanceObject.GetComponent<PlayerDroppedPowerUpContainerView>();
    }

    /// <summary>
    /// Returns whether the resolved view currently belongs to a valid scene object that can render world-space UI.
    /// </summary>
    /// <param name="containerView">View instance evaluated for runtime usability.</param>
    /// <returns>True when the view belongs to a scene object; otherwise false.</returns>
    private static bool IsRuntimeUsableView(PlayerDroppedPowerUpContainerView containerView)
    {
        if (containerView == null)
            return false;

        GameObject containerObject = containerView.gameObject;

        if (containerObject == null)
            return false;

        return containerObject.scene.IsValid();
    }

    /// <summary>
    /// Ensures the resolved runtime scene object is active before prompt or icon updates are applied.
    /// </summary>
    /// <param name="containerView">Runtime view activated when needed.</param>
    private static void EnsureViewActive(PlayerDroppedPowerUpContainerView containerView)
    {
        if (containerView == null)
            return;

        GameObject containerObject = containerView.gameObject;

        if (containerObject == null || containerObject.activeSelf)
            return;

        containerObject.SetActive(true);
    }

    /// <summary>
    /// Returns whether the owning dropped-container entity still exists and still carries a valid payload component.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect ECS state.</param>
    /// <param name="containerEntity">Container entity evaluated for lifetime.</param>
    /// <returns>True when the container is still alive; otherwise false.</returns>
    private static bool IsContainerEntityAlive(EntityManager entityManager, Entity containerEntity)
    {
        return PlayerDroppedPowerUpContainerPayloadUtility.IsContainerUsable(entityManager, containerEntity);
    }

    /// <summary>
    /// Resets and pools one fallback scene clone for later reuse.
    /// </summary>
    /// <param name="fallbackView">Runtime scene clone returned to the pool.</param>
    private static void ReleaseFallbackView(PlayerDroppedPowerUpContainerView fallbackView)
    {
        if (!IsRuntimeUsableView(fallbackView))
            return;

        HideRuntimeView(fallbackView, false);
        GameObject fallbackObject = fallbackView.gameObject;
        fallbackObject.SetActive(false);
        fallbackViewPool.Push(fallbackView);
    }

    /// <summary>
    /// Clears visible state from one runtime container view and optionally disables its scene object.
    /// </summary>
    /// <param name="containerView">Runtime container view to hide.</param>
    /// <param name="deactivateObject">True when the backing GameObject should be disabled immediately.</param>
    private static void HideRuntimeView(PlayerDroppedPowerUpContainerView containerView, bool deactivateObject)
    {
        if (!IsRuntimeUsableView(containerView))
            return;

        containerView.HidePrompts();
        containerView.SetIcon(null);

        if (!deactivateObject)
            return;

        GameObject containerObject = containerView.gameObject;

        if (containerObject != null && containerObject.activeSelf)
            containerObject.SetActive(false);
    }

    /// <summary>
    /// Removes cached entries for container entities that no longer exist, even when they were backed by authored scene views.
    /// </summary>
    /// <param name="entityManager">Entity manager used to validate cached entity keys.</param>
    private static void ReleaseStaleCachedViews(EntityManager entityManager)
    {
        if (cachedViewsByEntity.Count <= 0)
            return;

        fallbackReleaseCandidates.Clear();

        foreach (KeyValuePair<Entity, PlayerDroppedPowerUpContainerView> pair in cachedViewsByEntity)
        {
            if (IsContainerEntityAlive(entityManager, pair.Key))
                continue;

            fallbackReleaseCandidates.Add(pair.Key);
        }

        for (int releaseIndex = 0; releaseIndex < fallbackReleaseCandidates.Count; releaseIndex++)
        {
            Entity containerEntity = fallbackReleaseCandidates[releaseIndex];

            if (cachedViewsByEntity.TryGetValue(containerEntity, out PlayerDroppedPowerUpContainerView cachedView))
                HideRuntimeView(cachedView, true);

            cachedViewsByEntity.Remove(containerEntity);
        }
    }

    /// <summary>
    /// Destroys every fallback view contained in the provided enumerable collection.
    /// </summary>
    /// <param name="fallbackViews">Collection of fallback views that must be destroyed.</param>
    private static void DestroyFallbackViews(IEnumerable<PlayerDroppedPowerUpContainerView> fallbackViews)
    {
        foreach (PlayerDroppedPowerUpContainerView fallbackView in fallbackViews)
        {
            if (fallbackView == null)
                continue;

            GameObject fallbackObject = fallbackView.gameObject;

            if (fallbackObject == null)
                continue;

            Object.Destroy(fallbackObject);
        }
    }
    #endregion

    #endregion
}
