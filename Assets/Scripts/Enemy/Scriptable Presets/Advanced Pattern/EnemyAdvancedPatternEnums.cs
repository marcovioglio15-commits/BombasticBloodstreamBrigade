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
    ShortRangeDash = 6,
    PowerUpStealer = 7,
    Bombardier = 8
}

/// <summary>
/// Declares when a Power-Up Stealer enemy attempts to steal one player power-up.
/// </summary>
public enum EnemyPowerUpStealTriggerMode
{
    OnModuleActivation = 0,
    OnFirstPlayerHit = 1,
    OnEveryPlayerHit = 2
}

/// <summary>
/// Declares which player power-up categories are valid targets for Power-Up Stealer modules.
/// </summary>
public enum EnemyPowerUpStealTargetKind
{
    Active = 0,
    Passive = 1,
    ActiveOrPassive = 2
}

/// <summary>
/// Declares how Power-Up Stealer modules choose a specific power-up after the target category is resolved.
/// </summary>
public enum EnemyPowerUpStealSelectionMode
{
    FirstObtained = 0,
    LastObtained = 1,
    Random = 2
}

/// <summary>
/// Declares movement variants available for Wanderer modules.
/// </summary>
public enum EnemyWandererMode
{
    Basic = 0,
    Dvd = 1,
    Acid = 2
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
/// Declares how Bombardier modules select landing targets in a reach state.
/// </summary>
public enum EnemyBombardierTargetingMode
{
    Disabled = 0,
    Player = 1,
    RandomAroundEnemy = 2,
    RandomAroundPlayer = 3
}

/// <summary>
/// Declares how one Bombardier launch distributes its bomb group.
/// </summary>
public enum EnemyBombardierLaunchPattern
{
    Cluster = 0,
    Radial = 1
}

/// <summary>
/// Declares how Bombardier trajectory velocity is solved.
/// </summary>
public enum EnemyBombardierTrajectoryMode
{
    FixedFlightTimeAndGravity = 0,
    FixedApexHeight = 1
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
    ShortRangeDash = 5,
    WandererAcid = 6
}

/// <summary>
/// Declares how many tactical movement candidates an enemy can evaluate at its current steering LOD.
/// </summary>
public enum EnemyTacticalCandidateBudget : byte
{
    Low = 0,
    Balanced = 1,
    High = 2
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
    ExtraComboPoints = 1,
    Recovery = 2
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
/// Declares how multiple Drop Items module bindings inside one pattern are resolved at enemy death.
/// </summary>
public enum EnemyDropItemsModuleCombineMode
{
    AllModules = 0,
    SingleWeightedModule = 1,
    WeightedSubset = 2
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
