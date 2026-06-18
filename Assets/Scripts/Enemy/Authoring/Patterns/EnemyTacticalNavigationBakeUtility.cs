using Unity.Mathematics;

/// <summary>
/// Builds ECS tactical navigation configuration from resolved enemy authoring presets.
/// </summary>
internal static class EnemyTacticalNavigationBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds tactical navigation config from the resolved brain preset, clamping only for runtime safety.
    /// </summary>
    /// <param name="authoring">Enemy authoring source being baked.</param>
    /// <returns>Runtime tactical navigation config.</returns>
    public static EnemyTacticalNavigationConfig BuildConfig(EnemyAuthoring authoring)
    {
        EnemyTacticalNavigationConfig defaultConfig = EnemyPatternDefaultsUtility.CreateTacticalNavigationConfig();

        if (authoring == null)
            return defaultConfig;

        EnemyBrainTacticalNavigationSettings settings = EnemyAuthoringPresetResolverUtility.ResolveTacticalNavigationSettings(authoring.MasterPreset,
                                                                                                                             authoring.BrainPreset);

        if (settings == null)
            return defaultConfig;

        return new EnemyTacticalNavigationConfig
        {
            CandidateBudget = ResolveTacticalCandidateBudget(settings.CandidateBudget),
            NavigationInfluence = math.saturate(settings.NavigationInfluence),
            PredictionHorizonSeconds = math.clamp(settings.PredictionHorizonSeconds, 0f, 2f),
            SidePassPreference = math.saturate(settings.SidePassPreference),
            CrowdLanePreference = math.saturate(settings.CrowdLanePreference),
            WallTangentPreference = math.saturate(settings.WallTangentPreference),
            OscillationDamping = math.saturate(settings.OscillationDamping),
            StuckRecoverySeconds = math.max(0.05f, settings.StuckRecoverySeconds)
        };
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves unsupported tactical budget enum values to a stable runtime default.
    /// </summary>
    /// <param name="candidateBudget">Authored budget value.</param>
    /// <returns>Supported tactical candidate budget.</returns>
    private static EnemyTacticalCandidateBudget ResolveTacticalCandidateBudget(EnemyTacticalCandidateBudget candidateBudget)
    {
        switch (candidateBudget)
        {
            case EnemyTacticalCandidateBudget.Low:
            case EnemyTacticalCandidateBudget.Balanced:
            case EnemyTacticalCandidateBudget.High:
                return candidateBudget;

            default:
                return EnemyTacticalCandidateBudget.Balanced;
        }
    }
    #endregion

    #endregion
}
