using System;
using System.Collections.Generic;

/// <summary>
/// Adds baseline returning-projectile definitions to existing presets without replacing authored content.
/// </summary>
public static class PlayerReturningProjectilesPresetDefaultsUtility
{
    #region Constants
    /// <summary>Stable module catalog identifier used by serialized bindings and tests.</summary>
    public const string ModuleId = "Module_ReturningProjectiles";
    /// <summary>Stable active power-up identifier for the baseline Boomerang entry.</summary>
    public const string BoomerangPowerUpId = "ActiveBoomerang";
    /// <summary>Stable passive power-up identifier for the baseline Two-Step Treatment entry.</summary>
    public const string TwoStepTreatmentPowerUpId = "PassiveTwoStepTreatment";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds the returning-projectile module and its two baseline power-ups when their stable IDs are missing.
    /// </summary>
    /// <param name="preset">Preset that receives any missing baseline entries.</param>
    /// <returns>True when at least one entry was added.</returns>
    public static bool EnsureContent(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return false;

        bool changed = false;
        List<string> defaultDropPools = PlayerPowerUpsPresetDefaultsUtility.BuildDefaultDropPools(preset);

        if (preset.ModuleDefinitionsMutable == null)
            preset.ModuleDefinitionsMutable = new List<PowerUpModuleDefinition>();

        if (preset.ActivePowerUpsMutable == null)
            preset.ActivePowerUpsMutable = new List<ModularPowerUpDefinition>();

        if (preset.PassivePowerUpsMutable == null)
            preset.PassivePowerUpsMutable = new List<ModularPowerUpDefinition>();

        if (!ContainsModule(preset.ModuleDefinitionsMutable,
                            ModuleId))
        {
            preset.ModuleDefinitionsMutable.Add(PlayerPowerUpsPresetDefaultsUtility.CreateModuleDefinition(ModuleId,
                                                                                                              "Returning Projectiles",
                                                                                                              PowerUpModuleKind.ReturningProjectiles,
                                                                                                              PowerUpModuleStage.Execute,
                                                                                                              "Converts projectile termination into retraced or player-seeking return travel with configurable hit and interaction rules."));
            changed = true;
        }

        if (!ContainsPowerUp(preset.ActivePowerUpsMutable,
                             BoomerangPowerUpId))
        {
            preset.ActivePowerUpsMutable.Add(PlayerPowerUpsPresetDefaultsUtility.CreateDefaultBoomerang(defaultDropPools));
            changed = true;
        }

        if (!ContainsPowerUp(preset.PassivePowerUpsMutable,
                             TwoStepTreatmentPowerUpId))
        {
            preset.PassivePowerUpsMutable.Add(PlayerPowerUpsPresetDefaultsUtility.CreateDefaultTwoStepTreatment(defaultDropPools));
            changed = true;
        }

        return changed;
    }
    #endregion

    #region Lookup Helpers
    /// <summary>
    /// Reports whether a module catalog already contains the requested stable identifier.
    /// </summary>
    /// <param name="definitions">Module catalog to inspect.</param>
    /// <param name="moduleId">Stable module identifier to match.</param>
    /// <returns>True when a matching module exists.</returns>
    private static bool ContainsModule(List<PowerUpModuleDefinition> definitions, string moduleId)
    {
        for (int index = 0; index < definitions.Count; index++)
        {
            PowerUpModuleDefinition definition = definitions[index];

            if (definition != null && string.Equals(definition.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reports whether a modular power-up list already contains the requested stable identifier.
    /// </summary>
    /// <param name="definitions">Power-up definitions to inspect.</param>
    /// <param name="powerUpId">Stable power-up identifier to match.</param>
    /// <returns>True when a matching power-up exists.</returns>
    private static bool ContainsPowerUp(List<ModularPowerUpDefinition> definitions, string powerUpId)
    {
        for (int index = 0; index < definitions.Count; index++)
        {
            ModularPowerUpDefinition definition = definitions[index];

            if (definition != null &&
                definition.CommonData != null &&
                string.Equals(definition.CommonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #endregion
}
