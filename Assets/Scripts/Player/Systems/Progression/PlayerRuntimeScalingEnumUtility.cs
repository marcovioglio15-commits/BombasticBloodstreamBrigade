using Unity.Mathematics;

/// <summary>
/// Centralizes non-reflection enum resolution used by runtime Add Scaling application.
/// </summary>
internal static class PlayerRuntimeScalingEnumUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a safe MovementDirectionsMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static MovementDirectionsMode ResolveMovementDirectionsMode(float value)
    {
        return (MovementDirectionsMode)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe ReferenceFrame from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ReferenceFrame ResolveReferenceFrame(float value)
    {
        return (ReferenceFrame)ResolveEnumIndex(value, 2);
    }

    /// <summary>
    /// Resolves a safe LookDirectionsMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static LookDirectionsMode ResolveLookDirectionsMode(float value)
    {
        return (LookDirectionsMode)ResolveEnumIndex(value, 3);
    }

    /// <summary>
    /// Resolves a safe RotationMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static RotationMode ResolveRotationMode(float value)
    {
        return (RotationMode)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe LookMultiplierSampling from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static LookMultiplierSampling ResolveLookMultiplierSampling(float value)
    {
        return (LookMultiplierSampling)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe CameraBehavior from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static CameraBehavior ResolveCameraBehavior(float value)
    {
        return (CameraBehavior)ResolveEnumIndex(value, 3);
    }

    /// <summary>
    /// Resolves a safe ShootingTriggerMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ShootingTriggerMode ResolveShootingTriggerMode(float value)
    {
        return (ShootingTriggerMode)ResolveEnumIndex(value, 2);
    }

    /// <summary>
    /// Resolves a safe PlayerProjectileAppliedElement from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static PlayerProjectileAppliedElement ResolvePlayerProjectileAppliedElement(float value)
    {
        return (PlayerProjectileAppliedElement)ResolveEnumIndex(value, 4);
    }

    /// <summary>
    /// Resolves a safe PlayerMaxStatAdjustmentMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static PlayerMaxStatAdjustmentMode ResolvePlayerMaxStatAdjustmentMode(float value)
    {
        return (PlayerMaxStatAdjustmentMode)ResolveEnumIndex(value, 2);
    }

    /// <summary>
    /// Resolves a safe ProjectilePenetrationMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ProjectilePenetrationMode ResolveProjectilePenetrationMode(float value)
    {
        return (ProjectilePenetrationMode)ResolveEnumIndex(value, 3);
    }

    /// <summary>
    /// Resolves a safe ProjectileKnockbackDirectionMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ProjectileKnockbackDirectionMode ResolveProjectileKnockbackDirectionMode(float value)
    {
        return (ProjectileKnockbackDirectionMode)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe ProjectileKnockbackStackingMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ProjectileKnockbackStackingMode ResolveProjectileKnockbackStackingMode(float value)
    {
        return (ProjectileKnockbackStackingMode)ResolveEnumIndex(value, 2);
    }

    /// <summary>
    /// Resolves a safe ElementalEffectKind from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ElementalEffectKind ResolveElementalEffectKind(float value)
    {
        return (ElementalEffectKind)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe ElementType from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ElementType ResolveElementType(float value)
    {
        return (ElementType)ResolveEnumIndex(value, 3);
    }

    /// <summary>
    /// Resolves a safe ElementalProcMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ElementalProcMode ResolveElementalProcMode(float value)
    {
        return (ElementalProcMode)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe ElementalProcReapplyMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ElementalProcReapplyMode ResolveElementalProcReapplyMode(float value)
    {
        return (ElementalProcReapplyMode)ResolveEnumIndex(value, 2);
    }

    /// <summary>
    /// Resolves a safe PowerUpResourceType from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static PowerUpResourceType ResolvePowerUpResourceType(float value)
    {
        return (PowerUpResourceType)ResolveEnumIndex(value, 3);
    }

    /// <summary>
    /// Resolves a safe PowerUpChargeType from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static PowerUpChargeType ResolvePowerUpChargeType(float value)
    {
        return (PowerUpChargeType)ResolveEnumIndex(value, 5);
    }

    /// <summary>
    /// Resolves a safe DashDirectionMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static DashDirectionMode ResolveDashDirectionMode(float value)
    {
        return (DashDirectionMode)ResolveEnumIndex(value, 3);
    }

    /// <summary>
    /// Resolves a safe SpawnOffsetOrientationMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static SpawnOffsetOrientationMode ResolveSpawnOffsetOrientationMode(float value)
    {
        return (SpawnOffsetOrientationMode)ResolveEnumIndex(value, 2);
    }

    /// <summary>
    /// Resolves a safe BombVelocityDirectionMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static BombVelocityDirectionMode ResolveBombVelocityDirectionMode(float value)
    {
        return (BombVelocityDirectionMode)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe PowerUpHealApplicationMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static PowerUpHealApplicationMode ResolvePowerUpHealApplicationMode(float value)
    {
        return (PowerUpHealApplicationMode)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe PlayerComboDamageBreakMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static PlayerComboDamageBreakMode ResolveComboDamageBreakMode(float value)
    {
        return (PlayerComboDamageBreakMode)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe ProjectileOrbitPathMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static ProjectileOrbitPathMode ResolveProjectileOrbitPathMode(float value)
    {
        return (ProjectileOrbitPathMode)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe OrbitalProjectionMotionMode from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static OrbitalProjectionMotionMode ResolveOrbitalProjectionMotionMode(float value)
    {
        return (OrbitalProjectionMotionMode)ResolveEnumIndex(value, 2);
    }

    /// <summary>
    /// Resolves a safe OrbitalProjectionFullOrbitConeResponse from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static OrbitalProjectionFullOrbitConeResponse ResolveOrbitalProjectionFullOrbitConeResponse(float value)
    {
        return (OrbitalProjectionFullOrbitConeResponse)ResolveEnumIndex(value, 1);
    }

    /// <summary>
    /// Resolves a safe OrbitalProjectionAcquisitionPolicy from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static OrbitalProjectionAcquisitionPolicy ResolveOrbitalProjectionAcquisitionPolicy(float value)
    {
        return (OrbitalProjectionAcquisitionPolicy)ResolveEnumIndex(value, 2);
    }

    /// <summary>
    /// Resolves a safe LaserBeamBodyProfile from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static LaserBeamBodyProfile ResolveLaserBeamBodyProfile(float value)
    {
        return (LaserBeamBodyProfile)ResolveEnumIndex(value, 2);
    }

    /// <summary>
    /// Resolves a safe LaserBeamCapShape from one numeric Add Scaling result.
    /// </summary>
    /// <param name="value">Resolved numeric formula result.</param>
    /// <returns>Clamped enum value.</returns>
    public static LaserBeamCapShape ResolveLaserBeamCapShape(float value)
    {
        return (LaserBeamCapShape)ResolveEnumIndex(value, 2);
    }
    #endregion

    #region Private Methods
    private static int ResolveEnumIndex(float value, int maximumValue)
    {
        return math.clamp((int)math.round(value), 0, math.max(0, maximumValue));
    }
    #endregion

    #endregion
}
