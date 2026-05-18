using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Handles world-space prompts and overlay interactions for dropped active power-up containers.
/// none.
/// </summary>
[System.Serializable]
public sealed class HUDPowerUpContainerInteractionSection
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Full-screen overlay root shown when Overlay Panel mode is opened from a dropped power-up container.")]
    [SerializeField] private GameObject overlayPanelRoot;

    [Tooltip("Optional title text updated with the dropped power-up display name inside the overlay panel.")]
    [SerializeField] private TMP_Text overlayTitleText;

    [Tooltip("Optional description text updated with the dropped power-up description inside the overlay panel.")]
    [SerializeField] private TMP_Text overlayDescriptionText;

    [Tooltip("Optional icon image updated with the dropped power-up sprite inside the overlay panel.")]
    [SerializeField] private Image overlayIconImage;

    [Tooltip("Button that swaps the dropped power-up into the primary active slot.")]
    [SerializeField] private Button replacePrimaryButton;

    [Tooltip("Optional label used to customize the primary-slot button text.")]
    [SerializeField] private TMP_Text replacePrimaryButtonText;

    [Tooltip("Button that swaps the dropped power-up into the secondary active slot.")]
    [SerializeField] private Button replaceSecondaryButton;

    [Tooltip("Optional label used to customize the secondary-slot button text.")]
    [SerializeField] private TMP_Text replaceSecondaryButtonText;
    #endregion

    private Button registeredPrimaryButton;
    private Button registeredSecondaryButton;
    private UnityEngine.Events.UnityAction registeredPrimaryButtonAction;
    private UnityEngine.Events.UnityAction registeredSecondaryButtonAction;
    private EntityManager entityManager;
    private Entity currentPlayerEntity;
    private Entity promptContainerEntity;
    private PlayerDroppedPowerUpContainerView promptContainerView;
    private Entity overlayContainerEntity;
    private PlayerDroppedPowerUpContainerView overlayContainerView;
    private bool overlayOpen;
    private bool overlayButtonsArmed;
    private bool isTimeScaleResuming;
    private float resumeStartTimeScale;
    private float resumeTargetTimeScale = 1f;
    private float resumeDurationSeconds;
    private float resumeElapsedSeconds;
    private HUDPowerUpContainerInteractionInputGate interactionInputGate;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers button listeners and applies the initial hidden state.
    /// none.
    /// </summary>
    public void Initialize()
    {
        interactionInputGate.Clear();
        CacheButtonTexts();
        RegisterButtons();
        HideOverlayImmediate();
    }

    /// <summary>
    /// Unregisters button listeners and restores a safe default Time.timeScale.
    /// none.
    /// </summary>
    public void Dispose()
    {
        UnregisterButtons();
        currentPlayerEntity = Entity.Null;
        HideTrackedPromptView();
        promptContainerEntity = Entity.Null;
        promptContainerView = null;
        overlayContainerEntity = Entity.Null;
        overlayContainerView = null;
        overlayOpen = false;
        overlayButtonsArmed = false;
        interactionInputGate.Clear();
        HUDPowerUpContainerTimeScaleUtility.StopResume(ref isTimeScaleResuming,
                                                       ref resumeStartTimeScale,
                                                       ref resumeTargetTimeScale,
                                                       ref resumeDurationSeconds,
                                                       ref resumeElapsedSeconds);
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Clears presentation state when no valid player entity is available.
    /// none.
    /// </summary>
    public void HandleMissingPlayer()
    {
        currentPlayerEntity = Entity.Null;
        HideTrackedPromptView();
        promptContainerEntity = Entity.Null;
        promptContainerView = null;
        HideOverlayImmediate();
        interactionInputGate.Clear();
        HUDPowerUpContainerTimeScaleUtility.StopResume(ref isTimeScaleResuming,
                                                       ref resumeStartTimeScale,
                                                       ref resumeTargetTimeScale,
                                                       ref resumeDurationSeconds,
                                                       ref resumeElapsedSeconds);
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Updates dropped-container prompts, overlay visibility, and swap command submission for the current player entity.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read and write runtime ECS state.</param>
    /// <param name="playerEntity">Current local player entity driving the HUD.</param>
    public void Update(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        entityManager = runtimeEntityManager;
        currentPlayerEntity = playerEntity;
        CacheButtonTexts();
        RegisterButtons();

        bool milestoneSelectionActive = IsMilestoneSelectionActive(playerEntity);
        HUDPowerUpContainerTimeScaleUtility.UpdateResume(ref isTimeScaleResuming,
                                                         ref resumeStartTimeScale,
                                                         ref resumeTargetTimeScale,
                                                         ref resumeDurationSeconds,
                                                         ref resumeElapsedSeconds,
                                                         milestoneSelectionActive);
        bool hardGameplayPauseActive = PlayerGameplayPauseUtility.IsHardGameplayPauseActive();
        interactionInputGate.SynchronizeHardPause(hardGameplayPauseActive);
        interactionInputGate.Refresh();

        if (!entityManager.Exists(playerEntity) ||
            !entityManager.HasComponent<PlayerPowerUpContainerInteractionConfig>(playerEntity) ||
            !entityManager.HasComponent<PlayerPowerUpContainerProximityState>(playerEntity))
        {
            HideTrackedPromptView();
            promptContainerEntity = Entity.Null;
            promptContainerView = null;

            if (overlayOpen)
                CloseOverlay(true);

            return;
        }

        if (overlayOpen)
        {
            HandleOverlayUpdate(milestoneSelectionActive);
            return;
        }

        if (milestoneSelectionActive)
        {
            HideTrackedPromptView();
            promptContainerEntity = Entity.Null;
            promptContainerView = null;
            return;
        }

        if (hardGameplayPauseActive)
        {
            HideTrackedPromptView();
            promptContainerEntity = Entity.Null;
            promptContainerView = null;
            return;
        }

        PlayerPowerUpContainerInteractionConfig interactionConfig = entityManager.GetComponentData<PlayerPowerUpContainerInteractionConfig>(playerEntity);
        PlayerPowerUpContainerProximityState proximityState = entityManager.GetComponentData<PlayerPowerUpContainerProximityState>(playerEntity);

        if (proximityState.HasContainerInRange == 0 ||
            !IsContainerUsable(proximityState.NearestContainerEntity) ||
            !TryResolveContainerView(proximityState.NearestContainerEntity, out PlayerDroppedPowerUpContainerView containerView))
        {
            HideTrackedPromptView();
            promptContainerEntity = Entity.Null;
            promptContainerView = null;
            return;
        }

        if (promptContainerEntity != proximityState.NearestContainerEntity)
        {
            HideTrackedPromptView();
            promptContainerEntity = proximityState.NearestContainerEntity;
            promptContainerView = containerView;
        }
        else
        {
            promptContainerView = containerView;
        }

        switch (interactionConfig.InteractionMode)
        {
            case PlayerPowerUpContainerInteractionMode.OverlayPanel:
                UpdateOverlayPrompt(promptContainerEntity, containerView);
                return;
            case PlayerPowerUpContainerInteractionMode.Prompt3D:
                UpdateDirectSwapPrompt(playerEntity, promptContainerEntity, containerView);
                return;
            default:
                containerView.HidePrompts();
                return;
        }
    }
    #endregion

    #region Setup
    /// <summary>
    /// Registers the two overlay buttons used to pick the active slot replacement target.
    /// none.
    /// </summary>
    private void RegisterButtons()
    {
        if (!ReferenceEquals(registeredPrimaryButton, replacePrimaryButton))
        {
            if (registeredPrimaryButton != null && registeredPrimaryButtonAction != null)
                registeredPrimaryButton.onClick.RemoveListener(registeredPrimaryButtonAction);

            registeredPrimaryButton = replacePrimaryButton;
            registeredPrimaryButtonAction = HandleReplacePrimaryButtonPressed;

            if (registeredPrimaryButton != null)
                registeredPrimaryButton.onClick.AddListener(registeredPrimaryButtonAction);
        }

        if (!ReferenceEquals(registeredSecondaryButton, replaceSecondaryButton))
        {
            if (registeredSecondaryButton != null && registeredSecondaryButtonAction != null)
                registeredSecondaryButton.onClick.RemoveListener(registeredSecondaryButtonAction);

            registeredSecondaryButton = replaceSecondaryButton;
            registeredSecondaryButtonAction = HandleReplaceSecondaryButtonPressed;

            if (registeredSecondaryButton != null)
                registeredSecondaryButton.onClick.AddListener(registeredSecondaryButtonAction);
        }
    }

    /// <summary>
    /// Removes the listeners registered on the overlay action buttons.
    /// none.
    /// </summary>
    private void UnregisterButtons()
    {
        if (registeredPrimaryButton != null && registeredPrimaryButtonAction != null)
            registeredPrimaryButton.onClick.RemoveListener(registeredPrimaryButtonAction);

        if (registeredSecondaryButton != null && registeredSecondaryButtonAction != null)
            registeredSecondaryButton.onClick.RemoveListener(registeredSecondaryButtonAction);

        registeredPrimaryButton = null;
        registeredSecondaryButton = null;
        registeredPrimaryButtonAction = null;
        registeredSecondaryButtonAction = null;
    }

    /// <summary>
    /// Auto-resolves button labels from the assigned button hierarchy when explicit references are missing.
    /// none.
    /// </summary>
    private void CacheButtonTexts()
    {
        if (replacePrimaryButtonText == null && replacePrimaryButton != null)
            replacePrimaryButtonText = replacePrimaryButton.GetComponentInChildren<TMP_Text>(true);

        if (replaceSecondaryButtonText == null && replaceSecondaryButton != null)
            replaceSecondaryButtonText = replaceSecondaryButton.GetComponentInChildren<TMP_Text>(true);
    }
    #endregion

    #region Update
    /// <summary>
    /// Updates overlay-mode prompt text and opens the full-screen panel on a fresh interaction press.
    /// </summary>
    /// <param name="playerEntity">Current player entity.</param>
    /// <param name="containerEntity">Nearest dropped container currently in range.</param>
    /// <param name="containerView">Companion view used to display the world-space prompt.</param>
    private void UpdateOverlayPrompt(Entity containerEntity, PlayerDroppedPowerUpContainerView containerView)
    {
        InputAction interactAction = PlayerInputRuntime.PowerUpContainerInteractAction;
        string bindingDisplayString = PlayerInputRuntime.ResolveBindingDisplayString(interactAction, "F");

        if (TryResolveVacantActiveSlot(out int vacantSlotIndex))
        {
            containerView.ShowSinglePrompt(string.Format("Press [{0}] to pick up", bindingDisplayString));

            if (interactAction == null)
                return;

            if (interactionInputGate.IsBlocked())
                return;

            if (!interactAction.WasPressedThisFrame())
                return;

            if (TryQueueSwapCommand(currentPlayerEntity, containerEntity, vacantSlotIndex))
                interactionInputGate.Begin(PlayerPowerUpContainerInteractionRuntimeUtility.ResolveInteractionLockDuration(entityManager, currentPlayerEntity));

            return;
        }

        containerView.ShowSinglePrompt(string.Format("Press [{0}] to swap", bindingDisplayString));

        if (interactAction == null)
            return;

        if (interactionInputGate.IsBlocked())
            return;

        if (!interactAction.WasPressedThisFrame())
            return;

        interactionInputGate.Begin(0f);
        OpenOverlay(containerEntity, containerView);
    }

    /// <summary>
    /// Updates 3D Prompt mode and queues an authoritative swap command when one direct-replacement action is pressed.
    /// </summary>
    /// <param name="playerEntity">Current player entity.</param>
    /// <param name="containerEntity">Nearest dropped container currently in range.</param>
    /// <param name="containerView">Companion view used to display the world-space prompt.</param>
    private void UpdateDirectSwapPrompt(Entity playerEntity, Entity containerEntity, PlayerDroppedPowerUpContainerView containerView)
    {
        InputAction replacePrimaryAction = PlayerInputRuntime.PowerUpContainerReplacePrimaryAction;
        InputAction replaceSecondaryAction = PlayerInputRuntime.PowerUpContainerReplaceSecondaryAction;
        InputAction interactAction = PlayerInputRuntime.PowerUpContainerInteractAction;
        string interactBindingDisplayString = PlayerInputRuntime.ResolveBindingDisplayString(interactAction, "F");
        string primaryBindingDisplayString = PlayerInputRuntime.ResolveBindingDisplayString(replacePrimaryAction, "1");
        string secondaryBindingDisplayString = PlayerInputRuntime.ResolveBindingDisplayString(replaceSecondaryAction, "2");

        if (TryResolveVacantActiveSlot(out int vacantSlotIndex))
        {
            containerView.ShowSinglePrompt(string.Format("Press [{0}] to pick up", interactBindingDisplayString));

            if (interactionInputGate.IsBlocked())
                return;

            bool interactPressed = interactAction != null && interactAction.WasPressedThisFrame();
            bool primaryPressed = replacePrimaryAction != null && replacePrimaryAction.WasPressedThisFrame();
            bool secondaryPressed = replaceSecondaryAction != null && replaceSecondaryAction.WasPressedThisFrame();

            if (!interactPressed && !primaryPressed && !secondaryPressed)
                return;

            if (TryQueueSwapCommand(playerEntity, containerEntity, vacantSlotIndex))
                interactionInputGate.Begin(PlayerPowerUpContainerInteractionRuntimeUtility.ResolveInteractionLockDuration(entityManager, currentPlayerEntity));

            return;
        }

        containerView.ShowSwapPrompt(string.Format("[{0}] Slot 1", primaryBindingDisplayString),
                                     string.Format("[{0}] Slot 2", secondaryBindingDisplayString));

        if (interactionInputGate.IsBlocked())
            return;

        if (replacePrimaryAction != null && replacePrimaryAction.WasPressedThisFrame())
        {
            if (TryQueueSwapCommand(playerEntity, containerEntity, 0))
                interactionInputGate.Begin(PlayerPowerUpContainerInteractionRuntimeUtility.ResolveInteractionLockDuration(entityManager, currentPlayerEntity));

            return;
        }

        if (replaceSecondaryAction != null && replaceSecondaryAction.WasPressedThisFrame())
        {
            if (TryQueueSwapCommand(playerEntity, containerEntity, 1))
                interactionInputGate.Begin(PlayerPowerUpContainerInteractionRuntimeUtility.ResolveInteractionLockDuration(entityManager, currentPlayerEntity));
        }
    }

    /// <summary>
    /// Updates the overlay state while it is open and closes it when canceled or invalidated.
    /// </summary>
    /// <param name="playerEntity">Current player entity.</param>
    /// <param name="milestoneSelectionActive">True when a milestone selection is currently open and must keep gameplay paused.</param>
    private void HandleOverlayUpdate(bool milestoneSelectionActive)
    {
        HideTrackedPromptView();

        if (!IsContainerUsable(overlayContainerEntity))
        {
            CloseOverlay(true);
            return;
        }

        UpdateOverlayContent(overlayContainerEntity);
        TryArmOverlayButtons();

        if (overlayButtonsArmed)
            HUDPowerUpContainerInteractionSelectionUtility.EnsureOverlaySelection(replacePrimaryButton, replaceSecondaryButton);

        if (milestoneSelectionActive)
            return;

        InputAction cancelAction = PlayerInputRuntime.UICancelAction;

        if (cancelAction != null && cancelAction.WasPressedThisFrame())
            CloseOverlay(true);
    }
    #endregion

    #region Overlay
    /// <summary>
    /// Opens the full-screen overlay for the specified dropped container and pauses gameplay immediately.
    /// </summary>
    /// <param name="containerEntity">Dropped container selected by the player.</param>
    private void OpenOverlay(Entity containerEntity, PlayerDroppedPowerUpContainerView containerView)
    {
        if (overlayPanelRoot == null)
            return;

        overlayContainerEntity = containerEntity;
        overlayContainerView = containerView;
        overlayOpen = true;
        overlayButtonsArmed = false;
        HUDPowerUpContainerTimeScaleUtility.StopResume(ref isTimeScaleResuming,
                                                       ref resumeStartTimeScale,
                                                       ref resumeTargetTimeScale,
                                                       ref resumeDurationSeconds,
                                                       ref resumeElapsedSeconds);
        HUDPowerUpContainerTimeScaleUtility.CancelMilestoneResume(entityManager, currentPlayerEntity);
        UpdateOverlayContent(containerEntity);

        if (!overlayPanelRoot.activeSelf)
            overlayPanelRoot.SetActive(true);

        Time.timeScale = 0f;
        HideTrackedPromptView();
        promptContainerEntity = containerEntity;
        promptContainerView = containerView;

        HUDPowerUpContainerInteractionSelectionUtility.SetOverlayButtonsInteractable(replacePrimaryButton, replaceSecondaryButton, false);
        HUDPowerUpContainerInteractionSelectionUtility.ClearOverlaySelection(replacePrimaryButton, replaceSecondaryButton);
    }

    /// <summary>
    /// Updates overlay labels with the current dropped power-up metadata.
    /// </summary>
    /// <param name="containerEntity">Dropped container currently shown by the overlay.</param>
    private void UpdateOverlayContent(Entity containerEntity)
    {
        if (!entityManager.Exists(containerEntity) || !entityManager.HasComponent<PlayerDroppedPowerUpContainerContent>(containerEntity))
            return;

        PlayerDroppedPowerUpContainerContent containerContent = entityManager.GetComponentData<PlayerDroppedPowerUpContainerContent>(containerEntity);
        string powerUpId = containerContent.StoredPowerUp.SlotConfig.PowerUpId.ToString();
        string title = PlayerPowerUpPresentationRuntime.ResolveDisplayName(powerUpId, powerUpId);
        string description = string.Empty;

        if (PlayerPowerUpPresentationRuntime.TryResolveEntry(powerUpId, out PlayerPowerUpPresentationRuntime.PowerUpPresentationEntry presentationEntry))
            description = presentationEntry.Description;

        if (overlayTitleText != null)
            overlayTitleText.text = title;

        if (overlayDescriptionText != null)
            overlayDescriptionText.text = string.IsNullOrWhiteSpace(description) ? "Choose which active slot to replace." : description;

        if (overlayIconImage != null)
        {
            if (PlayerPowerUpPresentationRuntime.TryResolveIcon(powerUpId, out Sprite icon))
            {
                overlayIconImage.sprite = icon;
                overlayIconImage.enabled = true;
            }
            else
            {
                overlayIconImage.sprite = null;
                overlayIconImage.enabled = false;
            }
        }

        if (replacePrimaryButtonText != null)
            replacePrimaryButtonText.text = "Replace Slot 1";

        if (replaceSecondaryButtonText != null)
            replaceSecondaryButtonText.text = "Replace Slot 2";
    }

    /// <summary>
    /// Closes the overlay and starts the configured Time.timeScale resume.
    /// </summary>
    /// <param name="resumeTimeScale">True to restore Time.timeScale using the configured duration; false to restore it immediately.</param>
    private void CloseOverlay(bool resumeTimeScale)
    {
        if (!overlayOpen)
            return;

        Entity containerEntity = overlayContainerEntity;
        PlayerDroppedPowerUpContainerView closedOverlayContainerView = overlayContainerView;
        overlayOpen = false;
        overlayButtonsArmed = false;
        overlayContainerEntity = Entity.Null;
        overlayContainerView = null;
        HUDPowerUpContainerInteractionSelectionUtility.SetOverlayButtonsInteractable(replacePrimaryButton, replaceSecondaryButton, false);
        HUDPowerUpContainerInteractionSelectionUtility.ClearOverlaySelection(replacePrimaryButton, replaceSecondaryButton);

        if (overlayPanelRoot != null && overlayPanelRoot.activeSelf)
            overlayPanelRoot.SetActive(false);

        if (resumeTimeScale)
            HUDPowerUpContainerTimeScaleUtility.BeginResume(entityManager,
                                                           currentPlayerEntity,
                                                           ref isTimeScaleResuming,
                                                           ref resumeStartTimeScale,
                                                           ref resumeTargetTimeScale,
                                                           ref resumeDurationSeconds,
                                                           ref resumeElapsedSeconds);
        else
            Time.timeScale = 1f;

        if (closedOverlayContainerView != null)
            closedOverlayContainerView.HidePrompts();

        if (containerEntity == promptContainerEntity)
            HideTrackedPromptView();
    }

    /// <summary>
    /// Immediately hides the overlay panel without creating a Time.timeScale resume.
    /// none.
    /// </summary>
    private void HideOverlayImmediate()
    {
        overlayOpen = false;
        overlayButtonsArmed = false;
        overlayContainerEntity = Entity.Null;
        HUDPowerUpContainerInteractionSelectionUtility.SetOverlayButtonsInteractable(replacePrimaryButton, replaceSecondaryButton, false);
        HUDPowerUpContainerInteractionSelectionUtility.ClearOverlaySelection(replacePrimaryButton, replaceSecondaryButton);

        if (overlayContainerView != null)
            overlayContainerView.HidePrompts();

        overlayContainerView = null;

        if (overlayPanelRoot != null && overlayPanelRoot.activeSelf)
            overlayPanelRoot.SetActive(false);
    }

    #endregion

    #region Commands
    /// <summary>
    /// Handles the overlay primary-slot button press by queuing one authoritative swap command.
    /// none.
    /// </summary>
    private void HandleReplacePrimaryButtonPressed()
    {
        TryQueueOverlaySwapCommand(0);
    }

    /// <summary>
    /// Handles the overlay secondary-slot button press by queuing one authoritative swap command.
    /// none.
    /// </summary>
    private void HandleReplaceSecondaryButtonPressed()
    {
        TryQueueOverlaySwapCommand(1);
    }

    /// <summary>
    /// Queues one authoritative swap command from the currently open overlay and closes it afterward.
    /// </summary>
    /// <param name="targetSlotIndex">Selected active-slot index. 0 is primary and 1 is secondary.</param>
    private void TryQueueOverlaySwapCommand(int targetSlotIndex)
    {
        if (!overlayOpen)
            return;

        if (!overlayButtonsArmed)
            return;

        if (currentPlayerEntity == Entity.Null)
            return;

        if (!TryQueueSwapCommand(currentPlayerEntity, overlayContainerEntity, targetSlotIndex))
            return;

        interactionInputGate.Begin(PlayerPowerUpContainerInteractionRuntimeUtility.ResolveInteractionLockDuration(entityManager, currentPlayerEntity));
        CloseOverlay(true);
    }

    /// <summary>
    /// Queues one authoritative dropped-container swap command on the player entity buffer.
    /// </summary>
    /// <param name="playerEntity">Player entity receiving the command.</param>
    /// <param name="containerEntity">Dropped container targeted by the swap.</param>
    /// <param name="targetSlotIndex">Selected active-slot index. 0 is primary and 1 is secondary.</param>
    /// <returns>True when the command was queued; otherwise false.</returns>
    private bool TryQueueSwapCommand(Entity playerEntity, Entity containerEntity, int targetSlotIndex)
    {
        if (!entityManager.Exists(playerEntity) ||
            !entityManager.HasBuffer<PlayerPowerUpContainerSwapCommand>(playerEntity) ||
            !IsContainerUsable(containerEntity))
        {
            return false;
        }

        DynamicBuffer<PlayerPowerUpContainerSwapCommand> swapCommands = entityManager.GetBuffer<PlayerPowerUpContainerSwapCommand>(playerEntity);
        swapCommands.Add(new PlayerPowerUpContainerSwapCommand
        {
            ContainerEntity = containerEntity,
            TargetSlotIndex = targetSlotIndex
        });
        HideTrackedPromptView();
        return true;
    }

    #endregion

    #region Interaction Input Gate
    /// <summary>
    /// Enables overlay buttons only after the opening input is released, preventing one gamepad press from opening and confirming the overlay.
    /// none.
    /// </summary>
    private void TryArmOverlayButtons()
    {
        if (overlayButtonsArmed)
            return;

        if (!HUDPowerUpContainerInteractionInputGate.AreActionsReleased())
            return;

        overlayButtonsArmed = true;
        HUDPowerUpContainerInteractionSelectionUtility.SetOverlayButtonsInteractable(replacePrimaryButton, replaceSecondaryButton, true);
        HUDPowerUpContainerInteractionSelectionUtility.SelectFirstOverlayButton(replacePrimaryButton, replaceSecondaryButton);
    }
    #endregion

    #region Time Scale
    #endregion

    #region Helpers
    /// <summary>
    /// Returns whether milestone selection is currently active on the player entity.
    /// </summary>
    /// <param name="playerEntity">Player entity inspected for milestone selection state.</param>
    /// <returns>True when milestone selection is active; otherwise false.</returns>
    private bool IsMilestoneSelectionActive(Entity playerEntity)
    {
        if (!entityManager.Exists(playerEntity) || !entityManager.HasComponent<PlayerMilestonePowerUpSelectionState>(playerEntity))
            return false;

        return entityManager.GetComponentData<PlayerMilestonePowerUpSelectionState>(playerEntity).IsSelectionActive != 0;
    }

    /// <summary>
    /// Returns whether the target dropped container entity still exists and stores one valid power-up payload.
    /// </summary>
    /// <param name="containerEntity">Dropped container entity inspected for usability.</param>
    /// <returns>True when the container can still be interacted with; otherwise false.</returns>
    private bool IsContainerUsable(Entity containerEntity)
    {
        if (containerEntity == Entity.Null || !entityManager.Exists(containerEntity))
            return false;

        if (!entityManager.HasComponent<PlayerDroppedPowerUpContainerContent>(containerEntity))
            return false;

        PlayerDroppedPowerUpContainerContent containerContent = entityManager.GetComponentData<PlayerDroppedPowerUpContainerContent>(containerEntity);
        return containerContent.StoredPowerUp.SlotConfig.IsDefined != 0;
    }

    /// <summary>
    /// Resolves the first empty active power-up slot on the current player.
    /// </summary>
    /// <param name="vacantSlotIndex">Resolved vacant slot index, where 0 is primary and 1 is secondary.</param>
    /// <returns>True when the player currently has an empty active slot.</returns>
    private bool TryResolveVacantActiveSlot(out int vacantSlotIndex)
    {
        vacantSlotIndex = -1;

        if (currentPlayerEntity == Entity.Null || !entityManager.Exists(currentPlayerEntity))
            return false;

        if (!entityManager.HasComponent<PlayerPowerUpsConfig>(currentPlayerEntity))
            return false;

        PlayerPowerUpsConfig powerUpsConfig = entityManager.GetComponentData<PlayerPowerUpsConfig>(currentPlayerEntity);

        if (powerUpsConfig.PrimarySlot.IsDefined == 0)
        {
            vacantSlotIndex = 0;
            return true;
        }

        if (powerUpsConfig.SecondarySlot.IsDefined == 0)
        {
            vacantSlotIndex = 1;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the companion view attached to one dropped container entity.
    /// </summary>
    /// <param name="containerEntity">Dropped container entity inspected for a companion view.</param>
    /// <param name="containerView">Resolved companion view when available.</param>
    /// <returns>True when the view exists; otherwise false.</returns>
    private bool TryResolveContainerView(Entity containerEntity, out PlayerDroppedPowerUpContainerView containerView)
    {
        containerView = null;

        if (containerEntity == Entity.Null)
            return false;

        return PlayerDroppedPowerUpContainerViewRuntimeUtility.TryResolveRuntimeView(entityManager,
                                                                                     containerEntity,
                                                                                     out containerView);
    }

    /// <summary>
    /// Hides the currently tracked prompt view without touching ECS state, allowing safe teardown after the world is destroyed.
    /// none.
    /// </summary>
    private void HideTrackedPromptView()
    {
        if (promptContainerView != null)
            promptContainerView.HidePrompts();
    }

    #endregion

    #endregion
}
