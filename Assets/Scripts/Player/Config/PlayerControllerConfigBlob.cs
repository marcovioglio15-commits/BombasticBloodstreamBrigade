using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#region Root Blob
/// <summary>
/// Holds all configuration data for the player controller, 
/// including movement, looking direction, camera behavior, and shooting mechanics.
/// </summary>
public struct PlayerControllerConfigBlob
{
    public MovementConfig Movement;
    public LookConfig Look;
    public CameraConfig Camera;
    public ShootingConfig Shooting;
    public HealthStatisticsConfig HealthStatistics;
}
#endregion

#region Health Statistics
/// <summary>
/// Holds configuration data related to player health and shield reserves.
/// </summary>
public struct HealthStatisticsConfig
{
    public float MaxHealth;
    public float MaxShield;
    public float GraceTimeSeconds;
}
#endregion

#region Movement
/// <summary>
/// Holds configuration data related to player movement, including direction modes, 
/// reference frames, and movement values such as speed, acceleration, and input dead zones.
/// 
/// </summary>
public struct MovementConfig
{
    public MovementDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public ReferenceFrame MovementReference;
    public MovementValuesBlob Values;
}

/// <summary>
/// This structure holds movement-related values such as base speed, max speed, acceleration,
/// deceleration, input dead zone, and digital release grace time.
/// </summary>
public struct MovementValuesBlob
{
    public float BaseSpeed;
    public float MaxSpeed;
    public float Acceleration;
    public float Deceleration;
    public float OppositeDirectionBrakeMultiplier;
    public float WallBounceCoefficient;
    public float WallCollisionSkinWidth;
    public float InputDeadZone;
    public float DigitalReleaseGraceSeconds;
}
#endregion

#region Shooting
/// <summary>
/// This structure holds configuration data related to player shooting mechanics, 
/// including trigger modes, projectile behavior, shooting offsets, 
/// and values such as shoot speed, rate of fire, explosion radius, range, lifetime, and damage.
/// </summary>
public struct ShootingConfig
{
    public ShootingTriggerMode TriggerMode;
    public byte ProjectilesInheritPlayerSpeed;
    public float3 ShootOffset;
    public ShootingValuesBlob Values;
}

/// <summary>
/// Holds shooting-related values such as shoot speed, 
/// rate of fire, explosion radius, range, lifetime, damage, default penetration and base elemental payload tuning for projectiles.
/// </summary>
public struct ShootingValuesBlob
{
    public float ShootSpeed;
    public float RateOfFire;
    public float ProjectileSizeMultiplier;
    public float ExplosionRadius;
    public float Range;
    public float Lifetime;
    public float Damage;
    public ElementBulletSettingsByElementBlob ElementBehaviours;
    public ProjectilePenetrationMode PenetrationMode;
    public int MaxPenetrations;
    public ProjectileKnockbackSettingsBlob Knockback;
}

/// <summary>
/// Holds the default knockback payload emitted by base player projectiles.
/// </summary>
public struct ProjectileKnockbackSettingsBlob
{
    public byte Enabled;
    public float Strength;
    public float DurationSeconds;
    public ProjectileKnockbackDirectionMode DirectionMode;
    public ProjectileKnockbackStackingMode StackingMode;
}

/// <summary>
/// Holds the detailed gameplay behaviour emitted by one elemental payload entry.
/// </summary>
public struct ElementBulletSettingsBlob
{
    public ElementalEffectKind EffectKind;
    public ElementalProcMode ProcMode;
    public ElementalProcReapplyMode ReapplyMode;
    public float StacksPerHit;
    public float ProcThresholdStacks;
    public float MaximumStacks;
    public float StackDecayPerSecond;
    public byte ConsumeStacksOnProc;
    public float DotDamagePerTick;
    public float DotTickInterval;
    public float DotDurationSeconds;
    public float ImpedimentSlowPercentPerStack;
    public float ImpedimentProcSlowPercent;
    public float ImpedimentMaxSlowPercent;
    public float ImpedimentDurationSeconds;
}

/// <summary>
/// Holds the per-element gameplay behaviour blocks referenced by stacked default projectile elements.
/// </summary>
public struct ElementBulletSettingsByElementBlob
{
    public ElementBulletSettingsBlob Fire;
    public ElementBulletSettingsBlob Ice;
    public ElementBulletSettingsBlob Poison;
    public ElementBulletSettingsBlob Custom;
}
#endregion

#region Look
/// <summary>
/// Holds configuration data related to player looking direction, 
/// including direction modes, rotation modes, 
/// speed multipliers based on look direction, and values such as rotation speed, 
/// damping, and dead zones.
/// </summary>
public struct LookConfig
{
    public LookDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public RotationMode RotationMode;
    public float RotationSpeed;
    public LookMultiplierSampling MultiplierSampling;
    public BlobArray<float> DiscreteMaxSpeedMultipliers;
    public BlobArray<float> DiscreteAccelerationMultipliers;
    public ConeConfig FrontCone;
    public ConeConfig BackCone;
    public ConeConfig LeftCone;
    public ConeConfig RightCone;
    public LookValuesBlob Values;
}

/// <summary>
/// Holds look-related values such as rotation speed, damping, max speed, 
/// input dead zones, and digital release grace time for look direction stabilization. 
/// </summary>
public struct LookValuesBlob
{
    public float RotationDamping;
    public float RotationMaxSpeed;
    public float RotationDeadZone;
    public float DigitalReleaseGraceSeconds;
}

/// <summary>
/// Holds configuration for a directional cone used in look direction 
/// speed multipliers, including whether the cone is enabled, its angle in degrees, 
/// and the max speed and acceleration multipliers applied when the look direction falls within the cone. 
/// </summary>
public struct ConeConfig
{
    public bool Enabled;
    public float AngleDegrees;
    public float MaxSpeedMultiplier;
    public float AccelerationMultiplier;
}
#endregion

#region Camera
/// <summary>
/// Holds configuration data related to camera behavior, including the follow behavior, the offset from
/// the player, and the follow spring tuning values (smooth time, max follow distance, dead zone radius).
/// </summary>
public struct CameraConfig
{
    public CameraBehavior Behavior;
    public float3 FollowOffset;
    public CameraValuesBlob Values;
    public CameraShakeBlob Shake;
}

/// <summary>
/// Holds camera-related values driving the critically damped follow spring: smooth time,
/// max follow distance (leash) and dead zone radius.
/// </summary>
public struct CameraValuesBlob
{
    public float SmoothTime;
    public float MaxFollowDistance;
    public float DeadZoneRadius;
}

/// <summary>
/// Holds the trauma-based damage camera-shake tuning resolved at bake time. Numeric ranges are clamped
/// defensively at point of use by the runtime shake utility rather than at bake.
/// </summary>
public struct CameraShakeBlob
{
    public byte Enabled;
    public float DurationSeconds;
    public float PositionalAmplitude;
    public float RotationalAmplitude;
    public float Frequency;
    public CameraShakeFalloff Falloff;
    public byte ScaleWithDamage;
    public float DamageForFullStrength;
}
#endregion
