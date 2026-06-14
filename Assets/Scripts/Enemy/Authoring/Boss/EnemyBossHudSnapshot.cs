using Unity.Entities;

/// <summary>
/// Stores one aggregated snapshot of all active boss HUD entities for a single presentation update.
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
    /// </summary>
    /// <param name="primaryEntity">Boss entity used for name, colors and offscreen projection.</param>
    /// <param name="primaryConfig">Boss HUD config used for name, colors and indicator settings.</param>
    /// <param name="currentHealth">Summed current health across active boss HUD entities.</param>
    /// <param name="maxHealth">Summed max health across active boss HUD entities.</param>
    /// <param name="currentShield">Summed current shield across active boss HUD entities.</param>
    /// <param name="maxShield">Summed max shield across active boss HUD entities.</param>
    /// <param name="bossCount">Number of active boss HUD entities included in the sums.</param>
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
