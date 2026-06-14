using UnityEngine;

/// <summary>
/// Validates hybrid authoring references used by PlayerAuthoringBaker.
/// </summary>
public static class PlayerAuthoringBakerValidationUtility
{
    #region Methods

    #region Public Methods
    public static bool IsInvalidProjectilePrefab(PlayerAuthoring authoring, GameObject projectilePrefabObject)
    {
        if (projectilePrefabObject == null)
            return true;

        if (projectilePrefabObject == authoring.gameObject)
            return true;

        if (projectilePrefabObject.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    public static Animator ResolveAnimatorComponent(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return null;

        Animator assignedAnimator = authoring.AnimatorComponent;

        if (IsAnimatorValidForCompanionBake(authoring, assignedAnimator))
            return assignedAnimator;

        Animator fallbackAnimator = authoring.GetComponentInChildren<Animator>(true);

        if (!IsAnimatorValidForCompanionBake(authoring, fallbackAnimator))
            return null;

#if UNITY_EDITOR
        if (assignedAnimator == null)
        {
            Debug.LogWarning(string.Format("[PlayerAuthoringBaker] AnimatorComponent missing on '{0}'. Falling back to child Animator '{1}'.",
                                           authoring.name,
                                           fallbackAnimator.name),
                             authoring);
        }
        else
        {
            Debug.LogWarning(string.Format("[PlayerAuthoringBaker] AnimatorComponent on '{0}' is not in the PlayerAuthoring hierarchy. Falling back to child Animator '{1}'.",
                                           authoring.name,
                                           fallbackAnimator.name),
                             authoring);
        }
#endif
        return fallbackAnimator;
    }

    public static bool IsAnimatorValidForCompanionBake(PlayerAuthoring authoring, Animator animator)
    {
        if (authoring == null || animator == null || animator.gameObject == null)
            return false;

        if (!animator.gameObject.scene.IsValid())
            return false;

        Transform authoringTransform = authoring.transform;
        Transform animatorTransform = animator.transform;

        if (authoringTransform == null || animatorTransform == null)
            return false;

        if (animatorTransform == authoringTransform)
            return true;

        if (animatorTransform.IsChildOf(authoringTransform))
            return true;

        return false;
    }

    public static GameObject ResolveRuntimeVisualBridgePrefab(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return null;

        GameObject assignedPrefab = authoring.RuntimeVisualBridgePrefab;

        if (assignedPrefab == null)
            return null;

        if (assignedPrefab.scene.IsValid())
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[PlayerAuthoringBaker] RuntimeVisualBridgePrefab on '{0}' points to a scene object. Assign a prefab asset on the active visual preset or hidden authoring fallback instead.",
                                           authoring.name),
                             authoring);
#endif
            return null;
        }

        if (assignedPrefab.GetComponent<PlayerAuthoring>() != null)
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[PlayerAuthoringBaker] RuntimeVisualBridgePrefab '{0}' contains PlayerAuthoring and is not valid as visual-only runtime prefab.",
                                           assignedPrefab.name),
                             authoring);
#endif
            return null;
        }

        return assignedPrefab;
    }

    public static RuntimeAnimatorController ResolveAnimatorController(Animator resolvedAnimatorComponent, PlayerAnimationBindingsPreset animationBindingsPreset)
    {
        if (resolvedAnimatorComponent != null && resolvedAnimatorComponent.runtimeAnimatorController != null)
            return resolvedAnimatorComponent.runtimeAnimatorController;

        if (animationBindingsPreset != null && animationBindingsPreset.AnimatorController != null)
            return animationBindingsPreset.AnimatorController;

        return null;
    }

    public static Avatar ResolveAnimatorAvatar(Animator resolvedAnimatorComponent)
    {
        if (resolvedAnimatorComponent == null)
            return null;

        if (resolvedAnimatorComponent.avatar != null)
            return resolvedAnimatorComponent.avatar;

        return null;
    }
    #endregion

    #endregion
}
