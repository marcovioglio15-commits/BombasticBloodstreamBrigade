using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides stateless seed, diagnostic, label and color helpers for the graph preview window.
/// </summary>
internal static class GameProceduralLevelGraphPreviewUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns a deterministic next preview seed without altering the preset's authored seed policy.
    /// </summary>
    /// <param name="seed">Current local preview seed.</param>
    /// <returns>Next local preview seed.</returns>
    public static uint NextPreviewSeed(uint seed)
    {
        return unchecked(seed * 1664525u + 1013904223u);
    }

    /// <summary>
    /// Resolves the fixed preset seed when applicable, otherwise a stable non-zero preview default.
    /// </summary>
    /// <param name="preset">Selected procedural preset.</param>
    /// <returns>Initial local preview seed.</returns>
    public static uint ResolveInitialSeed(GameProceduralLevelPreset preset)
    {
        if (preset != null &&
            preset.GenerationSettings != null &&
            preset.GenerationSettings.SeedMode == GameProceduralLevelSeedMode.Fixed)
            return preset.GenerationSettings.FixedSeed;

        return 1u;
    }

    /// <summary>
    /// Resolves the first non-null level technical ID for preset changes.
    /// </summary>
    /// <param name="preset">Selected procedural preset.</param>
    /// <returns>First valid technical ID or an empty value.</returns>
    public static string ResolveFirstLevelTechnicalId(GameProceduralLevelPreset preset)
    {
        if (preset == null)
            return string.Empty;

        for (int index = 0; index < preset.Levels.Count; index++)
        {
            if (preset.Levels[index] != null)
                return preset.Levels[index].TechnicalId;
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves a level by immutable technical ID and updates the selection to the first valid fallback when needed.
    /// </summary>
    /// <param name="preset">Procedural preset containing the authored level collection.</param>
    /// <param name="levelTechnicalId">Current selection, updated only when its authored level is unavailable.</param>
    /// <returns>Selected level definition, or null when the preset contains no valid level.</returns>
    public static GameProceduralLevelDefinition ResolveSelectedLevel(GameProceduralLevelPreset preset,
                                                                     ref string levelTechnicalId)
    {
        if (preset == null)
            return null;

        GameProceduralLevelDefinition firstValid = null;

        for (int index = 0; index < preset.Levels.Count; index++)
        {
            GameProceduralLevelDefinition level = preset.Levels[index];

            if (level == null)
                continue;

            if (firstValid == null)
                firstValid = level;

            if (string.Equals(level.TechnicalId, levelTechnicalId, System.StringComparison.Ordinal))
                return level;
        }

        if (firstValid != null)
            levelTechnicalId = firstValid.TechnicalId;

        return firstValid;
    }

    /// <summary>
    /// Runs the exact authored-data and runtime-catalog compatibility guard used before procedural ECS baking.
    /// </summary>
    /// <param name="preset">Procedural preset selected in the preview window.</param>
    /// <param name="runtimeSceneCatalog">Effective Scene Manager catalog resolved by the current Game Master.</param>
    /// <param name="report">Complete bake-equivalent validation report.</param>
    /// <returns>True only when the preset can be generated against the current runtime catalog.</returns>
    public static bool TryValidateRuntimeCompatibility(GameProceduralLevelPreset preset,
                                                       GameSceneManagerPreset runtimeSceneCatalog,
                                                       out GameProceduralLevelValidationReport report)
    {
        report = GameProceduralLevelRuntimeValidationUtility.ValidateCompatibility(preset,
                                                                                   runtimeSceneCatalog);
        return report.IsValid;
    }

    /// <summary>
    /// Resolves the first diagnostic message suitable for the compact preview status strip.
    /// </summary>
    /// <param name="report">Validation report to summarize.</param>
    /// <returns>First error, otherwise first warning, with stable code and context.</returns>
    public static string ResolveFirstValidationMessage(GameProceduralLevelValidationReport report)
    {
        GameProceduralLevelValidationDiagnostic fallback = default;
        bool hasFallback = false;

        for (int index = 0; index < report.Diagnostics.Count; index++)
        {
            GameProceduralLevelValidationDiagnostic diagnostic = report.Diagnostics[index];

            if (diagnostic.Severity == GameProceduralLevelValidationSeverity.Error)
                return FormatDiagnostic(diagnostic);

            if (!hasFallback)
            {
                fallback = diagnostic;
                hasFallback = true;
            }
        }

        return hasFallback
            ? FormatDiagnostic(fallback)
            : "No validation diagnostics.";
    }

    /// <summary>
    /// Resolves a compact blocking message while prioritizing runtime scene-catalog incompatibility diagnostics.
    /// </summary>
    /// <param name="report">Bake-equivalent validation report to summarize.</param>
    /// <returns>Runtime catalog mismatch when present, otherwise the first validation diagnostic.</returns>
    public static string ResolveBlockingValidationMessage(GameProceduralLevelValidationReport report)
    {
        for (int index = 0; index < report.Diagnostics.Count; index++)
        {
            GameProceduralLevelValidationDiagnostic diagnostic = report.Diagnostics[index];

            if (diagnostic.Code == GameProceduralLevelValidationCode.SceneCatalogMismatch)
                return FormatDiagnostic(diagnostic);
        }

        return ResolveFirstValidationMessage(report);
    }

    /// <summary>
    /// Formats selected-node portal assignments, including center-arrival edges with no target entrance.
    /// </summary>
    /// <param name="sourcePortalId">Physical source exit ID.</param>
    /// <param name="targetPortalId">Physical target entrance ID, or empty for center arrival.</param>
    /// <returns>Compact portal assignment label.</returns>
    public static string ResolvePortalLabel(string sourcePortalId, string targetPortalId)
    {
        return string.IsNullOrEmpty(targetPortalId)
            ? sourcePortalId + " → CENTER"
            : sourcePortalId + " → " + targetPortalId;
    }

    /// <summary>
    /// Returns the canvas background color appropriate for the active editor skin.
    /// </summary>
    /// <returns>Opaque canvas background color.</returns>
    public static Color ResolveCanvasColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.105f, 0.115f, 0.13f, 1f)
            : new Color(0.78f, 0.79f, 0.81f, 1f);
    }

    /// <summary>
    /// Returns a distinct role color used as the node card background tint.
    /// </summary>
    /// <param name="role">Generated room role.</param>
    /// <returns>Role-specific card color.</returns>
    public static Color ResolveRoleColor(GameProceduralRoomRole role)
    {
        switch (role)
        {
            case GameProceduralRoomRole.Start:
                return new Color(0.34f, 0.76f, 0.48f, 1f);

            case GameProceduralRoomRole.Boss:
                return new Color(0.88f, 0.35f, 0.34f, 1f);

            default:
                return new Color(0.38f, 0.58f, 0.88f, 1f);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Formats one validation diagnostic with its stable code and authored context.
    /// </summary>
    /// <param name="diagnostic">Validation diagnostic to display in the compact preview status area.</param>
    /// <returns>Formatted diagnostic message.</returns>
    private static string FormatDiagnostic(GameProceduralLevelValidationDiagnostic diagnostic)
    {
        return "[" + diagnostic.Code + "] " + diagnostic.Context + ": " + diagnostic.Message;
    }
    #endregion

    #endregion
}

/// <summary>
/// Caches the preview's bake-equivalent compatibility result and invalidates it when preset or catalog revisions change.
/// </summary>
internal sealed class GameProceduralLevelGraphPreviewCompatibilityGuard
{
    #region Fields
    private GameProceduralLevelPreset validatedPreset;
    private GameSceneManagerPreset validatedPresetCatalog;
    private GameSceneManagerPreset validatedRuntimeCatalog;
    private GameProceduralLevelValidationReport report;
    private int validatedPresetDirtyCount;
    private int validatedPresetCatalogDirtyCount;
    private int validatedRuntimeCatalogDirtyCount;
    #endregion

    #region Properties
    /// <summary>
    /// Gets the latest complete bake-equivalent validation report.
    /// </summary>
    public GameProceduralLevelValidationReport Report
    {
        get
        {
            return report;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes full validation when requested or when preset and runtime catalog identities or content revisions change.
    /// </summary>
    /// <param name="preset">Procedural preset currently selected by the preview ObjectField.</param>
    /// <param name="runtimeSceneCatalog">Effective Scene Manager catalog resolved from the live Game Master context.</param>
    /// <param name="force">True when authored values may have changed without changing their asset identities.</param>
    /// <param name="refreshed">True when callers must discard any graph generated from the previous validation state.</param>
    /// <returns>True only when the selected preset passes the same complete guard used before ECS baking.</returns>
    public bool Refresh(GameProceduralLevelPreset preset,
                        GameSceneManagerPreset runtimeSceneCatalog,
                        bool force,
                        out bool refreshed)
    {
        GameSceneManagerPreset presetSceneCatalog = preset != null ? preset.SceneCatalogPreset : null;
        int presetDirtyCount = ResolveDirtyCount(preset);
        int presetCatalogDirtyCount = ResolveDirtyCount(presetSceneCatalog);
        int runtimeCatalogDirtyCount = ResolveDirtyCount(runtimeSceneCatalog);
        refreshed = force ||
                    validatedPreset != preset ||
                    validatedPresetCatalog != presetSceneCatalog ||
                    validatedRuntimeCatalog != runtimeSceneCatalog ||
                    validatedPresetDirtyCount != presetDirtyCount ||
                    validatedPresetCatalogDirtyCount != presetCatalogDirtyCount ||
                    validatedRuntimeCatalogDirtyCount != runtimeCatalogDirtyCount;

        if (!refreshed)
            return report != null && report.IsValid;

        validatedPreset = preset;
        validatedPresetCatalog = presetSceneCatalog;
        validatedRuntimeCatalog = runtimeSceneCatalog;
        validatedPresetDirtyCount = presetDirtyCount;
        validatedPresetCatalogDirtyCount = presetCatalogDirtyCount;
        validatedRuntimeCatalogDirtyCount = runtimeCatalogDirtyCount;

        if (preset == null)
        {
            report = null;
            return false;
        }

        return GameProceduralLevelGraphPreviewUtility.TryValidateRuntimeCompatibility(preset,
                                                                                      runtimeSceneCatalog,
                                                                                      out report);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves an Editor revision counter without treating a missing optional catalog as a changed asset.
    /// </summary>
    /// <param name="asset">Preset or Scene Manager catalog whose serialized content can invalidate an open preview.</param>
    /// <returns>Current Editor dirty revision, or zero for a missing asset.</returns>
    private static int ResolveDirtyCount(UnityEngine.Object asset)
    {
        return asset != null ? EditorUtility.GetDirtyCount(asset) : 0;
    }
    #endregion

    #endregion
}
