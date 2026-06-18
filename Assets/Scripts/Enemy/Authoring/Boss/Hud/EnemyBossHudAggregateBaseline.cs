/// <summary>
/// Tracks stable aggregate max values for a boss HUD encounter so bars do not refill when one boss leaves the active set.
/// </summary>
internal struct EnemyBossHudAggregateBaseline
{
    #region Fields
    private float healthMax;
    private float shieldMax;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the health denominator for the current aggregate, preserving the highest observed active-boss max.
    /// </summary>
    /// <param name="currentMaxHealth">Current summed max health from active boss HUD entities.</param>
    /// <returns>Stable non-decreasing max health used as the bar denominator.</returns>
    public float ResolveHealthMax(float currentMaxHealth)
    {
        if (currentMaxHealth > healthMax)
            healthMax = currentMaxHealth;

        return healthMax;
    }

    /// <summary>
    /// Resolves the shield denominator for the current aggregate, preserving the highest observed active-boss max.
    /// </summary>
    /// <param name="currentMaxShield">Current summed max shield from active boss HUD entities.</param>
    /// <returns>Stable non-decreasing max shield used as the bar denominator.</returns>
    public float ResolveShieldMax(float currentMaxShield)
    {
        if (currentMaxShield > shieldMax)
            shieldMax = currentMaxShield;

        return shieldMax;
    }

    /// <summary>
    /// Clears encounter baseline data when no boss HUD entity remains active.
    /// </summary>
    public void Reset()
    {
        healthMax = 0f;
        shieldMax = 0f;
    }
    #endregion

    #endregion
}
