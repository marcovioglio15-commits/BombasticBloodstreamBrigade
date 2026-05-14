using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves runtime-usable offensive engagement billboard views, creating scene clones when ECS component objects are not directly renderable.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyOffensiveEngagementBillboardRuntimeUtility
{
    #region Fields
    private static Dictionary<Entity, EnemyOffensiveEngagementBillboardView> cachedViewsByEnemy;
    private static Dictionary<Entity, EnemyOffensiveEngagementBillboardView> fallbackViewsByEnemy;
    private static Stack<EnemyOffensiveEngagementBillboardView> fallbackViewPool;
    private static List<Entity> fallbackReleaseCandidates;
    private static EnemyOffensiveEngagementBillboardView fallbackTemplateView;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one offensive billboard view that is guaranteed to belong to a live scene object.
    /// /params entityManager Entity manager used to inspect runtime ECS state.
    /// /params enemyEntity Enemy entity whose billboard view must be resolved.
    /// /params billboardView Runtime-usable billboard view returned to the caller when available.
    /// /returns True when a runtime-usable billboard view was resolved; otherwise false.
    /// </summary>
    public static bool TryResolveRuntimeView(EntityManager entityManager,
                                             Entity enemyEntity,
                                             out EnemyOffensiveEngagementBillboardView billboardView)
    {
        // Ensure caches exist before the first resolve attempt.
        EnsureCollections();
        billboardView = null;

        // Reject invalid or incompatible enemy entities early.
        if (enemyEntity == Entity.Null ||
            !entityManager.Exists(enemyEntity) ||
            !entityManager.HasComponent<EnemyOffensiveEngagementBillboardView>(enemyEntity))
        {
            return false;
        }

        // Reuse a cached scene-valid view when one is already available.
        if (cachedViewsByEnemy.TryGetValue(enemyEntity, out EnemyOffensiveEngagementBillboardView cachedView))
        {
            if (IsRuntimeUsableView(cachedView))
            {
                EnsureViewActive(cachedView);
                billboardView = cachedView;
                return true;
            }

            cachedViewsByEnemy.Remove(enemyEntity);
        }

        // Resolve the baked ECS component object and upgrade it to a scene-usable view when needed.
        EnemyOffensiveEngagementBillboardView resolvedView = entityManager.GetComponentObject<EnemyOffensiveEngagementBillboardView>(enemyEntity);

        if (resolvedView == null)
        {
            return false;
        }

        EnemyOffensiveEngagementBillboardView runtimeView = ResolveRuntimeUsableView(enemyEntity, resolvedView);

        if (runtimeView == null)
        {
            return false;
        }

        cachedViewsByEnemy[enemyEntity] = runtimeView;
        billboardView = runtimeView;
        return true;
    }

    /// <summary>
    /// Releases fallback runtime clones whose owning enemies are no longer active.
    /// /params entityManager Entity manager used to inspect enemy lifetime and activation state.
    /// /returns None.
    /// </summary>
    public static void ReleaseInactiveViews(EntityManager entityManager)
    {
        // Ensure caches exist even when the first call happens during teardown.
        EnsureCollections();

        // Collect fallback clones that must be returned to the pool.
        fallbackReleaseCandidates.Clear();

        foreach (KeyValuePair<Entity, EnemyOffensiveEngagementBillboardView> pair in fallbackViewsByEnemy)
        {
            if (IsEnemyAlive(entityManager, pair.Key))
            {
                continue;
            }

            fallbackReleaseCandidates.Add(pair.Key);
        }

        // Return every inactive fallback clone to the reusable pool.
        for (int releaseIndex = 0; releaseIndex < fallbackReleaseCandidates.Count; releaseIndex++)
        {
            Entity enemyEntity = fallbackReleaseCandidates[releaseIndex];

            if (!fallbackViewsByEnemy.TryGetValue(enemyEntity, out EnemyOffensiveEngagementBillboardView fallbackView))
            {
                continue;
            }

            cachedViewsByEnemy.Remove(enemyEntity);
            fallbackViewsByEnemy.Remove(enemyEntity);
            ReleaseFallbackView(fallbackView);
        }

        // Drop stale cached direct views so future resolves revalidate them cleanly.
        ReleaseStaleCachedViews(entityManager);
    }

    /// <summary>
    /// Destroys every active or pooled fallback clone and clears runtime caches.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void Shutdown()
    {
        // Ensure caches exist before teardown so cleanup remains null-safe.
        EnsureCollections();

        // Destroy active fallback clones first, then pooled clones, and finally clear caches.
        DestroyViews(fallbackViewsByEnemy.Values);
        fallbackViewsByEnemy.Clear();
        DestroyViews(fallbackViewPool);
        fallbackViewPool.Clear();
        cachedViewsByEnemy.Clear();
        fallbackReleaseCandidates.Clear();
        fallbackTemplateView = null;
        EnemyOffensiveEngagementBillboardMaterialUtility.DestroySharedRuntimeMaterial();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Lazily allocates the runtime caches used by the billboard resolver utility.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void EnsureCollections()
    {
        // Allocate dictionaries and pools only once to keep the hot path allocation free.
        if (cachedViewsByEnemy == null)
        {
            cachedViewsByEnemy = new Dictionary<Entity, EnemyOffensiveEngagementBillboardView>(256);
        }

        if (fallbackViewsByEnemy == null)
        {
            fallbackViewsByEnemy = new Dictionary<Entity, EnemyOffensiveEngagementBillboardView>(128);
        }

        if (fallbackViewPool == null)
        {
            fallbackViewPool = new Stack<EnemyOffensiveEngagementBillboardView>(64);
        }

        if (fallbackReleaseCandidates == null)
        {
            fallbackReleaseCandidates = new List<Entity>(128);
        }
    }

    /// <summary>
    /// Returns a scene-usable billboard view, instantiating one fallback clone when the baked ECS object is not renderable directly.
    /// /params enemyEntity Enemy entity that owns the resolved view.
    /// /params resolvedView View returned by ECS component-object lookup.
    /// /returns Scene-usable billboard view when available; otherwise null.
    /// </summary>
    private static EnemyOffensiveEngagementBillboardView ResolveRuntimeUsableView(Entity enemyEntity,
                                                                                  EnemyOffensiveEngagementBillboardView resolvedView)
    {
        // Prefer the resolved view directly when it already belongs to a live scene object.
        if (IsRuntimeUsableView(resolvedView))
        {
            EnsureViewActive(resolvedView);
            return resolvedView;
        }

        // Remember the first baked template so future fallback clones preserve authored settings and references.
        if (resolvedView != null && fallbackTemplateView == null)
        {
            fallbackTemplateView = resolvedView;
        }

        // Reuse an existing fallback clone for this enemy when one is still valid.
        if (fallbackViewsByEnemy.TryGetValue(enemyEntity, out EnemyOffensiveEngagementBillboardView fallbackView))
        {
            if (IsRuntimeUsableView(fallbackView))
            {
                EnsureViewActive(fallbackView);
                return fallbackView;
            }

            fallbackViewsByEnemy.Remove(enemyEntity);
        }

        // Acquire a pooled clone or instantiate a new one from the authored template.
        EnemyOffensiveEngagementBillboardView acquiredView = AcquireFallbackView();

        if (acquiredView == null)
        {
            return null;
        }

        acquiredView.SyncPresetSources(resolvedView);
        fallbackViewsByEnemy[enemyEntity] = acquiredView;
        EnsureViewActive(acquiredView);
        return acquiredView;
    }

    /// <summary>
    /// Acquires one fallback clone from the pool or creates a fresh runtime instance from the baked template.
    /// /params None.
    /// /returns Runtime scene clone when available; otherwise null.
    /// </summary>
    private static EnemyOffensiveEngagementBillboardView AcquireFallbackView()
    {
        // Reuse pooled clones first to avoid repeated instantiation during combat peaks.
        while (fallbackViewPool.Count > 0)
        {
            EnemyOffensiveEngagementBillboardView pooledView = fallbackViewPool.Pop();

            if (!IsRuntimeUsableView(pooledView))
            {
                continue;
            }

            EnsureViewActive(pooledView);
            return pooledView;
        }

        // Instantiate from the authored template only when no reusable clone is available.
        if (fallbackTemplateView == null)
        {
            return null;
        }

        GameObject templateObject = fallbackTemplateView.gameObject;

        if (templateObject == null)
        {
            return null;
        }

        GameObject instanceObject = Object.Instantiate(templateObject);

        if (instanceObject == null)
        {
            return null;
        }

        instanceObject.name = string.Format("{0}_RuntimeClone", templateObject.name);
        instanceObject.SetActive(true);
        return instanceObject.GetComponent<EnemyOffensiveEngagementBillboardView>();
    }

    /// <summary>
    /// Returns whether the provided billboard view currently belongs to a valid live scene object.
    /// /params billboardView Billboard view evaluated for runtime usability.
    /// /returns True when the view belongs to a live scene object; otherwise false.
    /// </summary>
    private static bool IsRuntimeUsableView(EnemyOffensiveEngagementBillboardView billboardView)
    {
        // Reject null views or detached objects first.
        if (billboardView == null)
        {
            return false;
        }

        GameObject billboardObject = billboardView.gameObject;

        if (billboardObject == null)
        {
            return false;
        }

        return billboardObject.scene.IsValid();
    }

    /// <summary>
    /// Ensures a resolved runtime billboard object is active before rendering commands are applied.
    /// /params billboardView Billboard view that should be active in the live scene.
    /// /returns None.
    /// </summary>
    private static void EnsureViewActive(EnemyOffensiveEngagementBillboardView billboardView)
    {
        // Reactivate pooled or hidden clones so the renderer can be shown again this frame.
        if (billboardView == null)
        {
            return;
        }

        GameObject billboardObject = billboardView.gameObject;

        if (billboardObject == null || billboardObject.activeSelf)
        {
            return;
        }

        billboardObject.SetActive(true);
    }

    /// <summary>
    /// Returns whether the enemy still exists and remains enabled for gameplay updates.
    /// /params entityManager Entity manager used to inspect ECS lifetime state.
    /// /params enemyEntity Enemy entity evaluated for runtime validity.
    /// /returns True when the enemy still exists and remains active; otherwise false.
    /// </summary>
    private static bool IsEnemyAlive(EntityManager entityManager, Entity enemyEntity)
    {
        // Mirror the pool semantics so fallback clones are released as soon as the owning enemy returns inactive.
        if (enemyEntity == Entity.Null ||
            !entityManager.Exists(enemyEntity) ||
            !entityManager.HasComponent<EnemyActive>(enemyEntity))
        {
            return false;
        }

        return entityManager.IsComponentEnabled<EnemyActive>(enemyEntity);
    }

    /// <summary>
    /// Resets and pools one fallback clone for later reuse.
    /// /params fallbackView Runtime billboard clone returned to the pool.
    /// /returns None.
    /// </summary>
    private static void ReleaseFallbackView(EnemyOffensiveEngagementBillboardView fallbackView)
    {
        // Hide the clone first so pooled instances never keep stale sprite or transform state.
        if (fallbackView == null)
        {
            return;
        }

        fallbackView.Hide();

        if (fallbackViewPool == null)
        {
            return;
        }

        fallbackViewPool.Push(fallbackView);
    }

    /// <summary>
    /// Removes cached views that no longer belong to valid active enemies.
    /// /params entityManager Entity manager used to inspect enemy lifetime state.
    /// /returns None.
    /// </summary>
    private static void ReleaseStaleCachedViews(EntityManager entityManager)
    {
        // Collect stale cache keys first to avoid mutating the dictionary during enumeration.
        fallbackReleaseCandidates.Clear();

        foreach (KeyValuePair<Entity, EnemyOffensiveEngagementBillboardView> pair in cachedViewsByEnemy)
        {
            if (IsEnemyAlive(entityManager, pair.Key) &&
                pair.Value != null)
            {
                continue;
            }

            fallbackReleaseCandidates.Add(pair.Key);
        }

        // Remove stale cached references so future resolves rebuild them safely.
        for (int releaseIndex = 0; releaseIndex < fallbackReleaseCandidates.Count; releaseIndex++)
        {
            cachedViewsByEnemy.Remove(fallbackReleaseCandidates[releaseIndex]);
        }
    }

    /// <summary>
    /// Destroys every view instance provided by the supplied enumerable.
    /// /params views View collection that should be destroyed during shutdown.
    /// /returns None.
    /// </summary>
    private static void DestroyViews(IEnumerable<EnemyOffensiveEngagementBillboardView> views)
    {
        // Destroy the backing GameObjects because the cloned billboards are scene objects.
        if (views == null)
        {
            return;
        }

        foreach (EnemyOffensiveEngagementBillboardView view in views)
        {
            if (view == null)
            {
                continue;
            }

            GameObject billboardObject = view.gameObject;

            if (billboardObject == null)
            {
                continue;
            }

            Object.Destroy(billboardObject);
        }
    }
    #endregion

    #endregion
}
