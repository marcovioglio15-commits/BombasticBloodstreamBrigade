using Unity.Entities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Coordinates pause and ending menus for gameplay scenes while reading the authoritative run outcome from ECS.
/// /params None.
/// /returns None.
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
    private MenuSelectionController selectionController;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Caches local menu helpers and applies the authored startup visibility.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void Awake()
    {
        selectionController = GetComponent<MenuSelectionController>();
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Registers UI and input callbacks while restoring gameplay cursor and time state.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnEnable()
    {
        RegisterButtons();
        RegisterRuntimeEvents();
        RefreshPauseActionBinding();
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// Removes callbacks and resets transient menu state when the scene unloads.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnDisable()
    {
        UnregisterRuntimeEvents();
        UnregisterPauseActionBinding();
        UnregisterButtons();
        Time.timeScale = 1f;
        pauseMenuVisible = false;
        endingMenuVisible = false;
    }

    /// <summary>
    /// Polls the ECS run outcome and opens the ending menu once the run is finalized.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void Update()
    {
        if (!TryInitializeEcsBindings())
            return;

        if (!TryResolvePlayerEntity(out Entity playerEntity))
            return;

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
    /// /params None.
    /// /returns None.
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
    /// /params None.
    /// /returns None.
    /// </summary>
    private void RegisterButtons()
    {
        if (resumeButton != null)
            resumeButton.onClick.AddListener(HandleResumePressed);

        if (pauseRestartButton != null)
            pauseRestartButton.onClick.AddListener(HandleRestartPressed);

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
    }

    /// <summary>
    /// Removes authored button callbacks from pause and ending menus.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void UnregisterButtons()
    {
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(HandleResumePressed);

        if (pauseRestartButton != null)
            pauseRestartButton.onClick.RemoveListener(HandleRestartPressed);

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
    }
    #endregion

    #region Runtime Input
    /// <summary>
    /// Registers runtime input lifecycle events so pause input can follow PlayerInputRuntime reinitialization.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void RegisterRuntimeEvents()
    {
        PlayerInputRuntime.RuntimeInitialized += HandleInputRuntimeInitialized;
        PlayerInputRuntime.RuntimeShutdown += HandleInputRuntimeShutdown;
    }

    /// <summary>
    /// Removes runtime input lifecycle event subscriptions.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void UnregisterRuntimeEvents()
    {
        PlayerInputRuntime.RuntimeInitialized -= HandleInputRuntimeInitialized;
        PlayerInputRuntime.RuntimeShutdown -= HandleInputRuntimeShutdown;
    }

    /// <summary>
    /// Rebinds the pause toggle action whenever the shared input runtime is recreated.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandleInputRuntimeInitialized()
    {
        RefreshPauseActionBinding();
    }

    /// <summary>
    /// Clears the current pause-toggle action subscription when the shared input runtime shuts down.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandleInputRuntimeShutdown()
    {
        UnregisterPauseActionBinding();
    }

    /// <summary>
    /// Refreshes the pause-toggle binding from PlayerInputRuntime.PauseAction with UI cancel fallback.
    /// /params None.
    /// /returns None.
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
    /// /params None.
    /// /returns None.
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
    /// /params context Input callback context for the performed cancel action.
    /// /returns None.
    /// </summary>
    private void HandlePausePerformed(InputAction.CallbackContext context)
    {
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
    /// /params None.
    /// /returns True when ECS bindings are ready, otherwise false.
    /// </summary>
    private bool TryInitializeEcsBindings()
    {
        World currentWorld = World.DefaultGameObjectInjectionWorld;

        if (currentWorld == null || !currentWorld.IsCreated)
        {
            defaultWorld = null;
            cachedPlayerEntity = Entity.Null;
            playerQueryInitialized = false;
            return false;
        }

        if (!ReferenceEquals(defaultWorld, currentWorld))
        {
            defaultWorld = currentWorld;
            cachedPlayerEntity = Entity.Null;
            playerQueryInitialized = false;
        }

        entityManager = defaultWorld.EntityManager;

        if (playerQueryInitialized)
            return true;

        EntityQueryDesc playerQueryDescription = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<PlayerControllerConfig>(),
                ComponentType.ReadOnly<PlayerRunOutcomeState>()
            }
        };

        playerQuery = entityManager.CreateEntityQuery(playerQueryDescription);
        playerQueryInitialized = true;
        return true;
    }

    /// <summary>
    /// Resolves the single local player entity used to drive gameplay menu state.
    /// /params playerEntity Resolved player entity when available.
    /// /returns True when exactly one valid player entity exists, otherwise false.
    /// </summary>
    private bool TryResolvePlayerEntity(out Entity playerEntity)
    {
        if (cachedPlayerEntity != Entity.Null &&
            entityManager.Exists(cachedPlayerEntity) &&
            entityManager.HasComponent<PlayerControllerConfig>(cachedPlayerEntity) &&
            entityManager.HasComponent<PlayerRunOutcomeState>(cachedPlayerEntity))
        {
            playerEntity = cachedPlayerEntity;
            return true;
        }

        if (playerQuery.IsEmptyIgnoreFilter)
        {
            playerEntity = Entity.Null;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        if (playerQuery.CalculateEntityCount() != 1)
        {
            playerEntity = Entity.Null;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        Entity resolvedPlayerEntity = playerQuery.GetSingletonEntity();

        if (!entityManager.Exists(resolvedPlayerEntity))
        {
            playerEntity = Entity.Null;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        cachedPlayerEntity = resolvedPlayerEntity;
        playerEntity = resolvedPlayerEntity;
        return true;
    }
    #endregion

    #region Menu Flow
    /// <summary>
    /// Shows the authored pause menu and freezes gameplay time.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ShowPauseMenu()
    {
        pauseMenuVisible = true;
        Time.timeScale = 0f;

        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SelectDefaultButton(resumeButton, pauseRestartButton, pauseMainMenuButton, pauseQuitButton);
    }

    /// <summary>
    /// Hides the authored pause menu and restores gameplay time.
    /// /params None.
    /// /returns None.
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
    /// /params outcome Finalized outcome computed by ECS.
    /// /returns None.
    /// </summary>
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
        SelectDefaultButton(endingPlayAgainButton, endingMainMenuButton, endingQuitButton);
    }
    #endregion

    #region Button Callbacks
    /// <summary>
    /// Handles the Resume button from the pause menu.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandleResumePressed()
    {
        ResumeGameplay();
    }

    /// <summary>
    /// Reloads the active gameplay scene from the pause menu.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandleRestartPressed()
    {
        ReloadActiveScene();
    }

    /// <summary>
    /// Returns to the main menu scene from the pause menu.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandleMainMenuPressed()
    {
        LoadMainMenuScene();
    }

    /// <summary>
    /// Requests application shutdown from the pause menu.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandleQuitPressed()
    {
        Time.timeScale = 1f;
        AppUtils.QuitGame();
    }

    /// <summary>
    /// Reloads the active gameplay scene from the ending menu.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandlePlayAgainPressed()
    {
        ReloadActiveScene();
    }

    /// <summary>
    /// Returns to the main menu scene from the ending menu.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandleEndingMainMenuPressed()
    {
        LoadMainMenuScene();
    }

    /// <summary>
    /// Requests application shutdown from the ending menu.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandleEndingQuitPressed()
    {
        Time.timeScale = 1f;
        AppUtils.QuitGame();
    }
    #endregion

    #region Scene Flow
    /// <summary>
    /// Requests an ECS Scene Manager restart for the active gameplay scene.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ReloadActiveScene()
    {
        Time.timeScale = 1f;

        if (GameSceneTransitionRequestUtility.EnqueueRestartActiveScene())
            return;

        Debug.LogWarning("[GameplayMenuController] Unable to enqueue gameplay restart. Start from SCN_Bootstrap or verify the GameSceneManagerAuthoring setup.");
    }

    /// <summary>
    /// Requests the configured main menu through the ECS Scene Manager.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void LoadMainMenuScene()
    {
        Time.timeScale = 1f;

        if (GameSceneTransitionRequestUtility.EnqueueLoadMainMenu())
            return;

        Debug.LogWarning("[GameplayMenuController] Unable to enqueue main-menu loading. Start from SCN_Bootstrap or verify the GameSceneManagerAuthoring setup.");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Selects the first non-null button from the provided authored button order.
    /// /params preferredButtons Ordered button candidates for UI selection.
    /// /returns None.
    /// </summary>
    private void SelectDefaultButton(params Button[] preferredButtons)
    {
        if (selectionController != null)
        {
            for (int buttonIndex = 0; buttonIndex < preferredButtons.Length; buttonIndex++)
            {
                Button candidateButton = preferredButtons[buttonIndex];

                if (candidateButton == null)
                    continue;

                selectionController.SelectSelectable(candidateButton, rememberAsDefault : true);
                return;
            }

            return;
        }

        EventSystem resolvedEventSystem = eventSystemOverride != null
            ? eventSystemOverride
            : EventSystem.current;

        if (resolvedEventSystem == null)
            return;

        for (int buttonIndex = 0; buttonIndex < preferredButtons.Length; buttonIndex++)
        {
            Button candidateButton = preferredButtons[buttonIndex];

            if (candidateButton == null)
                continue;

            Canvas.ForceUpdateCanvases();
            resolvedEventSystem.SetSelectedGameObject(null);
            candidateButton.Select();
            resolvedEventSystem.SetSelectedGameObject(candidateButton.gameObject);
            return;
        }
    }
    #endregion

    #endregion
}
