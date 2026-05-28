using Unity.Mathematics;
using Unity.Entities;

/// <summary>
/// Applies controller Add Scaling results and copies controller baselines into mutable runtime configs.
/// </summary>
internal static class PlayerRuntimeScalingControllerFieldApplyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one numeric controller scaling result to the matching runtime controller field.
    /// </summary>
    /// <param name="fieldId">Runtime controller field identifier.</param>
    /// <param name="slotIndex">Resolved applied-element slot index for dynamic slot rewrites.</param>
    /// <param name="resolvedValue">Numeric result already evaluated from the formula.</param>
    /// <param name="runtimeMovement">Mutable runtime movement config.</param>
    /// <param name="runtimeLook">Mutable runtime look config.</param>
    /// <param name="runtimeCamera">Mutable runtime camera config.</param>
    /// <param name="runtimeShooting">Mutable runtime shooting config.</param>
    /// <param name="runtimeAppliedElementSlots">Mutable runtime applied-element slot buffer.</param>
    /// <param name="runtimeHealth">Mutable runtime health config.</param>
    public static void ApplyValue(PlayerRuntimeControllerFieldId fieldId,
                                  int slotIndex,
                                  float resolvedValue,
                                  ref PlayerRuntimeMovementConfig runtimeMovement,
                                  ref PlayerRuntimeLookConfig runtimeLook,
                                  ref PlayerRuntimeCameraConfig runtimeCamera,
                                  ref PlayerRuntimeShootingConfig runtimeShooting,
                                  DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlots,
                                  ref PlayerRuntimeHealthStatisticsConfig runtimeHealth)
    {
        if (PlayerRuntimeScalingControllerElementFieldUtility.TryApplyField(fieldId,
                                                                           slotIndex,
                                                                           resolvedValue,
                                                                           ref runtimeShooting,
                                                                           runtimeAppliedElementSlots))
            return;

        switch (fieldId)
        {
            case PlayerRuntimeControllerFieldId.MovementDiscreteDirectionCount:
                runtimeMovement.DiscreteDirectionCount = math.max(1, (int)resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.MovementDirectionsMode:
                runtimeMovement.DirectionsMode = PlayerRuntimeScalingEnumUtility.ResolveMovementDirectionsMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.MovementDirectionOffsetDegrees:
                runtimeMovement.DirectionOffsetDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementBaseSpeed:
                runtimeMovement.Values.BaseSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementMaxSpeed:
                runtimeMovement.Values.MaxSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementAcceleration:
                runtimeMovement.Values.Acceleration = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementDeceleration:
                runtimeMovement.Values.Deceleration = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementOppositeDirectionBrakeMultiplier:
                runtimeMovement.Values.OppositeDirectionBrakeMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementWallBounceCoefficient:
                runtimeMovement.Values.WallBounceCoefficient = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementWallCollisionSkinWidth:
                runtimeMovement.Values.WallCollisionSkinWidth = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementInputDeadZone:
                runtimeMovement.Values.InputDeadZone = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementDigitalReleaseGraceSeconds:
                runtimeMovement.Values.DigitalReleaseGraceSeconds = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementReference:
                runtimeMovement.MovementReference = PlayerRuntimeScalingEnumUtility.ResolveReferenceFrame(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.LookDirectionsMode:
                runtimeLook.DirectionsMode = PlayerRuntimeScalingEnumUtility.ResolveLookDirectionsMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.LookDiscreteDirectionCount:
                runtimeLook.DiscreteDirectionCount = math.max(1, (int)resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.LookDirectionOffsetDegrees:
                runtimeLook.DirectionOffsetDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRotationMode:
                runtimeLook.RotationMode = PlayerRuntimeScalingEnumUtility.ResolveRotationMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.LookRotationSpeed:
                runtimeLook.RotationSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookFrontConeAngleDegrees:
                runtimeLook.FrontCone.AngleDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookFrontConeMaxSpeedMultiplier:
                runtimeLook.FrontCone.MaxSpeedMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookFrontConeAccelerationMultiplier:
                runtimeLook.FrontCone.AccelerationMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookBackConeAngleDegrees:
                runtimeLook.BackCone.AngleDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookBackConeMaxSpeedMultiplier:
                runtimeLook.BackCone.MaxSpeedMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookBackConeAccelerationMultiplier:
                runtimeLook.BackCone.AccelerationMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookLeftConeAngleDegrees:
                runtimeLook.LeftCone.AngleDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookLeftConeMaxSpeedMultiplier:
                runtimeLook.LeftCone.MaxSpeedMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookLeftConeAccelerationMultiplier:
                runtimeLook.LeftCone.AccelerationMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRightConeAngleDegrees:
                runtimeLook.RightCone.AngleDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRightConeMaxSpeedMultiplier:
                runtimeLook.RightCone.MaxSpeedMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRightConeAccelerationMultiplier:
                runtimeLook.RightCone.AccelerationMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRotationDamping:
                runtimeLook.Values.RotationDamping = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRotationMaxSpeed:
                runtimeLook.Values.RotationMaxSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRotationDeadZone:
                runtimeLook.Values.RotationDeadZone = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookDigitalReleaseGraceSeconds:
                runtimeLook.Values.DigitalReleaseGraceSeconds = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookMultiplierSampling:
                runtimeLook.MultiplierSampling = PlayerRuntimeScalingEnumUtility.ResolveLookMultiplierSampling(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.CameraBehavior:
                runtimeCamera.Behavior = PlayerRuntimeScalingEnumUtility.ResolveCameraBehavior(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.CameraFollowOffsetX:
                runtimeCamera.FollowOffset.x = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraFollowOffsetY:
                runtimeCamera.FollowOffset.y = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraFollowOffsetZ:
                runtimeCamera.FollowOffset.z = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraSmoothTime:
                runtimeCamera.Values.SmoothTime = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.CameraMaxFollowDistance:
                runtimeCamera.Values.MaxFollowDistance = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraDeadZoneRadius:
                runtimeCamera.Values.DeadZoneRadius = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraShakeDuration:
                runtimeCamera.Shake.DurationSeconds = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.CameraShakePositionalAmplitude:
                runtimeCamera.Shake.PositionalAmplitude = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.CameraShakeRotationalAmplitude:
                runtimeCamera.Shake.RotationalAmplitude = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.CameraShakeFrequency:
                runtimeCamera.Shake.Frequency = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.CameraShakeDamageForFullStrength:
                runtimeCamera.Shake.DamageForFullStrength = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.CameraShakeFalloffMode:
                runtimeCamera.Shake.Falloff = PlayerRuntimeScalingEnumUtility.ResolveCameraShakeFalloff(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingTriggerMode:
                runtimeShooting.TriggerMode = PlayerRuntimeScalingEnumUtility.ResolveShootingTriggerMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingShootOffsetX:
                runtimeShooting.ShootOffset.x = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingShootOffsetY:
                runtimeShooting.ShootOffset.y = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingShootOffsetZ:
                runtimeShooting.ShootOffset.z = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingShootSpeed:
                runtimeShooting.Values.ShootSpeed = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingRateOfFire:
                runtimeShooting.Values.RateOfFire = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingProjectileSizeMultiplier:
                runtimeShooting.Values.ProjectileSizeMultiplier = math.max(0.01f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingExplosionRadius:
                runtimeShooting.Values.ExplosionRadius = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingRange:
                runtimeShooting.Values.Range = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingLifetime:
                runtimeShooting.Values.Lifetime = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingDamage:
                runtimeShooting.Values.Damage = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingAppliedElement:
                PlayerElementBulletSettingsUtility.SetAppliedElementAt(runtimeAppliedElementSlots,
                                                                      0,
                                                                      PlayerRuntimeScalingEnumUtility.ResolvePlayerProjectileAppliedElement(resolvedValue));
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletEffectKind:
                runtimeShooting.Values.ElementBehaviours.Fire.EffectKind = PlayerRuntimeScalingEnumUtility.ResolveElementalEffectKind(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletProcMode:
                runtimeShooting.Values.ElementBehaviours.Fire.ProcMode = PlayerRuntimeScalingEnumUtility.ResolveElementalProcMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletReapplyMode:
                runtimeShooting.Values.ElementBehaviours.Fire.ReapplyMode = PlayerRuntimeScalingEnumUtility.ResolveElementalProcReapplyMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletStacksPerHit:
                runtimeShooting.Values.ElementBehaviours.Fire.StacksPerHit = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletProcThresholdStacks:
                runtimeShooting.Values.ElementBehaviours.Fire.ProcThresholdStacks = math.max(0.1f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletMaximumStacks:
                runtimeShooting.Values.ElementBehaviours.Fire.MaximumStacks = math.max(0.1f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletStackDecayPerSecond:
                runtimeShooting.Values.ElementBehaviours.Fire.StackDecayPerSecond = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletDotDamagePerTick:
                runtimeShooting.Values.ElementBehaviours.Fire.DotDamagePerTick = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletDotTickInterval:
                runtimeShooting.Values.ElementBehaviours.Fire.DotTickInterval = math.max(0.01f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletDotDurationSeconds:
                runtimeShooting.Values.ElementBehaviours.Fire.DotDurationSeconds = math.max(0.05f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletImpedimentSlowPercentPerStack:
                runtimeShooting.Values.ElementBehaviours.Fire.ImpedimentSlowPercentPerStack = math.clamp(resolvedValue, 0f, 100f);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletImpedimentProcSlowPercent:
                runtimeShooting.Values.ElementBehaviours.Fire.ImpedimentProcSlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletImpedimentMaxSlowPercent:
                runtimeShooting.Values.ElementBehaviours.Fire.ImpedimentMaxSlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletImpedimentDurationSeconds:
                runtimeShooting.Values.ElementBehaviours.Fire.ImpedimentDurationSeconds = math.max(0.05f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingPenetrationMode:
                runtimeShooting.Values.PenetrationMode = PlayerRuntimeScalingEnumUtility.ResolveProjectilePenetrationMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingMaxPenetrations:
                runtimeShooting.Values.MaxPenetrations = math.max(0, (int)resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingKnockbackStrength:
                runtimeShooting.Values.Knockback.Strength = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingKnockbackDurationSeconds:
                runtimeShooting.Values.Knockback.DurationSeconds = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingKnockbackDirectionMode:
                runtimeShooting.Values.Knockback.DirectionMode = PlayerRuntimeScalingEnumUtility.ResolveProjectileKnockbackDirectionMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingKnockbackStackingMode:
                runtimeShooting.Values.Knockback.StackingMode = PlayerRuntimeScalingEnumUtility.ResolveProjectileKnockbackStackingMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.HealthMaxHealth:
                runtimeHealth.MaxHealth = math.max(1f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.HealthMaxHealthAdjustmentMode:
                runtimeHealth.MaxHealthAdjustmentMode = PlayerRuntimeScalingEnumUtility.ResolvePlayerMaxStatAdjustmentMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.HealthMaxShield:
                runtimeHealth.MaxShield = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.HealthMaxShieldAdjustmentMode:
                runtimeHealth.MaxShieldAdjustmentMode = PlayerRuntimeScalingEnumUtility.ResolvePlayerMaxStatAdjustmentMode(resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.HealthGraceTimeSeconds:
                runtimeHealth.GraceTimeSeconds = math.max(0f, resolvedValue);
                return;
        }
    }

    /// <summary>
    /// Applies one boolean controller scaling result to the matching runtime controller field.
    /// </summary>
    /// <param name="fieldId">Runtime controller field identifier.</param>
    /// <param name="resolvedValue">Boolean result already evaluated from the formula.</param>
    /// <param name="runtimeMovement">Mutable runtime movement config.</param>
    /// <param name="runtimeLook">Mutable runtime look config.</param>
    /// <param name="runtimeCamera">Mutable runtime camera config.</param>
    /// <param name="runtimeShooting">Mutable runtime shooting config.</param>
    /// <param name="runtimeHealth">Mutable runtime health config.</param>
    public static void ApplyBooleanValue(PlayerRuntimeControllerFieldId fieldId,
                                         bool resolvedValue,
                                         ref PlayerRuntimeMovementConfig runtimeMovement,
                                         ref PlayerRuntimeLookConfig runtimeLook,
                                         ref PlayerRuntimeCameraConfig runtimeCamera,
                                         ref PlayerRuntimeShootingConfig runtimeShooting,
                                         ref PlayerRuntimeHealthStatisticsConfig runtimeHealth)
    {
        if (PlayerRuntimeScalingControllerElementFieldUtility.TryApplyBooleanField(fieldId, resolvedValue, ref runtimeShooting))
            return;

        switch (fieldId)
        {
            case PlayerRuntimeControllerFieldId.LookFrontConeEnabled:
                runtimeLook.FrontCone.Enabled = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookBackConeEnabled:
                runtimeLook.BackCone.Enabled = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookLeftConeEnabled:
                runtimeLook.LeftCone.Enabled = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRightConeEnabled:
                runtimeLook.RightCone.Enabled = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingProjectilesInheritPlayerSpeed:
                runtimeShooting.ProjectilesInheritPlayerSpeed = resolvedValue ? (byte)1 : (byte)0;
                return;
            case PlayerRuntimeControllerFieldId.ShootingElementBulletConsumeStacksOnProc:
                runtimeShooting.Values.ElementBehaviours.Fire.ConsumeStacksOnProc = resolvedValue ? (byte)1 : (byte)0;
                return;
            case PlayerRuntimeControllerFieldId.ShootingKnockbackEnabled:
                runtimeShooting.Values.Knockback.Enabled = resolvedValue ? (byte)1 : (byte)0;
                return;
            case PlayerRuntimeControllerFieldId.CameraShakeEnabled:
                runtimeCamera.Shake.Enabled = resolvedValue ? (byte)1 : (byte)0;
                return;
            case PlayerRuntimeControllerFieldId.CameraShakeScaleWithDamage:
                runtimeCamera.Shake.ScaleWithDamage = resolvedValue ? (byte)1 : (byte)0;
                return;
        }
    }

    /// <summary>
    /// Copies the immutable movement baseline into the mutable runtime movement config.
    /// </summary>
    /// <param name="baseMovement">Immutable baseline movement config.</param>
    /// <returns>Runtime movement config copied from the baseline.</returns>
    public static PlayerRuntimeMovementConfig CopyMovement(in PlayerBaseMovementConfig baseMovement)
    {
        return new PlayerRuntimeMovementConfig
        {
            DirectionsMode = baseMovement.DirectionsMode,
            DiscreteDirectionCount = baseMovement.DiscreteDirectionCount,
            DirectionOffsetDegrees = baseMovement.DirectionOffsetDegrees,
            MovementReference = baseMovement.MovementReference,
            Values = baseMovement.Values
        };
    }

    /// <summary>
    /// Copies the immutable look baseline into the mutable runtime look config.
    /// </summary>
    /// <param name="baseLook">Immutable baseline look config.</param>
    /// <returns>Runtime look config copied from the baseline.</returns>
    public static PlayerRuntimeLookConfig CopyLook(in PlayerBaseLookConfig baseLook)
    {
        return new PlayerRuntimeLookConfig
        {
            DirectionsMode = baseLook.DirectionsMode,
            DiscreteDirectionCount = baseLook.DiscreteDirectionCount,
            DirectionOffsetDegrees = baseLook.DirectionOffsetDegrees,
            RotationMode = baseLook.RotationMode,
            RotationSpeed = baseLook.RotationSpeed,
            MultiplierSampling = baseLook.MultiplierSampling,
            FrontCone = baseLook.FrontCone,
            BackCone = baseLook.BackCone,
            LeftCone = baseLook.LeftCone,
            RightCone = baseLook.RightCone,
            Values = baseLook.Values
        };
    }

    /// <summary>
    /// Copies the immutable camera baseline into the mutable runtime camera config.
    /// </summary>
    /// <param name="baseCamera">Immutable baseline camera config.</param>
    /// <returns>Runtime camera config copied from the baseline.</returns>
    public static PlayerRuntimeCameraConfig CopyCamera(in PlayerBaseCameraConfig baseCamera)
    {
        return new PlayerRuntimeCameraConfig
        {
            Behavior = baseCamera.Behavior,
            FollowOffset = baseCamera.FollowOffset,
            Values = baseCamera.Values,
            Shake = baseCamera.Shake
        };
    }

    /// <summary>
    /// Copies the immutable shooting baseline into the mutable runtime shooting config.
    /// </summary>
    /// <param name="baseShooting">Immutable baseline shooting config.</param>
    /// <returns>Runtime shooting config copied from the baseline.</returns>
    public static PlayerRuntimeShootingConfig CopyShooting(in PlayerBaseShootingConfig baseShooting)
    {
        return new PlayerRuntimeShootingConfig
        {
            TriggerMode = baseShooting.TriggerMode,
            ProjectilesInheritPlayerSpeed = baseShooting.ProjectilesInheritPlayerSpeed,
            ShootOffset = baseShooting.ShootOffset,
            Values = baseShooting.Values
        };
    }

    /// <summary>
    /// Copies the immutable health baseline into the mutable runtime health config.
    /// </summary>
    /// <param name="baseHealth">Immutable baseline health config.</param>
    /// <returns>Runtime health config copied from the baseline.</returns>
    public static PlayerRuntimeHealthStatisticsConfig CopyHealth(in PlayerBaseHealthStatisticsConfig baseHealth)
    {
        return new PlayerRuntimeHealthStatisticsConfig
        {
            MaxHealth = baseHealth.MaxHealth,
            MaxHealthAdjustmentMode = baseHealth.MaxHealthAdjustmentMode,
            MaxShield = baseHealth.MaxShield,
            MaxShieldAdjustmentMode = baseHealth.MaxShieldAdjustmentMode,
            GraceTimeSeconds = baseHealth.GraceTimeSeconds
        };
    }
    #endregion

    #endregion
}
