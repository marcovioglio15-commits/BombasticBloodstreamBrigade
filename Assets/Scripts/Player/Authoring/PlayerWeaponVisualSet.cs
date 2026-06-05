using UnityEngine;

/// <summary>
/// Preserves the authored weapon-mesh configuration and applies cached Switch Weapon overrides.
/// </summary>
public sealed class PlayerWeaponVisualSet : MonoBehaviour
{
    #region Constants
    private const byte AppliedStateUnknown = 0;
    private const byte AppliedStateAuthored = 1;
    private const byte AppliedStateSwitchWeapon = 2;
    #endregion

    #region Fields
    [Header("Weapon Meshes")]
    [Tooltip("Base weapon mesh forced visible while Switch Weapon is active; its authored active state is preserved otherwise.")]
    [SerializeField]
    private GameObject baseGun;

    [Tooltip("Alternate cannon mesh selectable by Switch Weapon; its authored active state is preserved otherwise.")]
    [SerializeField]
    private GameObject cannon;

    [Tooltip("Alternate gatling mesh selectable by Switch Weapon; its authored active state is preserved otherwise.")]
    [SerializeField]
    private GameObject gatling;

    [Tooltip("Alternate railgun mesh selectable by Switch Weapon; its authored active state is preserved otherwise.")]
    [SerializeField]
    private GameObject railgun;

    private bool authoredBaseGunActive;
    private bool authoredCannonActive;
    private bool authoredGatlingActive;
    private bool authoredRailgunActive;
    private bool authoredStateCaptured;
    private byte appliedState;
    private PlayerWeaponVisualSlot appliedSlot = (PlayerWeaponVisualSlot)(-1);
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
    /// Captures the prefab-authored visual configuration before any runtime override can be applied.
    /// </summary>
    private void Awake()
    {
        CaptureAuthoredState();
    }

    /// <summary>
    /// Invalidates the applied-state cache without modifying the currently authored or overridden mesh state.
    /// </summary>
    private void OnEnable()
    {
        ResetAppliedState();
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Assigns the weapon mesh references and captures their current authored active states.
    /// </summary>
    /// <param name="baseGunValue">Base gun mesh forced visible only while Switch Weapon is active.</param>
    /// <param name="cannonValue">Alternate cannon mesh.</param>
    /// <param name="gatlingValue">Alternate gatling mesh.</param>
    /// <param name="railgunValue">Alternate railgun mesh.</param>
    public void Configure(GameObject baseGunValue,
                          GameObject cannonValue,
                          GameObject gatlingValue,
                          GameObject railgunValue)
    {
        baseGun = baseGunValue;
        cannon = cannonValue;
        gatling = gatlingValue;
        railgun = railgunValue;
        CaptureAuthoredState();
        ResetAppliedState();
    }
    #endregion

    #region Runtime Application
    /// <summary>
    /// Applies one alternate-weapon override or restores the exact prefab-authored configuration.
    /// </summary>
    /// <param name="hasWeaponSwitch">Whether an active Switch Weapon module currently owns the visual override.</param>
    /// <param name="weaponVisualSlot">Requested alternate mesh slot.</param>
    public void Apply(bool hasWeaponSwitch, PlayerWeaponVisualSlot weaponVisualSlot)
    {
        if (!hasWeaponSwitch)
        {
            if (appliedState == AppliedStateAuthored)
                return;

            RestoreAuthoredState();
            appliedState = AppliedStateAuthored;
            return;
        }

        PlayerWeaponVisualSlot resolvedSlot = ResolveAvailableAlternateSlot(weaponVisualSlot);

        if (appliedState == AppliedStateSwitchWeapon && appliedSlot == resolvedSlot)
            return;

        appliedState = AppliedStateSwitchWeapon;
        appliedSlot = resolvedSlot;
        SetActive(baseGun, true);
        SetActive(cannon, resolvedSlot == PlayerWeaponVisualSlot.Cannon);
        SetActive(gatling, resolvedSlot == PlayerWeaponVisualSlot.Gatling);
        SetActive(railgun, resolvedSlot == PlayerWeaponVisualSlot.Railgun);
    }
    #endregion

    #region Authored State
    /// <summary>
    /// Captures the current active states used as the no-power-up visual configuration.
    /// </summary>
    private void CaptureAuthoredState()
    {
        authoredBaseGunActive = IsActive(baseGun);
        authoredCannonActive = IsActive(cannon);
        authoredGatlingActive = IsActive(gatling);
        authoredRailgunActive = IsActive(railgun);
        authoredStateCaptured = baseGun != null || cannon != null || gatling != null || railgun != null;
    }

    /// <summary>
    /// Restores the mesh active states captured from the authored prefab configuration.
    /// </summary>
    private void RestoreAuthoredState()
    {
        if (!authoredStateCaptured)
            CaptureAuthoredState();

        SetActive(baseGun, authoredBaseGunActive);
        SetActive(cannon, authoredCannonActive);
        SetActive(gatling, authoredGatlingActive);
        SetActive(railgun, authoredRailgunActive);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the requested alternate slot and falls back deterministically when its mesh is unavailable.
    /// </summary>
    /// <param name="requestedSlot">Requested alternate weapon slot.</param>
    /// <returns>Available alternate slot, or BaseGun when no alternate mesh exists.</returns>
    private PlayerWeaponVisualSlot ResolveAvailableAlternateSlot(PlayerWeaponVisualSlot requestedSlot)
    {
        switch (requestedSlot)
        {
            case PlayerWeaponVisualSlot.Cannon:
                if (cannon != null)
                    return requestedSlot;

                break;
            case PlayerWeaponVisualSlot.Gatling:
                if (gatling != null)
                    return requestedSlot;

                break;
            case PlayerWeaponVisualSlot.Railgun:
                if (railgun != null)
                    return requestedSlot;

                break;
        }

        if (cannon != null)
            return PlayerWeaponVisualSlot.Cannon;

        if (gatling != null)
            return PlayerWeaponVisualSlot.Gatling;

        if (railgun != null)
            return PlayerWeaponVisualSlot.Railgun;

        return PlayerWeaponVisualSlot.BaseGun;
    }

    /// <summary>
    /// Clears the runtime application cache so the next ECS presentation update reapplies the correct state.
    /// </summary>
    private void ResetAppliedState()
    {
        appliedState = AppliedStateUnknown;
        appliedSlot = (PlayerWeaponVisualSlot)(-1);
    }

    /// <summary>
    /// Returns the current authored active state for one optional mesh object.
    /// </summary>
    /// <param name="target">Optional mesh object.</param>
    /// <returns>True when the object exists and is active in its own hierarchy node.</returns>
    private static bool IsActive(GameObject target)
    {
        return target != null && target.activeSelf;
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

    #endregion
}
