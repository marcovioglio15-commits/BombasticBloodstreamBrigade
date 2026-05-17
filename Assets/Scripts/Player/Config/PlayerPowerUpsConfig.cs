using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

#region Components
/// <summary>
/// Holds baked runtime configuration for the player's power-up slots.
/// </summary>
public struct PlayerPowerUpsConfig : IComponentData
{
    public PlayerPowerUpSlotConfig PrimarySlot;
    public PlayerPowerUpSlotConfig SecondarySlot;
}

/// <summary>
/// Input edge used to trigger non-charge active power-up execution.
/// </summary>
public enum PowerUpActivationInputMode
{
    OnPress = 0,
    OnRelease = 1
}

/// <summary>
/// Holds baked runtime configuration for a single active-tool slot.
/// </summary>
public struct PlayerPowerUpSlotConfig
{
    public byte IsDefined;
    public FixedString64Bytes PowerUpId;
    public ActiveToolKind ToolKind;
    public PowerUpResourceType ActivationResource;
    public PowerUpResourceType MaintenanceResource;
    public PowerUpChargeType ChargeType;
    public float MaximumEnergy;
    public float ActivationCost;
    public float MaintenanceCostPerSecond;
    public float MaintenanceTicksPerSecond;
    public float ChargePerTrigger;
    public float CooldownSeconds;
    public PowerUpActivationInputMode ActivationInputMode;
    public byte Toggleable;
    public byte AllowRechargeDuringToggleStartupLock;
    public float MinimumActivationEnergyPercent;
    public byte Unreplaceable;
    public byte SuppressBaseShootingWhileActive;
    public byte InterruptOtherSlotOnEnter;
    public byte InterruptOtherSlotChargingOnly;
    public Entity BombPrefabEntity;
    public BombPowerUpConfig Bomb;
    public DashPowerUpConfig Dash;
    public BulletTimePowerUpConfig BulletTime;
    public ShotgunPowerUpConfig Shotgun;
    public ChargeShotPowerUpConfig ChargeShot;
    public PortableHealthPackPowerUpConfig PortableHealthPack;
    public PlayerPassiveToolConfig TriggeredProjectilePassiveTool;
    public PlayerPassiveToolConfig TogglePassiveTool;
}

/// <summary>
/// Holds baked runtime configuration for the Bomb active tool.
/// </summary>
public struct BombPowerUpConfig
{
    public float3 SpawnOffset;
    public SpawnOffsetOrientationMode SpawnOffsetOrientation;
    public float DeploySpeed;
    public BombVelocityDirectionMode VelocityDirection;
    public float CollisionRadius;
    public byte BounceOnWalls;
    public float BounceDamping;
    public float LinearDampingPerSecond;
    public float FuseSeconds;
    public byte EnableDamagePayload;
    public float Radius;
    public float Damage;
    public byte AffectAllEnemiesInRadius;
    public Entity ExplosionVfxPrefabEntity;
    public byte ScaleVfxToRadius;
    public float VfxScaleMultiplier;
}

/// <summary>
/// Holds baked runtime configuration for the Dash active tool.
/// </summary>
public struct DashPowerUpConfig
{
    public float Distance;
    public DashDirectionMode DirectionMode;
    public float Duration;
    public float SpeedTransitionInSeconds;
    public float SpeedTransitionOutSeconds;
    public float WallBounceIntensity;
    public byte GrantsInvulnerability;
    public float InvulnerabilityExtraTime;
}

/// <summary>
/// Holds baked runtime configuration for the Bullet Time active tool.
/// </summary>
public struct BulletTimePowerUpConfig
{
    public float Duration;
    public float EnemySlowPercent;
    public float TransitionTimeSeconds;
}

/// <summary>
/// Holds baked runtime configuration for the Shotgun active tool.
/// </summary>
public struct ShotgunPowerUpConfig
{
    public int ProjectileCount;
    public float ConeAngleDegrees;
    public float LaserDurationSeconds;
    public float SizeMultiplier;
    public float DamageMultiplier;
    public float SpeedMultiplier;
    public float RangeMultiplier;
    public float LifetimeMultiplier;
    public ProjectilePenetrationMode PenetrationMode;
    public int MaxPenetrations;
    public byte HasElementalPayload;
    public ElementalEffectConfig ElementalEffect;
    public float ElementalStacksPerHit;
}

/// <summary>
/// Holds baked runtime configuration for the Charge Shot active tool.
/// </summary>
public struct ChargeShotPowerUpConfig
{
    public const int PlayerSlowCurveSampleCount = 16;

    public float RequiredCharge;
    public float MaximumCharge;
    public float ChargeRatePerSecond;
    public float LaserDurationSeconds;
    public byte UseChargedLaserBeam;
    public float ChargedLaserDurationSeconds;
    public LaserBeamPassiveConfig ChargedLaserBeam;
    public byte DecayAfterRelease;
    public float DecayAfterReleasePercentPerSecond;
    public byte PassiveChargeGainWhileReleased;
    public float PassiveChargeGainPercentPerSecond;
    public byte SuppressBaseShootingWhileCharging;
    public byte SlowPlayerWhileCharging;
    public float MaximumPlayerSlowPercent;
    public FixedList128Bytes<float> PlayerSlowCurveSamples;
    public float SizeMultiplier;
    public float DamageMultiplier;
    public float SpeedMultiplier;
    public float RangeMultiplier;
    public float LifetimeMultiplier;
    public ProjectilePenetrationMode PenetrationMode;
    public int MaxPenetrations;
    public byte HasElementalPayload;
    public ElementalEffectConfig ElementalEffect;
    public float ElementalStacksPerHit;
}

/// <summary>
/// Holds baked runtime configuration for the Portable Health Pack active tool.
/// </summary>
public struct PortableHealthPackPowerUpConfig
{
    public PowerUpHealApplicationMode ApplyMode;
    public float HealAmount;
    public float DurationSeconds;
    public float TickIntervalSeconds;
    public PowerUpHealStackPolicy StackPolicy;
}

/// <summary>
/// Runtime trigger mode used by passive heal payloads.
/// </summary>
public enum PassiveHealTriggerMode
{
    Periodic = 0,
    OnPlayerDamaged = 1,
    OnEnemyKilled = 2
}

/// <summary>
/// Runtime trigger mode used by passive bullet-time payloads.
/// </summary>
public enum PassiveBulletTimeTriggerMode
{
    Periodic = 0,
    OnPlayerDamaged = 1,
    OnEnemyKilled = 2
}

/// <summary>
/// Holds baked runtime configuration for passive heal-over-time behavior.
/// </summary>
public struct PassiveHealConfig
{
    public PassiveHealTriggerMode TriggerMode;
    public float CooldownSeconds;
    public float HealAmount;
    public float DurationSeconds;
    public float TickIntervalSeconds;
    public PowerUpHealStackPolicy StackPolicy;
}

/// <summary>
/// Holds baked runtime configuration for passive bullet-time behavior.
/// </summary>
public struct PassiveBulletTimeConfig
{
    public PassiveBulletTimeTriggerMode TriggerMode;
    public float CooldownSeconds;
    public float DurationSeconds;
    public float EnemySlowPercent;
    public float TransitionTimeSeconds;
}

/// <summary>
/// Holds baked runtime configuration for the Laser Beam passive shooting override.
/// </summary>
public struct LaserBeamPassiveConfig
{
    public float DamageMultiplier;
    public float ContinuousDamagePerSecondMultiplier;
    public float VirtualProjectileSpeedMultiplier;
    public float DamageTickIntervalSeconds;
    public float MaximumContinuousActiveSeconds;
    public float CooldownSeconds;
    public int MaximumBounceSegments;
    public byte ApplyPlayerHandlingNerfWhileFiring;
    public float FiringMoveSpeedMultiplier;
    public float FiringRotationSpeedMultiplier;
    public int VisualPresetId;
    public LaserBeamBodyProfile BodyProfile;
    public LaserBeamCapShape SourceShape;
    public LaserBeamCapShape TerminalCapShape;
    public float BodyWidthMultiplier;
    public float CollisionWidthMultiplier;
    public float SourceScaleMultiplier;
    public float TerminalCapScaleMultiplier;
    public float ContactFlareScaleMultiplier;
    public float BodyOpacity;
    public float CoreWidthMultiplier;
    public float CoreBrightness;
    public float RimBrightness;
    public float FlowScrollSpeed;
    public float FlowPulseFrequency;
    public float StormTwistSpeed;
    public float StormTickPostTravelHoldSeconds;
    public float StormIdleIntensity;
    public float StormBurstIntensity;
    public float SourceOffset;
    public float SourceDischargeIntensity;
    public float StormShellWidthMultiplier;
    public float StormShellSeparation;
    public float StormRingFrequency;
    public float StormRingThickness;
    public float StormTickTravelSpeed;
    public float StormTickDamageLengthTolerance;
    public float TerminalCapIntensity;
    public float ContactFlareIntensity;
    public float WobbleAmplitude;
    public float BubbleDriftSpeed;
}

/// <summary>
/// Holds baked runtime configuration for a single passive-tool slot.
/// </summary>
public struct PlayerPassiveToolConfig
{
    public byte IsDefined;
    public PassiveToolKind ToolKind;
    public byte HasProjectileSize;
    public byte HasShotgun;
    public byte HasElementalProjectiles;
    public byte HasPerfectCircle;
    public byte HasBouncingProjectiles;
    public byte HasSplittingProjectiles;
    public byte HasExplosion;
    public byte HasElementalTrail;
    public byte HasHeal;
    public byte HasBulletTime;
    public byte HasLaserBeam;
    public ProjectileSizePassiveConfig ProjectileSize;
    public ShotgunPowerUpConfig Shotgun;
    public ElementalProjectilesPassiveConfig ElementalProjectiles;
    public PerfectCirclePassiveConfig PerfectCircle;
    public BouncingProjectilesPassiveConfig BouncingProjectiles;
    public SplittingProjectilesPassiveConfig SplittingProjectiles;
    public ExplosionPassiveConfig Explosion;
    public ElementalTrailPassiveConfig ElementalTrail;
    public PassiveHealConfig Heal;
    public PassiveBulletTimeConfig BulletTime;
    public LaserBeamPassiveConfig LaserBeam;
}

/// <summary>
/// Buffer entry representing one equipped passive tool in the player's startup loadout.
/// </summary>
[InternalBufferCapacity(0)]
public struct EquippedPassiveToolElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public PlayerPassiveToolConfig Tool;
}

/// <summary>
/// Holds baked runtime configuration for the Projectile Size passive tool.
/// </summary>
public struct ProjectileSizePassiveConfig
{
    public float SizeMultiplier;
    public float DamageMultiplier;
    public float SpeedMultiplier;
    public float LifetimeSecondsMultiplier;
    public float LifetimeRangeMultiplier;
}

/// <summary>
/// Shared elemental payload configuration used by elemental projectiles and elemental trail.
/// </summary>
public struct ElementalEffectConfig
{
    public ElementType ElementType;
    public ElementalEffectKind EffectKind;
    public ElementalProcMode ProcMode;
    public ElementalProcReapplyMode ReapplyMode;
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
/// Holds baked runtime VFX assignment for one elemental type.
/// </summary>
public struct ElementalVfxDefinitionConfig
{
    public byte SpawnStackVfx;
    public Entity StackVfxPrefabEntity;
    public float StackVfxScaleMultiplier;
    public byte SpawnProcVfx;
    public Entity ProcVfxPrefabEntity;
    public float ProcVfxScaleMultiplier;
}

/// <summary>
/// Holds baked runtime VFX assignments for all elemental types.
/// </summary>
public struct PlayerElementalVfxConfig : IComponentData
{
    public ElementalVfxDefinitionConfig Fire;
    public ElementalVfxDefinitionConfig Ice;
    public ElementalVfxDefinitionConfig Poison;
    public ElementalVfxDefinitionConfig Custom;
}

/// <summary>
/// Holds baked runtime configuration for the Elemental Projectiles passive tool.
/// </summary>
public struct ElementalProjectilesPassiveConfig
{
    public ElementalEffectConfig Effect;
    public float StacksPerHit;
}

/// <summary>
/// Holds baked runtime configuration for the Perfect Circle passive tool.
/// </summary>
public struct PerfectCirclePassiveConfig
{
    public ProjectileOrbitPathMode PathMode;
    public float RadialEntrySpeed;
    public float OrbitalSpeed;
    public float OrbitRadiusMin;
    public float OrbitRadiusMax;
    public float OrbitPulseFrequency;
    public float OrbitEntryRatio;
    public float OrbitBlendDuration;
    public float HeightOffset;
    public float GoldenAngleDegrees;
    public float SpiralStartRadius;
    public float SpiralMaximumRadius;
    public float SpiralAngularSpeedDegreesPerSecond;
    public float SpiralGrowthMultiplier;
    public float SpiralTurnsBeforeDespawn;
    public byte SpiralClockwise;
}

/// <summary>
/// Holds baked runtime configuration for the Bouncing Projectiles passive tool.
/// </summary>
public struct BouncingProjectilesPassiveConfig
{
    public int MaxBounces;
    public float SpeedPercentChangePerBounce;
    public float MinimumSpeedMultiplierAfterBounce;
    public float MaximumSpeedMultiplierAfterBounce;
}

/// <summary>
/// Holds baked runtime configuration for the Splitting Projectiles passive tool.
/// </summary>
public struct SplittingProjectilesPassiveConfig
{
    public ProjectileSplitTriggerMode TriggerMode;
    public ProjectileSplitDirectionMode DirectionMode;
    public int SplitProjectileCount;
    public float SplitOffsetDegrees;
    public FixedList128Bytes<float> CustomAnglesDegrees;
    public float SplitDamageMultiplier;
    public float SplitSizeMultiplier;
    public float SplitSpeedMultiplier;
    public float SplitLifetimeMultiplier;
}

/// <summary>
/// Holds baked runtime configuration for the Explosion passive tool.
/// </summary>
public struct ExplosionPassiveConfig
{
    public PassiveExplosionTriggerMode TriggerMode;
    public float CooldownSeconds;
    public float Radius;
    public float Damage;
    public byte AffectAllEnemiesInRadius;
    public float3 TriggerOffset;
    public Entity ExplosionVfxPrefabEntity;
    public byte ScaleVfxToRadius;
    public float VfxScaleMultiplier;
}

/// <summary>
/// Holds baked runtime configuration for the Elemental Trail passive tool.
/// </summary>
public struct ElementalTrailPassiveConfig
{
    public ElementalEffectConfig Effect;
    public float TrailSegmentLifetimeSeconds;
    public float TrailSpawnDistance;
    public float TrailSpawnIntervalSeconds;
    public float TrailRadius;
    public int MaxActiveSegmentsPerPlayer;
    public float StacksPerTick;
    public float ApplyIntervalSeconds;
    public Entity TrailAttachedVfxPrefabEntity;
    public float TrailAttachedVfxScaleMultiplier;
    public float3 TrailAttachedVfxOffset;
}
#endregion
