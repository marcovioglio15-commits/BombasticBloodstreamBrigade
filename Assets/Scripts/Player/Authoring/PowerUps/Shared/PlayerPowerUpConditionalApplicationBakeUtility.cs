using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// Compiles conditional application payloads from one modular composition and its already synthesized active-effect data.
/// </summary>
public static class PlayerPowerUpConditionalApplicationBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one runtime conditional application config without duplicating active payload synthesis.
    /// </summary>
    /// <param name="preset">Preset used to resolve module definitions.</param>
    /// <param name="powerUp">Modular power-up being compiled.</param>
    /// <param name="sourceSlotConfig">Synthesized active slot containing reusable charge, spawn, dash, and presentation payloads.</param>
    /// <param name="allowSelfPreservation">Whether the passive-only Self-Preservation Instinct mode may be compiled.</param>
    /// <param name="allowResourceGate">Whether a toggleable Active may retain its Resource Gate alongside a shot condition.</param>
    /// <param name="conditionalConfig">Resolved conditional config, or default when no supported trigger is present.</param>
    public static void Build(PlayerPowerUpsPreset preset,
                             ModularPowerUpDefinition powerUp,
                             in PlayerPowerUpSlotConfig sourceSlotConfig,
                             bool allowSelfPreservation,
                             bool allowResourceGate,
                             out PowerUpConditionalApplicationConfig conditionalConfig)
    {
        conditionalConfig = default;

        if (preset == null || powerUp == null || powerUp.ModuleBindings == null)
            return;

        bool hasSpawnObject = false;
        bool hasDash = false;
        bool hasHeal = false;
        bool hasHoldCharge = false;
        bool hasResourceGate = false;
        bool hasDiscreteProjectileEffect = false;
        bool hasShotIncompatibleEffect = false;
        bool hasSelfPreservationIncompatibleEffect = false;
        bool hasAutomaticActiveEffect = false;
        int conditionalModuleCount = 0;
        IReadOnlyList<PowerUpModuleBinding> bindings = powerUp.ModuleBindings;

        // Resolve the one supported conditional trigger and retain active-only sibling payload presence.
        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            PowerUpModuleBinding binding = bindings[bindingIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            PowerUpModuleDefinition definition = PlayerPowerUpBakeSharedUtility.ResolveModuleDefinitionById(preset, binding.ModuleId);

            if (definition == null)
                continue;

            PowerUpModuleData payload = binding.ResolvePayload(definition);

            if (payload == null)
                continue;

            switch (definition.ModuleKind)
            {
                case PowerUpModuleKind.DelayedShootApplication:
                    conditionalModuleCount++;
                    conditionalConfig.Mode = PowerUpConditionalApplicationMode.DelayedShootApplication;
                    conditionalConfig.DelayedShotInterval = math.max(1, payload.DelayedShootApplication.ShotInterval);
                    break;
                case PowerUpModuleKind.SuddenStrike:
                    conditionalModuleCount++;
                    conditionalConfig.Mode = PowerUpConditionalApplicationMode.SuddenStrike;
                    conditionalConfig.SuddenStrikeConditionMode = payload.SuddenStrike.ConditionMode;
                    conditionalConfig.CountRotationAsMovement = payload.SuddenStrike.CountRotationAsMovement ? (byte)1 : (byte)0;
                    conditionalConfig.StationarySpeedTolerance = math.max(0f, payload.SuddenStrike.StationarySpeedTolerance);
                    conditionalConfig.StationaryRotationToleranceDegrees = math.max(0f, payload.SuddenStrike.StationaryRotationToleranceDegrees);
                    conditionalConfig.ApplyChargeMovementSlow = payload.SuddenStrike.ApplyChargeMovementSlow ? (byte)1 : (byte)0;
                    conditionalConfig.MovementSlowRecoverySeconds = math.max(0f, payload.SuddenStrike.MovementSlowRecoverySeconds);
                    break;
                case PowerUpModuleKind.SelfPreservationInstinct:
                    conditionalModuleCount++;

                    if (!allowSelfPreservation)
                        break;

                    conditionalConfig.Mode = PowerUpConditionalApplicationMode.SelfPreservationInstinct;
                    conditionalConfig.HealthThresholdMode = payload.SelfPreservationInstinct.ThresholdMode;
                    conditionalConfig.HealthThreshold = payload.SelfPreservationInstinct.ThresholdMode == SelfPreservationHealthThresholdMode.MaximumHealthPercent
                        ? math.clamp(payload.SelfPreservationInstinct.HealthThreshold, 0f, 100f)
                        : math.max(0f, payload.SelfPreservationInstinct.HealthThreshold);
                    break;
                case PowerUpModuleKind.TriggerHoldCharge:
                    hasHoldCharge = true;
                    break;
                case PowerUpModuleKind.GateResource:
                    hasResourceGate = true;
                    break;
                case PowerUpModuleKind.ProjectilesPatternCone:
                case PowerUpModuleKind.OrbitalProjectiles:
                case PowerUpModuleKind.BouncingProjectiles:
                case PowerUpModuleKind.ProjectileSplit:
                case PowerUpModuleKind.ReturningProjectiles:
                    hasDiscreteProjectileEffect = true;
                    hasSelfPreservationIncompatibleEffect = true;
                    break;
                case PowerUpModuleKind.CharacterTuning:
                    hasDiscreteProjectileEffect = true;
                    hasSelfPreservationIncompatibleEffect = true;
                    break;
                case PowerUpModuleKind.SpawnObject:
                    hasSpawnObject = true;
                    hasAutomaticActiveEffect = true;
                    break;
                case PowerUpModuleKind.Dash:
                    hasDash = true;
                    hasShotIncompatibleEffect = true;
                    hasAutomaticActiveEffect = true;
                    break;
                case PowerUpModuleKind.Heal:
                    hasHeal = true;
                    hasShotIncompatibleEffect = true;
                    hasAutomaticActiveEffect = true;
                    break;
                case PowerUpModuleKind.TimeDilationEnemies:
                case PowerUpModuleKind.OrbitalProjections:
                case PowerUpModuleKind.ImpactFrame:
                case PowerUpModuleKind.GhostTrail:
                case PowerUpModuleKind.AttractDrops:
                    hasShotIncompatibleEffect = true;
                    hasAutomaticActiveEffect = true;
                    break;
                case PowerUpModuleKind.DeathExplosion:
                case PowerUpModuleKind.SpawnTrailSegment:
                case PowerUpModuleKind.AreaTickApplyElement:
                case PowerUpModuleKind.TriggerEvent:
                case PowerUpModuleKind.LaserBeam:
                case PowerUpModuleKind.SwitchWeapon:
                case PowerUpModuleKind.StateSuppressShooting:
                    hasShotIncompatibleEffect = true;
                    hasSelfPreservationIncompatibleEffect = true;
                    break;
            }
        }

        if (conditionalModuleCount <= 0)
            return;

        bool validComposition;

        switch (conditionalConfig.Mode)
        {
            case PowerUpConditionalApplicationMode.DelayedShootApplication:
                validComposition = conditionalModuleCount == 1 &&
                                   hasDiscreteProjectileEffect &&
                                   !hasHoldCharge &&
                                   !hasSpawnObject &&
                                   !hasShotIncompatibleEffect &&
                                   (!hasResourceGate || allowResourceGate);
                break;
            case PowerUpConditionalApplicationMode.SuddenStrike:
                validComposition = conditionalModuleCount == 1 &&
                                   hasHoldCharge &&
                                   !hasShotIncompatibleEffect &&
                                   (!hasResourceGate || allowResourceGate);
                break;
            case PowerUpConditionalApplicationMode.SelfPreservationInstinct:
                validComposition = conditionalModuleCount == 1 &&
                                   allowSelfPreservation &&
                                   !hasResourceGate &&
                                   !hasHoldCharge &&
                                   hasAutomaticActiveEffect &&
                                   !hasSelfPreservationIncompatibleEffect;
                break;
            default:
                validComposition = false;
                break;
        }

        if (!validComposition)
        {
            conditionalConfig = new PowerUpConditionalApplicationConfig
            {
                Mode = PowerUpConditionalApplicationMode.InvalidComposition
            };
            return;
        }

        conditionalConfig.HoldCharge = sourceSlotConfig.ChargeShot;
        conditionalConfig.HasSpawnObject = hasSpawnObject && sourceSlotConfig.BombPrefabEntity != Unity.Entities.Entity.Null ? (byte)1 : (byte)0;
        conditionalConfig.SpawnObjectPrefabEntity = sourceSlotConfig.BombPrefabEntity;
        conditionalConfig.SpawnObject = sourceSlotConfig.Bomb;
        conditionalConfig.HasDash = hasDash ? (byte)1 : (byte)0;
        conditionalConfig.Dash = sourceSlotConfig.Dash;
        conditionalConfig.HasHeal = hasHeal ? (byte)1 : (byte)0;
        conditionalConfig.Heal = sourceSlotConfig.PortableHealthPack;
        conditionalConfig.HasImpactFrame = sourceSlotConfig.HasImpactFrame;
        conditionalConfig.ImpactFrame = sourceSlotConfig.ImpactFrame;
        conditionalConfig.HasGhostTrail = sourceSlotConfig.HasGhostTrail;
        conditionalConfig.GhostTrail = sourceSlotConfig.GhostTrail;
    }
    #endregion

    #endregion
}
