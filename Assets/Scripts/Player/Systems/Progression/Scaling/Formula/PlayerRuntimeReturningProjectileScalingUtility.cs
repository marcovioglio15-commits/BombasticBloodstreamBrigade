using Unity.Mathematics;

/// <summary>
/// Applies typed unified-formula results to returning-projectile unmanaged configurations.
/// </summary>
public static class PlayerRuntimeReturningProjectileScalingUtility
{
    #region Methods

    #region Numeric and Enum Values
    /// <summary>
    /// Applies one numeric or enum Add Scaling result to a returning-projectile config.
    /// </summary>
    /// <param name="payloadPath">Full modular payload path carried by the scaling rule.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="config">Mutable returning-projectile config rebuilt from immutable baseline data.</param>
    /// <returns>True when the path matched a supported returning-projectile field.</returns>
    public static bool TryApplyValue(string payloadPath,
                                     float resolvedValue,
                                     ref ReturningProjectilesConfig config)
    {
        switch (payloadPath)
        {
            case "returningProjectiles.returnPathMode":
                config.ReturnPathMode = PlayerRuntimeScalingEnumUtility.ResolveProjectileReturnPathMode(resolvedValue);
                return true;
            case "returningProjectiles.returnSpeedMultiplier":
                config.ReturnSpeedMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "returningProjectiles.outboundRangeMultiplier":
                config.OutboundRangeMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "returningProjectiles.outboundLifetimeMultiplier":
                config.OutboundLifetimeMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "returningProjectiles.outboundHitPolicy":
                config.OutboundHitPolicy = PlayerRuntimeScalingEnumUtility.ResolveProjectileOutboundHitPolicy(resolvedValue);
                return true;
            case "returningProjectiles.additionalOutboundHits":
                config.AdditionalOutboundHits = math.max(1, (int)math.round(resolvedValue));
                return true;
            case "returningProjectiles.returnStartMode":
                config.ReturnStartMode = PlayerRuntimeScalingEnumUtility.ResolveProjectileReturnStartMode(resolvedValue);
                return true;
            case "returningProjectiles.returnDelaySeconds":
                config.ReturnDelaySeconds = math.max(0f, resolvedValue);
                return true;
            case "returningProjectiles.returnRumbleMultiplier":
                config.ReturnRumbleMultiplier = math.max(0f, resolvedValue);
                return true;
            case "returningProjectiles.returnCameraShakeMultiplier":
                config.ReturnCameraShakeMultiplier = math.max(0f, resolvedValue);
                return true;
            case "returningProjectiles.outboundSizeMultiplier":
                config.OutboundSizeMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "returningProjectiles.returnSizeMultiplier":
                config.ReturnSizeMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "returningProjectiles.spinSpeedDegreesPerSecond":
                config.SpinSpeedDegreesPerSecond = math.max(0f, resolvedValue);
                return true;
            case "returningProjectiles.spinAxis":
                config.SpinAxis = PlayerRuntimeScalingEnumUtility.ResolveProjectileReturnRotationAxis(resolvedValue);
                return true;
            case "returningProjectiles.turnaroundRotationSpeedDegreesPerSecond":
                config.TurnaroundRotationSpeedDegreesPerSecond = math.max(0.01f, resolvedValue);
                return true;
            case "returningProjectiles.turnaroundAxis":
                config.TurnaroundAxis = PlayerRuntimeScalingEnumUtility.ResolveProjectileReturnRotationAxis(resolvedValue);
                return true;
            case "returningProjectiles.returnHitPolicy":
                config.ReturnHitPolicy = PlayerRuntimeScalingEnumUtility.ResolveProjectileReturnHitPolicy(resolvedValue);
                return true;
            case "returningProjectiles.additionalReturnHits":
                config.AdditionalReturnHits = math.max(1, (int)math.round(resolvedValue));
                return true;
            case "returningProjectiles.repeatedContactDamage":
                config.RepeatedContactDamage = math.max(0f, resolvedValue);
                return true;
            case "returningProjectiles.repeatedContactDamageIntervalSeconds":
                config.RepeatedContactDamageIntervalSeconds = math.max(0.01f, resolvedValue);
                return true;
            case "returningProjectiles.pathSampleDistance":
                config.PathSampleDistance = math.max(0.01f, resolvedValue);
                return true;
            case "returningProjectiles.returnCompletionDistance":
                config.ReturnCompletionDistance = math.max(0.01f, resolvedValue);
                return true;
            default:
                return false;
        }
    }
    #endregion

    #region Boolean Values
    /// <summary>
    /// Applies one Boolean Add Scaling result to a returning-projectile config.
    /// </summary>
    /// <param name="payloadPath">Full modular payload path carried by the scaling rule.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="config">Mutable returning-projectile config rebuilt from immutable baseline data.</param>
    /// <returns>True when the path matched a supported returning-projectile field.</returns>
    public static bool TryApplyBooleanValue(string payloadPath,
                                            bool resolvedValue,
                                            ref ReturningProjectilesConfig config)
    {
        byte byteValue = resolvedValue ? (byte)1 : (byte)0;

        switch (payloadPath)
        {
            case "returningProjectiles.keepProjectileVfx":
                config.KeepProjectileVfx = byteValue;
                return true;
            case "returningProjectiles.keepMuzzleFlashVfx":
                config.KeepMuzzleFlashVfx = byteValue;
                return true;
            case "returningProjectiles.keepHitVfx":
                config.KeepHitVfx = byteValue;
                return true;
            case "returningProjectiles.keepDeathVfx":
                config.KeepDeathVfx = byteValue;
                return true;
            case "returningProjectiles.spinDuringFlight":
                config.SpinDuringFlight = byteValue;
                return true;
            case "returningProjectiles.allowOtherPowerUpInteractions":
                config.AllowOtherPowerUpInteractions = byteValue;
                return true;
            case "returningProjectiles.enableProjectileSplitting":
                config.EnableProjectileSplitting = byteValue;
                return true;
            case "returningProjectiles.applyToSplitProjectiles":
                config.ApplyToSplitProjectiles = byteValue;
                return true;
            case "returningProjectiles.completeBouncesBeforeReturn":
                config.CompleteBouncesBeforeReturn = byteValue;
                return true;
            case "returningProjectiles.completeOrbitalPathBeforeReturn":
                config.CompleteOrbitalPathBeforeReturn = byteValue;
                return true;
            case "returningProjectiles.applyTinyMegaProjectileScaling":
                config.ApplyTinyMegaProjectileScaling = byteValue;
                return true;
            case "returningProjectiles.applyToActivePowerUpProjectiles":
                config.ApplyToActivePowerUpProjectiles = byteValue;
                return true;
            case "returningProjectiles.allowConcurrentActiveProjectiles":
                config.AllowConcurrentActiveProjectiles = byteValue;
                return true;
            case "returningProjectiles.allowEarlyActivationRecall":
                config.AllowEarlyActivationRecall = byteValue;
                return true;
            case "returningProjectiles.reapplyResourceGateCostOnRecall":
                config.ReapplyResourceGateCostOnRecall = byteValue;
                return true;
            case "returningProjectiles.enableRepeatedContactDamage":
                config.EnableRepeatedContactDamage = byteValue;
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
