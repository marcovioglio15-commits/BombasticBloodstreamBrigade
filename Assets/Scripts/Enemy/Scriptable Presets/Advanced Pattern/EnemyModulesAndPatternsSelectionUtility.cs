using System.Collections.Generic;

/// <summary>
/// Resolves the currently selected shared pattern from one advanced-pattern preset loadout.
/// </summary>
internal static class EnemyModulesAndPatternsSelectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the first valid selected shared pattern from the active loadout.
    /// </summary>
    /// <param name="preset">Advanced-pattern preset that stores the active loadout.</param>
    /// <returns>The first valid selected shared pattern, or null when none can be resolved.</returns>
    public static EnemyModulesPatternDefinition ResolveSelectedPattern(EnemyAdvancedPatternPreset preset)
    {
        if (preset == null)
            return null;

        EnemyModulesAndPatternsPreset sharedPreset = preset.ModulesAndPatternsPreset;

        if (sharedPreset == null)
            return null;

        IReadOnlyList<string> activePatternIds = preset.ActivePatternIds;

        for (int index = 0; index < activePatternIds.Count; index++)
        {
            string patternId = activePatternIds[index];
            EnemyModulesPatternDefinition pattern = sharedPreset.ResolvePatternById(patternId);

            if (pattern != null)
                return pattern;
        }

        if (sharedPreset.Patterns.Count <= 0)
            return null;

        return sharedPreset.Patterns[0];
    }
    #endregion

    #endregion
}
