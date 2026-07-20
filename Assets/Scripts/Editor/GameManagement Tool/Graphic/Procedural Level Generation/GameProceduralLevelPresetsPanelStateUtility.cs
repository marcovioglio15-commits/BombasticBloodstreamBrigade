using UnityEditor;

/// <summary>
/// Persists Procedural Level editor navigation using stable asset and nested technical identifiers.
/// </summary>
internal static class GameProceduralLevelPresetsPanelStateUtility
{
    #region Constants
    private const string ActiveSectionStateKey = "NashCore.GameManagement.ProceduralLevel.ActiveSection";
    private const string SelectedLevelStateKeyPrefix = "NashCore.GameManagement.ProceduralLevel.SelectedLevel.";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the last visible Procedural Level details section.
    /// </summary>
    /// <returns>Persisted section or Metadata when no valid state exists.</returns>
    public static GameProceduralLevelPresetsPanel.DetailsSectionType LoadActiveSection()
    {
        return ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, GameProceduralLevelPresetsPanel.DetailsSectionType.Metadata);
    }

    /// <summary>
    /// Saves the active Procedural Level details section for the next tool session.
    /// </summary>
    /// <param name="section">Section currently shown by the panel.</param>
    public static void SaveActiveSection(GameProceduralLevelPresetsPanel.DetailsSectionType section)
    {
        ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, section);
    }

    /// <summary>
    /// Loads the selected nested level technical ID for one preset asset.
    /// </summary>
    /// <param name="preset">Preset whose nested selection should be restored.</param>
    /// <returns>Persisted technical ID, or an empty string when no state exists.</returns>
    public static string LoadSelectedLevelTechnicalId(GameProceduralLevelPreset preset)
    {
        if (preset == null || string.IsNullOrWhiteSpace(preset.PresetId))
            return string.Empty;

        return EditorPrefs.GetString(SelectedLevelStateKeyPrefix + preset.PresetId, string.Empty);
    }

    /// <summary>
    /// Saves one selected nested level by immutable technical ID rather than mutable list index.
    /// </summary>
    /// <param name="preset">Preset that owns the level.</param>
    /// <param name="levelTechnicalId">Stable level technical ID to persist.</param>
    public static void SaveSelectedLevelTechnicalId(GameProceduralLevelPreset preset, string levelTechnicalId)
    {
        if (preset == null || string.IsNullOrWhiteSpace(preset.PresetId))
            return;

        string stateKey = SelectedLevelStateKeyPrefix + preset.PresetId;

        if (string.IsNullOrWhiteSpace(levelTechnicalId))
        {
            EditorPrefs.DeleteKey(stateKey);
            return;
        }

        EditorPrefs.SetString(stateKey, levelTechnicalId);
    }
    #endregion

    #endregion
}
