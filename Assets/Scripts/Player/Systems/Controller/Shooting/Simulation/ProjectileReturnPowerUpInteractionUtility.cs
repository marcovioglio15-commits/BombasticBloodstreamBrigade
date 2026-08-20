using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves Returning Projectiles interoperability while preserving modules composed inside the same power-up.
/// Cross-power-up policies are evaluated separately so disabling them never breaks an owning power-up's module stack.
/// </summary>
public static class ProjectileReturnPowerUpInteractionUtility
{
    #region Methods

    #region Source Inheritance
    /// <summary>
    /// Reports whether Projectile Split may generate child shots from a returning projectile.
    /// Co-located split modules bypass only the external master gate and still respect their dedicated interaction setting.
    /// </summary>
    /// <param name="config">Resolved return config containing the split policy and baked module provenance.</param>
    /// <returns>True when Projectile Split may activate for the modified projectile.</returns>
    public static bool AllowsProjectileSplitting(in ReturningProjectilesConfig config)
    {
        return config.EnableProjectileSplitting != 0 &&
               (config.SamePowerUpHasProjectileSplit != 0 || config.AllowOtherPowerUpInteractions != 0);
    }

    /// <summary>
    /// Reports whether Projectile Split children inherit return behavior from their parent.
    /// A co-located split module always remains compatible; an external split module requires both cross-power-up gates.
    /// </summary>
    /// <param name="config">Resolved return config containing authored policies and baked module provenance.</param>
    /// <returns>True when split children may inherit Returning Projectiles.</returns>
    public static bool AllowsSplitChildren(in ReturningProjectilesConfig config)
    {
        return config.ApplyToSplitProjectiles != 0 &&
               (config.SamePowerUpHasProjectileSplit != 0 || config.AllowOtherPowerUpInteractions != 0);
    }

    /// <summary>
    /// Reports whether a passive Returning Projectiles module applies to shots emitted by a different active power-up.
    /// Same-power-up active compositions carry an explicit request override and do not use this external-source gate.
    /// </summary>
    /// <param name="config">Resolved passive return config.</param>
    /// <returns>True when external active projectile shots may receive return behavior.</returns>
    public static bool AllowsOtherActivePowerUpProjectiles(in ReturningProjectilesConfig config)
    {
        return config.AllowOtherPowerUpInteractions != 0 && config.ApplyToActivePowerUpProjectiles != 0;
    }

    /// <summary>
    /// Resolves how much formula-driven projectile-size tuning remains embedded in one returning shot.
    /// External Tiny/Mega-style sources require both interaction gates, while an owning power-up source remains identifiable by ID.
    /// </summary>
    /// <param name="config">Resolved returning-projectile config and owning power-up identifier.</param>
    /// <param name="embeddedMultiplier">Combined power-up size multiplier already present in the shoot request.</param>
    /// <param name="sourceMultipliers">Per-power-up runtime ratios evaluated by Character Tuning.</param>
    /// <returns>Positive size multiplier permitted by the authored interaction policy.</returns>
    public static float ResolveProjectileSizePowerUpMultiplier(in ReturningProjectilesConfig config,
                                                               float embeddedMultiplier,
                                                               DynamicBuffer<PlayerProjectileSizePowerUpMultiplierElement> sourceMultipliers)
    {
        float safeEmbeddedMultiplier = embeddedMultiplier > 0f ? embeddedMultiplier : 1f;

        if (config.ApplyTinyMegaProjectileScaling == 0)
            return 1f;

        if (config.AllowOtherPowerUpInteractions != 0)
            return safeEmbeddedMultiplier;

        if (config.OwningPowerUpId.Length <= 0 || !sourceMultipliers.IsCreated)
            return 1f;

        float owningPowerUpMultiplier = 1f;

        // Multiple entries can share an ID when independent scoped sources contribute to the same composed power-up.
        for (int sourceIndex = 0; sourceIndex < sourceMultipliers.Length; sourceIndex++)
        {
            PlayerProjectileSizePowerUpMultiplierElement sourceMultiplier = sourceMultipliers[sourceIndex];

            if (sourceMultiplier.PowerUpId != config.OwningPowerUpId)
                continue;

            owningPowerUpMultiplier *= math.max(0.01f, sourceMultiplier.Multiplier);
        }

        return math.max(0.01f, owningPowerUpMultiplier);
    }
    #endregion

    #region Trajectory Prerequisites
    /// <summary>
    /// Reports whether a wall contact consumes an available bounce instead of starting return travel.
    /// Range, lifetime, and hit-capacity termination are intentionally not gated by unused bounce budget.
    /// </summary>
    /// <param name="config">Resolved return config containing bounce policy and module provenance.</param>
    /// <returns>True when an available wall bounce must be consumed before a wall-triggered return.</returns>
    public static bool CompletesBouncesBeforeReturn(in ReturningProjectilesConfig config)
    {
        return config.CompleteBouncesBeforeReturn != 0 &&
               (config.SamePowerUpHasBouncingProjectiles != 0 || config.AllowOtherPowerUpInteractions != 0);
    }

    /// <summary>
    /// Reports whether an incomplete orbital trajectory delays return travel.
    /// Co-located orbital modules remain compatible independently from the external-interaction master toggle.
    /// </summary>
    /// <param name="config">Resolved return config containing orbital policy and module provenance.</param>
    /// <returns>True when a full orbital path must complete before return begins.</returns>
    public static bool CompletesOrbitalPathBeforeReturn(in ReturningProjectilesConfig config)
    {
        return AllowsOrbitalTrajectory(in config);
    }

    /// <summary>
    /// Reports whether an orbital trajectory may remain active on a projectile modified by Returning Projectiles.
    /// The dedicated setting is authoritative for both co-located and external orbital sources, while the external
    /// master gate prevents unrelated passive orbital modules from altering isolated returning shots.
    /// </summary>
    /// <param name="config">Resolved return config containing the orbital policy and baked module provenance.</param>
    /// <returns>True when the projectile may initialize and simulate an orbital trajectory.</returns>
    public static bool AllowsOrbitalTrajectory(in ReturningProjectilesConfig config)
    {
        return config.CompleteOrbitalPathBeforeReturn != 0 &&
               (config.SamePowerUpHasOrbitalProjectiles != 0 || config.AllowOtherPowerUpInteractions != 0);
    }
    #endregion

    #endregion
}
