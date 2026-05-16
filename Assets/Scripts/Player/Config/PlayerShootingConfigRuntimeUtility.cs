using Unity.Mathematics;

/// <summary>
/// Builds runtime-safe shooting value blobs from controller authoring data.
/// </summary>
public static class PlayerShootingConfigRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts authored shooting values into the runtime blob used by controller config, bake baselines and runtime rebuilds.
    /// </summary>
    /// <param name="sourceValues">Authoring-side shooting values block.</param>
    /// <returns>Runtime-safe shooting values blob.</returns>
    public static ShootingValuesBlob BuildRuntimeValues(ShootingValues sourceValues)
    {
        ShootingValues resolvedValues = sourceValues;

        if (resolvedValues == null)
            resolvedValues = new ShootingValues();

        resolvedValues.Validate();

        return new ShootingValuesBlob
        {
            ShootSpeed = resolvedValues.ShootSpeed,
            RateOfFire = resolvedValues.RateOfFire,
            ProjectileSizeMultiplier = math.max(0.01f, resolvedValues.ProjectileSizeMultiplier),
            ExplosionRadius = math.max(0f, resolvedValues.ExplosionRadius),
            Range = resolvedValues.Range,
            Lifetime = resolvedValues.Lifetime,
            Damage = math.max(0f, resolvedValues.Damage),
            ElementBehaviours = PlayerElementBulletSettingsUtility.BuildRuntimeSettingsByElement(resolvedValues.ElementBehaviours),
            PenetrationMode = resolvedValues.PenetrationMode,
            MaxPenetrations = math.max(0, resolvedValues.MaxPenetrations),
            Knockback = PlayerProjectileKnockbackSettingsUtility.BuildRuntimeSettings(resolvedValues.Knockback)
        };
    }
    #endregion

    #endregion
}
