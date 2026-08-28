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
    /// <returns>The PlayerControllerPreset from the master preset, or null if the master preset is not set.</returns>
    public PlayerControllerPreset GetControllerPreset()
    {
        if (masterPreset == null)
            return null;

        return masterPreset.ControllerPreset;
    }

    /// <summary>
    /// Retrieves the progression preset from the master preset.
    /// </summary>
    /// <returns>The PlayerProgressionPreset from the master preset, or null if the master preset is not set.</returns>
    public PlayerProgressionPreset GetProgressionPreset()
    {
        if (masterPreset == null)
            return null;

        return masterPreset.ProgressionPreset;
    }

    /// <summary>
    /// Retrieves the power-ups preset from the master preset.
    /// </summary>
    /// <returns>The PlayerPowerUpsPreset from the master preset, or null if the master preset is not set.</returns>
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
        PlayerVisualPreset visualPreset = authoring.MasterPreset != null ? authoring.MasterPreset.VisualPreset : null;
        PlayerUiVisualPreset uiVisualPreset = authoring.MasterPreset != null ? authoring.MasterPreset.UiVisualPreset : null;
        PlayerVisualPreset sourceVisualPreset = visualPreset;
        PlayerUiVisualPreset sourceUiVisualPreset = uiVisualPreset;
        PlayerProgressionPreset sourceProgressionPreset = progressionPreset;
        PlayerPowerUpsPreset sourcePowerUpsPreset = powerUpsPreset;
        PlayerAnimationBindingsPreset animationBindingsPreset = authoring.MasterPreset != null ? authoring.MasterPreset.AnimationBindingsPreset : null;

#if UNITY_EDITOR
        PlayerScaledPresetScope scaledPresetScope = PlayerPresetScalingBakeUtility.CreateScope(controllerPreset,
                                                                                               progressionPreset,
                                                                                               powerUpsPreset,
                                                                                               visualPreset,
                                                                                               uiVisualPreset,
                                                                                               animationBindingsPreset);
        controllerPreset = scaledPresetScope.ControllerPreset;
        progressionPreset = scaledPresetScope.ProgressionPreset;
        powerUpsPreset = scaledPresetScope.PowerUpsPreset;
        visualPreset = scaledPresetScope.VisualPreset;
        uiVisualPreset = scaledPresetScope.UiVisualPreset;
        animationBindingsPreset = scaledPresetScope.AnimationBindingsPreset;

        try
        {
#endif

        IPlayerUiVisualPresetData resolvedUiVisualPreset = uiVisualPreset != null
            ? uiVisualPreset
            : visualPreset;
        IPlayerUiVisualPresetData sourceUiVisualPresetData = sourceUiVisualPreset != null
            ? sourceUiVisualPreset
            : sourceVisualPreset;

        // Create entity and build configuration blob
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        Entity visualRuntimeDataEntity = CreateAdditionalEntity(TransformUsageFlags.None,
                                                                false,
                                                                "Player Visual Runtime Data");
        AddComponent(visualRuntimeDataEntity, new PlayerVisualRuntimeDataOwner
        {
            PlayerEntity = entity
        });
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
                SmoothTime = controllerPreset.CameraSettings.Values.SmoothTime,
                MaxFollowDistance = controllerPreset.CameraSettings.Values.MaxFollowDistance,
                DeadZoneRadius = controllerPreset.CameraSettings.Values.DeadZoneRadius
            },
            Shake = PlayerControllerConfigBakeUtility.BuildCameraShakeBlob(controllerPreset.CameraSettings.DamageShake),
            FireShake = PlayerControllerConfigBakeUtility.BuildCameraFireShakeBlob(controllerPreset.CameraSettings.FireShake)
        });
        AddComponent(entity, new PlayerCameraShakeState());
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
        AddComponent(entity, new PlayerRandomStatGrowthState());
        AddBuffer<PlayerRandomStatGrowthModifierElement>(entity);
        AddComponent(entity, new PlayerRoomRewardGrantState
        {
            LastNodeIndex = -1
        });
        AddComponent(entity, new PlayerRoomRewardTemporaryState());
        AddBuffer<PlayerRoomRewardTemporaryModifierElement>(entity);
        AddBuffer<PlayerRoomRewardTemporaryResourceElement>(entity);
        AddBuffer<PlayerRoomRewardPresentationEvent>(entity);
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
            AddComponent(visualRuntimeDataEntity,
                         PlayerControllerConfigBakeUtility.BuildAnimatorParameterConfig(animationBindingsPreset));
            AddComponent(visualRuntimeDataEntity,
                         PlayerControllerConfigBakeUtility.BuildUpperBodyAnimationClipConfig(animationBindingsPreset,
                                                                                             visualPreset));
            AddComponent(visualRuntimeDataEntity, new PlayerAnimatorRuntimeState
            {
                PreviousShooting = 0,
                PreviousPrimaryCharging = 0,
                PreviousSecondaryCharging = 0,
                UpperBodyActionKind = PlayerUpperBodyAnimationActionKind.None,
                UpperBodyActionActive = 0,
                Initialized = 0,
                ParametersValidated = 0,
                BoundAnimatorInstanceId = 0,
                UpperBodyActionElapsed = 0f,
                UpperBodyActionDuration = 0f,
                RecoilValue = 0f,
                AimWeightValue = 0f,
                LeanValue = 0f,
                LastMoveX = 0f,
                LastMoveY = 1f
            });
            TryAddAnimatorAssetFallbackComponents(visualRuntimeDataEntity,
                                                  resolvedAnimatorComponent,
                                                  animationBindingsPreset);
        }

        PlayerVisualRuntimeBridgeConfig visualRuntimeBridgeConfig = new PlayerVisualRuntimeBridgeConfig
        {
            VisualPrefab = resolvedRuntimeVisualBridgePrefab,
            PositionOffset = new float3(authoring.RuntimeVisualBridgeOffset.x,
                                        authoring.RuntimeVisualBridgeOffset.y,
                                        authoring.RuntimeVisualBridgeOffset.z),
            SyncRotation = authoring.RuntimeVisualBridgeSyncRotation ? (byte)1 : (byte)0,
            SpawnWhenAnimatorMissing = authoring.SpawnRuntimeVisualBridgeWhenAnimatorMissing ? (byte)1 : (byte)0
        };
        PlayerWeaponVisualBakeUtility.ApplyRuntimeConfig(visualPreset, ref visualRuntimeBridgeConfig);
        AddComponent(visualRuntimeDataEntity, visualRuntimeBridgeConfig);
        AddComponent(visualRuntimeDataEntity, PlayerWeaponVisualBakeUtility.BuildBaseConfig(sourceVisualPreset));
        AddComponent(visualRuntimeDataEntity, new PlayerWeaponVisualScalingState());

        // Runtime + baseline mountable-weapons buffers. Runtime entries are mutated by Add Scaling rebuilds when
        // the unified scalable-stat hash changes; the baseline is the cloned source-of-truth for that rebuild.
        DynamicBuffer<PlayerAdditionalWeaponVisualElement> additionalWeaponsBuffer =
            AddBuffer<PlayerAdditionalWeaponVisualElement>(visualRuntimeDataEntity);
        PlayerWeaponVisualBakeUtility.PopulateAdditionalWeaponsBuffer(visualPreset, additionalWeaponsBuffer);
        DynamicBuffer<PlayerBaseAdditionalWeaponVisualElement> baseAdditionalWeaponsBuffer =
            AddBuffer<PlayerBaseAdditionalWeaponVisualElement>(visualRuntimeDataEntity);
        PlayerWeaponVisualBakeUtility.PopulateBaseAdditionalWeaponsBuffer(sourceVisualPreset, baseAdditionalWeaponsBuffer);

        DynamicBuffer<PlayerRuntimeWeaponVisualScalingElement> weaponVisualScalingBuffer =
            AddBuffer<PlayerRuntimeWeaponVisualScalingElement>(visualRuntimeDataEntity);
#if UNITY_EDITOR
        PlayerWeaponVisualBakeUtility.PopulateScalingMetadata(sourceVisualPreset, weaponVisualScalingBuffer);
#endif

        if (visualPreset != null)
        {
            AddComponent(visualRuntimeDataEntity, PlayerVisualVfxBakeUtility.BuildJetpackVfxConfig(visualPreset));
            AddComponent(visualRuntimeDataEntity, PlayerVisualVfxBakeUtility.BuildBaseJetpackVfxConfig(sourceVisualPreset));
            AddComponent(visualRuntimeDataEntity, new PlayerJetpackVfxScalingState());
            AddComponent(visualRuntimeDataEntity, new PlayerJetpackVfxRuntimeState());
            DynamicBuffer<PlayerRuntimeJetpackVfxScalingElement> jetpackVfxScalingBuffer = AddBuffer<PlayerRuntimeJetpackVfxScalingElement>(visualRuntimeDataEntity);
#if UNITY_EDITOR
            PlayerRuntimeScalingVisualBakeUtility.PopulateJetpackVfxScalingMetadata(sourceVisualPreset,
                                                                                    jetpackVfxScalingBuffer);
#endif
        }

        AddComponent(visualRuntimeDataEntity, PlayerGroundShadowBakeUtility.BuildConfig(visualPreset));
        AddComponent(visualRuntimeDataEntity, PlayerGroundShadowBakeUtility.BuildBaseConfig(sourceVisualPreset));
        AddComponent(visualRuntimeDataEntity, new PlayerGroundShadowScalingState());
        DynamicBuffer<PlayerRuntimeGroundShadowScalingElement> groundShadowScalingBuffer = AddBuffer<PlayerRuntimeGroundShadowScalingElement>(visualRuntimeDataEntity);
#if UNITY_EDITOR
        PlayerRuntimeScalingVisualBakeUtility.PopulateGroundShadowScalingMetadata(sourceVisualPreset,
                                                                                  groundShadowScalingBuffer);
#endif

        // Keep the large visual payload off the already dense player archetype. The player retains only a stable
        // reference, while the companion entity owns runtime/base configs and formula metadata.
        Entity healthBarVisualEntity = CreateAdditionalEntity(TransformUsageFlags.None,
                                                              false,
                                                              "Player Health Bar Visual Configuration");
        AddComponent(healthBarVisualEntity, new PlayerHealthBarVisualOwner
        {
            PlayerEntity = entity
        });
        AddComponent(healthBarVisualEntity, PlayerHealthBarVisualBakeUtility.BuildConfig(resolvedUiVisualPreset));
        AddComponent(healthBarVisualEntity, PlayerHealthBarVisualBakeUtility.BuildBaseConfig(sourceUiVisualPresetData));
        AddComponent(healthBarVisualEntity, new PlayerHealthBarVisualScalingState());
        DynamicBuffer<PlayerRuntimeHealthBarVisualScalingElement> healthBarVisualScalingBuffer = AddBuffer<PlayerRuntimeHealthBarVisualScalingElement>(healthBarVisualEntity);
#if UNITY_EDITOR
        PlayerRuntimeScalingVisualBakeUtility.PopulateHealthBarVisualScalingMetadata(sourceUiVisualPresetData,
                                                                                     healthBarVisualScalingBuffer);
#endif

        Entity activePowerUpHudVisualEntity = CreateAdditionalEntity(TransformUsageFlags.None,
                                                                     false,
                                                                     "Player Active Power-Up HUD Visual Configuration");
        AddComponent(activePowerUpHudVisualEntity, new PlayerActivePowerUpHudVisualOwner
        {
            PlayerEntity = entity
        });
        AddComponent(activePowerUpHudVisualEntity, PlayerActivePowerUpHudVisualBakeUtility.BuildConfig(resolvedUiVisualPreset));
        AddComponent(activePowerUpHudVisualEntity, PlayerActivePowerUpHudVisualBakeUtility.BuildBaseConfig(sourceUiVisualPresetData));
        AddComponent(activePowerUpHudVisualEntity, new PlayerActivePowerUpHudVisualScalingState());
        DynamicBuffer<PlayerRuntimeActivePowerUpHudVisualScalingElement> activePowerUpHudVisualScalingBuffer = AddBuffer<PlayerRuntimeActivePowerUpHudVisualScalingElement>(activePowerUpHudVisualEntity);
#if UNITY_EDITOR
        PlayerRuntimeScalingVisualBakeUtility.PopulateActivePowerUpHudVisualScalingMetadata(sourceUiVisualPresetData,
                                                                                           activePowerUpHudVisualScalingBuffer);
#endif

        Entity portraitHudVisualEntity = CreateAdditionalEntity(TransformUsageFlags.None,
                                                                false,
                                                                "Player Portrait HUD Visual Configuration");
        AddComponent(portraitHudVisualEntity, new PlayerPortraitHudVisualOwner
        {
            PlayerEntity = entity
        });
        AddComponent(portraitHudVisualEntity, PlayerHudPortraitGrowthVisualBakeUtility.BuildPortraitConfig(resolvedUiVisualPreset));
        AddComponent(portraitHudVisualEntity, PlayerHudPortraitGrowthVisualBakeUtility.BuildBasePortraitConfig(sourceUiVisualPresetData));
        AddComponent(portraitHudVisualEntity, new PlayerPortraitHudVisualScalingState());
        DynamicBuffer<PlayerPortraitHudAnimationElement> portraitAnimationBuffer = AddBuffer<PlayerPortraitHudAnimationElement>(portraitHudVisualEntity);
        DynamicBuffer<PlayerBasePortraitHudAnimationElement> basePortraitAnimationBuffer = AddBuffer<PlayerBasePortraitHudAnimationElement>(portraitHudVisualEntity);
        DynamicBuffer<PlayerPortraitHudFrameElement> portraitFrameBuffer = AddBuffer<PlayerPortraitHudFrameElement>(portraitHudVisualEntity);
        DynamicBuffer<PlayerRuntimePortraitHudVisualScalingElement> portraitHudScalingBuffer = AddBuffer<PlayerRuntimePortraitHudVisualScalingElement>(portraitHudVisualEntity);
        PlayerHudPortraitGrowthVisualBakeUtility.PopulatePortraitBuffers(resolvedUiVisualPreset,
                                                                         portraitAnimationBuffer,
                                                                         portraitFrameBuffer);
        PlayerHudPortraitGrowthVisualBakeUtility.PopulateBasePortraitBuffers(sourceUiVisualPresetData,
                                                                             basePortraitAnimationBuffer);
#if UNITY_EDITOR
        PlayerRuntimeScalingVisualBakeUtility.PopulatePortraitHudVisualScalingMetadata(sourceUiVisualPresetData,
                                                                                       portraitHudScalingBuffer);
#endif

        Entity growthSequenceHudVisualEntity = CreateAdditionalEntity(TransformUsageFlags.None,
                                                                      false,
                                                                      "Player Growth Sequence HUD Visual Configuration");
        AddComponent(growthSequenceHudVisualEntity, new PlayerGrowthSequenceHudVisualOwner
        {
            PlayerEntity = entity
        });
        AddComponent(growthSequenceHudVisualEntity, PlayerHudGrowthSequenceVisualBakeUtility.BuildGrowthSequenceConfig(resolvedUiVisualPreset));
        AddComponent(growthSequenceHudVisualEntity, PlayerHudGrowthSequenceVisualBakeUtility.BuildBaseGrowthSequenceConfig(sourceUiVisualPresetData));
        AddComponent(growthSequenceHudVisualEntity, new PlayerGrowthSequenceHudVisualScalingState());
        DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> growthSequenceStepBuffer = AddBuffer<PlayerGrowthSequenceHudStepVisualElement>(growthSequenceHudVisualEntity);
        DynamicBuffer<PlayerBaseGrowthSequenceHudStepVisualElement> baseGrowthSequenceStepBuffer = AddBuffer<PlayerBaseGrowthSequenceHudStepVisualElement>(growthSequenceHudVisualEntity);
        DynamicBuffer<PlayerRuntimeGrowthSequenceHudVisualScalingElement> growthSequenceHudScalingBuffer = AddBuffer<PlayerRuntimeGrowthSequenceHudVisualScalingElement>(growthSequenceHudVisualEntity);
        PlayerHudGrowthSequenceVisualBakeUtility.PopulateGrowthSequenceBuffer(resolvedUiVisualPreset,
                                                                              progressionPreset,
                                                                              growthSequenceStepBuffer);
        PlayerHudGrowthSequenceVisualBakeUtility.PopulateBaseGrowthSequenceBuffer(sourceUiVisualPresetData,
                                                                                  sourceProgressionPreset,
                                                                                  baseGrowthSequenceStepBuffer);
#if UNITY_EDITOR
        PlayerRuntimeScalingVisualBakeUtility.PopulateGrowthSequenceHudVisualScalingMetadata(sourceUiVisualPresetData,
                                                                                            growthSequenceHudScalingBuffer);
#endif
        AddComponent(entity, new PlayerPresentationRuntimeReferences
        {
            VisualRuntimeEntity = visualRuntimeDataEntity,
            HealthBarVisualEntity = healthBarVisualEntity,
            ActivePowerUpHudVisualEntity = activePowerUpHudVisualEntity,
            PortraitHudVisualEntity = portraitHudVisualEntity,
            GrowthSequenceHudVisualEntity = growthSequenceHudVisualEntity
        });

        // Conditional weapon switches authored in the controller preset are baked into a dedicated ECS table.
        // The dedicated system evaluates the table against the player's scalable stats and writes the winning
        // entry into PlayerConditionalWeaponSwitchState so the animator presentation pipeline can override the
        // equipped Switch Weapon power-up when an authored entry opts in.
        PlayerConditionalWeaponSwitchSettings conditionalWeaponSwitchSettings = controllerPreset != null && controllerPreset.ShootingSettings != null
            ? controllerPreset.ShootingSettings.ConditionalWeaponSwitches
            : null;
        PlayerConditionalWeaponSwitchSettings sourceConditionalWeaponSwitchSettings = sourceControllerPreset != null &&
                                                                                       sourceControllerPreset.ShootingSettings != null
            ? sourceControllerPreset.ShootingSettings.ConditionalWeaponSwitches
            : null;
        AddComponent(entity, PlayerConditionalWeaponSwitchBakeUtility.BuildConfig(conditionalWeaponSwitchSettings));
        AddComponent(entity, PlayerConditionalWeaponSwitchBakeUtility.BuildInitialState());
        AddComponent(entity, new PlayerConditionalWeaponSwitchScalingState());
        DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> conditionalEntryBuffer = AddBuffer<PlayerConditionalWeaponSwitchEntryElement>(entity);
        DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> conditionalConditionBuffer = AddBuffer<PlayerConditionalWeaponSwitchConditionElement>(entity);
        DynamicBuffer<PlayerBaseConditionalWeaponSwitchEntryElement> baseConditionalEntryBuffer = AddBuffer<PlayerBaseConditionalWeaponSwitchEntryElement>(entity);
        DynamicBuffer<PlayerBaseConditionalWeaponSwitchConditionElement> baseConditionalConditionBuffer = AddBuffer<PlayerBaseConditionalWeaponSwitchConditionElement>(entity);
        DynamicBuffer<PlayerRuntimeConditionalWeaponSwitchScalingElement> conditionalScalingBuffer = AddBuffer<PlayerRuntimeConditionalWeaponSwitchScalingElement>(entity);
        PlayerConditionalWeaponSwitchBakeUtility.PopulateBuffers(conditionalWeaponSwitchSettings,
                                                                  conditionalEntryBuffer,
                                                                  conditionalConditionBuffer);
        PlayerConditionalWeaponSwitchBakeUtility.PopulateBaseBuffers(sourceConditionalWeaponSwitchSettings,
                                                                      baseConditionalEntryBuffer,
                                                                      baseConditionalConditionBuffer);
#if UNITY_EDITOR
        PlayerConditionalWeaponSwitchBakeUtility.PopulateScalingMetadata(sourceControllerPreset, conditionalScalingBuffer);
#endif
        AddComponent(visualRuntimeDataEntity, new OutlineVisualConfig
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

        // Damage Feedback vignette config + state. The presentation system seeds previous health/shield on the first frame so the initial bake values stay neutral.
        if (PlayerDamageVignetteBakeUtility.TryBuildConfig(visualPreset, out PlayerDamageVignetteConfig damageVignetteConfig))
        {
            AddComponent(entity, damageVignetteConfig);
            AddComponent(entity, PlayerDamageVignetteBakeUtility.BuildInitialState());
        }

        // Death animation config + state. Always present on the player entity (defaults are used when no visual preset
        // authors the section) so PlayerRunOutcomeSystem can always read the dying-window duration from one place.
        PlayerDeathAnimationBakeUtility.BuildConfig(visualPreset,
                                                     out PlayerDeathAnimationConfig deathAnimationConfig,
                                                     out GameObject deathAnimationDespawnVfxPrefab);
        AddComponent(entity, PlayerDeathAnimationBakeUtility.BuildBaseConfig(sourceVisualPreset));
        AddComponent(entity, deathAnimationConfig);
        AddComponent(entity, PlayerDeathAnimationBakeUtility.BuildInitialState());
        AddComponentObject(entity, new PlayerDeathAnimationManagedConfig
        {
            DespawnVfxPrefab = deathAnimationDespawnVfxPrefab
        });
        DynamicBuffer<PlayerRuntimeDeathAnimationScalingElement> deathAnimationScalingBuffer = AddBuffer<PlayerRuntimeDeathAnimationScalingElement>(entity);
#if UNITY_EDITOR
        PlayerRuntimeScalingVisualBakeUtility.PopulateDeathAnimationScalingMetadata(sourceVisualPreset, deathAnimationScalingBuffer);
#endif
        Vector3 authoringPosition = authoring.transform.position;
        Quaternion authoringRotation = authoring.transform.rotation;
        AddComponent(entity, new PlayerAnimatedMuzzleWorldPose
        {
            Position = new float3(authoringPosition.x, authoringPosition.y, authoringPosition.z),
            Rotation = new quaternion(authoringRotation.x, authoringRotation.y, authoringRotation.z, authoringRotation.w),
            LocalPosition = float3.zero,
            ForwardShotOffset = 0f,
            MinimumPlanarDistanceFromPlayer = 0f,
            IsValid = 0
        });
        AddComponent(entity, PlayerLaserBeamVisualBakeUtility.BuildConfig(authoring));
        DynamicBuffer<PlayerLaserBeamSourceVariantElement> laserBeamSourceVariantBuffer = AddBuffer<PlayerLaserBeamSourceVariantElement>(entity);
        DynamicBuffer<PlayerLaserBeamImpactVariantElement> laserBeamImpactVariantBuffer = AddBuffer<PlayerLaserBeamImpactVariantElement>(entity);
        DynamicBuffer<PlayerLaserBeamVisualPresetElement> laserBeamVisualPresetBuffer = AddBuffer<PlayerLaserBeamVisualPresetElement>(entity);
        PlayerLaserBeamVisualBakeUtility.PopulateSourceVariantBuffer(authoring, laserBeamSourceVariantBuffer);
        PlayerLaserBeamVisualBakeUtility.PopulateImpactVariantBuffer(authoring, laserBeamImpactVariantBuffer);
        PlayerLaserBeamVisualBakeUtility.PopulateVisualPresetBuffer(authoring, laserBeamVisualPresetBuffer);

        // Bake the optional aiming laser pointer that reuses the Laser Beam body material and palette colors.
        if (PlayerVisualPointerBakeUtility.TryBuildConfig(visualPreset,
                                                          controllerPreset.ShootingSettings.Values,
                                                          out PlayerVisualPointerConfig visualPointerConfig))
        {
            AddComponent(entity, visualPointerConfig);
        }
        DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> powerUpVfxPrefabBindingsBuffer = default;
        bool hasPowerUpVfxRuntime = false;

        if (visualPreset != null &&
            (visualPreset.LevelUpVfxPrefab != null ||
             visualPreset.HealthIncreaseVfxPrefab != null ||
             visualPreset.ShieldIncreaseVfxPrefab != null ||
             visualPreset.ChargeShotVfxPrefab != null ||
             visualPreset.PlayerProjectileVfxPrefab != null ||
             (visualPreset.ProjectileDeathVfx != null && visualPreset.ProjectileDeathVfx.HasAnyPrefab) ||
             visualPreset.MuzzleFlashVfxPrefab != null))
        {
            EnsurePowerUpVfxRuntime(authoring,
                                    entity,
                                    ref hasPowerUpVfxRuntime,
                                    ref powerUpVfxPrefabBindingsBuffer);
            Func<GameObject, Entity> resolveVisualVfxPrefabEntity = (GameObject prefab) =>
                ResolveDynamicPowerUpVfxPrefabEntity(prefab, powerUpVfxPrefabBindingsBuffer);

            if (PlayerVisualVfxBakeUtility.TryBuildLevelUpVfxConfig(visualPreset,
                                                                     resolveVisualVfxPrefabEntity,
                                                                     out PlayerLevelUpVfxConfig levelUpVfxConfig))
            {
                AddComponent(entity, levelUpVfxConfig);
            }

            bool hasStatIncreaseVfx = false;

            if (PlayerVisualVfxBakeUtility.TryBuildHealthIncreaseVfxConfig(visualPreset,
                                                                           resolveVisualVfxPrefabEntity,
                                                                           out PlayerHealthIncreaseVfxConfig healthIncreaseVfxConfig))
            {
                AddComponent(entity, healthIncreaseVfxConfig);
                hasStatIncreaseVfx = true;
            }

            if (PlayerVisualVfxBakeUtility.TryBuildShieldIncreaseVfxConfig(visualPreset,
                                                                           resolveVisualVfxPrefabEntity,
                                                                           out PlayerShieldIncreaseVfxConfig shieldIncreaseVfxConfig))
            {
                AddComponent(entity, shieldIncreaseVfxConfig);
                hasStatIncreaseVfx = true;
            }

            if (hasStatIncreaseVfx)
                AddComponent(entity, new PlayerStatIncreaseVfxRuntimeState());

            if (PlayerVisualVfxBakeUtility.TryBuildChargeShotVfxConfig(visualPreset,
                                                                        resolveVisualVfxPrefabEntity,
                                                                        out PlayerChargeShotVfxConfig chargeShotVfxConfig))
            {
                AddComponent(entity, chargeShotVfxConfig);
                AddComponent(entity, new PlayerChargeShotVfxRuntimeState());
            }

            if (PlayerVisualVfxBakeUtility.TryBuildProjectileAttachedVfxConfig(visualPreset,
                                                                               resolveVisualVfxPrefabEntity,
                                                                               out PlayerProjectileAttachedVfxConfig projectileAttachedVfxConfig))
            {
                AddComponent(entity, projectileAttachedVfxConfig);
            }

            if (PlayerVisualVfxBakeUtility.TryBuildProjectileDeathVfxConfig(visualPreset,
                                                                            resolveVisualVfxPrefabEntity,
                                                                            out PlayerProjectileDeathVfxConfig projectileDeathVfxConfig))
            {
                AddComponent(entity, projectileDeathVfxConfig);
                AddComponent(entity, PlayerVisualVfxBakeUtility.BuildBaseProjectileDeathVfxConfig(sourceVisualPreset,
                                                                                                   resolveVisualVfxPrefabEntity));
                AddComponent(entity, new PlayerProjectileDeathVfxScalingState());
                DynamicBuffer<PlayerRuntimeProjectileDeathVfxScalingElement> projectileDeathVfxScalingBuffer = AddBuffer<PlayerRuntimeProjectileDeathVfxScalingElement>(entity);
#if UNITY_EDITOR
                PlayerRuntimeScalingVisualBakeUtility.PopulateProjectileDeathVfxScalingMetadata(sourceVisualPreset,
                                                                                                projectileDeathVfxScalingBuffer);
#endif
            }

            if (PlayerVisualVfxBakeUtility.TryBuildMuzzleFlashVfxConfig(visualPreset,
                                                                        resolveVisualVfxPrefabEntity,
                                                                        out PlayerMuzzleFlashVfxConfig muzzleFlashVfxConfig))
            {
                AddComponent(entity, muzzleFlashVfxConfig);
            }
        }

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
            EnsurePowerUpVfxRuntime(authoring,
                                    entity,
                                    ref hasPowerUpVfxRuntime,
                                    ref powerUpVfxPrefabBindingsBuffer);
            DynamicBuffer<PlayerOrbitalProjectionPrefabElement> orbitalProjectionPrefabBindingsBuffer = AddBuffer<PlayerOrbitalProjectionPrefabElement>(entity);
            DynamicBuffer<PlayerOrbitalProjectionHullVertexElement> orbitalProjectionHullVerticesBuffer = AddBuffer<PlayerOrbitalProjectionHullVertexElement>(entity);
            AddBuffer<PlayerOrbitalProjectionLostElement>(entity);
            Func<GameObject, Entity> resolveDynamicPowerUpVfxPrefabEntity = (GameObject prefab) =>
                ResolveDynamicPowerUpVfxPrefabEntity(prefab, powerUpVfxPrefabBindingsBuffer);
            Func<GameObject, int> resolveOrbitalProjectionPrefabBindingIndex = (GameObject prefab) =>
                ResolveOrbitalProjectionPrefabBindingIndex(prefab, orbitalProjectionPrefabBindingsBuffer, orbitalProjectionHullVerticesBuffer);
            PlayerPowerUpSlotConfig primaryPowerUpSlotConfig;
            PlayerPowerUpSlotConfig secondaryPowerUpSlotConfig;
            PlayerPowerUpActiveBakeUtility.BuildPowerUpSlots(authoring,
                                                             powerUpsPreset,
                                                             resolveDynamicPowerUpVfxPrefabEntity,
                                                             out primaryPowerUpSlotConfig,
                                                             out secondaryPowerUpSlotConfig,
                                                             resolveOrbitalProjectionPrefabBindingIndex);
            DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer = AddBuffer<PlayerPowerUpsConfigElement>(entity);
            PlayerPowerUpsConfigBufferUtility.WriteSlots(powerUpsConfigBuffer,
                                                         in primaryPowerUpSlotConfig,
                                                         in secondaryPowerUpSlotConfig);
            AddComponent(entity, new PlayerLaserBeamState
            {
                NextStormTickPulseId = 1
            });
            AddBuffer<PlayerLaserBeamStormTickPulse>(entity);
            AddBuffer<PlayerLaserBeamLaneElement>(entity);
            AddBuffer<PlayerLaserBeamPulseHitElement>(entity);
            AddComponent(entity, new PlayerChargeCharacterTuningState());
            AddBuffer<PlayerChargeCharacterTuningBaseStatElement>(entity);
            AddBuffer<PlayerProjectileSizePowerUpMultiplierElement>(entity);
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
                                                                               equippedPassiveToolsBuffer,
                                                                               resolveOrbitalProjectionPrefabBindingIndex);
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
                                                                             powerUpTierEntryScalingBuffer,
                                                                             resolveOrbitalProjectionPrefabBindingIndex);
            PlayerRuntimeScalingBakeUtility.PopulatePowerUpBaseConfigs(authoring,
                                                                       sourcePowerUpsPreset,
                                                                       resolveDynamicPowerUpVfxPrefabEntity,
                                                                       powerUpBaseConfigBuffer,
                                                                       resolveOrbitalProjectionPrefabBindingIndex);
#if UNITY_EDITOR
            PlayerRuntimeScalingBakeUtility.PopulatePowerUpScalingMetadata(sourcePowerUpsPreset, powerUpScalingBuffer);
#endif
            DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntriesBuffer = AddBuffer<PlayerPowerUpCheatPresetEntry>(entity);
            DynamicBuffer<PlayerPowerUpCheatPresetSlotElement> cheatPresetSlotsBuffer = AddBuffer<PlayerPowerUpCheatPresetSlotElement>(entity);
            DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassivesBuffer = AddBuffer<PlayerPowerUpCheatPresetPassiveElement>(entity);
            PlayerPowerUpCatalogBakeUtility.PopulatePowerUpCheatPresetBuffers(authoring,
                                                                              resolveDynamicPowerUpVfxPrefabEntity,
                                                                              cheatPresetEntriesBuffer,
                                                                              cheatPresetSlotsBuffer,
                                                                              cheatPresetPassivesBuffer,
                                                                              resolveOrbitalProjectionPrefabBindingIndex);
            AddBuffer<PlayerOrbitalProjectionSpawnRequest>(entity);
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
    /// Adds shared managed VFX buffers and caps once for visual-preset and power-up runtime VFX requests.
    /// </summary>
    /// <param name="authoring">Source authoring component used to resolve VFX cap settings.</param>
    /// <param name="entity">Player entity receiving the managed VFX runtime buffers.</param>
    /// <param name="hasRuntime">Mutable guard that prevents duplicate buffer and component additions.</param>
    /// <param name="prefabBindingsBuffer">Binding buffer returned for prefab registration.</param>
    private void EnsurePowerUpVfxRuntime(PlayerAuthoring authoring,
                                         Entity entity,
                                         ref bool hasRuntime,
                                         ref DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindingsBuffer)
    {
        if (hasRuntime)
            return;

        AddBuffer<PlayerPowerUpVfxSpawnRequest>(entity);
        prefabBindingsBuffer = AddBuffer<PlayerPowerUpVfxPrefabBindingElement>(entity);
        AddComponent(entity, PlayerPowerUpBakeSharedUtility.BuildPowerUpVfxCapConfig(authoring));
        hasRuntime = true;
    }

    /// <summary>
    /// Declares preset dependencies consumed by this baker so editing preset assets triggers a player rebake.
    /// </summary>
    /// <param name="authoring">Source authoring component used to resolve all preset references.</param>
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

            if (masterPreset.UiVisualPreset != null)
                DependsOn(masterPreset.UiVisualPreset);

            if (masterPreset.AnimationBindingsPreset != null)
                DependsOn(masterPreset.AnimationBindingsPreset);
        }

        if (authoring.PowerUpsCheatPresetLibrary != null)
            DependsOn(authoring.PowerUpsCheatPresetLibrary);
    }

    /// <summary>
    /// Declares prefab dependencies consumed by the Laser Beam visual rig so prefab edits trigger a rebake.
    /// </summary>
    /// <param name="runtimeVisualBridgePrefab">Resolved visual bridge prefab that may host the rig authoring component.</param>
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
    /// </summary>
    /// <param name="prefab">Prefab asset to resolve.</param>
    /// <returns>ECS prefab entity or Entity.Null when the prefab is missing.</returns>
    private Entity ResolveDynamicPrefabEntity(GameObject prefab)
    {
        if (prefab == null)
            return Entity.Null;

        return GetEntity(prefab, TransformUsageFlags.Dynamic);
    }

    /// <summary>
    /// Resolves one power-up VFX prefab and stores the source prefab reference beside the baked entity reference.
    /// </summary>
    /// <param name="prefab">Prefab asset to resolve.</param>
    /// <param name="bindingsBuffer">Player-owned buffer receiving the prefab-to-entity VFX binding.</param>
    /// <returns>ECS prefab entity or Entity.Null when the prefab is missing.</returns>
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

    /// <summary>
    /// Registers one orbital projection prefab in a remappable player-owned binding table and bakes
    /// its XZ collision hull into the parallel hull table on first registration.
    /// </summary>
    /// <param name="prefab">Prefab asset referenced by an orbital projection config.</param>
    /// <param name="bindingsBuffer">Player-owned buffer receiving remappable prefab entities.</param>
    /// <param name="hullVerticesBuffer">Player-owned buffer receiving binding-indexed hull vertices.</param>
    /// <returns>Stable binding index used by fixed-list projection configs, or -1 when the prefab is missing.</returns>
    private int ResolveOrbitalProjectionPrefabBindingIndex(GameObject prefab,
                                                           DynamicBuffer<PlayerOrbitalProjectionPrefabElement> bindingsBuffer,
                                                           DynamicBuffer<PlayerOrbitalProjectionHullVertexElement> hullVerticesBuffer)
    {
        Entity prefabEntity = ResolveDynamicPrefabEntity(prefab);

        if (prefab == null || prefabEntity == Entity.Null)
            return -1;

        for (int bindingIndex = 0; bindingIndex < bindingsBuffer.Length; bindingIndex++)
        {
            PlayerOrbitalProjectionPrefabElement binding = bindingsBuffer[bindingIndex];

            if (binding.PrefabEntity == prefabEntity)
                return binding.BindingIndex;
        }

        int newBindingIndex = bindingsBuffer.Length;
        bindingsBuffer.Add(new PlayerOrbitalProjectionPrefabElement
        {
            BindingIndex = newBindingIndex,
            PrefabEntity = prefabEntity
        });

        // Bake the model silhouette once per unique prefab so Adapt Collision To Model projections
        // can copy it at spawn; prefabs without usable meshes simply contribute no hull entries.
        List<float2> hullVertices = new List<float2>(PlayerOrbitalProjectionCollisionHullBakeUtility.MaximumHullVertices);

        if (PlayerOrbitalProjectionCollisionHullBakeUtility.TryBuildHull(prefab, hullVertices))
        {
            for (int vertexIndex = 0; vertexIndex < hullVertices.Count; vertexIndex++)
            {
                hullVerticesBuffer.Add(new PlayerOrbitalProjectionHullVertexElement
                {
                    BindingIndex = newBindingIndex,
                    LocalPositionXZ = hullVertices[vertexIndex]
                });
            }
        }

        return newBindingIndex;
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
