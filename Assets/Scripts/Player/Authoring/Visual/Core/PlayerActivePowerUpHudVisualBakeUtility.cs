using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds safe ECS active power-up HUD visual configurations from Player UI visual preset authoring data.
/// </summary>
public static class PlayerActivePowerUpHudVisualBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the runtime active power-up HUD visual configuration from the currently scaled UI visual preset.
    /// </summary>
    /// <param name="visualPreset">Scaled UI visual preset currently used by the baker.</param>
    /// <returns>Safe runtime active power-up HUD visual configuration.</returns>
    public static PlayerActivePowerUpHudVisualConfig BuildConfig(IPlayerUiVisualPresetData visualPreset)
    {
        PlayerActivePowerUpHudVisualSettings settings = visualPreset != null && visualPreset.ActivePowerUpHud != null
            ? visualPreset.ActivePowerUpHud
            : new PlayerActivePowerUpHudVisualSettings();
        PlayerPowerUpRequirementMarkerVisualSettings marker = settings.RequirementMarker ?? new PlayerPowerUpRequirementMarkerVisualSettings();
        PlayerPowerUpChargeRingVisualSettings chargeRing = settings.ChargeRing ?? new PlayerPowerUpChargeRingVisualSettings();
        PlayerPowerUpIconCooldownVisualSettings iconCooldown = settings.IconCooldown ?? new PlayerPowerUpIconCooldownVisualSettings();

        return new PlayerActivePowerUpHudVisualConfig
        {
            EnergySyringe = PlayerHealthBarVisualBakeUtility.BuildConfig(settings.EnergySyringe),
            RequirementMarker = new PlayerPowerUpRequirementMarkerVisualConfig
            {
                Color = ToFloat4(marker.Color),
                Width = math.clamp(ResolveFinite(marker.Width, 0.018f), 0.001f, 0.1f),
                Height = math.clamp(ResolveFinite(marker.Height, 0.12f), 0.001f, 0.5f),
                VerticalOffset = math.clamp(ResolveFinite(marker.VerticalOffset, 0.03f), -0.5f, 0.5f),
                Enabled = marker.Enabled ? (byte)1 : (byte)0
            },
            ChargeRing = new PlayerPowerUpChargeRingVisualConfig
            {
                BackgroundColor = ToFloat4(chargeRing.BackgroundColor),
                FillColor = ToFloat4(chargeRing.FillColor),
                OutlineColor = ToFloat4(chargeRing.OutlineColor),
                Thickness = math.clamp(ResolveFinite(chargeRing.Thickness, 0.18f), 0.02f, 0.6f),
                OutlineThickness = math.clamp(ResolveFinite(chargeRing.OutlineThickness, 0.035f), 0f, 0.2f),
                StartAngleDegrees = math.clamp(ResolveFinite(chargeRing.StartAngleDegrees, 110f), -360f, 360f),
                ArcDegrees = math.clamp(ResolveFinite(chargeRing.ArcDegrees, 140f), 10f, 360f),
                FillDirection = ResolveChargeRingFillDirection(chargeRing.FillDirection),
                Enabled = chargeRing.Enabled ? (byte)1 : (byte)0
            },
            IconCooldown = new PlayerPowerUpIconCooldownVisualConfig
            {
                LockedTint = ToFloat4(iconCooldown.LockedTint),
                DesaturationStrength = math.saturate(ResolveFinite(iconCooldown.DesaturationStrength, 0.95f)),
                RevealFeather = math.clamp(ResolveFinite(iconCooldown.RevealFeather, 0.025f), 0f, 0.25f),
                FillDirection = ResolveFillDirection(iconCooldown.FillDirection),
                Enabled = iconCooldown.Enabled ? (byte)1 : (byte)0
            },
            ChargeSmoothingSeconds = math.max(0f, ResolveFinite(settings.ChargeSmoothingSeconds, 0.05f)),
            Enabled = settings.Enabled ? (byte)1 : (byte)0,
            HideWhenPlayerMissing = settings.HideWhenPlayerMissing ? (byte)1 : (byte)0,
            HideEnergyWhenModuleMissing = settings.HideEnergyWhenModuleMissing ? (byte)1 : (byte)0,
            HideChargeWhenModuleMissing = settings.HideChargeWhenModuleMissing ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds the immutable active power-up HUD visual baseline from the unscaled source UI visual preset.
    /// </summary>
    /// <param name="visualPreset">Unscaled source UI visual preset.</param>
    /// <returns>Immutable active power-up HUD visual baseline.</returns>
    public static PlayerBaseActivePowerUpHudVisualConfig BuildBaseConfig(IPlayerUiVisualPresetData visualPreset)
    {
        return new PlayerBaseActivePowerUpHudVisualConfig
        {
            Config = BuildConfig(visualPreset)
        };
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves one authored charge-ring fill direction to a supported runtime value.
    /// </summary>
    /// <param name="value">Authored charge-ring fill direction.</param>
    /// <returns>Supported charge-ring fill direction.</returns>
    private static PlayerPowerUpChargeRingFillDirection ResolveChargeRingFillDirection(PlayerPowerUpChargeRingFillDirection value)
    {
        switch (value)
        {
            case PlayerPowerUpChargeRingFillDirection.TopToBottom:
            case PlayerPowerUpChargeRingFillDirection.BottomToTop:
                return value;
            default:
                return PlayerPowerUpChargeRingFillDirection.TopToBottom;
        }
    }

    /// <summary>
    /// Resolves one authored cooldown fill direction to a supported runtime value.
    /// </summary>
    /// <param name="value">Authored fill direction.</param>
    /// <returns>Supported fill direction.</returns>
    private static PlayerPowerUpIconCooldownFillDirection ResolveFillDirection(PlayerPowerUpIconCooldownFillDirection value)
    {
        switch (value)
        {
            case PlayerPowerUpIconCooldownFillDirection.BottomToTop:
            case PlayerPowerUpIconCooldownFillDirection.TopToBottom:
                return value;
            default:
                return PlayerPowerUpIconCooldownFillDirection.BottomToTop;
        }
    }

    /// <summary>
    /// Converts one Unity color into an unmanaged shader color.
    /// </summary>
    /// <param name="color">Authored Unity color.</param>
    /// <returns>Equivalent unmanaged RGBA value.</returns>
    private static float4 ToFloat4(Color color)
    {
        return new float4(color.r, color.g, color.b, color.a);
    }

    /// <summary>
    /// Replaces non-finite authoring data only at the bake boundary.
    /// </summary>
    /// <param name="value">Authored value.</param>
    /// <param name="fallback">Safe fallback used for non-finite values.</param>
    /// <returns>Finite value safe for ECS runtime use.</returns>
    private static float ResolveFinite(float value, float fallback)
    {
        return math.isfinite(value) ? value : fallback;
    }
    #endregion

    #endregion
}
