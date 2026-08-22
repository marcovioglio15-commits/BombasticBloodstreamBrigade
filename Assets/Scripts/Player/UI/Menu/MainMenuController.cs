using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Handles the simple front-end scene flow for the authored main menu.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Buttons")]
    [Tooltip("Button that starts the gameplay scene.")]
    [SerializeField] private Button playButton;

#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
    [Tooltip("Button that opens the runtime enemy spawner override tool.")]
    [SerializeField] private Button enemySpawnerToolButton;
#endif

    [Tooltip("Button that opens the runtime Settings menu.")]
    [SerializeField] private Button settingsButton;

    [Tooltip("Button that closes the application.")]
    [SerializeField] private Button quitButton;

#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
    [Header("Runtime Tools")]
    [Tooltip("Runtime enemy spawner override panel opened from the main menu.")]
    [SerializeField] private EnemySpawnerRuntimeToolPanelController enemySpawnerToolPanel;
#endif

    [Tooltip("Reusable runtime Settings menu opened from the main menu.")]
    [SerializeField] private SettingsMenuController settingsMenu;

    [Header("Navigation")]
    [Tooltip("Optional EventSystem override used to select the default menu button.")]
    [SerializeField] private EventSystem eventSystemOverride;
    #endregion

    #region Runtime
    private MenuSelectionController selectionController;
    private Selectable selectionBeforeLock;
    private Selectable fallbackSelectionAfterUnlock;
    private bool navigationLocked;
    private bool terminalCommandSubmitted;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Caches optional local menu selection helpers before UI binding.
    /// </summary>
    private void Awake()
    {
        selectionController = GetComponent<MenuSelectionController>();
#if UNITY_EDITOR && !NASHCORE_RUNTIME_SPAWNER_TOOL
        if (enemySpawnerToolButton != null)
            enemySpawnerToolButton.gameObject.SetActive(false);
#endif
    }

    /// <summary>
    /// Registers menu callbacks and applies the controller-aware menu cursor state.
    /// </summary>
    private void OnEnable()
    {
        terminalCommandSubmitted = false;
        navigationLocked = false;
        SetMenuButtonsInteractable(true);

        if (selectionController != null)
            selectionController.enabled = true;

        RegisterButtons();
        SelectDefaultButton();
        Time.timeScale = 1f;

        // Hide and lock the pointer while a controller is connected, even before the spawner tool is opened.
        InputSystem.onDeviceChange += HandleDeviceChange;
        MenuPointerVisibilityUtility.ApplyForGamepadPresence();
    }

    /// <summary>
    /// Removes menu and device callbacks when the controller leaves the active scene.
    /// </summary>
    private void OnDisable()
    {
        UnregisterButtons();
        InputSystem.onDeviceChange -= HandleDeviceChange;
    }
    #endregion

    #region Wiring
    /// <summary>
    /// Registers click handlers for the authored menu buttons.
    /// </summary>
    private void RegisterButtons()
    {
        if (playButton != null)
            playButton.onClick.AddListener(HandlePlayPressed);

#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
        if (enemySpawnerToolButton != null)
            enemySpawnerToolButton.onClick.AddListener(HandleEnemySpawnerToolPressed);
#endif

        if (settingsButton != null)
            settingsButton.onClick.AddListener(HandleSettingsPressed);

        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuitPressed);

#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
        if (enemySpawnerToolPanel != null)
            enemySpawnerToolPanel.ToolClosed += HandleToolClosed;
#endif

        if (settingsMenu != null)
            settingsMenu.MenuClosed += HandleSettingsClosed;
    }

    /// <summary>
    /// Removes click handlers from the authored menu buttons.
    /// </summary>
    private void UnregisterButtons()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(HandlePlayPressed);

#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
        if (enemySpawnerToolButton != null)
            enemySpawnerToolButton.onClick.RemoveListener(HandleEnemySpawnerToolPressed);
#endif

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(HandleSettingsPressed);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(HandleQuitPressed);

#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
        if (enemySpawnerToolPanel != null)
            enemySpawnerToolPanel.ToolClosed -= HandleToolClosed;
#endif

        if (settingsMenu != null)
            settingsMenu.MenuClosed -= HandleSettingsClosed;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Locks or unlocks main-menu navigation while a modal runtime overlay owns input. Locking remembers the focused
    /// button and disables the authored menu buttons and the selection helper so gamepad and keyboard focus cannot
    /// leave the overlay; unlocking restores them and reselects the button focused before the overlay opened.
    /// </summary>
    /// <param name="locked">True to suspend menu navigation, false to restore it.</param>
    public void SetNavigationLocked(bool locked)
    {
        if (terminalCommandSubmitted && !locked)
            return;

        if (navigationLocked == locked)
            return;

        navigationLocked = locked;

        // Remember which button opened the overlay so focus can return to it instead of the first button.
        if (locked)
            CaptureSelectionBeforeLock();

        SetMenuButtonsInteractable(!locked);

        if (selectionController != null)
            selectionController.enabled = !locked;

        // Restore focus to the button selected before the overlay opened once it releases input.
        if (!locked)
            RestoreSelectionAfterUnlock();
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Requests the configured default gameplay scene through the ECS Scene Manager.
    /// </summary>
    private void HandlePlayPressed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        Time.timeScale = 1f;

        if (GameSceneTransitionRequestUtility.EnqueueLoadDefaultGameplay())
        {
            LockTerminalCommands();
            return;
        }

        Debug.LogWarning("[MainMenuController] Unable to enqueue gameplay loading. Start from SCN_Bootstrap or verify the GameSceneManagerAuthoring setup.");
    }

#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
    /// <summary>
    /// Opens the runtime enemy spawner override panel from the main menu.
    /// </summary>
    private void HandleEnemySpawnerToolPressed()
    {
        if (navigationLocked ||
            GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        if (enemySpawnerToolPanel == null)
        {
            Debug.LogWarning("[MainMenuController] Enemy spawner runtime tool panel is not assigned.");
            return;
        }

        // Suspend menu navigation before the overlay opens so input cannot leak back to the menu buttons.
        fallbackSelectionAfterUnlock = enemySpawnerToolButton;
        SetNavigationLocked(true);
        enemySpawnerToolPanel.OpenTool();
    }
#endif

    /// <summary>
    /// Opens the shared runtime Settings menu from the main menu.
    /// </summary>
    private void HandleSettingsPressed()
    {
        if (navigationLocked ||
            GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        if (settingsMenu == null)
        {
            Debug.LogWarning("[MainMenuController] Settings menu is not assigned.");
            return;
        }

        // Suspend menu navigation before the overlay opens so input cannot leak back to the main menu buttons.
        fallbackSelectionAfterUnlock = settingsButton;
        SetNavigationLocked(true);
        settingsMenu.Open(settingsButton);
    }

#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
    /// <summary>
    /// Restores main-menu navigation when the spawner tool overlay reports that it has closed.
    /// </summary>
    private void HandleToolClosed()
    {
        SetNavigationLocked(false);
    }
#endif

    /// <summary>
    /// Restores main-menu navigation when the Settings overlay reports that it has closed.
    /// </summary>
    private void HandleSettingsClosed()
    {
        SetNavigationLocked(false);
    }

    /// <summary>
    /// Re-applies the menu cursor state when a gamepad is connected or removed, unless the spawner tool overlay
    /// currently owns the cursor.
    /// </summary>
    /// <param name="device">Device that changed state.</param>
    /// <param name="change">Kind of change reported by the input system.</param>
    private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad)
            return;

        // The overlay drives the cursor while it is open; only refresh the plain menu state here.
        if (navigationLocked)
            return;

        switch (change)
        {
            case InputDeviceChange.Added:
            case InputDeviceChange.Removed:
            case InputDeviceChange.Reconnected:
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Enabled:
            case InputDeviceChange.Disabled:
                MenuPointerVisibilityUtility.ApplyForGamepadPresence();
                break;
        }
    }

    /// <summary>
    /// Requests application shutdown through the shared helper.
    /// </summary>
    private void HandleQuitPressed()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockTerminalUiCommand(terminalCommandSubmitted))
            return;

        LockTerminalCommands();
        AppUtils.QuitGame();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Sets the interactable state of every authored menu button so navigation can be suspended in one call.
    /// </summary>
    /// <param name="interactable">True to enable the menu buttons, false to disable them.</param>
    private void SetMenuButtonsInteractable(bool interactable)
    {
        if (playButton != null)
            playButton.interactable = interactable;

#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
        if (enemySpawnerToolButton != null)
            enemySpawnerToolButton.interactable = interactable;
#endif

        if (settingsButton != null)
            settingsButton.interactable = interactable;

        if (quitButton != null)
            quitButton.interactable = interactable;
    }

    /// <summary>
    /// Locks every authored command immediately after a terminal action is accepted so pointer, keyboard, and
    /// controller submits cannot enqueue or invoke the same operation again while the scene is still visible.
    /// </summary>
    private void LockTerminalCommands()
    {
        terminalCommandSubmitted = true;
        navigationLocked = true;
        SetMenuButtonsInteractable(false);

        if (selectionController != null)
            selectionController.enabled = false;

        EventSystem resolvedEventSystem = ResolveEventSystem();

        if (resolvedEventSystem != null)
            resolvedEventSystem.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Caches the selectable focused before the overlay opened so focus can return to it on close.
    /// </summary>
    private void CaptureSelectionBeforeLock()
    {
        EventSystem resolvedEventSystem = ResolveEventSystem();
        GameObject selectedObject = resolvedEventSystem != null ? resolvedEventSystem.currentSelectedGameObject : null;
        selectionBeforeLock = selectedObject != null ? selectedObject.GetComponent<Selectable>() : null;
    }

    /// <summary>
    /// Restores focus to the button selected before the overlay opened, falling back to the tool button or default.
    /// </summary>
    private void RestoreSelectionAfterUnlock()
    {
        Selectable restoreTarget = selectionBeforeLock != null ? selectionBeforeLock : fallbackSelectionAfterUnlock;
        selectionBeforeLock = null;
        fallbackSelectionAfterUnlock = null;

        // Fall back to the tool button (then Play) when the previous selection is gone or no longer usable.
        if (restoreTarget == null || !restoreTarget.IsInteractable())
            restoreTarget = ResolveToolOrPlayFallback();

        if (restoreTarget == null)
        {
            SelectDefaultButton();
            return;
        }

        if (selectionController != null)
        {
            selectionController.SelectSelectable(restoreTarget, rememberAsDefault : true);
            return;
        }

        ApplyEventSystemSelection(restoreTarget);
    }

    /// <summary>
    /// Selects a sensible default authored button so keyboard and controller navigation work immediately.
    /// </summary>
    private void SelectDefaultButton()
    {
        Selectable defaultTarget = playButton != null ? playButton : quitButton;

        if (defaultTarget == null)
            return;

        if (selectionController != null)
        {
            selectionController.SelectSelectable(defaultTarget, rememberAsDefault : true);
            return;
        }

        ApplyEventSystemSelection(defaultTarget);
    }

    /// <summary>
    /// Applies one selection directly through the resolved EventSystem when no selection helper is present.
    /// </summary>
    /// <param name="selectable">Selectable that should own UI focus.</param>
    private void ApplyEventSystemSelection(Selectable selectable)
    {
        EventSystem resolvedEventSystem = ResolveEventSystem();

        if (resolvedEventSystem == null)
            return;

        Canvas.ForceUpdateCanvases();
        resolvedEventSystem.SetSelectedGameObject(null);
        selectable.Select();
        resolvedEventSystem.SetSelectedGameObject(selectable.gameObject);
    }

    /// <summary>
    /// Resolves the EventSystem used to drive menu selection.
    /// </summary>
    /// <returns>EventSystem override when set, otherwise the active EventSystem.</returns>
    private EventSystem ResolveEventSystem()
    {
        return eventSystemOverride != null ? eventSystemOverride : EventSystem.current;
    }

    /// <summary>
    /// Resolves the non-settings fallback button used when focus cannot return to its previous selectable.
    /// </summary>
    /// <returns>Runtime tool button when available, otherwise Play button.</returns>
    private Selectable ResolveToolOrPlayFallback()
    {
#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
        return enemySpawnerToolButton != null ? enemySpawnerToolButton : playButton;
#else
        return playButton;
#endif
    }
    #endregion

    #endregion
}
