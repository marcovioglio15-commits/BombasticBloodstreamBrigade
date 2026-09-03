using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static SettingsMenuControllerUiUtility;

/// <summary>
/// Controls the reusable runtime Settings menu used by the main menu and the gameplay pause menu.
/// </summary>
[DisallowMultipleComponent]
public sealed class SettingsMenuController : MonoBehaviour
{
    #region Types
    private enum SettingsPanel
    {
        Audio = 0,
        Gameplay = 1,
        Dev = 2
    }
    #endregion

    #region Fields

    #region Serialized Fields - Root
    [Header("Root")]
    [Tooltip("Full-screen panel root toggled when the Settings menu opens or closes.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Optional EventSystem override used for default selection and focus recovery.")]
    [SerializeField] private EventSystem eventSystemOverride;
    #endregion

    #region Serialized Fields - Navigation
    [Header("Navigation")]
    [Tooltip("Button that shows the Audio settings panel.")]
    [SerializeField] private Button audioTabButton;

    [Tooltip("Button that shows the gameplay settings panel.")]
    [FormerlySerializedAs("experienceCustomizationTabButton")]
    [SerializeField] private Button gameplayTabButton;

    [Tooltip("Button that saves the current settings draft and closes the menu.")]
    [SerializeField] private Button confirmButton;

    [Tooltip("Button that restores every setting control to its default value.")]
    [SerializeField] private Button resetDefaultsButton;

    [Tooltip("Button that closes the menu without saving the current draft.")]
    [SerializeField] private Button closeButton;

    [Tooltip("Shared project Input Action asset used by Settings navigation before a player runtime input clone exists.")]
    [SerializeField] private InputActionAsset navigationInputAsset;

    [Tooltip("Controller for the authored Dev tab, account flow and configurable reveal Input Action.")]
    [SerializeField] private SettingsDevSectionController devSectionController;
    #endregion

    #region Serialized Fields - Panels
    [Header("Panels")]
    [Tooltip("Root object containing audio settings controls.")]
    [SerializeField] private GameObject audioPanelRoot;

    [Tooltip("Root object containing gameplay controls.")]
    [FormerlySerializedAs("experienceCustomizationPanelRoot")]
    [SerializeField] private GameObject gameplayPanelRoot;
    #endregion

    #region Serialized Fields - Audio Controls
    [Header("Audio Controls")]
    [Tooltip("Slider controlling the FMOD master bus volume.")]
    [SerializeField] private Slider masterVolumeSlider;

    [Tooltip("Slider controlling the FMOD SFX bus volume.")]
    [SerializeField] private Slider sfxVolumeSlider;

    [Tooltip("Slider controlling the FMOD music bus volume.")]
    [SerializeField] private Slider musicVolumeSlider;

    [Tooltip("Optional label showing the current master volume percentage.")]
    [SerializeField] private TMP_Text masterVolumeValueLabel;

    [Tooltip("Optional label showing the current SFX volume percentage.")]
    [SerializeField] private TMP_Text sfxVolumeValueLabel;

    [Tooltip("Optional label showing the current music volume percentage.")]
    [SerializeField] private TMP_Text musicVolumeValueLabel;
    #endregion

    #region Serialized Fields - Gameplay Controls
    [Header("Gameplay Controls")]
    [Tooltip("Toggle that allows the user to hide the player visual pointer even when the active visual preset bakes it.")]
    [SerializeField] private Toggle visualPointerToggle;

    [Tooltip("Toggle that switches between fullscreen and windowed presentation.")]
    [SerializeField] private Toggle fullscreenToggle;

    [Tooltip("Segmented control that locks Application.targetFrameRate to 60, 120 or 180 FPS.")]
    [SerializeField] private SettingsFrameRateSelector frameRateSelector;

    [Tooltip("Slider multiplying the authored damage feedback rumble intensity.")]
    [SerializeField] private Slider damageRumbleMultiplierSlider;

    [Tooltip("Slider multiplying the authored fire feedback rumble intensity.")]
    [SerializeField] private Slider fireRumbleMultiplierSlider;

    [Tooltip("Optional label showing the current damage rumble multiplier.")]
    [SerializeField] private TMP_Text damageRumbleValueLabel;

    [Tooltip("Optional label showing the current fire rumble multiplier.")]
    [SerializeField] private TMP_Text fireRumbleValueLabel;
    #endregion

    #region Serialized Fields - Audio Runtime
    [Header("Audio Runtime")]
    [Tooltip("FMOD master bus path controlled by the Master slider.")]
    [SerializeField] private string masterBusPath = "bus:/";

    [Tooltip("FMOD SFX bus path controlled by the SFX slider.")]
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    [Tooltip("FMOD music bus path controlled by the Music slider.")]
    [SerializeField] private string musicBusPath = "bus:/Music";

    [Tooltip("FMOD event path previewed while the Master slider is adjusted. Ignored when Master previews all other slider events.")]
    [SerializeField] private string masterPreviewEventPath = "event:/SFX/Weapon/SFX_Shoot_Projectile";

    [Tooltip("Optional FMOD bank loaded before the Master preview event is resolved.")]
    [SerializeField] private string masterPreviewBankName = "BankSounds";

    [Tooltip("Fallback used only before the baked Settings Manager config exists: when enabled the Master slider previews the SFX and Music events together instead of its own event. The baked Settings Manager preset value overrides this at runtime.")]
    [SerializeField] private bool masterPreviewPlaysAllOthers;

    [Tooltip("FMOD event path previewed while the SFX slider is adjusted.")]
    [SerializeField] private string sfxPreviewEventPath = "event:/SFX/Weapon/SFX_Shoot_Projectile";

    [Tooltip("Optional FMOD bank loaded before the SFX preview event is resolved.")]
    [SerializeField] private string sfxPreviewBankName = "BankSounds";

    [Tooltip("FMOD event path previewed while the Music slider is adjusted.")]
    [SerializeField] private string musicPreviewEventPath = "event:/MUSIC/mus_past";

    [Tooltip("Optional FMOD bank loaded before the Music preview event is resolved.")]
    [SerializeField] private string musicPreviewBankName = "BankMusic";

    [Tooltip("Seconds after the last preview trigger before a tracked preview voice is faded out and stopped.")]
    [SerializeField] private float audioPreviewStopDelaySeconds = 0.8f;

    [Tooltip("Seconds a freshly started slider preview takes to fade in from silence so it never clicks on start.")]
    [SerializeField] private float audioPreviewFadeInSeconds = 0.04f;

    [Tooltip("Seconds a stopped slider preview takes to fade out before it is released so it never cuts abruptly.")]
    [SerializeField] private float audioPreviewFadeOutSeconds = 0.18f;
    #endregion

    #region Runtime
    private SettingsMenuInputActionNavigationController inputActionNavigation;
    private SettingsMenuTabController tabController;
    private GameUserSettingsData savedSettings;
    private GameUserSettingsData draftSettings;
    private GameUserSettingsRuntimeOptions runtimeOptions;
    private GameAudioSettingsPreviewSet audioPreviewSet;
    private Selectable restoreSelectionTarget;
    private SettingsMenuAudioPreviewStopController audioPreviewStopController;
    private bool suppressControlCallbacks;
    private bool wrapSettingsTabs;
    #endregion

    #endregion

    #region Events
    public event Action MenuClosed;
    #endregion

    #region Properties
    public bool IsOpen
    {
        get
        {
            return panelRoot != null && panelRoot.activeSelf;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Applies saved runtime settings as soon as the prefab instance becomes active in a loaded UI scene.
    /// </summary>
    private void Awake()
    {
        inputActionNavigation = new SettingsMenuInputActionNavigationController(StepSettingsPanel, CancelAndClose);
        tabController = new SettingsMenuTabController(audioPanelRoot,
                                                      gameplayPanelRoot,
                                                      eventSystemOverride,
                                                      masterVolumeSlider,
                                                      visualPointerToggle,
                                                      audioTabButton,
                                                      gameplayTabButton,
                                                      confirmButton,
                                                      devSectionController);
        GameAudioSettingsFmodRuntimeUtility.ConfigurePreviewFades(audioPreviewFadeInSeconds, audioPreviewFadeOutSeconds);
        audioPreviewStopController = new SettingsMenuAudioPreviewStopController(this, audioPreviewStopDelaySeconds);
        RefreshRuntimeConfig();
        savedSettings = GameUserSettingsRuntimeUtility.LoadAndApply(in runtimeOptions);
        draftSettings = savedSettings;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Advances the slider preview fade ramps while the menu is open, on unscaled time so fades keep progressing
    /// while gameplay is paused.
    /// </summary>
    private void Update()
    {
        if (!IsOpen)
            return;

        float deltaSeconds = Time.unscaledDeltaTime;
        GameAudioSettingsFmodRuntimeUtility.TickPreviewFades(deltaSeconds);

        if (inputActionNavigation != null)
            inputActionNavigation.Tick(deltaSeconds);
    }

    /// <summary>
    /// Registers authored UI callbacks.
    /// </summary>
    private void OnEnable()
    {
        RegisterCallbacks();
    }

    /// <summary>
    /// Removes authored UI callbacks and stops any preview voice owned by this menu.
    /// </summary>
    private void OnDisable()
    {
        UnregisterCallbacks();
        audioPreviewStopController?.StopNow();

        if (inputActionNavigation != null)
            inputActionNavigation.Deactivate();

        if (devSectionController != null)
            devSectionController.Deactivate();
    }

    /// <summary>
    /// Releases the Input Action navigation controller owned by this Settings menu.
    /// </summary>
    private void OnDestroy()
    {
        if (inputActionNavigation != null)
            inputActionNavigation.Dispose();

        audioPreviewStopController?.Dispose();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Opens the menu using the saved settings as the new editable draft.
    /// </summary>
    /// <param name="selectionTarget">Selectable that should regain focus when the menu closes.</param>
    public void Open(Selectable selectionTarget)
    {
        restoreSelectionTarget = selectionTarget;
        RefreshRuntimeConfig();
        savedSettings = GameUserSettingsStore.Load(in runtimeOptions);
        draftSettings = savedSettings;
        ApplyDraftToControls();
        if (panelRoot != null)
            panelRoot.SetActive(true);

        ApplyDraftToRuntime();
        if (devSectionController != null)
            devSectionController.Activate(navigationInputAsset);

        tabController.Show((int)SettingsPanel.Audio, false);

        Selectable defaultSelectable = tabController.ResolveDefault((int)SettingsPanel.Audio);
        GameHudSettingsNavigationRuntimeConfig navigationConfig;

        bool directNavigationActive = false;

        if (inputActionNavigation != null &&
            GameHudSettingsNavigationRuntimeUtility.TryResolve(out navigationConfig))
        {
            wrapSettingsTabs = navigationConfig.WrapTabs != 0;
            directNavigationActive = inputActionNavigation.Activate(in navigationConfig,
                                                                     panelRoot,
                                                                     defaultSelectable,
                                                                     eventSystemOverride,
                                                                     navigationInputAsset,
                                                                     audioTabButton,
                                                                     gameplayTabButton,
                                                                     devSectionController != null
                                                                         ? devSectionController.TabButton
                                                                         : null);
        }

        if (!directNavigationActive)
            SettingsMenuControllerUiUtility.SelectSelectable(eventSystemOverride, defaultSelectable);
    }

    /// <summary>
    /// Closes the menu without saving the current draft and restores the saved settings snapshot.
    /// </summary>
    public void CancelAndClose()
    {
        draftSettings = savedSettings;
        ApplyDraftToRuntime();
        CloseMenu();
    }
    #endregion

    #region Callback Wiring
    /// <summary>
    /// Registers all button, slider and toggle callbacks.
    /// </summary>
    private void RegisterCallbacks()
    {
        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(HandleAudioTabPressed);

        if (gameplayTabButton != null)
            gameplayTabButton.onClick.AddListener(HandleGameplayTabPressed);

        if (devSectionController != null && devSectionController.TabButton != null)
            devSectionController.TabButton.onClick.AddListener(HandleDevTabPressed);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(HandleConfirmPressed);

        if (resetDefaultsButton != null)
            resetDefaultsButton.onClick.AddListener(HandleResetDefaultsPressed);

        if (closeButton != null)
            closeButton.onClick.AddListener(CancelAndClose);

        RegisterValueControls();
    }

    /// <summary>
    /// Removes all button, slider and toggle callbacks.
    /// </summary>
    private void UnregisterCallbacks()
    {
        if (audioTabButton != null)
            audioTabButton.onClick.RemoveListener(HandleAudioTabPressed);

        if (gameplayTabButton != null)
            gameplayTabButton.onClick.RemoveListener(HandleGameplayTabPressed);

        if (devSectionController != null && devSectionController.TabButton != null)
            devSectionController.TabButton.onClick.RemoveListener(HandleDevTabPressed);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(HandleConfirmPressed);

        if (resetDefaultsButton != null)
            resetDefaultsButton.onClick.RemoveListener(HandleResetDefaultsPressed);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CancelAndClose);

        UnregisterValueControls();
    }

    /// <summary>
    /// Registers value-control callbacks for draft updates and live preview.
    /// </summary>
    private void RegisterValueControls()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);

        if (visualPointerToggle != null)
            visualPointerToggle.onValueChanged.AddListener(HandleVisualPointerChanged);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);

        if (damageRumbleMultiplierSlider != null)
            damageRumbleMultiplierSlider.onValueChanged.AddListener(HandleDamageRumbleChanged);

        if (fireRumbleMultiplierSlider != null)
            fireRumbleMultiplierSlider.onValueChanged.AddListener(HandleFireRumbleChanged);

        if (frameRateSelector != null)
            frameRateSelector.FrameRateChanged += HandleFrameRateChanged;
    }

    /// <summary>
    /// Removes value-control callbacks.
    /// </summary>
    private void UnregisterValueControls()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);

        if (visualPointerToggle != null)
            visualPointerToggle.onValueChanged.RemoveListener(HandleVisualPointerChanged);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(HandleFullscreenChanged);

        if (damageRumbleMultiplierSlider != null)
            damageRumbleMultiplierSlider.onValueChanged.RemoveListener(HandleDamageRumbleChanged);

        if (fireRumbleMultiplierSlider != null)
            fireRumbleMultiplierSlider.onValueChanged.RemoveListener(HandleFireRumbleChanged);

        if (frameRateSelector != null)
            frameRateSelector.FrameRateChanged -= HandleFrameRateChanged;
    }
    #endregion

    #region Button Callbacks
    /// <summary>
    /// Shows the Audio settings panel.
    /// </summary>
    private void HandleAudioTabPressed()
    {
        tabController.Show((int)SettingsPanel.Audio, true);
    }

    /// <summary>
    /// Shows the gameplay settings panel.
    /// </summary>
    private void HandleGameplayTabPressed()
    {
        tabController.Show((int)SettingsPanel.Gameplay, true);
    }

    /// <summary>
    /// Shows the Dev account and dashboard panel.
    /// </summary>
    private void HandleDevTabPressed()
    {
        tabController.Show((int)SettingsPanel.Dev, true);
    }

    /// <summary>
    /// Persists the current draft and closes the menu.
    /// </summary>
    private void HandleConfirmPressed()
    {
        savedSettings = GameUserSettingsStore.ClampForRuntime(draftSettings);
        GameUserSettingsRuntimeUtility.SaveAndApply(savedSettings, in runtimeOptions);
        CloseMenu();
    }

    /// <summary>
    /// Restores the editable draft to project defaults and applies them live for immediate feedback.
    /// </summary>
    private void HandleResetDefaultsPressed()
    {
        draftSettings = GameUserSettingsStore.CreateDefaults(in runtimeOptions);
        ApplyDraftToControls();
        ApplyDraftToRuntime();
        SettingsMenuControllerUiUtility.SelectSelectable(eventSystemOverride, confirmButton != null ? confirmButton : resetDefaultsButton);
    }
    #endregion

    #region Value Callbacks
    /// <summary>
    /// Updates the draft master volume and plays its preview event.
    /// </summary>
    /// <param name="value">Slider value in the normalized 0..1 range.</param>
    private void HandleMasterVolumeChanged(float value)
    {
        if (suppressControlCallbacks)
            return;

        draftSettings.MasterVolume = value;
        ApplyDraftToRuntime();
        RefreshValueLabels();
        SettingsMenuAudioPreviewUtility.PlayMasterPreview(in audioPreviewSet,
                                                          value,
                                                          audioPreviewStopController.Queue);
    }

    /// <summary>
    /// Updates the draft SFX volume and plays its preview event.
    /// </summary>
    /// <param name="value">Slider value in the normalized 0..1 range.</param>
    private void HandleSfxVolumeChanged(float value)
    {
        if (suppressControlCallbacks)
            return;

        draftSettings.SfxVolume = value;
        ApplyDraftToRuntime();
        RefreshValueLabels();
        SettingsMenuAudioPreviewUtility.PlayPreview(audioPreviewSet.Sfx, value, audioPreviewStopController.Queue);
    }

    /// <summary>
    /// Updates the draft music volume and plays its preview event.
    /// </summary>
    /// <param name="value">Slider value in the normalized 0..1 range.</param>
    private void HandleMusicVolumeChanged(float value)
    {
        if (suppressControlCallbacks)
            return;

        draftSettings.MusicVolume = value;
        ApplyDraftToRuntime();
        RefreshValueLabels();
        SettingsMenuAudioPreviewUtility.PlayPreview(audioPreviewSet.Music, value, audioPreviewStopController.Queue);
    }

    /// <summary>
    /// Updates whether the baked player visual pointer may render.
    /// </summary>
    /// <param name="enabled">True when the pointer is allowed by user settings.</param>
    private void HandleVisualPointerChanged(bool enabled)
    {
        if (suppressControlCallbacks)
            return;

        draftSettings.VisualPointerEnabled = enabled ? (byte)1 : (byte)0;
        ApplyDraftToRuntime();
    }

    /// <summary>
    /// Updates fullscreen mode immediately while the draft is open.
    /// </summary>
    /// <param name="enabled">True to request fullscreen presentation.</param>
    private void HandleFullscreenChanged(bool enabled)
    {
        if (suppressControlCallbacks)
            return;

        draftSettings.FullscreenEnabled = enabled ? (byte)1 : (byte)0;
        ApplyDraftToRuntime();
    }

    /// <summary>
    /// Updates the target frame-rate lock immediately while the draft is open.
    /// </summary>
    /// <param name="frameRate">Requested frame-rate lock from the segmented selector.</param>
    private void HandleFrameRateChanged(int frameRate)
    {
        if (suppressControlCallbacks)
            return;

        draftSettings.FrameRateLimit = GameUserSettingsStore.ResolveSupportedFrameRateLimit(frameRate);
        ApplyDraftToRuntime();
    }

    /// <summary>
    /// Updates the damage-rumble user multiplier.
    /// </summary>
    /// <param name="value">Slider value in the 0..2 multiplier range.</param>
    private void HandleDamageRumbleChanged(float value)
    {
        if (suppressControlCallbacks)
            return;

        draftSettings.DamageRumbleMultiplier = value;
        ApplyDraftToRuntime();
        RefreshValueLabels();
    }

    /// <summary>
    /// Updates the fire-rumble user multiplier.
    /// </summary>
    /// <param name="value">Slider value in the 0..2 multiplier range.</param>
    private void HandleFireRumbleChanged(float value)
    {
        if (suppressControlCallbacks)
            return;

        draftSettings.FireRumbleMultiplier = value;
        ApplyDraftToRuntime();
        RefreshValueLabels();
    }
    #endregion

    #region UI State
    /// <summary>
    /// Moves to the adjacent Settings macro tab and focuses its first available control.
    /// </summary>
    /// <param name="direction">Negative for the previous tab or positive for the next tab.</param>
    private void StepSettingsPanel(int direction)
    {
        tabController.Step(direction, wrapSettingsTabs);
    }

    /// <summary>
    /// Copies the current draft values into the authored controls without triggering callbacks.
    /// </summary>
    private void ApplyDraftToControls()
    {
        suppressControlCallbacks = true;
        GameUserSettingsData clampedSettings = GameUserSettingsStore.ClampForRuntime(draftSettings);
        draftSettings = clampedSettings;

        SetSliderValue(masterVolumeSlider, clampedSettings.MasterVolume);
        SetSliderValue(sfxVolumeSlider, clampedSettings.SfxVolume);
        SetSliderValue(musicVolumeSlider, clampedSettings.MusicVolume);
        SetToggleValue(visualPointerToggle, clampedSettings.VisualPointerEnabled != 0);
        SetToggleValue(fullscreenToggle, clampedSettings.FullscreenEnabled != 0);
        SetFrameRateValue(frameRateSelector, clampedSettings.FrameRateLimit);
        SetSliderValue(damageRumbleMultiplierSlider, clampedSettings.DamageRumbleMultiplier);
        SetSliderValue(fireRumbleMultiplierSlider, clampedSettings.FireRumbleMultiplier);
        suppressControlCallbacks = false;
        RefreshValueLabels();
    }

    /// <summary>
    /// Updates optional value labels beside sliders.
    /// </summary>
    private void RefreshValueLabels()
    {
        SetPercentLabel(masterVolumeValueLabel, draftSettings.MasterVolume);
        SetPercentLabel(sfxVolumeValueLabel, draftSettings.SfxVolume);
        SetPercentLabel(musicVolumeValueLabel, draftSettings.MusicVolume);
        SetMultiplierLabel(damageRumbleValueLabel, draftSettings.DamageRumbleMultiplier);
        SetMultiplierLabel(fireRumbleValueLabel, draftSettings.FireRumbleMultiplier);
    }
    #endregion

    #region Runtime Application
    /// <summary>
    /// Applies the current draft to screen state, FMOD buses and ECS presentation settings.
    /// </summary>
    private void ApplyDraftToRuntime()
    {
        GameUserSettingsRuntimeUtility.Apply(draftSettings, in runtimeOptions, true, true, true);
    }

    /// <summary>
    /// Refreshes Settings menu runtime options from ECS, falling back to serialized prefab defaults before ECS exists.
    /// </summary>
    private void RefreshRuntimeConfig()
    {
        if (GameSettingsMenuRuntimeConfigUtility.TryResolve(out runtimeOptions, out audioPreviewSet))
            return;

        GameUserSettingsAudioBusPaths fallbackBusPaths = new GameUserSettingsAudioBusPaths(masterBusPath, sfxBusPath, musicBusPath);
        GameUserSettingsWindowedDisplaySettings fallbackWindowedDisplay = GameUserSettingsWindowedDisplaySettings.CreateDefault();
        GameUserSettingsData fallbackDefaults = GameUserSettingsStore.CreateDefaults();
        runtimeOptions = new GameUserSettingsRuntimeOptions(in fallbackBusPaths, in fallbackWindowedDisplay, in fallbackDefaults);
        audioPreviewSet = new GameAudioSettingsPreviewSet(new GameAudioSettingsPreviewEvent(masterPreviewEventPath, masterPreviewBankName),
                                                          new GameAudioSettingsPreviewEvent(sfxPreviewEventPath, sfxPreviewBankName),
                                                          new GameAudioSettingsPreviewEvent(musicPreviewEventPath, musicPreviewBankName),
                                                          masterPreviewPlaysAllOthers);
    }
    #endregion

    #region Closing
    /// <summary>
    /// Hides the menu, stops preview audio and reports closure to the owning menu controller.
    /// </summary>
    private void CloseMenu()
    {
        audioPreviewStopController?.StopNow();

        if (inputActionNavigation != null)
            inputActionNavigation.Deactivate();

        if (devSectionController != null)
            devSectionController.Deactivate();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        SettingsMenuControllerUiUtility.SelectSelectable(eventSystemOverride, restoreSelectionTarget);
        restoreSelectionTarget = null;
        MenuClosed?.Invoke();
    }
    #endregion

    #endregion
}
