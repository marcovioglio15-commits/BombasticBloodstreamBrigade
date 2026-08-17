using Unity.Entities;

/// <summary>
/// Resolves replacement-projectile VFX policies without changing presentation for base-prefab fallbacks.
/// </summary>
public static class ProjectileReturnVfxPolicyUtility
{
    #region Methods

    #region Public Policies
    /// <summary>
    /// Reports whether the Player Visual preset's attached projectile VFX may be spawned for one resolved prefab.
    /// </summary>
    /// <param name="resolvedPrefabEntity">Prefab partition selected for the projectile.</param>
    /// <param name="hasReturningProjectiles">Whether Returning Projectiles applies to the shot.</param>
    /// <param name="config">Resolved return config containing replacement-prefab VFX policies.</param>
    /// <returns>True when attached projectile VFX may be enqueued.</returns>
    public static bool AllowsProjectileVfx(Entity resolvedPrefabEntity,
                                           bool hasReturningProjectiles,
                                           in ReturningProjectilesConfig config)
    {
        return AllowsReplacementVfx(resolvedPrefabEntity,
                                    hasReturningProjectiles,
                                    in config,
                                    config.KeepProjectileVfx);
    }

    /// <summary>
    /// Reports whether the standard muzzle-flash VFX may represent a shot using one resolved prefab.
    /// </summary>
    /// <param name="resolvedPrefabEntity">Prefab partition selected for the projectile.</param>
    /// <param name="hasReturningProjectiles">Whether Returning Projectiles applies to the shot.</param>
    /// <param name="config">Resolved return config containing replacement-prefab VFX policies.</param>
    /// <returns>True when muzzle-flash VFX may be enqueued.</returns>
    public static bool AllowsMuzzleFlashVfx(Entity resolvedPrefabEntity,
                                           bool hasReturningProjectiles,
                                           in ReturningProjectilesConfig config)
    {
        return AllowsReplacementVfx(resolvedPrefabEntity,
                                    hasReturningProjectiles,
                                    in config,
                                    config.KeepMuzzleFlashVfx);
    }

    /// <summary>
    /// Reports whether enemy hit-react and elemental hit VFX may be emitted by one projectile prefab partition.
    /// </summary>
    /// <param name="resolvedPrefabEntity">Prefab partition used by the projectile.</param>
    /// <param name="hasReturningProjectiles">Whether Returning Projectiles applies to the projectile.</param>
    /// <param name="config">Resolved return config containing replacement-prefab VFX policies.</param>
    /// <returns>True when hit VFX may be enqueued.</returns>
    public static bool AllowsHitVfx(Entity resolvedPrefabEntity,
                                    bool hasReturningProjectiles,
                                    in ReturningProjectilesConfig config)
    {
        return AllowsReplacementVfx(resolvedPrefabEntity,
                                    hasReturningProjectiles,
                                    in config,
                                    config.KeepHitVfx);
    }

    /// <summary>
    /// Reports whether range, lifetime, and terminal-wall death VFX may be emitted by one projectile prefab partition.
    /// </summary>
    /// <param name="resolvedPrefabEntity">Prefab partition used by the projectile.</param>
    /// <param name="hasReturningProjectiles">Whether Returning Projectiles applies to the projectile.</param>
    /// <param name="config">Resolved return config containing replacement-prefab VFX policies.</param>
    /// <returns>True when projectile death VFX may be enqueued.</returns>
    public static bool AllowsDeathVfx(Entity resolvedPrefabEntity,
                                      bool hasReturningProjectiles,
                                      in ReturningProjectilesConfig config)
    {
        return AllowsReplacementVfx(resolvedPrefabEntity,
                                    hasReturningProjectiles,
                                    in config,
                                    config.KeepDeathVfx);
    }
    #endregion

    #region Shared Resolution
    /// <summary>
    /// Applies one VFX policy only when Returning Projectiles selected its valid replacement prefab.
    /// </summary>
    /// <param name="resolvedPrefabEntity">Prefab partition used by the projectile.</param>
    /// <param name="hasReturningProjectiles">Whether Returning Projectiles applies.</param>
    /// <param name="config">Resolved return config containing the replacement prefab.</param>
    /// <param name="keepVfx">Baked VFX policy selected by the caller.</param>
    /// <returns>True for ordinary or fallback projectiles, otherwise the replacement-specific policy.</returns>
    private static bool AllowsReplacementVfx(Entity resolvedPrefabEntity,
                                             bool hasReturningProjectiles,
                                             in ReturningProjectilesConfig config,
                                             byte keepVfx)
    {
        return !hasReturningProjectiles ||
               config.ReplacementProjectilePrefabEntity == Entity.Null ||
               resolvedPrefabEntity != config.ReplacementProjectilePrefabEntity ||
               keepVfx != 0;
    }
    #endregion

    #endregion
}
