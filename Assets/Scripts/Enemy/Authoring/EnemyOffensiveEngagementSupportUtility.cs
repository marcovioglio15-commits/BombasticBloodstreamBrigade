/// <summary>
/// Resolves which offensive module kinds can emit predictive engagement feedback and which timing model they use.
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
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return ResolveCoreMovementTimingMode(moduleKind);

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return ResolveShortRangeTimingMode(moduleKind);

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return ResolveWeaponTimingMode(moduleKind);

            case EnemyPatternModuleCatalogSection.DropItems:
                return EnemyOffensiveEngagementTimingMode.ModuleActivation;

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
    #endregion

    #region Private Methods
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
            case EnemyPatternModuleKind.Coward:
                return EnemyOffensiveEngagementTimingMode.ModuleActivation;

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }

    /// <summary>
    /// Resolves the predictive engagement timing mode supported by one short-range module kind.
    /// </summary>
    /// <param name="moduleKind">Selected short-range module kind.</param>
    /// <returns>Supported timing mode, or None when no predictive trigger is currently implemented.</returns>
    private static EnemyOffensiveEngagementTimingMode ResolveShortRangeTimingMode(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.ShortRangeDash:
                return EnemyOffensiveEngagementTimingMode.ShortRangeDashRelease;

            case EnemyPatternModuleKind.Grunt:
            case EnemyPatternModuleKind.Wanderer:
            case EnemyPatternModuleKind.Coward:
            case EnemyPatternModuleKind.Stationary:
                return EnemyOffensiveEngagementTimingMode.ModuleActivation;

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }

    /// <summary>
    /// Resolves the predictive engagement timing mode supported by one weapon module kind.
    /// </summary>
    /// <param name="moduleKind">Selected weapon module kind.</param>
    /// <returns>Supported timing mode, or None when no predictive trigger is currently implemented.</returns>
    private static EnemyOffensiveEngagementTimingMode ResolveWeaponTimingMode(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Shooter:
                return EnemyOffensiveEngagementTimingMode.WeaponShot;

            case EnemyPatternModuleKind.Grunt:
            case EnemyPatternModuleKind.Stationary:
            case EnemyPatternModuleKind.Wanderer:
            case EnemyPatternModuleKind.Coward:
            case EnemyPatternModuleKind.ShortRangeDash:
                return EnemyOffensiveEngagementTimingMode.ModuleActivation;

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }
    #endregion

    #endregion
}
