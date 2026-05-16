using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds controller baselines and controller-specific Add Scaling metadata used by runtime ECS resynchronization.
/// </summary>
internal static class PlayerRuntimeScalingControllerBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the immutable baseline movement config from one authoring preset.
    /// </summary>
    /// <param name="preset">Source controller preset.</param>
    /// <returns>Sanitized runtime baseline movement config.</returns>
    public static PlayerBaseMovementConfig BuildBaseMovementConfig(PlayerControllerPreset preset)
    {
        MovementSettings movementSettings = preset != null ? preset.MovementSettings : null;

        if (movementSettings == null)
            movementSettings = new MovementSettings();

        return new PlayerBaseMovementConfig
        {
            DirectionsMode = movementSettings.DirectionsMode,
            DiscreteDirectionCount = math.max(1, movementSettings.DiscreteDirectionCount),
            DirectionOffsetDegrees = movementSettings.DirectionOffsetDegrees,
            MovementReference = movementSettings.MovementReference,
            Values = new MovementValuesBlob
            {
                BaseSpeed = movementSettings.Values.BaseSpeed,
                MaxSpeed = movementSettings.Values.MaxSpeed,
                Acceleration = movementSettings.Values.Acceleration,
                Deceleration = movementSettings.Values.Deceleration,
                OppositeDirectionBrakeMultiplier = movementSettings.Values.OppositeDirectionBrakeMultiplier,
                WallBounceCoefficient = movementSettings.Values.WallBounceCoefficient,
                WallCollisionSkinWidth = movementSettings.Values.WallCollisionSkinWidth,
                InputDeadZone = movementSettings.Values.InputDeadZone,
                DigitalReleaseGraceSeconds = movementSettings.Values.DigitalReleaseGraceSeconds
            }
        };
    }

    /// <summary>
    /// Builds the immutable baseline look config from one authoring preset.
    /// </summary>
    /// <param name="preset">Source controller preset.</param>
    /// <returns>Sanitized runtime baseline look config.</returns>
    public static PlayerBaseLookConfig BuildBaseLookConfig(PlayerControllerPreset preset)
    {
        LookSettings lookSettings = preset != null ? preset.LookSettings : null;

        if (lookSettings == null)
            lookSettings = new LookSettings();

        return new PlayerBaseLookConfig
        {
            DirectionsMode = lookSettings.DirectionsMode,
            DiscreteDirectionCount = math.max(1, lookSettings.DiscreteDirectionCount),
            DirectionOffsetDegrees = lookSettings.DirectionOffsetDegrees,
            RotationMode = lookSettings.RotationMode,
            RotationSpeed = lookSettings.RotationSpeed,
            MultiplierSampling = lookSettings.MultiplierSampling,
            FrontCone = new ConeConfig
            {
                Enabled = lookSettings.FrontConeEnabled,
                AngleDegrees = lookSettings.FrontConeAngle,
                MaxSpeedMultiplier = lookSettings.FrontConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.FrontConeAccelerationMultiplier
            },
            BackCone = new ConeConfig
            {
                Enabled = lookSettings.BackConeEnabled,
                AngleDegrees = lookSettings.BackConeAngle,
                MaxSpeedMultiplier = lookSettings.BackConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.BackConeAccelerationMultiplier
            },
            LeftCone = new ConeConfig
            {
                Enabled = lookSettings.LeftConeEnabled,
                AngleDegrees = lookSettings.LeftConeAngle,
                MaxSpeedMultiplier = lookSettings.LeftConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.LeftConeAccelerationMultiplier
            },
            RightCone = new ConeConfig
            {
                Enabled = lookSettings.RightConeEnabled,
                AngleDegrees = lookSettings.RightConeAngle,
                MaxSpeedMultiplier = lookSettings.RightConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.RightConeAccelerationMultiplier
            },
            Values = new LookValuesBlob
            {
                RotationDamping = lookSettings.Values.RotationDamping,
                RotationMaxSpeed = lookSettings.Values.RotationMaxSpeed,
                RotationDeadZone = lookSettings.Values.RotationDeadZone,
                DigitalReleaseGraceSeconds = lookSettings.Values.DigitalReleaseGraceSeconds
            }
        };
    }

    /// <summary>
    /// Builds the immutable baseline camera config from one authoring preset.
    /// </summary>
    /// <param name="preset">Source controller preset.</param>
    /// <returns>Sanitized runtime baseline camera config.</returns>
    public static PlayerBaseCameraConfig BuildBaseCameraConfig(PlayerControllerPreset preset)
    {
        CameraSettings cameraSettings = preset != null ? preset.CameraSettings : null;

        if (cameraSettings == null)
            cameraSettings = new CameraSettings();

        Vector3 followOffset = cameraSettings.FollowOffset;
        return new PlayerBaseCameraConfig
        {
            Behavior = cameraSettings.Behavior,
            FollowOffset = new float3(followOffset.x, followOffset.y, followOffset.z),
            Values = new CameraValuesBlob
            {
                FollowSpeed = cameraSettings.Values.FollowSpeed,
                CameraLag = cameraSettings.Values.CameraLag,
                Damping = cameraSettings.Values.Damping,
                MaxFollowDistance = cameraSettings.Values.MaxFollowDistance,
                DeadZoneRadius = cameraSettings.Values.DeadZoneRadius
            }
        };
    }

    /// <summary>
    /// Builds the immutable baseline shooting config from one authoring preset.
    /// </summary>
    /// <param name="preset">Source controller preset.</param>
    /// <returns>Sanitized runtime baseline shooting config.</returns>
    public static PlayerBaseShootingConfig BuildBaseShootingConfig(PlayerControllerPreset preset)
    {
        ShootingSettings shootingSettings = preset != null ? preset.ShootingSettings : null;

        if (shootingSettings == null)
            shootingSettings = new ShootingSettings();

        Vector3 shootOffset = shootingSettings.ShootOffset;
        return new PlayerBaseShootingConfig
        {
            TriggerMode = shootingSettings.TriggerMode,
            ProjectilesInheritPlayerSpeed = shootingSettings.ProjectilesInheritPlayerSpeed ? (byte)1 : (byte)0,
            ShootOffset = new float3(shootOffset.x, shootOffset.y, shootOffset.z),
            Values = PlayerShootingConfigRuntimeUtility.BuildRuntimeValues(shootingSettings.Values)
        };
    }

    /// <summary>
    /// Populates immutable baseline applied-element slots from one authoring preset.
    /// </summary>
    /// <param name="preset">Source controller preset.</param>
    /// <param name="buffer">Destination immutable baseline slot buffer.</param>
    public static void PopulateBaseAppliedElementSlots(PlayerControllerPreset preset,
                                                       DynamicBuffer<PlayerBaseShootingAppliedElementSlot> buffer)
    {
        ShootingValues shootingValues = ResolveShootingValues(preset);
        PlayerElementBulletSettingsUtility.PopulateBaseAppliedElementsBuffer(shootingValues != null ? shootingValues.AppliedElements : null,
                                                                             buffer);
    }

    /// <summary>
    /// Populates mutable runtime applied-element slots from one authoring preset.
    /// </summary>
    /// <param name="preset">Source controller preset.</param>
    /// <param name="buffer">Destination mutable runtime slot buffer.</param>
    public static void PopulateRuntimeAppliedElementSlots(PlayerControllerPreset preset,
                                                          DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> buffer)
    {
        ShootingValues shootingValues = ResolveShootingValues(preset);
        PlayerElementBulletSettingsUtility.PopulateRuntimeAppliedElementsBuffer(shootingValues != null ? shootingValues.AppliedElements : null,
                                                                                buffer);
    }

    /// <summary>
    /// Builds the immutable baseline health config from one authoring preset.
    /// </summary>
    /// <param name="preset">Source controller preset.</param>
    /// <returns>Sanitized runtime baseline health config.</returns>
    public static PlayerBaseHealthStatisticsConfig BuildBaseHealthStatisticsConfig(PlayerControllerPreset preset)
    {
        PlayerHealthStatisticsSettings healthStatistics = preset != null ? preset.HealthStatistics : null;

        if (healthStatistics == null)
            healthStatistics = new PlayerHealthStatisticsSettings();

        return new PlayerBaseHealthStatisticsConfig
        {
            MaxHealth = math.max(1f, healthStatistics.MaxHealth),
            MaxHealthAdjustmentMode = healthStatistics.MaxHealthAdjustmentMode,
            MaxShield = math.max(0f, healthStatistics.MaxShield),
            MaxShieldAdjustmentMode = healthStatistics.MaxShieldAdjustmentMode,
            GraceTimeSeconds = math.max(0f, healthStatistics.GraceTimeSeconds)
        };
    }

#if UNITY_EDITOR
    /// <summary>
    /// Populates controller scaling metadata from the unscaled controller preset.
    /// </summary>
    /// <param name="sourcePreset">Unscaled source controller preset.</param>
    /// <param name="scalingBuffer">Destination scaling metadata buffer.</param>
    public static void PopulateControllerScalingMetadata(PlayerControllerPreset sourcePreset,
                                                         DynamicBuffer<PlayerRuntimeControllerScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();
        PopulateScalingMetadata(sourcePreset, sourcePreset != null ? sourcePreset.ScalingRules : null, scalingBuffer);
    }
#endif
    #endregion

    #region Private Methods
    private static ShootingValues ResolveShootingValues(PlayerControllerPreset preset)
    {
        ShootingSettings shootingSettings = preset != null ? preset.ShootingSettings : null;

        if (shootingSettings == null)
            return null;

        return shootingSettings.Values;
    }

#if UNITY_EDITOR
    private static void PopulateScalingMetadata(PlayerControllerPreset sourcePreset,
                                                IReadOnlyList<PlayerStatScalingRule> scalingRules,
                                                DynamicBuffer<PlayerRuntimeControllerScalingElement> scalingBuffer)
    {
        if (sourcePreset == null || scalingRules == null || scalingRules.Count <= 0)
            return;

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < scalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling)
                continue;

            if (string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            if (!TryMapControllerFieldId(scalingRule.StatKey, out PlayerRuntimeControllerFieldId fieldId, out int slotIndex))
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            if (!PlayerRuntimeScalingBakeUtility.TryResolveScalingBaseMetadata(property,
                                                                              out byte valueType,
                                                                              out float baseValue,
                                                                              out byte baseBooleanValue,
                                                                              out byte isInteger))
            {
                continue;
            }

            scalingBuffer.Add(new PlayerRuntimeControllerScalingElement
            {
                FieldId = fieldId,
                SlotIndex = slotIndex,
                ValueType = valueType,
                BaseValue = baseValue,
                BaseBooleanValue = baseBooleanValue,
                IsInteger = isInteger,
                Formula = new FixedString512Bytes(PlayerRuntimeScalingBakeUtility.ResolveStoredFormula(scalingRule.Formula,
                                                                                                       property,
                                                                                                       null))
            });
        }
    }

    private static bool TryMapControllerFieldId(string statKey,
                                                out PlayerRuntimeControllerFieldId fieldId,
                                                out int slotIndex)
    {
        fieldId = default;
        slotIndex = -1;
        string normalizedStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(statKey);

        if (PlayerRuntimeScalingControllerElementFieldUtility.TryMapFieldId(normalizedStatKey, out fieldId, out slotIndex))
            return true;

        switch (normalizedStatKey)
        {
            case "movementSettings.directionsMode":
                fieldId = PlayerRuntimeControllerFieldId.MovementDirectionsMode;
                return true;
            case "movementSettings.discreteDirectionCount":
                fieldId = PlayerRuntimeControllerFieldId.MovementDiscreteDirectionCount;
                return true;
            case "movementSettings.directionOffsetDegrees":
                fieldId = PlayerRuntimeControllerFieldId.MovementDirectionOffsetDegrees;
                return true;
            case "movementSettings.values.baseSpeed":
                fieldId = PlayerRuntimeControllerFieldId.MovementBaseSpeed;
                return true;
            case "movementSettings.values.maxSpeed":
                fieldId = PlayerRuntimeControllerFieldId.MovementMaxSpeed;
                return true;
            case "movementSettings.values.acceleration":
                fieldId = PlayerRuntimeControllerFieldId.MovementAcceleration;
                return true;
            case "movementSettings.values.deceleration":
                fieldId = PlayerRuntimeControllerFieldId.MovementDeceleration;
                return true;
            case "movementSettings.values.oppositeDirectionBrakeMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.MovementOppositeDirectionBrakeMultiplier;
                return true;
            case "movementSettings.values.wallBounceCoefficient":
                fieldId = PlayerRuntimeControllerFieldId.MovementWallBounceCoefficient;
                return true;
            case "movementSettings.values.wallCollisionSkinWidth":
                fieldId = PlayerRuntimeControllerFieldId.MovementWallCollisionSkinWidth;
                return true;
            case "movementSettings.values.inputDeadZone":
                fieldId = PlayerRuntimeControllerFieldId.MovementInputDeadZone;
                return true;
            case "movementSettings.values.digitalReleaseGraceSeconds":
                fieldId = PlayerRuntimeControllerFieldId.MovementDigitalReleaseGraceSeconds;
                return true;
            case "movementSettings.movementReference":
                fieldId = PlayerRuntimeControllerFieldId.MovementReference;
                return true;
            case "lookSettings.directionsMode":
                fieldId = PlayerRuntimeControllerFieldId.LookDirectionsMode;
                return true;
            case "lookSettings.discreteDirectionCount":
                fieldId = PlayerRuntimeControllerFieldId.LookDiscreteDirectionCount;
                return true;
            case "lookSettings.directionOffsetDegrees":
                fieldId = PlayerRuntimeControllerFieldId.LookDirectionOffsetDegrees;
                return true;
            case "lookSettings.rotationMode":
                fieldId = PlayerRuntimeControllerFieldId.LookRotationMode;
                return true;
            case "lookSettings.rotationSpeed":
                fieldId = PlayerRuntimeControllerFieldId.LookRotationSpeed;
                return true;
            case "lookSettings.frontConeAngle":
                fieldId = PlayerRuntimeControllerFieldId.LookFrontConeAngleDegrees;
                return true;
            case "lookSettings.frontConeEnabled":
                fieldId = PlayerRuntimeControllerFieldId.LookFrontConeEnabled;
                return true;
            case "lookSettings.frontConeMaxSpeedMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookFrontConeMaxSpeedMultiplier;
                return true;
            case "lookSettings.frontConeAccelerationMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookFrontConeAccelerationMultiplier;
                return true;
            case "lookSettings.backConeAngle":
                fieldId = PlayerRuntimeControllerFieldId.LookBackConeAngleDegrees;
                return true;
            case "lookSettings.backConeEnabled":
                fieldId = PlayerRuntimeControllerFieldId.LookBackConeEnabled;
                return true;
            case "lookSettings.backConeMaxSpeedMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookBackConeMaxSpeedMultiplier;
                return true;
            case "lookSettings.backConeAccelerationMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookBackConeAccelerationMultiplier;
                return true;
            case "lookSettings.leftConeAngle":
                fieldId = PlayerRuntimeControllerFieldId.LookLeftConeAngleDegrees;
                return true;
            case "lookSettings.leftConeEnabled":
                fieldId = PlayerRuntimeControllerFieldId.LookLeftConeEnabled;
                return true;
            case "lookSettings.leftConeMaxSpeedMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookLeftConeMaxSpeedMultiplier;
                return true;
            case "lookSettings.leftConeAccelerationMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookLeftConeAccelerationMultiplier;
                return true;
            case "lookSettings.rightConeAngle":
                fieldId = PlayerRuntimeControllerFieldId.LookRightConeAngleDegrees;
                return true;
            case "lookSettings.rightConeEnabled":
                fieldId = PlayerRuntimeControllerFieldId.LookRightConeEnabled;
                return true;
            case "lookSettings.rightConeMaxSpeedMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookRightConeMaxSpeedMultiplier;
                return true;
            case "lookSettings.rightConeAccelerationMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookRightConeAccelerationMultiplier;
                return true;
            case "lookSettings.values.rotationDamping":
                fieldId = PlayerRuntimeControllerFieldId.LookRotationDamping;
                return true;
            case "lookSettings.values.rotationMaxSpeed":
                fieldId = PlayerRuntimeControllerFieldId.LookRotationMaxSpeed;
                return true;
            case "lookSettings.values.rotationDeadZone":
                fieldId = PlayerRuntimeControllerFieldId.LookRotationDeadZone;
                return true;
            case "lookSettings.values.digitalReleaseGraceSeconds":
                fieldId = PlayerRuntimeControllerFieldId.LookDigitalReleaseGraceSeconds;
                return true;
            case "lookSettings.multiplierSampling":
                fieldId = PlayerRuntimeControllerFieldId.LookMultiplierSampling;
                return true;
            case "cameraSettings.behavior":
                fieldId = PlayerRuntimeControllerFieldId.CameraBehavior;
                return true;
            case "cameraSettings.followOffset.x":
                fieldId = PlayerRuntimeControllerFieldId.CameraFollowOffsetX;
                return true;
            case "cameraSettings.followOffset.y":
                fieldId = PlayerRuntimeControllerFieldId.CameraFollowOffsetY;
                return true;
            case "cameraSettings.followOffset.z":
                fieldId = PlayerRuntimeControllerFieldId.CameraFollowOffsetZ;
                return true;
            case "cameraSettings.values.followSpeed":
                fieldId = PlayerRuntimeControllerFieldId.CameraFollowSpeed;
                return true;
            case "cameraSettings.values.cameraLag":
                fieldId = PlayerRuntimeControllerFieldId.CameraLag;
                return true;
            case "cameraSettings.values.damping":
                fieldId = PlayerRuntimeControllerFieldId.CameraDamping;
                return true;
            case "cameraSettings.values.maxFollowDistance":
                fieldId = PlayerRuntimeControllerFieldId.CameraMaxFollowDistance;
                return true;
            case "cameraSettings.values.deadZoneRadius":
                fieldId = PlayerRuntimeControllerFieldId.CameraDeadZoneRadius;
                return true;
            case "shootingSettings.triggerMode":
                fieldId = PlayerRuntimeControllerFieldId.ShootingTriggerMode;
                return true;
            case "shootingSettings.projectilesInheritPlayerSpeed":
                fieldId = PlayerRuntimeControllerFieldId.ShootingProjectilesInheritPlayerSpeed;
                return true;
            case "shootingSettings.shootOffset.x":
                fieldId = PlayerRuntimeControllerFieldId.ShootingShootOffsetX;
                return true;
            case "shootingSettings.shootOffset.y":
                fieldId = PlayerRuntimeControllerFieldId.ShootingShootOffsetY;
                return true;
            case "shootingSettings.shootOffset.z":
                fieldId = PlayerRuntimeControllerFieldId.ShootingShootOffsetZ;
                return true;
            case "shootingSettings.values.shootSpeed":
                fieldId = PlayerRuntimeControllerFieldId.ShootingShootSpeed;
                return true;
            case "shootingSettings.values.rateOfFire":
                fieldId = PlayerRuntimeControllerFieldId.ShootingRateOfFire;
                return true;
            case "shootingSettings.values.projectileSizeMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.ShootingProjectileSizeMultiplier;
                return true;
            case "shootingSettings.values.explosionRadius":
                fieldId = PlayerRuntimeControllerFieldId.ShootingExplosionRadius;
                return true;
            case "shootingSettings.values.range":
                fieldId = PlayerRuntimeControllerFieldId.ShootingRange;
                return true;
            case "shootingSettings.values.lifetime":
                fieldId = PlayerRuntimeControllerFieldId.ShootingLifetime;
                return true;
            case "shootingSettings.values.damage":
                fieldId = PlayerRuntimeControllerFieldId.ShootingDamage;
                return true;
            case "shootingSettings.values.appliedElement":
                fieldId = PlayerRuntimeControllerFieldId.ShootingAppliedElement;
                return true;
            case "shootingSettings.values.elementBulletSettings.effectKind":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletEffectKind;
                return true;
            case "shootingSettings.values.elementBulletSettings.procMode":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletProcMode;
                return true;
            case "shootingSettings.values.elementBulletSettings.reapplyMode":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletReapplyMode;
                return true;
            case "shootingSettings.values.elementBulletSettings.stacksPerHit":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletStacksPerHit;
                return true;
            case "shootingSettings.values.elementBulletSettings.procThresholdStacks":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletProcThresholdStacks;
                return true;
            case "shootingSettings.values.elementBulletSettings.maximumStacks":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletMaximumStacks;
                return true;
            case "shootingSettings.values.elementBulletSettings.stackDecayPerSecond":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletStackDecayPerSecond;
                return true;
            case "shootingSettings.values.elementBulletSettings.consumeStacksOnProc":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletConsumeStacksOnProc;
                return true;
            case "shootingSettings.values.elementBulletSettings.dotDamagePerTick":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletDotDamagePerTick;
                return true;
            case "shootingSettings.values.elementBulletSettings.dotTickInterval":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletDotTickInterval;
                return true;
            case "shootingSettings.values.elementBulletSettings.dotDurationSeconds":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletDotDurationSeconds;
                return true;
            case "shootingSettings.values.elementBulletSettings.impedimentSlowPercentPerStack":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletImpedimentSlowPercentPerStack;
                return true;
            case "shootingSettings.values.elementBulletSettings.impedimentProcSlowPercent":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletImpedimentProcSlowPercent;
                return true;
            case "shootingSettings.values.elementBulletSettings.impedimentMaxSlowPercent":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletImpedimentMaxSlowPercent;
                return true;
            case "shootingSettings.values.elementBulletSettings.impedimentDurationSeconds":
                fieldId = PlayerRuntimeControllerFieldId.ShootingElementBulletImpedimentDurationSeconds;
                return true;
            case "shootingSettings.values.penetrationMode":
                fieldId = PlayerRuntimeControllerFieldId.ShootingPenetrationMode;
                return true;
            case "shootingSettings.values.maxPenetrations":
                fieldId = PlayerRuntimeControllerFieldId.ShootingMaxPenetrations;
                return true;
            case "shootingSettings.values.knockback.enabled":
                fieldId = PlayerRuntimeControllerFieldId.ShootingKnockbackEnabled;
                return true;
            case "shootingSettings.values.knockback.strength":
                fieldId = PlayerRuntimeControllerFieldId.ShootingKnockbackStrength;
                return true;
            case "shootingSettings.values.knockback.durationSeconds":
                fieldId = PlayerRuntimeControllerFieldId.ShootingKnockbackDurationSeconds;
                return true;
            case "shootingSettings.values.knockback.directionMode":
                fieldId = PlayerRuntimeControllerFieldId.ShootingKnockbackDirectionMode;
                return true;
            case "shootingSettings.values.knockback.stackingMode":
                fieldId = PlayerRuntimeControllerFieldId.ShootingKnockbackStackingMode;
                return true;
            case "healthStatistics.maxHealth":
                fieldId = PlayerRuntimeControllerFieldId.HealthMaxHealth;
                return true;
            case "healthStatistics.maxHealthAdjustmentMode":
                fieldId = PlayerRuntimeControllerFieldId.HealthMaxHealthAdjustmentMode;
                return true;
            case "healthStatistics.maxShield":
                fieldId = PlayerRuntimeControllerFieldId.HealthMaxShield;
                return true;
            case "healthStatistics.maxShieldAdjustmentMode":
                fieldId = PlayerRuntimeControllerFieldId.HealthMaxShieldAdjustmentMode;
                return true;
            case "healthStatistics.graceTimeSeconds":
                fieldId = PlayerRuntimeControllerFieldId.HealthGraceTimeSeconds;
                return true;
            default:
                return false;
        }
    }
#endif
    #endregion

    #endregion
}
