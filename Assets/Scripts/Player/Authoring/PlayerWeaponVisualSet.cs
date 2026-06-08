using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves designer-defined weapon visual IDs against the runtime mountable-weapons buffer. Base Gun remains
/// visible while at most one valid default or Switch Weapon attachment is shown.
/// </summary>
public sealed class PlayerWeaponVisualSet : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Weapon Mesh Fallbacks")]
    [Tooltip("Base Gun mesh fallback used before ECS applies the active Player Visual Preset or when its scalable runtime reference cannot be resolved.")]
    [SerializeField]
    private GameObject baseGun;
    #endregion

    #region Runtime State
    private readonly List<ResolvedWeaponVisual> resolvedAdditionalWeapons = new List<ResolvedWeaponVisual>(4);
    private GameObject resolvedBaseGun;
    private FixedString128Bytes appliedBaseGunReference;
    private FixedString64Bytes appliedDefaultAdditionalWeaponId;
    private FixedString64Bytes appliedAdditionalWeaponId;
    private uint appliedRevision;
    private byte hasAppliedConfiguration;
    #endregion

    #endregion

    #region Properties
    public bool HasBaseGunFallback
    {
        get
        {
            return baseGun != null;
        }
    }
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Shows only the serialized Base Gun fallback until ECS presentation supplies the active weapon table.
    /// </summary>
    private void Awake()
    {
        resolvedBaseGun = baseGun;
        ApplyVisibility(default);
    }

    /// <summary>
    /// Invalidates cached runtime configuration after the visual hierarchy is enabled.
    /// </summary>
    private void OnEnable()
    {
        ResetAppliedState();
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Assigns the generated Base Gun fallback and invalidates runtime presentation caches.
    /// </summary>
    /// <param name="baseGunValue">Generated Base Gun mesh object.</param>
    public void Configure(GameObject baseGunValue)
    {
        HideResolvedWeaponVisuals();
        baseGun = baseGunValue;
        resolvedBaseGun = baseGun;
        ApplyVisibility(default);
        ResetAppliedState();
    }
    #endregion

    #region Runtime Application
    /// <summary>
    /// Keeps Base Gun visible and applies the configured default attachment or one equipped Switch Weapon
    /// replacement. Prefab selectors are resolved only when the scalable weapon-visual revision changes.
    /// </summary>
    /// <param name="visualConfig">Current ECS runtime visual bridge configuration.</param>
    /// <param name="additionalWeapons">Current ECS mountable-weapons table.</param>
    /// <param name="weaponVisualRevision">Revision derived from runtime weapon-visual scaling state.</param>
    /// <param name="hasWeaponSwitch">Whether an equipped Switch Weapon module currently owns the visual.</param>
    /// <param name="weaponId">Designer-defined Weapon Id requested by Switch Weapon.</param>
    public void Apply(in PlayerVisualRuntimeBridgeConfig visualConfig,
                      in DynamicBuffer<PlayerAdditionalWeaponVisualElement> additionalWeapons,
                      uint weaponVisualRevision,
                      bool hasWeaponSwitch,
                      FixedString64Bytes weaponId)
    {
        EnsureRuntimeConfiguration(in visualConfig, in additionalWeapons, weaponVisualRevision);
        FixedString64Bytes desiredWeaponId = ResolveDesiredWeaponId(in visualConfig,
                                                                    hasWeaponSwitch,
                                                                    weaponId);

        if (appliedAdditionalWeaponId.Equals(desiredWeaponId) &&
            (resolvedBaseGun == null || resolvedBaseGun.activeSelf))
            return;

        ApplyVisibility(desiredWeaponId);
        appliedAdditionalWeaponId = desiredWeaponId;
    }
    #endregion

    #region Reference Resolution
    /// <summary>
    /// Rebuilds resolved GameObject references when scalable configuration changes.
    /// </summary>
    /// <param name="visualConfig">Current ECS runtime visual bridge configuration.</param>
    /// <param name="additionalWeapons">Current ECS mountable-weapons table.</param>
    /// <param name="weaponVisualRevision">Revision derived from runtime weapon-visual scaling state.</param>
    private void EnsureRuntimeConfiguration(in PlayerVisualRuntimeBridgeConfig visualConfig,
                                            in DynamicBuffer<PlayerAdditionalWeaponVisualElement> additionalWeapons,
                                            uint weaponVisualRevision)
    {
        if (hasAppliedConfiguration != 0 &&
            appliedRevision == weaponVisualRevision &&
            appliedBaseGunReference.Equals(visualConfig.BaseGunReference) &&
            appliedDefaultAdditionalWeaponId.Equals(visualConfig.DefaultAdditionalWeaponId))
            return;

        HideResolvedWeaponVisuals();
        resolvedAdditionalWeapons.Clear();
        resolvedBaseGun = ResolveReference(visualConfig.BaseGunReference, baseGun);

        if (additionalWeapons.IsCreated)
        {
            for (int entryIndex = 0; entryIndex < additionalWeapons.Length; entryIndex++)
            {
                PlayerAdditionalWeaponVisualElement entry = additionalWeapons[entryIndex];

                if (entry.WeaponId.Length <= 0)
                    continue;

                GameObject resolvedWeapon = ResolveReference(entry.RuntimeReference, null);

                if (resolvedWeapon == null)
                    continue;

                resolvedAdditionalWeapons.Add(new ResolvedWeaponVisual
                {
                    WeaponId = entry.WeaponId,
                    Target = resolvedWeapon
                });
            }
        }

        appliedBaseGunReference = visualConfig.BaseGunReference;
        appliedDefaultAdditionalWeaponId = visualConfig.DefaultAdditionalWeaponId;
        appliedRevision = weaponVisualRevision;
        hasAppliedConfiguration = 1;
        appliedAdditionalWeaponId = default;
    }

    /// <summary>
    /// Resolves one prefab-relative selector and returns its optional serialized fallback when unresolved.
    /// </summary>
    /// <param name="reference">Prefab-relative path or unique GameObject name.</param>
    /// <param name="fallback">Optional serialized fallback mesh object.</param>
    /// <returns>Resolved runtime mesh object or the supplied fallback.</returns>
    private GameObject ResolveReference(FixedString128Bytes reference, GameObject fallback)
    {
        if (PlayerWeaponVisualReferenceUtility.TryResolve(transform, reference.ToString(), out Transform resolvedTransform))
            return resolvedTransform.gameObject;

        return fallback;
    }

    /// <summary>
    /// Chooses the equipped Switch Weapon ID when it resolves, otherwise the scalable preset default.
    /// </summary>
    /// <param name="visualConfig">Current ECS runtime visual bridge configuration.</param>
    /// <param name="hasWeaponSwitch">Whether an equipped Switch Weapon module currently owns the visual.</param>
    /// <param name="weaponId">Designer-defined Weapon Id requested by Switch Weapon.</param>
    /// <returns>Available attachment ID, or an empty ID when only Base Gun should remain visible.</returns>
    private FixedString64Bytes ResolveDesiredWeaponId(in PlayerVisualRuntimeBridgeConfig visualConfig,
                                                      bool hasWeaponSwitch,
                                                      FixedString64Bytes weaponId)
    {
        if (hasWeaponSwitch && ContainsWeaponId(weaponId))
            return weaponId;

        if (ContainsWeaponId(visualConfig.DefaultAdditionalWeaponId))
            return visualConfig.DefaultAdditionalWeaponId;

        return default;
    }

    /// <summary>
    /// Checks whether one designer-defined ID resolves to a mountable weapon GameObject.
    /// </summary>
    /// <param name="weaponId">Runtime Weapon Id to inspect.</param>
    /// <returns>True when one resolved mountable weapon owns the ID.</returns>
    private bool ContainsWeaponId(FixedString64Bytes weaponId)
    {
        if (weaponId.Length <= 0)
            return false;

        for (int entryIndex = 0; entryIndex < resolvedAdditionalWeapons.Count; entryIndex++)
        {
            if (resolvedAdditionalWeapons[entryIndex].WeaponId.Equals(weaponId))
                return true;
        }

        return false;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Keeps Base Gun visible and displays only the first resolved attachment matching the requested ID.
    /// </summary>
    /// <param name="additionalWeaponId">Optional mountable Weapon Id that should remain visible.</param>
    private void ApplyVisibility(FixedString64Bytes additionalWeaponId)
    {
        bool matchedWeapon = false;

        for (int entryIndex = 0; entryIndex < resolvedAdditionalWeapons.Count; entryIndex++)
        {
            ResolvedWeaponVisual entry = resolvedAdditionalWeapons[entryIndex];
            bool shouldShow = !matchedWeapon &&
                              additionalWeaponId.Length > 0 &&
                              entry.WeaponId.Equals(additionalWeaponId);
            SetActive(entry.Target, shouldShow);
            matchedWeapon |= shouldShow;
        }

        SetActive(resolvedBaseGun, true);
    }

    /// <summary>
    /// Hides previously resolved references before a configuration change so scalable selectors cannot leave
    /// stale meshes visible.
    /// </summary>
    private void HideResolvedWeaponVisuals()
    {
        for (int entryIndex = 0; entryIndex < resolvedAdditionalWeapons.Count; entryIndex++)
            SetActive(resolvedAdditionalWeapons[entryIndex].Target, false);

        SetActive(resolvedBaseGun, false);
    }

    /// <summary>
    /// Applies one active state only when the optional target requires a change.
    /// </summary>
    /// <param name="target">Optional mesh object to update.</param>
    /// <param name="isActive">Desired active state.</param>
    private static void SetActive(GameObject target, bool isActive)
    {
        if (target != null && target.activeSelf != isActive)
            target.SetActive(isActive);
    }
    #endregion

    #region Cache
    /// <summary>
    /// Clears runtime application caches so the next ECS presentation update reapplies the active visual
    /// configuration.
    /// </summary>
    private void ResetAppliedState()
    {
        appliedAdditionalWeaponId = default;
        hasAppliedConfiguration = 0;
    }
    #endregion

    #endregion

    #region Nested Types
    private struct ResolvedWeaponVisual
    {
        public FixedString64Bytes WeaponId;
        public GameObject Target;
    }
    #endregion
}
