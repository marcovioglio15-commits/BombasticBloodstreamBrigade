using Unity.Mathematics;

/// <summary>
/// Converts authored projectile knockback settings into the runtime ECS blob representation.
/// </summary>
public static class PlayerProjectileKnockbackSettingsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one runtime-safe knockback blob from authored shooting settings.
    /// </summary>
    /// <param name="knockbackSettings">Authored knockback configuration from the controller preset.</param>
    /// <returns>Runtime knockback blob ready for bake-time and runtime controller data.</returns>
    public static ProjectileKnockbackSettingsBlob BuildRuntimeSettings(ProjectileKnockbackSettings knockbackSettings)
    {
        if (knockbackSettings == null)
            return default;

        return new ProjectileKnockbackSettingsBlob
        {
            Enabled = knockbackSettings.Enabled ? (byte)1 : (byte)0,
            Strength = math.max(0f, knockbackSettings.Strength),
            DurationSeconds = math.max(0f, knockbackSettings.DurationSeconds),
            DirectionMode = knockbackSettings.DirectionMode,
            StackingMode = knockbackSettings.StackingMode
        };
    }
    #endregion

    #endregion
}
