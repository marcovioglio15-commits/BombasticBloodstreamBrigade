using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#region Controller Runtime Components
/// <summary>
/// Stores the current runtime movement config after scalable-stat formulas are resolved.
/// none.
/// </summary>
public struct PlayerRuntimeMovementConfig : IComponentData
{
    public MovementDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public ReferenceFrame MovementReference;
    public MovementValuesBlob Values;
}

/// <summary>
/// Stores the immutable baseline movement config used to rebuild runtime-scaled values.
/// none.
/// </summary>
public struct PlayerBaseMovementConfig : IComponentData
{
    public MovementDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public ReferenceFrame MovementReference;
    public MovementValuesBlob Values;
}

/// <summary>
/// Stores the current runtime look config after scalable-stat formulas are resolved.
/// none.
/// </summary>
public struct PlayerRuntimeLookConfig : IComponentData
{
    public LookDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public RotationMode RotationMode;
    public float RotationSpeed;
    public LookMultiplierSampling MultiplierSampling;
    public ConeConfig FrontCone;
    public ConeConfig BackCone;
    public ConeConfig LeftCone;
    public ConeConfig RightCone;
    public LookValuesBlob Values;
}

/// <summary>
/// Stores the immutable baseline look config used to rebuild runtime-scaled values.
/// none.
/// </summary>
public struct PlayerBaseLookConfig : IComponentData
{
    public LookDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public RotationMode RotationMode;
    public float RotationSpeed;
    public LookMultiplierSampling MultiplierSampling;
    public ConeConfig FrontCone;
    public ConeConfig BackCone;
    public ConeConfig LeftCone;
    public ConeConfig RightCone;
    public LookValuesBlob Values;
}

/// <summary>
/// Stores the current runtime camera config after scalable-stat formulas are resolved.
/// none.
/// </summary>
public struct PlayerRuntimeCameraConfig : IComponentData
{
    public CameraBehavior Behavior;
    public float3 FollowOffset;
    public CameraValuesBlob Values;
}

/// <summary>
/// Stores the immutable baseline camera config used to rebuild runtime-scaled values.
/// none.
/// </summary>
public struct PlayerBaseCameraConfig : IComponentData
{
    public CameraBehavior Behavior;
    public float3 FollowOffset;
    public CameraValuesBlob Values;
}

/// <summary>
/// Stores the current runtime shooting config after scalable-stat formulas are resolved.
/// none.
/// </summary>
public struct PlayerRuntimeShootingConfig : IComponentData
{
    public ShootingTriggerMode TriggerMode;
    public byte ProjectilesInheritPlayerSpeed;
    public float3 ShootOffset;
    public ShootingValuesBlob Values;
}

/// <summary>
/// Stores the immutable baseline shooting config used to rebuild runtime-scaled values.
/// none.
/// </summary>
public struct PlayerBaseShootingConfig : IComponentData
{
    public ShootingTriggerMode TriggerMode;
    public byte ProjectilesInheritPlayerSpeed;
    public float3 ShootOffset;
    public ShootingValuesBlob Values;
}

/// <summary>
/// Stores one immutable baseline default projectile element slot used to rebuild runtime shooting payload selection.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseShootingAppliedElementSlot : IBufferElementData
{
    public PlayerProjectileAppliedElement Value;
}

/// <summary>
/// Stores one mutable runtime default projectile element slot after scalable-stat formulas are resolved.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeShootingAppliedElementSlot : IBufferElementData
{
    public PlayerProjectileAppliedElement Value;
}

/// <summary>
/// Stores current runtime health/shield limits after scalable-stat formulas are resolved.
/// none.
/// </summary>
public struct PlayerRuntimeHealthStatisticsConfig : IComponentData
{
    public float MaxHealth;
    public PlayerMaxStatAdjustmentMode MaxHealthAdjustmentMode;
    public float MaxShield;
    public PlayerMaxStatAdjustmentMode MaxShieldAdjustmentMode;
    public float GraceTimeSeconds;
}

/// <summary>
/// Stores immutable baseline health/shield limits used to rebuild runtime-scaled values.
/// none.
/// </summary>
public struct PlayerBaseHealthStatisticsConfig : IComponentData
{
    public float MaxHealth;
    public PlayerMaxStatAdjustmentMode MaxHealthAdjustmentMode;
    public float MaxShield;
    public PlayerMaxStatAdjustmentMode MaxShieldAdjustmentMode;
    public float GraceTimeSeconds;
}
#endregion

#region Progression Runtime Components
/// <summary>
/// Stores one immutable baseline phase used to rebuild runtime progression requirements.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseGamePhaseElement : IBufferElementData
{
    public int StartsAtLevel;
    public float StartingRequiredLevelUpExp;
    public float RequiredExperienceGrouth;
}

/// <summary>
/// Stores one current runtime phase after scalable-stat formulas are resolved.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeGamePhaseElement : IBufferElementData
{
    public int StartsAtLevel;
    public float StartingRequiredLevelUpExp;
    public float RequiredExperienceGrouth;
}
#endregion

#region Power-Up Runtime Components
/// <summary>
/// Stores immutable base active/passive configs for one modular power-up entry.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpBaseConfigElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public PlayerPowerUpUnlockKind UnlockKind;
    public PlayerPowerUpSlotConfig ActiveSlotConfig;
    public PlayerPassiveToolConfig PassiveToolConfig;
}
#endregion

#region Runtime Scaling State
/// <summary>
/// Tracks the last scalable-stat hash synchronized into runtime-scaled configs.
/// none.
/// </summary>
public struct PlayerRuntimeScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}
#endregion

#region Scaling Metadata
/// <summary>
/// Identifies one controller runtime field that can be rewritten from a scaling formula.
/// none.
/// </summary>
public enum PlayerRuntimeControllerFieldId : byte
{
    MovementDiscreteDirectionCount = 0,
    MovementDirectionOffsetDegrees = 1,
    MovementBaseSpeed = 2,
    MovementMaxSpeed = 3,
    MovementAcceleration = 4,
    MovementDeceleration = 5,
    MovementOppositeDirectionBrakeMultiplier = 6,
    MovementWallBounceCoefficient = 7,
    MovementWallCollisionSkinWidth = 8,
    MovementInputDeadZone = 9,
    MovementDigitalReleaseGraceSeconds = 10,
    LookDiscreteDirectionCount = 11,
    LookDirectionOffsetDegrees = 12,
    LookRotationSpeed = 13,
    LookFrontConeAngleDegrees = 14,
    LookFrontConeMaxSpeedMultiplier = 15,
    LookFrontConeAccelerationMultiplier = 16,
    LookBackConeAngleDegrees = 17,
    LookBackConeMaxSpeedMultiplier = 18,
    LookBackConeAccelerationMultiplier = 19,
    LookLeftConeAngleDegrees = 20,
    LookLeftConeMaxSpeedMultiplier = 21,
    LookLeftConeAccelerationMultiplier = 22,
    LookRightConeAngleDegrees = 23,
    LookRightConeMaxSpeedMultiplier = 24,
    LookRightConeAccelerationMultiplier = 25,
    LookRotationDamping = 26,
    LookRotationMaxSpeed = 27,
    LookRotationDeadZone = 28,
    LookDigitalReleaseGraceSeconds = 29,
    CameraFollowOffsetX = 30,
    CameraFollowOffsetY = 31,
    CameraFollowOffsetZ = 32,
    CameraFollowSpeed = 33,
    CameraLag = 34,
    CameraDamping = 35,
    CameraMaxFollowDistance = 36,
    CameraDeadZoneRadius = 37,
    ShootingShootOffsetX = 38,
    ShootingShootOffsetY = 39,
    ShootingShootOffsetZ = 40,
    ShootingShootSpeed = 41,
    ShootingRateOfFire = 42,
    ShootingProjectileSizeMultiplier = 43,
    ShootingExplosionRadius = 44,
    ShootingRange = 45,
    ShootingLifetime = 46,
    ShootingDamage = 47,
    ShootingMaxPenetrations = 48,
    HealthMaxHealth = 49,
    HealthMaxShield = 50,
    HealthGraceTimeSeconds = 51,
    MovementDirectionsMode = 52,
    MovementReference = 53,
    LookDirectionsMode = 54,
    LookRotationMode = 55,
    LookMultiplierSampling = 56,
    CameraBehavior = 57,
    ShootingTriggerMode = 58,
    ShootingProjectilesInheritPlayerSpeed = 59,
    ShootingPenetrationMode = 60,
    ShootingAppliedElement = 61,
    HealthMaxHealthAdjustmentMode = 62,
    HealthMaxShieldAdjustmentMode = 63,
    LookFrontConeEnabled = 64,
    LookBackConeEnabled = 65,
    LookLeftConeEnabled = 66,
    LookRightConeEnabled = 67,
    ShootingElementBulletEffectKind = 68,
    ShootingElementBulletProcMode = 69,
    ShootingElementBulletReapplyMode = 70,
    ShootingElementBulletStacksPerHit = 71,
    ShootingElementBulletProcThresholdStacks = 72,
    ShootingElementBulletMaximumStacks = 73,
    ShootingElementBulletStackDecayPerSecond = 74,
    ShootingElementBulletConsumeStacksOnProc = 75,
    ShootingElementBulletDotDamagePerTick = 76,
    ShootingElementBulletDotTickInterval = 77,
    ShootingElementBulletDotDurationSeconds = 78,
    ShootingElementBulletImpedimentSlowPercentPerStack = 79,
    ShootingElementBulletImpedimentProcSlowPercent = 80,
    ShootingElementBulletImpedimentMaxSlowPercent = 81,
    ShootingElementBulletImpedimentDurationSeconds = 82,
    ShootingKnockbackEnabled = 83,
    ShootingKnockbackStrength = 84,
    ShootingKnockbackDurationSeconds = 85,
    ShootingKnockbackDirectionMode = 86,
    ShootingKnockbackStackingMode = 87,
    ShootingAppliedElementSlot0 = 88,
    ShootingAppliedElementSlot1 = 89,
    ShootingAppliedElementSlot2 = 90,
    ShootingAppliedElementSlot3 = 91,
    ShootingElementFireEffectKind = 92,
    ShootingElementFireProcMode = 93,
    ShootingElementFireReapplyMode = 94,
    ShootingElementFireStacksPerHit = 95,
    ShootingElementFireProcThresholdStacks = 96,
    ShootingElementFireMaximumStacks = 97,
    ShootingElementFireStackDecayPerSecond = 98,
    ShootingElementFireConsumeStacksOnProc = 99,
    ShootingElementFireDotDamagePerTick = 100,
    ShootingElementFireDotTickInterval = 101,
    ShootingElementFireDotDurationSeconds = 102,
    ShootingElementFireImpedimentSlowPercentPerStack = 103,
    ShootingElementFireImpedimentProcSlowPercent = 104,
    ShootingElementFireImpedimentMaxSlowPercent = 105,
    ShootingElementFireImpedimentDurationSeconds = 106,
    ShootingElementIceEffectKind = 107,
    ShootingElementIceProcMode = 108,
    ShootingElementIceReapplyMode = 109,
    ShootingElementIceStacksPerHit = 110,
    ShootingElementIceProcThresholdStacks = 111,
    ShootingElementIceMaximumStacks = 112,
    ShootingElementIceStackDecayPerSecond = 113,
    ShootingElementIceConsumeStacksOnProc = 114,
    ShootingElementIceDotDamagePerTick = 115,
    ShootingElementIceDotTickInterval = 116,
    ShootingElementIceDotDurationSeconds = 117,
    ShootingElementIceImpedimentSlowPercentPerStack = 118,
    ShootingElementIceImpedimentProcSlowPercent = 119,
    ShootingElementIceImpedimentMaxSlowPercent = 120,
    ShootingElementIceImpedimentDurationSeconds = 121,
    ShootingElementPoisonEffectKind = 122,
    ShootingElementPoisonProcMode = 123,
    ShootingElementPoisonReapplyMode = 124,
    ShootingElementPoisonStacksPerHit = 125,
    ShootingElementPoisonProcThresholdStacks = 126,
    ShootingElementPoisonMaximumStacks = 127,
    ShootingElementPoisonStackDecayPerSecond = 128,
    ShootingElementPoisonConsumeStacksOnProc = 129,
    ShootingElementPoisonDotDamagePerTick = 130,
    ShootingElementPoisonDotTickInterval = 131,
    ShootingElementPoisonDotDurationSeconds = 132,
    ShootingElementPoisonImpedimentSlowPercentPerStack = 133,
    ShootingElementPoisonImpedimentProcSlowPercent = 134,
    ShootingElementPoisonImpedimentMaxSlowPercent = 135,
    ShootingElementPoisonImpedimentDurationSeconds = 136,
    ShootingElementCustomEffectKind = 137,
    ShootingElementCustomProcMode = 138,
    ShootingElementCustomReapplyMode = 139,
    ShootingElementCustomStacksPerHit = 140,
    ShootingElementCustomProcThresholdStacks = 141,
    ShootingElementCustomMaximumStacks = 142,
    ShootingElementCustomStackDecayPerSecond = 143,
    ShootingElementCustomConsumeStacksOnProc = 144,
    ShootingElementCustomDotDamagePerTick = 145,
    ShootingElementCustomDotTickInterval = 146,
    ShootingElementCustomDotDurationSeconds = 147,
    ShootingElementCustomImpedimentSlowPercentPerStack = 148,
    ShootingElementCustomImpedimentProcSlowPercent = 149,
    ShootingElementCustomImpedimentMaxSlowPercent = 150,
    ShootingElementCustomImpedimentDurationSeconds = 151,
    ShootingAppliedElementDynamicSlot = 152
}

/// <summary>
/// Stores one controller scaling entry baked from Add Scaling authoring data.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeControllerScalingElement : IBufferElementData
{
    public PlayerRuntimeControllerFieldId FieldId;
    public int SlotIndex;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString512Bytes Formula;
}

/// <summary>
/// Identifies one progression runtime field that can be rewritten from a scaling formula.
/// none.
/// </summary>
public enum PlayerRuntimeProgressionFieldId : byte
{
    PhaseStartingRequiredLevelUpExp = 0,
    PhaseRequiredExperienceGrouth = 1
}

/// <summary>
/// Stores one progression scaling entry baked from Add Scaling authoring data.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeProgressionScalingElement : IBufferElementData
{
    public int PhaseIndex;
    public PlayerRuntimeProgressionFieldId FieldId;
    public float BaseValue;
    public byte IsInteger;
    public FixedString512Bytes Formula;
}

/// <summary>
/// Stores one power-up scaling entry baked from Add Scaling authoring data.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimePowerUpScalingElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public PlayerPowerUpUnlockKind UnlockKind;
    public FixedString128Bytes PayloadPath;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString64Bytes BaseTokenValue;
    public FixedString512Bytes Formula;
}
#endregion
