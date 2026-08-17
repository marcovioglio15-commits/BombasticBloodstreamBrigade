using System;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Validates Returning Projectiles interoperability gates that are shared by spawn and trajectory runtime paths.
/// </summary>
public static class PlayerReturningProjectileInteractionSmokeTest
{
    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs deterministic checks for outbound hits, bounce, orbit, split, and replacement-prefab VFX policies.
    /// </summary>
    public static void Run()
    {
        ValidateOutboundHitPolicies();
        ValidateBounceAndOrbitPolicies();
        ValidateProjectileSplittingPolicy();
        ValidateProjectileVfxPolicy();
    }
    #endregion

    #region Outbound Hits
    /// <summary>
    /// Verifies enemy-hit continuation policies preserve natural behavior and keep trajectory limits authoritative.
    /// </summary>
    private static void ValidateOutboundHitPolicies()
    {
        // Natural penetration must retain the terminal result produced by the projectile's own hit mode.
        ProjectileReturnState returnState = new ProjectileReturnState
        {
            Enabled = 1,
            Phase = ProjectileReturnPhase.Outbound,
            OriginalDamage = 12f
        };
        Projectile projectile = new Projectile
        {
            Damage = 12f,
            PenetrationMode = ProjectilePenetrationMode.None,
            MaxRange = 5f,
            MaxLifetime = 3f
        };

        if (ProjectileReturnRuntimeUtility.TryExtendOutboundAfterNaturalHitCapacity(ref returnState, ref projectile))
            throw new InvalidOperationException("Natural outbound penetration unexpectedly continued after its terminal enemy hit.");

        // Complete outbound travel converts only exhausted enemy penetration and leaves normal trajectory limits untouched.
        returnState.Config.OutboundHitPolicy = ProjectileOutboundHitPolicy.CompleteOutboundTravel;

        if (!ProjectileReturnRuntimeUtility.TryExtendOutboundAfterNaturalHitCapacity(ref returnState, ref projectile) ||
            projectile.PenetrationMode != ProjectilePenetrationMode.Infinite ||
            returnState.OutboundNaturalHitCapacityExhausted == 0)
            throw new InvalidOperationException("Complete outbound travel did not continue after natural enemy penetration was exhausted.");

        ProjectileRuntimeState runtimeState = new ProjectileRuntimeState
        {
            TraveledDistance = 5f
        };

        if (!ProjectileReturnRuntimeUtility.HasReachedOutboundLimit(in projectile, in runtimeState))
            throw new InvalidOperationException("Complete outbound travel bypassed the projectile's authoritative range limit.");

        // Limited continuation converts the configured budget into full-damage fixed hits exactly once.
        returnState = new ProjectileReturnState
        {
            Enabled = 1,
            Phase = ProjectileReturnPhase.Outbound,
            OriginalDamage = 12f,
            AdditionalOutboundHitsRemaining = 3,
            Config = new ReturningProjectilesConfig
            {
                OutboundHitPolicy = ProjectileOutboundHitPolicy.LimitedAdditionalHits
            }
        };
        projectile.Damage = 0f;
        projectile.PenetrationMode = ProjectilePenetrationMode.None;

        if (!ProjectileReturnRuntimeUtility.TryExtendOutboundAfterNaturalHitCapacity(ref returnState, ref projectile) ||
            projectile.PenetrationMode != ProjectilePenetrationMode.FixedHits ||
            projectile.RemainingPenetrations != 2 ||
            math.abs(projectile.Damage - 12f) > 0.0001f ||
            returnState.AdditionalOutboundHitsRemaining != 0)
            throw new InvalidOperationException("Limited outbound continuation did not apply its configured additional enemy-hit budget.");

        if (ProjectileReturnRuntimeUtility.TryExtendOutboundAfterNaturalHitCapacity(ref returnState, ref projectile))
            throw new InvalidOperationException("Limited outbound continuation reused an exhausted additional hit budget.");
    }
    #endregion

    #region Trajectory Interactions
    /// <summary>
    /// Verifies natural terminal limits and same-power-up provenance across bounce and orbital prerequisites.
    /// </summary>
    private static void ValidateBounceAndOrbitPolicies()
    {
        // Build an outbound return state with the external bounce policy enabled.
        ProjectileReturnState returnState = new ProjectileReturnState
        {
            Enabled = 1,
            Phase = ProjectileReturnPhase.Outbound,
            Config = new ReturningProjectilesConfig
            {
                AllowOtherPowerUpInteractions = 1,
                CompleteBouncesBeforeReturn = 1
            }
        };
        ProjectilePerfectCircleState perfectCircleState = default;

        // Unused bounce capacity can affect a wall contact, but cannot keep an expired projectile alive.
        if (!ProjectileReturnPowerUpInteractionUtility.CompletesBouncesBeforeReturn(in returnState.Config) ||
            !ProjectileReturnRuntimeUtility.CanBeginReturn(in returnState, in perfectCircleState))
            throw new InvalidOperationException("Returning Projectiles still allowed unused bounce capacity to override range or lifetime termination.");

        // Disable external policies while retaining the authored bounce setting.
        returnState.Config.AllowOtherPowerUpInteractions = 0;

        if (ProjectileReturnPowerUpInteractionUtility.CompletesBouncesBeforeReturn(in returnState.Config))
            throw new InvalidOperationException("External Bouncing Projectiles interaction ignored the disabled master gate.");

        // Co-located bounce provenance must bypass only the external master gate.
        returnState.Config.SamePowerUpHasBouncingProjectiles = 1;

        if (!ProjectileReturnPowerUpInteractionUtility.CompletesBouncesBeforeReturn(in returnState.Config))
            throw new InvalidOperationException("A co-located Bouncing Projectiles module was excluded with external interactions disabled.");

        returnState.Config.CompleteBouncesBeforeReturn = 0;

        if (ProjectileReturnPowerUpInteractionUtility.CompletesBouncesBeforeReturn(in returnState.Config))
            throw new InvalidOperationException("The bounce interaction policy was ignored for a module composed inside the same power-up.");

        // Apply the same provenance rules to the full-orbit prerequisite.
        perfectCircleState.Enabled = 1;
        returnState.Config.CompleteOrbitalPathBeforeReturn = 1;
        returnState.Config.SamePowerUpHasOrbitalProjectiles = 0;

        if (!ProjectileReturnRuntimeUtility.CanBeginReturn(in returnState, in perfectCircleState))
            throw new InvalidOperationException("An external orbital module delayed return while cross-power-up interactions were disabled.");

        returnState.Config.SamePowerUpHasOrbitalProjectiles = 1;

        if (ProjectileReturnRuntimeUtility.CanBeginReturn(in returnState, in perfectCircleState))
            throw new InvalidOperationException("A co-located orbital module did not preserve its full-path prerequisite.");
    }
    #endregion

    #region Split Interactions
    /// <summary>
    /// Verifies the dedicated split-generation gate without changing ordinary non-returning projectile behavior.
    /// </summary>
    private static void ValidateProjectileSplittingPolicy()
    {
        // Build a minimal valid split payload and an externally isolated return config.
        SplittingProjectilesPassiveConfig splittingConfig = new SplittingProjectilesPassiveConfig
        {
            SplitProjectileCount = 2,
            SplitDamageMultiplier = 1f,
            SplitSizeMultiplier = 1f,
            SplitSpeedMultiplier = 1f,
            SplitLifetimeMultiplier = 1f
        };
        ReturningProjectilesConfig returningConfig = new ReturningProjectilesConfig
        {
            EnableProjectileSplitting = 1
        };

        // An external split module must be excluded with the master interaction gate disabled.
        ProjectileSplitState splitState = ProjectileSpawnInitializationUtility.BuildSplitState(in splittingConfig,
                                                                                                true,
                                                                                                false,
                                                                                                true,
                                                                                                in returningConfig);

        if (splitState.CanSplit != 0)
            throw new InvalidOperationException("Returning Projectiles allowed an external Projectile Split module while cross-power-up interactions were disabled.");

        // Same-power-up provenance keeps the local module composition available.
        returningConfig.SamePowerUpHasProjectileSplit = 1;
        splitState = ProjectileSpawnInitializationUtility.BuildSplitState(in splittingConfig,
                                                                           true,
                                                                           false,
                                                                           true,
                                                                           in returningConfig);

        if (splitState.CanSplit == 0)
            throw new InvalidOperationException("Returning Projectiles suppressed a Projectile Split module composed inside the same power-up.");

        // The dedicated setting must still disable a co-located split module.
        returningConfig.EnableProjectileSplitting = 0;
        splitState = ProjectileSpawnInitializationUtility.BuildSplitState(in splittingConfig,
                                                                           true,
                                                                           false,
                                                                           true,
                                                                           in returningConfig);

        if (splitState.CanSplit != 0)
            throw new InvalidOperationException("Returning Projectiles ignored its dedicated projectile-splitting setting.");

        // Ordinary projectiles remain governed only by their Projectile Split module.
        splitState = ProjectileSpawnInitializationUtility.BuildSplitState(in splittingConfig,
                                                                           true,
                                                                           false,
                                                                           false,
                                                                           in returningConfig);

        if (splitState.CanSplit == 0)
            throw new InvalidOperationException("Returning Projectiles split filtering leaked into an ordinary projectile.");
    }
    #endregion

    #region Projectile VFX
    /// <summary>
    /// Verifies all replacement-prefab VFX toggles affect only the selected replacement partition.
    /// </summary>
    private static void ValidateProjectileVfxPolicy()
    {
        // Use stable synthetic entity identifiers because the policy compares prefab partitions without entity access.
        Entity replacementPrefabEntity = new Entity
        {
            Index = 41,
            Version = 1
        };
        Entity basePrefabEntity = new Entity
        {
            Index = 42,
            Version = 1
        };
        ReturningProjectilesConfig config = new ReturningProjectilesConfig
        {
            ReplacementProjectilePrefabEntity = replacementPrefabEntity,
            KeepProjectileVfx = 0,
            KeepMuzzleFlashVfx = 0,
            KeepHitVfx = 0,
            KeepDeathVfx = 0
        };

        // Suppression applies to each presentation channel only when the replacement prefab was selected.
        if (ProjectileReturnVfxPolicyUtility.AllowsProjectileVfx(replacementPrefabEntity, true, in config) ||
            ProjectileReturnVfxPolicyUtility.AllowsMuzzleFlashVfx(replacementPrefabEntity, true, in config) ||
            ProjectileReturnVfxPolicyUtility.AllowsHitVfx(replacementPrefabEntity, true, in config) ||
            ProjectileReturnVfxPolicyUtility.AllowsDeathVfx(replacementPrefabEntity, true, in config))
            throw new InvalidOperationException("Returning Projectiles retained replacement-prefab VFX after their dedicated settings were disabled.");

        if (!ProjectileReturnVfxPolicyUtility.AllowsProjectileVfx(basePrefabEntity, true, in config) ||
            !ProjectileReturnVfxPolicyUtility.AllowsMuzzleFlashVfx(basePrefabEntity, true, in config) ||
            !ProjectileReturnVfxPolicyUtility.AllowsHitVfx(basePrefabEntity, true, in config) ||
            !ProjectileReturnVfxPolicyUtility.AllowsDeathVfx(basePrefabEntity, true, in config))
            throw new InvalidOperationException("Returning Projectiles suppressed VFX after falling back to the base projectile prefab.");

        // Re-enabling every channel restores the complete VFX presentation on the replacement partition.
        config.KeepProjectileVfx = 1;
        config.KeepMuzzleFlashVfx = 1;
        config.KeepHitVfx = 1;
        config.KeepDeathVfx = 1;

        if (!ProjectileReturnVfxPolicyUtility.AllowsProjectileVfx(replacementPrefabEntity, true, in config) ||
            !ProjectileReturnVfxPolicyUtility.AllowsMuzzleFlashVfx(replacementPrefabEntity, true, in config) ||
            !ProjectileReturnVfxPolicyUtility.AllowsHitVfx(replacementPrefabEntity, true, in config) ||
            !ProjectileReturnVfxPolicyUtility.AllowsDeathVfx(replacementPrefabEntity, true, in config))
            throw new InvalidOperationException("Returning Projectiles did not restore every VFX channel for its replacement prefab.");
    }
    #endregion

    #endregion
}
