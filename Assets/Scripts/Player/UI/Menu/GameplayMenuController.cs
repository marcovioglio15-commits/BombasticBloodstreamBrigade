using Unity.Entities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Coordinates pause and ending menus for gameplay scenes while reading the authoritative run outcome from ECS.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayMenuController : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Pause Menu")]
    [Tooltip("Root object of the authored pause menu panel.")]
    [SerializeField] private GameObject pauseMenuRoot;

    [Tooltip("Button used as the default selection when the pause menu opens.")]
    [SerializeField] private Button resumeButton;

    [Tooltip("Button that reloads the active gameplay scene from the pause menu.")]
    [SerializeField] private Button pauseRestartButton;

    [Tooltip("Button that opens the runtime Settings menu from the pause menu.")]
    [SerializeField] private Button pauseSettingsButton;

    [Tooltip("Button that returns to the main menu scene from the pause menu.")]
    [SerializeField] private Button pauseMainMenuButton;

    [Tooltip("Button that closes the application from the pause menu.")]
    [SerializeField] private Button pauseQuitButton;

    [Header("Ending Menu")]
    [Tooltip("Root object of the authored ending menu panel.")]
    [SerializeField] private GameObject endingMenuRoot;

    [Tooltip("Message label updated with the resolved victory or defeat text.")]
    [SerializeField] private TMP_Text endingMessageText;

    [Tooltip("Button used as the default selection when the ending menu opens.")]
    [SerializeField] private Button endingPlayAgainButton;

    [Tooltip("Button that returns to the main menu scene from the ending menu.")]
    [SerializeField] private Button endingMainMenuButton;

    [Tooltip("Button that closes the application from the ending menu.")]
    [SerializeField] private Button endingQuitButton;

    [Header("Settings Menu")]
    [Tooltip("Reusable runtime Settings menu opened from the pause menu.")]
    [SerializeField] private SettingsMenuController settingsMenu;

    [Header("Messages")]
    [Tooltip("Message shown when every authored enemy wave has completed.")]
    [SerializeField] private string victoryMessage = "Victory";

    [Tooltip("Message shown when the player reaches zero health.")]
    [SerializeField] private string defeatMessage = "Defeat";

    [Header("Navigation")]
    [Tooltip("Optional EventSystem override used for default selection and navigation recovery.")]
    [SerializeField] private EventSystem eventSystemOverride;
    #endregion

    #region Runtime
    private World defaultWorld;
    private EntityManager entityManager;
    private EntityQuery playerQuery;
    private bool playerQueryInitialized;
    private Entity cachedPlayerEntity;
    private InputAction pauseAction;
    private bool pauseMenuVisible;
    private bool endingMenuVisible;
    private bool settingsMenuVisible;
    private bool terminalCommandSubmitted;
    private MenuSelectionController selectionController;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Caches local menu helpers and applies the authored startup visibility.
    /// </summary>
    private void Awake()
    {
        selectionController = GetComponent<MenuSelectionController>();
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Registers UI and input callbacks while restoring gameplay cursor and time state.
    /// </summary>
    private void OnEnable()
    {
        terminalCommandSubmitted = false;
        GameplayMenuEcsBindingUtility.SetTerminalButtonsInteractable(resumeButton, pauseSettingsButton,
                                                                     pauseRestartButton, pauseMainMenuButton, pauseQuitButton,
                                                                     endingPlayAgainButton, endingMainMenuButton, endingQuitButton, true);

        if (selectionController != null)
            selectionController.enabled = true;

        RegisterButtons();
        RegisterRuntimeEvents();
        RefreshPauseActionBinding();

        if (!GameSceneTransitionRuntimeGuardUtility.IsDefaultWorldTransitioning())
            Time.timeScale = 1f;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// Removes callbacks and resets transient menu state when the scene unloads.
    /// </summary>
    private void OnDisable()
    {
        UnregisterRuntimeEvents();
        UnregisterPauseActionBinding();
        UnregisterButtons();

        if (!GameSceneTransitionRuntimeGuardUtility.IsDefaultWorldTransitioning())
            Time.timeScale = 1f;

        pauseMenuVisible = false;
        endingMenuVisible = false;
        settingsMenuVisible = false;
    }

    /// <summary>
    /// Polls the ECS run outcome and opens the ending menu once the run is finalized.
    /// </summary>
    private void Update()
    {
        if (!TryInitializeEcsBindings())
            return;

        if (!TryResolvePlayerEntity(out Entity playerEntity))
            return;

        if (GameplayMenuEcsBindingUtility.IsMilestoneSelectionActive(entityManager, playerEntity))
            SuppressPauseMenuForMilestoneSelection();

        if (!entityManager.HasComponent<PlayerRunOutcomeState>(playerEntity))
            return;

        PlayerRunOutcomeState runOutcomeState = entityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);

        if (runOutcomeState.IsFinalized == 0)
            return;

        if (endingMenuVisible)
            return;

        ShowEndingMenu(runOutcomeState.Outcome);
    }
    #endregion

    #region Lifecycle
    /// <summary>
    /// Applies the authored startup visibility for pause and ending menus.
    /// </summary>
    private void ApplyInitialVisualState()
    {
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);

        if (endingMenuRoot != null)
            endingMenuRoot.SetActive(false);
    }
    #endregion

    #region Buttons
    /// <summary>
    /// Registers authored button callbacks for pause and ending menus.
    /// </summary>
    private void RegisterButtons()
    {
        if (resumeButton != null)
            resumeButton.onClick.AddListener(HandleResumePressed);

        if (pauseRestartButton != null)
            pauseRestartButton.onClick.AddListener(HandleRestartPressed);

        if (pauseSettingsButton != null)
            pauseSettingsButton.onClick.AddListener(HandleSettingsPressed);

        if (pauseMainMenuButton != null)
            pauseMainMenuButton.onClick.AddListener(HandleMainMenuPressed);

        if (pauseQuitButton != null)
            pauseQuitButton.onClick.AddListener(HandleQuitPressed);

        if (endingPlayAgainButton != null)
            endingPlayAgainButton.onClick.AddListener(HandlePlayAgainPressed);

        if (endingMainMenuButton != null)
            endingMainMenuButton.onClick.AddListener(HandleEndingMainMenuPressed);

        if (endingQuitButton != null)
            endingQuitButton.onClick.AddListener(HandleEndingQuitPressed);

        if (settingsMenu != null)
            settingsMenu.MenuClosed += HandleSettingsClosed;
    }

    /// <summary>
    /// Removes authored button callbacks from pause and ending menus.
    /// </summary>
    private void UnregisterButtons()
    {
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(HandleResumePressed);

        if (pauseRestartButton != null)
            pauseRestartButton.onClick.RemoveListener(HandleRestartPressed);

        if (pauseSettingsButton != null)
            pauseSettingsButton.onClick.RemoveListener(HandleSettingsPressed);

        if (pauseMainMenuButton != null)
            pauseMainMenuButton.onClick.RemoveListener(HandleMainMenuPressed);

        if (pauseQuitButton != null)
            pauseQuitButton.onClick.RemoveListener(HandleQuitPressed);

        if (endingPlayAgainButton != null)
            endingPlayAgainButton.onClick.RemoveListener(HandlePlayAgainPressed);

        if (endingMainMenuButton != null)
            endingMainMenuButton.onClick.RemoveListener(HandleEndingMainMenuPressed);

        if (endingQuitButton != null)
            endingQuitButton.onClick.RemoveListener(HandleEndingQuitPressed);

        if (settingsMenu != null)
            settingsMenu.MenuClosed -= HandleSettingsClosed;
    }
    #endregion

    #region Runtime Input
    /// <summary>
    /// Registers runtime input lifecycle events so pause input can follow PlayerInputRuntime reinitialization.
    /// </summary>
    private void RegisterRuntimeEvents()
    {
        PlayerInputRuntime.RuntimeInitialized += HandleInputRuntimeInitialized;
        PlayerInputRuntime.RuntimeShutdown += HandleInputRuntimeShutdown;
    }

    /// <summary>
    /// Removes runtime input lifecycle event subscriptions.
    /// </summary>
    private void UnregisterRuntimeEvents()
    {
        PlayerInputRuntime.RuntimeInitialized -= HandleInputRuntimeInitialized;
        PlayerInputRuntime.RuntimeShutdown -= HandleInputRuntimeShutdown;
    }

    /// <summary>
    /// Rebinds the pause toggle action whenever the shared input runtime is recreated.
    /// </summary>
    private void HandleInputRuntimeInitialized()
    {
        RefreshPauseActionBinding();
    }

    /// <summary>
    /// Clears the current pause-toggle action subscription when the shared input runtime shuts down.
    /// </summary>
    private void HandleInputRuntimeShutdown()
    {
        UnregisterPauseActionBinding();
    }

    /// <summary>
    /// Refreshes the pause-toggle binding from PlayerInputRuntime.PauseAction with UI cancel fallback.
    /// </summary>
    private void RefreshPauseActionBinding()
    {
        InputAction runtimePauseAction = PlayerInputRuntime.PauseAction;

        if (runtimePauseAction == null)
            runtimePauseAction = PlayerInputRuntime.UICancelAction;

        if (ReferenceEquals(pauseAction, runtimePauseAction))
            return;

        UnregisterPauseActionBinding();
        pauseAction = runtimePauseAction;

        if (pauseAction == null)
            return;

        pauseAction.performed += HandlePausePerformed;
    }

    /// <summary>
    /// Removes the current pause-toggle subscription from the cached gameplay pause action.
    /// </summary>
    private void UnregisterPauseActionBinding()
    {
        if (pauseAction == null)
            return;

        pauseAction.performed -= HandlePausePerformed;
        pauseAction = null;
    }

    /// <summary>
    /// Toggles pause only when gameplay is not already owned by another pause-capable overlay or ending screen.
    /// </summary>
    /// <param name="context">Input callback context for the performed cancel action.</param>
    private void HandlePausePerformed(InputAction.CallbackContext context)
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        if (IsMilestoneSelectionActive())
        {
            SuppressPauseMenuForMilestoneSelection();
            return;
        }

        if (settingsMenuVisible)
        {
            if (settingsMenu != null)
                settingsMenu.CancelAndClose();

            return;
        }

        if (endingMenuVisible)
            return;

        if (!pauseMenuVisible && Time.timeScale < 0.999f)
            return;

        if (pauseMenuVisible)
        {
            ResumeGameplay();
            return;
        }

        ShowPauseMenu();
    }
    #endregion

    #region ECS
    /// <summary>
    /// Initializes world and player-query state once a valid default world exists.
    /// </summary>
    /// <returns>True when ECS bindings are ready, otherwise false.</returns>
    private bool TryInitializeEcsBindings()
    {
        return GameplayMenuEcsBindingUtility.TryInitializeBindings(ref defaultWorld,
                                                                   ref entityManager,
                                                                   ref playerQuery,
                                                                   ref playerQueryInitialized,
                                                                   ref cachedPlayerEntity);
    }

    /// <summary>
    /// Resolves the single local player entity used to drive gameplay menu state.
    /// </summary>
    /// <param name="playerEntity">Resolved player entity when available.</param>
    /// <returns>True when exactly one valid player entity exists, otherwise false.</returns>
    private bool TryResolvePlayerEntity(out Entity playerEntity)
    {
        return GameplayMenuEcsBindingUtility.TryResolvePlayerEntity(entityManager,
                                                                    playerQuery,
                                                                    ref cachedPlayerEntity,
                                                                    out playerEntity);
    }

    /// <summary>
    /// Resolves whether the cached player is currently blocked by an active milestone selection.
    /// </summary>
    /// <returns>True when milestone selection owns gameplay input; otherwise false.</returns>
    private bool IsMilestoneSelectionActive()
    {
        if (!TryInitializeEcsBindings())
            return false;

        if (!TryResolvePlayerEntity(out Entity playerEntity))
            return false;

        return GameplayMenuEcsBindingUtility.IsMilestoneSelectionActive(entityManager, playerEntity);
    }
    #endregion

    #region Menu Flow
    /// <summary>
    /// Shows the authored pause menu and freezes gameplay time.
    /// </summary>
    private void ShowPauseMenu()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        if (IsMilestoneSelectionActive())
            return;

        pauseMenuVisible = true;
        Time.timeScale = 0f;

        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        GameplayMenuEcsBindingUtility.SelectDefaultButton(selectionController, eventSystemOverride,
                                                          resumeButton, pauseSettingsButton, pauseRestartButton,
                                                          pauseMainMenuButton, pauseQuitButton);
    }

    /// <summary>
    /// Hides the authored pause menu and restores gameplay time.
    /// </summary>
    private void ResumeGameplay()
    {
        pauseMenuVisible = false;

        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);

        if (!endingMenuVisible)
        {
            Time.timeScale = 1f;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    /// <summary>
    /// Shows the authored ending menu using the resolved terminal run outcome.
    /// </summary>
    /// <param name="outcome">Finalized outcome computed by ECS.</param>
    private void ShowEndingMenu(PlayerRunOutcome outcome)
    {
        if (pauseMenuVisible)
            ResumeGameplay();

        endingMenuVisible = true;
        Time.timeScale = 0f;

        if (endingMessageText != null)
        {
            switch (outcome)
            {
                case PlayerRunOutcome.Victory:
                    endingMessageText.text = string.IsNullOrWhiteSpace(victoryMessage) ? "Victory" : victoryMessage;
                    break;
                case PlayerRunOutcome.Defeat:
                    endingMessageText.text = string.IsNullOrWhiteSpace(defeatMessage) ? "Defeat" : defeatMessage;
                    break;
                default:
                    endingMessageText.text = string.Empty;
                    break;
            }
        }

        if (endingMenuRoot != null)
            endingMenuRoot.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        GameplayMenuEcsBindingUtility.SelectDefaultButton(selectionController, eventSystemOverride,
                                                          endingPlayAgainButton, endingMainMenuButton, endingQuitButton);
    }

    /// <summary>
    /// Closes pause-owned UI immediately when milestone selection owns the gameplay overlay.
    /// </summary>
    private void SuppressPauseMenuForMilestoneSelection()
    {
        GameplayMenuEcsBindingUtility.SuppressPauseMenuForMilestoneSelection(pauseMenuRoot,
                                                                             settingsMenu,
                                                                             selectionController,
                                                                             resumeButton,
                                                                             pauseSettingsButton,
                                                                             pauseRestartButton,
                                                                             pauseMainMenuButton,
                                                                             pauseQuitButton,
                                                                             ref pauseMenuVisible,
                                                                             ref settingsMenuVisible);
    }
    #endregion

    #region Button Callbacks
    /// <summary>
    /// Handles the Resume button from the pause menu.
    /// </summary>
    private void HandleResumePressed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        ResumeGameplay();
    }

    /// <summary>
    /// Reloads the active gameplay scene from the pause menu.
    /// </summary>
    private void HandleRestartPressed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        if (ReloadActiveScene())
            LockTerminalCommands();
    }

    /// <summary>
    /// Opens the shared runtime Settings menu from the pause menu while keeping gameplay paused.
    /// </summary>
    private void HandleSettingsPressed()
    {
        if (settingsMenuVisible ||
            GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        if (settingsMenu == null)
        {
            Debug.LogWarning("[GameplayMenuController] Settings menu is not assigned.");
            return;
        }

        settingsMenuVisible = true;
        SetPauseButtonsInteractable(false);

        if (selectionController != null)
            selectionController.enabled = false;

        settingsMenu.Open(pauseSettingsButton);
    }

    /// <summary>
    /// Restores pause-menu navigation after the Settings overlay closes.
    /// </summary>
    private void HandleSettingsClosed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        settingsMenuVisible = false;
        SetPauseButtonsInteractable(true);

        if (selectionController != null)
            selectionController.enabled = true;

        if (pauseMenuVisible)
            GameplayMenuEcsBindingUtility.SelectDefaultButton(selectionController,
                                                              eventSystemOverride,
                                                              pauseSettingsButton,
                                                              resumeButton,
                                                              pauseRestartButton,
                                                              pauseMainMenuButton,
                                                              pauseQuitButton);
    }

    /// <summary>
    /// Returns to the main menu scene from the pause menu.
    /// </summary>
    private void HandleMainMenuPressed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        if (LoadMainMenuScene())
            LockTerminalCommands();
    }

    /// <summary>
    /// Requests application shutdown from the pause menu.
    /// </summary>
    private void HandleQuitPressed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        LockTerminalCommands();
        Time.timeScale = 1f;
        AppUtils.QuitGame();
    }

    /// <summary>
    /// Reloads the active gameplay scene from the ending menu.
    /// </summary>
    private void HandlePlayAgainPressed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        if (ReloadActiveScene())
            LockTerminalCommands();
    }

    /// <summary>
    /// Returns to the main menu scene from the ending menu.
    /// </summary>
    private void HandleEndingMainMenuPressed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        if (LoadMainMenuScene())
            LockTerminalCommands();
    }

    /// <summary>
    /// Requests application shutdown from the ending menu.
    /// </summary>
    private void HandleEndingQuitPressed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        LockTerminalCommands();
        Time.timeScale = 1f;
        AppUtils.QuitGame();
    }
    #endregion

    #region Pause Menu Helpers
    /// <summary>
    /// Sets interactability on every authored pause-menu button while the Settings overlay owns input.
    /// </summary>
    /// <param name="interactable">True to enable pause-menu buttons, false to suspend them.</param>
    private void SetPauseButtonsInteractable(bool interactable)
    {
        GameplayMenuEcsBindingUtility.SetPauseButtonsInteractable(resumeButton,
                                                                  pauseSettingsButton,
                                                                  pauseRestartButton,
                                                                  pauseMainMenuButton,
                                                                  pauseQuitButton,
                                                                  interactable);
    }

    /// <summary>
    /// Commits one terminal menu command and clears selection before asynchronous scene work begins.
    /// </summary>
    private void LockTerminalCommands()
    {
        GameplayMenuEcsBindingUtility.LockTerminalCommands(ref terminalCommandSubmitted,
                                                           ref settingsMenuVisible,
                                                           selectionController,
                                                           eventSystemOverride,
                                                           resumeButton,
                                                           pauseSettingsButton,
                                                           pauseRestartButton,
                                                           pauseMainMenuButton,
                                                           pauseQuitButton,
                                                           endingPlayAgainButton,
                                                           endingMainMenuButton,
                                                           endingQuitButton);
    }
    #endregion

    #region Scene Flow
    /// <summary>
    /// Restarts the active procedural run when available, otherwise requests the active managed scene restart.
    /// </summary>
    /// <returns>True when the authoritative runtime accepted or already owns the restart request.</returns>
    private bool ReloadActiveScene()
    {
        if (GameProceduralLevelRunRequestUtility.TryRestartActiveRun())
            return true;

        if (GameSceneTransitionRequestUtility.EnqueueRestartActiveScene())
            return true;

        Debug.LogWarning("[GameplayMenuController] Unable to enqueue gameplay restart. Start from SCN_Bootstrap or verify the GameSceneManagerAuthoring setup.");
        return false;
    }

    /// <summary>
    /// Requests the configured main menu through the ECS Scene Manager.
    /// </summary>
    /// <returns>True when the authoritative Scene Manager accepted or already owns the main-menu request.</returns>
    private bool LoadMainMenuScene()
    {
        if (GameSceneTransitionRequestUtility.EnqueueLoadMainMenu())
            return true;

        Debug.LogWarning("[GameplayMenuController] Unable to enqueue main-menu loading. Start from SCN_Bootstrap or verify the GameSceneManagerAuthoring setup.");
        return false;
    }
    #endregion

    #endregion
}
