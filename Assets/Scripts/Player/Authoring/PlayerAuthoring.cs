using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Authoring component for configuring player presets, runtime visual bridge settings and hybrid bake safety.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAuthoring : MonoBehaviour
{
    #region Constants
    private const float DefaultOutlineThickness = 1f;
    private static readonly Color DefaultOutlineColor = Color.black;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Preset")]
    [Tooltip("Master preset used to configure this player instance.")]
    [FormerlySerializedAs("m_MasterPreset")]
    [SerializeField] private PlayerMasterPreset masterPreset;

    [Header("Cheats")]
    [Tooltip("Optional power-up preset library used by runtime cheat shortcuts. Ctrl+Number applies the preset at the matching index.")]
    [SerializeField] private PlayerPowerUpsPresetLibrary powerUpsCheatPresetLibrary;

    [Header("Shooting")]
    [Tooltip("Optional muzzle transform used as shooting reference for spawn orientation and offset.")]
    [SerializeField] private Transform weaponReference;

    [Header("Animation")]
    [Tooltip("Optional Animator used for ECS-driven visual animation sync.")]
    [HideInInspector]
    [SerializeField] private Animator animatorComponent;

    [Header("Runtime Visual Bridge")]
    [Tooltip("Optional prefab asset instantiated at runtime when no valid Animator companion exists. Use a visual-only prefab with Animator and full rig hierarchy.")]
    [HideInInspector]
    [SerializeField] private GameObject runtimeVisualBridgePrefab;

    [Tooltip("When enabled, spawns the runtime visual bridge only if Animator companion is missing or null at runtime.")]
    [HideInInspector]
    [SerializeField] private bool spawnRuntimeVisualBridgeWhenAnimatorMissing = true;

    [Tooltip("When enabled, runtime visual bridge follows ECS player rotation.")]
    [HideInInspector]
    [SerializeField] private bool runtimeVisualBridgeSyncRotation = true;

    [Tooltip("Local-space position offset applied to runtime visual bridge relative to ECS player transform.")]
    [HideInInspector]
    [SerializeField] private Vector3 runtimeVisualBridgeOffset = Vector3.zero;

    [Header("Damage Feedback")]
    [Tooltip("Tint color applied during the brief damage flash after the player takes valid damage.")]
    [HideInInspector]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("Flash duration in seconds. Use very small values for a 1-3 frame reaction.")]
    [HideInInspector]
    [SerializeField] private float damageFlashDurationSeconds = 0.06f;

    [Tooltip("Maximum overlay strength reached immediately after a valid hit.")]
    [HideInInspector]
    [SerializeField] private float damageFlashMaximumBlend = 0.85f;

    [Header("Hybrid Bake Safety")]
    [Tooltip("When enabled, bakes the attached Elemental Trail prefab reference into ECS. Disable to isolate SubScene object-reference streaming issues.")]
    [SerializeField] private bool bakeElementalTrailAttachedVfxReference = false;

    [Tooltip("When enabled, converts power-up VFX prefabs into ECS prefab entities (explosion/proc/stack). Disable to isolate SubScene object-reference streaming issues.")]
    [SerializeField] private bool bakePowerUpVfxEntityPrefabs = false;

    [Header("Power-Ups VFX")]
    [Tooltip("Optional attached VFX prefab activated while Elemental Trail passive is enabled.")]
    [HideInInspector]
    [SerializeField] private GameObject elementalTrailAttachedVfxPrefab;

    [Tooltip("Scale multiplier applied to the attached Elemental Trail VFX instance.")]
    [HideInInspector]
    [SerializeField] private float elementalTrailAttachedVfxScaleMultiplier = 1f;

    [Tooltip("Maximum number of identical one-shot VFX allowed in the same spatial cell. Set 0 to disable this cap.")]
    [HideInInspector]
    [SerializeField] private int maxIdenticalOneShotVfxPerCell = 6;

    [Tooltip("Cell size in meters used by the one-shot VFX per-cell cap.")]
    [HideInInspector]
    [SerializeField] private float oneShotVfxCellSize = 2.5f;

    [Tooltip("Maximum number of identical attached elemental VFX allowed on the same target. Set 0 to disable this cap.")]
    [HideInInspector]
    [SerializeField] private int maxAttachedElementalVfxPerTarget = 1;

    [Tooltip("Maximum number of active one-shot power-up VFX managed by one player. Set 0 to disable this cap.")]
    [HideInInspector]
    [SerializeField] private int maxActiveOneShotPowerUpVfx = 400;

    [Tooltip("When enabled, hitting the attached-target cap refreshes lifetime of the existing VFX.")]
    [HideInInspector]
    [SerializeField] private bool refreshAttachedElementalVfxLifetimeOnCapHit = true;
    #endregion

    #endregion

    #region Properties
    public PlayerMasterPreset MasterPreset
    {
        get
        {
            return masterPreset;
        }
    }

    public PlayerPowerUpsPresetLibrary PowerUpsCheatPresetLibrary
    {
        get
        {
            return powerUpsCheatPresetLibrary;
        }
    }

    public Transform WeaponReference
    {
        get
        {
            return weaponReference;
        }
    }

    public Animator AnimatorComponent
    {
        get
        {
            return animatorComponent;
        }
    }

    public GameObject RuntimeVisualBridgePrefab
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveRuntimeVisualBridgePrefab(masterPreset, runtimeVisualBridgePrefab);
        }
    }

    public bool SpawnRuntimeVisualBridgeWhenAnimatorMissing
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveSpawnRuntimeVisualBridgeWhenAnimatorMissing(masterPreset,
                                                                                                                spawnRuntimeVisualBridgeWhenAnimatorMissing);
        }
    }

    public bool RuntimeVisualBridgeSyncRotation
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveRuntimeVisualBridgeSyncRotation(masterPreset,
                                                                                                     runtimeVisualBridgeSyncRotation);
        }
    }

    public Vector3 RuntimeVisualBridgeOffset
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveRuntimeVisualBridgeOffset(masterPreset, runtimeVisualBridgeOffset);
        }
    }

    public bool EnableOutline
    {
        get
        {
            PlayerVisualOutlineSettings settings = PlayerAuthoringVisualPresetResolverUtility.ResolveOutlineSettings(masterPreset);

            if (settings == null)
                return true;

            return settings.EnableOutline;
        }
    }

    public float OutlineThickness
    {
        get
        {
            PlayerVisualOutlineSettings settings = PlayerAuthoringVisualPresetResolverUtility.ResolveOutlineSettings(masterPreset);

            if (settings == null)
                return DefaultOutlineThickness;

            return settings.OutlineThickness;
        }
    }

    public Color OutlineColor
    {
        get
        {
            PlayerVisualOutlineSettings settings = PlayerAuthoringVisualPresetResolverUtility.ResolveOutlineSettings(masterPreset);

            if (settings == null)
                return DefaultOutlineColor;

            return settings.OutlineColor;
        }
    }

    public Color DamageFlashColor
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveDamageFlashColor(masterPreset, damageFlashColor);
        }
    }

    public float DamageFlashDurationSeconds
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveDamageFlashDurationSeconds(masterPreset, damageFlashDurationSeconds);
        }
    }

    public float DamageFlashMaximumBlend
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveDamageFlashMaximumBlend(masterPreset, damageFlashMaximumBlend);
        }
    }

    public bool BakeElementalTrailAttachedVfxReference
    {
        get
        {
            return bakeElementalTrailAttachedVfxReference;
        }
    }

    public bool BakePowerUpVfxEntityPrefabs
    {
        get
        {
            return bakePowerUpVfxEntityPrefabs;
        }
    }

    public GameObject ElementalTrailAttachedVfxPrefab
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveElementalTrailAttachedVfxPrefab(masterPreset, elementalTrailAttachedVfxPrefab);
        }
    }

    public float ElementalTrailAttachedVfxScaleMultiplier
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveElementalTrailAttachedVfxScaleMultiplier(masterPreset,
                                                                                                               elementalTrailAttachedVfxScaleMultiplier);
        }
    }

    public int MaxIdenticalOneShotVfxPerCell
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveMaxIdenticalOneShotVfxPerCell(masterPreset, maxIdenticalOneShotVfxPerCell);
        }
    }

    public float OneShotVfxCellSize
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveOneShotVfxCellSize(masterPreset, oneShotVfxCellSize);
        }
    }

    public int MaxAttachedElementalVfxPerTarget
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveMaxAttachedElementalVfxPerTarget(masterPreset,
                                                                                                       maxAttachedElementalVfxPerTarget);
        }
    }

    public int MaxActiveOneShotPowerUpVfx
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveMaxActiveOneShotPowerUpVfx(masterPreset, maxActiveOneShotPowerUpVfx);
        }
    }

    public bool RefreshAttachedElementalVfxLifetimeOnCapHit
    {
        get
        {
            return PlayerAuthoringVisualPresetResolverUtility.ResolveRefreshAttachedElementalVfxLifetimeOnCapHit(masterPreset,
                                                                                                                  refreshAttachedElementalVfxLifetimeOnCapHit);
        }
    }
    #endregion

    #region Methods

    #region Preset
    /// <summary>
    /// Retrieves the controller preset from the master preset.
    /// </summary>
    /// <returns>The PlayerControllerPreset from the master preset, or null if the master preset is not set.<returns>
    public PlayerControllerPreset GetControllerPreset()
    {
        if (masterPreset == null)
            return null;

        return masterPreset.ControllerPreset;
    }

    /// <summary>
    /// Retrieves the progression preset from the master preset.
    /// </summary>
    /// <returns>The PlayerProgressionPreset from the master preset, or null if the master preset is not set.<returns>
    public PlayerProgressionPreset GetProgressionPreset()
    {
        if (masterPreset == null)
            return null;

        return masterPreset.ProgressionPreset;
    }

    /// <summary>
    /// Retrieves the power-ups preset from the master preset.
    /// </summary>
    /// <returns>The PlayerPowerUpsPreset from the master preset, or null if the master preset is not set.<returns>
    public PlayerPowerUpsPreset GetPowerUpsPreset()
    {
        if (masterPreset == null)
            return null;

        return masterPreset.PowerUpsPreset;
    }
    #endregion
    #endregion
}

/// <summary>
/// Bakes PlayerAuthoring data into ECS components and configuration blobs for player controller and camera setup.
/// </summary>
public sealed class PlayerAuthoringBaker : Baker<PlayerAuthoring>
{
    #region Bake
    /// <summary>
    /// Configures and adds player controller and camera anchor components to the entity based on the provided authoring
    /// data.
    /// </summary>
    /// <param name="authoring">The PlayerAuthoring instance containing configuration data.</param>
    public override void Bake(PlayerAuthoring authoring)
    {
        // Validate authoring data
        if (authoring == null)
            return;

        DeclarePresetDependencies(authoring);

        PlayerControllerPreset controllerPreset = authoring.GetControllerPreset();

        if (controllerPreset == null)
            return;

        PlayerControllerPreset sourceControllerPreset = controllerPreset;
        PlayerProgressionPreset progressionPreset = authoring.GetProgressionPreset();
        PlayerPowerUpsPreset powerUpsPreset = authoring.GetPowerUpsPreset();
        PlayerProgressionPreset sourceProgressionPreset = progressionPreset;
        PlayerPowerUpsPreset sourcePowerUpsPreset = powerUpsPreset;
        PlayerAnimationBindingsPreset animationBindingsPreset = authoring.MasterPreset != null ? authoring.MasterPreset.AnimationBindingsPreset : null;

#if UNITY_EDITOR
        PlayerScaledPresetScope scaledPresetScope = PlayerPresetScalingBakeUtility.CreateScope(controllerPreset,
                                                                                               progressionPreset,
                                                                                               powerUpsPreset,
                                                                                               animationBindingsPreset);
        controllerPreset = scaledPresetScope.ControllerPreset;
        progressionPreset = scaledPresetScope.ProgressionPreset;
        powerUpsPreset = scaledPresetScope.PowerUpsPreset;
        animationBindingsPreset = scaledPresetScope.AnimationBindingsPreset;

        try
        {
#endif

        // Create entity and build configuration blob
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
#if UNITY_EDITOR
        TryAddScalingDebugBuffers(entity,
                                  scaledPresetScope);
#endif
        BlobAssetReference<PlayerControllerConfigBlob> blob = PlayerControllerConfigBakeUtility.BuildConfigBlob(controllerPreset);
        AddBlobAsset(ref blob, out Unity.Entities.Hash128 _);

        PlayerControllerConfig config = new PlayerControllerConfig
        {
            Config = blob
        };

        //  Add player controller config component to entity
        AddComponent(entity, config);
        AddComponent(entity, PlayerRuntimeScalingControllerBakeUtility.BuildBaseMovementConfig(sourceControllerPreset));
        AddComponent(entity, new PlayerRuntimeMovementConfig
        {
            DirectionsMode = controllerPreset.MovementSettings.DirectionsMode,
            DiscreteDirectionCount = math.max(1, controllerPreset.MovementSettings.DiscreteDirectionCount),
            DirectionOffsetDegrees = controllerPreset.MovementSettings.DirectionOffsetDegrees,
            MovementReference = controllerPreset.MovementSettings.MovementReference,
            Values = new MovementValuesBlob
            {
                BaseSpeed = controllerPreset.MovementSettings.Values.BaseSpeed,
                MaxSpeed = controllerPreset.MovementSettings.Values.MaxSpeed,
                Acceleration = controllerPreset.MovementSettings.Values.Acceleration,
                Deceleration = controllerPreset.MovementSettings.Values.Deceleration,
                OppositeDirectionBrakeMultiplier = controllerPreset.MovementSettings.Values.OppositeDirectionBrakeMultiplier,
                WallBounceCoefficient = controllerPreset.MovementSettings.Values.WallBounceCoefficient,
                WallCollisionSkinWidth = controllerPreset.MovementSettings.Values.WallCollisionSkinWidth,
                InputDeadZone = controllerPreset.MovementSettings.Values.InputDeadZone,
                DigitalReleaseGraceSeconds = controllerPreset.MovementSettings.Values.DigitalReleaseGraceSeconds
            }
        });
        AddComponent(entity, PlayerRuntimeScalingControllerBakeUtility.BuildBaseLookConfig(sourceControllerPreset));
        AddComponent(entity, new PlayerRuntimeLookConfig
        {
            DirectionsMode = controllerPreset.LookSettings.DirectionsMode,
            DiscreteDirectionCount = math.max(1, controllerPreset.LookSettings.DiscreteDirectionCount),
            DirectionOffsetDegrees = controllerPreset.LookSettings.DirectionOffsetDegrees,
            RotationMode = controllerPreset.LookSettings.RotationMode,
            RotationSpeed = controllerPreset.LookSettings.RotationSpeed,
            MultiplierSampling = controllerPreset.LookSettings.MultiplierSampling,
            FrontCone = new ConeConfig
            {
                Enabled = controllerPreset.LookSettings.FrontConeEnabled,
                AngleDegrees = controllerPreset.LookSettings.FrontConeAngle,
                MaxSpeedMultiplier = controllerPreset.LookSettings.FrontConeMaxSpeedMultiplier,
                AccelerationMultiplier = controllerPreset.LookSettings.FrontConeAccelerationMultiplier
            },
            BackCone = new ConeConfig
            {
                Enabled = controllerPreset.LookSettings.BackConeEnabled,
                AngleDegrees = controllerPreset.LookSettings.BackConeAngle,
                MaxSpeedMultiplier = controllerPreset.LookSettings.BackConeMaxSpeedMultiplier,
                AccelerationMultiplier = controllerPreset.LookSettings.BackConeAccelerationMultiplier
            },
            LeftCone = new ConeConfig
            {
                Enabled = controllerPreset.LookSettings.LeftConeEnabled,
                AngleDegrees = controllerPreset.LookSettings.LeftConeAngle,
                MaxSpeedMultiplier = controllerPreset.LookSettings.LeftConeMaxSpeedMultiplier,
                AccelerationMultiplier = controllerPreset.LookSettings.LeftConeAccelerationMultiplier
            },
            RightCone = new ConeConfig
            {
                Enabled = controllerPreset.LookSettings.RightConeEnabled,
                AngleDegrees = controllerPreset.LookSettings.RightConeAngle,
                MaxSpeedMultiplier = controllerPreset.LookSettings.RightConeMaxSpeedMultiplier,
                AccelerationMultiplier = controllerPreset.LookSettings.RightConeAccelerationMultiplier
            },
            Values = new LookValuesBlob
            {
                RotationDamping = controllerPreset.LookSettings.Values.RotationDamping,
                RotationMaxSpeed = controllerPreset.LookSettings.Values.RotationMaxSpeed,
                RotationDeadZone = controllerPreset.LookSettings.Values.RotationDeadZone,
                DigitalReleaseGraceSeconds = controllerPreset.LookSettings.Values.DigitalReleaseGraceSeconds
            }
        });
        AddComponent(entity, PlayerRuntimeScalingControllerBakeUtility.BuildBaseCameraConfig(sourceControllerPreset));
        AddComponent(entity, new PlayerRuntimeCameraConfig
        {
            Behavior = controllerPreset.CameraSettings.Behavior,
            FollowOffset = new float3(controllerPreset.CameraSettings.FollowOffset.x,
                                      controllerPreset.CameraSettings.FollowOffset.y,
                                      controllerPreset.CameraSettings.FollowOffset.z),
            Values = new CameraValuesBlob
            {
                FollowSpeed = controllerPreset.CameraSettings.Values.FollowSpeed,
                CameraLag = controllerPreset.CameraSettings.Values.CameraLag,
                Damping = controllerPreset.CameraSettings.Values.Damping,
                MaxFollowDistance = controllerPreset.CameraSettings.Values.MaxFollowDistance,
                DeadZoneRadius = controllerPreset.CameraSettings.Values.DeadZoneRadius
            }
        });
        AddComponent(entity, PlayerRuntimeScalingControllerBakeUtility.BuildBaseShootingConfig(sourceControllerPreset));
        AddComponent(entity, new PlayerRuntimeShootingConfig
        {
            TriggerMode = controllerPreset.ShootingSettings.TriggerMode,
            ProjectilesInheritPlayerSpeed = controllerPreset.ShootingSettings.ProjectilesInheritPlayerSpeed ? (byte)1 : (byte)0,
            ShootOffset = new float3(controllerPreset.ShootingSettings.ShootOffset.x,
                                     controllerPreset.ShootingSettings.ShootOffset.y,
                                     controllerPreset.ShootingSettings.ShootOffset.z),
            Values = PlayerShootingConfigRuntimeUtility.BuildRuntimeValues(controllerPreset.ShootingSettings.Values)
        });
        DynamicBuffer<PlayerBaseShootingAppliedElementSlot> baseAppliedElementSlotsBuffer = AddBuffer<PlayerBaseShootingAppliedElementSlot>(entity);
        DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlotsBuffer = AddBuffer<PlayerRuntimeShootingAppliedElementSlot>(entity);
        PlayerRuntimeScalingControllerBakeUtility.PopulateBaseAppliedElementSlots(sourceControllerPreset, baseAppliedElementSlotsBuffer);
        PlayerRuntimeScalingControllerBakeUtility.PopulateRuntimeAppliedElementSlots(controllerPreset, runtimeAppliedElementSlotsBuffer);
        AddComponent(entity, PlayerRuntimeScalingControllerBakeUtility.BuildBaseHealthStatisticsConfig(sourceControllerPreset));
        AddComponent(entity, new PlayerRuntimeHealthStatisticsConfig
        {
            MaxHealth = math.max(1f, controllerPreset.HealthStatistics.MaxHealth),
            MaxHealthAdjustmentMode = controllerPreset.HealthStatistics.MaxHealthAdjustmentMode,
            MaxShield = math.max(0f, controllerPreset.HealthStatistics.MaxShield),
            MaxShieldAdjustmentMode = controllerPreset.HealthStatistics.MaxShieldAdjustmentMode,
            GraceTimeSeconds = math.max(0f, controllerPreset.HealthStatistics.GraceTimeSeconds)
        });
        AddComponent(entity, new PlayerRuntimeScalingState());
        DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScalingBuffer = AddBuffer<PlayerRuntimeControllerScalingElement>(entity);
#if UNITY_EDITOR
        PlayerRuntimeScalingControllerBakeUtility.PopulateControllerScalingMetadata(sourceControllerPreset, controllerScalingBuffer);
#endif

        PlayerWorldLayersConfig worldLayersConfig = PlayerControllerConfigBakeUtility.BuildWorldLayersConfig(authoring.MasterPreset);
        AddComponent(entity, worldLayersConfig);
        Animator resolvedAnimatorComponent = PlayerAuthoringBakerValidationUtility.ResolveAnimatorComponent(authoring);
        GameObject resolvedRuntimeVisualBridgePrefab = PlayerAuthoringBakerValidationUtility.ResolveRuntimeVisualBridgePrefab(authoring);

        if (resolvedRuntimeVisualBridgePrefab != null)
            DeclareLaserBeamVisualRigDependencies(resolvedRuntimeVisualBridgePrefab);

        if (animationBindingsPreset != null)
        {
            AddComponent(entity, PlayerControllerConfigBakeUtility.BuildAnimatorParameterConfig(animationBindingsPreset));
            AddComponent(entity, new PlayerAnimatorRuntimeState
            {
                PreviousShooting = 0,
                Initialized = 0,
                ParametersValidated = 0,
                BoundAnimatorInstanceId = 0,
                RecoilValue = 0f,
                AimWeightValue = 0f,
                LeanValue = 0f,
                LastMoveX = 0f,
                LastMoveY = 1f
            });
            TryAddAnimatorAssetFallbackComponents(entity, resolvedAnimatorComponent, animationBindingsPreset);
        }

        AddComponent(entity, new PlayerVisualRuntimeBridgeConfig
        {
            VisualPrefab = resolvedRuntimeVisualBridgePrefab,
            PositionOffset = new float3(authoring.RuntimeVisualBridgeOffset.x,
                                        authoring.RuntimeVisualBridgeOffset.y,
                                        authoring.RuntimeVisualBridgeOffset.z),
            SyncRotation = authoring.RuntimeVisualBridgeSyncRotation ? (byte)1 : (byte)0,
            SpawnWhenAnimatorMissing = authoring.SpawnRuntimeVisualBridgeWhenAnimatorMissing ? (byte)1 : (byte)0
        });
        AddComponent(entity, new OutlineVisualConfig
        {
            Enabled = authoring.EnableOutline ? (byte)1 : (byte)0,
            Thickness = math.max(0f, authoring.OutlineThickness),
            Color = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.OutlineColor)
        });
        AddComponent(entity, new DamageFlashConfig
        {
            FlashColor = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.DamageFlashColor),
            DurationSeconds = math.max(0f, authoring.DamageFlashDurationSeconds),
            MaximumBlend = math.saturate(authoring.DamageFlashMaximumBlend)
        });
        AddComponent(entity, new DamageFlashState
        {
            RemainingSeconds = 0f,
            AppliedBlend = 0f
        });
        AddComponent(entity, PlayerLaserBeamVisualBakeUtility.BuildConfig(authoring));
        DynamicBuffer<PlayerLaserBeamSourceVariantElement> laserBeamSourceVariantBuffer = AddBuffer<PlayerLaserBeamSourceVariantElement>(entity);
        DynamicBuffer<PlayerLaserBeamImpactVariantElement> laserBeamImpactVariantBuffer = AddBuffer<PlayerLaserBeamImpactVariantElement>(entity);
        DynamicBuffer<PlayerLaserBeamVisualPresetElement> laserBeamVisualPresetBuffer = AddBuffer<PlayerLaserBeamVisualPresetElement>(entity);
        PlayerLaserBeamVisualBakeUtility.PopulateSourceVariantBuffer(authoring, laserBeamSourceVariantBuffer);
        PlayerLaserBeamVisualBakeUtility.PopulateImpactVariantBuffer(authoring, laserBeamImpactVariantBuffer);
        PlayerLaserBeamVisualBakeUtility.PopulateVisualPresetBuffer(authoring, laserBeamVisualPresetBuffer);

        if (authoring.SpawnRuntimeVisualBridgeWhenAnimatorMissing &&
            resolvedRuntimeVisualBridgePrefab == null)
        {
            Debug.LogWarning(string.Format("[PlayerAuthoringBaker] Runtime visual bridge spawn is enabled on '{0}', but RuntimeVisualBridgePrefab is missing or invalid.",
                                           authoring.name),
                             authoring);
        }

        DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer = default;

        if (progressionPreset != null || powerUpsPreset != null)
        {
            characterTuningFormulaBuffer = AddBuffer<PlayerPowerUpCharacterTuningFormulaElement>(entity);
        }

        if (progressionPreset != null)
        {
            BlobAssetReference<PlayerProgressionConfigBlob> progressionBlob = PlayerProgressionBlobBakeUtility.BuildProgressionConfigBlob(progressionPreset,
                                                                                                                                        powerUpsPreset,
                                                                                                                                        sourceProgressionPreset,
                                                                                                                                        sourcePowerUpsPreset);
            AddBlobAsset(ref progressionBlob, out Unity.Entities.Hash128 _);

            PlayerProgressionConfig progressionConfig = new PlayerProgressionConfig
            {
                Config = progressionBlob
            };

            AddComponent(entity, progressionConfig);
            DynamicBuffer<PlayerBaseGamePhaseElement> baseGamePhasesBuffer = AddBuffer<PlayerBaseGamePhaseElement>(entity);
            DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhasesBuffer = AddBuffer<PlayerRuntimeGamePhaseElement>(entity);
            DynamicBuffer<PlayerRuntimeProgressionScalingElement> progressionScalingBuffer = AddBuffer<PlayerRuntimeProgressionScalingElement>(entity);
            DynamicBuffer<PlayerBaseComboRankElement> baseComboRanksBuffer = AddBuffer<PlayerBaseComboRankElement>(entity);
            DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanksBuffer = AddBuffer<PlayerRuntimeComboRankElement>(entity);
            DynamicBuffer<PlayerBaseComboPassiveUnlockElement> baseComboPassiveUnlocksBuffer = AddBuffer<PlayerBaseComboPassiveUnlockElement>(entity);
            DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimeComboPassiveUnlocksBuffer = AddBuffer<PlayerRuntimeComboPassiveUnlockElement>(entity);
            DynamicBuffer<PlayerComboRankVisualElement> comboRankVisualsBuffer = AddBuffer<PlayerComboRankVisualElement>(entity);
            DynamicBuffer<PlayerRuntimeComboCounterScalingElement> comboScalingBuffer = AddBuffer<PlayerRuntimeComboCounterScalingElement>(entity);
            PlayerRuntimeScalingBakeUtility.PopulateProgressionPhaseBuffers(progressionPreset,
                                                                           sourceProgressionPreset,
                                                                           baseGamePhasesBuffer,
                                                                           runtimeGamePhasesBuffer);
            PlayerRuntimeScalingComboBakeUtility.PopulateComboCounterRuntimeData(progressionPreset,
                                                                                sourceProgressionPreset,
                                                                                baseComboRanksBuffer,
                                                                                runtimeComboRanksBuffer,
                                                                                baseComboPassiveUnlocksBuffer,
                                                                                runtimeComboPassiveUnlocksBuffer,
                                                                                characterTuningFormulaBuffer,
                                                                                out PlayerBaseComboCounterConfig baseComboConfig,
                                                                                out PlayerRuntimeComboCounterConfig runtimeComboConfig);
            PlayerRuntimeScalingComboBakeUtility.PopulateComboCounterRankVisuals(sourceProgressionPreset != null ? sourceProgressionPreset : progressionPreset,
                                                                                 comboRankVisualsBuffer);
            AddComponent(entity, baseComboConfig);
            AddComponent(entity, runtimeComboConfig);
            AddComponent(entity, new PlayerComboCounterState
            {
                DecayPointsCarry = 0f,
                GainPointsCarry = 0f,
                CurrentRankIndex = -1,
                ActivePassiveUnlockRankIndex = -1,
                NextRankRequiredValue = -1
            });
            AddBuffer<PlayerComboPassivePowerUpGrantElement>(entity);
#if UNITY_EDITOR
            PlayerRuntimeScalingProgressionBakeUtility.PopulateProgressionScalingMetadata(sourceProgressionPreset, progressionScalingBuffer);
            PlayerRuntimeScalingComboBakeUtility.PopulateComboCounterScalingMetadata(sourceProgressionPreset, comboScalingBuffer);
#endif
            AddComponent(entity, PlayerPowerUpContainerBakeUtility.BuildInteractionConfig(progressionPreset,
                                                                                         ResolveDynamicPrefabEntity));
            AddComponent(entity, new PlayerPowerUpContainerProximityState
            {
                NearestContainerEntity = Entity.Null,
                NearestDistanceSquared = 0f,
                HasContainerInRange = 0
            });
            AddComponent(entity, new PlayerPowerUpContainerInteractionLock
            {
                LockedContainerEntity = Entity.Null
            });
            AddBuffer<PlayerPowerUpContainerSwapCommand>(entity);
        }

        if (powerUpsPreset != null)
        {
            DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> powerUpVfxPrefabBindingsBuffer = AddBuffer<PlayerPowerUpVfxPrefabBindingElement>(entity);
            Func<GameObject, Entity> resolveDynamicPowerUpVfxPrefabEntity = (GameObject prefab) =>
                ResolveDynamicPowerUpVfxPrefabEntity(prefab, powerUpVfxPrefabBindingsBuffer);
            PlayerPowerUpsConfig powerUpsConfig = PlayerPowerUpActiveBakeUtility.BuildPowerUpsConfig(authoring,
                                                                                                     powerUpsPreset,
                                                                                                     resolveDynamicPowerUpVfxPrefabEntity);
            AddComponent(entity, powerUpsConfig);
            AddComponent(entity, new PlayerChargeCharacterTuningState());
            AddBuffer<PlayerChargeCharacterTuningBaseStatElement>(entity);
            PlayerPowerUpVfxCapConfig powerUpVfxCapConfig = PlayerPowerUpBakeSharedUtility.BuildPowerUpVfxCapConfig(authoring);
            AddComponent(entity, powerUpVfxCapConfig);
            IReadOnlyList<ElementalVfxByElementData> elementalEnemyVfxAssignments = PlayerAuthoringVisualPresetResolverUtility.ResolveElementalEnemyVfxAssignments(authoring.MasterPreset,
                                                                                                                                                                  powerUpsPreset);
            PlayerElementalVfxConfig elementalVfxConfig = PlayerPowerUpBakeSharedUtility.BuildElementalVfxConfig(authoring,
                                                                                                                 elementalEnemyVfxAssignments,
                                                                                                                 resolveDynamicPowerUpVfxPrefabEntity);
            AddComponent(entity, elementalVfxConfig);

            if (authoring.BakeElementalTrailAttachedVfxReference && authoring.ElementalTrailAttachedVfxPrefab != null)
            {
                AddComponent(entity, new PlayerElementalTrailAttachedVfxPrefabReference
                {
                    Prefab = authoring.ElementalTrailAttachedVfxPrefab
                });
            }
#if UNITY_EDITOR
            else if (authoring.BakeElementalTrailAttachedVfxReference && authoring.ElementalTrailAttachedVfxPrefab == null)
            {
                Debug.LogWarning(string.Format("[PlayerAuthoringBaker] Attached Elemental Trail prefab reference bake enabled on '{0}', but no prefab is assigned.",
                                               authoring.name),
                                 authoring);
            }
#endif

            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = AddBuffer<EquippedPassiveToolElement>(entity);
            PlayerPowerUpCatalogBakeUtility.PopulateEquippedPassiveToolsBuffer(authoring,
                                                                               powerUpsPreset,
                                                                               resolveDynamicPowerUpVfxPrefabEntity,
                                                                               equippedPassiveToolsBuffer);
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> powerUpUnlockCatalogBuffer = AddBuffer<PlayerPowerUpUnlockCatalogElement>(entity);
            DynamicBuffer<PlayerPowerUpTierDefinitionElement> powerUpTierDefinitionsBuffer = AddBuffer<PlayerPowerUpTierDefinitionElement>(entity);
            DynamicBuffer<PlayerPowerUpTierEntryElement> powerUpTierEntriesBuffer = AddBuffer<PlayerPowerUpTierEntryElement>(entity);
            DynamicBuffer<PlayerPowerUpTierEntryScalingElement> powerUpTierEntryScalingBuffer = AddBuffer<PlayerPowerUpTierEntryScalingElement>(entity);
            DynamicBuffer<PlayerPowerUpBaseConfigElement> powerUpBaseConfigBuffer = AddBuffer<PlayerPowerUpBaseConfigElement>(entity);
            DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScalingBuffer = AddBuffer<PlayerRuntimePowerUpScalingElement>(entity);
            PlayerPowerUpCatalogBakeUtility.PopulatePowerUpUnlockTierBuffers(authoring,
                                                                             powerUpsPreset,
                                                                             sourcePowerUpsPreset,
                                                                             resolveDynamicPowerUpVfxPrefabEntity,
                                                                             powerUpUnlockCatalogBuffer,
                                                                             characterTuningFormulaBuffer,
                                                                             powerUpTierDefinitionsBuffer,
                                                                             powerUpTierEntriesBuffer,
                                                                             powerUpTierEntryScalingBuffer);
            PlayerRuntimeScalingBakeUtility.PopulatePowerUpBaseConfigs(authoring,
                                                                       sourcePowerUpsPreset,
                                                                       resolveDynamicPowerUpVfxPrefabEntity,
                                                                       powerUpBaseConfigBuffer);
#if UNITY_EDITOR
            PlayerRuntimeScalingBakeUtility.PopulatePowerUpScalingMetadata(sourcePowerUpsPreset, powerUpScalingBuffer);
#endif
            DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntriesBuffer = AddBuffer<PlayerPowerUpCheatPresetEntry>(entity);
            DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassivesBuffer = AddBuffer<PlayerPowerUpCheatPresetPassiveElement>(entity);
            PlayerPowerUpCatalogBakeUtility.PopulatePowerUpCheatPresetBuffers(authoring,
                                                                              resolveDynamicPowerUpVfxPrefabEntity,
                                                                              cheatPresetEntriesBuffer,
                                                                              cheatPresetPassivesBuffer);
        }

        ShootingSettings shootingSettings = controllerPreset.ShootingSettings;

        if (shootingSettings != null && shootingSettings.ProjectilePrefab != null)
        {
            GameObject projectilePrefabObject = shootingSettings.ProjectilePrefab;

            if (PlayerAuthoringBakerValidationUtility.IsInvalidProjectilePrefab(authoring, projectilePrefabObject))
            {
#if UNITY_EDITOR
                Debug.LogError(string.Format("[PlayerAuthoringBaker] Invalid projectile prefab '{0}' on '{1}'. Assign a dedicated projectile prefab without PlayerAuthoring.", projectilePrefabObject.name, authoring.name), authoring);
#endif
            }
            else
            {
                Entity projectilePrefabEntity = GetEntity(projectilePrefabObject, TransformUsageFlags.Dynamic);
                ShooterProjectilePrefab projectilePrefab = new ShooterProjectilePrefab
                {
                    PrefabEntity = projectilePrefabEntity
                };

                AddComponent(entity, projectilePrefab);
                AddComponent(entity, new ProjectilePoolState
                {
                    InitialCapacity = math.max(0, shootingSettings.InitialPoolCapacity),
                    ExpandBatch = math.max(1, shootingSettings.PoolExpandBatch),
                    Initialized = 0
                });

                AddBuffer<ShootRequest>(entity);
                AddBuffer<ProjectilePoolElement>(entity);
            }
        }

        if (authoring.WeaponReference != null)
        {
            Entity muzzleAnchorEntity = GetEntity(authoring.WeaponReference, TransformUsageFlags.Dynamic);
            ShooterMuzzleAnchor muzzleAnchor = new ShooterMuzzleAnchor
            {
                AnchorEntity = muzzleAnchorEntity
            };

            AddComponent(entity, muzzleAnchor);
        }

        Transform roomAnchor = controllerPreset.CameraSettings.RoomAnchor;

        if (roomAnchor == null)
            return;

        Entity anchorEntity = GetEntity(roomAnchor, TransformUsageFlags.Dynamic);
        PlayerCameraAnchor cameraAnchor = new PlayerCameraAnchor
        {
            AnchorEntity = anchorEntity
        };

        AddComponent(entity, cameraAnchor);
#if UNITY_EDITOR
        }
        finally
        {
            if (scaledPresetScope != null)
                scaledPresetScope.Dispose();
        }
#endif
    }
    #endregion

    #region Bake Helpers
    /// <summary>
    /// Declares preset dependencies consumed by this baker so editing preset assets triggers a player rebake.
    /// /params authoring Source authoring component used to resolve all preset references.
    /// /returns None.
    /// </summary>
    private void DeclarePresetDependencies(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return;

        PlayerMasterPreset masterPreset = authoring.MasterPreset;

        if (masterPreset != null)
        {
            DependsOn(masterPreset);

            if (masterPreset.ControllerPreset != null)
                DependsOn(masterPreset.ControllerPreset);

            if (masterPreset.ProgressionPreset != null)
                DependsOn(masterPreset.ProgressionPreset);

            if (masterPreset.PowerUpsPreset != null)
                DependsOn(masterPreset.PowerUpsPreset);

            if (masterPreset.VisualPreset != null)
                DependsOn(masterPreset.VisualPreset);

            if (masterPreset.AnimationBindingsPreset != null)
                DependsOn(masterPreset.AnimationBindingsPreset);
        }

        if (authoring.PowerUpsCheatPresetLibrary != null)
            DependsOn(authoring.PowerUpsCheatPresetLibrary);
    }

    /// <summary>
    /// Declares prefab dependencies consumed by the Laser Beam visual rig so prefab edits trigger a rebake.
    /// /params runtimeVisualBridgePrefab Resolved visual bridge prefab that may host the rig authoring component.
    /// /returns None.
    /// </summary>
    private void DeclareLaserBeamVisualRigDependencies(GameObject runtimeVisualBridgePrefab)
    {
        if (runtimeVisualBridgePrefab == null)
            return;

        DependsOn(runtimeVisualBridgePrefab);
        PlayerLaserBeamVisualRigAuthoring rigAuthoring = runtimeVisualBridgePrefab.GetComponent<PlayerLaserBeamVisualRigAuthoring>();

        if (rigAuthoring == null)
            return;

        DependsOn(rigAuthoring.BubbleBurstSourcePrefab);
        DependsOn(rigAuthoring.StarBloomSourcePrefab);
        DependsOn(rigAuthoring.SoftDiscSourcePrefab);
        DependsOn(rigAuthoring.BubbleBurstImpactPrefab);
        DependsOn(rigAuthoring.StarBloomImpactPrefab);
        DependsOn(rigAuthoring.SoftDiscImpactPrefab);
    }

    /// <summary>
    /// Resolves one prefab asset as a dynamic ECS prefab entity for power-up bake helpers.
    /// prefab: Prefab asset to resolve.
    /// returns ECS prefab entity or Entity.Null when the prefab is missing.
    /// </summary>
    private Entity ResolveDynamicPrefabEntity(GameObject prefab)
    {
        if (prefab == null)
            return Entity.Null;

        return GetEntity(prefab, TransformUsageFlags.Dynamic);
    }

    /// <summary>
    /// Resolves one power-up VFX prefab and stores the source prefab reference beside the baked entity reference.
    /// /params prefab Prefab asset to resolve.
    /// /params bindingsBuffer Player-owned buffer receiving the prefab-to-entity VFX binding.
    /// /returns ECS prefab entity or Entity.Null when the prefab is missing.
    /// </summary>
    private Entity ResolveDynamicPowerUpVfxPrefabEntity(GameObject prefab,
                                                        DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> bindingsBuffer)
    {
        Entity prefabEntity = ResolveDynamicPrefabEntity(prefab);

        if (prefab == null || prefabEntity == Entity.Null)
            return prefabEntity;

        for (int bindingIndex = 0; bindingIndex < bindingsBuffer.Length; bindingIndex++)
        {
            PlayerPowerUpVfxPrefabBindingElement binding = bindingsBuffer[bindingIndex];

            if (binding.PrefabEntity == prefabEntity)
                return prefabEntity;
        }

        bindingsBuffer.Add(new PlayerPowerUpVfxPrefabBindingElement
        {
            PrefabEntity = prefabEntity,
            Prefab = prefab
        });
        return prefabEntity;
    }
    #endregion

    #region Validation
    private void TryAddAnimatorAssetFallbackComponents(Entity entity,
                                                       Animator resolvedAnimatorComponent,
                                                       PlayerAnimationBindingsPreset animationBindingsPreset)
    {
        RuntimeAnimatorController resolvedController = PlayerAuthoringBakerValidationUtility.ResolveAnimatorController(resolvedAnimatorComponent,
                                                                                                                       animationBindingsPreset);

        if (resolvedController != null)
        {
            AddComponent(entity, new PlayerAnimatorControllerReference
            {
                Controller = resolvedController
            });
        }

        Avatar resolvedAvatar = PlayerAuthoringBakerValidationUtility.ResolveAnimatorAvatar(resolvedAnimatorComponent);

        if (resolvedAvatar != null)
        {
            AddComponent(entity, new PlayerAnimatorAvatarReference
            {
                Avatar = resolvedAvatar
            });
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Adds editor-only runtime debug buffers derived from scaling rules that enable Debug in Console.
    /// </summary>
    /// <param name="entity">Target baked player entity receiving debug buffers.</param>
    /// <param name="scaledPresetScope">Scaled preset scope containing evaluated debug snapshots for [this] and final values.</param>

    private void TryAddScalingDebugBuffers(Entity entity,
                                           PlayerScaledPresetScope scaledPresetScope)
    {
        if (UnityEditor.BuildPipeline.isBuildingPlayer)
            return;

        IReadOnlyList<PlayerScalingDebugRuleSnapshot> debugRuleSnapshots = scaledPresetScope != null
            ? scaledPresetScope.DebugRuleSnapshots
            : null;
        bool hasDebugRuleSnapshots = debugRuleSnapshots != null && debugRuleSnapshots.Count > 0;

        if (!hasDebugRuleSnapshots)
            return;

        DynamicBuffer<PlayerScalingDebugRuleElement> debugRuleBuffer = AddBuffer<PlayerScalingDebugRuleElement>(entity);

        for (int index = 0; index < debugRuleSnapshots.Count; index++)
        {
            PlayerScalingDebugRuleSnapshot snapshot = debugRuleSnapshots[index];
            string presetTypeLabel = string.IsNullOrWhiteSpace(snapshot.PresetTypeLabel) ? "Preset" : snapshot.PresetTypeLabel;
            string targetDisplayName = string.IsNullOrWhiteSpace(snapshot.TargetDisplayName) ? "Scaled Stat" : snapshot.TargetDisplayName;
            string statKey = string.IsNullOrWhiteSpace(snapshot.StatKey) ? "<unknown>" : snapshot.StatKey;
            string formula = string.IsNullOrWhiteSpace(snapshot.Formula) ? "[this]" : snapshot.Formula;
            Color debugColor = snapshot.DebugColor;
            debugRuleBuffer.Add(new PlayerScalingDebugRuleElement
            {
                PresetTypeLabel = new FixedString64Bytes(presetTypeLabel),
                TargetDisplayName = new FixedString64Bytes(targetDisplayName),
                StatKey = new FixedString128Bytes(statKey),
                Formula = new FixedString512Bytes(formula),
                ThisValue = snapshot.ThisValue,
                FinalValue = snapshot.FinalValue,
                DebugColor = new float4(debugColor.r, debugColor.g, debugColor.b, debugColor.a)
            });
        }
    }
#endif
    #endregion

}
