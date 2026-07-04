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

    /// <summary>
    /// Resolves the active enemy UI visual preset from the master preset or direct authoring fallback.
    /// </summary>
    /// <param name="masterPreset">Optional master preset that can override the direct UI visual preset.</param>
    /// <param name="fallbackUiVisualPreset">Fallback UI visual preset assigned directly on the authoring component.</param>
    /// <returns>Resolved UI visual preset, or null when no UI visual preset is available.</returns>
    public static EnemyUiVisualPreset ResolveUiVisualPreset(EnemyMasterPreset masterPreset, EnemyUiVisualPreset fallbackUiVisualPreset)
    {
        if (masterPreset != null && masterPreset.UiVisualPreset != null)
            return masterPreset.UiVisualPreset;

        return fallbackUiVisualPreset;
    }

    /// <summary>
    /// Resolves UI visual data from the new UI visual preset, falling back to legacy visual preset data for non-migrated assets.
    /// </summary>
    /// <param name="masterPreset">Optional master preset that can override direct fallback presets.</param>
    /// <param name="fallbackUiVisualPreset">Fallback UI visual preset assigned directly on the authoring component.</param>
    /// <param name="legacyFallbackVisualPreset">Legacy gameplay visual preset used only when no UI visual preset is assigned.</param>
    /// <returns>Resolved UI visual data, or null when no compatible preset is available.</returns>
    public static IEnemyUiVisualPresetData ResolveUiVisualPresetData(EnemyMasterPreset masterPreset,
                                                                     EnemyUiVisualPreset fallbackUiVisualPreset,
                                                                     EnemyVisualPreset legacyFallbackVisualPreset)
    {
        EnemyUiVisualPreset resolvedUiVisualPreset = ResolveUiVisualPreset(masterPreset, fallbackUiVisualPreset);

        if (resolvedUiVisualPreset != null)
            return resolvedUiVisualPreset;

        return ResolveVisualPreset(masterPreset, legacyFallbackVisualPreset);
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

    /// <summary>
    /// Resolves face flipbook settings from the active visual preset.
    /// </summary>
    /// <param name="masterPreset">Optional master preset that can override the visual preset.</param>
    /// <param name="fallbackVisualPreset">Fallback visual preset assigned directly on the authoring component.</param>
    /// <returns>Face flipbook settings, or null when no visual preset is available.</returns>
    public static EnemyVisualFaceFlipbookSettings ResolveFaceFlipbookSettings(EnemyMasterPreset masterPreset, EnemyVisualPreset fallbackVisualPreset)
    {
        EnemyVisualPreset resolvedVisualPreset = ResolveVisualPreset(masterPreset, fallbackVisualPreset);

        if (resolvedVisualPreset == null)
            return null;

        return resolvedVisualPreset.FaceFlipbook;
    }

    public static EnemyVisualOutlineSettings ResolveOutlineSettings(EnemyMasterPreset masterPreset, EnemyVisualPreset fallbackVisualPreset)
    {
        EnemyVisualPreset resolvedVisualPreset = ResolveVisualPreset(masterPreset, fallbackVisualPreset);

        if (resolvedVisualPreset == null)
            return null;

        return resolvedVisualPreset.Outline;
    }

    /// <summary>
    /// Resolves ground-footprint settings from the active visual preset.
    /// </summary>
    /// <param name="masterPreset">Optional master preset that can override the visual preset.</param>
    /// <param name="fallbackVisualPreset">Fallback visual preset assigned directly on the authoring component.</param>
    /// <returns>Footprint settings, or null when no visual preset is available.</returns>
    public static EnemyVisualFootprintSettings ResolveFootprintSettings(EnemyMasterPreset masterPreset, EnemyVisualPreset fallbackVisualPreset)
    {
        EnemyVisualPreset resolvedVisualPreset = ResolveVisualPreset(masterPreset, fallbackVisualPreset);

        if (resolvedVisualPreset == null)
            return null;

        return resolvedVisualPreset.Footprint;
    }

    /// <summary>
    /// Resolves ground-footprint settings from the active UI visual preset, with legacy visual fallback for older assets.
    /// </summary>
    /// <param name="masterPreset">Optional master preset that can override direct fallback presets.</param>
    /// <param name="fallbackUiVisualPreset">Fallback UI visual preset assigned directly on the authoring component.</param>
    /// <param name="legacyFallbackVisualPreset">Legacy gameplay visual preset used only when no UI visual preset is assigned.</param>
    /// <returns>Footprint settings, or null when no compatible preset is available.</returns>
    public static EnemyVisualFootprintSettings ResolveFootprintSettings(EnemyMasterPreset masterPreset,
                                                                        EnemyUiVisualPreset fallbackUiVisualPreset,
                                                                        EnemyVisualPreset legacyFallbackVisualPreset)
    {
        IEnemyUiVisualPresetData resolvedUiVisualPreset = ResolveUiVisualPresetData(masterPreset,
                                                                                   fallbackUiVisualPreset,
                                                                                   legacyFallbackVisualPreset);

        if (resolvedUiVisualPreset == null)
            return null;

        return resolvedUiVisualPreset.Footprint;
    }

    /// <summary>
    /// Resolves boss HUD settings from the active UI visual preset, with legacy visual fallback for older assets.
    /// </summary>
    /// <param name="masterPreset">Optional master preset that can override direct fallback presets.</param>
    /// <param name="fallbackUiVisualPreset">Fallback UI visual preset assigned directly on the authoring component.</param>
    /// <param name="legacyFallbackVisualPreset">Legacy gameplay visual preset used only when no UI visual preset is assigned.</param>
    /// <returns>Boss HUD settings, or null when no compatible preset is available.</returns>
    public static EnemyBossVisualUiSettings ResolveBossUiSettings(EnemyMasterPreset masterPreset,
                                                                  EnemyUiVisualPreset fallbackUiVisualPreset,
                                                                  EnemyVisualPreset legacyFallbackVisualPreset)
    {
        IEnemyUiVisualPresetData resolvedUiVisualPreset = ResolveUiVisualPresetData(masterPreset,
                                                                                   fallbackUiVisualPreset,
                                                                                   legacyFallbackVisualPreset);

        if (resolvedUiVisualPreset == null)
            return null;

        return resolvedUiVisualPreset.BossUi;
    }

    /// <summary>
    /// Resolves projectile offscreen-warning settings from the active UI visual preset, with legacy visual fallback for older assets.
    /// </summary>
    /// <param name="masterPreset">Optional master preset that can override direct fallback presets.</param>
    /// <param name="fallbackUiVisualPreset">Fallback UI visual preset assigned directly on the authoring component.</param>
    /// <param name="legacyFallbackVisualPreset">Legacy gameplay visual preset used only when no UI visual preset is assigned.</param>
    /// <returns>Projectile offscreen-warning settings, or null when no compatible preset is available.</returns>
    public static EnemyProjectileOffscreenWarningSettings ResolveProjectileOffscreenWarningSettings(EnemyMasterPreset masterPreset,
                                                                                                    EnemyUiVisualPreset fallbackUiVisualPreset,
                                                                                                    EnemyVisualPreset legacyFallbackVisualPreset)
    {
        IEnemyUiVisualPresetData resolvedUiVisualPreset = ResolveUiVisualPresetData(masterPreset,
                                                                                   fallbackUiVisualPreset,
                                                                                   legacyFallbackVisualPreset);

        if (resolvedUiVisualPreset == null)
            return null;

        return resolvedUiVisualPreset.ProjectileOffscreenWarning;
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
