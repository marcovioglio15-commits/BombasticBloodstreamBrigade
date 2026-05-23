using System;

/// <summary>
/// Stores live runtime visibility flags shared by the debug canvas panel and the runtime gizmo renderers.
/// none.
/// </summary>
public static class RuntimeGizmoDebugState
{
    #region Constants
    private const bool DefaultPanelVisible = false;
    private const bool DefaultShowLabels = false;
    private const bool DefaultPlayerPickupRadiusEnabled = false;
    private const bool DefaultPlayerMoveVectorEnabled = false;
    private const bool DefaultPlayerLookDirectionEnabled = false;
    private const bool DefaultOrbitalProjectionCollisionRadiusEnabled = false;
    private const bool DefaultEnemyBodyRadiusEnabled = false;
    private const bool DefaultEnemyContactRadiusEnabled = false;
    private const bool DefaultEnemyAreaRadiusEnabled = false;
    private const bool DefaultEnemySeparationRadiusEnabled = false;
    private const bool DefaultEnemyWanderTargetEnabled = false;
    private const bool DefaultSpawnerSpawnRadiusEnabled = false;
    private const bool DefaultSpawnerDespawnRadiusEnabled = false;
    private const bool DefaultBombRadiusEnabled = false;
    private const bool DefaultBombVelocityEnabled = false;
    private const bool DefaultProjectileImpactRadiusEnabled = false;
    private const bool DefaultProjectileVelocityEnabled = false;
    private const bool DefaultProjectileRemainingRangeEnabled = false;
    #endregion

    #region Fields
    private static bool panelVisible;
    private static bool showLabels;
    private static bool playerPickupRadiusEnabled;
    private static bool playerMoveVectorEnabled;
    private static bool playerLookDirectionEnabled;
    private static bool orbitalProjectionCollisionRadiusEnabled;
    private static bool enemyBodyRadiusEnabled;
    private static bool enemyContactRadiusEnabled;
    private static bool enemyAreaRadiusEnabled;
    private static bool enemySeparationRadiusEnabled;
    private static bool enemyWanderTargetEnabled;
    private static bool spawnerSpawnRadiusEnabled;
    private static bool spawnerDespawnRadiusEnabled;
    private static bool bombRadiusEnabled;
    private static bool bombVelocityEnabled;
    private static bool projectileImpactRadiusEnabled;
    private static bool projectileVelocityEnabled;
    private static bool projectileRemainingRangeEnabled;
    #endregion

    #region Events
    public static event Action StateChanged;
    #endregion

    #region Constructor
    static RuntimeGizmoDebugState()
    {
        ApplyDefaults();
    }
    #endregion

    #region Properties
    public static bool PanelVisible
    {
        get
        {
            return panelVisible;
        }
        set
        {
            SetFlag(ref panelVisible, value);
        }
    }

    public static bool ShowLabels
    {
        get
        {
            return showLabels;
        }
        set
        {
            SetFlag(ref showLabels, value);
        }
    }

    public static bool PlayerPickupRadiusEnabled
    {
        get
        {
            return playerPickupRadiusEnabled;
        }
        set
        {
            SetFlag(ref playerPickupRadiusEnabled, value);
        }
    }

    public static bool PlayerMoveVectorEnabled
    {
        get
        {
            return playerMoveVectorEnabled;
        }
        set
        {
            SetFlag(ref playerMoveVectorEnabled, value);
        }
    }

    public static bool PlayerLookDirectionEnabled
    {
        get
        {
            return playerLookDirectionEnabled;
        }
        set
        {
            SetFlag(ref playerLookDirectionEnabled, value);
        }
    }

    public static bool OrbitalProjectionCollisionRadiusEnabled
    {
        get
        {
            return orbitalProjectionCollisionRadiusEnabled;
        }
        set
        {
            SetFlag(ref orbitalProjectionCollisionRadiusEnabled, value);
        }
    }

    public static bool EnemyBodyRadiusEnabled
    {
        get
        {
            return enemyBodyRadiusEnabled;
        }
        set
        {
            SetFlag(ref enemyBodyRadiusEnabled, value);
        }
    }

    public static bool EnemyContactRadiusEnabled
    {
        get
        {
            return enemyContactRadiusEnabled;
        }
        set
        {
            SetFlag(ref enemyContactRadiusEnabled, value);
        }
    }

    public static bool EnemyAreaRadiusEnabled
    {
        get
        {
            return enemyAreaRadiusEnabled;
        }
        set
        {
            SetFlag(ref enemyAreaRadiusEnabled, value);
        }
    }

    public static bool EnemySeparationRadiusEnabled
    {
        get
        {
            return enemySeparationRadiusEnabled;
        }
        set
        {
            SetFlag(ref enemySeparationRadiusEnabled, value);
        }
    }

    public static bool EnemyWanderTargetEnabled
    {
        get
        {
            return enemyWanderTargetEnabled;
        }
        set
        {
            SetFlag(ref enemyWanderTargetEnabled, value);
        }
    }

    public static bool SpawnerSpawnRadiusEnabled
    {
        get
        {
            return spawnerSpawnRadiusEnabled;
        }
        set
        {
            SetFlag(ref spawnerSpawnRadiusEnabled, value);
        }
    }

    public static bool SpawnerDespawnRadiusEnabled
    {
        get
        {
            return spawnerDespawnRadiusEnabled;
        }
        set
        {
            SetFlag(ref spawnerDespawnRadiusEnabled, value);
        }
    }

    public static bool BombRadiusEnabled
    {
        get
        {
            return bombRadiusEnabled;
        }
        set
        {
            SetFlag(ref bombRadiusEnabled, value);
        }
    }

    public static bool BombVelocityEnabled
    {
        get
        {
            return bombVelocityEnabled;
        }
        set
        {
            SetFlag(ref bombVelocityEnabled, value);
        }
    }

    public static bool ProjectileImpactRadiusEnabled
    {
        get
        {
            return projectileImpactRadiusEnabled;
        }
        set
        {
            SetFlag(ref projectileImpactRadiusEnabled, value);
        }
    }

    public static bool ProjectileVelocityEnabled
    {
        get
        {
            return projectileVelocityEnabled;
        }
        set
        {
            SetFlag(ref projectileVelocityEnabled, value);
        }
    }

    public static bool ProjectileRemainingRangeEnabled
    {
        get
        {
            return projectileRemainingRangeEnabled;
        }
        set
        {
            SetFlag(ref projectileRemainingRangeEnabled, value);
        }
    }

    public static bool AnyRuntimeGizmoEnabled
    {
        get
        {
            if (playerPickupRadiusEnabled ||
                playerMoveVectorEnabled ||
                playerLookDirectionEnabled ||
                orbitalProjectionCollisionRadiusEnabled)
            {
                return true;
            }

            if (enemyBodyRadiusEnabled ||
                enemyContactRadiusEnabled ||
                enemyAreaRadiusEnabled ||
                enemySeparationRadiusEnabled ||
                enemyWanderTargetEnabled)
            {
                return true;
            }

            if (spawnerSpawnRadiusEnabled || spawnerDespawnRadiusEnabled)
                return true;

            if (bombRadiusEnabled || bombVelocityEnabled)
                return true;

            if (projectileImpactRadiusEnabled ||
                projectileVelocityEnabled ||
                projectileRemainingRangeEnabled)
            {
                return true;
            }

            return false;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Restores the whole debug state to project defaults, usually on domain reload or when the user wants a clean baseline.
    /// none.
    /// </summary>
    public static void ResetToDefaults()
    {
        ApplyDefaults();
        RaiseStateChanged();
    }
    #endregion

    #region Private Methods
    private static void ApplyDefaults()
    {
        panelVisible = DefaultPanelVisible;
        showLabels = DefaultShowLabels;
        playerPickupRadiusEnabled = DefaultPlayerPickupRadiusEnabled;
        playerMoveVectorEnabled = DefaultPlayerMoveVectorEnabled;
        playerLookDirectionEnabled = DefaultPlayerLookDirectionEnabled;
        orbitalProjectionCollisionRadiusEnabled = DefaultOrbitalProjectionCollisionRadiusEnabled;
        enemyBodyRadiusEnabled = DefaultEnemyBodyRadiusEnabled;
        enemyContactRadiusEnabled = DefaultEnemyContactRadiusEnabled;
        enemyAreaRadiusEnabled = DefaultEnemyAreaRadiusEnabled;
        enemySeparationRadiusEnabled = DefaultEnemySeparationRadiusEnabled;
        enemyWanderTargetEnabled = DefaultEnemyWanderTargetEnabled;
        spawnerSpawnRadiusEnabled = DefaultSpawnerSpawnRadiusEnabled;
        spawnerDespawnRadiusEnabled = DefaultSpawnerDespawnRadiusEnabled;
        bombRadiusEnabled = DefaultBombRadiusEnabled;
        bombVelocityEnabled = DefaultBombVelocityEnabled;
        projectileImpactRadiusEnabled = DefaultProjectileImpactRadiusEnabled;
        projectileVelocityEnabled = DefaultProjectileVelocityEnabled;
        projectileRemainingRangeEnabled = DefaultProjectileRemainingRangeEnabled;
    }

    private static void SetFlag(ref bool target, bool value)
    {
        if (target == value)
            return;

        target = value;
        RaiseStateChanged();
    }

    private static void RaiseStateChanged()
    {
        Action stateChanged = StateChanged;

        if (stateChanged == null)
            return;

        stateChanged.Invoke();
    }
    #endregion

    #endregion
}
