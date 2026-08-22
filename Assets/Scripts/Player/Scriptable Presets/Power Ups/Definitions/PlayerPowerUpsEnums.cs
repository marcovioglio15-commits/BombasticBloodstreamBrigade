using UnityEngine;

public enum PassiveModifierKind
{
    StatModifier = 0,
    GameplayModifier = 1
}

public enum PassiveStatType
{
    MaxHealth = 0,
    MoveSpeed = 1,
    ProjectileDamage = 2,
    FireRate = 3
}

public enum PassiveStatOperation
{
    Add = 0,
    Multiply = 1
}

public enum ActiveToolKind
{
    Bomb = 0,
    Dash = 1,
    BulletTime = 2,
    Custom = 3,
    Shotgun = 4,
    ChargeShot = 5,
    PortableHealthPack = 6,
    PassiveToggle = 7,
    OrbitalProjections = 8,
    ImpactFrame = 9,
    GhostTrail = 10,
    DropAttraction = 11,
    ReturningProjectile = 12
}

/// <summary>
/// Selects the easing curve used by the Impact Frame transitions during impact entry and recovery.
/// </summary>
public enum ImpactFrameEasingMode
{
    Linear = 0,
    EaseInOutSine = 1,
    EaseOutCubic = 2,
    EaseInExpo = 3,
    EaseOutExpo = 4
}

/// <summary>
/// Selects which duration source ends the Impact Frame effect first.
/// </summary>
public enum ImpactFrameDurationMode
{
    UseEarliestLimit = 0,
    FramesOnly = 1,
    UnscaledSecondsOnly = 2
}

public enum PowerUpResourceType
{
    None = 0,
    Energy = 1,
    Health = 2,
    Shield = 3
}

public enum PowerUpChargeType
{
    Time = 0,
    EnemiesDestroyed = 1,
    WavesCleared = 2,
    RoomsCleared = 3,
    DamageInflicted = 4,
    DamageTaken = 5
}

/// <summary>
/// Selects the runtime direction source used when a Dash active module starts.
/// </summary>
public enum DashDirectionMode
{
    PlayerMovement = 0,
    OppositePlayerMovement = 1,
    PlayerLook = 2,
    OppositePlayerLook = 3
}

public enum PassiveToolKind
{
    ProjectileSize = 0,
    ElementalProjectiles = 1,
    PerfectCircle = 2,
    BouncingProjectiles = 3,
    SplittingProjectiles = 4,
    Explosion = 5,
    ElementalTrail = 6,
    Custom = 7,
    BulletTime = 8,
    LaserBeam = 9,
    DropAttraction = 10,
    ReturningProjectiles = 11
}

/// <summary>
/// Selects how a returning projectile reaches its owner after the outbound phase ends.
/// </summary>
public enum ProjectileReturnPathMode
{
    RetraceOutboundPath = 0,
    SeekPlayer = 1
}

/// <summary>
/// Selects which active trigger can begin returning-projectile travel.
/// </summary>
public enum ProjectileReturnStartMode
{
    AutomaticDelay = 0,
    ActivationTap = 1,
    ResourceDrain = 2,
    ActivationTapOrResourceDrain = 3,
    AutomaticDelayOrActivationTapOrResourceDrain = 4
}

/// <summary>
/// Selects how live returning projectiles react when their unprotected owning active power-up is stolen.
/// </summary>
public enum ProjectileStolenOwnershipPolicy
{
    Despawn = 0,
    PreserveAndReconnect = 1
}

/// <summary>
/// Centralizes trigger capabilities shared by authoring, scaling, activation, and projectile simulation.
/// </summary>
public static class ProjectileReturnStartModeUtility
{
    #region Methods

    /// <summary>
    /// Resolves whether a mode supports an additional active-input recall tap.
    /// </summary>
    /// <param name="mode">Return trigger mode to inspect.</param>
    /// <returns>True when active input can request return.</returns>
    public static bool UsesActivationTap(ProjectileReturnStartMode mode)
    {
        switch (mode)
        {
            case ProjectileReturnStartMode.ActivationTap:
            case ProjectileReturnStartMode.ActivationTapOrResourceDrain:
            case ProjectileReturnStartMode.AutomaticDelayOrActivationTapOrResourceDrain:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves whether a mode continuously consumes the owning Resource Gate resource while its projectile is outside.
    /// </summary>
    /// <param name="mode">Return trigger mode to inspect.</param>
    /// <returns>True when continuous resource drain can request return.</returns>
    public static bool UsesResourceDrain(ProjectileReturnStartMode mode)
    {
        switch (mode)
        {
            case ProjectileReturnStartMode.ResourceDrain:
            case ProjectileReturnStartMode.ActivationTapOrResourceDrain:
            case ProjectileReturnStartMode.AutomaticDelayOrActivationTapOrResourceDrain:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves whether endpoint suspension has a maximum authored duration.
    /// </summary>
    /// <param name="mode">Return trigger mode to inspect.</param>
    /// <returns>True when Return Delay remains an automatic return trigger.</returns>
    public static bool UsesAutomaticDelay(ProjectileReturnStartMode mode)
    {
        return mode == ProjectileReturnStartMode.AutomaticDelay ||
               mode == ProjectileReturnStartMode.AutomaticDelayOrActivationTapOrResourceDrain;
    }

    /// <summary>
    /// Resolves whether endpoint suspension waits indefinitely for an external recall request.
    /// </summary>
    /// <param name="mode">Return trigger mode to inspect.</param>
    /// <returns>True when no automatic endpoint delay can complete the return trigger.</returns>
    public static bool WaitsForExternalRecall(ProjectileReturnStartMode mode)
    {
        return !UsesAutomaticDelay(mode);
    }

    #endregion
}

/// <summary>
/// Selects the local axis used by flight spin and turnaround rotation.
/// </summary>
public enum ProjectileReturnRotationAxis
{
    Vertical = 0,
    Horizontal = 1
}

/// <summary>
/// Selects whether enemy impacts may terminate outbound travel after natural penetration is exhausted.
/// </summary>
public enum ProjectileOutboundHitPolicy
{
    NaturalPenetration = 0,
    CompleteOutboundTravel = 1,
    LimitedAdditionalHits = 2
}

/// <summary>
/// Selects whether return travel ignores penetration limits or consumes an additional hit budget.
/// </summary>
public enum ProjectileReturnHitPolicy
{
    CompleteReturn = 0,
    LimitedAdditionalHits = 1
}

/// <summary>
/// Selects one optional upper-body hold-charge clip from the active animation bindings preset.
/// </summary>
public enum PlayerChargeAnimationClipSlot
{
    None = 0,
    Primary = 1,
    Secondary = 2
}

/// <summary>
/// Selects one optional upper-body hold-charge release clip from the active animation bindings preset.
/// </summary>
public enum PlayerReleaseAnimationClipSlot
{
    None = 0,
    Primary = 1,
    Secondary = 2
}

public enum ProjectileOrbitPathMode
{
    Circle = 0,
    GoldenSpiral = 1
}

/// <summary>
/// Selects how one orbital projection resolves its desired angle around the player.
/// </summary>
public enum OrbitalProjectionMotionMode
{
    StaticOffset = 0,
    IndependentOrbit = 1,
    FollowPlayerLook = 2
}

/// <summary>
/// Selects how a full-circle independent orbital projection reacts to cone-bounce projections on the same orbit ring.
/// </summary>
public enum OrbitalProjectionFullOrbitConeResponse
{
    IgnoreCones = 0,
    BounceInComplementaryCone = 1
}

/// <summary>
/// Selects how a newly acquired orbital projection module coexists with already active projections.
/// </summary>
public enum OrbitalProjectionAcquisitionPolicy
{
    Additive = 0,
    ReplaceMatchingPowerUp = 1,
    ReplaceAllOrbitalProjections = 2
}

public enum ElementType
{
    Fire = 0,
    Ice = 1,
    Poison = 2,
    Custom = 3
}

public enum ElementalEffectKind
{
    Dots = 0,
    Impediment = 1
}

public enum ElementalProcMode
{
    ThresholdOnly = 0,
    ProgressiveUntilThreshold = 1
}

public enum ElementalProcReapplyMode
{
    AccumulateStacks = 0,
    RefreshActiveProc = 1,
    IgnoreWhileProcActive = 2
}

public enum PassiveExplosionTriggerMode
{
    Periodic = 0,
    OnPlayerDamaged = 1,
    OnEnemyKilled = 2
}

public enum ProjectileSplitDirectionMode
{
    Uniform = 0,
    CustomAngles = 1
}

public enum ProjectileSplitTriggerMode
{
    OnEnemyKilled = 0,
    OnEnemyHit = 1,
    OnProjectileDespawn = 2
}

public enum SpawnOffsetOrientationMode
{
    PlayerForward = 0,
    PlayerLookDirection = 1,
    WorldForward = 2
}

/// <summary>
/// Selects whether a deployed bomb moves away from its actual spawned position relative to the player, or back toward the player after spawning.
/// </summary>
public enum BombVelocityDirectionMode
{
    AwayFromPlayer = 0,
    TowardPlayer = 1
}

public enum PowerUpHealApplicationMode
{
    Instant = 0,
    OverTime = 1
}

public enum PowerUpHealStackPolicy
{
    Refresh = 0,
    Additive = 1,
    IgnoreIfActive = 2
}
