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
    public float TravelledDistance;
    public float3 LastPosition;
    public float LastObservedDamageLifetimeSeconds;
    public byte Initialized;
}

/// <summary>
/// Stores the base boss pattern used when no boss-specific interaction is active.
/// </summary>
public struct EnemyBossPatternBaseConfig : IComponentData
{
    public byte HasCustomMovement;
    public int FirstShooterConfigIndex;
    public int ShooterConfigCount;
    public int FirstOffensiveEngagementConfigIndex;
    public int OffensiveEngagementConfigCount;
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
/// Stores shooter configs referenced by the base boss pattern and boss-specific interactions.
/// </summary>
public struct EnemyBossPatternShooterConfigElement : IBufferElementData
{
    public EnemyShooterConfigElement ShooterConfig;
}

/// <summary>
/// Stores offensive engagement configs referenced by the base boss pattern and boss-specific interactions.
/// </summary>
public struct EnemyBossPatternOffensiveEngagementConfigElement : IBufferElementData
{
    public EnemyOffensiveEngagementConfigElement Config;
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
