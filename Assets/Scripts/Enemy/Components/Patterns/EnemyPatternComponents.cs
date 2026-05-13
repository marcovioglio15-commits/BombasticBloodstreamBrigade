using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

#region Pattern Components
/// <summary>
/// Stores compiled movement pattern configuration used at runtime.
/// </summary>
public struct EnemyPatternConfig : IComponentData
{
    public EnemyCompiledMovementPatternKind MovementKind;
    public byte HasShortRangeInteraction;
    public EnemyCompiledMovementPatternKind ShortRangeMovementKind;
    public float ShortRangeActivationRange;
    public float ShortRangeReleaseDistanceBuffer;
    public float ShortRangeSearchRadius;
    public float ShortRangeMinimumTravelDistance;
    public float ShortRangeMaximumTravelDistance;
    public float ShortRangeArrivalTolerance;
    public int ShortRangeCandidateSampleCount;
    public byte ShortRangeUseInfiniteDirectionSampling;
    public float ShortRangeInfiniteDirectionStepDegrees;
    public float ShortRangeMinimumEnemyClearance;
    public float ShortRangeTrajectoryPredictionTime;
    public float ShortRangeFreeTrajectoryPreference;
    public float ShortRangeBlockedPathRetryDelay;
    public float ShortRangeRetreatDirectionPreference;
    public float ShortRangeOpenSpacePreference;
    public float ShortRangeNavigationPreference;
    public float ShortRangeRetreatSpeedMultiplierFar;
    public float ShortRangeRetreatSpeedMultiplierNear;
    public float ShortRangeDashAimDuration;
    public float ShortRangeDashAimMoveSpeedMultiplier;
    public float ShortRangeDashCooldownSeconds;
    public float ShortRangeDashDuration;
    public EnemyShortRangeDashDistanceSource ShortRangeDashDistanceSource;
    public float ShortRangeDashDistanceMultiplier;
    public float ShortRangeDashDistanceOffset;
    public float ShortRangeDashFixedDistance;
    public float ShortRangeDashMinimumTravelDistance;
    public float ShortRangeDashMaximumTravelDistance;
    public float ShortRangeDashLateralAmplitude;
    public EnemyShortRangeDashMirrorMode ShortRangeDashMirrorMode;
    public FixedList128Bytes<float2> ShortRangeDashPathSamples;
    public byte StationaryFreezeRotation;
    public float BasicSearchRadius;
    public float BasicMinimumTravelDistance;
    public float BasicMaximumTravelDistance;
    public float BasicArrivalTolerance;
    public float BasicWaitCooldownSeconds;
    public int BasicCandidateSampleCount;
    public byte BasicUseInfiniteDirectionSampling;
    public float BasicInfiniteDirectionStepDegrees;
    public float BasicUnexploredDirectionPreference;
    public float BasicTowardPlayerPreference;
    public float BasicMinimumEnemyClearance;
    public float BasicTrajectoryPredictionTime;
    public float BasicFreeTrajectoryPreference;
    public float BasicBlockedPathRetryDelay;
    public float CowardDetectionRadius;
    public float CowardReleaseDistanceBuffer;
    public float CowardRetreatDirectionPreference;
    public float CowardOpenSpacePreference;
    public float CowardNavigationPreference;
    public float CowardPatrolRadius;
    public float CowardPatrolWaitSeconds;
    public float CowardPatrolSpeedMultiplier;
    public float CowardRetreatSpeedMultiplierFar;
    public float CowardRetreatSpeedMultiplierNear;
    public float DvdSpeedMultiplier;
    public float DvdBounceDamping;
    public byte DvdRandomizeInitialDirection;
    public float DvdFixedInitialDirectionDegrees;
    public float DvdCornerNudgeDistance;
    public byte DvdIgnoreSteeringAndPriority;
}

/// <summary>
/// Stores mutable runtime state used by custom movement patterns.
/// </summary>
public struct EnemyPatternRuntimeState : IComponentData
{
    public byte ShortRangeInteractionActive;
    public EnemyShortRangeDashPhase ShortRangeDashPhase;
    public float ShortRangeDashPhaseElapsed;
    public float ShortRangeDashCooldownRemaining;
    public float3 ShortRangeDashOrigin;
    public float3 ShortRangeDashAimDirection;
    public float ShortRangeDashTravelDistance;
    public float ShortRangeDashLateralSign;
    public float3 WanderTargetPosition;
    public float WanderWaitTimer;
    public float WanderRetryTimer;
    public float LastWanderDirectionAngle;
    public byte WanderHasTarget;
    public byte WanderInitialized;
    public float3 CowardPatrolAnchorPosition;
    public byte CowardPatrolAnchorInitialized;
    public float3 DvdDirection;
    public byte DvdInitialized;
}

/// <summary>
/// Runtime state that allows Shooter modules to temporarily lock movement.
/// </summary>
public struct EnemyShooterControlState : IComponentData
{
    public byte MovementLocked;
    public float3 AimDirection;
    public byte HasAimDirection;
}

/// <summary>
/// Runtime shooter module configuration compiled from active patterns.
/// </summary>
public struct EnemyShooterConfigElement : IBufferElementData
{
    public EnemyShooterAimPolicy AimPolicy;
    public EnemyShooterMovementPolicy MovementPolicy;
    public EnemyShooterShotPattern ShotPattern;
    public float FireInterval;
    public int BurstCount;
    public float AimWindupSeconds;
    public float PreFireStopSeconds;
    public float PostFireStopSeconds;
    public float IntraBurstDelay;
    public byte UseMinimumRange;
    public float MinimumRange;
    public byte UseMaximumRange;
    public float MaximumRange;
    public byte ExclusiveLookDirectionControl;
    public EnemyWeaponInteractionActivationGate ActivationGates;
    public float MaximumActivationSpeed;
    public float RecentlyDamagedWindowSeconds;
    public int ProjectilesPerShot;
    public float SpreadAngleDegrees;
    public float ProjectileSpeed;
    public float ProjectileDamage;
    public float ProjectileRange;
    public float ProjectileLifetime;
    public float ProjectileExplosionRadius;
    public float ProjectileScaleMultiplier;
    public ProjectilePenetrationMode PenetrationMode;
    public int MaxPenetrations;
    public byte InheritShooterSpeed;
    public byte HasElementalPayload;
    public ElementalEffectConfig ElementalEffect;
    public float ElementalStacksPerHit;
}

/// <summary>
/// Mutable runtime shooter module state.
/// </summary>
public struct EnemyShooterRuntimeElement : IBufferElementData
{
    public float NextBurstTimer;
    public float NextShotInBurstTimer;
    public float PostFireStopTimer;
    public int RemainingBurstShots;
    public int ShotsFiredInCurrentBurst;
    public float BurstWindupDurationSeconds;
    public byte IsPlayerInRange;
    public float3 LockedAimDirection;
    public byte HasLockedAimDirection;
}

/// <summary>
/// Tags enemies whose movement is driven by custom pattern systems instead of default steering.
/// </summary>
public struct EnemyCustomPatternMovementTag : IComponentData
{
}
#endregion
