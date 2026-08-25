using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls the two authored Settings macro panels and their deterministic initial selections.
/// </summary>
internal sealed class SettingsMenuTabController
{
    #region Constants
    private const int AudioPanelIndex = 0;
    private const int GameplayPanelIndex = 1;
    private const int PanelCount = 2;
    #endregion

    #region Fields
    private readonly GameObject audioPanelRoot;
    private readonly GameObject gameplayPanelRoot;
    private readonly EventSystem eventSystemOverride;
    private readonly Slider masterVolumeSlider;
    private readonly Toggle visualPointerToggle;
    private readonly Button audioTabButton;
    private readonly Button gameplayTabButton;
    private readonly Button confirmButton;
    private int activePanelIndex;
    #endregion

    #region Constructors
    /// <summary>
    /// Captures authored panel and selection references without creating runtime UI.
    /// </summary>
    /// <param name="audioPanelRootValue">Audio panel root.</param>
    /// <param name="gameplayPanelRootValue">Gameplay panel root.</param>
    /// <param name="eventSystemOverrideValue">Optional EventSystem override.</param>
    /// <param name="masterVolumeSliderValue">Preferred first Audio control.</param>
    /// <param name="visualPointerToggleValue">Preferred first Gameplay control.</param>
    /// <param name="audioTabButtonValue">Audio tab fallback.</param>
    /// <param name="gameplayTabButtonValue">Gameplay tab fallback.</param>
    /// <param name="confirmButtonValue">Shared final fallback.</param>
    public SettingsMenuTabController(GameObject audioPanelRootValue,
                                     GameObject gameplayPanelRootValue,
                                     EventSystem eventSystemOverrideValue,
                                     Slider masterVolumeSliderValue,
                                     Toggle visualPointerToggleValue,
                                     Button audioTabButtonValue,
                                     Button gameplayTabButtonValue,
                                     Button confirmButtonValue)
    {
        audioPanelRoot = audioPanelRootValue;
        gameplayPanelRoot = gameplayPanelRootValue;
        eventSystemOverride = eventSystemOverrideValue;
        masterVolumeSlider = masterVolumeSliderValue;
        visualPointerToggle = visualPointerToggleValue;
        audioTabButton = audioTabButtonValue;
        gameplayTabButton = gameplayTabButtonValue;
        confirmButton = confirmButtonValue;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Shows one macro panel and optionally moves focus to its first useful control.
    /// </summary>
    /// <param name="panelIndex">Zero-based macro panel index.</param>
    /// <param name="focusFirstControl">True to update EventSystem selection.</param>
    public void Show(int panelIndex, bool focusFirstControl)
    {
        activePanelIndex = Mathf.Clamp(panelIndex, AudioPanelIndex, GameplayPanelIndex);

        if (audioPanelRoot != null)
            audioPanelRoot.SetActive(activePanelIndex == AudioPanelIndex);

        if (gameplayPanelRoot != null)
            gameplayPanelRoot.SetActive(activePanelIndex == GameplayPanelIndex);

        if (focusFirstControl)
            SettingsMenuControllerUiUtility.SelectSelectable(eventSystemOverride, ResolveDefault(activePanelIndex));
    }

    /// <summary>
    /// Selects an adjacent macro panel using the requested boundary policy.
    /// </summary>
    /// <param name="direction">Negative for previous or positive for next.</param>
    /// <param name="wrap">True to continue across panel boundaries.</param>
    public void Step(int direction, bool wrap)
    {
        if (direction == 0)
            return;

        int targetIndex = SettingsMenuControllerUiUtility.ResolveAdjacentTabIndex(activePanelIndex,
                                                                                  direction,
                                                                                  PanelCount,
                                                                                  wrap);

        if (targetIndex != activePanelIndex)
            Show(targetIndex, true);
    }

    /// <summary>
    /// Resolves the first useful selectable for one macro panel.
    /// </summary>
    /// <param name="panelIndex">Zero-based macro panel index.</param>
    /// <returns>First available control with tab and confirmation fallbacks.</returns>
    public Selectable ResolveDefault(int panelIndex)
    {
        return SettingsMenuControllerUiUtility.ResolvePanelDefault(panelIndex == GameplayPanelIndex,
                                                                   masterVolumeSlider,
                                                                   visualPointerToggle,
                                                                   audioTabButton,
                                                                   gameplayTabButton,
                                                                   confirmButton);
    }
    #endregion

    #endregion
}
