using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#region Core State And Cheat Data
public enum PlayerPowerUpCheatCommandType : byte
{
    None = 0,
    ApplyPresetByIndex = 1
}

/// <summary>
/// Holds runtime state for power-up slots and activation inputs.
/// </summary>
public struct PlayerPowerUpsState : IComponentData
{
    public float PrimaryEnergy;
    public float SecondaryEnergy;
    public float PrimaryCooldownRemaining;
    public float SecondaryCooldownRemaining;
    public float PrimaryCharge;
    public float SecondaryCharge;
    public float PrimaryMaintenanceTickTimer;
    public float SecondaryMaintenanceTickTimer;
    public byte PrimaryIsCharging;
    public byte SecondaryIsCharging;
    public byte PrimaryIsActive;
    public byte SecondaryIsActive;
    public byte IsShootingSuppressed;
    public byte PreviousPrimaryPressed;
    public byte PreviousSecondaryPressed;
    public byte PreviousSwapSlotsPressed;
    public byte HasPendingCheatCommand;
    public PlayerPowerUpCheatCommandType PendingCheatCommandType;
    public int PrimaryEquipOrder;
    public int SecondaryEquipOrder;
    public int NextEquipOrder;
    public int PendingCheatPresetIndex;
    public int PrimaryReturningProjectileCount;
    public int SecondaryReturningProjectileCount;
    public int PrimaryReturningProjectileRecallReadyCount;
    public int SecondaryReturningProjectileRecallReadyCount;
    public uint PrimaryReturningProjectileGeneration;
    public uint SecondaryReturningProjectileGeneration;
    public uint PrimaryReturningProjectileRecallVersion;
    public uint SecondaryReturningProjectileRecallVersion;
    public uint LastObservedGlobalKillCount;
    public uint LastObservedRoomClearCount;
    public float3 LastValidMovementDirection;
    public PowerUpConditionalApplicationRuntimeState PrimaryConditionalApplication;
    public PowerUpConditionalApplicationRuntimeState SecondaryConditionalApplication;
}

/// <summary>
/// Stores mutable cadence, automatic-charge, recovery, and threshold-edge state for one conditional power-up instance.
/// </summary>
public struct PowerUpConditionalApplicationRuntimeState
{
    public int ShotCounter;
    public float Charge;
    public float MovementSlowPercent;
    public float ToggleActiveElapsedSeconds;
    public uint LastObservedShotPulseVersion;
    public byte Initialized;
    public byte Armed;
    public byte HealthConditionWasMet;
}

/// <summary>
/// Runtime snapshot metadata for one cheat-selectable power-up preset.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpCheatPresetEntry : IBufferElementData
{
    public byte IsDefined;
    public int SlotStartIndex;
    public int SlotCount;
    public int PassiveStartIndex;
    public int PassiveCount;
}

/// <summary>
/// Flattened active-slot payloads referenced by PlayerPowerUpCheatPresetEntry.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpCheatPresetSlotElement : IBufferElementData
{
    public byte SlotIndex;
    public PlayerPowerUpSlotConfig Slot;
}

/// <summary>
/// Flattened passive-tool payloads referenced by PlayerPowerUpCheatPresetEntry.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpCheatPresetPassiveElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public PlayerPassiveToolConfig Tool;
}
#endregion

#region Unlock Catalog And Scaling
/// <summary>
/// Runtime payload kind baked for one unlockable modular power-up catalog entry.
/// </summary>
public enum PlayerPowerUpUnlockKind : byte
{
    Active = 0,
    Passive = 1
}

/// <summary>
/// One unlockable modular power-up entry baked for milestone tier extraction.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpUnlockCatalogElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public FixedString64Bytes DisplayName;
    public FixedString4096Bytes Description;
    public PlayerPowerUpUnlockKind UnlockKind;
    public byte StealProtected;
    public byte IsUnlocked;
    public byte PendingInitialCharacterTuningApply;
    public int CurrentUnlockCount;
    public int MaximumUnlockCount;
    public float LastAcquiredTime;
    public int CharacterTuningFormulaStartIndex;
    public int CharacterTuningFormulaCount;
    public PlayerPowerUpSlotConfig ActiveSlotConfig;
    public PlayerPassiveToolConfig PassiveToolConfig;
}

/// <summary>
/// Stores one flattened Character Tuning formula referenced by unlock catalog entries.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpCharacterTuningFormulaElement : IBufferElementData
{
    public FixedString128Bytes Formula;
}

/// <summary>
/// Tracks which active runtime-scoped slots currently own a temporary Character Tuning application.
/// </summary>
public struct PlayerChargeCharacterTuningState : IComponentData
{
    public byte PrimaryIsApplied;
    public byte SecondaryIsApplied;
    public uint PrimaryOwnershipSignature;
    public uint SecondaryOwnershipSignature;
    public uint PassiveOwnershipSignature;
}

/// <summary>
/// Stores one baseline scalable-stat value that must be restored after temporary runtime-scoped Character Tuning ends.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerChargeCharacterTuningBaseStatElement : IBufferElementData
{
    public FixedString64Bytes Name;
    public byte Type;
    public float Value;
    public byte BooleanValue;
    public FixedString64Bytes TokenValue;
}

/// <summary>
/// Stores the projectile-size multiplier contributed by one runtime-scoped power-up source.
/// Returning Projectiles uses this provenance to retain same-power-up tuning while filtering external Tiny/Mega effects.
/// </summary>
[InternalBufferCapacity(4)]
public struct PlayerProjectileSizePowerUpMultiplierElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public float Multiplier;
}

/// <summary>
/// Tier metadata pointing to a contiguous range inside the flattened tier-entry buffer.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpTierDefinitionElement : IBufferElementData
{
    public FixedString64Bytes TierId;
    public int EntryStartIndex;
    public int EntryCount;
}

/// <summary>
/// Flattened weighted tier entry referencing one unlock catalog index.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpTierEntryElement : IBufferElementData
{
    public int CatalogIndex;
    public float SelectionWeight;
}

/// <summary>
/// Optional runtime scaling metadata for one flattened tier-entry weight.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpTierEntryScalingElement : IBufferElementData
{
    public int TierEntryIndex;
    public float BaseSelectionWeight;
    public FixedString512Bytes ScalingFormula;
}
#endregion

#region Milestone Selection
/// <summary>
/// Runtime milestone-selection state used to pause gameplay and expose power-up choices to HUD.
/// </summary>
public struct PlayerMilestonePowerUpSelectionState : IComponentData
{
    public byte IsSelectionActive;
    public byte HasPendingCommand;
    public PlayerMilestoneSelectionCommandType PendingCommandType;
    public int MilestoneLevel;
    public int GamePhaseIndex;
    public int MilestoneIndex;
    public int OfferCount;
    public int PendingOfferIndex;
}

/// <summary>
/// One rolled power-up option presented to the player at milestone selection time.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerMilestonePowerUpSelectionOfferElement : IBufferElementData
{
    public int CatalogIndex;
    public FixedString64Bytes PowerUpId;
    public FixedString64Bytes DisplayName;
    public FixedString4096Bytes Description;
    public PlayerPowerUpUnlockKind UnlockKind;
}

/// <summary>
/// Identifies which action the HUD requested for the active milestone selection.
/// </summary>
public enum PlayerMilestoneSelectionCommandType : byte
{
    SelectOffer = 0,
    Skip = 1
}

/// <summary>
/// Holds transient state used to restore Time.timeScale smoothly after a milestone selection closes.
/// </summary>
public struct PlayerMilestoneTimeScaleResumeState : IComponentData
{
    public byte IsResuming;
    public float StartTimeScale;
    public float TargetTimeScale;
    public float DurationSeconds;
    public float ElapsedUnscaledSeconds;
}
#endregion

#region Aggregated Passive Tools
/// <summary>
/// Holds aggregated runtime multipliers from equipped passive tools.
/// </summary>
public struct PlayerPassiveToolsState
{
    public float ProjectileSizeMultiplier;
    public float ProjectileSizePowerUpMultiplier;
    public float ProjectileDamageMultiplier;
    public float ProjectileSpeedMultiplier;
    public float ProjectileLifetimeSecondsMultiplier;
    public float ProjectileLifetimeRangeMultiplier;
    public byte HasShotgun;
    public ShotgunPowerUpConfig Shotgun;
    public byte HasElementalProjectiles;
    public ElementalProjectilesPassiveConfig ElementalProjectiles;
    public byte HasPerfectCircle;
    public PerfectCirclePassiveConfig PerfectCircle;
    public byte HasBouncingProjectiles;
    public BouncingProjectilesPassiveConfig BouncingProjectiles;
    public byte HasSplittingProjectiles;
    public SplittingProjectilesPassiveConfig SplittingProjectiles;
    public byte HasExplosion;
    public ExplosionPassiveConfig Explosion;
    public byte HasElementalTrail;
    public ElementalTrailPassiveConfig ElementalTrail;
    public byte HasHeal;
    public PassiveHealConfig Heal;
    public byte HasBulletTime;
    public PassiveBulletTimeConfig BulletTime;
    public byte HasLaserBeam;
    public LaserBeamPassiveConfig LaserBeam;
    public byte HasOrbitalProjections;
    public FixedList4096Bytes<OrbitalProjectionConfig> OrbitalProjections;
    public byte HasWeaponSwitch;
    public FixedString64Bytes WeaponId;
    public byte HasDropAttraction;
    public DropAttractionPowerUpConfig DropAttraction;
    public byte HasReturningProjectiles;
    public ReturningProjectilesConfig ReturningProjectiles;
    public byte HasReturningProjectilesActiveSlotOwner;
    public byte ReturningProjectilesActiveSlotIndex;
}

/// <summary>
/// Stores the single aggregated passive-tools snapshot outside the player chunk payload.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPassiveToolsStateElement : IBufferElementData
{
    public PlayerPassiveToolsState Value;
}
#endregion

#region Active Power-Up Runtime
/// <summary>
/// Holds runtime dash motion and invulnerability state.
/// </summary>
public struct PlayerDashState : IComponentData
{
    public byte IsDashing;
    public byte ClearVelocityAfterApply;
    public byte Phase;
    public float PhaseRemaining;
    public float HoldDuration;
    public float RemainingInvulnerability;
    public float Duration;
    public float Distance;
    public float ElapsedDuration;
    public float3 Direction;
    public float3 EntryVelocity;
    public float Speed;
    public float TransitionInDuration;
    public float TransitionOutDuration;
    public float WallBounceIntensity;
}

/// <summary>
/// Enqueued request to spawn a bomb entity for delayed explosion.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBombSpawnRequest : IBufferElementData
{
    public Entity OwnerEntity;
    public Entity BombPrefabEntity;
    public float3 Position;
    public quaternion Rotation;
    public float3 Velocity;
    public float CollisionRadius;
    public byte BounceOnWalls;
    public float BounceDamping;
    public float LinearDampingPerSecond;
    public float FuseSeconds;
    public float Radius;
    public float Damage;
    public byte AffectAllEnemiesInRadius;
    public Entity ExplosionVfxPrefabEntity;
    public byte ScaleVfxToRadius;
    public float VfxScaleMultiplier;
    public ImpactFramePowerUpConfig ImpactFrame;
    public byte HasImpactFrame;
}

/// <summary>
/// Runtime fuse state for spawned bomb entities.
/// </summary>
public struct BombFuseState : IComponentData
{
    public Entity OwnerEntity;
    public float3 Position;
    public float3 Velocity;
    public float CollisionRadius;
    public byte BounceOnWalls;
    public float BounceDamping;
    public float LinearDampingPerSecond;
    public float FuseRemaining;
    public float Radius;
    public float Damage;
    public byte AffectAllEnemiesInRadius;
    public Entity ExplosionVfxPrefabEntity;
    public byte ScaleVfxToRadius;
    public float VfxScaleMultiplier;
    public ImpactFramePowerUpConfig ImpactFrame;
    public byte HasImpactFrame;
}

/// <summary>
/// Marks bombs that must execute explosion logic this frame.
/// </summary>
public struct BombExplodeRequest : IComponentData
{
}
#endregion

#region Passive Effect Runtime
/// <summary>
/// Holds runtime timers for passive explosion logic.
/// </summary>
public struct PlayerPassiveExplosionState : IComponentData
{
    public float CooldownRemaining;
    public float PreviousObservedHealth;
}

/// <summary>
/// Holds runtime timers for passive heal-over-time logic.
/// </summary>
public struct PlayerPassiveHealState : IComponentData
{
    public float CooldownRemaining;
    public float PreviousObservedHealth;
}

/// <summary>
/// Holds runtime timers for passive bullet-time logic.
/// </summary>
public struct PlayerPassiveBulletTimeState : IComponentData
{
    public float CooldownRemaining;
    public float PreviousObservedHealth;
}

/// <summary>
/// Stores one traveling Laser Beam damage packet with a stable id and current elapsed travel time.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerLaserBeamStormTickPulse : IBufferElementData
{
    public int PulseId;
    public float CurrentElapsedSeconds;
}

/// <summary>
/// Stores the passive hooks needed by a timed active Laser Beam without embedding the full passive runtime state.
/// </summary>
public struct PlayerLaserBeamPassiveSnapshot
{
    public byte HasLaserBeam;
    public LaserBeamPassiveConfig LaserBeam;
    public byte HasPerfectCircle;
    public PerfectCirclePassiveConfig PerfectCircle;
    public byte HasShotgun;
    public ShotgunPowerUpConfig Shotgun;
    public byte HasBouncingProjectiles;
    public BouncingProjectilesPassiveConfig BouncingProjectiles;
    public byte HasSplittingProjectiles;
    public SplittingProjectilesPassiveConfig SplittingProjectiles;
}

/// <summary>
/// Holds runtime activation and timing data for the Laser Beam passive shooting override.
/// </summary>
public struct PlayerLaserBeamState : IComponentData
{
    public byte IsActive;
    public byte IsOverheated;
    public byte IsTickReady;
    public int LastResolvedPrimaryLaneCount;
    public float CooldownRemaining;
    public float ConsecutiveActiveElapsed;
    public float DamageTickTimer;
    public float ContinuousDamageAccumulatorSeconds;
    public float StormBurstRemainingSeconds;
    public int NextStormTickPulseId;
    public float TriggeredActiveRemainingSeconds;
    public ProjectilePenetrationMode TriggeredActivePenetrationMode;
    public int TriggeredActiveMaxPenetrations;
    public PlayerProjectileRequestTemplate TriggeredActiveProjectileTemplate;
    public PlayerLaserBeamPassiveSnapshot TriggeredActivePassiveSnapshot;
    public float ChargeImpulseRemainingSeconds;
    public float ChargeImpulseDamageMultiplier;
    public float ChargeImpulseWidthMultiplier;
    public float ChargeImpulseTravelDistance;
}

/// <summary>
/// Tracks one enemy already damaged by one Laser Beam storm pulse.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerLaserBeamPulseHitElement : IBufferElementData
{
    public int PulseId;
    public Entity EnemyEntity;
}

/// <summary>
/// Stores one resolved Laser Beam lane for the current frame.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerLaserBeamLaneElement : IBufferElementData
{
    public int LaneIndex;
    public byte IsSplitChild;
    public byte IsTerminalSegment;
    public byte TerminalBlockedByWall;
    public float3 StartPoint;
    public float3 EndPoint;
    public float3 Direction;
    public float Length;
    public float CollisionRadius;
    public float VisualWidth;
    public float DamageMultiplier;
    public float3 TerminalNormal;
}

/// <summary>
/// Stores runtime trail spawning state for elemental trail passive.
/// </summary>
public struct PlayerElementalTrailState : IComponentData
{
    public float3 LastSpawnPosition;
    public float SpawnTimer;
    public int ActiveSegments;
    public byte Initialized;
}

/// <summary>
/// Tracks trail segment entities currently owned by one player.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerElementalTrailSegmentElement : IBufferElementData
{
    public Entity SegmentEntity;
}

/// <summary>
/// Runtime payload of one elemental trail segment entity.
/// </summary>
public struct ElementalTrailSegment : IComponentData
{
    public Entity OwnerEntity;
    public float Radius;
    public float RemainingLifetime;
    public float ApplyIntervalSeconds;
    public float ApplyTimer;
    public float StacksPerTick;
    public ElementalEffectConfig Effect;
}
#endregion

#region VFX Requests
/// <summary>
/// Request to apply an explosion payload at a specific world position.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerExplosionRequest : IBufferElementData
{
    public float3 Position;
    public float Radius;
    public float Damage;
    public GameAudioEventId AudioEventId;
    public byte AffectAllEnemiesInRadius;
    public Entity ExplosionVfxPrefabEntity;
    public byte ScaleVfxToRadius;
    public float VfxScaleMultiplier;
}

/// <summary>
/// Request to spawn one-shot VFX entities for passive/elemental feedback.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpVfxSpawnRequest : IBufferElementData
{
    public Entity PrefabEntity;
    public UnityObjectRef<GameObject> SourcePrefab;
    public float3 Position;
    public quaternion Rotation;
    public float UniformScale;
    public float ParticleSimulationSpeedMultiplier;
    public float TrailRendererWidthOverride;
    public float TrailRendererTimeOverrideSeconds;
    public float LifetimeSeconds;
    public Entity FollowTargetEntity;
    public float3 FollowPositionOffset;
    public Entity FollowValidationEntity;
    public uint FollowValidationSpawnVersion;
    public float3 Velocity;
    public int RefreshKey;
    public byte BypassAreaCellCap;
    public byte RestartOldestOnCap;
    public byte ForceLooping;
    public byte DetachWhenFollowTargetInvalid;
    public byte KeepAliveWhileFollowTargetValid;
    public byte HasColorOverride;
    public float4 ColorOverride;
    public float4 SecondaryColorOverride;
    public byte ColorOverrideCount;
    public FixedString64Bytes ColorOverrideChildName;
    public byte FollowMuzzlePose;
    public byte FollowTargetRotation;
}
#endregion

#region VFX Configuration
/// <summary>
/// Selects when the player level-up VFX should be spawned by progression runtime.
/// </summary>
public enum PlayerLevelUpVfxTriggerMode : byte
{
    EveryLevelUp = 0,
    MilestonePowerUpsOnly = 1
}

/// <summary>
/// Selects how charge-shot VFX playback should cover the charge window.
/// </summary>
public enum PlayerChargeShotVfxPlaybackMode : byte
{
    PlayOnceTimedWithChargeCompletion = 0,
    LoopWhileCharging = 1,
    StretchSinglePlaybackToCharge = 2
}

/// <summary>
/// Runtime VFX settings spawned at the projectile origin every time the player fires a shot.
/// </summary>
public struct PlayerMuzzleFlashVfxConfig : IComponentData
{
    public Entity PrefabEntity;
    public UnityObjectRef<GameObject> SourcePrefab;
    public float3 SpawnOffset;
    public float UniformScale;
    public float LifetimeSeconds;
}

/// <summary>
/// Runtime VFX settings spawned when player level progression advances.
/// </summary>
public struct PlayerLevelUpVfxConfig : IComponentData
{
    public Entity PrefabEntity;
    public UnityObjectRef<GameObject> SourcePrefab;
    public float3 SpawnOffset;
    public float UniformScale;
    public float LifetimeSeconds;
    public PlayerLevelUpVfxTriggerMode TriggerMode;
}

/// <summary>
/// Runtime VFX settings spawned while a Charge Shot active tool is charging.
/// </summary>
public struct PlayerChargeShotVfxConfig : IComponentData
{
    public Entity PrefabEntity;
    public UnityObjectRef<GameObject> SourcePrefab;
    public float3 SpawnOffset;
    public float UniformScale;
    public float LifetimeSeconds;
    public PlayerChargeShotVfxPlaybackMode PlaybackMode;
    public byte AppliesToAllHoldChargePowerUps;
}

/// <summary>
/// Tracks charge-shot VFX playback edges so one-shot modes do not restart every frame.
/// </summary>
public struct PlayerChargeShotVfxRuntimeState : IComponentData
{
    public byte PrimaryWasCharging;
    public byte SecondaryWasCharging;
    public byte PrimaryTimedVfxSpawned;
    public byte SecondaryTimedVfxSpawned;
    public byte PrimaryStretchVfxSpawned;
    public byte SecondaryStretchVfxSpawned;
}

/// <summary>
/// Runtime caps applied to power-up VFX spawning.
/// </summary>
public struct PlayerPowerUpVfxCapConfig : IComponentData
{
    public int MaxSamePrefabPerCell;
    public float CellSize;
    public int MaxAttachedSamePrefabPerTarget;
    public int MaxActiveOneShotVfx;
    public byte RefreshAttachedLifetimeOnCapHit;
}
#endregion

#region VFX Runtime
/// <summary>
/// Lifetime tracker for temporary spawned VFX entities.
/// </summary>
public struct PlayerPowerUpVfxLifetime : IComponentData
{
    public float RemainingSeconds;
}

/// <summary>
/// Makes a spawned VFX follow a target entity using LocalTransform.
/// </summary>
public struct PlayerPowerUpVfxFollowTarget : IComponentData
{
    public Entity TargetEntity;
    public float3 PositionOffset;
    public Entity ValidationEntity;
    public uint ValidationSpawnVersion;
}

/// <summary>
/// Moves a spawned VFX with a constant velocity while alive.
/// </summary>
public struct PlayerPowerUpVfxVelocity : IComponentData
{
    public float3 Value;
}

/// <summary>
/// Marks VFX entities managed by the pooled VFX pipeline.
/// </summary>
public struct PlayerPowerUpVfxPooled : IComponentData
{
}

/// <summary>
/// Maps baked power-up VFX prefab entities back to their source prefab assets for managed runtime spawning.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpVfxPrefabBindingElement : IBufferElementData
{
    public Entity PrefabEntity;
    public UnityObjectRef<GameObject> Prefab;
}

/// <summary>
/// Runtime-safe Unity object reference to the source prefab used by the Elemental Trail attached VFX fallback.
/// </summary>
public struct PlayerElementalTrailAttachedVfxPrefabReference : IComponentData
{
    public UnityObjectRef<GameObject> Prefab;
}
#endregion
