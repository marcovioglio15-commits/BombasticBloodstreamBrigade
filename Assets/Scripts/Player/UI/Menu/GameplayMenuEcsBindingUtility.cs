using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Provides ECS binding and milestone-gate helpers for the gameplay pause menu controller.
/// </summary>
internal static class GameplayMenuEcsBindingUtility
{
    #region Methods

    #region ECS Binding
    /// <summary>
    /// Initializes cached world, entity manager, and player query state for gameplay menu reads.
    /// </summary>
    /// <param name="defaultWorld">Cached default world reference updated when Unity recreates the ECS world.</param>
    /// <param name="entityManager">Entity manager resolved from the current default world.</param>
    /// <param name="playerQuery">Query selecting the single local player used by gameplay menus.</param>
    /// <param name="playerQueryInitialized">True when the cached player query is valid for the current world.</param>
    /// <param name="cachedPlayerEntity">Cached player entity cleared when the world changes or disappears.</param>
    /// <returns>True when ECS bindings are ready for menu reads; otherwise false.</returns>
    public static bool TryInitializeBindings(ref World defaultWorld,
                                             ref EntityManager entityManager,
                                             ref EntityQuery playerQuery,
                                             ref bool playerQueryInitialized,
                                             ref Entity cachedPlayerEntity)
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
    /// </summary>
    /// <param name="entityManager">Entity manager used to validate cached player entity state.</param>
    /// <param name="playerQuery">Query selecting player entities compatible with gameplay menu reads.</param>
    /// <param name="cachedPlayerEntity">Cached player entity reused while it remains valid.</param>
    /// <param name="playerEntity">Resolved player entity when exactly one valid player exists.</param>
    /// <returns>True when exactly one valid player entity exists; otherwise false.</returns>
    public static bool TryResolvePlayerEntity(EntityManager entityManager,
                                              EntityQuery playerQuery,
                                              ref Entity cachedPlayerEntity,
                                              out Entity playerEntity)
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

    /// <summary>
    /// Resolves whether one player entity currently owns an active milestone selection.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read milestone selection state.</param>
    /// <param name="playerEntity">Player entity inspected for milestone selection state.</param>
    /// <returns>True when the milestone selection menu must exclusively own gameplay UI input.</returns>
    public static bool IsMilestoneSelectionActive(EntityManager entityManager, Entity playerEntity)
    {
        if (!entityManager.Exists(playerEntity))
            return false;

        if (!entityManager.HasComponent<PlayerMilestonePowerUpSelectionState>(playerEntity))
            return false;

        PlayerMilestonePowerUpSelectionState selectionState = entityManager.GetComponentData<PlayerMilestonePowerUpSelectionState>(playerEntity);
        return selectionState.IsSelectionActive != 0;
    }
    #endregion

    #region Pause UI
    /// <summary>
    /// Sets interactability on every authored pause-menu button while another overlay owns input.
    /// </summary>
    /// <param name="resumeButton">Button that resumes gameplay from the pause menu.</param>
    /// <param name="pauseSettingsButton">Button that opens the settings overlay from pause.</param>
    /// <param name="pauseRestartButton">Button that restarts the active gameplay scene.</param>
    /// <param name="pauseMainMenuButton">Button that returns to the main menu scene.</param>
    /// <param name="pauseQuitButton">Button that exits the application.</param>
    /// <param name="interactable">True to enable pause-menu buttons, false to suspend them.</param>
    public static void SetPauseButtonsInteractable(Button resumeButton,
                                                   Button pauseSettingsButton,
                                                   Button pauseRestartButton,
                                                   Button pauseMainMenuButton,
                                                   Button pauseQuitButton,
                                                   bool interactable)
    {
        if (resumeButton != null)
            resumeButton.interactable = interactable;

        if (pauseSettingsButton != null)
            pauseSettingsButton.interactable = interactable;

        if (pauseRestartButton != null)
            pauseRestartButton.interactable = interactable;

        if (pauseMainMenuButton != null)
            pauseMainMenuButton.interactable = interactable;

        if (pauseQuitButton != null)
            pauseQuitButton.interactable = interactable;
    }

    /// <summary>
    /// Sets every pause and ending command to one consistent interaction state during terminal scene flow.
    /// </summary>
    /// <param name="resumeButton">Button that resumes gameplay from the pause menu.</param>
    /// <param name="pauseSettingsButton">Button that opens settings from pause.</param>
    /// <param name="pauseRestartButton">Button that restarts from pause.</param>
    /// <param name="pauseMainMenuButton">Button that returns to the main menu from pause.</param>
    /// <param name="pauseQuitButton">Button that exits from pause.</param>
    /// <param name="endingPlayAgainButton">Button that restarts from the ending menu.</param>
    /// <param name="endingMainMenuButton">Button that returns to the main menu from the ending menu.</param>
    /// <param name="endingQuitButton">Button that exits from the ending menu.</param>
    /// <param name="interactable">True while commands may be submitted; false after one terminal action is accepted.</param>
    public static void SetTerminalButtonsInteractable(Button resumeButton,
                                                      Button pauseSettingsButton,
                                                      Button pauseRestartButton,
                                                      Button pauseMainMenuButton,
                                                      Button pauseQuitButton,
                                                      Button endingPlayAgainButton,
                                                      Button endingMainMenuButton,
                                                      Button endingQuitButton,
                                                      bool interactable)
    {
        SetPauseButtonsInteractable(resumeButton,
                                    pauseSettingsButton,
                                    pauseRestartButton,
                                    pauseMainMenuButton,
                                    pauseQuitButton,
                                    interactable);

        if (endingPlayAgainButton != null)
            endingPlayAgainButton.interactable = interactable;

        if (endingMainMenuButton != null)
            endingMainMenuButton.interactable = interactable;

        if (endingQuitButton != null)
            endingQuitButton.interactable = interactable;
    }

    /// <summary>
    /// Commits one terminal menu command, disables every related button, and clears navigation focus before
    /// asynchronous scene work begins.
    /// </summary>
    /// <param name="terminalCommandSubmitted">Mutable gate set after the first accepted terminal action.</param>
    /// <param name="settingsMenuVisible">Mutable Settings overlay flag cleared when terminal flow takes ownership.</param>
    /// <param name="selectionController">Optional selection controller disabled while the command is pending.</param>
    /// <param name="eventSystemOverride">Optional EventSystem used to clear current focus.</param>
    /// <param name="resumeButton">Button that resumes gameplay from the pause menu.</param>
    /// <param name="pauseSettingsButton">Button that opens settings from pause.</param>
    /// <param name="pauseRestartButton">Button that restarts from pause.</param>
    /// <param name="pauseMainMenuButton">Button that returns to the main menu from pause.</param>
    /// <param name="pauseQuitButton">Button that exits from pause.</param>
    /// <param name="endingPlayAgainButton">Button that restarts from the ending menu.</param>
    /// <param name="endingMainMenuButton">Button that returns to the main menu from the ending menu.</param>
    /// <param name="endingQuitButton">Button that exits from the ending menu.</param>
    public static void LockTerminalCommands(ref bool terminalCommandSubmitted,
                                            ref bool settingsMenuVisible,
                                            MenuSelectionController selectionController,
                                            EventSystem eventSystemOverride,
                                            Button resumeButton,
                                            Button pauseSettingsButton,
                                            Button pauseRestartButton,
                                            Button pauseMainMenuButton,
                                            Button pauseQuitButton,
                                            Button endingPlayAgainButton,
                                            Button endingMainMenuButton,
                                            Button endingQuitButton)
    {
        terminalCommandSubmitted = true;
        settingsMenuVisible = false;
        SetTerminalButtonsInteractable(resumeButton,
                                       pauseSettingsButton,
                                       pauseRestartButton,
                                       pauseMainMenuButton,
                                       pauseQuitButton,
                                       endingPlayAgainButton,
                                       endingMainMenuButton,
                                       endingQuitButton,
                                       false);

        if (selectionController != null)
            selectionController.enabled = false;

        EventSystem resolvedEventSystem = eventSystemOverride != null
            ? eventSystemOverride
            : EventSystem.current;

        if (resolvedEventSystem != null)
            resolvedEventSystem.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Selects the first authored button available in the requested order through the local selection controller or
    /// the active EventSystem fallback.
    /// </summary>
    /// <param name="selectionController">Optional menu selection helper.</param>
    /// <param name="eventSystemOverride">Optional authored EventSystem override.</param>
    /// <param name="preferredButtons">Ordered button candidates for UI selection.</param>
    public static void SelectDefaultButton(MenuSelectionController selectionController,
                                           EventSystem eventSystemOverride,
                                           params Button[] preferredButtons)
    {
        if (selectionController != null)
        {
            // Select the first authored candidate through the controller so its fallback state stays synchronized.
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

        // Apply direct EventSystem selection only when no shared selection controller is present.
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

    /// <summary>
    /// Closes pause-owned UI immediately when milestone selection owns the gameplay overlay.
    /// </summary>
    /// <param name="pauseMenuRoot">Root object of the authored pause menu panel.</param>
    /// <param name="settingsMenu">Settings menu that may be opened from the pause panel.</param>
    /// <param name="selectionController">Selection controller re-enabled after suppressing pause UI.</param>
    /// <param name="resumeButton">Button that resumes gameplay from the pause menu.</param>
    /// <param name="pauseSettingsButton">Button that opens the settings overlay from pause.</param>
    /// <param name="pauseRestartButton">Button that restarts the active gameplay scene.</param>
    /// <param name="pauseMainMenuButton">Button that returns to the main menu scene.</param>
    /// <param name="pauseQuitButton">Button that exits the application.</param>
    /// <param name="pauseMenuVisible">Pause menu visibility flag updated after suppression.</param>
    /// <param name="settingsMenuVisible">Settings menu visibility flag updated after suppression.</param>
    public static void SuppressPauseMenuForMilestoneSelection(GameObject pauseMenuRoot,
                                                              SettingsMenuController settingsMenu,
                                                              MenuSelectionController selectionController,
                                                              Button resumeButton,
                                                              Button pauseSettingsButton,
                                                              Button pauseRestartButton,
                                                              Button pauseMainMenuButton,
                                                              Button pauseQuitButton,
                                                              ref bool pauseMenuVisible,
                                                              ref bool settingsMenuVisible)
    {
        bool hadPauseUiOpen = pauseMenuVisible || settingsMenuVisible || pauseMenuRoot != null && pauseMenuRoot.activeSelf;
        pauseMenuVisible = false;

        if (pauseMenuRoot != null && pauseMenuRoot.activeSelf)
            pauseMenuRoot.SetActive(false);

        if (settingsMenuVisible)
        {
            settingsMenuVisible = false;

            if (settingsMenu != null)
                settingsMenu.CancelAndClose();
        }

        SetPauseButtonsInteractable(resumeButton,
                                    pauseSettingsButton,
                                    pauseRestartButton,
                                    pauseMainMenuButton,
                                    pauseQuitButton,
                                    true);

        if (selectionController != null)
            selectionController.enabled = true;

        if (!hadPauseUiOpen)
            return;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    #endregion

    #endregion
}
