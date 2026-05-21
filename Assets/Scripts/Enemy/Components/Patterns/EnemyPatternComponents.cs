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
    public byte AcidTrailEnabled;
    public float AcidTrailSegmentLifetimeSeconds;
    public float AcidTrailSpawnDistance;
    public float AcidTrailSpawnIntervalSeconds;
    public float AcidTrailRadius;
    public int AcidTrailMaxActiveSegments;
    public float AcidTrailDamagePerTick;
    public float AcidTrailApplyIntervalSeconds;
    public float AcidTrailMinimumMovementSpeed;
    public byte AcidTrailDebugDrawSegments;
    public Entity AcidTrailVfxPrefabEntity;
    public byte AcidTrailScaleVfxToRadius;
    public float AcidTrailVfxScaleMultiplier;
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
    public float3 CowardLastResolvedVelocity;
    public float CowardRetargetLockTimer;
    public float CowardRecoveryTimer;
    public byte CowardHasResolvedVelocity;
    public float3 DvdDirection;
    public byte DvdInitialized;
    public float3 AcidLastSpawnPosition;
    public float AcidSpawnTimer;
    public float AcidPlayerDamageCooldown;
    public byte AcidInitialized;
    public byte AcidPlayerOverlapping;
}

/// <summary>
/// Stores one active acid trail segment emitted by an Acid Wanderer enemy.
/// </summary>
[InternalBufferCapacity(8)]
public struct EnemyAcidTrailSegmentElement : IBufferElementData
{
    public float3 StartPosition;
    public float3 EndPosition;
    public float Radius;
    public float RemainingLifetime;
    public float ApplyIntervalSeconds;
    public float DamagePerTick;
    public byte VfxSpawned;
}

/// <summary>
/// Stores compact tactical navigation weights baked from the enemy brain preset.
/// </summary>
public struct EnemyTacticalNavigationConfig : IComponentData
{
    public EnemyTacticalCandidateBudget CandidateBudget;
    public float NavigationInfluence;
    public float PredictionHorizonSeconds;
    public float SidePassPreference;
    public float CrowdLanePreference;
    public float WallTangentPreference;
    public float OscillationDamping;
    public float StuckRecoverySeconds;
}

/// <summary>
/// Stores short-lived movement memory used to reduce direction flipping and wall-stuck loops.
/// </summary>
public struct EnemyNavigationRuntimeState : IComponentData
{
    public float3 LastDesiredDirection;
    public float3 LastResolvedPosition;
    public float PathCommitTimer;
    public float StuckTimer;
    public sbyte LastSideSign;
    public byte HadValidDirection;
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
/// Runtime Power-Up Stealer module configuration compiled from Weapon Interaction patterns.
/// </summary>
public struct EnemyPowerUpStealerConfigElement : IBufferElementData
{
    public EnemyPowerUpStealTriggerMode TriggerMode;
    public EnemyPowerUpStealTargetKind TargetKind;
    public EnemyPowerUpStealSelectionMode SelectionMode;
    public byte ConsumeModuleActivationAttemptOnSpawnOnly;
    public float ActiveTargetBiasPercent;
    public float AcquisitionStealCooldownSeconds;
    public byte UseMinimumRange;
    public float MinimumRange;
    public byte UseMaximumRange;
    public float MaximumRange;
    public byte ExclusiveLookDirectionControl;
    public EnemyWeaponInteractionActivationGate ActivationGates;
    public float MaximumActivationSpeed;
    public float RecentlyDamagedWindowSeconds;
    public byte UseDamageRecovery;
    public float DamageRecoveryPercent;
    public byte UseTimedDamageRecovery;
    public float TimedDamageRecoveryPercent;
    public float TimedDamageRecoverySeconds;
}

/// <summary>
/// Mutable runtime state for one Power-Up Stealer module instance.
/// </summary>
public struct EnemyPowerUpStealerRuntimeElement : IBufferElementData
{
    public byte HasTriggeredOnce;
    public byte HasStolenPowerUp;
    public PlayerPowerUpUnlockKind StolenKind;
    public FixedString64Bytes PowerUpId;
    public PlayerStoredActivePowerUpData StoredActivePowerUp;
    public PlayerPassiveToolConfig StoredPassiveTool;
    public int OriginalActiveSlotIndex;
    public int OriginalActiveEquipOrder;
    public int OriginalPassiveCatalogIndex;
    public int OriginalPassiveBufferIndex;
    public int OriginalPassiveUnlockCount;
    public Entity PlayerEntity;
    public byte UseDamageRecovery;
    public float DamageRecoveryPercent;
    public byte UseTimedDamageRecovery;
    public float TimedDamageRecoveryPercent;
    public float TimedDamageRecoverySeconds;
    public float HealthAtSteal;
    public float LastObservedHealth;
    public float RecoveryWindowElapsedSeconds;
    public float RecoveryWindowAccumulatedPercent;
}

/// <summary>
/// Stores the icon state currently displayed above enemies holding a stolen power-up.
/// </summary>
public struct EnemyPowerUpStealerVisualState : IComponentData
{
    public byte HasStolenPowerUp;
    public FixedString64Bytes PowerUpId;
    public PlayerPowerUpUnlockKind StolenKind;
}

/// <summary>
/// Tags enemies whose movement is driven by custom pattern systems instead of default steering.
/// </summary>
public struct EnemyCustomPatternMovementTag : IComponentData
{
}
#endregion
