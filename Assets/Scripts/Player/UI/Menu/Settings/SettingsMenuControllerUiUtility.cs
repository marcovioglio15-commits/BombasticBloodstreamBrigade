using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Stateless UI helpers shared by the runtime Settings menu controller.
/// </summary>
internal static class SettingsMenuControllerUiUtility
{
    #region Methods

    #region Control Values
    /// <summary>
    /// Sets one slider value without dispatching UI callbacks.
    /// </summary>
    /// <param name="slider">Slider to update.</param>
    /// <param name="value">Value to assign.</param>
    public static void SetSliderValue(Slider slider, float value)
    {
        if (slider == null)
            return;

        slider.SetValueWithoutNotify(value);
    }

    /// <summary>
    /// Sets one toggle value without dispatching UI callbacks.
    /// </summary>
    /// <param name="toggle">Toggle to update.</param>
    /// <param name="enabled">Value to assign.</param>
    public static void SetToggleValue(Toggle toggle, bool enabled)
    {
        if (toggle == null)
            return;

        toggle.SetIsOnWithoutNotify(enabled);
    }

    /// <summary>
    /// Sets one segmented frame-rate selector without dispatching UI callbacks.
    /// </summary>
    /// <param name="selector">Frame-rate selector to update.</param>
    /// <param name="frameRate">Frame-rate value to display.</param>
    public static void SetFrameRateValue(SettingsFrameRateSelector selector, int frameRate)
    {
        if (selector == null)
            return;

        selector.SetFrameRateWithoutNotify(frameRate);
    }
    #endregion

    #region Labels
    /// <summary>
    /// Formats a normalized value as a percentage label.
    /// </summary>
    /// <param name="label">Optional label to update.</param>
    /// <param name="value">Normalized value to display.</param>
    public static void SetPercentLabel(TMP_Text label, float value)
    {
        if (label == null)
            return;

        label.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    /// <summary>
    /// Formats a multiplier value for rumble settings.
    /// </summary>
    /// <param name="label">Optional label to update.</param>
    /// <param name="value">Multiplier value to display.</param>
    public static void SetMultiplierLabel(TMP_Text label, float value)
    {
        if (label == null)
            return;

        label.text = Mathf.Clamp(value, 0f, 2f).ToString("0.00") + "x";
    }
    #endregion

    #region Selection
    /// <summary>
    /// Resolves the adjacent macro-tab index with optional boundary wrapping.
    /// </summary>
    /// <param name="currentIndex">Current zero-based tab index.</param>
    /// <param name="direction">Negative for previous or positive for next.</param>
    /// <param name="tabCount">Number of available macro tabs.</param>
    /// <param name="wrap">True to continue from one boundary to the opposite boundary.</param>
    /// <returns>Valid target tab index.</returns>
    public static int ResolveAdjacentTabIndex(int currentIndex, int direction, int tabCount, bool wrap)
    {
        int safeCount = Mathf.Max(1, tabCount);
        int targetIndex = Mathf.Clamp(currentIndex, 0, safeCount - 1) + System.Math.Sign(direction);

        if (wrap)
            return (targetIndex + safeCount) % safeCount;

        return Mathf.Clamp(targetIndex, 0, safeCount - 1);
    }

    /// <summary>
    /// Resolves the first useful control for the active Settings macro tab.
    /// </summary>
    /// <param name="gameplayPanel">True when resolving the Gameplay tab.</param>
    /// <param name="masterVolumeSlider">Preferred first Audio control.</param>
    /// <param name="visualPointerToggle">Preferred first Gameplay control.</param>
    /// <param name="audioTabButton">Audio tab fallback.</param>
    /// <param name="gameplayTabButton">Gameplay tab fallback.</param>
    /// <param name="confirmButton">Shared final fallback.</param>
    /// <returns>First available selectable for the requested tab.</returns>
    public static Selectable ResolvePanelDefault(bool gameplayPanel,
                                                 Slider masterVolumeSlider,
                                                 Toggle visualPointerToggle,
                                                 Button audioTabButton,
                                                 Button gameplayTabButton,
                                                 Button confirmButton)
    {
        if (gameplayPanel)
            return visualPointerToggle != null ? visualPointerToggle : gameplayTabButton != null ? gameplayTabButton : confirmButton;

        return masterVolumeSlider != null ? masterVolumeSlider : audioTabButton != null ? audioTabButton : confirmButton;
    }

    /// <summary>
    /// Applies UI focus to one selectable through the resolved EventSystem.
    /// </summary>
    /// <param name="eventSystemOverride">Optional EventSystem override.</param>
    /// <param name="selectable">Selectable that should own focus.</param>
    public static void SelectSelectable(EventSystem eventSystemOverride, Selectable selectable)
    {
        if (selectable == null)
            return;

        if (!selectable.gameObject.activeInHierarchy || !selectable.IsInteractable())
            return;

        EventSystem resolvedEventSystem = eventSystemOverride != null ? eventSystemOverride : EventSystem.current;

        if (resolvedEventSystem == null)
            return;

        Canvas.ForceUpdateCanvases();
        resolvedEventSystem.SetSelectedGameObject(null);
        selectable.Select();
        resolvedEventSystem.SetSelectedGameObject(selectable.gameObject);
    }
    #endregion

    #endregion
}
