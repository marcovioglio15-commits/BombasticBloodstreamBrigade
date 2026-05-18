using System;

/// <summary>
/// Declares supported enemy pattern module categories.
/// </summary>
public enum EnemyPatternModuleKind
{
    Stationary = 0,
    Grunt = 1,
    Wanderer = 2,
    Shooter = 3,
    DropItems = 4,
    Coward = 5,
    ShortRangeDash = 6
}

/// <summary>
/// Declares movement variants available for Wanderer modules.
/// </summary>
public enum EnemyWandererMode
{
    Basic = 0,
    Dvd = 1
}

/// <summary>
/// Declares how Shooter modules resolve aim direction.
/// </summary>
public enum EnemyShooterAimPolicy
{
    LockOnFireStart = 0,
    ContinuousTracking = 1
}

/// <summary>
/// Declares how Shooter modules interact with movement while firing.
/// </summary>
public enum EnemyShooterMovementPolicy
{
    KeepMoving = 0,
    StopWhileAiming = 1
}

/// <summary>
/// Declares how one Shooter activation distributes its projectile group.
/// </summary>
public enum EnemyShooterShotPattern
{
    ForwardSpread = 0,
    RadialBurst = 1
}

/// <summary>
/// Declares runtime-resolved movement pattern kind used by ECS systems.
/// </summary>
public enum EnemyCompiledMovementPatternKind : byte
{
    Grunt = 0,
    Stationary = 1,
    WandererBasic = 2,
    WandererDvd = 3,
    Coward = 4,
    ShortRangeDash = 5
}

/// <summary>
/// Declares how short-range dash travel distance is resolved.
/// </summary>
public enum EnemyShortRangeDashDistanceSource
{
    PlayerDistance = 0,
    FixedDistance = 1
}

/// <summary>
/// Declares how the short-range dash picks a lateral side around the locked aim line.
/// </summary>
public enum EnemyShortRangeDashMirrorMode
{
    Right = 0,
    Left = 1,
    Alternate = 2,
    Random = 3
}

/// <summary>
/// Declares runtime phases used by the short-range dash override.
/// </summary>
public enum EnemyShortRangeDashPhase : byte
{
    Idle = 0,
    Aiming = 1,
    Dashing = 2
}

/// <summary>
/// Declares drop-items payload categories supported by DropItems modules.
/// </summary>
public enum EnemyDropItemsPayloadKind
{
    Experience = 0,
    ExtraComboPoints = 1
}

/// <summary>
/// Declares the runtime metric inspected by one Extra Combo Points condition.
/// </summary>
public enum EnemyExtraComboPointsMetric
{
    LifetimeSinceSpawnSeconds = 0,
    TimeSinceFirstDamageSeconds = 1,
    TimeSinceLastDamageSeconds = 2,
    DamageWindowSeconds = 3,
    SpawnToFirstDamageSeconds = 4
}

/// <summary>
/// Declares how matching Extra Combo Points conditions are combined inside one module.
/// </summary>
public enum EnemyExtraComboPointsConditionCombineMode
{
    MultiplyMatchingConditions = 0,
    HighestMatchingMultiplier = 1,
    LowestMatchingMultiplier = 2
}

/// <summary>
/// Declares optional runtime gates that can further restrict Weapon Interaction activation.
/// </summary>
[Flags]
public enum EnemyWeaponInteractionActivationGate
{
    Always = 0,
    RequireBelowSpeed = 1,
    RequireRecentlyDamaged = 2,
    RequireWandererWait = 4
}

/// <summary>
/// Declares boss-only eligibility criteria used by pattern and module extraction.
/// </summary>
public enum EnemyBossPatternInteractionType
{
    MissingHealth = 0,
    ElapsedTime = 1,
    TravelledDistance = 2,
    PlayerDistance = 3,
    RecentlyDamaged = 4,
    Always = 5
}

/// <summary>
/// Declares the player-distance condition used by boss pattern extraction rules.
/// </summary>
public enum EnemyBossPatternPlayerDistanceCondition
{
    Disabled = 0,
    BelowThreshold = 1,
    AboveThreshold = 2
}

/// <summary>
/// Declares whether one boss module candidate applies a real module or intentionally clears its slot.
/// </summary>
public enum EnemyBossPatternModuleMode
{
    NullModule = 0,
    Module = 1
}

/// <summary>
/// Declares boss pattern slots controlled by per-pattern internal extraction.
/// </summary>
public enum EnemyBossPatternSlotKind : byte
{
    CoreMovement = 0,
    ShortRangeInteraction = 1,
    WeaponInteraction = 2
}

/// <summary>
/// Declares how boss death drops select their authored candidates.
/// </summary>
public enum EnemyBossDropExtractionMode
{
    SingleCandidate = 0,
    SumAllCandidates = 1
}

/// <summary>
/// Declares how one boss minion spawn rule is activated at runtime.
/// </summary>
public enum EnemyBossMinionSpawnTrigger
{
    Interval = 0,
    BossDamaged = 1,
    HealthBelowPercent = 2
}
