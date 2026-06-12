using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Suspends baked companion VFX while the runtime Visual Player bridge owns presentation, then restores authored active states.
/// </summary>
public static class PlayerManagedVisualBridgeCompanionVfxVisibilityUtility
{
    #region Fields
    private static readonly Dictionary<int, CompanionVfxActiveState> authoredActiveStates = new Dictionary<int, CompanionVfxActiveState>(8);
    #endregion

    #region Methods

    #region Visibility
    /// <summary>
    /// Applies runtime-bridge visibility ownership to companion VFX components found on one baked hierarchy entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to resolve registered companion components.</param>
    /// <param name="hierarchyEntity">Baked visual hierarchy entity to inspect.</param>
    /// <param name="hidden">True while the runtime Visual Player bridge replaces baked presentation.</param>
    public static void SetHidden(EntityManager entityManager, Entity hierarchyEntity, bool hidden)
    {
        if (entityManager.HasComponent<ParticleSystem>(hierarchyEntity))
        {
            ParticleSystem particleSystem = entityManager.GetComponentObject<ParticleSystem>(hierarchyEntity);

            if (particleSystem != null)
                SetGameObjectHidden(particleSystem.gameObject, hidden);
        }

        if (entityManager.HasComponent<TrailRenderer>(hierarchyEntity))
        {
            TrailRenderer trailRenderer = entityManager.GetComponentObject<TrailRenderer>(hierarchyEntity);

            if (trailRenderer != null)
                SetGameObjectHidden(trailRenderer.gameObject, hidden);
        }
    }

    /// <summary>
    /// Restores all cached companion VFX active states during presentation-world teardown.
    /// </summary>
    public static void RestoreAll()
    {
        Dictionary<int, CompanionVfxActiveState>.Enumerator enumerator = authoredActiveStates.GetEnumerator();

        while (enumerator.MoveNext())
            RestoreActiveState(enumerator.Current.Value);

        enumerator.Dispose();
        authoredActiveStates.Clear();
    }

    /// <summary>
    /// Hides or restores one companion VFX GameObject without changing its authored active-state contract.
    /// </summary>
    /// <param name="targetObject">Companion VFX GameObject controlled by the baked hierarchy.</param>
    /// <param name="hidden">True while the runtime Visual Player bridge replaces baked presentation.</param>
    private static void SetGameObjectHidden(GameObject targetObject, bool hidden)
    {
        if (targetObject == null)
            return;

        int instanceId = targetObject.GetInstanceID();

        if (hidden)
        {
            if (!authoredActiveStates.ContainsKey(instanceId))
            {
                authoredActiveStates.Add(instanceId, new CompanionVfxActiveState
                {
                    TargetObject = targetObject,
                    ActiveSelf = targetObject.activeSelf
                });
            }

            if (targetObject.activeSelf)
                targetObject.SetActive(false);

            return;
        }

        if (!authoredActiveStates.TryGetValue(instanceId, out CompanionVfxActiveState authoredState))
            return;

        RestoreActiveState(authoredState);
        authoredActiveStates.Remove(instanceId);
    }

    /// <summary>
    /// Restores one cached companion VFX GameObject when it still exists.
    /// </summary>
    /// <param name="authoredState">Cached GameObject and its authored active state.</param>
    private static void RestoreActiveState(CompanionVfxActiveState authoredState)
    {
        if (authoredState.TargetObject != null &&
            authoredState.TargetObject.activeSelf != authoredState.ActiveSelf)
            authoredState.TargetObject.SetActive(authoredState.ActiveSelf);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores one companion VFX GameObject active state before runtime bridge presentation replaces it.
    /// </summary>
    private struct CompanionVfxActiveState
    {
        public GameObject TargetObject;
        public bool ActiveSelf;
    }
    #endregion
}
