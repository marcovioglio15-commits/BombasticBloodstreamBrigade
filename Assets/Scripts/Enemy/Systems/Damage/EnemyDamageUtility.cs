using Unity.Mathematics;

/// <summary>
/// Provides shared enemy damage helpers used by combat and elemental systems.
/// </summary>
public static class EnemyDamageUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies incoming flat damage to enemy shield and health and reports whether any survivability value changed.
    /// </summary>
    /// <param name="enemyHealth">Mutable enemy health and shield state that receives the damage.</param>
    /// <param name="incomingDamage">Raw incoming damage value. Negative values are treated as zero.</param>
    /// <returns>True when shield or health changed after the hit.</returns>
    public static bool TryApplyFlatShieldDamage(ref EnemyHealth enemyHealth, float incomingDamage)
    {
        float previousHealth = enemyHealth.Current;
        float previousShield = enemyHealth.CurrentShield;
        ApplyFlatShieldDamage(ref enemyHealth, incomingDamage);
        return enemyHealth.Current != previousHealth || enemyHealth.CurrentShield != previousShield;
    }

    /// <summary>
    /// Applies incoming flat damage to enemy shield first and then to health.
    /// </summary>
    /// <param name="enemyHealth">Mutable enemy health and shield state that receives the damage.</param>
    /// <param name="incomingDamage">Raw incoming damage value. Negative values are treated as zero.</param>
    public static void ApplyFlatShieldDamage(ref EnemyHealth enemyHealth, float incomingDamage)
    {
        ConsumeFlatShieldDamage(ref enemyHealth, incomingDamage);
    }

    /// <summary>
    /// Consumes incoming damage against enemy shield and health and returns any unapplied remainder.
    /// </summary>
    /// <param name="enemyHealth">Mutable enemy health and shield state that receives the damage.</param>
    /// <param name="incomingDamage">Raw incoming damage value. Negative values are treated as zero.</param>
    /// <returns>Damage remainder left after shield and health were fully consumed.</returns>
    public static float ConsumeFlatShieldDamage(ref EnemyHealth enemyHealth, float incomingDamage)
    {
        float remainingDamage = math.max(0f, incomingDamage);

        if (remainingDamage <= 0f)
            return 0f;

        if (enemyHealth.CurrentShield > 0f)
        {
            float absorbedDamage = math.min(enemyHealth.CurrentShield, remainingDamage);
            enemyHealth.CurrentShield -= absorbedDamage;
            remainingDamage -= absorbedDamage;

            if (enemyHealth.CurrentShield < 0f)
                enemyHealth.CurrentShield = 0f;
        }

        if (remainingDamage <= 0f)
            return 0f;

        float appliedHealthDamage = math.min(enemyHealth.Current, remainingDamage);
        enemyHealth.Current -= appliedHealthDamage;
        remainingDamage -= appliedHealthDamage;

        if (enemyHealth.Current < 0f)
            enemyHealth.Current = 0f;

        return math.max(0f, remainingDamage);
    }
    #endregion

    #endregion
}
