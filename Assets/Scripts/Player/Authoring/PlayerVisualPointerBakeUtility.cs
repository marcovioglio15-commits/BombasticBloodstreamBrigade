using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds the baked aiming laser pointer config from the resolved (already scaled) player visual preset.
/// The pointer reuses the Laser Beam body material and palette colors so it stays visually consistent with the Laser Beam power-up.
/// </summary>
public static class PlayerVisualPointerBakeUtility
{
    #region Constants
    private const float MinimumPointerWidth = 0.005f;
    private const float MinimumPointerLengthMultiplier = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the aiming pointer config from the resolved visual preset and the authored base shooting values.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="baseShootingValues">Base authored shooting values used to bake the fixed straight length applied while orbital shots are active.</param>
    /// <param name="config">Built ECS config when the pointer is enabled and a Laser Beam body material is available.</param>
    /// <returns>True when the visual preset enables the pointer and can resolve a body material to render with.</returns>
    public static bool TryBuildConfig(PlayerVisualPreset visualPreset,
                                      ShootingValues baseShootingValues,
                                      out PlayerVisualPointerConfig config)
    {
        config = default;

        // The pointer is opt-in per visual preset; skip baking the component entirely when it is disabled.
        if (visualPreset == null || !visualPreset.EnablePointer)
            return false;

        PlayerLaserBeamVisualSettings laserBeamSettings = visualPreset.LaserBeam;
        Material bodyMaterial = laserBeamSettings != null ? laserBeamSettings.BodyMaterial : null;

        // Rendering reuses the Laser Beam body material, so an absent material makes the pointer impossible to draw.
        if (bodyMaterial == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPointerBake] Visual preset '{0}' enables the Visual Pointer but no Laser Beam body material is assigned. Pointer will not be baked.",
                                           visualPreset.name),
                             visualPreset);
            return false;
        }

        ResolvePointerPalette(visualPreset,
                              laserBeamSettings,
                              out Color coreColor,
                              out Color flowColor,
                              out Color stormColor,
                              out Color contactColor);

        config = new PlayerVisualPointerConfig
        {
            Enabled = 1,
            BodyMaterial = bodyMaterial,
            CoreColor = DamageFlashRuntimeUtility.ToLinearFloat4(coreColor),
            FlowColor = DamageFlashRuntimeUtility.ToLinearFloat4(flowColor),
            StormColor = DamageFlashRuntimeUtility.ToLinearFloat4(stormColor),
            ContactColor = DamageFlashRuntimeUtility.ToLinearFloat4(contactColor),
            Width = math.max(MinimumPointerWidth, visualPreset.PointerWidth),
            LengthMultiplier = math.max(MinimumPointerLengthMultiplier, visualPreset.PointerLengthMultiplier),
            MaxLength = math.max(0f, visualPreset.PointerMaxLength),
            Opacity = math.saturate(visualPreset.PointerOpacity),
            VerticalLift = math.max(0f, visualPreset.PointerVerticalLift),
            FreezeWithOrbitalProjectiles = visualPreset.FreezePointerWithOrbitalProjectiles ? (byte)1 : (byte)0,
            OrbitalFrozenLength = math.max(0f, visualPreset.PointerOrbitalFrozenLength),
            BaseStraightLength = ResolveBaseStraightLength(baseShootingValues)
        };
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the pointer palette by reusing the Laser Beam visual preset definition matching the authored pointer ID, falling back to the shared default palette.
    /// </summary>
    /// <param name="visualPreset">Owning visual preset used for warning context.</param>
    /// <param name="laserBeamSettings">Laser Beam visual settings that own the reusable palette definitions.</param>
    /// <param name="coreColor">Resolved white-hot core color.</param>
    /// <param name="flowColor">Resolved primary beam flow color.</param>
    /// <param name="stormColor">Resolved electrical storm color.</param>
    /// <param name="contactColor">Resolved terminal contact color.</param>
    private static void ResolvePointerPalette(PlayerVisualPreset visualPreset,
                                              PlayerLaserBeamVisualSettings laserBeamSettings,
                                              out Color coreColor,
                                              out Color flowColor,
                                              out Color stormColor,
                                              out Color contactColor)
    {
        int pointerVisualPresetId = visualPreset.PointerVisualPresetId;
        IReadOnlyList<PlayerLaserBeamVisualPresetDefinition> visualPresets = laserBeamSettings != null
            ? laserBeamSettings.VisualPresets
            : null;

        if (visualPresets != null)
        {
            for (int presetIndex = 0; presetIndex < visualPresets.Count; presetIndex++)
            {
                PlayerLaserBeamVisualPresetDefinition visualPresetDefinition = visualPresets[presetIndex];

                if (visualPresetDefinition == null || visualPresetDefinition.StableId != pointerVisualPresetId)
                    continue;

                coreColor = visualPresetDefinition.CoreColor;
                flowColor = visualPresetDefinition.FlowColor;
                stormColor = visualPresetDefinition.StormColor;
                contactColor = visualPresetDefinition.ContactColor;
                return;
            }
        }

        // No authored palette matched the selected ID, so reuse the shared default Laser Beam palette for stable colors.
        Debug.LogWarning(string.Format("[PlayerVisualPointerBake] Visual preset '{0}' selects Visual Pointer palette ID {1}, which is missing from the Laser Beam visual presets. Falling back to default beam colors.",
                                       visualPreset.name,
                                       pointerVisualPresetId),
                         visualPreset);
        PlayerLaserBeamVisualDefaultsUtility.ResolveDefaultPreset(pointerVisualPresetId,
                                                                  out string _,
                                                                  out coreColor,
                                                                  out flowColor,
                                                                  out stormColor,
                                                                  out contactColor);
    }

    /// <summary>
    /// Resolves the fixed straight pointer length used while orbital shots are active by reusing the Laser Beam travel-distance math on the authored base shooting values.
    /// </summary>
    /// <param name="baseShootingValues">Authored base shooting values containing range, lifetime and speed.</param>
    /// <returns>Clamped straight travel distance, or zero when no base values are available.</returns>
    private static float ResolveBaseStraightLength(ShootingValues baseShootingValues)
    {
        if (baseShootingValues == null)
            return 0f;

        return PlayerLaserBeamUtility.ResolveMaximumTravelDistance(baseShootingValues.ShootSpeed,
                                                                   baseShootingValues.Range,
                                                                   baseShootingValues.Lifetime);
    }
    #endregion

    #endregion
}
