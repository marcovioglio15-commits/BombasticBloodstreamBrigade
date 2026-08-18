using Unity.Mathematics;

/// <summary>
/// Applies unified formula results to conditional power-up gates and their embedded active-effect payloads.
/// </summary>
public static class PlayerRuntimePowerUpConditionalScalingApplyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies numeric and enum formula results to a conditional gate or one of its embedded active-effect payloads.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling-rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="config">Mutable conditional config rebuilt from immutable baseline data.</param>
    /// <returns>True when the path targeted a conditional field and was applied.</returns>
    public static bool TryApplyValue(string payloadPath,
                                     float resolvedValue,
                                     ref PowerUpConditionalApplicationConfig config)
    {
        if (config.Mode == PowerUpConditionalApplicationMode.None)
            return false;

        if (PlayerRuntimePowerUpScalingPathUtility.TryApplyLaserBeamValue(payloadPath,
                                                                          PlayerRuntimePowerUpScalingPathUtility.HoldChargeChargedLaserBeamPayloadPrefix,
                                                                          resolvedValue,
                                                                          ref config.HoldCharge.ChargedLaserBeam))
        {
            return true;
        }

        if (PlayerRuntimePowerUpBombScalingApplyUtility.TryApplyValue(payloadPath,
                                                                      resolvedValue,
                                                                      ref config.SpawnObject))
        {
            return true;
        }

        if (PlayerRuntimePowerUpImpactFrameScalingApplyUtility.TryApplyValue(payloadPath,
                                                                             resolvedValue,
                                                                             ref config.ImpactFrame))
        {
            return true;
        }

        if (PlayerRuntimePowerUpGhostTrailScalingApplyUtility.TryApplyValue(payloadPath,
                                                                            resolvedValue,
                                                                            ref config.GhostTrail))
        {
            return true;
        }

        switch (payloadPath)
        {
            case "delayedShootApplication.shotInterval":
                config.DelayedShotInterval = math.max(1, (int)math.round(resolvedValue));
                return true;
            case "suddenStrike.conditionMode":
                config.SuddenStrikeConditionMode = PlayerRuntimeScalingEnumUtility.ResolveSuddenStrikeChargeConditionMode(resolvedValue);
                return true;
            case "suddenStrike.stationarySpeedTolerance":
                config.StationarySpeedTolerance = math.max(0f, resolvedValue);
                return true;
            case "suddenStrike.stationaryRotationToleranceDegrees":
                config.StationaryRotationToleranceDegrees = math.max(0f, resolvedValue);
                return true;
            case "suddenStrike.movementSlowRecoverySeconds":
                config.MovementSlowRecoverySeconds = math.max(0f, resolvedValue);
                return true;
            case "selfPreservationInstinct.thresholdMode":
                config.HealthThresholdMode = PlayerRuntimeScalingEnumUtility.ResolveSelfPreservationHealthThresholdMode(resolvedValue);
                return true;
            case "selfPreservationInstinct.healthThreshold":
                config.HealthThreshold = math.max(0f, resolvedValue);
                return true;
            case "holdCharge.requiredCharge":
                config.HoldCharge.RequiredCharge = math.max(0f, resolvedValue);

                if (config.HoldCharge.MaximumCharge < config.HoldCharge.RequiredCharge)
                    config.HoldCharge.MaximumCharge = config.HoldCharge.RequiredCharge;

                return true;
            case "holdCharge.maximumCharge":
                config.HoldCharge.MaximumCharge = math.max(config.HoldCharge.RequiredCharge, resolvedValue);
                return true;
            case "holdCharge.chargeRatePerSecond":
                config.HoldCharge.ChargeRatePerSecond = math.max(0f, resolvedValue);
                return true;
            case "holdCharge.chargeAnimationClipSlot":
                config.HoldCharge.ChargeAnimationClipSlot = PlayerRuntimeScalingEnumUtility.ResolvePlayerChargeAnimationClipSlot(resolvedValue);
                return true;
            case "holdCharge.releaseAnimationClipSlot":
                config.HoldCharge.ReleaseAnimationClipSlot = PlayerRuntimeScalingEnumUtility.ResolvePlayerReleaseAnimationClipSlot(resolvedValue);
                return true;
            case "holdCharge.decayAfterReleasePercentPerSecond":
                config.HoldCharge.DecayAfterReleasePercentPerSecond = math.max(0f, resolvedValue);
                return true;
            case "holdCharge.passiveChargeGainPercentPerSecond":
                config.HoldCharge.PassiveChargeGainPercentPerSecond = math.max(0f, resolvedValue);
                return true;
            case "holdCharge.laserDurationSeconds":
                config.HoldCharge.LaserDurationSeconds = math.max(0f, resolvedValue);
                return true;
            case "holdCharge.chargedLaserDurationSeconds":
                config.HoldCharge.ChargedLaserDurationSeconds = math.max(0f, resolvedValue);
                return true;
            case "holdCharge.maximumPlayerSlowPercent":
                config.HoldCharge.MaximumPlayerSlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return true;
            case "dash.distance":
                config.Dash.Distance = math.max(0f, resolvedValue);
                return true;
            case "dash.directionMode":
                config.Dash.DirectionMode = PlayerRuntimeScalingEnumUtility.ResolveDashDirectionMode(resolvedValue);
                return true;
            case "dash.duration":
                config.Dash.Duration = math.max(0.01f, resolvedValue);
                return true;
            case "dash.speedTransitionInSeconds":
                config.Dash.SpeedTransitionInSeconds = math.max(0f, resolvedValue);
                return true;
            case "dash.speedTransitionOutSeconds":
                config.Dash.SpeedTransitionOutSeconds = math.max(0f, resolvedValue);
                return true;
            case "dash.wallBounceIntensity":
                config.Dash.WallBounceIntensity = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "dash.invulnerabilityExtraTime":
                config.Dash.InvulnerabilityExtraTime = math.max(0f, resolvedValue);
                return true;
            case "healMissingHealth.healAmount":
                config.Heal.HealAmount = math.max(0f, resolvedValue);
                return true;
            case "healMissingHealth.durationSeconds":
                config.Heal.DurationSeconds = math.max(0f, resolvedValue);
                return true;
            case "healMissingHealth.tickIntervalSeconds":
                config.Heal.TickIntervalSeconds = math.max(0.01f, resolvedValue);
                return true;
            case "healMissingHealth.applyMode":
                config.Heal.ApplyMode = PlayerRuntimeScalingEnumUtility.ResolvePowerUpHealApplicationMode(resolvedValue);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies boolean formula results to a conditional gate or one of its embedded active-effect payloads.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling-rule stat key.</param>
    /// <param name="resolvedValue">Boolean formula result already evaluated by the unified scaling system.</param>
    /// <param name="config">Mutable conditional config rebuilt from immutable baseline data.</param>
    /// <returns>True when the path targeted a conditional boolean field and was applied.</returns>
    public static bool TryApplyBooleanValue(string payloadPath,
                                            bool resolvedValue,
                                            ref PowerUpConditionalApplicationConfig config)
    {
        if (config.Mode == PowerUpConditionalApplicationMode.None)
            return false;

        if (PlayerRuntimePowerUpScalingPathUtility.TryApplyLaserBeamBooleanValue(payloadPath,
                                                                                 PlayerRuntimePowerUpScalingPathUtility.HoldChargeChargedLaserBeamPayloadPrefix,
                                                                                 resolvedValue,
                                                                                 ref config.HoldCharge.ChargedLaserBeam))
        {
            return true;
        }

        if (PlayerRuntimePowerUpBombScalingApplyUtility.TryApplyBooleanValue(payloadPath,
                                                                             resolvedValue,
                                                                             ref config.SpawnObject))
        {
            return true;
        }

        if (PlayerRuntimePowerUpImpactFrameScalingApplyUtility.TryApplyBooleanValue(payloadPath,
                                                                                    resolvedValue,
                                                                                    ref config.ImpactFrame))
        {
            return true;
        }

        if (PlayerRuntimePowerUpGhostTrailScalingApplyUtility.TryApplyBooleanValue(payloadPath,
                                                                                   resolvedValue,
                                                                                   ref config.GhostTrail))
        {
            return true;
        }

        switch (payloadPath)
        {
            case "suddenStrike.countRotationAsMovement":
                config.CountRotationAsMovement = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "suddenStrike.applyChargeMovementSlow":
                config.ApplyChargeMovementSlow = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "holdCharge.decayAfterRelease":
                config.HoldCharge.DecayAfterRelease = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "holdCharge.passiveChargeGainWhileReleased":
                config.HoldCharge.PassiveChargeGainWhileReleased = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "holdCharge.useChargedLaserBeam":
                config.HoldCharge.UseChargedLaserBeam = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "holdCharge.slowPlayerWhileCharging":
                config.HoldCharge.SlowPlayerWhileCharging = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "holdCharge.ignoreInheritedPlayerVelocityX":
                config.HoldCharge.IgnoreInheritedPlayerVelocityX = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "holdCharge.ignoreInheritedPlayerVelocityZ":
                config.HoldCharge.IgnoreInheritedPlayerVelocityZ = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "dash.grantsInvulnerability":
                config.Dash.GrantsInvulnerability = resolvedValue ? (byte)1 : (byte)0;
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
