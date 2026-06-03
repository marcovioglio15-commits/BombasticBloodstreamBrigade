using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Authoring component that defines ECS enemy movement, combat and presentation settings.
/// Main configuration is sourced from EnemyMasterPreset and its linked sub-presets.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyAuthoring : MonoBehaviour
{
    #region Constants
    private const EnemyVisualMode DefaultVisualMode = EnemyVisualMode.GpuBaked;
    private const float DefaultVisualAnimationSpeed = 1f;
    private const float DefaultGpuAnimationLoopDuration = 1f;
    private const float DefaultMaxVisibleDistance = 55f;
    private const float DefaultVisibleDistanceHysteresis = 6f;
    private const float DefaultHitVfxLifetimeSeconds = 0.35f;
    private const float DefaultHitVfxScaleMultiplier = 1f;
    private const float DefaultSpawnVfxLifetimeSeconds = 0.5f;
    private const float DefaultSpawnVfxScaleMultiplier = 1f;
    private const float DefaultDeathVfxLifetimeSeconds = 0.75f;
    private const float DefaultDeathVfxScaleMultiplier = 1f;
    private const float DefaultSpatialUiRingThickness = 0.08f;
    private const float DefaultSpatialUiRingSpacing = 0.03f;
    private const float DefaultSpatialUiHeightOffset = 0.035f;
    private const float DefaultRingDistanceFromShadow = 0.05f;
    private const bool DefaultHealthRingsEnabled = true;
    private static readonly Vector2 DefaultPositionOffsetXZ = Vector2.zero;
    private const float DefaultShadowAlpha = 1f;
    private const float DefaultShadowEdgeSoftness = 0.08f;
    private const float DefaultRingEdgeSoftness = 0.05f;
    private const float DefaultRingAngularSoftness = 0.02f;
    private const bool DefaultLockRingsToWorld = false;
    private const float DefaultLockedRingsWorldAngleDegrees = 0f;
    private static readonly Color DefaultShadowColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color DefaultHealthRingFillColor = new Color(0.92f, 0.18f, 0.16f, 0.95f);
    private static readonly Color DefaultHealthRingBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 0.7f);
    private static readonly Color DefaultShieldRingFillColor = new Color(0.25f, 0.85f, 1f, 0.95f);
    private static readonly Color DefaultShieldRingBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 0.7f);
    private static readonly Color DefaultDamageFlashColor = new Color(1f, 0.15f, 0.15f, 1f);
    private static readonly Color DefaultOutlineColor = Color.black;
    private const float DefaultDamageFlashDurationSeconds = 0.06f;
    private const float DefaultDamageFlashMaximumBlend = 0.85f;
    private const float DefaultOutlineThickness = 1f;
    private const int GizmoEllipseSegmentCount = 48;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Preset")]
    [Tooltip("Enemy master preset that resolves sub-presets used by this enemy.")]
    [SerializeField] private EnemyMasterPreset masterPreset;

    [Tooltip("Direct brain preset fallback used when MasterPreset is missing or has no Brain preset assigned.")]
    [SerializeField] private EnemyBrainPreset brainPreset;

    [Tooltip("Direct visual preset fallback used when MasterPreset is missing or has no Visual preset assigned.")]
    [SerializeField] private EnemyVisualPreset visualPreset;

    [Tooltip("Direct advanced pattern preset fallback used when MasterPreset is missing or has no Advanced Pattern preset assigned.")]
    [SerializeField] private EnemyAdvancedPatternPreset advancedPatternPreset;

    [Tooltip("Direct boss pattern preset fallback used when MasterPreset is missing or has no Boss Pattern preset assigned.")]
    [SerializeField] private EnemyBossPatternPreset bossPatternPreset;

    [Tooltip("Fallback move speed used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float moveSpeed = 3f;

    [Tooltip("Fallback max speed used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxSpeed = 4f;

    [Tooltip("Fallback acceleration used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float acceleration = 8f;

    [Tooltip("Fallback deceleration used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float deceleration = 8f;

    [Tooltip("Fallback post-spawn inactivity duration used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float inactivityTime;

    [Tooltip("Fallback self-rotation speed in degrees per second used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float rotationSpeedDegreesPerSecond;

    [Tooltip("Fallback extra distance in meters kept from static wall colliders by standard steering-driven enemies when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float minimumWallDistance = 0.25f;

    [Tooltip("Fallback separation radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float separationRadius = 1.1f;

    [Tooltip("Fallback separation weight used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float separationWeight = 2f;

    [Tooltip("Fallback body radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float bodyRadius = 0.55f;

    [Tooltip("Fallback horizontal X scale applied to Body Radius when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float bodyRadiusXScale = 1f;

    [Tooltip("Fallback horizontal Z scale applied to Body Radius when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float bodyRadiusZScale = 1f;

    [Tooltip("Fallback contact radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float contactRadius = 1.2f;

    [Tooltip("Fallback contact damage enable used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private bool contactDamageEnabled = true;

    [Tooltip("Fallback contact amount per tick used when MasterPreset and BrainPreset are missing.")]
    [FormerlySerializedAs("contactDamage")]
    [SerializeField]
    [HideInInspector] private float contactAmountPerTick = 5f;

    [Tooltip("Fallback contact tick interval used when MasterPreset and BrainPreset are missing.")]
    [FormerlySerializedAs("contactInterval")]
    [SerializeField]
    [HideInInspector] private float contactTickInterval = 0.75f;

    [Tooltip("Fallback area damage enable used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private bool areaDamageEnabled;

    [Tooltip("Fallback area radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float areaRadius = 2.25f;

    [Tooltip("Fallback area amount per tick percent used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float areaAmountPerTickPercent = 2f;

    [Tooltip("Fallback area tick interval used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float areaTickInterval = 1f;

    [Tooltip("Fallback max health used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxHealth = 30f;

    [Tooltip("Fallback max shield used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxShield;

    [Tooltip("Fallback general priority tier used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private int priorityTier;

    [Tooltip("Fallback steering and clearance reactivity scalar used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float steeringAggressiveness = 1f;

    [Header("Visual References")]
    [Tooltip("Optional Animator used when visual mode is CompanionAnimator.")]
    [SerializeField] private Animator animatorComponent;

    [Tooltip("Optional transform used as anchor for attached elemental status VFX.")]
    [SerializeField] private Transform elementalVfxAnchor;

    [Tooltip("Optional ground-indicator view that renders the shader-driven shadow plus fillable health and shield rings on the floor under this enemy.")]
    [FormerlySerializedAs("worldSpaceStatusBarsView")]
    [SerializeField] private EnemyGroundIndicatorView groundIndicatorView;

    [Tooltip("Optional billboard sprite view used by offensive engagement feedback before short-range or weapon attacks commit.")]
    [SerializeField] private EnemyOffensiveEngagementBillboardView offensiveEngagementBillboardView;
    #endregion

    #endregion

    #region Properties
    public EnemyMasterPreset MasterPreset
    {
        get
        {
            return masterPreset;
        }
    }

    public EnemyBrainPreset BrainPreset
    {
        get
        {
            return brainPreset;
        }
    }

    public EnemyVisualPreset VisualPreset
    {
        get
        {
            return EnemyAuthoringPresetResolverUtility.ResolveVisualPreset(masterPreset, visualPreset);
        }
    }

    public EnemyAdvancedPatternPreset AdvancedPatternPreset
    {
        get
        {
            return EnemyAuthoringPresetResolverUtility.ResolveAdvancedPatternPreset(masterPreset, advancedPatternPreset);
        }
    }

    public EnemyBossPatternPreset BossPatternPreset
    {
        get
        {
            return EnemyAuthoringPresetResolverUtility.ResolveBossPatternPreset(masterPreset, bossPatternPreset);
        }
    }

    public float MoveSpeed
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings == null)
                return moveSpeed;

            return settings.MoveSpeed;
        }
    }

    public float MaxSpeed
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings == null)
                return maxSpeed;

            return settings.MaxSpeed;
        }
    }

    public float Acceleration
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings == null)
                return acceleration;

            return settings.Acceleration;
        }
    }

    public float Deceleration
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings == null)
                return deceleration;

            return settings.Deceleration;
        }
    }

    public float InactivityTime
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings == null)
                return math.max(0f, inactivityTime);

            return math.max(0f, settings.InactivityTime);
        }
    }

    public float RotationSpeedDegreesPerSecond
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings == null)
                return rotationSpeedDegreesPerSecond;

            return settings.RotationSpeedDegreesPerSecond;
        }
    }

    public float MinimumWallDistance
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings == null)
                return minimumWallDistance;

            return settings.MinimumWallDistance;
        }
    }

    public bool DisablePlayerKnockback
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings == null)
                return false;

            return settings.DisablePlayerKnockback;
        }
    }

    public float SeparationRadius
    {
        get
        {
            EnemyBrainSteeringSettings settings = ResolveSteeringSettings();

            if (settings == null)
                return separationRadius;

            return settings.SeparationRadius;
        }
    }

    public float SeparationWeight
    {
        get
        {
            EnemyBrainSteeringSettings settings = ResolveSteeringSettings();

            if (settings == null)
                return separationWeight;

            return settings.SeparationWeight;
        }
    }

    public float BodyRadius
    {
        get
        {
            return math.max(BodyRadiusX, BodyRadiusZ);
        }
    }

    public float BodyRadiusX
    {
        get
        {
            EnemyBrainSteeringSettings settings = ResolveSteeringSettings();

            if (settings == null)
                return ResolveScaledBodyRadius(bodyRadius, bodyRadiusXScale);

            return settings.BodyRadiusX;
        }
    }

    public float BodyRadiusZ
    {
        get
        {
            EnemyBrainSteeringSettings settings = ResolveSteeringSettings();

            if (settings == null)
                return ResolveScaledBodyRadius(bodyRadius, bodyRadiusZScale);

            return settings.BodyRadiusZ;
        }
    }

    public float ContactRadius
    {
        get
        {
            EnemyBrainDamageSettings settings = ResolveDamageSettings();

            if (settings == null)
                return contactRadius;

            return settings.ContactRadius;
        }
    }

    public bool ContactDamageEnabled
    {
        get
        {
            EnemyBrainDamageSettings settings = ResolveDamageSettings();

            if (settings == null)
                return contactDamageEnabled;

            return settings.ContactDamageEnabled;
        }
    }

    public float ContactAmountPerTick
    {
        get
        {
            EnemyBrainDamageSettings settings = ResolveDamageSettings();

            if (settings == null)
                return contactAmountPerTick;

            return settings.ContactAmountPerTick;
        }
    }

    public float ContactTickInterval
    {
        get
        {
            EnemyBrainDamageSettings settings = ResolveDamageSettings();

            if (settings == null)
                return contactTickInterval;

            return settings.ContactTickInterval;
        }
    }

    public bool AreaDamageEnabled
    {
        get
        {
            EnemyBrainDamageSettings settings = ResolveDamageSettings();

            if (settings == null)
                return areaDamageEnabled;

            return settings.AreaDamageEnabled;
        }
    }

    public float AreaRadius
    {
        get
        {
            EnemyBrainDamageSettings settings = ResolveDamageSettings();

            if (settings == null)
                return areaRadius;

            return settings.AreaRadius;
        }
    }

    public float AreaAmountPerTickPercent
    {
        get
        {
            EnemyBrainDamageSettings settings = ResolveDamageSettings();

            if (settings == null)
                return areaAmountPerTickPercent;

            return settings.AreaAmountPerTickPercent;
        }
    }

    public float AreaTickInterval
    {
        get
        {
            EnemyBrainDamageSettings settings = ResolveDamageSettings();

            if (settings == null)
                return areaTickInterval;

            return settings.AreaTickInterval;
        }
    }

    public float MaxHealth
    {
        get
        {
            EnemyBrainHealthStatisticsSettings settings = ResolveHealthSettings();

            if (settings == null)
                return maxHealth;

            return settings.MaxHealth;
        }
    }

    public float MaxShield
    {
        get
        {
            EnemyBrainHealthStatisticsSettings settings = ResolveHealthSettings();

            if (settings == null)
                return maxShield;

            return settings.MaxShield;
        }
    }

    public EnemyVisualMode VisualMode
    {
        get
        {
            EnemyVisualVisibilitySettings settings = ResolveVisibilitySettings();

            if (settings == null)
                return DefaultVisualMode;

            return settings.VisualMode;
        }
    }

    public float VisualAnimationSpeed
    {
        get
        {
            EnemyVisualVisibilitySettings settings = ResolveVisibilitySettings();

            if (settings == null)
                return DefaultVisualAnimationSpeed;

            return settings.VisualAnimationSpeed;
        }
    }

    public float GpuAnimationLoopDuration
    {
        get
        {
            EnemyVisualVisibilitySettings settings = ResolveVisibilitySettings();

            if (settings == null)
                return DefaultGpuAnimationLoopDuration;

            return settings.GpuAnimationLoopDuration;
        }
    }

    public bool EnableDistanceCulling
    {
        get
        {
            EnemyVisualVisibilitySettings settings = ResolveVisibilitySettings();

            if (settings == null)
                return true;

            return settings.EnableDistanceCulling;
        }
    }

    public float MaxVisibleDistance
    {
        get
        {
            EnemyVisualVisibilitySettings settings = ResolveVisibilitySettings();

            if (settings == null)
                return DefaultMaxVisibleDistance;

            return settings.MaxVisibleDistance;
        }
    }

    public float VisibleDistanceHysteresis
    {
        get
        {
            EnemyVisualVisibilitySettings settings = ResolveVisibilitySettings();

            if (settings == null)
                return DefaultVisibleDistanceHysteresis;

            return settings.VisibleDistanceHysteresis;
        }
    }

    public int PriorityTier
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings != null)
                return math.clamp(settings.PriorityTier, -128, 128);

            return math.clamp(priorityTier, -128, 128);
        }
    }

    public float SteeringAggressiveness
    {
        get
        {
            EnemyBrainMovementSettings settings = ResolveMovementSettings();

            if (settings != null)
            {
                float resolvedAggressiveness = settings.SteeringAggressiveness;

                if (float.IsNaN(resolvedAggressiveness) || float.IsInfinity(resolvedAggressiveness))
                    return 1f;

                return math.clamp(resolvedAggressiveness, 0f, 2.5f);
            }

            if (float.IsNaN(steeringAggressiveness) || float.IsInfinity(steeringAggressiveness))
                return 1f;

            return math.clamp(steeringAggressiveness, 0f, 2.5f);
        }
    }

    public GameObject HitVfxPrefab
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return null;

            return settings.HitVfxPrefab;
        }
    }

    public float HitVfxLifetimeSeconds
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return DefaultHitVfxLifetimeSeconds;

            return settings.HitVfxLifetimeSeconds;
        }
    }

    public Vector3 HitVfxSpawnOffset
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return Vector3.zero;

            return settings.HitVfxSpawnOffset;
        }
    }

    public float HitVfxScaleMultiplier
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return DefaultHitVfxScaleMultiplier;

            return settings.HitVfxScaleMultiplier;
        }
    }

    public GameObject SpawnVfxPrefab
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return null;

            return settings.SpawnVfxPrefab;
        }
    }

    public EnemySpawnVfxTiming SpawnVfxTiming
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return EnemySpawnVfxTiming.OnSpawn;

            return settings.SpawnVfxTiming;
        }
    }

    public float SpawnVfxLifetimeSeconds
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return DefaultSpawnVfxLifetimeSeconds;

            return settings.SpawnVfxLifetimeSeconds;
        }
    }

    public Vector3 SpawnVfxSpawnOffset
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return Vector3.zero;

            return settings.SpawnVfxSpawnOffset;
        }
    }

    public float SpawnVfxScaleMultiplier
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return DefaultSpawnVfxScaleMultiplier;

            return settings.SpawnVfxScaleMultiplier;
        }
    }

    public GameObject DeathVfxPrefab
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return null;

            return settings.DeathVfxPrefab;
        }
    }

    public float DeathVfxLifetimeSeconds
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return DefaultDeathVfxLifetimeSeconds;

            return settings.DeathVfxLifetimeSeconds;
        }
    }

    public Vector3 DeathVfxSpawnOffset
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return Vector3.zero;

            return settings.DeathVfxSpawnOffset;
        }
    }

    public float DeathVfxScaleMultiplier
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return DefaultDeathVfxScaleMultiplier;

            return settings.DeathVfxScaleMultiplier;
        }
    }

    public bool UseEnemyBaseColorForDeathDebris
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return true;

            return settings.UseEnemyBaseColorForDeathDebris;
        }
    }

    public Color DeathDebrisFallbackColor
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return Color.white;

            return settings.DeathDebrisFallbackColor;
        }
    }

    public string DeathDebrisParticleChildName
    {
        get
        {
            EnemyVisualPrefabSettings settings = ResolveVisualPrefabSettings();

            if (settings == null)
                return "VFX_Debris";

            return settings.DeathDebrisParticleChildName;
        }
    }

    public bool EnableOutline
    {
        get
        {
            EnemyVisualOutlineSettings settings = ResolveOutlineSettings();

            if (settings == null)
                return true;

            return settings.EnableOutline;
        }
    }

    public float OutlineThickness
    {
        get
        {
            EnemyVisualOutlineSettings settings = ResolveOutlineSettings();

            if (settings == null)
                return DefaultOutlineThickness;

            return settings.OutlineThickness;
        }
    }

    public Color OutlineColor
    {
        get
        {
            EnemyVisualOutlineSettings settings = ResolveOutlineSettings();

            if (settings == null)
                return DefaultOutlineColor;

            return settings.OutlineColor;
        }
    }

    public EnemyShadowCoverageMode ShadowCoverageMode
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return EnemyShadowCoverageMode.ShadowOnly;

            return settings.ShadowCoverageMode;
        }
    }

    public float SpatialUiRingThickness
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultSpatialUiRingThickness;

            return settings.SpatialUiRingThickness;
        }
    }

    public float SpatialUiRingSpacing
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultSpatialUiRingSpacing;

            return settings.SpatialUiRingSpacing;
        }
    }

    public float SpatialUiHeightOffset
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultSpatialUiHeightOffset;

            return settings.SpatialUiHeightOffset;
        }
    }

    public bool HealthRingsEnabled
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultHealthRingsEnabled;

            return settings.HealthRingsEnabled;
        }
    }

    public float RingDistanceFromShadow
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultRingDistanceFromShadow;

            return settings.RingDistanceFromShadow;
        }
    }

    public float RingArcDegrees
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return EnemyVisualFootprintSettings.DefaultRingArcDegrees;

            return settings.RingArcDegrees;
        }
    }

    public Vector2 PositionOffsetXZ
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultPositionOffsetXZ;

            return settings.PositionOffsetXZ;
        }
    }

    public Color ShadowColor
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultShadowColor;

            return settings.ShadowColor;
        }
    }

    public float ShadowAlpha
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultShadowAlpha;

            return settings.ShadowAlpha;
        }
    }

    public float ShadowEdgeSoftness
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultShadowEdgeSoftness;

            return settings.ShadowEdgeSoftness;
        }
    }

    public Color HealthRingFillColor
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultHealthRingFillColor;

            return settings.HealthRingFillColor;
        }
    }

    public Color HealthRingBackgroundColor
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultHealthRingBackgroundColor;

            return settings.HealthRingBackgroundColor;
        }
    }

    public Color ShieldRingFillColor
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultShieldRingFillColor;

            return settings.ShieldRingFillColor;
        }
    }

    public Color ShieldRingBackgroundColor
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultShieldRingBackgroundColor;

            return settings.ShieldRingBackgroundColor;
        }
    }

    public float RingEdgeSoftness
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultRingEdgeSoftness;

            return settings.RingEdgeSoftness;
        }
    }

    public float RingAngularSoftness
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultRingAngularSoftness;

            return settings.RingAngularSoftness;
        }
    }

    public bool LockRingsToWorld
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultLockRingsToWorld;

            return settings.LockRingsToWorld;
        }
    }

    public float LockedRingsWorldAngleDegrees
    {
        get
        {
            EnemyVisualFootprintSettings settings = ResolveFootprintSettings();

            if (settings == null)
                return DefaultLockedRingsWorldAngleDegrees;

            return settings.LockedRingsWorldAngleDegrees;
        }
    }

    public Color DamageFlashColor
    {
        get
        {
            EnemyVisualDamageFeedbackSettings settings = ResolveDamageFeedbackSettings();

            if (settings == null)
                return DefaultDamageFlashColor;

            return settings.FlashColor;
        }
    }

    public float DamageFlashDurationSeconds
    {
        get
        {
            EnemyVisualDamageFeedbackSettings settings = ResolveDamageFeedbackSettings();

            if (settings == null)
                return DefaultDamageFlashDurationSeconds;

            return settings.FlashDurationSeconds;
        }
    }

    public float DamageFlashMaximumBlend
    {
        get
        {
            EnemyVisualDamageFeedbackSettings settings = ResolveDamageFeedbackSettings();

            if (settings == null)
                return DefaultDamageFlashMaximumBlend;

            return settings.FlashMaximumBlend;
        }
    }

    public EnemyOffensiveEngagementFeedbackSettings OffensiveEngagementFeedbackSettings
    {
        get
        {
            return ResolveOffensiveEngagementFeedbackSettings();
        }
    }

    public Animator AnimatorComponent
    {
        get
        {
            return animatorComponent;
        }
    }

    public Transform ElementalVfxAnchor
    {
        get
        {
            return elementalVfxAnchor;
        }
    }

    public EnemyGroundIndicatorView GroundIndicatorView
    {
        get
        {
            return groundIndicatorView;
        }
    }

    public EnemyOffensiveEngagementBillboardView OffensiveEngagementBillboardView
    {
        get
        {
            return offensiveEngagementBillboardView;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Sanitizes fallback values and validates linked presets after inspector edits.
    /// </summary>
    private void OnValidate()
    {
        EnemyAuthoringFallbackValidationUtility.ValidateFallbackValues(ref moveSpeed,
                                                                      ref maxSpeed,
                                                                      ref acceleration,
                                                                      ref deceleration,
                                                                      ref inactivityTime,
                                                                      ref rotationSpeedDegreesPerSecond,
                                                                      ref minimumWallDistance,
                                                                      ref separationRadius,
                                                                      ref separationWeight,
                                                                      ref bodyRadius,
                                                                      ref bodyRadiusXScale,
                                                                      ref bodyRadiusZScale,
                                                                      ref contactRadius,
                                                                      ref contactAmountPerTick,
                                                                      ref contactTickInterval,
                                                                      ref areaRadius,
                                                                      ref areaAmountPerTickPercent,
                                                                      ref areaTickInterval,
                                                                      ref maxHealth,
                                                                      ref maxShield,
                                                                      ref priorityTier,
                                                                      ref steeringAggressiveness);

        if (masterPreset != null)
            masterPreset.ValidateValues();

        if (brainPreset != null)
            brainPreset.ValidateValues();

        if (visualPreset != null)
            visualPreset.ValidateValues();

        if (advancedPatternPreset != null)
            advancedPatternPreset.ValidateValues();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws a preview of the resolved ground indicator footprint in the scene view so authors can
    /// sanity-check shadow size, ring distance, ring thickness and ring arc without entering Play mode.
    /// The preview matches the geometry the shader will draw at runtime.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Resolve world position from the same local hit-center offset used by baking. The shadow source
        // is the contact damage radius, not the body radius used for enemy-vs-enemy steering.
        Transform selfTransform = transform;
        float bakedContactRadius = math.max(0.05f, ContactRadius);
        float ringDistance = math.max(0f, RingDistanceFromShadow);
        float ringThickness = math.max(0f, SpatialUiRingThickness);
        float ringSpacing = math.max(0f, SpatialUiRingSpacing);
        float heightOffset = SpatialUiHeightOffset;
        Vector2 positionOffset = PositionOffsetXZ;
        float2 manualOffsetXZ = new float2(positionOffset.x, positionOffset.y);
        bool rotateHitCenterOffset = EnemyHitCenterBakeUtility.ShouldRotateHitCenterOffset(RotationSpeedDegreesPerSecond);
        float2 localHitCenterOffsetXZ = manualOffsetXZ;

        if (rotateHitCenterOffset)
            localHitCenterOffsetXZ = EnemyHitCenterBakeUtility.ResolveLocalHitCenterOffsetXZ(this, manualOffsetXZ);

        Vector3 origin = EnemyHitboxCenterUtility.ResolveWorldCenter(selfTransform.position,
                                                                     selfTransform.rotation,
                                                                     selfTransform.lossyScale,
                                                                     localHitCenterOffsetXZ,
                                                                     rotateHitCenterOffset,
                                                                     heightOffset);

        // Contact damage hit area (white) — drawn as a circle on the local XZ plane.
        Gizmos.color = new Color(1f, 1f, 1f, 0.85f);
        DrawEllipseGizmo(origin, bakedContactRadius, bakedContactRadius);

        if (!HealthRingsEnabled)
            return;

        // Health ring band (red) — radii are taken from the contact circle outer edge.
        float healthInner = math.max(0.001f, bakedContactRadius + ringDistance);
        float healthOuter = healthInner + ringThickness;
        float ringArcDegrees = EnemyGroundIndicatorFootprintUtility.ResolveRuntimeRingArcDegrees(RingArcDegrees);
        float ringArcCenterAngleRadians = ResolveGroundIndicatorGizmoArcCenterAngleRadians(origin);
        Gizmos.color = new Color(0.95f, 0.25f, 0.2f, 0.85f);
        DrawRingArcGizmo(origin, healthInner, healthOuter, ringArcDegrees, ringArcCenterAngleRadians);

        // Shield ring band (cyan) is only meaningful when the enemy has shield capacity.
        if (MaxShield <= 0f)
            return;
        float shieldInner = healthOuter + ringSpacing;
        float shieldOuter = shieldInner + ringThickness;
        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.85f);
        DrawRingArcGizmo(origin, shieldInner, shieldOuter, ringArcDegrees, ringArcCenterAngleRadians);
    }

    /// <summary>
    /// Draws one ellipse outline on the world XZ plane using the current Gizmos.color.
    /// </summary>
    /// <param name="origin">World-space center of the ellipse.</param>
    /// <param name="radiusX">Half-axis along world X.</param>
    /// <param name="radiusZ">Half-axis along world Z.</param>
    private static void DrawEllipseGizmo(Vector3 origin, float radiusX, float radiusZ)
    {
        float angularStep = (2f * Mathf.PI) / GizmoEllipseSegmentCount;
        Vector3 previousPoint = origin + new Vector3(radiusX, 0f, 0f);

        for (int segmentIndex = 1; segmentIndex <= GizmoEllipseSegmentCount; segmentIndex++)
        {
            float currentAngle = segmentIndex * angularStep;
            Vector3 currentPoint = origin + new Vector3(Mathf.Cos(currentAngle) * radiusX,
                                                        0f,
                                                        Mathf.Sin(currentAngle) * radiusZ);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }

    /// <summary>
    /// Draws a ring band as either a full pair of ellipses or a camera-facing arc with side connectors.
    /// Used by editor previews to mirror the runtime shader arc setting from the visual preset.
    /// </summary>
    /// <param name="origin">World-space center of the ring band.</param>
    /// <param name="innerRadius">Inner radius of the ring band.</param>
    /// <param name="outerRadius">Outer radius of the ring band.</param>
    /// <param name="arcDegrees">Visible ring arc width in degrees.</param>
    /// <param name="centerAngleRadians">World XZ angle used as the center of the arc.</param>
    private static void DrawRingArcGizmo(Vector3 origin,
                                         float innerRadius,
                                         float outerRadius,
                                         float arcDegrees,
                                         float centerAngleRadians)
    {
        float resolvedArcDegrees = Mathf.Clamp(arcDegrees, 0f, EnemyVisualFootprintSettings.DefaultRingArcDegrees);

        if (resolvedArcDegrees >= EnemyVisualFootprintSettings.DefaultRingArcDegrees - 0.001f)
        {
            DrawEllipseGizmo(origin, innerRadius, innerRadius);
            DrawEllipseGizmo(origin, outerRadius, outerRadius);
            return;
        }

        if (resolvedArcDegrees <= 0.001f)
            return;

        // Resolve arc endpoints once so both radii and side connectors use the exact same angles.
        float halfArcRadians = resolvedArcDegrees * Mathf.Deg2Rad * 0.5f;
        float startAngleRadians = centerAngleRadians - halfArcRadians;
        float endAngleRadians = centerAngleRadians + halfArcRadians;
        DrawArcGizmo(origin, innerRadius, startAngleRadians, endAngleRadians, resolvedArcDegrees);
        DrawArcGizmo(origin, outerRadius, startAngleRadians, endAngleRadians, resolvedArcDegrees);
        Gizmos.DrawLine(ResolveArcPoint(origin, innerRadius, startAngleRadians),
                        ResolveArcPoint(origin, outerRadius, startAngleRadians));
        Gizmos.DrawLine(ResolveArcPoint(origin, innerRadius, endAngleRadians),
                        ResolveArcPoint(origin, outerRadius, endAngleRadians));
    }

    /// <summary>
    /// Draws one circular arc on the world XZ plane using a segment count proportional to the authored arc width.
    /// </summary>
    /// <param name="origin">World-space center of the arc.</param>
    /// <param name="radius">Arc radius on the XZ plane.</param>
    /// <param name="startAngleRadians">Start angle in radians.</param>
    /// <param name="endAngleRadians">End angle in radians.</param>
    /// <param name="arcDegrees">Arc width in degrees used to choose preview tessellation density.</param>
    private static void DrawArcGizmo(Vector3 origin,
                                     float radius,
                                     float startAngleRadians,
                                     float endAngleRadians,
                                     float arcDegrees)
    {
        int segmentCount = Mathf.Max(2, Mathf.CeilToInt(GizmoEllipseSegmentCount * arcDegrees / EnemyVisualFootprintSettings.DefaultRingArcDegrees));
        float angularStep = (endAngleRadians - startAngleRadians) / segmentCount;
        Vector3 previousPoint = ResolveArcPoint(origin, radius, startAngleRadians);

        // Step through the arc without allocating temporary point arrays.
        for (int segmentIndex = 1; segmentIndex <= segmentCount; segmentIndex++)
        {
            float currentAngleRadians = startAngleRadians + angularStep * segmentIndex;
            Vector3 currentPoint = ResolveArcPoint(origin, radius, currentAngleRadians);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }

    /// <summary>
    /// Resolves the Scene View camera-facing angle used by editor footprint gizmos.
    /// </summary>
    /// <param name="origin">World-space enemy footprint center.</param>
    /// <returns>Camera-facing XZ angle in radians, or zero when no camera vector is available.</returns>
    private static float ResolveGroundIndicatorGizmoArcCenterAngleRadians(Vector3 origin)
    {
        Camera currentCamera = Camera.current;

        if (currentCamera == null)
            return 0f;

        Vector3 toCamera = currentCamera.transform.position - origin;
        toCamera.y = 0f;

        if (toCamera.sqrMagnitude <= 0.0001f)
            return 0f;

        return Mathf.Atan2(toCamera.z, toCamera.x);
    }

    /// <summary>
    /// Resolves one point on a circular XZ arc.
    /// </summary>
    /// <param name="origin">World-space center of the arc.</param>
    /// <param name="radius">Arc radius.</param>
    /// <param name="angleRadians">Angle measured on the world XZ plane.</param>
    /// <returns>World-space point on the arc.</returns>
    private static Vector3 ResolveArcPoint(Vector3 origin, float radius, float angleRadians)
    {
        return origin + new Vector3(Mathf.Cos(angleRadians) * radius,
                                    0f,
                                    Mathf.Sin(angleRadians) * radius);
    }
#endif
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the active movement settings source.
    /// </summary>
    /// <returns>Resolved movement settings or null when no preset source is available.</returns>
    private EnemyBrainMovementSettings ResolveMovementSettings()
    {
        return EnemyAuthoringPresetResolverUtility.ResolveMovementSettings(masterPreset, brainPreset);
    }

    /// <summary>
    /// Resolves the active steering settings source.
    /// </summary>
    /// <returns>Resolved steering settings or null when no preset source is available.</returns>
    private EnemyBrainSteeringSettings ResolveSteeringSettings()
    {
        return EnemyAuthoringPresetResolverUtility.ResolveSteeringSettings(masterPreset, brainPreset);
    }

    /// <summary>
    /// Resolves the active damage settings source.
    /// </summary>
    /// <returns>Resolved damage settings or null when no preset source is available.</returns>
    private EnemyBrainDamageSettings ResolveDamageSettings()
    {
        return EnemyAuthoringPresetResolverUtility.ResolveDamageSettings(masterPreset, brainPreset);
    }

    /// <summary>
    /// Resolves the active health settings source.
    /// </summary>
    /// <returns>Resolved health settings or null when no preset source is available.</returns>
    private EnemyBrainHealthStatisticsSettings ResolveHealthSettings()
    {
        return EnemyAuthoringPresetResolverUtility.ResolveHealthStatisticsSettings(masterPreset, brainPreset);
    }

    /// <summary>
    /// Resolves the active visual visibility settings source.
    /// </summary>
    /// <returns>Resolved visibility settings or null when no preset source is available.</returns>
    private EnemyVisualVisibilitySettings ResolveVisibilitySettings()
    {
        return EnemyAuthoringPresetResolverUtility.ResolveVisibilitySettings(masterPreset, visualPreset);
    }

    /// <summary>
    /// Resolves the active visual prefab settings source.
    /// </summary>
    /// <returns>Resolved prefab settings or null when no preset source is available.</returns>
    private EnemyVisualPrefabSettings ResolveVisualPrefabSettings()
    {
        return EnemyAuthoringPresetResolverUtility.ResolveVisualPrefabSettings(masterPreset, visualPreset);
    }

    /// <summary>
    /// Resolves the active damage flash settings source.
    /// </summary>
    /// <returns>Resolved damage flash settings or null when no preset source is available.</returns>
    private EnemyVisualDamageFeedbackSettings ResolveDamageFeedbackSettings()
    {
        EnemyVisualPreset resolvedVisualPreset = EnemyAuthoringPresetResolverUtility.ResolveVisualPreset(masterPreset, visualPreset);

        if (resolvedVisualPreset == null)
            return null;

        return resolvedVisualPreset.DamageFeedback;
    }

    /// <summary>
    /// Resolves the active outline settings source.
    /// </summary>
    /// <returns>Resolved outline settings or null when no preset source is available.</returns>
    private EnemyVisualOutlineSettings ResolveOutlineSettings()
    {
        return EnemyAuthoringPresetResolverUtility.ResolveOutlineSettings(masterPreset, visualPreset);
    }

    /// <summary>
    /// Resolves the active ground-footprint visual settings source.
    /// </summary>
    /// <returns>Resolved footprint settings or null when no preset source is available.</returns>
    private EnemyVisualFootprintSettings ResolveFootprintSettings()
    {
        return EnemyAuthoringPresetResolverUtility.ResolveFootprintSettings(masterPreset, visualPreset);
    }

    /// <summary>
    /// Resolves the active offensive engagement feedback settings source.
    /// </summary>
    /// <returns>Resolved offensive engagement feedback settings or null when no preset source is available.</returns>
    private EnemyOffensiveEngagementFeedbackSettings ResolveOffensiveEngagementFeedbackSettings()
    {
        return EnemyAuthoringPresetResolverUtility.ResolveOffensiveEngagementFeedbackSettings(masterPreset, visualPreset);
    }

    /// <summary>
    /// Resolves one fallback body-hit axis radius without mutating authoring fallback fields during validation.
    /// </summary>
    /// <param name="baseRadius">Fallback base body radius.</param>
    /// <param name="scale">Fallback axis scale applied to the base body radius.</param>
    /// <returns>Scaled fallback axis radius, or the base radius when the scale is not finite.</returns>
    private static float ResolveScaledBodyRadius(float baseRadius, float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale))
            return math.max(0f, baseRadius);

        return math.max(0f, baseRadius) * math.max(0f, scale);
    }
    #endregion

    #endregion
}
