/// <summary>
/// Extends authored Procedural Level validation with the exact Scene Manager catalog identity used at runtime.
/// </summary>
public static class GameProceduralLevelRuntimeValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates authored data and requires the Procedural Level scene catalog to be the exact catalog baked by the
    /// active Scene Manager. This guard is shared by SubScene baking and the managed bootstrap fallback so room IDs
    /// cannot be generated against a catalog that runtime loading does not own.
    /// </summary>
    /// <param name="preset">Procedural preset to inspect without changing its serialized values.</param>
    /// <param name="runtimeSceneCatalog">Effective Scene Manager preset resolved by the active Game Master.</param>
    /// <returns>Complete validation report including runtime catalog compatibility.</returns>
    public static GameProceduralLevelValidationReport ValidateCompatibility(GameProceduralLevelPreset preset,
                                                                            GameSceneManagerPreset runtimeSceneCatalog)
    {
        GameProceduralLevelValidationReport report = GameProceduralLevelValidator.ValidatePreset(preset);

        if (preset == null || preset.SceneCatalogPreset == null)
            return report;

        if (runtimeSceneCatalog == null)
        {
            report.Add(GameProceduralLevelValidationCode.SceneCatalogMismatch,
                       GameProceduralLevelValidationSeverity.Error,
                       "Scene Catalog",
                       "The active Scene Manager has no runtime catalog. Assign its preset before baking procedural levels.");
            return report;
        }

        if (preset.SceneCatalogPreset != runtimeSceneCatalog)
        {
            report.Add(GameProceduralLevelValidationCode.SceneCatalogMismatch,
                       GameProceduralLevelValidationSeverity.Error,
                       "Scene Catalog",
                       "The Procedural Level catalog must reference the exact Scene Manager preset resolved by the active Game Master.");
        }

        if (report.IsValid)
            GameProceduralLevelSolvabilityUtility.AppendDiagnostics(preset, report);

        return report;
    }
    #endregion

    #endregion
}
