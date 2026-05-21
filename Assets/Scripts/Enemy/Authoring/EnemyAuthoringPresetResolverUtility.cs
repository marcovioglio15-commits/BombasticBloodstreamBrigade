using UnityEngine;

/// <summary>
/// Resolves fallback and preset-derived enemy authoring settings without duplicating lookup code in the authoring component.
/// </summary>
public static class EnemyAuthoringPresetResolverUtility
{
    #region Methods

    #region Preset Resolution
    public static EnemyBrainPreset ResolveBrainPreset(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        if (masterPreset != null && masterPreset.BrainPreset != null)
            return masterPreset.BrainPreset;

        return fallbackBrainPreset;
    }

    public static EnemyAdvancedPatternPreset ResolveAdvancedPatternPreset(EnemyMasterPreset masterPreset, EnemyAdvancedPatternPreset fallbackAdvancedPatternPreset)
    {
        if (masterPreset != null && masterPreset.AdvancedPatternPreset != null)
            return masterPreset.AdvancedPatternPreset;

        return fallbackAdvancedPatternPreset;
    }

    public static EnemyBossPatternPreset ResolveBossPatternPreset(EnemyMasterPreset masterPreset, EnemyBossPatternPreset fallbackBossPatternPreset)
    {
        if (masterPreset != null && masterPreset.BossPatternPreset != null)
            return masterPreset.BossPatternPreset;

        return fallbackBossPatternPreset;
    }

    public static EnemyVisualPreset ResolveVisualPreset(EnemyMasterPreset masterPreset, EnemyVisualPreset fallbackVisualPreset)
    {
        if (masterPreset != null && masterPreset.VisualPreset != null)
            return masterPreset.VisualPreset;

        return fallbackVisualPreset;
    }

    public static EnemyBrainMovementSettings ResolveMovementSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Movement;
    }

    public static EnemyBrainSteeringSettings ResolveSteeringSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Steering;
    }

    public static EnemyBrainTacticalNavigationSettings ResolveTacticalNavigationSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.TacticalNavigation;
    }

    public static EnemyBrainDamageSettings ResolveDamageSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Damage;
    }

    public static EnemyBrainHealthStatisticsSettings ResolveHealthStatisticsSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.HealthStatistics;
    }

    public static EnemyVisualVisibilitySettings ResolveVisibilitySettings(EnemyMasterPreset masterPreset, EnemyVisualPreset fallbackVisualPreset)
    {
        EnemyVisualPreset resolvedVisualPreset = ResolveVisualPreset(masterPreset, fallbackVisualPreset);

        if (resolvedVisualPreset == null)
            return null;

        return resolvedVisualPreset.Visibility;
    }

    public static EnemyVisualPrefabSettings ResolveVisualPrefabSettings(EnemyMasterPreset masterPreset, EnemyVisualPreset fallbackVisualPreset)
    {
        EnemyVisualPreset resolvedVisualPreset = ResolveVisualPreset(masterPreset, fallbackVisualPreset);

        if (resolvedVisualPreset == null)
            return null;

        return resolvedVisualPreset.Prefabs;
    }

    public static EnemyVisualOutlineSettings ResolveOutlineSettings(EnemyMasterPreset masterPreset, EnemyVisualPreset fallbackVisualPreset)
    {
        EnemyVisualPreset resolvedVisualPreset = ResolveVisualPreset(masterPreset, fallbackVisualPreset);

        if (resolvedVisualPreset == null)
            return null;

        return resolvedVisualPreset.Outline;
    }

    public static EnemyOffensiveEngagementFeedbackSettings ResolveOffensiveEngagementFeedbackSettings(EnemyMasterPreset masterPreset,
                                                                                                      EnemyVisualPreset fallbackVisualPreset)
    {
        EnemyVisualPreset resolvedVisualPreset = ResolveVisualPreset(masterPreset, fallbackVisualPreset);

        if (resolvedVisualPreset == null)
            return null;

        return resolvedVisualPreset.OffensiveEngagementFeedback;
    }

    /// <summary>
    /// Resolves boss pattern-change feedback settings from the active visual preset.
    /// </summary>
    /// <param name="masterPreset">Optional master preset that can override the visual preset.</param>
    /// <param name="fallbackVisualPreset">Fallback visual preset assigned directly on the authoring component.</param>
    /// <returns>Boss pattern-change feedback settings, or null when no visual preset is available.</returns>
    public static EnemyOffensiveEngagementFeedbackSettings ResolveBossPatternChangeFeedbackSettings(EnemyMasterPreset masterPreset,
                                                                                                    EnemyVisualPreset fallbackVisualPreset)
    {
        EnemyVisualPreset resolvedVisualPreset = ResolveVisualPreset(masterPreset, fallbackVisualPreset);

        if (resolvedVisualPreset == null)
            return null;

        return resolvedVisualPreset.BossPatternChangeFeedback;
    }
    #endregion

    #endregion
}
