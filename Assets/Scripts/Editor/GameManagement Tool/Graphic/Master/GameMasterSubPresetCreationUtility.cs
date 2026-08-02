/// <summary>
/// Creates standalone Game Management sub-presets and opens their dedicated editor panels.
/// </summary>
public static class GameMasterSubPresetCreationUtility
{
    #region Constants
    private const string DifficultyScalingFolder = "Assets/Scriptable Objects/Game/Difficulty Scaling";
    private const string WavesFolder = "Assets/Scriptable Objects/Game/Waves";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates, initializes and assigns a Difficulty Scaling preset to the selected Game Master preset.
    /// </summary>
    /// <param name="panel">Owning panel with selected Game Master preset context.</param>
    public static void CreateDifficultyScalingPreset(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        GameDifficultyScalingPreset newPreset =
            GameManagementStandalonePresetAssetUtility.CreateAsset<GameDifficultyScalingPreset>(
                DifficultyScalingFolder,
                "GameDifficultyScalingPreset",
                createdPreset => createdPreset.EnsureInitialized());

        if (newPreset == null)
            return;

        GameMasterPresetsPanelSectionsUtility.AssignSubPreset(panel, "difficultyScalingPreset", newPreset);
        panel.OpenSidePanel(GameManagementWindow.PanelType.DifficultyScaling);

        if (panel.SidePanels.TryGetValue(GameManagementWindow.PanelType.DifficultyScaling,
                                         out GameMasterPresetsPanel.SidePanelEntry sidePanelEntry) &&
            sidePanelEntry.DifficultyScalingPanel != null)
        {
            sidePanelEntry.DifficultyScalingPanel.SelectPresetFromExternal(newPreset);
        }
    }

    /// <summary>
    /// Creates, initializes and assigns a Waves preset to the selected Game Master preset.
    /// </summary>
    /// <param name="panel">Owning panel with selected Game Master preset context.</param>
    public static void CreateWavesPreset(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        GameWavesPreset newPreset =
            GameManagementStandalonePresetAssetUtility.CreateAsset<GameWavesPreset>(
                WavesFolder,
                "GameWavesPreset",
                createdPreset => createdPreset.EnsureInitialized());

        if (newPreset == null)
            return;

        GameMasterPresetsPanelSectionsUtility.AssignSubPreset(panel, "wavesPreset", newPreset);
        panel.OpenSidePanel(GameManagementWindow.PanelType.Waves);

        if (panel.SidePanels.TryGetValue(GameManagementWindow.PanelType.Waves,
                                         out GameMasterPresetsPanel.SidePanelEntry sidePanelEntry) &&
            sidePanelEntry.WavesPanel != null)
        {
            sidePanelEntry.WavesPanel.SelectPresetFromExternal(newPreset);
        }
    }
    #endregion

    #endregion
}
