using System;
using UnityEngine;

[Serializable]
public sealed class BombToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Bomb")]
    [Tooltip("Prefab spawned when this bomb tool is activated.")]
    [SerializeField] private GameObject bombPrefab;

    [Tooltip("Local-space spawn offset from player origin, rotated by player rotation at activation time.")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, 1.2f);

    [Tooltip("Reference used to orient the spawn offset. PlayerForward uses player transform, PlayerLookDirection uses runtime look vector, WorldForward ignores player rotation.")]
    [SerializeField] private SpawnOffsetOrientationMode spawnOffsetOrientation = SpawnOffsetOrientationMode.PlayerForward;

    [Tooltip("Initial planar speed applied to the bomb when deployed.")]
    [SerializeField] private float deploySpeed = 4.5f;

    [Tooltip("Direction applied to Deploy Speed after spawning. Away From Player follows the actual planar vector from the player to the spawned bomb; Toward Player inverts it.")]
    [SerializeField] private BombVelocityDirectionMode velocityDirection = BombVelocityDirectionMode.AwayFromPlayer;

    [Tooltip("Collision radius used for bomb wall interaction.")]
    [SerializeField] private float collisionRadius = 0.18f;

    [Tooltip("When enabled, the bomb bounces on wall impact instead of stopping.")]
    [SerializeField] private bool bounceOnWalls = true;

    [Tooltip("Velocity multiplier applied after each wall bounce. Range: 0 to 1.")]
    [SerializeField] private float bounceDamping = 0.65f;

    [Tooltip("Linear speed damping applied every second while the bomb moves.")]
    [SerializeField] private float linearDampingPerSecond = 1.2f;

    [Tooltip("Fuse duration in seconds before the bomb explodes.")]
    [SerializeField] private float fuseSeconds = 1.25f;

    [Tooltip("Explosion radius applied when the bomb detonates.")]
    [SerializeField] private float radius = 7f;

    [Tooltip("When disabled, damage payload is not applied by this spawned object.")]
    [SerializeField] private bool enableDamagePayload = true;

    [Tooltip("Damage dealt to enemies inside the explosion radius.")]
    [SerializeField] private float damage = 120f;

    [Tooltip("When enabled, all enemies in radius are affected by the explosion.")]
    [SerializeField] private bool affectAllEnemiesInRadius = true;

    [Header("Explosion VFX (Optional)")]
    [Tooltip("Optional VFX prefab spawned when the bomb detonates.")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Tooltip("When enabled, bomb explosion VFX scale is multiplied by explosion radius.")]
    [SerializeField] private bool scaleVfxToRadius = true;

    [Tooltip("Additional scale multiplier applied to bomb explosion VFX.")]
    [SerializeField] private float vfxScaleMultiplier = 1f;
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

    public Vector3 SpawnOffset
    {
        get
        {
            return spawnOffset;
        }
    }

    public SpawnOffsetOrientationMode SpawnOffsetOrientation
    {
        get
        {
            return spawnOffsetOrientation;
        }
    }

    public float DeploySpeed
    {
        get
        {
            return deploySpeed;
        }
    }

    public BombVelocityDirectionMode VelocityDirection
    {
        get
        {
            return velocityDirection;
        }
    }

    public float CollisionRadius
    {
        get
        {
            return collisionRadius;
        }
    }

    public bool BounceOnWalls
    {
        get
        {
            return bounceOnWalls;
        }
    }

    public float BounceDamping
    {
        get
        {
            return bounceDamping;
        }
    }

    public float LinearDampingPerSecond
    {
        get
        {
            return linearDampingPerSecond;
        }
    }

    public float FuseSeconds
    {
        get
        {
            return fuseSeconds;
        }
    }

    public float Radius
    {
        get
        {
            return radius;
        }
    }

    public bool EnableDamagePayload
    {
        get
        {
            return enableDamagePayload;
        }
    }

    public float Damage
    {
        get
        {
            return damage;
        }
    }

    public bool AffectAllEnemiesInRadius
    {
        get
        {
            return affectAllEnemiesInRadius;
        }
    }

    public GameObject ExplosionVfxPrefab
    {
        get
        {
            return explosionVfxPrefab;
        }
    }

    public bool ScaleVfxToRadius
    {
        get
        {
            return scaleVfxToRadius;
        }
    }

    public float VfxScaleMultiplier
    {
        get
        {
            return vfxScaleMultiplier;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (float.IsNaN(spawnOffset.x) ||
            float.IsNaN(spawnOffset.y) ||
            float.IsNaN(spawnOffset.z) ||
            float.IsInfinity(spawnOffset.x) ||
            float.IsInfinity(spawnOffset.y) ||
            float.IsInfinity(spawnOffset.z))
        {
            spawnOffset = new Vector3(0f, 0f, 1.2f);
        }

        if (deploySpeed < 0f)
            deploySpeed = 0f;

        if (collisionRadius < 0.01f)
            collisionRadius = 0.01f;

        if (bounceDamping < 0f)
            bounceDamping = 0f;

        if (bounceDamping > 1f)
            bounceDamping = 1f;

        if (linearDampingPerSecond < 0f)
            linearDampingPerSecond = 0f;

        if (fuseSeconds < 0.05f)
            fuseSeconds = 0.05f;

        if (radius < 0.1f)
            radius = 0.1f;

        if (damage < 0f)
            damage = 0f;

        if (vfxScaleMultiplier < 0.01f)
            vfxScaleMultiplier = 0.01f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores authoring data for Dash active modules and legacy Dash tools.
/// </summary>
[Serializable]
public sealed class DashToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Dash")]
    [Tooltip("Distance covered by the dash movement.")]
    [SerializeField] private float distance = 6f;

    [Tooltip("Reference direction used when the dash starts.")]
    [SerializeField] private DashDirectionMode directionMode = DashDirectionMode.PlayerMovement;

    [Tooltip("Duration in seconds used to complete the dash movement.")]
    [SerializeField] private float duration = 0.18f;

    [Tooltip("Seconds used to blend from zero dash speed to peak dash speed while preserving the authored travel distance.")]
    [SerializeField] private float speedTransitionInSeconds = 0.06f;

    [Tooltip("Seconds used to blend from peak dash speed back to zero dash speed while preserving the authored travel distance.")]
    [SerializeField] private float speedTransitionOutSeconds = 0.08f;

    [Tooltip("Wall bounce intensity applied when the dash hits walls. 0 stops into the wall, 1 preserves the reflected dash speed.")]
    [SerializeField] private float wallBounceIntensity = 0.65f;

    [Tooltip("When enabled, the player ignores damage during the dash.")]
    [SerializeField] private bool grantsInvulnerability = true;

    [Tooltip("Extra invulnerability time after dash end.")]
    [SerializeField] private float invulnerabilityExtraTime = 0.1f;
    #endregion

    #endregion

    #region Properties
    public float Distance
    {
        get
        {
            return distance;
        }
    }

    public float Duration
    {
        get
        {
            return duration;
        }
    }

    public DashDirectionMode DirectionMode
    {
        get
        {
            return directionMode;
        }
    }

    public bool GrantsInvulnerability
    {
        get
        {
            return grantsInvulnerability;
        }
    }

    public float SpeedTransitionInSeconds
    {
        get
        {
            return speedTransitionInSeconds;
        }
    }

    public float SpeedTransitionOutSeconds
    {
        get
        {
            return speedTransitionOutSeconds;
        }
    }

    public float WallBounceIntensity
    {
        get
        {
            return wallBounceIntensity;
        }
    }

    public float InvulnerabilityExtraTime
    {
        get
        {
            return invulnerabilityExtraTime;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (distance < 0f)
            distance = 0f;

        if (duration < 0.01f)
            duration = 0.01f;

        if (speedTransitionInSeconds < 0f)
            speedTransitionInSeconds = 0f;

        if (speedTransitionOutSeconds < 0f)
            speedTransitionOutSeconds = 0f;

        if (invulnerabilityExtraTime < 0f)
            invulnerabilityExtraTime = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class BulletTimeToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Bullet Time")]
    [Tooltip("Duration in seconds while enemy simulation is slowed.")]
    [SerializeField] private float duration = 3f;

    [Tooltip("Enemy slowdown percentage while Bullet Time is active. 0 means no slowdown, 100 means full stop.")]
    [SerializeField] private float enemySlowPercent = 40f;

    [Tooltip("Seconds used to blend the slow effect in and out whenever Bullet Time changes state.")]
    [SerializeField] private float transitionTimeSeconds = 0.2f;
    #endregion

    #endregion

    #region Properties
    public float Duration
    {
        get
        {
            return duration;
        }
    }

    public float EnemySlowPercent
    {
        get
        {
            return enemySlowPercent;
        }
    }

    public float TransitionTimeSeconds
    {
        get
        {
            return transitionTimeSeconds;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (duration < 0.05f)
            duration = 0.05f;

        if (enemySlowPercent < 0f)
            enemySlowPercent = 0f;

        if (enemySlowPercent > 100f)
            enemySlowPercent = 100f;

        if (transitionTimeSeconds < 0f)
            transitionTimeSeconds = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class ActiveToolDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Common metadata and drop data for this active tool.")]
    [SerializeField] private PowerUpCommonData commonData = new PowerUpCommonData();

    [Tooltip("Active tool behavior type.")]
    [SerializeField] private ActiveToolKind toolKind = ActiveToolKind.Bomb;

    [Header("Resources")]
    [Tooltip("Maximum energy reserve for this tool. Set 0 for tools that do not use energy.")]
    [SerializeField] private float maximumEnergy = 100f;

    [Tooltip("When enabled, activation toggles ON/OFF state instead of one-shot behavior.")]
    [SerializeField] private bool toggleable;

    [Tooltip("Resource consumed on activation.")]
    [SerializeField] private PowerUpResourceType activationResource = PowerUpResourceType.Energy;

    [Tooltip("Amount of ActivationResource consumed when tool is activated.")]
    [SerializeField] private float activationCost = 25f;

    [Tooltip("Resource consumed each second while toggleable tool remains active.")]
    [SerializeField] private PowerUpResourceType maintenanceResource = PowerUpResourceType.Energy;

    [Tooltip("Amount consumed per second while toggleable tool remains active.")]
    [SerializeField] private float maintenanceCostPerSecond;

    [Tooltip("Event type that grants recharge to this tool.")]
    [SerializeField] private PowerUpChargeType chargeType = PowerUpChargeType.EnemiesDestroyed;

    [Tooltip("Recharge amount granted for each charge event.")]
    [SerializeField] private float chargePerTrigger = 10f;

    [Tooltip("Minimum energy percentage required for activation. 0 disables this gate.")]
    [SerializeField] private float minimumActivationEnergyPercent;

    [Tooltip("When enabled, this tool cannot be replaced from slots.")]
    [SerializeField] private bool unreplaceable;

    [Header("Tool Specific")]
    [Tooltip("Bomb-specific payload data.")]
    [SerializeField] private BombToolData bombData = new BombToolData();

    [Tooltip("Dash-specific payload data.")]
    [SerializeField] private DashToolData dashData = new DashToolData();

    [Tooltip("Bullet Time-specific payload data.")]
    [SerializeField] private BulletTimeToolData bulletTimeData = new BulletTimeToolData();
    #endregion

    #endregion

    #region Properties
    public PowerUpCommonData CommonData
    {
        get
        {
            return commonData;
        }
    }

    public ActiveToolKind ToolKind
    {
        get
        {
            return toolKind;
        }
    }

    public float MaximumEnergy
    {
        get
        {
            return maximumEnergy;
        }
    }

    public bool Toggleable
    {
        get
        {
            return toggleable;
        }
    }

    public PowerUpResourceType ActivationResource
    {
        get
        {
            return activationResource;
        }
    }

    public float ActivationCost
    {
        get
        {
            return activationCost;
        }
    }

    public PowerUpResourceType MaintenanceResource
    {
        get
        {
            return maintenanceResource;
        }
    }

    public float MaintenanceCostPerSecond
    {
        get
        {
            return maintenanceCostPerSecond;
        }
    }

    public PowerUpChargeType ChargeType
    {
        get
        {
            return chargeType;
        }
    }

    public float ChargePerTrigger
    {
        get
        {
            return chargePerTrigger;
        }
    }

    public float MinimumActivationEnergyPercent
    {
        get
        {
            return minimumActivationEnergyPercent;
        }
    }

    public bool Unreplaceable
    {
        get
        {
            return unreplaceable;
        }
    }

    public BombToolData BombData
    {
        get
        {
            return bombData;
        }
    }

    public DashToolData DashData
    {
        get
        {
            return dashData;
        }
    }

    public BulletTimeToolData BulletTimeData
    {
        get
        {
            return bulletTimeData;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (commonData == null)
            commonData = new PowerUpCommonData();

        commonData.Validate();

        if (toolKind == ActiveToolKind.Custom)
            toolKind = ActiveToolKind.Bomb;

        if (maximumEnergy < 0f)
            maximumEnergy = 0f;

        if (activationCost < 0f)
            activationCost = 0f;

        if (maintenanceCostPerSecond < 0f)
            maintenanceCostPerSecond = 0f;

        if (chargePerTrigger < 0f)
            chargePerTrigger = 0f;

        if (minimumActivationEnergyPercent < 0f)
            minimumActivationEnergyPercent = 0f;

        if (minimumActivationEnergyPercent > 100f)
            minimumActivationEnergyPercent = 100f;

        if (maximumEnergy <= 0f)
            minimumActivationEnergyPercent = 0f;

        if (bombData == null)
            bombData = new BombToolData();

        if (dashData == null)
            dashData = new DashToolData();

        if (bulletTimeData == null)
            bulletTimeData = new BulletTimeToolData();

        bombData.Validate();
        dashData.Validate();
        bulletTimeData.Validate();
    }
    #endregion

    #endregion
}
