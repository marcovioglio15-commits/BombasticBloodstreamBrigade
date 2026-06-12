using System;
using UnityEngine;

/// <summary>
/// Selects which player activity keeps the attached Jetpack VFX visible.
/// </summary>
public enum PlayerJetpackVfxActivationMode : byte
{
    Always = 0,
    WhileMoving = 1,
    WhileRotating = 2,
    WhileMovingOrRotating = 3
}

/// <summary>
/// Stores configurable player-attached Jetpack VFX settings.
/// </summary>
[Serializable]
public sealed class PlayerJetpackVfxSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Optional looping VFX prefab attached to the player while the configured activity condition is valid.")]
    [SerializeField] private GameObject vfxPrefab;

    [Tooltip("Controls whether the Jetpack VFX is always visible or only visible while the player moves, rotates, or performs either activity.")]
    [SerializeField] private PlayerJetpackVfxActivationMode activationMode = PlayerJetpackVfxActivationMode.WhileMoving;

    [Tooltip("Player-local offset applied to the Jetpack VFX. The offset rotates with the player.")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier applied to the attached Jetpack VFX instance.")]
    [SerializeField] private float scaleMultiplier = 1f;

    [Tooltip("Minimum player movement speed in world units per second required by movement-based activation modes.")]
    [SerializeField] private float movementSpeedThreshold = 0.05f;

    [Tooltip("Minimum player angular speed in degrees per second required by rotation-based activation modes.")]
    [SerializeField] private float rotationSpeedThresholdDegrees = 1f;
    #endregion

    #endregion

    #region Properties
    public GameObject VfxPrefab
    {
        get
        {
            return vfxPrefab;
        }
    }

    public PlayerJetpackVfxActivationMode ActivationMode
    {
        get
        {
            return activationMode;
        }
    }

    public Vector3 SpawnOffset
    {
        get
        {
            return spawnOffset;
        }
    }

    public float ScaleMultiplier
    {
        get
        {
            return scaleMultiplier;
        }
    }

    public float MovementSpeedThreshold
    {
        get
        {
            return movementSpeedThreshold;
        }
    }

    public float RotationSpeedThresholdDegrees
    {
        get
        {
            return rotationSpeedThresholdDegrees;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid authored Jetpack VFX values without mutating the visual preset.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (!IsSupportedActivationMode(activationMode))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: activation mode is invalid.", ownerAssetName));

        if (!IsFinite(spawnOffset))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: spawn offset contains an invalid numeric value.", ownerAssetName));

        if (!IsFinite(scaleMultiplier) || scaleMultiplier <= 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: scale multiplier should be finite and greater than zero.", ownerAssetName));

        if (UsesMovement(activationMode) &&
            (!IsFinite(movementSpeedThreshold) || movementSpeedThreshold < 0f))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: movement speed threshold should be finite and non-negative.", ownerAssetName));

        if (UsesRotation(activationMode) &&
            (!IsFinite(rotationSpeedThresholdDegrees) || rotationSpeedThresholdDegrees < 0f))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: rotation speed threshold should be finite and non-negative.", ownerAssetName));
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether one Jetpack activation mode is supported without runtime reflection.
    /// </summary>
    /// <param name="value">Activation mode to inspect.</param>
    /// <returns>True when the activation mode is supported.</returns>
    private static bool IsSupportedActivationMode(PlayerJetpackVfxActivationMode value)
    {
        switch (value)
        {
            case PlayerJetpackVfxActivationMode.Always:
            case PlayerJetpackVfxActivationMode.WhileMoving:
            case PlayerJetpackVfxActivationMode.WhileRotating:
            case PlayerJetpackVfxActivationMode.WhileMovingOrRotating:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether one activation mode consumes the movement-speed threshold.
    /// </summary>
    /// <param name="value">Activation mode to inspect.</param>
    /// <returns>True when movement activity contributes to visibility.</returns>
    private static bool UsesMovement(PlayerJetpackVfxActivationMode value)
    {
        return value == PlayerJetpackVfxActivationMode.WhileMoving ||
               value == PlayerJetpackVfxActivationMode.WhileMovingOrRotating;
    }

    /// <summary>
    /// Checks whether one activation mode consumes the angular-speed threshold.
    /// </summary>
    /// <param name="value">Activation mode to inspect.</param>
    /// <returns>True when rotation activity contributes to visibility.</returns>
    private static bool UsesRotation(PlayerJetpackVfxActivationMode value)
    {
        return value == PlayerJetpackVfxActivationMode.WhileRotating ||
               value == PlayerJetpackVfxActivationMode.WhileMovingOrRotating;
    }

    /// <summary>
    /// Checks whether every vector component is finite.
    /// </summary>
    /// <param name="value">Vector value to inspect.</param>
    /// <returns>True when every component is finite.</returns>
    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    /// <summary>
    /// Checks whether one floating-point value is finite.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
