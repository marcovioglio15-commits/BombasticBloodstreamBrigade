using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Centralizes controller and animator config baking used by PlayerAuthoringBaker.
/// Keeps the baker focused on orchestration while the data normalization lives in one static utility.
/// </summary>
public static class PlayerControllerConfigBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the player controller blob from one preset at bake time.
    /// Used by PlayerAuthoringBaker before the blob is attached to the player entity.
    /// </summary>
    /// <param name="preset">Source controller preset that contains movement, look, camera, shooting and health data.</param>
    /// <returns>Persistent blob asset reference ready to be added to the baked entity.</returns>
    public static BlobAssetReference<PlayerControllerConfigBlob> BuildConfigBlob(PlayerControllerPreset preset)
    {
        BlobBuilder builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
        ref PlayerControllerConfigBlob root = ref builder.ConstructRoot<PlayerControllerConfigBlob>();

        FillMovementConfig(ref root, preset.MovementSettings);
        FillLookConfig(ref root, preset.LookSettings, ref builder);
        FillCameraConfig(ref root, preset.CameraSettings);
        FillShootingConfig(ref root, preset.ShootingSettings);
        FillHealthStatisticsConfig(ref root, preset.HealthStatistics);

        BlobAssetReference<PlayerControllerConfigBlob> blob = builder.CreateBlobAssetReference<PlayerControllerConfigBlob>(Unity.Collections.Allocator.Persistent);
        builder.Dispose();
        return blob;
    }

    /// <summary>
    /// Builds the world collision layer config resolved from the master preset.
    /// Used during baking so runtime systems do not need to resolve layer names every frame.
    /// </summary>
    /// <param name="masterPreset">Master preset that may override the default walls layer name.</param>
    /// <returns>Runtime-safe world layers config with a resolved walls layer mask.</returns>
    public static PlayerWorldLayersConfig BuildWorldLayersConfig(PlayerMasterPreset masterPreset)
    {
        string wallsLayerName = ResolveWallsLayerName(masterPreset);
        int wallsLayerMask = ResolveLayerMaskByName(wallsLayerName);

        if (wallsLayerMask == 0)
            wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        return new PlayerWorldLayersConfig
        {
            WallsLayerMask = wallsLayerMask
        };
    }

    /// <summary>
    /// Builds the runtime camera damage-shake blob from authoring shake settings. Shared by the controller config
    /// blob, the immutable baseline camera config and the mutable runtime camera config so every bake path emits
    /// identical shake data. Raw values are baked as-authored and clamped defensively by the runtime shake utility.
    /// </summary>
    /// <param name="shake">Authoring damage-shake settings, or null to emit a disabled shake block.</param>
    /// <returns>Runtime-ready camera shake blob.</returns>
    public static CameraShakeBlob BuildCameraShakeBlob(CameraDamageShakeSettings shake)
    {
        if (shake == null)
            return default;

        return new CameraShakeBlob
        {
            Enabled = shake.Enabled ? (byte)1 : (byte)0,
            DurationSeconds = shake.DurationSeconds,
            PositionalAmplitude = shake.PositionalAmplitude,
            RotationalAmplitude = shake.RotationalAmplitude,
            Frequency = shake.Frequency,
            Falloff = shake.Falloff,
            ScaleWithDamage = shake.ScaleWithDamage ? (byte)1 : (byte)0,
            DamageForFullStrength = shake.DamageForFullStrength,
            RumbleEnabled = shake.RumbleEnabled ? (byte)1 : (byte)0,
            RumbleLowFrequency = shake.RumbleLowFrequency,
            RumbleHighFrequency = shake.RumbleHighFrequency
        };
    }

    /// <summary>
    /// Builds the animator parameter hash config from the selected animation bindings preset.
    /// Called by PlayerAuthoringBaker before ECS animation runtime state is added.
    /// </summary>
    /// <param name="preset">Optional animation bindings preset. Null falls back to default parameter names.</param>
    /// <returns>Hash-based animator parameter config used by runtime animator sync systems.</returns>
    public static PlayerAnimatorParameterConfig BuildAnimatorParameterConfig(PlayerAnimationBindingsPreset preset)
    {
        string moveXParameter = preset != null ? preset.MoveXParameter : "MoveX";
        string moveYParameter = preset != null ? preset.MoveYParameter : "MoveY";
        string moveSpeedParameter = preset != null ? preset.MoveSpeedParameter : "MoveSpeed";
        string aimXParameter = preset != null ? preset.AimXParameter : "AimX";
        string aimYParameter = preset != null ? preset.AimYParameter : "AimY";
        string isMovingParameter = preset != null ? preset.IsMovingParameter : "IsMoving";
        string isShootingParameter = preset != null ? preset.IsShootingParameter : "IsShooting";
        string isDashingParameter = preset != null ? preset.IsDashingParameter : "IsDashing";
        string shotPulseParameter = preset != null ? preset.ShotPulseParameter : "ShotPulse";
        string proceduralRecoilParameter = preset != null ? preset.ProceduralRecoilParameter : "ProcRecoil";
        string proceduralAimWeightParameter = preset != null ? preset.ProceduralAimWeightParameter : "ProcAimWeight";
        string proceduralLeanParameter = preset != null ? preset.ProceduralLeanParameter : "ProcLean";

        PlayerAnimatorParameterConfig config = default;
        byte hasMoveX;
        byte hasMoveY;
        byte hasMoveSpeed;
        byte hasAimX;
        byte hasAimY;
        byte hasIsMoving;
        byte hasIsShooting;
        byte hasIsDashing;
        byte hasShotPulse;
        byte hasProceduralRecoil;
        byte hasProceduralAimWeight;
        byte hasProceduralLean;

        config.MoveXHash = ResolveParameterHash(moveXParameter, out hasMoveX);
        config.MoveYHash = ResolveParameterHash(moveYParameter, out hasMoveY);
        config.MoveSpeedHash = ResolveParameterHash(moveSpeedParameter, out hasMoveSpeed);
        config.AimXHash = ResolveParameterHash(aimXParameter, out hasAimX);
        config.AimYHash = ResolveParameterHash(aimYParameter, out hasAimY);
        config.IsMovingHash = ResolveParameterHash(isMovingParameter, out hasIsMoving);
        config.IsShootingHash = ResolveParameterHash(isShootingParameter, out hasIsShooting);
        config.IsDashingHash = ResolveParameterHash(isDashingParameter, out hasIsDashing);
        config.ShotPulseHash = ResolveParameterHash(shotPulseParameter, out hasShotPulse);
        config.ProceduralRecoilHash = ResolveParameterHash(proceduralRecoilParameter, out hasProceduralRecoil);
        config.ProceduralAimWeightHash = ResolveParameterHash(proceduralAimWeightParameter, out hasProceduralAimWeight);
        config.ProceduralLeanHash = ResolveParameterHash(proceduralLeanParameter, out hasProceduralLean);
        config.HasMoveX = hasMoveX;
        config.HasMoveY = hasMoveY;
        config.HasMoveSpeed = hasMoveSpeed;
        config.HasAimX = hasAimX;
        config.HasAimY = hasAimY;
        config.HasIsMoving = hasIsMoving;
        config.HasIsShooting = hasIsShooting;
        config.HasIsDashing = hasIsDashing;
        config.HasShotPulse = hasShotPulse;
        config.HasProceduralRecoil = hasProceduralRecoil;
        config.HasProceduralAimWeight = hasProceduralAimWeight;
        config.HasProceduralLean = hasProceduralLean;
        config.FloatDampTime = preset != null ? math.max(0f, preset.FloatDampTime) : 0.08f;
        config.MovingSpeedThreshold = preset != null ? math.max(0f, preset.MovingSpeedThreshold) : 0.02f;
        config.UseFloatDamping = preset == null || preset.UseFloatDamping ? (byte)1 : (byte)0;
        config.DisableRootMotion = preset == null || preset.DisableRootMotion ? (byte)1 : (byte)0;
        config.ProceduralRecoilEnabled = preset != null && preset.ProceduralRecoilEnabled ? (byte)1 : (byte)0;
        config.ProceduralAimWeightEnabled = preset != null && preset.ProceduralAimWeightEnabled ? (byte)1 : (byte)0;
        config.ProceduralLeanEnabled = preset != null && preset.ProceduralLeanEnabled ? (byte)1 : (byte)0;
        config.ProceduralRecoilKick = preset != null ? math.max(0f, preset.ProceduralRecoilKick) : 0f;
        config.ProceduralRecoilRecoveryPerSecond = preset != null ? math.max(0f, preset.ProceduralRecoilRecoveryPerSecond) : 0f;
        config.ProceduralAimWeightSmoothing = preset != null ? math.max(0f, preset.ProceduralAimWeightSmoothing) : 0f;
        config.ProceduralLeanSmoothing = preset != null ? math.max(0f, preset.ProceduralLeanSmoothing) : 0f;
        return config;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the walls layer name configured by the master preset.
    /// </summary>
    /// <param name="masterPreset">Optional master preset that may override the default walls layer.</param>
    /// <returns>Trimmed layer name or the project default when not configured.</returns>
    private static string ResolveWallsLayerName(PlayerMasterPreset masterPreset)
    {
        if (masterPreset == null)
            return WorldWallCollisionUtility.DefaultWallsLayerName;

        string wallsLayerName = masterPreset.WallsLayerName;

        if (string.IsNullOrWhiteSpace(wallsLayerName))
            return WorldWallCollisionUtility.DefaultWallsLayerName;

        return wallsLayerName.Trim();
    }

    /// <summary>
    /// Resolves a layer name to a bitmask value.
    /// </summary>
    /// <param name="layerName">Unity layer name to convert.</param>
    /// <returns>Bitmask for the resolved layer or zero when the layer does not exist.</returns>
    private static int ResolveLayerMaskByName(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return 0;

        int layerIndex = LayerMask.NameToLayer(layerName);

        if (layerIndex < 0)
            return 0;

        return 1 << layerIndex;
    }

    /// <summary>
    /// Hashes one animator parameter name while also reporting whether the parameter is defined.
    /// </summary>
    /// <param name="parameterName">Animator parameter name to hash.</param>
    /// <returns>Hash of the trimmed parameter name, or zero when the parameter is missing.</returns>
    private static int ResolveParameterHash(string parameterName, out byte hasParameter)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            hasParameter = 0;
            return 0;
        }

        hasParameter = 1;
        return Animator.StringToHash(parameterName.Trim());
    }

    /// <summary>
    /// Writes movement settings into the controller blob.
    /// </summary>
    /// <param name="root">Controller blob root being populated.</param>
    private static void FillMovementConfig(ref PlayerControllerConfigBlob root, MovementSettings movementSettings)
    {
        int resolvedDiscreteDirectionCount = Mathf.Max(1, movementSettings.DiscreteDirectionCount);
        MovementConfig movementConfig = new MovementConfig
        {
            DirectionsMode = movementSettings.DirectionsMode,
            DiscreteDirectionCount = resolvedDiscreteDirectionCount,
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

        root.Movement = movementConfig;
    }

    /// <summary>
    /// Writes look settings and discrete direction multipliers into the controller blob.
    /// </summary>
    /// <param name="root">Controller blob root being populated.</param>
    private static void FillLookConfig(ref PlayerControllerConfigBlob root, LookSettings lookSettings, ref BlobBuilder builder)
    {
        ref LookConfig lookConfig = ref root.Look;
        int resolvedDiscreteDirectionCount = Mathf.Max(1, lookSettings.DiscreteDirectionCount);

        lookConfig.DirectionsMode = lookSettings.DirectionsMode;
        lookConfig.DiscreteDirectionCount = resolvedDiscreteDirectionCount;
        lookConfig.DirectionOffsetDegrees = lookSettings.DirectionOffsetDegrees;
        lookConfig.RotationMode = lookSettings.RotationMode;
        lookConfig.RotationSpeed = lookSettings.RotationSpeed;
        lookConfig.MultiplierSampling = lookSettings.MultiplierSampling;
        lookConfig.FrontCone = new ConeConfig
        {
            Enabled = lookSettings.FrontConeEnabled,
            AngleDegrees = lookSettings.FrontConeAngle,
            MaxSpeedMultiplier = lookSettings.FrontConeMaxSpeedMultiplier,
            AccelerationMultiplier = lookSettings.FrontConeAccelerationMultiplier
        };
        lookConfig.BackCone = new ConeConfig
        {
            Enabled = lookSettings.BackConeEnabled,
            AngleDegrees = lookSettings.BackConeAngle,
            MaxSpeedMultiplier = lookSettings.BackConeMaxSpeedMultiplier,
            AccelerationMultiplier = lookSettings.BackConeAccelerationMultiplier
        };
        lookConfig.LeftCone = new ConeConfig
        {
            Enabled = lookSettings.LeftConeEnabled,
            AngleDegrees = lookSettings.LeftConeAngle,
            MaxSpeedMultiplier = lookSettings.LeftConeMaxSpeedMultiplier,
            AccelerationMultiplier = lookSettings.LeftConeAccelerationMultiplier
        };
        lookConfig.RightCone = new ConeConfig
        {
            Enabled = lookSettings.RightConeEnabled,
            AngleDegrees = lookSettings.RightConeAngle,
            MaxSpeedMultiplier = lookSettings.RightConeMaxSpeedMultiplier,
            AccelerationMultiplier = lookSettings.RightConeAccelerationMultiplier
        };
        lookConfig.Values = new LookValuesBlob
        {
            RotationDamping = lookSettings.Values.RotationDamping,
            RotationMaxSpeed = lookSettings.Values.RotationMaxSpeed,
            RotationDeadZone = lookSettings.Values.RotationDeadZone,
            DigitalReleaseGraceSeconds = lookSettings.Values.DigitalReleaseGraceSeconds
        };

        int maxSpeedCount = lookSettings.DiscreteDirectionMaxSpeedMultipliers.Count;
        int accelerationCount = lookSettings.DiscreteDirectionAccelerationMultipliers.Count;
        int multipliersCount = Mathf.Max(maxSpeedCount, accelerationCount);

        // Allocate both multiplier arrays once and fill missing values with the neutral multiplier.
        BlobBuilderArray<float> maxSpeedArray = builder.Allocate(ref lookConfig.DiscreteMaxSpeedMultipliers, multipliersCount);
        BlobBuilderArray<float> accelerationArray = builder.Allocate(ref lookConfig.DiscreteAccelerationMultipliers, multipliersCount);

        for (int index = 0; index < multipliersCount; index++)
        {
            float maxSpeedMultiplier = index < maxSpeedCount ? lookSettings.DiscreteDirectionMaxSpeedMultipliers[index] : 1f;
            float accelerationMultiplier = index < accelerationCount ? lookSettings.DiscreteDirectionAccelerationMultipliers[index] : 1f;
            maxSpeedArray[index] = maxSpeedMultiplier;
            accelerationArray[index] = accelerationMultiplier;
        }
    }

    /// <summary>
    /// Writes camera follow settings into the controller blob.
    /// </summary>
    /// <param name="root">Controller blob root being populated.</param>
    private static void FillCameraConfig(ref PlayerControllerConfigBlob root, CameraSettings cameraSettings)
    {
        CameraConfig cameraConfig = new CameraConfig
        {
            Behavior = cameraSettings.Behavior,
            FollowOffset = new float3(cameraSettings.FollowOffset.x, cameraSettings.FollowOffset.y, cameraSettings.FollowOffset.z),
            Values = new CameraValuesBlob
            {
                SmoothTime = cameraSettings.Values.SmoothTime,
                MaxFollowDistance = cameraSettings.Values.MaxFollowDistance,
                DeadZoneRadius = cameraSettings.Values.DeadZoneRadius
            },
            Shake = BuildCameraShakeBlob(cameraSettings.DamageShake)
        };

        root.Camera = cameraConfig;
    }

    /// <summary>
    /// Writes shooting settings into the controller blob.
    /// </summary>
    /// <param name="root">Controller blob root being populated.</param>
    private static void FillShootingConfig(ref PlayerControllerConfigBlob root, ShootingSettings shootingSettings)
    {
        if (shootingSettings == null)
        {
            root.Shooting = default;
            return;
        }

        ShootingConfig shootingConfig = new ShootingConfig
        {
            TriggerMode = shootingSettings.TriggerMode,
            ProjectilesInheritPlayerSpeed = shootingSettings.ProjectilesInheritPlayerSpeed ? (byte)1 : (byte)0,
            ShootOffset = new float3(shootingSettings.ShootOffset.x, shootingSettings.ShootOffset.y, shootingSettings.ShootOffset.z),
            Values = PlayerShootingConfigRuntimeUtility.BuildRuntimeValues(shootingSettings.Values)
        };

        root.Shooting = shootingConfig;
    }

    /// <summary>
    /// Writes health and shield values into the controller blob.
    /// </summary>
    /// <param name="root">Controller blob root being populated.</param>
    private static void FillHealthStatisticsConfig(ref PlayerControllerConfigBlob root, PlayerHealthStatisticsSettings healthStatistics)
    {
        if (healthStatistics == null)
        {
            root.HealthStatistics = new HealthStatisticsConfig
            {
                MaxHealth = 100f,
                MaxShield = 0f,
                GraceTimeSeconds = 0f
            };
            return;
        }

        root.HealthStatistics = new HealthStatisticsConfig
        {
            MaxHealth = math.max(1f, healthStatistics.MaxHealth),
            MaxShield = math.max(0f, healthStatistics.MaxShield),
            GraceTimeSeconds = math.max(0f, healthStatistics.GraceTimeSeconds)
        };
    }
    #endregion

    #endregion
}
