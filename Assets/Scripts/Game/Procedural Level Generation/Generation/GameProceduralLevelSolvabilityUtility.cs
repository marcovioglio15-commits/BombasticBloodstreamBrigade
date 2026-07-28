using System.Collections.Generic;

/// <summary>
/// Proves authored enabled levels against fixed or representative authoritative seeds before bake and Play Mode.
/// </summary>
public static class GameProceduralLevelSolvabilityUtility
{
    #region Constants
    private const uint GoldenRatioSeed = 0x9E3779B9u;
    #endregion

    #region Fields

    #region Readonly Fields
    private static readonly uint[] representativeSeeds =
    {
        0u,
        1u,
        GoldenRatioSeed,
        uint.MaxValue
    };
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends one actionable error for each enabled level and validation seed that cannot produce a complete graph.
    /// </summary>
    /// <param name="preset">Structurally valid authored preset to prove without mutating it.</param>
    /// <param name="report">Validation report receiving deterministic solvability diagnostics.</param>
    public static void AppendDiagnostics(GameProceduralLevelPreset preset,
                                         GameProceduralLevelValidationReport report)
    {
        if (preset == null || report == null || preset.GenerationSettings == null || !report.IsValid)
            return;

        IReadOnlyList<uint> seeds = ResolveValidationSeeds(preset.GenerationSettings);

        // Validate every enabled level independently because runtime generates each from the same run seed.
        for (int levelIndex = 0; levelIndex < preset.Levels.Count; levelIndex++)
        {
            GameProceduralLevelDefinition level = preset.Levels[levelIndex];

            if (level == null || !level.Enabled)
                continue;

            for (int seedIndex = 0; seedIndex < seeds.Count; seedIndex++)
            {
                uint seed = seeds[seedIndex];
                GameProceduralLevelGenerationResult result =
                    GameProceduralLevelSolver.Generate(preset, level, seed);

                if (result.Success)
                    continue;

                report.Add(
                    GameProceduralLevelValidationCode.GenerationSeedUnsolvable,
                    GameProceduralLevelValidationSeverity.Error,
                    "Level '" + level.LevelId + "' / seed " + seed,
                    "The bounded solver cannot produce a safe complete graph. [" +
                    result.FailureCode + "] " + result.Diagnostic);
            }
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the exact fixed seed or a conservative deterministic suite for runtime-provided seed modes.
    /// </summary>
    /// <param name="settings">Validated global generation settings.</param>
    /// <returns>Read-only seed collection used by pre-bake and pre-Play checks.</returns>
    private static IReadOnlyList<uint> ResolveValidationSeeds(
        GameProceduralLevelGenerationSettings settings)
    {
        if (settings.SeedMode == GameProceduralLevelSeedMode.Fixed)
            return new uint[] { settings.FixedSeed };

        return representativeSeeds;
    }
    #endregion

    #endregion
}
