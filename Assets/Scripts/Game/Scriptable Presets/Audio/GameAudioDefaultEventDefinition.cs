/// <summary>
/// Immutable editor/runtime descriptor used to seed audio manager presets with every supported gameplay event.
/// </summary>
public readonly struct GameAudioDefaultEventDefinition
{
    #region Fields
    public readonly GameAudioEventId EventId;
    public readonly string EventCode;
    public readonly string DisplayName;
    public readonly string Description;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one default audio event descriptor.
    /// </summary>
    /// <param name="eventId">Stable gameplay event identifier.</param>
    /// <param name="eventCode">Production-facing event code shown in the tool.</param>
    /// <param name="displayName">Human-readable section label.</param>
    /// <param name="description">Short explanation of when this event is requested.</param>
    public GameAudioDefaultEventDefinition(GameAudioEventId eventId,
                                           string eventCode,
                                           string displayName,
                                           string description)
    {
        EventId = eventId;
        EventCode = eventCode;
        DisplayName = displayName;
        Description = description;
    }
    #endregion
}

/// <summary>
/// Central catalog of audio events that must exist in every GameAudioManagerPreset.
/// </summary>
public static class GameAudioDefaultEventDefinitions
{
    #region Fields
    private static readonly GameAudioDefaultEventDefinition[] definitions =
    {
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerShootProjectile,
                                            "MC_SFX_Shoot_Projectile_Player",
                                            "Player Projectile Shot",
                                            "Requested when the player emits one projectile shot batch."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.EnemyShootProjectile,
                                            "MC_SFX_Shoot_Projectile_Enemy",
                                            "Enemy Projectile Shot",
                                            "Requested when an enemy shooter module emits projectiles; rate-limited by default."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerShootLaserContinuous,
                                            "MC_SFX_Shoot_Laser_Continuous",
                                            "Laser Continuous",
                                            "Requested when the player's laser beam enters the active loop."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerShootLaserTick,
                                            "MC_SFX_Shoot_Laser_Tick",
                                            "Laser Tick",
                                            "Requested on authored laser damage ticks."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerShootCannon,
                                            "MC_SFX_Shoot_Cannon",
                                            "Charged Cannon Shot",
                                            "Requested when a charged shot is released successfully."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerHealthDamage,
                                            "MC_SFX_HealthDamage",
                                            "Player Health Damage",
                                            "Requested when accepted damage reaches the player's health bar."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerShieldDamage,
                                            "MC_SFX_ShieldDamage",
                                            "Player Shield Damage",
                                            "Requested when accepted damage is absorbed by the player's shield bar."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerHealthRecharge,
                                            "MC_SFX_HealthRecharge",
                                            "Player Health Recharge",
                                            "Requested when runtime healing restores player health."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerShieldRecharge,
                                            "MC_SFX_ShieldRecharge",
                                            "Player Shield Recharge",
                                            "Requested when runtime shield compensation restores player shield."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerLevelUp,
                                            "MC_SFX_LevelUp",
                                            "Level Up",
                                            "Requested after a level-up that does not open a milestone selection."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerLevelUpMilestone,
                                            "MC_SFX_LevelUpMilestone",
                                            "Milestone Level Up",
                                            "Requested after a level-up reaches a milestone."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.ActiveEnergyFull,
                                            "MC_SFX_Active_EnergyFull",
                                            "Active Energy Full",
                                            "Requested when an active tool reaches its energy requirement."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.ActiveCharge,
                                            "MC_SFX_Active_Charge",
                                            "Active Charge",
                                            "Requested when a charge-bar active tool starts charging."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.ActiveRelease,
                                            "MC_SFX_Active_Release",
                                            "Active Release",
                                            "Requested when a charge-bar active tool is released."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.ActiveDash,
                                            "MC_SFX_Active_Dash",
                                            "Active Dash",
                                            "Requested when a dash-type active tool starts dashing."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.ActiveThrow,
                                            "MC_SFX_Active_Throw",
                                            "Active Throw",
                                            "Requested when a throw/deploy-type active tool is used."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerDeath,
                                            "MC_SFX_Misc_Death",
                                            "Player Death",
                                            "Requested when the run outcome resolves to defeat."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerSpawn,
                                            "MC_SFX_Misc_Spawn",
                                            "Player Spawn",
                                            "Requested when player progression runtime state is initialized."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerVictory,
                                            "MC_SFX_Misc_Victory",
                                            "Player Victory",
                                            "Requested when the run outcome resolves to victory."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.BulletImpactEnemy,
                                            "MISC_SFX_BulletImpact_Enemy",
                                            "Bullet Impact Enemy",
                                            "Requested when a player projectile impacts an enemy."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.BulletImpactPlayer,
                                            "MISC_SFX_BulletImpact_Player",
                                            "Bullet Impact Player",
                                            "Requested when an enemy projectile impacts the player."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.PlayerLaserImpact,
                                            "MISC_SFX_LaserImpact",
                                            "Laser Impact",
                                            "Requested when the player's laser beam damages enemies on its tick cadence."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.ExplosionBomb,
                                            "MISC_SFX_Explosion_Bomb",
                                            "Bomb Explosion",
                                            "Requested when a bomb active tool explodes."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.ExplosionPassive,
                                            "MISC_SFX_Explosion_Passive",
                                            "Passive Explosion",
                                            "Requested when passive explosion power-up payloads resolve."),
        new GameAudioDefaultEventDefinition(GameAudioEventId.ExplosionEnemy,
                                            "MISC_SFX_Explosion_Enemy",
                                            "Enemy Explosion",
                                            "Requested when enemy-kill passive explosion payloads resolve.")
    };
    #endregion

    #region Properties
    public static GameAudioDefaultEventDefinition[] Definitions
    {
        get
        {
            return definitions;
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Resolves one default event descriptor by stable event identifier.
    /// </summary>
    /// <param name="eventId">Identifier to search.</param>
    /// <param name="definition">Output descriptor when found.</param>
    /// <returns>True when a matching descriptor exists.</returns>
    public static bool TryGetDefinition(GameAudioEventId eventId, out GameAudioDefaultEventDefinition definition)
    {
        for (int index = 0; index < definitions.Length; index++)
        {
            GameAudioDefaultEventDefinition candidate = definitions[index];

            if (candidate.EventId != eventId)
                continue;

            definition = candidate;
            return true;
        }

        definition = default;
        return false;
    }
    #endregion
}
