using Unity.Mathematics;

/// <summary>
/// Applies runtime Add Scaling payload values that target Bomb active-tool settings.
/// </summary>
internal static class PlayerRuntimePowerUpBombScalingApplyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one numeric or enum-like Add Scaling result to a Bomb runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="bombConfig">Mutable Bomb config rebuilt from immutable baselines.</param>
    /// <returns>True when the payload path targeted a Bomb field.</returns>
    public static bool TryApplyValue(string payloadPath, float resolvedValue, ref BombPowerUpConfig bombConfig)
    {
        switch (payloadPath)
        {
            case "bomb.spawnOffset.x":
                bombConfig.SpawnOffset.x = resolvedValue;
                return true;
            case "bomb.spawnOffset.y":
                bombConfig.SpawnOffset.y = resolvedValue;
                return true;
            case "bomb.spawnOffset.z":
                bombConfig.SpawnOffset.z = resolvedValue;
                return true;
            case "bomb.spawnOffsetOrientation":
                bombConfig.SpawnOffsetOrientation = PlayerRuntimeScalingEnumUtility.ResolveSpawnOffsetOrientationMode(resolvedValue);
                return true;
            case "bomb.deploySpeed":
                bombConfig.DeploySpeed = math.max(0f, resolvedValue);
                return true;
            case "bomb.velocityDirection":
                bombConfig.VelocityDirection = PlayerRuntimeScalingEnumUtility.ResolveBombVelocityDirectionMode(resolvedValue);
                return true;
            case "bomb.collisionRadius":
                bombConfig.CollisionRadius = math.max(0.01f, resolvedValue);
                return true;
            case "bomb.bounceDamping":
                bombConfig.BounceDamping = math.clamp(resolvedValue, 0f, 1f);
                return true;
            case "bomb.linearDampingPerSecond":
                bombConfig.LinearDampingPerSecond = math.max(0f, resolvedValue);
                return true;
            case "bomb.fuseSeconds":
                bombConfig.FuseSeconds = math.max(0.05f, resolvedValue);
                return true;
            case "bomb.radius":
                bombConfig.Radius = math.max(0.1f, resolvedValue);
                bombConfig.EnableDamagePayload = bombConfig.Radius > 0f || bombConfig.Damage > 0f ? (byte)1 : (byte)0;
                return true;
            case "bomb.damage":
                bombConfig.Damage = math.max(0f, resolvedValue);
                bombConfig.EnableDamagePayload = bombConfig.Radius > 0f || bombConfig.Damage > 0f ? (byte)1 : (byte)0;
                return true;
            case "bomb.vfxScaleMultiplier":
                bombConfig.VfxScaleMultiplier = math.max(0.01f, resolvedValue);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies one boolean Add Scaling result to a Bomb runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="bombConfig">Mutable Bomb config rebuilt from immutable baselines.</param>
    /// <returns>True when the payload path targeted a Bomb boolean field.</returns>
    public static bool TryApplyBooleanValue(string payloadPath, bool resolvedValue, ref BombPowerUpConfig bombConfig)
    {
        switch (payloadPath)
        {
            case "bomb.bounceOnWalls":
                bombConfig.BounceOnWalls = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "bomb.affectAllEnemiesInRadius":
                bombConfig.AffectAllEnemiesInRadius = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "bomb.scaleVfxToRadius":
                bombConfig.ScaleVfxToRadius = resolvedValue ? (byte)1 : (byte)0;
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
