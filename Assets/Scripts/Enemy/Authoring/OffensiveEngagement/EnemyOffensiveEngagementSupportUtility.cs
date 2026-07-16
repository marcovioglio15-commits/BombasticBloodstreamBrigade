/// <summary>
/// Identifies the runtime owner available to evaluate an offensive engagement timing window.
/// </summary>
public enum EnemyOffensiveEngagementTimingContext : byte
{
    SharedPattern = 0,
    BossMixedPattern = 1
}

/// <summary>
/// Resolves which offensive module kinds can emit engagement feedback in shared patterns or boss-owned mixed-pattern slots.
/// </summary>
public static class EnemyOffensiveEngagementSupportUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the predictive engagement timing mode supported by one module inside the provided catalog section.
    /// </summary>
    /// <param name="section">Catalog section that owns the module binding.</param>
    /// <param name="moduleKind">Resolved module kind selected in that section.</param>
    /// <returns>Supported timing mode, or None when predictive engagement feedback is not implemented for that module kind.</returns>
    public static EnemyOffensiveEngagementTimingMode ResolveTimingMode(EnemyPatternModuleCatalogSection section,
                                                                       EnemyPatternModuleKind moduleKind)
    {
        return ResolveTimingMode(section,
                                 moduleKind,
                                 EnemyOffensiveEngagementTimingContext.SharedPattern);
    }

    /// <summary>
    /// Resolves the engagement timing mode supported by one module for the runtime context that will evaluate it.
    /// </summary>
    /// <param name="section">Catalog section that owns the module binding.</param>
    /// <param name="moduleKind">Resolved module kind selected in that section.</param>
    /// <param name="timingContext">Runtime owner available to evaluate predictive or activation timing.</param>
    /// <returns>Supported timing mode, or None when that context has no matching runtime hook.</returns>
    public static EnemyOffensiveEngagementTimingMode ResolveTimingMode(EnemyPatternModuleCatalogSection section,
                                                                       EnemyPatternModuleKind moduleKind,
                                                                       EnemyOffensiveEngagementTimingContext timingContext)
    {
        if (timingContext == EnemyOffensiveEngagementTimingContext.SharedPattern)
            return ResolveSharedPatternTimingMode(section, moduleKind);

        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return ResolveCoreMovementTimingMode(moduleKind);

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return ResolveShortRangeTimingMode(moduleKind);

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return ResolveWeaponTimingMode(moduleKind);

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }

    /// <summary>
    /// Returns whether the provided module kind currently supports predictive engagement feedback inside the provided catalog section.
    /// </summary>
    /// <param name="section">Catalog section that owns the module binding.</param>
    /// <param name="moduleKind">Resolved module kind selected in that section.</param>
    /// <returns>True when the module kind currently maps to a supported timing mode.</returns>
    public static bool SupportsTimingMode(EnemyPatternModuleCatalogSection section,
                                          EnemyPatternModuleKind moduleKind)
    {
        return ResolveTimingMode(section, moduleKind) != EnemyOffensiveEngagementTimingMode.None;
    }

    /// <summary>
    /// Returns whether one module kind has a runtime timing hook in the requested pattern context.
    /// </summary>
    /// <param name="section">Catalog section that owns the module binding.</param>
    /// <param name="moduleKind">Resolved module kind selected in that section.</param>
    /// <param name="timingContext">Runtime owner available to evaluate the warning window.</param>
    /// <returns>True when the requested context can evaluate the resolved timing mode.</returns>
    public static bool SupportsTimingMode(EnemyPatternModuleCatalogSection section,
                                          EnemyPatternModuleKind moduleKind,
                                          EnemyOffensiveEngagementTimingContext timingContext)
    {
        return ResolveTimingMode(section, moduleKind, timingContext) != EnemyOffensiveEngagementTimingMode.None;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves predictive timing that can be evaluated without boss-owned module slot runtime state.
    /// </summary>
    /// <param name="section">Shared pattern catalog section.</param>
    /// <param name="moduleKind">Module kind selected by the shared pattern.</param>
    /// <returns>Predictive timing mode, or None when the normal enemy runtime exposes no matching hook.</returns>
    private static EnemyOffensiveEngagementTimingMode ResolveSharedPatternTimingMode(EnemyPatternModuleCatalogSection section,
                                                                                     EnemyPatternModuleKind moduleKind)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return moduleKind == EnemyPatternModuleKind.ShortRangeDash
                    ? EnemyOffensiveEngagementTimingMode.ShortRangeDashRelease
                    : EnemyOffensiveEngagementTimingMode.None;

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return moduleKind == EnemyPatternModuleKind.Shooter ||
                       moduleKind == EnemyPatternModuleKind.Bombardier
                    ? EnemyOffensiveEngagementTimingMode.WeaponShot
                    : EnemyOffensiveEngagementTimingMode.None;

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }

    /// <summary>
    /// Resolves the visual engagement timing mode for Core Movement modules that do not own a more specific commit hook.
    /// </summary>
    /// <param name="moduleKind">Selected Core Movement module kind.</param>
    /// <returns>Activation timing for every concrete Core Movement module, or None when the binding is invalid.</returns>
    private static EnemyOffensiveEngagementTimingMode ResolveCoreMovementTimingMode(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
            case EnemyPatternModuleKind.Grunt:
            case EnemyPatternModuleKind.Wanderer:
                return EnemyOffensiveEngagementTimingMode.ModuleActivation;

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }

    /// <summary>
    /// Resolves predictive commit timing or boss-owned activation timing for one short-range module kind.
    /// </summary>
    /// <param name="moduleKind">Selected short-range module kind.</param>
    /// <returns>Supported predictive or activation timing mode, or None when no matching runtime trigger is implemented.</returns>
    private static EnemyOffensiveEngagementTimingMode ResolveShortRangeTimingMode(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.ShortRangeDash:
                return EnemyOffensiveEngagementTimingMode.ShortRangeDashRelease;

            case EnemyPatternModuleKind.Grunt:
            case EnemyPatternModuleKind.Coward:
                return EnemyOffensiveEngagementTimingMode.ModuleActivation;

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }

    /// <summary>
    /// Resolves predictive commit timing or boss-owned activation timing for one weapon module kind.
    /// </summary>
    /// <param name="moduleKind">Selected weapon module kind.</param>
    /// <returns>Supported predictive or activation timing mode, or None when no matching runtime trigger is implemented.</returns>
    private static EnemyOffensiveEngagementTimingMode ResolveWeaponTimingMode(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Shooter:
            case EnemyPatternModuleKind.Bombardier:
                return EnemyOffensiveEngagementTimingMode.WeaponShot;

            case EnemyPatternModuleKind.PowerUpStealer:
                return EnemyOffensiveEngagementTimingMode.ModuleActivation;

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }
    #endregion

    #endregion
}
