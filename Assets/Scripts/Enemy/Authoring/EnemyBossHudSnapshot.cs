using Unity.Entities;

/// <summary>
/// Stores one aggregated snapshot of all active boss HUD entities for a single presentation update.
/// /params None.
/// /returns None.
/// </summary>
internal readonly struct EnemyBossHudSnapshot
{
    #region Fields
    public readonly Entity PrimaryEntity;
    public readonly EnemyBossHudConfig PrimaryConfig;
    public readonly float CurrentHealth;
    public readonly float MaxHealth;
    public readonly float CurrentShield;
    public readonly float MaxShield;
    public readonly int BossCount;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one immutable boss HUD aggregation snapshot.
    /// /params primaryEntity Boss entity used for name, colors and offscreen projection.
    /// /params primaryConfig Boss HUD config used for name, colors and indicator settings.
    /// /params currentHealth Summed current health across active boss HUD entities.
    /// /params maxHealth Summed max health across active boss HUD entities.
    /// /params currentShield Summed current shield across active boss HUD entities.
    /// /params maxShield Summed max shield across active boss HUD entities.
    /// /params bossCount Number of active boss HUD entities included in the sums.
    /// /returns None.
    /// </summary>
    public EnemyBossHudSnapshot(Entity primaryEntity,
                                in EnemyBossHudConfig primaryConfig,
                                float currentHealth,
                                float maxHealth,
                                float currentShield,
                                float maxShield,
                                int bossCount)
    {
        PrimaryEntity = primaryEntity;
        PrimaryConfig = primaryConfig;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        CurrentShield = currentShield;
        MaxShield = maxShield;
        BossCount = bossCount;
    }
    #endregion

    #endregion
}
