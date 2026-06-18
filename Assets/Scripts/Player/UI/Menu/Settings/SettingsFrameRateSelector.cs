using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the authored 60/120/180 FPS segmented selector in the runtime Settings menu.
/// </summary>
[DisallowMultipleComponent]
public sealed class SettingsFrameRateSelector : MonoBehaviour
{
    #region Constants
    private const int Fps60 = 60;
    private const int Fps120 = 120;
    private const int Fps180 = 180;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Buttons")]
    [Tooltip("Button that selects a 60 FPS target frame-rate lock.")]
    [SerializeField] private Button fps60Button;

    [Tooltip("Button that selects a 120 FPS target frame-rate lock.")]
    [SerializeField] private Button fps120Button;

    [Tooltip("Button that selects a 180 FPS target frame-rate lock.")]
    [SerializeField] private Button fps180Button;

    [Header("Display")]
    [Tooltip("Optional label showing the selected frame-rate lock.")]
    [SerializeField] private TMP_Text selectedValueLabel;

    [Tooltip("Button color used by non-selected FPS options.")]
    [SerializeField] private Color normalButtonColor = new Color(0.2f, 0.28f, 0.35f, 1f);

    [Tooltip("Button color used by the selected FPS option.")]
    [SerializeField] private Color selectedButtonColor = new Color(0.36f, 0.78f, 0.52f, 1f);
    #endregion

    #region Runtime
    private int selectedFrameRate = Fps60;
    private bool suppressCallbacks;
    #endregion

    #endregion

    #region Events
    public event Action<int> FrameRateChanged;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Registers button callbacks for the authored segmented selector.
    /// </summary>
    private void OnEnable()
    {
        RegisterCallbacks();
        RefreshVisualState();
    }

    /// <summary>
    /// Removes button callbacks when the settings menu instance is disabled.
    /// </summary>
    private void OnDisable()
    {
        UnregisterCallbacks();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Updates the selector without notifying the owning Settings menu controller.
    /// </summary>
    /// <param name="frameRate">Frame-rate lock to display.</param>
    public void SetFrameRateWithoutNotify(int frameRate)
    {
        suppressCallbacks = true;
        selectedFrameRate = GameUserSettingsStore.ResolveSupportedFrameRateLimit(frameRate);
        RefreshVisualState();
        suppressCallbacks = false;
    }
    #endregion

    #region Callback Wiring
    /// <summary>
    /// Registers authored FPS option button callbacks.
    /// </summary>
    private void RegisterCallbacks()
    {
        if (fps60Button != null)
            fps60Button.onClick.AddListener(HandleFps60Pressed);

        if (fps120Button != null)
            fps120Button.onClick.AddListener(HandleFps120Pressed);

        if (fps180Button != null)
            fps180Button.onClick.AddListener(HandleFps180Pressed);
    }

    /// <summary>
    /// Removes authored FPS option button callbacks.
    /// </summary>
    private void UnregisterCallbacks()
    {
        if (fps60Button != null)
            fps60Button.onClick.RemoveListener(HandleFps60Pressed);

        if (fps120Button != null)
            fps120Button.onClick.RemoveListener(HandleFps120Pressed);

        if (fps180Button != null)
            fps180Button.onClick.RemoveListener(HandleFps180Pressed);
    }
    #endregion

    #region Button Callbacks
    /// <summary>
    /// Selects 60 FPS from the segmented selector.
    /// </summary>
    private void HandleFps60Pressed()
    {
        SelectFrameRate(Fps60);
    }

    /// <summary>
    /// Selects 120 FPS from the segmented selector.
    /// </summary>
    private void HandleFps120Pressed()
    {
        SelectFrameRate(Fps120);
    }

    /// <summary>
    /// Selects 180 FPS from the segmented selector.
    /// </summary>
    private void HandleFps180Pressed()
    {
        SelectFrameRate(Fps180);
    }
    #endregion

    #region State
    /// <summary>
    /// Applies a user-selected frame-rate lock and notifies the owning menu when needed.
    /// </summary>
    /// <param name="frameRate">Frame-rate lock requested by a button.</param>
    private void SelectFrameRate(int frameRate)
    {
        int resolvedFrameRate = GameUserSettingsStore.ResolveSupportedFrameRateLimit(frameRate);

        if (selectedFrameRate == resolvedFrameRate)
            return;

        selectedFrameRate = resolvedFrameRate;
        RefreshVisualState();

        if (suppressCallbacks)
            return;

        Action<int> changed = FrameRateChanged;

        if (changed != null)
            changed.Invoke(selectedFrameRate);
    }

    /// <summary>
    /// Refreshes labels and selected button colors after the current FPS value changes.
    /// </summary>
    private void RefreshVisualState()
    {
        SetButtonSelected(fps60Button, selectedFrameRate == Fps60);
        SetButtonSelected(fps120Button, selectedFrameRate == Fps120);
        SetButtonSelected(fps180Button, selectedFrameRate == Fps180);

        if (selectedValueLabel != null)
            selectedValueLabel.text = selectedFrameRate + " FPS";
    }

    /// <summary>
    /// Applies the selected or normal color to one FPS option button.
    /// </summary>
    /// <param name="button">Button to update.</param>
    /// <param name="selected">True when this button represents the current frame-rate lock.</param>
    private void SetButtonSelected(Button button, bool selected)
    {
        if (button == null)
            return;

        Graphic targetGraphic = button.targetGraphic;

        if (targetGraphic != null)
            targetGraphic.color = selected ? selectedButtonColor : normalButtonColor;

        ColorBlock colors = button.colors;
        Color resolvedColor = selected ? selectedButtonColor : normalButtonColor;
        colors.normalColor = resolvedColor;
        colors.highlightedColor = resolvedColor;
        colors.selectedColor = resolvedColor;
        button.colors = colors;
    }
    #endregion

    #endregion
}
