using UnityEngine;
using UnityEngine.EventSystems;
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

    [Tooltip("Button that opens the runtime enemy spawner override tool.")]
    [SerializeField] private Button enemySpawnerToolButton;

    [Tooltip("Button that closes the application.")]
    [SerializeField] private Button quitButton;

    [Header("Runtime Tools")]
    [Tooltip("Runtime enemy spawner override panel opened from the main menu.")]
    [SerializeField] private EnemySpawnerRuntimeToolPanelController enemySpawnerToolPanel;

    [Header("Navigation")]
    [Tooltip("Optional EventSystem override used to select the default menu button.")]
    [SerializeField] private EventSystem eventSystemOverride;
    #endregion

    #region Runtime
    private MenuSelectionController selectionController;
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
    }

    /// <summary>
    /// Registers menu callbacks and restores menu cursor state.
    /// </summary>
    private void OnEnable()
    {
        RegisterButtons();
        SelectDefaultButton();
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>
    /// Removes menu callbacks when the controller leaves the active scene.
    /// </summary>
    private void OnDisable()
    {
        UnregisterButtons();
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

        if (enemySpawnerToolButton != null)
            enemySpawnerToolButton.onClick.AddListener(HandleEnemySpawnerToolPressed);

        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuitPressed);
    }

    /// <summary>
    /// Removes click handlers from the authored menu buttons.
    /// </summary>
    private void UnregisterButtons()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(HandlePlayPressed);

        if (enemySpawnerToolButton != null)
            enemySpawnerToolButton.onClick.RemoveListener(HandleEnemySpawnerToolPressed);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(HandleQuitPressed);
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Requests the configured default gameplay scene through the ECS Scene Manager.
    /// </summary>
    private void HandlePlayPressed()
    {
        Time.timeScale = 1f;

        if (GameSceneTransitionRequestUtility.EnqueueLoadDefaultGameplay())
            return;

        Debug.LogWarning("[MainMenuController] Unable to enqueue gameplay loading. Start from SCN_Bootstrap or verify the GameSceneManagerAuthoring setup.");
    }

    /// <summary>
    /// Opens the runtime enemy spawner override panel from the main menu.
    /// </summary>
    private void HandleEnemySpawnerToolPressed()
    {
        if (enemySpawnerToolPanel == null)
        {
            Debug.LogWarning("[MainMenuController] Enemy spawner runtime tool panel is not assigned.");
            return;
        }

        enemySpawnerToolPanel.OpenTool();
    }

    /// <summary>
    /// Requests application shutdown through the shared helper.
    /// </summary>
    private void HandleQuitPressed()
    {
        AppUtils.QuitGame();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Selects the first non-null authored menu button so keyboard and controller navigation work immediately.
    /// </summary>
    private void SelectDefaultButton()
    {
        if (selectionController != null && playButton != null)
        {
            selectionController.SelectSelectable(playButton, rememberAsDefault : true);
            return;
        }

        EventSystem resolvedEventSystem = eventSystemOverride != null
            ? eventSystemOverride
            : EventSystem.current;

        if (resolvedEventSystem == null)
            return;

        if (playButton != null)
        {
            Canvas.ForceUpdateCanvases();
            resolvedEventSystem.SetSelectedGameObject(null);
            playButton.Select();
            resolvedEventSystem.SetSelectedGameObject(playButton.gameObject);
            return;
        }

        if (quitButton != null)
        {
            Canvas.ForceUpdateCanvases();
            resolvedEventSystem.SetSelectedGameObject(null);
            quitButton.Select();
            resolvedEventSystem.SetSelectedGameObject(quitButton.gameObject);
        }
    }
    #endregion

    #endregion
}
