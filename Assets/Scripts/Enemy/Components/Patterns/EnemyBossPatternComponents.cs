using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#region Boss Pattern Components
/// <summary>
/// Tags an enemy entity as a boss controlled by a boss pattern preset.
/// </summary>
public struct EnemyBossTag : IComponentData
{
}

/// <summary>
/// Stores immutable boss HUD text and color configuration baked from the visual preset.
/// </summary>
public struct EnemyBossHudConfig : IComponentData
{
    public byte Enabled;
    public byte ShowHealthBar;
    public byte ShowOffscreenIndicator;
    public FixedString64Bytes DisplayName;
    public float4 HealthFillColor;
    public float4 HealthBackgroundColor;
    public float4 ShieldFillColor;
    public float4 ShieldBackgroundColor;
    public float4 OffscreenIndicatorColor;
    public float OffscreenIndicatorSizePixels;
    public float EdgePaddingPixels;
}

/// <summary>
/// Managed boss visual data for UI assets that cannot be stored in unmanaged ECS components.
/// </summary>
public sealed class EnemyBossHudManagedConfig : IComponentData
{
    #region Fields
    public Sprite OffscreenIndicatorSprite;
    #endregion
}

/// <summary>
/// Tracks mutable boss interaction switching state.
/// </summary>
public struct EnemyBossPatternRuntimeState : IComponentData
{
    public int ActiveInteractionIndex;
    public float ElapsedSeconds;
    public float ActiveInteractionElapsedSeconds;
    public float ExtractionElapsedSeconds;
    public float TravelledDistance;
    public float DistanceSinceLastExtraction;
    public float LastExtractionMissingHealthPercent;
    public float PlayerDistanceHoldSeconds;
    public float DamageWindowElapsedSeconds;
    public float DamageWindowAccumulated;
    public float PreviousObservedDurability;
    public float3 LastPosition;
    public float LastObservedDamageLifetimeSeconds;
    public byte Initialized;
}

/// <summary>
/// Stores top-level boss pattern extraction settings and the default null pattern state.
/// </summary>
public struct EnemyBossPatternExtractionConfig : IComponentData
{
    public byte HasCustomMovement;
    public byte RerollWhenCurrentPatternBecomesInvalid;
    public byte UseElapsedIntervalExtraction;
    public byte UseMissingHealthStepExtraction;
    public byte UseTravelledDistanceExtraction;
    public byte UseDamageWindowExtraction;
    public int FirstShooterConfigIndex;
    public int ShooterConfigCount;
    public int FirstOffensiveEngagementConfigIndex;
    public int OffensiveEngagementConfigCount;
    public EnemyBossPatternPlayerDistanceCondition PlayerDistanceCondition;
    public float MinimumSecondsBetweenExtractions;
    public float ElapsedIntervalSeconds;
    public float MissingHealthStepPercent;
    public float TravelledDistanceSinceLastExtraction;
    public float PlayerDistanceThreshold;
    public float PlayerDistanceHoldSeconds;
    public float DamageWindowSeconds;
    public float DamageThreshold;
    public EnemyPatternConfig PatternConfig;
}

/// <summary>
/// Stores one compiled boss-specific interaction layer.
/// </summary>
public struct EnemyBossPatternInteractionElement : IBufferElementData
{
    public int InteractionIndex;
    public EnemyBossPatternInteractionType InteractionType;
    public float MinimumActiveSeconds;
    public float SelectionWeight;
    public float MinimumMissingHealthPercent;
    public float MaximumMissingHealthPercent;
    public float MinimumElapsedSeconds;
    public float MaximumElapsedSeconds;
    public float MinimumTravelledDistance;
    public float MaximumTravelledDistance;
    public float MinimumPlayerDistance;
    public float MaximumPlayerDistance;
    public float RecentlyDamagedWindowSeconds;
    public byte HasCustomMovement;
    public int FirstShooterConfigIndex;
    public int ShooterConfigCount;
    public int FirstOffensiveEngagementConfigIndex;
    public int OffensiveEngagementConfigCount;
    public EnemyPatternConfig PatternConfig;
}

/// <summary>
/// Stores extraction settings for one internal boss pattern module slot.
/// </summary>
public struct EnemyBossPatternModuleExtractionElement : IBufferElementData
{
    public int PatternIndex;
    public EnemyBossPatternSlotKind SlotKind;
    public byte RerollWhenCurrentPatternBecomesInvalid;
    public byte UseElapsedIntervalExtraction;
    public byte UseMissingHealthStepExtraction;
    public byte UseTravelledDistanceExtraction;
    public byte UseDamageWindowExtraction;
    public EnemyBossPatternPlayerDistanceCondition PlayerDistanceCondition;
    public float MinimumSecondsBetweenExtractions;
    public float ElapsedIntervalSeconds;
    public float MissingHealthStepPercent;
    public float TravelledDistanceSinceLastExtraction;
    public float PlayerDistanceThreshold;
    public float PlayerDistanceHoldSeconds;
    public float DamageWindowSeconds;
    public float DamageThreshold;
}

/// <summary>
/// Stores one compiled module candidate owned by an internal boss pattern slot.
/// </summary>
public struct EnemyBossPatternModuleCandidateElement : IBufferElementData
{
    public int PatternIndex;
    public EnemyBossPatternSlotKind SlotKind;
    public int CandidateIndex;
    public EnemyBossPatternInteractionType EligibilityType;
    public byte IsNullModule;
    public byte HasCustomMovement;
    public float MinimumActiveSeconds;
    public float SelectionWeight;
    public float MinimumMissingHealthPercent;
    public float MaximumMissingHealthPercent;
    public float MinimumElapsedSeconds;
    public float MaximumElapsedSeconds;
    public float MinimumTravelledDistance;
    public float MaximumTravelledDistance;
    public float MinimumPlayerDistance;
    public float MaximumPlayerDistance;
    public float RecentlyDamagedWindowSeconds;
    public int FirstShooterConfigIndex;
    public int ShooterConfigCount;
    public int FirstPowerUpStealerConfigIndex;
    public int PowerUpStealerConfigCount;
    public int FirstOffensiveEngagementConfigIndex;
    public int OffensiveEngagementConfigCount;
    public EnemyPatternConfig PatternConfig;
}

/// <summary>
/// Tracks runtime extraction state for one internal boss pattern slot.
/// </summary>
public struct EnemyBossPatternSlotRuntimeElement : IBufferElementData
{
    public EnemyBossPatternSlotKind SlotKind;
    public int ActivePatternIndex;
    public int ActiveCandidateIndex;
    public float ActiveCandidateElapsedSeconds;
    public float ExtractionElapsedSeconds;
    public float DistanceSinceLastExtraction;
    public float LastExtractionMissingHealthPercent;
    public float PlayerDistanceHoldSeconds;
    public float DamageWindowElapsedSeconds;
    public float DamageWindowAccumulated;
    public float PreviousObservedDurability;
}

/// <summary>
/// Stores shooter configs referenced by boss module candidates.
/// </summary>
public struct EnemyBossPatternShooterConfigElement : IBufferElementData
{
    public EnemyShooterConfigElement ShooterConfig;
}

/// <summary>
/// Stores Power-Up Stealer configs referenced by boss module candidates.
/// </summary>
public struct EnemyBossPatternPowerUpStealerConfigElement : IBufferElementData
{
    public EnemyPowerUpStealerConfigElement StealerConfig;
}

/// <summary>
/// Stores offensive engagement configs referenced by boss module candidates.
/// </summary>
public struct EnemyBossPatternOffensiveEngagementConfigElement : IBufferElementData
{
    public EnemyOffensiveEngagementConfigElement Config;
}

/// <summary>
/// Stores boss death drop extraction mode baked from the boss pattern preset.
/// </summary>
public struct EnemyBossDropExtractionConfig : IComponentData
{
    public byte Enabled;
    public EnemyBossDropExtractionMode ExtractionMode;
}

/// <summary>
/// Tracks whether boss death drop candidates have already been selected for the current death event.
/// </summary>
public struct EnemyBossDropRuntimeState : IComponentData
{
    public byte SelectionResolved;
}

/// <summary>
/// Stores one boss drop candidate and its source buffer slices.
/// </summary>
public struct EnemyBossDropCandidateElement : IBufferElementData
{
    public int CandidateIndex;
    public byte Enabled;
    public float SelectionWeight;
    public int FirstExperienceModuleIndex;
    public int ExperienceModuleCount;
    public int FirstExtraComboPointsModuleIndex;
    public int ExtraComboPointsModuleCount;
}

/// <summary>
/// Stores a selected boss drop candidate for the current death event.
/// </summary>
public struct EnemyBossSelectedDropCandidateElement : IBufferElementData
{
    public int CandidateIndex;
}

/// <summary>
/// Stores one boss-owned source experience-drop module before death-time extraction.
/// </summary>
public struct EnemyBossDropExperienceModuleElement : IBufferElementData
{
    public EnemyExperienceDropModuleElement Module;
}

/// <summary>
/// Stores one boss-owned source experience-drop definition before death-time extraction.
/// </summary>
public struct EnemyBossDropExperienceDefinitionElement : IBufferElementData
{
    public EnemyExperienceDropDefinitionElement Definition;
}

/// <summary>
/// Stores one boss-owned source Extra Combo Points module before death-time extraction.
/// </summary>
public struct EnemyBossDropExtraComboPointsModuleElement : IBufferElementData
{
    public EnemyExtraComboPointsModuleElement Module;
}

/// <summary>
/// Stores one boss-owned source Extra Combo Points condition before death-time extraction.
/// </summary>
public struct EnemyBossDropExtraComboPointsConditionElement : IBufferElementData
{
    public EnemyExtraComboPointsConditionElement Condition;
}

/// <summary>
/// Stores one boss-owned minion spawn rule and its runtime pool state.
/// </summary>
public struct EnemyBossMinionSpawnElement : IBufferElementData
{
    public Entity PrefabEntity;
    public EnemyBossMinionSpawnTrigger Trigger;
    public float IntervalSeconds;
    public float BossHitCooldownSeconds;
    public float HealthThresholdPercent;
    public int SpawnCount;
    public int MaxAliveMinions;
    public float SpawnRadius;
    public float DespawnDistance;
    public float ExperienceDropMultiplier;
    public float ExtraComboPointsMultiplier;
    public float FutureDropsMultiplier;
    public int AutomaticPoolSize;
    public int PoolExpandBatch;
    public byte KillMinionsOnBossDeath;
    public byte RequireMinionsKilledForRunCompletion;
    public Entity PoolEntity;
    public float NextSpawnTime;
    public float LastObservedDamageLifetimeSeconds;
    public byte Triggered;
    public byte Initialized;
}

/// <summary>
/// Stores one boss minion reserved during its spawn warning window and activated once the ring completes.
/// </summary>
public struct EnemyBossPendingMinionSpawnElement : IBufferElementData
{
    public Entity MinionEntity;
    public Entity PoolEntity;
    public int RuleIndex;
    public float3 SpawnPosition;
    public float ActivationTime;
}

/// <summary>
/// Marks minions spawned by a boss and stores the source rule for alive-count throttling.
/// </summary>
public struct EnemyBossMinionOwner : IComponentData
{
    public Entity BossEntity;
    public int RuleIndex;
    public byte KillOnBossDeath;
    public byte BlocksRunCompletion;
}

/// <summary>
/// Scales rewards emitted by special enemies such as boss-spawned minions.
/// </summary>
public struct EnemyDropRewardMultiplier : IComponentData
{
    public float ExperienceMultiplier;
    public float ExtraComboPointsMultiplier;
    public float FutureDropsMultiplier;
}
#endregion
