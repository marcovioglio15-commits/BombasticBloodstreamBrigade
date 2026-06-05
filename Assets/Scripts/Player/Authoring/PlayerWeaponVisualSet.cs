using Unity.Collections;
using UnityEngine;

/// <summary>
/// Resolves the four player weapon mesh objects once per runtime visual configuration change. Base Gun remains visible,
/// while the configured default or Switch Weapon selects at most one Cannon, Gatling, or Railgun attachment.
/// </summary>
public sealed class PlayerWeaponVisualSet : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Weapon Mesh Fallbacks")]
    [Tooltip("Base Gun mesh fallback used when the active Player Visual Preset reference cannot be resolved.")]
    [SerializeField]
    private GameObject baseGun;

    [Tooltip("Cannon mesh fallback used when the active Player Visual Preset reference cannot be resolved.")]
    [SerializeField]
    private GameObject cannon;

    [Tooltip("Gatling mesh fallback used when the active Player Visual Preset reference cannot be resolved.")]
    [SerializeField]
    private GameObject gatling;

    [Tooltip("Railgun mesh fallback used when the active Player Visual Preset reference cannot be resolved.")]
    [SerializeField]
    private GameObject railgun;

    [Header("Fallback Default")]
    [Tooltip("Optional weapon attachment shown alongside Base Gun before ECS applies the active Player Visual Preset configuration.")]
    [SerializeField]
    private PlayerWeaponVisualSlot fallbackDefaultAdditionalWeaponVisual = PlayerWeaponVisualSlot.None;
    #endregion

    #region Runtime State
    private GameObject resolvedBaseGun;
    private GameObject resolvedCannon;
    private GameObject resolvedGatling;
    private GameObject resolvedRailgun;
    private FixedString128Bytes appliedBaseGunReference;
    private FixedString128Bytes appliedCannonReference;
    private FixedString128Bytes appliedGatlingReference;
    private FixedString128Bytes appliedRailgunReference;
    private PlayerWeaponVisualSlot appliedDefaultAdditionalWeaponVisual;
    private PlayerWeaponVisualSlot appliedAdditionalSlot;
    private byte hasAppliedConfiguration;
    #endregion

    #endregion

    #region Properties
    public bool HasCompleteWeaponSet
    {
        get
        {
            return baseGun != null && cannon != null && gatling != null && railgun != null;
        }
    }
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Keeps Base Gun visible and applies the serialized fallback attachment before ECS presentation provides configuration.
    /// </summary>
    private void Awake()
    {
        ResolveFallbackReferences();
        ShowBaseGunAndOnlyAdditional(ResolveAvailableAdditionalSlot(fallbackDefaultAdditionalWeaponVisual));
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
    /// Assigns generated prefab fallback references and invalidates runtime presentation caches.
    /// </summary>
    /// <param name="baseGunValue">Generated Base Gun mesh object.</param>
    /// <param name="cannonValue">Generated Cannon mesh object.</param>
    /// <param name="gatlingValue">Generated Gatling mesh object.</param>
    /// <param name="railgunValue">Generated Railgun mesh object.</param>
    public void Configure(GameObject baseGunValue,
                          GameObject cannonValue,
                          GameObject gatlingValue,
                          GameObject railgunValue)
    {
        HideResolvedWeaponVisuals();
        baseGun = baseGunValue;
        cannon = cannonValue;
        gatling = gatlingValue;
        railgun = railgunValue;
        ResolveFallbackReferences();
        ShowBaseGunAndOnlyAdditional(ResolveAvailableAdditionalSlot(fallbackDefaultAdditionalWeaponVisual));
        ResetAppliedState();
    }
    #endregion

    #region Runtime Application
    /// <summary>
    /// Keeps Base Gun visible and applies the configured default attachment or one equipped Switch Weapon replacement.
    /// Reference selectors are resolved only when the ECS visual configuration changes.
    /// </summary>
    /// <param name="visualConfig">Current ECS runtime visual bridge configuration.</param>
    /// <param name="hasWeaponSwitch">Whether an equipped Switch Weapon module currently owns the visual.</param>
    /// <param name="weaponVisualSlot">Requested Cannon, Gatling, or Railgun replacement.</param>
    public void Apply(in PlayerVisualRuntimeBridgeConfig visualConfig,
                      bool hasWeaponSwitch,
                      PlayerWeaponVisualSlot weaponVisualSlot)
    {
        EnsureRuntimeConfiguration(in visualConfig);
        bool useSwitchWeapon = hasWeaponSwitch && IsAlternateSlot(weaponVisualSlot) && IsSlotAvailable(weaponVisualSlot);
        PlayerWeaponVisualSlot desiredAdditionalSlot = useSwitchWeapon
            ? weaponVisualSlot
            : ResolveAvailableAdditionalSlot(visualConfig.DefaultAdditionalWeaponVisual);

        if (appliedAdditionalSlot == desiredAdditionalSlot &&
            (resolvedBaseGun == null || resolvedBaseGun.activeSelf))
        {
            return;
        }

        ShowBaseGunAndOnlyAdditional(desiredAdditionalSlot);
        appliedAdditionalSlot = desiredAdditionalSlot;
    }
    #endregion

    #region Reference Resolution
    /// <summary>
    /// Resolves changed prefab-relative selectors and keeps serialized component references as deterministic fallbacks.
    /// </summary>
    /// <param name="visualConfig">Current ECS runtime visual bridge configuration.</param>
    private void EnsureRuntimeConfiguration(in PlayerVisualRuntimeBridgeConfig visualConfig)
    {
        if (hasAppliedConfiguration != 0 &&
            appliedBaseGunReference == visualConfig.BaseGunReference &&
            appliedCannonReference == visualConfig.CannonReference &&
            appliedGatlingReference == visualConfig.GatlingReference &&
            appliedRailgunReference == visualConfig.RailgunReference &&
            appliedDefaultAdditionalWeaponVisual == visualConfig.DefaultAdditionalWeaponVisual)
        {
            return;
        }

        HideResolvedWeaponVisuals();
        resolvedBaseGun = ResolveReference(visualConfig.BaseGunReference, baseGun);
        resolvedCannon = ResolveReference(visualConfig.CannonReference, cannon);
        resolvedGatling = ResolveReference(visualConfig.GatlingReference, gatling);
        resolvedRailgun = ResolveReference(visualConfig.RailgunReference, railgun);
        appliedBaseGunReference = visualConfig.BaseGunReference;
        appliedCannonReference = visualConfig.CannonReference;
        appliedGatlingReference = visualConfig.GatlingReference;
        appliedRailgunReference = visualConfig.RailgunReference;
        appliedDefaultAdditionalWeaponVisual = visualConfig.DefaultAdditionalWeaponVisual;
        hasAppliedConfiguration = 1;
        appliedAdditionalSlot = (PlayerWeaponVisualSlot)(-1);
    }

    /// <summary>
    /// Resolves one prefab-relative selector and returns its serialized fallback when unresolved.
    /// </summary>
    /// <param name="reference">Prefab-relative path or unique GameObject name.</param>
    /// <param name="fallback">Serialized fallback mesh object.</param>
    /// <returns>Resolved runtime mesh object or the supplied fallback.</returns>
    private GameObject ResolveReference(FixedString128Bytes reference, GameObject fallback)
    {
        if (PlayerWeaponVisualReferenceUtility.TryResolve(transform, reference.ToString(), out Transform resolvedTransform))
            return resolvedTransform.gameObject;

        return fallback;
    }

    /// <summary>
    /// Restores runtime resolved references from the generated prefab fallback fields.
    /// </summary>
    private void ResolveFallbackReferences()
    {
        resolvedBaseGun = baseGun;
        resolvedCannon = cannon;
        resolvedGatling = gatling;
        resolvedRailgun = railgun;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Keeps Base Gun visible and displays at most one resolved optional weapon attachment.
    /// </summary>
    /// <param name="additionalSlot">Optional Cannon, Gatling, or Railgun attachment that should remain visible.</param>
    private void ShowBaseGunAndOnlyAdditional(PlayerWeaponVisualSlot additionalSlot)
    {
        SetActive(resolvedCannon, additionalSlot == PlayerWeaponVisualSlot.Cannon);
        SetActive(resolvedGatling, additionalSlot == PlayerWeaponVisualSlot.Gatling);
        SetActive(resolvedRailgun, additionalSlot == PlayerWeaponVisualSlot.Railgun);
        SetActive(resolvedBaseGun, true);
    }

    /// <summary>
    /// Hides previously resolved references before a configuration change so scalable selectors cannot leave stale meshes visible.
    /// </summary>
    private void HideResolvedWeaponVisuals()
    {
        SetActive(resolvedCannon, false);
        SetActive(resolvedGatling, false);
        SetActive(resolvedRailgun, false);
        SetActive(resolvedBaseGun, false);
    }

    /// <summary>
    /// Resolves the requested optional attachment and falls back to no attachment when it is unavailable.
    /// </summary>
    /// <param name="requestedSlot">Requested no-power-up optional attachment.</param>
    /// <returns>Available optional attachment or None.</returns>
    private PlayerWeaponVisualSlot ResolveAvailableAdditionalSlot(PlayerWeaponVisualSlot requestedSlot)
    {
        if (IsAlternateSlot(requestedSlot) && IsSlotAvailable(requestedSlot))
            return requestedSlot;

        return PlayerWeaponVisualSlot.None;
    }

    /// <summary>
    /// Checks whether one weapon slot has a resolved mesh object.
    /// </summary>
    /// <param name="slot">Weapon slot to inspect.</param>
    /// <returns>True when the slot has a resolved mesh object.</returns>
    private bool IsSlotAvailable(PlayerWeaponVisualSlot slot)
    {
        switch (slot)
        {
            case PlayerWeaponVisualSlot.Cannon:
                return resolvedCannon != null;
            case PlayerWeaponVisualSlot.Gatling:
                return resolvedGatling != null;
            case PlayerWeaponVisualSlot.Railgun:
                return resolvedRailgun != null;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether one slot can be selected by a Switch Weapon module.
    /// </summary>
    /// <param name="slot">Weapon visual slot to inspect.</param>
    /// <returns>True for Cannon, Gatling, or Railgun.</returns>
    private static bool IsAlternateSlot(PlayerWeaponVisualSlot slot)
    {
        return slot >= PlayerWeaponVisualSlot.Cannon && slot <= PlayerWeaponVisualSlot.Railgun;
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
    /// Clears runtime application caches so the next ECS presentation update reapplies the active visual configuration.
    /// </summary>
    private void ResetAppliedState()
    {
        appliedAdditionalSlot = (PlayerWeaponVisualSlot)(-1);
        hasAppliedConfiguration = 0;
    }
    #endregion

    #endregion
}
