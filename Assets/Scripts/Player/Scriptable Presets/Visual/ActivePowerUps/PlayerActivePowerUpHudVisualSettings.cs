using System;
using UnityEngine;

/// <summary>
/// Selects the direction used by active power-up icons to reveal their original colors while cooldown expires.
/// </summary>
public enum PlayerPowerUpIconCooldownFillDirection : byte
{
    BottomToTop = 0,
    TopToBottom = 1
}

/// <summary>
/// Selects the direction used by active power-up charge semirings to reveal the filled arc.
/// </summary>
public enum PlayerPowerUpChargeRingFillDirection : byte
{
    TopToBottom = 0,
    BottomToTop = 1
}

/// <summary>
/// Stores the activation-energy marker drawn over an active power-up energy syringe.
/// </summary>
[Serializable]
public sealed class PlayerPowerUpRequirementMarkerVisualSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Shows a triangle marker on the energy syringe when the active power-up has an energy activation requirement.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Direct color applied to the activation-requirement triangle marker.")]
    [SerializeField] private Color color = new Color(1f, 0.04f, 0.02f, 1f);

    [Tooltip("Reference-length normalized width of the marker. Runtime compensation keeps its pixel footprint stable across syringe lengths.")]
    [Range(0.001f, 0.1f)]
    [SerializeField] private float width = 0.018f;

    [Tooltip("Normalized marker height in the syringe shader UV space.")]
    [Range(0.001f, 0.5f)]
    [SerializeField] private float height = 0.12f;

    [Tooltip("Normalized marker offset from the chamber top. Positive values move the marker upward.")]
    [Range(-0.5f, 0.5f)]
    [SerializeField] private float verticalOffset = 0.03f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public Color Color => color;
    public float Width => width;
    public float Height => height;
    public float VerticalOffset => verticalOffset;
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid marker values without mutating serialized data.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (!enabled)
            return;

        if (!IsFinite(color))
            LogWarning(ownerAssetName, "Color channels should be finite.");

        if (!IsFinite(width) || width < 0.001f || width > 0.1f)
            LogWarning(ownerAssetName, "Width should be finite and within 0.001-0.1.");

        if (!IsFinite(height) || height < 0.001f || height > 0.5f)
            LogWarning(ownerAssetName, "Height should be finite and within 0.001-0.5.");

        if (!IsFinite(verticalOffset) || verticalOffset < -0.5f || verticalOffset > 0.5f)
            LogWarning(ownerAssetName, "Vertical Offset should be finite and within -0.5 to 0.5.");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one requirement-marker preset warning.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name.</param>
    /// <param name="message">Warning message.</param>
    private static void LogWarning(string ownerAssetName, string message)
    {
        Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Active Power-Up HUD/Requirement Marker: {1}",
                                       ownerAssetName,
                                       message));
    }

    /// <summary>
    /// Checks whether every color channel is finite.
    /// </summary>
    /// <param name="value">Color to inspect.</param>
    /// <returns>True when all RGBA channels are finite.</returns>
    private static bool IsFinite(Color value)
    {
        return IsFinite(value.r) &&
               IsFinite(value.g) &&
               IsFinite(value.b) &&
               IsFinite(value.a);
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

/// <summary>
/// Stores visual settings for the active power-up charge semiring.
/// </summary>
[Serializable]
public sealed class PlayerPowerUpChargeRingVisualSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Shows the charge progress as a procedural semiring around the active power-up icon.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Direct color used by the unfilled semiring track.")]
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.075f, 0.055f, 0.78f);

    [Tooltip("Direct color used by the filled semiring arc.")]
    [SerializeField] private Color fillColor = new Color(1f, 0.86f, 0.02f, 1f);

    [Tooltip("Direct color used by the semiring outline.")]
    [SerializeField] private Color outlineColor = new Color(0.035f, 0.03f, 0.025f, 1f);

    [Tooltip("Direction used by the charge semiring to reveal the filled arc along its authored angle range.")]
    [SerializeField] private PlayerPowerUpChargeRingFillDirection fillDirection = PlayerPowerUpChargeRingFillDirection.TopToBottom;

    [Tooltip("Normalized semiring band thickness relative to the widget half-size.")]
    [Range(0.02f, 0.6f)]
    [SerializeField] private float thickness = 0.18f;

    [Tooltip("Normalized outline thickness around both edges of the semiring.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float outlineThickness = 0.035f;

    [Tooltip("Start angle in degrees for the semiring. Zero points right and positive values rotate counter-clockwise.")]
    [Range(-360f, 360f)]
    [SerializeField] private float startAngleDegrees = 110f;

    [Tooltip("Total arc length in degrees covered by the charge semiring.")]
    [Range(10f, 360f)]
    [SerializeField] private float arcDegrees = 140f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public Color BackgroundColor => backgroundColor;
    public Color FillColor => fillColor;
    public Color OutlineColor => outlineColor;
    public PlayerPowerUpChargeRingFillDirection FillDirection => fillDirection;
    public float Thickness => thickness;
    public float OutlineThickness => outlineThickness;
    public float StartAngleDegrees => startAngleDegrees;
    public float ArcDegrees => arcDegrees;
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid charge-ring values without mutating serialized data.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (!enabled)
            return;

        if (!IsFinite(backgroundColor) || !IsFinite(fillColor) || !IsFinite(outlineColor))
            LogWarning(ownerAssetName, "All color channels should be finite.");

        if (fillDirection != PlayerPowerUpChargeRingFillDirection.TopToBottom &&
            fillDirection != PlayerPowerUpChargeRingFillDirection.BottomToTop)
        {
            LogWarning(ownerAssetName, "Fill Direction should resolve to Top To Bottom or Bottom To Top.");
        }

        if (!IsFinite(thickness) || thickness < 0.02f || thickness > 0.6f)
            LogWarning(ownerAssetName, "Thickness should be finite and within 0.02-0.6.");

        if (!IsFinite(outlineThickness) || outlineThickness < 0f || outlineThickness > 0.2f)
            LogWarning(ownerAssetName, "Outline Thickness should be finite and within 0-0.2.");

        if (!IsFinite(startAngleDegrees) || startAngleDegrees < -360f || startAngleDegrees > 360f)
            LogWarning(ownerAssetName, "Start Angle Degrees should be finite and within -360 to 360.");

        if (!IsFinite(arcDegrees) || arcDegrees < 10f || arcDegrees > 360f)
            LogWarning(ownerAssetName, "Arc Degrees should be finite and within 10-360.");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one charge-ring preset warning.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name.</param>
    /// <param name="message">Warning message.</param>
    private static void LogWarning(string ownerAssetName, string message)
    {
        Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Active Power-Up HUD/Charge Ring: {1}",
                                       ownerAssetName,
                                       message));
    }

    /// <summary>
    /// Checks whether every color channel is finite.
    /// </summary>
    /// <param name="value">Color to inspect.</param>
    /// <returns>True when all RGBA channels are finite.</returns>
    private static bool IsFinite(Color value)
    {
        return IsFinite(value.r) &&
               IsFinite(value.g) &&
               IsFinite(value.b) &&
               IsFinite(value.a);
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

/// <summary>
/// Stores icon desaturation and color-reveal settings used while active power-up cooldown blocks energy recovery.
/// </summary>
[Serializable]
public sealed class PlayerPowerUpIconCooldownVisualSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Desaturates the active power-up icon while cooldown or toggle reactivation lock is still running.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Direction used by the icon to reveal original colors while cooldown expires.")]
    [SerializeField] private PlayerPowerUpIconCooldownFillDirection fillDirection = PlayerPowerUpIconCooldownFillDirection.BottomToTop;

    [Tooltip("Strength of grayscale conversion while the icon is locked by cooldown.")]
    [Range(0f, 1f)]
    [SerializeField] private float desaturationStrength = 0.95f;

    [Tooltip("Tint multiplied over the desaturated locked portion of the icon.")]
    [SerializeField] private Color lockedTint = new Color(0.38f, 0.38f, 0.38f, 0.92f);

    [Tooltip("Softness of the transition between locked grayscale and revealed original colors.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float revealFeather = 0.025f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public PlayerPowerUpIconCooldownFillDirection FillDirection => fillDirection;
    public float DesaturationStrength => desaturationStrength;
    public Color LockedTint => lockedTint;
    public float RevealFeather => revealFeather;
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid icon cooldown values without mutating serialized data.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (!enabled)
            return;

        if (fillDirection != PlayerPowerUpIconCooldownFillDirection.BottomToTop &&
            fillDirection != PlayerPowerUpIconCooldownFillDirection.TopToBottom)
        {
            LogWarning(ownerAssetName, "Fill Direction should resolve to Bottom To Top or Top To Bottom.");
        }

        if (!IsFinite(desaturationStrength) || desaturationStrength < 0f || desaturationStrength > 1f)
            LogWarning(ownerAssetName, "Desaturation Strength should be finite and within 0-1.");

        if (!IsFinite(lockedTint))
            LogWarning(ownerAssetName, "Locked Tint channels should be finite.");

        if (!IsFinite(revealFeather) || revealFeather < 0f || revealFeather > 0.25f)
            LogWarning(ownerAssetName, "Reveal Feather should be finite and within 0-0.25.");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one icon-cooldown preset warning.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name.</param>
    /// <param name="message">Warning message.</param>
    private static void LogWarning(string ownerAssetName, string message)
    {
        Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Active Power-Up HUD/Icon Cooldown: {1}",
                                       ownerAssetName,
                                       message));
    }

    /// <summary>
    /// Checks whether every color channel is finite.
    /// </summary>
    /// <param name="value">Color to inspect.</param>
    /// <returns>True when all RGBA channels are finite.</returns>
    private static bool IsFinite(Color value)
    {
        return IsFinite(value.r) &&
               IsFinite(value.g) &&
               IsFinite(value.b) &&
               IsFinite(value.a);
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

/// <summary>
/// Stores scalable HUD presentation settings for active power-up icons, energy syringes, and charge semirings.
/// </summary>
[Serializable]
public sealed class PlayerActivePowerUpHudVisualSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the redesigned active power-up HUD widgets.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Hides active power-up HUD widgets while no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;

    [Tooltip("Hides a slot's energy syringe while the equipped active power-up has no energy module.")]
    [SerializeField] private bool hideEnergyWhenModuleMissing = true;

    [Tooltip("Hides a slot's charge semiring while the equipped active power-up has no hold-charge module.")]
    [SerializeField] private bool hideChargeWhenModuleMissing = true;

    [Tooltip("Seconds used to smooth charge semiring fill transitions. Set zero for immediate movement.")]
    [Range(0f, 2f)]
    [SerializeField] private float chargeSmoothingSeconds = 0.05f;

    [Tooltip("Single-channel syringe settings used by active power-up energy bars.")]
    [SerializeField] private PlayerHealthBarsVisualSettings energySyringe = new PlayerHealthBarsVisualSettings(true);

    [Tooltip("Triangle marker settings used to show energy activation requirements.")]
    [SerializeField] private PlayerPowerUpRequirementMarkerVisualSettings requirementMarker = new PlayerPowerUpRequirementMarkerVisualSettings();

    [Tooltip("Procedural semiring settings used by charge progress.")]
    [SerializeField] private PlayerPowerUpChargeRingVisualSettings chargeRing = new PlayerPowerUpChargeRingVisualSettings();

    [Tooltip("Icon cooldown desaturation and color-reveal settings.")]
    [SerializeField] private PlayerPowerUpIconCooldownVisualSettings iconCooldown = new PlayerPowerUpIconCooldownVisualSettings();
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public bool HideWhenPlayerMissing => hideWhenPlayerMissing;
    public bool HideEnergyWhenModuleMissing => hideEnergyWhenModuleMissing;
    public bool HideChargeWhenModuleMissing => hideChargeWhenModuleMissing;
    public float ChargeSmoothingSeconds => chargeSmoothingSeconds;
    public PlayerHealthBarsVisualSettings EnergySyringe => energySyringe;
    public PlayerPowerUpRequirementMarkerVisualSettings RequirementMarker => requirementMarker;
    public PlayerPowerUpChargeRingVisualSettings ChargeRing => chargeRing;
    public PlayerPowerUpIconCooldownVisualSettings IconCooldown => iconCooldown;
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid active power-up HUD values without mutating serialized data.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (energySyringe == null ||
            requirementMarker == null ||
            chargeRing == null ||
            iconCooldown == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Active Power-Up HUD: one or more settings blocks are missing.",
                                           ownerAssetName));
            return;
        }

        if (!IsFinite(chargeSmoothingSeconds) || chargeSmoothingSeconds < 0f || chargeSmoothingSeconds > 2f)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Active Power-Up HUD: Charge Smoothing Seconds should be finite and within 0-2.",
                                           ownerAssetName));
        }

        energySyringe.Validate(ownerAssetName);
        requirementMarker.Validate(ownerAssetName);
        chargeRing.Validate(ownerAssetName);
        iconCooldown.Validate(ownerAssetName);
    }
    #endregion

    #region Helpers
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
