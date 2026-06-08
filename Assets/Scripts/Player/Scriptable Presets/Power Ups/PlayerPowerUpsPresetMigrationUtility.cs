using System;
using System.Collections.Generic;

/// <summary>
/// Migrates legacy power-up preset content to the unified modular data model.
/// </summary>
internal static class PlayerPowerUpsPresetMigrationUtility
{
    public static void MigrateModuleDefinitionsToUnifiedKinds(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        List<PowerUpModuleDefinition> moduleDefinitions = preset.ModuleDefinitionsMutable;
        HashSet<string> validModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            PowerUpModuleDefinition moduleDefinition = moduleDefinitions[index];

            if (moduleDefinition == null)
                continue;

            PowerUpModuleData payload = moduleDefinition.Data;

            if (payload == null)
                continue;

            if (!TryResolveLegacyModuleDefinition(moduleDefinition.ModuleId,
                                                  moduleDefinition.DisplayName,
                                                  moduleDefinition.ModuleKind,
                                                  out string mappedModuleId,
                                                  out string mappedDisplayName,
                                                  out PowerUpModuleKind mappedModuleKind,
                                                  out bool removeDefinition))
            {
                mappedModuleId = moduleDefinition.ModuleId;
                mappedDisplayName = moduleDefinition.DisplayName;
                mappedModuleKind = moduleDefinition.ModuleKind;
                removeDefinition = false;
            }

            if (removeDefinition)
            {
                moduleDefinitions[index] = null;
                continue;
            }

            if (mappedModuleKind == PowerUpModuleKind.Heal)
            {
                payload.HealMissingHealth.Configure(PowerUpHealApplicationMode.Instant,
                                                    payload.HealMissingHealth.HealAmount,
                                                    0f,
                                                    0.2f,
                                                    PowerUpHealStackPolicy.Refresh);
            }

            moduleDefinition.Configure(mappedModuleId,
                                       mappedDisplayName,
                                       mappedModuleKind,
                                       PowerUpModuleKindUtility.ResolveStageFromKind(mappedModuleKind),
                                       moduleDefinition.Notes,
                                       payload);
            validModuleIds.Add(mappedModuleId);
        }

        if (!validModuleIds.Contains(PlayerPowerUpsPresetDefaultsUtility.ModuleIdTriggerEvent))
        {
            moduleDefinitions.Add(PlayerPowerUpsPresetDefaultsUtility.CreateModuleDefinition(PlayerPowerUpsPresetDefaultsUtility.ModuleIdTriggerEvent,
                                                                                             "Trigger Event",
                                                                                             PowerUpModuleKind.TriggerEvent,
                                                                                             PowerUpModuleStage.Hook,
                                                                                             "Fires from a runtime event selected in payload."));
        }

        if (!validModuleIds.Contains(PlayerPowerUpsPresetDefaultsUtility.ModuleIdLaserBeam))
        {
            moduleDefinitions.Add(PlayerPowerUpsPresetDefaultsUtility.CreateModuleDefinition(PlayerPowerUpsPresetDefaultsUtility.ModuleIdLaserBeam,
                                                                                             "Laser Beam",
                                                                                             PowerUpModuleKind.LaserBeam,
                                                                                             PowerUpModuleStage.Hook,
                                                                                             "Overrides base projectile spawning with one or more continuous liquid beam lanes."));
        }

        if (!validModuleIds.Contains(PlayerPowerUpsPresetDefaultsUtility.ModuleIdImpactFrame))
        {
            moduleDefinitions.Add(PlayerPowerUpsPresetDefaultsUtility.CreateModuleDefinition(PlayerPowerUpsPresetDefaultsUtility.ModuleIdImpactFrame,
                                                                                             "Impact Frame",
                                                                                             PowerUpModuleKind.ImpactFrame,
                                                                                             PowerUpModuleStage.Execute,
                                                                                             "Runs a global time impact and fullscreen filter when an active power-up activation succeeds."));
        }

        if (!validModuleIds.Contains(PlayerPowerUpsPresetDefaultsUtility.ModuleIdSwitchWeapon))
        {
            moduleDefinitions.Add(PlayerPowerUpsPresetDefaultsUtility.CreateModuleDefinition(PlayerPowerUpsPresetDefaultsUtility.ModuleIdSwitchWeapon,
                                                                                             "Switch Weapon",
                                                                                             PowerUpModuleKind.SwitchWeapon,
                                                                                             PowerUpModuleStage.Hook,
                                                                                             "Keeps Base Gun visible and replaces the configured optional attachment with the mountable mesh identified by a defined Weapon Id while the owning power-up is equipped."));
        }

        ApplyUnifiedBindings(preset.ActivePowerUpsMutable, moduleDefinitions);
        ApplyUnifiedBindings(preset.PassivePowerUpsMutable, moduleDefinitions);
    }

    public static void MigrateLoadoutIds(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (string.IsNullOrWhiteSpace(preset.PrimaryActivePowerUpIdMutable) &&
            !string.IsNullOrWhiteSpace(preset.PrimaryActiveToolIdMutable))
        {
            preset.PrimaryActivePowerUpIdMutable = preset.PrimaryActiveToolIdMutable;
        }

        if (string.IsNullOrWhiteSpace(preset.SecondaryActivePowerUpIdMutable) &&
            !string.IsNullOrWhiteSpace(preset.SecondaryActiveToolIdMutable))
        {
            preset.SecondaryActivePowerUpIdMutable = preset.SecondaryActiveToolIdMutable;
        }

        if (preset.EquippedPassivePowerUpIdsMutable == null)
            preset.EquippedPassivePowerUpIdsMutable = new List<string>();

        if (preset.EquippedPassivePowerUpIdsMutable.Count > 0)
            return;

        if (preset.EquippedPassiveToolIdsMutable == null || preset.EquippedPassiveToolIdsMutable.Count == 0)
            return;

        for (int index = 0; index < preset.EquippedPassiveToolIdsMutable.Count; index++)
        {
            string passiveToolId = preset.EquippedPassiveToolIdsMutable[index];

            if (!string.IsNullOrWhiteSpace(passiveToolId))
                preset.EquippedPassivePowerUpIdsMutable.Add(passiveToolId);
        }
    }

    public static void MigratePassiveToolIds(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (preset.EquippedPassiveToolIdsMutable == null)
            preset.EquippedPassiveToolIdsMutable = new List<string>();

        if (preset.EquippedPassiveToolIdsMutable.Count > 0)
        {
            PlayerPowerUpsPresetLoadoutUtility.ClearPassiveToolIds(preset);
            return;
        }

        TryAppendPassiveToolId(preset, preset.PrimaryPassiveToolIdLegacy);
        TryAppendPassiveToolId(preset, preset.SecondaryPassiveToolIdLegacy);
        PlayerPowerUpsPresetLoadoutUtility.ClearPassiveToolIds(preset);
    }

    public static void MigrateLegacyDropPoolCatalog(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (preset.DropPoolsMutable == null)
            preset.DropPoolsMutable = new List<PowerUpDropPoolDefinition>();

        if (preset.DropPoolsMutable.Count > 0)
            return;

        List<PowerUpDropPoolDefinition> migratedDropPools = PlayerPowerUpsPresetDefaultsUtility.BuildDefaultDropPoolDefinitions(preset);

        for (int dropPoolIndex = 0; dropPoolIndex < migratedDropPools.Count; dropPoolIndex++)
            preset.DropPoolsMutable.Add(migratedDropPools[dropPoolIndex]);
    }

    private static void ApplyUnifiedBindings(List<ModularPowerUpDefinition> powerUps, List<PowerUpModuleDefinition> moduleDefinitions)
    {
        if (powerUps == null)
            return;

        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp == null)
                continue;

            IReadOnlyList<PowerUpModuleBinding> bindings = powerUp.ModuleBindings;

            if (bindings == null)
                continue;

            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                PowerUpModuleBinding binding = bindings[bindingIndex];

                if (binding == null)
                    continue;

                string mappedBindingModuleId = ResolveUnifiedModuleId(binding.ModuleId);
                PowerUpModuleDefinition resolvedDefinition = ResolveModuleDefinitionById(moduleDefinitions, mappedBindingModuleId);
                PowerUpModuleStage stage = binding.Stage;

                if (resolvedDefinition != null)
                    stage = resolvedDefinition.DefaultStage;

                binding.Configure(mappedBindingModuleId, stage, binding.IsEnabled);
                binding.ConfigureOverride(binding.UseOverridePayload, binding.OverridePayload);
            }
        }
    }

    private static PowerUpModuleDefinition ResolveModuleDefinitionById(List<PowerUpModuleDefinition> moduleDefinitions, string moduleId)
    {
        if (moduleDefinitions == null || string.IsNullOrWhiteSpace(moduleId))
            return null;

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            PowerUpModuleDefinition moduleDefinition = moduleDefinitions[index];

            if (moduleDefinition == null)
                continue;

            if (string.Equals(moduleDefinition.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                return moduleDefinition;
        }

        return null;
    }

    private static void TryAppendPassiveToolId(PlayerPowerUpsPreset preset, string passiveToolId)
    {
        if (preset == null || string.IsNullOrWhiteSpace(passiveToolId))
            return;

        if (!PlayerPowerUpsPresetLoadoutUtility.HasPassiveToolWithId(preset.PassiveToolsMutable, passiveToolId))
            return;

        for (int index = 0; index < preset.EquippedPassiveToolIdsMutable.Count; index++)
        {
            if (string.Equals(preset.EquippedPassiveToolIdsMutable[index], passiveToolId, StringComparison.OrdinalIgnoreCase))
                return;
        }

        preset.EquippedPassiveToolIdsMutable.Add(passiveToolId);
    }

    private static string ResolveUnifiedModuleId(string moduleId)
    {
        switch (moduleId)
        {
            case "ModuleTriggerPress":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdTriggerPress;
            case "ModuleTriggerRelease":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdTriggerRelease;
            case "ModuleTriggerHoldCharge":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdTriggerHoldCharge;
            case "ModuleGateResource":
            case "ModuleGateCooldown":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdGateResource;
            case "ModuleStateSuppressShooting":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdStateSuppressShooting;
            case "ModuleEffectProjectilePatternCone":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdProjectilesPatternCone;
            case "ModuleEffectProjectileScale":
            case "ModuleEffectProjectilePenetration":
            case "ModuleEffectProjectileTuning":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdCharacterTuning;
            case "ModuleEffectSpawnBomb":
            case "ModuleEffectSpawnObject":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdSpawnObject;
            case "ModuleEffectDash":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdDash;
            case "ModuleEffectTimeDilationEnemies":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdTimeDilationEnemies;
            case "ModuleEffectHealMissingHealth":
            case "ModuleEffectHeal":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdHeal;
            case "ModulePassiveSpawnTrailSegment":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdSpawnTrailSegment;
            case "ModulePassiveAreaTickApplyElement":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdAreaTickApplyElement;
            case "ModulePassiveDeathExplosion":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdDeathExplosion;
            case "ModulePassiveProjectileOrbitOverride":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdOrbitalProjectiles;
            case "ModulePassiveProjectileBounceOnWalls":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdBouncingProjectiles;
            case "ModulePassiveProjectileSplit":
                return PlayerPowerUpsPresetDefaultsUtility.ModuleIdProjectileSplit;
        }

        return moduleId;
    }

    private static bool TryResolveLegacyModuleDefinition(string moduleId,
                                                         string fallbackDisplayName,
                                                         PowerUpModuleKind fallbackKind,
                                                         out string mappedModuleId,
                                                         out string mappedDisplayName,
                                                         out PowerUpModuleKind mappedModuleKind,
                                                         out bool removeDefinition)
    {
        mappedModuleId = moduleId;
        mappedDisplayName = fallbackDisplayName;
        mappedModuleKind = fallbackKind;
        removeDefinition = false;

        switch (moduleId)
        {
            case "ModuleTriggerPress":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdTriggerPress;
                mappedDisplayName = "Trigger Press";
                mappedModuleKind = PowerUpModuleKind.TriggerPress;
                return true;
            case "ModuleTriggerRelease":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdTriggerRelease;
                mappedDisplayName = "Trigger Release";
                mappedModuleKind = PowerUpModuleKind.TriggerRelease;
                return true;
            case "ModuleTriggerHoldCharge":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdTriggerHoldCharge;
                mappedDisplayName = "Trigger Hold Charge";
                mappedModuleKind = PowerUpModuleKind.TriggerHoldCharge;
                return true;
            case "ModuleGateResource":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdGateResource;
                mappedDisplayName = "Resource Gate";
                mappedModuleKind = PowerUpModuleKind.GateResource;
                return true;
            case "ModuleGateCooldown":
                removeDefinition = true;
                return true;
            case "ModuleStateSuppressShooting":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdStateSuppressShooting;
                mappedDisplayName = "Suppress Shooting";
                mappedModuleKind = PowerUpModuleKind.StateSuppressShooting;
                return true;
            case "ModuleEffectProjectilePatternCone":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdProjectilesPatternCone;
                mappedDisplayName = "Projectiles Pattern Cone";
                mappedModuleKind = PowerUpModuleKind.ProjectilesPatternCone;
                return true;
            case "ModuleEffectProjectileScale":
            case "ModuleEffectProjectilePenetration":
            case "ModuleEffectProjectileTuning":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdCharacterTuning;
                mappedDisplayName = "Character Tuning";
                mappedModuleKind = PowerUpModuleKind.CharacterTuning;
                return true;
            case "ModuleEffectSpawnBomb":
            case "ModuleEffectSpawnObject":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdSpawnObject;
                mappedDisplayName = "Spawn Object";
                mappedModuleKind = PowerUpModuleKind.SpawnObject;
                return true;
            case "ModuleEffectDash":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdDash;
                mappedDisplayName = "Dash";
                mappedModuleKind = PowerUpModuleKind.Dash;
                return true;
            case "ModuleEffectTimeDilationEnemies":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdTimeDilationEnemies;
                mappedDisplayName = "Time Dilation Enemies";
                mappedModuleKind = PowerUpModuleKind.TimeDilationEnemies;
                return true;
            case "ModuleEffectHealMissingHealth":
            case "ModuleEffectHeal":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdHeal;
                mappedDisplayName = "Heal";
                mappedModuleKind = PowerUpModuleKind.Heal;
                return true;
            case "ModulePassiveSpawnTrailSegment":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdSpawnTrailSegment;
                mappedDisplayName = "Spawn Trail Segment";
                mappedModuleKind = PowerUpModuleKind.SpawnTrailSegment;
                return true;
            case "ModulePassiveAreaTickApplyElement":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdAreaTickApplyElement;
                mappedDisplayName = "Area Tick Apply Element";
                mappedModuleKind = PowerUpModuleKind.AreaTickApplyElement;
                return true;
            case "ModulePassiveDeathExplosion":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdDeathExplosion;
                mappedDisplayName = "Death Explosion";
                mappedModuleKind = PowerUpModuleKind.DeathExplosion;
                return true;
            case "ModulePassiveProjectileOrbitOverride":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdOrbitalProjectiles;
                mappedDisplayName = "Orbital Projectiles";
                mappedModuleKind = PowerUpModuleKind.OrbitalProjectiles;
                return true;
            case "ModulePassiveProjectileBounceOnWalls":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdBouncingProjectiles;
                mappedDisplayName = "Bouncing Projectiles";
                mappedModuleKind = PowerUpModuleKind.BouncingProjectiles;
                return true;
            case "ModulePassiveProjectileSplit":
                mappedModuleId = PlayerPowerUpsPresetDefaultsUtility.ModuleIdProjectileSplit;
                mappedDisplayName = "Projectile Split";
                mappedModuleKind = PowerUpModuleKind.ProjectileSplit;
                return true;
        }

        return false;
    }
}
