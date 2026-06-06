using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Stores prefab-relative weapon visual references, the default attachment, and the Base Gun shooting clip.
/// Reference selectors accept either an exact hierarchy path or a unique GameObject name inside the runtime visual bridge.
/// </summary>
[Serializable]
public sealed class PlayerWeaponVisualSettings
{
    #region Constants
    public const int MaximumReferenceSelectorUtf8Bytes = 125;
    public const string DefaultBaseGunSelector = "base gun";
    public const string DefaultCannonSelector = "cannon";
    public const string DefaultGatlingSelector = "gatling";
    public const string DefaultRailgunSelector = "railgun";
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Prefab-relative path or unique GameObject name resolving the Base Gun mesh object inside the runtime visual bridge.")]
    [SerializeField]
    private string baseGunReference = DefaultBaseGunSelector;

    [Tooltip("Prefab-relative path or unique GameObject name resolving the Cannon mesh object inside the runtime visual bridge.")]
    [SerializeField]
    private string cannonReference = DefaultCannonSelector;

    [Tooltip("Prefab-relative path or unique GameObject name resolving the Gatling mesh object inside the runtime visual bridge.")]
    [SerializeField]
    private string gatlingReference = DefaultGatlingSelector;

    [Tooltip("Prefab-relative path or unique GameObject name resolving the Railgun mesh object inside the runtime visual bridge.")]
    [SerializeField]
    private string railgunReference = DefaultRailgunSelector;

    [Tooltip("Optional Cannon, Gatling, or Railgun attachment shown alongside the always-visible Base Gun while no equipped power-up owns Switch Weapon. None keeps only Base Gun visible.")]
    [SerializeField]
    private PlayerWeaponVisualSlot defaultAdditionalWeaponVisual = PlayerWeaponVisualSlot.None;

    [Tooltip("Upper-body shooting clip used by the Base Gun and as fallback when an equipped Switch Weapon shooting-animation slot is empty.")]
    [SerializeField]
    private AnimationClip defaultShootAnimationClip;
    #endregion

    #endregion

    #region Properties
    public string BaseGunReference
    {
        get
        {
            return baseGunReference;
        }
    }

    public string CannonReference
    {
        get
        {
            return cannonReference;
        }
    }

    public string GatlingReference
    {
        get
        {
            return gatlingReference;
        }
    }

    public string RailgunReference
    {
        get
        {
            return railgunReference;
        }
    }

    public PlayerWeaponVisualSlot DefaultAdditionalWeaponVisual
    {
        get
        {
            return defaultAdditionalWeaponVisual;
        }
    }

    public AnimationClip DefaultShootAnimationClip
    {
        get
        {
            return defaultShootAnimationClip;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid weapon reference selectors without modifying designer-authored values.
    /// </summary>
    /// <param name="runtimeVisualBridgePrefab">Runtime visual bridge prefab used to resolve selectors.</param>
    /// <param name="ownerAssetName">Visual preset asset name included in warnings.</param>
    public void Validate(GameObject runtimeVisualBridgePrefab, string ownerAssetName)
    {
        ValidateSelector(baseGunReference, "Base Gun", runtimeVisualBridgePrefab, ownerAssetName);
        ValidateSelector(cannonReference, "Cannon", runtimeVisualBridgePrefab, ownerAssetName);
        ValidateSelector(gatlingReference, "Gatling", runtimeVisualBridgePrefab, ownerAssetName);
        ValidateSelector(railgunReference, "Railgun", runtimeVisualBridgePrefab, ownerAssetName);

        int defaultWeaponValue = (int)defaultAdditionalWeaponVisual;

        if (defaultWeaponValue < (int)PlayerWeaponVisualSlot.None ||
            defaultWeaponValue > (int)PlayerWeaponVisualSlot.Railgun)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: Default Additional Weapon Visual uses unsupported value {1}.",
                                           ownerAssetName,
                                           defaultWeaponValue));
        }

        if (HasDuplicateSelectors())
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: two or more weapon slots use the same reference selector.",
                                           ownerAssetName));
        }

        if (defaultShootAnimationClip == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: Default Shoot Animation Clip is missing.",
                                           ownerAssetName));
        }
    }

    /// <summary>
    /// Reports one empty, oversized, or unresolved weapon reference selector.
    /// </summary>
    /// <param name="selector">Authored prefab-relative path or unique object name.</param>
    /// <param name="slotLabel">Human-readable weapon slot label.</param>
    /// <param name="runtimeVisualBridgePrefab">Runtime visual bridge prefab used to resolve the selector.</param>
    /// <param name="ownerAssetName">Visual preset asset name included in warnings.</param>
    private static void ValidateSelector(string selector,
                                         string slotLabel,
                                         GameObject runtimeVisualBridgePrefab,
                                         string ownerAssetName)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: {1} reference is empty.",
                                           ownerAssetName,
                                           slotLabel));
            return;
        }

        if (Encoding.UTF8.GetByteCount(selector) > MaximumReferenceSelectorUtf8Bytes)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: {1} reference exceeds {2} UTF-8 bytes and cannot be baked into ECS.",
                                           ownerAssetName,
                                           slotLabel,
                                           MaximumReferenceSelectorUtf8Bytes));
            return;
        }

        if (runtimeVisualBridgePrefab != null &&
            !PlayerWeaponVisualReferenceUtility.TryResolve(runtimeVisualBridgePrefab.transform, selector, out Transform _))
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: {1} reference '{2}' does not resolve inside runtime visual bridge prefab '{3}'.",
                                           ownerAssetName,
                                           slotLabel,
                                           selector,
                                           runtimeVisualBridgePrefab.name));
        }
    }

    /// <summary>
    /// Checks whether two or more authored selectors address the same path or object name.
    /// </summary>
    /// <returns>True when at least one non-empty selector is duplicated.</returns>
    private bool HasDuplicateSelectors()
    {
        string[] selectors =
        {
            baseGunReference,
            cannonReference,
            gatlingReference,
            railgunReference
        };

        for (int leftIndex = 0; leftIndex < selectors.Length; leftIndex++)
        {
            if (string.IsNullOrWhiteSpace(selectors[leftIndex]))
                continue;

            for (int rightIndex = leftIndex + 1; rightIndex < selectors.Length; rightIndex++)
            {
                if (string.Equals(selectors[leftIndex], selectors[rightIndex], StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }
    #endregion

    #endregion
}

/// <summary>
/// Resolves scalable prefab-relative weapon selectors without runtime reflection.
/// </summary>
public static class PlayerWeaponVisualReferenceUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves an exact hierarchy path first, then falls back to a recursive exact-name lookup.
    /// </summary>
    /// <param name="root">Runtime visual bridge root.</param>
    /// <param name="selector">Prefab-relative path or unique GameObject name.</param>
    /// <param name="resolvedTransform">Resolved transform when found.</param>
    /// <returns>True when the selector resolves inside the supplied root.</returns>
    public static bool TryResolve(Transform root, string selector, out Transform resolvedTransform)
    {
        resolvedTransform = null;

        if (root == null || string.IsNullOrWhiteSpace(selector))
            return false;

        string normalizedSelector = selector.Trim();
        resolvedTransform = root.Find(normalizedSelector);

        if (resolvedTransform != null)
            return true;

        resolvedTransform = FindChildRecursive(root, normalizedSelector);
        return resolvedTransform != null;
    }

    /// <summary>
    /// Builds a stable hierarchy path from one runtime visual bridge root to a selected child object.
    /// </summary>
    /// <param name="root">Runtime visual bridge root.</param>
    /// <param name="target">Selected child transform.</param>
    /// <param name="relativePath">Resolved prefab-relative path.</param>
    /// <returns>True when the target belongs to the supplied root.</returns>
    public static bool TryBuildRelativePath(Transform root, Transform target, out string relativePath)
    {
        relativePath = string.Empty;

        if (root == null || target == null || target == root)
            return false;

        Transform current = target;
        string path = target.name;

        while (current.parent != null && current.parent != root)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        if (current.parent != root)
            return false;

        relativePath = path;
        return true;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds the first child whose name exactly matches the supplied selector.
    /// </summary>
    /// <param name="root">Current hierarchy root.</param>
    /// <param name="targetName">Exact target object name.</param>
    /// <returns>Matching transform or null when no child matches.</returns>
    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (string.Equals(root.name, targetName, StringComparison.Ordinal))
            return root;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform resolvedTransform = FindChildRecursive(root.GetChild(childIndex), targetName);

            if (resolvedTransform != null)
                return resolvedTransform;
        }

        return null;
    }
    #endregion

    #endregion
}
