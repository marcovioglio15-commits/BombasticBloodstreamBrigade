using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#region Mountable Weapon Entry
/// <summary>
/// Authoring data for one mountable weapon attachment shown alongside Base Gun. Each entry is identified by a
/// defined string ID that is also referenced by the Default Additional Weapon ID on the visual preset
/// and by every Switch Weapon module that wants to swap to this mountable weapon at runtime.
/// </summary>
[Serializable]
public sealed class PlayerAdditionalWeaponVisualEntry
{
    #region Fields

    #region Serialized Fields
    [Tooltip("defined weapon identifier. Referenced by the visual preset Default Additional Weapon Id and by Switch Weapon modules. Must be unique inside the array and stay within the ECS FixedString64 capacity.")]
    [SerializeField]
    private string weaponId = string.Empty;

    [Tooltip("Prefab-relative path or unique GameObject name resolving this mountable weapon mesh inside the runtime visual bridge.")]
    [SerializeField]
    private string runtimeReference = string.Empty;

    [Tooltip("Upper-body shooting clip played while this mountable weapon is the visible attachment. Used as the default-shot animation when this entry matches the visual preset Default Additional Weapon Id.")]
    [SerializeField]
    private AnimationClip shootAnimationClip;
    #endregion

    #endregion

    #region Properties
    public string WeaponId
    {
        get
        {
            return weaponId;
        }
    }

    public string RuntimeReference
    {
        get
        {
            return runtimeReference;
        }
    }

    public AnimationClip ShootAnimationClip
    {
        get
        {
            return shootAnimationClip;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns defined weapon ID, runtime selector, and the matching upper-body shooting clip in one
    /// call. Used by editor helpers and the defaults utility when creating a pre-populated entry.
    /// </summary>
    /// <param name="weaponIdValue">defined weapon identifier.</param>
    /// <param name="runtimeReferenceValue">Prefab-relative selector resolving the weapon mesh.</param>
    /// <param name="shootAnimationClipValue">Upper-body shooting clip played while the entry is visible.</param>
    public void Configure(string weaponIdValue,
                          string runtimeReferenceValue,
                          AnimationClip shootAnimationClipValue)
    {
        weaponId = weaponIdValue;
        runtimeReference = runtimeReferenceValue;
        shootAnimationClip = shootAnimationClipValue;
    }
    #endregion

    #endregion
}
#endregion

#region Weapon Visual Settings
/// <summary>
/// Stores Base Gun authoring data and the array of mountable weapon attachments. Base Gun is permanently visible
/// and carries no shoot animation; mountable weapons are mutually exclusive and contribute their own shooting
/// clip. The Default Additional Weapon ID selects which array entry remains active while no equipped Switch
/// Weapon module owns the visual, and the bound shoot clip becomes the implicit Base Gun shooting clip.
/// </summary>
[Serializable]
public sealed class PlayerWeaponVisualSettings
{
    #region Constants
    public const int MaximumReferenceSelectorUtf8Bytes = 125;
    public const int MaximumWeaponIdUtf8Bytes = 60;
    public const string DefaultBaseGunSelector = "base gun";
    #endregion

    #region Fields

    #region Serialized Fields

    #region Base Gun
    [Tooltip("Prefab-relative path or unique GameObject name resolving the Base Gun mesh. Always kept active beside the optional mountable weapon and never plays a shooting animation of its own.")]
    [SerializeField]
    private string baseGunReference = DefaultBaseGunSelector;
    #endregion

    #region Mountable Weapons
    [Tooltip("Array of mountable weapon attachments. At most one entry is visible at runtime alongside Base Gun: the default below, or the entry whose ID is owned by an equipped Switch Weapon module. Each entry carries its own runtime reference and shooting animation.")]
    [SerializeField]
    private List<PlayerAdditionalWeaponVisualEntry> additionalWeapons = new List<PlayerAdditionalWeaponVisualEntry>();

    [Tooltip("defined weapon ID of the mountable attachment shown by default alongside Base Gun while no equipped power-up owns Switch Weapon. Must match one Weapon Id from the array above. An empty value keeps only Base Gun visible.")]
    [SerializeField]
    private string defaultAdditionalWeaponId = string.Empty;
    #endregion

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

    public IReadOnlyList<PlayerAdditionalWeaponVisualEntry> AdditionalWeapons
    {
        get
        {
            return additionalWeapons;
        }
    }

    public string DefaultAdditionalWeaponId
    {
        get
        {
            return defaultAdditionalWeaponId;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the prefab-relative selector authored for one weapon ID. Returns an empty string when no entry
    /// owns the ID. Used by bake to populate ECS reference fields and by the runtime visual bridge.
    /// </summary>
    /// <param name="weaponId">defined weapon ID to query.</param>
    /// <returns>Authored selector string, or empty when no matching entry exists.</returns>
    public string ResolveRuntimeReference(string weaponId)
    {
        PlayerAdditionalWeaponVisualEntry entry = ResolveEntry(weaponId);
        return entry != null ? entry.RuntimeReference : string.Empty;
    }

    /// <summary>
    /// Resolves the upper-body shooting clip authored for one weapon ID. Returns null when no entry owns the ID
    /// or the entry has no clip. Used by bake to derive the implicit default shoot clip and to populate the
    /// runtime additional-weapons buffer consumed by upper-body presentation.
    /// </summary>
    /// <param name="weaponId">defined weapon ID to query.</param>
    /// <returns>Authored shooting clip, or null when no matching entry exists.</returns>
    public AnimationClip ResolveShootClip(string weaponId)
    {
        PlayerAdditionalWeaponVisualEntry entry = ResolveEntry(weaponId);
        return entry != null ? entry.ShootAnimationClip : null;
    }

    /// <summary>
    /// Resolves the mountable entry stored for the supplied weapon ID, or null when the ID is empty or unknown.
    /// Comparison is ordinal and trims authored whitespace so s can format the field freely.
    /// </summary>
    /// <param name="weaponId">defined weapon ID to look up.</param>
    /// <returns>Authored entry matching the ID, or null when not present.</returns>
    public PlayerAdditionalWeaponVisualEntry ResolveEntry(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId) || additionalWeapons == null)
            return null;

        string normalizedId = weaponId.Trim();

        for (int entryIndex = 0; entryIndex < additionalWeapons.Count; entryIndex++)
        {
            PlayerAdditionalWeaponVisualEntry entry = additionalWeapons[entryIndex];

            if (entry == null || string.IsNullOrWhiteSpace(entry.WeaponId))
                continue;

            if (string.Equals(entry.WeaponId.Trim(), normalizedId, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Reports invalid Base Gun selector, malformed mountable entries, duplicate IDs/selectors, oversized
    /// references, unresolved selectors, and incoherent default-attachment selections without mutating data.
    /// </summary>
    /// <param name="runtimeVisualBridgePrefab">Runtime visual bridge prefab used to resolve selectors.</param>
    /// <param name="ownerAssetName">Visual preset asset name included in warnings.</param>
    public void Validate(GameObject runtimeVisualBridgePrefab, string ownerAssetName)
    {
        ValidateSelector(baseGunReference, "Base Gun", runtimeVisualBridgePrefab, ownerAssetName);

        if (additionalWeapons == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: mountable weapons collection is missing.",
                                           ownerAssetName));
            return;
        }

        HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> seenSelectors = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(baseGunReference))
            seenSelectors.Add(baseGunReference.Trim());

        for (int entryIndex = 0; entryIndex < additionalWeapons.Count; entryIndex++)
        {
            PlayerAdditionalWeaponVisualEntry entry = additionalWeapons[entryIndex];

            if (entry == null)
            {
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: mountable weapon entry at index {1} is null.",
                                               ownerAssetName,
                                               entryIndex));
                continue;
            }

            string entryLabel = BuildEntryLabel(entry.WeaponId, entryIndex);

            // Per-entry validation: weapon ID + runtime reference + animation clip.
            ValidateWeaponId(entry.WeaponId, entryLabel, seenIds, ownerAssetName);
            ValidateSelector(entry.RuntimeReference, entryLabel, runtimeVisualBridgePrefab, ownerAssetName);

            if (!string.IsNullOrWhiteSpace(entry.RuntimeReference) &&
                !seenSelectors.Add(entry.RuntimeReference.Trim()))
            {
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: {1} reference '{2}' is duplicated across multiple entries.",
                                               ownerAssetName,
                                               entryLabel,
                                               entry.RuntimeReference));
            }

            if (entry.ShootAnimationClip == null)
            {
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: {1} has no shoot animation clip assigned.",
                                               ownerAssetName,
                                               entryLabel));
            }
        }

        ValidateDefaultAdditionalWeapon(ownerAssetName);
    }

    /// <summary>
    /// Reports one empty, oversized, or unresolved selector without mutating the authored value. Shared between
    /// Base Gun and each mountable entry to keep messages consistent.
    /// </summary>
    /// <param name="selector">Authored prefab-relative path or unique object name.</param>
    /// <param name="slotLabel"> label included in the warning text.</param>
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
    /// Reports empty, oversized, or duplicated weapon IDs without mutating authored data. Empty IDs
    /// prevent the entry from being referenced by the default attachment or by any Switch Weapon module.
    /// </summary>
    /// <param name="weaponId">Authored weapon identifier.</param>
    /// <param name="entryLabel">Compact entry label included in the warning text.</param>
    /// <param name="seenIds">Set used to detect cross-entry duplicates.</param>
    /// <param name="ownerAssetName">Visual preset asset name included in warnings.</param>
    private static void ValidateWeaponId(string weaponId,
                                          string entryLabel,
                                          HashSet<string> seenIds,
                                          string ownerAssetName)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: {1} has no Weapon Id; default attachment and Switch Weapon modules cannot reference it.",
                                           ownerAssetName,
                                           entryLabel));
            return;
        }

        string normalizedId = weaponId.Trim();

        if (Encoding.UTF8.GetByteCount(normalizedId) > MaximumWeaponIdUtf8Bytes)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: {1} Weapon Id exceeds {2} UTF-8 bytes and cannot be baked into ECS.",
                                           ownerAssetName,
                                           entryLabel,
                                           MaximumWeaponIdUtf8Bytes));
            return;
        }

        if (!seenIds.Add(normalizedId))
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: Weapon Id '{1}' is duplicated across multiple entries.",
                                           ownerAssetName,
                                           normalizedId));
        }
    }

    /// <summary>
    /// Validates the Default Additional Weapon Id against the authored entries. Reports unknown IDs and a
    /// missing shoot animation clip on the matching entry so s can fix incoherent setups.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name included in warnings.</param>
    private void ValidateDefaultAdditionalWeapon(string ownerAssetName)
    {
        if (string.IsNullOrWhiteSpace(defaultAdditionalWeaponId))
            return;

        string normalizedId = defaultAdditionalWeaponId.Trim();

        if (Encoding.UTF8.GetByteCount(normalizedId) > MaximumWeaponIdUtf8Bytes)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: Default Additional Weapon Id exceeds {1} UTF-8 bytes and cannot be baked into ECS.",
                                           ownerAssetName,
                                           MaximumWeaponIdUtf8Bytes));
            return;
        }

        PlayerAdditionalWeaponVisualEntry defaultEntry = ResolveEntry(normalizedId);

        if (defaultEntry == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: Default Additional Weapon Id '{1}' does not match any authored mountable entry.",
                                           ownerAssetName,
                                           normalizedId));
            return;
        }

        if (defaultEntry.ShootAnimationClip == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Weapon Visuals: Default Additional Weapon Id '{1}' resolves but its entry has no shoot animation clip; the implicit Base Gun shoot clip stays unassigned.",
                                           ownerAssetName,
                                           normalizedId));
        }
    }

    /// <summary>
    /// Builds a stable label used by validation and editor warnings for one entry. Falls back to a numeric
    /// label when the weapon ID is empty so error messages remain readable while s are authoring.
    /// </summary>
    /// <param name="weaponId">Weapon ID authored on the entry.</param>
    /// <param name="entryIndex">Authored array index of the entry.</param>
    /// <returns>Compact label suitable for warning logs and HelpBoxes.</returns>
    public static string BuildEntryLabel(string weaponId, int entryIndex)
    {
        return string.IsNullOrWhiteSpace(weaponId)
            ? string.Format("Mountable Weapon [{0}]", entryIndex)
            : string.Format("'{0}'", weaponId.Trim());
    }
    #endregion

    #endregion
}
#endregion

#region Reference Resolution Utility
/// <summary>
/// Resolves scalable prefab-relative weapon selectors without runtime reflection. Shared by the visual preset
/// validation, the editor warnings, and the runtime <see cref="PlayerWeaponVisualSet"/> presentation.
/// </summary>
public static class PlayerWeaponVisualReferenceUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves an exact hierarchy path first, then falls back to a recursive exact-name lookup. Returns false
    /// without allocating when no child matches.
    /// </summary>
    /// <param name="root">Runtime visual bridge root used as resolution origin.</param>
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
    /// Builds a stable hierarchy path from one runtime visual bridge root to a selected child object. Editor
    /// object pickers use the path as a scalable selector without storing direct scene-object references.
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
    /// Finds the first child whose name exactly matches the supplied selector. Walks the hierarchy depth-first.
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
#endregion
