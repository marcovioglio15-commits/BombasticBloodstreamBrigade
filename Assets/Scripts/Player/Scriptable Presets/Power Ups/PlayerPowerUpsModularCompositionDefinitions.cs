using System;
using System.Collections.Generic;
using UnityEngine;

#region Modular Composition Definitions
/// <summary>
/// Defines the alternate weapon mesh shown alongside Base Gun and its scalable upper-body shooting animation selector.
/// </summary>
[Serializable]
public sealed class PowerUpSwitchWeaponModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Alternate weapon mesh displayed beside Base Gun while the owning passive or toggleable active power-up is in effect.")]
    [SerializeField]
    private PlayerWeaponVisualSlot weaponSlot = PlayerWeaponVisualSlot.Cannon;

    [Tooltip("Upper-body shooting animation selected while this Switch Weapon payload owns the visible attachment. Automatic resolves the matching weapon-specific clip.")]
    [SerializeField]
    private PlayerShootAnimationClipSlot shootAnimationClipSlot = PlayerShootAnimationClipSlot.Automatic;
    #endregion

    #endregion

    #region Properties
    public PlayerWeaponVisualSlot WeaponSlot
    {
        get
        {
            return weaponSlot;
        }
    }

    public PlayerShootAnimationClipSlot ShootAnimationClipSlot
    {
        get
        {
            return shootAnimationClipSlot;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the alternate weapon slot and restores automatic animation selection for default payload creation.
    /// </summary>
    /// <param name="weaponSlotValue">Alternate weapon slot to assign.</param>
    public void Configure(PlayerWeaponVisualSlot weaponSlotValue)
    {
        weaponSlot = weaponSlotValue;
        shootAnimationClipSlot = PlayerShootAnimationClipSlot.Automatic;
    }

    /// <summary>
    /// Assigns the alternate weapon slot and explicit upper-body shooting animation selection.
    /// </summary>
    /// <param name="weaponSlotValue">Alternate weapon slot to assign.</param>
    /// <param name="shootAnimationClipSlotValue">Shooting animation slot selected while this module owns the weapon visual.</param>
    public void Configure(PlayerWeaponVisualSlot weaponSlotValue,
                          PlayerShootAnimationClipSlot shootAnimationClipSlotValue)
    {
        weaponSlot = weaponSlotValue;
        shootAnimationClipSlot = shootAnimationClipSlotValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Preserves authored enum values so validation can report incoherent selections without mutating preset data.
    /// </summary>
    public void Validate()
    {
        // The management tool reports invalid selections and runtime resolution provides the deterministic fallback.
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one composed power-up definition and its ordered module bindings.
/// </summary>
[Serializable]
public sealed class ModularPowerUpDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Common metadata and drop data for this composed power up.")]
    [SerializeField]
    private PowerUpCommonData commonData = new PowerUpCommonData();

    [Tooltip("Ordered list of module bindings composing this power up.")]
    [SerializeField]
    private List<PowerUpModuleBinding> moduleBindings = new List<PowerUpModuleBinding>();

    [Tooltip("When enabled, this power up cannot be replaced from runtime slots.")]
    [SerializeField]
    private bool unreplaceable;
    #endregion

    #endregion

    #region Properties
    public PowerUpCommonData CommonData
    {
        get
        {
            return commonData;
        }
    }

    public IReadOnlyList<PowerUpModuleBinding> ModuleBindings
    {
        get
        {
            return moduleBindings;
        }
    }

    public bool Unreplaceable
    {
        get
        {
            return unreplaceable;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns common metadata while preserving or creating the ordered module-binding collection.
    /// </summary>
    /// <param name="commonDataValue">Common metadata assigned to the composed power up.</param>
    /// <param name="unreplaceableValue">Whether runtime loadout replacement must reject this power up.</param>
    public void Configure(PowerUpCommonData commonDataValue, bool unreplaceableValue)
    {
        commonData = commonDataValue;
        unreplaceable = unreplaceableValue;

        if (moduleBindings == null)
            moduleBindings = new List<PowerUpModuleBinding>();
    }

    /// <summary>
    /// Removes every authored module binding while retaining an allocated collection.
    /// </summary>
    public void ClearBindings()
    {
        if (moduleBindings == null)
            moduleBindings = new List<PowerUpModuleBinding>();

        moduleBindings.Clear();
    }

    /// <summary>
    /// Appends one non-null module binding to the composed power up.
    /// </summary>
    /// <param name="binding">Module binding appended in execution order.</param>
    public void AddBinding(PowerUpModuleBinding binding)
    {
        if (binding == null)
            return;

        if (moduleBindings == null)
            moduleBindings = new List<PowerUpModuleBinding>();

        moduleBindings.Add(binding);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Allocates missing references and guarantees unique stable binding identifiers.
    /// </summary>
    public void Validate()
    {
        if (commonData == null)
            commonData = new PowerUpCommonData();

        commonData.Validate();

        if (moduleBindings == null)
            moduleBindings = new List<PowerUpModuleBinding>();

        HashSet<string> visitedBindingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < moduleBindings.Count; index++)
        {
            PowerUpModuleBinding binding = moduleBindings[index];

            if (binding == null)
                continue;

            binding.Validate();

            while (!visitedBindingIds.Add(binding.BindingId))
                binding.RegenerateBindingId();
        }
    }
    #endregion

    #endregion
}
#endregion
