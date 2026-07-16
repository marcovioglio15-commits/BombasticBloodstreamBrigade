/// <summary>
/// Centralizes the candidate eligibility contract shared by boss baking and managed visual-settings resolution.
/// </summary>
internal static class EnemyBossPatternCandidateCompilationUtility
{
    #region Methods

    #region Internal Methods
    /// <summary>
    /// Determines whether one authored candidate contributes to the compiled boss candidate sequence and its visual-settings key space.
    /// Null-module candidates remain legal without an enabled module source because they intentionally clear their slot.
    /// </summary>
    /// <param name="eligibility">Selection eligibility authored for the candidate.</param>
    /// <param name="moduleMode">Whether the candidate applies a module or intentionally clears its slot.</param>
    /// <param name="moduleSourceEnabled">Whether the binding or nested interaction can supply an enabled module.</param>
    /// <returns>True when the candidate must be compiled and counted by visual-settings resolution.</returns>
    internal static bool CanCompile(EnemyBossPatternModuleCandidateEligibilityDefinition eligibility,
                                    EnemyBossPatternModuleMode moduleMode,
                                    bool moduleSourceEnabled)
    {
        // Exclude candidates that cannot participate in selection.
        if (eligibility == null || !eligibility.Enabled)
            return false;

        // Null modules intentionally clear a slot; real modules require an enabled authored source.
        return moduleMode == EnemyBossPatternModuleMode.NullModule || moduleSourceEnabled;
    }
    #endregion

    #endregion
}
