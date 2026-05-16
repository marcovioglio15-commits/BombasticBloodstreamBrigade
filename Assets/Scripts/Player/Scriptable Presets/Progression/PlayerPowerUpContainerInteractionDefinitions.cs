using System;
using UnityEngine;

/// <summary>
/// Defines how dropped active power-up containers expose their interaction flow to the player.
/// </summary>
public enum PlayerPowerUpContainerInteractionMode
{
    OverlayPanel = 0,
    Prompt3D = 1
}

/// <summary>
/// Defines whether dropped active power-up containers preserve or reset runtime slot resources.
/// </summary>
public enum PlayerPowerUpContainerStoredStateMode
{
    ResetEnergyAndCooldown = 0,
    PreserveEnergyAndCooldown = 1
}

/// <summary>
/// Stores authoring settings used to spawn and interact with dropped active power-up containers.
/// </summary>
[Serializable]
public sealed class PlayerPowerUpContainerInteractionSettings
{
    #region Fields

    #region Constants
    public const float DefaultInteractionLockDurationSeconds = 0.25f;
    #endregion

    #region Serialized Fields
    [Header("Container")]
    [Tooltip("Prefab spawned on the ground when a milestone selection replaces one equipped active power up.")]
    [SerializeField] private GameObject containerPrefab;

    [Tooltip("Maximum distance from the player that enables interaction with a dropped power-up container.")]
    [SerializeField] private float interactionRadius = 2.25f;

    [Tooltip("Chooses whether interaction opens an overlay panel or exposes direct world-space swap prompts.")]
    [SerializeField] private PlayerPowerUpContainerInteractionMode interactionMode = PlayerPowerUpContainerInteractionMode.OverlayPanel;

    [Tooltip("Determines whether dropped containers preserve the current slot energy/cooldown or reset them before storage and re-equip.")]
    [SerializeField] private PlayerPowerUpContainerStoredStateMode storedStateMode = PlayerPowerUpContainerStoredStateMode.PreserveEnergyAndCooldown;

    [Tooltip("Seconds that must pass after a container swap before the same dropped container can be interacted with again.")]
    [SerializeField] private float interactionLockDuration = DefaultInteractionLockDurationSeconds;

    [Header("Overlay Panel")]
    [Tooltip("Seconds used to restore Time.timeScale after the overlay panel closes following a confirmed swap.")]
    [SerializeField] private float overlayPanelTimeScaleResumeDurationSeconds = 0.2f;

    [Header("Bindings")]
    [Tooltip("Input action ID used to open Overlay Panel interactions while the player is inside the container interaction radius.")]
    [SerializeField] private string interactActionId;

    [Tooltip("Input action ID used by 3D Prompt mode to replace the primary active slot.")]
    [SerializeField] private string replacePrimaryActionId;

    [Tooltip("Input action ID used by 3D Prompt mode to replace the secondary active slot.")]
    [SerializeField] private string replaceSecondaryActionId;
    #endregion

    #endregion

    #region Properties
    public GameObject ContainerPrefab
    {
        get
        {
            return containerPrefab;
        }
    }

    public float InteractionRadius
    {
        get
        {
            return interactionRadius;
        }
    }

    public PlayerPowerUpContainerInteractionMode InteractionMode
    {
        get
        {
            return interactionMode;
        }
    }

    public PlayerPowerUpContainerStoredStateMode StoredStateMode
    {
        get
        {
            return storedStateMode;
        }
    }

    public float OverlayPanelTimeScaleResumeDurationSeconds
    {
        get
        {
            return overlayPanelTimeScaleResumeDurationSeconds;
        }
    }

    public float InteractionLockDuration
    {
        get
        {
            return interactionLockDuration;
        }
    }

    public string InteractActionId
    {
        get
        {
            return interactActionId;
        }
    }

    public string ReplacePrimaryActionId
    {
        get
        {
            return replacePrimaryActionId;
        }
    }

    public string ReplaceSecondaryActionId
    {
        get
        {
            return replaceSecondaryActionId;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Sanitizes serialized interaction settings so runtime ECS and HUD code can consume them safely.
    /// none.
    /// </summary>
    public void Validate()
    {
        interactActionId = SanitizeActionId(interactActionId);
        replacePrimaryActionId = SanitizeActionId(replacePrimaryActionId);
        replaceSecondaryActionId = SanitizeActionId(replaceSecondaryActionId);

        if (string.IsNullOrWhiteSpace(interactActionId))
            interactActionId = "PowerUpContainerInteract";

        if (string.IsNullOrWhiteSpace(replacePrimaryActionId))
            replacePrimaryActionId = "PowerUpContainerReplacePrimary";

        if (string.IsNullOrWhiteSpace(replaceSecondaryActionId))
            replaceSecondaryActionId = "PowerUpContainerReplaceSecondary";
    }

    /// <summary>
    /// Trims one serialized input action identifier and normalizes empty values to an empty string.
    /// </summary>
    /// <param name="actionIdValue">Serialized action identifier to sanitize.</param>
    /// <returns>Sanitized action identifier.</returns>
    private static string SanitizeActionId(string actionIdValue)
    {
        if (string.IsNullOrWhiteSpace(actionIdValue))
            return string.Empty;

        return actionIdValue.Trim();
    }
    #endregion

    #endregion
}
