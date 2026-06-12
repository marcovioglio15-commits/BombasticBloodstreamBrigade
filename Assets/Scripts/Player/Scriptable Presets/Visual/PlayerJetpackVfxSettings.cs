using System;
using System.Text;
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
/// Stores configurable visibility settings for a Jetpack VFX authored inside the Visual Player hierarchy.
/// </summary>
[Serializable]
public sealed class PlayerJetpackVfxSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Prefab-relative path or unique GameObject name resolving the designer-authored Jetpack VFX inside the Visual Player hierarchy.")]
    [SerializeField] private string runtimeReference = string.Empty;

    [Tooltip("Controls whether the Jetpack VFX is always visible or only visible while the player moves, rotates, or performs either activity.")]
    [SerializeField] private PlayerJetpackVfxActivationMode activationMode = PlayerJetpackVfxActivationMode.WhileMoving;

    [Tooltip("Minimum player movement speed in world units per second required by movement-based activation modes.")]
    [SerializeField] private float movementSpeedThreshold = 0.05f;

    [Tooltip("Minimum player angular speed in degrees per second required by rotation-based activation modes.")]
    [SerializeField] private float rotationSpeedThresholdDegrees = 1f;

    [Tooltip("When enabled, shrinks or grows the Jetpack VFX local scale around its designer-authored scale according to current player movement speed.")]
    [SerializeField] private bool scaleWithMovementSpeed;

    [Tooltip("Player movement speed in world units per second at which the Jetpack VFX reaches its maximum configured size.")]
    [SerializeField] private float speedForMaximumScale = 10f;

    [Tooltip("Percentage of Speed For Maximum Scale at which the Jetpack VFX uses its designer-authored local scale.")]
    [SerializeField] private float normalScaleSpeedPercent = 50f;

    [Tooltip("Total Jetpack VFX scale variation across the full zero-to-Speed For Maximum Scale range. The authored scale is preserved at Normal Scale Speed Percent.")]
    [SerializeField] private float scaleVariationPercent = 100f;
    #endregion

    #endregion

    #region Properties
    public string RuntimeReference
    {
        get
        {
            return runtimeReference;
        }
    }

    public PlayerJetpackVfxActivationMode ActivationMode
    {
        get
        {
            return activationMode;
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

    public bool ScaleWithMovementSpeed
    {
        get
        {
            return scaleWithMovementSpeed;
        }
    }

    public float SpeedForMaximumScale
    {
        get
        {
            return speedForMaximumScale;
        }
    }

    public float NormalScaleSpeedPercent
    {
        get
        {
            return normalScaleSpeedPercent;
        }
    }

    public float ScaleVariationPercent
    {
        get
        {
            return scaleVariationPercent;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid authored Jetpack VFX values without mutating the visual preset.
    /// </summary>
    /// <param name="runtimeVisualBridgePrefab">Visual Player prefab used to resolve the Jetpack VFX reference.</param>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    public void Validate(GameObject runtimeVisualBridgePrefab, string ownerAssetName)
    {
        if (!IsSupportedActivationMode(activationMode))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: activation mode is invalid.", ownerAssetName));

        if (string.IsNullOrWhiteSpace(runtimeReference))
            return;

        if (Encoding.UTF8.GetByteCount(runtimeReference.Trim()) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: runtime reference exceeds the ECS fixed-string capacity.", ownerAssetName));
        }
        else if (runtimeVisualBridgePrefab != null &&
                 !PlayerWeaponVisualReferenceUtility.TryResolve(runtimeVisualBridgePrefab.transform,
                                                                runtimeReference,
                                                                out Transform _))
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: runtime reference '{1}' does not resolve inside Visual Player prefab '{2}'.",
                                           ownerAssetName,
                                           runtimeReference,
                                           runtimeVisualBridgePrefab.name));
        }

        if (UsesMovement(activationMode) &&
            (!IsFinite(movementSpeedThreshold) || movementSpeedThreshold < 0f))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: movement speed threshold should be finite and non-negative.", ownerAssetName));

        if (UsesRotation(activationMode) &&
            (!IsFinite(rotationSpeedThresholdDegrees) || rotationSpeedThresholdDegrees < 0f))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: rotation speed threshold should be finite and non-negative.", ownerAssetName));

        if (scaleWithMovementSpeed &&
            (!IsFinite(speedForMaximumScale) || speedForMaximumScale <= 0f))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: speed for maximum scale should be finite and greater than zero.", ownerAssetName));

        if (scaleWithMovementSpeed &&
            (!IsFinite(normalScaleSpeedPercent) || normalScaleSpeedPercent < 0f || normalScaleSpeedPercent > 100f))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: normal scale speed percent should be finite and between zero and one hundred.", ownerAssetName));

        if (scaleWithMovementSpeed &&
            (!IsFinite(scaleVariationPercent) || scaleVariationPercent < 0f))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: scale variation percent should be finite and non-negative.", ownerAssetName));

        if (scaleWithMovementSpeed &&
            IsFinite(normalScaleSpeedPercent) &&
            IsFinite(scaleVariationPercent) &&
            1f - normalScaleSpeedPercent * 0.01f * scaleVariationPercent * 0.01f <= 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Player Jetpack VFX: configured variation reaches a non-positive scale at zero speed and will use the runtime safety minimum.", ownerAssetName));
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
