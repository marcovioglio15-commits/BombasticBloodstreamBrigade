using System;
using UnityEngine;

/// <summary>
/// Contains the runtime prefab payload used by Bombardier modules.
/// </summary>
[Serializable]
public sealed class EnemyBombardierRuntimeBombPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Bomb prefab instantiated for each Bombardier launch. The prefab should be a lightweight visual entity without EnemyAuthoring or PlayerAuthoring.")]
    [SerializeField] private GameObject bombPrefab;

    [Tooltip("Optional one-shot VFX prefab spawned at the bomb landing position when the explosion is resolved.")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Tooltip("When enabled, the explosion VFX uniform scale is multiplied by the authored Damage Radius.")]
    [SerializeField] private bool scaleExplosionVfxToDamageRadius = true;

    [Tooltip("Additional uniform scale multiplier applied to the explosion VFX after optional radius scaling.")]
    [SerializeField] private float explosionVfxScaleMultiplier = 1f;
    #endregion

    #endregion

    #region Properties
    public GameObject BombPrefab
    {
        get
        {
            return bombPrefab;
        }
    }

    public GameObject ExplosionVfxPrefab
    {
        get
        {
            return explosionVfxPrefab;
        }
    }

    public bool ScaleExplosionVfxToDamageRadius
    {
        get
        {
            return scaleExplosionVfxToDamageRadius;
        }
    }

    public float ExplosionVfxScaleMultiplier
    {
        get
        {
            return explosionVfxScaleMultiplier;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Keeps the runtime bomb payload structurally valid without snapping authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains optional landing warning ring settings for Bombardier bombs.
/// </summary>
[Serializable]
public sealed class EnemyBombardierLandingWarningPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, a warning ring is shown at the predicted bomb landing point before impact.")]
    [SerializeField] private bool enableLandingWarning = true;

    [Tooltip("Seconds before impact when the landing warning should appear.")]
    [SerializeField] private float warningLeadTimeSeconds = 0.75f;

    [Tooltip("Multiplier applied to Damage Radius to resolve warning ring radius.")]
    [SerializeField] private float warningRadiusScale = 1f;

    [Tooltip("Line width used by the warning ring.")]
    [SerializeField] private float ringWidth = 0.08f;

    [Tooltip("Vertical offset added to the landing position when rendering the warning ring.")]
    [SerializeField] private float heightOffset = 0.04f;

    [Tooltip("Maximum opacity reached by the warning ring.")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumAlpha = 0.85f;

    [Tooltip("Seconds spent fading the warning ring out after the bomb impacts.")]
    [SerializeField] private float fadeOutSeconds = 0.12f;

    [Tooltip("Warning ring color before opacity animation is applied.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color color = new Color(1f, 0.35f, 0.05f, 1f);
    #endregion

    #endregion

    #region Properties
    public bool EnableLandingWarning
    {
        get
        {
            return enableLandingWarning;
        }
    }

    public float WarningLeadTimeSeconds
    {
        get
        {
            return warningLeadTimeSeconds;
        }
    }

    public float WarningRadiusScale
    {
        get
        {
            return warningRadiusScale;
        }
    }

    public float RingWidth
    {
        get
        {
            return ringWidth;
        }
    }

    public float HeightOffset
    {
        get
        {
            return heightOffset;
        }
    }

    public float MaximumAlpha
    {
        get
        {
            return maximumAlpha;
        }
    }

    public float FadeOutSeconds
    {
        get
        {
            return fadeOutSeconds;
        }
    }

    public Color Color
    {
        get
        {
            return color;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Keeps the landing warning payload structurally valid without snapping authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Groups Bombardier cadence, targeting, trajectory, damage and runtime payloads.
/// </summary>
[Serializable]
public sealed class EnemyBombardierModuleData
{
    #region Fields

    #region Serialized Fields
    [Header("Cadence")]
    [Tooltip("Aim mode used when deciding landing targets during a burst.")]
    [SerializeField] private EnemyShooterAimPolicy aimPolicy = EnemyShooterAimPolicy.LockOnFireStart;

    [Tooltip("Movement policy applied while this module is preparing and launching bombs.")]
    [SerializeField] private EnemyShooterMovementPolicy movementPolicy = EnemyShooterMovementPolicy.KeepMoving;

    [Tooltip("Seconds between burst starts for this Bombardier module.")]
    [SerializeField] private float fireInterval = 2f;

    [Tooltip("Launch groups emitted for each activation. 1 means a single launch group.")]
    [SerializeField] private int burstCount = 1;

    [Tooltip("Extra aim windup in seconds applied before the first launch group of each burst. 0 launches immediately.")]
    [SerializeField] private float aimWindupSeconds = 0.45f;

    [Tooltip("Minimum seconds this enemy must remain stopped before the first launch when Movement Policy is Stop While Aiming.")]
    [SerializeField] private float preLaunchStopSeconds = 0.35f;

    [Tooltip("Minimum seconds this enemy remains stopped after the final launch when Movement Policy is Stop While Aiming.")]
    [SerializeField] private float postLaunchStopSeconds = 0.15f;

    [Tooltip("Seconds between launch groups inside the same burst.")]
    [SerializeField] private float intraBurstDelay = 0.15f;

    [Header("Targeting")]
    [Tooltip("Landing target mode used while the player satisfies this Weapon Interaction reach gates.")]
    [SerializeField] private EnemyBombardierTargetingMode inReachTargetingMode = EnemyBombardierTargetingMode.Player;

    [Tooltip("Landing target mode used while the player does not satisfy this Weapon Interaction reach gates.")]
    [SerializeField] private EnemyBombardierTargetingMode outOfReachTargetingMode = EnemyBombardierTargetingMode.Disabled;

    [Tooltip("How each launch group distributes its bombs around the resolved landing target.")]
    [SerializeField] private EnemyBombardierLaunchPattern launchPattern = EnemyBombardierLaunchPattern.Cluster;

    [Tooltip("Bombs emitted by each launch group. Cluster launches can scatter them; Radial launches place them evenly around the target.")]
    [SerializeField] private int bombsPerLaunch = 1;

    [Tooltip("Random scatter radius used by Cluster launches when more than one bomb is emitted.")]
    [SerializeField] private float landingSpreadRadius = 1.25f;

    [Tooltip("Landing radius used by Radial launches when more than one bomb is emitted.")]
    [SerializeField] private float radialPatternRadius = 2f;

    [Tooltip("Minimum random distance from the selected random origin for Random Around Enemy and Random Around Player targeting.")]
    [SerializeField] private float randomMinimumDistance = 2f;

    [Tooltip("Maximum random distance from the selected random origin for Random Around Enemy and Random Around Player targeting.")]
    [SerializeField] private float randomMaximumDistance = 8f;

    [Header("Trajectory")]
    [Tooltip("Trajectory solver used to convert launch and landing positions into parabolic velocity.")]
    [SerializeField] private EnemyBombardierTrajectoryMode trajectoryMode = EnemyBombardierTrajectoryMode.FixedFlightTimeAndGravity;

    [Tooltip("Flight duration used when Trajectory Mode is Fixed Flight Time And Gravity.")]
    [SerializeField] private float flightDurationSeconds = 1.15f;

    [Tooltip("Downward acceleration used by Bombardier trajectory simulation.")]
    [SerializeField] private float gravity = 16f;

    [Tooltip("Extra apex height above the higher of launch and landing positions when Trajectory Mode is Fixed Apex Height.")]
    [SerializeField] private float apexHeight = 3f;

    [Tooltip("Vertical offset applied to the enemy position when resolving bomb launch position.")]
    [SerializeField] private float launchHeightOffset = 0.65f;

    [Tooltip("Vertical offset applied to the target position when resolving bomb landing position.")]
    [SerializeField] private float landingHeightOffset;

    [Header("Damage")]
    [Tooltip("Flat damage applied to the player when a bomb explosion overlaps them.")]
    [SerializeField] private float damage = 12f;

    [Tooltip("Explosion radius used for player overlap checks and warning radius scaling.")]
    [SerializeField] private float damageRadius = 1.75f;

    [Tooltip("Seconds between ground impact and explosion. 0 explodes immediately on impact.")]
    [SerializeField] private float impactExplosionDelaySeconds;

    [Tooltip("Uniform scale multiplier applied to each spawned bomb visual.")]
    [SerializeField] private float bombScaleMultiplier = 1f;

    [Header("Runtime")]
    [Tooltip("Runtime bomb prefab payload used by this module.")]
    [SerializeField] private EnemyBombardierRuntimeBombPayload runtimeBomb = new EnemyBombardierRuntimeBombPayload();

    [Tooltip("Optional landing warning payload shown before bomb impact.")]
    [SerializeField] private EnemyBombardierLandingWarningPayload landingWarning = new EnemyBombardierLandingWarningPayload();
    #endregion

    #endregion

    #region Properties
    public EnemyShooterAimPolicy AimPolicy
    {
        get
        {
            return aimPolicy;
        }
    }

    public EnemyShooterMovementPolicy MovementPolicy
    {
        get
        {
            return movementPolicy;
        }
    }

    public float FireInterval
    {
        get
        {
            return fireInterval;
        }
    }

    public int BurstCount
    {
        get
        {
            return burstCount;
        }
    }

    public float AimWindupSeconds
    {
        get
        {
            return aimWindupSeconds;
        }
    }

    public float PreLaunchStopSeconds
    {
        get
        {
            return preLaunchStopSeconds;
        }
    }

    public float PostLaunchStopSeconds
    {
        get
        {
            return postLaunchStopSeconds;
        }
    }

    public float IntraBurstDelay
    {
        get
        {
            return intraBurstDelay;
        }
    }

    public EnemyBombardierTargetingMode InReachTargetingMode
    {
        get
        {
            return inReachTargetingMode;
        }
    }

    public EnemyBombardierTargetingMode OutOfReachTargetingMode
    {
        get
        {
            return outOfReachTargetingMode;
        }
    }

    public EnemyBombardierLaunchPattern LaunchPattern
    {
        get
        {
            return launchPattern;
        }
    }

    public int BombsPerLaunch
    {
        get
        {
            return bombsPerLaunch;
        }
    }

    public float LandingSpreadRadius
    {
        get
        {
            return landingSpreadRadius;
        }
    }

    public float RadialPatternRadius
    {
        get
        {
            return radialPatternRadius;
        }
    }

    public float RandomMinimumDistance
    {
        get
        {
            return randomMinimumDistance;
        }
    }

    public float RandomMaximumDistance
    {
        get
        {
            return randomMaximumDistance;
        }
    }

    public EnemyBombardierTrajectoryMode TrajectoryMode
    {
        get
        {
            return trajectoryMode;
        }
    }

    public float FlightDurationSeconds
    {
        get
        {
            return flightDurationSeconds;
        }
    }

    public float Gravity
    {
        get
        {
            return gravity;
        }
    }

    public float ApexHeight
    {
        get
        {
            return apexHeight;
        }
    }

    public float LaunchHeightOffset
    {
        get
        {
            return launchHeightOffset;
        }
    }

    public float LandingHeightOffset
    {
        get
        {
            return landingHeightOffset;
        }
    }

    public float Damage
    {
        get
        {
            return damage;
        }
    }

    public float DamageRadius
    {
        get
        {
            return damageRadius;
        }
    }

    public float ImpactExplosionDelaySeconds
    {
        get
        {
            return impactExplosionDelaySeconds;
        }
    }

    public float BombScaleMultiplier
    {
        get
        {
            return bombScaleMultiplier;
        }
    }

    public EnemyBombardierRuntimeBombPayload RuntimeBomb
    {
        get
        {
            return runtimeBomb;
        }
    }

    public EnemyBombardierLandingWarningPayload LandingWarning
    {
        get
        {
            return landingWarning;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures Bombardier module nested payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
        if (runtimeBomb == null)
            runtimeBomb = new EnemyBombardierRuntimeBombPayload();

        if (landingWarning == null)
            landingWarning = new EnemyBombardierLandingWarningPayload();

        runtimeBomb.Validate();
        landingWarning.Validate();
    }
    #endregion

    #endregion
}
